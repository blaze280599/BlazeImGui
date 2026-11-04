using System.Globalization;
using System.Numerics;
using ImGuiNET;

namespace BlazeImGui;

/// <summary>Visual weight of <see cref="Ui.Button"/>.</summary>
public enum ButtonKind { Secondary, Primary, Ghost, Danger }

/// <summary>Semantic color for badges, toasts, stat deltas.</summary>
public enum Tone { Neutral, Accent, Success, Warning, Danger }

/// <summary>
/// Themed, animated widgets. Labeled controls stack: the label on its own line, the control under it at
/// <see cref="ControlWidth"/>. A description is shown under ABOUT in the menu's status column while the control is
/// hovered (or under the control when the menu has no status column). Labels follow ImGui rules: text after "##"
/// is hidden but keeps the ID unique ("Enabled##esp"). All widgets return true on the frame the value changes.
/// Plain ImGui widgets (Combo, InputText, Tables, Plot*, TreeNode, TabBar...) are themed too and can be mixed freely.
/// </summary>
public static partial class Ui
{
    /// <summary>UI scale; set from <c>OverlayOptions.UiScale</c>.</summary>
    public static float Scale { get; set; } = 1f;

    /// <summary>True while a <see cref="Keybind"/> waits for a key; the overlay ignores its hotkeys meanwhile.</summary>
    public static bool IsCapturingKey => _capturingKey != 0;

    /// <summary>The text under ABOUT in the status column: the last hovered control's description, or the tab's.</summary>
    public static string? CurrentAbout { get; set; }

    /// <summary>The name of the control <see cref="CurrentAbout"/> describes, shown above it; null for the tab's description.</summary>
    public static string? CurrentAboutTitle { get; set; }

    /// <summary>Draws descriptions under their controls instead of under ABOUT (set by the menu each frame).</summary>
    public static bool InlineDescriptions { get; set; }

    private static uint _capturingKey;

    /// <summary>Ends a pending keybind capture and clears the ABOUT text; call it when the host replaces its ImGui context.</summary>
    public static void ResetState()
    {
        _capturingKey = 0;
        CurrentAbout = null;
        CurrentAboutTitle = null;
        InlineDescriptions = false;
    }

    private static float S(float value) => value * Scale;
    private static Palette P => Theme.Colors;
    private static uint Col(Vector4 color, float alpha = 1f) => Theme.U32(color, alpha);

    // ---------------------------------------------------------------- toggles & checkboxes

    /// <summary>A checkbox row: the box and its label, the whole row clickable. The settings-style boolean.</summary>
    public static bool Toggle(string label, ref bool value, string? description = null)
    {
        ImGui.PushID(label);
        var width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetFrameHeight();
        var start = ImGui.GetCursorScreenPos();
        var changed = ImGui.InvisibleButton("##row", new Vector2(width, height));
        if (changed) value = !value;
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        DrawCheck(start, height, Visible(label), value, hovered);
        ImGui.PopID();
        About(description, label);
        Gap();
        return changed;
    }

    /// <summary>A compact checkbox sized to its label, for several on one line (<c>ImGui.SameLine()</c> between).</summary>
    public static bool Checkbox(string label, ref bool value)
    {
        ImGui.PushID(label);
        var text = Visible(label);
        var height = ImGui.GetFrameHeight();
        var width = CheckSize() + (text.Length > 0 ? S(10) + ImGui.CalcTextSize(text).X : 0);
        var start = ImGui.GetCursorScreenPos();
        var changed = ImGui.InvisibleButton("##box", new Vector2(width, height));
        if (changed) value = !value;
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        DrawCheck(start, height, text, value, hovered);
        ImGui.PopID();
        return changed;
    }

    private static float CheckSize() => MathF.Round(S(14));

