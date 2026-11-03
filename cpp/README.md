# BlazeImGui for C++

The BlazeImGui menu (icon rail, tabs, status column, light/dark themes, animated widgets, toasts, Inter +
1,544 Lucide icons) as a
native Dear ImGui library, for hosts that own their ImGui context: DirectX / OpenGL / Vulkan hooks, internal
overlays, plain apps. It is a port of the C# library in `src/BlazeImGui` with the same look, tokens and widget set.

| Requirement | Value |
|---|---|
| Dear ImGui | **1.92+** (dynamic fonts: one `ImFont` per weight, any size) |
| C++ | 17 or later |
| Platform | Windows for `Blaze::Input` (GetAsyncKeyState); everything else is portable |

## Add it to a project

Projects use the **packaged** library, committed in their own repository so they build from a plain clone. Never
copy `cpp/BlazeImGui` into a project or reference this folder from another repository.

The package (~4.9 MB, 2.3 MB zipped) is the headers, `blaze_imgui.cpp` and the fonts as prebuilt, data-only libraries
(`lib/x64`, `lib/x86`, ~2.3 MB each), in place of the 6.6 MB `blaze_font_data.cpp`. `blaze_imgui.cpp` stays source on purpose:
it compiles against the project's own Dear ImGui, so any 1.92+ version and any `imconfig` works. The fonts
libraries have no ImGui or CRT dependency and link into `/MT` and `/MD`, Debug and Release builds with MSVC or
clang-cl.

1. **Pack it into the project** (needs Visual Studio's C++ tools; replaces the folder if it exists):

   ```powershell
   & "C:\Users\blazi\Desktop\AI Projects\BlazeImGui\tools\pack-cpp.ps1" -Into "<target-repo>\external\blazeimgui"
   ```

   The script also leaves `artifacts\blazeimgui-cpp-<version>.zip` here to hand out.

2. **Reference it.** CMake:

   ```cmake
   add_subdirectory(external/blazeimgui)
   target_link_libraries(MyApp PRIVATE blaze_imgui)
   ```

   Dear ImGui's include path comes from a CMake target named `imgui` if the project defines one, otherwise from
   `MyApp`'s own include directories. Remove any old `blaze_imgui.cpp` / `blaze_font_data.cpp` entries from the
   target's sources.

   Visual Studio `.vcxproj`, right after the `Microsoft.Cpp.props` import:

   ```xml
   <Import Project="external\blazeimgui\BlazeImGui.props" />
   ```

3. **Commit the folder** and check nothing in it is ignored (`git status --ignored external/blazeimgui`;
   some templates ignore `lib/` or `*.lib`). A fresh clone in an empty folder must build.

**Updating:** run `pack-cpp.ps1 -Into` again and commit. **Releasing a change:** edit `cpp/BlazeImGui` here and
bump `BLAZE_IMGUI_VERSION` in `blaze_imgui.h`; the package is named after it.

## Use it

```cpp
#include "blaze_imgui.h"

Blaze::OverlayOptions options;
options.Title = "mytool";
options.Version = "v1.0.0";
Blaze::Overlay overlay(options);

bool enabled = true;
float distance = 250.0f;
Blaze::MenuPage& visuals = overlay.menu.AddPage("Visuals", ICON_LC_EYE, "Overlay");
visuals.AddTab("General", [&] {
    Blaze::Ui::Toggle("Enabled", &enabled, "Shown under ABOUT while hovered.");
    Blaze::Ui::Slider("Distance", &distance, 10, 500, "%.0f");
});
overlay.menu.Status = [&] {
    Blaze::Ui::Caption("Session");
    Blaze::Ui::Readout("Overlay", "running", Blaze::Tone::Success);
};

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
| `Menu.AddPage(...)` / `page.AddTab(...)` | `overlay.menu.AddPage(...)` returns `MenuPage&` (`.AddTab`, `.Status`, `.PinBottom`, `.Badge`, `.Scroll`) |
| `ref bool` / `ref float` | pointers: `Ui::Toggle("x", &value)` |
| .NET formats `"0 'yd'"` | printf formats `"%.0f yd"`, `"%.0f%%"` |
| `Icons.ChevronRight` | `ICON_LC_CHEVRON_RIGHT` (UTF-8 literal, concatenates: `ICON_LC_SAVE "  Save"`) |
| `Fonts.SemiBold` (ImFontPtr) | `Blaze::Fonts::SemiBold` (`{ ImFont*, size }`); `Fonts::Push/Pop`, `.Px()` for draw lists |
| `Fonts.Add` in `OnLoadFonts` | `Fonts::Add(weight, size)` any time after `Setup` (1.92 fonts have no fixed size) |
| `Theme.Changed += ...` | `Theme::OnChanged(callback)` |
| `Menu.CloseRequested` / `RequestClose()` | `menu.OnClose` / `menu.RequestClose()` (the overlay hides the menu by default) |
| `Ui.CurrentAbout` / `CurrentAboutTitle` (string?) | `Ui::CurrentAbout` / `CurrentAboutTitle` (std::string, empty for none) |

C++-only additions: `MenuPage::Scroll = false` gives a page's tabs the whole body without scrolling (for tabs that
lay out their own child windows); `Menu::Center()` / `WindowCenter()`; the menu is never sized larger than the
screen; `OverlayOptions::MergeFonts` merges extra glyph sources into every Inter weight.

## Files

```
BlazeImGui/
  blaze_imgui.h / .cpp   the library (namespace Blaze)
  blaze_icons.h          ICON_LC_* (generated)
  blaze_font_data.*      Inter Regular/Medium/SemiBold/Bold + Lucide (generated)
  fonts/                 Inter (SIL OFL 1.1) and Lucide (ISC) licenses
package/                 CMakeLists.txt, BlazeImGui.props and README.md shipped in the package
samples/demo_dx11.cpp    Win32 + DirectX 11 demo (--theme dark, --page "Page" or "Page/Tab", --screenshot file.bmp)
```

`tools/generate-cpp.ps1` regenerates the icons and font data from `src/BlazeImGui` (after `generate-icons.ps1`).
