# Architecture agent capability (MCP4Rhino)

Agents use MCP tools + Cursor skills under [`.cursor/skills/`](../.cursor/skills/) to design residential (IRC) and commercial (IBC) projects in Rhino. Output is for **licensed architect review** — tools never assert legal code compliance.

## Tool phases

| Phase | Module | Capability |
|-------|--------|------------|
| P0 | `CurveFoundationTools` + `SurfaceFoundationTools` + `GeometryFoundationTools` | Curves, NURBS surfaces (see below), extrude, booleans, transforms, layers, blocks, units (`get_document_units`, `set_document_units`, `convert_length`, `measure_size`), `run_rhino_command` |
| P1 | `ArchElementTools` | Levels, walls/slabs/roofs, doors/windows, stairs/ramps, spaces, types, building graph, `measure_distance` / `measure_area` / `measure_clear_width` |
| P2 | `DocSheetTools` | Sections/elevations, dims/tags, sheets, schedules, DWG/PNG export, layer standard |
| P3 | `InteropTools` | Minimal IFC export, OBJ/3DM, links, clash, quantity takeoff |
| P4 | `CodeCheckTools` | `set_code_context`, `run_code_checks`, `get_code_report` (IRC/IBC/ADA heuristics) |

### P0 curve/line tools (Rhino → MCP)

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

### P0 surface / NURBS tools (Rhino → MCP)

Module: `SurfaceFoundationTools`. Inventory based on the Rhino 8 Help command list and Create surfaces topics.

| Rhino command | MCP tool | Phase |
|---------------|----------|-------|
| Plane | `create_plane_surface` | P0 |
| SrfPt | `create_srf_pt` | P0 |
| PlanarSrf | `create_planar_surface` | P0 |
| EdgeSrf | `create_edge_surface` | P0 |
| ExtrudeCrv | `extrude_curve` | P0 (Geometry) |
| ExtrudeCrvAlongCrv | `extrude_curve_along_curve` | P0 |
| Loft | `loft_surface` | P0 |
| Sweep1 | `sweep1_surface` | P0 |
| Sweep2 | `sweep2_surface` | P0 |
| Revolve | `revolve_surface` | P0 |
| RailRevolve | `rail_revolve_surface` | P0 |
| NetworkSrf | `network_surface` | P0 |
| Patch | `patch_surface` | P0 |
| Pipe | `pipe_surface` | P0 |
| FilletSrf | `fillet_surfaces` | P0 |
| BlendSrf | `blend_surfaces` | P0 |
| ChamferSrf | `chamfer_surfaces` | P0 |
| OffsetSrf | `offset_surface` | P0 |
| Cap | `cap_planar_holes` | P0 |
| Join (surfaces) | `join_surfaces` | P0 |
| Trim / Split (srf) | `trim_surface` / `split_surface` | P0 |
| DupBorder | `dup_border` | P0 |
| DupEdge | `dup_edge` | P0 |
| ExtractIsoCurve | `extract_isocurve` | P0 |
| (helper) | `list_surface_edges` | P0 |
| MatchSrf, MergeSrf, ExtendSrf, Untrim, ShrinkTrimmedSrf, Rebuild, ChangeDegree, FitSrf, FilletEdge, ExtrudeCrvTapered/ToPoint, SrfPtGrid, Cone/Torus/Ellipsoid, ConnectSrf, ExtractSrf | — | P1 (not yet) |
| VariableFilletSrf, VariableBlendSrf, FilletSrfCrv, SoftEditSrf, Drape, Heightfield, DevLoft, Ribbon/Fin, UnrollSrf, analysis, SubD sweeps | — | P2 / `run_rhino_command` |

Face pick for fillet/chamfer: `face_index_a` / `face_index_b`, or `pick_point_a` / `pick_point_b` (required when the brep has more than one face). Edge pick for blend/dup_edge: `edge_index` from `list_surface_edges`, or `pick_point` (unambiguous nearest). Curve cutters for `trim_surface` / `split_surface` must be planar; use a surface or brep cutter otherwise.

Semantics use Attribute UserText keys `mcp4:*` and document strings `MCP4RHINO_*_JSON`.

### Document units (P0)

Create and transform tools take **naked doubles in the current Rhino document unit system**. They do not parse “32 inches” from the argument list.

| Tool | Role |
|------|------|
| `get_document_units` | Current `unit_system` plus factors such as `model_units_per_inch` / `inches_per_model_unit` |
| `convert_length` | `{ value, from_unit, to_unit? }` → `result` in `to_unit` (default = document). Use `model_units` for create tools. `model_breakdown` is always document-based; `result_breakdown` matches `to_unit` |
| `measure_size` | Object bbox size in model + mm/in/ft/m |
| `measure_distance` | Points `a`/`b`: `distance_*`. Two object ids: `center_distance_*` and `bbox_gap_*` (not `distance_*`) |
| `set_document_units` | Change unit system; `scale_existing: true` scales object geometry (not `mcp4:` tags / level JSON) |

Agent rule: if the user names a unit that differs from `unit_system`, convert before create, then measure to confirm. Details: [CHANGELOG.md](../CHANGELOG.md).

## Skills

See `.cursor/skills/arch-*` — bootstrap, program, massing, envelope, openings, circulation, spaces, sheets, IFC, code checks, accessibility, design-review pack, LOD gate.

**Modeling quality loop** (run before claiming done):

1. `arch-modeling-iterate` — orchestrates build → check → improve
2. `arch-model-coherence-pass` — non-visual levels/tags/roof-bearing/hosts
3. `arch-model-visual-review` — multi-angle `capture_viewport` (use embedded `image` when present), measurements vs brief

Then `arch-lod-gate` → `arch-design-review-pack` for human RA handoff.

## Hot-reload

```bash
bash scripts/hot-reload-tools.sh
# MCP: mcp4rhino_reload
```

## Testing

Pure logic lives in `MCP4Rhino.Logic` (code checks, IFC text, QTO/clash, JSON args, MCP JSON-RPC). The **80% unit-coverage gate** applies to Logic + Contracts only — see [TESTING.md](TESTING.md).
