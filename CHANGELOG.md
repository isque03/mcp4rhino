# Changelog

All notable changes to MCP4Rhino are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Package version lives in [manifest.yml](manifest.yml).

## [Unreleased]

## [0.6.1-alpha] — 2026-10-22

### Added

- Opt-out anonymous usage telemetry. It is on unless you turn it off from **Usage data…**. MCP instructions do not mention it, and tool calls are not blocked. The default endpoint is `https://events.agenttoolkit.ai/telemetry` (`MCP4RHINO_TELEMETRY_URL` overrides it). Requests send the packaged install token, `net8.0/telemetry-install-token`, as one `X-Api-Key` header. `MCP4RHINO_TELEMETRY_API_KEY` overrides that file. Nothing is sent while both are unset. The token is not part of the URL. Events cover plugin start, MCP session start, and tool name, duration, and success. See [docs/TELEMETRY.md](docs/TELEMETRY.md).

## [0.6.0-alpha] — 2026-10-19

### Added

- Experimental Grasshopper 1 support for Rhino 8: native editable definitions, component discovery, batched graph edits, queued incremental solves, paginated tree inspection, and explicit additive snapshot baking.
- Portable parametric-design agent skill and `gh_help` covering design reasoning, data trees, repair workflows, and array/facade/loft examples.
- Opt-in prerelease distribution through Rhino Package Manager and a GitHub prerelease, with the matching skill ZIP and standalone native smoke runner.

### Testing status

