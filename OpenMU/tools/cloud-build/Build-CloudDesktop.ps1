[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('osx-arm64', 'osx-x64', 'linux-x64')][string]$Runtime
)

$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$client = Join-Path $root 'source/MuMain'
$server = Join-Path $root 'source/OpenMU'
$logs = Join-Path $root 'build-logs'
$dist = Join-Path $root 'dist'
[void][IO.Directory]::CreateDirectory($logs)
[void][IO.Directory]::CreateDirectory($dist)

function Invoke-Build([string]$Name, [string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $logs "$Name.log")
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit $LASTEXITCODE)." }
}

$cmakeArguments = @('-S', $client, '-B', 'build-client', '-G', 'Ninja',
    '-DCMAKE_BUILD_TYPE=Release', '-DENABLE_EDITOR=OFF', '-DBUILD_TESTING=ON')
if ($IsMacOS) {
    $brew = (& brew --prefix).Trim()
    $bison = (& brew --prefix bison).Trim()
    $env:PATH = "$bison/bin$([IO.Path]::PathSeparator)$env:PATH"
    $cmakeArguments += "-DCMAKE_PREFIX_PATH=$brew;$brew/opt/jpeg-turbo;$brew/opt/glew"
}
Invoke-Build 'configure-client' 'cmake' $cmakeArguments
Invoke-Build 'build-client' 'cmake' @('--build', 'build-client', '--target', 'Main', 'core_input_timing_tests', 'rime_runtime_smoke', '--parallel', '3', '--', '-k', '0')
Invoke-Build 'input-timing-tests' (Join-Path $root 'build-client/tests/core/core_input_timing_tests') @()
Invoke-Build 'platform-rime-tests' 'ctest' @('--test-dir', 'build-client', '--output-on-failure', '-R', 'client_library_|rime_runtime_smoke')

# Compile a relocatable private PostgreSQL runtime; never copy a configured cluster.
$postgresVersion = '17.11'
$postgresHash = 'DD27F2B3C59E73ED14AA3324901242BF69A032A6347805F274E6260322D42979'
$postgresArchive = Join-Path $root "postgresql-$postgresVersion.tar.bz2"
Invoke-WebRequest "https://ftp.postgresql.org/pub/source/v$postgresVersion/postgresql-$postgresVersion.tar.bz2" -OutFile $postgresArchive
if ((Get-FileHash $postgresArchive -Algorithm SHA256).Hash -ne $postgresHash) { throw 'PostgreSQL source hash mismatch.' }
Invoke-Build 'extract-postgres' 'tar' @('-xf', $postgresArchive, '-C', $root)
$postgresSource = Join-Path $root "postgresql-$postgresVersion"
$postgresRuntime = Join-Path $root 'postgres-runtime'
Push-Location $postgresSource
try {
    Invoke-Build 'configure-postgres' './configure' @("--prefix=$postgresRuntime", '--without-readline', '--without-icu', '--without-ldap')
    Invoke-Build 'build-postgres' 'make' @('-j3')
    Invoke-Build 'install-postgres' 'make' @('install')
} finally { Pop-Location }

$package = Join-Path $dist "OpenMU-Solo-$Runtime-unsigned"
& (Join-Path $server 'tools/local-package/Build-DesktopPackage.ps1') `
    -Runtime $Runtime -GameDirectory (Join-Path $root 'build-client/src') `
    -PostgreSqlDirectory $postgresRuntime -PostgreSqlLicense (Join-Path $postgresSource 'COPYRIGHT') `
    -OutputDirectory $package -Version '0.9.10-solo.3'
if ($LASTEXITCODE -ne 0) { throw 'Desktop package creation failed.' }

Invoke-Build 'verify-package' (Join-Path $package 'App/GMHost/OpenMU-GM') @('--root', $package, '--verify')
$smokeRoot = Join-Path ([IO.Path]::GetTempPath()) "openmu-package-smoke-$([Guid]::NewGuid().ToString('N'))"
Invoke-Build 'copy-smoke-package' 'cp' @('-a', $package, $smokeRoot)
$env:OPENMU_LOCAL_STACK_SMOKE_ROOT = $smokeRoot
try {
    Invoke-Build 'package-lifecycle-tests' 'dotnet' @('test',
        (Join-Path $server 'tests/MUnique.OpenMU.LocalLauncher.Tests/MUnique.OpenMU.LocalLauncher.Tests.csproj'),
        '-f', 'net10.0', '-p:TargetFrameworks=net10.0', '-p:RunAnalyzers=false',
        '--filter', 'FullyQualifiedName~OpenMuServerManagerTests|FullyQualifiedName~LocalStackPackageSmokeTests',
        '--logger', 'console;verbosity=normal', '--', 'NUnit.ExplicitMode=Relaxed')
} finally { Remove-Item Env:OPENMU_LOCAL_STACK_SMOKE_ROOT }
$archive = "$package.tar.gz"
Invoke-Build 'archive-package' 'tar' @('-czf', $archive, '-C', $dist, (Split-Path $package -Leaf))
"$((Get-FileHash $archive -Algorithm SHA256).Hash)  $([IO.Path]::GetFileName($archive))" |
    Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
[ordered]@{
    runtime = $Runtime
    package = [IO.Path]::GetFileName($archive)
    nativeClient = 'compiled'
    controllerCore = 'passed'
    nativeRime = 'passed'
    manifest = 'passed'
    databaseBackupRestoreShutdown = 'passed'
    repeatedConnectHandshake = 'passed'
    graphics = 'not-yet-verified'
    fullPlaythrough = 'not-verified'
    physicalHaptics = 'not-verified'
    signed = $false
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dist 'verification.json') -Encoding utf8NoBOM
