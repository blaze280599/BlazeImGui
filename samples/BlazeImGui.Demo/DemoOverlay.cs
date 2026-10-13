using System.Numerics;
using ImGuiNET;

namespace BlazeImGui.Demo;

/// <summary>A tour of every BlazeImGui widget plus the plain ImGui widgets under the theme.</summary>
internal sealed class DemoOverlay : BlazeOverlay
{
    private readonly bool _backdrop;

    // Controls
    private bool _esp = true, _names = true, _boxes, _health = true, _hideUnfocused;
    private bool _checkA = true, _checkB, _checkC = true;
    private float _distance = 250f, _opacity = .8f, _smoothing = 4.5f;
    private int _fov = 90, _quality = 1, _menuKey = (int)VirtualKey.Insert, _aimKey = (int)VirtualKey.XButton2;

    // Inputs
    private int _mode, _radio = 1, _listItem = 2;
    private string _name = "", _notes = "Multiline text\nworks too.";
    private float _drag = 12.5f;
    private int _dragInt = 64;
    private Vector4 _enemyColor = Palette.Hex("#FF6B6B"), _allyColor = Palette.Hex("#3DD68C"), _textColor = new(1, 1, 1, .9f);

    // Data
    private readonly List<Player> _players =
    [
        new("Aria", "Mage", 60, 4820, 32.4f, true), new("Brom", "Warrior", 58, 7310, 12.1f, false),
        new("Cyra", "Priest", 60, 3990, 48.9f, true), new("Dax", "Rogue", 55, 4105, 21.7f, false),
        new("Elowen", "Druid", 60, 5220, 64.0f, true), new("Fenn", "Hunter", 57, 4630, 88.3f, false),
        new("Garr", "Shaman", 59, 5010, 103.6f, true),
    ];
    private int _selectedRow = -1;

    // Charts
    private readonly float[] _series = new float[90];
    private readonly float[] _bars = [3, 7, 4, 9, 6, 11, 8, 5, 10, 7, 12, 9];
    private float _seriesClock;

    // Settings
    private int _accent;
    private static readonly (string Name, Vector4 Color)[] Accents =
    [
        ("Indigo", Palette.Hex("#6D8CFF")), ("Blue", Palette.Hex("#3B82F6")), ("Violet", Palette.Hex("#8B5CF6")),
        ("Teal", Palette.Hex("#14B8A6")), ("Green", Palette.Hex("#22C55E")), ("Amber", Palette.Hex("#F59E0B")),
        ("Rose", Palette.Hex("#F43F5E")),
    ];
    private bool _crosshair, _watermark = true;
    private ImFontPtr _hudFont;

    public DemoOverlay(ThemeMode theme, string? page, bool backdrop)
        : base(new OverlayOptions { Title = "Blaze", Subtitle = "ImGui overlay base", Logo = Icons.Flame, Theme = theme })
    {
        _backdrop = backdrop;
        for (var i = 0; i < _series.Length; i++) _series[i] = Sample(i * .08f - _series.Length * .08f);
        Menu.AddPage("Dashboard", Icons.LayoutDashboard, DrawDashboard, "Overview of the session", group: "Overview");
        Menu.AddPage("Controls", Icons.ToggleRight, DrawControls, "Toggles, sliders, buttons and keybinds", group: "Widgets");
        Menu.AddPage("Inputs", Icons.TextCursorInput, DrawInputs, "Dropdowns, fields, colors and choices", group: "Widgets");
        Menu.AddPage("Data", Icons.Table2, DrawData, "Tables, trees and tabs", group: "Widgets");
        Menu.AddPage("Charts", Icons.ChartSpline, DrawCharts, "Live line charts and native plots", group: "Widgets").Badge = "New";
        Menu.AddPage("Feedback", Icons.Bell, DrawFeedback, "Toasts, modals, tooltips and progress", group: "Widgets");
        Menu.AddPage("Settings", Icons.Settings, DrawSettings, "Appearance and hotkeys", group: "App");
        if (page is not null) Menu.Select(page);
        Menu.HeaderActions = () =>
        {
            if (Ui.IconButton(Icons.Search, "##search", "Search")) Notify.Show("Search", "Hook this up to your own command palette.");
        };
    }

