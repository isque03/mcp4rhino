# Testing

## Coverage gate (CI / local)

**≥80% line coverage** is enforced on:

- `MCP4Rhino.Logic` — extractable business logic (JSON args, geometry builders, code checks, IFC text, QTO/clash, MCP JSON-RPC)
- `MCP4Rhino.Contracts` — shared bridge contracts

Rhino UI / `UiThread` / document facades in `MCP4Rhino.Tools` and the host `.rhp` are **not** in the 80% denominator. Those types carry `[ExcludeFromCodeCoverage]` after logic extraction.

```bash
bash scripts/test-coverage.sh
```

Equivalent:

```bash
dotnet test tests/MCP4Rhino.Tests -c Release
```

(The test project sets Coverlet `Threshold=80` / `ThresholdType=line` with `Include=[MCP4Rhino.Logic]*,[MCP4Rhino.Contracts]*`.)

## What is unit-tested

| Suite | Module |
|-------|--------|
| `JsonArgsTests` | Point/id/tag/color/schema parsing |
| `CodeCheckEngineTests` | IRC/IBC/ADA pre-check heuristics + markdown |
| `MinimalIfcExporterTests` | IFC2x3 text + storey GUID scan |
| `QuantityTakeoffTests` / `BboxClashTests` | Aggregation / AABB clashes |
| `GeometryBuildersTests` | Documents that builders need Rhino natives (excluded from coverage) |
| `UnitConversionTests` | Unit aliases, inch↔feet conversion, document factor JSON |
| `McpJsonRpcTests` | `initialize` / `tools/list` / `tools/call` / errors |

## Rhino integration (manual)

After hot-reload, smoke one facade path:

1. `bash scripts/hot-reload-tools.sh` then `MCP4RhinoReload` (or `mcp4rhino_reload`)
2. Call `create_wall` + `run_code_checks` from an MCP client

Full Rhino.Testing harness for ArchElement/ViewCapture is optional and out of the 80% gate.
