[CmdletBinding()]
param(
    [string]$SourceRoot,
    [string]$OutputCsv
)

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $SourceRoot) {
    $SourceRoot = Join-Path $scriptDirectory "../../src/source"
}
if (-not $OutputCsv) {
    $OutputCsv = Join-Path $scriptDirectory "../../out/frame-rate-audit.csv"
}
$resolvedRoot = (Resolve-Path -LiteralPath $SourceRoot).Path
$outputPath = [IO.Path]::GetFullPath($OutputCsv)
$outputDirectory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$checks = @(
    [pscustomobject]@{
        Category = "raw-state-step"
        Pattern = "\b\w*(AnimationFrame|Timer|LifeTime|Alpha|BlendMeshLight|Velocity|Gravity|Scale)\w*\s*(\+\+|--|\+=\s*[-+]?\d+(?:\.\d+)?f?|-=\s*[-+]?\d+(?:\.\d+)?f?)"
    },
    [pscustomobject]@{
        Category = "raw-random-condition"
        Pattern = "\bif\s*\([^\r\n]*(rand\(\)|GetLargeRand\([^)]*\))\s*%\s*\d+[^\r\n]*\)"
    },
    [pscustomobject]@{
        Category = "wall-clock"
        Pattern = "\b(WorldTime|GetTickCount|timeGetTime|SDL_GetTicks)\b"
    },
    [pscustomobject]@{
        Category = "scaled-update"
        Pattern = "\b(FPS_ANIMATION_FACTOR|ScaleLinearStep|ScaleBlendCoefficient|ScaleRetention|rand_fps_check|FpsCheck)\b"
    }
)

$records = [Collections.Generic.List[object]]::new()
$files = Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File -Include *.cpp,*.h |
    Where-Object { $_.FullName -notmatch "[\\/]ThirdParty[\\/]" }

foreach ($file in $files) {
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        if ($line.TrimStart().StartsWith("//")) {
            continue
        }
        foreach ($check in $checks) {
            if ($line -match $check.Pattern) {
                # A line which already carries an explicit time scale is not a
                # raw state step even if it also matches the broad state regex.
                if ($check.Category -eq "raw-state-step" -and
                    $line -match "FPS_ANIMATION_FACTOR|ScaleLinearStep|ScaleBlendCoefficient|ScaleRetention") {
                    continue
                }

                $relativePath = $file.FullName.Substring($resolvedRoot.Length).TrimStart([char[]]@('\', '/'))
                $records.Add([pscustomobject]@{
                    Category = $check.Category
                    File = $relativePath
                    Line = $lineNumber
                    Text = $line.Trim()
                })
            }
        }
    }
}

$records |
    Sort-Object Category, File, Line |
    Export-Csv -LiteralPath $outputPath -NoTypeInformation -Encoding utf8

Write-Host "Frame-rate audit: $($records.Count) matches"
$records | Group-Object Category | Sort-Object Name | ForEach-Object {
    Write-Host ("  {0,-24} {1,6}" -f $_.Name, $_.Count)
}
Write-Host "CSV: $outputPath"
