@echo off
rem Build Main + all unit tests (Debug) for the windows-x86 tree.
cd /d "%~dp0\.."
call "D:\Microsoft Visual Studio\VC\Auxiliary\Build\vcvars32.bat" >nul
cmake --build out/build/windows-x86 --config Debug
echo ===BUILD EXIT CODE=%ERRORLEVEL%
exit /b %ERRORLEVEL%
