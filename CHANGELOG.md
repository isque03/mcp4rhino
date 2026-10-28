# Changelog

All notable changes to MCP4Rhino are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Package version lives in [manifest.yml](manifest.yml).

## [Unreleased]

## [0.7.2-alpha] — 2026-10-28

### Added

- Opt-in prerelease for separate-machine testing. Stable 0.7.2 publication waits for explicit user confirmation.
- Installed component categories: verified, available and restricted, with exact assembly identities and explicit authorization metadata. The verified manifest and legacy support flag remain compatible.
- Local `MCP4RHINO_GH_TRUSTED_ASSEMBLIES` configuration for reviewed available assemblies; known scripting, cluster, timer and external-I/O types remain restricted. Discovery alone grants no execution permission.
- In-place slider-only patches for existing mixed definitions, retaining unrelated object instances, ports and wires, targeted undo/redo, raw slider state and full accuracy modes.
- Component policy/slider planning tests, compatibility smoke runner, architecture review and Windows/macOS native acceptance matrix.

### Changed

- Structural edits require authorization before serialization and validate cloned graph identities. Archive loading rejects warnings/errors rather than accepting potentially incomplete definitions.
- Patch/undo/load operations suppress implicit solutions; undo/redo requires a subsequent explicit solve. Authorized solves retain native preview behavior.
- Native 0.7.2 discovery, plugin graphs, solve/bake, persistence and undo/redo remain unverified until licensed platform acceptance runs succeed. Build and pure tests do not establish these behaviors.

## [0.7.1] — 2026-10-28

### Added

- Anonymous tool-failure diagnostics: controlled error codes, exception categories, and known failure stages in the existing usage-data events.
- Canonical English names for recognized leading commands in `run_rhino_command`. Scripts, arguments, paths, error messages, and stack traces are never included in telemetry.

### Fixed

- `run_rhino_command` telemetry now records a failure when Rhino's `RunScript` returns `false`, while preserving the existing MCP response. This is the first release with corrected generic-command success measurement; earlier success rates may be inflated and historical diagnostics cannot be reconstructed.

## [0.7.0] — 2026-10-27

### Added

- `grasshopper_getting_started` prompt and `mcp4rhino://server/grasshopper-getting-started` walkthrough for a first parametric session.
- Tutorial guidance to show viewport/canvas captures in the chat harness and to record completion in workspace `.mcp4rhino/tutorial-memory.md`.

### Changed

- `getting_started` now names Grasshopper as a first-class next lane and points at the Grasshopper tutorial.
- Stable package version `0.7.0` (includes prior 0.6.x alpha tooling, resources, prompts, and agent-guidance balance).

## [0.6.5-alpha] — 2026-10-27

### Added

- Opt-in `getting_started` prompt and `mcp4rhino://server/getting-started` walkthrough (orient → practice box → measure/capture → next lane). Asks before creating geometry.
- `mcp4rhino://server/writing-style` with pragmatic clear-English rules (ASD-STE100 style, not full compliance or dictionary).

### Changed

- MCP `instructions` now map document, geometry, architecture, drawings, analysis, code-check, and Grasshopper lanes so agents do not treat Grasshopper as the default path. New-user and writing-style pointers are included.
- `mcp4rhino://server/capabilities` includes a compact `lanes` map (including `getting_started`) with exemplar tools and lane guidance.
- General prompts (`getting_started`, `inspect_model`, `prepare_model_report`) start from the active-document resource; Grasshopper prompts keep the GH guide index. Prompt descriptions name Grasshopper when the workflow is parametric.
- `gh_help` description refers to Grasshopper parametric-design help for this package, not “this server” as a whole.
- Package, assembly, MCP version, and CI prerelease provenance checks support `0.6.5-alpha` on `release/0.6.5-alpha`.

## [0.6.4-alpha] — 2026-10-27

### Added

- Read-only resources and URI templates for active Rhino document context, Grasshopper sessions/graphs/nodes/components/results, embedded guides and versioned recipes. Reads preserve tree paths, redact text/paths, enforce active-document scope and reject stale results.
- Seven generic workflow prompts with validated arguments and no native actions during retrieval.
- MCP 2026-07-28 discovery/metadata and Streamable HTTP JSON responses, 2025-11-25 initialization support, and retained 2024-11-05 POST compatibility. Includes Origin/Host validation, request-local protocol context, `/health`, bounded requests and resource responses.
- Generic native Grasshopper support for Polyline, Join Curves, Weave, Merge, Shift List, Insert Items, Reverse List, List Length, Entwine, Boundary Surfaces, and Cap Holes. Component GUIDs and port contracts remain installation-discovered.
- `gh_get_solution_summary` for revision-bound controls, persistent-input samples, named output pages, units, and diagnostics. Geometry inspection includes curve closedness/planarity/self-intersection/length, Brep area/solid volume, and explicit unsupported measurements.
- `gh_capture_canvas` for native graph PNGs with explicit framing and bounded MCP image content; `gh_show_document` for deliberate editor activation. `capture_viewport` accepts optional guarded Grasshopper preview context.
- Generic ordered-profile and capped-extrusion fixture, plus a native stair-study example exercising the same tools. Stair calculations live only in the example graph; there is no staircase tool or compliance check.
- Multi-patch recipe smoke checks, capture options, and explicit reporting when save/reopen checks are skipped.
- Explicit whole-graph `layout` patch operation with revision checks and the batch's existing undo/rollback behavior.

### Changed

- Unsupported persistent-value errors explain how to connect typed native constant parameters to Generic Data inputs.
- Document activation suppresses the native canvas setter's implicit solve, preserving inspection-only open behavior and current result handles.
- Package, assembly, MCP version, and CI prerelease provenance checks now support `0.6.4-alpha` on `release/0.6.4-alpha`.

### Testing status

- Native rendering, geometry, manual-edit/undo behavior, baking and persistence require licensed macOS/Windows acceptance. The local Rhino installation cannot load Grasshopper and reports saving unavailable; automated builds and unit tests do not establish native runtime behavior.
- Persistent adapter, host and contract changes require a full package install and Rhino restart.

## [0.6.2-alpha] — 2026-10-22

### Removed

- `execute_csharp`. It was registered but always failed, and assistants treated that failure as a reason to script Grasshopper through `run_rhino_command` and Python. Parametric work uses the `gh_*` tools (`gh_help`, `gh_status`, `gh_search_components`, `gh_apply_patch`, `gh_solve`, `gh_bake`).

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
