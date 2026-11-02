# BlazeImGui for C++ (package)

The BlazeImGui menu for native Dear ImGui 1.92+ hosts, packaged by `tools/pack-cpp.ps1` from
https://github.com/blaze280599/BlazeImGui. The version is `BLAZE_IMGUI_VERSION` in `include/blaze_imgui.h`.
Don't edit these files; update by packing a newer version over this folder.

```
include/   blaze_imgui.h, blaze_icons.h (ICON_LC_*), blaze_font_data.h
src/       blaze_imgui.cpp - compiled by your project against your own Dear ImGui
lib/       x64/ and x86/blaze_fonts.lib - Inter + Lucide, prebuilt data-only (MSVC / clang-cl)
licenses/  Inter (SIL OFL 1.1), Lucide (ISC)
```

**CMake**

```cmake
add_subdirectory(external/blazeimgui)
target_link_libraries(MyApp PRIVATE blaze_imgui)
```

Dear ImGui's include path comes from a CMake target named `imgui` if the project has one; otherwise it is the
consuming target's own include path (`target_include_directories(MyApp PRIVATE ${imgui_SOURCE_DIR})`).

**Visual Studio (.vcxproj)** — after the `Microsoft.Cpp.props` import:

```xml
<Import Project="external\blazeimgui\BlazeImGui.props" />
```

API and usage: https://github.com/blaze280599/BlazeImGui/blob/main/cpp/README.md
