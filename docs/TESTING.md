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
| `RhinoLicenseStatusTests` | Save-gate allow/deny messages and evaluation classification from license snapshots |
| `TelemetryTests` | Opt-out gate, installation id, event JSON, tool success/failure, batching, `X-Api-Key` header, URL secret stripping, delivery failure, revoke, and payload redaction |
| `TelemetryDiagnosticsTests` / `RhinoCommandNameTests` | Controlled failure mapping, optional-field applicability, generic-command outcomes with unchanged MCP responses, safe leading-command lookup, and diagnostics privacy |

## Telemetry 0.7.1 native acceptance

Use a licensed Rhino installation with the complete candidate package installed and Rhino restarted; Tools-only hot reload cannot update the shared Contracts types. Confirm `get_support_info` reports `0.7.1`.

In a disposable document, exercise a harmless successful generic command, a failed command, a prefixed command, an unresolved alias, and a macro with more than one command. Verify the leading canonical English command name through RhinoCommon, and test a localized command when that language is available. Compare MCP responses with their previous shape, including the existing `script` field, while confirming scripts and paths never enter telemetry.

Use the excluded synthetic installation ID from the backend handoff for delivery checks. Require empty HTTP 204 and persisted `error_code`, `error_type`, `error_stage`, and `rhino_command_name` values in BigQuery. Inspect `rhino-events errors`, `commands`, and `recent` for that installation. Record native results separately from Windows/macOS CI; unit tests cannot prove native command lookup or execution.

## Rhino integration (manual)

After hot-reload, smoke one facade path:

1. `bash scripts/hot-reload-tools.sh` then `MCP4RhinoReload` (or `mcp4rhino_reload`)
2. Call `create_wall` + `run_code_checks` from an MCP client
3. Units smoke: `set_document_units` Feet → `convert_length` 32 in → create → `measure_size` (~32 in); with `scale_existing: true`, flip Inches→Feet and confirm geometry height ≈ /12
4. Viewport capture (after Tools/Logic hot-reload): `capture_viewport` at default size usually returns text with `image_embedded: true` then `type: "image"` / `mimeType: "image/png"`. Confirm the model can describe the view without reading the file path. If omitted: text has `image_omitted_reason: "exceeds_max_bytes"` (no image block). Invalid PNG fails the call.
5. License save gate: `check_rhino_license` returns `can_save` plus installation/eval fields. When `can_save` is false, `export_3dm` must fail with the same clear license/evaluation message and must not write a file. When `can_save` is true, `export_3dm` should write successfully.

Full Rhino.Testing harness for ArchElement/ViewCapture is optional and out of the 80% gate.

## Grasshopper 0.7.0

`McpProtocolTests`, `McpProtocolValidationTests`, and `McpHttpIntegrationTests` cover the 2024/2025/2026 profiles, required headers, negotiation, errors, Origin/Host checks, legacy behavior and concurrent requests over real local HTTP. `McpResourceTests` and `McpPromptTests` cover every route/catalog, privacy, byte/item limits, identity scope helpers, cursor validation, and static reads/prompts that cannot reach tools or native gateways. See [native resource acceptance](MCP_RESOURCES_PROMPTS.md#validation-and-migration) for licensed checks and smoke-runner flags.

`GrasshopperInspectionTests` covers result/revision guards, unique named selections, bounded report pagination, canvas framing validation, MCP image limits, and capture association contracts. Recipe tests validate all packaged examples against the released schemas and support manifest, including multi-patch recipes.

`GrasshopperSdkContractTests` inspects the pinned SDK's metadata without loading Grasshopper. It verifies the guarded canvas-render binding and solver-gate fields used to prevent implicit solutions during capture/activation. A different installed SDK still needs native acceptance; incompatible bindings fail explicitly.

Native checks use `scripts/grasshopper-smoke.py --example profile` and `--example stair --capture` on a licensed Rhino installation with the matching package installed and Rhino restarted. They exercise native point ordering, separate closed profiles, capped solids, parameter changes, stale results, rollback, request retries, canvas capture and save/reopen. `--bake` adds snapshots; `--skip-save` explicitly leaves persistence unverified. Record Windows/macOS results separately and manually verify hidden-editor capture, view preservation, manual-edit invalidation, and native undo/redo. CI cannot establish those behaviors.
