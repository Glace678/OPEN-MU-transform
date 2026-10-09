@echo off
rem Run the full doctest suite via ctest (Debug).
setlocal
call "%~dp0setup-msvc-x86-env.bat"
if errorlevel 1 exit /b %errorlevel%
set "CTEST=%VSPATH%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\ctest.exe"
if not exist "%CTEST%" (
    where ctest >nul 2>&1
    if errorlevel 1 (
        echo error: no ctest found: neither "%CTEST%" nor a ctest on PATH. Install the Visual Studio CMake component or add ctest to PATH. 1>&2
        exit /b 9009
    )
    set "CTEST=ctest"
)
cd /d "%~dp0\.."
"%CTEST%" --test-dir out/build/windows-x86 -C Debug --output-on-failure
echo ===TEST EXIT CODE=%ERRORLEVEL%
exit /b %ERRORLEVEL%