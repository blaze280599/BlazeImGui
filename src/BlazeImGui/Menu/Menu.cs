using System.Numerics;
using ImGuiNET;

namespace BlazeImGui;

/// <summary>One sidebar entry and the content drawn when it is selected.</summary>
public sealed class MenuPage
{
    public required string Title { get; init; }
    public required string Icon { get; init; }
    /// <summary>Draws the page body. Called every frame while the page is visible.</summary>
    public required Action Draw { get; init; }
    /// <summary>Optional muted line under the page title.</summary>
    public string? Description { get; set; }
    /// <summary>Optional sidebar group label; consecutive pages with the same group are listed under it.</summary>
    public string? Group { get; init; }
    /// <summary>Optional pill shown at the end of the sidebar entry ("New", "3").</summary>
    public string? Badge { get; set; }
}

/// <summary>
/// The menu window: a sidebar with the brand, page navigation and the theme switch, and a content area with
/// the page header and the selected page. Pages fade/slide in when switched and the selection pill glides.
/// </summary>
public sealed class Menu
{
    private readonly List<MenuPage> _pages = new();
    private int _selected;
    private float _pageProgress = 1f;
    private Vector2? _restPosition;

    public string Title { get; set; } = "Blaze";
    public string? Subtitle { get; set; }
    /// <summary>Icon in the brand mark (an <see cref="Icons"/> constant).</summary>
    public string Logo { get; set; } = Icons.Flame;
    /// <summary>Initial size; the user can resize when <see cref="Resizable"/>.</summary>
    public Vector2 Size { get; set; } = new(820, 540);
    public Vector2 MinSize { get; set; } = new(620, 400);
    public bool Resizable { get; set; } = true;
    public float SidebarWidth { get; set; } = 210;
    public bool ShowThemeSwitch { get; set; } = true;
    public bool ShowCloseButton { get; set; } = true;
    /// <summary>Extra controls drawn at the right of the page header (before the close button).</summary>
    public Action? HeaderActions { get; set; }
    /// <summary>Extra content drawn at the bottom of the sidebar, above the theme switch.</summary>
    public Action? SidebarFooter { get; set; }

    /// <summary>Raised when the header close button is clicked; the overlay hides the menu.</summary>
    public event Action? CloseRequested;

    public IReadOnlyList<MenuPage> Pages => _pages;

    /// <summary>The selected page index. Setting it plays the page transition.</summary>
    public int Selected
    {
        get => _selected;
        set
        {
            value = Math.Clamp(value, 0, Math.Max(0, _pages.Count - 1));
            if (value == _selected) return;
            _selected = value;
            _pageProgress = 0f;
        }
    }

    public MenuPage AddPage(string title, string icon, Action draw, string? description = null, string? group = null)
    {
        var page = new MenuPage { Title = title, Icon = icon, Draw = draw, Description = description, Group = group };
        _pages.Add(page);
        return page;
    }

    /// <summary>Selects the first page with this title.</summary>
    public void Select(string title)
    {
        var index = _pages.FindIndex(p => p.Title == title);
        if (index >= 0) Selected = index;
    }

    /// <summary>Draws the window. <paramref name="visibility"/> is the open/close animation (0..1).</summary>
    public void Draw(float visibility, bool interactive)
    {
        var scale = Ui.Scale;
        var p = Theme.Colors;
        var viewport = ImGui.GetMainViewport();
        var size = Size * scale;
        ImGui.SetNextWindowSize(size, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(MinSize * scale, new Vector2(float.MaxValue));
        // The window rises slightly while it fades in; once open the user can drag it freely.
        var rise = (1f - Anim.EaseOutCubic(visibility)) * 14f * scale;
        if (_restPosition is { } rest && visibility < 1f)
            ImGui.SetNextWindowPos(rest + new Vector2(0, rise), ImGuiCond.Always);
        else if (_restPosition is null)
            ImGui.SetNextWindowPos(viewport.WorkPos + viewport.WorkSize * .5f, ImGuiCond.FirstUseEver, new Vector2(.5f));

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse;
        if (!Resizable) flags |= ImGuiWindowFlags.NoResize;
        if (!interactive) flags |= ImGuiWindowFlags.NoInputs;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.Begin($"{Title}##BlazeMenu", flags);
        ImGui.PopStyleVar(2);
        if (visibility >= 1f) _restPosition = ImGui.GetWindowPos();
        else _restPosition ??= ImGui.GetWindowPos();

        var origin = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var sidebar = SidebarWidth * scale;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + new Vector2(sidebar, windowSize.Y), Theme.U32(p.Sidebar),
            ImGui.GetStyle().WindowRounding, ImDrawFlags.RoundCornersLeft);
        draw.AddLine(origin + new Vector2(sidebar, 0), origin + new Vector2(sidebar, windowSize.Y), Theme.U32(p.Border));

        DrawSidebar(sidebar, windowSize.Y);
        ImGui.SetCursorPos(new Vector2(sidebar + 1, 0));
        DrawContent(windowSize - new Vector2(sidebar + 1, 0));
        ImGui.End();
    }

