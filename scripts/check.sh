#!/usr/bin/env bash
set -euo pipefail
# shellcheck source=build-env.sh
source "$(dirname "${BASH_SOURCE[0]}")/build-env.sh"
# The verifier and archive assertions use this canonical package location.
export DIST_DIR="$ROOT_DIR/dist/liyux.RevealStarMap"
"$ROOT_DIR/scripts/build.sh"
if command -v shellcheck >/dev/null 2>&1; then
  shellcheck -x -P "$ROOT_DIR/scripts" "$ROOT_DIR"/scripts/*.sh "$ROOT_DIR"/tests/*.sh
fi
"$DOTNET_BIN" run --project "$ROOT_DIR/tests/Verify.csproj" \
  "-p:OniManagedPath=$ONI_MANAGED_PATH" -- "$ONI_MANAGED_PATH" "$ROOT_DIR"
