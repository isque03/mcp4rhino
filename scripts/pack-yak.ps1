# Build Release and produce one universal .yak under dist/ (mac + win).
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

$icon = Join-Path $Root "icon.png"
if (-not (Test-Path $icon)) { throw "Missing icon.png at repo root" }

dotnet build "$Root\MCP4Rhino.sln" -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

$stage = Join-Path $env:TEMP ("mcp4rhino-yak-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Force -Path "$stage\net8.0" | Out-Null

$hostOut = Join-Path $Root "src\MCP4Rhino\bin\Release\net8.0"
$toolsOut = Join-Path $Root "src\MCP4Rhino.Tools\bin\Release\net8.0"
$ghOut = Join-Path $Root "src\MCP4Rhino.Grasshopper\bin\Release\net8.0"

$required = @(
    (Join-Path $hostOut "MCP4Rhino.rhp"),
    (Join-Path $hostOut "MCP4Rhino.Contracts.dll"),
    (Join-Path $hostOut "MCP4Rhino.Logic.dll"),
    (Join-Path $hostOut "MCP4Rhino.rui"),
    (Join-Path $toolsOut "MCP4Rhino.Tools.dll")
    (Join-Path $ghOut "MCP4Rhino.Grasshopper.dll")
)
foreach ($path in $required) {
    if (-not (Test-Path $path)) { throw "Required build output missing: $path" }
}

Copy-Item (Join-Path $hostOut "MCP4Rhino.rhp") "$stage\net8.0\"
Copy-Item (Join-Path $hostOut "MCP4Rhino.Contracts.dll") "$stage\net8.0\"
Copy-Item (Join-Path $hostOut "MCP4Rhino.Logic.dll") "$stage\net8.0\"
Copy-Item (Join-Path $hostOut "MCP4Rhino.rui") "$stage\net8.0\"
Copy-Item (Join-Path $toolsOut "MCP4Rhino.Tools.dll") "$stage\net8.0\"
Copy-Item (Join-Path $ghOut "MCP4Rhino.Grasshopper.dll") "$stage\net8.0\"
Copy-Item (Join-Path $Root "skills") (Join-Path $stage "skills") -Recurse

$skip = @("RhinoCommon.dll", "Rhino.UI.dll", "Eto.dll", "Grasshopper.dll", "GH_IO.dll", "MCP4Rhino.rhp")
Get-ChildItem "$toolsOut\*.dll" | Where-Object { $_.Name -notin $skip } |
    Copy-Item -Destination "$stage\net8.0\" -Force

Copy-Item (Join-Path $Root "manifest.yml") $stage
Copy-Item $icon $stage

$tokenPath = Join-Path $stage "net8.0\telemetry-install-token"
$token = $env:MCP4RHINO_TELEMETRY_API_KEY
if ($null -eq $token) { $token = "" }
$token = $token.Replace("`r", "").Trim()
if ([string]::IsNullOrWhiteSpace($token)) {
    if ($env:REQUIRE_TELEMETRY_INSTALL_TOKEN) {
        throw "MCP4RHINO_TELEMETRY_API_KEY is missing. Add it as a repository Actions secret."
    }
} elseif ($token.Contains("`n")) {
    throw "MCP4RHINO_TELEMETRY_API_KEY must be a single line."
} else {
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($tokenPath, $token, $utf8)
}

$dist = Join-Path $Root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$dest = Join-Path $dist "MCP4Rhino-$Version.yak"
Remove-Item $dest -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dist "MCP4Rhino-$Version-mac.yak") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dist "MCP4Rhino-$Version-win.yak") -ErrorAction SilentlyContinue

Push-Location $stage
try {
    Get-ChildItem *.yak -ErrorAction SilentlyContinue | Remove-Item -Force
    & $Yak build
    if ($LASTEXITCODE -ne 0) { throw "yak build failed" }
    $yakPkg = Get-ChildItem *.yak -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $yakPkg -or $yakPkg.Extension -ne ".yak") {
        throw "No .yak produced by yak build"
    }
    Copy-Item $yakPkg.FullName $dest -Force
    Write-Host "Wrote $dest"
}
finally {
    Pop-Location
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($dest)
try {
    $names = $zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' }
    foreach ($need in @(
            "icon.png", "manifest.yml",
            "net8.0/MCP4Rhino.Logic.dll", "net8.0/MCP4Rhino.rhp",
            "net8.0/MCP4Rhino.Tools.dll", "net8.0/MCP4Rhino.Contracts.dll",
            "net8.0/MCP4Rhino.rui"
            "net8.0/MCP4Rhino.Grasshopper.dll", "skills/grasshopper-parametric-design/SKILL.md"
        )) {
        if ($need -notin $names) { throw "Archive missing $need : $dest" }
    }
    if ($names | Where-Object { $_ -match '^net8.0/(Grasshopper|GH_IO|RhinoCommon)\.dll$' }) {
        throw "Archive must not bundle Rhino/Grasshopper SDK assemblies"
    }
    $manifestEntry = $zip.Entries | Where-Object { ($_.FullName -replace '\\', '/') -eq "manifest.yml" } | Select-Object -First 1
    $reader = New-Object System.IO.StreamReader($manifestEntry.Open())
    $manifestText = $reader.ReadToEnd()
    $reader.Close()
    if ($manifestText -notmatch '(?m)^icon:\s*icon\.png\s*$') {
        throw "manifest.yml in $dest missing icon: icon.png"
    }
    if ($manifestText -notmatch '(?m)^name:\s*MCP4Rhino\s*$') {
        throw "manifest.yml in $dest unexpected name (expected MCP4Rhino)"
    }
    $escapedVersion = [regex]::Escape($Version)
    if ($manifestText -notmatch "(?m)^version:\s*$escapedVersion\s*$") {
        throw "manifest.yml in $dest version does not match $Version"
    }
    $tokenName = "net8.0/telemetry-install-token"
    $tokenPresent = $tokenName -in $names
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        if (-not $tokenPresent) { throw "Package install token does not match the pack environment." }
        $tokenEntry = $zip.Entries | Where-Object { ($_.FullName -replace '\\', '/') -eq $tokenName } | Select-Object -First 1
        $tokenReader = New-Object System.IO.StreamReader($tokenEntry.Open())
        $packedToken = $tokenReader.ReadToEnd()
        $tokenReader.Close()
        if ($packedToken -ne $token) { throw "Package install token does not match the pack environment." }
    } elseif ($tokenPresent) {
        throw "Package contains an install token, but MCP4RHINO_TELEMETRY_API_KEY was empty."
    }
}
finally { $zip.Dispose() }
$skillZip = Join-Path $dist "MCP4Rhino-$Version-skills.zip"
Compress-Archive -Path (Join-Path $Root "skills\*") -DestinationPath $skillZip -Force
Copy-Item (Join-Path $Root "scripts\grasshopper-smoke.py") (Join-Path $dist "grasshopper-smoke.py") -Force
Write-Host "OK package: $dest"

Write-Host "Packed MCP4Rhino $Version → $dest"
Write-Host "Release publication uses the exact CI Build artifact via the Publish workflow; see docs/PUBLISHING.md."
