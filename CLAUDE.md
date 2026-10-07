# CLAUDE.md — agent playbook for MCP4Rhino

You are helping a user build, install, and run **MCP4Rhino**: a Rhino 8 plugin that hosts a local MCP JSON-RPC server at `http://127.0.0.1:4010/mcp`.

Package/commands are `MCP4Rhino`. Default port is **4010** (`MCP4RHINO_PORT` to override). Do not invent other ports.

Detect the user’s OS and use the matching install path below. Human-oriented docs: [README.md](README.md).

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

No official Rhino 8 desktop. Use a **Windows 11 VM** (or remote Windows), follow the [Windows 11](#get-a-working-server-windows-11) steps there, and point the Linux MCP client at `http://<windows-host>:4010/mcp`.

---

## Finish in Rhino (agent cannot do this)

Ask the user to:

1. **Quit and reopen Rhino** (required after install / host `.rhp` changes).
2. Click Rhino’s **command line** (above the viewports — not Help search).
3. Type `MCP4Rhino` and Enter.
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

If connection refused: Rhino is not running or the user has not run `MCP4Rhino` yet.

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
| [src/MCP4Rhino.Tools/](src/MCP4Rhino.Tools/) | Tool implementations (`RhinoToolCatalog`, `ViewTools`, …) | Hot-reload — no restart |
| [src/MCP4Rhino.Contracts/](src/MCP4Rhino.Contracts/) | Shared bridge interfaces | Usually with host |
| [scripts/install-yak.sh](scripts/install-yak.sh) | macOS full build + yak install | Restart Rhino after |
| [scripts/hot-reload-tools.sh](scripts/hot-reload-tools.sh) | macOS Tools build + copy into package | Then `mcp4rhino_reload` |
| [examples/claude_desktop_config.json](examples/claude_desktop_config.json) | Claude Desktop MCP client snippet | — |

Prefer editing **Tools** for new MCP capabilities. Keep host thin.

---

## Change loop (tools)

**Never** tell the user to restart Rhino for tools-only changes.

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
foreach ($f in @("System.Drawing.Common.dll","Microsoft.Win32.SystemEvents.dll")) {
  $src = Join-Path $toolsOut $f
  if (Test-Path $src) { Copy-Item $src $dest -Force }
}
```

Then Rhino command `MCP4RhinoReload` or MCP tool `mcp4rhino_reload`.

---

## Hard rules

- Default MCP URL: `http://127.0.0.1:4010/mcp` — keep it unless the user set `MCP4RHINO_PORT`.
- Do not commit or push unless the user asks.
- Do not edit Cursor plan files under `~/.cursor/plans/`.
- Tool mutations that touch the document/views must run on the UI thread (existing `UiThread.Invoke` pattern).
- On **macOS**, `capture_viewport` uses `_-ViewCaptureToFile` (avoid System.Drawing/GDI+). PNGs default to `~/Library/Logs/MCP4Rhino/`.
- On **Windows**, prefer `CaptureToBitmap` when it works; still fine to use ViewCaptureToFile. PNGs default under `%LOCALAPPDATA%\MCP4Rhino\` when using the same default path logic, or the `path` argument.
- Server does **not** auto-start when the plugin loads; the user must run `MCP4Rhino`.

---

## Architecture workflow

For **residential (IRC)** and **commercial (IBC)** design in Rhino, use project skills in [`.cursor/skills/`](.cursor/skills/) (`arch-project-bootstrap`, program/massing/envelope skills, documentation, IFC, code pre-checks, review pack). Roadmap summary: [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md).

- MCP endpoint: `http://127.0.0.1:4010/mcp` (user must run `MCP4Rhino` first).
- Store element semantics with **`mcp4:`** user-text keys (`set_user_text` / dedicated arch tools when available).
- Skills require jurisdiction/amendments from the user; agents report **pre-check findings**, never legal “code compliant” or RA stamp.
- After modeling, run **`arch-model-coherence-pass`** then **`arch-model-visual-review`** (multi-angle screenshots + measurements vs the brief) before claiming done. Orchestrator: `arch-modeling-iterate`.

---

## Useful tools for agents

- **Inspect:** `get_document_info`, `get_objects`, `get_building_model`, `query_elements`
- **Geometry P0:** `create_polyline`, `extrude_curve`, booleans, `transform_objects`, layers/blocks
- **Architecture P1:** `create_level`, `create_wall`/`slab`/`roof`, `create_door`/`window`, `create_stair`/`ramp`, `create_space`
- **Docs P2 / Interop P3 / Code P4:** sheets, `export_ifc`, `run_code_checks` (findings only — never “compliant”)
- **Camera / see:** view tools + `capture_viewport`
- **Iterate:** `mcp4rhino_reload` after the OS-specific tools copy step
