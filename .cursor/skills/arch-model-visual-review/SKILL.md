---
name: arch-model-visual-review
description: >-
  Reviews MCP4Rhino architectural models for visual cohesion and brief fit by
  capturing multi-angle viewport screenshots, reading the PNGs, and checking
  key measurements. Use when the user asks to review the model, check if it
  looks right, take screenshots, verify against the brief, or before claiming
  modeling is done.
---

# arch-model-visual-review

MCP endpoint: `http://127.0.0.1:4010/mcp` (user must have run `MCP4Rhino`). Prefer running `arch-model-coherence-pass` first.

## Rules

- Restate the user’s brief; if story count / footprint / program is unclear, ask once.
- Never claim legal code compliance or an RA stamp. IRC/IBC notes are findings only.
- Wireframe can look like “missing solids” — prefer Shaded for captures when possible.
- Max **2** fix→re-capture loops unless the user asks for more.

## Known failure modes (flag these)

- Roof level elevation far above wall tops with no intermediate story walls.
- `zoom_extents` / camera framing leftover models (e.g. other projects on other layers).
- Doors/windows without host cuts / openings tags.
- Stairs with no ceiling/landing above the run.
- Units changed without scaling, leaving prior geometry at the wrong scale.

## Procedure

### 1. Restate the goal

Quote program, story count, approximate footprint, and style from the user. Note document units via `get_document_units`.

### 2. Isolate the subject

- Hide unrelated layers with `set_layer_props` (`name` = full path, e.g. `Parthenon::Columns`; **not** `layer`).
- Use `get_building_model` / `query_elements` to get the subject bbox (min/max of tagged elements).

### 3. Capture pack

Save PNGs under the MCP4Rhino logs dir with stable names (macOS `~/Library/Logs/MCP4Rhino/`, Windows `%LOCALAPPDATA%\MCP4Rhino\`).

Optional: `run_rhino_command` with a shaded display script if available; note display mode in the report.

Views (Perspective unless noted):

| Name | How |
|------|-----|
| `review-se.png` | Target model center; camera SE of bbox (outside +Z and +XY) |
| `review-sw.png` | `orbit_view` yaw ~90° (or set_view SW) |
| `review-ne.png` | Opposite corner |
| `review-top.png` | `set_view` high +Z, target center, looking down |
| `review-elev.png` | Camera on primary facade axis, target mid-height |

Workflow tip: `set_view` to a good SE camera → `capture_viewport` with `path` → `orbit_view` for other corners → top via `set_view`. Avoid blind `zoom_extents` if junk geometry is still visible.

### 4. Read every PNG

Use the Read tool on each capture. For each view note:

- What building parts are visible (walls, roof, openings, stair, porch).
- Floating / detached / clipped / empty-story gaps.
- Mismatch vs the restated brief.

### 5. Measure against the brief

Call as needed:

- `measure_size` — object bbox vs named sizes in the brief (use `size_z.inches` / `size_*.feet`, not model numbers alone).
- `measure_distance` — overall span, wall top ↔ roof bottom. Point pairs expose `distance_in` / `distance_ft` / `distance_mm`; two object ids expose `center_distance_*` and `bbox_gap_*`.
- `measure_area` or space tags (`mcp4:area`) — rooms vs brief.
- `measure_clear_width` — primary egress door.

Compare to the brief in **user units**. A Feet document with height `32` is 32 feet, not 32 inches. Cite IRC/IBC only as heuristic findings.

### 6. Verdict

Output exactly one of: `pass` | `needs_fix`.

```markdown
## Visual review: <pass|needs_fix>
**Brief:** …
**Units:** …

| Issue | Evidence | Suggested MCP fix |
|-------|----------|-------------------|
| … | image path + measurement | tool + args sketch |

**Captures:** list of PNG paths
```

### 7. Fix loop

If `needs_fix`: apply MCP fixes (levels, `create_roof`/`create_wall`, hide layers, etc.), re-run failing captures + Read, update the verdict. Stop after 2 loops or on `pass` / user acceptance.
