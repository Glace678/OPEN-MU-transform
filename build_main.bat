@echo off
call "D:\Microsoft Visual Studio\VC\Auxiliary\Build\vcvars32.bat" >nul 2>&1
"D:\Microsoft Visual Studio\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build "%~dp0MuMain\out\build\windows-x86" --config RelWithDebInfo --target Main > "%~dp0build_log.txt" 2>&1
exit /b %ERRORLEVEL%
