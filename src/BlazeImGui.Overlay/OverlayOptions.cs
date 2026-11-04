using System.Numerics;

namespace BlazeImGui;

/// <summary>Everything configurable about a <see cref="BlazeOverlay"/>. All properties have sensible defaults.</summary>
public sealed class OverlayOptions
{
    /// <summary>Window title (not shown) and the brand name at the bottom of the navigation column.</summary>
    public string Title { get; init; } = "Blaze Overlay";
    /// <summary>Small muted word after the brand name, e.g. the game ("osu!").</summary>
    public string? Subtitle { get; init; }
    /// <summary>Version after the subtitle ("v1.0.0").</summary>
    public string? Version { get; init; }
    /// <summary>
    /// The brand mark at the top of the icon rail, drawn in bold with a <see cref="Palette.Brand"/> dot.
    /// Null uses the first letter of <see cref="Title"/>; an <see cref="Icons"/> constant works too.
    /// </summary>
    public string? Logo { get; init; }

    /// <summary>Virtual-key that shows/hides the menu. Default Insert.</summary>
    public int MenuKey { get; init; } = (int)VirtualKey.Insert;
    /// <summary>Virtual-key that closes the overlay. Default End. 0 disables it.</summary>
    public int ExitKey { get; init; } = (int)VirtualKey.End;
    /// <summary>
    /// When hotkeys may fire. Default: always, or only while the <see cref="Follow"/> target or the overlay is
    /// focused when a target is set and running. Hotkeys never fire while typing in a text field or binding a key.
    /// </summary>
    public Func<bool>? HotkeyGate { get; init; }

    /// <summary>Show the menu on startup.</summary>
    public bool StartVisible { get; init; } = true;
    public ThemeMode Theme { get; init; } = ThemeMode.Light;
    /// <summary>Selection / fill color for both themes. Null keeps the default ink (near-black, near-white in dark).</summary>
    public Vector4? Accent { get; init; }
    /// <summary>Body text size in pixels (before <see cref="UiScale"/>).</summary>
    public float FontSize { get; init; } = 15f;
    /// <summary>Scales fonts, spacing and widgets, e.g. 1.25 on high-DPI screens.</summary>
    public float UiScale { get; init; } = 1f;
    /// <summary>Menu window size before scale.</summary>
    public Vector2 MenuSize { get; init; } = new(1000, 600);

    /// <summary>
    /// Keep the overlay over this window's client area (e.g. a game). Null covers the primary screen, and so does a
    /// target that isn't running yet (or has closed), so the menu can be used without it.
    /// </summary>
    public WindowTarget? Follow { get; init; }
    /// <summary>Checked every frame; the overlay closes when it returns true (e.g. the game exited).</summary>
    public Func<bool>? ExitWhen { get; init; }

    /// <summary>Present on every vertical blank. When false, <see cref="FpsLimit"/> applies.</summary>
    public bool VSync { get; init; } = true;
    public int FpsLimit { get; init; } = 144;
    /// <summary>Keep the overlay off the taskbar and Alt+Tab.</summary>
    public bool HideFromTaskbar { get; init; } = true;
}
