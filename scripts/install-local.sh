#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MOD_ID="liyux.RevealStarMap"
DIST_DIR="$ROOT_DIR/dist/$MOD_ID"
LOCAL_MODS_DIR="${ONI_LOCAL_MODS:-$HOME/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local}"

"$ROOT_DIR/scripts/build.sh"

mkdir -p "$LOCAL_MODS_DIR"
rm -rf "$LOCAL_MODS_DIR/$MOD_ID"
cp -R "$DIST_DIR" "$LOCAL_MODS_DIR/$MOD_ID"

echo "Installed mod into:"
echo "  $LOCAL_MODS_DIR/$MOD_ID"
