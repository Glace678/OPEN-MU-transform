param(
    [string]$ServerAddress = '192.168.215.56',
    [switch]$RebuildGameData,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\OpenMU-安卓手机版-可安装'),
    [string]$NdkPath = '',
    # A-03: Debug (default, sideload preview, debug-signed) or Release. Release
    # requires a keystore.properties / env signing material (see game-app and
    # gm-app build.gradle) and runs assembleRelease instead of assembleDebug.
    [ValidateSet('Debug','Release')][string]$BuildType = 'Debug',
    # A-02: resolve apksigner/build-tools from an explicit SDK root instead of a
    # hardcoded Program Files path. Falls back to ANDROID_SDK_ROOT / local.properties.
    [string]$AndroidSdkRoot = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '..'))
$muMainRoot = Join-Path $workspaceRoot 'MuMain'
$assetArchive = Join-Path $projectRoot 'game-app\src\main\assets\game-data.zip'
$nativeRoot = Join-Path $projectRoot 'native\arm64-v8a'
$nativeBuildDirectory = Join-Path $muMainRoot 'out\build\android-arm64-release'
$builtMainLibrary = Join-Path $nativeBuildDirectory 'src\libmain.so'
$builtGlLibrary = Join-Path $nativeBuildDirectory 'src\ThirdParty\gl4es\libGL.so'
$builtSdlLibrary = Join-Path $nativeBuildDirectory 'src\ThirdParty\SDL\libSDL3.so'
# NDK 根目录解析顺序：-NdkPath 参数 > OPENMU_ANDROID_NDK_PATH / ANDROID_NDK_HOME /
# ANDROID_NDK_ROOT 环境变量 > 原开发机默认路径。找不到时明确报错而不是静默用错工具链。
$androidNdkRoot = $NdkPath
foreach ($candidate in @($env:OPENMU_ANDROID_NDK_PATH, $env:ANDROID_NDK_HOME, $env:ANDROID_NDK_ROOT)) {
    if (-not $androidNdkRoot -and $candidate) { $androidNdkRoot = $candidate }
}
if (-not $androidNdkRoot) {
    $androidNdkRoot = 'C:\Program Files (x86)\Android\AndroidNDK\android-ndk-r27c'
}
if (-not (Test-Path -LiteralPath $androidNdkRoot)) {
    throw "Android NDK not found at '$androidNdkRoot'. Pass -NdkPath <ndk-root> or set ANDROID_NDK_HOME."
}
$serverPackage = Join-Path $projectRoot 'server-build\OpenMU-Local'
$mobileServerSettings = Join-Path $projectRoot 'mobile-server-settings.json'
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$outputStaging = $outputRoot + '.pending'
$outputBackup = $outputRoot + '.backup'

