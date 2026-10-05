@echo off
rem Configure the windows-x86 build tree with local classic-mode vcpkg deps.
rem Pure ASCII on purpose (GBK-safe invocation from git-bash).
cd /d "%~dp0\.."
call "D:\Microsoft Visual Studio\VC\Auxiliary\Build\vcvars32.bat" >nul
cmake -S . -B out/build/windows-x86 ^
  -DCMAKE_PREFIX_PATH=D:\vcpkg\installed\x86-windows ^
  -DENABLE_VIRTUAL_GAMEPAD_TESTS=ON
echo ===CONFIG EXIT CODE=%ERRORLEVEL%
exit /b %ERRORLEVEL%
