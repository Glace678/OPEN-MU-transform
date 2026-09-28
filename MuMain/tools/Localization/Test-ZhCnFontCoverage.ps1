param(
    [string]$LocalizationDirectory = (Join-Path $PSScriptRoot "..\..\src\Localization"),
    [string]$RegularFont = (Join-Path $PSScriptRoot "..\..\src\bin\fonts\NotoSansCJKsc-Regular.otf"),
    [string]$BoldFont = (Join-Path $PSScriptRoot "..\..\src\bin\fonts\NotoSansCJKsc-Bold.otf")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$expectedFamily = "Noto Sans CJK SC"
$codePoints = [System.Collections.Generic.HashSet[int]]::new()

Get-ChildItem -LiteralPath $LocalizationDirectory -Filter "*.zh-CN.resx" | ForEach-Object {
    [xml]$resource = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($entry in $resource.root.data) {
        $value = [string]$entry.value
        for ($index = 0; $index -lt $value.Length;) {
            $rune = [System.Text.Rune]::GetRuneAt($value, $index)
            $index += $rune.Utf16SequenceLength
            if (-not [System.Text.Rune]::IsControl($rune)) {
                [void]$codePoints.Add($rune.Value)
            }
        }
    }
}

if ($codePoints.Count -eq 0) {
    throw "No zh-CN resource text was found in '$LocalizationDirectory'."
}

Add-Type -AssemblyName PresentationCore
foreach ($fontPath in $RegularFont, $BoldFont) {
    $resolvedPath = (Resolve-Path -LiteralPath $fontPath).Path
    $typeface = [Windows.Media.GlyphTypeface]::new([Uri]::new($resolvedPath))
    if ($typeface.FamilyNames.Values -notcontains $expectedFamily) {
        throw "'$resolvedPath' does not declare the expected family '$expectedFamily'."
    }

    $missing = @($codePoints | Where-Object {
        -not $typeface.CharacterToGlyphMap.ContainsKey($_)
    } | Sort-Object)
    if ($missing.Count -ne 0) {
        $summary = $missing | Select-Object -First 20 | ForEach-Object {
            "U+{0:X4} ({1})" -f $_, [char]::ConvertFromUtf32($_)
        }
        throw "'$resolvedPath' is missing $($missing.Count) resource glyphs: $($summary -join ', ')"
    }

    Write-Output "$(Split-Path $resolvedPath -Leaf): $($codePoints.Count)/$($codePoints.Count) resource code points covered."
}
