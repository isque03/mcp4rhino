#!/usr/bin/env bash
# Check archive entries and embedded version; default expected version is the source manifest.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
python3 "$ROOT/scripts/release_checks.py" package "$@"
