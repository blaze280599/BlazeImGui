using System.Numerics;

namespace BlazeImGui;

/// <summary>
/// The semantic colors of one theme. Widgets only ever read these tokens (never raw colors), so a theme
/// switch or a new accent restyles everything. Read the live, animated values from <see cref="Theme.Colors"/>.
/// </summary>
public sealed class Palette
{
    /// <summary>Menu window background.</summary>
    public Vector4 Background;
    /// <summary>Sidebar / secondary panels.</summary>
    public Vector4 Sidebar;
    /// <summary>Cards and grouped content sitting on the background.</summary>
    public Vector4 Surface;
    /// <summary>Popups, combo lists, tooltips, toasts.</summary>
    public Vector4 Elevated;
    /// <summary>Input fields, buttons and tracks at rest / hovered / pressed.</summary>
    public Vector4 Field, FieldHover, FieldActive;
    /// <summary>A control sitting on top of a field (segmented thumb, selected chip).</summary>
    public Vector4 Raised;
    /// <summary>Hairline borders and separators.</summary>
    public Vector4 Border;
    /// <summary>Primary text, secondary text, and the faintest text (placeholders, captions).</summary>
    public Vector4 Text, TextMuted, TextFaint;
    /// <summary>Brand color for selected/active state, and its hovered variant.</summary>
    public Vector4 Accent, AccentHover;
    /// <summary>Text/icons drawn on top of <see cref="Accent"/>.</summary>
    public Vector4 OnAccent;
    /// <summary>Knob color of toggles and sliders.</summary>
    public Vector4 Knob;
    /// <summary>The brand mark's dot: a small splash of color the rest of the menu stays neutral around.</summary>
    public Vector4 Brand;
    public Vector4 Success, Warning, Danger;

    public Palette Clone() => (Palette)MemberwiseClone();

    /// <summary>Replaces the accent (and derives its hover shade) on this palette.</summary>
    public Palette WithAccent(Vector4 accent, bool lightTheme)
    {
        var copy = Clone();
        copy.Accent = accent;
        copy.AccentHover = lightTheme ? Shade(accent, -.08f) : Shade(accent, .08f);
        return copy;
    }

    internal static Palette Lerp(Palette a, Palette b, float t)
    {
        static Vector4 L(Vector4 x, Vector4 y, float t) => x + (y - x) * t;
        return new Palette
        {
            Background = L(a.Background, b.Background, t), Sidebar = L(a.Sidebar, b.Sidebar, t),
            Surface = L(a.Surface, b.Surface, t), Elevated = L(a.Elevated, b.Elevated, t),
            Field = L(a.Field, b.Field, t), FieldHover = L(a.FieldHover, b.FieldHover, t), FieldActive = L(a.FieldActive, b.FieldActive, t),
            Border = L(a.Border, b.Border, t), Raised = L(a.Raised, b.Raised, t),
            Text = L(a.Text, b.Text, t), TextMuted = L(a.TextMuted, b.TextMuted, t), TextFaint = L(a.TextFaint, b.TextFaint, t),
            Accent = L(a.Accent, b.Accent, t), AccentHover = L(a.AccentHover, b.AccentHover, t), OnAccent = L(a.OnAccent, b.OnAccent, t),
            Knob = L(a.Knob, b.Knob, t), Brand = L(a.Brand, b.Brand, t),
            Success = L(a.Success, b.Success, t), Warning = L(a.Warning, b.Warning, t), Danger = L(a.Danger, b.Danger, t),
        };
    }

    /// <summary>"#RRGGBB" or "#RRGGBBAA" to a color.</summary>
    public static Vector4 Hex(string hex, float alpha = 1f)
    {
        hex = hex.TrimStart('#');
        float C(int i) => Convert.ToInt32(hex.Substring(i, 2), 16) / 255f;
        return new(C(0), C(2), C(4), hex.Length >= 8 ? C(6) : alpha);
    }

    /// <summary>Lightens (positive) or darkens (negative) a color by mixing toward white or black.</summary>
    public static Vector4 Shade(Vector4 c, float amount)
    {
        var target = amount >= 0 ? Vector4.One : new Vector4(0, 0, 0, 1);
        var mixed = c + (target - c) * MathF.Abs(amount);
        return mixed with { W = c.W };
    }

    /// <summary>The same color with a new alpha.</summary>
    public static Vector4 Alpha(Vector4 c, float alpha) => c with { W = alpha };

    public static Palette DefaultDark => new()
    {
        Background = Hex("#17171B"),
        Sidebar = Hex("#17171B"),
        Surface = Hex("#1D1D22"),
        Elevated = Hex("#202026"),
        Field = Hex("#2A2A31"),
        FieldHover = Hex("#313139"),
        FieldActive = Hex("#393941"),
        Raised = Hex("#33333B"),
        Border = Hex("#FFFFFF", .08f),
        Text = Hex("#E7E7EB"),
        TextMuted = Hex("#8C8C96"),
        TextFaint = Hex("#56565F"),
        Accent = Hex("#E7E7EB"),
        AccentHover = Hex("#FFFFFF"),
        OnAccent = Hex("#17171B"),
        Knob = Hex("#F2F2F5"),
        Brand = Hex("#FF5C9D"),
        Success = Hex("#3DD68C"),
        Warning = Hex("#F5A524"),
        Danger = Hex("#FF6B6B"),
    };

    public static Palette DefaultLight => new()
    {
        Background = Hex("#ECECF0"),
        Sidebar = Hex("#ECECF0"),
        Surface = Hex("#F3F3F6"),
        Elevated = Hex("#F7F7F9"),
        Field = Hex("#E1E1E6"),
        FieldHover = Hex("#D9D9DF"),
        FieldActive = Hex("#CFCFD6"),
        Raised = Hex("#F9F9FB"),
        Border = Hex("#0B0B12", .10f),
        Text = Hex("#2B2B30"),
        TextMuted = Hex("#8E8E98"),
        TextFaint = Hex("#B6B6BF"),
        Accent = Hex("#2B2B30"),
        AccentHover = Hex("#3B3B41"),
        OnAccent = Hex("#F3F3F6"),
        Knob = Hex("#F9F9FB"),
        Brand = Hex("#FF5C9D"),
        Success = Hex("#2EA44F"),
        Warning = Hex("#E8820C"),
        Danger = Hex("#E5484D"),
    };
}
