cmake_minimum_required(VERSION 3.25)
project(OpenMUHarmony LANGUAGES C CXX)

# The DevEco Native toolchain targets OpenHarmony. Normalize the platform the
# same way MuMain's CMake does, and force the mobile (shared SDL / GLES) path.
if(NOT OHOS AND (CMAKE_SYSTEM_NAME STREQUAL "OHOS" OR CMAKE_SYSTEM_NAME STREQUAL "OpenHarmony"))
  set(OHOS ON)
endif()
set(OHOS ON CACHE BOOL "Build for OpenHarmony" FORCE)

# Resolve the shared MuMain checkout. Override MU_REPOSITORY_ROOT if this
# project lives elsewhere; the default matches the repo layout
# (openmu自用/OpenMU-HarmonyOS/... and openmu自用/MuMain).
if(NOT DEFINED MU_REPOSITORY_ROOT)
  # cpp/ -> main/ -> src/ -> entry/ -> harmony-game/ -> OpenMU-HarmonyOS/ ->
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

# Prebuilt native libraries produced for OpenHarmony (see ../../nativeaot-ohos
# and the gl4es OHOS build). Drop the arm64 .so files here:
#   prebuilt/arm64-v8a/libMUnique.Client.Library.so   (.NET NativeAOT protocol lib)
#   prebuilt/arm64-v8a/libGL.so                       (gl4es, optional if built from source)
set(MU_OHOS_PREBUILT_DIR "${CMAKE_CURRENT_LIST_DIR}/prebuilt/${OHOS_ARCH}")

# Build the whole game (MuClient + libmain.so + shared SDL3) from the shared
# MuMain source. MuMain's CMake defines the OHOS branch: it outputs libmain.so,
# links gl4es/EGL/GLES and the .NET protocol library, and compiles the shared
# MobileGestureMapper touch path.
set(MU_OHOS_GL4ES_ROOT "" CACHE PATH "Path to an OHOS gl4es source checkout")
add_subdirectory("${MU_REPOSITORY_ROOT}/src" "${CMAKE_CURRENT_BINARY_DIR}/mumain")

# The .NET protocol library is loaded at runtime by Connection.h via dlopen by
# soname (same model as Android), so it only needs to be packaged into the
# libs directory — no link-time dependency. The copy step below places it next
# to libmain.so / libSDL3.so so the DevEco packager includes it.

# The NAPI bridge the ArkTS shell calls to inject auto-login identity and the
# data root before SDL_main starts.
add_library(openmubridge SHARED native_bridge.cpp)
target_compile_features(openmubridge PUBLIC cxx_std_20)
target_link_libraries(openmubridge PUBLIC libace_napi.z.so)

# Copy every built/prebuilt .so next to the bridge so DevEco packages them all
# into libs/arm64-v8a of the .hap.
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
