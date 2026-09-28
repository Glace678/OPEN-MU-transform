@echo off
cd /d "%~dp0"
call "D:\Microsoft Visual Studio\VC\Auxiliary\Build\vcvars32.bat" >nul
cmake --build out/build/windows-x86 --config RelWithDebInfo --target Main
echo ===BUILD EXIT CODE=%ERRORLEVEL%
