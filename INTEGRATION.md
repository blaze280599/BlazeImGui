# BlazeImGui — Integration Guide for AI Agents

This file tells an AI coding agent (or a human) exactly how to add the BlazeImGui menu to a C# overlay
project. **Native C++ / Dear ImGui project (a hook, an internal overlay)?** Use the C++ port instead:
follow [cpp/README.md](cpp/README.md). **Rust project?** Use the Rust port: follow [rust/README.md](rust/README.md). Follow the steps in order. Every API named here exists; if something is not listed, use plain
ImGui.NET — it is themed automatically.

> **Source of this library:** `C:\Users\blazi\Desktop\AI Projects\BlazeImGui` · https://gitlab.com/Blvckberry/BlazeImGui
> **Library projects:** `src\BlazeImGui` (menu, widgets, theme, fonts) and `src\BlazeImGui.Overlay` (the
> `BlazeOverlay` window) · **Working example:** `samples\BlazeImGui.Demo\DemoOverlay.cs`
>
> **Rule:** projects use the **NuGet packages** from a local feed committed in their own repository (section 2),
> so every project builds on its own from a plain clone. Never reference this folder from another repository,
> and don't copy the library source into it.

---

## 0. What you get

* `BlazeOverlay` — an abstract base class over `ClickableTransparentOverlay.Overlay`. It handles fonts,
  theme, hotkeys (Insert = show/hide menu, End = exit), menu fade in/out, hiding from the taskbar,
  covering the screen or following a game window, and toast notifications.
* `Menu` — a four-column window: an icon rail (brand mark, one icon per page, settings pinned at the bottom),
  the page's title and tabs (brand name and version at the bottom), the selected tab's controls, and a status
  column with live readouts plus an ABOUT hint for the hovered control. Selection glides, content fades in.
* `Ui` — animated, themed widgets (toggles, sliders, buttons, segmented control, combo, inputs, color
  pickers, keybinds, cards, sections, grids, badges, stats, progress, spinner, tooltips, line chart).
* `Theme` — light (default) + dark palettes with an animated cross-fade; the accent is an ink color
  (near-black / near-white) and a pink `Brand` dot marks the logo.
* `Fonts` — Inter (Regular/Medium/SemiBold, plus Bold on request) with the Lucide icon font merged into every size.
* `Icons` — 1,544 Lucide icon constants (`Icons.Settings`, `Icons.Eye`, …).
* `Notify` — toast notifications, callable from any thread.

Fonts are embedded in `BlazeImGui.dll`; nothing needs copying.

There are two packages (both in namespace `BlazeImGui`):

| Package | Contains | Use it when |
|---|---|---|
| **`BlazeImGui.Overlay`** | `BlazeOverlay`, `OverlayOptions`, `WindowTarget`; depends on `BlazeImGui` and ClickableTransparentOverlay | the app is its own transparent overlay window (the usual case; sections 3–11) |
| **`BlazeImGui`** | everything else above; depends only on ImGui.NET | the host already owns the ImGui context and renderer (an injected hook, an engine): section 2b |

## 1. Requirements (check the target project first)

| Requirement | Value |
|---|---|
| Target framework | `net8.0-windows` (or later Windows TFM) |
| Platform | `x64` (`<Platforms>x64</Platforms>` and `<PlatformTarget>x64</PlatformTarget>`) |
| ImGui.NET | **1.91.6.1** — must match exactly if the project also references it |
| ClickableTransparentOverlay | **11.1.0** (`BlazeImGui.Overlay` only) — must match exactly if the project also references it |

The packages pin their dependencies to these exact versions, so they flow in transitively and the target
needs no `PackageReference` of its own for them. If the target already references them at **different
versions**, restore fails with `NU1605` (package downgrade): change the target's versions to the ones
above, or remove those references.

## 2. Add the package (default)

The library ships as two NuGet packages: `BlazeImGui.<version>.nupkg` (~1.2 MB: the DLL with embedded fonts,
XML docs for IntelliSense, and the font licenses) and `BlazeImGui.Overlay.<version>.nupkg` (~20 KB). The target
keeps them in a `packages` folder in its own repository, so it builds from a plain `git clone` on any machine with no BlazeImGui folder next to it.
Do **not** copy `src\BlazeImGui` into the target, point a `ProjectReference` outside the target repository,
or use a git submodule unless the user asks.

