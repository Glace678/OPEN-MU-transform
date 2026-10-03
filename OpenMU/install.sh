#!/usr/bin/env bash
#
# One-command cloud installer for OpenMU (all-in-one stack).
#
#   curl -fsSL https://<your-host>/install.sh | sudo bash -s -- --public-address 123.45.67.89
#
# It installs Docker + the Compose plugin when they are missing, then runs
# deploy/quick-start.sh which builds/starts the stack and persists credentials.
#
set -Eeuo pipefail

PUBLIC_ADDRESS=''
PORTAL_DOMAIN=''
WEB_PORT=''
NO_PULL=0
LOGS=0

usage() {
  cat <<'EOF'
Usage: install.sh [options]

  --public-address IP  REQUIRED for LAN/cloud clients. Advertised IPv4 of this server.
  --portal-domain DNS  Optional public HTTPS player portal (443; admin stays loopback).
  --web-port PORT      Admin webpage port (default 80).
  --no-pull            Do not pull images (use local/already-present images).
  --logs               Follow logs after the stack starts.
  -h, --help           Show this help.
EOF
}

fail() { echo "install.sh: $*" >&2; exit 1; }
need_value() { [[ $# -ge 2 && -n "${2:-}" ]] || fail "Missing value for $1"; }

while (( $# )); do
  case "$1" in
    --public-address) need_value "$@"; PUBLIC_ADDRESS="$2"; shift ;;
    --portal-domain)  need_value "$@"; PORTAL_DOMAIN="$2"; shift ;;
    --web-port)       need_value "$@"; WEB_PORT="$2"; shift ;;
    --no-pull)        NO_PULL=1 ;;
    --logs)           LOGS=1 ;;
    -h|--help)        usage; exit 0 ;;
    *) fail "Unknown option: $1" ;;
  esac
  shift
done

# Resolve the repository root (the directory containing this script).
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
QUICK_START="$SCRIPT_DIR/deploy/quick-start.sh"
[[ -f "$QUICK_START" ]] || fail "Cannot find deploy/quick-start.sh relative to $SCRIPT_DIR."

log() { echo "[install] $*"; }

have() { command -v "$1" >/dev/null 2>&1; }

ensure_docker() {
  if have docker && docker compose version >/dev/null 2>&1; then
    log "Docker with Compose plugin already installed."
    return
  fi

  log "Docker (with Compose plugin) not found. Installing..."
  if [[ ! -f /etc/os-release ]]; then
    fail "Automatic Docker install is only supported on distros with /etc/os-release. Please install Docker manually, then re-run."
  fi
  # shellcheck disable=SC1091
  . /etc/os-release
  case "${ID:-}" in
    ubuntu|debian)
      export DEBIAN_FRONTEND=noninteractive
      apt-get update -y
      apt-get install -y ca-certificates curl gnupg
      install -m 0755 -d /etc/apt/keyrings
      curl -fsSL "https://download.docker.com/linux/${ID}/gpg" \
        | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
      chmod a+r /etc/apt/keyrings/docker.gpg
      # shellcheck disable=SC1091
      . /etc/os-release
      echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/${ID} ${VERSION_CODENAME} stable" \
        > /etc/apt/sources.list.d/docker.list
      apt-get update -y
      apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
      ;;
    centos|rhel|fedora|rocky|almalinux)
      if have dnf; then PM=dnf; else PM=yum; fi
      $PM install -y yum-utils
      $PM config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo || true
      $PM install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
      systemctl enable --now docker || true
      ;;
    *)
      fail "Unsupported distro '${ID:-}'. Install Docker Engine + Compose plugin manually, then re-run."
      ;;
  esac

  docker compose version >/dev/null 2>&1 || fail "Docker installed but the Compose plugin is unavailable."
  log "Docker installed successfully."
}

ensure_docker

# Auto-detect the primary non-loopback IPv4 when none was provided and the user
# wants a quick LAN start. Public/cloud servers should pass --public-address.
if [[ -z "$PUBLIC_ADDRESS" ]]; then
  detected="$(ip -4 route get 1.1.1.1 2>/dev/null | awk '{for (i=1;i<=NF;i++) if ($i=="src") {print $(i+1); exit}}' || true)"
  if [[ -n "$detected" ]]; then
    PUBLIC_ADDRESS="$detected"
    log "No --public-address given; using detected primary IPv4: $PUBLIC_ADDRESS"
  else
    fail "Re-run with --public-address <server-ipv4> so remote clients can connect."
  fi
fi

args=(--public-address "$PUBLIC_ADDRESS")
[[ -z "$PORTAL_DOMAIN" ]] || args+=(--portal-domain "$PORTAL_DOMAIN")
[[ -z "$WEB_PORT" ]]       || args+=(--web-port "$WEB_PORT")
(( ! NO_PULL ))           || args+=(--no-pull)
(( ! LOGS ))              || args+=(--logs)

log "Starting OpenMU all-in-one stack advertised at $PUBLIC_ADDRESS ..."
exec bash "$QUICK_START" "${args[@]}"
