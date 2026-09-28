#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
QUICK_START="$SCRIPT_DIR/../quick-start.sh"
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/openmu-deploy-test.XXXXXX")"
export OPENMU_TEST_DOCKER_LOG="$TEST_ROOT/docker-calls"
export OPENMU_TEST_FAILURE_VERB=''
checks=0
cleanup() {
  case "$TEST_ROOT" in
    "${TMPDIR:-/tmp}"/openmu-deploy-test.*) rm -rf -- "$TEST_ROOT" ;;
    *) echo "Refusing to clean unexpected test path: $TEST_ROOT" >&2 ;;
  esac
}
trap cleanup EXIT

docker() {
  printf '%s\n' "$*" >> "$OPENMU_TEST_DOCKER_LOG"
  if [[ "$*" != 'compose version' && -n "${DB_ADMIN_PW:-}" ]]; then
    echo 'Inherited credentials leaked into Compose.' >&2
    return 1
  fi
  if [[ -n "$OPENMU_TEST_FAILURE_VERB" && " $* " == *" $OPENMU_TEST_FAILURE_VERB "* ]]; then return 42; fi
  return 0
}
export -f docker

assert_contains() {
  [[ "$1" == *"$2"* ]] || { echo "Missing expected output: $2" >&2; exit 1; }
  checks=$((checks + 1))
}
assert_absent() {
  [[ "$1" != *"$2"* ]] || { echo "Unexpected output: $2" >&2; exit 1; }
  checks=$((checks + 1))
}
assert_rejected() {
  if bash "$QUICK_START" "$@" > "$TEST_ROOT/rejection" 2>&1; then
    echo "Expected rejection: $*" >&2
    exit 1
  fi
  checks=$((checks + 1))
}

empty_config="$TEST_ROOT/not-created"
plan="$(bash "$QUICK_START" --dry-run --config-dir "$empty_config")"
assert_contains "$plan" 'ImageMode: local-source'
assert_contains "$plan" 'AdvertisedAddress: 127.0.0.1'
assert_contains "$plan" 'GameBind: 127.0.0.1'
assert_contains "$plan" 'AdminBind: 127.0.0.1'
plan="$(bash "$QUICK_START" --dry-run --config-dir "$empty_config" --public-address 203.0.113.25 --portal-domain accounts.example.com)"
assert_contains "$plan" 'PlayerPortal: https://accounts.example.com'
assert_contains "$plan" 'docker-compose.portal.yml'
assert_contains "$plan" 'AdminBind: 127.0.0.1'
assert_rejected --dry-run --config-dir "$empty_config" --portal-domain accounts.example.com
assert_rejected --dry-run --config-dir "$empty_config" --public-address 203.0.113.25 --portal-domain accounts.example.com --published-image
for invalid in localhost 127.0.0.1 https://accounts.example.com accounts.example.com:443 a..com a.local a.com/path; do
  assert_rejected --dry-run --config-dir "$empty_config" --portal-domain "$invalid"
done
assert_contains "$plan" 'docker-compose.local.yml'
assert_contains "$plan" ' build openmu-startup'
assert_contains "$plan" ' up -d --no-build'
assert_absent "$plan" ' pull '
assert_absent "$plan" ' push '
assert_absent "$plan" 'docker-compose.override.yml'
[[ ! -d "$empty_config" && ! -f "$OPENMU_TEST_DOCKER_LOG" ]] || { echo 'DryRun had side effects.' >&2; exit 1; }
checks=$((checks + 1))

plan="$(bash "$QUICK_START" --dry-run --config-dir "$empty_config" --public-address 203.0.113.25 --web-port 8088)"
assert_contains "$plan" 'AdvertisedAddress: 203.0.113.25'
assert_contains "$plan" 'GameBind: 0.0.0.0'
assert_contains "$plan" 'AdminBind: 127.0.0.1'
for invalid in 127.0.0.1 0.0.0.0 256.1.1.1 ::1 https://example.com 224.1.2.3; do
  assert_rejected --dry-run --config-dir "$empty_config" --public-address "$invalid"
done
assert_rejected --dry-run --config-dir "$empty_config" --published-image --build
assert_rejected --dry-run --config-dir "$empty_config" --web-port 65536
plan="$(bash "$QUICK_START" --dry-run --config-dir "$empty_config" --down)"
assert_absent "$plan" 'Command:'

plan="$(bash "$QUICK_START" --dry-run --config-dir "$empty_config" --published-image)"
assert_contains "$plan" 'ImageMode: published-upstream'
assert_contains "$plan" ' pull openmu-startup'
assert_absent "$plan" 'docker-compose.local.yml'

export DB_ADMIN_PW='inherited-not-the-saved-password'
config="$TEST_ROOT/mock-start"
bash "$QUICK_START" --config-dir "$config" --public-address 203.0.113.25 --portal-domain accounts.example.com > "$TEST_ROOT/first-output"
env_file="$config/.env"
cp -- "$env_file" "$TEST_ROOT/first.env"
grep -Eq '^OPENMU_ADMIN_PASSWORD=[A-Za-z0-9_-]{32,48}$' "$env_file"
grep -Eq '^DB_ADMIN_PW=[A-Za-z0-9_-]{32,48}$' "$env_file"
grep -q '^RESOLVE_IP=203.0.113.25$' "$env_file"
checks=$((checks + 3))
bash "$QUICK_START" --config-dir "$config" > "$TEST_ROOT/second-output"
if ! cmp -s "$env_file" "$TEST_ROOT/first.env"; then
  echo 'Repeated startup changed credentials.' >&2
  diff -u <(sed -E 's/^(OPENMU_ADMIN_PASSWORD|DB_ADMIN_PW)=.*/\1=<redacted>/' "$TEST_ROOT/first.env") \
    <(sed -E 's/^(OPENMU_ADMIN_PASSWORD|DB_ADMIN_PW)=.*/\1=<redacted>/' "$env_file") || true
  exit 1
fi
checks=$((checks + 1))
plan="$(bash "$QUICK_START" --dry-run --config-dir "$config")"
assert_contains "$plan" 'AdvertisedAddress: 203.0.113.25'
assert_contains "$plan" 'PlayerPortal: https://accounts.example.com'
plan="$(bash "$QUICK_START" --dry-run --config-dir "$config" --down)"
assert_contains "$plan" 'docker-compose.portal.yml'
assert_absent "$plan" 'OPENMU_ADMIN_PASSWORD='
assert_absent "$plan" 'inherited-not-the-saved-password'

export OPENMU_TEST_FAILURE_VERB=build
: > "$OPENMU_TEST_DOCKER_LOG"
assert_rejected --config-dir "$config"
calls="$(< "$OPENMU_TEST_DOCKER_LOG")"
assert_absent "$calls" ' up '
export OPENMU_TEST_FAILURE_VERB=''
printf 'DB_NAME=other\n' >> "$env_file"
assert_rejected --dry-run --config-dir "$config"
echo "Quick-start Bash: $checks checks passed (Docker mocked; no database access)."
