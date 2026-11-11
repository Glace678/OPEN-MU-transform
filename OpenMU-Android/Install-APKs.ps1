param([string]$Device)

$ErrorActionPreference = 'Stop'
$sdkRoot = if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { Join-Path ${env:ProgramFiles(x86)} 'Android\android-sdk' }
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
if (-not (Test-Path -LiteralPath $adb -PathType Leaf)) {
    throw "adb.exe not found: $adb"
}

$devices = @(& $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\sdevice$' } | ForEach-Object { ($_ -split '\s+')[0] })
if ($Device) {
    if ($Device -notin $devices) { throw "Android device is not authorized: $Device" }
    $selector = @('-s', $Device)
} elseif ($devices.Count -eq 1) {
    $selector = @('-s', $devices[0])
} else {
    throw "Connect and authorize exactly one Android device. Authorized devices: $($devices -join ', ')"
}

$apks = @('OpenMU-GM.apk', 'OpenMU-Game-arm64.apk')
$sumsPath = Join-Path $PSScriptRoot 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $sumsPath -PathType Leaf)) {
    throw "Missing checksum manifest: $sumsPath"
}
$expectedHashes = @{}
foreach ($rawLine in (Get-Content -LiteralPath $sumsPath -Encoding UTF8)) {
    $line = $rawLine.Trim()
    if ([string]::IsNullOrEmpty($line)) { continue }
    if ($line -notmatch '^([0-9a-fA-F]{64})\s+\*?([A-Za-z0-9._-]+)$') {
        throw "Malformed line in SHA256SUMS.txt: $rawLine"
    }
    $hash = $Matches[1].ToLowerInvariant()
    $name = $Matches[2]
    if ($expectedHashes.ContainsKey($name)) {
        throw "Duplicate entry in SHA256SUMS.txt: $name"
    }
    $expectedHashes[$name] = $hash
}
foreach ($apk in $apks) {
    $path = Join-Path $PSScriptRoot $apk
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing APK: $path" }
    if (-not $expectedHashes.ContainsKey($apk)) {
        throw "APK missing from SHA256SUMS.txt: $apk"
    }
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHashes[$apk]) {
        throw "SHA-256 mismatch for ${apk}: expected $($expectedHashes[$apk]), got $actualHash"
    }
}

foreach ($apk in $apks) {
    $path = Join-Path $PSScriptRoot $apk
    & $adb @selector install -r $path
    if ($LASTEXITCODE -ne 0) { throw "Installation failed: $apk" }
}
