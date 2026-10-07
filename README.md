# MCP4Rhino

[github.com/isque03/mcp4rhino](https://github.com/isque03/mcp4rhino)

Rhino 8 plugin that embeds a local **MCP** (Model Context Protocol) HTTP server so AI agents (Claude Desktop, Cursor, etc.) can inspect and drive the active Rhino document.

Inspired by the public behavior of [RhinoAiMCP (Food4Rhino)](https://www.food4rhino.com/en/app/rhinoaimcp), but this is a **separate clean-room MIT** project with its own name, package, and commands — not a fork or rename of that product.

## Features (v0.1)

- Rhino commands: **`MCP4Rhino`** (start), **`MCP4RhinoStop`**, **`MCP4RhinoReload`** (hot-reload tools)
- MCP tool **`mcp4rhino_reload`** — agents can hot-reload tools without restarting Rhino
- Yak package: `MCP4Rhino` (host `.rhp` + `MCP4Rhino.Tools.dll`)
- MCP HTTP at `http://localhost:4010/mcp` (override with `MCP4RHINO_PORT`)
- Tools DLL path override: `MCP4RHINO_TOOLS_PATH`
- Geometry tools: `get_document_info`, `get_objects`, `create_*`, `delete_objects`, `capture_viewport`
- Tagging: `set_user_text` / `get_user_text`, `set_object_name`, `set_object_layer`, `list_groups` / `create_group` / `add_to_group`; `get_objects` filters by `tag_key`/`tag_value`/`group`
- Views: `list_views`, `get_view`, `set_active_view`, `set_view`, `orbit_view`, `pan_view`, `zoom_view`, `zoom_extents`
- Log: `~/Library/Logs/MCP4Rhino/mcp4rhino.log`

### Hot-reload loop (tools only)

```bash
dotnet build src/MCP4Rhino.Tools/MCP4Rhino.Tools.csproj -c Release
# copy Tools dll into the installed yak net8.0 folder OR set MCP4RHINO_TOOLS_PATH
# then in Rhino or via MCP:
MCP4RhinoReload
# or tools/call name=mcp4rhino_reload
```

Host (`.rhp`) changes still need a Rhino restart once.

## Requirements

- [Rhino 8](https://www.rhino3d.com/) (macOS or Windows)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build
- [Node.js](https://nodejs.org/) (`npx`) for `mcp-remote` clients

## Build

```bash
dotnet build src/MCP4Rhino/MCP4Rhino.csproj -c Release
```

Output `.rhp`:

```
src/MCP4Rhino/bin/Release/net8.0/MCP4Rhino.rhp
```

## Install in Rhino

```bash
# from a staging dir with manifest.yml + net8.0/MCP4Rhino.rhp (+ dependency DLLs):
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" build --platform mac
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" install ./mcp4rhino-*.yak
```

Or drag `MCP4Rhino.rhp` (with its sibling DLLs) onto Rhino / install via Plug-ins.

1. Restart Rhino.
2. On startup you should see: `MCP4Rhino … loaded. Run MCP4Rhino to start…`
3. Click the **command line** above the viewports (not the Help search box).
4. Run: `MCP4Rhino`
5. Expect: `MCP server started: http://localhost:4010`

## Connect Claude Desktop

Merge [`examples/claude_desktop_config.json`](examples/claude_desktop_config.json):

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
| `capture_viewport` | PNG screenshot (`width`/`height`/`view`/`path`; Mac-safe fallbacks) |
| `execute_csharp` | Run a short RhinoCommon C# script (`Doc` global) |
| `mcp4rhino_reload` | Hot-reload Tools DLL without restarting Rhino |

## License

MIT — see [LICENSE](LICENSE).

## Citations

- Public product docs (behavioral reference only): [RhinoAiMCP — Food4Rhino](https://www.food4rhino.com/en/app/rhinoaimcp)
- Architecture peers (not forked): [jingcheng-chen/rhinomcp](https://github.com/jingcheng-chen/rhinomcp), [mcneel/RhinoAI](https://github.com/mcneel/RhinoAI)
