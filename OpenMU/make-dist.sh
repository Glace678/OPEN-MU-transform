#!/usr/bin/env bash
#
# Builds a self-contained private-server distribution archive:
#   openmu-server-<version>.tar.gz
#
# The archive lets anyone self-host + invite players. On the server they run:
#   tar -xzf openmu-server-<version>.tar.gz
#   cd openmu-server
#   sudo bash install.sh --public-address <this-server-ipv4>
#
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
VERSION="$(date -u +%Y%m%d)"
OUT_DIR="$SCRIPT_DIR/dist"
STAGE="$OUT_DIR/openmu-server"

log() { echo "[dist] $*"; }
fail() { echo "[dist] $*" >&2; exit 1; }

[[ -d "$SCRIPT_DIR/deploy/all-in-one" ]] || fail "deploy/all-in-one not found; run from the repository root."
[[ -d "$SCRIPT_DIR/src" ]] || fail "src/ not found at repository root; the local-source docker build context would be missing (84-01). Run make-dist.sh from a full checkout."
[[ -f "$SCRIPT_DIR/install.sh" ]] || fail "install.sh not found at repository root."
[[ -f "$SCRIPT_DIR/deploy/quick-start.sh" ]] || fail "deploy/quick-start.sh not found."

rm -rf -- "$STAGE"
mkdir -p "$STAGE/deploy"

log "Staging installer and deployment files..."
cp -- "$SCRIPT_DIR/install.sh" "$STAGE/install.sh"
chmod +x "$STAGE/install.sh"

cp -R -- "$SCRIPT_DIR/deploy/all-in-one" "$STAGE/deploy/all-in-one"
cp -- "$SCRIPT_DIR/deploy/quick-start.sh" "$STAGE/deploy/quick-start.sh"
chmod +x "$STAGE/deploy/quick-start.sh"

# DELIVERY-03: stage the source build context so the default local-source build
# works on the target server (docker-compose.local.yml builds from ../../src).
log "Staging source build context..."
cp -R -- "$SCRIPT_DIR/src" "$STAGE/src"
find "$STAGE/src" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true

# 84-01: fail the build if the staged archive would be missing key directories
# (previously the committed tar.gz shipped without src/ and one-click install broke).
[[ -d "$STAGE/src" ]] || fail "Staging failed: src/ missing from stage; refusing to produce a broken archive."
[[ -d "$STAGE/deploy/all-in-one" ]] || fail "Staging failed: deploy/all-in-one missing from stage."
[[ -f "$STAGE/install.sh" ]] || fail "Staging failed: install.sh missing from stage."
[[ -f "$STAGE/deploy/quick-start.sh" ]] || fail "Staging failed: deploy/quick-start.sh missing from stage."
log "Pre-flight archive contents OK (src/, deploy/, install.sh, quick-start.sh present)."

# Drop build/test-only noise and any local secrets from the distributed copy.
find "$STAGE" -type f \( -name '.env' -o -name '*.local' \) -delete 2>/dev/null || true
rm -rf -- "$STAGE/deploy/all-in-one/.git" 2>/dev/null || true

log "Writing distribution README..."
cat > "$STAGE/README.txt" <<'EOF'
OpenMU Private Server - Quick Start
====================================

1. Copy this folder onto a Linux server (Ubuntu/Debian/CentOS/Rocky recommended).
2. Run the one-command installer (it installs Docker if needed):

     sudo bash install.sh --public-address <SERVER_PUBLIC_IPV4>

   Optional player web portal:  --portal-domain mu.example.com
   Custom admin webpage port:   --web-port 8080

3. The stack builds and starts. Credentials are generated and stored in
   deploy/all-in-one/.env (chmod 600).

Connect:
  Game client: <SERVER_PUBLIC_IPV4>:44406 (TCP)
  Admin panel: http://127.0.0.1:80/   (loopback only; use an SSH tunnel)

Stop (database is preserved):
     bash deploy/quick-start.sh --down

Notes:
  - Open firewall/security-group TCP ports: 443 (portal, optional),
    44405 (legacy) and 44406 (game).
  - The admin panel stays loopback-only for safety.
EOF

ARCHIVE="$OUT_DIR/openmu-server-$VERSION.tar.gz"
log "Creating archive $ARCHIVE ..."
tar -C "$OUT_DIR" -czf "$ARCHIVE" openmu-server

CHECKSUM="$ARCHIVE.sha256"
sha256sum "$ARCHIVE" > "$CHECKSUM"
log "Checksum written to $CHECKSUM"

log "Verifying the archive checksum..."
sha256sum -c "$CHECKSUM" >/dev/null || fail "Checksum verification failed for $ARCHIVE"

log "Done: $ARCHIVE"
echo
echo "Upload/extract on the server, then run:"
echo "  sudo bash install.sh --public-address <SERVER_PUBLIC_IPV4>"