1. **Pack them into the target's feed** from this repository (builds both packages into `artifacts\`, copies
   them into the folder and removes older versions there):

   ```powershell
   & "C:\Users\blazi\Desktop\AI Projects\BlazeImGui\tools\pack.ps1" -Into "<target-repo>\packages"
   ```

   If this folder is not available, clone https://github.com/blaze280599/BlazeImGui into a temporary folder
   and run its `tools\pack.ps1` the same way.

2. **Add a `nuget.config`** at the target repository root (next to the `.sln`), or add the `<add …>` line to
   the one already there. The path is relative to the `nuget.config` file; nuget.org stays available.

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <add key="local" value="packages" />
     </packageSources>
   </configuration>
   ```

3. **Reference one package** from the app's `.csproj`, using the version the pack script printed:
   `BlazeImGui.Overlay` for a standalone overlay (it brings `BlazeImGui` with it), or `BlazeImGui` alone for a
   host that owns its ImGui context (section 2b).

   ```xml
   <ItemGroup>
     <!-- BlazeOverlay + the menu, theme, fonts and widgets. The packages are in packages\ (see nuget.config). -->
     <PackageReference Include="BlazeImGui.Overlay" Version="1.2.2" />
   </ItemGroup>
   ```

4. **Commit the packages.** Many `.gitignore` templates (including Visual Studio's) ignore `*.nupkg` and
   `packages/`. Check with `git check-ignore -v packages\BlazeImGui.1.2.2.nupkg`; if it prints a rule, add these
   lines to the end of the target's `.gitignore` (adjust the folder if the feed is not at the root):

   ```gitignore
   !packages/
   !packages/BlazeImGui.*.nupkg
   ```

   A project that references only `BlazeImGui` may delete `BlazeImGui.Overlay.*.nupkg` from its feed.

   The `.gitignore` must also cover `**/bin/`, `**/obj/` and `imgui.ini`.

5. `dotnet build` once to confirm the package restores before writing UI code.

6. **Prove it is self-contained** after committing: clone the repository into an empty temporary folder
   and build there — it must succeed with no BlazeImGui folder beside it:

   ```powershell
   git clone <target-repo> "$env:TEMP\clone-check"; dotnet build "$env:TEMP\clone-check\<path-to-app>.csproj"
   ```

**Updating** a project to a newer BlazeImGui: run the pack script with `-Into` again (it replaces the old
`.nupkg` files), set the new `Version` on the `PackageReference`, build, and commit both.

**Releasing a change to the library:** edit `src\BlazeImGui` or `src\BlazeImGui.Overlay` here, then **bump
`<Version>`** in `src\Directory.Build.props` (shared by both packages; or pass `tools\pack.ps1 -Version 1.2.0`).
NuGet caches packages by version in `%USERPROFILE%\.nuget\packages`, so re-packing the same version gives
projects the old copy. Fixes go here and ship as a new version; never patch the library inside a target project.

## 2b. A host that owns the ImGui context (no `BlazeOverlay`)

Reference only the `BlazeImGui` package and do what `BlazeOverlay` would. The host's ImGui must be the version
ImGui.NET 1.91.6.1 binds (Dear ImGui 1.91.6), since both sides share one context.

```csharp
var menu = new Menu { Title = "mytool" };                 // pages and tabs as in section 3, on menu instead of Menu
menu.AddPage("Visuals", Icons.Eye, "Overlay").AddTab("General", DrawVisuals);

// Once per ImGui context, after it exists and before the host builds/uploads its font atlas:
Anim.Reset(); Ui.ResetState(); Notify.Clear();            // only needed when the host replaces a context
Ui.Scale = 1f;
Fonts.Load();                                             // Fonts.Load(bodySize, scale) for other sizes
Theme.Set(ThemeMode.Light, animate: false);
Theme.ApplyStyle();

