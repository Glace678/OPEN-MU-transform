[CmdletBinding()]
param(
    [string]$WorkspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,

    [string]$OutputRoot = (Join-Path $env:LOCALAPPDATA 'OpenMU\BalanceBackups'),

    [string]$Name = ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
)

$ErrorActionPreference = 'Stop'

function Get-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
}

function Test-PathWithin([string]$Candidate, [string]$Parent) {
    $candidateFull = Get-FullPath $Candidate
    $parentFull = Get-FullPath $Parent
    return $candidateFull.Equals($parentFull, [StringComparison]::OrdinalIgnoreCase) -or
        $candidateFull.StartsWith($parentFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Get-GitText([string]$Repository, [string[]]$Arguments) {
    $result = & git -C $Repository @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed for '$Repository': $result"
    }

    return ($result -join [Environment]::NewLine)
}

$workspace = Get-FullPath $WorkspaceRoot
$openMuRoot = Get-FullPath (Join-Path $workspace 'OpenMU')
$muMainRoot = Get-FullPath (Join-Path $workspace 'MuMain')
foreach ($repository in @($openMuRoot, $muMainRoot)) {
    if (-not (Test-Path -LiteralPath (Join-Path $repository '.git') -PathType Container)) {
        throw "Expected git repository not found: $repository"
    }
}

$outputParent = Get-FullPath $OutputRoot
$backupRoot = Get-FullPath (Join-Path $outputParent $Name)
if (-not (Test-PathWithin $backupRoot $outputParent) -or $backupRoot -eq $outputParent) {
    throw "Unsafe backup target: $backupRoot"
}

if (Test-Path -LiteralPath $backupRoot) {
    throw "Backup target already exists: $backupRoot"
}

New-Item -ItemType Directory -Path $backupRoot | Out-Null
if ((Get-Item -LiteralPath $backupRoot -Force).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
    throw "Backup root must not be a reparse point: $backupRoot"
}

$sourceSelections = @(
    [pscustomobject]@{ Repository = 'openmu'; Root = $openMuRoot; Path = 'src\Persistence\Initialization' },
    [pscustomobject]@{ Repository = 'openmu'; Root = $openMuRoot; Path = 'src\DataModel\Configuration' },
    [pscustomobject]@{ Repository = 'openmu'; Root = $openMuRoot; Path = 'src\GameLogic' },
    [pscustomobject]@{ Repository = 'mumain'; Root = $muMainRoot; Path = 'src\bin\Data\Local' },
    [pscustomobject]@{ Repository = 'mumain'; Root = $muMainRoot; Path = 'src\source\Data' },
    [pscustomobject]@{ Repository = 'mumain'; Root = $muMainRoot; Path = 'src\source\Engine\Object\ZzzInfomation.cpp' },
    [pscustomobject]@{ Repository = 'mumain'; Root = $muMainRoot; Path = 'src\source\Network\Server\WSclient.cpp' }
)

$selectedFiles = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($selection in $sourceSelections) {
    $sourcePath = Get-FullPath (Join-Path $selection.Root $selection.Path)
    if (-not (Test-PathWithin $sourcePath $selection.Root)) {
        throw "Source selection escaped its repository: $sourcePath"
    }

    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required balance source is missing: $sourcePath"
    }

    $item = Get-Item -LiteralPath $sourcePath -Force
    $files = if ($item.PSIsContainer) {
        Get-ChildItem -LiteralPath $sourcePath -File -Recurse -Force
    }
    else {
        @($item)
    }

    foreach ($file in $files) {
        if ($file.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to copy reparse point: $($file.FullName)"
        }

        $key = "$($selection.Repository)|$($file.FullName)"
        $selectedFiles[$key] = [pscustomobject]@{
            Repository = $selection.Repository
            RepositoryRoot = $selection.Root
            File = $file
        }
    }
}

