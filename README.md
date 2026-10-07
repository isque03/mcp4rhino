# MCP4Rhino

[github.com/isque03/mcp4rhino](https://github.com/isque03/mcp4rhino)

Rhino 8 plugin that embeds a local **MCP** (Model Context Protocol) HTTP server so AI agents (Claude Desktop, Cursor, etc.) can inspect and drive the active Rhino document.

> Pointing Claude (or similar) at this repo? Start with [`CLAUDE.md`](CLAUDE.md).

## Quick start (macOS)

**Prerequisites:** [Rhino 8](https://www.rhino3d.com/), [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js](https://nodejs.org/) (`npx` for MCP clients).

```bash
git clone https://github.com/isque03/mcp4rhino.git
cd mcp4rhino
bash scripts/install-yak.sh
```

That builds the solution, packs a yak package (`MCP4Rhino` **0.3.0**), and installs it into Rhino’s package folder.

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

Rhino 8 has **no official Linux desktop** ([system requirements](https://www.rhino3d.com/8/system-requirements/) list Linux as unsupported). MCP4Rhino still needs a running Rhino UI process.

**Recommended:** run [Rhino 8 for Windows](https://www.rhino3d.com/) in a **Windows 11 VM** (or remote Windows box), follow the [Windows 11](#quick-start-windows-11) quick start there, then point your Linux MCP client at `http://<windows-host>:4010/mcp` (open the firewall for that port if needed).

**Unsupported community path:** if you already run Rhino under Wine, use the Windows install steps inside that prefix (`Yak.exe` / `Rhino.exe` under `drive_c/Program Files/Rhino 8/...`). Expect breakage; not supported here.

**Build-only on Linux** (produce a `.yak` for a Mac/Windows Rhino machine — does not start the MCP server):

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

After install on any supported host:

1. **Restart Rhino** (required after install / host `.rhp` changes).
2. On startup you should see something like: `MCP4Rhino … loaded. Run MCP4Rhino to start…`
3. Click the **command line** above the viewports (not the Help search box).
4. Run: `MCP4Rhino`
5. Expect: `MCP server started: http://localhost:4010` (endpoint: `http://127.0.0.1:4010/mcp`)

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

**Cursor / other** — same MCP URL via `npx mcp-remote http://localhost:4010/mcp` (or your client’s HTTP MCP equivalent). On Linux talking to Rhino in a VM, replace `localhost` with the Windows host IP.

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

Edit tools → rebuild + copy → hot-reload (no Rhino restart):

**macOS**

```bash
bash scripts/hot-reload-tools.sh
# then in Rhino: MCP4RhinoReload
# or MCP tool: mcp4rhino_reload
```

**Windows** — rebuild Tools, copy `MCP4Rhino.Tools.dll` (and any new deps) into the installed package `net8.0` folder under `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP4Rhino\<version>\net8.0\`, then `MCP4RhinoReload` / `mcp4rhino_reload`.

Host plugin (`.rhp`) changes still need a Rhino restart, then `MCP4Rhino` again.

## Commands and environment

| Item | Meaning |
|------|---------|
| `MCP4Rhino` | Start the MCP HTTP server |
| `MCP4RhinoStop` | Stop the server |
| `MCP4RhinoReload` | Hot-reload `MCP4Rhino.Tools.dll` |
| `MCP4RHINO_PORT` | Listen port (default **4010**) |
| `MCP4RHINO_TOOLS_PATH` | Override directory containing `MCP4Rhino.Tools.dll` |

## Features

- Yak package: host `.rhp` + hot-reloadable `MCP4Rhino.Tools.dll`
- Geometry, tagging/groups, view/camera control, viewport capture
- Agent-callable `mcp4rhino_reload` so iteration does not require restarting Rhino
- **Architecture agent (roadmap):** IRC/IBC-oriented MCP tools (phased P0–P4) plus project skills under [`.cursor/skills/`](.cursor/skills/) — overview in [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md). Semantic tags use `mcp4:` user-text keys on model objects.

## MCP tools

| Tool | Description |
|------|-------------|
| `get_document_info` | Units, layers, counts, selection |
| `get_objects` | List/filter objects (`tag_key`/`tag_value`/`group`, layer, name, …) |
| `create_box` / `create_sphere` / `create_cylinder` | Add solids |
| `delete_objects` | Delete by GUID list |
| `set_user_text` / `get_user_text` | Object key/value tags |
| `set_object_name` / `set_object_layer` | Rename / re-layer |
| `list_groups` / `create_group` / `add_to_group` | Group membership |
| `list_views` | Viewports + camera summaries |
| `get_view` | Full camera state (`view` name optional; default active) |
| `set_active_view` | Activate by name (`Perspective`, `Top`, …) |
| `set_view` | Absolute `camera` + `target` (+ optional `up`) |
| `orbit_view` | Relative yaw/pitch in degrees around target |
| `pan_view` | Screen-space pan (`right` / `up` in model units) |
| `zoom_view` | Dolly toward/away from target (`factor` > 1 zooms in) |
| `zoom_extents` | Fit all objects (optional `ids`) |
| `capture_viewport` | PNG screenshot (`width`/`height`/`view`/`path`; Mac uses ViewCaptureToFile) |
| `run_rhino_command` | Scripted Rhino command escape hatch |
| Geometry P0 | `create_point` / `create_line` / `create_polyline` / `create_circle` / `create_ellipse` / `create_polygon` / `join_curves` / `trim_curve` / `split_curve` / `explode_objects` / `extrude_curve` / booleans / transforms / layers / blocks / units |
| Architecture P1 | `create_level` / `create_wall` / `create_slab` / `create_door` / `create_window` / `create_stair` / `create_space` / `get_building_model` / … |
| Documentation P2 | sections/elevations, dims/tags, sheets, schedules, `export_dwg` / `export_images` |
| Interop P3 | `export_ifc` / `clash_detect` / `quantity_takeoff` / links |
| Code P4 | `set_code_context` / `run_code_checks` / `get_code_report` (pre-check only) |
| `mcp4rhino_reload` | Hot-reload Tools DLL without restarting Rhino |

Full architecture tool + skill map: [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md). Project skills: [`.cursor/skills/arch-*`](.cursor/skills/).

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Nothing listens on 4010 | Run `MCP4Rhino` in Rhino’s **command line** (not Help). Server does not auto-start on load. |
| Port bind error | Another process owns the port, or set `MCP4RHINO_PORT` and update your client URL. |
| Commands not found | Install failed or Rhino not restarted after yak install. Re-run `bash scripts/install-yak.sh`, restart Rhino. |
| Tools code not updating | Use `bash scripts/hot-reload-tools.sh` then `MCP4RhinoReload` / `mcp4rhino_reload`. Host `.rhp` edits need restart. |
| `capture_viewport` on Mac | Uses scripted ViewCaptureToFile (GDI+ `CaptureToBitmap` is unreliable). PNGs land under `~/Library/Logs/MCP4Rhino/` by default. |

## License

MIT — see [LICENSE](LICENSE).