# A-06: the old regex ^[A-Za-z0-9.-]+$ accepted "." alone, "-" alone, leading/
# trailing hyphens and consecutive dots -- a build would succeed but bake an
# unusable default address into the APK. Validate IPv4 strictly, else treat the
# value as a hostname with proper label rules (no empty labels, no leading or
# trailing hyphens). This mirrors the runtime LocalIpv4Address policy.
function Test-ServerAddress([string]$value) {
    $value = $value.Trim()
    if ([string]::IsNullOrEmpty($value)) { return $false }
    if ($value -match '^\d{1,3}(\.\d{1,3}){3}$') {
        foreach ($octet in $value.Split('.')) {
            if ($octet.Length -gt 1 -and $octet.StartsWith('0')) { return $false }
            $n = 0
            if (-not [int]::TryParse($octet, [ref]$n)) { return $false }
            if ($n -lt 0 -or $n -gt 255) { return $false }
        }
        return $true
    }
    if ($value.EndsWith('.')) { return $false }
    foreach ($label in $value.Split('.')) {
        if ($label.Length -eq 0 -or $label.Length -gt 63) { return $false }
        if ($label -notmatch '^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?$') { return $false }
    }
    return $true
}
if (-not (Test-ServerAddress $ServerAddress)) {
    throw "ServerAddress '$ServerAddress' is not a valid IPv4 address or hostname " +
        '(no empty labels, no leading/trailing hyphens, no leading-zero octets).'
}
if ($outputRoot -eq $workspaceRoot -or $outputRoot -eq $projectRoot `
    -or $outputRoot -eq [IO.Path]::GetPathRoot($outputRoot)) {
    throw "OutputDirectory is too broad: $outputRoot"
}
if (-not (Test-Path -LiteralPath $mobileServerSettings -PathType Leaf)) {
    throw "Missing mobile server settings: $mobileServerSettings"
}

try {
    $mobileSettings = Get-Content -LiteralPath $mobileServerSettings -Raw | ConvertFrom-Json
} catch {
    throw "Invalid mobile server settings JSON: $mobileServerSettings"
}
$mobilePackageKey = [string]$mobileSettings.MobilePackageKey
if ($mobilePackageKey -notmatch '^[A-Za-z0-9_-]{43}$') {
    throw 'MobilePackageKey must be a 32-byte base64url value without padding.'
}
if ($mobileSettings.MobileAccessEnabled -ne $true -or $mobileSettings.AutomaticGameLogin -ne $true) {
    throw 'The Android delivery requires MobileAccessEnabled and AutomaticGameLogin.'
}

function Remove-DeliveryTree {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [IO.Path]::GetFullPath($Path)
    if ($resolved -ne $outputStaging -and $resolved -ne $outputBackup) {
        throw "Refusing to remove an unexpected delivery path: $resolved"
    }
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            if ([IO.Directory]::Exists($resolved)) {
                [IO.Directory]::Delete($resolved, $true)
            } elseif ([IO.File]::Exists($resolved)) {
                [IO.File]::Delete($resolved)
            }
            return
        } catch {
            if ($attempt -eq 5) {
                throw
            }
            Start-Sleep -Milliseconds (250 * $attempt)
        }
    }
}

function New-GameDataArchive {
    param([string]$Destination)

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $sourceRoot = Join-Path $muMainRoot 'src\bin'
    $temporary = $Destination + '.tmp'
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Destination)) | Out-Null
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Force
    }

    $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            foreach ($relativeRoot in @('Data', 'fonts')) {
                $absoluteRoot = Join-Path $sourceRoot $relativeRoot
                foreach ($file in [IO.Directory]::EnumerateFiles($absoluteRoot, '*', [IO.SearchOption]::AllDirectories)) {
                    $baseUri = New-Object System.Uri(($sourceRoot.TrimEnd('\') + '\'))
                    $relative = [Uri]::UnescapeDataString($baseUri.MakeRelativeUri((New-Object System.Uri($file))).ToString()).Replace('\', '/')
                    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                        $archive, $file, $relative, [IO.Compression.CompressionLevel]::Fastest) | Out-Null
                }
            }
            $configTemplate = Join-Path $sourceRoot 'config.ini.template'
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $configTemplate, 'config.ini.template', [IO.Compression.CompressionLevel]::Fastest) | Out-Null
        } finally {
            $archive.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
    Move-Item -LiteralPath $temporary -Destination $Destination -Force
}

# A-05: the previous guard only compared the newest source write time against the
# archive. That catches edits but NOT deletions: if a Data/ or fonts/ source file
# is removed and -RebuildGameData is not passed, no remaining file is newer than
# the archive, so the stale archive (with the deleted file still inside) is reused
# -- and gradle derives GAME_DATA_VERSION from the archive's own sha256, so the
# on-device freshness check cannot detect it either. We now record the exact set
# of source inputs (relative path -> length + mtime) in a sidecar manifest and
# rebuild whenever the set changes, including when a file disappears.
function Get-GameDataManifest {
    $sourceRoot = Join-Path $muMainRoot 'src\bin'
    $entries = @{}
    foreach ($relativeRoot in @('Data', 'fonts')) {
        $absoluteRoot = Join-Path $sourceRoot $relativeRoot
        if (Test-Path -LiteralPath $absoluteRoot -PathType Container) {
            $baseUri = New-Object System.Uri(($sourceRoot.TrimEnd('\') + '\'))
            foreach ($file in [IO.Directory]::EnumerateFiles($absoluteRoot, '*', [IO.SearchOption]::AllDirectories)) {
                $relative = [Uri]::UnescapeDataString($baseUri.MakeRelativeUri((New-Object System.Uri($file))).ToString()).Replace('\', '/')
                $item = Get-Item -LiteralPath $file
                $entries[$relative] = "$($item.Length):$($item.LastWriteTimeUtc.Ticks)"
            }
        }
    }
    $template = Join-Path $sourceRoot 'config.ini.template'
    if (Test-Path -LiteralPath $template -PathType Leaf) {
        $item = Get-Item -LiteralPath $template
        $entries['config.ini.template'] = "$($item.Length):$($item.LastWriteTimeUtc.Ticks)"
    }
    return $entries
}

function Test-GameDataStale {
    param([string]$Archive)

    $manifestPath = $Archive + '.sig'
    if (-not (Test-Path -LiteralPath $Archive -PathType Leaf)) {
        return $true
    }
    $current = Get-GameDataManifest
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        return $true
    }
    try {
        $saved = (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
    } catch {
        return $true
    }
    $savedProps = @{}
    foreach ($prop in $saved.PSObject.Properties) { $savedProps[$prop.Name] = [string]$prop.Value }
    # Missing or extra files, or any changed length/mtime, all mean rebuild.
    if ($savedProps.Count -ne $current.Count) { return $true }
    foreach ($key in $current.Keys) {
        if (-not $savedProps.ContainsKey($key) -or $savedProps[$key] -ne $current[$key]) {
            return $true
        }
    }
    return $false
}

function Write-GameDataManifest {
    param([string]$Archive)
    $current = Get-GameDataManifest
    $obj = [ordered]@{}
    foreach ($key in ($current.Keys | Sort-Object)) { $obj[$key] = $current[$key] }
    [IO.File]::WriteAllText($Archive + '.sig',
        ($obj | ConvertTo-Json -Depth 3), [Text.UTF8Encoding]::new($false))
}

if ($RebuildGameData -or (Test-GameDataStale -Archive $assetArchive)) {
    New-GameDataArchive -Destination $assetArchive
    Write-GameDataManifest -Archive $assetArchive
}

# Make the native client an explicit build input. This prevents a successful
# Gradle package from silently embedding an older staged libmain.so after C++
# sources have changed.
$cmakeCommand = Get-Command cmake.exe -ErrorAction SilentlyContinue
$cmakeCandidates = @()
if ($null -ne $cmakeCommand) {
    $cmakeCandidates += $cmakeCommand.Source
}
$cmakeCandidates += 'D:\Microsoft Visual Studio\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
$cmakeExe = $cmakeCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if ($null -eq $cmakeExe) {
    throw 'CMake was not found; refusing to package a potentially stale native client.'
}
if (-not (Test-Path -LiteralPath (Join-Path $nativeBuildDirectory 'build.ninja') -PathType Leaf)) {
    throw "Android native build directory is not configured: $nativeBuildDirectory"
}

& $cmakeExe --build $nativeBuildDirectory --target Main --parallel 4
if ($LASTEXITCODE -ne 0) {
    throw 'Android native client build failed.'
}

# llvm-strip 位于 NDK 按宿主平台命名的 prebuilt 目录，Windows/macOS/Linux 通用。
$ndkPrebuiltHost = if ($IsMacOS) { 'darwin-x86_64' } elseif ($IsLinux) { 'linux-x86_64' } else { 'windows-x86_64' }
$llvmStripName = if ($env:OS -eq 'Windows_NT') { 'llvm-strip.exe' } else { 'llvm-strip' }
$llvmStrip = Join-Path $androidNdkRoot "toolchains/llvm/prebuilt/$ndkPrebuiltHost/bin/$llvmStripName"
if (-not (Test-Path -LiteralPath $llvmStrip -PathType Leaf)) {
    throw "NDK llvm-strip was not found: $llvmStrip"
}
[IO.Directory]::CreateDirectory($nativeRoot) | Out-Null
$builtNativeLibraries = @(
    [PSCustomObject]@{ Source = $builtMainLibrary; Name = 'libmain.so' },
    [PSCustomObject]@{ Source = $builtSdlLibrary; Name = 'libSDL3.so' },
    [PSCustomObject]@{ Source = $builtGlLibrary; Name = 'libGL.so' }
)
foreach ($builtLibrary in $builtNativeLibraries) {
    if (-not (Test-Path -LiteralPath $builtLibrary.Source -PathType Leaf)) {
        throw "Missing native build output: $($builtLibrary.Source)"
    }
    $stagedLibrary = Join-Path $nativeRoot $builtLibrary.Name
    $temporaryLibrary = $stagedLibrary + '.tmp'
    Copy-Item -LiteralPath $builtLibrary.Source -Destination $temporaryLibrary -Force
    try {
        & $llvmStrip --strip-unneeded $temporaryLibrary
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to strip Android native library: $($builtLibrary.Name)"
        }
        Move-Item -LiteralPath $temporaryLibrary -Destination $stagedLibrary -Force
    } finally {
        if (Test-Path -LiteralPath $temporaryLibrary -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryLibrary -Force
        }
    }
}

$requiredNativeLibraries = @(
    'libmain.so',
    'libSDL3.so',
    'libGL.so',
    'libMUnique.Client.Library.so',
    'libc++_shared.so'
)
foreach ($library in $requiredNativeLibraries) {
    $path = Join-Path $nativeRoot $library
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Missing Android arm64 library: $path"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $serverPackage 'OpenMU-Local.exe') -PathType Leaf)) {
    throw "Missing packaged Windows server: $serverPackage"
}
# A-03: choose lint/assemble tasks from -BuildType instead of hardcoding Debug.
# This delivery package talks to the bundled LAN server, so the GM client uses
# cleartext HTTP only against the private LAN address (A-04: release defaults to
# HTTPS; the scheme is a build property, not baked as http:// in the APK).
$buildLower = $BuildType.ToLowerInvariant()
Push-Location $projectRoot
try {
    & (Join-Path $projectRoot 'gradlew.bat') `
        ":game-app:lint$BuildType" ":gm-app:lint$BuildType" `
        ":game-app:assemble$BuildType" ":gm-app:assemble$BuildType" `
        "-POPENMU_SERVER_ADDRESS=$ServerAddress" `
        "-POPENMU_MOBILE_PACKAGE_KEY=$mobilePackageKey" `
        "-POPENMU_SERVER_SCHEME=http"
} finally {
    Pop-Location
}
if ($LASTEXITCODE -ne 0) {
    throw 'Android APK build failed.'
}

