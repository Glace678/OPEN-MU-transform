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

rm -rf -- "$STAGE"
mkdir -p "$STAGE/deploy"

log "Staging installer and deployment files..."
cp -- "$SCRIPT_DIR/install.sh" "$STAGE/install.sh"
chmod +x "$STAGE/install.sh"

cp -R -- "$SCRIPT_DIR/deploy/all-in-one" "$STAGE/deploy/all-in-one"
cp -- "$SCRIPT_DIR/deploy/quick-start.sh" "$STAGE/deploy/quick-start.sh"
chmod +x "$STAGE/deploy/quick-start.sh"

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

log "Done: $ARCHIVE"
echo
echo "Upload/extract on the server, then run:"
echo "  sudo bash install.sh --public-address <SERVER_PUBLIC_IPV4>"
