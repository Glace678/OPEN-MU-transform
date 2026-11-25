$root = (@($env:MU_SERVER_ROOT, 'C:\OpenMU-Local') | Where-Object { $_ } |
    Select-Object -First 1)
$entryPoint = Join-Path $root 'OpenMU-Local.exe'
if (-not (Test-Path -LiteralPath $entryPoint -PathType Leaf)) {
    throw "Launcher not found: $entryPoint. Build the local package first or set MU_SERVER_ROOT."
}

$ws = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath('Desktop')
$path = Join-Path $desktop 'OpenMU-Solo.lnk'
$lnk = $ws.CreateShortcut($path)
$lnk.TargetPath = $entryPoint
$lnk.Arguments = ''
$lnk.WorkingDirectory = $root
$gameExe = Join-Path $root 'App\Game\Main.exe'
$lnk.IconLocation = if (Test-Path -LiteralPath $gameExe -PathType Leaf) { $gameExe + ',0' } else { $entryPoint + ',0' }
$lnk.Description = 'OpenMU local single-player'
$lnk.WindowStyle = 1
$lnk.Save()
if (Test-Path $path) { Write-Output ('SHORTCUT_OK=' + $path) } else { Write-Output 'SHORTCUT_FAILED' }
