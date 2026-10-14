# BlazeImGui for C++

The BlazeImGui menu (sidebar, light/dark themes, animated widgets, toasts, Inter + 1,544 Lucide icons) as a
native Dear ImGui library, for hosts that own their ImGui context: DirectX / OpenGL / Vulkan hooks, internal
overlays, plain apps. It is a port of the C# library in `src/BlazeImGui` with the same look, tokens and widget set.

| Requirement | Value |
|---|---|
| Dear ImGui | **1.92+** (dynamic fonts: one `ImFont` per weight, any size) |
| C++ | 17 or later |
| Platform | Windows for `Blaze::Input` (GetAsyncKeyState); everything else is portable |

## Add it to a project

1. Copy `cpp/BlazeImGui` into the target repository (e.g. `external/blazeimgui`). Never reference this folder
   from another repository.
2. Compile `blaze_imgui.cpp` and `blaze_font_data.cpp` with the project, and add the folder to the include path.
   The fonts are embedded in `blaze_font_data.cpp`; nothing else needs copying.
3. Record where the copy came from (BlazeImGui commit, https://gitlab.com/Blvckberry/BlazeImGui) in a README
   next to it. Updates are made by copying a newer `cpp/BlazeImGui` over it.

## Use it

```cpp
#include "blaze_imgui.h"

Blaze::OverlayOptions options;
options.Title = "My Tool";
options.Subtitle = "v1.0";
Blaze::Overlay overlay(options);

bool enabled = true;
float distance = 250.0f;
overlay.menu.AddPage("General", ICON_LC_HOUSE, [&] {
    Blaze::Ui::Section("General");
    Blaze::Ui::BeginCard("main");
    Blaze::Ui::Toggle("Enabled", &enabled, "Draw players and objects");
    Blaze::Ui::Slider("Distance", &distance, 10, 500, "%.0f yd");
    Blaze::Ui::EndCard();
}, "What is drawn in the world", "Main");

// once, after ImGui::CreateContext() and the backends' Init:
overlay.Setup();

// every frame, between ImGui::NewFrame() and ImGui::Render():
overlay.Render();   // theme, the menu with its fade, toasts
```

The host routes input and handles its own hotkeys: `overlay.ToggleMenu()`, `SetMenuVisible()`, and skip hotkeys
while `Blaze::Ui::IsCapturingKey()` or `io.WantTextInput`. Call `overlay.SetScale(s)` when the resolution changes
(games: `display.y / 1080`).

## Differences from the C# API

| C# | C++ |
|---|---|
| `class X : BlazeOverlay` | own a `Blaze::Overlay`, call `Setup()` / `Render()` from your hook |
| `Menu.AddPage(...)` | `overlay.menu.AddPage(...)` (returns `MenuPage&`: `.Badge`, `.Description`, `.Scroll`) |
| `ref bool` / `ref float` | pointers: `Ui::Toggle("x", &value)` |
| .NET formats `"0 'yd'"` | printf formats `"%.0f yd"`, `"%.0f%%"` |
| `Icons.ChevronRight` | `ICON_LC_CHEVRON_RIGHT` (UTF-8 literal, concatenates: `ICON_LC_SAVE "  Save"`) |
| `Fonts.SemiBold` (ImFontPtr) | `Blaze::Fonts::SemiBold` (`{ ImFont*, size }`); `Fonts::Push/Pop`, `.Px()` for draw lists |
| `Fonts.Add` in `OnLoadFonts` | `Fonts::Add(weight, size)` any time after `Setup` (1.92 fonts have no fixed size) |
| `Theme.Changed += ...` | `Theme::OnChanged(callback)` |
| `Menu.CloseRequested` | `menu.OnClose` (the overlay hides the menu by default) |

C++-only additions: `MenuPage::Scroll = false` gives a page the whole body without scrolling (for pages that lay
out their own child windows); `Menu::Center()` / `WindowCenter()`; the menu is never sized larger than the
screen; `OverlayOptions::MergeFonts` merges extra glyph sources into every Inter weight.

## Files

```
BlazeImGui/
  blaze_imgui.h / .cpp   the library (namespace Blaze)
  blaze_icons.h          ICON_LC_* (generated)
  blaze_font_data.*      Inter Regular/Medium/SemiBold/Bold + Lucide (generated)
  fonts/                 Inter (SIL OFL 1.1) and Lucide (ISC) licenses
samples/demo_dx11.cpp    Win32 + DirectX 11 demo (--theme light, --page, --screenshot file.bmp)
```

`tools/generate-cpp.ps1` regenerates the icons and font data from `src/BlazeImGui` (after `generate-icons.ps1`).
