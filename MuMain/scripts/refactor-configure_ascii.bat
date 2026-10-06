@echo off
rem Configure the windows-x86 build tree with local classic-mode vcpkg deps.
rem Pure ASCII on purpose (GBK-safe invocation from git-bash).setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo error: vswhere not found at "%VSWHERE%" - install Visual Studio with the C++ workload. 1>&2
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
)cd /d "%~dp0\.."
call "%VCVARS%" >nul 2>&1
if errorlevel 1 (
    echo error: vcvars32.bat failed to initialize the MSVC environment. 1>&2
    exit /b %errorlevel%
)
"%CMAKE%" -S . -B out/build/windows-x86 ^
  -DCMAKE_PREFIX_PATH=D:\vcpkg\installed\x86-windows ^
  -DENABLE_VIRTUAL_GAMEPAD_TESTS=ON
echo ===CONFIG EXIT CODE=%ERRORLEVEL%
exit /b %ERRORLEVEL%