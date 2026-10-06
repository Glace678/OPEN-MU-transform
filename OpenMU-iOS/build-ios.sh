#!/usr/bin/env bash
# 生成并构建 iOS 预览版 OpenMU-Game.app（Xcode 生成器，真机 arm64）。
#
# 前置：macOS + Xcode 16+（含 iOS 15+ SDK）、CMake ≥3.25、.NET SDK 10
# （协议库需要，见 nativeaot-ios/）。真机构建需要 Apple 开发者签名：
#   DEVELOPMENT_TEAM=<你的团队ID> ./build-ios.sh
# 模拟器构建（仅渲染冒烟，不支持真机性能结论）：
#   MU_IOS_SIMULATOR=1 ./build-ios.sh
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
BUILD_DIR="${SCRIPT_DIR}/build/ios"
SYSROOT="iphoneos"
# I-05: the previous version always passed CMAKE_OSX_ARCHITECTURES=arm64 even for
# the simulator. On an Intel Mac the simulator is x86_64, so an arm64 simulator
# slice cannot run. Pick the simulator arch from the host (Apple Silicon -> arm64,
# Intel -> x86_64), overridable with MU_IOS_ARCH. Device builds stay arm64.
if [[ "${MU_IOS_SIMULATOR:-0}" == "1" ]]; then
  SYSROOT="iphonesimulator"
  HOST_ARCH="$(uname -m)"
  case "${HOST_ARCH}" in
    arm64|aarch64) MU_DEFAULT_ARCH="arm64" ;;
    x86_64)        MU_DEFAULT_ARCH="x86_64" ;;
    *)             MU_DEFAULT_ARCH="arm64" ;;
  esac
else
  MU_DEFAULT_ARCH="arm64"
fi
MU_IOS_ARCH="${MU_IOS_ARCH:-${MU_DEFAULT_ARCH}}"

TEAM_ARGS=()
if [[ -n "${DEVELOPMENT_TEAM:-}" ]]; then
  TEAM_ARGS+=("-DCMAKE_XCODE_ATTRIBUTE_DEVELOPMENT_TEAM=${DEVELOPMENT_TEAM}")
fi

# PLAT-1: point the engine at the dylib staged for embedding when it exists.
# Passing a non-existent path is a FATAL, so only append when the file is there.
PREBUILT_ARGS=()
IOS_PREBUILT="${SCRIPT_DIR}/game-ios/native/libMUnique.Client.Library.dylib"
if [[ -f "${IOS_PREBUILT}" ]]; then
  PREBUILT_ARGS+=("-DMU_CLIENT_LIBRARY_PREBUILT=${IOS_PREBUILT}")
fi

cmake -S "${SCRIPT_DIR}/game-ios" -B "${BUILD_DIR}" -G Xcode \
  -DCMAKE_SYSTEM_NAME=iOS \
  -DCMAKE_OSX_SYSROOT="${SYSROOT}" \
  -DCMAKE_OSX_ARCHITECTURES="${MU_IOS_ARCH}" \
  -DCMAKE_OSX_DEPLOYMENT_TARGET="${MU_IOS_DEPLOYMENT_TARGET:-15.0}" \
  -DMU_IOS_PREVIEW=ON \
  -DENABLE_EDITOR=OFF \
  -DBUILD_TESTING=OFF \
  "${TEAM_ARGS[@]}" \
  "${PREBUILT_ARGS[@]}"

cmake --build "${BUILD_DIR}" --config Release --target Main -- -allowProvisioningUpdates

APP_DIR="${BUILD_DIR}/Release-${SYSROOT}"
echo
echo "构建产物目录: ${APP_DIR}"
echo "  OpenMU-Game.app   用 Xcode 打开 ${BUILD_DIR}/OpenMUiOS.xcodeproj 部署到设备，"
echo "                    或: xcrun devicectl device install app --device <udid> ${APP_DIR}/OpenMU-Game.app"
echo
echo "剩余移植事项（详见 BUILD-iOS构建说明.md）：渲染层 GLES/Metal 真机验证、"
echo "协议库 @rpath 加载、game-data.zip 首启动解包、IME 与安全区。"
