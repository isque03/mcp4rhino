# Build, pack, and install MCP4Rhino yak for Rhino 8 on Windows.
# Run from repo root:  powershell -ExecutionPolicy Bypass -File scripts/install-yak.ps1
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$Yak = if ($env:YAK) { $env:YAK } else { "C:\Program Files\Rhino 8\System\Yak.exe" }
if (-not (Test-Path $Yak)) {
    throw "Yak not found at '$Yak'. Install Rhino 8 or set env YAK to Yak.exe."
}

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

Push-Location $stage
try {
    & $Yak build --platform win
    if ($LASTEXITCODE -ne 0) { throw "yak build failed" }
    $yakPkg = Get-ChildItem *.yak | Select-Object -First 1
    if (-not $yakPkg) { throw "No .yak produced in $stage" }
    & $Yak install $yakPkg.FullName
    if ($LASTEXITCODE -ne 0) { throw "yak install failed" }
    & $Yak list
}
finally {
    Pop-Location
}

Write-Host "Installed. In Rhino: MCP4Rhino once after host changes; MCP4RhinoReload or tool mcp4rhino_reload for tools-only."
Write-Host "Package lands under %APPDATA%\*\Rhinoceros\packages\8.0\MCP4Rhino\ (or set RHINO_PACKAGES_DIR)."
