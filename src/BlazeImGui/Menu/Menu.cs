using System.Numerics;
using ImGuiNET;

namespace BlazeImGui;

/// <summary>One entry of a page's tab list and the content drawn when it is selected.</summary>
public sealed class MenuTab
{
    public required string Title { get; init; }
    /// <summary>Draws the tab body. Called every frame while the tab is visible.</summary>
    public required Action Draw { get; init; }
    /// <summary>Shown under ABOUT in the status column until a control with a description is hovered.</summary>
    public string? About { get; set; }
}

/// <summary>One icon on the rail: a titled page with its own tabs and, optionally, its own status readout.</summary>
public sealed class MenuPage
{
    private readonly List<MenuTab> _tabs = new();
    private int _selectedTab;

    public required string Title { get; init; }
    public required string Icon { get; init; }
    /// <summary>Quiet uppercase line under the page title ("ASSIST").</summary>
    public string? Caption { get; set; }
    /// <summary>Draws the status column for this page; null falls back to <see cref="Menu.Status"/>.</summary>
    public Action? Status { get; set; }
    /// <summary>Puts the icon at the bottom of the rail (settings).</summary>
    public bool PinBottom { get; set; }
    /// <summary>When true, a small brand dot marks the rail icon (something new or needing attention).</summary>
    public bool Badge { get; set; }

    public IReadOnlyList<MenuTab> Tabs => _tabs;

    /// <summary>The selected tab index. Setting it plays the content transition.</summary>
    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            value = Math.Clamp(value, 0, Math.Max(0, _tabs.Count - 1));
            if (value == _selectedTab) return;
            _selectedTab = value;
            TabChanged?.Invoke();
        }
    }

    internal event Action? TabChanged;

    public MenuTab AddTab(string title, Action draw, string? about = null)
    {
        var tab = new MenuTab { Title = title, Draw = draw, About = about };
        _tabs.Add(tab);
        return tab;
    }

    /// <summary>Selects the first tab with this title.</summary>
    public void SelectTab(string title)
    {
        var index = _tabs.FindIndex(t => t.Title == title);
        if (index >= 0) SelectedTab = index;
    }
}

/// <summary>
/// The menu window, in four columns: an icon rail with the brand mark, the page's title and tabs with the brand
/// footer, the selected tab's controls, and a status column with live readouts and an ABOUT hint for the hovered
/// control. The rail tile and tab highlight glide; content fades in when the page or tab changes.
/// </summary>
public sealed class Menu
{
    private readonly List<MenuPage> _pages = new();
    private int _selected;
    private float _pageProgress = 1f;
    private float _navProgress = 1f;
    private float _aboutProgress = 1f;
    private (string? Title, string? Text) _aboutShown;
    private Vector2? _restPosition;
    // Where the user put the window, where it was last frame, and the viewport work area it was last fitted to.
    private (Vector2 Pos, Vector2 Size)? _wanted, _placed, _workArea;

    /// <summary>Brand name at the bottom of the tab column.</summary>
    public string Title { get; set; } = "Blaze";
    /// <summary>Small muted word after the brand name.</summary>
    public string? Subtitle { get; set; }
    /// <summary>Version after the subtitle ("v1.0.0").</summary>
    public string? Version { get; set; }
    /// <summary>The rail's brand mark. Null uses the first letter of <see cref="Title"/>.</summary>
    public string? Logo { get; set; }
    /// <summary>Initial size; the user can resize when <see cref="Resizable"/>.</summary>
    public Vector2 Size { get; set; } = new(1000, 600);
    public Vector2 MinSize { get; set; } = new(860, 500);
    public bool Resizable { get; set; } = true;
    public float RailWidth { get; set; } = 62;
    public float NavWidth { get; set; } = 200;
    /// <summary>Width of the status column; the controls column takes the rest of the window.</summary>
    public float StatusWidth { get; set; } = 280;
    /// <summary>
    /// Shows the status column. Without it the controls column takes the rest of the window and control
    /// descriptions are drawn under each control instead of under ABOUT.
    /// </summary>
    public bool ShowStatus { get; set; } = true;
    public string StatusTitle { get; set; } = "Status";
    /// <summary>Draws the status column for pages without their own <see cref="MenuPage.Status"/>.</summary>
    public Action? Status { get; set; }
    public string AboutTitle { get; set; } = "About";

