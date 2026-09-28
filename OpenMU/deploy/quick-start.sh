#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_DIR="$SCRIPT_DIR/all-in-one"
CONFIG_DIR="$COMPOSE_DIR"
PUBLISHED=0
NO_PULL=0
DOWN=0
LOGS=0
DRY_RUN=0
PUBLIC_ADDRESS=''
PORTAL_DOMAIN=''
WEB_PORT=''
EXPLICIT_BUILD=0

usage() {
  cat <<'EOF'
Usage: bash deploy/quick-start.sh [options]

  --public-address IP  Advertise this IPv4 and expose game ports for LAN/cloud clients.
  --portal-domain DNS  Optional public HTTPS player portal (443 only, admin stays private).
  --web-port PORT      Local admin webpage port (default 80).
  --config-dir DIR     Directory for persistent .env credentials.
  --dry-run            Print the plan; no Docker calls or file writes.
  --published-image    Explicitly use the upstream image WITHOUT local changes.
  --build             Compatibility option; local source is already the default.
  --no-pull           Skip pulling an explicitly requested upstream image.
  --down              Stop containers without deleting database volumes.
  --logs              Follow startup logs after starting.
EOF
}

fail() { echo "$*" >&2; exit 1; }
need_value() { [[ $# -ge 2 && -n "$2" ]] || fail "Missing value for $1"; }
valid_public_address() {
  [[ "$1" =~ ^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$ ]] || return 1
  local first="${BASH_REMATCH[1]}" octet
  for octet in "${BASH_REMATCH[@]:1}"; do
    (( 10#$octet <= 255 )) || return 1
  done
  (( 10#$first > 0 && 10#$first != 127 && 10#$first < 224 ))
}
valid_portal_domain() {
  local label domain="${1,,}"
  set -- "$domain"
  [[ ${#1} -le 253 && "$1" == *.* && "$1" =~ \.[a-zA-Z][a-zA-Z0-9-]*$ ]] || return 1
  [[ "$1" != *.localhost && "$1" != *.local && "$1" != *.internal ]] || return 1
  local IFS='.'
  for label in $1; do
    [[ "$label" =~ ^[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$ ]] || return 1
  done
  [[ "$1" != .* && "$1" != *. && "$1" != *..* ]]
}
while (( $# )); do
  case "$1" in
    --public-address) need_value "$@"; PUBLIC_ADDRESS="$2"; shift ;;
    --portal-domain) need_value "$@"; PORTAL_DOMAIN="${2,,}"; shift ;;
    --web-port) need_value "$@"; WEB_PORT="$2"; shift ;;
    --config-dir) need_value "$@"; CONFIG_DIR="$2"; shift ;;
    --published-image) PUBLISHED=1 ;;
    --build) EXPLICIT_BUILD=1 ;;
    --no-pull) NO_PULL=1 ;;
    --down) DOWN=1 ;;
    --logs) LOGS=1 ;;
    --dry-run) DRY_RUN=1 ;;
    -h|--help) usage; exit 0 ;;
    *) fail "Unknown option: $1" ;;
  esac
  shift
done
(( ! PUBLISHED || ! EXPLICIT_BUILD )) || fail '--build and --published-image cannot be combined.'
[[ -d "$COMPOSE_DIR" ]] || fail "Compose directory not found: $COMPOSE_DIR"
if [[ -n "$PUBLIC_ADDRESS" ]]; then
  valid_public_address "$PUBLIC_ADDRESS" || fail 'public-address must be a reachable IPv4, not loopback/unspecified/multicast.'
fi
if [[ -n "$PORTAL_DOMAIN" ]]; then valid_portal_domain "$PORTAL_DOMAIN" || fail 'portal-domain must be a public DNS hostname, not a URL, IP, port, or local hostname.'; fi
if [[ -n "$WEB_PORT" ]]; then
  [[ "$WEB_PORT" =~ ^[0-9]{1,5}$ ]] && (( 10#$WEB_PORT >= 1 && 10#$WEB_PORT <= 65535 )) || fail 'web-port must be 1-65535.'
fi
if [[ "$CONFIG_DIR" != /* ]]; then CONFIG_DIR="$PWD/$CONFIG_DIR"; fi
ENV_FILE="$CONFIG_DIR/.env"

# Parse only assignments; never execute a credentials file as shell code.
keys=()
values=()
env_lines=()
get_value() {
  local index
  for (( index=0; index<${#keys[@]}; index++ )); do
    if [[ "${keys[index]}" == "$1" ]]; then printf '%s' "${values[index]}"; return; fi
  done
}
set_value() {
  local index
  for (( index=0; index<${#keys[@]}; index++ )); do
    if [[ "${keys[index]}" == "$1" ]]; then values[index]="$2"; return; fi
  done
  keys+=("$1")
  values+=("$2")
}
env_exists=0
if [[ -f "$ENV_FILE" ]]; then
  env_exists=1
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    env_lines+=("$line")
    if [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
      key="${BASH_REMATCH[1]}"; value="${BASH_REMATCH[2]}"
      for known_key in "${keys[@]}"; do [[ "$known_key" != "$key" ]] || fail "Duplicate key $key in $ENV_FILE"; done
      set_value "$key" "$value"
    elif [[ -n "$line" && ! "$line" =~ ^[[:space:]]*# ]]; then
      fail "Unsupported assignment in $ENV_FILE; use KEY=value without quotes or spaces."
    fi
  done < "$ENV_FILE"
fi

if (( DOWN && ! env_exists )); then
  echo 'No deployment credentials file exists. Nothing was stopped or deleted.'
  exit 0
fi

if (( ! DOWN )); then
  if (( env_exists )); then
    for key in OPENMU_ADMIN_USER OPENMU_ADMIN_PASSWORD DB_ADMIN_USER DB_ADMIN_PW; do
      [[ -n "$(get_value "$key")" ]] || fail "Missing $key in $ENV_FILE; existing credentials are never regenerated."
    done
    [[ -z "$(get_value DB_NAME)" || "$(get_value DB_NAME)" == openmu ]] || fail 'OpenMU uses the database name openmu; DB_NAME cannot select another database.'
    [[ "$(get_value DB_ADMIN_USER)" =~ ^[A-Za-z_][A-Za-z0-9_]{0,62}$ ]] || fail 'Invalid DB_ADMIN_USER.'
    [[ "$(get_value OPENMU_ADMIN_USER)" =~ ^[A-Za-z0-9_.-]{3,32}$ ]] || fail 'Invalid OPENMU_ADMIN_USER.'
    for key in OPENMU_ADMIN_PASSWORD DB_ADMIN_PW; do
      [[ "$(get_value "$key")" =~ ^[A-Za-z0-9_.-]{12,128}$ ]] || fail "$key must have 12-128 URL-safe characters; credentials are not changed automatically."
    done
  else
    set_value OPENMU_ADMIN_USER admin
    set_value OPENMU_ADMIN_PASSWORD '<generated-at-start>'
    set_value DB_ADMIN_USER postgres
    set_value DB_ADMIN_PW '<generated-at-start>'
    set_value DB_NAME openmu
  fi
  [[ -n "$(get_value RESOLVE_IP)" ]] || set_value RESOLVE_IP 127.0.0.1
  [[ -n "$(get_value OPENMU_GAME_BIND)" ]] || set_value OPENMU_GAME_BIND 127.0.0.1
  [[ -n "$(get_value OPENMU_WEB_PORT)" ]] || set_value OPENMU_WEB_PORT 80
  if [[ -n "$PUBLIC_ADDRESS" ]]; then
    set_value RESOLVE_IP "$PUBLIC_ADDRESS"
    set_value OPENMU_GAME_BIND 0.0.0.0
  fi
  if [[ -n "$WEB_PORT" ]]; then set_value OPENMU_WEB_PORT "$WEB_PORT"; fi
  if [[ -n "$PORTAL_DOMAIN" ]]; then set_value OPENMU_PORTAL_DOMAIN "$PORTAL_DOMAIN"; fi
  game_bind="$(get_value OPENMU_GAME_BIND)"
  address="$(get_value RESOLVE_IP)"
  port="$(get_value OPENMU_WEB_PORT)"
  [[ "$game_bind" == 127.0.0.1 || "$game_bind" == 0.0.0.0 ]] || fail 'OPENMU_GAME_BIND must be 127.0.0.1 or 0.0.0.0.'
  [[ "$port" =~ ^[0-9]{1,5}$ ]] && (( 10#$port >= 1 && 10#$port <= 65535 )) || fail 'OPENMU_WEB_PORT must be 1-65535.'
  if [[ "$address" != 127.0.0.1 ]]; then
    valid_public_address "$address" || fail 'RESOLVE_IP must be a reachable explicit IPv4.'
  elif [[ "$game_bind" == 0.0.0.0 ]]; then
    fail 'Public game ports require an explicit reachable RESOLVE_IP.'
  fi
fi

if [[ -n "$(get_value OPENMU_PORTAL_DOMAIN)" ]]; then
  valid_portal_domain "$(get_value OPENMU_PORTAL_DOMAIN)" || fail 'Invalid saved OPENMU_PORTAL_DOMAIN.'
  if (( ! DOWN )); then
    (( ! PUBLISHED )) && [[ "$(get_value RESOLVE_IP)" != 127.0.0.1 ]] || fail 'The public player portal requires local source and an explicit reachable public-address.'
  fi
fi

compose=(docker compose --project-directory "$COMPOSE_DIR" --project-name all-in-one)
if (( env_exists || ! DOWN )); then compose+=(--env-file "$ENV_FILE"); fi
compose+=(-f "$COMPOSE_DIR/docker-compose.yml")
if (( ! PUBLISHED )); then compose+=(-f "$COMPOSE_DIR/docker-compose.local.yml"); fi
if [[ -n "$(get_value OPENMU_PORTAL_DOMAIN)" ]]; then compose+=(-f "$COMPOSE_DIR/docker-compose.portal.yml"); fi
run_compose() {
  if (( DRY_RUN )); then
    printf 'Command:'
    printf ' %q' "${compose[@]}" "$@"
    printf '\n'
  else
    (
      unset DB_ADMIN_USER DB_ADMIN_PW OPENMU_ADMIN_USER OPENMU_ADMIN_PASSWORD \
        OPENMU_ADMIN_TOTP_SECRET RESOLVE_IP OPENMU_GAME_BIND OPENMU_WEB_PORT POSTGRES_IMAGE OPENMU_PORTAL_DOMAIN
      "${compose[@]}" "$@"
    )
  fi
}
if (( DRY_RUN )); then
  if (( PUBLISHED )); then echo 'ImageMode: published-upstream'; else echo 'ImageMode: local-source'; fi
  echo "EnvFile: $ENV_FILE"
  echo "AdvertisedAddress: $(get_value RESOLVE_IP)"
  echo "GameBind: $(get_value OPENMU_GAME_BIND)"
  echo 'AdminBind: 127.0.0.1'
  echo 'DatabaseName: openmu'
  if [[ -n "$(get_value OPENMU_PORTAL_DOMAIN)" ]]; then echo "PlayerPortal: https://$(get_value OPENMU_PORTAL_DOMAIN)"; fi
else
  command -v docker >/dev/null 2>&1 || fail 'Docker with the Compose plugin is required.'
  docker compose version >/dev/null
  if (( ! DOWN )); then
    if (( ! env_exists )); then
      new_secret() {
        if command -v openssl >/dev/null 2>&1; then
          openssl rand -base64 24 | tr -d '\r\n=' | tr '+/' '-_'
        else
          od -An -N24 -tx1 /dev/urandom | tr -d ' \n'
        fi
      }
      set_value OPENMU_ADMIN_PASSWORD "$(new_secret)"
      set_value DB_ADMIN_PW "$(new_secret)"
    fi
    umask 077
    mkdir -p "$CONFIG_DIR"
    temp_file="$(mktemp "$CONFIG_DIR/.env.XXXXXX")"
    trap '[[ -z "${temp_file:-}" ]] || rm -f -- "$temp_file"' EXIT
    for line in "${env_lines[@]}"; do
      if [[ ! "$line" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]]; then printf '%s\n' "$line" >> "$temp_file"; fi
    done
    for (( index=0; index<${#keys[@]}; index++ )); do
      printf '%s=%s\n' "${keys[index]}" "${values[index]}" >> "$temp_file"
    done
    if (( env_exists )) && cmp -s "$ENV_FILE" "$temp_file"; then
      rm -f -- "$temp_file"
    else
      mv -- "$temp_file" "$ENV_FILE"
    fi
    chmod 600 "$ENV_FILE"
    temp_file=''
  fi
fi
if (( DOWN )); then
  run_compose down
  if (( ! DRY_RUN )); then echo 'OpenMU containers stopped. Database volumes were preserved.'; fi
  exit 0
fi
run_compose config --quiet
if (( PUBLISHED )); then
  if (( ! NO_PULL )); then run_compose pull openmu-startup; fi
else
  run_compose build openmu-startup
fi
run_compose up -d --no-build
if (( ! DRY_RUN )); then
  echo "OpenMU startup requested. Admin panel: http://127.0.0.1:$(get_value OPENMU_WEB_PORT)/"
  echo "MuMain connection: $(get_value RESOLVE_IP):44406 (TCP, fresh Season 6 open-source configuration)"
  echo 'The legacy GMO client uses 44405. Existing database endpoint settings are kept.'
  echo "Credentials kept locally in $ENV_FILE"
  echo 'The admin panel is loopback-only. Use an SSH tunnel for remote administration.'
  if [[ -n "$(get_value OPENMU_PORTAL_DOMAIN)" ]]; then echo "Player accounts: https://$(get_value OPENMU_PORTAL_DOMAIN)/register (443 only; admin stays private)."; fi
  if (( PUBLISHED )); then echo 'WARNING: The upstream image does not include local changes.'; fi
fi
if (( LOGS )); then run_compose logs -f openmu-startup; fi