    // A square box with a hairline border; when on, it fills with ink and a check draws itself in.
    private static void DrawCheck(Vector2 start, float height, string text, bool value, bool hovered)
    {
        var on = Anim.Toggle(ImGui.GetID("##on"), value, 20f);
        var hover = Anim.Toggle(ImGui.GetID("##hover"), hovered, 18f);
        var draw = ImGui.GetWindowDrawList();
        var box = CheckSize();
        var min = new Vector2(start.X, MathF.Round(start.Y + (height - box) * .5f));
        var ink = Anim.Lerp(P.Accent, P.AccentHover, hover);
        draw.AddRectFilled(min, min + new Vector2(box), Col(Anim.Lerp(Anim.Lerp(P.Surface, P.Elevated, hover), ink, on)), S(3));
        draw.AddRect(min, min + new Vector2(box), Col(Anim.Lerp(Anim.Lerp(P.Border, P.TextFaint, hover * .6f), ink, on)), S(3));
        if (on > .01f) DrawCheckMark(draw, min, box, Anim.EaseOutCubic(on));
        if (text.Length > 0)
            draw.AddText(new Vector2(min.X + box + S(10), start.Y + (height - ImGui.GetTextLineHeight()) * .5f), Col(P.Text), text);
    }

    // The check's stroke runs from the end of its short leg to the tip of its long one as progress goes 0..1.
    private static void DrawCheckMark(ImDrawListPtr draw, Vector2 min, float box, float progress)
    {
        var a = min + new Vector2(box * .26f, box * .53f);
        var b = min + new Vector2(box * .43f, box * .70f);
        var c = min + new Vector2(box * .75f, box * .33f);
        var shortLeg = Vector2.Distance(a, b);
        var longLeg = Vector2.Distance(b, c);
        var length = (shortLeg + longLeg) * progress;
        var color = Col(P.OnAccent);
        var thickness = MathF.Max(1.5f, S(1.75f));
        if (length <= shortLeg)
        {
            draw.AddLine(a, Vector2.Lerp(a, b, length / shortLeg), color, thickness);
            return;
        }
        draw.PathLineTo(a);
        draw.PathLineTo(b);
        draw.PathLineTo(Vector2.Lerp(b, c, (length - shortLeg) / longLeg));
        draw.PathStroke(color, ImDrawFlags.None, thickness);
    }

    // ---------------------------------------------------------------- sliders

    /// <summary>
    /// A labeled slider: the label above, the value in a box on the left and the track to its right.
    /// <paramref name="format"/> is a .NET format string; the value snaps to the precision it shows ("0.00" stores
    /// 1.00, never 1.0042). When the track has under 2 px per shown unit, drags snap to round 1-2-5 steps (10 yd on a
    /// 10 - 500 yd slider); Left/Right still step by one shown unit while the slider is hovered or dragged.
    /// </summary>
    public static bool Slider(string label, ref float value, float min, float max, string format = "0.0", string? description = null)
        => SliderCore(label, ref value, min, max, 0f, format, description);

    /// <summary>Integer slider; snaps to whole numbers.</summary>
    public static bool Slider(string label, ref int value, int min, int max, string format = "0", string? description = null)
    {
        float f = value;
        if (!SliderCore(label, ref f, min, max, 1f, format, description)) return false;
        value = (int)MathF.Round(f);
        return true;
    }

