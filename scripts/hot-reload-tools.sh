#!/usr/bin/env bash
# Build tools and copy into the installed yak package so MCP4RhinoReload / mcp4rhino_reload picks them up.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"

dotnet build "$ROOT/src/MCP4Rhino.Tools/MCP4Rhino.Tools.csproj" -c Release

DEST_ROOT="$HOME/Library/Application Support/McNeel/Rhinoceros/packages/8.0/MCP4Rhino"
VER="$(cat "$DEST_ROOT/manifest.txt" 2>/dev/null || echo 0.3.0)"
DEST="$DEST_ROOT/$VER/net8.0"
TOOLS_OUT="$ROOT/src/MCP4Rhino.Tools/bin/Release/net8.0"

if [[ ! -d "$DEST" ]]; then
  echo "Installed package dir not found: $DEST" >&2
  echo "Run scripts/install-yak.sh first." >&2
  exit 1
fi

cp "$TOOLS_OUT/MCP4Rhino.Tools.dll" "$DEST/"
# Logic + drawing deps (host also needs Logic.dll beside the .rhp)
for f in MCP4Rhino.Logic.dll System.Drawing.Common.dll Microsoft.Win32.SystemEvents.dll; do
  [[ -f "$TOOLS_OUT/$f" ]] && cp "$TOOLS_OUT/$f" "$DEST/"
done

echo "Updated: $DEST/MCP4Rhino.Tools.dll (+ Logic deps)"
echo "In Rhino run MCP4RhinoReload, or via MCP call tool mcp4rhino_reload"
echo "Optional: export MCP4RHINO_TOOLS_PATH=\"$DEST\""
