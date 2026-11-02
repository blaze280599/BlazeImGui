# Builds the BlazeImGui and BlazeImGui.Overlay NuGet packages into artifacts\ and, with -Into, drops them into a
# project's local feed.
#
#   tools\pack.ps1                                   # pack the Version in src\Directory.Build.props
#   tools\pack.ps1 -Version 1.2.0                    # pack a specific version
#   tools\pack.ps1 -Into C:\code\MyTool\packages     # also copy both into MyTool's feed, replacing older ones
param(
    [string]$Version,
    [string]$Into
)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts'
$ids = 'BlazeImGui', 'BlazeImGui.Overlay'

if (-not $Version) { $Version = ([xml](Get-Content (Join-Path $root 'src\Directory.Build.props'))).Project.PropertyGroup.Version }
foreach ($id in $ids) {
    $project = Join-Path $root "src\$id\$id.csproj"
    & dotnet pack $project -c Release -o $out -p:Platform=x64 "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack $id failed ($LASTEXITCODE)" }
}

$packages = foreach ($id in $ids) {
    $package = Join-Path $out "$id.$Version.nupkg"
    if (-not (Test-Path $package)) { throw "Expected $package" }
    Write-Host "Packed $package"
    $package
}

if ($Into) {
    New-Item -ItemType Directory -Force $Into | Out-Null
    foreach ($id in $ids) {
        # Exact name match: removing BlazeImGui.*.nupkg must not take BlazeImGui.Overlay.* with it.
        Get-ChildItem $Into -Filter "$id.*.nupkg" | Where-Object { $_.Name -match "^$([regex]::Escape($id))\.\d" } | Remove-Item
    }
    Copy-Item $packages $Into -Force
    Write-Host "Copied to $Into - set the PackageReference Version to `"$Version`" (BlazeImGui.Overlay for a standalone overlay, BlazeImGui for a host that owns its ImGui context)."
}
