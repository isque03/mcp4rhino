# Usage telemetry

MCP4Rhino sends anonymous usage data unless you turn it off. Events go to `https://events.agenttoolkit.ai/telemetry` when the packaged install token or `MCP4RHINO_TELEMETRY_API_KEY` is set. The token is one `X-Api-Key` header. It is not part of the URL.

Usage data was introduced in `0.6.1-alpha`. Optional tool diagnostics are included starting with `0.7.1`.

## What is collected

Each event includes:

- A random installation id created for this plugin the first time usage data is on
- Plugin version
- Rhino major version, such as `8`
- Operating system: `macOS`, `Windows`, or `Linux`
- Event name: `plugin_started`, `mcp_connected`, or `tool_executed`
- UTC timestamp

`tool_executed` also includes the MCP tool name, duration in milliseconds, and whether the call succeeded.

Starting with `0.7.1`, failed calls can include a controlled error code, a known exception type (or `Other`), and a known stage (`DISPATCH`, `EXECUTION`, or `RHINO_COMMAND`). Error codes distinguish invalid arguments or state, unsupported operations, timeouts, cancellations, returned tool errors, failed Rhino commands, and unhandled exceptions. Explicit structured diagnostics can also identify missing documents, views or objects, and invalid geometry. Unknown details are omitted rather than guessed from error text.

For `run_rhino_command`, events can include the canonical English name of the leading command, resolved by Rhino. For example, `! _-Box 0,0,0 10,10,10 _Enter` reports only `Box`. Localized names resolve to the same English name. Unresolved aliases and ambiguous tokens are omitted. A macro reports only its leading command, without claiming separate outcomes for later commands.

The plugin does not collect your name, username, machine identifiers, or IP address in its event payload. It does not collect tool arguments, scripts, results, geometry, document names, prompts, file paths, exception messages, or stack traces.

Version `0.7.1` is the first release that records `run_rhino_command` as failed when Rhino returns `ok: false`, even though the existing MCP wrapper has `isError: false`. The MCP response is unchanged. Older generic-command success rates may be inflated. Older events remain valid without diagnostics; historical details cannot be reconstructed.

## Choice

Usage data is on unless you turn it off. Rhino does not ask first, and tool calls are not blocked. MCP server instructions do not mention usage data, so the agent is not told to bring it up in chat. The choice is **MCP4Rhino → Usage data…**. Events already delivered stay delivered if you turn usage data off afterward.

The choice is stored in Rhino plugin settings:

| Key | Value |
|-----|--------|
| `TelemetryConsent` | Absent or `enabled` means on. `disabled` is the opt-out |
| `TelemetryInstallationId` | Random UUID, created the first time usage data is on |
| `TelemetryNoticeShown` | `shown` after the chat notice has been delivered |

The same UUID is reused across Rhino restarts. Turning telemetry off keeps the id and stops sending. The in-memory queue is cleared immediately. Turning it on again does not create a new id and does not send `plugin_started` until the next Rhino launch. Each later launch while telemetry is already on sends `plugin_started` once when sending is active.

Change the choice with:

- **MCP4Rhino → Usage data…**
- **Status → Usage data…**
- Command `MCP4RhinoTelemetry`

Closing the later dialog without a button leaves the stored choice unchanged.

## When events are recorded

| Event | When |
|-------|------|
| `plugin_started` | Once per Rhino session when sending is active, at startup. Sending is active when usage data is on, the endpoint is valid, and the packaged install token or `MCP4RHINO_TELEMETRY_API_KEY` is available. Turning telemetry back on from **Usage data…** waits until the next launch |
| `mcp_connected` | An MCP client completes `initialize` |
| `tool_executed` | An MCP `tools/call` finishes, including failures |

Tool timing and outcome classification are recorded in the shared MCP dispatch path. The generic command adapter resolves its leading command name on Rhino's UI thread. Individual modeling tools do not send their own events.

## Delivery

Events stay in a memory queue of at most 100. The plugin POSTs a batch when 20 events are queued or every 30 seconds. Each request looks like:

```json
{
  "events": [
    {
      "installationId": "11111111-1111-1111-1111-111111111111",
      "event": "tool_executed",
      "timestamp": "2026-10-20T14:30:00Z",
      "pluginVersion": "0.7.1",
      "rhinoVersion": "8",
      "os": "macOS",
      "tool": "create_box",
      "durationMs": 42,
      "success": true
    }
  ]
}
```

`tool`, `durationMs`, and `success` are omitted for `plugin_started` and `mcp_connected`.

Optional diagnostics use `errorCode`, `errorType`, `errorStage`, and `rhinoCommandName`. All four are omitted for lifecycle events. Successful calls omit all error fields; only `run_rhino_command` can include `rhinoCommandName`. Unavailable values are omitted, never sent as JSON null. Diagnostics use the backend's controlled catalog so an unknown value cannot invalidate a batch.

Maintainers can inspect failures, leading commands, and individual events using the telemetry backend's existing `rhino-events errors`, `rhino-events commands`, and `rhino-events recent` reports. This release adds no automatic notifications.

| Item | Value |
|------|--------|
| Method and path | `POST /telemetry` |
| Body | `{ "events": [ ... ] }` |
| Authentication | One `X-Api-Key` header. The key is not in the URL |
| Maximum batch | 100 events |
| Success | Empty HTTP 204 |
| Timeout | 3 seconds |
| User-Agent | `MCP4Rhino/{pluginVersion}` |

The queue is not written to disk. A timeout, network error, or response other than an empty 204 discards that batch. Telemetry never blocks the Rhino UI thread or an MCP tool, and a telemetry failure does not change the tool result.

On shutdown the flush timer stops. Rhino does not wait for an in-flight send to finish.

## Configuration

The compiled endpoint is `https://events.agenttoolkit.ai/telemetry`. `MCP4RHINO_TELEMETRY_URL` overrides it. Set that override in the environment before launching Rhino when you want a different target:

```bash
export MCP4RHINO_TELEMETRY_URL="http://127.0.0.1:8080"
```

```powershell
$env:MCP4RHINO_TELEMETRY_URL = "http://127.0.0.1:8080"
```

CI writes the repository Actions secret `MCP4RHINO_TELEMETRY_API_KEY` into `net8.0/telemetry-install-token` inside the yak. The plugin reads that file and sends it as one `X-Api-Key` header. If `MCP4RHINO_TELEMETRY_API_KEY` is set in the environment, that value is used instead. The token is an install token: it ships in the package, so it is not a confidential secret. Do not put it in the URL, source, logs, or command-line arguments. A `k` query parameter is removed before the request and is not copied into the header.

If the path already ends in `/telemetry`, that path is used. Otherwise `/telemetry` is appended. Other query parameters are kept. The value must be an absolute `http` or `https` URL. An invalid override disables sending even when usage data is on. Status then shows `Usage data: on, not sending (MCP4RHINO_TELEMETRY_URL unset)`. When the URL is valid and neither the packaged token nor the environment variable is set, status shows `Usage data: on, not sending (install token unset)`.
