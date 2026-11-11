# MinGW-w64 toolchain file for cross-compiling Main on Linux (64-bit Windows)

set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)

# Compilers (prefer -posix thread model so C++20 <mutex>/<thread> are available on Debian/Ubuntu)
find_program(MINGW_X86_64_C_COMPILER NAMES x86_64-w64-mingw32-gcc-posix x86_64-w64-mingw32-gcc)
find_program(MINGW_X86_64_CXX_COMPILER NAMES x86_64-w64-mingw32-g++-posix x86_64-w64-mingw32-g++)
set(CMAKE_C_COMPILER "${MINGW_X86_64_C_COMPILER}")
set(CMAKE_CXX_COMPILER "${MINGW_X86_64_CXX_COMPILER}")
if(NOT CMAKE_C_COMPILER)
  set(CMAKE_C_COMPILER x86_64-w64-mingw32-gcc)
endif()
if(NOT CMAKE_CXX_COMPILER)
  set(CMAKE_CXX_COMPILER x86_64-w64-mingw32-g++)
endif()
set(CMAKE_RC_COMPILER x86_64-w64-mingw32-windres)

set(CMAKE_FIND_ROOT_PATH /usr/x86_64-w64-mingw32)

# Prefer static GCC runtimes to avoid shipping libgcc_s_seh-1.dll / libstdc++-6.dll
set(CMAKE_EXE_LINKER_FLAGS_INIT "${CMAKE_EXE_LINKER_FLAGS_INIT} -static-libgcc -static-libstdc++")
set(CMAKE_SHARED_LINKER_FLAGS_INIT "${CMAKE_SHARED_LINKER_FLAGS_INIT} -static-libgcc -static-libstdc++")

# Only search libraries and headers in the target environment
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
