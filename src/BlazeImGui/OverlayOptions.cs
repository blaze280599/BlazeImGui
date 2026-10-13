using System.Numerics;

namespace BlazeImGui;

/// <summary>Everything configurable about a <see cref="BlazeOverlay"/>. All properties have sensible defaults.</summary>
public sealed class OverlayOptions
{
    /// <summary>Window title (not shown) and the brand name in the sidebar.</summary>
    public string Title { get; init; } = "Blaze Overlay";
    /// <summary>Small line under the brand name, e.g. a version ("v1.0.0").</summary>
    public string? Subtitle { get; init; }
    /// <summary>Brand icon, an <see cref="Icons"/> constant.</summary>
    public string Logo { get; init; } = Icons.Flame;

    /// <summary>Virtual-key that shows/hides the menu. Default Insert.</summary>
    public int MenuKey { get; init; } = (int)VirtualKey.Insert;
    /// <summary>Virtual-key that closes the overlay. Default End. 0 disables it.</summary>
    public int ExitKey { get; init; } = (int)VirtualKey.End;
    /// <summary>
    /// When hotkeys may fire. Default: always, or only while the <see cref="Follow"/> target or the overlay is
    /// focused when a target is set. Hotkeys never fire while typing in a text field or binding a key.
    /// </summary>
    public Func<bool>? HotkeyGate { get; init; }

    /// <summary>Show the menu on startup.</summary>
    public bool StartVisible { get; init; } = true;
    public ThemeMode Theme { get; init; } = ThemeMode.Dark;
    /// <summary>Brand color for both themes. Null keeps the default indigo.</summary>
    public Vector4? Accent { get; init; }
    /// <summary>Body text size in pixels (before <see cref="UiScale"/>).</summary>
    public float FontSize { get; init; } = 15f;
    /// <summary>Scales fonts, spacing and widgets, e.g. 1.25 on high-DPI screens.</summary>
    public float UiScale { get; init; } = 1f;
    /// <summary>Menu window size before scale.</summary>
    public Vector2 MenuSize { get; init; } = new(820, 540);

    /// <summary>Keep the overlay over this window's client area (e.g. a game). Null covers the primary screen.</summary>
    public WindowTarget? Follow { get; init; }
    /// <summary>Checked every frame; the overlay closes when it returns true (e.g. the game exited).</summary>
    public Func<bool>? ExitWhen { get; init; }

    /// <summary>Present on every vertical blank. When false, <see cref="FpsLimit"/> applies.</summary>
    public bool VSync { get; init; } = true;
    public int FpsLimit { get; init; } = 144;
    /// <summary>Keep the overlay off the taskbar and Alt+Tab.</summary>
    public bool HideFromTaskbar { get; init; } = true;
}
