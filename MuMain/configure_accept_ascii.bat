@echo off
cd /d "%~dp0"
"D:\Microsoft Visual Studio\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S . -B out/build/windows-x86 -DENABLE_VIRTUAL_GAMEPAD_TESTS=ON
echo ===CONFIG EXIT CODE=%ERRORLEVEL%
