# Changelog

All notable changes to MCP4Rhino are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Package version lives in [manifest.yml](manifest.yml).

## [Unreleased]

### Added

- You can convert spoken sizes into document model units with `convert_length` before any `create_*` call. Example: 32 inches in a Feet document → about 2.667 model units.
- You can read conversion factors from `get_document_units` (`model_units_per_inch`, `inches_per_model_unit`, and related fields).
- You can check object size with `measure_size` (model units plus mm / in / ft / m).
- `measure_distance` and `get_objects` bbox now also return `_in` / `_ft` / `_mm` (and bbox `size_*`) so agents can verify sizes in the units the user named.

### Fixed

- `set_document_units` with `scale_existing: true` now scales **object geometry** (and absolute tolerance) when the unit system changes. The flag previously reported success but did not scale. Level elevations, element-type dimensions, and `mcp4:*` path/size tags are **not** rewritten — re-tag or recreate semantics after a unit flip if needed.
- Primary solid create tools (`create_box` / `create_sphere` / `create_cylinder`) and several geometry tools now state that length coordinates are **document model units**. Call `get_document_units` or `convert_length` first when the user names inches, feet, or millimeters.

### Agent guidance

- Length coordinates on create/transform tools mean the current document unit (Rhino model units). They do not mean the unit the user said out loud. Angles, counts, and scale factors keep their own meanings.
- Workflow: `get_document_units` → `convert_length` when speech ≠ document → create → `measure_size` / `measure_distance` and compare to the brief.
- Skills updated: `arch-project-bootstrap`, `arch-modeling-iterate`, `arch-model-visual-review`.

## [0.3.0] — 2026-03-29

### Added

- Hot-reloadable Rhino 8 MCP plugin (host `.rhp` + `MCP4Rhino.Tools.dll` / `MCP4Rhino.Logic.dll`) on `http://127.0.0.1:4010/mcp`. See [README.md](README.md).
