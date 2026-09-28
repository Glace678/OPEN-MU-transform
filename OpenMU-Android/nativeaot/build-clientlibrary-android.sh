#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT_FILE="$(cd -- "${SCRIPT_DIR}/../../MuMain/ClientLibrary" && pwd -P)/MUnique.Client.Library.csproj"
BUILD_ROOT="${SCRIPT_DIR}/build"
PUBLISH_DIR="${BUILD_ROOT}/publish"
INTERMEDIATE_DIR="${BUILD_ROOT}/obj/"
BINARY_DIR="${BUILD_ROOT}/bin/"
FINAL_DIR="$(cd -- "${SCRIPT_DIR}/.." && pwd -P)/native/arm64-v8a"
FINAL_LIBRARY="${FINAL_DIR}/libMUnique.Client.Library.so"

NDK_REVISION="r27c"
NDK_ARCHIVE_NAME="android-ndk-${NDK_REVISION}-linux.zip"
NDK_URL="https://dl.google.com/android/repository/${NDK_ARCHIVE_NAME}"
NDK_SHA1="090e8083a715fdb1a3e402d0763c388abb03fb4e"
CACHE_BASE="${XDG_CACHE_HOME:-${HOME}/.cache}/android-ndk"
NDK_ARCHIVE="${CACHE_BASE}/${NDK_ARCHIVE_NAME}"
NDK_ROOT="${ANDROID_NDK_ROOT:-${CACHE_BASE}/android-ndk-${NDK_REVISION}}"
NDK_BIN="${NDK_ROOT}/toolchains/llvm/prebuilt/linux-x86_64/bin"

download_ndk() {
    mkdir -p -- "${CACHE_BASE}"
    if [[ ! -f "${NDK_ARCHIVE}" ]]; then
        curl --fail --location --retry 5 --retry-delay 2 \
            --output "${NDK_ARCHIVE}" "${NDK_URL}"
    fi

    printf '%s  %s\n' "${NDK_SHA1}" "${NDK_ARCHIVE}" | sha1sum --check --status
}

find_unzip() {
    if command -v unzip >/dev/null 2>&1; then
        command -v unzip
        return
    fi

    local local_unzip="${CACHE_BASE}/tools/root/usr/bin/unzip"
    if [[ ! -x "${local_unzip}" ]]; then
        mkdir -p -- "${CACHE_BASE}/tools"
        (
            cd -- "${CACHE_BASE}/tools"
            apt-get download unzip
            local package
            package="$(find . -maxdepth 1 -type f -name 'unzip_*.deb' -print -quit)"
            [[ -n "${package}" ]]
            dpkg-deb --extract "${package}" root
        )
    fi

    printf '%s\n' "${local_unzip}"
}

prepare_ndk() {
    if [[ -x "${NDK_BIN}/clang" ]]; then
        return
    fi

    download_ndk
    local unzip_command
    unzip_command="$(find_unzip)"
    "${unzip_command}" -q -o "${NDK_ARCHIVE}" -d "${CACHE_BASE}"
    [[ -x "${NDK_BIN}/clang" ]]
}

prepare_ndk
mkdir -p -- "${PUBLISH_DIR}" "${INTERMEDIATE_DIR}" "${BINARY_DIR}" "${FINAL_DIR}"

export PATH="${NDK_BIN}:/usr/bin:/bin"

dotnet publish "${PROJECT_FILE}" \
    --configuration Release \
    --runtime linux-bionic-arm64 \
    --output "${PUBLISH_DIR}" \
    -p:BaseIntermediateOutputPath="${INTERMEDIATE_DIR}" \
    -p:BaseOutputPath="${BINARY_DIR}" \
    -p:EnableDefaultCompileItems=false \
    -p:CustomBeforeMicrosoftCommonProps="${SCRIPT_DIR}/ClientLibrary.AndroidAot.props" \
    -p:DisableUnsupportedError=true \
    -p:PublishAotUsingRuntimePack=true \
    -p:NativeLib=Shared \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:StripSymbols=false \
    -p:ci=true \
    2>&1 | tee "${SCRIPT_DIR}/publish.log"

