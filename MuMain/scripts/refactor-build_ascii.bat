@echo off
rem Build Main + all unit tests (Debug) for the windows-x86 tree.
setlocal
call "%~dp0setup-msvc-x86-env.bat"
if errorlevel 1 exit /b %errorlevel%
cd /d "%~dp0\.."
"%CMAKE%" --build out/build/windows-x86 --config Debug
echo ===BUILD EXIT CODE=%ERRORLEVEL%
exit /b %ERRORLEVEL%