- This alpha is published for licensed native testing. Compilation, pure unit tests, help/recipe contracts, and package validation do not establish native graph, undo, solve, bake, or persistence behavior.
- Initial native target: Rhino 8.35 and its bundled Grasshopper 1 on Windows and macOS. Record results for each platform; stable promotion remains blocked until native acceptance checks pass.
- Install: enable **Include pre-releases** in `PackageManager`, choose `0.6.0-alpha`, install/update, apply, and restart Rhino. Confirm `get_support_info` reports the alpha, then start with `gh_help` and `gh_status`.
- Rollback: uninstall the alpha, explicitly reinstall stable `0.5.3`, disable prerelease selection, and restart. Clearing the prerelease checkbox alone does not downgrade the installed plugin. Use disposable documents; see [the tester checklist](docs/GRASSHOPPER.md#alpha-tester-checklist).

### Changed

- Docs clarify the **MCP4Rhino** menu path: under **Tools** on Windows; top-level menu bar on macOS.

## [0.5.3] — 2026-10-11

### Fixed

- Status dialog labels and buttons are left-aligned (Mac Eto was right-aligning the modal content).

## [0.5.2] — 2026-10-11

### Added

- **MCP4Rhino** menu (RUI) with Status, Start/Stop, Check for updates, and Report issue (under **Tools** on Windows; top-level on macOS).
- Eto **Status** dialog (`MCP4RhinoStatus` interactive / `MCP4RhinoStatusUI` from the menu) showing Running/Stopped, endpoint, log path, and Start/Stop/Copy/Updates/Log/Issue actions.
- Commands `MCP4RhinoUpdates` (opens Package Manager) and `MCP4RhinoReportIssue` (opens GitHub Issues).
- MCP `initialize` `instructions` plus host tool `get_support_info` with Issues URLs and filing tips.
- README Package Manager screenshot updated; Feedback / Issues section.

### Changed

- Package version **0.5.2**; yak packs include `MCP4Rhino.rui`.

## [0.5.1] — 2026-10-11

### Added

- MCP server **auto-starts** on Rhino Idle after plugin load (opt out with `MCP4RHINO_AUTOSTART=0` / `false` / `no`).
- `MCP4RhinoStatus` command prints running state, endpoint, log path, and autostart flag.
- End-user README rewrite (Package Manager first) plus docs: `INSTALL_FROM_SOURCE`, `DEVELOPMENT`, `MCP_TOOLS`, `TROUBLESHOOTING`, `PUBLISHING`, `CONTRIBUTING`.

### Changed

- Yak Package Manager description is user-facing (Claude/Cursor / natural language modeling).
- Plugin `LoadTime` is `AtStartup` with deferred Idle bind (avoids requiring a manual `MCP4Rhino` for first connect).

## [0.5.0] — 2026-10-11

### Added

- Pack-only scripts write one universal `dist/MCP4Rhino-<version>.yak` (`yak build` → `rh8_*-any`; icon + hard-require Logic/host outputs).
- Local smoke scripts `scripts/smoke-local-yak.sh` / `.ps1` pack+install, check archive/install layout, then probe MCP `tools/list`.
- GitHub Actions: **Build** (Windows + macOS tests, ubuntu universal yak artifact) and manual **Publish** (download Build artifact → `yak push` + GitHub Release; `production` / `YAK_TOKEN` only).
- Agent skill [`.cursor/skills/publish/`](.cursor/skills/publish/) for the merge → Build → tag → Publish checklist.
- Quick start installs from the GitHub universal `.yak`; Package Manager after `yak push`; source install under Build from source.
- You can ask whether Rhino allows saving with `check_rhino_license` before calling `export_3dm`. Response fields: `can_save`, `installation_type`, `installation_type_string`, `is_evaluation`, `days_until_expiration` (null when the license does not expire), `saves_left` (always null on RhinoCommon 8.21 — save-count API not exposed), and `message`.
- Unit/coverage tests run on CI without Rhino installed (`DocumentUnitSystem` + pure license snapshots; NuGet RhinoCommon is reference-only).

### Changed

- You can no longer save a `.3dm` via `export_3dm` when Rhino blocks saving (expired evaluation, expired lease, or inactive license seat). The tool returns a clear error; call `check_rhino_license` first.
- You can review Rhino viewports from a sandboxed agent without opening a local file: `capture_viewport` returns text metadata with `path` plus an MCP `image` content block when the file is ≤ 1 MiB raw and starts with a PNG signature. Text stays `content[0]` for existing clients. Larger files stay on disk with `image_omitted_reason: "exceeds_max_bytes"`. Read/base64 run off the Rhino UI thread.

### Fixed

- `capture_viewport` default path now uses `ToolHelpers.LogsDir()` (`%LOCALAPPDATA%\MCP4Rhino` on Windows, `~/Library/Logs/MCP4Rhino` on macOS) instead of always appending macOS `Library/Logs` under the user profile.
- CaptureToBitmap success now requires a non-trivial file and a readable PNG header (same bar as ViewCaptureToFile), so empty/corrupt bitmap saves are not treated as success.

## [0.4.0] — 2026-10-07

### Added

- You can convert spoken sizes into document model units with `convert_length` before any `create_*` call. Example: 32 inches in a Feet document → about 2.667 model units.
- You can read conversion factors from `get_document_units` (`model_units_per_inch`, `inches_per_model_unit`, and related fields).
- You can check object size with `measure_size` (model units plus mm / in / ft / m).
- `measure_distance` and `get_objects` bbox now also return `_in` / `_ft` / `_mm` (and bbox `size_*`) so agents can verify sizes in the units the user named.
- Windows install and hot-reload scripts: `scripts/install-yak.ps1`, `scripts/hot-reload-tools.ps1` (same one-liner flow as macOS).

### Fixed

- `set_document_units` with `scale_existing: true` now uses Rhino `AdjustModelUnitSystem` (scales objects, block definitions, and tolerances). The flag previously reported success but did not scale. Level elevations, element-type dimensions, and `mcp4:*` path/size tags are **not** rewritten — re-tag or recreate semantics after a unit flip if needed.
- Primary solid create tools (`create_box` / `create_sphere` / `create_cylinder`) and several geometry tools now state that length coordinates are **document model units**. Call `get_document_units` or `convert_length` first when the user names inches, feet, or millimeters.

### Changed

- Quick start in README/CLAUDE is script-first (one install command per OS).
- Package paths in docs/scripts no longer hard-code a vendor Application Support folder name; scripts discover `*/Rhinoceros/packages/8.0/MCP4Rhino` or use `RHINO_PACKAGES_DIR`.

### Agent guidance

- Length coordinates on create/transform tools mean the current document unit (Rhino model units). They do not mean the unit the user said out loud. Angles, counts, and scale factors keep their own meanings.
- Workflow: `get_document_units` → `convert_length` when speech ≠ document → create → `measure_size` / `measure_distance` and compare to the brief.
- Skills updated: `arch-project-bootstrap`, `arch-modeling-iterate`, `arch-model-visual-review`.

## [0.3.0] — 2026-03-29

### Added

- Hot-reloadable Rhino 8 MCP plugin (host `.rhp` + `MCP4Rhino.Tools.dll` / `MCP4Rhino.Logic.dll`) on `http://127.0.0.1:4010/mcp`. See [README.md](README.md).
