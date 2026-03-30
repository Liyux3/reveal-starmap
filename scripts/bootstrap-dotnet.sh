#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INSTALL_DIR="${DOTNET_INSTALL_DIR:-$ROOT_DIR/.tooling/dotnet}"
INSTALL_SCRIPT="$ROOT_DIR/artifacts/dotnet-install.sh"

mkdir -p "$ROOT_DIR/artifacts" "$INSTALL_DIR"

curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
bash "$INSTALL_SCRIPT" --channel 8.0 --install-dir "$INSTALL_DIR"

echo
echo "Local dotnet installed at:"
echo "  $INSTALL_DIR"
echo
echo "Temporary shell setup:"
echo "  export PATH=\"$INSTALL_DIR:\$PATH\""
