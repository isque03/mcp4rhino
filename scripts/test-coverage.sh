#!/usr/bin/env bash
# Run unit tests with Coverlet; fail if line coverage on MCP4Rhino.Logic (+ Contracts) < 80%.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"

# Include/Threshold are set in MCP4Rhino.Tests.csproj (Logic + Contracts, 80% line).
dotnet test "$ROOT/tests/MCP4Rhino.Tests/MCP4Rhino.Tests.csproj" -c Release \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput="$ROOT/tests/MCP4Rhino.Tests/coverage/"

echo "Coverage gate passed (≥80% line on MCP4Rhino.Logic + MCP4Rhino.Contracts)."
