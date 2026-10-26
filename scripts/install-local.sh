#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MOD_ID="liyux.RevealStarMap"
DIST_DIR="$ROOT_DIR/dist/$MOD_ID"
LOCAL_MODS_DIR="${ONI_LOCAL_MODS:-$HOME/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local}"

DIST_DIR="$DIST_DIR" "$ROOT_DIR/scripts/build.sh"

mkdir -p "$LOCAL_MODS_DIR"
if [[ -e "$LOCAL_MODS_DIR/$MOD_ID" ]]; then
  if [[ -L "$LOCAL_MODS_DIR/$MOD_ID" ]] || ! grep -q "^staticID: $MOD_ID$" "$LOCAL_MODS_DIR/$MOD_ID/mod.yaml"; then
    echo "Refusing to replace an unrecognized local mod directory." >&2
    exit 1
  fi
  mkdir -p "$HOME/.Trash"
  RETIRED="$(mktemp -d "$HOME/.Trash/reveal-starmap-install.XXXXXX")"
  mv "$LOCAL_MODS_DIR/$MOD_ID" "$RETIRED/$MOD_ID"
fi
cp -R "$DIST_DIR" "$LOCAL_MODS_DIR/$MOD_ID"

echo "Installed mod into:"
echo "  $LOCAL_MODS_DIR/$MOD_ID"
