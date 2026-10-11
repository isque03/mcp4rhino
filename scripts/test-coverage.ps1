# Run unit tests with Coverlet; fail if line coverage on MCP4Rhino.Logic (+ Contracts) < 80%.
# Run from repo root:  powershell -ExecutionPolicy Bypass -File scripts/test-coverage.ps1
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$proj = Join-Path $Root "tests\MCP4Rhino.Tests\MCP4Rhino.Tests.csproj"
$out = Join-Path $Root "tests\MCP4Rhino.Tests\coverage\"

dotnet test $proj -c Release `
  /p:CollectCoverage=true `
  /p:CoverletOutputFormat=cobertura `
  /p:CoverletOutput=$out
if ($LASTEXITCODE -ne 0) { throw "dotnet test / coverage gate failed" }

Write-Host "Coverage gate passed (≥80% line on MCP4Rhino.Logic + MCP4Rhino.Contracts)."
