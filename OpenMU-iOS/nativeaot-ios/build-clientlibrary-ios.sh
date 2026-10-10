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
# I-04: only ios-arm64 is a supported iOS NativeAOT target here. The final copy
# and packaging assume an arm64 slice, so refuse any other RID instead of silently
# staging it under a fixed arm64 file name.
if [[ "${RID}" != "ios-arm64" ]]; then
  echo "error: OPENMU_IOS_RID='${RID}' is not supported; expected 'ios-arm64'." >&2
  echo "An arm64 host slice is required for OpenMU-Game.app/Frameworks (I-04)." >&2
  exit 2
fi

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

# 108-05: this script produces a Mach-O dylib for iOS. Fail closed if the host
# lacks lipo/file (Xcode command line tools) rather than silently skipping the
# architecture/format check. Also refuse to stage a bare .so (ELF) just by
# renaming it to .dylib -- that produces a binary that only fails at load time
# with a confusing error far from the root cause.
if ! command -v lipo >/dev/null 2>&1; then
  echo "error: 'lipo' not found. Install Xcode command line tools " >&2
  echo "  (xcode-select --install) before building the iOS ClientLibrary." >&2
  exit 1
fi
if ! command -v file >/dev/null 2>&1; then
  echo "error: 'file' utility not found; cannot validate Mach-O format (108-05)." >&2
  exit 1
fi

SOURCE_LIBRARY="${PUBLISH_DIR}/MUnique.Client.Library.dylib"
if [[ ! -f "${SOURCE_LIBRARY}" ]]; then
  echo "error: expected Mach-O dylib not found: ${SOURCE_LIBRARY}" >&2
  echo "  (Some toolchains emit .so, but a .so is ELF and cannot be loaded by iOS;" >&2
  echo "   do not just rename it -- fix the publish RID/toolchain, 108-05.)" >&2
  exit 1
fi

# Verify the produced slice is arm64 Mach-O before staging it as the arm64 dylib.
ARCHES="$(lipo -info "${SOURCE_LIBRARY}" 2>&1)"
echo "  ${SOURCE_LIBRARY}: ${ARCHES}"
if [[ "${ARCHES}" != *arm64* ]]; then
  echo "error: produced library is not arm64 (${ARCHES}); refusing to stage (I-04/108-05)." >&2
  exit 1
fi
# Also confirm it is actually a Mach-O dynamic library, not some other format.
FILETYPE="$(file -b "${SOURCE_LIBRARY}")"
if [[ "${FILETYPE}" != *"Mach-O"* ]]; then
  echo "error: produced file is not a Mach-O binary (${FILETYPE}); refusing to stage (108-05)." >&2
  exit 1
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
