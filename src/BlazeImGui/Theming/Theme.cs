using System.Numerics;
using ImGuiNET;

namespace BlazeImGui;

public enum ThemeMode { Dark, Light }

/// <summary>
/// The active theme. <see cref="Set"/> or <see cref="Toggle"/> cross-fades every color over ~0.3 s;
/// <see cref="Colors"/> always holds the current (possibly mid-transition) palette for custom drawing.
/// </summary>
public static class Theme
{
    private static Palette _dark = Palette.DefaultDark;
    private static Palette _light = Palette.DefaultLight;
    private static Palette _from = _dark;
    private static float _progress = 1f;
    private const float TransitionSeconds = .32f;

    /// <summary>The selected mode (the target of any running transition).</summary>
    public static ThemeMode Mode { get; private set; } = ThemeMode.Dark;

    /// <summary>Current colors. Read every frame; do not cache across frames during a transition.</summary>
    public static Palette Colors { get; private set; } = _dark;

    /// <summary>Raised after the mode changes (e.g. to save the user's choice).</summary>
    public static event Action<ThemeMode>? Changed;

    public static bool IsDark => Mode == ThemeMode.Dark;

    /// <summary>Corner radii shared by every widget, after UI scale.</summary>
    public static float Rounding { get; private set; } = 8f;

    /// <summary>Switches theme. With <paramref name="animate"/> the colors cross-fade.</summary>
    public static void Set(ThemeMode mode, bool animate = true)
    {
        if (mode == Mode && _progress >= 1f) return;
        _from = Colors;
        Mode = mode;
        _progress = animate ? 0f : 1f;
        if (!animate)
        {
            Colors = Target;
            ApplyColors(Colors);
        }
        Changed?.Invoke(mode);
    }

    public static void Toggle() => Set(IsDark ? ThemeMode.Light : ThemeMode.Dark);

    /// <summary>Uses one accent color in both themes (the light theme gets a slightly deeper hover).</summary>
    public static void SetAccent(Vector4 accent, bool animate = true)
    {
        _from = Colors;
        _dark = _dark.WithAccent(accent, lightTheme: false);
        _light = _light.WithAccent(accent, lightTheme: true);
        _progress = animate ? 0f : 1f;
        if (!animate)
        {
            Colors = Target;
            ApplyColors(Colors);
        }
    }

    /// <summary>Replaces a whole palette, e.g. <c>Theme.Customize(ThemeMode.Dark, p => p.Background = Palette.Hex("#0E0E10"))</c>.</summary>
    public static void Customize(ThemeMode mode, Action<Palette> edit)
    {
        _from = Colors;
        var palette = (mode == ThemeMode.Dark ? _dark : _light).Clone();
        edit(palette);
        if (mode == ThemeMode.Dark) _dark = palette; else _light = palette;
        _progress = 0f;
    }

    private static Palette Target => Mode == ThemeMode.Dark ? _dark : _light;

    /// <summary>Packs a color for ImDrawList calls, multiplying in the current style alpha (so fades apply).</summary>
    public static uint U32(Vector4 color, float alpha = 1f)
        => ImGui.ColorConvertFloat4ToU32(color with { W = color.W * alpha * ImGui.GetStyle().Alpha });

    /// <summary>Applies sizes and rounding. Called once at startup by <see cref="BlazeOverlay"/>.</summary>
    public static void ApplyStyle(float scale = 1f)
    {
        var style = ImGui.GetStyle();
        style.WindowRounding = 12;
        style.ChildRounding = 10;
        style.FrameRounding = 7;
        style.PopupRounding = 9;
        style.GrabRounding = 7;
        style.TabRounding = 7;
        style.ScrollbarRounding = 9;
        style.WindowBorderSize = 1;
        style.ChildBorderSize = 1;
        style.PopupBorderSize = 1;
        style.FrameBorderSize = 0;
        style.TabBorderSize = 0;
        style.TabBarBorderSize = 1;
        style.TabBarOverlineSize = 2;
        style.WindowPadding = new(16, 14);
        style.FramePadding = new(10, 6);
        style.ItemSpacing = new(10, 8);
        style.ItemInnerSpacing = new(8, 6);
        style.CellPadding = new(10, 6);
        style.IndentSpacing = 18;
        style.ScrollbarSize = 10;
        style.GrabMinSize = 10;
        style.WindowMinSize = new(160, 80);
        style.SeparatorTextBorderSize = 1;
        style.SeparatorTextPadding = new(0, 6);
        style.WindowTitleAlign = new(0, .5f);
        style.ButtonTextAlign = new(.5f, .5f);
        style.HoverDelayShort = .25f;
        style.HoverDelayNormal = .45f;
        style.AntiAliasedFill = true;
        style.AntiAliasedLines = true;
        if (MathF.Abs(scale - 1f) > .001f) style.ScaleAllSizes(scale);
        Rounding = style.FrameRounding;
        ApplyColors(Colors);
    }

