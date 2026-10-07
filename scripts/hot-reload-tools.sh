#!/usr/bin/env bash
# Build tools and copy into the installed yak package so MCP4RhinoReload / mcp4rhino_reload picks them up.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"

dotnet build "$ROOT/src/MCP4Rhino.Tools/MCP4Rhino.Tools.csproj" -c Release

find_pkg_root() {
  if [[ -n "${RHINO_PACKAGES_DIR:-}" ]]; then
    echo "${RHINO_PACKAGES_DIR%/}/MCP4Rhino"
    return 0
  fi
  local d
  for d in "$HOME/Library/Application Support"/*/Rhinoceros/packages/8.0/MCP4Rhino; do
    if [[ -d "$d" ]]; then
      echo "$d"
      return 0
    fi
  done
  return 1
}

DEST_ROOT="$(find_pkg_root)" || {
  echo "Installed package dir not found under Application Support/*/Rhinoceros/packages/8.0/MCP4Rhino" >&2
  echo "Run scripts/install-yak.sh first, or set RHINO_PACKAGES_DIR to the Rhino packages/8.0 folder." >&2
  exit 1
}

VER="$(cat "$DEST_ROOT/manifest.txt" 2>/dev/null || echo 0.4.0)"
DEST="$DEST_ROOT/$VER/net8.0"
TOOLS_OUT="$ROOT/src/MCP4Rhino.Tools/bin/Release/net8.0"

if [[ ! -d "$DEST" ]]; then
  # Prefer newest version folder with net8.0
  DEST="$(ls -1d "$DEST_ROOT"/*/net8.0 2>/dev/null | tail -1 || true)"
fi
if [[ -z "${DEST:-}" || ! -d "$DEST" ]]; then
  echo "Installed package net8.0 dir not found under: $DEST_ROOT" >&2
  echo "Run scripts/install-yak.sh first." >&2
  exit 1
fi

cp "$TOOLS_OUT/MCP4Rhino.Tools.dll" "$DEST/"
for f in MCP4Rhino.Logic.dll System.Drawing.Common.dll Microsoft.Win32.SystemEvents.dll; do
  [[ -f "$TOOLS_OUT/$f" ]] && cp "$TOOLS_OUT/$f" "$DEST/"
done

echo "Updated: $DEST/MCP4Rhino.Tools.dll (+ Logic deps)"
echo "In Rhino run MCP4RhinoReload, or via MCP call tool mcp4rhino_reload"
echo "Optional: export MCP4RHINO_TOOLS_PATH=\"$DEST\""
