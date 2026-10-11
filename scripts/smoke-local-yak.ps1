# Local Rhino smoke test for the yak package (Windows).
#   powershell -ExecutionPolicy Bypass -File scripts/smoke-local-yak.ps1
#   ... -File scripts/smoke-local-yak.ps1 -NoInstall
#   ... -File scripts/smoke-local-yak.ps1 -McpOnly
#
# Exit 0: package checks passed and MCP tools/list responded.
# Exit 1: pack/install or package layout failed.
# Exit 2: package OK but MCP not up — finish Rhino steps, then re-run -McpOnly.
param(
    [switch]$NoInstall,
    [switch]$McpOnly
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$Yak = if ($env:YAK) { $env:YAK } else { "C:\Program Files\Rhino 8\System\Yak.exe" }
$Port = if ($env:MCP4RHINO_PORT) { $env:MCP4RHINO_PORT } else { "4010" }

$manifest = Get-Content (Join-Path $Root "manifest.yml")
$Version = ($manifest | Where-Object { $_ -match '^version:\s*(.+)$' } | ForEach-Object { $Matches[1].Trim() } | Select-Object -First 1)
if (-not $Version) { throw "Could not read version from manifest.yml" }

$Pkg = Join-Path $Root "dist\MCP4Rhino-$Version.yak"

function Test-YakArchive([string]$YakPath) {
    if (-not (Test-Path $YakPath)) { throw "Missing package: $YakPath" }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($YakPath)
    try {
        $names = $zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' }
        foreach ($need in @("icon.png", "manifest.yml", "net8.0/MCP4Rhino.Logic.dll", "net8.0/MCP4Rhino.rhp")) {
            if ($need -notin $names) { throw "Archive missing $need : $YakPath" }
        }
        $manifestEntry = $zip.Entries | Where-Object { ($_.FullName -replace '\\', '/') -eq "manifest.yml" } | Select-Object -First 1
        $reader = New-Object System.IO.StreamReader($manifestEntry.Open())
        $manifestText = $reader.ReadToEnd()
        $reader.Close()
        if ($manifestText -notmatch '(?m)^icon:\s*icon\.png\s*$') {
            throw "manifest.yml in $YakPath missing icon: icon.png"
        }
    }
    finally { $zip.Dispose() }
    Write-Host "OK archive: $YakPath"
}

function Get-InstalledRoot {
    if ($env:RHINO_PACKAGES_DIR) {
        $base = Join-Path $env:RHINO_PACKAGES_DIR "MCP4Rhino"
        if (-not (Test-Path $base)) { throw "RHINO_PACKAGES_DIR set but MCP4Rhino folder missing: $base" }
        return $base
    }
    $appData = $env:APPDATA
    $hits = Get-ChildItem -Path $appData -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\Rhinoceros\\packages\\8\.0\\MCP4Rhino$' } |
        Select-Object -First 1
    if (-not $hits) { throw "Installed package folder not found under %APPDATA% (or set RHINO_PACKAGES_DIR)." }
    return $hits.FullName
}

function Test-Installed {
    $root = Get-InstalledRoot
    $verDir = Join-Path $root $Version
    if (-not (Test-Path $verDir)) {
        $net = Get-ChildItem -Path $root -Directory -Recurse -Filter "net8.0" -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $net) { throw "No version folder under $root" }
        $verDir = $net.Parent.FullName
    }
    foreach ($rel in @("icon.png", "manifest.yml", "net8.0\MCP4Rhino.Logic.dll", "net8.0\MCP4Rhino.rhp")) {
        $p = Join-Path $verDir $rel
        if (-not (Test-Path $p)) { throw "Installed package missing $rel under $verDir" }
    }
    Write-Host "OK installed: $verDir"
}

function Write-RhinoSteps {
    Write-Host ""
    Write-Host "Next (manual — Rhino cannot be started from this script):"
    Write-Host "  1. Quit and reopen Rhino 8."
    Write-Host "  2. Wait for Idle auto-start (or run MCP4Rhino / MCP4RhinoStatus)."
    Write-Host "  3. Expect: MCP4Rhino ready: http://127.0.0.1:$Port/mcp"
    Write-Host "  4. Re-run: powershell -ExecutionPolicy Bypass -File scripts/smoke-local-yak.ps1 -McpOnly"
    Write-Host "Logs: %LOCALAPPDATA%\MCP4Rhino\mcp4rhino.log"
}

function Test-Mcp {
    $url = "http://127.0.0.1:$Port/mcp"
    try {
        $resp = Invoke-RestMethod -Method Post -Uri $url -ContentType "application/json" -TimeoutSec 5 -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
    }
    catch {
        return $false
    }
    if (-not $resp.result.tools) {
        Write-Host "MCP responded but tools/list payload looks wrong."
        return $false
    }
    Write-Host "OK MCP tools/list on $url"
    return $true
}

if (-not $McpOnly) {
    if (-not $NoInstall) {
        & (Join-Path $PSScriptRoot "install-yak.ps1")
    }
    elseif (-not (Test-Path $Pkg)) {
        throw "No $Pkg — run without -NoInstall, or scripts/pack-yak.ps1 first."
    }
    Test-YakArchive $Pkg
    Test-Installed
    if (Test-Path $Yak) { & $Yak list }
}

if (Test-Mcp) {
    if ($McpOnly) { Write-Host "Smoke passed: MCP tools/list." }
    else { Write-Host "Smoke passed: package + MCP." }
    exit 0
}

if ($McpOnly) { Write-Host "MCP not reachable on port $Port." }
else { Write-Host "Package checks OK; MCP not reachable on port $Port." }
Write-RhinoSteps
exit 2