    // Extra font sizes are added while the atlas is built; keep the returned font in a field.
    protected override void OnLoadFonts() => _hudFont = Fonts.Add(FontWeight.SemiBold, 14);

    protected override void OnUpdate()
    {
        // A smooth, slowly changing series for the live chart.
        _seriesClock += Anim.Delta;
        while (_seriesClock > .08f)
        {
            _seriesClock -= .08f;
            Array.Copy(_series, 1, _series, 0, _series.Length - 1);
            _series[^1] = Sample((float)ImGui.GetTime());
        }
    }

    private static float Sample(float t) => 50 + 18 * MathF.Sin(t * .9f) + 9 * MathF.Sin(t * 2.3f + 1) + 4 * MathF.Sin(t * 5.1f);

    protected override void DrawOverlay(ImDrawListPtr draw)
    {
        var display = ImGui.GetIO().DisplaySize;
        if (_backdrop)
        {
            // Only for screenshots: a quiet backdrop instead of whatever is behind the transparent overlay.
            var top = Theme.IsDark ? Palette.Hex("#2A2F3A") : Palette.Hex("#C9CFDA");
            var bottom = Theme.IsDark ? Palette.Hex("#15171C") : Palette.Hex("#E9ECF1");
            var t = ImGui.ColorConvertFloat4ToU32(top);
            var b = ImGui.ColorConvertFloat4ToU32(bottom);
            draw.AddRectFilledMultiColor(Vector2.Zero, display, t, t, b, b);
            // Exact marker pixels the screenshot tool checks for before saving a capture.
            draw.AddRectFilled(new Vector2(0, 0), new Vector2(6, 6), 0xFFFF00FF);
            draw.AddRectFilled(new Vector2(6, 0), new Vector2(12, 6), 0xFFFFFF00);
        }
        if (_watermark)
        {
            var text = $"{Icons.Flame}  Blaze  ·  {ImGui.GetIO().Framerate:0} fps";
            ImGui.PushFont(_hudFont);
            var size = ImGui.CalcTextSize(text);
            ImGui.PopFont();
            var pos = new Vector2(16, 16);
            draw.AddRectFilled(pos, pos + size + new Vector2(20, 12), Theme.U32(Theme.Colors.Background, .92f), 8);
            draw.AddRect(pos, pos + size + new Vector2(20, 12), Theme.U32(Theme.Colors.Border), 8);
            draw.AddText(_hudFont, _hudFont.FontSize, pos + new Vector2(10, 6), Theme.U32(Theme.Colors.Text), text);
        }
        if (_crosshair)
        {
            var c = display * .5f;
            var color = ImGui.ColorConvertFloat4ToU32(_enemyColor);
            draw.AddLine(c - new Vector2(8, 0), c + new Vector2(9, 0), color, 2);
            draw.AddLine(c - new Vector2(0, 8), c + new Vector2(0, 9), color, 2);
        }
    }

    // ------------------------------------------------------------------ pages

