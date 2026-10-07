# Architecture agent capability (MCP4Rhino)

Agents use MCP tools + Cursor skills under [`.cursor/skills/`](../.cursor/skills/) to design residential (IRC) and commercial (IBC) projects in Rhino. Output is for **licensed architect review** — tools never assert legal code compliance.

## Tool phases

| Phase | Module | Capability |
|-------|--------|------------|
| P0 | `CurveFoundationTools` + `GeometryFoundationTools` | Curves/line tools (see below), extrude, booleans, transforms, layers, blocks, units, `run_rhino_command` |
| P1 | `ArchElementTools` | Levels, walls/slabs/roofs, doors/windows, stairs/ramps, spaces, types, building graph |
| P2 | `DocSheetTools` | Sections/elevations, dims/tags, sheets, schedules, DWG/PNG export, layer standard |
| P3 | `InteropTools` | Minimal IFC export, OBJ/3DM, links, clash, quantity takeoff |
| P4 | `CodeCheckTools` | `set_code_context`, `run_code_checks`, `get_code_report` (IRC/IBC/ADA heuristics) |

### P0 curve/line tools (McNeel → MCP)

| Rhino command | MCP tool |
|---------------|----------|
| Point | `create_point` |
| Line | `create_line` |
| Polyline | `create_polyline` |
| Curve / InterpCrv | `create_curve` |
| Circle | `create_circle` |
| Arc | `create_arc` |
| Ellipse | `create_ellipse` |
| Rectangle | `create_rectangle` |
| Polygon | `create_polygon` |
| Fillet | `fillet_curve` |
| Offset | `offset_curve` |
| Join | `join_curves` |
| Trim | `trim_curve` |
| Split | `split_curve` |
| Explode | `explode_objects` (polycurve, polyline, extrusion, polysurface, block instance) |

Obscure Line variants (e.g. LineThroughPt): use `run_rhino_command`.

Semantics use Attribute UserText keys `mcp4:*` and document strings `MCP4RHINO_*_JSON`.

## Skills

See `.cursor/skills/arch-*` — bootstrap, program, massing, envelope, openings, circulation, spaces, sheets, IFC, code checks, accessibility, design-review pack, LOD gate.

**Modeling quality loop** (run before claiming done):

1. `arch-modeling-iterate` — orchestrates build → check → improve
2. `arch-model-coherence-pass` — non-visual levels/tags/roof-bearing/hosts
3. `arch-model-visual-review` — multi-angle `capture_viewport`, Read PNGs, measurements vs brief

Then `arch-lod-gate` → `arch-design-review-pack` for human RA handoff.

## Hot-reload

```bash
bash scripts/hot-reload-tools.sh
# MCP: mcp4rhino_reload
```

## Testing

Pure logic lives in `MCP4Rhino.Logic` (code checks, IFC text, QTO/clash, JSON args, MCP JSON-RPC). The **80% unit-coverage gate** applies to Logic + Contracts only — see [TESTING.md](TESTING.md).
