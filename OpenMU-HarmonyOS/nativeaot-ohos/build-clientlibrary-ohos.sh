#!/usr/bin/env bash
# Builds the .NET protocol library (MuMain/ClientLibrary) as a native
# libMUnique.Client.Library.so for OpenHarmony / HarmonyOS (arm64), analogous to
# nativeaot/build-clientlibrary-android.sh.
#
# .NET NativeAOT compiles managed code to a native shared library. Android uses
# the linux-bionic-arm64 RID. HarmonyOS is musl-based; when the .NET toolchain in
# use ships an OpenHarmony runtime pack use RID linux-ohos-arm64, otherwise use
# linux-bionic-arm64 with the OHOS NDK sysroot as the crossrootfs (the symbols
# overlap closely enough for the networking code the client uses).
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
HARMONY_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd -P)"
PROJECT_FILE="$(cd -- "${SCRIPT_DIR}/../../MuMain/ClientLibrary" && pwd -P)/MUnique.Client.Library.csproj"
BUILD_ROOT="${SCRIPT_DIR}/build"
PUBLISH_DIR="${BUILD_ROOT}/publish"
# Stage the arm64 library into both the phone project and the arm64 slot of the
# PC project (most HarmonyOS PCs are arm64/Kunpeng). x86_64 HarmonyOS PCs need a
# separate linux-ohos-x64 build and must be dropped in harmony-pc's prebuilt/x86_64/.
FINAL_DIRS=(
  "${HARMONY_ROOT}/harmony-game/entry/src/main/cpp/prebuilt/arm64-v8a"
  "${HARMONY_ROOT}/harmony-pc/entry/src/main/cpp/prebuilt/arm64-v8a"
)
FINAL_LIBRARY_NAME="libMUnique.Client.Library.so"

# Choose the RID. Prefer the OpenHarmony runtime pack; fall back to bionic.
RID="${OPENMU_OHOS_RID:-linux-ohos-arm64}"

# The HarmonyOS NDK (command-line tools, or DevEco's native SDK) provides the
# clang cross toolchain and the OHOS sysroot. Set OHOS_NDK_HOME to its root.
OHOS_NDK_HOME="${OHOS_NDK_HOME:-/opt/ohos-sdk/native}"
OHOS_LLVM_BIN="${OHOS_NDK_HOME}/llvm/bin"

mkdir -p -- "${PUBLISH_DIR}" "${FINAL_DIRS[@]}"

# When targeting bionic-compatible AOT against the OHOS sysroot, point the
# linker/clang at the OHOS NDK. The NativeAOT publish uses these env vars.
export PATH="${OHOS_LLVM_BIN}:${PATH}"
# crossrootfs dir (sysroot) used by the runtime pack to resolve libc symbols:
export CROSSROOTFS="${CROSSROOTFS:-${OHOS_NDK_HOME}/sysroot}"

dotnet publish "${PROJECT_FILE}" \
    --configuration Release \
    --runtime "${RID}" \
    --output "${PUBLISH_DIR}" \
    -p:BaseIntermediateOutputPath="${BUILD_ROOT}/obj/" \
    -p:BaseOutputPath="${BUILD_ROOT}/bin/" \
    -p:EnableDefaultCompileItems=false \
    -p:CustomBeforeMicrosoftCommonProps="${SCRIPT_DIR}/ClientLibrary.OhosAot.props" \
    -p:DisableUnsupportedError=true \
    -p:PublishAotUsingRuntimePack=true \
    -p:NativeLib=Shared \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:StripSymbols=false \
    -p:ci=true \
    2>&1 | tee "${SCRIPT_DIR}/publish.log" || {
      echo
      echo "Publish for ${RID} failed. If linux-ohos-arm64 is not available in"
      echo "this .NET preview, retry with: OPENMU_OHOS_RID=linux-bionic-arm64 and"
      echo "ensure gl4es/SDL were also built for OpenHarmony."
      exit 1
    }

SOURCE_LIBRARY="${PUBLISH_DIR}/MUnique.Client.Library.so"
[[ -f "${SOURCE_LIBRARY}" ]]

for dest_dir in "${FINAL_DIRS[@]}"; do
  cp -- "${SOURCE_LIBRARY}" "${dest_dir}/${FINAL_LIBRARY_NAME}"
done

FINAL_LIBRARY="${FINAL_DIRS[0]}/${FINAL_LIBRARY_NAME}"
if command -v llvm-readelf >/dev/null 2>&1; then
  llvm-readelf --file-header "${FINAL_LIBRARY}" | grep -E 'Class|Machine'
  llvm-readelf --dynamic "${FINAL_LIBRARY}" | grep -E 'SONAME|NEEDED'
fi

echo
echo "OpenHarmony arm64 ClientLibrary staged into:"
for dest_dir in "${FINAL_DIRS[@]}"; do
  echo "  ${dest_dir}/${FINAL_LIBRARY_NAME}"
done
echo "Also place an OHOS-built libGL.so (gl4es) and libSDL3.so into the same"
echo "prebuilt/arm64-v8a folders, or let MuMain's CMake build them from source."
echo "For x86_64 HarmonyOS PCs, build linux-ohos-x64 and drop the library into"
echo "harmony-pc/.../prebuilt/x86_64/."
