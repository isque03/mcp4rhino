#!/usr/bin/env bash
# Verify a packed MCP4Rhino .yak archive (icon, manifest, required assemblies).
# Usage: bash scripts/verify-yak-package.sh path/to/MCP4Rhino-<version>.yak
set -euo pipefail

PKG="${1:-}"
if [[ -z "$PKG" || ! -f "$PKG" ]]; then
  echo "Usage: $0 path/to/package.yak" >&2
  exit 1
fi

listing="$(unzip -l "$PKG")"
for need in icon.png manifest.yml net8.0/MCP4Rhino.Logic.dll net8.0/MCP4Rhino.rhp net8.0/MCP4Rhino.Tools.dll net8.0/MCP4Rhino.Contracts.dll net8.0/MCP4Rhino.rui; do
  echo "$listing" | grep -q "$need" || {
    echo "Archive missing $need: $PKG" >&2
    exit 1
  }
done

manifest="$(unzip -p "$PKG" manifest.yml)"
echo "$manifest" | grep -q '^icon: icon.png' || {
  echo "manifest.yml missing icon: icon.png in $PKG" >&2
  exit 1
}
echo "$manifest" | grep -q '^name: MCP4Rhino' || {
  echo "manifest.yml unexpected name in $PKG" >&2
  exit 1
}

echo "OK package: $PKG"
