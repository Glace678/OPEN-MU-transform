param(
    [string]$ServerAddress = '192.168.215.56',
    [switch]$RebuildGameData,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\OpenMU-安卓手机版-可安装'),
    [string]$NdkPath = ''
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

if ($ServerAddress -notmatch '^[A-Za-z0-9.-]+$') {
    throw 'ServerAddress must be an IPv4 address or hostname.'
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

function Test-GameDataStale {
    # The archive is rebuilt from MuMain\src\bin, but nothing recorded that fact:
    # a developer who edited Data/ or fonts/ and forgot -RebuildGameData got a
    # successful package embedding the older zip, and because gradle derives
    # GAME_DATA_VERSION from the zip's own sha256 the stale archive is
    # self-consistent, so the on-device freshness check cannot catch it either.
    # Compare the newest source write time against the archive instead -- the
    # same "make the input explicit" guard the build.ninja check below applies to
    # libmain.so.
    param([string]$Archive)

    if (-not (Test-Path -LiteralPath $Archive -PathType Leaf)) {
        return $true
    }
    $archiveTime = (Get-Item -LiteralPath $Archive).LastWriteTimeUtc
    $sourceRoot = Join-Path $muMainRoot 'src\bin'
    foreach ($relativeRoot in @('Data', 'fonts', 'config.ini.template')) {
        $absoluteRoot = Join-Path $sourceRoot $relativeRoot
        if (Test-Path -LiteralPath $absoluteRoot -PathType Leaf) {
            if ((Get-Item -LiteralPath $absoluteRoot).LastWriteTimeUtc -gt $archiveTime) {
                return $true
            }
        } elseif (Test-Path -LiteralPath $absoluteRoot -PathType Container) {
            foreach ($file in [IO.Directory]::EnumerateFiles($absoluteRoot, '*', [IO.SearchOption]::AllDirectories)) {
                if ((Get-Item -LiteralPath $file).LastWriteTimeUtc -gt $archiveTime) {
                    return $true
                }
            }
        }
    }
    return $false
}

if ($RebuildGameData -or (Test-GameDataStale -Archive $assetArchive)) {
    New-GameDataArchive -Destination $assetArchive
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
Push-Location $projectRoot
try {
    & (Join-Path $projectRoot 'gradlew.bat') `
        ':game-app:lintDebug' ':gm-app:lintDebug' `
        ':game-app:assembleDebug' ':gm-app:assembleDebug' `
        "-POPENMU_SERVER_ADDRESS=$ServerAddress" `
        "-POPENMU_MOBILE_PACKAGE_KEY=$mobilePackageKey"
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
$gameApk = Join-Path $projectRoot 'game-app\build\outputs\apk\debug\game-app-debug.apk'
$gmApk = Join-Path $projectRoot 'gm-app\build\outputs\apk\debug\gm-app-debug.apk'
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

$buildTools = Join-Path ${env:ProgramFiles(x86)} 'Android\android-sdk\build-tools\36.0.0\apksigner.bat'
foreach ($apk in @('OpenMU-Game-arm64.apk', 'OpenMU-GM.apk')) {
    & $buildTools verify --verbose (Join-Path $outputStaging $apk)
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
