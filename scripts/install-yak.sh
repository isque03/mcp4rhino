#!/usr/bin/env bash
# Pack universal .yak into dist/, then install it locally (macOS).
# Run from repo root:  bash scripts/install-yak.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
YAK="${YAK:-/Applications/Rhino 8.app/Contents/Resources/bin/yak}"

bash "$ROOT/scripts/pack-yak.sh"

VERSION="$(awk '/^version:/ { print $2; exit }' "$ROOT/manifest.yml")"
PKG="$ROOT/dist/MCP4Rhino-${VERSION}.yak"
if [[ ! -f "$PKG" ]]; then
  echo "Expected package not found: $PKG" >&2
  exit 1
fi

"$YAK" install "$PKG"
"$YAK" list
echo "Installed. In Rhino: MCP4Rhino once after host changes; MCP4RhinoReload or tool mcp4rhino_reload for tools-only."
