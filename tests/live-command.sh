#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
ROOT="${LIVE_TEST_ROOT:-$ROOT_DIR/artifacts/live-tests}"
TMP="$(mktemp "$ROOT/request.XXXXXX")"
ID="$(basename "$TMP")"
TIMEOUT="${LIVE_TEST_TIMEOUT_SECONDS:-120}"
if [[ ! "$TIMEOUT" =~ ^[1-9][0-9]*$ ]]; then
  echo "LIVE_TEST_TIMEOUT_SECONDS must be a positive integer." >&2
  exit 1
fi
jq -n --argjson command "$1" --arg id "$ID" '$command + {id:$id}' > "$TMP"
mv "$TMP" "$ROOT/command.json"
DEADLINE=$((SECONDS + TIMEOUT))
while ((SECONDS < DEADLINE)); do
  if [[ -f "$ROOT/result.json" ]] && jq -e --arg id "$ID" '.id==$id' "$ROOT/result.json" >/dev/null 2>&1; then
    cp "$ROOT/result.json" "$ROOT/$ID.json"
    jq . "$ROOT/result.json"
    jq -e '.ok == true' "$ROOT/result.json" >/dev/null || exit 2
    exit 0
  fi
  sleep 0.25
done
echo "Timed out waiting for live probe: $ID" >&2
exit 1
