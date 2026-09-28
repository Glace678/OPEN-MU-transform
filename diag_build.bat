@echo off
call "D:\Microsoft Visual Studio\VC\Auxiliary\Build\vcvars32.bat" >nul 2>&1
cd /d "D:\openmu自用\MuMain\out\build\windows-x86"
"D:\Microsoft Visual Studio\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build . --config RelWithDebInfo
exit /b %ERRORLEVEL%
