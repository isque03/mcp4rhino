# Troubleshooting

For common end-user issues, see the short table in [README.md](../README.md#troubleshooting).

## Server and connection

| Symptom | Fix |
|---------|-----|
| Nothing listens on 4010 | Confirm Rhino is open. Use **MCP4Rhino → Status…** (Windows: under Tools; macOS: top-level menu) or `MCP4RhinoStatus`. If Stopped, Start server / run `MCP4Rhino` in the Rhino **command line** (not Help search). If auto-start is off (`MCP4RHINO_AUTOSTART=0`), you must start manually. |
| Port bind error | Another process owns the port. Set `MCP4RHINO_PORT` and update the client URL. |
| Commands not found | Install failed, or Rhino was not restarted after install. Reinstall via Package Manager or the GitHub `.yak`, restart Rhino. From source: see [INSTALL_FROM_SOURCE.md](INSTALL_FROM_SOURCE.md). |
| Claude / Cursor cannot connect | Rhino must be running and MCP4Rhino listening. Restart the AI client after editing MCP config. Confirm the URL is `http://127.0.0.1:4010/mcp` (or your custom port). Node.js/`npx` required for `mcp-remote`. |
| AI client on Linux, Rhino on Windows VM | Replace `localhost` with the Windows host IP in the MCP config. |

## Tools and updates (developers)

| Symptom | Fix |
|---------|-----|
| Tools or Logic code not updating | Copy both DLLs (`hot-reload-tools.sh` / `.ps1`), then `MCP4RhinoReload` / `mcp4rhino_reload`. Restart Rhino after host `.rhp` edits. |

## Units and sizing

| Symptom | Fix |
|---------|-----|
| Object is huge vs the size the user named (e.g. “32 inches” → tens of feet) | Document is likely Feet (or another unit) while the agent passed the inch number raw. Call `get_document_units`, `convert_length`, recreate, then `measure_size`. |
| Unit change did not resize geometry | Pass `scale_existing: true` to `set_document_units`. |

## Viewport capture

| Symptom | Fix |
|---------|-----|
| `capture_viewport` on Mac | Scripted ViewCaptureToFile (does not pass `width`/`height` into the script). Default PNG path: `~/Library/Logs/MCP4Rhino/`. Embeds MCP `image` when ≤ 1 MiB. |
| `capture_viewport` on Windows | ViewCaptureToFile first; CaptureToBitmap fallback uses `width`/`height`. Default PNG path: `%LOCALAPPDATA%\MCP4Rhino\`. Same embed rules. |
| Path but no embedded image | Text may set `image_embedded: false` and `image_omitted_reason: "exceeds_max_bytes"`. On Windows, retry smaller `width`/`height`. On macOS, open `path` or reframe the view. Invalid PNG fails the tool. |

## Export and license

| Symptom | Fix |
|---------|-----|
| `export_3dm` fails with a license / evaluation message | Rhino is not allowing save (`RhinoApp.CanSave` is false). Call `check_rhino_license`; renew or activate a valid license / evaluation before saving. |

## Logs

| OS | Path |
|----|------|
| macOS | `~/Library/Logs/MCP4Rhino/mcp4rhino.log` |
| Windows | `%LOCALAPPDATA%\MCP4Rhino\mcp4rhino.log` |

Shell probe (developers):

```bash
curl -sS -X POST http://127.0.0.1:4010/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

Or open `http://127.0.0.1:4010/` in a browser for a plain OK response when the server is up.
