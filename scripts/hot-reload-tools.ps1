# Rebuild Tools + Logic and copy into the installed yak package (Windows).
# Run from repo root:  powershell -ExecutionPolicy Bypass -File scripts/hot-reload-tools.ps1
# Then in Rhino: MCP4RhinoReload  (or MCP tool mcp4rhino_reload)
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

dotnet build "$Root\src\MCP4Rhino.Tools\MCP4Rhino.Tools.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

$toolsOut = Join-Path $Root "src\MCP4Rhino.Tools\bin\Release\net8.0"

function Get-Mcp4RhinoPackageRoot {
    if ($env:RHINO_PACKAGES_DIR) {
        return (Join-Path ($env:RHINO_PACKAGES_DIR.TrimEnd('\', '/')) "MCP4Rhino")
    }
    Get-ChildItem $env:APPDATA -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $p = Join-Path $_.FullName "Rhinoceros\packages\8.0\MCP4Rhino"
        if (Test-Path $p) { return $p }
    }
    return $null
}

$pkgRoot = Get-Mcp4RhinoPackageRoot
if (-not $pkgRoot) {
    throw "Package folder not found under %APPDATA%\*\Rhinoceros\packages\8.0\MCP4Rhino — run scripts/install-yak.ps1 first, or set RHINO_PACKAGES_DIR."
}

$dest = Get-ChildItem $pkgRoot -Directory | Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName "net8.0" } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1
if (-not $dest) { throw "No net8.0 folder under $pkgRoot" }

Copy-Item "$toolsOut\MCP4Rhino.Tools.dll" $dest -Force
foreach ($f in @("MCP4Rhino.Logic.dll", "MCP4Rhino.Contracts.dll", "System.Drawing.Common.dll", "Microsoft.Win32.SystemEvents.dll")) {
    $src = Join-Path $toolsOut $f
    if (Test-Path $src) { Copy-Item $src $dest -Force }
}

Write-Host "Copied Tools/Logic into $dest"
Write-Host "In Rhino: MCP4RhinoReload  (or MCP tool mcp4rhino_reload)"
