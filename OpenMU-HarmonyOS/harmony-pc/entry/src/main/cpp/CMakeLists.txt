cmake_minimum_required(VERSION 3.25)
project(OpenMUHarmonyPc LANGUAGES C CXX)

if(NOT OHOS AND (CMAKE_SYSTEM_NAME STREQUAL "OHOS" OR CMAKE_SYSTEM_NAME STREQUAL "OpenHarmony"))
  set(OHOS ON)
endif()
set(OHOS ON CACHE BOOL "Build for OpenHarmony" FORCE)

if(NOT DEFINED MU_REPOSITORY_ROOT)
  # cpp/ -> main/ -> src/ -> entry/ -> harmony-pc/ -> OpenMU-HarmonyOS/ ->
  # <repo root>/MuMain (six levels up).
  get_filename_component(MU_REPOSITORY_ROOT
    "${CMAKE_CURRENT_LIST_DIR}/../../../../../../MuMain" ABSOLUTE)
endif()
if(NOT EXISTS "${MU_REPOSITORY_ROOT}/src/CMakeLists.txt")
  message(FATAL_ERROR
    "MuMain checkout not found at '${MU_REPOSITORY_ROOT}'. Pass "
    "-DMU_REPOSITORY_ROOT=<absolute path to MuMain> in externalNativeOptions "
    "arguments (relative paths resolve against the cpp/ CMakeLists folder).")
endif()

# Prebuilt native libraries per ABI. HarmonyOS PC ships on arm64 (Kunpeng) and
# x86_64; place the matching .so under prebuilt/<abi>/.
# Prefer the toolchain's own target-ABI variable: CMAKE_SYSTEM_PROCESSOR is the
# *host* CPU, so a build machine whose architecture differs from the target
# (or an x86_64 host building arm64, the common case) would silently select the
# wrong ABI's prebuilt directory, and the packaging step below would then copy
# nothing instead of failing.
if(NOT OHOS_ARCH)
  if(CMAKE_OHOS_ARCH_ABI)
    set(OHOS_ARCH "${CMAKE_OHOS_ARCH_ABI}")
  elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
    set(OHOS_ARCH "x86_64")
  else()
    set(OHOS_ARCH "arm64-v8a")
  endif()
endif()
set(MU_OHOS_PREBUILT_DIR "${CMAKE_CURRENT_LIST_DIR}/prebuilt/${OHOS_ARCH}")

set(MU_OHOS_GL4ES_ROOT "" CACHE PATH "Path to an OHOS gl4es source checkout")
add_subdirectory("${MU_REPOSITORY_ROOT}/src" "${CMAKE_CURRENT_BINARY_DIR}/mumain")

# The .NET protocol library is dlopen'd at runtime by soname (same as Android);
# it only needs to be packaged next to libmain.so / libSDL3.so via the copy step.

add_library(openmubridge SHARED native_bridge.cpp)
target_compile_features(openmubridge PUBLIC cxx_std_20)
target_link_libraries(openmubridge PUBLIC libace_napi.z.so)

# The prebuilt .so files are dlopen'd at runtime by soname, so a missing or
# empty prebuilt directory still produces a .hap that builds and links cleanly
# and only fails at launch. Fail here instead -- the same way the MuMain lookup
# above does -- so a misconfigured build cannot package silently.
if(NOT EXISTS "${MU_OHOS_PREBUILT_DIR}")
  message(FATAL_ERROR
    "Prebuilt native libraries not found at '${MU_OHOS_PREBUILT_DIR}'. Build "
    "libMUnique.Client.Library.so for this ABI (see "
    "../../nativeaot-ohos/build-clientlibrary-ohos.sh) and place it under "
    "prebuilt/${OHOS_ARCH}/.")
endif()
file(GLOB MU_PREBUILT_SO "${MU_OHOS_PREBUILT_DIR}/*.so")
if(NOT MU_PREBUILT_SO)
  message(FATAL_ERROR "No .so files found in '${MU_OHOS_PREBUILT_DIR}'.")
endif()
foreach(so ${MU_PREBUILT_SO})
  get_filename_component(so_name "${so}" NAME)
  configure_file("${so}" "${CMAKE_LIBRARY_OUTPUT_DIRECTORY}/${so_name}" COPYONLY)
endforeach()