foreach ($entry in $selectedFiles.Values) {
    $relativeSource = [IO.Path]::GetRelativePath($entry.RepositoryRoot, $entry.File.FullName)
    $destination = Join-Path $backupRoot (Join-Path 'source' (Join-Path $entry.Repository $relativeSource))
    $destinationDirectory = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $entry.File.FullName -Destination $destination
}

$stateDirectory = Join-Path $backupRoot 'state'
New-Item -ItemType Directory -Path $stateDirectory | Out-Null
$repositories = @(
    [pscustomobject]@{ Name = 'openmu'; Root = $openMuRoot },
    [pscustomobject]@{ Name = 'mumain'; Root = $muMainRoot }
)

$repositoryState = foreach ($repository in $repositories) {
    $head = Get-GitText $repository.Root @('rev-parse', 'HEAD')
    $status = Get-GitText $repository.Root @('status', '--porcelain=v2', '--untracked-files=all')
    $diff = Get-GitText $repository.Root @('diff', '--binary', '--no-ext-diff')
    $stagedDiff = Get-GitText $repository.Root @('diff', '--cached', '--binary', '--no-ext-diff')
    [IO.File]::WriteAllText((Join-Path $stateDirectory "$($repository.Name)-status.txt"), $status, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $stateDirectory "$($repository.Name)-working-tree.patch"), $diff, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $stateDirectory "$($repository.Name)-staged.patch"), $stagedDiff, [Text.UTF8Encoding]::new($false))
    [pscustomobject]@{
        Name = $repository.Name
        SourcePath = $repository.Root
        Head = $head.Trim()
        Dirty = -not [string]::IsNullOrWhiteSpace($status)
    }
}

$readme = @'
This is the immutable pre-balance source and client-data baseline.

It includes OpenMU initialization/configuration/game-logic sources, MuMain
numeric BMD mirrors and client-side formulas, plus both repositories' git state
and binary patches. It does not contain a PostgreSQL dump because the accepted
delivery was still unprovisioned and the live test server used in-memory data.
A persistent database must be dumped and restore-tested before changing an
already-provisioned installation.
'@
[IO.File]::WriteAllText((Join-Path $backupRoot 'README.txt'), $readme.Trim() + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

$manifestFiles = Get-ChildItem -LiteralPath $backupRoot -File -Recurse -Force |
    Sort-Object { [IO.Path]::GetRelativePath($backupRoot, $_.FullName) } |
    ForEach-Object {
        [pscustomobject]@{
            Path = [IO.Path]::GetRelativePath($backupRoot, $_.FullName).Replace('\', '/')
            Length = $_.Length
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }

$manifest = [ordered]@{
    SchemaVersion = 1
    CreatedUtc = [DateTime]::UtcNow.ToString('O')
    Kind = 'pre-balance-source-baseline'
    WorkspaceRoot = $workspace
    Repositories = @($repositoryState)
    PersistentDatabaseIncluded = $false
    Files = @($manifestFiles)
}
$manifestPath = Join-Path $backupRoot 'manifest.json'
[IO.File]::WriteAllText(
    $manifestPath,
    ($manifest | ConvertTo-Json -Depth 8),
    [Text.UTF8Encoding]::new($false))

$writtenManifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($file in $writtenManifest.Files) {
    $candidate = Get-FullPath (Join-Path $backupRoot $file.Path)
    if (-not (Test-PathWithin $candidate $backupRoot) -or -not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Manifest path failed verification: $($file.Path)"
    }

    $actual = Get-FileHash -LiteralPath $candidate -Algorithm SHA256
    if ($actual.Hash -ne $file.Sha256 -or (Get-Item -LiteralPath $candidate).Length -ne $file.Length) {
        throw "Backup verification failed: $($file.Path)"
    }
}

[pscustomobject]@{
    BackupRoot = $backupRoot
    Manifest = $manifestPath
    FileCount = @($manifestFiles).Count
    TotalBytes = ($manifestFiles | Measure-Object Length -Sum).Sum
    ManifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
} | ConvertTo-Json -Depth 4
