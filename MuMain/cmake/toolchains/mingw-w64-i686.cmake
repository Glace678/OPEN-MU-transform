# MinGW-w64 toolchain file for cross-compiling Main on Linux (32-bit Windows)

set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR i686)

# Compilers (prefer -posix thread model so C++20 <mutex>/<thread> are available on Debian/Ubuntu)
find_program(MINGW_I686_C_COMPILER NAMES i686-w64-mingw32-gcc-posix i686-w64-mingw32-gcc)
find_program(MINGW_I686_CXX_COMPILER NAMES i686-w64-mingw32-g++-posix i686-w64-mingw32-g++)
set(CMAKE_C_COMPILER "${MINGW_I686_C_COMPILER}")
set(CMAKE_CXX_COMPILER "${MINGW_I686_CXX_COMPILER}")
if(NOT CMAKE_C_COMPILER)
  set(CMAKE_C_COMPILER i686-w64-mingw32-gcc)
endif()
if(NOT CMAKE_CXX_COMPILER)
  set(CMAKE_CXX_COMPILER i686-w64-mingw32-g++)
endif()
set(CMAKE_RC_COMPILER i686-w64-mingw32-windres)

set(CMAKE_FIND_ROOT_PATH /usr/i686-w64-mingw32)

# Prefer static GCC runtimes to avoid shipping libgcc_s_dw2-1.dll / libstdc++-6.dll
set(CMAKE_EXE_LINKER_FLAGS_INIT "${CMAKE_EXE_LINKER_FLAGS_INIT} -static-libgcc -static-libstdc++")
set(CMAKE_SHARED_LINKER_FLAGS_INIT "${CMAKE_SHARED_LINKER_FLAGS_INIT} -static-libgcc -static-libstdc++")

# Only search libraries and headers in the target environment
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
