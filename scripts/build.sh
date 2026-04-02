#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="$ROOT_DIR/artifacts/build"
DIST_DIR="$ROOT_DIR/dist/liyux.RevealStarMap"
DOTNET_CLI_HOME_DIR="$ROOT_DIR/.tooling/dotnet-cli"
DEFAULT_ONI_APP="/Users/liyux/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app"

find_dotnet() {
  if [[ -n "${DOTNET_BIN:-}" && -x "${DOTNET_BIN}" ]]; then
    echo "${DOTNET_BIN}"
    return 0
  fi

  if command -v dotnet >/dev/null 2>&1; then
    command -v dotnet
    return 0
  fi

  if [[ -x "$ROOT_DIR/.tooling/dotnet/dotnet" ]]; then
    echo "$ROOT_DIR/.tooling/dotnet/dotnet"
    return 0
  fi

  return 1
}

find_plib() {
  if [[ -n "${PLIB_DLL:-}" && -f "${PLIB_DLL}" ]]; then
    echo "${PLIB_DLL}"
    return 0
  fi

  local mods_dir="${ONI_LOCAL_MODS:-$HOME/Library/Application Support/unity.Klei.Oxygen Not Included/mods}"
  local plib_path

  plib_path="$(find "$mods_dir/Steam" -path '*/PLib.dll' ! -path '*/_old/*' 2>/dev/null | sort | head -n 1)"
  if [[ -n "$plib_path" ]]; then
    echo "$plib_path"
    return 0
  fi

  plib_path="$(find "$mods_dir/Local" -path '*/PLib.dll' ! -path '*/_old/*' 2>/dev/null | sort | head -n 1)"
  if [[ -n "$plib_path" ]]; then
    echo "$plib_path"
    return 0
  fi

  return 1
}

find_csc() {
  if [[ -n "${CSC_DLL:-}" && -f "${CSC_DLL}" ]]; then
    echo "${CSC_DLL}"
    return 0
  fi

  if [[ -d "$ROOT_DIR/.tooling/dotnet/sdk" ]]; then
    local csc_path
    csc_path="$(find "$ROOT_DIR/.tooling/dotnet/sdk" -path '*Roslyn/bincore/csc.dll' | sort | tail -n 1)"
    if [[ -n "$csc_path" ]]; then
      echo "$csc_path"
      return 0
    fi
  fi

  return 1
}

DOTNET_PATH="$(find_dotnet || true)"
CSC_PATH="$(find_csc || true)"
PLIB_DLL="$(find_plib || true)"

if [[ -z "$DOTNET_PATH" || -z "$CSC_PATH" || -z "$PLIB_DLL" ]]; then
  echo "dotnet SDK, Roslyn compiler, or PLib.dll not found."
  echo "Run ./scripts/bootstrap-dotnet.sh first, and make sure PLib is installed in ONI mods."
  exit 1
fi

ONI_APP_PATH="${ONI_APP_PATH:-$DEFAULT_ONI_APP}"
ONI_MANAGED_PATH="${ONI_MANAGED_PATH:-$ONI_APP_PATH/Contents/Resources/Data/Managed}"

rm -rf "$BUILD_DIR" "$DIST_DIR"
mkdir -p "$BUILD_DIR" "$DIST_DIR" "$DOTNET_CLI_HOME_DIR"

export DOTNET_CLI_HOME="$DOTNET_CLI_HOME_DIR"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

mapfile -t SOURCE_FILES < <(find "$ROOT_DIR/src" \
  -path "$ROOT_DIR/src/obj" -prune -o \
  -path "$ROOT_DIR/src/bin" -prune -o \
  -name '*.cs' -print | sort)

"$DOTNET_PATH" "$CSC_PATH" \
  -nologo \
  -noconfig \
  -nostdlib+ \
  -target:library \
  -langversion:latest \
  -nullable:enable \
  -debug:portable \
  -optimize+ \
  -out:"$BUILD_DIR/RevealStarMap.dll" \
  "${SOURCE_FILES[@]}" \
  -r:"$ONI_MANAGED_PATH/mscorlib.dll" \
  -r:"$ONI_MANAGED_PATH/System.dll" \
  -r:"$ONI_MANAGED_PATH/System.Core.dll" \
  -r:"$ONI_MANAGED_PATH/netstandard.dll" \
  -r:"$ONI_MANAGED_PATH/Assembly-CSharp.dll" \
  -r:"$ONI_MANAGED_PATH/Assembly-CSharp-firstpass.dll" \
  -r:"$ONI_MANAGED_PATH/0Harmony.dll" \
  -r:"$ONI_MANAGED_PATH/Newtonsoft.Json.dll" \
  -r:"$PLIB_DLL" \
  -r:"$ONI_MANAGED_PATH/UnityEngine.dll" \
  -r:"$ONI_MANAGED_PATH/UnityEngine.CoreModule.dll" \
  -r:"$ONI_MANAGED_PATH/UnityEngine.InputLegacyModule.dll" \
  -r:"$ONI_MANAGED_PATH/Unity.TextMeshPro.dll"

cp "$BUILD_DIR/RevealStarMap.dll" "$DIST_DIR/"

if [[ -f "$BUILD_DIR/RevealStarMap.pdb" ]]; then
  cp "$BUILD_DIR/RevealStarMap.pdb" "$DIST_DIR/"
fi

cp "$ROOT_DIR/mod.yaml" "$DIST_DIR/"
cp "$ROOT_DIR/mod_info.yaml" "$DIST_DIR/"
cp "$PLIB_DLL" "$DIST_DIR/"

echo "Built mod into:"
echo "  $DIST_DIR"
