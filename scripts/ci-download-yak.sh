#!/usr/bin/env bash
# Download a pinned standalone Yak CLI (no Rhino required).
# Prints the absolute path to stdout (last line) for:  export YAK="$(bash scripts/ci-download-yak.sh)"
#
# Optional: YAK_VERSION (default 0.15.1), YAK_DEST (default $RUNNER_TEMP or /tmp), YAK_OS override.
set -euo pipefail

YAK_VERSION="${YAK_VERSION:-0.15.1}"
DEST_DIR="${YAK_DEST:-${RUNNER_TEMP:-/tmp}/yak-cli-${YAK_VERSION}}"
mkdir -p "$DEST_DIR"

# SHA256 pins for YAK_VERSION=0.15.1 (McNeel files.mcneel.com). Bump version + digests together.
expected_sha256() {
  case "$1" in
    linux-x64) echo "ac8e9c4101fa5c6ca24907065288080363801f26cca04838d90ab933b68903c0" ;;
    linux-arm64) echo "7c4eea9e38b9f16950f84ff22a41104140f8a9ddc9532894e8e0072fbeeef626" ;;
    osx-universal) echo "555c380bd3a7f97a37745fa40e640c5035b64855cfae801044bba5b903e17168" ;;
    win-x64) echo "9a4356bb1d95e2c7fd6131818e1fb817aff51249f4d27fc5aba34dbe8ba7b52e" ;;
    *) return 1 ;;
  esac
}

uname_s="$(uname -s | tr '[:upper:]' '[:lower:]')"
uname_m="$(uname -m)"

if [[ -n "${YAK_OS:-}" ]]; then
  platform="$YAK_OS"
else
  case "$uname_s" in
    linux)
      case "$uname_m" in
        aarch64|arm64) platform="linux-arm64" ;;
        *) platform="linux-x64" ;;
      esac
      ;;
    darwin)
      platform="osx-universal"
      ;;
    mingw*|msys*|cygwin*)
      case "$uname_m" in
        aarch64|arm64)
          echo "win-arm64 Yak pin not defined; set YAK_OS=win-x64 under emulation or add a digest." >&2
          exit 1
          ;;
        *) platform="win-x64" ;;
      esac
      ;;
    *)
      echo "Unsupported OS for Yak download: $uname_s ($uname_m). Set YAK_OS." >&2
      exit 1
      ;;
  esac
fi

case "$platform" in
  win-*) name="yak.exe" ;;
  *) name="yak" ;;
esac

if [[ "$YAK_VERSION" != "0.15.1" ]]; then
  echo "No SHA256 pin table for YAK_VERSION=$YAK_VERSION (update scripts/ci-download-yak.sh)." >&2
  exit 1
fi

expect="$(expected_sha256 "$platform")" || {
  echo "No SHA256 pin for platform $platform" >&2
  exit 1
}

url="https://files.mcneel.com/yak/tools/${YAK_VERSION}/${platform}/${name}"
out="${DEST_DIR}/${name}"

echo "Downloading Yak ${YAK_VERSION} (${platform}) from ${url}" >&2
curl -fsSL "$url" -o "$out"
chmod +x "$out"

if [[ ! -f "$out" ]]; then
  echo "Downloaded Yak missing at $out" >&2
  exit 1
fi
if [[ ! -x "$out" ]]; then
  echo "Downloaded Yak is not executable: $out" >&2
  exit 1
fi

if command -v sha256sum >/dev/null 2>&1; then
  actual="$(sha256sum "$out" | awk '{ print $1 }')"
elif command -v shasum >/dev/null 2>&1; then
  actual="$(shasum -a 256 "$out" | awk '{ print $1 }')"
else
  echo "Need sha256sum or shasum to verify Yak download" >&2
  exit 1
fi

if [[ "$actual" != "$expect" ]]; then
  echo "Yak SHA256 mismatch for ${platform}:" >&2
  echo "  expected $expect" >&2
  echo "  actual   $actual" >&2
  exit 1
fi

echo "OK Yak SHA256 ${platform}" >&2
echo "$out"
