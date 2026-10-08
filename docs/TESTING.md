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
| `CurveOpsTests` / `SurfaceOpsTests` | Curve/surface pure-logic helpers |
| `McpJsonRpcTests` | `initialize` / `tools/list` / `tools/call` / errors |
| `McpContentTests` | MCP text/image content builders; PNG signature validation; image embed helpers |

## Rhino integration (manual)

After hot-reload, smoke one facade path:

1. `bash scripts/hot-reload-tools.sh` then `MCP4RhinoReload` (or `mcp4rhino_reload`)
2. Call `create_wall` + `run_code_checks` from an MCP client
3. Units smoke: `set_document_units` Feet → `convert_length` 32 in → create → `measure_size` (~32 in); with `scale_existing: true`, flip Inches→Feet and confirm geometry height ≈ /12
4. Viewport capture (after Tools/Logic hot-reload): `capture_viewport` at default size usually returns text with `image_embedded: true` then `type: "image"` / `mimeType: "image/png"`. Confirm the model can describe the view without reading the file path. If omitted: text has `image_omitted_reason: "exceeds_max_bytes"` (no image block). Invalid PNG fails the call.

Full Rhino.Testing harness for ArchElement/ViewCapture is optional and out of the 80% gate.
