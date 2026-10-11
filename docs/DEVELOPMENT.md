# Development

Agent playbook for coding in this repo: [CLAUDE.md](../CLAUDE.md). Architecture roadmap: [ARCHITECTURE_AGENT.md](ARCHITECTURE_AGENT.md). Tests: [TESTING.md](TESTING.md). Publishing: [PUBLISHING.md](PUBLISHING.md).

## Layout

| Path | Role | Reload |
|------|------|--------|
| [src/MCP4Rhino/](../src/MCP4Rhino/) | Host `.rhp`, HTTP listener, commands | Restart Rhino |
| [src/MCP4Rhino.Tools/](../src/MCP4Rhino.Tools/) | Tool implementations | Hot-reload |
| [src/MCP4Rhino.Logic/](../src/MCP4Rhino.Logic/) | Pure helpers; loaded with Tools | Hot-reload with Tools |
| [src/MCP4Rhino.Contracts/](../src/MCP4Rhino.Contracts/) | Shared bridge interfaces | Usually with host |

Prefer editing **Tools** (and **Logic** helpers) for new MCP capabilities. Keep the host thin.

## Daily loop (tools only)

Rebuild Tools/Logic, copy into the installed package, then hot-reload (no Rhino restart):

| Host | Command |
|------|---------|
| **macOS** | `bash scripts/hot-reload-tools.sh` |
| **Windows** | `powershell -ExecutionPolicy Bypass -File scripts/hot-reload-tools.ps1` |

Then in Rhino: `MCP4RhinoReload` (or MCP tool `mcp4rhino_reload`). After host `.rhp` changes, restart Rhino.

## Commands and environment

| Item | Meaning |
|------|---------|
| `MCP4Rhino` | Start the MCP HTTP server (also used if auto-start was skipped) |
| `MCP4RhinoStop` | Stop the server |
| `MCP4RhinoStatus` | Print running state, endpoint, log path, autostart flag |
| `MCP4RhinoReload` | Hot-reload `MCP4Rhino.Tools.dll` (+ Logic) |
| `MCP4RHINO_PORT` | Listen port (default **4010**) |
| `MCP4RHINO_AUTOSTART` | Set to `0` / `false` / `no` to disable Idle auto-start |
| `MCP4RHINO_TOOLS_PATH` | Override directory containing `MCP4Rhino.Tools.dll` |

Default MCP endpoint: `http://127.0.0.1:4010/mcp`.

## Local smoke

| Host | Command |
|------|---------|
| **macOS** | `bash scripts/smoke-local-yak.sh` |
| **Windows** | `powershell -ExecutionPolicy Bypass -File scripts/smoke-local-yak.ps1` |

Coverage gate: `bash scripts/test-coverage.sh` (≥80% line on Logic + Contracts).

## Demo assets

Office-park time-lapse plan: [README_DEMO_PLAN.md](README_DEMO_PLAN.md).
