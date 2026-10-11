---
name: publish
description: >-
  Releases MCP4Rhino to McNeel Yak Package Manager and GitHub Releases via
  GitHub Actions. Use when the user asks to publish, release, yak push, cut a
  version, tag a release, or run the Publish workflow.
---

# publish

Ship a versioned universal `.yak` using **Build** (artifact) then manual **Publish**.
Do **not** rebuild or `yak push` from the agent machine during a release.

Human docs: [README.md](../../../README.md) (CI and publishing). Workflows:
[build.yml](../../../.github/workflows/build.yml),
[publish.yml](../../../.github/workflows/publish.yml).

## Hard rules

- Publish only from a **merged `main` commit** (or the tagged SHA that is on `main`). Never publish from an open feature PR tip unless the user explicitly overrides.
- **Tag after merge.** Tag = `v` + [manifest.yml](../../../manifest.yml) `version` (e.g. `0.5.1` → `v0.5.1`).
- Publish downloads **`yak-packages`** from a successful **Build** for that commit — it does not rebuild.
- Never echo, commit, or paste `YAK_TOKEN`. Token lives only as Environment secret **`production`**.
- Never run `yak push` locally as a substitute for `publish.yml` unless the user explicitly asks for an emergency path.
- Do not invent ports, package names, or workflow names. Package: **MCP4Rhino**. Default MCP port stays **4010**.

## Preconditions (check before tagging)

```bash
# On main, clean, up to date
git checkout main && git pull

# Version you intend to ship
awk '/^version:/ { print $2; exit }' manifest.yml

# Green Build for THIS commit (required)
gh run list --workflow=build.yml --commit="$(git rev-parse HEAD)" --status=success --limit=1
```

If no successful Build: push/wait or `gh workflow run build.yml` is **not** enough unless it runs on this SHA — fix CI or merge first, then re-check.

## Release procedure

Track progress:

```
Release:
- [ ] 1. main green Build for release SHA
- [ ] 2. manifest.yml version confirmed
- [ ] 3. tag vX.Y.Z pushed on that SHA
- [ ] 4. Publish workflow dispatched with same tag
- [ ] 5. production approved (human)
- [ ] 6. GitHub Release + yak search verified
```

### 1. Confirm Build artifact

```bash
SHA="$(git rev-parse HEAD)"
gh run list --workflow=build.yml --commit="$SHA" --status=success --limit=1
```

Optional: download and inspect `yak-packages` from that run (`gh run download <id> -n yak-packages`).

### 2. Tag

```bash
VERSION="$(awk '/^version:/ { print $2; exit }' manifest.yml)"
TAG="v${VERSION}"
git tag -a "$TAG" -m "MCP4Rhino ${VERSION}"
git push origin "$TAG"
```

Annotated tag preferred. Tag must point at the commit that has the green Build.

### 3. Dispatch Publish

```bash
gh workflow run publish.yml -f tag="v${VERSION}"
gh run watch --workflow=publish.yml
# or: gh run list --workflow=publish.yml --limit=1
```

Ask the user to **approve Environment `production`** in the GitHub UI if required reviewers are configured. Agent cannot approve that for them.

### 4. Verify

```bash
gh release view "v${VERSION}"
# Asset: MCP4Rhino-<version>.yak

# After McNeel index refresh:
yak search MCP4Rhino
# or Rhino: Tools → Package Manager → MCP4Rhino
```

## Version bumps

If Yak already has the version (Publish duplicate guard fails):

1. Bump `version` in [manifest.yml](../../../manifest.yml) (and keep plugin/project versions in sync if the repo requires it).
2. Merge to `main`, wait for green Build.
3. Tag the **new** `vX.Y.Z` and run Publish again.

Do not reuse or move a published tag. Do not re-push a yanked Yak version expecting success.

## One-time maintainer setup (document only)

If Publish fails on missing token:

1. Repo Settings → Environments → **`production`** (optional required reviewers).
2. Environment secret **`YAK_TOKEN` only** (not a repository secret).
3. Generate locally, never commit:

```bash
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" login --ci
```

## Agent boundaries

| Agent may | Agent must ask user / stop |
|-----------|----------------------------|
| Verify Build, create/push tag, `gh workflow run publish.yml`, watch runs, verify Release/`yak search` | Approve `production`, create/paste `YAK_TOKEN`, force-overwrite tags, `yak yank` without explicit ask |
| Explain troubleshooting from README CI table | Publish mid-PR “to test the token” |

## Troubleshooting (short)

| Symptom | Action |
|---------|--------|
| No Build run for commit | Get green `build.yml` on that SHA first |
| Tag ≠ `v` + manifest | Fix tag or manifest; do not dispatch mismatched tag |
| Version already on Yak | Bump version; new tag; new Publish |
| Missing `YAK_TOKEN` | Add to Environment **`production`** only |
| Package Manager empty | Check GitHub Release asset + `yak search`; wait for index |
