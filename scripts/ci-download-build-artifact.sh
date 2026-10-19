#!/usr/bin/env bash
# Download an available push Build artifact for exactly the requested SHA and branch.
# Usage: bash scripts/ci-download-build-artifact.sh <git-sha> [dest-dir] [branch]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SHA="${1:-}"
DEST="${2:-dist}"
BRANCH="${3:-main}"
if [[ ! "$SHA" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Usage: $0 <full-git-sha> [dest-dir] [branch]" >&2
  exit 1
fi

if ! command -v gh >/dev/null 2>&1; then
  echo "gh CLI required" >&2
  exit 1
fi

RUN_ID="$(gh run list \
  --workflow=build.yml \
  --branch="$BRANCH" \
  --event=push \
  --commit="$SHA" \
  --status=success \
  --limit=20 \
  --json databaseId,headSha,headBranch,event,conclusion |
  python3 "$ROOT/scripts/release_checks.py" select-run "$SHA" "$BRANCH")"

gh api "repos/{owner}/{repo}/actions/runs/$RUN_ID/artifacts" |
  python3 "$ROOT/scripts/release_checks.py" artifact >&2

echo "Using Build run id ${RUN_ID} for commit ${SHA} on ${BRANCH}" >&2
mkdir -p "$DEST"
# Download into DEST (artifact files land in DEST/)
gh run download "$RUN_ID" --name yak-packages --dir "$DEST"
echo "$RUN_ID"
