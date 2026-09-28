$root = (@($env:MU_SERVER_ROOT, 'C:\OpenMU-Local') | Where-Object { $_ } |
    Select-Object -First 1)
$ws = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath('Desktop')
$path = Join-Path $desktop 'OpenMU-Solo.lnk'
$lnk = $ws.CreateShortcut($path)
$lnk.TargetPath = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
$lnk.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $root 'play.ps1') + '"'
$lnk.WorkingDirectory = $root
$lnk.IconLocation = (Join-Path $root 'App\Game\Main.exe') + ',0'
$lnk.Description = 'OpenMU local single-player'
$lnk.WindowStyle = 1
$lnk.Save()
if (Test-Path $path) { Write-Output ('SHORTCUT_OK=' + $path) } else { Write-Output 'SHORTCUT_FAILED' }
