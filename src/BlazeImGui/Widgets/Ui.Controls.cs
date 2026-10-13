using System.Globalization;
using System.Numerics;
using ImGuiNET;

namespace BlazeImGui;

/// <summary>Visual weight of <see cref="Ui.Button"/>.</summary>
public enum ButtonKind { Secondary, Primary, Ghost, Danger }

/// <summary>Semantic color for badges, toasts, stat deltas.</summary>
public enum Tone { Neutral, Accent, Success, Warning, Danger }

/// <summary>
/// Themed, animated widgets. Every labeled control follows the same "settings row" layout: the label on the
/// left, the control right-aligned, an optional description underneath. Labels follow ImGui rules: text after
/// "##" is hidden but keeps the ID unique ("Enabled##esp"). All widgets return true on the frame the value changes.
/// Plain ImGui widgets (Combo, InputText, Tables, Plot*, TreeNode, TabBar...) are themed too and can be mixed freely.
/// </summary>
public static partial class Ui
{
    /// <summary>UI scale; set from <see cref="OverlayOptions.UiScale"/>.</summary>
    public static float Scale { get; set; } = 1f;

    /// <summary>True while a <see cref="Keybind"/> waits for a key; the overlay ignores its hotkeys meanwhile.</summary>
    public static bool IsCapturingKey => _capturingKey != 0;

    private static uint _capturingKey;
    private static float S(float value) => value * Scale;
    private static Palette P => Theme.Colors;
    private static uint Col(Vector4 color, float alpha = 1f) => Theme.U32(color, alpha);

    // ---------------------------------------------------------------- toggles & checkboxes

    /// <summary>A full-width row with a label and an animated switch. The whole row is clickable.</summary>
    public static bool Toggle(string label, ref bool value, string? description = null)
    {
        ImGui.PushID(label);
        var text = Visible(label);
        var width = ImGui.GetContentRegionAvail().X;
        var start = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        var descriptionHeight = description is null ? 0 : SmallLineHeight() + S(2);
        var height = MathF.Max(ImGui.GetFrameHeight(), lineHeight + descriptionHeight + S(6));

        var changed = ImGui.InvisibleButton("##row", new Vector2(width, height));
        if (changed) value = !value;
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var on = Anim.Toggle(ImGui.GetID("##on"), value, 16f);
        var hover = Anim.Toggle(ImGui.GetID("##hover"), hovered, 18f);

        var draw = ImGui.GetWindowDrawList();
        var size = new Vector2(S(34), S(20));
        var track = new Vector2(start.X + width - size.X, start.Y + (height - size.Y) * .5f);
        var off = Anim.Lerp(P.Field, P.FieldActive, hover);
        var onColor = Anim.Lerp(P.Accent, P.AccentHover, hover);
        draw.AddRectFilled(track, track + size, Col(Anim.Lerp(off, onColor, on)), size.Y * .5f);
        var radius = size.Y * .5f - S(3);
        var knob = new Vector2(Anim.Lerp(track.X + S(3) + radius, track.X + size.X - S(3) - radius, on), track.Y + size.Y * .5f);
        draw.AddCircleFilled(knob + new Vector2(0, S(.75f)), radius + S(.5f), Col(new Vector4(0, 0, 0, Theme.IsDark ? .35f : .18f)));
        draw.AddCircleFilled(knob, radius, Col(P.Knob));

        var textY = start.Y + (height - lineHeight - descriptionHeight) * .5f;
        draw.AddText(new Vector2(start.X, textY), Col(P.Text), text);
        if (description is not null)
            draw.AddText(Fonts.Small, Fonts.Small.FontSize, new Vector2(start.X, textY + lineHeight + S(2)), Col(P.TextMuted), description);
        ImGui.PopID();
        return changed;
    }

