[CmdletBinding()]
param([switch]$SkipEngineProbe)

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    $dotnet = Get-Command dotnet -ErrorAction Stop
    $engineRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\OpenMU'))
    $criticalFiles = @(
        'src\GameLogic\AttackableExtensions.cs',
        'src\GameLogic\DefaultDropGenerator.cs',
        'src\GameLogic\ItemPriceCalculator.cs',
        'src\GameLogic\PlayerExperience.cs',
        'src\GameLogic\SoloBalance.cs',
        'src\Persistence\Initialization\SoloBalanceInitializer.cs',
        'src\Persistence\Initialization\VersionSeasonSix\GameConfigurationInitializer.cs'
    )
    $before = @{}
    foreach ($relative in $criticalFiles) {
        $candidate = Join-Path $engineRoot $relative
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $before[$relative] = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
        }
    }
    if (-not $SkipEngineProbe) {
        & $dotnet.Source run --project integration\EngineProbe -- artifacts
        if ($LASTEXITCODE -ne 0) {
            throw 'Read-only engine catalog probe failed. No live configuration was changed.'
        }
    }
    & $dotnet.Source run --project src\BalanceLab -- report design\balance.v1.json artifacts
    if ($LASTEXITCODE -ne 0) {
        throw 'Candidate balance checks failed. Inspect artifacts\verification.json and the report.'
    }
    $sourceChecks = foreach ($relative in $before.Keys) {
        $after = (Get-FileHash -LiteralPath (Join-Path $engineRoot $relative) -Algorithm SHA256).Hash
        [pscustomobject]@{ File = $relative; Sha256Before = $before[$relative]; Sha256After = $after; Unchanged = $after -eq $before[$relative] }
    }
    $manifest = [ordered]@{
        Kind = 'isolated-candidate-verification'
        CompletedUtc = [DateTime]::UtcNow.ToString('O')
        DotnetVersion = (& $dotnet.Source --version)
        DesignSha256 = (Get-FileHash -LiteralPath 'design\balance.v1.json' -Algorithm SHA256).Hash
        ContentPoliciesSha256 = (Get-FileHash -LiteralPath 'design\content-policies.v1.json' -Algorithm SHA256).Hash
        LiveProfileInstalled = $false
        DatabaseOpened = $false
        SourceChecks = @($sourceChecks)
    }
    [IO.File]::WriteAllText(
        (Join-Path $PSScriptRoot 'artifacts\run-manifest.json'),
        ($manifest | ConvertTo-Json -Depth 6),
        [Text.UTF8Encoding]::new($false))
    if (@($sourceChecks | Where-Object { -not $_.Unchanged }).Count -gt 0) {
        throw 'An original source file changed during verification. Inspect for concurrent edits; no rollback was attempted.'
    }
    Write-Host 'Independent balance candidate verified. This does not install the profile into OpenMU.'
}
finally {
    Pop-Location
}
