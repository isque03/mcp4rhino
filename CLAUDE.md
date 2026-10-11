# CLAUDE.md — agent playbook for MCP4Rhino

You help the user build, install, and run **MCP4Rhino**. MCP4Rhino is a Rhino 8 plugin that hosts a local MCP JSON-RPC server at `http://127.0.0.1:4010/mcp`.

Use these names and values:

- Package and Rhino commands: `MCP4Rhino`, `MCP4RhinoStatus`, `MCP4RhinoStatusUI`, `MCP4RhinoStop`, `MCP4RhinoUpdates`, `MCP4RhinoReportIssue`, `MCP4RhinoReload` (also the **MCP4Rhino** menu: under **Tools** on Windows; top-level on macOS)
- Default port: **4010** (override with `MCP4RHINO_PORT`)
- Do not invent other ports

**End users:** [README.md](README.md) (Package Manager install + Claude/Cursor setup).  
**Contributors:** [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md), [docs/INSTALL_FROM_SOURCE.md](docs/INSTALL_FROM_SOURCE.md).  
**Maintainers publishing:** [docs/PUBLISHING.md](docs/PUBLISHING.md) and [`.cursor/skills/publish/`](.cursor/skills/publish/).

Prefer Package Manager or GitHub `.yak` for end users. Use source install scripts only when developing from this repo.

---

## Get a working server (end user)

Prerequisites: Rhino 8. For AI clients, Node.js (`npx`).