SOURCE_LIBRARY="${PUBLISH_DIR}/MUnique.Client.Library.so"
[[ -f "${SOURCE_LIBRARY}" ]]
cp -- "${SOURCE_LIBRARY}" "${FINAL_LIBRARY}"

EXPECTED_EXPORTS="${BUILD_ROOT}/expected-exports.txt"
ACTUAL_EXPORTS="${BUILD_ROOT}/actual-exports.txt"
MISSING_EXPORTS="${BUILD_ROOT}/missing-exports.txt"
grep -h -oE 'EntryPoint[[:space:]]*=[[:space:]]*"[^"]+"' \
    "$(dirname -- "${PROJECT_FILE}")"/*.cs \
    | sed -E 's/.*"([^"]+)"/\1/' \
    | sort -u > "${EXPECTED_EXPORTS}"
"${NDK_BIN}/llvm-nm" --dynamic --defined-only "${FINAL_LIBRARY}" \
    | awk '{print $NF}' \
    | sed -E 's/@.*//' \
    | sort -u > "${ACTUAL_EXPORTS}"
comm -23 "${EXPECTED_EXPORTS}" "${ACTUAL_EXPORTS}" > "${MISSING_EXPORTS}"

{
    printf 'file:\n'
    file "${FINAL_LIBRARY}"
    printf '\nelf-header:\n'
    "${NDK_BIN}/llvm-readelf" --file-header "${FINAL_LIBRARY}"
    printf '\ndynamic-dependencies:\n'
    "${NDK_BIN}/llvm-readelf" --dynamic "${FINAL_LIBRARY}" | grep -E 'SONAME|NEEDED'
    printf '\nrequired-exports:\n'
    "${NDK_BIN}/llvm-readelf" --dyn-syms --wide "${FINAL_LIBRARY}" \
        | grep -E 'ConnectionManager_(Connect|Send|BeginReceive|Disconnect)(@@V1\.0)?$'
    printf '\nmanaged-entrypoint-audit:\n'
    printf 'expected=%s actual-dynamic=%s missing=%s\n' \
        "$(wc -l < "${EXPECTED_EXPORTS}")" \
        "$(wc -l < "${ACTUAL_EXPORTS}")" \
        "$(wc -l < "${MISSING_EXPORTS}")"
    printf '\nsha256:\n'
    sha256sum "${FINAL_LIBRARY}"
} | tee "${SCRIPT_DIR}/verify.txt"

file "${FINAL_LIBRARY}" | grep 'ELF 64-bit LSB shared object, ARM aarch64' >/dev/null
"${NDK_BIN}/llvm-readelf" --file-header "${FINAL_LIBRARY}" | grep 'Machine:.*AArch64' >/dev/null
"${NDK_BIN}/llvm-readelf" --dynamic "${FINAL_LIBRARY}" \
    | grep 'SONAME.*libMUnique.Client.Library.so' >/dev/null
"${NDK_BIN}/llvm-readelf" --dyn-syms --wide "${FINAL_LIBRARY}" \
    | grep -E 'ConnectionManager_Connect(@@V1\.0)?$' >/dev/null
[[ ! -s "${MISSING_EXPORTS}" ]]
# Relative filename in the checksum line: sha256sum writes the input path, and
# FINAL_LIBRARY is the build machine's absolute WSL path (/mnt/d/openmu自用/...),
# which leaks a developer's local layout into an artifact that gets handed
# around and breaks `sha256sum -c` anywhere but that exact machine. Running from
# the library's own directory keeps the standard two-field format so the sidecar
# stays verifiable.
( cd "$(dirname "${FINAL_LIBRARY}")" \
    && sha256sum "$(basename "${FINAL_LIBRARY}")" > "$(basename "${FINAL_LIBRARY}").sha256" )

printf '\nAndroid ARM64 ClientLibrary ready: %s\n' "${FINAL_LIBRARY}"
