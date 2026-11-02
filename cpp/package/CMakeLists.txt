# BlazeImGui for C++, packaged (built by tools/pack-cpp.ps1). Use from a CMake project:
#
#   add_subdirectory(external/blazeimgui)
#   target_link_libraries(MyApp PRIVATE blaze_imgui)
#
# blaze_imgui.cpp compiles into each target that links blaze_imgui, with that target's Dear ImGui include path and
# settings, so it always matches the project's own ImGui version and imconfig. The fonts are a prebuilt, data-only
# library (no ImGui or CRT dependency) for MSVC-compatible toolchains (MSVC, clang-cl).
cmake_minimum_required(VERSION 3.16)

if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(_blaze_arch x64)
else()
    set(_blaze_arch x86)
endif()

add_library(blaze_imgui INTERFACE)
target_sources(blaze_imgui INTERFACE "${CMAKE_CURRENT_LIST_DIR}/src/blaze_imgui.cpp")
set_source_files_properties("${CMAKE_CURRENT_LIST_DIR}/src/blaze_imgui.cpp" PROPERTIES SKIP_PRECOMPILE_HEADERS ON)
target_include_directories(blaze_imgui INTERFACE "${CMAKE_CURRENT_LIST_DIR}/include")
target_link_libraries(blaze_imgui INTERFACE "${CMAKE_CURRENT_LIST_DIR}/lib/${_blaze_arch}/blaze_fonts.lib")
# A project that builds Dear ImGui as a CMake target named imgui gets its include path from it.
if(TARGET imgui)
    target_link_libraries(blaze_imgui INTERFACE imgui)
endif()
