cmake_minimum_required(VERSION 3.25)

# iOS 游戏壳工程（预览脚手架）。由 ../build-ios.sh 以 Xcode 生成器配置：
#   cmake -G Xcode -DCMAKE_SYSTEM_NAME=iOS -DMU_IOS_PREVIEW=ON ...
# main.mm 需要 Objective-C++；SDL3 的 CMake 在 iOS 工具链下会自行链接
# UIKit/Metal 等系统框架。
project(OpenMUiOS LANGUAGES C CXX OBJC OBJCXX)

# 解析共享的 MuMain 检出目录（仓库布局：OpenMU-iOS/game-ios 与 MuMain 平级）。
# 布局不同时用 -DMU_REPOSITORY_ROOT=<绝对路径> 覆盖。
if(NOT DEFINED MU_REPOSITORY_ROOT)
  get_filename_component(MU_REPOSITORY_ROOT
    "${CMAKE_CURRENT_LIST_DIR}/../../MuMain" ABSOLUTE)
endif()
if(NOT EXISTS "${MU_REPOSITORY_ROOT}/src/CMakeLists.txt")
  message(FATAL_ERROR
    "MuMain checkout not found at '${MU_REPOSITORY_ROOT}'. Pass "
    "-DMU_REPOSITORY_ROOT=<absolute path to MuMain>.")
endif()

# 打开引擎里的 iOS 预览分支（默认 configure 会明确报“暂不支持”）。
set(MU_IOS_PREVIEW ON CACHE BOOL "Enable the experimental iOS application scaffold" FORCE)

add_subdirectory("${MU_REPOSITORY_ROOT}/src" "${CMAKE_CURRENT_BINARY_DIR}/mumain")

# ---------------------------------------------------------------------------
# Bundle 元数据与签名。产物为 OpenMU-Game.app；真机构建需要签名团队：
#   DEVELOPMENT_TEAM=xxxxxxxxxx ./build-ios.sh
# （脚本会转成 -DCMAKE_XCODE_ATTRIBUTE_DEVELOPMENT_TEAM）
# ---------------------------------------------------------------------------
set(MU_IOS_BUNDLE_ID "net.munique.openmu.game" CACHE STRING "iOS application bundle identifier")
set(MU_IOS_BUNDLE_VERSION "1.0.0" CACHE STRING "iOS application marketing version")

set_target_properties(Main PROPERTIES
  OUTPUT_NAME "OpenMU-Game"
  MACOSX_BUNDLE TRUE
  MACOSX_BUNDLE_INFO_PLIST "${CMAKE_CURRENT_LIST_DIR}/Info.plist.in"
  MACOSX_BUNDLE_GUI_IDENTIFIER "${MU_IOS_BUNDLE_ID}"
  MACOSX_BUNDLE_BUNDLE_NAME "OpenMU-Game"
  MACOSX_BUNDLE_EXECUTABLE_NAME "OpenMU-Game"
  MACOSX_BUNDLE_SHORT_VERSION_STRING "${MU_IOS_BUNDLE_VERSION}"
  MACOSX_BUNDLE_BUNDLE_VERSION "${MU_IOS_BUNDLE_VERSION}"
  XCODE_ATTRIBUTE_PRODUCT_BUNDLE_IDENTIFIER "${MU_IOS_BUNDLE_ID}"
  XCODE_ATTRIBUTE_TARGETED_DEVICE_FAMILY "1,2"
  XCODE_ATTRIBUTE_CODE_SIGN_STYLE "Automatic"
  XCODE_ATTRIBUTE_ENABLE_BITCODE NO)

# 游戏数据（Data/ + fonts/，约 447MB）不入库；打包时把上游 data 发布解出的
# game-data.zip 放进 bundle 资源，首启动解包到 Documents/Data（对应
# Platform/iOS/main.mm 的数据根约定）。
#
# I-02: previously the zip was silently skipped when absent, producing an
# incomplete but successfully built .app that only failed at first launch. Treat
# the data archive as mandatory; pass -DMU_IOS_ALLOW_EMPTY_DATA=ON only for an
# explicitly non-runnable shell build.
set(MU_GAME_DATA_ZIP "${CMAKE_CURRENT_LIST_DIR}/resources/game-data.zip")
if(NOT EXISTS "${MU_GAME_DATA_ZIP}")
  if(NOT MU_IOS_ALLOW_EMPTY_DATA)
    message(FATAL_ERROR
      "Missing '${MU_GAME_DATA_ZIP}'. Stage the pinned data release there before "
      "building, or pass -DMU_IOS_ALLOW_EMPTY_DATA=ON for a non-runnable shell.")
  endif()
else()
  # I-03: verify the archive against a committed/expected SHA-256 sidecar before
  # bundling. game-data.zip.sha256 must contain the expected hex digest (optionally
  # followed by the filename). Fail the configure if it is missing or mismatches.
  set(MU_GAME_DATA_SIG "${MU_GAME_DATA_ZIP}.sha256")

  # I-03 verification lives in the standalone VerifyDataArchive.cmake so the
  # same check is host-side testable (XC-18). FATALs at configure on a
  # missing sidecar or mismatch.
  set(ARCHIVE "${MU_GAME_DATA_ZIP}")
  set(SIDECAR "${MU_GAME_DATA_SIG}")
  include("${CMAKE_CURRENT_LIST_DIR}/VerifyDataArchive.cmake")

  set_source_files_properties("${MU_GAME_DATA_ZIP}"
    PROPERTIES MACOSX_PACKAGE_LOCATION Resources)
  target_sources(Main PRIVATE "${MU_GAME_DATA_ZIP}")
endif()

# I-01: the NativeAOT client dylib must be embedded in the app bundle's
# Frameworks/ directory and code-signed, not left as a loose file next to the
# sources. Declare it as a framework-located resource and set the rpath so the
# main executable can load it at @executable_path/Frameworks.
set(MU_IOS_AOT_DYLIB "${CMAKE_CURRENT_LIST_DIR}/native/libMUnique.Client.Library.dylib")
if(EXISTS "${MU_IOS_AOT_DYLIB}")
  set_source_files_properties("${MU_IOS_AOT_DYLIB}"
    PROPERTIES MACOSX_PACKAGE_LOCATION Frameworks)
  target_sources(Main PRIVATE "${MU_IOS_AOT_DYLIB}")
  set_target_properties(Main PROPERTIES
    XCODE_ATTRIBUTE_LD_RUNPATH_SEARCH_PATHS "@executable_path/Frameworks"
    XCODE_ATTRIBUTE_EMBEDDED_CONTENT_CONTAINS_SWIFT NO)
elseif(NOT MU_IOS_ALLOW_EMPTY_DATA)
  message(FATAL_ERROR
    "Missing NativeAOT dylib '${MU_IOS_AOT_DYLIB}'. Build it with "
    "../nativeaot-ios/build-clientlibrary-ios.sh first (I-01).")
endif()
