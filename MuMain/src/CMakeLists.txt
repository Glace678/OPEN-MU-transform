cmake_minimum_required(VERSION 3.25)

project(MuMain)

# The HarmonyOS / OpenHarmony NDK toolchain sets CMAKE_SYSTEM_NAME=OHOS (and
# CMAKE_OHOS_ARCH_ABI). Normalize it into an OHOS variable so the rest of the
# build can mirror the Android mobile path (shared SDL, GLES via gl4es, a
# libmain.so that the platform shell loads).
if(CMAKE_SYSTEM_NAME STREQUAL "OHOS" OR CMAKE_SYSTEM_NAME STREQUAL "OpenHarmony")
  set(OHOS ON)
endif()
# MU_MOBILE covers every touch/mobile target that shares the SDL shell, the
# MobileGestureMapper input path and the OpenGL ES renderer.
if(ANDROID OR OHOS)
  set(MU_MOBILE ON)
else()
  set(MU_MOBILE OFF)
endif()

# Repository root (parent of this src/ directory). Defined here so all
# sibling-directory references (cmake/, ClientLibrary/, etc.) have a
# single, stable anchor instead of scattered "../" navigations.
cmake_path(GET CMAKE_CURRENT_SOURCE_DIR PARENT_PATH REPO_ROOT)

include("${REPO_ROOT}/cmake/EnsureSubmodule.cmake")
include("${REPO_ROOT}/cmake/ClientLibraryPlatform.cmake")

mu_ensure_submodule("ThirdParty/SDL"
  INDICATOR "CMakeLists.txt"
  URL "https://github.com/libsdl-org/SDL.git")

mu_ensure_submodule("ThirdParty/SDL_mixer"
  INDICATOR "CMakeLists.txt"
  URL "https://github.com/libsdl-org/SDL_mixer.git")

# The platform shell loads libSDL3.so before libmain.so on Android and HarmonyOS.
# Desktop builds keep the existing static-link model.
if(MU_MOBILE)
  set(SDL_SHARED ON CACHE BOOL "" FORCE)
  set(SDL_STATIC OFF CACHE BOOL "" FORCE)
else()
  set(SDL_SHARED OFF CACHE BOOL "" FORCE)
  set(SDL_STATIC ON CACHE BOOL "" FORCE)
endif()
set(SDL_TEST_LIBRARY OFF CACHE BOOL "" FORCE)
set(SDL_TESTS OFF CACHE BOOL "" FORCE)
set(SDL_INSTALL OFF CACHE BOOL "" FORCE)
add_subdirectory(ThirdParty/SDL EXCLUDE_FROM_ALL)

if(MU_MOBILE)
  # CMake shortens the NDK compiler path on Windows. The resulting 8.3 name
  # (CLANG_~1.EXE) no longer tells clang that it is acting as clang++, so its
  # link step omits libc++ even though C++ sources compile normally. The same
  # happens with the HarmonyOS NDK under a Windows-hosted DevEco, so link the
  # shared C++ runtime into libSDL3.so on both mobile targets.
  if(ANDROID OR OHOS)
    target_link_libraries(SDL3-shared PRIVATE c++_shared)
  endif()
  # Android 15+ devices may use 16 KiB pages. Keep every shared object built
  # by this project loadable there even when the toolchain cache option was
  # omitted by a caller.
  target_link_options(SDL3-shared PRIVATE "-Wl,-z,max-page-size=16384")
endif()

# SDL3_mixer — static link, MP3 + Ogg Vorbis only (matches our music assets)
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(SDLMIXER_INSTALL OFF CACHE BOOL "" FORCE)
set(SDLMIXER_VENDORED ON CACHE BOOL "" FORCE)
set(SDLMIXER_SAMPLES OFF CACHE BOOL "" FORCE)
set(SDLMIXER_MP3 ON CACHE BOOL "" FORCE)
set(SDLMIXER_MP3_DRMP3 ON CACHE BOOL "" FORCE)
# mpg123 is built as a dynamically loaded library — drmp3 alone is enough
# for the assets we ship and keeps everything statically linked.
set(SDLMIXER_MP3_MPG123 OFF CACHE BOOL "" FORCE)
set(SDLMIXER_VORBIS_STB ON CACHE BOOL "" FORCE)
set(SDLMIXER_VORBIS_VORBISFILE OFF CACHE BOOL "" FORCE)
set(SDLMIXER_FLAC OFF CACHE BOOL "" FORCE)
set(SDLMIXER_OPUS OFF CACHE BOOL "" FORCE)
set(SDLMIXER_WAVPACK OFF CACHE BOOL "" FORCE)
set(SDLMIXER_MOD OFF CACHE BOOL "" FORCE)
set(SDLMIXER_MIDI OFF CACHE BOOL "" FORCE)
set(SDLMIXER_GME OFF CACHE BOOL "" FORCE)
add_subdirectory(ThirdParty/SDL_mixer EXCLUDE_FROM_ALL)

# Determine architecture if CMAKE_SIZEOF_VOID_P isn't set
if (NOT DEFINED CMAKE_SIZEOF_VOID_P OR CMAKE_SIZEOF_VOID_P EQUAL 0)
  # Try to detect from compiler path
  if (CMAKE_CXX_COMPILER MATCHES "x64|amd64|AMD64")
    set(CMAKE_SIZEOF_VOID_P 8 CACHE INTERNAL "Pointer size" FORCE)
    message(STATUS "Detected x64 from compiler path, setting CMAKE_SIZEOF_VOID_P=8")
  elseif (CMAKE_CXX_COMPILER MATCHES "x86|i386|i686")
    set(CMAKE_SIZEOF_VOID_P 4 CACHE INTERNAL "Pointer size" FORCE)
    message(STATUS "Detected x86 from compiler path, setting CMAKE_SIZEOF_VOID_P=4")
  else()
    # Default fallback
    set(CMAKE_SIZEOF_VOID_P 4 CACHE INTERNAL "Pointer size" FORCE)
    message(WARNING "Could not detect architecture, defaulting to x86")
  endif()
endif()

