@echo off
call "D:\Microsoft Visual Studio\VC\Auxiliary\Build\vcvars32.bat" >nul 2>&1
cd /d "%~dp0MuMain"
"D:\Microsoft Visual Studio\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --preset windows-x86 > "%~dp0configure_log.txt" 2>&1
exit /b %ERRORLEVEL%
