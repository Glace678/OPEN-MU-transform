# tests/e2e/run_all_tests.ps1
# Master PowerShell Test Runner Wrapper for OpenMU & MuMain E2E Testing Track

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RunnerPy = Join-Path $ScriptDir "run_all_tests.py"

Write-Host "Executing Master E2E Test Suite via Python runner..." -ForegroundColor Cyan
& python $RunnerPy
if ($LASTEXITCODE -ne 0) {
    Write-Error "E2E Test Suite execution encountered errors or failures (Exit code: $LASTEXITCODE)"
    exit $LASTEXITCODE
}
Write-Host "E2E Test Suite execution completed with 100% pass." -ForegroundColor Green
exit 0