// Every frame, between the host's NewFrame and Render:
Input.Update();
Theme.Update();
var alpha = Anim.To(ImGui.GetID("##menu-fade"), visible ? 1f : 0f, visible ? 14f : 20f, 0f);
if (alpha > .002f)
{
    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, alpha);
    menu.Draw(alpha, visible);
    ImGui.PopStyleVar();
}
Notify.Draw();
```

The host handles its own show/hide key: skip it while `Ui.IsCapturingKey` or `ImGui.GetIO().WantTextInput`.
A host that skips frames while the menu is hidden keeps drawing while `alpha > .002f || Notify.HasPending`.
`menu.CloseRequested` fires when a "Close menu" button calls `menu.RequestClose()`.

## 3. Create the overlay

```csharp
using System.Numerics;
using BlazeImGui;
using ImGuiNET;

namespace MyApp;

public sealed class MyOverlay : BlazeOverlay
{
    // Settings live in fields; widgets take them by ref.
    private bool _esp = true, _names = true;
    private float _distance = 250f;
    private int _mode;
    private Vector4 _enemyColor = Palette.Hex("#FF6B6B");

    public MyOverlay() : base(new OverlayOptions
    {
        Title = "mytool",           // brand name under the tabs; its first letter is the rail's brand mark
        Subtitle = "game",          // optional small muted word after it
        Version = "v1.0.0",         // optional version after that
    })
    {
        // A page is one rail icon with its own tabs. The caption is the quiet uppercase line under its title.
        var visuals = Menu.AddPage("Visuals", Icons.Eye, "Overlay");
        visuals.AddTab("General", DrawVisuals, "Shown under ABOUT until a control with a description is hovered.");
        visuals.AddTab("Colors", DrawColors);

        var settings = Menu.AddPage("Settings", Icons.Settings, "Menu");
        settings.AddTab("General", DrawSettings);
        settings.PinBottom = true;            // the gear sits at the bottom of the rail

        Menu.Status = DrawStatus;             // the status column (a page can override it with page.Status)
    }

    private void DrawVisuals()
    {
        // Descriptions appear under ABOUT in the status column while the control is hovered.
        Ui.Toggle("Enable visuals", ref _esp, "Draw players and objects in the world.");
        ImGui.BeginDisabled(!_esp);
        Ui.Toggle("Names", ref _names, "A name tag above each player.");
        Ui.Slider("Max distance", ref _distance, 10, 500, "0", "Players further away are not drawn.");
        Ui.Combo("Mode", ref _mode, ["Nearest", "Lowest health"]);
        ImGui.EndDisabled();
    }

    private void DrawColors() => Ui.Color("Enemy", ref _enemyColor);

    private void DrawSettings()
    {
        if (Ui.Button("Close menu", ButtonKind.Secondary, new Vector2(Ui.ControlWidth, 0))) Menu.RequestClose();
    }

    private void DrawStatus()
    {
        Ui.Caption("Visuals");
        Ui.Readout("State", _esp ? "drawing" : "off", _esp ? Tone.Success : Tone.Neutral);
        Ui.Readout("Distance", $"{_distance:0} yd");
    }