    private static bool SliderCore(string label, ref float value, float min, float max, float step, string format, string? description)
    {
        ImGui.PushID(label);
        ImGui.BeginGroup();
        Label(label);
        var width = ControlWidth;
        var height = ImGui.GetFrameHeight() + S(4);
        var start = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        // The value box, as wide as the widest text the slider can show: if it followed the current text, the track
        // would shift under a still cursor whenever the text changed width (Inter's digits are proportional).
        var shown = value.ToString(format, CultureInfo.InvariantCulture);
        var textSize = ImGui.CalcTextSize(shown);
        var boxWidth = MathF.Max(MathF.Max(S(48), SliderTextWidth(format, min, max) + S(20)), textSize.X + S(20));
        draw.AddRectFilled(start, start + new Vector2(boxWidth, height), Col(P.Surface), S(4));
        draw.AddRect(start, start + new Vector2(boxWidth, height), Col(P.Border), S(4));
        draw.AddText(start + (new Vector2(boxWidth, height) - textSize) * .5f, Col(P.Text), shown);

        // The track: a thin rail, an ink fill up to a small grip.
        var gripSize = new Vector2(S(22), S(15));
        var gap = S(30);
        var trackStart = new Vector2(start.X + boxWidth + gap, start.Y);
        var trackWidth = MathF.Max(S(40), width - boxWidth - gap);
        ImGui.SetCursorScreenPos(trackStart);
        ImGui.InvisibleButton("##track", new Vector2(trackWidth, height));
        var active = ImGui.IsItemActive();
        var hovered = ImGui.IsItemHovered();
        if (hovered || active) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var left = trackStart.X + gripSize.X * .5f;
        var right = trackStart.X + trackWidth - gripSize.X * .5f;
        var changed = false;
        if ((active || hovered) && max > min)
        {
            var io = ImGui.GetIO();
            var storage = ImGui.GetStateStorage();
            var grabId = ImGui.GetID("##grab");
            float FromMouse(float x) => min + Math.Clamp((x - left) / (right - left), 0f, 1f) * (max - min);
            float? next = null;
            if (ImGui.IsItemActivated())
            {
                // Pressing the grip keeps the value and drags from where it was grabbed; pressing the rail jumps there.
                var gripCenter = Anim.Lerp(left, right, Math.Clamp((value - min) / (max - min), 0f, 1f));
                var onGrip = MathF.Abs(io.MousePos.X - gripCenter) <= gripSize.X * .5f;
                storage.SetFloat(grabId, onGrip ? io.MousePos.X - gripCenter : 0f);
                if (!onGrip) next = FromMouse(io.MousePos.X);
            }
            else if (active && io.MouseDelta.X != 0)
                next = FromMouse(io.MousePos.X - storage.GetFloat(grabId, 0f));
            // Left/Right step by one shown unit, for values the track has too few pixels to land on. The slider owns
            // them meanwhile, so keyboard nav does not move with them.
            var keys = active || !io.WantTextInput;
            if (keys)
            {
                ImGui.SetItemKeyOwner(ImGuiKey.LeftArrow);
                ImGui.SetItemKeyOwner(ImGuiKey.RightArrow);
            }
            var direction = (keys && ImGui.IsKeyPressed(ImGuiKey.RightArrow) ? 1 : 0) - (keys && ImGui.IsKeyPressed(ImGuiKey.LeftArrow) ? 1 : 0);
            if (next is not null || direction != 0)
            {
                // Values snap to what the box shows, so the stored value is the one on screen and equal-looking
                // values cannot alternate.
                var decimals = step > 0 ? -1 : ShownDecimals(format, min, max);
                var unit = step > 0 ? step : decimals >= 0 ? MathF.Pow(10, -decimals) : (max - min) / 100f;
                var candidate = next ?? value;
                // Drags snap to round steps when one unit is narrower than the mouse can pick out (10 yd steps for
                // 10 - 500 yd on a 165 px track); Left/Right still move one unit.
                if (next is not null && (step > 0 || decimals >= 0))
                {
                    var dragStep = DragStep(unit, (right - left) / (max - min));
                    candidate = MathF.Round(candidate / dragStep) * dragStep;
                }
                candidate += direction * unit;
                if (step > 0) candidate = MathF.Round(candidate / step) * step;
                else if (decimals >= 0) candidate = (float)Math.Round(candidate, decimals);
                candidate = Math.Clamp(candidate, min, max);
                if (candidate != value) { value = candidate; changed = true; }
            }
        }
        var target = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) : 0f;
        var t = Anim.To(ImGui.GetID("##fill"), target, active ? 40f : 16f);
        var hover = Anim.Toggle(ImGui.GetID("##hover"), hovered || active, 18f);

