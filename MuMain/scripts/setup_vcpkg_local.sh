#!/usr/bin/env bash
# One-time local vcpkg bootstrap for the offline refactor workflow.
# Installs x86-windows OpenSSL + CURL used by PlatformCrypto and GameShop.
set -e

VCPKG_DIR="/d/vcpkg"

if [ ! -d "$VCPKG_DIR/.git" ]; then
  echo "[setup] cloning vcpkg (shallow)..."
  git clone --depth 1 https://github.com/microsoft/vcpkg.git "$VCPKG_DIR"
fi

cd "$VCPKG_DIR"
if [ ! -f "$VCPKG_DIR/vcpkg.exe" ]; then
  echo "[setup] bootstrapping vcpkg..."
  cmd //c "bootstrap-vcpkg.bat -disableMetrics"
fi

echo "[setup] installing openssl + curl for x86-windows..."
./vcpkg.exe install openssl curl --triplet x86-windows

echo "[setup] DONE"
