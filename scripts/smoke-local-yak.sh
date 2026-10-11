#!/usr/bin/env bash
# Local Rhino smoke test for the yak package.
#   bash scripts/smoke-local-yak.sh              # pack+install, verify package, probe MCP
#   bash scripts/smoke-local-yak.sh --no-install # verify existing install + probe MCP
#   bash scripts/smoke-local-yak.sh --mcp-only   # only probe http://127.0.0.1:$PORT/mcp
#
# Exit 0: package checks passed and MCP tools/list responded.
# Exit 1: pack/install or package layout failed.
# Exit 2: package OK but MCP not up — finish Rhino steps printed below, then re-run --mcp-only.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
YAK="${YAK:-/Applications/Rhino 8.app/Contents/Resources/bin/yak}"
PORT="${MCP4RHINO_PORT:-4010}"
DO_INSTALL=1
MCP_ONLY=0

for arg in "$@"; do
  case "$arg" in
    --no-install) DO_INSTALL=0 ;;
    --mcp-only) MCP_ONLY=1; DO_INSTALL=0 ;;
    -h|--help)
      sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "Unknown option: $arg (try --help)" >&2
      exit 1
      ;;
  esac
done

VERSION="$(awk '/^version:/ { print $2; exit }' "$ROOT/manifest.yml")"
if [[ -z "$VERSION" ]]; then
  echo "Could not read version from manifest.yml" >&2
  exit 1
fi

PKG="$ROOT/dist/MCP4Rhino-${VERSION}.yak"

verify_yak_archive() {
  bash "$ROOT/scripts/verify-yak-package.sh" "$1"
}

find_installed_root() {
  if [[ -n "${RHINO_PACKAGES_DIR:-}" ]]; then
    local base="$RHINO_PACKAGES_DIR/MCP4Rhino"
    [[ -d "$base" ]] || return 1
    echo "$base"
    return 0
  fi
  local found
  found="$(find "$HOME/Library/Application Support" -type d -path '*/Rhinoceros/packages/8.0/MCP4Rhino' 2>/dev/null | head -n 1 || true)"
  [[ -n "$found" ]] || return 1
  echo "$found"
}

verify_installed() {
  local root
  if ! root="$(find_installed_root)"; then
    echo "Installed package folder not found under Application Support (or RHINO_PACKAGES_DIR)." >&2
    exit 1
  fi
  local ver_dir="$root/$VERSION"
  if [[ ! -d "$ver_dir" ]]; then
    # newest version folder with net8.0
    ver_dir="$(find "$root" -type d -name net8.0 2>/dev/null | head -n 1 | xargs dirname 2>/dev/null || true)"
  fi
  [[ -n "$ver_dir" && -d "$ver_dir" ]] || { echo "No version folder under $root" >&2; exit 1; }
  [[ -f "$ver_dir/icon.png" ]] || { echo "Installed package missing icon.png under $ver_dir" >&2; exit 1; }
  [[ -f "$ver_dir/manifest.yml" ]] || { echo "Installed package missing manifest.yml under $ver_dir" >&2; exit 1; }
  [[ -f "$ver_dir/net8.0/MCP4Rhino.Logic.dll" ]] || { echo "Installed package missing Logic.dll under $ver_dir/net8.0" >&2; exit 1; }
  [[ -f "$ver_dir/net8.0/MCP4Rhino.rhp" ]] || { echo "Installed package missing .rhp under $ver_dir/net8.0" >&2; exit 1; }
  echo "OK installed: $ver_dir"
}

print_rhino_steps() {
  cat <<EOF

Next (manual — Rhino cannot be started from this script):
  1. Quit and reopen Rhino 8.
  2. Confirm load message: MCP4Rhino … loaded…
  3. In the Rhino command line (not Help): MCP4Rhino
  4. Expect: MCP server started: http://localhost:${PORT}
  5. Re-run: bash scripts/smoke-local-yak.sh --mcp-only

Logs: ~/Library/Logs/MCP4Rhino/mcp4rhino.log
EOF
}

probe_mcp() {
  local url="http://127.0.0.1:${PORT}/mcp"
  local body
  if ! body="$(curl -sS -m 5 -X POST "$url" \
    -H 'Content-Type: application/json' \
    -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' 2>/dev/null)"; then
    return 1
  fi
  if ! echo "$body" | grep -q '"tools"'; then
    echo "MCP responded but tools/list payload looks wrong:" >&2
    echo "$body" | head -c 400 >&2
    echo >&2
    return 1
  fi
  echo "OK MCP tools/list on $url"
  return 0
}

if [[ "$MCP_ONLY" -eq 0 ]]; then
  if [[ "$DO_INSTALL" -eq 1 ]]; then
    bash "$ROOT/scripts/install-yak.sh"
  elif [[ ! -f "$PKG" ]]; then
    echo "No $PKG — run without --no-install, or bash scripts/pack-yak.sh first." >&2
    exit 1
  fi
  verify_yak_archive "$PKG"
  verify_installed
  "$YAK" list || true
fi

if probe_mcp; then
  if [[ "$MCP_ONLY" -eq 1 ]]; then
    echo "Smoke passed: MCP tools/list."
  else
    echo "Smoke passed: package + MCP."
  fi
  exit 0
fi

if [[ "$MCP_ONLY" -eq 1 ]]; then
  echo "MCP not reachable on port ${PORT}." >&2
else
  echo "Package checks OK; MCP not reachable on port ${PORT}." >&2
fi
print_rhino_steps
exit 2

