@echo off
rem Run the full doctest suite via ctest (Debug).
cd /d "%~dp0\.."
call "D:\Microsoft Visual Studio\VC\Auxiliary\Build\vcvars32.bat" >nul
ctest --test-dir out/build/windows-x86 -C Debug --output-on-failure
echo ===TEST EXIT CODE=%ERRORLEVEL%
exit /b %ERRORLEVEL%
