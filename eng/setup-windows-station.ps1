param([string]$Root = (Split-Path $PSScriptRoot -Parent))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT' -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64') {
    throw 'Run this installer in native 64-bit Windows PowerShell on an x64 PC.'
}
$Root = (Resolve-Path $Root).Path
$sdk = (Get-Content "$Root/global.json" -Raw | ConvertFrom-Json).sdk.version
$manifest = Get-Content "$Root/eng/toolchain.json" -Raw | ConvertFrom-Json
$version = $manifest.godot.version -replace '\.stable.*$', ''
if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Install Python 3.13 first (winget install --exact --id Python.Python.3.13), then reopen PowerShell.'
}
& python -c "import sys; assert sys.version_info >= (3, 12), 'Python 3.12+ required'"
if ($LASTEXITCODE -ne 0) { throw 'A working Python 3.12+ is required; disable the Microsoft Store python alias if needed.' }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$downloads = "$Root/.tools/downloads"
New-Item -ItemType Directory -Force $downloads | Out-Null
$dotnet = "$Root/.tools/dotnet/dotnet.exe"
Push-Location $Root
try {
    if (-not (Test-Path $dotnet) -or -not ((& $dotnet --list-sdks) -match "^$([regex]::Escape($sdk)) ")) {
        $installer = "$downloads/dotnet-install.ps1"
        Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Version $sdk -Architecture x64 -InstallDir "$Root/.tools/dotnet" -NoPath
        if ($LASTEXITCODE -ne 0) { throw 'Pinned .NET SDK installation failed.' }
    }
    $archiveName = "Godot_v$version-stable_mono_win64.zip"
    $release = "https://github.com/godotengine/godot-builds/releases/download/$version-stable"
    $godot = "$Root/.tools/godot/windows/Godot_v$version-stable_mono_win64/Godot_v$version-stable_mono_win64_console.exe"
    if (-not (Test-Path $godot)) {
        $archive = "$downloads/$archiveName"
        $sums = (Invoke-WebRequest -UseBasicParsing "$release/SHA512-SUMS.txt").Content
        $line = @($sums -split "`n" | Where-Object { $_.Trim().EndsWith("  $archiveName") })
        if ($line.Count -ne 1) { throw 'Godot release checksum is missing or ambiguous.' }
        $expected = ($line[0].Trim() -split '\s+')[0]
        if (-not (Test-Path $archive) -or (Get-FileHash $archive -Algorithm SHA512).Hash -ine $expected) {
            Invoke-WebRequest -UseBasicParsing "$release/$archiveName" -OutFile $archive
        }
        if ((Get-FileHash $archive -Algorithm SHA512).Hash -ine $expected) { throw 'Godot SHA512 mismatch.' }
        Expand-Archive -Path $archive -DestinationPath "$Root/.tools/godot/windows" -Force
    }
    $actual = & $godot --headless --version
    if ($LASTEXITCODE -ne 0 -or $actual -ne $manifest.godot.version) { throw "Unexpected Godot version: $actual" }
    if ((& $dotnet --version) -ne $sdk -or $LASTEXITCODE -ne 0) { throw 'Unexpected .NET SDK version.' }
    # Runtime models are supplied as GLB; do not require Blender just to import them.
    $override = "$Root/game/override.cfg"
    $settings = "[filesystem]`nimport/blender/enabled=false`n"
    if (Test-Path $override) {
        $filesystem = @((Get-Content $override -Raw) -split '(?m)^\[' | Where-Object { $_ -match '^filesystem\]' })
        if ($filesystem.Count -ne 1 -or $filesystem[0] -notmatch '(?m)^import/blender/enabled\s*=\s*false\s*$') {
            throw 'Existing game/override.cfg was preserved. Merge the Blender-disable setting manually, then rerun setup.'
        }
    } else {
        Set-Content -Path $override -Value $settings -Encoding ASCII
    }
    $exclude = & git rev-parse --git-path info/exclude
    if ($LASTEXITCODE -ne 0) { throw 'A Git checkout is required to exclude the station-only override.' }
    if (-not (Test-Path $exclude) -or -not ((Get-Content $exclude) -contains '/game/override.cfg')) {
        Add-Content -Path $exclude -Value "`n/game/override.cfg" -Encoding ASCII
    }
    Write-Host "Installed .NET $sdk and Godot $actual. No game was started."
    Write-Host 'Next: powershell -NoProfile -ExecutionPolicy Bypass -File eng/run-windows-check.ps1 -Mode smoke -Scene res://tests/act1_main_menu_smoke_test.tscn'
} finally { Pop-Location }