if ((Test-Path -LiteralPath $outputBackup) -and -not (Test-Path -LiteralPath $outputRoot)) {
    Move-Item -LiteralPath $outputBackup -Destination $outputRoot
}
if (Test-Path -LiteralPath $outputBackup) {
    Remove-DeliveryTree -Path $outputBackup
}
if (Test-Path -LiteralPath $outputStaging) {
    Remove-DeliveryTree -Path $outputStaging
}
[IO.Directory]::CreateDirectory($outputStaging) | Out-Null
$gameApk = Join-Path $projectRoot "game-app\build\outputs\apk\$buildLower\game-app-$buildLower.apk"
$gmApk = Join-Path $projectRoot "gm-app\build\outputs\apk\$buildLower\gm-app-$buildLower.apk"
if (-not (Test-Path -LiteralPath $gameApk -PathType Leaf)) {
    throw "Expected built APK not found: $gameApk"
}
if (-not (Test-Path -LiteralPath $gmApk -PathType Leaf)) {
    throw "Expected built APK not found: $gmApk"
}
Copy-Item -LiteralPath $gameApk -Destination (Join-Path $outputStaging 'OpenMU-Game-arm64.apk') -Force
Copy-Item -LiteralPath $gmApk -Destination (Join-Path $outputStaging 'OpenMU-GM.apk') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Install-APKs.ps1') -Destination $outputStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Open-Windows-Firewall.ps1') -Destination $outputStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README-zh-CN.md') -Destination (Join-Path $outputStaging '使用说明.md') -Force

