#!/usr/bin/env bash
# Find the latest successful Build workflow run for a commit and download yak-packages.
# Usage: bash scripts/ci-download-build-artifact.sh <git-sha> [dest-dir]
# Requires: gh auth (GITHUB_TOKEN in Actions), jq.
set -euo pipefail

SHA="${1:-}"
DEST="${2:-dist}"
if [[ -z "$SHA" ]]; then
  echo "Usage: $0 <git-sha> [dest-dir]" >&2
  exit 1
fi

if ! command -v gh >/dev/null 2>&1; then
  echo "gh CLI required" >&2
  exit 1
fi

RUN_ID="$(gh run list \
  --workflow=build.yml \
  --commit="$SHA" \
  --status=success \
  --limit=1 \
  --json databaseId \
  --jq '.[0].databaseId // empty')"

if [[ -z "$RUN_ID" ]]; then
  echo "No successful Build workflow run found for commit ${SHA}." >&2
  echo "Push/merge so build.yml completes successfully on that commit, then retry publish." >&2
  exit 1
fi

echo "Using Build run id ${RUN_ID} for commit ${SHA}" >&2
mkdir -p "$DEST"
# Download into DEST (artifact files land in DEST/)
gh run download "$RUN_ID" --name yak-packages --dir "$DEST"
echo "$RUN_ID"
