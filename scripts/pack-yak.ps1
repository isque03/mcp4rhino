# Build Release and produce mac + win .yak packages under dist/.
# Run from repo root:  powershell -ExecutionPolicy Bypass -File scripts/pack-yak.ps1
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$Yak = if ($env:YAK) { $env:YAK } else { "C:\Program Files\Rhino 8\System\Yak.exe" }
if (-not (Test-Path $Yak)) {
    throw "Yak not found at '$Yak'. Install Rhino 8 or set env YAK to Yak.exe."
}

$manifest = Get-Content (Join-Path $Root "manifest.yml")
$Version = ($manifest | Where-Object { $_ -match '^version:\s*(.+)$' } | ForEach-Object { $Matches[1].Trim() } | Select-Object -First 1)
if (-not $Version) { throw "Could not read version from manifest.yml" }

dotnet build "$Root\MCP4Rhino.sln" -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

$stage = Join-Path $env:TEMP ("mcp4rhino-yak-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Force -Path "$stage\net8.0" | Out-Null

$hostOut = Join-Path $Root "src\MCP4Rhino\bin\Release\net8.0"
$toolsOut = Join-Path $Root "src\MCP4Rhino.Tools\bin\Release\net8.0"

Copy-Item "$hostOut\MCP4Rhino.rhp" "$stage\net8.0\"
Copy-Item "$hostOut\MCP4Rhino.Contracts.dll" "$stage\net8.0\"
$logicHost = Join-Path $hostOut "MCP4Rhino.Logic.dll"
if (Test-Path $logicHost) { Copy-Item $logicHost "$stage\net8.0\" }
Copy-Item "$toolsOut\MCP4Rhino.Tools.dll" "$stage\net8.0\"

$skip = @("RhinoCommon.dll", "Rhino.UI.dll", "Eto.dll", "MCP4Rhino.rhp")
Get-ChildItem "$toolsOut\*.dll" | Where-Object { $_.Name -notin $skip } |
    Copy-Item -Destination "$stage\net8.0\" -Force

Copy-Item (Join-Path $Root "manifest.yml") $stage
Copy-Item (Join-Path $Root "icon.png") $stage

$dist = Join-Path $Root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Remove-Item (Join-Path $dist "MCP4Rhino-$Version-mac.yak") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dist "MCP4Rhino-$Version-win.yak") -ErrorAction SilentlyContinue

Push-Location $stage
try {
    foreach ($platform in @("mac", "win")) {
        Get-ChildItem *.yak -ErrorAction SilentlyContinue | Remove-Item -Force
        & $Yak build --platform $platform
        if ($LASTEXITCODE -ne 0) { throw "yak build --platform $platform failed" }
        $yakPkg = Get-ChildItem *.yak | Select-Object -First 1
        if (-not $yakPkg) { throw "No .yak produced for platform $platform" }
        $dest = Join-Path $dist "MCP4Rhino-$Version-$platform.yak"
        Copy-Item $yakPkg.FullName $dest -Force
        Write-Host "Wrote $dest"
    }
}
finally {
    Pop-Location
}

Write-Host "Packed MCP4Rhino $Version → dist/MCP4Rhino-$Version-{mac,win}.yak"
Write-Host "Publish checklist: yak login; yak push dist/MCP4Rhino-$Version-mac.yak; yak push dist/MCP4Rhino-$Version-win.yak"
