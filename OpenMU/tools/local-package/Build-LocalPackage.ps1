param(
    [Parameter(Mandatory = $true)]
    [string]$GamePublishDirectory,

    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$PostgreSqlSha256,

    [string]$PostgreSqlArchivePath,

    [string]$VisualCppRuntimeDirectory,

    [string]$VisualCppRedistNoticePath,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\artifacts'),

    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.9.10-local.1'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$postgresDescriptor = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'postgresql-runtime.json') -Raw | ConvertFrom-Json
$resolvedPostgreSqlSha256 = if ([string]::IsNullOrWhiteSpace($PostgreSqlSha256)) {
    [string]$postgresDescriptor.sha256
} else {
    $PostgreSqlSha256
}
if ($resolvedPostgreSqlSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
    throw 'The pinned PostgreSQL SHA-256 is missing or malformed.'
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$packageRoot = Join-Path $outputRoot 'OpenMU-Local'
$serverPublish = Join-Path $outputRoot '.server-publish'
$launcherPublish = Join-Path $outputRoot '.launcher-publish'
$gameSource = [IO.Path]::GetFullPath($GamePublishDirectory)
$gameConfigTemplate = Join-Path $gameSource 'config.ini.template'
. (Join-Path $PSScriptRoot 'GamePayloadPackaging.ps1')

function Resolve-VisualCppRuntimeDirectory([string]$RequestedDirectory) {
    $candidates = [Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($RequestedDirectory)) {
        $candidates.Add([IO.Path]::GetFullPath($RequestedDirectory))
    }

    if (-not [string]::IsNullOrWhiteSpace($env:VCToolsRedistDir)) {
        $redistRoot = [IO.Path]::GetFullPath($env:VCToolsRedistDir)
        Get-ChildItem -LiteralPath (Join-Path $redistRoot 'x64') -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue |
            ForEach-Object { $candidates.Add($_.FullName) }
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $installationPaths = & $vswhere -products '*' -property installationPath
        foreach ($installationPath in $installationPaths) {
            $versionRoots = Get-ChildItem -LiteralPath (Join-Path $installationPath 'VC\Redist\MSVC') -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
                Sort-Object { [Version]$_.Name } -Descending
            foreach ($versionRoot in $versionRoots) {
                Get-ChildItem -LiteralPath (Join-Path $versionRoot.FullName 'x64') -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue |
                    ForEach-Object { $candidates.Add($_.FullName) }
            }
        }
    }

    $requiredFiles = @('msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll')
    foreach ($candidate in $candidates | Select-Object -Unique) {
        if ((Split-Path -Leaf (Split-Path -Parent $candidate)) -ne 'x64') {
            continue
        }

        $missingRequired = $requiredFiles | Where-Object {
            -not (Test-Path -LiteralPath (Join-Path $candidate $_) -PathType Leaf)
        }
        if ((Test-Path -LiteralPath $candidate -PathType Container) -and -not $missingRequired) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }

    throw '找不到完整的 x64 Microsoft Visual C++ 应用本地运行库。请通过 -VisualCppRuntimeDirectory 指定 Visual Studio 的 Microsoft.VC*.CRT 目录。'
}

function Resolve-VisualCppRedistNotice([string]$RequestedPath, [string]$RuntimeDirectory) {
    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = [IO.Path]::GetFullPath($RequestedPath)
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf) -or (Get-Item -LiteralPath $resolved).Length -eq 0) {
            throw "Visual C++ 再发行说明不存在：$resolved"
        }

        return $resolved
    }

    $current = Get-Item -LiteralPath $RuntimeDirectory
    while ($null -ne $current) {
        $candidate = Join-Path $current.FullName 'Licenses\1033\Redist.txt'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }

        $current = $current.Parent
    }

    throw '找不到 Visual Studio Redist.txt。请通过 -VisualCppRedistNoticePath 指定随 Visual Studio 安装的再发行说明。'
}

function Test-PathWithin([string]$Candidate, [string]$Parent) {
    $relative = [IO.Path]::GetRelativePath($Parent, $Candidate)
    $parentPrefix = '..' + [IO.Path]::DirectorySeparatorChar
    return $relative -eq '.' -or (-not [IO.Path]::IsPathRooted($relative) -and $relative -ne '..' -and -not $relative.StartsWith($parentPrefix, [StringComparison]::Ordinal))
}

if (-not (Test-Path -LiteralPath (Join-Path $gameSource 'Main.exe'))) {
    throw "Game publish directory does not contain Main.exe: $gameSource"
}
Assert-GameConfigTemplate -TemplatePath $gameConfigTemplate

if ((Test-PathWithin $gameSource $outputRoot) -or (Test-PathWithin $outputRoot $gameSource)) {
    throw 'GamePublishDirectory and OutputDirectory must not contain each other.'
}

$visualCppRuntime = Resolve-VisualCppRuntimeDirectory $VisualCppRuntimeDirectory
$visualCppNotice = Resolve-VisualCppRedistNotice $VisualCppRedistNoticePath $visualCppRuntime