        var cy = MathF.Round(trackStart.Y + height * .5f);
        var thick = MathF.Round(S(8));
        var railMin = new Vector2(trackStart.X, cy - thick * .5f);
        var railMax = new Vector2(trackStart.X + trackWidth, cy + thick * .5f);
        draw.AddRectFilled(railMin, railMax, Col(P.Surface), thick * .5f);
        draw.AddRect(railMin, railMax, Col(P.Border), thick * .5f);
        var gripX = Anim.Lerp(left, right, t);
        if (gripX - railMin.X > S(2))
            draw.AddRectFilled(railMin + new Vector2(S(1), S(1)), new Vector2(gripX, railMax.Y - S(1)),
                Col(Anim.Lerp(P.Accent, P.AccentHover, hover)), thick * .5f);
        var gripMin = new Vector2(MathF.Round(gripX - gripSize.X * .5f), MathF.Round(cy - gripSize.Y * .5f));
        draw.AddRectFilled(gripMin + new Vector2(0, S(1)), gripMin + gripSize + new Vector2(0, S(1)),
            Col(new Vector4(0, 0, 0, Theme.IsDark ? .3f : .08f)), S(4));
        draw.AddRectFilled(gripMin, gripMin + gripSize, Col(P.Knob), S(4));
        draw.AddRect(gripMin, gripMin + gripSize, Col(Anim.Lerp(P.Border, P.TextFaint, hover)), S(4));
        // Three grip lines.
        for (var i = -1; i <= 1; i++)
        {
            var lx = MathF.Round(gripMin.X + gripSize.X * .5f + i * S(3)) + .5f;
            draw.AddLine(new Vector2(lx, gripMin.Y + S(4.5f)), new Vector2(lx, gripMin.Y + gripSize.Y - S(4.5f)), Col(P.TextMuted), MathF.Max(1, S(1)));
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.EndGroup();
        ImGui.PopID();
        About(description, label);
        Gap();
        return changed;
    }

    private static readonly string[] DigitText = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

    // The width of the widest text the slider can show: min and max as shown, with every digit drawn as the widest one.
    private static float SliderTextWidth(string format, float min, float max)
    {
        var widest = '0';
        var widestWidth = 0f;
        for (var i = 0; i < DigitText.Length; i++)
        {
            var digitWidth = ImGui.CalcTextSize(DigitText[i]).X;
            if (digitWidth > widestWidth) (widest, widestWidth) = ((char)('0' + i), digitWidth);
        }
        string Widen(float value)
        {
            var text = value.ToString(format, CultureInfo.InvariantCulture).ToCharArray();
            for (var i = 0; i < text.Length; i++)
                if (char.IsAsciiDigit(text[i])) text[i] = widest;
            return new string(text);
        }
        return MathF.Max(ImGui.CalcTextSize(Widen(min)).X, ImGui.CalcTextSize(Widen(max)).X);
    }

    // The decimals the format shows ("0.00'x'" shows 2, "0%" shows 2 of the fraction): the fewest at which rounding
    // leaves the shown text unchanged. -1 when none up to 6 does, or when one shown step is wider than the range.
    private static int ShownDecimals(string format, float min, float max)
    {
        ReadOnlySpan<float> probes = [.1234567f, .4567891f, .7654321f];
        for (var decimals = 0; decimals <= 6; decimals++)
        {
            var same = true;
            foreach (var probe in probes)
            {
                var value = min + (max - min) * probe;
                if (((float)Math.Round(value, decimals)).ToString(format, CultureInfo.InvariantCulture) !=
                    value.ToString(format, CultureInfo.InvariantCulture)) { same = false; break; }
            }
            if (same) return MathF.Pow(10, -decimals) <= max - min ? decimals : -1;
        }
        return -1;
    }

    // The step a drag snaps to: one shown unit, or when that is under 2 px of track, the smallest 1-2-5 multiple of it
    // (2, 5, 10, 20, 50...) that is at least 2 px, so round values stay within the mouse's reach.
    private static float DragStep(float unit, float pixelsPerValue)
    {
        ReadOnlySpan<float> factors = [1, 2, 5];
        for (var scale = 1f; scale < 1e9f; scale *= 10)
            foreach (var factor in factors)
                if (unit * factor * scale * pixelsPerValue >= 2f) return unit * factor * scale;
        return unit;
    }

    // ---------------------------------------------------------------- buttons

