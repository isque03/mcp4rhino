# MCP4Rhino

[github.com/isque03/mcp4rhino](https://github.com/isque03/mcp4rhino)

MCP4Rhino is a Rhino 8 plugin. It hosts a local **MCP** (Model Context Protocol) HTTP server. AI agents such as Claude Desktop and Cursor can inspect and control the active Rhino document.

> If you use Claude or a similar agent with this repository, start with [`CLAUDE.md`](CLAUDE.md).

## Quick start (macOS)

**Prerequisites:** [Rhino 8](https://www.rhino3d.com/), [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js](https://nodejs.org/) (`npx` for MCP clients).

```bash
git clone https://github.com/isque03/mcp4rhino.git
cd mcp4rhino
bash scripts/install-yak.sh
```

This command builds the solution, packs a yak package (`MCP4Rhino` **0.3.0**), and installs the package into the Rhino package folder.

Then [start the server in Rhino](#start-in-rhino).

## Quick start (Windows 11)

**Prerequisites:** [Rhino 8 for Windows](https://www.rhino3d.com/), [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js](https://nodejs.org/) (`npx` for MCP clients).

In **PowerShell**:

```powershell
git clone https://github.com/isque03/mcp4rhino.git
cd mcp4rhino
dotnet build MCP4Rhino.sln -c Release

$yak = "C:\Program Files\Rhino 8\System\Yak.exe"
$stage = Join-Path $env:TEMP ("mcp4rhino-yak-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Force -Path "$stage\net8.0" | Out-Null

$hostOut = "src\MCP4Rhino\bin\Release\net8.0"
$toolsOut = "src\MCP4Rhino.Tools\bin\Release\net8.0"
Copy-Item "$hostOut\MCP4Rhino.rhp" "$stage\net8.0\"
Copy-Item "$hostOut\MCP4Rhino.Contracts.dll" "$stage\net8.0\"
Copy-Item "$toolsOut\MCP4Rhino.Tools.dll" "$stage\net8.0\"
Get-ChildItem "$toolsOut\*.dll" | Where-Object {
  $_.Name -notin @("RhinoCommon.dll","Rhino.UI.dll","Eto.dll")
} | Copy-Item -Destination "$stage\net8.0\" -Force
Copy-Item "manifest.yml" $stage

Push-Location $stage
& $yak build --platform win
& $yak install (Get-ChildItem *.yak | Select-Object -First 1).FullName
& $yak list
Pop-Location
```

Package install location (typical): `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP4Rhino\`.

Then [start the server in Rhino](#start-in-rhino).

## Quick start (Linux)

Rhino 8 has **no official Linux desktop** ([system requirements](https://www.rhino3d.com/8/system-requirements/)). MCP4Rhino needs a running Rhino UI process.

**Recommended path:**

1. Run [Rhino 8 for Windows](https://www.rhino3d.com/) in a **Windows 11 VM** or on a remote Windows host.
2. Follow the [Windows 11](#quick-start-windows-11) quick start on that host.
3. Point your Linux MCP client at `http://<windows-host>:4010/mcp`.
4. Open the firewall for that port if the client cannot connect.

**Unsupported community path:** If you already run Rhino under Wine, use the Windows install steps in that prefix (`Yak.exe` / `Rhino.exe` under `drive_c/Program Files/Rhino 8/...`). This path is not supported.

**Build-only on Linux** (builds a `.yak` for a Mac or Windows Rhino host; does not start the MCP server):

```bash
git clone https://github.com/isque03/mcp4rhino.git
cd mcp4rhino
dotnet build MCP4Rhino.sln -c Release
# Optional: download standalone yak for linux-x64 from McNeel’s yak releases,
# stage net8.0/ + manifest.yml like scripts/install-yak.sh, then:
#   ./yak build --platform win   # or --platform mac
# Copy the .yak to the Rhino machine and: yak install ./mcp4rhino-*.yak
```

## Start in Rhino

After you install the package on a supported host:

1. **Restart Rhino** after install or after host `.rhp` changes.
2. Confirm a load message such as: `MCP4Rhino … loaded. Run MCP4Rhino to start…`
3. Click the **command line** above the viewports. Do not use the Help search box.
4. Run `MCP4Rhino`.
5. Confirm: `MCP server started: http://localhost:4010`.
6. Use this MCP endpoint: `http://127.0.0.1:4010/mcp`.

## Connect a client

**Claude Desktop** — merge [`examples/claude_desktop_config.json`](examples/claude_desktop_config.json):

```json
{
  "mcpServers": {
    "mcp4rhino": {
      "command": "npx",
      "args": ["mcp-remote", "http://localhost:4010/mcp"]
    }
  }
}
```

**Cursor and other clients** — connect with `npx mcp-remote http://localhost:4010/mcp`, or use the client HTTP MCP equivalent. If the client runs on Linux and Rhino runs in a Windows VM, replace `localhost` with the Windows host IP.

## Run tests

Unit tests + **≥80% line coverage** on `MCP4Rhino.Logic` / `MCP4Rhino.Contracts` (Rhino UI facades excluded):

```bash
bash scripts/test-coverage.sh
```

Details: [docs/TESTING.md](docs/TESTING.md).

## Run tests

Unit tests + **≥80% line coverage** on `MCP4Rhino.Logic` / `MCP4Rhino.Contracts` (Rhino UI facades excluded):

```bash
bash scripts/test-coverage.sh
```

Details: [docs/TESTING.md](docs/TESTING.md).

## Verify

With Rhino open and `MCP4Rhino` already started:

```bash
curl -sS -X POST http://127.0.0.1:4010/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

PowerShell:

```powershell
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:4010/mcp `
  -ContentType 'application/json' `
  -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

Or call tool `get_document_info` from your MCP client.

**Logs:** macOS `~/Library/Logs/MCP4Rhino/mcp4rhino.log` · Windows `%LOCALAPPDATA%\MCP4Rhino\mcp4rhino.log`

## Daily loop (tools only)

For Tools or Logic changes, rebuild, copy the DLLs, then hot-reload. Do not restart Rhino for tools-only changes.

**macOS**

```bash
bash scripts/hot-reload-tools.sh
# then in Rhino: MCP4RhinoReload
# or MCP tool: mcp4rhino_reload
```

The script copies `MCP4Rhino.Tools.dll` and `MCP4Rhino.Logic.dll` into the installed package.

**Windows**

1. Rebuild `src\MCP4Rhino.Tools\MCP4Rhino.Tools.csproj` in Release.
2. Copy `MCP4Rhino.Tools.dll` and `MCP4Rhino.Logic.dll` into `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP4Rhino\<version>\net8.0\`.
3. Copy new dependency DLLs from the Tools build output if needed.
4. Run `MCP4RhinoReload` or call `mcp4rhino_reload`.

After host plugin (`.rhp`) changes, restart Rhino, then run `MCP4Rhino` again.

## Commands and environment

| Item | Meaning |
|------|---------|
| `MCP4Rhino` | Start the MCP HTTP server |
| `MCP4RhinoStop` | Stop the server |
| `MCP4RhinoReload` | Hot-reload `MCP4Rhino.Tools.dll` |
| `MCP4RHINO_PORT` | Listen port (default **4010**) |
| `MCP4RHINO_TOOLS_PATH` | Override directory containing `MCP4Rhino.Tools.dll` |

## Features

MCP4Rhino provides:

- A yak package with a host `.rhp` and hot-reloadable `MCP4Rhino.Tools.dll` (plus `MCP4Rhino.Logic.dll`)
- Curve, surface, solid, tag, group, view, and viewport-capture tools
- The `mcp4rhino_reload` tool so you can iterate without a Rhino restart
- Architecture-agent tools and skills (phases P0–P4) under [`.cursor/skills/`](.cursor/skills/) — see [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md)

Semantic tags use `mcp4:` user-text keys on model objects.

## MCP tools

Core document and view tools:

| Tool | Description |
|------|-------------|
| `get_document_info` | Units, layers, counts, selection |
| `get_document_units` | Unit system, tolerances, and conversion factors (`model_units_per_inch`, …) |
| `convert_length` | Convert a named unit (in/ft/mm/…) into document model units |
| `measure_size` | Bounding-box size of one object in model units plus mm/in/ft/m |
| `get_objects` | List or filter objects by tag, group, layer, or name (bbox includes `size_in` / `size_ft` / `size_mm`) |
| `create_box` / `create_sphere` / `create_cylinder` | Add solids (**sizes are document model units**) |
| `delete_objects` | Delete by GUID list |
| `set_user_text` / `get_user_text` | Object key/value tags |
| `set_object_name` / `set_object_layer` | Rename or change layer |
| `list_groups` / `create_group` / `add_to_group` | Group membership |
| `list_views` | Viewports and camera summaries |
| `get_view` | Full camera state (`view` optional; default is the active view) |
| `set_active_view` | Activate a view by name (`Perspective`, `Top`, …) |
| `set_view` | Set absolute `camera` and `target` (optional `up`) |
| `orbit_view` | Relative yaw and pitch in degrees around the target |
| `pan_view` | Screen-space pan (`right` / `up` in model units) |
| `zoom_view` | Dolly toward or away from the target (`factor` > 1 zooms in) |
| `zoom_extents` | Fit all objects (optional `ids`) |
| `capture_viewport` | PNG screenshot (`width`/`height`/`view`/`path`; macOS uses ViewCaptureToFile) |
| `run_rhino_command` | Scripted Rhino command escape hatch |
| `mcp4rhino_reload` | Hot-reload Tools and Logic without a Rhino restart |

**Geometry P0 (curves and solids)** — also listed in [`CLAUDE.md`](CLAUDE.md):

- Create: `create_point`, `create_line`, `create_polyline`, `create_circle`, `create_arc`, `create_ellipse`, `create_polygon`, `create_rectangle`, `create_curve`
- Edit: `join_curves`, `trim_curve`, `split_curve`, `explode_objects`, `offset_curve`, `fillet_curve`, `extrude_curve`
- Other: boolean tools, `transform_objects`, layers, blocks, `set_document_units` / `get_document_units`, `convert_length`, `measure_size`

**Document units:** every create/edit size argument is in the **current document unit system** (same as Rhino). If the user says “32 inches” and the file is in Feet, call `convert_length` first, then create, then verify with `measure_size` or `measure_distance` (`distance_in`). See [CHANGELOG.md](CHANGELOG.md).

**Surfaces P0** — full Rhino-command map in [`docs/ARCHITECTURE_AGENT.md`](docs/ARCHITECTURE_AGENT.md):

- Create: `create_plane_surface`, `create_srf_pt`, `create_planar_surface`, `create_edge_surface`, `loft_surface`, `sweep1_surface`, `sweep2_surface`, `revolve_surface`, `rail_revolve_surface`, `network_surface`, `patch_surface`, `pipe_surface`, `extrude_curve_along_curve`
- Edit: `fillet_surfaces`, `blend_surfaces`, `chamfer_surfaces`, `offset_surface`, `trim_surface`, `split_surface`, `join_surfaces`, `cap_planar_holes`
- Edges: `list_surface_edges`, `dup_border`, `dup_edge`, `extract_isocurve`

Later phases (see [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md)):

| Phase | Examples |
|-------|----------|
| Architecture P1 | `create_level`, `create_wall`, `create_slab`, `create_door`, `create_window`, `create_stair`, `create_space`, `get_building_model` |
| Documentation P2 | Sections, elevations, dims, tags, sheets, schedules, `export_dwg`, `export_images` |
| Interop P3 | `export_ifc`, `clash_detect`, `quantity_takeoff`, links |
| Code P4 | `set_code_context`, `run_code_checks`, `get_code_report` (pre-check findings only) |

Project skills: [`.cursor/skills/arch-*`](.cursor/skills/).

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Nothing listens on 4010 | Run `MCP4Rhino` in the Rhino **command line** (not Help). The server does not start when the plugin loads. |
| Port bind error | Another process owns the port. Set `MCP4RHINO_PORT` and update the client URL. |
| Commands not found | Install failed, or Rhino was not restarted after yak install. Re-run `bash scripts/install-yak.sh`, then restart Rhino. |
| Tools or Logic code not updating | Copy both DLLs (`hot-reload-tools.sh` on macOS), then run `MCP4RhinoReload` / `mcp4rhino_reload`. Restart Rhino after host `.rhp` edits. |
| Object is huge vs the size the user named (e.g. “32 inches” → tens of feet) | Document is likely Feet (or another unit) while the agent passed the inch number raw. Call `get_document_units`, `convert_length`, recreate, then `measure_size`. |
| Unit change did not resize geometry | Pass `scale_existing: true` to `set_document_units` (fixed to actually scale). |
| `capture_viewport` on Mac | Uses scripted ViewCaptureToFile. Default PNG path: `~/Library/Logs/MCP4Rhino/`. |

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

MIT — see [LICENSE](LICENSE).