    /// <summary>Raised by <see cref="RequestClose"/>; the overlay hides the menu.</summary>
    public event Action? CloseRequested;

    /// <summary>Asks the host to hide the menu (e.g. from a "Close menu" button).</summary>
    public void RequestClose() => CloseRequested?.Invoke();

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
            _navProgress = 0f;
            ResetAbout();
        }
    }

    /// <summary>Adds a rail page. Give it tabs with <see cref="MenuPage.AddTab"/>.</summary>
    public MenuPage AddPage(string title, string icon, string? caption = null)
    {
        var page = new MenuPage { Title = title, Icon = icon, Caption = caption };
        page.TabChanged += () =>
        {
            if (_pages.Count > _selected && _pages[_selected] != page) return;
            _pageProgress = 0f;
            ResetAbout();
        };
        _pages.Add(page);
        if (_pages.Count == 1) ResetAbout();
        return page;
    }

    /// <summary>Adds a page with a single tab of the same title.</summary>
    public MenuPage AddPage(string title, string icon, Action draw, string? caption = null)
    {
        var page = AddPage(title, icon, caption);
        page.AddTab(title, draw);
        return page;
    }

    /// <summary>Selects the first page with this title; "Page/Tab" also selects the tab.</summary>
    public void Select(string title)
    {
        var slash = title.IndexOf('/');
        var pageTitle = slash < 0 ? title : title[..slash];
        var index = _pages.FindIndex(p => p.Title == pageTitle);
        if (index < 0) return;
        Selected = index;
        if (slash >= 0) _pages[index].SelectTab(title[(slash + 1)..]);
    }

    private MenuPage? Page => _pages.Count > 0 ? _pages[_selected] : null;
    private MenuTab? Tab => Page is { Tabs.Count: > 0 } page ? page.Tabs[page.SelectedTab] : null;

    private void ResetAbout()
    {
        Ui.CurrentAbout = Tab?.About;
        Ui.CurrentAboutTitle = null;
    }

    /// <summary>Draws the window. <paramref name="visibility"/> is the open/close animation (0..1).</summary>
    public void Draw(float visibility, bool interactive)
    {
        var scale = Ui.Scale;
        var p = Theme.Colors;
        var viewport = ImGui.GetMainViewport();
        // Never larger than the viewport (small game windows); the minimum size gives way when it doesn't fit.
        var available = Vector2.Max(Vector2.One, viewport.WorkSize - new Vector2(24f * scale));
        var minimum = Vector2.Min(MinSize * scale, available);
        ImGui.SetNextWindowSize(Vector2.Clamp(Size * scale, minimum, available), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(minimum, available);
        // imgui.ini may restore a place saved for another screen size, and the screen (or the overlay's target
        // window) can change size at any time: the menu then goes back to where the user put it, as far as it fits,
        // instead of hanging off the edge or staying shrunk after a small window.
        var refit = false;
        if (_wanted is { } wanted && _workArea != (viewport.WorkPos, viewport.WorkSize))
        {
            refit = true;
            _workArea = (viewport.WorkPos, viewport.WorkSize);
            var fitted = Vector2.Clamp(wanted.Size, minimum, available);
            var end = viewport.WorkPos + viewport.WorkSize;
            _restPosition = new Vector2(Inside(wanted.Pos.X, fitted.X, viewport.WorkPos.X, end.X, 12f * scale),
                Inside(wanted.Pos.Y, fitted.Y, viewport.WorkPos.Y, end.Y, 12f * scale));
            ImGui.SetNextWindowSize(fitted, ImGuiCond.Always);
        }
        // The window rises slightly while it fades in; once open the user can drag it freely.
        var rise = (1f - Anim.EaseOutCubic(visibility)) * 14f * scale;
        if (_restPosition is { } rest && (visibility < 1f || refit))
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
        var size = ImGui.GetWindowSize();
        // A move or resize by the user (anything but a refit) becomes the place to go back to.
        var placed = (_restPosition.Value, size);
        if (!refit && placed != _placed) _wanted = placed;
        _placed = placed;
        DrawShadow(origin, size);

        _pageProgress = Math.Min(1f, _pageProgress + Anim.Delta / .26f);
        _navProgress = Math.Min(1f, _navProgress + Anim.Delta / .26f);
        var rail = RailWidth * scale;
        var nav = NavWidth * scale;
        var content = size.X - rail - nav - (ShowStatus ? StatusWidth * scale : 0);
        var draw = ImGui.GetWindowDrawList();
        foreach (var x in ShowStatus ? new[] { rail, rail + nav, rail + nav + content } : new[] { rail, rail + nav })
            draw.AddLine(origin + new Vector2(x, 0), origin + new Vector2(x, size.Y), Theme.U32(p.Border));

        DrawRail(rail, size.Y);
        DrawNav(rail, nav, size.Y);
        Ui.InlineDescriptions = !ShowStatus;
        if (Ui.CurrentAbout is null) ResetAbout();
        DrawContent(new Vector2(rail + nav + 1, 0), new Vector2(content - 1, size.Y));
        if (ShowStatus) DrawStatus(new Vector2(rail + nav + content + 1, 0), new Vector2(size.X - rail - nav - content - 1, size.Y));
        ImGui.End();
    }

    // A span already inside min..max stays put; otherwise it moves in, a margin from the edge when there is room.
    private static float Inside(float pos, float size, float min, float max, float margin)
    {
        if (pos >= min && pos + size <= max) return pos;
        margin = Math.Clamp((max - min - size) * .5f, 0f, margin);
        return Math.Max(min + margin, Math.Min(pos, max - size - margin));
    }

    // A soft shadow under the window, drawn behind every ImGui window.
    private static void DrawShadow(Vector2 origin, Vector2 size)
    {
        var draw = ImGui.GetBackgroundDrawList();
        var scale = Ui.Scale;
        var rounding = ImGui.GetStyle().WindowRounding;
        var strength = Theme.IsDark ? .09f : .028f;
        for (var i = 1; i <= 10; i++)
        {
            var grow = i * 2.4f * scale;
            var offset = new Vector2(0, 6f * scale);
            draw.AddRectFilled(origin - new Vector2(grow) + offset, origin + size + new Vector2(grow) + offset,
                Theme.U32(new Vector4(0, 0, 0, strength * (1f - i / 11f))), rounding + grow);
        }
    }

    private float TitleY => 17f * Ui.Scale;
    private float BodyTop => 54f * Ui.Scale;
    private float Pad => 18f * Ui.Scale;
    // The ABOUT text's bottom edge above the window's, level with the brand footer.
    private float AboutBottom => 30f * Ui.Scale;

    private void DrawRail(float width, float height)
    {
        var scale = Ui.Scale;
        var p = Theme.Colors;
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos();

        // Brand mark: a bold letter (or icon) with the brand dot at its baseline.
        var mark = Logo ?? (Title.Length > 0 ? Title[..1].ToUpperInvariant() : "");
        ImGui.PushFont(Fonts.Brand);
        var markSize = ImGui.CalcTextSize(mark);
        ImGui.PopFont();
        var dot = 2.6f * scale;
        var markPos = origin + new Vector2((width - markSize.X - dot * 2.4f) * .5f, 29f * scale - markSize.Y * .5f);
        draw.AddText(Fonts.Brand, Fonts.Brand.FontSize, markPos, Theme.U32(p.Text), mark);
        draw.AddCircleFilled(markPos + new Vector2(markSize.X + dot * 1.3f, markSize.Y * .78f), dot, Theme.U32(p.Brand));

        // Icons: top-aligned pages from the top, pinned pages from the bottom. The tile glides beneath them.
        var tile = 40f * scale;
        var pitch = 52f * scale;
        var top = 99f * scale;
        var bottom = height - 40f * scale;
        var tops = 0;
        var bottoms = _pages.Count(page => page.PinBottom);
        var selectedY = 0f;
        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);
        for (var i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            var cy = page.PinBottom ? bottom - --bottoms * pitch : top + tops++ * pitch;
            var min = new Vector2((width - tile) * .5f, cy - tile * .5f);
            ImGui.SetCursorPos(min);
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##rail", new Vector2(tile))) Selected = i;
            var hovered = ImGui.IsItemHovered();
            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var hover = Anim.Toggle(ImGui.GetID("##hover"), hovered && i != _selected, 18f);
            var active = Anim.Toggle(ImGui.GetID("##active"), i == _selected, 16f);
            ImGui.PopID();
            Ui.Tooltip(page.Title);
            var iconSize = Fonts.IconLarge.FontSize;
            var center = origin + min + new Vector2(tile * .5f);
            var color = Anim.Lerp(Anim.Lerp(p.TextMuted, p.Text, hover), p.OnAccent, active);
            ImGui.PushFont(Fonts.IconLarge);
            var glyph = ImGui.CalcTextSize(page.Icon);
            ImGui.PopFont();
            draw.AddText(Fonts.IconLarge, iconSize, center - glyph * .5f, Theme.U32(color), page.Icon);
            if (page.Badge) draw.AddCircleFilled(center + new Vector2(tile * .3f, -tile * .3f), 3f * scale, Theme.U32(p.Brand));
            if (i == _selected) selectedY = min.Y;
        }
        draw.ChannelsSetCurrent(0);
        if (_pages.Count > 0)
        {
            var y = Anim.To(ImGui.GetID("##railtile"), selectedY, 18f);
            var min = origin + new Vector2((width - tile) * .5f, y);
            draw.AddRectFilled(min, min + new Vector2(tile), Theme.U32(p.Accent), 9f * scale);
        }
        draw.ChannelsMerge();
    }

    private void DrawNav(float x, float width, float height)
    {
        var scale = Ui.Scale;
        var p = Theme.Colors;
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos() + new Vector2(x, 0);
        var page = Page;
        var t = Anim.EaseOutCubic(_navProgress);
        var slide = (1f - t) * 6f * scale;

        if (page is not null)
        {
            draw.AddText(Fonts.Title, Fonts.Title.FontSize, origin + new Vector2(Pad + slide, TitleY), Theme.U32(p.Text, t), page.Title);
            if (page.Caption is not null)
                draw.AddText(Fonts.Small, Fonts.Small.FontSize, origin + new Vector2(Pad + slide, 59f * scale), Theme.U32(p.TextFaint, t),
                    page.Caption.ToUpperInvariant());

            // Tabs. The selected one is in full text color on a soft pill that glides beneath the labels.
            var y = 102f * scale;
            var row = 29f * scale;
            var selectedY = y;
            draw.ChannelsSplit(2);
            draw.ChannelsSetCurrent(1);
            for (var i = 0; i < page.Tabs.Count; i++)
            {
                var tab = page.Tabs[i];
                ImGui.SetCursorPos(new Vector2(x + Pad * .5f, y));
                ImGui.PushID(_selected * 1000 + i);
                if (ImGui.InvisibleButton("##tab", new Vector2(width - Pad, row))) page.SelectedTab = i;
                var hovered = ImGui.IsItemHovered();
                if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                var hover = Anim.Toggle(ImGui.GetID("##hover"), hovered, 18f);
                var active = Anim.Toggle(ImGui.GetID("##active"), i == page.SelectedTab, 16f);
                ImGui.PopID();
                var color = Anim.Lerp(p.TextMuted, p.Text, MathF.Max(hover, active));
                var textY = y + (row - ImGui.GetTextLineHeight()) * .5f;
                draw.AddText(origin + new Vector2(Pad + slide, textY), Theme.U32(color, t), tab.Title);
                if (i == page.SelectedTab) selectedY = y;
                y += row;
            }
            draw.ChannelsSetCurrent(0);
            if (page.Tabs.Count > 0)
            {
                // Keyed per page so switching pages snaps the pill instead of gliding across pages.
                var pillY = Anim.To(ImGui.GetID($"##tabpill{_selected}"), selectedY, 18f);
                var min = origin + new Vector2(Pad * .5f + slide, pillY);
                draw.AddRectFilled(min, min + new Vector2(width - Pad, row), Theme.U32(p.Field, t), 6f * scale);
            }
            draw.ChannelsMerge();
        }

        // Footer: brand name, subtitle and version on one baseline, level with the bottom rail icon.
        var baseline = height - 40f * scale;
        var titleTop = baseline - Fonts.Title.FontSize * .5f;
        var footer = origin + new Vector2(Pad, titleTop);
        draw.AddText(Fonts.Title, Fonts.Title.FontSize, footer, Theme.U32(p.Text), Title);
        ImGui.PushFont(Fonts.Title);
        var cursor = footer.X + ImGui.CalcTextSize(Title).X + 8f * scale;
        ImGui.PopFont();
        var smallY = footer.Y + Fonts.Title.FontSize - Fonts.Small.FontSize - 1f * scale;
        if (Subtitle is not null)
        {
            draw.AddText(Fonts.Small, Fonts.Small.FontSize, new Vector2(cursor, smallY), Theme.U32(p.TextMuted), Subtitle);
            ImGui.PushFont(Fonts.Small);
            cursor += ImGui.CalcTextSize(Subtitle).X + 6f * scale;
            ImGui.PopFont();
        }
        if (Version is not null)
            draw.AddText(Fonts.SmallSemiBold, Fonts.SmallSemiBold.FontSize, new Vector2(cursor, smallY), Theme.U32(p.Text), Version);
    }

    private void DrawContent(Vector2 position, Vector2 size)
    {
        var scale = Ui.Scale;
        var p = Theme.Colors;
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos() + position;
        var tab = Tab;
        var t = Anim.EaseOutCubic(_pageProgress);
        if (tab is not null)
            draw.AddText(Fonts.Title, Fonts.Title.FontSize, origin + new Vector2(Pad + (1f - t) * 6f * scale, TitleY), Theme.U32(p.Text, t), tab.Title);

        // Body: fades and rises in after a switch. Each tab keeps its own scroll position.
        ImGui.SetCursorPos(position + new Vector2(0, BodyTop));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Pad, 6f * scale));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * t);
        ImGui.BeginChild($"##content{_selected}.{Page?.SelectedTab}", new Vector2(size.X, size.Y - BodyTop - 4f * scale),
            ImGuiChildFlags.AlwaysUseWindowPadding, ImGuiWindowFlags.NoBackground);
        ImGui.PopStyleVar(2);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * t);
        if (t < 1f) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (1f - t) * 10f * scale);
        tab?.Draw();
        ImGui.Dummy(new Vector2(0, 6f * scale));
        ImGui.PopStyleVar();
        ImGui.EndChild();
    }

    private void DrawStatus(Vector2 position, Vector2 size)
    {
        var scale = Ui.Scale;
        var p = Theme.Colors;
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos() + position;
        draw.AddText(Fonts.Title, Fonts.Title.FontSize, origin + new Vector2(Pad, TitleY), Theme.U32(p.Text), StatusTitle);

        // The ABOUT hint for the hovered control sits on the bottom edge, as tall as its text; the live readouts fill
        // the space above it.
        var about = Ui.CurrentAbout;
        var title = Ui.CurrentAboutTitle;
        var wrap = size.X - Pad * 2;
        var captionGap = 8f * scale;
        var titleGap = 3f * scale;
        var aboutTop = size.Y;
        if (about is not null)
        {
            ImGui.PushFont(Fonts.Small);
            var height = ImGui.GetTextLineHeight() + captionGap + ImGui.CalcTextSize(about, wrap).Y;
            ImGui.PopFont();
            if (title is not null)
            {
                ImGui.PushFont(Fonts.Medium);
                height += ImGui.CalcTextSize(title, wrap).Y + titleGap;
                ImGui.PopFont();
            }
            aboutTop = size.Y - AboutBottom - height;
        }

        var readoutsBottom = about is null ? size.Y - 4f * scale : aboutTop - 16f * scale;
        ImGui.SetCursorPos(position + new Vector2(0, BodyTop));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Pad, 6f * scale));
        ImGui.BeginChild("##status", new Vector2(size.X, MathF.Max(1, readoutsBottom - BodyTop)), ImGuiChildFlags.AlwaysUseWindowPadding,
            ImGuiWindowFlags.NoBackground);
        ImGui.PopStyleVar();
        (Page?.Status ?? Status)?.Invoke();
        ImGui.EndChild();

        if (about is null) return;
        // A new description fades in, as content does after a tab switch.
        if (_aboutShown != (title, about))
        {
            _aboutShown = (title, about);
            _aboutProgress = 0f;
        }
        _aboutProgress = Math.Min(1f, _aboutProgress + Anim.Delta / .2f);
        ImGui.SetCursorPos(position + new Vector2(Pad, aboutTop));
        ImGui.BeginGroup();
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
        ImGui.PushFont(Fonts.Small);
        Ui.Text(AboutTitle.ToUpperInvariant(), p.TextFaint);
        ImGui.PopFont();
        ImGui.Dummy(new Vector2(0, captionGap));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * Anim.EaseOutCubic(_aboutProgress));
        if (title is not null)
        {
            ImGui.PushFont(Fonts.Medium);
            Ui.Text(title, p.Text);
            ImGui.PopFont();
            ImGui.Dummy(new Vector2(0, titleGap));
        }
        ImGui.PushFont(Fonts.Small);
        Ui.Text(about, p.TextMuted);
        ImGui.PopFont();
        ImGui.PopStyleVar(2);
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
    }
}