    // Drawn every frame, also while the menu is hidden. Coordinates are overlay pixels.
    protected override void DrawOverlay(ImDrawListPtr draw)
    {
        if (!_esp) return;
        // draw.AddRect(...), draw.AddText(...), etc.
    }
}
```

`Program.cs`:

```csharp
using var overlay = new MyApp.MyOverlay();
await overlay.Run();
```

The project's `OutputType` should be `WinExe` (no console window) — wrap `Run()` in try/catch and show a
`MessageBox` if you need to surface startup errors.

## 4. Lifecycle — which method to use for what

| Override / member | When it runs | Use it for |
|---|---|---|
| constructor | once | `Menu.AddPage(...)`, `Menu.*` settings, loading saved settings |
| `OnLoadFonts()` | while the font atlas is built | `myFont = Fonts.Add(FontWeight.SemiBold, 18)` |
| `OnSetup()` | once, after theme + fonts are set | `Theme.SetAccent`, `Theme.Customize`, `Notify.Corner`, one-time ImGui config |
| `OnUpdate()` | every frame, first | logic: reading snapshots, timers, polling `Input.Pressed(key)` |
| `DrawOverlay(ImDrawListPtr)` | every frame, behind all windows | ESP / HUD / world drawings |
| `DrawMenu()` | every frame while the menu is visible or fading | default draws `Menu`; override to draw your own `ImGui.Begin` windows instead |
| `OnExit()` | before the overlay closes | saving settings |
| `Exit()` | call it | close the overlay; `Run()` returns |
| `MenuVisible` (get/set) | — | show/hide the menu with a fade |
| `MenuAlpha` | — | current fade 0..1 |
| `TargetVisible` | — | false while the followed window is missing/minimized |
| `Handle` | — | the overlay HWND |

**Do not override `Render()`** (it is sealed) and **do not override `PostInitialized()`** — use
`OnSetup()`/`OnLoadFonts()` instead. Do not call `ReplaceFont` or `Theme.ApplyStyle` yourself.

## 5. Options (`OverlayOptions`, all optional)

| Property | Default | Notes |
|---|---|---|
| `Title` / `Subtitle` / `Version` | "Blaze Overlay" / null / null | Brand line under the tabs: `mytool game v1.0.0` |
| `Logo` | null (first letter of `Title`) | Rail brand mark, drawn bold with the `Brand` dot; an `Icons.*` constant works too |
| `MenuKey` | `(int)VirtualKey.Insert` | Virtual-key code |
| `ExitKey` | `(int)VirtualKey.End` | `0` disables |
| `HotkeyGate` | null | `Func<bool>`; when hotkeys may fire. Default: always, or only while the `Follow` target/overlay is focused |
| `StartVisible` | true | |
| `Theme` | `ThemeMode.Light` | |
| `Accent` | null (ink: `#2B2B30` light, `#E7E7EB` dark) | selection, fills, primary buttons: `Palette.Hex("#22C55E")` |
| `FontSize` | 15 | body px |
| `UiScale` | 1.0 | scales fonts, spacing, widgets |
| `MenuSize` | 1000×600 | menu is resizable, min 860×500 |
| `Follow` | null (covers primary monitor) | `WindowTarget.FromProcessName("Game")`, `.FromProcessId(pid)`, `.FromTitle("…")`, `.FromHandle(hwnd)` |
| `ExitWhen` | null | `Func<bool>` checked every frame, e.g. `() => process.HasExited` |
| `VSync` / `FpsLimit` | true / 144 | FpsLimit applies when VSync is false |
| `HideFromTaskbar` | true | |

Hotkeys never fire while an ImGui text field has focus or a `Ui.Keybind` is waiting for a key.

## 6. Menu and widget reference

**Menu** (`Menu.*`, set in the constructor)

| Member | Notes |
|---|---|
| `Menu.AddPage(title, icon, caption?)` → `MenuPage` | one rail icon; `caption` is the uppercase line under the page title |
| `Menu.AddPage(title, icon, draw, caption?)` | shortcut: a page with a single tab of the same title |
| `page.AddTab(title, draw, about?)` → `MenuTab` | `about` shows under ABOUT until a control with a description is hovered |
| `page.Status = () => …` | this page's status column; falls back to `Menu.Status` |
| `page.PinBottom = true` | icon at the bottom of the rail (settings) |
| `page.Badge = true` | a small brand dot on the rail icon |
| `page.SelectedTab` / `page.SelectTab("Timing")` | current tab |
| `Menu.Select("Relax")` / `Menu.Select("Relax/Timing")` / `Menu.Selected = i` | current page (and tab) |
| `Menu.Status` / `StatusTitle` / `AboutTitle` | default status column, its title ("Status"), the hint caption ("About") |
| `Menu.ShowStatus = false` | drop the status column; descriptions then show under each control |
| `Menu.RailWidth` / `NavWidth` / `StatusWidth` | column widths (62 / 200 / 280); the controls column takes the rest |
| `Menu.RequestClose()` / `Menu.CloseRequested` | hide the menu (e.g. a "Close menu" button) |

**Widgets.** Labeled controls stack: the label on its own line, the control under it at `Ui.ControlWidth`
(the column width, up to 280 px). The optional `description` is shown under ABOUT in the status column while
the control is hovered. Labels follow ImGui ID rules — text after `##` is hidden but makes the ID unique
(`"Enabled##esp"`, `"Enabled##names"`). Controls return `true` on the frame the value changes.

