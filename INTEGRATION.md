# BlazeImGui — Integration Guide for AI Agents

This file tells an AI coding agent (or a human) exactly how to add the BlazeImGui menu to a C# overlay
project. Follow the steps in order. Every API named here exists; if something is not listed, use plain
ImGui.NET — it is themed automatically.

> **Source of this library:** `C:\Users\blazi\Desktop\AI Projects\BlazeImGui` · https://gitlab.com/Blvckberry/BlazeImGui
> **Library project:** `src\BlazeImGui\BlazeImGui.csproj` · **Working example:** `samples\BlazeImGui.Demo\DemoOverlay.cs`
>
> **Rule:** the library is *copied into* each project that uses it (section 2), so every project builds on
> its own from a plain clone. Never reference this folder from another repository.

---

## 0. What you get

* `BlazeOverlay` — an abstract base class over `ClickableTransparentOverlay.Overlay`. It handles fonts,
  theme, hotkeys (Insert = show/hide menu, End = exit), menu fade in/out, hiding from the taskbar,
  covering the screen or following a game window, and toast notifications.
* `Menu` — a sidebar window: brand, grouped pages with icons, gliding selection, page fade/slide
  transitions, a header with the page title, a close button, and a Light/Dark switch.
* `Ui` — animated, themed widgets (toggles, sliders, buttons, segmented control, combo, inputs, color
  pickers, keybinds, cards, sections, grids, badges, stats, progress, spinner, tooltips, line chart).
* `Theme` — dark + light palettes with an animated cross-fade and a configurable accent color.
* `Fonts` — Inter (Regular/Medium/SemiBold, plus Bold on request) with the Lucide icon font merged into every size.
* `Icons` — 1,544 Lucide icon constants (`Icons.Settings`, `Icons.Eye`, …).
* `Notify` — toast notifications, callable from any thread.

Fonts are embedded in `BlazeImGui.dll`; nothing needs copying.

## 1. Requirements (check the target project first)

| Requirement | Value |
|---|---|
| Target framework | `net8.0-windows` (or later Windows TFM) |
| Platform | `x64` (`<Platforms>x64</Platforms>` and `<PlatformTarget>x64</PlatformTarget>`) |
| ImGui.NET | **1.91.6.1** — must match exactly if the project also references it |
| ClickableTransparentOverlay | **11.1.0** — must match exactly if the project also references it |

If the target project already has `PackageReference`s to ImGui.NET / ClickableTransparentOverlay at these
versions, keep or remove them (they flow transitively from BlazeImGui). If they are at **different
versions**, change them to the versions above (or change both projects together) — mixed versions break
at runtime.

## 2. Copy the library into the target project (default)

**Copy the library source into the target repository and reference the copy.** The target must build from
a plain `git clone` on any machine, without this BlazeImGui folder existing next to it. Do **not** point a
`ProjectReference` at a path outside the target repository (absolute paths or `..\..\BlazeImGui\...`):
it works on this PC only and breaks for every other clone. Do not use a git submodule unless the user asks.

1. **Copy** `src\BlazeImGui` (code + `Assets\Fonts`, which include the font licenses) to a `BlazeImGui`
   folder at the target repository root, **without** `bin` and `obj`:

   ```powershell
   robocopy "C:\Users\blazi\Desktop\AI Projects\BlazeImGui\src\BlazeImGui" "<target-repo>\BlazeImGui" /E /XD bin obj
   ```

   (robocopy exit codes 0–7 mean success.) If this folder is not available, get the source from
   `git clone https://gitlab.com/Blvckberry/BlazeImGui.git` into a temporary folder and copy its
   `src\BlazeImGui` the same way.

2. **Reference the copy** from the app's `.csproj` with a path *inside* the repository, e.g. for
   `<target-repo>\MyOverlay\MyOverlay.csproj`:

   ```xml
   <ItemGroup>
     <!-- Menu, theme, fonts and widgets: a copy of the BlazeImGui library kept in this repository. -->
     <ProjectReference Include="..\BlazeImGui\BlazeImGui.csproj" />
   </ItemGroup>
   ```

   If the target has a `.sln`, also run: `dotnet sln <Solution>.sln add BlazeImGui\BlazeImGui.csproj`

