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
# Platform/iOS/main.mm 的数据根约定）。此处仅声明资源目录约定：
if(EXISTS "${CMAKE_CURRENT_LIST_DIR}/resources/game-data.zip")
  set_source_files_properties("${CMAKE_CURRENT_LIST_DIR}/resources/game-data.zip"
    PROPERTIES MACOSX_PACKAGE_LOCATION Resources)
  target_sources(Main PRIVATE "${CMAKE_CURRENT_LIST_DIR}/resources/game-data.zip")
endif()
