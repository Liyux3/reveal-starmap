#!/usr/bin/env bash
# Shared by the build, managed checks and optional in-game test driver.
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ONI_APP_PATH="${ONI_APP_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app}"
ONI_MANAGED_PATH="${ONI_MANAGED_PATH:-$ONI_APP_PATH/Contents/Resources/Data/Managed}"
PLIB_DLL="$ROOT_DIR/vendor/PLib.dll"

if [[ -z "${DOTNET_BIN:-}" ]]; then
  DOTNET_BIN="$(command -v dotnet || true)"
  if [[ -z "$DOTNET_BIN" ]]; then
    DOTNET_BIN="$ROOT_DIR/.tooling/dotnet/dotnet"
  fi
fi
if [[ ! -x "$DOTNET_BIN" ]]; then
  echo "A .NET SDK is required. Install one or run ./scripts/bootstrap-dotnet.sh." >&2
  exit 1
fi

if [[ -z "${CSC_DLL:-}" ]]; then
  SDK_LINE="$("$DOTNET_BIN" --list-sdks | tail -n 1)"
  SDK_VERSION="${SDK_LINE%% *}"
  SDK_ROOT="${SDK_LINE#*\[}"
  SDK_ROOT="${SDK_ROOT%\]}"
  CSC_DLL="$SDK_ROOT/$SDK_VERSION/Roslyn/bincore/csc.dll"
fi
if [[ ! -f "$CSC_DLL" || ! -f "$PLIB_DLL" || ! -f "$ONI_MANAGED_PATH/Assembly-CSharp.dll" ]]; then
  echo "Missing Roslyn, vendor/PLib.dll or ONI managed assemblies. Set ONI_MANAGED_PATH for your game installation." >&2
  exit 1
fi

export DOTNET_CLI_HOME="$ROOT_DIR/.tooling/dotnet-cli"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# The verifier targets .NET 8; a newer installed runtime may run it too.
export DOTNET_ROLL_FORWARD=Major
mkdir -p "$DOTNET_CLI_HOME"
