#!/usr/bin/env bash
# Publish a released version to Workshop item 3695988693 and verify a subscriber install.
# Usage: scripts/publish-workshop.sh VERSION NOTE_FILE
# The Workshop ZIP must be the asset of GitHub release vVERSION, byte for byte. Each version
# has one attempt folder; rerunning resumes it and never resubmits an unconfirmed commit.
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:?usage: publish-workshop.sh VERSION NOTE_FILE}"
NOTE="${2:?usage: publish-workshop.sh VERSION NOTE_FILE}"

# Publishing makes Steam update the item inside any running ONI, and verification downloads it
# as the game, so first take the lock local ONI projects share for native runs. The lock's file
# descriptor passes to this script when Python execs it, and closes when the script ends.
LOCK="$HOME/Library/Application Support/unity.Klei.Oxygen Not Included/mods/.native-validation.lock"
if [[ -z "${ONI_LOCK_FD:-}" ]]; then
  exec python3 -c '
import fcntl, json, os, sys
from datetime import datetime, timezone
lock = open(sys.argv[1], "a+")
try:
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
except BlockingIOError:
    lock.seek(0)
    print("Waiting for the shared ONI install: " + lock.read(), flush=True)
    fcntl.flock(lock, fcntl.LOCK_EX)
lock.seek(0); lock.truncate()
json.dump({"pid": os.getpid(), "project": sys.argv[2], "since": datetime.now(timezone.utc).isoformat()}, lock)
lock.flush()
os.set_inheritable(lock.fileno(), True)
os.environ["ONI_LOCK_FD"] = str(lock.fileno())
os.execvp("bash", ["bash"] + sys.argv[3:])
' "$LOCK" "$ROOT_DIR" "$0" "$@"
fi
ITEM=3695988693
OWNER=76561198853917603
ONI_APP_PATH="${ONI_APP_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app}"
SDK="$ONI_APP_PATH/Contents/PlugIns/steam_api.bundle/Contents/MacOS/libsteam_api.dylib"
ZIP_NAME="RevealStarMap-$VERSION-workshop.zip"
OUT="$ROOT_DIR/artifacts/workshop/$VERSION"
mkdir -p "$OUT"
if ! mkdir "$OUT/.active" 2>/dev/null; then
  echo "Another publication of $VERSION is running, or one was interrupted; inspect $OUT and remove $OUT/.active" >&2
  exit 1
fi
trap 'rmdir "$OUT/.active"; : > "$LOCK"' EXIT

readback() {
  curl -fsS -X POST https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/ \
    -d itemcount=1 -d "publishedfileids[0]=$ITEM" > "$OUT/$1.json"
}
field() {
  python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); d=d.get("response",{}).get("publishedfiledetails",[d])[0]; print(d.get(sys.argv[2], ""))' "$OUT/$1" "$2"
}
installed_release() {  # readback shows a legacy ZIP of exactly the release size
  [[ -n "$(field after.json filename)" && "$(field after.json file_size)" == "$(stat -f %z "$OUT/$ZIP_NAME")" ]]
}

# GitHub is the canonical artifact; Workshop must carry the same bytes.
curl -fsSL -o "$OUT/$ZIP_NAME.download" "https://github.com/Liyux3/reveal-starmap/releases/download/v$VERSION/$ZIP_NAME"
if [[ -f "$OUT/$ZIP_NAME" ]] && ! cmp -s "$OUT/$ZIP_NAME" "$OUT/$ZIP_NAME.download"; then
  echo "The release asset changed since this attempt began; inspect $OUT" >&2
  exit 1
fi
mv "$OUT/$ZIP_NAME.download" "$OUT/$ZIP_NAME"
cc -O2 -Wall -Wextra -o "$OUT/workshop-publish" "$ROOT_DIR/scripts/workshop-publish.c"

if [[ ! -f "$OUT/publish.json" ]]; then
  readback before
  "$OUT/workshop-publish" "$SDK" publish "$OWNER" "$OUT/$ZIP_NAME" "$NOTE" "$OUT/publish.json"
  readback after
elif [[ "$(field publish.json phase)" == completed && "$(field publish.json result)" == 1 &&
  "$(field publish.json item)" == "$ITEM" && "$(field publish.json legalAgreementRequired)" == False ]]; then
  readback after
elif [[ "$(field publish.json phase)" == completed ]]; then
  echo "The earlier submission completed without success; see $OUT/publish.json" >&2
  exit 1
else
  # The earlier commit's outcome is unknown: accept it only if the item already shows it.
  readback after
  if ! installed_release || (( "$(field after.json time_updated)" <= "$(field before.json time_updated)" )); then
    echo "An earlier submission's outcome is unknown and the item does not show it; inspect $OUT" >&2
    exit 1
  fi
fi
if ! installed_release; then
  echo "Readback is not the legacy release ZIP; see $OUT/after.json" >&2
  exit 1
fi
[[ -f "$OUT/verify.json" ]] && mv "$OUT/verify.json" "$OUT/verify-$(date -u +%Y%m%dT%H%M%SZ).json"
"$OUT/workshop-publish" "$SDK" verify "$OWNER" "$OUT/$ZIP_NAME" "$(field after.json time_updated)" "$OUT/verify.json"
printf 'Published %s to %s and verified a subscriber install.\nReceipts: %s\n' "$VERSION" "$ITEM" "$OUT"