    /// <summary>
    /// A button with a smooth hover transition. <paramref name="size"/>: 0 = fit text, negative X = fill the row.
    /// Secondary buttons are outlined, primary ones filled with the accent ink.
    /// Icons can be part of the label: <c>Ui.Button($"{Icons.Save}  Save", ButtonKind.Primary)</c>.
    /// </summary>
    public static bool Button(string label, ButtonKind kind = ButtonKind.Secondary, Vector2 size = default)
    {
        var text = Visible(label);
        var textSize = ImGui.CalcTextSize(text);
        var padding = ImGui.GetStyle().FramePadding;
        if (size.X < 0) size.X = ImGui.GetContentRegionAvail().X;
        if (size.X == 0) size.X = textSize.X + padding.X * 2 + S(10);
        if (size.Y == 0) size.Y = ImGui.GetFrameHeight();

        var start = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(label, size);
        var hovered = ImGui.IsItemHovered();
        var id = ImGui.GetItemID();
        var hover = Anim.Toggle(id ^ 0x5A17u, hovered, 18f);
        var press = Anim.Toggle(id ^ 0x7E11u, ImGui.IsItemActive(), 28f);
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        Vector4 background, foreground, border;
        switch (kind)
        {
            case ButtonKind.Primary:
                background = Anim.Lerp(Anim.Lerp(P.Accent, P.AccentHover, hover), P.Accent, press);
                foreground = P.OnAccent;
                border = Palette.Alpha(P.Accent, 0);
                break;
            case ButtonKind.Ghost:
                background = Anim.Lerp(Palette.Alpha(P.Field, 0), P.Field, MathF.Max(hover, press));
                foreground = Anim.Lerp(P.TextMuted, P.Text, hover);
                border = Palette.Alpha(P.Border, 0);
                break;
            case ButtonKind.Danger:
                background = Palette.Alpha(P.Danger, Anim.Lerp(.06f, .12f, hover) + .06f * press);
                foreground = P.Danger;
                border = Palette.Alpha(P.Danger, .35f);
                break;
            default:
                background = Anim.Lerp(Anim.Lerp(P.Surface, P.Elevated, hover), P.Field, press);
                foreground = P.Text;
                border = Anim.Lerp(P.Border, P.TextFaint, hover * .7f);
                break;
        }
        var draw = ImGui.GetWindowDrawList();
        var rounding = ImGui.GetStyle().FrameRounding;
        draw.AddRectFilled(start, start + size, Col(background), rounding);
        if (border.W > 0) draw.AddRect(start, start + size, Col(border), rounding);
        draw.AddText(start + (size - textSize) * .5f, Col(foreground), text);
        return clicked;
    }

