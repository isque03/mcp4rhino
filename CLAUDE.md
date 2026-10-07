# CLAUDE.md — agent playbook for MCP4Rhino

You help the user build, install, and run **MCP4Rhino**. MCP4Rhino is a Rhino 8 plugin that hosts a local MCP JSON-RPC server at `http://127.0.0.1:4010/mcp`.

Use these names and values:

- Package and Rhino commands: `MCP4Rhino`
- Default port: **4010** (override with `MCP4RHINO_PORT`)
- Do not invent other ports

Detect the user OS. Use the matching install path below. Human-oriented docs: [README.md](README.md).

---

## Get a working server (macOS)

Prerequisites: Rhino 8, .NET 8 SDK, Node.js (`npx`).

```bash
bash scripts/install-yak.sh
```

Installs into: `~/Library/Application Support/McNeel/Rhinoceros/packages/8.0/MCP4Rhino/` (manifest version **0.3.0** — see [manifest.yml](manifest.yml)).

Then [finish in Rhino](#finish-in-rhino-agent-cannot-do-this).

---

## Get a working server (Windows 11)

Prerequisites: Rhino 8 for Windows, .NET 8 SDK, Node.js (`npx`).

Run from the repo root in **PowerShell**:

```powershell
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

Installs into (typical): `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP4Rhino\`

Yak CLI: `C:\Program Files\Rhino 8\System\Yak.exe`

Then [finish in Rhino](#finish-in-rhino-agent-cannot-do-this).

---

## Linux

Rhino 8 has no official Linux desktop. Use a **Windows 11 VM** or a remote Windows host. Follow the [Windows 11](#get-a-working-server-windows-11) steps on that host. Point the Linux MCP client at `http://<windows-host>:4010/mcp`.

---

## Finish in Rhino (agent cannot do this)

Ask the user to:

1. Quit and reopen Rhino after install or host `.rhp` changes.
2. Click the Rhino **command line** above the viewports. Do not use Help search.
3. Type `MCP4Rhino` and press Enter.
4. Confirm the server started on port **4010**.

Only then connect a client (`npx mcp-remote http://localhost:4010/mcp`) or call tools from the shell.

---

## Verify from the shell

**macOS / Linux client** (bash):

```bash
curl -sS -X POST http://127.0.0.1:4010/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

curl -sS -X POST http://127.0.0.1:4010/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_document_info","arguments":{}}}'
```

**Windows 11** (PowerShell):

```powershell
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:4010/mcp `
  -ContentType 'application/json' `
  -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

Invoke-RestMethod -Method Post -Uri http://127.0.0.1:4010/mcp `
  -ContentType 'application/json' `
  -Body '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_document_info","arguments":{}}}'
```

**Cross-platform Python** (works on both):

```python
import json, urllib.request

def mcp(method, params=None, id=1):
    body = json.dumps({"jsonrpc": "2.0", "id": id, "method": method, "params": params or {}}).encode()
    req = urllib.request.Request(
        "http://127.0.0.1:4010/mcp",
        data=body,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())

print(mcp("tools/list"))
print(mcp("tools/call", {"name": "get_document_info", "arguments": {}}))
```

If the connection is refused: Rhino is not running, or the user has not run `MCP4Rhino` yet.

**Logs**

| OS | Path |
|----|------|
| macOS | `~/Library/Logs/MCP4Rhino/mcp4rhino.log` |
| Windows 11 | `%LOCALAPPDATA%\MCP4Rhino\mcp4rhino.log` |

---

## Architecture

| Path | Role | Reload |
|------|------|--------|
| [src/MCP4Rhino/](src/MCP4Rhino/) | Host `.rhp`, HTTP listener, commands | **Restart Rhino**, then `MCP4Rhino` |
| [src/MCP4Rhino.Tools/](src/MCP4Rhino.Tools/) | Tool implementations (`RhinoToolCatalog`, surface/curve tools, …) | Hot-reload — no restart |
| [src/MCP4Rhino.Logic/](src/MCP4Rhino.Logic/) | Pure helpers (`SurfaceOps`, `CurveOps`, …); loaded with Tools in the collectible ALC | Hot-reload with Tools (copy `MCP4Rhino.Logic.dll`) |
| [src/MCP4Rhino.Contracts/](src/MCP4Rhino.Contracts/) | Shared bridge interfaces | Usually with host |
| [scripts/install-yak.sh](scripts/install-yak.sh) | macOS full build + yak install | Restart Rhino after |
| [scripts/hot-reload-tools.sh](scripts/hot-reload-tools.sh) | macOS Tools + Logic build and copy into package | Then `mcp4rhino_reload` |
| [examples/claude_desktop_config.json](examples/claude_desktop_config.json) | Claude Desktop MCP client snippet | — |

Prefer editing **Tools** (and **Logic** helpers) for new MCP capabilities. Keep the host thin.

---

## Change loop (tools)

**Do not** tell the user to restart Rhino for tools-only or Logic-only changes.

### macOS

```bash
bash scripts/hot-reload-tools.sh
# then Rhino: MCP4RhinoReload  — or MCP tool: mcp4rhino_reload
```

### Windows 11

```powershell
dotnet build src\MCP4Rhino.Tools\MCP4Rhino.Tools.csproj -c Release

$pkgRoot = Join-Path $env:APPDATA "McNeel\Rhinoceros\packages\8.0\MCP4Rhino"
$ver = Get-Content (Join-Path $pkgRoot "manifest.txt") -ErrorAction SilentlyContinue
if (-not $ver) { $ver = "0.3.0" }
$dest = Join-Path $pkgRoot "$ver\net8.0"
$toolsOut = "src\MCP4Rhino.Tools\bin\Release\net8.0"
Copy-Item "$toolsOut\MCP4Rhino.Tools.dll" $dest -Force
foreach ($f in @("MCP4Rhino.Logic.dll","System.Drawing.Common.dll","Microsoft.Win32.SystemEvents.dll")) {
  $src = Join-Path $toolsOut $f
  if (Test-Path $src) { Copy-Item $src $dest -Force }
}
```

Then run Rhino command `MCP4RhinoReload` or MCP tool `mcp4rhino_reload`.

---

## Hard rules

- Keep the default MCP URL `http://127.0.0.1:4010/mcp` unless the user set `MCP4RHINO_PORT`.
- Do not commit or push unless the user asks.
- Do not edit Cursor plan files under `~/.cursor/plans/`.
- Run document and view mutations on the UI thread (`UiThread.Invoke`).
- On **macOS**, `capture_viewport` uses `_-ViewCaptureToFile`. Do not use System.Drawing/GDI+. Default PNG path: `~/Library/Logs/MCP4Rhino/`.
- On **Windows**, prefer `CaptureToBitmap` when it works. ViewCaptureToFile is also valid. Default PNG path: `%LOCALAPPDATA%\MCP4Rhino\` (or the `path` argument).
- The server does **not** start when the plugin loads. The user must run `MCP4Rhino`.

---

## Architecture workflow

For **residential (IRC)** and **commercial (IBC)** design in Rhino, use project skills in [`.cursor/skills/`](.cursor/skills/). Roadmap: [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md).

- MCP endpoint: `http://127.0.0.1:4010/mcp` (the user must run `MCP4Rhino` first).
- Store element semantics with **`mcp4:`** user-text keys (`set_user_text` or dedicated arch tools).
- Skills need jurisdiction and amendments from the user. Agents report **pre-check findings**. Do not claim legal “code compliant” status or an RA stamp.
- After modeling, run **`arch-model-coherence-pass`**, then **`arch-model-visual-review`**, before you claim done. Orchestrator: `arch-modeling-iterate`.

---

## Useful tools for agents

- **Inspect:** `get_document_info`, `get_objects`, `get_building_model`, `query_elements`
- **Geometry P0 — create:** `create_point`, `create_line`, `create_polyline`, `create_circle`, `create_arc`, `create_ellipse`, `create_polygon`, `create_rectangle`, `create_curve`
- **Geometry P0 — edit:** `join_curves`, `trim_curve`, `split_curve`, `explode_objects`, `offset_curve`, `fillet_curve`, `extrude_curve`, booleans, `transform_objects`, layers, blocks
- **Surfaces P0 — create:** `create_plane_surface`, `create_srf_pt`, `create_planar_surface`, `create_edge_surface`, `loft_surface`, `sweep1_surface`, `sweep2_surface`, `revolve_surface`, `rail_revolve_surface`, `network_surface`, `patch_surface`, `pipe_surface`, `extrude_curve_along_curve`
- **Surfaces P0 — edit:** `fillet_surfaces`, `blend_surfaces`, `chamfer_surfaces`, `offset_surface`, `trim_surface`, `split_surface`, `join_surfaces`, `cap_planar_holes`
- **Surfaces P0 — edges:** `list_surface_edges`, `dup_border`, `dup_edge`, `extract_isocurve`
- **Architecture P1:** `create_level`, `create_wall` / `create_slab` / `create_roof`, `create_door` / `create_window`, `create_stair` / `create_ramp`, `create_space`
- **Docs P2 / Interop P3 / Code P4:** sheets, `export_ifc`, `run_code_checks` (findings only — never “compliant”)
- **Camera:** view tools + `capture_viewport`
- **Iterate:** copy Tools and Logic DLLs, then `mcp4rhino_reload`

Surface face pick for fillet/chamfer: `face_index_*` or `pick_point_*` (required on multi-face breps). Edge pick for blend/dup_edge: `edge_index` or `pick_point` from `list_surface_edges`. Curve cutters for trim/split must be planar (or pass a surface/brep cutter). Full McNeel map: [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md).