foreach ($path in $packageRoot,$serverPublish,$launcherPublish) {
    Remove-PackagingDirectorySafely -Path $path
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
dotnet publish (Join-Path $repositoryRoot 'src\Startup\MUnique.OpenMU.Startup.csproj') `
    -c Release -r win-x64 --self-contained true --disable-build-servers -m:1 `
    -p:ci=true -p:BuildInParallel=false -p:UseSharedCompilation=false `
    -p:GeneratePersistenceModels=false -p:PersistenceGeneratorRunning=true `
    -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishAot=false `
    -o $serverPublish
if ($LASTEXITCODE -ne 0) { throw 'OpenMU server publish failed.' }

dotnet publish (Join-Path $repositoryRoot 'src\LocalLauncher\MUnique.OpenMU.LocalLauncher.csproj') `
    -c Release -r win-x64 --self-contained true --disable-build-servers -m:1 `
    -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GeneratePersistenceModels=false `
    -p:Version=$Version -p:InformationalVersion=$Version `
    -p:PublishSingleFile=true -p:PublishTrimmed=false -p:PublishAot=false `
    -o $launcherPublish
if ($LASTEXITCODE -ne 0) { throw 'OpenMU local launcher publish failed.' }

$directories = @(
    'App\Game', 'App\Server', 'Runtime', 'Data\PostgreSQL', 'Data\Keys',
    'Data\Logs', 'Data\Backups', 'Licenses'
)
foreach ($directory in $directories) {
    New-Item -ItemType Directory -Path (Join-Path $packageRoot $directory) -Force | Out-Null
}

Copy-GamePayload -SourceDirectory $gameSource -DestinationDirectory (Join-Path $packageRoot 'App\Game')
Get-ChildItem -LiteralPath $serverPublish -Force | Copy-Item -Destination (Join-Path $packageRoot 'App\Server') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $launcherPublish 'OpenMU-Local.exe') -Destination (Join-Path $packageRoot 'OpenMU-Local.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README-zh-CN.txt') -Destination (Join-Path $packageRoot 'README-简体中文.txt')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools\balance\README.md') -Destination (Join-Path $packageRoot 'Solo-Balance.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination (Join-Path $packageRoot 'Licenses\OpenMU-MIT.txt')
Copy-Item -LiteralPath $visualCppNotice -Destination (Join-Path $packageRoot 'Licenses\Microsoft-Visual-Cpp-Redistributables.txt')

& (Join-Path $PSScriptRoot 'Download-PostgreSql.ps1') `
    -ExpectedSha256 $resolvedPostgreSqlSha256 `
    -SourceArchive $PostgreSqlArchivePath `
    -Destination (Join-Path $packageRoot 'Runtime\PostgreSQL')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL runtime download failed.' }

$visualCppFiles = Get-ChildItem -LiteralPath $visualCppRuntime -Filter '*.dll' -File
if ($visualCppFiles.Count -eq 0) {
    throw "Visual C++ 应用本地运行库目录中没有 DLL：$visualCppRuntime"
}
foreach ($destination in @(
    (Join-Path $packageRoot 'App\Game'),
    (Join-Path $packageRoot 'Runtime\PostgreSQL\bin'))) {
    $visualCppFiles | Copy-Item -Destination $destination -Force
}

$postgresLicense = @(
    (Join-Path $packageRoot 'Runtime\PostgreSQL\doc\COPYRIGHT'),
    (Join-Path $packageRoot 'Runtime\PostgreSQL\COPYRIGHT'),
    (Join-Path $packageRoot 'Runtime\PostgreSQL\server_license.txt')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
Copy-Item -LiteralPath $postgresLicense -Destination (Join-Path $packageRoot 'Licenses\PostgreSQL.txt')
$postgresThirdPartyLicense = Join-Path $packageRoot 'Runtime\PostgreSQL\commandlinetools_3rd_party_licenses.txt'
if (-not (Test-Path -LiteralPath $postgresThirdPartyLicense)) {
    throw 'The PostgreSQL runtime is missing its command-line third-party license file.'
}
Copy-Item -LiteralPath $postgresThirdPartyLicense -Destination (Join-Path $packageRoot 'Licenses\PostgreSQL-ThirdParty.txt')

& (Join-Path $PSScriptRoot 'New-PackageManifest.ps1') `
    -PackageRoot $packageRoot `
    -Version $Version `
    -PostgreSqlVersion $postgresDescriptor.version `
    -PostgreSqlArchiveSha256 $resolvedPostgreSqlSha256 `
    -PostgreSqlSourceUrl $postgresDescriptor.url | Out-Null
$archivePath = Join-Path $outputRoot "OpenMU-Local-$Version-win-x64.zip"
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath -CompressionLevel Optimal

foreach ($path in $serverPublish,$launcherPublish) {
    Remove-PackagingDirectorySafely -Path $path
}

Write-Output $archivePath
