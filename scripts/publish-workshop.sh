#!/usr/bin/env bash
# Publish a released version to Workshop item 3695988693 and verify a subscriber install.
# Usage: scripts/publish-workshop.sh VERSION NOTE_FILE
# The Workshop ZIP must be the asset of GitHub release vVERSION, byte for byte.
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:?usage: publish-workshop.sh VERSION NOTE_FILE}"
NOTE="${2:?usage: publish-workshop.sh VERSION NOTE_FILE}"
ITEM=3695988693
OWNER=76561198853917603
ONI_APP_PATH="${ONI_APP_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app}"
SDK="$ONI_APP_PATH/Contents/PlugIns/steam_api.bundle/Contents/MacOS/libsteam_api.dylib"
ZIP_NAME="RevealStarMap-$VERSION-workshop.zip"
OUT="$ROOT_DIR/artifacts/workshop/$VERSION-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$OUT"

readback() {
  curl -fsS -X POST https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/ \
    -d itemcount=1 -d "publishedfileids[0]=$ITEM" > "$OUT/$1.json"
}
field() {
  python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["response"]["publishedfiledetails"][0].get(sys.argv[2], ""))' "$OUT/$1.json" "$2"
}

# GitHub is the canonical artifact; Workshop must carry the same bytes.
curl -fsSL -o "$OUT/$ZIP_NAME" "https://github.com/Liyux3/reveal-starmap/releases/download/v$VERSION/$ZIP_NAME"
cc -O2 -Wall -Wextra -o "$OUT/workshop-publish" "$ROOT_DIR/scripts/workshop-publish.c"
readback before
"$OUT/workshop-publish" "$SDK" publish "$OWNER" "$OUT/$ZIP_NAME" "$NOTE" "$OUT/publish.json"
readback after
SIZE="$(field after file_size)"
if [[ -z "$(field after filename)" || "$SIZE" != "$(stat -f %z "$OUT/$ZIP_NAME")" ]]; then
  echo "Readback is not the legacy ZIP; see $OUT/after.json" >&2
  exit 1
fi
"$OUT/workshop-publish" "$SDK" verify "$OWNER" "$OUT/$ZIP_NAME" "$(field after time_updated)" "$OUT/verify.json"
printf 'Published %s to %s and verified a subscriber install.\nReceipts: %s\n' "$VERSION" "$ITEM" "$OUT"
