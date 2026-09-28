[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')][string]$Runtime,
    [Parameter(Mandatory)][string]$GameDirectory,
    [Parameter(Mandatory)][string]$PostgreSqlDirectory,
    [Parameter(Mandatory)][string]$PostgreSqlLicense,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')][string]$Version = '0.9.10-solo.2'
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'GamePayloadPackaging.ps1')
$output = [IO.Path]::GetFullPath($OutputDirectory)
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$postgres = (Resolve-Path -LiteralPath $PostgreSqlDirectory).Path
$license = (Resolve-Path -LiteralPath $PostgreSqlLicense).Path
$architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
$hostRuntime = if ($IsLinux) { "linux-$architecture" } elseif ($IsMacOS) { "osx-$architecture" } else { 'unsupported' }
if ($Runtime -ne $hostRuntime) {
    throw "Package on the matching native host ($Runtime required; current host: $hostRuntime). Windows cannot validate Unix dependencies or preserve bundle permissions."
}
if (Test-Path -LiteralPath $output) {
    throw "Output must be a new directory. Existing packages and saves are never overwritten: $output"
}
foreach ($source in @($game, $postgres)) {
    $relativeOutput = [IO.Path]::GetRelativePath($source, $output)
    $outside = [IO.Path]::IsPathRooted($relativeOutput) -or $relativeOutput -eq '..' -or $relativeOutput.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal)
    if (-not $outside) {
        throw "Output must not be inside an input payload: $source"
    }
}

function Assert-NativeBinary([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing native payload: $Path" }
    $stream = [IO.File]::OpenRead($Path)
    try {
        $header = [byte[]]::new(32)
        if ($stream.Read($header, 0, $header.Length) -lt 32) { throw "Truncated native binary: $Path" }
    } finally { $stream.Dispose() }
    if ($Runtime.StartsWith('linux-')) {
        $machine = [BitConverter]::ToUInt16($header, 18)
        $expected = if ($Runtime.EndsWith('arm64')) { 183 } else { 62 }
        if ($header[0] -ne 127 -or [Text.Encoding]::ASCII.GetString($header, 1, 3) -ne 'ELF' -or $header[4] -ne 2 -or $header[5] -ne 1 -or $machine -ne $expected) {
            throw "Expected 64-bit $Runtime ELF binary: $Path"
        }
    } else {
        $cpu = [BitConverter]::ToUInt32($header, 4)
        $expected = if ($Runtime.EndsWith('arm64')) { 0x0100000c } else { 0x01000007 }
        if ([Convert]::ToHexString($header, 0, 4) -ne 'CFFAEDFE' -or $cpu -ne $expected) {
            throw "Expected single-architecture $Runtime Mach-O binary: $Path"
        }
    }
}

function Invoke-Checked([string]$File, [string[]]$Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$File failed with exit code $LASTEXITCODE." }
}

$networkLibrary = if ($IsMacOS) { 'MUnique.Client.Library.dylib' } else { 'MUnique.Client.Library.so' }
Assert-NativeBinary (Join-Path $game 'Main')
Assert-NativeBinary (Join-Path $game $networkLibrary)
$rimeLibrary = if ($IsMacOS) { 'librime.dylib' } else { 'librime.so' }
Assert-NativeBinary (Join-Path $game $rimeLibrary)
foreach ($name in @('postgres', 'initdb', 'pg_ctl', 'pg_isready', 'pg_dump', 'pg_restore', 'psql')) {
    Assert-NativeBinary (Join-Path $postgres "bin/$name")
}
foreach ($asset in @('Data/Local/mix.bmd', 'config.ini')) {
    if (-not (Test-Path -LiteralPath (Join-Path $game $asset))) { throw "Missing game asset: $asset" }
}
foreach ($userFile in @('Data/Keys', 'Data/PostgreSQL')) {
    if (Test-Path -LiteralPath (Join-Path $game $userFile)) { throw "Game source contains user data: $userFile" }
}
$template = [IO.Path]::GetFullPath((Join-Path $repo '../MuMain/src/bin/config.ini.template'))
if (-not (Test-Path -LiteralPath $template)) { throw "Missing clean client configuration template: $template" }
Assert-GameConfigTemplate -TemplatePath $template

# Publish to a fresh tree; never ship a configured database or credentials.
[void][IO.Directory]::CreateDirectory($output)
foreach ($directory in @('App/Game', 'App/GameHost', 'App/GMHost', 'App/Server', 'Runtime/PostgreSQL', 'Licenses')) {
    [void][IO.Directory]::CreateDirectory((Join-Path $output $directory))
}
foreach ($directory in @('Data', 'fonts', 'rime', 'Licenses')) {
    $source = Join-Path $game $directory
    if (Test-Path -LiteralPath $source) {
        Invoke-Checked 'cp' @('-a', $source, (Join-Path $output 'App/Game'))
    }
}
foreach ($file in @('Main', $networkLibrary, $rimeLibrary)) {
    Copy-Item -LiteralPath (Join-Path $game $file) -Destination (Join-Path $output "App/Game/$file")
}
Copy-Item -LiteralPath $template -Destination (Join-Path $output 'App/Game/config.ini') -Force
Copy-Item -LiteralPath $template -Destination (Join-Path $output 'App/Game/config.ini.template') -Force
Invoke-Checked 'cp' @('-a', "$postgres/.", (Join-Path $output 'Runtime/PostgreSQL'))
foreach ($entry in @(
    @('GameLauncher/MUnique.OpenMU.GameLauncher.csproj', 'App/GameHost'),
    @('GmLauncher/MUnique.OpenMU.GmLauncher.csproj', 'App/GMHost'),
    @('Startup/MUnique.OpenMU.Startup.csproj', 'App/Server')
)) {
    Invoke-Checked 'dotnet' @('publish', (Join-Path $repo "src/$($entry[0])"), '-c', 'Release',
        '-r', $Runtime, '--self-contained', 'true', '--disable-build-servers',
        '-p:ci=true', '-p:BuildInParallel=false', '-maxcpucount:1', '-p:RunAnalyzers=false',
        '-p:GeneratePersistenceModels=false', '-p:PersistenceGeneratorRunning=true', '-o', (Join-Path $output $entry[1]))
}
# Optional EventPipe/LTTng tracing is not required by the packaged game.
if ($IsLinux) {
    foreach ($directory in @('App/GameHost', 'App/GMHost', 'App/Server')) {
        $provider = Join-Path $output "$directory/libcoreclrtraceptprovider.so"
        if (Test-Path -LiteralPath $provider) { Remove-Item -LiteralPath $provider }
    }
}
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $output 'Licenses/OpenMU-MIT.txt')
Copy-Item -LiteralPath $license -Destination (Join-Path $output 'Licenses/PostgreSQL.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DESKTOP-README.txt') -Destination (Join-Path $output 'README.txt')

