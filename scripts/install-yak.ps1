# Pack universal .yak into dist/, then install it locally (Windows).
# Run from repo root:  powershell -ExecutionPolicy Bypass -File scripts/install-yak.ps1
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$Yak = if ($env:YAK) { $env:YAK } else { "C:\Program Files\Rhino 8\System\Yak.exe" }
if (-not (Test-Path $Yak)) {
    throw "Yak not found at '$Yak'. Install Rhino 8 or set env YAK to Yak.exe."
}

& (Join-Path $PSScriptRoot "pack-yak.ps1")

$manifest = Get-Content (Join-Path $Root "manifest.yml")
$Version = ($manifest | Where-Object { $_ -match '^version:\s*(.+)$' } | ForEach-Object { $Matches[1].Trim() } | Select-Object -First 1)
if (-not $Version) { throw "Could not read version from manifest.yml" }

$pkg = Join-Path $Root "dist\MCP4Rhino-$Version.yak"
if (-not (Test-Path $pkg)) { throw "Expected package not found: $pkg" }

& $Yak install $pkg
if ($LASTEXITCODE -ne 0) { throw "yak install failed" }
& $Yak list

Write-Host "Installed. In Rhino: MCP4Rhino once after host changes; MCP4RhinoReload or tool mcp4rhino_reload for tools-only."
Write-Host "Package lands under %APPDATA%\*\Rhinoceros\packages\8.0\MCP4Rhino\. Hot-reload can use RHINO_PACKAGES_DIR."
