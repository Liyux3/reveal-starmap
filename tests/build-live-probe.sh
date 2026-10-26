#!/usr/bin/env bash
set -euo pipefail
# shellcheck source=../scripts/build-env.sh
source "$(dirname "${BASH_SOURCE[0]}")/../scripts/build-env.sh"
TEST_ROOT="${LIVE_TEST_ROOT:-$ROOT_DIR/artifacts/live-tests}"
mkdir -p "$TEST_ROOT"
REFS=()
for dll in mscorlib System System.Core netstandard Assembly-CSharp Assembly-CSharp-firstpass 0Harmony Newtonsoft.Json UnityEngine UnityEngine.CoreModule UnityEngine.UI Unity.TextMeshPro; do
  REFS+=("-r:$ONI_MANAGED_PATH/$dll.dll")
done
"$DOTNET_BIN" "$CSC_DLL" -nologo -noconfig -nostdlib+ -langversion:latest \
  -target:library -out:"$TEST_ROOT/LiveProbe.dll" "$ROOT_DIR/tests/LiveProbe.cs" "${REFS[@]}" \
  -r:"$ROOT_DIR/dist/liyux.RevealStarMap/RevealStarMap.dll" -r:"$PLIB_DLL"
printf '%s\n' "$TEST_ROOT" > "$TEST_ROOT/probe-root.txt"
printf '%s\n' "$TEST_ROOT/LiveProbe.dll"