**Layout**

| Call | Notes |
|---|---|
| `Ui.Caption("Title")` | quiet uppercase label ("AIM ASSIST"); heads a group of readouts or rows |
| `Ui.Section("Title")` | a caption with room above it when it follows other content |
| `Ui.Readout(label, value, Tone = Neutral)` | status line: muted label, value in a column to the right (`Tone.Success` = green) |
| `Ui.About(description, title?)` | attach an ABOUT description to the previous item or group (any ImGui widget); `title` names the control above it |
| `Ui.BeginCard("id", title?, icon?)` … `Ui.EndCard()` | rounded surface that fits its content; **always pair** |
| `if (Ui.BeginGrid("id", columns)) { Ui.NextCell(); … Ui.EndGrid(); }` | equal columns; `EndGrid` only if `BeginGrid` returned true |
| `Ui.Row("Label", controlWidth = 0)` | label on its own line, then the cursor/width for **any** ImGui widget: `Ui.Row("Mode"); ImGui.SliderAngle("##a", ref r);` |
| `Ui.ControlWidth` | default control width (column width, up to 280 px) |
| `Ui.SameLineRight(width)` | put the next item flush right on the current line |
| `Ui.Divider()` · `Ui.Hint(text)` | hairline · muted wrapped text |
| `Ui.Text(text, Tone or Vector4)` · `Ui.TextWrapped(text)` | `%`-safe text (see pitfalls) |

**Controls**

| Call | Notes |
|---|---|
| `Ui.Toggle(label, ref bool, description?)` | checkbox row, whole row clickable — preferred for on/off settings |
| `Ui.Checkbox(label, ref bool)` | compact checkbox sized to its label, for several on one line |
| `Ui.Slider(label, ref float, min, max, format = "0.0", description?)` | value box + track with grip; .NET format: `"0"`, `"0%"` (×100), `"0.00"`; the value snaps to the precision the format shows; on tracks with under 2 px per unit, drags snap to round 1-2-5 steps (10 yd for 10 - 500 yd), and Left/Right step one shown unit while hovered or dragged |
| `Ui.Slider(label, ref int, min, max, format = "0", description?)` | snaps to integers |
| `Ui.Button(label, ButtonKind = Secondary, size = default)` | `Secondary` outlined, `Primary` ink-filled, `Ghost`, `Danger`; `size.X < 0` = full width; settings buttons look best at `new Vector2(Ui.ControlWidth, 0)` |
| `Ui.IconButton(Icons.X, "##id", tooltip?, size = 0)` | square borderless button |
| `Ui.Segmented("##id", ref int, string[] items, width = 0)` | segmented control with a sliding ink thumb; width 0 = `Ui.ControlWidth` |
| `Ui.Combo(label, ref int, string[] items, description?)` | outlined dropdown; the list opens just under it |
| `Ui.TextInput(label, ref string, hint = "", maxLength = 256, description?)` | text field with placeholder |
| `Ui.Number(label, ref float, speed, min, max, format = "%.2f", description?)` | drag/double-click to type; `min == max == 0` = unbounded; format is printf-style here |
| `Ui.Number(label, ref int, speed, min, max, description?)` | |
| `Ui.Color(label, ref Vector4, alpha = true, description?)` | swatch with its label beside it → picker popup |
| `Ui.Keybind(label, ref int vk, description?)` | outlined button reading "Label: Key"; click then press a key/mouse button; Esc cancels, Backspace clears (0) |

**Feedback / display**

| Call | Notes |
|---|---|
| `Ui.Badge(text, Tone)` · `Ui.BadgeSize(text)` | pill; `Tone.Neutral/Accent/Success/Warning/Danger` |
| `Ui.Stat(label, value, change?, changeTone, icon?)` | KPI tile; good inside a grid |
| `Ui.Progress(fraction, label?, Tone, height)` | thin animated bar with % |
| `Ui.Spinner(radius, thickness)` | indeterminate |
| `Ui.Tooltip(text)` | call right after the item it describes; fades in |
| `Ui.HelpMarker(text)` | muted ⓘ with tooltip; use after `ImGui.SameLine()` |
| `Ui.LineChart("##id", ReadOnlySpan<float>, height = 120, min?, max?, valueFormat = "0.0", Tone)` | area chart with hover readout |
| `Notify.Show(title, message?, Tone, seconds = 3.5f, icon?)` | toast; **thread-safe**; `Notify.Corner = ToastCorner.BottomRight` etc. |

