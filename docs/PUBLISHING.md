# CI and publishing (maintainers)

GitHub Actions on this public repo (GitHub Free). **Build** never sees publishing secrets.

## Automated builds (`build.yml`)

On every pull request and every push to `main`:

1. **Test** on `windows-latest` and `macos-latest` (.NET 8, NuGet cache, coverage gate).
2. **Pack** one universal `MCP4Rhino-<version>.yak` on `ubuntu-latest` (standalone Yak CLI; Rhino not installed).
3. Upload the `.yak` as the workflow artifact **`yak-packages`** (kept 14 days).

### Download a development build

1. GitHub → **Actions** → **Build**.
2. Open a green run for your commit.
3. Download **`yak-packages`** → `MCP4Rhino-<version>.yak`.
4. `yak install` that file, restart Rhino.

### Configure `YAK_TOKEN` (one-time)

1. Create Environment **`production`** (Settings → Environments). On public Free repos you can add **required reviewers**.
2. Add environment secret **`YAK_TOKEN` only** (not a repository secret). Yak does not use `YAK_USERNAME`.
3. Generate the token locally (never commit it):

```bash
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" login --ci
```

### Trigger a release (`publish.yml`)

Publishing is **manual** and uses packages from a **prior successful Build** of the tagged commit (no rebuild). Agent checklist: [`.cursor/skills/publish/`](../.cursor/skills/publish/).

1. Merge to `main` and wait for **Build** to succeed on that commit.
2. Ensure [manifest.yml](../manifest.yml) `version` is what you want (e.g. `0.5.1`).
3. Create and push tag `v0.5.1` on that same commit (`v` + manifest version).
4. Confirm **Build** succeeded for that commit.
5. **Actions → Publish → Run workflow** → enter tag `v0.5.1` → approve **`production`** if prompted.
6. Workflow: download `yak-packages` → verify → reject duplicate Yak version → `yak push` → create GitHub Release.

### Verify publication

- GitHub → **Releases** → assets include `MCP4Rhino-<version>.yak`.
- `yak search MCP4Rhino` or Rhino **Package Manager** → search **MCP4Rhino**.

### Troubleshooting

| Problem | What to check |
|---------|----------------|
| Windows/macOS test failed | Matrix job log; NuGet/restore and coverage gate (≥80% Logic/Contracts). |
| Pack failed | Ubuntu “Pack yak” job; Yak download SHA mismatch or `pack-yak.sh` missing outputs. |
| Publish: no Build run | That commit never got a green **Build**. |
| Publish: version already exists | Bump `manifest.yml` (and tag), or `yak yank` the old version. |
| Publish: missing / invalid `YAK_TOKEN` | Secret must be on Environment **`production`** only. |
| Package Manager empty | Confirm `yak search` and the GitHub Release asset; wait for index refresh. |

Local pack: `bash scripts/pack-yak.sh` → `dist/MCP4Rhino-<version>.yak`.
