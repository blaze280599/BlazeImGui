# Builds the C++ package into artifacts\blazeimgui-cpp-<version>\ (+ .zip) and, with -Into, replaces a project's copy.
# The package holds the headers, blaze_imgui.cpp (compiled by the project against its own Dear ImGui) and the fonts as
# prebuilt data-only libraries (x64, x86) instead of the 6.6 MB blaze_font_data.cpp. Needs Visual Studio (C++ tools).
#
#   tools\pack-cpp.ps1                                       # version = BLAZE_IMGUI_VERSION in blaze_imgui.h
#   tools\pack-cpp.ps1 -Into C:\code\MyHook\external\blazeimgui
param(
    [string]$Into
)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'cpp\BlazeImGui'
$header = Get-Content (Join-Path $source 'blaze_imgui.h') -Raw
$version = [regex]::Match($header, '#define BLAZE_IMGUI_VERSION "([^"]+)"').Groups[1].Value
if (-not $version) { throw 'BLAZE_IMGUI_VERSION not found in blaze_imgui.h' }

$out = Join-Path $root "artifacts\blazeimgui-cpp-$version"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
foreach ($dir in 'include', 'src', 'lib\x64', 'lib\x86', 'licenses') { New-Item -ItemType Directory -Force (Join-Path $out $dir) | Out-Null }

Copy-Item (Join-Path $source 'blaze_imgui.h'), (Join-Path $source 'blaze_icons.h'), (Join-Path $source 'blaze_font_data.h') (Join-Path $out 'include')
Copy-Item (Join-Path $source 'blaze_imgui.cpp') (Join-Path $out 'src')
Copy-Item (Join-Path $source 'fonts\*-LICENSE.txt') (Join-Path $out 'licenses')
Copy-Item (Join-Path $root 'cpp\package\*') $out

# Fonts: compile blaze_font_data.cpp per architecture. /Zl leaves out the default CRT library, so the data-only object
# links into /MT and /MD, Debug and Release builds alike.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio with the C++ tools was not found' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvarsall.bat'
$fontSource = Join-Path $source 'blaze_font_data.cpp'
$temp = Join-Path ([IO.Path]::GetTempPath()) "blaze-pack-$PID"
foreach ($arch in @(@{ Name = 'x64'; Vc = 'x64' }, @{ Name = 'x86'; Vc = 'x86' })) {
    $obj = Join-Path $temp $arch.Name
    New-Item -ItemType Directory -Force $obj | Out-Null
    $lib = Join-Path $out "lib\$($arch.Name)\blaze_fonts.lib"
    cmd /c "set `"PATH=$(Split-Path $vswhere);%PATH%`" && `"$vcvars`" $($arch.Vc) >nul && cl /nologo /c /std:c++17 /O2 /Zl /Fo`"$obj\\`" `"$fontSource`" && lib /nologo /out:`"$lib`" `"$obj\blaze_font_data.obj`""
    if ($LASTEXITCODE -ne 0) { throw "Building the $($arch.Name) fonts library failed ($LASTEXITCODE)" }
}
Remove-Item $temp -Recurse -Force

$zip = "$out.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
Write-Host "Packed $out (+ .zip)"

if ($Into) {
    if (Test-Path $Into) { Remove-Item $Into -Recurse -Force }
    Copy-Item $out $Into -Recurse
    Write-Host "Copied to $Into"
}
