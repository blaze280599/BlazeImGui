using System.ComponentModel;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace BlazeImGui;

/// <summary>
/// Base class for a transparent, click-through ImGui overlay with a themed menu.
/// <para>Derive from it, add pages to <see cref="Menu"/> in the constructor, and <c>await overlay.Run()</c>:</para>
/// <code>
/// sealed class MyOverlay : BlazeOverlay
/// {
///     bool _enabled;
///     public MyOverlay() : base(new OverlayOptions { Title = "My Tool" })
///         => Menu.AddPage("General", Icons.House, () => Ui.Toggle("Enabled", ref _enabled));
/// }
/// </code>
/// Override <see cref="DrawOverlay"/> for drawings that stay visible while the menu is hidden (ESP, HUD),
/// <see cref="OnUpdate"/> for per-frame logic, and <see cref="DrawMenu"/> to replace the whole menu window.
/// </summary>
public abstract class BlazeOverlay : ClickableTransparentOverlay.Overlay
{
    private const uint MenuAnimationId = 0xB1A2_E001;
    private bool _menuVisible;
    private System.Drawing.Rectangle _area;

    protected BlazeOverlay(OverlayOptions? options = null) : base((options ??= new OverlayOptions()).Title, true)
    {
        Options = options;
        _menuVisible = options.StartVisible;
        Menu = new Menu { Title = options.Title, Subtitle = options.Subtitle, Logo = options.Logo, Size = options.MenuSize };
        Menu.CloseRequested += () => MenuVisible = false;
    }

    public OverlayOptions Options { get; }

    /// <summary>The sidebar menu. Add pages with <c>Menu.AddPage(title, icon, draw)</c>.</summary>
    public Menu Menu { get; }

    /// <summary>The overlay's window handle (valid after startup).</summary>
    public IntPtr Handle => window.Handle;

    /// <summary>Shows or hides the menu with a fade. The overlay keeps running either way.</summary>
    public bool MenuVisible
    {
        get => _menuVisible;
        set
        {
            if (_menuVisible == value) return;
            _menuVisible = value;
            MenuVisibilityChanged?.Invoke(value);
        }
    }

    /// <summary>The menu's current fade (0 hidden .. 1 shown).</summary>
    public float MenuAlpha { get; private set; }

    /// <summary>True while the <see cref="OverlayOptions.Follow"/> target is present and not minimized (always true without a target).</summary>
    public bool TargetVisible { get; private set; } = true;

    public event Action<bool>? MenuVisibilityChanged;

    /// <summary>
    /// Add extra fonts here with <see cref="Fonts.Add"/> (e.g. a larger label font for world drawings).
    /// Runs while the font atlas is built, after the built-in fonts.
    /// </summary>
    protected virtual void OnLoadFonts() { }

    /// <summary>Called once after the theme is applied, before the first frame.</summary>
    protected virtual void OnSetup() { }

    /// <summary>Per-frame logic that runs before any drawing, whether or not the menu is shown.</summary>
    protected virtual void OnUpdate() { }

    /// <summary>
    /// World/HUD drawing behind every ImGui window. Runs every frame, also while the menu is hidden.
    /// Coordinates are overlay-window pixels (the target's client area when following one).
    /// </summary>
    protected virtual void DrawOverlay(ImDrawListPtr drawList) { }

    /// <summary>Draws the menu. The default draws <see cref="Menu"/>; override to draw your own windows instead.</summary>
    protected virtual void DrawMenu() => Menu.Draw(MenuAlpha, MenuVisible);

    /// <summary>Called right before the overlay closes (exit hotkey, <see cref="Exit"/>, or ExitWhen).</summary>
    protected virtual void OnExit() { }

    /// <summary>Closes the overlay; <c>Run()</c> then returns.</summary>
    public void Exit()
    {
        OnExit();
        Close();
    }

    protected override Task PostInitialized()
    {
        if (Options.HideFromTaskbar) HideFromTaskbar(window.Handle);
        // The window starts at 800x600; without a target to follow it covers the whole primary monitor.
        if (Options.Follow is null)
        {
            Position = System.Drawing.Point.Empty;
            Size = new System.Drawing.Size(GetSystemMetrics(0), GetSystemMetrics(1));
        }
        VSync = Options.VSync;
        FPSLimit = Options.VSync ? 0 : Options.FpsLimit;
        Ui.Scale = Options.UiScale;
        unsafe
        {
            ReplaceFont(_ =>
            {
                Fonts.Load(Options.FontSize, Options.UiScale);
                OnLoadFonts();
            });
        }
        Theme.Set(Options.Theme, animate: false);
        if (Options.Accent is { } accent) Theme.SetAccent(accent, animate: false);
        Theme.ApplyStyle(Options.UiScale);
        OnSetup();
        return Task.CompletedTask;
    }

    protected sealed override void Render()
    {
        Input.Update();
        Theme.Update();
        if (Options.Follow is not null) FollowTarget();
        if (Options.ExitWhen?.Invoke() == true)
        {
            Exit();
            return;
        }
        if (HotkeysAllowed())
        {
            if (Input.Pressed(Options.MenuKey)) MenuVisible = !MenuVisible;
            if (Options.ExitKey != 0 && Input.Pressed(Options.ExitKey))
            {
                Exit();
                return;
            }
        }

        OnUpdate();
        DrawOverlay(ImGui.GetBackgroundDrawList());
        MenuAlpha = Anim.To(MenuAnimationId, MenuVisible ? 1f : 0f, MenuVisible ? 14f : 20f, 0f);
        if (MenuAlpha > .002f)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, MenuAlpha);
            DrawMenu();
            ImGui.PopStyleVar();
        }
        Notify.Draw();
    }

    private bool HotkeysAllowed()
    {
        if (Ui.IsCapturingKey || ImGui.GetIO().WantTextInput) return false;
        if (Options.HotkeyGate is { } gate) return gate();
        return Options.Follow is null || Options.Follow.IsForeground(window.Handle);
    }

    private void FollowTarget()
    {
        TargetVisible = Options.Follow!.TryGetClientArea(out var area);
        if (!TargetVisible || area == _area) return;
        _area = area;
        Position = area.Location;
        Size = area.Size;
    }

    private static void HideFromTaskbar(IntPtr handle)
    {
        const int extendedStyle = -20;
        const long toolWindow = 0x80, appWindow = 0x40000;
        var style = GetWindowLongPtr(handle, extendedStyle).ToInt64();
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(handle, extendedStyle, new IntPtr((style | toolWindow) & ~appWindow));
        if (previous == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not hide the overlay from the taskbar.");
    }

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value);
}