    private void DrawDashboard()
    {
        if (Ui.BeginGrid("stats", 3))
        {
            Ui.NextCell(); Ui.Stat("Players tracked", "128", "+12%", Tone.Success, Icons.Users);
            Ui.NextCell(); Ui.Stat("Frame time", "2.4 ms", "-0.3", Tone.Success, Icons.Gauge);
            Ui.NextCell(); Ui.Stat("Memory reads", "4.1k/s", "+8%", Tone.Warning, Icons.Cpu);
            Ui.EndGrid();
        }
        ImGui.Spacing();
        Ui.BeginCard("activity", "Activity", Icons.Activity);
        Ui.SameLineRight(Ui.BadgeSize("Live").X);
        Ui.Badge("Live", Tone.Success);
        Ui.LineChart("##activity", _series, 130, 0, 100);
        Ui.EndCard();
        ImGui.Spacing();
        if (Ui.BeginGrid("lower", 2))
        {
            Ui.NextCell();
            Ui.BeginCard("status", "Status", Icons.CircleCheck);
            Ui.Progress(.72f, "Memory scan");
            ImGui.Spacing();
            Ui.Progress(.38f, "Offsets verified", Tone.Warning);
            ImGui.Spacing();
            Ui.Progress(1f, "Renderer", Tone.Success);
            Ui.EndCard();
            Ui.NextCell();
            Ui.BeginCard("quick", "Quick actions", Icons.Zap);
            Ui.Toggle("Visuals", ref _esp);
            Ui.Toggle("Crosshair", ref _crosshair);
            if (Ui.Button($"{Icons.RefreshCw}  Reload", ButtonKind.Secondary, new Vector2(-1, 0)))
                Notify.Show("Reloaded", "Offsets and settings were reloaded.", Tone.Success);
            Ui.EndCard();
            Ui.EndGrid();
        }
    }

    private void DrawControls()
    {
        Ui.Section("Toggles");
        Ui.BeginCard("toggles");
        Ui.Toggle("Enable visuals", ref _esp, "Draw players and objects in the world");
        ImGui.BeginDisabled(!_esp);
        Ui.Toggle("Names", ref _names);
        Ui.Toggle("Bounding boxes", ref _boxes);
        Ui.Toggle("Health bars", ref _health);
        ImGui.EndDisabled();
        Ui.Toggle("Hide when game is unfocused", ref _hideUnfocused);
        Ui.EndCard();

        Ui.Section("Sliders");
        Ui.BeginCard("sliders");
        Ui.Slider("Max distance", ref _distance, 10, 500, "0 'yd'");
        Ui.Slider("Field of view", ref _fov, 30, 180, "0'°'");
        Ui.Slider("Opacity", ref _opacity, 0, 1, "0%");
        Ui.Slider("Smoothing", ref _smoothing, 1, 10, "0.0", "Higher values ease movement more");
        Ui.EndCard();

        Ui.Section("Buttons");
        Ui.BeginCard("buttons");
        if (Ui.Button($"{Icons.Save}  Save", ButtonKind.Primary)) Notify.Show("Saved", "Your settings were saved.", Tone.Success);
        ImGui.SameLine();
        Ui.Button($"{Icons.Copy}  Duplicate");
        ImGui.SameLine();
        Ui.Button("Ghost", ButtonKind.Ghost);
        ImGui.SameLine();
        if (Ui.Button($"{Icons.Trash2}  Delete", ButtonKind.Danger)) Notify.Show("Deleted", "Profile removed.", Tone.Danger);
        ImGui.SameLine();
        Ui.IconButton(Icons.Ellipsis, "##more", "More actions");
        ImGui.Spacing();
        Ui.Segmented("##quality", ref _quality, ["Low", "Balanced", "High", "Ultra"]);
        ImGui.Spacing();
        Ui.Checkbox("Players", ref _checkA); ImGui.SameLine(0, 18);
        Ui.Checkbox("NPCs", ref _checkB); ImGui.SameLine(0, 18);
        Ui.Checkbox("Objects", ref _checkC);
        Ui.EndCard();

        Ui.Section("Keybinds");
        Ui.BeginCard("keys");
        Ui.Keybind("Toggle menu", ref _menuKey, "Click, then press a key. Esc cancels, Backspace clears.");
        Ui.Keybind("Hold to aim", ref _aimKey);
        Ui.EndCard();
    }