    private void DrawSidebar(float width, float height)
    {
        var scale = Ui.Scale;
        var p = Theme.Colors;
        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.BeginChild("##sidebar", new Vector2(width, height), ImGuiChildFlags.None,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
        var draw = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var origin = ImGui.GetWindowPos();

        // Brand: an accent tile with the logo, the title and an optional subtitle.
        var mark = 32f * scale;
        var markMin = origin + new Vector2(pad + 2 * scale, 20f * scale);
        draw.AddRectFilled(markMin, markMin + new Vector2(mark), Theme.U32(p.Accent), 9f * scale);
        ImGui.PushFont(Fonts.IconLarge);
        var logoSize = ImGui.CalcTextSize(Logo);
        ImGui.PopFont();
        draw.AddText(Fonts.IconLarge, Fonts.IconLarge.FontSize, markMin + (new Vector2(mark) - logoSize) * .5f, Theme.U32(p.OnAccent), Logo);
        var textX = markMin.X + mark + 11f * scale;
        if (Subtitle is null)
            draw.AddText(Fonts.SemiBold, Fonts.SemiBold.FontSize, new Vector2(textX, markMin.Y + (mark - Fonts.SemiBold.FontSize) * .5f), Theme.U32(p.Text), Title);
        else
        {
            draw.AddText(Fonts.SemiBold, Fonts.SemiBold.FontSize, new Vector2(textX, markMin.Y - 1f * scale), Theme.U32(p.Text), Title);
            draw.AddText(Fonts.Small, Fonts.Small.FontSize, new Vector2(textX, markMin.Y + mark - Fonts.Small.FontSize + 1f * scale), Theme.U32(p.TextMuted), Subtitle);
        }

        // Navigation. Items draw on channel 1 and the gliding selection pill on channel 0 beneath them.
        var y = 20f * scale + mark + 22f * scale;
        var itemHeight = 34f * scale;
        var itemWidth = width - pad * 2;
        var selectedY = 0f;
        string? group = null;
        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);
        for (var i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            if (page.Group != group)
            {
                group = page.Group;
                if (group is not null)
                {
                    if (i > 0) y += 10f * scale;
                    draw.AddText(Fonts.SmallSemiBold, Fonts.SmallSemiBold.FontSize, origin + new Vector2(pad + 10f * scale, y + 4f * scale), Theme.U32(p.TextFaint), group);
                    y += 26f * scale;
                }
            }
            ImGui.SetCursorPos(new Vector2(pad, y));
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##nav", new Vector2(itemWidth, itemHeight))) Selected = i;
            var hovered = ImGui.IsItemHovered();
            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var hover = Anim.Toggle(ImGui.GetID("##hover"), hovered && i != _selected, 18f);
            var active = Anim.Toggle(ImGui.GetID("##active"), i == _selected, 16f);
            ImGui.PopID();
            var min = origin + new Vector2(pad, y);
            if (hover > .001f) draw.AddRectFilled(min, min + new Vector2(itemWidth, itemHeight), Theme.U32(p.Field, hover * .55f), 8f * scale);
            var textY = min.Y + (itemHeight - ImGui.GetTextLineHeight()) * .5f;
            var iconColor = Anim.Lerp(Anim.Lerp(p.TextMuted, p.Text, hover), p.Accent, active);
            var labelColor = Anim.Lerp(Anim.Lerp(p.TextMuted, p.Text, hover), p.Text, active);
            draw.AddText(new Vector2(min.X + 11f * scale, textY), Theme.U32(iconColor), page.Icon);
            draw.AddText(active > .5f ? Fonts.Medium : Fonts.Regular, Fonts.Regular.FontSize, new Vector2(min.X + 40f * scale, textY), Theme.U32(labelColor), page.Title);
            if (page.Badge is not null)
            {
                ImGui.PushFont(Fonts.Small);
                var badgeSize = ImGui.CalcTextSize(page.Badge) + new Vector2(12f, 4f) * scale;
                ImGui.PopFont();
                var badgeMin = new Vector2(min.X + itemWidth - badgeSize.X - 8f * scale, min.Y + (itemHeight - badgeSize.Y) * .5f);
                draw.AddRectFilled(badgeMin, badgeMin + badgeSize, Theme.U32(p.Accent, .16f), badgeSize.Y * .5f);
                draw.AddText(Fonts.Small, Fonts.Small.FontSize, badgeMin + new Vector2(6f, 2f) * scale, Theme.U32(p.Accent), page.Badge);
            }
            if (i == _selected) selectedY = y;
            y += itemHeight + 2f * scale;
        }
        draw.ChannelsSetCurrent(0);
        if (_pages.Count > 0)
        {
            var pillY = Anim.To(ImGui.GetID("##pill"), selectedY, 18f);
            var pillMin = origin + new Vector2(pad, pillY);
            draw.AddRectFilled(pillMin, pillMin + new Vector2(itemWidth, itemHeight), Theme.U32(p.Field), 8f * scale);
        }
        draw.ChannelsMerge();

        // Footer: custom content, then the theme switch.
        var footer = height - pad - ImGui.GetFrameHeight();
        if (SidebarFooter is not null)
        {
            // Sits just above the theme switch, using the height it took on the previous frame.
            var bottom = ShowThemeSwitch ? footer - 10f * scale : height - pad;
            ImGui.SetCursorPos(new Vector2(pad, Math.Max(y + 8f * scale, bottom - _footerHeight)));
            ImGui.PushItemWidth(itemWidth);
            ImGui.BeginGroup();
            SidebarFooter();
            ImGui.EndGroup();
            ImGui.PopItemWidth();
            _footerHeight = ImGui.GetItemRectSize().Y;
        }
        if (ShowThemeSwitch)
        {
            ImGui.SetCursorPos(new Vector2(pad, footer));
            var mode = Theme.IsDark ? 1 : 0;
            if (Ui.Segmented("##theme", ref mode, [$"{Icons.Sun}  Light", $"{Icons.Moon}  Dark"], itemWidth))
                Theme.Set(mode == 1 ? ThemeMode.Dark : ThemeMode.Light);
        }
        ImGui.EndChild();
    }

