#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MANAGED="$HOME/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app/Contents/Resources/Data/Managed"
COMPILER="$ROOT/.tooling/dotnet/sdk/8.0.419/Roslyn/bincore/csc.dll"
if [[ ! -f "$COMPILER" ]]; then
  COMPILER="$(find "$ROOT/.tooling/dotnet/sdk" -path '*/Roslyn/bincore/csc.dll' | sort | tail -1)"
fi
OUT="$ROOT/artifacts/e2e-20260922/LiveProbe.dll"
REFS=()
for dll in mscorlib System System.Core netstandard Assembly-CSharp Assembly-CSharp-firstpass 0Harmony Newtonsoft.Json UnityEngine UnityEngine.CoreModule UnityEngine.UI Unity.TextMeshPro; do
  REFS+=("-r:$MANAGED/$dll.dll")
done
"$ROOT/.tooling/dotnet/dotnet" "$COMPILER" -nologo -noconfig -nostdlib+ -langversion:latest \
  -target:library -out:"$OUT" "$ROOT/tests/LiveProbe.cs" "${REFS[@]}" \
  -r:"$ROOT/dist/liyux.RevealStarMap/RevealStarMap.dll" -r:"$ROOT/vendor/PLib.dll"
echo "$OUT"