    /// <summary>A compact checkbox with an animated fill, for dense lists. Use <see cref="Toggle"/> for settings rows.</summary>
    public static bool Checkbox(string label, ref bool value)
    {
        ImGui.PushID(label);
        var text = Visible(label);
        var box = ImGui.GetFrameHeight() - S(8);
        var start = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(text);
        var height = ImGui.GetFrameHeight();
        var width = box + (text.Length > 0 ? ImGui.GetStyle().ItemInnerSpacing.X + textSize.X : 0);
        var changed = ImGui.InvisibleButton("##box", new Vector2(width, height));
        if (changed) value = !value;
        var hovered = ImGui.IsItemHovered();
        var on = Anim.Toggle(ImGui.GetID("##on"), value, 20f);
        var hover = Anim.Toggle(ImGui.GetID("##hover"), hovered, 18f);

        var draw = ImGui.GetWindowDrawList();
        var min = new Vector2(start.X, start.Y + (height - box) * .5f);
        var rest = Anim.Lerp(P.Field, P.FieldHover, hover);
        draw.AddRectFilled(min, min + new Vector2(box), Col(Anim.Lerp(rest, P.Accent, on)), S(5));
        if (on > .01f)
        {
            // A drawn check that grows in with the fill.
            var a = min + new Vector2(box * .24f, box * .52f);
            var b = min + new Vector2(box * .42f, box * .70f);
            var c = min + new Vector2(box * .76f, box * .32f);
            var t = Anim.EaseOutCubic(on);
            var mid = Anim.Lerp(a, b, MathF.Min(1, t * 2));
            draw.AddLine(a, mid, Col(P.OnAccent, on), S(1.8f));
            if (t > .5f) draw.AddLine(b, Anim.Lerp(b, c, (t - .5f) * 2), Col(P.OnAccent, on), S(1.8f));
        }
        if (text.Length > 0)
            draw.AddText(new Vector2(min.X + box + ImGui.GetStyle().ItemInnerSpacing.X, start.Y + (height - textSize.Y) * .5f), Col(P.Text), text);
        ImGui.PopID();
        return changed;
    }

    // ---------------------------------------------------------------- sliders

    /// <summary>A labeled slider with the value shown on the right. <paramref name="format"/> is a .NET format string.</summary>
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
        var width = ImGui.GetContentRegionAvail().X;
        var x0 = ImGui.GetCursorPosX();
        ImGui.TextUnformatted(Visible(label));
        var shown = value.ToString(format, CultureInfo.InvariantCulture);
        ImGui.SameLine(x0 + width - ImGui.CalcTextSize(shown).X);
        Text(shown, P.TextMuted);

