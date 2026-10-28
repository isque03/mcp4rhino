# CI and publishing (maintainers)

Publish through GitHub Actions: **Build** produces an artifact, then manual **Publish** distributes that exact artifact to McNeel Yak and GitHub Releases. Do not rebuild or run `yak push` from a workstation. Build has no package-publishing secrets. Its pack job reads the repository Actions secret `MCP4RHINO_TELEMETRY_API_KEY` and writes that install token into the yak. Push builds fail when the secret is missing. Pull-request builds omit the file when the secret is unavailable.

## Release sources

| Release | Source / workflow ref | Tag example | GitHub status |
|---|---|---|---|
| Stable | Merged `main` commit; dispatch on `main` | `v0.7.3` | Stable / latest |
| Grasshopper alpha | `release/0.6.0-alpha`, initially from `feat/grasshopper1` at `37d9691` | `v0.6.0-alpha` | Prerelease / not latest |
| Telemetry alpha | `release/0.6.1-alpha` | `v0.6.1-alpha` | Prerelease / not latest |
| Grasshopper tools alpha | `release/0.6.2-alpha` | `v0.6.2-alpha` | Prerelease / not latest |
| General Grasshopper tooling alpha | `release/0.6.4-alpha` | `v0.6.4-alpha` | Prerelease / not latest |
| Agent-guidance balance alpha | `release/0.6.5-alpha` | `v0.6.5-alpha` | Prerelease / not latest |
| Component extensibility alpha | `release/0.7.2-alpha` | `v0.7.2-alpha` | Prerelease / not latest |

Configured alphas, including `0.7.2-alpha` and numbered successors such as `0.7.2-alpha.1`, publish from their matching release branch. They are public and opt-in; successful Windows/macOS CI is the prerelease publication gate. Preserve published alpha tags and assets.

The user confirmed the separate-machine alpha workflow and authorized stable `0.7.3` on October 28, 2026. The tested OS and Rhino build were not supplied; other native scenarios remain unverified. Merge the promotion into `main`, obtain fresh push CI and publish a new stable artifact. Preserve all published alpha tags and assets.

## Build and versions

Build runs for pull requests and pushes to `main`, `release/0.6.0-alpha`, `release/0.6.1-alpha`, `release/0.6.2-alpha`, `release/0.6.4-alpha`, `release/0.6.5-alpha`, or `release/0.7.2-alpha`:

1. Build/test on Windows and macOS; enforce at least 80% Logic/Contracts line coverage.
2. Validate release contracts, source-version consistency, canonical skill/help and example recipes.
3. Pack a universal Rhino 8 package on Linux, excluding RhinoCommon, Grasshopper and GH_IO SDK DLLs.
4. Upload `yak-packages` for 14 days, containing `MCP4Rhino-<version>.yak`, `MCP4Rhino-<version>-skills.zip`, `grasshopper-smoke.py`, and `grasshopper-compatibility-smoke.py`.

Keep manifest, all five application project versions, and MCP server version synchronized. Assembly/file versions stay numeric (`0.7.3.0` for stable 0.7.3); informational versions match the package version (with optional Git metadata).

Local checks:

```bash
python3 -m unittest discover -s tests/release -p 'test_*.py'
python3 scripts/release_checks.py source-versions
dotnet build MCP4Rhino.sln -c Release
bash scripts/test-coverage.sh
bash scripts/pack-yak.sh
```

Use PowerShell equivalents for .NET coverage and local packaging on Windows. A local package is useful for inspection; publication always uses the CI artifact. The [tester checklist](GRASSHOPPER.md#alpha-tester-checklist) covers installation, restart, automated/native tests and rollback.

## Publish stable 0.7.3

After the promotion is merged, use the exact successful push Build SHA on `main` (a clean `main` checkout is shown):

```bash
VERSION="$(awk '/^version:/ { print $2; exit }' manifest.yml)"
SHA="$(git rev-parse HEAD)"
python3 scripts/release_checks.py metadata "v$VERSION" refs/heads/main
# Wait for a successful push Build for THIS SHA, not a PR merge build.
gh run list --workflow=build.yml --branch main --event push --commit="$SHA" --status=success
bash scripts/ci-download-build-artifact.sh "$SHA" artifacts/release-candidate main
bash scripts/verify-yak-package.sh "artifacts/release-candidate/MCP4Rhino-$VERSION.yak" "$VERSION"
git tag -a "v$VERSION" -m "MCP4Rhino $VERSION"
git push origin "v$VERSION"
gh workflow run publish.yml --ref main -f tag="v$VERSION"
```

Use an empty download destination each time. The tag must identify the exact green SHA, be annotated, and remain immutable. Publish checks the dispatch branch, tag/manifest/project/MCP versions, source ancestry, exact successful push Build, available artifact, and embedded package manifest version. Expired/missing artifacts require rerunning Build on the same SHA before publishing.

Publish searches all Yak versions including prereleases and rejects exact duplicates. Registry query, token, or authentication failures stop publication. The existing environment is **production** with **YAK_TOKEN** as an environment-only secret. The telemetry install token is a separate repository Actions secret, `MCP4RHINO_TELEMETRY_API_KEY`, consumed by Build rather than by Publish. If required reviewers are configured, a human must approve the environment in GitHub. Never log, paste or commit tokens.

Stable publication uses `prerelease: false` and `make_latest: true`. Version `0.7.3` requires a fresh successful push Build on the merged `main` SHA and an annotated `v0.7.3` tag on that SHA. Publish the newly built stable artifact; never relabel an alpha archive or move its tag. Alpha publication continues to use its matching release branch with `prerelease: true` and `make_latest: false`; the alpha branch cannot publish stable packages.

## Verify distribution

```bash
gh release view v0.7.3 --json tagName,isPrerelease,assets,url
# Read-only checks using the Yak executable shipped with Rhino:
yak search MCP4Rhino --all --prerelease
yak search MCP4Rhino
gh api repos/isque03/mcp4rhino/releases/latest --jq .tag_name
```

Expect all four release assets, `isPrerelease: false`, `0.7.3` in the default Yak search, and `v0.7.3` as GitHub latest stable. Allow Yak index refresh before retrying read-only checks. Install/update through Package Manager and fully restart Rhino, then confirm `server_version: 0.7.3` with `get_support_info`. Prerelease selection is not required. Publication and compilation do not establish additional native acceptance results.

## Failure recovery

- **CI failure:** fix it on the release branch, push, and obtain a green Build before tagging.
- **Artifact unavailable:** rerun Build on the same tagged SHA. Never use another SHA or local output.
- **Yak duplicate:** do not rerun the push, reuse the version, or move the tag. Use the next version and a new tag.
- **Yak succeeded, GitHub release failed:** recover only GitHub metadata/assets from the exact original Build artifact. Verify Yak already has that version; create/repair the GitHub release with matching stable/prerelease and latest settings, the existing tag, and the four CI assets. Do not repush Yak. Copy the tester instructions and known limitations from the tagged guide/changelog into the repaired release.
- **Missing token / environment approval:** resolve it through GitHub environment settings; never expose the token or bypass the environment.
- **Broken alpha:** document the failure and provide the explicit `0.7.1` rollback steps. Publish the next numbered alpha for fixes. Yanking is a separate maintainer action, not automatic rollback; yanked versions cannot be re-pushed.

Stable and prerelease versions use the same package/plugin identity. Prereleases are not private and do not install beside stable. Clearing the prerelease checkbox alone does not downgrade an installed alpha.