# Check architecture and configure accordingly
if (MSVC)
  if (CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(STATUS "Configuring for 64-bit (x64) build")
  else()
    message(STATUS "Configuring for 32-bit (x86) build")
  endif()
endif()

if (NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE RelWithDebInfo CACHE STRING
      "Build type (default RelWithDebInfo for useful symbols)" FORCE)
endif()

# Editor mode option (for admin builds)
option(ENABLE_EDITOR "Enable editor features for admin builds" OFF)
option(ENABLE_VIRTUAL_GAMEPAD_TESTS "Enable the SDL virtual-gamepad acceptance-test bridge" OFF)
option(ENABLE_FRAMEBUFFER_CAPTURE_TESTS "Enable opt-in framebuffer capture for native acceptance tests" OFF)
# iOS 预览脚手架开关：默认关闭时 configure 仍然明确报"暂不支持"，避免把未完成
# 的移动端渲染路径误当成可用目标；打开后走 source/App/Platform/iOS/ 入口，
# 供 OpenMU-iOS/build-ios.sh 生成 Xcode 工程做移植开发（详见
# OpenMU-iOS/BUILD-iOS构建说明.md）。
option(MU_IOS_PREVIEW "Enable the experimental iOS application scaffold" OFF)

# ---------------------------------------------------------------------------
# Engine/game code -> static library (libMuClient); MuMain -> thin executable.
#
# All engine and game logic compiles into the MuClient static library. The
# Main executable defined further down contains only the platform entry point
# (WinMain) and links the library. Keeping the two separated lets unit-test
# binaries link the same engine code instead of recompiling individual
# translation units, and stops bootstrap-only code from leaking into the
# engine (issue #345).
# ---------------------------------------------------------------------------

# Collect all C++ source files under source/
file(GLOB_RECURSE MU_CLIENT_SOURCES
  CONFIGURE_DEPENDS
        "source/*.cpp"
)

# Per-platform entry points (App/Platform/<OS>/Winmain.cpp, main.cpp, main.mm)
# belong to the executable, not the engine library. Removing them keeps the
# library free of a WinMain/main() so it can be linked into test binaries that
# bring their own entry point, and so a non-Windows entry (e.g. Linux main.cpp)
# does not get globbed into MuClient and collide with the executable's own
# entry symbol at link time. Match only the Platform/<OS>/ entry-file tail, not
# an absolute path: a ${CMAKE_CURRENT_SOURCE_DIR}-built path can differ from
# glob results in drive-letter casing or separators on Windows. This excludes
# only the entry files; other Platform/<OS>/ sources (e.g. Local.cpp) stay in
# the library.
list(FILTER MU_CLIENT_SOURCES EXCLUDE REGEX "[/\\]Platform[/\\][^/\\]+[/\\][Ww]in[Mm]ain\\.[Cc]pp$")
list(FILTER MU_CLIENT_SOURCES EXCLUDE REGEX "[/\\]Platform[/\\][^/\\]+[/\\]main\\.(cpp|mm)$")

# #40: vendored third-party trees (ThirdParty/third_party/dependencies) build as
# their own targets. GLOB_RECURSE must not sweep their .cpp into the client lib,
# or a checked-out submodule gets compiled twice / breaks the build.
list(FILTER MU_CLIENT_SOURCES EXCLUDE REGEX "[/\\](ThirdParty|third_party|dependencies)[/\\]")

# The in-game shop's catalog downloader is a WinINet/urlmon HTTP+FTP stack on
# Windows (FileDownloader/FTPConnecter/HTTPConnecter/DownloadInfo). Off Windows
# it is replaced by a libcurl implementation (CurlFileDownloader, issue #462).
# Keep only the transport that fits the platform in the build.
if(WIN32)
  list(FILTER MU_CLIENT_SOURCES EXCLUDE REGEX "[/\\]GameShop[/\\]FileDownloader[/\\]CurlFileDownloader\\.cpp$")
else()
  list(FILTER MU_CLIENT_SOURCES EXCLUDE REGEX "[/\\]GameShop[/\\]FileDownloader[/\\](FileDownloader|FTPConnecter|HTTPConnecter|DownloadInfo)\\.cpp$")
endif()

# Add MuEditor sources when editor is enabled
if(ENABLE_EDITOR)
  file(GLOB_RECURSE MU_EDITOR_SOURCES
    CONFIGURE_DEPENDS
    "MuEditor/*.cpp"
  )
  list(APPEND MU_CLIENT_SOURCES ${MU_EDITOR_SOURCES})
endif()

# ImGui static library (required when editor is enabled)
if(ENABLE_EDITOR)
  mu_ensure_submodule("ThirdParty/imgui"
    INDICATOR "imgui.cpp"
    URL "https://github.com/ocornut/imgui.git")

  add_library(imgui STATIC
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/imgui/imgui.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/imgui/imgui_draw.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/imgui/imgui_tables.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/imgui/imgui_widgets.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/imgui/backends/imgui_impl_sdl3.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/imgui/backends/imgui_impl_opengl2.cpp"
  )

  target_include_directories(imgui PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/imgui"
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/imgui/backends"
  )

  # The SDL3 ImGui backend (imgui_impl_sdl3.cpp) needs the SDL3 headers; link the
  # target so its include dirs propagate (issue #442).
  target_link_libraries(imgui PUBLIC SDL3::SDL3)

  # The Win32 + OpenGL2 backends call straight into opengl32 (glOrtho,
  # glShadeModel, ...) and dwmapi. Declaring them as PUBLIC dependencies of
  # imgui lets CMake place these libraries *after* libimgui.a on the final
  # link line, which MinGW's order-sensitive linker requires. Restrict to
  # Windows non-MSVC (MinGW): these are Windows-only libraries, and MSVC links
  # them elsewhere and ignores order anyway.
  if (WIN32 AND NOT MSVC)
    target_link_libraries(imgui PUBLIC opengl32 dwmapi)
  endif()

  # <-- Force C++20 for ImGui here
  target_compile_features(imgui PUBLIC cxx_std_20)
endif()

# Engine/game code as a static library. Compile- and link-time usage
# requirements (C++20, include paths, definitions, third-party libraries) are
# attached PUBLIC below so both the Main executable and any test binary that
# links MuClient inherit them automatically.
add_library(MuClient STATIC ${MU_CLIENT_SOURCES})
target_compile_features(MuClient PUBLIC cxx_std_20)

if(MSVC)
  # These translation units are strict UTF-8 and contain CJK wide literals;
  # decoding them with the system ANSI code page corrupts string tokenization.
  # Keep this list narrow because parts of the legacy client still use other
  # source encodings.
  set(MU_UTF8_SOURCES
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Engine/Object/ZzzCharacter.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Render/Effects/ZzzEffectJoint.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/UI/Legacy/UIWindows.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Scenes/SceneCommon.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/UI/Chat/ChatInput.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/UI/ControllerKeyboard/ControllerKeyboard.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/UI/Windows/PasswordServiceWin.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Engine/Object/ZzzInventory.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Engine/Object/ZzzInfomation.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Engine/Object/ZzzInterface.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Engine/Object/ZzzObject.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Engine/Object/ZzzOpenData.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Engine/Physics/PhysicsManager.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/GameLogic/Combat/ClassAttack.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Render/Effects/ZzzEffectParticle.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/World/GameMaps/GMEmpireGuardian1.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/World/GameMaps/GM_Raklion.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/World/GameMaps/GMBattleCastle.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/World/GameMaps/GMCrywolf1st.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/World/GameMaps/GM_Kanturu_2nd.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/World/GameMaps/GM_Kanturu_3rd.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/UI/NewUI/Dialogs/NewUICommonMessageBox.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/source/UI/Legacy/UIControls.cpp"
  )
  set_property(SOURCE ${MU_UTF8_SOURCES}
    APPEND PROPERTY COMPILE_OPTIONS /utf-8)

  # P1: high warning level for the legacy client. Intentionally NOT promoted to
  # errors (/WX): record the inventory and fix only uncontroversial categories
  # without changing logic (see docs/refactoring-plan.md).
  #   C4530 - the legacy client is compiled without C++ exception unwind
  #           semantics by design; enabling /EHsc would change behavior, so the
  #           standard-header catch handlers are expected.
  #   C4201 - nonstandard nameless struct/union, used intentionally in many
  #           math/interop types.
  # C4828 (source charset) is NOT suppressed: it is cleared by the P1 source
  # encoding normalization (CP949 -> UTF-8), not hidden.
  target_compile_options(MuClient PRIVATE /W4 /wd4530 /wd4201)
endif()

# The Android NDK does not ship libcurl. APK builds may inject an arm64-v8a
# prebuilt; leaving both values empty keeps configuration usable while making
# the missing compile dependency explicit in the warning below.
if(ANDROID)
  set(MU_ANDROID_CURL_INCLUDE_DIR "" CACHE PATH
      "Directory containing curl/curl.h for Android")
  set(MU_ANDROID_CURL_LIBRARY "" CACHE FILEPATH
      "Path to an Android arm64-v8a libcurl shared or static library")
  if(MU_ANDROID_CURL_INCLUDE_DIR AND MU_ANDROID_CURL_LIBRARY)
    if(NOT EXISTS "${MU_ANDROID_CURL_INCLUDE_DIR}/curl/curl.h")
      message(FATAL_ERROR
        "MU_ANDROID_CURL_INCLUDE_DIR does not contain curl/curl.h: "
        "${MU_ANDROID_CURL_INCLUDE_DIR}")
    endif()
    if(NOT EXISTS "${MU_ANDROID_CURL_LIBRARY}")
      message(FATAL_ERROR
        "MU_ANDROID_CURL_LIBRARY does not exist: ${MU_ANDROID_CURL_LIBRARY}")
    endif()
    add_library(mu_android_curl UNKNOWN IMPORTED)
    set_target_properties(mu_android_curl PROPERTIES
      IMPORTED_LOCATION "${MU_ANDROID_CURL_LIBRARY}"
      INTERFACE_INCLUDE_DIRECTORIES "${MU_ANDROID_CURL_INCLUDE_DIR}")
    add_library(CURL::libcurl ALIAS mu_android_curl)
  elseif(MU_ANDROID_CURL_INCLUDE_DIR OR MU_ANDROID_CURL_LIBRARY)
    message(FATAL_ERROR
      "Set both MU_ANDROID_CURL_INCLUDE_DIR and MU_ANDROID_CURL_LIBRARY")
  else()
    add_library(mu_android_curl_missing INTERFACE)
    add_library(CURL::libcurl ALIAS mu_android_curl_missing)
    message(WARNING
      "Android libcurl is not configured. CMake generation can continue, but "
      "MuClient compilation requires MU_ANDROID_CURL_INCLUDE_DIR and "
      "MU_ANDROID_CURL_LIBRARY.")
  endif()
  target_link_libraries(MuClient PUBLIC CURL::libcurl)
elseif(OHOS)
  # HarmonyOS / OpenHarmony. The OHOS NDK ships no libcurl and no FindCURL
  # package config, so find_package(CURL) here would abort configure. Mirror
  # the Android path: an OHOS-built libcurl may be injected via these cache
  # variables; otherwise generation proceeds with a warning. The only consumer
  # is the in-game shop catalog/FTP downloader (CurlFileDownloader.cpp, which
  # replaces the WinINet stack off Windows), exactly as on Android.
  set(MU_OHOS_CURL_INCLUDE_DIR "" CACHE PATH
      "Directory containing curl/curl.h for OpenHarmony")
  set(MU_OHOS_CURL_LIBRARY "" CACHE FILEPATH
      "Path to an OpenHarmony arm64 libcurl shared or static library")
  if(MU_OHOS_CURL_INCLUDE_DIR AND MU_OHOS_CURL_LIBRARY)
    if(NOT EXISTS "${MU_OHOS_CURL_INCLUDE_DIR}/curl/curl.h")
      message(FATAL_ERROR
        "MU_OHOS_CURL_INCLUDE_DIR does not contain curl/curl.h: "
        "${MU_OHOS_CURL_INCLUDE_DIR}")
    endif()
    if(NOT EXISTS "${MU_OHOS_CURL_LIBRARY}")
      message(FATAL_ERROR
        "MU_OHOS_CURL_LIBRARY does not exist: ${MU_OHOS_CURL_LIBRARY}")
    endif()
    add_library(mu_ohos_curl UNKNOWN IMPORTED)
    set_target_properties(mu_ohos_curl PROPERTIES
      IMPORTED_LOCATION "${MU_OHOS_CURL_LIBRARY}"
      INTERFACE_INCLUDE_DIRECTORIES "${MU_OHOS_CURL_INCLUDE_DIR}")
    add_library(CURL::libcurl ALIAS mu_ohos_curl)
  elseif(MU_OHOS_CURL_INCLUDE_DIR OR MU_OHOS_CURL_LIBRARY)
    message(FATAL_ERROR
      "Set both MU_OHOS_CURL_INCLUDE_DIR and MU_OHOS_CURL_LIBRARY")
  else()
    add_library(mu_ohos_curl_missing INTERFACE)
    add_library(CURL::libcurl ALIAS mu_ohos_curl_missing)
    message(WARNING
      "HarmonyOS libcurl is not configured. CMake generation can continue, but "
      "the in-game shop catalog/FTP downloader (CurlFileDownloader) needs "
      "MU_OHOS_CURL_INCLUDE_DIR and MU_OHOS_CURL_LIBRARY to link and run.")
  endif()
  target_link_libraries(MuClient PUBLIC CURL::libcurl)
elseif(NOT WIN32)
  find_package(CURL REQUIRED)
  target_link_libraries(MuClient PUBLIC CURL::libcurl)
endif()

# The engine relies on self-registering globals whose constructors get
# silently dropped if the linker only pulls archive members on demand, so
# every consumer must link MuClient with WHOLE_ARCHIVE. Rather than make each
# consumer remember that, MuClient::MuClient is an interface wrapper that
# carries the WHOLE_ARCHIVE requirement (and, transitively, all of MuClient's
# public compile/link properties). Executables and unit tests just link
# MuClient::MuClient; link the raw MuClient target directly only when an
# on-demand link is genuinely wanted.
add_library(MuClient_whole INTERFACE)
target_link_libraries(MuClient_whole INTERFACE $<LINK_LIBRARY:WHOLE_ARCHIVE,MuClient>)
add_library(MuClient::MuClient ALIAS MuClient_whole)

# Thin executable: platform bootstrap only, links the engine library. The
# executable shell's sources live under source/App/ (see issue #346). The entry
# point is selected per platform (issue #441): each OS keeps its bootstrap under
# source/App/Platform/<OS>/, so adding a platform is a new entry file plus a
# branch here, not a CMake rewrite. Linux and macOS keep a small native main()
# which forwards to the shared desktop bootstrap until that bootstrap is renamed.
#
# Unsupported platforms fail loudly rather than silently selecting the Linux
# entry: the mobile targets need a different entry model and a renderer port
# (the engine uses fixed-function desktop OpenGL), so quietly compiling a
# desktop main() for them would be wrong. iOS is matched before the generic
# APPLE/macOS branch, and Android before the generic UNIX/Linux branch.
if (WIN32)
  set(MU_APP_ENTRY "source/App/Platform/Windows/Winmain.cpp")
elseif (CMAKE_SYSTEM_NAME STREQUAL "iOS")
  # iOS 预览脚手架（opt-in）。入口与数据根已就位（source/App/Platform/iOS/），
  # 但渲染层（固定功能桌面 GL → gl4es/GLES2 或 Metal）与触摸 UI 尚未在真机
  # 验证，App Store 分发也需要 Apple 签名。默认仍然明确失败；显式
  # -DMU_IOS_PREVIEW=ON 才生成工程（OpenMU-iOS/build-ios.sh 会自动带上）。
  if(NOT MU_IOS_PREVIEW)
    message(FATAL_ERROR "iOS is a preview scaffold, not a supported build "
      "target yet: configure with -DMU_IOS_PREVIEW=ON to generate the Xcode "
      "project for porting work (see OpenMU-iOS/BUILD-iOS构建说明.md). The "
      "renderer still needs an OpenGL ES/Metal port validated on device, and "
      "distribution is App Store restricted.")
  endif()
  message(WARNING "MU_IOS_PREVIEW=ON: experimental iOS scaffold. The renderer "
    "and input layers are not device-verified; expect porting work.")
  set(MU_APP_ENTRY
    "source/App/Platform/iOS/main.mm"
    "source/App/Platform/Windows/Winmain.cpp")
elseif (APPLE)
  set(MU_APP_ENTRY
    "source/App/Platform/macOS/main.mm"
    "source/App/Platform/Windows/Winmain.cpp")
elseif (ANDROID)
  set(MU_APP_ENTRY
    "source/App/Platform/Android/main.cpp"
    "source/App/Platform/Windows/Winmain.cpp")
elseif (OHOS)
  # HarmonyOS / OpenHarmony. The SDL OpenHarmony backend loads libmain.so like
  # Android; touch, gestures, GLES-via-gl4es and auto-login are shared behind
  # `#if defined(__ANDROID__) || defined(__OHOS__)`.
  set(MU_APP_ENTRY
    "source/App/Platform/HarmonyOS/main.cpp"
    "source/App/Platform/Windows/Winmain.cpp")
elseif (UNIX)
  # Linux entry (main.cpp) plus the shared bootstrap/game-loop from Winmain.cpp,
  # which now compiles off Windows (issue #462). main() calls into it.
  set(MU_APP_ENTRY
    "source/App/Platform/Linux/main.cpp"
    "source/App/Platform/Windows/Winmain.cpp")
else()
  message(FATAL_ERROR "No executable entry point defined for this platform "
    "(CMAKE_SYSTEM_NAME=${CMAKE_SYSTEM_NAME}). Add source/App/Platform/<OS>/ "
    "and a branch above.")
endif()

if(ANDROID OR OHOS)
  add_library(Main SHARED
    "${CMAKE_CURRENT_SOURCE_DIR}/${MU_APP_ENTRY}"
  )
  set_target_properties(Main PROPERTIES OUTPUT_NAME main)
  target_link_options(Main PRIVATE "-Wl,-z,max-page-size=16384")
  if(OHOS)
    target_compile_definitions(MuClient PUBLIC __OHOS__=1)
    target_compile_definitions(Main PUBLIC __OHOS__=1)
  endif()
else()
  add_executable(Main
    "${CMAKE_CURRENT_SOURCE_DIR}/${MU_APP_ENTRY}"
  )
endif()
target_compile_features(Main PUBLIC cxx_std_20)
target_link_libraries(Main PRIVATE MuClient::MuClient)

# Static analysis / sanitizers attach to the engine library and the entry-point
# executable only. Vendored third-party (SDL, SDL_mixer, imgui) is deliberately
# left alone: its findings are not actionable in this repository. See
# cmake/StaticAnalysis.cmake; both are no-ops unless the matching option is on.
mu_enable_clang_tidy(MuClient)
mu_enable_clang_tidy(Main)
mu_enable_asan(MuClient)
mu_enable_asan(Main)

if (NOT MSVC)
  set(MU_MAIN_MAP_FILE "${CMAKE_CURRENT_BINARY_DIR}/Main.map")
  if(APPLE)
    target_link_options(Main PRIVATE "-Wl,-map,${MU_MAIN_MAP_FILE}")
  else()
    target_link_options(Main PRIVATE "-Wl,-Map,${MU_MAIN_MAP_FILE}")
  endif()
endif()

if (CMAKE_SYSTEM_NAME STREQUAL "Windows")
  # Build as a Windows GUI application (no console window) and let CMake
  # add the default system libraries like kernel32, user32, gdi32, etc.
  set_target_properties(Main PROPERTIES
    WIN32_EXECUTABLE TRUE
  )

  enable_language(RC)
  target_sources(Main PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/source/App/Platform/Windows/resource.rc"
  )
endif()

# Copy assets directory next to executable. Platform-agnostic: any build
# that produces the Main executable needs the assets alongside it.
# The custom command's stamp file depends on every file under
# ${ASSETS_DIR}, so the copy only runs when at least one asset is newer
# than the stamp -- unchanged builds skip the copy entirely, edited
# assets re-trigger it. CONFIGURE_DEPENDS makes CMake re-evaluate the
# glob at build time so newly-added asset files are picked up without a
# manual reconfigure.
set(ASSETS_DIR "${CMAKE_CURRENT_SOURCE_DIR}/bin")

# PLAT-8: src/bin is git-ignored (**/bin/**) but every build hard-depends on it
# (CopyAssets, SeedConfig). A fresh checkout used to fail deep in a custom command
# with an unclear error; fail at configure time with the action to take.
if(NOT EXISTS "${ASSETS_DIR}")
  message(FATAL_ERROR
    "Game assets directory '${ASSETS_DIR}' is missing. It is excluded from git; "
    "unpack/copy the released game data assets into 'src/bin' before building "
    "(the directory must at least contain config.ini.template and Data/).")
endif()
if(NOT EXISTS "${ASSETS_DIR}/config.ini.template")
  message(FATAL_ERROR
    "'${ASSETS_DIR}/config.ini.template' is missing. Restore the released "
    "assets into 'src/bin' before configuring.")
endif()

file(GLOB_RECURSE ASSET_FILES CONFIGURE_DEPENDS "${ASSETS_DIR}/*")
# $<CONFIG> in the stamp name keeps multi-config generators (Visual
# Studio, Xcode) honest: each configuration's output directory needs
# its own stamp, otherwise switching from Debug to Release would see
# the Debug stamp and skip the copy, leaving the Release dir empty.
set(ASSETS_STAMP "${CMAKE_CURRENT_BINARY_DIR}/.assets_copied_$<CONFIG>.stamp")
add_custom_command(
  OUTPUT "${ASSETS_STAMP}"
  COMMAND ${CMAKE_COMMAND} -E copy_directory
          "${ASSETS_DIR}"
          "$<TARGET_FILE_DIR:Main>"
  COMMAND ${CMAKE_COMMAND} -E touch "${ASSETS_STAMP}"
  DEPENDS ${ASSET_FILES}
  COMMENT "Copying game assets (sources changed)"
  VERBATIM
)
add_custom_target(CopyAssets DEPENDS "${ASSETS_STAMP}")
add_dependencies(Main CopyAssets)

# Seed config.ini from the template only when it doesn't already exist
# next to the executable. The bulk asset copy ships config.ini.template
# into the build dir on every build; this step turns it into config.ini
# the first time and then leaves the user's edited copy alone forever.
add_custom_command(TARGET Main POST_BUILD
  COMMAND "${CMAKE_COMMAND}"
    "-DINPUT=${ASSETS_DIR}/config.ini.template"
    "-DOUTPUT=$<TARGET_FILE_DIR:Main>/config.ini"
    "-P" "${REPO_ROOT}/cmake/SeedConfigIfMissing.cmake"
  VERBATIM
)

# Rime schemas are small source assets. Keep the upstream dictionary and our
# controller-specific schema together, then stage them beside the executable.
if(CMAKE_SIZEOF_VOID_P EQUAL 8)
  set(RIME_RUNTIME_ARCH "x64")
else()
  set(RIME_RUNTIME_ARCH "x86")
endif()
set(RIME_RUNTIME_DIR
    "${CMAKE_CURRENT_SOURCE_DIR}/third_party/librime/runtime/${RIME_RUNTIME_ARCH}")

add_custom_command(TARGET Main POST_BUILD
  COMMAND "${CMAKE_COMMAND}" -E make_directory
          "$<TARGET_FILE_DIR:Main>/rime/share"
  COMMAND "${CMAKE_COMMAND}" -E make_directory
          "$<TARGET_FILE_DIR:Main>/Licenses"
  COMMAND "${CMAKE_COMMAND}" -E copy_directory
          "${CMAKE_CURRENT_SOURCE_DIR}/third_party/librime/share"
          "$<TARGET_FILE_DIR:Main>/rime/share"
  COMMAND "${CMAKE_COMMAND}" -E copy_if_different
          "${CMAKE_CURRENT_SOURCE_DIR}/third_party/librime/LICENSE.librime"
          "$<TARGET_FILE_DIR:Main>/Licenses/librime-BSD-3-Clause.txt"
  COMMAND "${CMAKE_COMMAND}" -E copy_if_different
          "${CMAKE_CURRENT_SOURCE_DIR}/third_party/librime/LICENSE.rime-pinyin-simp"
          "$<TARGET_FILE_DIR:Main>/Licenses/rime-pinyin-simp-Apache-2.0.txt"
  VERBATIM
)

if(WIN32)
  set(RIME_RUNTIME_FILE "${RIME_RUNTIME_DIR}/rime.dll")
  set(RIME_RUNTIME_NAME "rime.dll")
else()
  find_library(MU_RIME_RUNTIME_LIBRARY NAMES rime)
  # PLAT-11: find_library leaves the "<var>-NOTFOUND" sentinel on failure; do
  # not let that string flow into later operations. Distinguish "not found"
  # explicitly.
  if(MU_RIME_RUNTIME_LIBRARY STREQUAL "MU_RIME_RUNTIME_LIBRARY-NOTFOUND")
    set(RIME_RUNTIME_FILE "")
  else()
    set(RIME_RUNTIME_FILE "${MU_RIME_RUNTIME_LIBRARY}")
  endif()
  if(APPLE)
    set(RIME_RUNTIME_NAME "librime.dylib")
  else()
    set(RIME_RUNTIME_NAME "librime.so")
  endif()
endif()

if(EXISTS "${RIME_RUNTIME_FILE}")
  add_custom_command(TARGET Main POST_BUILD
    COMMAND "${CMAKE_COMMAND}" -E copy_if_different
            "${RIME_RUNTIME_FILE}"
            "$<TARGET_FILE_DIR:Main>/${RIME_RUNTIME_NAME}"
    VERBATIM
  )
  if(NOT WIN32)
    # PLAT-11: also copy the library's non-system runtime dependencies so the
    # staged librime can actually be dlopen'd at runtime.
    add_custom_command(TARGET Main POST_BUILD
      COMMAND "${CMAKE_COMMAND}"
        "-DINPUT=${RIME_RUNTIME_FILE}"
        "-DTARGET_DIR=$<TARGET_FILE_DIR:Main>"
        "-P" "${REPO_ROOT}/cmake/BundleRimeDeps.cmake"
      VERBATIM
    )
  endif()
else()
  message(STATUS
    "Optional native librime runtime not installed; controller pinyin is unavailable. "
    "On Windows use tools/rime/Install-RimeRuntime.ps1; on Unix install librime "
    "or set MU_RIME_RUNTIME_LIBRARY to the native shared library.")
endif()

# Skeleton Linux-native linkage. This intentionally depends only on
# ubiquitous system libraries so we can focus on getting the project to
# compile; more platform-specific adjustments will follow as we iterate.
if(ANDROID)
  set(MU_GL4ES_ROOT "" CACHE PATH
      "Path to a gl4es source checkout containing CMakeLists.txt and include/GL")
  set(MU_ANDROID_GL4ES_INCLUDE_DIR "" CACHE PATH
      "Directory containing the gl4es GL compatibility headers")
  set(MU_ANDROID_GL4ES_LIBRARY "" CACHE FILEPATH
      "Path to an Android arm64-v8a libGL.so built by gl4es")
  set(MU_ANDROID_TURBOJPEG_LIBRARY "" CACHE FILEPATH
      "Path to an Android arm64-v8a turbojpeg library")

  target_link_libraries(MuClient PUBLIC
    android
    log
    EGL
    c++_shared
  )

  if(MU_GL4ES_ROOT)
    if(MU_ANDROID_GL4ES_INCLUDE_DIR OR MU_ANDROID_GL4ES_LIBRARY)
      message(FATAL_ERROR
        "MU_GL4ES_ROOT cannot be combined with MU_ANDROID_GL4ES_INCLUDE_DIR "
        "or MU_ANDROID_GL4ES_LIBRARY")
    endif()
    if(NOT EXISTS "${MU_GL4ES_ROOT}/CMakeLists.txt"
        OR NOT EXISTS "${MU_GL4ES_ROOT}/include/GL/gl.h"
        OR NOT EXISTS "${MU_GL4ES_ROOT}/include/GL/glu.h")
      message(FATAL_ERROR
        "MU_GL4ES_ROOT is not a gl4es source checkout with GL/GLU headers: "
        "${MU_GL4ES_ROOT}")
    endif()

    set(STATICLIB OFF CACHE BOOL "" FORCE)
    set(NOX11 ON CACHE BOOL "" FORCE)
    set(NO_GBM ON CACHE BOOL "" FORCE)
    set(USE_ANDROID_LOG ON CACHE BOOL "" FORCE)
    add_subdirectory("${MU_GL4ES_ROOT}"
      "${CMAKE_CURRENT_BINARY_DIR}/ThirdParty/gl4es" EXCLUDE_FROM_ALL)
    if(NOT TARGET GL)
      message(FATAL_ERROR "gl4es did not define its expected GL target")
    endif()
    # gl4es defaults to libGL.so.1 on Unix. Android packages native libraries
    # by their .so names, so expose libGL.so as an ordinary CMake dependency.
    set_target_properties(GL PROPERTIES
      OUTPUT_NAME GL
      SUFFIX ".so"
      LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/ThirdParty/gl4es")
    target_link_options(GL PRIVATE "-Wl,-z,max-page-size=16384")
    target_include_directories(MuClient BEFORE PUBLIC
      "${MU_GL4ES_ROOT}/include")
    target_link_libraries(MuClient PUBLIC GL)
  elseif(MU_ANDROID_GL4ES_INCLUDE_DIR AND MU_ANDROID_GL4ES_LIBRARY)
    if(NOT EXISTS "${MU_ANDROID_GL4ES_INCLUDE_DIR}/GL/gl.h")
      message(FATAL_ERROR
        "MU_ANDROID_GL4ES_INCLUDE_DIR does not contain GL/gl.h: "
        "${MU_ANDROID_GL4ES_INCLUDE_DIR}")
    endif()
    if(NOT EXISTS "${MU_ANDROID_GL4ES_LIBRARY}")
      message(FATAL_ERROR
        "MU_ANDROID_GL4ES_LIBRARY does not exist: ${MU_ANDROID_GL4ES_LIBRARY}")
    endif()
    add_library(mu_android_gl4es SHARED IMPORTED)
    set_target_properties(mu_android_gl4es PROPERTIES
      IMPORTED_LOCATION "${MU_ANDROID_GL4ES_LIBRARY}"
      INTERFACE_INCLUDE_DIRECTORIES "${MU_ANDROID_GL4ES_INCLUDE_DIR}")
    target_link_libraries(MuClient PUBLIC mu_android_gl4es)
  elseif(MU_ANDROID_GL4ES_INCLUDE_DIR OR MU_ANDROID_GL4ES_LIBRARY)
    message(FATAL_ERROR
      "Set both MU_ANDROID_GL4ES_INCLUDE_DIR and MU_ANDROID_GL4ES_LIBRARY")
  else()
    target_link_libraries(MuClient PUBLIC GLESv3)
    message(WARNING
      "gl4es is not configured. CMake generation can continue, but MuClient "
      "still includes desktop GL/GLEW headers and requires "
      "MU_GL4ES_ROOT or MU_ANDROID_GL4ES_INCLUDE_DIR plus "
      "MU_ANDROID_GL4ES_LIBRARY to compile.")
  endif()

  if(MU_ANDROID_TURBOJPEG_LIBRARY)
    if(NOT EXISTS "${MU_ANDROID_TURBOJPEG_LIBRARY}")
      message(FATAL_ERROR
        "MU_ANDROID_TURBOJPEG_LIBRARY does not exist: "
        "${MU_ANDROID_TURBOJPEG_LIBRARY}")
    endif()
    target_link_libraries(MuClient PUBLIC "${MU_ANDROID_TURBOJPEG_LIBRARY}")
  else()
    message(WARNING
      "Android turbojpeg is not configured. Final libmain.so linkage requires "
      "MU_ANDROID_TURBOJPEG_LIBRARY.")
  endif()
elseif(OHOS)
  # HarmonyOS / OpenHarmony. Like Android, the engine uses fixed-function
  # desktop OpenGL and needs gl4es translating to OpenGL ES over EGL. Provide
  # a gl4es source checkout built for OHOS; otherwise fall back to EGL/GLES so
  # CMake generation still completes (the final link then needs gl4es).
  set(MU_OHOS_GL4ES_ROOT "" CACHE PATH
      "Path to a gl4es source checkout built for OpenHarmony (contains include/GL)")
  set(MU_OHOS_TURBOJPEG_LIBRARY "" CACHE FILEPATH
      "Path to an OpenHarmony arm64 turbojpeg library")

  # EGL/GLESv3 plus the OHOS logging and C++ runtime. hilog is the OHOS
  # equivalent of Android's liblog.
  find_library(OHOS_EGL_LIBRARY EGL)
  find_library(OHOS_GLES_LIBRARY GLESv3)
  find_library(OHOS_HILOG_LIBRARY hilog_ndk.z)
  target_link_libraries(MuClient PUBLIC
    ${OHOS_EGL_LIBRARY}
    ${OHOS_GLES_LIBRARY}
    ${OHOS_HILOG_LIBRARY}
    c++_shared
  )

  if(MU_OHOS_GL4ES_ROOT)
    if(NOT EXISTS "${MU_OHOS_GL4ES_ROOT}/CMakeLists.txt"
        OR NOT EXISTS "${MU_OHOS_GL4ES_ROOT}/include/GL/gl.h")
      message(FATAL_ERROR
        "MU_OHOS_GL4ES_ROOT is not a gl4es source checkout with GL headers: "
        "${MU_OHOS_GL4ES_ROOT}")
    endif()
    set(STATICLIB OFF CACHE BOOL "" FORCE)
    set(NOX11 ON CACHE BOOL "" FORCE)
    set(NO_GBM ON CACHE BOOL "" FORCE)
    set(USE_OHOS_LOG ON CACHE BOOL "" FORCE)
    add_subdirectory("${MU_OHOS_GL4ES_ROOT}"
      "${CMAKE_CURRENT_BINARY_DIR}/ThirdParty/gl4es" EXCLUDE_FROM_ALL)
    if(NOT TARGET GL)
      message(FATAL_ERROR "gl4es did not define its expected GL target")
    endif()
    set_target_properties(GL PROPERTIES
      OUTPUT_NAME GL
      SUFFIX ".so"
      LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/ThirdParty/gl4es")
    target_link_options(GL PRIVATE "-Wl,-z,max-page-size=16384")
    target_include_directories(MuClient BEFORE PUBLIC
      "${MU_OHOS_GL4ES_ROOT}/include")
    target_link_libraries(MuClient PUBLIC GL)
  else()
    target_link_libraries(MuClient PUBLIC GLESv3)
    message(WARNING
      "gl4es is not configured for HarmonyOS. CMake generation can continue, "
      "but MuClient includes desktop GL headers and requires MU_OHOS_GL4ES_ROOT "
      "(an OHOS gl4es build) to link libmain.so.")
  endif()

  if(MU_OHOS_TURBOJPEG_LIBRARY)
    if(NOT EXISTS "${MU_OHOS_TURBOJPEG_LIBRARY}")
      message(FATAL_ERROR
        "MU_OHOS_TURBOJPEG_LIBRARY does not exist: ${MU_OHOS_TURBOJPEG_LIBRARY}")
    endif()
    target_link_libraries(MuClient PUBLIC "${MU_OHOS_TURBOJPEG_LIBRARY}")
  else()
    message(WARNING
      "HarmonyOS turbojpeg is not configured. Final libmain.so linkage requires "
      "MU_OHOS_TURBOJPEG_LIBRARY.")
  endif()
elseif (CMAKE_SYSTEM_NAME STREQUAL "Linux")
  # The engine uses fixed-function desktop OpenGL via GLEW; link the system GL
  # and GLEW so the gl*/glew* symbols resolve in the Linux executable.
  find_package(OpenGL REQUIRED)
  find_library(GLEW_LIBRARY NAMES GLEW glew32 REQUIRED)
  find_library(TURBOJPEG_LIBRARY NAMES turbojpeg REQUIRED)
  target_link_libraries(MuClient PUBLIC
    pthread
    dl
    m
    OpenGL::GL
    OpenGL::GLU
    ${GLEW_LIBRARY}
    ${TURBOJPEG_LIBRARY}
  )
elseif(APPLE)
  target_include_directories(MuClient BEFORE PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/source/Core/Platform/macOS")
  # macOS exposes desktop OpenGL through a framework. GLEW and turbojpeg remain
  # explicit dependencies because MuClient references their symbols directly.
  find_package(OpenGL REQUIRED)
  find_package(GLEW REQUIRED)
  find_library(TURBOJPEG_LIBRARY NAMES turbojpeg REQUIRED)
  target_link_libraries(MuClient PUBLIC
    OpenGL::GL
    OpenGL::GLU
    GLEW::GLEW
    ${TURBOJPEG_LIBRARY}
  )
endif()

# --- .NET / NuGet infrastructure (shared by wire-sizes codegen and ClientLibrary)
# Lifted up here so the wire-sizes block below can resolve the OpenMU packet
# XML from the same NuGet cache the ClientLibrary uses.
# A Linux target needs the native Linux dotnet to Native-AOT-compile the
# linux-x64 .so (a Windows dotnet.exe cannot cross-OS AOT). Everything else
# (Windows/MinGW targets, including from WSL) prefers dotnet.exe so the win-*
# AOT build works. Pick accordingly.
if (CMAKE_SYSTEM_NAME STREQUAL "Linux")
  find_program(DOTNET_EXECUTABLE dotnet)
  if (NOT DOTNET_EXECUTABLE)
    find_program(DOTNET_EXECUTABLE dotnet.exe)
  endif()
else()
  find_program(DOTNET_EXECUTABLE dotnet.exe)
  if (NOT DOTNET_EXECUTABLE)
    find_program(DOTNET_EXECUTABLE dotnet)
  endif()
endif()

# Helper macro: convert a path to Windows-native format when using
# Windows dotnet.exe from WSL. On all other platforms this is a no-op.
function(mu_native_path input_path output_var)
  if (DOTNET_EXECUTABLE MATCHES "\\.exe$" AND CMAKE_HOST_SYSTEM_NAME STREQUAL "Linux")
    execute_process(
      COMMAND wslpath -w "${input_path}"
      OUTPUT_VARIABLE native_path
      RESULT_VARIABLE rc
      OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    if (rc EQUAL 0)
      set(${output_var} "${native_path}" PARENT_SCOPE)
    else()
      message(FATAL_ERROR "wslpath failed for '${input_path}'. Ensure wslpath is available.")
    endif()
  else()
    set(${output_var} "${input_path}" PARENT_SCOPE)
  endif()
endfunction()

# NuGet cache: defaults to <project>/.nuget, override with -DMU_NUGET_CACHE_DIR=...
set(MU_NUGET_CACHE_DIR "${CMAKE_SOURCE_DIR}/.nuget" CACHE PATH "NuGet package cache directory")
file(MAKE_DIRECTORY "${MU_NUGET_CACHE_DIR}")

# --- Generated wire-size guards ----------------------------------------------
# tools/gen_wire_sizes.py reads OpenMU's authoritative packet XML and emits
# wire_sizes.generated.h with static_asserts that lock each client packet
# struct's sizeof <= the wire length declared by the server. Guards against
# the PR #402 class of bug (silent #pragma pack drift that freezes the client).
#
# The XML lives in the MUnique.OpenMU.Network.Packets NuGet package -- the
# same package the .NET ClientLibrary consumes (see MUnique.Client.Library.csproj).
# Sourcing from NuGet means a single versioned dependency instead of vendoring
# the entire OpenMU repository as a submodule.
set(OPENMU_PACKETS_VERSION "0.9.10" CACHE STRING
    "Version of MUnique.OpenMU.Network.Packets to source packet XML from")
set(OPENMU_PACKETS_XML
    "${MU_NUGET_CACHE_DIR}/munique.openmu.network.packets/${OPENMU_PACKETS_VERSION}/contentFiles/any/net10.0/ServerToClient/ServerToClientPackets.xml")

# Restore the package on demand at configure time if it isn't cached yet.
# dotnet restore is fast on cache-hit, so re-configuring stays cheap.
if (NOT EXISTS "${OPENMU_PACKETS_XML}")
  if (NOT DOTNET_EXECUTABLE)
    message(FATAL_ERROR
      "wire-size codegen needs MUnique.OpenMU.Network.Packets v${OPENMU_PACKETS_VERSION} "
      "but it is not cached at ${MU_NUGET_CACHE_DIR} and no .NET SDK was found to restore "
      "it. Install dotnet (Linux or Windows) or pre-populate the NuGet cache.")
  endif()
  message(STATUS "Restoring MUnique.OpenMU.Network.Packets v${OPENMU_PACKETS_VERSION} for wire-size codegen...")
  set(_packets_csproj "${CMAKE_CURRENT_SOURCE_DIR}/../ClientLibrary/MUnique.Client.Library.csproj")
  mu_native_path("${_packets_csproj}" _packets_csproj_native)
  mu_native_path("${MU_NUGET_CACHE_DIR}" _packets_nuget_native)
  # WSLENV=NUGET_PACKAGES/w is needed so the env var crosses the WSL->Windows
  # interop boundary when DOTNET_EXECUTABLE is dotnet.exe; harmless otherwise.
  execute_process(
    COMMAND ${CMAKE_COMMAND} -E env
            "WSLENV=NUGET_PACKAGES/w"
            "NUGET_PACKAGES=${_packets_nuget_native}"
            "${DOTNET_EXECUTABLE}" restore "${_packets_csproj_native}" --nologo
    RESULT_VARIABLE _restore_rc
  )
  if (NOT _restore_rc EQUAL 0)
    message(FATAL_ERROR "dotnet restore failed (rc=${_restore_rc}) -- see output above.")
  endif()
  if (NOT EXISTS "${OPENMU_PACKETS_XML}")
    message(FATAL_ERROR
      "dotnet restore completed but ServerToClientPackets.xml is still missing at "
      "${OPENMU_PACKETS_XML}. Did the NuGet package layout change?")
  endif()
endif()

set(WIRE_SIZES_GEN_SCRIPT "${REPO_ROOT}/tools/gen_wire_sizes.py")
set(WIRE_SIZES_GEN_HEADER "${CMAKE_BINARY_DIR}/generated/Network/Server/wire_sizes.generated.h")
set(WIRE_SIZES_GEN_STAMP "${CMAKE_BINARY_DIR}/generated/Network/Server/wire_sizes.stamp")
find_package(Python3 COMPONENTS Interpreter REQUIRED)
add_custom_command(
  OUTPUT "${WIRE_SIZES_GEN_STAMP}"
  BYPRODUCTS "${WIRE_SIZES_GEN_HEADER}"
  COMMAND "${Python3_EXECUTABLE}" "${WIRE_SIZES_GEN_SCRIPT}"
          --xml "${OPENMU_PACKETS_XML}"
          --source-label "MUnique.OpenMU.Network.Packets v${OPENMU_PACKETS_VERSION} (NuGet)"
          --output "${WIRE_SIZES_GEN_HEADER}"
  COMMAND "${CMAKE_COMMAND}" -E touch "${WIRE_SIZES_GEN_STAMP}"
  DEPENDS "${WIRE_SIZES_GEN_SCRIPT}" "${OPENMU_PACKETS_XML}"
  COMMENT "Generating wire_sizes.generated.h from OpenMU NuGet packet XML..."
  VERBATIM
)
add_custom_target(GenWireSizes DEPENDS "${WIRE_SIZES_GEN_STAMP}")
# WSclient.h includes wire_sizes.generated.h, and WSclient.cpp compiles into the
# MuClient library, not Main. The codegen ordering must therefore hang off
# MuClient so the header is generated before any consuming TU compiles.
# Attaching it to Main only ordered codegen before Main's own link step, which
# let a parallel `cmake --build build` compile MuClient sources before the
# header existed and fail (workaround was a manual --target GenWireSizes first).
add_dependencies(MuClient GenWireSizes)

target_include_directories(MuClient PUBLIC
  "${CMAKE_CURRENT_SOURCE_DIR}/source"
  # source/App holds the moved PCH header (stdafx.h). Keeping App/ on the
  # include path lets the ~350 bare #include "stdafx.h" resolve unchanged
  # after the bootstrap files moved out of source/ (issue #346).
  "${CMAKE_CURRENT_SOURCE_DIR}/source/App"
  "${CMAKE_CURRENT_SOURCE_DIR}/dependencies/include"
  "${CMAKE_CURRENT_SOURCE_DIR}/dependencies/netcore/includes"
  # Official librime C ABI. The runtime is loaded dynamically so a missing
  # optional DLL disables pinyin without preventing the game from starting.
  "${CMAKE_CURRENT_SOURCE_DIR}/third_party/librime/include"
  "${CMAKE_BINARY_DIR}/generated"
  # stb_truetype backs the portable GDI text rasterizer (issue #462).
  "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/stb"
  $<$<BOOL:${ENABLE_EDITOR}>:${CMAKE_CURRENT_SOURCE_DIR}/MuEditor>
)

# Basic compile definitions, roughly mirroring the original MSVC project
# (fine-tuning per configuration can be done later if needed.)
target_compile_definitions(MuClient PUBLIC
  _LANGUAGE_FOREIGN
  _LANGUAGE_ENG
  UNICODE
  $<$<EQUAL:${CMAKE_SIZEOF_VOID_P},4>:_USE_32BIT_TIME_T>  # Use 32-bit time_t only for 32-bit builds
  $<$<CONFIG:Debug>:_DEBUG;_FOREIGN_DEBUG>
  $<$<CONFIG:Release>:NDEBUG;_FOREIGN_NDEBUG;LDS_PATCH_GLOBAL_100520>
  $<$<BOOL:${ENABLE_EDITOR}>:_EDITOR>  # Editor features for admin builds
)

if (MSVC)
  target_compile_definitions(MuClient PUBLIC
    WIN32
    _WINDOWS
  )

  # Link against Windows system libraries and local static libs
  # Use architecture-specific library directories
  if (CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(LIB_ARCH_DIR "x64")
  else()
    set(LIB_ARCH_DIR "x86")
  endif()

  target_link_directories(MuClient PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/dependencies/lib/${LIB_ARCH_DIR}"
    "${CMAKE_CURRENT_SOURCE_DIR}/dependencies/lib"  # Fallback for arch-independent libs
  )

  target_link_libraries(MuClient PUBLIC
    imm32
    vfw32
    dsound
    dxguid
    opengl32
    glu32
    winmm
    ws2_32
    version
    shlwapi
    crypt32
    wininet
    urlmon
    turbojpeg-static
  )

  # Locate required DLLs in bin directory
  set(DLL_SEARCH_PATH "${CMAKE_CURRENT_SOURCE_DIR}/bin")
  set(REQUIRED_DLL_NAMES glew32 ogg vorbisfile)
  set(REQUIRED_DLLS)

  foreach(DLL_NAME IN LISTS REQUIRED_DLL_NAMES)
    find_file(${DLL_NAME}_DLL
      NAMES "${DLL_NAME}.dll"
      PATHS "${DLL_SEARCH_PATH}"
      NO_DEFAULT_PATH
      REQUIRED
    )
    list(APPEND REQUIRED_DLLS "${${DLL_NAME}_DLL}")
  endforeach()

  # Copy required DLLs to build directory for MSVC builds
  add_custom_command(TARGET Main POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
      ${REQUIRED_DLLS}
      $<TARGET_FILE_DIR:Main>
    COMMENT "Copying required DLLs to build directory"
  )
endif()

# .NET Client Library and tools (platform-agnostic, requires dotnet SDK)
# DOTNET_EXECUTABLE, mu_native_path() and MU_NUGET_CACHE_DIR are already
# resolved above (shared with the wire-size codegen block).
set(MU_CLIENT_LIBRARY_PREBUILT "" CACHE FILEPATH
    "Prebuilt target-native MUnique.Client.Library shared library")
mu_resolve_client_library_platform(
  SYSTEM_NAME "${CMAKE_SYSTEM_NAME}"
  POINTER_SIZE "${CMAKE_SIZEOF_VOID_P}"
  SYSTEM_PROCESSOR "${CMAKE_SYSTEM_PROCESSOR}"
  OSX_ARCHITECTURES "${CMAKE_OSX_ARCHITECTURES}"
  OUT_LIBRARY_NAME DOTNET_LIB_NAME
  OUT_RID DOTNET_RID
  OUT_PLATFORM DOTNET_PLATFORM
  OUT_EXTRA_ARGS DOTNET_EXTRA_ARGS)
message(STATUS ".NET Client Library target: ${DOTNET_RID} (${DOTNET_LIB_NAME})")

set(DOTNET_DLL_PATH "${CMAKE_CURRENT_BINARY_DIR}/$<CONFIG>/${DOTNET_LIB_NAME}")
set(MU_CLIENT_LIBRARY_AVAILABLE FALSE)
if(MU_CLIENT_LIBRARY_PREBUILT)
  get_filename_component(MU_CLIENT_LIBRARY_PREBUILT
    "${MU_CLIENT_LIBRARY_PREBUILT}" ABSOLUTE BASE_DIR "${CMAKE_SOURCE_DIR}")
  if(NOT EXISTS "${MU_CLIENT_LIBRARY_PREBUILT}")
    message(FATAL_ERROR
      "MU_CLIENT_LIBRARY_PREBUILT does not exist: ${MU_CLIENT_LIBRARY_PREBUILT}")
  endif()
  add_custom_command(
    OUTPUT "${DOTNET_DLL_PATH}"
    COMMAND "${CMAKE_COMMAND}" -E make_directory
            "${CMAKE_CURRENT_BINARY_DIR}/$<CONFIG>"
    COMMAND "${CMAKE_COMMAND}" -E copy_if_different
            "${MU_CLIENT_LIBRARY_PREBUILT}" "${DOTNET_DLL_PATH}"
    DEPENDS "${MU_CLIENT_LIBRARY_PREBUILT}"
    COMMENT "Staging prebuilt ${DOTNET_LIB_NAME}"
    VERBATIM)
  set(MU_CLIENT_LIBRARY_AVAILABLE TRUE)
  message(STATUS
    "Using prebuilt .NET Client Library: ${MU_CLIENT_LIBRARY_PREBUILT}")
endif()

if (DOTNET_EXECUTABLE)
  # Native AOT can't cross-compile across OS boundaries: a Linux dotnet
  # targeting win-x86/win-x64 will fail at publish time. Build-time C#
  # tools that emit C++ source (ConstantsReplacer, ResxGen) are unaffected
  # and only need a working dotnet on the host - so gate the ClientLibrary
  # step separately rather than clearing DOTNET_EXECUTABLE altogether.
  set(DOTNET_CAN_BUILD_CLIENT_LIBRARY TRUE)
  if(MU_CLIENT_LIBRARY_PREBUILT)
    set(DOTNET_CAN_BUILD_CLIENT_LIBRARY FALSE)
  elseif(CMAKE_SYSTEM_NAME STREQUAL "iOS")
    # PLAT-1: iOS previews consume a prebuilt dylib (built on macOS with the
    # ios-arm64 runtime); a plain `dotnet publish` from the generated Xcode
    # project is not the supported path.
    message(WARNING "The iOS client library is packaged as a prebuilt. "
      "libMUnique.Client.Library.dylib will NOT be built by this project; set "
      "MU_CLIENT_LIBRARY_PREBUILT to a macOS-built ios-arm64 dylib.")
    set(DOTNET_CAN_BUILD_CLIENT_LIBRARY FALSE)
  elseif(OHOS)
    # The OpenHarmony Native AOT library needs the OHOS NDK sysroot and is built
    # by nativeaot-ohos/build-clientlibrary-ohos.sh on Linux, not by a plain
    # `dotnet publish` from DevEco's CMake. It is packaged as a prebuilt.
    message(WARNING "The OpenHarmony Native AOT client library is built with "
      "nativeaot-ohos/build-clientlibrary-ohos.sh (Linux + OHOS NDK), not from "
      "this build. Drop the resulting libMUnique.Client.Library.so into the "
      "HarmonyOS project's entry/src/main/cpp/prebuilt/<abi>/ folder, or set "
      "MU_CLIENT_LIBRARY_PREBUILT to it.")
    set(DOTNET_CAN_BUILD_CLIENT_LIBRARY FALSE)
  elseif(ANDROID AND NOT CMAKE_HOST_SYSTEM_NAME STREQUAL "Linux")
    message(WARNING "The Android Native AOT client library must be built on Linux. "
      "libMUnique.Client.Library.so will NOT be built on ${CMAKE_HOST_SYSTEM_NAME}; "
      "set MU_CLIENT_LIBRARY_PREBUILT to a linux-bionic-arm64 build.")
    set(DOTNET_CAN_BUILD_CLIENT_LIBRARY FALSE)
  elseif (NOT DOTNET_EXECUTABLE MATCHES "\\.exe$" AND CMAKE_SYSTEM_NAME STREQUAL "Windows")
    message(WARNING "Found Linux dotnet but target is Windows. "
      "Cross-OS Native AOT is not supported. MUnique.Client.Library.dll will NOT be built. "
      "Install the Windows .NET SDK or use WSL interop (dotnet.exe) to enable.")
    set(DOTNET_CAN_BUILD_CLIENT_LIBRARY FALSE)
  elseif(CMAKE_SYSTEM_NAME STREQUAL "Darwin"
      AND NOT CMAKE_HOST_SYSTEM_NAME STREQUAL "Darwin")
    message(WARNING "The macOS Native AOT client library must be built on macOS. "
      "MUnique.Client.Library.dylib will NOT be built on ${CMAKE_HOST_SYSTEM_NAME}.")
    set(DOTNET_CAN_BUILD_CLIENT_LIBRARY FALSE)
  endif()
endif()
if (DOTNET_EXECUTABLE)
  message(STATUS "Found .NET SDK: ${DOTNET_EXECUTABLE}")

  # 1. The platform-native name and Native AOT target were resolved above so
  # the prebuilt-library path also works when the host has no .NET SDK.
  # DXP: $<CONFIG>-suffixed, not shared between Debug/Release. It used to be one shared path
  # with -c $<CONFIG> in the publish command below -- same bug class as ResxGen's fix just above
  # (Ninja Multi-Config generates a separate rule per config for whatever command produces a given
  # OUTPUT; a shared path with a config-dependent command makes every config flip look like "the
  # command that produced this output changed," forcing a full ClientLibrary republish on every
  # single Debug<->Release switch). Here it's worse than ResxGen's case: Debug and Release
  # legitimately SHOULD produce different DLLs (different optimization/debug info), so the fix is
  # to give each config its own path end-to-end, not pin to one fixed config. The republish
  # ping-pong was also the proximate cause of a real build failure (2026-08-03): it kept
  # re-triggering ClientLibrary's own XSLT-based PacketFunctions_ClientToServer.cpp regeneration
  # step in parallel with the main C++ build's compile of that same (checked-in source) file,
  # racing on Windows' exclusive file lock.
  set(DOTNET_PROJ "${CMAKE_CURRENT_SOURCE_DIR}/../ClientLibrary/MUnique.Client.Library.csproj")

  # Create temp directories for Native AOT build.
  set(DOTNET_TEMP_OUTPUT "${CMAKE_BINARY_DIR}/dotnet_out")
  set(DOTNET_TEMP_DIR "${CMAKE_BINARY_DIR}/.dotnet_temp")
  set(DOTNET_NUGET_DIR "${MU_NUGET_CACHE_DIR}")
  file(MAKE_DIRECTORY "${DOTNET_TEMP_OUTPUT}")
  file(MAKE_DIRECTORY "${DOTNET_TEMP_DIR}")

  # Convert paths to Windows-native format for MSBuild (no-op outside WSL).
  mu_native_path("${DOTNET_PROJ}" DOTNET_PROJ_NATIVE)
  mu_native_path("${DOTNET_TEMP_OUTPUT}" DOTNET_TEMP_OUTPUT_NATIVE)
  mu_native_path("${DOTNET_NUGET_DIR}" DOTNET_NUGET_DIR_NATIVE)
  mu_native_path("${DOTNET_TEMP_DIR}" DOTNET_TEMP_DIR_NATIVE)

  # 2. Tell CMake to watch ALL .cs files in the ClientLibrary folder
  file(GLOB_RECURSE DOTNET_SOURCES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/../ClientLibrary/*.cs")
  list(FILTER DOTNET_SOURCES EXCLUDE REGEX "/(obj|bin)/")

  # 3. Build to temp directory with Native AOT, then copy to build and bin directories
  # Override environment variables to avoid issues with umlauts in Windows username
  if (DOTNET_CAN_BUILD_CLIENT_LIBRARY)
    add_custom_command(
            OUTPUT "${DOTNET_DLL_PATH}"
            COMMAND ${CMAKE_COMMAND} -E echo "--- C# Changes Detected: Rebuilding Client Library ---"
            COMMAND ${CMAKE_COMMAND} -E make_directory "${CMAKE_CURRENT_BINARY_DIR}/$<CONFIG>"
            COMMAND ${CMAKE_COMMAND} -E env
                    "NUGET_PACKAGES=${DOTNET_NUGET_DIR_NATIVE}"
                    "DOTNET_CLI_HOME=${DOTNET_TEMP_DIR_NATIVE}"
                    "TEMP=${DOTNET_TEMP_DIR_NATIVE}"
                    "TMP=${DOTNET_TEMP_DIR_NATIVE}"
                    "${DOTNET_EXECUTABLE}" publish "${DOTNET_PROJ_NATIVE}" -c $<CONFIG> -r ${DOTNET_RID} -p:PlatformTarget=${DOTNET_PLATFORM} ${DOTNET_EXTRA_ARGS} -o "${DOTNET_TEMP_OUTPUT_NATIVE}" --nologo
            COMMAND ${CMAKE_COMMAND} -E copy_if_different "${DOTNET_TEMP_OUTPUT}/${DOTNET_LIB_NAME}" "${DOTNET_DLL_PATH}"
            DEPENDS "${DOTNET_PROJ}" ${DOTNET_SOURCES}
            COMMENT "Checking for .NET Client Library updates..."
            VERBATIM
    )
    set(MU_CLIENT_LIBRARY_AVAILABLE TRUE)
  endif()

  # ConstantsReplacer build target
  set(CONSTANTS_REPLACER_PROJ "${CMAKE_CURRENT_SOURCE_DIR}/../ConstantsReplacer/ConstantsReplacer.csproj")
  mu_native_path("${CONSTANTS_REPLACER_PROJ}" CONSTANTS_REPLACER_PROJ_NATIVE)
  mu_native_path("${CMAKE_BINARY_DIR}/ConstantsReplacer" CONSTANTS_REPLACER_OUTDIR_NATIVE)
  file(GLOB_RECURSE CONSTANTS_REPLACER_SOURCES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/../ConstantsReplacer/*.cs")
  set(CONSTANTS_REPLACER_OUTPUT "${CMAKE_BINARY_DIR}/ConstantsReplacer/ConstantsReplacer.exe")

  add_custom_command(
    OUTPUT "${CONSTANTS_REPLACER_OUTPUT}"
    COMMAND "${DOTNET_EXECUTABLE}" build "${CONSTANTS_REPLACER_PROJ_NATIVE}" -c $<CONFIG> -o "${CONSTANTS_REPLACER_OUTDIR_NATIVE}" --nologo
    DEPENDS "${CONSTANTS_REPLACER_PROJ}" ${CONSTANTS_REPLACER_SOURCES}
    COMMENT "Building ConstantsReplacer tool..."
    VERBATIM
  )

  add_custom_target(ConstantsReplacer DEPENDS "${CONSTANTS_REPLACER_OUTPUT}")

  # ResxGen: .resx -> typed C++ accessors under namespace I18N::<Group>.
  # Discovers groups from filenames (<Group>.<locale>.resx) and emits one
  # .h/.cpp per group plus a master All.h/All.cpp. Generated output lives
  # in the build tree only and is consumed by Main via target_sources.
  set(RESXGEN_PROJ "${CMAKE_CURRENT_SOURCE_DIR}/../tools/ResxGen/ResxGen.csproj")
  set(RESX_INPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/Localization")
  set(RESX_OUTPUT_DIR "${CMAKE_BINARY_DIR}/Generated/I18N")
  file(MAKE_DIRECTORY "${RESX_OUTPUT_DIR}")

  file(GLOB RESX_FILES CONFIGURE_DEPENDS "${RESX_INPUT_DIR}/*.resx")
  file(GLOB_RECURSE RESXGEN_SOURCES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/../tools/ResxGen/*.cs")
  list(FILTER RESXGEN_SOURCES EXCLUDE REGEX "/(bin|obj)/")

  if (RESX_FILES)
    # Compute groups from filenames: Editor.en.resx -> group "Editor".
    set(RESX_GROUPS "")
    foreach(resx_file IN LISTS RESX_FILES)
      get_filename_component(stem "${resx_file}" NAME_WE)  # Editor.en
      string(REGEX REPLACE "\\.[^.]*$" "" group_name "${stem}")
      list(APPEND RESX_GROUPS "${group_name}")
    endforeach()
    list(REMOVE_DUPLICATES RESX_GROUPS)

    # Predict generator output for the custom command's OUTPUT list.
    set(RESX_GEN_HEADERS "${RESX_OUTPUT_DIR}/All.h")
    set(RESX_GEN_SOURCES "${RESX_OUTPUT_DIR}/All.cpp")
    foreach(group_name IN LISTS RESX_GROUPS)
      list(APPEND RESX_GEN_HEADERS "${RESX_OUTPUT_DIR}/${group_name}.h")
      list(APPEND RESX_GEN_SOURCES "${RESX_OUTPUT_DIR}/${group_name}.cpp")
    endforeach()

    mu_native_path("${RESXGEN_PROJ}" RESXGEN_PROJ_NATIVE)
    mu_native_path("${RESX_INPUT_DIR}" RESX_INPUT_DIR_NATIVE)
    mu_native_path("${RESX_OUTPUT_DIR}" RESX_OUTPUT_DIR_NATIVE)

    set(RESX_GEN_STAMP "${RESX_OUTPUT_DIR}/generation.stamp")
    add_custom_command(
      OUTPUT "${RESX_GEN_STAMP}"
      BYPRODUCTS ${RESX_GEN_HEADERS} ${RESX_GEN_SOURCES}
      # -c Release (NOT $<CONFIG>) is deliberate: RESX_OUTPUT_DIR is shared between the Debug and
      # Release C++ builds (not config-suffixed), but Ninja Multi-Config generates a separate rule
      # per config for whatever command produces a given OUTPUT. With $<CONFIG> here, building
      # Debug then Release (build-all.ps1's default) recorded two different command lines for the
      # same output paths -- Ninja saw each config's rule as "the command that produced this
      # output changed" and reran ResxGen every single invocation, and CppEmitter.cs's unconditional
      # File.WriteAllText bumped every generated I18N file's mtime, cascading into a near-full
      # rebuild of every TU that includes an I18N header (most of the tree). The generated I18N
      # C++ source content never actually depended on which .NET config built the *generator*, so
      # pinning it to one fixed config removes the config-dependence entirely instead of fixing it
      # per-config (which would just duplicate identical output under Debug/Release subdirs).
      COMMAND ${CMAKE_COMMAND} -E env
              "NUGET_PACKAGES=${DOTNET_NUGET_DIR_NATIVE}"
              "DOTNET_CLI_HOME=${DOTNET_TEMP_DIR_NATIVE}"
              "${DOTNET_EXECUTABLE}" run --project "${RESXGEN_PROJ_NATIVE}" -c Release
              -- --input "${RESX_INPUT_DIR_NATIVE}" --output "${RESX_OUTPUT_DIR_NATIVE}"
              --wide-groups Game,Dialog
      COMMAND "${CMAKE_COMMAND}" -E touch "${RESX_GEN_STAMP}"
      DEPENDS ${RESX_FILES} ${RESXGEN_SOURCES} "${RESXGEN_PROJ}"
      COMMENT "ResxGen: regenerating C++ I18N accessors from .resx"
      VERBATIM
    )

    add_custom_target(ResxGen DEPENDS "${RESX_GEN_STAMP}")
    add_dependencies(MuClient ResxGen)
    target_sources(MuClient PRIVATE ${RESX_GEN_SOURCES})
    target_include_directories(MuClient PUBLIC "${CMAKE_BINARY_DIR}/Generated")
  endif()
else()
  if(NOT MU_CLIENT_LIBRARY_PREBUILT)
    message(WARNING ".NET SDK not found. The managed client library will NOT be built. "
      "The game will run but cannot connect to the server. Install .NET SDK 10.0+ "
      "or set MU_CLIENT_LIBRARY_PREBUILT to enable.")
  endif()
endif()

if(MU_CLIENT_LIBRARY_AVAILABLE)
  # The game resolves the Native AOT exports with LoadLibrary/dlopen at runtime,
  # so stage the generated or supplied library beside the platform target.
  add_custom_target(ClientLibrary DEPENDS "${DOTNET_DLL_PATH}")
  add_dependencies(Main ClientLibrary)
  # Native publishing can regenerate checked-in packet bindings as a side
  # effect. Keep it ordered before any MuClient translation unit compiles.
  add_dependencies(MuClient ClientLibrary)
  set_target_properties(ClientLibrary PROPERTIES EXCLUDE_FROM_ALL TRUE)

  add_custom_command(TARGET Main POST_BUILD
    COMMAND "${CMAKE_COMMAND}" -E copy_if_different
      "${DOTNET_DLL_PATH}" "$<TARGET_FILE_DIR:Main>/${DOTNET_LIB_NAME}"
    COMMENT "Staging ${DOTNET_LIB_NAME} beside the native client"
    VERBATIM)
endif()

# DXP-10 step 4: state-wrapper monopoly guard. Fails the build if a raw gl*()
# call appears outside Render/ that isn't on tools/check_gl_wrapper_monopoly.py's
# allowlist (which mirrors DXP-10-state-wrapper-monopoly.md's documented, deliberate
# deferrals -- DXP-12 texture territory, DXP-14 fog params, debug/editor files).
# No OUTPUT means this target is always considered out-of-date and re-runs every build.
add_custom_target(CheckGLWrapperMonopoly ALL
  COMMAND "${Python3_EXECUTABLE}" "${REPO_ROOT}/tools/check_gl_wrapper_monopoly.py"
          --source-root "${CMAKE_CURRENT_SOURCE_DIR}/source"
  COMMENT "DXP-10: checking for raw gl* calls outside Render/ (state-wrapper monopoly guard)"
  VERBATIM
)
add_dependencies(MuClient CheckGLWrapperMonopoly)

# MinGW (32-bit or 64-bit) cross-toolchains: link against the Windows/OpenGL
# system libraries so that gl*, wgl*, Imm*, timeGetTime, etc. resolve.
# For turbojpeg and wzAudio we link against the logically named libraries,
# which must be provided as MinGW-compatible .a import libraries.
if (CMAKE_CXX_COMPILER MATCHES "mingw32" OR CMAKE_CXX_COMPILER MATCHES "w64-mingw32")
  target_link_directories(MuClient PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/dependencies/lib"
  )

  set(MU_TURBOJPEG_STATIC_LIB "" CACHE FILEPATH "Path to MinGW-w64 static libturbojpeg.a")
  if (MU_TURBOJPEG_STATIC_LIB STREQUAL "" OR MU_TURBOJPEG_STATIC_LIB MATCHES "-NOTFOUND$")
    unset(MU_TURBOJPEG_STATIC_LIB CACHE)
  endif()
  if (NOT MU_TURBOJPEG_STATIC_LIB)
    find_library(MU_TURBOJPEG_STATIC_LIB
      NAMES turbojpeg
      PATHS
        "${CMAKE_CURRENT_SOURCE_DIR}/dependencies/lib"
      NO_CMAKE_FIND_ROOT_PATH
    )
  endif()
  if (NOT MU_TURBOJPEG_STATIC_LIB)
    set(_MU_OLD_FIND_LIBRARY_SUFFIXES "${CMAKE_FIND_LIBRARY_SUFFIXES}")
    set(CMAKE_FIND_LIBRARY_SUFFIXES ".a")
    set(_MU_TURBOJPEG_SEARCH_PATHS "")
    foreach(_mu_root IN LISTS CMAKE_FIND_ROOT_PATH)
      list(APPEND _MU_TURBOJPEG_SEARCH_PATHS "${_mu_root}/lib" "${_mu_root}/usr/lib")
    endforeach()
    list(APPEND _MU_TURBOJPEG_SEARCH_PATHS "/usr/i686-w64-mingw32/lib" "/usr/x86_64-w64-mingw32/lib")
    find_library(MU_TURBOJPEG_STATIC_LIB
      NAMES turbojpeg
      PATHS ${_MU_TURBOJPEG_SEARCH_PATHS}
      NO_CMAKE_FIND_ROOT_PATH
    )
    unset(_MU_TURBOJPEG_SEARCH_PATHS)
    unset(_mu_root)
    set(CMAKE_FIND_LIBRARY_SUFFIXES "${_MU_OLD_FIND_LIBRARY_SUFFIXES}")
    unset(_MU_OLD_FIND_LIBRARY_SUFFIXES)
  endif()
  if (NOT MU_TURBOJPEG_STATIC_LIB OR MU_TURBOJPEG_STATIC_LIB MATCHES "\\.dll\\.a$")
    message(FATAL_ERROR "Static libturbojpeg not found (libturbojpeg.a) or CMake picked an import library (.dll.a): '${MU_TURBOJPEG_STATIC_LIB}'. Install/build a MinGW-w64 static libjpeg-turbo to avoid shipping libturbojpeg.dll, or set MU_TURBOJPEG_STATIC_LIB to the full path of libturbojpeg.a.")
  endif()

  target_link_libraries(MuClient PUBLIC
    imm32
    vfw32
    dsound
    dxguid
    opengl32
    glu32
    winmm
    ws2_32
    version
    shlwapi
    crypt32
    wininet
    urlmon
    gdi32
    user32
    ole32
    oleaut32
    uuid
    comdlg32
    advapi32
    ${MU_TURBOJPEG_STATIC_LIB}
  )
endif()

# Link SDL3 + SDL3_mixer (audio) on all platforms.
target_link_libraries(MuClient PUBLIC
  SDL3::SDL3
  SDL3_mixer::SDL3_mixer
)

if(ENABLE_FRAMEBUFFER_CAPTURE_TESTS)
  target_compile_definitions(Main PRIVATE MU_ENABLE_FRAMEBUFFER_CAPTURE_TESTS=1)
endif()

if(ENABLE_VIRTUAL_GAMEPAD_TESTS)
  # Debug keeps the original Debug-only hook. RelWithDebInfo carries it too so
  # the personal single-player build can run the gamepad/focus/haptic acceptance
  # harness without debug-CRT mismatch; it stays inert unless
  # MU_VIRTUAL_GAMEPAD_STATE is set. Release is left clean.
  target_compile_definitions(MuClient PRIVATE
    $<$<CONFIG:Debug>:MU_ENABLE_VIRTUAL_GAMEPAD_TESTS=1>
    $<$<CONFIG:RelWithDebInfo>:MU_ENABLE_VIRTUAL_GAMEPAD_TESTS=1>)
  # Winmain.cpp belongs to the thin Main target, not MuClient. Keep the
  # framebuffer acceptance bridge out of every normal/Release executable.
  target_compile_definitions(Main PRIVATE
    $<$<CONFIG:Debug>:MU_ENABLE_VIRTUAL_GAMEPAD_TESTS=1>
    $<$<CONFIG:RelWithDebInfo>:MU_ENABLE_VIRTUAL_GAMEPAD_TESTS=1>
    $<$<CONFIG:Debug>:MU_ENABLE_FRAMEBUFFER_CAPTURE_TESTS=1>)
endif()

# Link ImGui for all platforms when editor is enabled. imgui carries its own
# opengl32/dwmapi link dependencies (see its definition above), so the engine
# only needs to link the imgui target itself.
if(ENABLE_EDITOR)
  target_link_libraries(MuClient PUBLIC imgui)
endif()
