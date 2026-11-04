using System.Numerics;
using ImGuiNET;

namespace BlazeImGui.Demo;

/// <summary>A tour of every BlazeImGui widget plus the plain ImGui widgets under the theme.</summary>
internal sealed class DemoOverlay : BlazeOverlay
{
    private readonly bool _backdrop;

    // Visuals
    private bool _esp = true, _names = true, _boxes, _health = true, _hideUnfocused;
    private float _distance = 250f, _opacity = .8f;
    private int _fov = 90;
    private Vector4 _enemyColor = Palette.Hex("#FF6B6B"), _allyColor = Palette.Hex("#3DD68C"), _textColor = new(1, 1, 1, .9f);

    // Controls
    private bool _checkA = true, _checkB, _checkC = true;
    private float _smoothing = 4.5f, _labelScale = 1f;
    private int _quality = 1, _range = 250, _menuKey = (int)VirtualKey.Insert, _aimKey = (int)VirtualKey.XButton2;

    // Inputs
    private int _mode, _radio = 1, _listItem = 2;
    private string _name = "", _notes = "Multiline text\nworks too.";
    private float _drag = 12.5f;
    private int _dragInt = 64;

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
    private bool _crosshair, _watermark = true, _dirty;
    private ImFontPtr _hudFont;

    public DemoOverlay(ThemeMode theme, string? page, bool backdrop)
        : base(new OverlayOptions { Title = "blaze", Subtitle = "overlay", Version = "v1.0.0", Theme = theme })
    {
        _backdrop = backdrop;
        for (var i = 0; i < _series.Length; i++) _series[i] = Sample(i * .08f - _series.Length * .08f);

        var visuals = Menu.AddPage("Visuals", Icons.Eye, "Overlay");
        visuals.AddTab("General", DrawVisualsGeneral, "What is drawn over the game. Hover a setting to read about it here.");
        visuals.AddTab("Colors", DrawVisualsColors, "Colors used by the world drawings.");

        var controls = Menu.AddPage("Controls", Icons.Crosshair, "Widgets");
        controls.AddTab("Toggles", DrawToggles, "Checkbox rows for settings, compact checkboxes for several on one line.");
        controls.AddTab("Sliders", DrawSliders, "The value sits in the box, drag anywhere on the track. Left/Right step by one unit.");
        controls.AddTab("Buttons", DrawButtons, "Outlined by default; primary is filled with the ink color.");
        controls.AddTab("Keybinds", DrawKeybinds, "Click, then press a key. Esc cancels, Backspace clears.");

        var inputs = Menu.AddPage("Inputs", Icons.TextCursorInput, "Fields");
        inputs.AddTab("Fields", DrawFields, "Dropdowns, text and number fields.");
        inputs.AddTab("Choices", DrawChoices, "Native ImGui choices, themed.");

        var data = Menu.AddPage("Data", Icons.Table2, "Tables");
        data.AddTab("Table", DrawTable, "A sortable native ImGui table. Click a header to sort.");
        data.AddTab("Tree", DrawTree, "Tree nodes and collapsing headers.");

        var charts = Menu.AddPage("Charts", Icons.ChartSpline, "Live data");
        charts.AddTab("Live", DrawLive, "Ui.LineChart with a hover readout.");
        charts.AddTab("Native", DrawNative, "ImGui.PlotLines and PlotHistogram under the theme.");
        charts.Badge = true;

        var feedback = Menu.AddPage("Feedback", Icons.Bell, "Messages");
        feedback.AddTab("Toasts", DrawToasts, "Toasts stack in the corner and fade out on their own.");
        feedback.AddTab("Progress", DrawProgress, "Progress bars, a spinner and badges.");
        feedback.AddTab("Dialogs", DrawDialogs, "Modals and tooltips.");

        var settings = Menu.AddPage("Settings", Icons.Settings, "Menu");
        settings.AddTab("General", DrawSettings, "Theme, hotkeys and the config file.");
        settings.PinBottom = true;
        settings.Status = DrawConfigStatus;

        Menu.Status = DrawSessionStatus;
        if (page is not null) Menu.Select(page);
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
            var top = Palette.Hex("#1C1622");
            var bottom = Palette.Hex("#0F0E14");
            var t = ImGui.ColorConvertFloat4ToU32(top);
            var b = ImGui.ColorConvertFloat4ToU32(bottom);
            draw.AddRectFilledMultiColor(Vector2.Zero, display, t, t, b, b);
            // Exact marker pixels the screenshot tool checks for before saving a capture.
            draw.AddRectFilled(new Vector2(0, 0), new Vector2(6, 6), 0xFFFF00FF);
            draw.AddRectFilled(new Vector2(6, 0), new Vector2(12, 6), 0xFFFFFF00);
        }
        if (_watermark)
        {
            var text = $"blaze  ·  {ImGui.GetIO().Framerate:0} fps";
            ImGui.PushFont(_hudFont);
            var size = ImGui.CalcTextSize(text);
            ImGui.PopFont();
            var pos = new Vector2(16, 16);
            draw.AddRectFilled(pos, pos + size + new Vector2(20, 12), Theme.U32(Theme.Colors.Background, .94f), 6);
            draw.AddRect(pos, pos + size + new Vector2(20, 12), Theme.U32(Theme.Colors.Border), 6);
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

    // ------------------------------------------------------------------ status column

    private void DrawSessionStatus()
    {
        Ui.Caption("Session");
        Ui.Readout("Overlay", "running", Tone.Success);
        Ui.Readout("Frame rate", $"{ImGui.GetIO().Framerate:0} fps");
        Ui.Readout("Visuals", _esp ? "drawing" : "off", _esp ? Tone.Success : Tone.Neutral);
        Ui.Readout("Distance", $"{_distance:0} yd  (FOV {_fov})");
        Ui.Readout("Players", $"{_players.Count(p => p.InCombat)} / {_players.Count} in combat");
        Ui.Readout("Menu key", Input.KeyName(Options.MenuKey));
        Ui.Readout("Theme", Theme.IsDark ? "dark" : "light");
    }

    private void DrawConfigStatus()
    {
        Ui.Caption("Config");
        Ui.Readout("Menu key", Input.KeyName(_menuKey));
        Ui.Readout("File", "blaze.ini");
        Ui.Readout("Changes", _dirty ? "not saved" : "saved", _dirty ? Tone.Warning : Tone.Success);
    }

    // ------------------------------------------------------------------ pages

    private void DrawVisualsGeneral()
    {
        _dirty |= Ui.Toggle("Enable visuals", ref _esp, "Draw players and objects in the world.");
        ImGui.BeginDisabled(!_esp);
        _dirty |= Ui.Slider("Max distance", ref _distance, 10, 500, "0", "Players further away than this are not drawn. 150 - 300 suits most maps.");
        _dirty |= Ui.Slider("Field of view", ref _fov, 30, 180, "0", "How wide the drawn area is around the crosshair, in degrees.");
        _dirty |= Ui.Toggle("Names", ref _names, "A name tag above each player.");
        _dirty |= Ui.Toggle("Health bars", ref _health, "A thin bar beside each player, colored by health.");
        ImGui.EndDisabled();
        _dirty |= Ui.Toggle("Hide when game is unfocused", ref _hideUnfocused, "Stops drawing while another window has focus.");
    }

    private void DrawVisualsColors()
    {
        Ui.Color("Enemy", ref _enemyColor, description: "Boxes, names and the crosshair for enemies.");
        Ui.Color("Ally", ref _allyColor, description: "Boxes and names for your team.");
        Ui.Color("Labels", ref _textColor, description: "Click a swatch to open the picker.");
        Ui.Slider("Opacity", ref _opacity, 0, 1, "0%", "How see-through the world drawings are.");
    }

    private void DrawToggles()
    {
        Ui.Toggle("Bounding boxes", ref _boxes, "A box around every player.");
        Ui.Toggle("Crosshair", ref _crosshair, "A small cross in the middle of the screen (drawn in DrawOverlay).");
        ImGui.BeginDisabled();
        var off = false;
        Ui.Toggle("Disabled option", ref off, "Disabled controls fade out.");
        ImGui.EndDisabled();
        Ui.Section("Compact");
        Ui.Checkbox("Players", ref _checkA); ImGui.SameLine(0, 18);
        Ui.Checkbox("NPCs", ref _checkB); ImGui.SameLine(0, 18);
        Ui.Checkbox("Objects", ref _checkC);
    }

    private void DrawSliders()
    {
        Ui.Slider("Smoothing", ref _smoothing, 1, 10, "0.0", "Higher values ease movement more. 3 - 6 feels natural.");
        Ui.Slider("Max distance", ref _distance, 10, 500, "0", "Players further away than this are not drawn.");
        Ui.Slider("Opacity", ref _opacity, 0, 1, "0%", "How see-through the world drawings are.");
        Ui.Slider("Label size", ref _labelScale, .75f, 1.5f, "0.00'x'", "Snaps to the two decimals it shows; Left/Right step by 0.01.");
        Ui.Slider("Draw range", ref _range, 10, 500, "0 'yd'", "Drags snap to 10 yd here (too few pixels for every yard); Left/Right step by 1.");
    }

    private void DrawButtons()
    {
        if (Ui.Button("Save", ButtonKind.Secondary, new Vector2(Ui.ControlWidth, 0)))
            Notify.Show("Saved", "Your settings were saved.", Tone.Success);
        ImGui.Spacing();
        if (Ui.Button("Primary", ButtonKind.Primary, new Vector2(Ui.ControlWidth, 0))) Notify.Show("Primary", "A filled button.");
        ImGui.Spacing();
        Ui.Button("Ghost", ButtonKind.Ghost);
        ImGui.SameLine();
        if (Ui.Button($"{Icons.Trash2}  Delete", ButtonKind.Danger)) Notify.Show("Deleted", "Profile removed.", Tone.Danger);
        ImGui.SameLine();
        Ui.IconButton(Icons.Ellipsis, "##more", "More actions");
        ImGui.Spacing();
        Ui.Row("Quality");
        Ui.Segmented("##quality", ref _quality, ["Low", "Mid", "High", "Ultra"]);
    }

    private void DrawKeybinds()
    {
        Ui.Keybind("Toggle menu", ref _menuKey, "Click, then press a key. Esc cancels, Backspace clears.");
        Ui.Keybind("Hold to aim", ref _aimKey, "Mouse buttons work too.");
    }

    private void DrawFields()
    {
        Ui.Combo("Mode", ref _mode, ["Automatic", "Manual", "Disabled"], "How the overlay picks its target.");
        Ui.TextInput("Profile name", ref _name, "e.g. Raid night", description: "Shown in the config file.");
        Ui.Number("Offset", ref _drag, .1f, 0, 100, "%.1f", "Drag, or double-click to type.");
        Ui.Number("Pool size", ref _dragInt, 1, 1, 512);
    }

    private void DrawChoices()
    {
        ImGui.RadioButton("Nearest", ref _radio, 0); ImGui.SameLine(0, 18);
        ImGui.RadioButton("Health", ref _radio, 1); ImGui.SameLine(0, 18);
        ImGui.RadioButton("Crosshair", ref _radio, 2);
        ImGui.Spacing();
        if (ImGui.BeginListBox("##list", new Vector2(-1, 120)))
        {
            string[] items = ["Overview", "Players", "Quests", "Professions", "Camera", "Diagnostics"];
            for (var i = 0; i < items.Length; i++)
                if (ImGui.Selectable(items[i], _listItem == i)) _listItem = i;
            ImGui.EndListBox();
        }
        ImGui.Spacing();
        ImGui.InputTextMultiline("##notes", ref _notes, 1024, new Vector2(-1, 70));
    }

    private void DrawTable()
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Sortable |
                                      ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8, 7));
        var open = ImGui.BeginTable("players", 3, flags, new Vector2(0, 360));
        ImGui.PopStyleVar();
        if (!open) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.DefaultSort, 1.2f);
        ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.None, 1f);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.NoSort, 1f);
        ImGui.PushFont(Fonts.SmallSemiBold);
        ImGui.TableHeadersRow();
        ImGui.PopFont();

        var specs = ImGui.TableGetSortSpecs();
        if (specs.SpecsDirty)
        {
            var ascending = specs.Specs.SortDirection == ImGuiSortDirection.Ascending;
            Comparison<Player> compare = specs.Specs.ColumnIndex == 1
                ? (a, b) => a.Distance.CompareTo(b.Distance)
                : (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal);
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
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); Ui.Text($"{p.Distance:0.0} yd", Theme.Colors.TextMuted);
            ImGui.TableNextColumn(); Ui.Badge(p.InCombat ? "Combat" : "Idle", p.InCombat ? Tone.Danger : Tone.Neutral);
        }
        ImGui.EndTable();
    }

    private void DrawTree()
    {
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
            Ui.Hint("Collapsing headers, tree nodes and selectables all use the theme's colors.");
    }

    private void DrawLive()
    {
        if (Ui.BeginGrid("stats", 2))
        {
            Ui.NextCell(); Ui.Stat("Players", "128", "+12%", Tone.Success);
            Ui.NextCell(); Ui.Stat("Frame time", "2.4 ms", "-0.3", Tone.Success);
            Ui.EndGrid();
        }
        ImGui.Spacing();
        Ui.Section("Activity");
        Ui.LineChart("##live", _series, 150, 0, 100);
        Ui.Section("Success tone");
        Ui.LineChart("##success", _series.AsSpan(30), 60, 0, 100, tone: Tone.Success);
    }

    private void DrawNative()
    {
        Ui.Section("ImGui.PlotLines");
        ImGui.PlotLines("##lines", ref _series[0], _series.Length, 0, null, 0, 100, new Vector2(-1, 90));
        Ui.Section("ImGui.PlotHistogram");
        ImGui.PlotHistogram("##hist", ref _bars[0], _bars.Length, 0, null, 0, 14, new Vector2(-1, 90));
    }

    private void DrawToasts()
    {
        var width = new Vector2(Ui.ControlWidth, 0);
        if (Ui.Button("Info", ButtonKind.Secondary, width)) Notify.Show("Heads up", "Toasts stack in the corner and fade out on their own.");
        ImGui.Spacing();
        if (Ui.Button("Success", ButtonKind.Secondary, width)) Notify.Show("Connected", "Attached to the game process.", Tone.Success);
        ImGui.Spacing();
        if (Ui.Button("Warning", ButtonKind.Secondary, width)) Notify.Show("Offsets outdated", "Some features are disabled.", Tone.Warning);
        ImGui.Spacing();
        if (Ui.Button("Error", ButtonKind.Secondary, width)) Notify.Show("Read failed", "Retrying in 2 seconds.", Tone.Danger);
    }

    private void DrawProgress()
    {
        var t = (float)(ImGui.GetTime() * .15 % 1.0);
        Ui.Progress(t, "Downloading update");
        ImGui.Spacing();
        Ui.Progress(.72f, "Memory scan", Tone.Success);
        ImGui.Spacing();
        Ui.Spinner();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        Ui.Hint("Waiting for the game window…");
        ImGui.Spacing();
        Ui.Badge("Neutral"); ImGui.SameLine();
        Ui.Badge("Success", Tone.Success); ImGui.SameLine();
        Ui.Badge("Warning", Tone.Warning); ImGui.SameLine();
        Ui.Badge("Danger", Tone.Danger);
    }

    private void DrawDialogs()
    {
        if (Ui.Button("Open modal", ButtonKind.Primary, new Vector2(Ui.ControlWidth, 0))) ImGui.OpenPopup("Reset settings?");
        ImGui.Spacing();
        Ui.Button("Hover me", ButtonKind.Secondary, new Vector2(Ui.ControlWidth, 0));
        Ui.Tooltip("Tooltips fade in after a short delay and wrap long text automatically.");
        ImGui.Spacing();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Help marker");
        ImGui.SameLine();
        Ui.HelpMarker("Small inline hints for settings that need explaining.");
        DrawModal();
    }

    private static void DrawModal()
    {
        ImGui.SetNextWindowSize(new Vector2(360, 0));
        var open = true;
        if (!ImGui.BeginPopupModal("Reset settings?", ref open, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
            return;
        ImGui.PushFont(Fonts.Title);
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
        Ui.Keybind("Menu key", ref _menuKey, "The key that shows and hides this menu.");
        Ui.Row("Theme");
        var mode = Theme.IsDark ? 1 : 0;
        if (Ui.Segmented("##theme", ref mode, ["Light", "Dark"]))
            Theme.Set(mode == 1 ? ThemeMode.Dark : ThemeMode.Light);
        ImGui.Dummy(new Vector2(0, 8));
        Ui.Toggle("Watermark", ref _watermark, "Small HUD in the top-left corner (drawn in DrawOverlay).");
        var width = new Vector2(Ui.ControlWidth, 0);
        if (Ui.Button("Save config", ButtonKind.Secondary, width))
        {
            _dirty = false;
            Notify.Show("Saved", "Settings written to blaze.ini.", Tone.Success);
        }
        ImGui.Spacing();
        if (Ui.Button("Load config", ButtonKind.Secondary, width)) Notify.Show("Loaded", "Settings read from blaze.ini.");
        ImGui.Dummy(new Vector2(0, 8));
        if (Ui.Button("Close menu", ButtonKind.Secondary, width)) Menu.RequestClose();
    }

    private sealed record Player(string Name, string Class, int Level, int Health, float Distance, bool InCombat);
}