    /// <summary>A square, borderless icon button. <paramref name="id"/> keeps it unique when the same icon repeats.</summary>
    public static bool IconButton(string icon, string id, string? tooltip = null, float size = 0)
    {
        if (size <= 0) size = ImGui.GetFrameHeight();
        var start = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, new Vector2(size));
        var hovered = ImGui.IsItemHovered();
        var hover = Anim.Toggle(ImGui.GetItemID() ^ 0x1C0Bu, hovered, 18f);
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + new Vector2(size), Col(P.Field, hover), ImGui.GetStyle().FrameRounding);
        var iconSize = ImGui.CalcTextSize(icon);
        draw.AddText(start + (new Vector2(size) - iconSize) * .5f, Col(Anim.Lerp(P.TextMuted, P.Text, hover)), icon);
        if (tooltip is not null) Tooltip(tooltip);
        return clicked;
    }

    /// <summary>
    /// A segmented control with a sliding ink thumb. <paramref name="width"/> 0 uses <see cref="ControlWidth"/>.
    /// Returns true when the selection changes.
    /// </summary>
    public static bool Segmented(string id, ref int index, string[] items, float width = 0)
    {
        if (items.Length == 0) return false;
        ImGui.PushID(id);
        if (width <= 0) width = ControlWidth;
        var height = ImGui.GetFrameHeight();
        var start = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        var rounding = ImGui.GetStyle().FrameRounding;
        var inset = S(3);
        var segment = (width - inset * 2) / items.Length;
        draw.AddRectFilled(start, start + new Vector2(width, height), Col(P.Surface), rounding);
        draw.AddRect(start, start + new Vector2(width, height), Col(P.Border), rounding);

        var at = Anim.To(ImGui.GetID("##thumb"), index, 16f);
        var thumbMin = new Vector2(start.X + inset + segment * at, start.Y + inset);
        var thumbMax = thumbMin + new Vector2(segment, height - inset * 2);
        draw.AddRectFilled(thumbMin, thumbMax, Col(P.Accent), MathF.Max(0, rounding - inset * .5f));

        var changed = false;
        for (var i = 0; i < items.Length; i++)
        {
            var itemMin = new Vector2(start.X + inset + segment * i, start.Y);
            ImGui.SetCursorScreenPos(itemMin);
            if (ImGui.InvisibleButton($"##seg{i}", new Vector2(segment, height)) && index != i) { index = i; changed = true; }
            var hovered = ImGui.IsItemHovered();
            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var selected = Math.Clamp(1f - MathF.Abs(at - i), 0f, 1f);
            var hover = Anim.Toggle(ImGui.GetItemID(), hovered, 18f);
            var color = Anim.Lerp(Anim.Lerp(P.TextMuted, P.Text, hover), P.OnAccent, selected);
            var text = Visible(items[i]);
            var size = ImGui.CalcTextSize(text);
            draw.AddText(new Vector2(itemMin.X + (segment - size.X) * .5f, itemMin.Y + (height - size.Y) * .5f), Col(color), text);
        }
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.PopID();
        return changed;
    }

    // ---------------------------------------------------------------- labeled native controls

    /// <summary>Width of a control under its label (the column width, up to 280 px).</summary>
    public static float ControlWidth => MathF.Min(S(280), ImGui.GetContentRegionAvail().X);

    /// <summary>
    /// Draws a label on its own line and leaves the cursor where the control goes, with the item width set to
    /// <paramref name="controlWidth"/> (0 = <see cref="ControlWidth"/>). Use it to put any ImGui widget in the
    /// standard layout: <code>Ui.Row("Mode"); ImGui.Combo("##mode", ref mode, items, items.Length);</code>
    /// </summary>
    public static void Row(string label, float controlWidth = 0)
    {
        if (controlWidth <= 0) controlWidth = ControlWidth;
        Label(label);
        ImGui.SetNextItemWidth(controlWidth);
    }

    /// <summary>A labeled dropdown; the list opens just under the field, the selected item highlighted.</summary>
    public static bool Combo(string label, ref int index, string[] items, string? description = null)
    {
        ImGui.PushID(label);
        ImGui.BeginGroup();
        Row(label);
        var width = ImGui.CalcItemWidth();
        var size = new Vector2(width, ImGui.GetFrameHeight());
        var min = ImGui.GetCursorScreenPos();
        var max = min + size;
        if (ImGui.InvisibleButton("##field", size)) ImGui.OpenPopup("##list");
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var open = ImGui.IsPopupOpen("##list");
        var hover = Anim.Toggle(ImGui.GetID("##hover"), hovered || open, 18f);

        var draw = ImGui.GetWindowDrawList();
        var rounding = ImGui.GetStyle().FrameRounding;
        var padding = ImGui.GetStyle().FramePadding;
        draw.AddRectFilled(min, max, Col(Anim.Lerp(P.Surface, P.Elevated, hover)), rounding);
        draw.AddRect(min, max, Col(Anim.Lerp(P.Border, P.TextFaint, hover * .7f)), rounding);
        var preview = index >= 0 && index < items.Length ? items[index] : "";
        var textY = min.Y + (size.Y - ImGui.GetTextLineHeight()) * .5f;
        draw.PushClipRect(min, max - new Vector2(padding.X + S(16), 0), true);
        draw.AddText(new Vector2(min.X + padding.X, textY), Col(P.Text), preview);
        draw.PopClipRect();
        var chevron = open ? Icons.ChevronUp : Icons.ChevronDown;
        var chevronSize = ImGui.CalcTextSize(chevron);
        draw.AddText(new Vector2(max.X - chevronSize.X - padding.X, min.Y + (size.Y - chevronSize.Y) * .5f), Col(P.TextMuted), chevron);

        var changed = false;
        ImGui.SetNextWindowPos(new Vector2(min.X, max.Y + S(6)));
        ImGui.SetNextWindowSize(new Vector2(width, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, S(5));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(6), S(6)));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, S(2)));
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, .5f));
        if (ImGui.BeginPopup("##list", ImGuiWindowFlags.NoMove))
        {
            for (var i = 0; i < items.Length; i++)
            {
                var selected = i == index;
                ImGui.PushStyleColor(ImGuiCol.Text, selected ? P.Text : P.TextMuted);
                if (ImGui.Selectable($"  {items[i]}##{i}", selected, ImGuiSelectableFlags.None, new Vector2(0, S(28)))) { index = i; changed = true; }
                ImGui.PopStyleColor();
                if (selected && ImGui.IsWindowAppearing()) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndPopup();
        }
        ImGui.PopStyleVar(4);
        ImGui.EndGroup();
        ImGui.PopID();
        About(description, label);
        Gap();
        return changed;
    }

    /// <summary>A labeled text field with placeholder text.</summary>
    public static bool TextInput(string label, ref string value, string hint = "", uint maxLength = 256, string? description = null)
    {
        ImGui.BeginGroup();
        Row(label);
        var changed = ImGui.InputTextWithHint("##" + label, hint, ref value, maxLength);
        ImGui.EndGroup();
        About(description, label);
        Gap();
        return changed;
    }

    /// <summary>A labeled number field that can be dragged or typed into (double-click).</summary>
    public static bool Number(string label, ref float value, float speed = .1f, float min = 0, float max = 0, string format = "%.2f", string? description = null)
    {
        ImGui.BeginGroup();
        Row(label);
        var changed = ImGui.DragFloat("##" + label, ref value, speed, min, max, format);
        ImGui.EndGroup();
        About(description, label);
        Gap();
        return changed;
    }

    /// <summary>Integer variant of <see cref="Number(string, ref float, float, float, float, string, string?)"/>.</summary>
    public static bool Number(string label, ref int value, float speed = .2f, int min = 0, int max = 0, string? description = null)
    {
        ImGui.BeginGroup();
        Row(label);
        var changed = ImGui.DragInt("##" + label, ref value, speed, min, max);
        ImGui.EndGroup();
        About(description, label);
        Gap();
        return changed;
    }

    /// <summary>A color swatch with its label beside it; clicking the swatch opens a picker.</summary>
    public static bool Color(string label, ref Vector4 color, bool alpha = true, string? description = null)
    {
        var swatch = ImGui.GetFrameHeight();
        ImGui.BeginGroup();
        var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaPreviewHalf;
        if (alpha) flags |= ImGuiColorEditFlags.AlphaBar; else flags |= ImGuiColorEditFlags.NoAlpha;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(swatch * .8f - S(1), ImGui.GetStyle().FramePadding.Y));
        var changed = ImGui.ColorEdit4("##" + label, ref color, flags);
        ImGui.PopStyleVar();
        ImGui.SameLine(0, S(12));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Visible(label));
        ImGui.EndGroup();
        About(description, label);
        Gap();
        return changed;
    }

    /// <summary>
    /// A key picker drawn as an outlined button reading "Label: Key". Click it, then press any key or mouse button.
    /// Escape cancels, Backspace clears. <paramref name="key"/> is a Windows virtual-key code (see
    /// <see cref="VirtualKey"/>); 0 means none.
    /// </summary>
    public static bool Keybind(string label, ref int key, string? description = null)
    {
        var id = ImGui.GetID(label);
        var waiting = _capturingKey == id;
        var changed = false;
        if (waiting)
        {
            var pressed = Input.AnyPressed();
            if (pressed == (int)VirtualKey.Escape) _capturingKey = 0;
            else if (pressed == (int)VirtualKey.Back) { key = 0; changed = true; _capturingKey = 0; }
            else if (pressed != 0) { key = pressed; changed = true; _capturingKey = 0; }
        }
        var text = waiting ? "press a key…" : Input.KeyName(key);
        if (Button($"{Visible(label)}: {text}##{label}", waiting ? ButtonKind.Primary : ButtonKind.Secondary, new Vector2(ControlWidth, 0)) && !waiting)
            _capturingKey = id;
        About(description, label);
        Gap();
        return changed;
    }

    // ---------------------------------------------------------------- feedback

    /// <summary>A thin animated progress bar. <paramref name="label"/> shows on the left, the percentage on the right.</summary>
    public static void Progress(float fraction, string? label = null, Tone tone = Tone.Accent, float height = 0)
    {
        if (height <= 0) height = S(6);
        var width = ImGui.GetContentRegionAvail().X;
        var key = label ?? "##progress";
        ImGui.PushID(key);
        if (label is not null)
        {
            var x0 = ImGui.GetCursorPosX();
            ImGui.TextUnformatted(Visible(label));
            var percent = $"{Math.Clamp(fraction, 0, 1) * 100:0}%";
            ImGui.SameLine(x0 + width - ImGui.CalcTextSize(percent).X);
            Text(percent, P.TextMuted);
        }
        var shown = Anim.To(ImGui.GetID("##value"), Math.Clamp(fraction, 0f, 1f), 10f, 0f);
        var start = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + new Vector2(width, height), Col(P.Field), height);
        if (shown > .001f) draw.AddRectFilled(start, start + new Vector2(MathF.Max(height, width * shown), height), Col(ToneColor(tone)), height);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.PopID();
    }

    /// <summary>An indeterminate loading spinner.</summary>
    public static void Spinner(float radius = 0, float thickness = 0)
    {
        if (radius <= 0) radius = S(8);
        if (thickness <= 0) thickness = S(2);
        var start = ImGui.GetCursorScreenPos();
        var center = start + new Vector2(radius + thickness);
        var time = (float)ImGui.GetTime();
        var draw = ImGui.GetWindowDrawList();
        draw.AddCircle(center, radius, Col(P.Field), 32, thickness);
        var head = time * 5.5f;
        var length = 1.2f + MathF.Sin(time * 2.4f) * .7f;
        draw.PathArcTo(center, radius, head, head + length, 24);
        draw.PathStroke(Col(P.Accent), ImDrawFlags.None, thickness);
        ImGui.Dummy(new Vector2((radius + thickness) * 2));
    }

    /// <summary>
    /// Shows a fading tooltip when the previous item is hovered. Call right after the widget it describes.
    /// </summary>
    public static void Tooltip(string text)
    {
        var id = ImGui.GetItemID() ^ 0x7007u;
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            Anim.Set(id, 0);
            return;
        }
        var alpha = Anim.To(id, 1f, 16f, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * alpha);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(10), S(7)));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, S(7));
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(S(280));
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
        ImGui.PopStyleVar(3);
    }

    /// <summary>A small muted info icon that shows <paramref name="text"/> on hover. Use after <c>ImGui.SameLine()</c>.</summary>
    public static void HelpMarker(string text)
    {
        Text(Icons.Info, P.TextFaint);
        Tooltip(text);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>The color for a <see cref="Tone"/> in the current theme.</summary>
    public static Vector4 ToneColor(Tone tone) => tone switch
    {
        Tone.Accent => P.Accent,
        Tone.Success => P.Success,
        Tone.Warning => P.Warning,
        Tone.Danger => P.Danger,
        _ => P.TextMuted,
    };

    /// <summary>
    /// Attaches a description to the previous item (or group): it shows under ABOUT in the status column while the
    /// item is hovered, or right under it when the menu has no status column. <paramref name="title"/> names the
    /// control above the description under ABOUT ("##" and what follows are dropped).
    /// </summary>
    public static void About(string? description, string? title = null)
    {
        if (description is null) return;
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
        {
            CurrentAbout = description;
            CurrentAboutTitle = title is null || Visible(title).Length == 0 ? null : Visible(title);
        }
        if (!InlineDescriptions) return;
        ImGui.PushFont(Fonts.Small);
        ImGui.PushStyleColor(ImGuiCol.Text, P.TextMuted);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - S(3));
        TextWrapped(description);
        ImGui.PopStyleColor();
        ImGui.PopFont();
    }

    // A control's label on its own line, a little closer to the control than to the row above.
    private static void Label(string label)
    {
        var text = Visible(label);
        if (text.Length == 0) return;
        ImGui.TextUnformatted(text);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - S(1));
    }

    // Extra room after a labeled control, so rows read as separate groups.
    private static void Gap() => ImGui.Dummy(new Vector2(0, S(8)));

    /// <summary>The visible part of an ImGui label (text before "##").</summary>
    public static string Visible(string label)
    {
        var index = label.IndexOf("##", StringComparison.Ordinal);
        return index < 0 ? label : label[..index];
    }
}
