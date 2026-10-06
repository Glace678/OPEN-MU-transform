#!/bin/bash
set -euo pipefail

# Resolve paths relative to this script, not the caller's working directory.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"

function generate_certificates {
  docker run --rm --entrypoint "" \
    -v "$SCRIPT_DIR/certificates:/certificates" \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    sh -c "dotnet dev-certs https --clean && dotnet dev-certs https -ep /certificates/aspnetapp.pfx -p ${1}"

  export CERTIFICATE_PASSWORD=${1}
  echo "success"
}
