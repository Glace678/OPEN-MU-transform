#!/usr/bin/env bash
# Builds the .NET protocol library (MuMain/ClientLibrary) as a native
# libMUnique.Client.Library.dylib for iOS (arm64), analogous to
# OpenMU-Android/nativeaot/build-clientlibrary-android.sh and
# OpenMU-HarmonyOS/nativeaot-ohos/build-clientlibrary-ohos.sh.
#
# 现状（务必阅读）：
#   * .NET 官方对 ios-arm64 的 NativeAOT（NativeLib=Shared 产出 dylib）支持
#     仍属实验/预览范畴；正式受支持的 iOS AOT 路线是 .NET for iOS workload
#     （net10.0-ios 类库 + 平台 AOT 编译器，产物为静态库并入 App）。
#   * 本脚本先走与安卓/鸿蒙一致的 NativeAOT 路线以保持三端同构；失败时给出
#     workload 路线指引。产物需放入 OpenMU-Game.app/Frameworks/ 并以
#     @rpath/libMUnique.Client.Library.dylib 加载（props 已写入 install_name）。
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT_FILE="$(cd -- "${SCRIPT_DIR}/../../MuMain/ClientLibrary" && pwd -P)/MUnique.Client.Library.csproj"
BUILD_ROOT="${SCRIPT_DIR}/build"
PUBLISH_DIR="${BUILD_ROOT}/publish"
FINAL_DIR="${SCRIPT_DIR}/../game-ios/native"
FINAL_LIBRARY_NAME="libMUnique.Client.Library.dylib"

RID="${OPENMU_IOS_RID:-ios-arm64}"

mkdir -p -- "${PUBLISH_DIR}" "${FINAL_DIR}"

if ! dotnet workload list 2>/dev/null | grep -qi '^ios'; then
  echo "提示: 未检测到 .NET iOS workload。若下面的 publish 失败，请先执行:" >&2
  echo "  dotnet workload install ios" >&2
fi

dotnet publish "${PROJECT_FILE}" \
    --configuration Release \
    --runtime "${RID}" \
    --output "${PUBLISH_DIR}" \
    -p:BaseIntermediateOutputPath="${BUILD_ROOT}/obj/" \
    -p:BaseOutputPath="${BUILD_ROOT}/bin/" \
    -p:EnableDefaultCompileItems=false \
    -p:CustomBeforeMicrosoftCommonProps="${SCRIPT_DIR}/ClientLibrary.IosAot.props" \
    -p:DisableUnsupportedError=true \
    -p:NativeLib=Shared \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:StripSymbols=false \
    -p:ci=true \
    2>&1 | tee "${SCRIPT_DIR}/publish.log" || {
      echo
      echo "Publish for ${RID} failed. iOS NativeAOT 属实验路线，常见处理："
      echo "  1) dotnet workload install ios 后重试；"
      echo "  2) 改用 .NET for iOS 类库 + 平台 AOT（静态库并入 App，dlopen 改为静态链接，"
      echo "     需要同步调整 MuMain 的 Connection 加载路径）；"
      echo "  3) 关注 .NET 官方 NativeAOT-iOS 进展后再回归本脚本。"
      exit 1
    }

SOURCE_LIBRARY="${PUBLISH_DIR}/MUnique.Client.Library.dylib"
if [[ ! -f "${SOURCE_LIBRARY}" ]]; then
  # 某些工具链版本输出 .so 命名；统一改名。
  if [[ -f "${PUBLISH_DIR}/MUnique.Client.Library.so" ]]; then
    SOURCE_LIBRARY="${PUBLISH_DIR}/MUnique.Client.Library.so"
  else
    echo "未在 ${PUBLISH_DIR} 找到协议库产物（.dylib/.so）。" >&2
    exit 1
  fi
fi

cp -- "${SOURCE_LIBRARY}" "${FINAL_DIR}/${FINAL_LIBRARY_NAME}"

FINAL_LIBRARY="${FINAL_DIR}/${FINAL_LIBRARY_NAME}"
if command -v otool >/dev/null 2>&1; then
  otool -l "${FINAL_LIBRARY}" | grep -A2 LC_ID_DYLIB || true
fi

echo
echo "iOS arm64 ClientLibrary staged into:"
echo "  ${FINAL_LIBRARY}"
echo "打包时把该目录内容放入 OpenMU-Game.app/Frameworks/（Xcode 目标的"
echo "Embed Frameworks 阶段，或 CMake 资源拷贝），并确保引擎按"
echo "@rpath/${FINAL_LIBRARY_NAME} dlopen。"
