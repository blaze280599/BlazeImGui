# Regenerates the Rust port's generated files from the C# library's assets:
#   rust/blaze-imgui/src/icons.rs   Lucide codepoints as &str constants, from src/BlazeImGui/Icons.g.cs
#   rust/blaze-imgui/fonts/         Inter + Lucide TTFs (embedded with include_bytes!) and their licenses
# Usage: powershell -File tools/generate-rust.ps1   (run generate-icons.ps1 first when updating Lucide)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$fonts = Join-Path $root "src\BlazeImGui\Assets\Fonts"
$crate = Join-Path $root "rust\blaze-imgui"

# ---------------------------------------------------------------- icons
$cs = Get-Content (Join-Path $root "src\BlazeImGui\Icons.g.cs") -Raw
$range = [regex]::Match($cs, 'RangeStart = 0x([0-9A-F]+), RangeEnd = 0x([0-9A-F]+)')
$icons = [regex]::Matches($cs, 'public const string (\w+) = "\\u([0-9A-F]{4})";')

$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine('//! Generated: Lucide icon font codepoints (ISC license).')
[void]$sb.AppendLine('//! Browse icons at <https://lucide.dev/icons> - "chevron-right" -> `icons::CHEVRON_RIGHT`.')
[void]$sb.AppendLine('//! Icons work inside any text: `format!("{}  Save", icons::SAVE)`.')
[void]$sb.AppendLine('//! Regenerate with tools/generate-rust.ps1; do not edit by hand.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('/// First and last codepoint in the icon font; used to build the glyph range.')
[void]$sb.AppendLine(('pub const RANGE_START: u32 = 0x{0};' -f $range.Groups[1].Value))
[void]$sb.AppendLine(('pub const RANGE_END: u32 = 0x{0};' -f $range.Groups[2].Value))
[void]$sb.AppendLine('')
$seen = @{}
foreach ($m in $icons) {
    # PascalCase -> UPPER_SNAKE: a break before each capital and between letters and digits.
    $name = $m.Groups[1].Value.TrimStart('_')
    $snake = [regex]::Replace($name, '(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=[0-9])', '_').ToUpper()
    if ($snake -match '^[0-9]') { $snake = "_$snake" }
    if ($seen.ContainsKey($snake)) { continue }
    $seen[$snake] = 1
    [void]$sb.AppendLine(('pub const {0}: &str = "\u{{{1}}}";' -f $snake, $m.Groups[2].Value))
}
[IO.File]::WriteAllText((Join-Path $crate "src\icons.rs"), $sb.ToString())
"Wrote $($seen.Count) icons"

# ---------------------------------------------------------------- fonts
$out = Join-Path $crate "fonts"
New-Item -ItemType Directory -Force $out | Out-Null
foreach ($file in "Inter-Regular.ttf", "Inter-Medium.ttf", "Inter-SemiBold.ttf", "Inter-Bold.ttf", "lucide.ttf", "Inter-LICENSE.txt", "Lucide-LICENSE.txt") {
    Copy-Item (Join-Path $fonts $file) (Join-Path $out $file) -Force
}
"Copied fonts"
