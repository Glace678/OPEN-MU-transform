@echo off
rem ===========================================================================
rem build_main.bat - Build script for the MuMain C++ client engine (Main.exe).
rem Automatically detects Visual Studio installation and MSVC x86 toolset.
rem ===========================================================================
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    if exist "D:\Microsoft Visual Studio" (
        set "VSPATH=D:\Microsoft Visual Studio"
    ) else (
        echo error: vswhere not found at "%VSWHERE%" - install Visual Studio with the C++ workload. 1>&2
        exit /b 9009
    )
)
if not defined VSPATH for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"
if not defined VSPATH if exist "D:\Microsoft Visual Studio" set "VSPATH=D:\Microsoft Visual Studio"
if not defined VSPATH (
    echo error: no Visual Studio with the VC++ x86 toolset was found. 1>&2
    exit /b 9009
)
set "VCVARS=%VSPATH%\VC\Auxiliary\Build\vcvars32.bat"
set "CMAKE=%VSPATH%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
if not exist "%VCVARS%" (
    echo error: vcvars32.bat not found at "%VCVARS%". 1>&2
    exit /b 9009
)
if not exist "%CMAKE%" (
    echo error: bundled cmake.exe not found at "%CMAKE%". 1>&2
    exit /b 9009
)
call "%VCVARS%" >nul 2>&1
if errorlevel 1 (
    echo error: vcvars32.bat failed to initialize the MSVC environment. 1>&2
    exit /b %errorlevel%
)
"%CMAKE%" --build "%~dp0MuMain\out\build\windows-x86" --config RelWithDebInfo --target Main > "%~dp0build_log.txt" 2>&1
exit /b %ERRORLEVEL%
