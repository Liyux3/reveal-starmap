#!/usr/bin/env bash
set -euo pipefail
# shellcheck source=build-env.sh
source "$(dirname "${BASH_SOURCE[0]}")/build-env.sh"

BUILD_DIR="$ROOT_DIR/artifacts/build"
DIST_DIR="${DIST_DIR:-$ROOT_DIR/dist/liyux.RevealStarMap}"
VERSION="$(awk '/^version:/ { print $2 }' "$ROOT_DIR/mod_info.yaml")"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Invalid mod_info.yaml version: $VERSION" >&2
  exit 1
fi
if [[ -e "$DIST_DIR" ]] && { [[ -L "$DIST_DIR" ]] || ! grep -q '^staticID: liyux.RevealStarMap$' "$DIST_DIR/mod.yaml"; }; then
  echo "Refusing to replace a directory that is not a generated Reveal StarMap package: $DIST_DIR" >&2
  exit 1
fi
mkdir -p "$BUILD_DIR" "$ROOT_DIR/dist" "$(dirname "$DIST_DIR")"
STAGE="$(mktemp -d "$ROOT_DIR/dist/.package.XXXXXX")"
trap 'rm -rf "$STAGE"' EXIT
PACKAGE="$STAGE/liyux.RevealStarMap"
mkdir -p "$PACKAGE/translations"

SOURCE_FILES=()
while IFS= read -r path; do SOURCE_FILES+=("$path"); done < <(
  find "$ROOT_DIR/src" -type d \( -name obj -o -name bin \) -prune -o -name '*.cs' -print | LC_ALL=C sort
)
RESOURCES=()
for path in "$ROOT_DIR"/translations/*.json; do
  RESOURCES+=("-resource:$path,RevealStarMap.$(basename "$path")")
done
REFERENCES=()
for dll in mscorlib System System.Core netstandard Assembly-CSharp Assembly-CSharp-firstpass 0Harmony Newtonsoft.Json UnityEngine UnityEngine.CoreModule UnityEngine.InputLegacyModule UnityEngine.UI Unity.TextMeshPro; do
  REFERENCES+=("-r:$ONI_MANAGED_PATH/$dll.dll")
done

"$DOTNET_BIN" "$CSC_DLL" -nologo -noconfig -nostdlib+ -target:library -langversion:latest \
  -nullable:enable -deterministic+ -debug:portable -optimize+ \
  "-pathmap:$ROOT_DIR=/_/RevealStarMap" -out:"$BUILD_DIR/RevealStarMap.dll" \
  "${RESOURCES[@]}" "${SOURCE_FILES[@]}" "${REFERENCES[@]}" "-r:$PLIB_DLL"

cp "$BUILD_DIR/RevealStarMap.dll" "$BUILD_DIR/RevealStarMap.pdb" "$ROOT_DIR/mod.yaml" \
  "$ROOT_DIR/mod_info.yaml" "$PLIB_DLL" "$ROOT_DIR/vendor/PLib.LICENSE.txt" "$PACKAGE/"
cp "$ROOT_DIR"/translations/*.json "$PACKAGE/translations/"
(cd "$STAGE" && zip -qrX "RevealStarMap-$VERSION.zip" liyux.RevealStarMap)
(cd "$PACKAGE" && zip -qrX "$STAGE/RevealStarMap-$VERSION-workshop.zip" .)

# Retain the previous generated output in the system Trash for easy rollback.
if [[ -e "$DIST_DIR" ]]; then
  mkdir -p "$HOME/.Trash"
  RETIRED="$(mktemp -d "$HOME/.Trash/reveal-starmap-build.XXXXXX")"
  mv "$DIST_DIR" "$RETIRED/package"
fi
mv "$PACKAGE" "$DIST_DIR"
mv "$STAGE/RevealStarMap-$VERSION.zip" "$ROOT_DIR/dist/RevealStarMap-$VERSION.zip"
mv "$STAGE/RevealStarMap-$VERSION-workshop.zip" "$ROOT_DIR/dist/RevealStarMap-$VERSION-workshop.zip"
printf 'Built %s\nPackage: %s\nArchive: %s\n' "$VERSION" "$DIST_DIR" "$ROOT_DIR/dist/RevealStarMap-$VERSION.zip"