$serverDestination = Join-Path $outputStaging 'OpenMU-手机服务器'
[IO.Directory]::CreateDirectory($serverDestination) | Out-Null
Get-ChildItem -LiteralPath $serverPackage -Force |
    Copy-Item -Destination $serverDestination -Recurse -Force
$serverKeys = Join-Path $serverDestination 'Data\Keys'
[IO.Directory]::CreateDirectory($serverKeys) | Out-Null
Copy-Item -LiteralPath $mobileServerSettings `
    -Destination (Join-Path $serverKeys 'local-settings.json') -Force

# A-02: the SDK root was hardcoded to a developer's Program Files path, so a
# machine with a custom SDK location (or without build-tools 36.0.0 exactly)
# failed signing verification and left the delivery half-built. Resolve the SDK
# root from -AndroidSdkRoot, then ANDROID_SDK_ROOT/ANDROID_HOME, then the
# project local.properties sdk.dir, and locate apksigner under build-tools.
$androidSdkRoot = $AndroidSdkRoot
if (-not $androidSdkRoot) {
    foreach ($candidate in @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME)) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { $androidSdkRoot = $candidate; break }
    }
}
if (-not $androidSdkRoot) {
    $localProps = Join-Path $projectRoot 'local.properties'
    if (Test-Path -LiteralPath $localProps -PathType Leaf) {
        foreach ($line in (Get-Content -LiteralPath $localProps)) {
            if ($line -match '^\s*sdk\.dir\s*=(.+)$') {
                $candidate = ($Matches[1].Trim() -replace '\\\\','\')
                if (Test-Path -LiteralPath $candidate) { $androidSdkRoot = $candidate; break }
            }
        }
    }
}
if (-not $androidSdkRoot -or -not (Test-Path -LiteralPath $androidSdkRoot)) {
    throw "Android SDK root not found. Pass -AndroidSdkRoot <sdk-root>, set ANDROID_SDK_ROOT, " +
        "or set sdk.dir in local.properties."
}
$apksigner = Join-Path $androidSdkRoot 'build-tools\36.0.0\apksigner.bat'
if (-not (Test-Path -LiteralPath $apksigner -PathType Leaf)) {
    # Fall back to the newest installed build-tools version.
    $btRoot = Join-Path $androidSdkRoot 'build-tools'
    $newest = Get-ChildItem -LiteralPath $btRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1
    if ($newest) { $apksigner = Join-Path $newest.FullName 'apksigner.bat' }
}
if (-not (Test-Path -LiteralPath $apksigner -PathType Leaf)) {
    throw "apksigner not found under '$androidSdkRoot\build-tools'. Install build-tools 36.0.0."
}
foreach ($apk in @('OpenMU-Game-arm64.apk', 'OpenMU-GM.apk')) {
    & $apksigner verify --verbose (Join-Path $outputStaging $apk)
    if ($LASTEXITCODE -ne 0) {
        throw "APK signature verification failed: $apk"
    }
}

$hashLines = @('OpenMU-Game-arm64.apk', 'OpenMU-GM.apk') |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $outputStaging $_) } |
    Sort-Object Name |
    ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
[IO.File]::WriteAllLines((Join-Path $outputStaging 'SHA256SUMS.txt'), $hashLines, [Text.UTF8Encoding]::new($false))

$outputMovedToBackup = $false
try {
    if (Test-Path -LiteralPath $outputRoot) {
        Move-Item -LiteralPath $outputRoot -Destination $outputBackup
        $outputMovedToBackup = $true
    }
    Move-Item -LiteralPath $outputStaging -Destination $outputRoot
    if ($outputMovedToBackup) {
        Remove-DeliveryTree -Path $outputBackup
    }
} catch {
    if (-not (Test-Path -LiteralPath $outputRoot) -and $outputMovedToBackup `
        -and (Test-Path -LiteralPath $outputBackup)) {
        Move-Item -LiteralPath $outputBackup -Destination $outputRoot
    }
    throw
}
Write-Output $outputRoot
