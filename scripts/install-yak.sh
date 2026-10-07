#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
YAK="${YAK:-/Applications/Rhino 8.app/Contents/Resources/bin/yak}"

dotnet build "$ROOT/MCP4Rhino.sln" -c Release

STAGE="$(mktemp -d)/mcp4rhino-yak"
mkdir -p "$STAGE/net8.0"
HOST_OUT="$ROOT/src/MCP4Rhino/bin/Release/net8.0"
TOOLS_OUT="$ROOT/src/MCP4Rhino.Tools/bin/Release/net8.0"

cp "$HOST_OUT/MCP4Rhino.rhp" "$STAGE/net8.0/"
cp "$HOST_OUT/MCP4Rhino.Contracts.dll" "$STAGE/net8.0/"
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
cd "$STAGE"
"$YAK" build --platform mac
"$YAK" install ./*.yak
"$YAK" list
echo "Installed. In Rhino: MCP4Rhino once after host changes; MCP4RhinoReload or tool mcp4rhino_reload for tools-only."
