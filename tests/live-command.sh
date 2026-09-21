#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)/artifacts/e2e-20260922"
TMP="$(mktemp "$ROOT/request.XXXXXX")"
ID="$(basename "$TMP")"
jq -n --argjson command "$1" --arg id "$ID" '$command + {id:$id}' > "$TMP"
mv "$TMP" "$ROOT/command.json"
for ((i=0; i<60; i++)); do
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