        var height = S(18);
        var start = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##track", new Vector2(width, height));
        var active = ImGui.IsItemActive();
        var hovered = ImGui.IsItemHovered();
        if (hovered || active) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var radius = S(7);
        var changed = false;
        if (active && max > min)
        {
            var fraction = Math.Clamp((ImGui.GetIO().MousePos.X - start.X - radius) / (width - radius * 2), 0f, 1f);
            var next = min + fraction * (max - min);
            if (step > 0) next = MathF.Round(next / step) * step;
            if (next != value) { value = next; changed = true; }
        }
        var target = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) : 0f;
        var t = Anim.To(ImGui.GetID("##fill"), target, active ? 40f : 16f);
        var grow = Anim.Toggle(ImGui.GetID("##grow"), hovered || active, 18f);

        var draw = ImGui.GetWindowDrawList();
        var cy = start.Y + height * .5f;
        var left = start.X + radius;
        var right = start.X + width - radius;
        var knobX = Anim.Lerp(left, right, t);
        var thick = S(4);
        draw.AddRectFilled(new Vector2(left - thick * .5f, cy - thick * .5f), new Vector2(right + thick * .5f, cy + thick * .5f), Col(P.Field), thick);
        draw.AddRectFilled(new Vector2(left - thick * .5f, cy - thick * .5f), new Vector2(knobX, cy + thick * .5f), Col(P.Accent), thick);
        var r = Anim.Lerp(S(6), radius, grow);
        draw.AddCircleFilled(new Vector2(knobX, cy + S(.75f)), r + S(.5f), Col(new Vector4(0, 0, 0, Theme.IsDark ? .4f : .18f)));
        draw.AddCircleFilled(new Vector2(knobX, cy), r, Col(P.Knob));
        Description(description);
        ImGui.PopID();
        return changed;
    }

    // ---------------------------------------------------------------- buttons

    /// <summary>
    /// A button with a smooth hover transition. <paramref name="size"/>: 0 = fit text, negative X = fill the row.
    /// Icons can be part of the label: <c>Ui.Button($"{Icons.Save}  Save", ButtonKind.Primary)</c>.
    /// </summary>
    public static bool Button(string label, ButtonKind kind = ButtonKind.Secondary, Vector2 size = default)
    {
        var text = Visible(label);
        var textSize = ImGui.CalcTextSize(text);
        var padding = ImGui.GetStyle().FramePadding;
        if (size.X < 0) size.X = ImGui.GetContentRegionAvail().X;
        if (size.X == 0) size.X = textSize.X + padding.X * 2 + S(6);
        if (size.Y == 0) size.Y = ImGui.GetFrameHeight();

        var start = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(label, size);
        var hovered = ImGui.IsItemHovered();
        var id = ImGui.GetItemID();
        var hover = Anim.Toggle(id ^ 0x5A17u, hovered, 18f);
        var press = Anim.Toggle(id ^ 0x7E11u, ImGui.IsItemActive(), 28f);
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        Vector4 background, foreground;
        switch (kind)
        {
            case ButtonKind.Primary:
                background = Palette.Shade(Anim.Lerp(P.Accent, P.AccentHover, hover), -.08f * press);
                foreground = P.OnAccent;
                break;
            case ButtonKind.Ghost:
                background = Anim.Lerp(Palette.Alpha(P.FieldHover, 0), P.FieldHover, MathF.Max(hover, press));
                foreground = Anim.Lerp(P.TextMuted, P.Text, hover);
                break;
            case ButtonKind.Danger:
                background = Palette.Alpha(P.Danger, Anim.Lerp(.12f, .2f, hover) + .06f * press);
                foreground = P.Danger;
                break;
            default:
                background = Anim.Lerp(Anim.Lerp(P.Field, P.FieldHover, hover), P.FieldActive, press);
                foreground = P.Text;
                break;
        }
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + size, Col(background), ImGui.GetStyle().FrameRounding);
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
        draw.AddRectFilled(start, start + new Vector2(size), Col(P.FieldHover, hover), ImGui.GetStyle().FrameRounding);
        var iconSize = ImGui.CalcTextSize(icon);
        draw.AddText(start + (new Vector2(size) - iconSize) * .5f, Col(Anim.Lerp(P.TextMuted, P.Text, hover)), icon);
        if (tooltip is not null) Tooltip(tooltip);
        return clicked;
    }

    /// <summary>
    /// A segmented control (pill tabs) with a sliding thumb. <paramref name="width"/> 0 fills the remaining width
    /// (after <see cref="Row"/> that is exactly the control column).
    /// Returns true when the selection changes.
    /// </summary>
    public static bool Segmented(string id, ref int index, string[] items, float width = 0)
    {
        if (items.Length == 0) return false;
        ImGui.PushID(id);
        if (width <= 0) width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetFrameHeight();
        var start = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        var rounding = ImGui.GetStyle().FrameRounding;
        var inset = S(3);
        var segment = (width - inset * 2) / items.Length;
        draw.AddRectFilled(start, start + new Vector2(width, height), Col(P.Field), rounding);

        var at = Anim.To(ImGui.GetID("##thumb"), index, 16f);
        var thumbMin = new Vector2(start.X + inset + segment * at, start.Y + inset);
        var thumbMax = thumbMin + new Vector2(segment, height - inset * 2);
        if (!Theme.IsDark) draw.AddRectFilled(thumbMin + new Vector2(0, S(1)), thumbMax + new Vector2(0, S(1)), Col(new Vector4(0, 0, 0, .06f)), rounding - inset * .5f);
        draw.AddRectFilled(thumbMin, thumbMax, Col(P.Raised), rounding - inset * .5f);

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
            var color = Anim.Lerp(Anim.Lerp(P.TextMuted, P.Text, hover * .6f), P.Text, selected);
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

    /// <summary>Width of the right-aligned control in a settings row.</summary>
    public static float ControlWidth => MathF.Min(S(210), ImGui.GetContentRegionAvail().X * .5f);

    /// <summary>
    /// Draws a settings-row label and leaves the cursor where a control of <paramref name="controlWidth"/>
    /// should go, with the item width already set. Use it to put any ImGui widget in the standard layout:
    /// <code>Ui.Row("Mode"); ImGui.Combo("##mode", ref mode, items, items.Length);</code>
    /// </summary>
    public static void Row(string label, float controlWidth = 0)
    {
        if (controlWidth <= 0) controlWidth = ControlWidth;
        var x0 = ImGui.GetCursorPosX();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Visible(label));
        ImGui.SameLine(x0 + width - controlWidth);
        ImGui.SetNextItemWidth(controlWidth);
    }

    /// <summary>A labeled dropdown.</summary>
    public static bool Combo(string label, ref int index, string[] items, string? description = null)
    {
        Row(label);
        // The frame is measured before BeginCombo, which moves into the popup window when open.
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());
        var draw = ImGui.GetWindowDrawList();
        var changed = false;
        var preview = index >= 0 && index < items.Length ? items[index] : "";
        var open = ImGui.BeginCombo("##" + label, preview, ImGuiComboFlags.NoArrowButton);
        var chevron = open ? Icons.ChevronUp : Icons.ChevronDown;
        var size = ImGui.CalcTextSize(chevron);
        draw.AddText(new Vector2(max.X - size.X - ImGui.GetStyle().FramePadding.X, min.Y + (max.Y - min.Y - size.Y) * .5f), Col(P.TextMuted), chevron);
        if (open)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (ImGui.Selectable(items[i], i == index)) { index = i; changed = true; }
                if (i == index) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        Description(description);
        return changed;
    }

    /// <summary>A labeled text field with placeholder text.</summary>
    public static bool TextInput(string label, ref string value, string hint = "", uint maxLength = 256, string? description = null)
    {
        Row(label);
        var changed = ImGui.InputTextWithHint("##" + label, hint, ref value, maxLength);
        Description(description);
        return changed;
    }

    /// <summary>A labeled number field that can be dragged or typed into (double-click).</summary>
    public static bool Number(string label, ref float value, float speed = .1f, float min = 0, float max = 0, string format = "%.2f", string? description = null)
    {
        Row(label);
        var changed = ImGui.DragFloat("##" + label, ref value, speed, min, max, format);
        Description(description);
        return changed;
    }

    /// <summary>Integer variant of <see cref="Number(string, ref float, float, float, float, string, string?)"/>.</summary>
    public static bool Number(string label, ref int value, float speed = .2f, int min = 0, int max = 0, string? description = null)
    {
        Row(label);
        var changed = ImGui.DragInt("##" + label, ref value, speed, min, max);
        Description(description);
        return changed;
    }

    /// <summary>A labeled color swatch that opens a picker.</summary>
    public static bool Color(string label, ref Vector4 color, bool alpha = true, string? description = null)
    {
        var swatch = ImGui.GetFrameHeight();
        Row(label, swatch * 1.6f);
        var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaPreviewHalf;
        if (alpha) flags |= ImGuiColorEditFlags.AlphaBar; else flags |= ImGuiColorEditFlags.NoAlpha;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(swatch * .8f - S(1), ImGui.GetStyle().FramePadding.Y));
        var changed = ImGui.ColorEdit4("##" + label, ref color, flags);
        ImGui.PopStyleVar();
        Description(description);
        return changed;
    }

    /// <summary>
    /// A labeled key picker. Click it, then press any key or mouse button. Escape cancels, Backspace clears.
    /// <paramref name="key"/> is a Windows virtual-key code (see <see cref="VirtualKey"/>); 0 means none.
    /// </summary>
    public static bool Keybind(string label, ref int key, string? description = null)
    {
        var width = ControlWidth;
        Row(label, width);
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
        var text = waiting ? "Press a key…" : Input.KeyName(key);
        if (Button($"{Icons.Keyboard}  {text}##{label}", waiting ? ButtonKind.Primary : ButtonKind.Secondary, new Vector2(width, 0)) && !waiting)
            _capturingKey = id;
        Description(description);
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

    private static void Description(string? description)
    {
        if (description is null) return;
        ImGui.PushFont(Fonts.Small);
        ImGui.PushStyleColor(ImGuiCol.Text, P.TextMuted);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - S(4));
        TextWrapped(description);
        ImGui.PopStyleColor();
        ImGui.PopFont();
    }

    private static float SmallLineHeight()
    {
        ImGui.PushFont(Fonts.Small);
        var height = ImGui.GetTextLineHeight();
        ImGui.PopFont();
        return height;
    }

    /// <summary>The visible part of an ImGui label (text before "##").</summary>
    public static string Visible(string label)
    {
        var index = label.IndexOf("##", StringComparison.Ordinal);
        return index < 0 ? label : label[..index];
    }
}