1. In Rhino: `PackageManager` → search **MCP4Rhino** → Install → restart Rhino.  
   Or download the universal `.yak` from [GitHub releases](https://github.com/isque03/mcp4rhino/releases) and `yak install` it, then restart.
2. After restart the plugin **auto-starts** the MCP server on Idle (unless `MCP4RHINO_AUTOSTART=0`). Confirm with `MCP4RhinoStatus`, or run `MCP4Rhino` if needed.
3. Connect the client: `npx mcp-remote http://localhost:4010/mcp` (see README).

---

## Get a working server from source (macOS)

Prerequisites: Rhino 8, .NET 8 SDK, Node.js (`npx`). Use this when iterating on this clone.

```bash
bash scripts/install-yak.sh
```

Packs `dist/MCP4Rhino-<version>.yak` (universal) and installs under Application Support (`*/Rhinoceros/packages/8.0/MCP4Rhino/`). Version from [manifest.yml](manifest.yml). Pack only: `bash scripts/pack-yak.sh`. Override yak with `YAK` if needed.

Then [finish in Rhino](#finish-in-rhino-agent-cannot-do-this).

---

## Get a working server from source (Windows 11)

Prerequisites: Rhino 8 for Windows, .NET 8 SDK, Node.js (`npx`).

From the repo root in **PowerShell**:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/install-yak.ps1
```

Packs `dist/MCP4Rhino-<version>.yak` (universal) and installs under `%APPDATA%\*\Rhinoceros/packages/8.0/MCP4Rhino\`. Pack only: `scripts/pack-yak.ps1`. Optional: `YAK` (path to Yak.exe). Hot-reload scripts may use `RHINO_PACKAGES_DIR`.

Then [finish in Rhino](#finish-in-rhino-agent-cannot-do-this).

---

## Upgrade from this source repo (existing install)

Use this when the user already installed MCP4Rhino and new commits land (after `git pull`, merging a PR, or checking out a feature branch). Do **not** tell them to re-clone or wipe the package folder unless install is broken.

### 1. Update the source

```bash
git pull
# or: git fetch && git checkout <branch> && git pull
```

Work from the **repo root** that matches the scripts they run (same clone they used to install).

### 2. Pick the upgrade path

| What changed in the pull | What to run | Rhino restart? |
|--------------------------|-------------|----------------|
| Only Tools / Logic (new or changed MCP tools, helpers) | Hot-reload script below, then `MCP4RhinoReload` or `mcp4rhino_reload` | **No** |
| Host `.rhp`, new Rhino commands, Contracts, or you are unsure | Full install script (`install-yak`), then quit and reopen Rhino | **Yes** |
| Docs / skills / `CLAUDE.md` only | Nothing to install | — |

**When unsure, run the full install** (`install-yak.sh` / `install-yak.ps1`). It rebuilds and overwrites the package. Still restart Rhino afterward for host safety.

### 3. Hot-reload (Tools + Logic only)

macOS:

```bash
bash scripts/hot-reload-tools.sh
# Rhino command line: MCP4RhinoReload
# or MCP tool: mcp4rhino_reload
```

Windows 11:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/hot-reload-tools.ps1
# Rhino: MCP4RhinoReload  — or MCP: mcp4rhino_reload
```

The MCP HTTP server can keep running across hot-reload. Clients on `http://127.0.0.1:4010/mcp` do not need a new URL unless `MCP4RHINO_PORT` changed.

### 4. Full reinstall (host or uncertain)

Same commands as [source install macOS](#get-a-working-server-from-source-macos) / [Windows](#get-a-working-server-from-source-windows-11). Then [finish in Rhino](#finish-in-rhino-agent-cannot-do-this).

### 5. Confirm the new bits are live

- `tools/list` shows new tool names / updated descriptions.
- Call a changed tool (e.g. `capture_viewport` text with `image_embedded: true` and a following `image` content block for typical default sizes).
- Image-embed changes are Tools/Logic — hot-reload is enough. Host `.rhp` edits still need full install + restart.
- If behavior is still old: they hot-reloaded but needed a full install, or they did not run `MCP4RhinoReload` / restart.

Claude Desktop / Cursor MCP config usually stays `npx mcp-remote http://localhost:4010/mcp` — no change for routine upgrades.

---

## Linux

Rhino 8 has no official Linux desktop. Use a **Windows 11 VM** or a remote Windows host. Install MCP4Rhino there (Package Manager, the universal `.yak` from GitHub, or `scripts/install-yak.ps1` from source). Point the Linux MCP client at `http://<windows-host>:4010/mcp`.

---

## Finish in Rhino (agent cannot do this)

Ask the user to:

1. Quit and reopen Rhino after install or host `.rhp` changes.
2. Wait a moment for Idle auto-start, or click the Rhino **command line** and run `MCP4Rhino` / `MCP4RhinoStatus`.
3. Confirm the endpoint on port **4010** (or `MCP4RHINO_PORT`).

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

If the connection is refused: Rhino is not running, auto-start has not fired yet, or the user disabled autostart (`MCP4RHINO_AUTOSTART=0`) and has not run `MCP4Rhino`.

**Logs**

| OS | Path |
|----|------|
| macOS | `~/Library/Logs/MCP4Rhino/mcp4rhino.log` |
| Windows 11 | `%LOCALAPPDATA%\MCP4Rhino\mcp4rhino.log` |

---

## Architecture

| Path | Role | Reload |
|------|------|--------|
| [src/MCP4Rhino/](src/MCP4Rhino/) | Host `.rhp`, HTTP listener, commands | **Restart Rhino** |
| [src/MCP4Rhino.Tools/](src/MCP4Rhino.Tools/) | Tool implementations (`RhinoToolCatalog`, surface/curve tools, …) | Hot-reload — no restart |
| [src/MCP4Rhino.Logic/](src/MCP4Rhino.Logic/) | Pure helpers (`SurfaceOps`, `CurveOps`, …); loaded with Tools in the collectible ALC | Hot-reload with Tools (copy `MCP4Rhino.Logic.dll`) |
| [src/MCP4Rhino.Contracts/](src/MCP4Rhino.Contracts/) | Shared bridge interfaces | Usually with host |
| [scripts/pack-yak.sh](scripts/pack-yak.sh) / [pack-yak.ps1](scripts/pack-yak.ps1) | Build Release → universal `dist/MCP4Rhino-<version>.yak` | — |
| [scripts/verify-yak-package.sh](scripts/verify-yak-package.sh) | Archive content checks (icon, Logic, `.rhp`, `.rui`, …) | — |
| [scripts/ci-download-yak.sh](scripts/ci-download-yak.sh) | Pin standalone Yak CLI + SHA256 for CI | — |
| [scripts/install-yak.sh](scripts/install-yak.sh) / [install-yak.ps1](scripts/install-yak.ps1) | Pack + local yak install (macOS / Windows) | Restart Rhino after |
| [scripts/smoke-local-yak.sh](scripts/smoke-local-yak.sh) / [smoke-local-yak.ps1](scripts/smoke-local-yak.ps1) | Local install + package checks + MCP `tools/list` probe | Restart if exit 2 |
| [`.github/workflows/build.yml`](.github/workflows/build.yml) | PR/`main`: win+mac tests, ubuntu universal yak artifact (no secrets) | — |
| [`.github/workflows/publish.yml`](.github/workflows/publish.yml) | Manual: download Build artifact → Yak push + GitHub Release (`production` / `YAK_TOKEN`) | — |
| [`.cursor/skills/publish/`](.cursor/skills/publish/) | Agent skill: merge → green Build → tag `vX.Y.Z` → dispatch Publish → verify | — |
| [scripts/test-coverage.ps1](scripts/test-coverage.ps1) | Windows coverage gate (same as `test-coverage.sh`) | — |
| [scripts/hot-reload-tools.sh](scripts/hot-reload-tools.sh) / [hot-reload-tools.ps1](scripts/hot-reload-tools.ps1) | Tools + Logic build and copy into package | Then `mcp4rhino_reload` |
| [examples/claude_desktop_config.json](examples/claude_desktop_config.json) | Claude Desktop MCP client snippet | — |
| [docs/MCP_TOOLS.md](docs/MCP_TOOLS.md) | Full MCP tool inventory | — |
| [docs/PUBLISHING.md](docs/PUBLISHING.md) | CI + Yak release procedure | — |

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
powershell -ExecutionPolicy Bypass -File scripts/hot-reload-tools.ps1
# then Rhino: MCP4RhinoReload  — or MCP tool: mcp4rhino_reload
```

---

## Hard rules

- Keep the default MCP URL `http://127.0.0.1:4010/mcp` unless the user set `MCP4RHINO_PORT`.
- Do not commit or push unless the user asks.
- Do not edit Cursor plan files under `~/.cursor/plans/`.
- Run document and view mutations on the UI thread (`UiThread.Invoke`).
- On **macOS**, `capture_viewport` uses `_-ViewCaptureToFile` (no System.Drawing/GDI+). The script does not take `width`/`height`. Default PNG path: `~/Library/Logs/MCP4Rhino/`.
- On **Windows**, capture tries ViewCaptureToFile first, then `CaptureToBitmap` with `width`/`height`. Default PNG path: `%LOCALAPPDATA%\MCP4Rhino\` (or the `path` argument).
- `capture_viewport` text metadata includes `path`, actual/requested sizes, `method`, `image_embedded`, and `byte_length`. When embedded, `content` also has `type: "image"` / `mimeType: "image/png"`. Sandboxed agents should use that image; only `Read` the path when they have host filesystem access. Oversize omit: `image_embedded: false`, `image_omitted_reason: "exceeds_max_bytes"`, `max_embedded_png_bytes` (1 MiB raw). Shrink `width`/`height` only helps when CaptureToBitmap wins (typically Windows); on macOS open `path` or reframe the view. Invalid PNG → tool error, not omit metadata.
- The plugin loads at Rhino startup and **auto-starts** the MCP server on the first Idle (unless `MCP4RHINO_AUTOSTART` is `0`/`false`/`no`). If nothing listens, ask the user to run `MCP4Rhino` or `MCP4RhinoStatus`.
- Saving with `export_3dm` requires a valid Rhino license or a non-expired evaluation (`RhinoApp.CanSave`). Call `check_rhino_license` first; do not save when `can_save` is false. Treat `days_until_expiration` as optional (null when not applicable); `saves_left` is always null on the current RhinoCommon build.

---

## Architecture workflow

For **residential (IRC)** and **commercial (IBC)** design in Rhino, use project skills in [`.cursor/skills/`](.cursor/skills/). Roadmap: [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md). For Yak / GitHub Releases, use the [`publish`](.cursor/skills/publish/) skill (never `yak push` with a pasted token).

- MCP endpoint: `http://127.0.0.1:4010/mcp` (Rhino open; server auto-started or user ran `MCP4Rhino`).
- Store element semantics with **`mcp4:`** user-text keys (`set_user_text` or dedicated arch tools).
- Skills need jurisdiction and amendments from the user. Agents report **pre-check findings**. Do not claim legal “code compliant” status or an RA stamp.
- After modeling, run **`arch-model-coherence-pass`**, then **`arch-model-visual-review`**, before you claim done. Orchestrator: `arch-modeling-iterate`.

---

## Useful tools for agents

Full inventory: [docs/MCP_TOOLS.md](docs/MCP_TOOLS.md).

- **Support:** `get_support_info` (version, endpoint, log path, GitHub Issues URLs) before filing bugs/enhancements at https://github.com/isque03/mcp4rhino/issues
- **Inspect:** `get_document_info`, `get_document_units`, `get_objects`, `get_building_model`, `query_elements`
- **Units / size:** `get_document_units`, `convert_length`, `measure_size`, `measure_distance` (returns `_in` / `_ft` / `_mm`). Create-tool numbers are **document model units** only. If the user names inches/feet/mm and that differs from `unit_system`, convert first, then verify after create. See [CHANGELOG.md](CHANGELOG.md).
- **Geometry — create:** `create_point`, `create_line`, `create_polyline`, `create_circle`, `create_arc`, `create_ellipse`, `create_polygon`, `create_rectangle`, `create_curve`
- **Geometry — edit:** `join_curves`, `trim_curve`, `split_curve`, `explode_objects`, `offset_curve`, `fillet_curve`, `extrude_curve`, booleans, `transform_objects`, layers, blocks, `set_document_units`
- **Surfaces — create:** `create_plane_surface`, `create_srf_pt`, `create_planar_surface`, `create_edge_surface`, `loft_surface`, `sweep1_surface`, `sweep2_surface`, `revolve_surface`, `rail_revolve_surface`, `network_surface`, `patch_surface`, `pipe_surface`, `extrude_curve_along_curve`
- **Surfaces — edit:** `fillet_surfaces`, `blend_surfaces`, `chamfer_surfaces`, `offset_surface`, `trim_surface`, `split_surface`, `join_surfaces`, `cap_planar_holes`
- **Surfaces — edges:** `list_surface_edges`, `dup_border`, `dup_edge`, `extract_isocurve`
- **Architecture:** `create_level`, `create_wall` / `create_slab` / `create_roof`, `create_door` / `create_window`, `create_stair` / `create_ramp`, `create_space`
- **Docs / Interop / Code:** sheets, `check_rhino_license`, `export_3dm` (gated on `can_save`), `export_ifc`, `run_code_checks` (findings only — never “compliant”)
- **Camera:** view tools + `capture_viewport` (use the tool’s `image` block when `image_embedded` is true; do not require a local file Read)
- **Iterate:** copy Tools and Logic DLLs, then `mcp4rhino_reload`

Surface face pick for fillet/chamfer: `face_index_*` or `pick_point_*` (required on multi-face breps). Edge pick for blend/dup_edge: `edge_index` or `pick_point` from `list_surface_edges`. Curve cutters for trim/split must be planar (or pass a surface/brep cutter). Full Rhino→MCP map: [docs/ARCHITECTURE_AGENT.md](docs/ARCHITECTURE_AGENT.md).