    private void DrawContent(Vector2 size)
    {
        var scale = Ui.Scale;
        var p = Theme.Colors;
        ImGui.BeginChild("##content", size, ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
        var origin = ImGui.GetWindowPos();
        var draw = ImGui.GetWindowDrawList();
        var pad = 26f * scale;
        var headerHeight = 72f * scale;
        var page = _pages.Count > 0 ? _pages[_selected] : null;

        _pageProgress = Math.Min(1f, _pageProgress + Anim.Delta / .26f);
        var t = Anim.EaseOutCubic(_pageProgress);

        // Header: page title + description, actions on the right.
        if (page is not null)
        {
            var titleY = page.Description is null ? (headerHeight - Fonts.Heading.FontSize) * .5f : 17f * scale;
            var slide = (1f - t) * 6f * scale;
            draw.AddText(Fonts.Heading, Fonts.Heading.FontSize, origin + new Vector2(pad + slide, titleY), Theme.U32(p.Text, t), page.Title);
            if (page.Description is not null)
                draw.AddText(origin + new Vector2(pad + slide, titleY + Fonts.Heading.FontSize + 3f * scale), Theme.U32(p.TextMuted, t), page.Description);
        }
        var button = ImGui.GetFrameHeight();
        var x = size.X - pad + 6f * scale;
        if (ShowCloseButton)
        {
            x -= button;
            ImGui.SetCursorPos(new Vector2(x, (headerHeight - button) * .5f));
            if (Ui.IconButton(Icons.X, "##close", "Hide menu")) CloseRequested?.Invoke();
        }
        if (HeaderActions is not null)
        {
            // Actions are laid out right-to-left into a group measured on the previous frame.
            var actionsWidth = Anim.To(ImGui.GetID("##actionsWidth"), _lastActionsWidth, 30f);
            ImGui.SetCursorPos(new Vector2(x - actionsWidth - 4f * scale, (headerHeight - button) * .5f));
            ImGui.BeginGroup();
            HeaderActions();
            ImGui.EndGroup();
            _lastActionsWidth = ImGui.GetItemRectSize().X;
        }
        draw.AddLine(origin + new Vector2(0, headerHeight), origin + new Vector2(size.X, headerHeight), Theme.U32(p.Border));

        // Page body: fades and rises in after a page switch. Each page keeps its own scroll position.
        ImGui.SetCursorPos(new Vector2(0, headerHeight + 1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, 18f * scale));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * t);
        ImGui.BeginChild($"##page{_selected}", new Vector2(size.X, size.Y - headerHeight - 1),
            ImGuiChildFlags.AlwaysUseWindowPadding, ImGuiWindowFlags.NoBackground);
        ImGui.PopStyleVar(2);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * t);
        if (t < 1f) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (1f - t) * 10f * scale);
        page?.Draw();
        ImGui.Dummy(new Vector2(0, 6f * scale));
        ImGui.PopStyleVar();
        ImGui.EndChild();
        ImGui.EndChild();
    }

    private float _lastActionsWidth;
    private float _footerHeight;
}
