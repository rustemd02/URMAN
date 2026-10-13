param(
    [ValidateSet('build', 'prepare', 'play', 'smoke', 'capture')][string]$Mode = 'smoke',
    [string]$Scene,
    [ValidateRange(1, 86400)][int]$TimeoutSeconds = 300,
    [ValidateRange(0, 2)][int]$StudioRestarts = 0,
    [switch]$Headless,
    [string]$ViewPoints,
    [ValidatePattern('^$|^[a-z0-9-]{1,48}$')][string]$AtmospherePhase,
    [ValidatePattern('^$|^player$|^\d{2,3}(\.\d+)?$')][string]$ViewFov,
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
$studioPlay = $Mode -eq 'play' -and $Scene -eq 'res://studio/studio_main.tscn'
if ($PSBoundParameters.ContainsKey('StudioRestarts') -and -not $studioPlay) {
    throw 'StudioRestarts is only valid for a native Studio play run.'
}
if ($Mode -eq 'capture' -and (-not $ViewPoints -or $Headless)) { throw 'Capture requires -ViewPoints and a native window.' }
if ($AtmospherePhase -and $Mode -ne 'capture') { throw 'An atmosphere phase only applies to a capture.' }
if ($ViewFov -and $Mode -ne 'capture') { throw 'A view FOV only applies to a capture.' }
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
if ($studioPlay) { $receipt.studioRuns = @() }
function Invoke-Logged([string]$Name, [string]$Exe, [string[]]$Arguments, [int]$Deadline = 180, [switch]$DeferEngineErrors) {
    # PS5 treats native stderr as ErrorRecord. Preserve it and decide from exit + log.
    $savedPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $stepLines = @()
    try { & $Python $bounded "$Deadline" $Exe @Arguments 2>&1 | ForEach-Object { $_.ToString() } | Tee-Object -Variable stepLines; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $savedPreference }
    [IO.File]::WriteAllLines("$output/$Name.log", [string[]]@($stepLines), [Text.UTF8Encoding]::new($false))
    $receipt.steps[$Name] = @{ exitCode = $code }
    $log = Get-Content "$output/$Name.log" -Raw
    if ($Arguments -contains $guard) {
        $receipt.userdataRestored = [bool]($log -match 'userdata guard: original files.*restored.*verified')
    }
    if ($code -ne 0) { throw "$Name exited $code; see $output/$Name.log" }
    $engineErrors = @($log -split '\r?\n' | Where-Object { $_ -match '(?im)(^|\s)(ERROR:|SCRIPT ERROR:)|\bFAIL\b|Unhandled exception' })
    if ($engineErrors.Count -gt 0) {
        if (-not $DeferEngineErrors) { throw "$Name reported engine errors; inspect the log before accepting it." }
        $receipt.steps[$Name].engineErrors = $engineErrors
    }
    if ($Arguments -contains $guard -and $log -notmatch 'userdata guard: original files.*restored.*verified') {
        throw "$Name did not confirm userdata restoration."
    }
}
Push-Location $Root
$savedCapture = $env:URMAN_VIEW_CAPTURE
$savedPoints = $env:URMAN_VIEW_POINTS
$savedPhase = $env:URMAN_ATMOSPHERE_PHASE
$savedFov = $env:URMAN_VIEW_FOV
$vehicleSmoke = $Mode -eq 'smoke' -and $Scene -eq 'res://tests/vehicle_smoke_test.tscn'
$vehicleEnvironment = @{}
foreach ($name in @('URMAN_VEHICLE_OUTPUT', 'URMAN_VEHICLE_CAPTURE', 'URMAN_VEHICLE_WHEELS_ONLY', 'URMAN_VEHICLE_MOUSE_ONLY', 'URMAN_VEHICLE_COMPOUND_ONLY', 'URMAN_VEHICLE_CONTACT_ONLY', 'URMAN_VEHICLE_ADDRESS_ONLY', 'URMAN_VEHICLE_CART_ONLY')) {
    $vehicleEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$studioSnapshotRevision = $null
try {
    Remove-Item Env:URMAN_VIEW_CAPTURE, Env:URMAN_VIEW_POINTS, Env:URMAN_ATMOSPHERE_PHASE, Env:URMAN_VIEW_FOV -ErrorAction SilentlyContinue
    if ($Provenance) {
        & $Python -c "import sys,json; from pathlib import Path; sys.path.insert(0,sys.argv[1]); from remote_common import verify_source; verify_source(Path(sys.argv[2]),json.loads(Path(sys.argv[3]).read_text(encoding='utf-8-sig')))" "$ToolsRoot/eng" $Root $Provenance
        if ($LASTEXITCODE -ne 0) { throw 'Snapshot provenance/hash verification failed.' }
        $receipt.source = Get-Content $Provenance -Raw | ConvertFrom-Json
        $receipt.commit = $receipt.source.base_commit
        $receipt.dirtyPaths = $receipt.source.dirty_paths
        $receipt.snapshotId = $receipt.source.snapshot_id
        if ($studioPlay) {
            $studioSnapshotRevision = [string]$receipt.source.base_commit
            if ($studioSnapshotRevision -cnotmatch '\A(?:[0-9a-f]{40}|[0-9a-f]{64})\z') {
                throw 'Verified Studio snapshot has an invalid base revision.'
            }
        }
    } else {
        $receipt.commit = (& git rev-parse HEAD)
        $receipt.dirtyPaths = @(& git status --porcelain)
        if ($LASTEXITCODE -ne 0) { throw 'Cannot record Git source identity.' }
    }
    $receipt.sdk = (& $dotnet --version)
    if ($LASTEXITCODE -ne 0 -or $receipt.sdk -ne (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version) { throw 'Pinned SDK is missing.' }
    $receipt.godot = (& $godot --headless --version)
    if ($LASTEXITCODE -ne 0 -or $receipt.godot -ne $manifest.godot.version) { throw 'Pinned Godot .NET is missing.' }
    if ($studioPlay) {
        $blenderPin = $manifest.blender.windows
        $blender = Join-Path $ToolsRoot ('.tools/blender/windows/' + $blenderPin.folder + '/blender.exe')
        if (-not (Test-Path -LiteralPath $blender -PathType Leaf)) { throw "Pinned Blender for Studio is missing: $blender" }
        $blenderOutput = @(& $blender --version)
        $blenderExitCode = $LASTEXITCODE
        $actualBlender = if ($blenderOutput.Count -gt 0) { [string]$blenderOutput[0] } else { '' }
        if ($blenderExitCode -ne 0 -or $actualBlender -notmatch ('^Blender\s+' + [regex]::Escape([string]$blenderPin.version) + '(\s|$)')) {
            throw "Unexpected pinned Blender version: $actualBlender"
        }
        $receipt.blender = $actualBlender.Trim()
    }
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
        $baseArguments = @($guard, '--clean', '--timeout', "$TimeoutSeconds", $godot, '--path', "$Root/game", '--resolution', '1920x1080')
        if ($Headless) { $baseArguments += '--headless' } else { $baseArguments += '--always-on-top' }
        if ($studioPlay) { $baseArguments += '--verbose' }
        if ($Mode -eq 'capture') {
            $env:URMAN_VIEW_CAPTURE = "$output/frames"
            $env:URMAN_VIEW_POINTS = $ViewPoints
            # VIS-067 colour states no zone owns by default; the game refuses an unknown id.
            if ($AtmospherePhase) { $env:URMAN_ATMOSPHERE_PHASE = $AtmospherePhase; $receipt.atmospherePhase = $AtmospherePhase }
            # VIS-111: 'player' = the player's camera FOV; default control views stay at 70.
            if ($ViewFov) { $env:URMAN_VIEW_FOV = $ViewFov; $receipt.viewFov = $ViewFov }
        }
        $baseArguments += $Scene
        if ($studioPlay) {
            $baseArguments += '--'
            $baseArguments += "--urman-studio-tools-root=$ToolsRoot"
            if ($Provenance) { $baseArguments += "--urman-studio-snapshot-revision=$studioSnapshotRevision" }
        }
        if ($vehicleSmoke) {
            foreach ($name in $vehicleEnvironment.Keys) {
                [Environment]::SetEnvironmentVariable($name, $null, 'Process')
            }
            # The existing test writes its receipt outside guarded player data.
            $env:URMAN_VEHICLE_OUTPUT = Join-Path ([IO.Path]::GetFullPath($output)) 'vehicle'
            if (-not $Headless) { $env:URMAN_VEHICLE_CAPTURE = '1' }
            $receipt.vehicleEvidence = 'vehicle/receipt.json'
        }
        if ($Mode -eq 'smoke') { $baseArguments += '--urman-smoke-background-input' }
        $runCount = if ($studioPlay) { 1 + $StudioRestarts } else { 1 }
        $gamePhaseClock = [Diagnostics.Stopwatch]::StartNew()
        for ($runIndex = 0; $runIndex -lt $runCount; $runIndex++) {
            $runTimeout = $TimeoutSeconds
            if ($studioPlay -and $runIndex -gt 0) {
                $runTimeout = $TimeoutSeconds - [int][Math]::Ceiling($gamePhaseClock.Elapsed.TotalSeconds)
                if ($runTimeout -le 0) { throw 'Studio restart skipped because the shared game timeout is exhausted.' }
            }
            $stepName = if ($runIndex -eq 0) { 'game' } else { "game-restart-$runIndex" }
            $runStarted = (Get-Date).ToUniversalTime().ToString('o')
            $runRecord = [ordered]@{ run = $runIndex + 1; step = $stepName; log = "$stepName.log";
                startedUtc = $runStarted; finishedUtc = $null; exitCode = $null; status = 'RUNNING'; engineErrors = @() }
            $runArguments = @($baseArguments)
            $runArguments[3] = "$runTimeout"
            $receipt.userdataRestored = $false
            try {
                Invoke-Logged $stepName $Python $runArguments ($runTimeout + 30) -DeferEngineErrors:($studioPlay -and $StudioRestarts -gt 0)
                $runRecord.exitCode = $receipt.steps[$stepName].exitCode
                if ($receipt.steps[$stepName].ContainsKey('engineErrors')) {
                    $runRecord.engineErrors = @($receipt.steps[$stepName].engineErrors)
                }
                if (-not $Headless) {
                    $rendererLines = @([IO.File]::ReadAllLines("$output/$stepName.log") | Where-Object { $_ -match 'Vulkan|Direct3D|D3D12|OpenGL API' })
                    if (-not $rendererLines -or ($rendererLines -join ' ') -match '(?i)llvmpipe|SwiftShader|software rasterizer|Microsoft Basic Render') { throw 'Native hardware renderer was not confirmed by the engine log.' }
                    $runRecord.renderer = $rendererLines
                    if ($runIndex -eq 0) { $receipt.renderer = $rendererLines }
                }
                if ($Mode -eq 'smoke' -and $Scene -eq 'res://tests/act1_main_menu_smoke_test.tscn' -and (Get-Content "$output/$stepName.log" -Raw) -notmatch '\bPASS\b') { throw 'Main menu smoke did not emit its expected PASS marker.' }
                if ($Mode -eq 'smoke' -and $Scene -eq 'res://tests/player_settings_smoke_test.tscn' -and (Get-Content "$output/$stepName.log" -Raw) -notmatch 'player-settings-smoke: SaveGameV3 settings') { throw 'Player settings smoke did not emit its expected completion marker.' }
                $receipt.gameExitCode = $runRecord.exitCode
                if ($Mode -eq 'capture' -and -not (Test-Path "$output/frames/*.png")) { throw 'No capture frames were produced.' }
                $runRecord.status = if ($runRecord.engineErrors.Count -gt 0) { 'FAIL' } else { 'PASS' }
            } catch {
                if ($receipt.steps.ContainsKey($stepName)) { $runRecord.exitCode = $receipt.steps[$stepName].exitCode }
                $runRecord.status = 'FAIL'
                throw
            } finally {
                $runRecord.finishedUtc = (Get-Date).ToUniversalTime().ToString('o')
                if ($studioPlay) { $receipt.studioRuns += $runRecord }
            }
        }
        if ($studioPlay) {
            $studioLogErrors = @($receipt.steps.GetEnumerator() |
                Where-Object { $_.Key -eq 'game' -or $_.Key -like 'game-restart-*' } |
                Where-Object { $_.Value.ContainsKey('engineErrors') })
            if ($studioLogErrors.Count -gt 0) {
                $details = @($studioLogErrors | ForEach-Object {
                    $lines = $_.Value.engineErrors -join [Environment]::NewLine
                    "$($_.Key): $lines"
                })
                throw "All requested Studio launches completed, but game logs reported engine errors:`n$($details -join [Environment]::NewLine)"
            }
        }
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
    if ($studioPlay -and $receipt.studioRuns.Count -gt 0) { $receipt.gameExitCode = $receipt.studioRuns[-1].exitCode }
    elseif ($receipt.steps.ContainsKey('game')) { $receipt.gameExitCode = $receipt.steps.game.exitCode }
    $receipt.exitCode = if ($receipt.status -eq 'PASS') { 0 } else { 1 }
    $receipt.finished = (Get-Date).ToUniversalTime().ToString('o')
    $receipt | ConvertTo-Json -Depth 5 | Set-Content "$output/receipt.json" -Encoding UTF8
    if ($vehicleSmoke) {
        foreach ($name in $vehicleEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $vehicleEnvironment[$name], 'Process')
        }
    }
    $env:URMAN_VIEW_CAPTURE = $savedCapture
    $env:URMAN_VIEW_POINTS = $savedPoints
    $env:URMAN_ATMOSPHERE_PHASE = $savedPhase
    $env:URMAN_VIEW_FOV = $savedFov
    Pop-Location
    $runLock.Dispose()
}
Write-Host "Windows check: $($receipt.status); receipt: $output/receipt.json"
if ($receipt.status -ne 'PASS') { exit 1 }
