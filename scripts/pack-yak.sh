#!/usr/bin/env bash
# Build Release and produce mac + win .yak packages under dist/.
# Run from repo root:  bash scripts/pack-yak.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
YAK="${YAK:-/Applications/Rhino 8.app/Contents/Resources/bin/yak}"

if [[ ! -x "$YAK" && ! -f "$YAK" ]]; then
  echo "Yak not found at '$YAK'. Install Rhino 8 or set env YAK." >&2
  exit 1
fi

VERSION="$(awk '/^version:/ { print $2; exit }' "$ROOT/manifest.yml")"
if [[ -z "$VERSION" ]]; then
  echo "Could not read version from manifest.yml" >&2
  exit 1
fi

dotnet build "$ROOT/MCP4Rhino.sln" -c Release

STAGE="$(mktemp -d)/mcp4rhino-yak"
mkdir -p "$STAGE/net8.0"
HOST_OUT="$ROOT/src/MCP4Rhino/bin/Release/net8.0"
TOOLS_OUT="$ROOT/src/MCP4Rhino.Tools/bin/Release/net8.0"

cp "$HOST_OUT/MCP4Rhino.rhp" "$STAGE/net8.0/"
cp "$HOST_OUT/MCP4Rhino.Contracts.dll" "$STAGE/net8.0/"
[[ -f "$HOST_OUT/MCP4Rhino.Logic.dll" ]] && cp "$HOST_OUT/MCP4Rhino.Logic.dll" "$STAGE/net8.0/"
cp "$TOOLS_OUT/MCP4Rhino.Tools.dll" "$STAGE/net8.0/"
# Tools deps (e.g. System.Drawing.Common) — skip RhinoCommon
shopt -s nullglob
for f in "$TOOLS_OUT"/*.dll; do
  base="$(basename "$f")"
  case "$base" in
    RhinoCommon.dll|Rhino.UI.dll|Eto.dll|MCP4Rhino.rhp) continue ;;
  esac
  cp "$f" "$STAGE/net8.0/"
done

cp "$ROOT/manifest.yml" "$STAGE/"
cp "$ROOT/icon.png" "$STAGE/"

DIST="$ROOT/dist"
mkdir -p "$DIST"
rm -f "$DIST/MCP4Rhino-${VERSION}-mac.yak" "$DIST/MCP4Rhino-${VERSION}-win.yak"

cd "$STAGE"
for platform in mac win; do
  rm -f ./*.yak
  "$YAK" build --platform "$platform"
  built="$(ls -1 ./*.yak | head -n 1)"
  if [[ -z "$built" ]]; then
    echo "yak build --platform $platform produced no .yak" >&2
    exit 1
  fi
  dest="$DIST/MCP4Rhino-${VERSION}-${platform}.yak"
  cp "$built" "$dest"
  echo "Wrote $dest"
done

echo "Packed MCP4Rhino $VERSION → dist/MCP4Rhino-${VERSION}-{mac,win}.yak"
echo "Publish checklist: yak login && yak push dist/MCP4Rhino-${VERSION}-mac.yak && yak push dist/MCP4Rhino-${VERSION}-win.yak"
