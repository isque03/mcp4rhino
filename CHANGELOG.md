# Changelog

All notable changes to MCP4Rhino are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Package version lives in [manifest.yml](manifest.yml).

## [Unreleased]

### Changed

- `capture_viewport` returns an MCP `image` content block (base64 PNG) plus text metadata with `path`, so sandboxed agents (Claude / Cursor) can see the capture without filesystem access. Text remains `content[0]` for existing clients. Embeds only valid PNGs ≤ 1 MiB raw; larger files stay on disk with `image_omitted_reason`. Read/base64 run off the Rhino UI thread.

### Fixed

- `capture_viewport` default path now uses `ToolHelpers.LogsDir()` (`%LOCALAPPDATA%\MCP4Rhino` on Windows, `~/Library/Logs/MCP4Rhino` on macOS) instead of always appending macOS `Library/Logs` under the user profile.

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