foreach ($name in @('Game', 'GM')) {
    $hostDirectory = if ($name -eq 'Game') { 'GameHost' } else { 'GMHost' }
    if ($IsMacOS) {
        $bundle = Join-Path $output "OpenMU-$name.app/Contents"
        [void][IO.Directory]::CreateDirectory((Join-Path $bundle 'MacOS'))
        $entryPath = Join-Path $bundle "MacOS/OpenMU-$name"
        $script = @'
#!/bin/sh
set -eu
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/../../.." && pwd)"
exec "$ROOT/App/HOST/OpenMU-NAME" --root "$ROOT" "$@"
'@
        $script.Replace('HOST', $hostDirectory).Replace('NAME', $name) | Set-Content -LiteralPath $entryPath -Encoding utf8NoBOM
        $plist = [xml]@"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>net.munique.openmu.$($name.ToLowerInvariant())</string>
<key>CFBundleName</key><string>OpenMU-$name</string>
<key>CFBundleExecutable</key><string>OpenMU-$name</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$($Version.Split('-')[0])</string>
</dict></plist>
"@
        $plist.Save((Join-Path $bundle 'Info.plist'))
    } else {
        $entryPath = Join-Path $output "OpenMU-$name"
        $script = @'
#!/bin/sh
set -eu
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
export LD_LIBRARY_PATH="$ROOT/Runtime/Native${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
exec "$ROOT/App/HOST/OpenMU-NAME" --root "$ROOT" "$@"
'@
        $script.Replace('HOST', $hostDirectory).Replace('NAME', $name) | Set-Content -LiteralPath $entryPath -Encoding utf8NoBOM
    }
    Invoke-Checked 'chmod' @('755', $entryPath)
}

& (Join-Path $PSScriptRoot 'Bundle-UnixDependencies.ps1') -PackageDirectory $output

# Run the same validator used at launch over the immutable native binaries and assets.
$mutableGameConfiguration = [IO.Path]::GetFullPath((Join-Path $output 'App/Game/config.ini'))
$entries = @(Get-ChildItem -LiteralPath $output -File -Recurse |
    Where-Object { $_.FullName -ne $mutableGameConfiguration } |
    Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = [IO.Path]::GetRelativePath($output, $_.FullName).Replace('\', '/')
        size = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
})
[ordered]@{
    formatVersion = 1
    version = $Version
    runtime = $Runtime
    acceptance = 'development-unverified'
    files = $entries
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding utf8NoBOM
Invoke-Checked (Join-Path $output 'App/GMHost/OpenMU-GM') @('--root', $output, '--verify')
Write-Output "Development package created: $output"
Write-Output 'Not a release: native playthrough, controller/motor tests and macOS signing/notarization are still required.'