**Plain ImGui is fully supported and themed** — use it for anything above doesn't cover: `ImGui.BeginTable`
(tables, sorting via `TableGetSortSpecs`), `ImGui.PlotLines`/`PlotHistogram`, `ImGui.BeginTabBar`,
`ImGui.TreeNodeEx`, `ImGui.CollapsingHeader`, `ImGui.Selectable`, `ImGui.BeginListBox`,
`ImGui.RadioButton`, `ImGui.InputTextMultiline`, `ImGui.DragFloat3`, `ImGui.OpenPopup`/`BeginPopupModal`,
`ImGui.BeginMenuBar`, `ImGui.ColorPicker4`, `ImGui.BeginDisabled`, etc. `samples\BlazeImGui.Demo\DemoOverlay.cs`
shows tables, trees, tabs, list boxes, modals and native plots.

## 7. Theme, colors, fonts, icons

* **Colors:** never hard-code UI colors. Read tokens from `Theme.Colors` every frame (they animate during a
  theme switch): `Background, Sidebar, Surface, Elevated, Field, FieldHover, FieldActive, Raised, Border,
  Text, TextMuted, TextFaint, Accent, AccentHover, OnAccent, Knob, Brand, Success, Warning, Danger`.
* **Draw lists:** pack with `Theme.U32(color, alpha = 1)` — it also applies the menu/page fade.
  For world drawings that must not fade with the menu, use `ImGui.ColorConvertFloat4ToU32(color)`.
* `Theme.Set(ThemeMode.Light)` / `Theme.Toggle()` (animated), `Theme.IsDark`, `Theme.Mode`,
  `Theme.Changed += mode => …` (save the choice), `Theme.SetAccent(Palette.Hex("#14B8A6"))`,
  `Theme.Customize(ThemeMode.Dark, p => p.Background = Palette.Hex("#0E0E10"))`.
  Helpers: `Palette.Hex("#RRGGBB[AA]")`, `Palette.Shade(c, ±amount)`, `Palette.Alpha(c, a)`.
* **Fonts:** `Fonts.Regular` (default), `Medium`, `SemiBold`, `Small`, `SmallSemiBold`, `Heading`, `Title`
  (column titles), `Brand` (rail mark), `IconLarge`.
  Switch with `ImGui.PushFont(Fonts.SemiBold); … ImGui.PopFont();`. Draw-list text with a font:
  `draw.AddText(Fonts.SemiBold, Fonts.SemiBold.FontSize, pos, col, text)`. Extra sizes only via
  `OnLoadFonts()` + `Fonts.Add(FontWeight.Regular/Medium/SemiBold/Bold, px)`.
