#!/usr/bin/env bash
# Build Release and produce one universal .yak under dist/ (mac + win).
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

[[ -f "$ROOT/icon.png" ]] || { echo "Missing icon.png at repo root" >&2; exit 1; }

dotnet build "$ROOT/MCP4Rhino.sln" -c Release

STAGE="$(mktemp -d)/mcp4rhino-yak"
mkdir -p "$STAGE/net8.0"
HOST_OUT="$ROOT/src/MCP4Rhino/bin/Release/net8.0"
TOOLS_OUT="$ROOT/src/MCP4Rhino.Tools/bin/Release/net8.0"
GH_OUT="$ROOT/src/MCP4Rhino.Grasshopper/bin/Release/net8.0"

require_file() {
  local path="$1"
  [[ -f "$path" ]] || { echo "Required build output missing: $path" >&2; exit 1; }
}

require_file "$HOST_OUT/MCP4Rhino.rhp"
require_file "$HOST_OUT/MCP4Rhino.Contracts.dll"
require_file "$HOST_OUT/MCP4Rhino.Logic.dll"
require_file "$HOST_OUT/MCP4Rhino.rui"
require_file "$TOOLS_OUT/MCP4Rhino.Tools.dll"
require_file "$GH_OUT/MCP4Rhino.Grasshopper.dll"

cp "$HOST_OUT/MCP4Rhino.rhp" "$STAGE/net8.0/"
cp "$HOST_OUT/MCP4Rhino.Contracts.dll" "$STAGE/net8.0/"
cp "$HOST_OUT/MCP4Rhino.Logic.dll" "$STAGE/net8.0/"
cp "$HOST_OUT/MCP4Rhino.rui" "$STAGE/net8.0/"
cp "$TOOLS_OUT/MCP4Rhino.Tools.dll" "$STAGE/net8.0/"
cp "$GH_OUT/MCP4Rhino.Grasshopper.dll" "$STAGE/net8.0/"
cp -R "$ROOT/skills" "$STAGE/skills"

# Tools deps (e.g. System.Drawing.Common) — skip RhinoCommon
shopt -s nullglob
for f in "$TOOLS_OUT"/*.dll; do
  base="$(basename "$f")"
  case "$base" in
    RhinoCommon.dll|Rhino.UI.dll|Eto.dll|Grasshopper.dll|GH_IO.dll|MCP4Rhino.rhp) continue ;;
  esac
  cp "$f" "$STAGE/net8.0/"
done
shopt -u nullglob

cp "$ROOT/manifest.yml" "$STAGE/"
cp "$ROOT/icon.png" "$STAGE/"

DIST="$ROOT/dist"
mkdir -p "$DIST"
DEST="$DIST/MCP4Rhino-${VERSION}.yak"
rm -f "$DEST" "$DIST/MCP4Rhino-${VERSION}-mac.yak" "$DIST/MCP4Rhino-${VERSION}-win.yak"

cd "$STAGE"
rm -f ./*.yak
# Plain build → rh8_*-any.yak (managed net8.0 plugin; no OS-specific natives staged).
"$YAK" build
shopt -s nullglob
yaks=( ./*.yak )
shopt -u nullglob
if [[ ${#yaks[@]} -eq 0 || ! -f "${yaks[0]}" ]]; then
  echo "yak build produced no .yak" >&2
  exit 1
fi
built="${yaks[0]}"
case "$built" in
  *.yak) ;;
  *)
    echo "yak build produced unexpected file: $built" >&2
    exit 1
    ;;
esac
cp "$built" "$DEST"
echo "Wrote $DEST"

bash "$ROOT/scripts/verify-yak-package.sh" "$DEST"
python3 "$ROOT/scripts/package-agent-skills.py"
cp "$ROOT/scripts/grasshopper-smoke.py" "$DIST/grasshopper-smoke.py"

echo "Packed MCP4Rhino $VERSION → $DEST"
echo "Release publication uses the exact CI Build artifact via the Publish workflow; see docs/PUBLISHING.md."