    private void DrawInputs()
    {
        Ui.Section("Fields");
        Ui.BeginCard("fields");
        Ui.Combo("Mode", ref _mode, ["Automatic", "Manual", "Disabled"]);
        Ui.TextInput("Profile name", ref _name, "e.g. Raid night");
        Ui.Number("Offset", ref _drag, .1f, 0, 100, "%.1f");
        Ui.Number("Pool size", ref _dragInt, 1, 1, 512);
        Ui.Row("Search");
        ImGui.InputTextWithHint("##search", $"{Icons.Search}  Filter…", ref _name, 64);
        Ui.EndCard();

        Ui.Section("Colors");
        Ui.BeginCard("colors");
        Ui.Color("Enemy", ref _enemyColor);
        Ui.Color("Ally", ref _allyColor);
        Ui.Color("Labels", ref _textColor, description: "Click a swatch to open the picker");
        Ui.EndCard();

        Ui.Section("Choices");
        Ui.BeginCard("choices");
        ImGui.RadioButton("Nearest", ref _radio, 0); ImGui.SameLine(0, 18);
        ImGui.RadioButton("Lowest health", ref _radio, 1); ImGui.SameLine(0, 18);
        ImGui.RadioButton("Crosshair", ref _radio, 2);
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginListBox("##list", new Vector2(-1, 120)))
        {
            string[] items = ["Overview", "Players", "Quests", "Professions", "Camera", "Diagnostics"];
            for (var i = 0; i < items.Length; i++)
                if (ImGui.Selectable(items[i], _listItem == i)) _listItem = i;
            ImGui.EndListBox();
        }
        ImGui.Spacing();
        ImGui.InputTextMultiline("##notes", ref _notes, 1024, new Vector2(-1, 70));
        Ui.EndCard();
    }

    private void DrawData()
    {
        if (ImGui.BeginTabBar("data-tabs"))
        {
            if (ImGui.BeginTabItem("Table"))
            {
                ImGui.Spacing();
                DrawTable();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Tree"))
            {
                ImGui.Spacing();
                if (ImGui.TreeNodeEx("World", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
                {
                    if (ImGui.TreeNodeEx("Players", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
                    {
                        foreach (var p in _players.Take(3))
                            ImGui.TreeNodeEx($"{Icons.User}  {p.Name}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanAvailWidth);
                        ImGui.TreePop();
                    }
                    if (ImGui.TreeNodeEx("Objects", ImGuiTreeNodeFlags.SpanAvailWidth))
                    {
                        ImGui.TreeNodeEx($"{Icons.Box}  Chest", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                        ImGui.TreePop();
                    }
                    ImGui.TreePop();
                }
                ImGui.Spacing();
                if (ImGui.CollapsingHeader("Collapsing header", ImGuiTreeNodeFlags.DefaultOpen))
                    Ui.Hint("Collapsing headers, tree nodes and selectables all use the theme's hover and accent colors.");
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Details"))
            {
                ImGui.Spacing();
                var p = _selectedRow >= 0 ? _players[_selectedRow] : _players[0];
                Ui.BeginCard("details", p.Name, Icons.User);
                Ui.Hint($"{p.Class} · Level {p.Level}");
                ImGui.Spacing();
                Ui.Progress(p.Health / 8000f, "Health", Tone.Success);
                Ui.EndCard();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawTable()
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Sortable |
                                      ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(10, 8));
        var open = ImGui.BeginTable("players", 5, flags, new Vector2(0, 330));
        ImGui.PopStyleVar();
        if (!open) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.DefaultSort, 1.4f);
        ImGui.TableSetupColumn("Class", ImGuiTableColumnFlags.None, 1.1f);
        ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.None, .7f);
        ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.None, 1f);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.NoSort, 1f);
        ImGui.PushFont(Fonts.SmallSemiBold);
        ImGui.TableHeadersRow();
        ImGui.PopFont();

        var specs = ImGui.TableGetSortSpecs();
        if (specs.SpecsDirty)
        {
            var column = specs.Specs.ColumnIndex;
            var ascending = specs.Specs.SortDirection == ImGuiSortDirection.Ascending;
            Comparison<Player> compare = column switch
            {
                1 => (a, b) => string.Compare(a.Class, b.Class, StringComparison.Ordinal),
                2 => (a, b) => a.Level.CompareTo(b.Level),
                3 => (a, b) => a.Distance.CompareTo(b.Distance),
                _ => (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal),
            };
            _players.Sort((a, b) => ascending ? compare(a, b) : compare(b, a));
            specs.SpecsDirty = false;
        }

        for (var i = 0; i < _players.Count; i++)
        {
            var p = _players[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (ImGui.Selectable($"{p.Name}##row{i}", _selectedRow == i, ImGuiSelectableFlags.SpanAllColumns)) _selectedRow = i;
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); Ui.Text(p.Class, Theme.Colors.TextMuted);
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted(p.Level.ToString());
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted($"{p.Distance:0.0} yd");
            ImGui.TableNextColumn(); Ui.Badge(p.InCombat ? "Combat" : "Idle", p.InCombat ? Tone.Danger : Tone.Neutral);
        }
        ImGui.EndTable();
    }

    private void DrawCharts()
    {
        Ui.BeginCard("live", "Live series", Icons.ChartSpline);
        Ui.LineChart("##live", _series, 150, 0, 100);
        Ui.EndCard();
        ImGui.Spacing();
        if (Ui.BeginGrid("native", 2))
        {
            Ui.NextCell();
            Ui.BeginCard("plotlines", "ImGui.PlotLines");
            ImGui.PlotLines("##lines", ref _series[0], _series.Length, 0, null, 0, 100, new Vector2(-1, 90));
            Ui.EndCard();
            Ui.NextCell();
            Ui.BeginCard("histogram", "ImGui.PlotHistogram");
            ImGui.PlotHistogram("##hist", ref _bars[0], _bars.Length, 0, null, 0, 14, new Vector2(-1, 90));
            Ui.EndCard();
            Ui.EndGrid();
        }
        ImGui.Spacing();
        Ui.BeginCard("tones", "Tones");
        Ui.LineChart("##success", _series.AsSpan(30), 60, 0, 100, tone: Tone.Success);
        Ui.EndCard();
    }

    private void DrawFeedback()
    {
        Ui.Section("Notifications");
        Ui.BeginCard("toasts");
        if (Ui.Button($"{Icons.Info}  Info")) Notify.Show("Heads up", "Toasts stack in the corner and fade out on their own.");
        ImGui.SameLine();
        if (Ui.Button($"{Icons.CircleCheck}  Success")) Notify.Show("Connected", "Attached to the game process.", Tone.Success);
        ImGui.SameLine();
        if (Ui.Button($"{Icons.TriangleAlert}  Warning")) Notify.Show("Offsets outdated", "Some features are disabled.", Tone.Warning);
        ImGui.SameLine();
        if (Ui.Button($"{Icons.CircleX}  Error")) Notify.Show("Read failed", "Retrying in 2 seconds.", Tone.Danger);
        Ui.EndCard();

        Ui.Section("Dialogs & tooltips");
        Ui.BeginCard("dialogs");
        if (Ui.Button("Open modal", ButtonKind.Primary)) ImGui.OpenPopup("Reset settings?");
        ImGui.SameLine();
        Ui.Button("Hover me");
        Ui.Tooltip("Tooltips fade in after a short delay and wrap long text automatically.");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Help marker");
        ImGui.SameLine();
        Ui.HelpMarker("Small inline hints for settings that need explaining.");
        DrawModal();
        Ui.EndCard();

        Ui.Section("Progress & status");
        Ui.BeginCard("progress");
        var t = (float)(ImGui.GetTime() * .15 % 1.0);
        Ui.Progress(t, "Downloading update");
        ImGui.Spacing();
        Ui.Spinner();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        Ui.Hint("Waiting for the game window…");
        ImGui.Spacing();
        Ui.Badge("Neutral"); ImGui.SameLine();
        Ui.Badge("Accent", Tone.Accent); ImGui.SameLine();
        Ui.Badge("Success", Tone.Success); ImGui.SameLine();
        Ui.Badge("Warning", Tone.Warning); ImGui.SameLine();
        Ui.Badge("Danger", Tone.Danger);
        Ui.EndCard();
    }

    private static void DrawModal()
    {
        ImGui.SetNextWindowSize(new Vector2(360, 0));
        var open = true;
        if (!ImGui.BeginPopupModal("Reset settings?", ref open, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
            return;
        ImGui.PushFont(Fonts.SemiBold);
        ImGui.TextUnformatted("Reset settings?");
        ImGui.PopFont();
        Ui.Hint("Every option goes back to its default. This cannot be undone.");
        ImGui.Spacing();
        var width = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2;
        if (Ui.Button("Cancel", ButtonKind.Secondary, new Vector2(width, 0))) ImGui.CloseCurrentPopup();
        ImGui.SameLine();
        if (Ui.Button("Reset", ButtonKind.Danger, new Vector2(width, 0)))
        {
            Notify.Show("Settings reset", tone: Tone.Warning);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawSettings()
    {
        Ui.Section("Appearance");
        Ui.BeginCard("appearance");
        Ui.Row("Theme");
        var mode = Theme.IsDark ? 1 : 0;
        if (Ui.Segmented("##theme-setting", ref mode, [$"{Icons.Sun}  Light", $"{Icons.Moon}  Dark"]))
            Theme.Set(mode == 1 ? ThemeMode.Dark : ThemeMode.Light);
        Ui.Row("Accent", Accents.Length * 26f);
        DrawAccentSwatches();
        Ui.Toggle("Watermark", ref _watermark, "Small HUD in the top-left corner (drawn in DrawOverlay)");
        Ui.Toggle("Crosshair", ref _crosshair);
        Ui.EndCard();

        Ui.Section("Hotkeys");
        Ui.BeginCard("hotkeys");
        ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Show / hide menu");
        Ui.SameLineRight(Ui.BadgeSize(Input.KeyName(Options.MenuKey)).X); Ui.Badge(Input.KeyName(Options.MenuKey));
        ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Exit overlay");
        Ui.SameLineRight(Ui.BadgeSize(Input.KeyName(Options.ExitKey)).X); Ui.Badge(Input.KeyName(Options.ExitKey));
        Ui.EndCard();

        Ui.Section("About");
        Ui.BeginCard("about");
        Ui.Hint("BlazeImGui · Inter + Lucide icons · ImGui.NET 1.91.6 on ClickableTransparentOverlay.");
        Ui.EndCard();
    }

    private void DrawAccentSwatches()
    {
        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var size = ImGui.GetFrameHeight();
        for (var i = 0; i < Accents.Length; i++)
        {
            var min = start + new Vector2(i * 26f, 0);
            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##accent", new Vector2(22, size)))
            {
                _accent = i;
                Theme.SetAccent(Accents[i].Color);
            }
            var hovered = ImGui.IsItemHovered();
            ImGui.PopID();
            if (hovered) Ui.Tooltip(Accents[i].Name);
            var center = min + new Vector2(11, size * .5f);
            var selected = Anim.Toggle(ImGui.GetID($"##accent-sel{i}"), _accent == i, 16f);
            if (selected > .01f) draw.AddCircle(center, 10.5f, Theme.U32(Accents[i].Color, selected), 32, 1.5f);
            draw.AddCircleFilled(center, Anim.Lerp(8f, 6.5f, selected) + (hovered ? .5f : 0), Theme.U32(Accents[i].Color));
        }
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(Accents.Length * 26f, size));
    }

    private sealed record Player(string Name, string Class, int Level, int Health, float Distance, bool InCombat);
}
