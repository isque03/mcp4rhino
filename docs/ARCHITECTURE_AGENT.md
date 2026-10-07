# Architecture agent capability (MCP4Rhino)

Agents use MCP tools + Cursor skills under [`.cursor/skills/`](../.cursor/skills/) to design residential (IRC) and commercial (IBC) projects in Rhino. Output is for **licensed architect review** — tools never assert legal code compliance.

## Tool phases

| Phase | Module | Capability |
|-------|--------|------------|
| P0 | `CurveFoundationTools` + `SurfaceFoundationTools` + `GeometryFoundationTools` | Curves, NURBS surfaces (see below), extrude, booleans, transforms, layers, blocks, units, `run_rhino_command` |
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

### P0 surface / NURBS tools (McNeel → MCP)

Module: `SurfaceFoundationTools`. Inventory based on [Rhino 8 command list](https://docs.mcneel.com/rhino/8/help/en-us/commandlist/command_list.htm) and [Create surfaces](https://docs.mcneel.com/rhino/8/help/en-us/seealso/sak_surface.htm).

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

Edge pick for fillet/blend/dup_edge: `edge_index` from `list_surface_edges`, or `pick_point` (unambiguous nearest).

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
