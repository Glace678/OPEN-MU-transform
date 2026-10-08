@echo off
rem Diagnostics build entry: resolve paths relative to this script (%~dp0) and
rem fail loudly if the build dir is unconfigured, instead of building
rem whatever is the current working directory.
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo error: vswhere not found at "%VSWHERE%". 1>&2
    exit /b 9009
)
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"
if not defined VSPATH (
    echo error: no Visual Studio with the VC++ toolset was found. 1>&2
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
set "BUILDDIR=%~dp0MuMain\out\build\windows-x86"
if not exist "%BUILDDIR%\CMakeCache.txt" (
    echo error: build directory is not configured at "%BUILDDIR%" - run configure first. 1>&2
    exit /b 1
)
cd /d "%BUILDDIR%"
if errorlevel 1 (
    echo error: could not enter "%BUILDDIR%". 1>&2
    exit /b 1
)
call "%VCVARS%" >nul 2>&1
if errorlevel 1 (
    echo error: vcvars32.bat failed. 1>&2
    exit /b %errorlevel%
)
"%CMAKE%" --build . --config RelWithDebInfo
exit /b %ERRORLEVEL%
