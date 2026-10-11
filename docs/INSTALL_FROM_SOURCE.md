# Install MCP4Rhino from source

For contributors iterating on this repository. End users should install from [Rhino Package Manager](../README.md#installation) or a [GitHub release](https://github.com/isque03/mcp4rhino/releases).

**Prerequisites:** Rhino 8, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Node.js (`npx`) for AI clients.

```bash
git clone https://github.com/isque03/mcp4rhino.git
cd mcp4rhino
```

| Host | Install into Rhino |
|------|--------------------|
| **macOS** | `bash scripts/install-yak.sh` |
| **Windows 11** | `powershell -ExecutionPolicy Bypass -File scripts/install-yak.ps1` |

Each script builds the solution, writes `dist/MCP4Rhino-<version>.yak` (universal; version from [manifest.yml](../manifest.yml)), and installs it under the Rhino 8 packages folder.

Pack only (no install):

- macOS: `bash scripts/pack-yak.sh`
- Windows: `scripts/pack-yak.ps1`

Override the Yak CLI with env `YAK` if needed.

After install: restart Rhino. By default the MCP server auto-starts shortly after Rhino is idle. Confirm with **MCP4Rhino → Status…** (Windows: under Tools; macOS: top-level menu) or `MCP4RhinoStatus`, or run `MCP4Rhino` if auto-start is disabled.

## Updating from source

Already installed from this clone and pulling new commits?

1. `git pull` (or check out the release / feature branch).
2. Pick the upgrade path:

| What changed | What to run | Rhino restart? |
|--------------|-------------|----------------|
| Only Tools / Logic | Hot-reload script, then `MCP4RhinoReload` or MCP tool `mcp4rhino_reload` | No |
| Host `.rhp`, new Rhino commands, Contracts, or unsure | Full `install-yak`, then quit and reopen Rhino | Yes |
| Docs / skills / `CLAUDE.md` only | Nothing to install | — |

Hot-reload:

| Host | Command |
|------|---------|
| **macOS** | `bash scripts/hot-reload-tools.sh` |
| **Windows** | `powershell -ExecutionPolicy Bypass -File scripts/hot-reload-tools.ps1` |

Then in Rhino: `MCP4RhinoReload`. The MCP URL stays `http://127.0.0.1:4010/mcp` unless `MCP4RHINO_PORT` changed.

Agent-oriented detail: [CLAUDE.md](../CLAUDE.md).
