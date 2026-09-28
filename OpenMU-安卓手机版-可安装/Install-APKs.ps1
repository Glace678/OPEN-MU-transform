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

foreach ($apk in @('OpenMU-GM.apk', 'OpenMU-Game-arm64.apk')) {
    $path = Join-Path $PSScriptRoot $apk
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing APK: $path" }
    & $adb @selector install -r $path
    if ($LASTEXITCODE -ne 0) { throw "Installation failed: $apk" }
}