3. **Record where the copy came from** in `<target-repo>\BlazeImGui\README.md`: the BlazeImGui commit
   (`git -C <BlazeImGui folder> log -1 --format=%h`), a link to https://gitlab.com/Blvckberry/BlazeImGui,
   and that updates are made by copying a newer `src\BlazeImGui` over this folder (fixes made in the copy
   should also go upstream so the copies don't drift).

4. **Make sure build output is ignored:** the target's `.gitignore` must cover `**/bin/` and `**/obj/`
   (add them if missing), and `imgui.ini`.

5. `dotnet build` once to confirm the reference resolves before writing UI code.

6. **Prove it is self-contained** after committing: clone the repository into an empty temporary folder
   and build there — it must succeed with no BlazeImGui folder beside it:

   ```powershell
   git clone <target-repo> "$env:TEMP\clone-check"; dotnet build "$env:TEMP\clone-check\<path-to-app>.csproj"
   ```

> Working on BlazeImGui itself (not integrating it)? Then edit `src\BlazeImGui` here, and re-copy it into
> projects that use it.

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
        Title = "My Tool",          // brand name in the sidebar
        Subtitle = "v1.0.0",        // optional small line under it
        Logo = Icons.Crosshair,     // any Icons.* constant
    })
    {
        Menu.AddPage("Visuals", Icons.Eye, DrawVisuals, "What is drawn in the world", group: "Main");
        Menu.AddPage("Settings", Icons.Settings, DrawSettings, group: "App");
    }

    private void DrawVisuals()
    {
        Ui.Section("General");
        Ui.BeginCard("general");
        Ui.Toggle("Enable visuals", ref _esp, "Draw players and objects");
        ImGui.BeginDisabled(!_esp);
        Ui.Toggle("Names", ref _names);
        Ui.Slider("Max distance", ref _distance, 10, 500, "0 'yd'");
        Ui.Combo("Mode", ref _mode, ["Nearest", "Lowest health"]);
        Ui.Color("Enemy color", ref _enemyColor);
        ImGui.EndDisabled();
        Ui.EndCard();
    }

    private void DrawSettings()
    {
        Ui.Section("About");
        Ui.BeginCard("about");
        Ui.Hint("My Tool — press Insert to hide this menu, End to exit.");
        Ui.EndCard();
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
| `Title` / `Subtitle` / `Logo` | "Blaze Overlay" / null / `Icons.Flame` | Sidebar brand |
| `MenuKey` | `(int)VirtualKey.Insert` | Virtual-key code |
| `ExitKey` | `(int)VirtualKey.End` | `0` disables |
| `HotkeyGate` | null | `Func<bool>`; when hotkeys may fire. Default: always, or only while the `Follow` target/overlay is focused |
| `StartVisible` | true | |
| `Theme` | `ThemeMode.Dark` | |
| `Accent` | null (indigo `#6D8CFF`) | `Palette.Hex("#22C55E")` |
| `FontSize` | 15 | body px |
| `UiScale` | 1.0 | scales fonts, spacing, widgets |
| `MenuSize` | 820×540 | menu is resizable, min 620×400 |
| `Follow` | null (covers primary monitor) | `WindowTarget.FromProcessName("Game")`, `.FromProcessId(pid)`, `.FromTitle("…")`, `.FromHandle(hwnd)` |
| `ExitWhen` | null | `Func<bool>` checked every frame, e.g. `() => process.HasExited` |
| `VSync` / `FpsLimit` | true / 144 | FpsLimit applies when VSync is false |
| `HideFromTaskbar` | true | |

Hotkeys never fire while an ImGui text field has focus or a `Ui.Keybind` is waiting for a key.

## 6. Widget reference (`Ui.*`)

All labeled controls use one "settings row" layout: label left, control right-aligned, optional
`description` in small muted text below. Labels follow ImGui ID rules — text after `##` is hidden but
makes the ID unique (`"Enabled##esp"`, `"Enabled##names"`). Controls return `true` on the frame the value
changes.

**Layout**

| Call | Notes |
|---|---|
| `Ui.Section("Title")` | quiet group label |
| `Ui.BeginCard("id", title?, icon?)` … `Ui.EndCard()` | rounded surface that fits its content; **always pair** |
| `if (Ui.BeginGrid("id", columns)) { Ui.NextCell(); … Ui.EndGrid(); }` | equal columns; `EndGrid` only if `BeginGrid` returned true |
| `Ui.Row("Label", controlWidth = 0)` | label + positions the cursor/width for **any** ImGui widget: `Ui.Row("Mode"); ImGui.SliderAngle("##a", ref r);` |
| `Ui.ControlWidth` | default control column width |
| `Ui.SameLineRight(width)` | put the next item flush right on the current line |
| `Ui.Divider()` · `Ui.Hint(text)` | hairline · muted wrapped text |
| `Ui.Text(text, Tone or Vector4)` · `Ui.TextWrapped(text)` | `%`-safe text (see pitfalls) |

**Controls**

| Call | Notes |
|---|---|
| `Ui.Toggle(label, ref bool, description?)` | animated switch, whole row clickable — preferred for on/off settings |
| `Ui.Checkbox(label, ref bool)` | compact animated checkbox for dense lists |
| `Ui.Slider(label, ref float, min, max, format = "0.0", description?)` | .NET format: `"0 'yd'"`, `"0%"` (×100), `"0.00"` |
| `Ui.Slider(label, ref int, min, max, format = "0", description?)` | snaps to integers |
| `Ui.Button(label, ButtonKind = Secondary, size = default)` | kinds: `Primary`, `Secondary`, `Ghost`, `Danger`; `size.X < 0` = full width |
| `Ui.IconButton(Icons.X, "##id", tooltip?, size = 0)` | square borderless button |
| `Ui.Segmented("##id", ref int, string[] items, width = 0)` | pill tabs with sliding thumb; width 0 fills (after `Ui.Row` = control column) |
| `Ui.Combo(label, ref int, string[] items, description?)` | dropdown with chevron |
| `Ui.TextInput(label, ref string, hint = "", maxLength = 256, description?)` | text field with placeholder |
| `Ui.Number(label, ref float, speed, min, max, format = "%.2f", description?)` | drag/double-click to type; `min == max == 0` = unbounded; format is printf-style here |
| `Ui.Number(label, ref int, speed, min, max, description?)` | |
| `Ui.Color(label, ref Vector4, alpha = true, description?)` | swatch → picker popup |
| `Ui.Keybind(label, ref int vk, description?)` | click then press a key/mouse button; Esc cancels, Backspace clears (0) |

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
  Text, TextMuted, TextFaint, Accent, AccentHover, OnAccent, Knob, Success, Warning, Danger`.
* **Draw lists:** pack with `Theme.U32(color, alpha = 1)` — it also applies the menu/page fade.
  For world drawings that must not fade with the menu, use `ImGui.ColorConvertFloat4ToU32(color)`.
* `Theme.Set(ThemeMode.Light)` / `Theme.Toggle()` (animated), `Theme.IsDark`, `Theme.Mode`,
  `Theme.Changed += mode => …` (save the choice), `Theme.SetAccent(Palette.Hex("#14B8A6"))`,
  `Theme.Customize(ThemeMode.Dark, p => p.Background = Palette.Hex("#0E0E10"))`.
  Helpers: `Palette.Hex("#RRGGBB[AA]")`, `Palette.Shade(c, ±amount)`, `Palette.Alpha(c, a)`.
* **Fonts:** `Fonts.Regular` (default), `Medium`, `SemiBold`, `Small`, `SmallSemiBold`, `Heading`, `IconLarge`.
  Switch with `ImGui.PushFont(Fonts.SemiBold); … ImGui.PopFont();`. Draw-list text with a font:
  `draw.AddText(Fonts.SemiBold, Fonts.SemiBold.FontSize, pos, col, text)`. Extra sizes only via
  `OnLoadFonts()` + `Fonts.Add(FontWeight.Regular/Medium/SemiBold/Bold, px)`.
* **Icons:** the constant is the [lucide.dev/icons](https://lucide.dev/icons) name in PascalCase
  (`"chevron-right"` → `Icons.ChevronRight`; names starting with a digit get `_`). Find one with
  `grep -n "Name" src/BlazeImGui/Icons.g.cs`. Icons work in any string in any font:
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

**Page badge / header actions / sidebar footer**

```csharp
Menu.AddPage("Updates", Icons.Download, DrawUpdates).Badge = "3";
Menu.HeaderActions = () => { if (Ui.IconButton(Icons.RefreshCw, "##reload", "Reload")) Reload(); };
Menu.SidebarFooter = () => Ui.Text("Connected", Tone.Success);
Menu.Select("Visuals"); // or Menu.Selected = 0
```

**Card with a badge or button on its title line**

```csharp
Ui.BeginCard("players", "Players", Icons.Users);
Ui.SameLineRight(Ui.BadgeSize("Live").X);
Ui.Badge("Live", Tone.Success);
…
Ui.EndCard();
```

**Your own window instead of the sidebar menu** — override `DrawMenu()`; the fade alpha is already applied
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
| `Render()` body | split: logic → `OnUpdate()`; world drawings (`GetBackgroundDrawList`) → `DrawOverlay(draw)`; each `BeginTabItem` → a `Menu.AddPage(...)` method |
| `ImGui.Begin("…")` main window with tabs | pages; or keep it by overriding `DrawMenu()` |
| `ImGui.Checkbox` / `SliderFloat` settings | `Ui.Toggle` / `Ui.Slider` (same `ref` fields) |
| `ImGui.SeparatorText("X")` | `Ui.Section("X")` + `Ui.BeginCard`/`EndCard` |
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
2. Run the app: the menu appears centered and fades in; Insert hides/shows it; End exits.
3. Click every page: pages fade in, the sidebar pill glides, and widgets respond.
4. Switch Light/Dark in the sidebar: all colors cross-fade, nothing stays dark-on-dark or light-on-light
   (if a custom draw stays wrong, it is using a hard-coded color instead of `Theme.Colors`).
5. If `Follow` is set: the overlay sits exactly on the game window and hotkeys only work while it is focused.

To see every widget in action: `dotnet run --project samples\BlazeImGui.Demo` (flags: `--theme light`,
`--page "Charts"`, `--backdrop` for an opaque background, `--toast` to show sample toasts).
