# Architecture agent capability (MCP4Rhino)

Agents use MCP tools + Cursor skills under [`.cursor/skills/`](../.cursor/skills/) to design residential (IRC) and commercial (IBC) projects in Rhino. Output is for **licensed architect review** — tools never assert legal code compliance.

## Tool phases

| Phase | Module | Capability |
|-------|--------|------------|
| P0 | `GeometryFoundationTools` | Curves, extrude, booleans, transforms, layers, blocks, units, `run_rhino_command` |
| P1 | `ArchElementTools` | Levels, walls/slabs/roofs, doors/windows, stairs/ramps, spaces, types, building graph |
| P2 | `DocSheetTools` | Sections/elevations, dims/tags, sheets, schedules, DWG/PNG export, layer standard |
| P3 | `InteropTools` | Minimal IFC export, OBJ/3DM, links, clash, quantity takeoff |
| P4 | `CodeCheckTools` | `set_code_context`, `run_code_checks`, `get_code_report` (IRC/IBC/ADA heuristics) |

Semantics use Attribute UserText keys `mcp4:*` and document strings `MCP4RHINO_*_JSON`.

## Skills

See `.cursor/skills/arch-*` — bootstrap, program, massing, envelope, openings, circulation, spaces, sheets, IFC, code checks, accessibility, design-review pack, LOD gate.

## Hot-reload

```bash
bash scripts/hot-reload-tools.sh
# MCP: mcp4rhino_reload
```

## Testing

Pure logic lives in `MCP4Rhino.Logic` (code checks, IFC text, QTO/clash, JSON args, MCP JSON-RPC). The **80% unit-coverage gate** applies to Logic + Contracts only — see [TESTING.md](TESTING.md).
