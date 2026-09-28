$target = 'D:\openmu自用\MuMain\out\build\windows-x86\src\RelWithDebInfo'
if (-not (Test-Path 'D:\mumain_dbg')) {
    New-Item -ItemType Junction -Path 'D:\mumain_dbg' -Target $target | Out-Null
}
Test-Path 'D:\mumain_dbg\Main.exe'
