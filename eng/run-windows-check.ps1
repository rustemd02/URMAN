param(
    [ValidateSet('build', 'prepare', 'play', 'smoke', 'capture')][string]$Mode = 'smoke',
    [string]$Scene,
    [ValidateRange(1, 86400)][int]$TimeoutSeconds = 300,
    [switch]$Headless,
    [string]$ViewPoints,
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$ToolsRoot,
    [string]$Provenance,
    [string]$Output,
    [string]$Python = 'python'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
if ($env:OS -ne 'Windows_NT') { throw 'Game checks belong on the Windows station. Local fallback is disabled.' }
$Root = (Resolve-Path $Root).Path
if (-not $ToolsRoot) { $ToolsRoot = $Root }
$ToolsRoot = (Resolve-Path $ToolsRoot).Path
if ($Mode -eq 'smoke' -and (-not $Scene -or $Scene -notlike 'res://tests/*.tscn')) {
    throw 'Select exactly one existing res://tests/*.tscn scene.'
}
if (-not $Scene) { $Scene = 'res://scenes/act1_demo.tscn' }
if ($Scene -notmatch '^res://[\w/.-]+\.tscn$' -or $Scene.Contains('..') -or
    -not (Test-Path (Join-Path "$Root/game" $Scene.Substring(6)))) { throw 'Invalid or missing scene.' }
if ($Mode -eq 'capture' -and (-not $ViewPoints -or $Headless)) { throw 'Capture requires -ViewPoints and a native window.' }
if ($Mode -eq 'play' -and $Headless) { throw 'Play requires a native window.' }
$manifest = Get-Content "$Root/eng/toolchain.json" -Raw | ConvertFrom-Json
$version = $manifest.godot.version -replace '\.stable.*$', ''
$godot = "$ToolsRoot/.tools/godot/windows/Godot_v$version-stable_mono_win64/Godot_v$version-stable_mono_win64_console.exe"
$dotnet = "$ToolsRoot/.tools/dotnet/dotnet.exe"
$guard = "$ToolsRoot/eng/protected_run.py"
$bounded = "$ToolsRoot/eng/remote_common.py"
foreach ($path in @($godot, $dotnet, $guard)) {
    if (-not (Test-Path $path)) { throw "Missing $path; run eng/setup-windows-station.ps1 first." }
}
$env:DOTNET_ROOT = "$ToolsRoot/.tools/dotnet"
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_HOME = "$ToolsRoot/.tools/dotnet-home"
$env:NUGET_PACKAGES = "$ToolsRoot/.tools/nuget"
$env:MSBUILDUSESERVER = '0'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
# ponytail: one station run at a time; separate workers only if another PC is added.
$runLock = [IO.File]::Open("$ToolsRoot/.tools/windows-station.lock", 'OpenOrCreate', 'ReadWrite', 'None')
if (-not $Output) { $Output = "$Root/.codex-captures/windows/$((Get-Date).ToString('yyyyMMdd-HHmmss'))-$([guid]::NewGuid().ToString('N').Substring(0,8))" }
New-Item -ItemType Directory -Force $output | Out-Null
$receipt = [ordered]@{ host = $env:COMPUTERNAME; mode = $Mode; scene = $Scene; headless = [bool]$Headless;
    started = (Get-Date).ToUniversalTime().ToString('o'); status = 'RUNNING'; logs = $output; humanPlaytest = 'not-run'; steps = @{} }
function Invoke-Logged([string]$Name, [string]$Exe, [string[]]$Arguments, [int]$Deadline = 180) {
    # PS5 treats native stderr as ErrorRecord. Preserve it and decide from exit + log.
    $savedPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $stepLines = @()
    try { & $Python $bounded "$Deadline" $Exe @Arguments 2>&1 | ForEach-Object { $_.ToString() } | Tee-Object -Variable stepLines; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $savedPreference }
    [IO.File]::WriteAllLines("$output/$Name.log", [string[]]@($stepLines), [Text.UTF8Encoding]::new($false))
    $receipt.steps[$Name] = @{ exitCode = $code }
    if ($code -ne 0) { throw "$Name exited $code; see $output/$Name.log" }
    $log = Get-Content "$output/$Name.log" -Raw
    if ($log -match '(?im)(^|\s)(ERROR:|SCRIPT ERROR:)|\bFAIL\b|Unhandled exception') { throw "$Name reported engine errors; inspect the log before accepting it." }
    if ($Arguments -contains $guard -and $log -notmatch 'userdata guard: original files.*restored.*verified') {
        throw "$Name did not confirm userdata restoration."
    }
}
Push-Location $Root
$savedCapture = $env:URMAN_VIEW_CAPTURE
$savedPoints = $env:URMAN_VIEW_POINTS
try {
    Remove-Item Env:URMAN_VIEW_CAPTURE, Env:URMAN_VIEW_POINTS -ErrorAction SilentlyContinue
    if ($Provenance) {
        & $Python -c "import sys,json; from pathlib import Path; sys.path.insert(0,sys.argv[1]); from remote_common import verify_source; verify_source(Path(sys.argv[2]),json.loads(Path(sys.argv[3]).read_text(encoding='utf-8-sig')))" "$ToolsRoot/eng" $Root $Provenance
        if ($LASTEXITCODE -ne 0) { throw 'Snapshot provenance/hash verification failed.' }
        $receipt.source = Get-Content $Provenance -Raw | ConvertFrom-Json
        $receipt.commit = $receipt.source.base_commit
        $receipt.dirtyPaths = $receipt.source.dirty_paths
        $receipt.snapshotId = $receipt.source.snapshot_id
    } else {
        $receipt.commit = (& git rev-parse HEAD)
        $receipt.dirtyPaths = @(& git status --porcelain)
        if ($LASTEXITCODE -ne 0) { throw 'Cannot record Git source identity.' }
    }
    $receipt.sdk = (& $dotnet --version)
    if ($LASTEXITCODE -ne 0 -or $receipt.sdk -ne (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version) { throw 'Pinned SDK is missing.' }
    $receipt.godot = (& $godot --headless --version)
    if ($LASTEXITCODE -ne 0 -or $receipt.godot -ne $manifest.godot.version) { throw 'Pinned Godot .NET is missing.' }
    if (Test-Path 'game/override.cfg') { $receipt.stationOverrideSha256 = (Get-FileHash 'game/override.cfg' -Algorithm SHA256).Hash }
    # Same content owner and commands as eng/compile-game-content.sh on Mac.
    Invoke-Logged 'content-build' $dotnet @('build', 'tools-dotnet/Urman.ContentCli/Urman.ContentCli.csproj', '-v', 'q', '--nologo', '--disable-build-servers')
    foreach ($campaign in @('urman.chapter1', 'urman.fullgame')) {
        Invoke-Logged "content-$campaign" $dotnet @('tools-dotnet/Urman.ContentCli/bin/Debug/net10.0/Urman.ContentCli.dll', 'compile', '--root', $Root, '--campaign', $campaign, '--out', "game/content/$campaign.compiled.v1.json")
    }
    Invoke-Logged 'document-images' $Python @('eng/package-document-images.py')
    Invoke-Logged 'build'  $dotnet @('build', 'game/Urman.Game.csproj', '-v', 'q', '--nologo', '--disable-build-servers')
    if ($Mode -ne 'build') {
        # A fresh isolated snapshot has no imported custom font yet. Godot loads
        # the project font before importing it; omit that assignment for import
        # only. Editor import ignores override.cfg, so temporarily change only
        # the isolated project copy, then restore and hash-check its exact bytes.
        $importProject = "$Root/game/project.godot"
        $projectBytes = $null
        if ($Provenance) {
            $projectBytes = [IO.File]::ReadAllBytes($importProject)
            $importText = [Text.Encoding]::UTF8.GetString($projectBytes) -replace '(?m)^theme/custom_font=.*$', 'theme/custom_font=""'
            $importText += "`n[editor]`nimport/use_multiple_threads=false`n"
            [IO.File]::WriteAllText($importProject, $importText, [Text.UTF8Encoding]::new($false))
            $receipt.importFontBootstrap = 'custom_font omitted in isolated project during import only; original project restored before game'
            $receipt.importThreading = 'serial import in isolated project only, to avoid the observed Windows font-import crash'
        }
        try {
            Invoke-Logged 'import' $Python @($guard, '--clean', '--timeout', "$TimeoutSeconds", $godot, '--headless', '--editor', '--path', "$Root/game", '--import') ($TimeoutSeconds + 30)
        } finally {
            if ($null -ne $projectBytes) {
                [IO.File]::WriteAllBytes($importProject, $projectBytes)
                $receipt.projectRestoredSha256 = (Get-FileHash $importProject -Algorithm SHA256).Hash
                if ($receipt.projectRestoredSha256 -ne $receipt.source.files.'game/project.godot'.sha256) { throw 'Project settings were not restored before game.' }
            }
        }
    }
    if ($Mode -in @('play', 'smoke', 'capture')) {
        $arguments = @($guard, '--clean', '--timeout', "$TimeoutSeconds", $godot, '--path', "$Root/game", '--resolution', '1920x1080')
        if ($Headless) { $arguments += '--headless' } else { $arguments += '--always-on-top' }
        if ($Mode -eq 'capture') {
            $env:URMAN_VIEW_CAPTURE = "$output/frames"
            $env:URMAN_VIEW_POINTS = $ViewPoints
        }
        $arguments += $Scene
        if ($Mode -eq 'smoke') { $arguments += '--urman-smoke-background-input' }
        Invoke-Logged 'game' $Python $arguments ($TimeoutSeconds + 30)
        if (-not $Headless) {
            $rendererLines = @([IO.File]::ReadAllLines("$output/game.log") | Where-Object { $_ -match 'Vulkan|Direct3D|D3D12|OpenGL API' })
            if (-not $rendererLines -or ($rendererLines -join ' ') -match '(?i)llvmpipe|SwiftShader|software rasterizer|Microsoft Basic Render') { throw 'Native hardware renderer was not confirmed by the engine log.' }
            $receipt.renderer = $rendererLines
        }
        if ($Mode -eq 'smoke' -and $Scene -eq 'res://tests/act1_main_menu_smoke_test.tscn' -and (Get-Content "$output/game.log" -Raw) -notmatch '\bPASS\b') { throw 'Main menu smoke did not emit its expected PASS marker.' }
        if ($Mode -eq 'smoke' -and $Scene -eq 'res://tests/player_settings_smoke_test.tscn' -and (Get-Content "$output/game.log" -Raw) -notmatch 'player-settings-smoke: SaveGameV3 settings') { throw 'Player settings smoke did not emit its expected completion marker.' }
        $receipt.userdataRestored = $true
        $receipt.gameExitCode = 0
        if ($Mode -eq 'capture' -and -not (Test-Path "$output/frames/*.png")) { throw 'No capture frames were produced.' }
    }
    $dll = "$Root/game/.godot/mono/temp/bin/Debug/Urman.Game.dll"
    if (-not (Test-Path $dll)) { throw 'Fresh game DLL was not found.' }
    $receipt.dll = @{ path = $dll; sha256 = (Get-FileHash $dll -Algorithm SHA256).Hash; writtenUtc = (Get-Item $dll).LastWriteTimeUtc.ToString('o') }
    $receipt.compiledContent = @{}
    foreach ($campaign in @('urman.chapter1', 'urman.fullgame')) {
        $receipt.compiledContent[$campaign] = (Get-FileHash "$Root/game/content/$campaign.compiled.v1.json" -Algorithm SHA256).Hash
    }
    $receipt.frames = @(Get-ChildItem "$output/frames" -Filter '*.png' -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    $receipt.status = 'PASS'
} catch {
    $receipt.status = 'FAIL'
    $receipt.error = $_.Exception.Message
    Write-Host $receipt.error
} finally {
    $engineErrors = @(Get-ChildItem $output -Filter '*.log' | ForEach-Object { Get-Content $_.FullName | Where-Object { $_ -match '(?i)ERROR:|SCRIPT ERROR:|Unhandled exception|\bFAIL\b' } })
    [IO.File]::WriteAllLines("$output/engine-errors.log", [string[]]@($engineErrors), [Text.UTF8Encoding]::new($false))
    $receipt.elapsedSeconds = ((Get-Date).ToUniversalTime() - [datetimeoffset]::Parse($receipt.started).UtcDateTime).TotalSeconds
    if ($receipt.steps.ContainsKey('game')) { $receipt.gameExitCode = $receipt.steps.game.exitCode }
    $receipt.exitCode = if ($receipt.status -eq 'PASS') { 0 } else { 1 }
    $receipt.finished = (Get-Date).ToUniversalTime().ToString('o')
    $receipt | ConvertTo-Json -Depth 5 | Set-Content "$output/receipt.json" -Encoding UTF8
    $env:URMAN_VIEW_CAPTURE = $savedCapture
    $env:URMAN_VIEW_POINTS = $savedPoints
    Pop-Location
    $runLock.Dispose()
}
Write-Host "Windows check: $($receipt.status); receipt: $output/receipt.json"
if ($receipt.status -ne 'PASS') { exit 1 }
