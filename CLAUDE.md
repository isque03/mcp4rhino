# CLAUDE.md — agent playbook for MCP4Rhino

You are helping a user build, install, and run **MCP4Rhino**: a Rhino 8 plugin that hosts a local MCP JSON-RPC server at `http://127.0.0.1:4010/mcp`.

This is **not** Food4Rhino “RhinoAiMCP”. Package/commands are `MCP4Rhino`. Default port is **4010** (`MCP4RHINO_PORT` to override). Do not invent other ports.

Human-oriented docs: [README.md](README.md).

---

## Get a working server

**macOS** — from the repo root:

```bash
# Prerequisites: Rhino 8, .NET 8 SDK, Node.js (npx)
bash scripts/install-yak.sh
```

That builds `MCP4Rhino.sln`, stages a yak package (see [manifest.yml](manifest.yml) version **0.3.0**), and installs into:

`~/Library/Application Support/McNeel/Rhinoceros/packages/8.0/MCP4Rhino/`

**Windows 11 / Linux** — follow the matching Quick start in [README.md](README.md). Linux has no official Rhino desktop; use a Windows VM (or remote Windows) and point the client at that host’s `:4010`.

**You cannot finish setup alone.** Ask the user to:

1. **Quit and reopen Rhino** (required after install / host `.rhp` changes).
2. Click Rhino’s **command line** (above the viewports — not Help search).
3. Type `MCP4Rhino` and Enter.
4. Confirm the command line shows the server started on port **4010**.

Only then connect a client (`npx mcp-remote http://localhost:4010/mcp`) or call tools from the shell.

---

## Verify from the shell

```bash
# List tools
curl -sS -X POST http://127.0.0.1:4010/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# Smoke-call
curl -sS -X POST http://127.0.0.1:4010/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_document_info","arguments":{}}}'
```

Python equivalent:

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

Logs: `~/Library/Logs/MCP4Rhino/mcp4rhino.log`

---

## Architecture

| Path | Role | Reload |
|------|------|--------|
| [src/MCP4Rhino/](src/MCP4Rhino/) | Host `.rhp`, HTTP listener, commands | **Restart Rhino**, then `MCP4Rhino` |
| [src/MCP4Rhino.Tools/](src/MCP4Rhino.Tools/) | Tool implementations (`RhinoToolCatalog`, `ViewTools`, …) | Hot-reload — no restart |
| [src/MCP4Rhino.Contracts/](src/MCP4Rhino.Contracts/) | Shared bridge interfaces | Usually with host |
| [scripts/install-yak.sh](scripts/install-yak.sh) | Full build + yak install | Restart Rhino after |
| [scripts/hot-reload-tools.sh](scripts/hot-reload-tools.sh) | Build Tools + copy into installed package | Then `mcp4rhino_reload` |
| [examples/claude_desktop_config.json](examples/claude_desktop_config.json) | Claude Desktop MCP client snippet | — |

Prefer editing **Tools** for new MCP capabilities. Keep host thin.

---

## Change loop (tools)

```bash
# After editing src/MCP4Rhino.Tools/
bash scripts/hot-reload-tools.sh
```

Then either:

- Rhino command: `MCP4RhinoReload`, or
- MCP tool: `mcp4rhino_reload`

**Never** tell the user to restart Rhino for tools-only changes.

---

## Hard rules

- Default MCP URL: `http://127.0.0.1:4010/mcp` — keep it unless the user set `MCP4RHINO_PORT`.
- Do not commit or push unless the user asks.
- Do not edit Cursor plan files under `~/.cursor/plans/`.
- Tool mutations that touch the document/views must run on the UI thread (existing `UiThread.Invoke` pattern).
- On macOS, `capture_viewport` uses `_-ViewCaptureToFile` (avoid System.Drawing/GDI+). Default PNGs: `~/Library/Logs/MCP4Rhino/`.
- Server does **not** auto-start when the plugin loads; the user must run `MCP4Rhino`.

---

## Useful tools for agents

- **Inspect:** `get_document_info`, `get_objects` (filters: `tag_key`/`tag_value`/`group`, layer, name)
- **Build:** `create_box` / `create_sphere` / `create_cylinder`, `delete_objects`
- **Structure:** `set_user_text`, `set_object_name`, `set_object_layer`, `list_groups` / `create_group` / `add_to_group`
- **Camera:** `list_views`, `get_view`, `set_active_view`, `set_view`, `orbit_view`, `pan_view`, `zoom_view`, `zoom_extents` — mutate tools return camera JSON so you can chain without a separate `get_view`
- **See:** `capture_viewport` → open the returned `path` PNG
- **Iterate:** `mcp4rhino_reload` after `scripts/hot-reload-tools.sh`