    /// <summary>Advances any running transition and writes the colors into ImGui. Called every frame.</summary>
    public static void Update()
    {
        if (_progress >= 1f) return;
        _progress = Math.Min(1f, _progress + Anim.Delta / TransitionSeconds);
        Colors = _progress >= 1f ? Target : Palette.Lerp(_from, Target, Anim.EaseInOutCubic(_progress));
        ApplyColors(Colors);
    }

    private static void ApplyColors(Palette p)
    {
        var c = ImGui.GetStyle().Colors;
        var none = Vector4.Zero;
        Vector4 A(Vector4 v, float a) => v with { W = v.W * a };

        c[(int)ImGuiCol.Text] = p.Text;
        c[(int)ImGuiCol.TextDisabled] = p.TextMuted;
        c[(int)ImGuiCol.WindowBg] = p.Background;
        c[(int)ImGuiCol.ChildBg] = none;
        c[(int)ImGuiCol.PopupBg] = p.Elevated;
        c[(int)ImGuiCol.Border] = p.Border;
        c[(int)ImGuiCol.BorderShadow] = none;
        c[(int)ImGuiCol.FrameBg] = p.Field;
        c[(int)ImGuiCol.FrameBgHovered] = p.FieldHover;
        c[(int)ImGuiCol.FrameBgActive] = p.FieldActive;
        c[(int)ImGuiCol.TitleBg] = p.Background;
        c[(int)ImGuiCol.TitleBgActive] = p.Background;
        c[(int)ImGuiCol.TitleBgCollapsed] = p.Background;
        c[(int)ImGuiCol.MenuBarBg] = p.Sidebar;
        c[(int)ImGuiCol.ScrollbarBg] = none;
        c[(int)ImGuiCol.ScrollbarGrab] = p.FieldActive;
        c[(int)ImGuiCol.ScrollbarGrabHovered] = A(p.TextFaint, .7f);
        c[(int)ImGuiCol.ScrollbarGrabActive] = p.TextFaint;
        c[(int)ImGuiCol.CheckMark] = p.Accent;
        c[(int)ImGuiCol.SliderGrab] = p.Accent;
        c[(int)ImGuiCol.SliderGrabActive] = p.AccentHover;
        c[(int)ImGuiCol.Button] = p.Field;
        c[(int)ImGuiCol.ButtonHovered] = p.FieldHover;
        c[(int)ImGuiCol.ButtonActive] = p.FieldActive;
        c[(int)ImGuiCol.Header] = A(p.Accent, .14f);
        c[(int)ImGuiCol.HeaderHovered] = p.FieldHover;
        c[(int)ImGuiCol.HeaderActive] = A(p.Accent, .22f);
        c[(int)ImGuiCol.Separator] = p.Border;
        c[(int)ImGuiCol.SeparatorHovered] = A(p.Accent, .6f);
        c[(int)ImGuiCol.SeparatorActive] = p.Accent;
        c[(int)ImGuiCol.ResizeGrip] = none;
        c[(int)ImGuiCol.ResizeGripHovered] = A(p.Accent, .35f);
        c[(int)ImGuiCol.ResizeGripActive] = A(p.Accent, .6f);
        c[(int)ImGuiCol.Tab] = none;
        c[(int)ImGuiCol.TabHovered] = p.FieldHover;
        c[(int)ImGuiCol.TabSelected] = p.Field;
        c[(int)ImGuiCol.TabSelectedOverline] = p.Accent;
        c[(int)ImGuiCol.TabDimmed] = none;
        c[(int)ImGuiCol.TabDimmedSelected] = p.Field;
        c[(int)ImGuiCol.TabDimmedSelectedOverline] = none;
        c[(int)ImGuiCol.PlotLines] = p.Accent;
        c[(int)ImGuiCol.PlotLinesHovered] = p.AccentHover;
        c[(int)ImGuiCol.PlotHistogram] = p.Accent;
        c[(int)ImGuiCol.PlotHistogramHovered] = p.AccentHover;
        c[(int)ImGuiCol.TableHeaderBg] = p.Surface;
        c[(int)ImGuiCol.TableBorderStrong] = p.Border;
        c[(int)ImGuiCol.TableBorderLight] = p.Border;
        c[(int)ImGuiCol.TableRowBg] = none;
        c[(int)ImGuiCol.TableRowBgAlt] = A(p.Text, .025f);
        c[(int)ImGuiCol.TextLink] = p.Accent;
        c[(int)ImGuiCol.TextSelectedBg] = A(p.Accent, .3f);
        c[(int)ImGuiCol.DragDropTarget] = p.Accent;
        c[(int)ImGuiCol.NavCursor] = A(p.Accent, .8f);
        c[(int)ImGuiCol.NavWindowingHighlight] = A(p.Text, .6f);
        c[(int)ImGuiCol.NavWindowingDimBg] = new(0, 0, 0, .3f);
        c[(int)ImGuiCol.ModalWindowDimBg] = new(0, 0, 0, IsDark ? .45f : .25f);
    }
}
