function(mu_resolve_client_library_platform)
  cmake_parse_arguments(PARSE_ARGV 0
    ARG
    ""
    "SYSTEM_NAME;POINTER_SIZE;SYSTEM_PROCESSOR;OUT_LIBRARY_NAME;OUT_RID;OUT_PLATFORM;OUT_EXTRA_ARGS"
    "OSX_ARCHITECTURES")

  foreach(required_argument
      SYSTEM_NAME
      POINTER_SIZE
      OUT_LIBRARY_NAME
      OUT_RID
      OUT_PLATFORM
      OUT_EXTRA_ARGS)
    if(NOT DEFINED ARG_${required_argument} OR ARG_${required_argument} STREQUAL "")
      message(FATAL_ERROR "mu_resolve_client_library_platform requires ${required_argument}")
    endif()
  endforeach()

  if(ARG_SYSTEM_NAME STREQUAL "Android")
    string(TOLOWER "${ARG_SYSTEM_PROCESSOR}" target_processor)
    if(NOT ARG_POINTER_SIZE EQUAL 8
        OR NOT target_processor MATCHES "^(aarch64|arm64|arm64-v8a)$")
      message(FATAL_ERROR
        "The Native AOT client library supports Android arm64-v8a only")
    endif()

    # Android's jniLibs packaging and native loader only discover shared
    # libraries which follow the conventional lib*.so naming scheme.
    set(library_name "libMUnique.Client.Library.so")
    set(runtime_identifier "linux-bionic-arm64")
    set(platform "arm64")
    set(extra_args "-p:ci=true;-p:NativeLib=Shared")
  elseif(ARG_SYSTEM_NAME STREQUAL "OHOS" OR ARG_SYSTEM_NAME STREQUAL "OpenHarmony")
    # HarmonyOS / OpenHarmony. The .NET library is Native-AOT compiled out of
    # band with nativeaot-ohos/build-clientlibrary-ohos.sh (Linux + OHOS NDK)
    # and packaged as a prebuilt, so this RID is only used if a host tries to
    # publish from CMake -- which the main build disables for OHOS. Phones and
    # most tablets are arm64; HarmonyOS PCs / 2-in-1 devices may be x86_64.
    if(NOT ARG_POINTER_SIZE EQUAL 8)
      message(FATAL_ERROR
        "The Native AOT client library supports 64-bit OpenHarmony only")
    endif()
    string(TOLOWER "${ARG_SYSTEM_PROCESSOR}" ohos_processor)
    if(ohos_processor MATCHES "^(aarch64|arm64|arm64-v8a)$")
      # Preferred RID when .NET ships an OHOS runtime pack; the build script
      # falls back to linux-bionic-arm64 against the OHOS sysroot otherwise.
      set(runtime_identifier "linux-ohos-arm64")
      set(platform "arm64")
    elseif(ohos_processor MATCHES "^(x86_64|amd64)$")
      set(runtime_identifier "linux-ohos-x64")
      set(platform "x64")
    else()
      message(FATAL_ERROR "Unsupported OpenHarmony processor: ${ohos_processor}")
    endif()
    # Connection.h dlopens the __OHOS__ soname "libMUnique.Client.Library.so";
    # the lib prefix also matches how DevEco packages libs/<abi>/*.so.
    set(library_name "libMUnique.Client.Library.so")
    set(extra_args "-p:ci=true;-p:NativeLib=Shared")
  elseif(ARG_SYSTEM_NAME STREQUAL "Linux")
    if(NOT ARG_POINTER_SIZE EQUAL 8)
      message(FATAL_ERROR "The Native AOT client library supports Linux x64 only")
    endif()
    set(library_name "MUnique.Client.Library.so")
    set(runtime_identifier "linux-x64")
    set(platform "x64")
    set(extra_args "-p:ci=true")
  elseif(ARG_SYSTEM_NAME STREQUAL "Darwin")
    set(osx_architectures ${ARG_OSX_ARCHITECTURES})
    list(LENGTH osx_architectures architecture_count)
    if(architecture_count GREATER 1)
      message(FATAL_ERROR
        "Native AOT cannot emit one universal macOS library. Configure osx-x64 "
        "and osx-arm64 separately, then package them as separate clients.")
    endif()

    if(architecture_count EQUAL 1)
      list(GET osx_architectures 0 target_processor)
    else()
      set(target_processor "${ARG_SYSTEM_PROCESSOR}")
    endif()
    string(TOLOWER "${target_processor}" target_processor)

    if(target_processor MATCHES "^(arm64|aarch64)$")
      set(runtime_identifier "osx-arm64")
      set(platform "arm64")
    elseif(target_processor MATCHES "^(x86_64|amd64)$")
      set(runtime_identifier "osx-x64")
      set(platform "x64")
    else()
      message(FATAL_ERROR "Unsupported macOS processor: ${target_processor}")
    endif()

    set(library_name "MUnique.Client.Library.dylib")
    set(extra_args "-p:ci=true")
  elseif(ARG_SYSTEM_NAME STREQUAL "Windows")
    set(library_name "MUnique.Client.Library.dll")
    set(extra_args "-p:IlcUseEnvironmentalTools=true")
    if(ARG_POINTER_SIZE EQUAL 8)
      set(runtime_identifier "win-x64")
      set(platform "x64")
    elseif(ARG_POINTER_SIZE EQUAL 4)
      set(runtime_identifier "win-x86")
      set(platform "x86")
    else()
      message(FATAL_ERROR "Unsupported Windows pointer size: ${ARG_POINTER_SIZE}")
    endif()
  else()
    message(FATAL_ERROR "Unsupported Native AOT client platform: ${ARG_SYSTEM_NAME}")
  endif()

  set(${ARG_OUT_LIBRARY_NAME} "${library_name}" PARENT_SCOPE)
  set(${ARG_OUT_RID} "${runtime_identifier}" PARENT_SCOPE)
  set(${ARG_OUT_PLATFORM} "${platform}" PARENT_SCOPE)
  set(${ARG_OUT_EXTRA_ARGS} "${extra_args}" PARENT_SCOPE)
endfunction()
