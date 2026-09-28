param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PostgreSqlVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$PostgreSqlArchiveSha256,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^https://get\.enterprisedb\.com/postgresql/.+\.zip$')]
    [string]$PostgreSqlSourceUrl
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PackageRoot)
$manifestPath = Join-Path $root 'manifest.json'
$excludedFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[void]$excludedFiles.Add([IO.Path]::GetFullPath((Join-Path $root 'App\Game\config.ini')))
$excludedRoots = @(
    [IO.Path]::GetFullPath((Join-Path $root 'Data\PostgreSQL')),
    [IO.Path]::GetFullPath((Join-Path $root 'Data\Keys')),
    [IO.Path]::GetFullPath((Join-Path $root 'Data\Logs')),
    [IO.Path]::GetFullPath((Join-Path $root 'Data\Backups'))
)

$files = Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    $fullName = $_.FullName
    $fullName -ne $manifestPath -and
    -not $excludedFiles.Contains($fullName) -and
    -not ($excludedRoots | Where-Object { $fullName.StartsWith($_ + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })
} | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
        size = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
}

$manifest = [ordered]@{
    formatVersion = 1
    version = $Version
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    components = [ordered]@{
        postgresql = [ordered]@{
            version = $PostgreSqlVersion
            archiveSha256 = $PostgreSqlArchiveSha256.ToUpperInvariant()
            sourceUrl = $PostgreSqlSourceUrl
        }
    }
    files = @($files)
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
Write-Output $manifestPath
