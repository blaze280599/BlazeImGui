using System.Globalization;
using System.Numerics;
using ImGuiNET;

namespace BlazeImGui;

public static partial class Ui
{
    /// <summary>
    /// Colored text. Safe for any string: ImGui.NET's TextColored/TextWrapped/Text treat the string as a
    /// printf format, so a '%' in user text is swallowed. Prefer these helpers or ImGui.TextUnformatted.
    /// </summary>
    public static void Text(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    /// <summary>Text in a <see cref="Tone"/> color.</summary>
    public static void Text(string text, Tone tone) => Text(text, ToneColor(tone));

    /// <summary>Text wrapped at the right edge of the content region ('%'-safe).</summary>
    public static void TextWrapped(string text)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    /// <summary>A quiet section label that groups the rows below it.</summary>
    public static void Section(string title)
    {
        if (ImGui.GetCursorPosY() > ImGui.GetStyle().WindowPadding.Y + 1) ImGui.Dummy(new Vector2(0, S(6)));
        ImGui.PushFont(Fonts.SmallSemiBold);
        Text(title, P.TextMuted);
        ImGui.PopFont();
        ImGui.Dummy(new Vector2(0, S(2)));
    }

    /// <summary>
    /// Starts a rounded card that grows to fit its content. Always pair with <see cref="EndCard"/>.
    /// <paramref name="title"/> and <paramref name="icon"/> are optional.
    /// </summary>
    public static void BeginCard(string id, string? title = null, string? icon = null, float width = 0)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, P.Surface);
        ImGui.PushStyleColor(ImGuiCol.Border, P.Border);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(16), S(14)));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, S(10));
        ImGui.BeginChild(id, new Vector2(width, 0),
            ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
        if (title is null) return;
        ImGui.PushFont(Fonts.SemiBold);
        if (icon is not null)
        {
            Text(icon, P.TextMuted);
            ImGui.SameLine(0, S(8));
        }
        ImGui.TextUnformatted(title);
        ImGui.PopFont();
        // No spacer here: the title stays the last item, so Ui.SameLineRight(...) can put a badge or button beside it.
    }

    public static void EndCard()
    {
        ImGui.EndChild();
    }

    /// <summary>
    /// Lays content out in equal-width columns. Call <see cref="NextCell"/> before each cell and
    /// <see cref="EndGrid"/> only when this returns true.
    /// </summary>
    public static bool BeginGrid(string id, int columns)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(S(6), S(6)));
        var open = ImGui.BeginTable(id, columns, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX);
        ImGui.PopStyleVar();
        return open;
    }

    public static void NextCell() => ImGui.TableNextColumn();
    public static void EndGrid() => ImGui.EndTable();

    /// <summary>A hairline divider with breathing room.</summary>
    public static void Divider()
    {
        ImGui.Dummy(new Vector2(0, S(2)));
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.GetWindowDrawList().AddLine(start, start + new Vector2(width, 0), Col(P.Border), 1);
        ImGui.Dummy(new Vector2(width, S(6)));
    }

    /// <summary>Muted, wrapped helper text.</summary>
    public static void Hint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, P.TextMuted);
        TextWrapped(text);
        ImGui.PopStyleColor();
    }


    /// <summary>Continues on the same line and moves the cursor so an item <paramref name="width"/> wide ends at the right edge.</summary>
    public static void SameLineRight(float width)
    {
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0, ImGui.GetContentRegionAvail().X - width));
    }

    /// <summary>The size <see cref="Badge"/> will take for this text (for right-aligning with <see cref="SameLineRight"/>).</summary>
    public static Vector2 BadgeSize(string text)
    {
        ImGui.PushFont(Fonts.Small);
        var size = ImGui.CalcTextSize(text) + new Vector2(S(7), S(2)) * 2;
        ImGui.PopFont();
        return size;
    }

    /// <summary>A small rounded pill, inline with text.</summary>
    public static void Badge(string text, Tone tone = Tone.Neutral)
    {
        ImGui.PushFont(Fonts.Small);
        var size = ImGui.CalcTextSize(text);
        var padding = new Vector2(S(7), S(2));
        var box = size + padding * 2;
        var start = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        // Center on the item it follows when it shares the same top edge (after SameLine), otherwise
        // on a frame-height line, which matches AlignTextToFramePadding text and table rows.
        var previousMin = ImGui.GetItemRectMin();
        var previousMax = ImGui.GetItemRectMax();
        if (MathF.Abs(previousMin.Y - start.Y) < 1f && previousMax.Y > previousMin.Y)
            start.Y = (previousMin.Y + previousMax.Y - box.Y) * .5f;
        else
            start.Y += MathF.Max(0, (ImGui.GetFrameHeight() - box.Y) * .5f);
        var color = tone == Tone.Neutral ? P.TextMuted : ToneColor(tone);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + box, Col(color, Theme.IsDark ? .16f : .12f), box.Y * .5f);
        draw.AddText(start + padding, Col(color), text);
        ImGui.Dummy(new Vector2(box.X, MathF.Min(box.Y, lineHeight)));
        ImGui.PopFont();
    }

    /// <summary>A statistic tile: muted label, big value, optional colored change ("+12%").</summary>
    public static void Stat(string label, string value, string? change = null, Tone changeTone = Tone.Success, string? icon = null)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var height = S(78);
        var start = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + new Vector2(width, height), Col(P.Surface), S(10));
        draw.AddRect(start, start + new Vector2(width, height), Col(P.Border), S(10));
        var pad = new Vector2(S(14), S(12));
        var labelText = icon is null ? label : $"{icon}  {label}";
        draw.AddText(Fonts.Small, Fonts.Small.FontSize, start + pad, Col(P.TextMuted), labelText);
        var valuePos = start + pad + new Vector2(0, Fonts.Small.FontSize + S(8));
        draw.AddText(Fonts.Heading, Fonts.Heading.FontSize, valuePos, Col(P.Text), value);
        if (change is not null)
        {
            ImGui.PushFont(Fonts.Heading);
            var valueWidth = ImGui.CalcTextSize(value).X;
            ImGui.PopFont();
            var y = valuePos.Y + Fonts.Heading.FontSize - Fonts.Small.FontSize - S(1);
            draw.AddText(Fonts.SmallSemiBold, Fonts.SmallSemiBold.FontSize, new Vector2(valuePos.X + valueWidth + S(8), y), Col(ToneColor(changeTone)), change);
        }
        ImGui.Dummy(new Vector2(width, height));
    }

    /// <summary>
    /// A smooth line chart with a soft area fill and a hover readout. Fills the row width.
    /// <paramref name="min"/>/<paramref name="max"/> default to the data range.
    /// </summary>
    public static void LineChart(string id, ReadOnlySpan<float> values, float height = 0, float? min = null, float? max = null,
        string valueFormat = "0.0", Tone tone = Tone.Accent)
    {
        if (height <= 0) height = S(120);
        var width = ImGui.GetContentRegionAvail().X;
        var start = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var draw = ImGui.GetWindowDrawList();
        var color = ToneColor(tone);

        // Three faint guide lines.
        for (var i = 0; i <= 2; i++)
        {
            var y = start.Y + S(4) + (height - S(8)) * i / 2f;
            draw.AddLine(new Vector2(start.X, y), new Vector2(start.X + width, y), Col(P.Border), 1);
        }
        if (values.Length < 2) return;

        var low = min ?? float.MaxValue;
        var high = max ?? float.MinValue;
        if (min is null || max is null)
            foreach (var v in values)
            {
                if (min is null) low = MathF.Min(low, v);
                if (max is null) high = MathF.Max(high, v);
            }
        if (high - low < 1e-6f) high = low + 1;
        var top = start.Y + S(6);
        var bottom = start.Y + height - S(6);
        Span<Vector2> points = values.Length <= 512 ? stackalloc Vector2[values.Length] : new Vector2[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var x = start.X + width * i / (values.Length - 1);
            var y = bottom - (bottom - top) * Math.Clamp((values[i] - low) / (high - low), 0f, 1f);
            points[i] = new Vector2(x, y);
        }

        // The area is drawn as quads; anti-aliased fill is turned off so adjacent quads do not leave seams.
        var flags = draw.Flags;
        draw.Flags &= ~ImDrawListFlags.AntiAliasedFill;
        var fill = Col(color, Theme.IsDark ? .12f : .10f);
        for (var i = 0; i < points.Length - 1; i++)
            draw.AddQuadFilled(points[i], points[i + 1], new Vector2(points[i + 1].X, bottom), new Vector2(points[i].X, bottom), fill);
        draw.Flags = flags;
        draw.AddPolyline(ref points[0], points.Length, Col(color), ImDrawFlags.None, S(2));

        if (!hovered) return;
        var mouse = ImGui.GetIO().MousePos.X;
        var index = (int)MathF.Round((mouse - start.X) / width * (values.Length - 1));
        index = Math.Clamp(index, 0, values.Length - 1);
        var point = points[index];
        draw.AddLine(new Vector2(point.X, top), new Vector2(point.X, bottom), Col(P.TextFaint, .6f), 1);
        draw.AddCircleFilled(point, S(5), Col(color));
        draw.AddCircleFilled(point, S(2.5f), Col(P.Background));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(10), S(6)));
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(values[index].ToString(valueFormat, CultureInfo.InvariantCulture));
        ImGui.EndTooltip();
        ImGui.PopStyleVar();
    }
}