* **Icons:** the constant is the [lucide.dev/icons](https://lucide.dev/icons) name in PascalCase
  (`"chevron-right"` → `Icons.ChevronRight`; names starting with a digit get `_`). Find one with
  `grep -n "Name" src/BlazeImGui/Icons.g.cs` in the BlazeImGui repository (or IntelliSense on `Icons.`). Icons work in any string in any font:
  `$"{Icons.Save}  Save"` (two spaces between icon and text look best).

## 8. Recipes

**Follow a game window and close when it exits**

```csharp
private static readonly Process Game = Process.GetProcessesByName("Game")[0];

public MyOverlay() : base(new OverlayOptions
{
    Title = "My Tool",
    Follow = WindowTarget.FromProcessId(Game.Id),  // overlay covers the game's client area
    ExitWhen = () => Game.HasExited,
}) { … }
```

Inside `DrawOverlay`, skip drawing when `!TargetVisible`. Hotkeys then only fire while the game or the
overlay is focused (override with `HotkeyGate`).

**Persist settings (incl. theme) as JSON**

```csharp
public sealed class Settings { public bool Esp = true; public float Distance = 250; public ThemeMode Theme; }
// fields: private readonly string _path = Path.Combine(AppContext.BaseDirectory, "settings.json"); private Settings _s;
// ctor:   _s = File.Exists(_path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path), new JsonSerializerOptions { IncludeFields = true })! : new();
// OnSetup: Theme.Set(_s.Theme, animate: false); Theme.Changed += m => _s.Theme = m;
// OnExit: File.WriteAllText(_path, JsonSerializer.Serialize(_s, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
// widgets: Ui.Toggle("Visuals", ref _s.Esp);   // fields (not properties) so they can be passed by ref
```

**A user-bindable action key**

```csharp
private int _panicKey = (int)VirtualKey.F10;
// page:      Ui.Keybind("Panic key", ref _panicKey);
// OnUpdate:  if (!Ui.IsCapturingKey && Input.Pressed(_panicKey)) { … }
```
`Input.IsDown(vk)` = held, `Input.Pressed(vk)` = this frame only, `Input.KeyName(vk)` = display name.

**Live status per page, a badge dot, opening on a tab**

```csharp
var relax = Menu.AddPage("Relax", Icons.Keyboard, "Auto-tap");
relax.AddTab("Timing", DrawTiming, "How the timing of each tap is chosen.");
relax.Status = () =>
{
    Ui.Caption("Relax");
    Ui.Readout("State", _active ? "active" : "idle", _active ? Tone.Success : Tone.Neutral);
    Ui.Readout("Changes", _dirty ? "not saved" : "saved", _dirty ? Tone.Warning : Tone.Success);
};
relax.Badge = true;            // brand dot on the rail icon
Menu.Select("Relax/Timing");   // or Menu.Selected = 0
```

**Card with a badge or button on its title line**

```csharp
Ui.BeginCard("players", "Players", Icons.Users);
Ui.SameLineRight(Ui.BadgeSize("Live").X);
Ui.Badge("Live", Tone.Success);
…
Ui.EndCard();
```

**Your own window instead of the built-in menu** — override `DrawMenu()`; the fade alpha is already applied
and all `Ui.*` widgets and the theme still work:

```csharp
protected override void DrawMenu()
{
    var open = true;
    var flags = MenuVisible ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs; // not clickable while fading out
    ImGui.SetNextWindowSize(new Vector2(480, 360), ImGuiCond.FirstUseEver);
    if (ImGui.Begin("My Tool", ref open, flags)) { Ui.Toggle("Visuals", ref _esp); }
    ImGui.End();
    if (!open) MenuVisible = false;
}
```

**Custom animation** — `var t = Anim.Toggle(ImGui.GetID("##hover"), hovered, 16f);` returns 0..1 eased;
`Anim.To(id, target, speed)` eases any float; `Anim.Lerp(a, b, t)` works for float/Vector2/Vector4.

## 9. Migrating an existing ClickableTransparentOverlay overlay

| Existing code | Replace with |
|---|---|
| `class X : ClickableTransparentOverlay.Overlay` + `base("Title", true)` | `class X : BlazeOverlay` + `base(new OverlayOptions { Title = "Title" })` |
| `PostInitialized()` with `ReplaceFont(...)`, `ApplyStyle()`, VSync/FPS, taskbar style | delete; use `OnSetup()` / `OnLoadFonts()` / `OverlayOptions` |
| Fonts loaded from `C:\Windows\Fonts` (Segoe UI) | `Fonts.*` (embedded Inter); extra label sizes via `Fonts.Add` in `OnLoadFonts` |
| Custom `ImGuiStyle` colors / `Accent` constants | `Theme.Colors.*` tokens; brand color via `OverlayOptions.Accent` |
| Insert/Delete polling with `GetAsyncKeyState` | built in (`MenuKey`/`ExitKey`); custom keys with `Input.Pressed` |
| Window-follow code (`GetClientRect`/`ClientToScreen`) | `OverlayOptions.Follow = WindowTarget.FromProcessId(pid)` |
| "close when the game exits" checks | `OverlayOptions.ExitWhen` |
| `Render()` body | split: logic → `OnUpdate()`; world drawings (`GetBackgroundDrawList`) → `DrawOverlay(draw)`; each top-level tab → a `Menu.AddPage(...)`, each sub-tab → `page.AddTab(...)` |
| `ImGui.Begin("…")` main window with tabs | pages; or keep it by overriding `DrawMenu()` |
| `ImGui.Checkbox` / `SliderFloat` settings | `Ui.Toggle` / `Ui.Slider` (same `ref` fields) |
| `ImGui.SeparatorText("X")` | `Ui.Section("X")`, or a separate tab |
| Status / debug text (`ImGui.Text($"Target: {t}")`) | `Ui.Readout("Target", t)` in `page.Status` / `Menu.Status` |
| `ImGui.TextColored(color, s)` / `ImGui.TextDisabled(s)` | `Ui.Text(s, color)` / `Ui.Text(s, Theme.Colors.TextMuted)` |
| Status / error lines | `Ui.Text(msg, Tone.Danger)`, `Ui.Badge`, or `Notify.Show` |

Keep the project's data/sampling code unchanged — only the presentation layer moves.

## 10. Rules & pitfalls (read before writing UI code)

1. **`%` in text:** ImGui.NET passes `ImGui.Text`, `TextColored`, `TextWrapped`, `TextDisabled`,
   `BulletText` strings as printf formats, so `%` disappears (and `%s` can crash). For any dynamic text use
   `ImGui.TextUnformatted`, `Ui.Text`, `Ui.TextWrapped`, or `Ui.Hint`.
2. **ImGui calls only on the render thread** (inside the overrides above). From other threads, only
   `Notify.Show` is safe; otherwise publish data to fields and read them in `OnUpdate`/pages.
3. **Unique IDs:** two widgets with the same label in one window collide. Add `##suffix`, or wrap in
   `ImGui.PushID(i)` / `PopID()` inside loops. **Labels with live values** (`$"Health {hp}"`) change their
   ID every frame, which restarts animations — end them with a fixed `###id`:
   `Ui.Progress(f, $"Health  {hp:N0} / {max:N0}###health")` (only the part after `###` is the ID).
4. **Pairing:** every `BeginCard` needs `EndCard`; `BeginGrid`/`BeginTable`/`BeginTabBar`/`TreeNode`
   need their `End*` only when they returned true; `PushFont`/`PushStyleColor`/`PushStyleVar`/`PushID`
   need their pops in the same frame.
5. `ImGui.GetContentRegionMax` does not exist in ImGui 1.91 — use `ImGui.GetContentRegionAvail()` or
   `Ui.SameLineRight`.
6. `ImGui.BeginDisabled(cond)` / `EndDisabled()` works with all `Ui` widgets (dims and blocks input).
7. Without `Follow`, the overlay covers the **primary monitor**; draw coordinates are screen pixels.
   With `Follow`, (0,0) is the target's client-area top-left.
8. Window positions are saved to `imgui.ini` in the working directory (ImGui default).
9. Don't create fonts outside `OnLoadFonts`; don't call `Theme.ApplyStyle` again; don't cache
   `Theme.Colors` across frames.

## 11. Verify

1. `dotnet build` — no errors, and a fresh clone in an empty folder also builds (section 2, step 6).
   `git ls-files packages` lists the `.nupkg`.
2. Run the app: the menu appears centered and fades in; Insert hides/shows it; End exits.
3. Click every rail icon and tab: content fades in, the rail tile and tab highlight glide, widgets respond, and
   hovering a control with a description shows its name and description under ABOUT in the status column.
4. Switch Light/Dark (`Theme.Toggle()`, e.g. from a settings page): all colors cross-fade, nothing stays dark-on-dark or light-on-light
   (if a custom draw stays wrong, it is using a hard-coded color instead of `Theme.Colors`).
5. If `Follow` is set: the overlay sits exactly on the game window and hotkeys only work while it is focused.

To see every widget in action: `dotnet run --project samples\BlazeImGui.Demo` (flags: `--theme dark`,
`--page "Charts"` or `"Controls/Sliders"`, `--backdrop` for an opaque background, `--toast` to show sample toasts).
