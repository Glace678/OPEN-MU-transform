[CmdletBinding()]
param(
    [double]$DurationSeconds = 1.5,
    [double]$WarmupSeconds = 0.2,
    [double]$TolerancePercent = 2.0,
    [double]$DisplayHertz = 143.98,
    [ValidateSet("sleep", "spin")]
    [string]$WaitMode = "sleep",
    [ValidateSet("x64", "Win32")]
    [string]$Architecture = "x64",
    [string]$BuildDirectory,
    [string]$CMakePath
)

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $scriptDirectory "../.."))

if (-not $BuildDirectory) {
    $BuildDirectory = Join-Path $repoRoot "out/frame-pacing-benchmark-$($Architecture.ToLowerInvariant())"
}
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)

if (-not $CMakePath) {
    $cmakeCommand = Get-Command cmake.exe -ErrorAction SilentlyContinue
    if ($cmakeCommand) {
        $CMakePath = $cmakeCommand.Source
    }
}

if (-not $CMakePath) {
    $knownCMakePaths = @(
        "D:\Microsoft Visual Studio\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
    )
    $CMakePath = $knownCMakePaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $CMakePath -or -not (Test-Path -LiteralPath $CMakePath)) {
    throw "CMake was not found. Pass -CMakePath with an installed cmake.exe."
}

if ($DurationSeconds -le 0 -or $WarmupSeconds -le 0 -or
    $TolerancePercent -le 0 -or $DisplayHertz -le 0) {
    throw "Duration, warm-up, tolerance, and display refresh values must all be positive."
}

& $CMakePath -S $scriptDirectory -B $BuildDirectory -A $Architecture
if ($LASTEXITCODE -ne 0) {
    throw "CMake configuration failed with exit code $LASTEXITCODE."
}

& $CMakePath --build $BuildDirectory --config Release --target frame_pacing_benchmark
if ($LASTEXITCODE -ne 0) {
    throw "Benchmark build failed with exit code $LASTEXITCODE."
}

$benchmark = Join-Path $BuildDirectory "Release/frame_pacing_benchmark.exe"
if (-not (Test-Path -LiteralPath $benchmark)) {
    $benchmark = Join-Path $BuildDirectory "frame_pacing_benchmark.exe"
}
if (-not (Test-Path -LiteralPath $benchmark)) {
    throw "Built benchmark executable was not found under $BuildDirectory."
}

& $benchmark `
    --duration-seconds $DurationSeconds `
    --warmup-seconds $WarmupSeconds `
    --tolerance-percent $TolerancePercent `
    --display-hz $DisplayHertz `
    --wait-mode $WaitMode
exit $LASTEXITCODE
