---
name: publish
description: >-
  Releases MCP4Rhino to McNeel Yak Package Manager and GitHub Releases via
  GitHub Actions. Use when the user asks to publish, release, yak push, cut a
  version, tag a release, or run the Publish workflow.
---

# Publish MCP4Rhino

Use **Build** → exact CI artifact → manual **Publish**. Read [docs/PUBLISHING.md](../../../docs/PUBLISHING.md) for commands and failure recovery.

## Release policy

- Stable versions publish only from a merged `main` commit. Tag after merge and green push CI; dispatch Publish with `--ref main`.
- Explicit exception: `0.6.0-alpha` and numbered successors (`0.6.0-alpha.1`, etc.) publish from `release/0.6.0-alpha`. `0.6.1-alpha` and numbered successors publish from `release/0.6.1-alpha`. `0.6.2-alpha` and numbered successors publish from `release/0.6.2-alpha`. Dispatch with that exact branch ref. Do not merge these alphas into `main` to publish them.
- Green Windows/macOS CI, the 80% Logic/Contracts coverage gate, release-contract tests, and package checks gate alpha publication. Licensed native tests happen afterward; native Windows/macOS acceptance gates stable promotion.
- Tag is `v` + manifest version and must be annotated, point to the exact successful push Build SHA, and be reachable from the allowed source branch. Never move/reuse a published tag.
- Manifest, five application project versions, and MCP-reported version must agree. Assembly/file versions are numeric; informational versions preserve the prerelease suffix.
- Publish only the available `yak-packages` artifact from a successful push Build on the exact SHA and source branch. A PR merge build, another SHA, expired artifact, or local rebuild is not a substitute.
- Never rebuild or run `yak push` locally during publication. Publishing secrets exist only in GitHub Environment **production** as **YAK_TOKEN**. Never print, paste, or commit tokens.
- Keep package/plugin identity and endpoint unchanged: **MCP4Rhino**, **4010**.

## Procedure

1. Verify clean checkout, release source, synchronized version and green exact-SHA CI. Inspect the downloaded package and companion skill/smoke assets.
2. Push an annotated version tag on that same SHA.
3. Dispatch `publish.yml` with the allowed `--ref` and matching `tag` input. Watch that run. If the environment requires approval, ask the human to approve it in GitHub; do not approve on their behalf.
4. Verify Yak prerelease/default searches, all three GitHub assets, and GitHub metadata. Alphas must have `prerelease: true`, `make_latest: false`; `v0.5.3` remains latest stable during alpha testing.
5. Give testers the tagged [Grasshopper checklist](../../../docs/GRASSHOPPER.md#alpha-tester-checklist): enable prereleases, select the alpha, apply and restart Rhino, check `get_support_info`, `gh_help`, `gh_status`, then run native scenarios. Record Windows/macOS separately.

## Failures and follow-up

- Registry search, authentication, missing artifact, version mismatch, and duplicate checks fail closed. Do not publish around them.
- If Yak succeeds but GitHub creation fails, repair only the GitHub prerelease/assets from the same CI artifact and tag. Do not repush Yak or move the tag.
- Each fix gets a new version/tag; follow `alpha` with `alpha.1`, `alpha.2`, etc. Apply fixes on the release branch and cherry-pick relevant implementation fixes to `feat/grasshopper1`.
- Rollback is explicit: uninstall alpha, reinstall stable **0.5.3**, disable prereleases, restart Rhino. Clearing the checkbox alone does not downgrade.
- Yanking requires a separate explicit request. Final stable release and merging Grasshopper into `main` are separate decisions.
