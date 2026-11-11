@echo off
rem Configure the windows-x86 build tree with local classic-mode vcpkg deps.
rem Pure ASCII on purpose (GBK-safe invocation from git-bash).
setlocal
if not defined VCPKG_ROOT set "VCPKG_ROOT=D:\vcpkg"
call "%~dp0setup-msvc-x86-env.bat"
if errorlevel 1 exit /b %errorlevel%
cd /d "%~dp0\.."
"%CMAKE%" --preset windows-x86 ^
  -DCMAKE_PREFIX_PATH="%VCPKG_ROOT%\installed\x86-windows" ^
  -DENABLE_VIRTUAL_GAMEPAD_TESTS=ON
echo ===CONFIG EXIT CODE=%ERRORLEVEL%
exit /b %ERRORLEVEL%