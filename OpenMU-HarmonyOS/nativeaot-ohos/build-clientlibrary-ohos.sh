#!/usr/bin/env bash
set -euo pipefail

# Build MUnique.Client.Library for OpenHarmony (aarch64-linux-ohos) and copy the
# native library plus reference assemblies into harmony-game/entry/src/main/cpp/prebuilt.
# Run this from WSL or a Linux machine that already has the .NET 8 SDK and the
# android-ndk toolchain configured for NativeAOT (see OpenMU/docs).

# H-07: restore and publish are separate steps. Restore uses the project's pinned
# package sources / lock file; publish runs --no-restore so it cannot pull a new
# graph at publish time.
# NEW-HARMONY-01: the RID is mapped to the correct prebuilt ABI slot instead of
# always copying into arm64-v8a. An unknown RID is rejected, and the produced ELF
# architecture is verified with llvm-readelf before it is staged.

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
ROOT_DIR="$(cd -- "${SCRIPT_DIR}/../.." &>/dev/null && pwd)"
SRC_DIR="${ROOT_DIR}/MuMain/src/MUnique/Server/Interfaces/ClientLibrary"
GAME_ENTRY_DIR="${ROOT_DIR}/OpenMU-HarmonyOS/harmony-game/entry"
PC_ENTRY_DIR="${ROOT_DIR}/OpenMU-HarmonyOS/harmony-pc/entry"

# NEW-HARMONY-01: accept only RIDs we can place correctly, and map each to its
# prebuilt ABI slot. linux-bionic-* is accepted as an alias for linux-ohos-*.
case "${RID:-linux-ohos-arm64}" in
  linux-ohos-arm64|linux-bionic-arm64)
    TARGET_RID="linux-ohos-arm64"; TARGET_ABI="arm64-v8a"; EXPECTED_MACHINE="AArch64" ;;
  linux-ohos-x64|linux-bionic-x64)
    TARGET_RID="linux-ohos-x64";    TARGET_ABI="x86_64";    EXPECTED_MACHINE="X86-64" ;;
  *)
    echo "error: unsupported RID '${RID}'. Expected linux-ohos-arm64 or linux-ohos-x64." >&2
    exit 2 ;;
esac

CONFIG="${CONFIG:-Release}"
PUBLISH_DIR="${SCRIPT_DIR}/publish/${TARGET_RID}"

command -v dotnet >/dev/null || { echo "dotnet SDK is required" >&2; exit 1; }
command -v llvm-readelf >/dev/null || echo "warning: llvm-readelf not found; skipping ELF architecture check" >&2

echo "Restoring ${TARGET_RID} (${CONFIG}) ..."
# H-07: restore explicitly. If a packages.lock.json is checked in, the build can
# additionally be run with --locked-mode in CI; here we keep restore separate and
# deterministic, then publish without restoring.
dotnet restore "${SRC_DIR}/MUnique.Client.Library.csproj" \
  -r "${TARGET_RID}" \
  -c "${CONFIG}"

echo "Publishing ${TARGET_RID} (${CONFIG}) ..."
dotnet publish "${SRC_DIR}/MUnique.Client.Library.csproj" \
  -r "${TARGET_RID}" \
  -c "${CONFIG}" \
  --self-contained true \
  -o "${PUBLISH_DIR}" \
  --no-restore

SOURCES=(
  "${PUBLISH_DIR}/libMUnique.Client.Library.so"
)
REFERENCE="$(cd "${PUBLISH_DIR}" && pwd)/ref/MUnique.Client.Library.dll"
for SOURCE in "${SOURCES[@]}"; do
  test -f "${SOURCE}" || { echo "Missing published library: ${SOURCE}" >&2; exit 1; }
done
test -f "${REFERENCE}" || { echo "Missing reference assembly: ${REFERENCE}" >&2; exit 1; }

# NEW-HARMONY-01: verify the produced ELF machine matches the RID's ABI slot
# before staging it. This prevents an arm64 .so from being copied into the x86_64
# prebuilt slot (or vice versa).
if command -v llvm-readelf >/dev/null; then
  for SOURCE in "${SOURCES[@]}"; do
    MACHINE="$(llvm-readelf -h "${SOURCE}" | awk '/Machine:/{print $2}')"
    echo "  ${SOURCE}: ELF Machine=${MACHINE}"
    if [[ "${MACHINE}" != *"${EXPECTED_MACHINE}"* ]]; then
      echo "error: ${SOURCE} is ${MACHINE}, expected ${EXPECTED_MACHINE} for ${TARGET_RID}" >&2
      exit 1
    fi
  done
fi

# NEW-HARMONY-01: stage into the ABI slot that matches the RID (prebuilt/<ABI>/),
# not a hardcoded arm64-v8a. The phone game only ships arm64; the PC slot gets the
# matching arch.
GAME_LIB_DIR="${GAME_ENTRY_DIR}/src/main/cpp/prebuilt/${TARGET_ABI}"
PC_LIB_DIR="${PC_ENTRY_DIR}/src/main/cpp/prebuilt/${TARGET_ABI}"
mkdir -p "${GAME_LIB_DIR}" "${PC_LIB_DIR}"

for SOURCE in "${SOURCES[@]}"; do
  install -m 0644 "${SOURCE}" "${GAME_LIB_DIR}/"
  install -m 0644 "${SOURCE}" "${PC_LIB_DIR}/"
done
install -m 0644 "${REFERENCE}" "${PC_ENTRY_DIR}/src/main/cpp/"

echo "Copied ${TARGET_ABI} library to:"
echo "  ${GAME_LIB_DIR}"
echo "  ${PC_LIB_DIR}"