# CMake toolchain file for x86 (32-bit) builds
# Cross-platform: works on Windows, Linux, and macOS

# Pointer width is detected by CMake from the real compiler ABI (vcvars on
# Windows, the -m32 flags below on Unix). Do NOT FORCE CMAKE_SIZEOF_VOID_P here:
# it is a compiler-detected result variable; overriding it desyncs CMake's
# architecture decisions (library search paths, package selection) from the
# actual target (issue 91-09).

# Platform-specific configuration
if(CMAKE_SYSTEM_NAME STREQUAL "Windows" OR (NOT CMAKE_SYSTEM_NAME AND CMAKE_HOST_WIN32))
  # On Windows with MSVC, rely on vcvarsall.bat environment setup
  # Visual Studio and Ninja will use the environment provided by the developer command prompt
  set(CMAKE_SYSTEM_NAME Windows)
  set(CMAKE_SYSTEM_PROCESSOR X86)
  message(STATUS "x86 Toolchain: Configured for 32-bit Windows build")

elseif(CMAKE_SYSTEM_NAME STREQUAL "Darwin" OR (NOT CMAKE_SYSTEM_NAME AND CMAKE_HOST_APPLE))
  # On macOS, add -m32 flag (note: 32-bit support removed in macOS 10.15+)
  string(APPEND CMAKE_C_FLAGS_INIT " -m32")
  string(APPEND CMAKE_CXX_FLAGS_INIT " -m32")
  string(APPEND CMAKE_EXE_LINKER_FLAGS_INIT " -m32")
  string(APPEND CMAKE_SHARED_LINKER_FLAGS_INIT " -m32")
  string(APPEND CMAKE_MODULE_LINKER_FLAGS_INIT " -m32")
  message(STATUS "x86 Toolchain: Configured for 32-bit macOS build (requires 32-bit libraries)")

elseif(CMAKE_SYSTEM_NAME STREQUAL "Linux" OR (NOT CMAKE_SYSTEM_NAME AND CMAKE_HOST_UNIX))
  # On Linux, seed -m32 via _INIT so re-configures never duplicate flags in CMakeCache.txt
  string(APPEND CMAKE_C_FLAGS_INIT " -m32")
  string(APPEND CMAKE_CXX_FLAGS_INIT " -m32")
  string(APPEND CMAKE_EXE_LINKER_FLAGS_INIT " -m32")
  string(APPEND CMAKE_SHARED_LINKER_FLAGS_INIT " -m32")
  string(APPEND CMAKE_MODULE_LINKER_FLAGS_INIT " -m32")
  message(STATUS "x86 Toolchain: Configured for 32-bit Linux build")
endif()
