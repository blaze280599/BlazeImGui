# Builds the BlazeImGui NuGet package into artifacts\ and, with -Into, drops it into a project's local feed.
#
#   tools\pack.ps1                                   # pack the Version in BlazeImGui.csproj
#   tools\pack.ps1 -Version 1.1.0                    # pack a specific version
#   tools\pack.ps1 -Into C:\code\MyTool\packages     # also copy it into MyTool's feed, replacing older ones
param(
    [string]$Version,
    [string]$Into
)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\BlazeImGui\BlazeImGui.csproj'
$out = Join-Path $root 'artifacts'

$packArgs = @('pack', $project, '-c', 'Release', '-o', $out, '-p:Platform=x64')
if ($Version) { $packArgs += "-p:Version=$Version" }
& dotnet @packArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed ($LASTEXITCODE)" }

if (-not $Version) { $Version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
$package = Join-Path $out "BlazeImGui.$Version.nupkg"
if (-not (Test-Path $package)) { throw "Expected $package" }
Write-Host "Packed $package"

if ($Into) {
    New-Item -ItemType Directory -Force $Into | Out-Null
    Get-ChildItem $Into -Filter 'BlazeImGui.*.nupkg' | Where-Object { $_.Name -ne (Split-Path $package -Leaf) } | Remove-Item
    Copy-Item $package $Into -Force
    Write-Host "Copied to $Into - set the BlazeImGui PackageReference to Version=`"$Version`"."
}
