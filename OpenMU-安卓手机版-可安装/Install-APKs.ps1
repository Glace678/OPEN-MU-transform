param([string]$Device)

$ErrorActionPreference = 'Stop'
$sdkRoot = if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { Join-Path ${env:ProgramFiles(x86)} 'Android\android-sdk' }
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
if (-not (Test-Path -LiteralPath $adb -PathType Leaf)) {
    throw "adb.exe not found: $adb"
}

# P-02 / P-01: verify every APK against SHA256SUMS.txt before touching the device.
# Refuse to install a tampered or mismatched APK instead of pushing it blindly.
$sumsFile = Join-Path $PSScriptRoot 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $sumsFile -PathType Leaf)) {
    throw "Missing checksum manifest: $sumsFile"
}
$expected = @{}
foreach ($line in (Get-Content -LiteralPath $sumsFile)) {
    if ($line -match '^\s*([0-9a-fA-F]{64})\s+\*?(.+?)\s*$') {
        $expected[$Matches[2].Trim()] = $Matches[1].ToLower()
    }
}
$apks = @('OpenMU-GM.apk', 'OpenMU-Game-arm64.apk')
foreach ($apk in $apks) {
    $path = Join-Path $PSScriptRoot $apk
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing APK: $path" }
    if (-not $expected.ContainsKey($apk)) { throw "No expected SHA-256 recorded for $apk in SHA256SUMS.txt" }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower()
    if ($actual -ne $expected[$apk]) {
        throw "SHA-256 mismatch for $apk`n  expected: $($expected[$apk])`n  actual:   $actual`nRefusing to install."
    }
    Write-Output "verified $apk $actual"
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

foreach ($apk in $apks) {
    $path = Join-Path $PSScriptRoot $apk
    & $adb @selector install -r $path
    if ($LASTEXITCODE -ne 0) { throw "Installation failed: $apk" }
}

Write-Output 'Installed. On the phone, open OpenMU Game and OpenMU GM; accept the loopback-only'
Write-Output 'warning if prompted, then connect to this PC''s private LAN address.'