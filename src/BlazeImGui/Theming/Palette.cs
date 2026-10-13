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
            Knob = L(a.Knob, b.Knob, t),
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
        Background = Hex("#111216"),
        Sidebar = Hex("#0C0D10"),
        Surface = Hex("#17181D"),
        Elevated = Hex("#1B1C22"),
        Field = Hex("#22242B"),
        FieldHover = Hex("#2A2C34"),
        FieldActive = Hex("#31343D"),
        Raised = Hex("#383B45"),
        Border = Hex("#FFFFFF", .07f),
        Text = Hex("#ECEDF0"),
        TextMuted = Hex("#959AA4"),
        TextFaint = Hex("#62666F"),
        Accent = Hex("#6D8CFF"),
        AccentHover = Hex("#8199FF"),
        OnAccent = Hex("#FFFFFF"),
        Knob = Hex("#FFFFFF"),
        Success = Hex("#3DD68C"),
        Warning = Hex("#F5B74F"),
        Danger = Hex("#FF6B6B"),
    };

    public static Palette DefaultLight => new()
    {
        Background = Hex("#FFFFFF"),
        Sidebar = Hex("#F6F7F9"),
        Surface = Hex("#F7F8FA"),
        Elevated = Hex("#FFFFFF"),
        Field = Hex("#EDEFF3"),
        FieldHover = Hex("#E4E7EC"),
        FieldActive = Hex("#DADEE5"),
        Raised = Hex("#FFFFFF"),
        Border = Hex("#0B0D12", .09f),
        Text = Hex("#15171C"),
        TextMuted = Hex("#646A75"),
        TextFaint = Hex("#9AA0AA"),
        Accent = Hex("#4F6EF7"),
        AccentHover = Hex("#4262EE"),
        OnAccent = Hex("#FFFFFF"),
        Knob = Hex("#FFFFFF"),
        Success = Hex("#16A36A"),
        Warning = Hex("#D9891A"),
        Danger = Hex("#E5484D"),
    };
}
