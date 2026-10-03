param(
    [ValidateSet('build', 'prepare', 'play', 'smoke', 'capture')][string]$Mode = 'smoke',
    [string]$Scene,
    [ValidateRange(1, 86400)][int]$TimeoutSeconds = 300,
    [switch]$Headless,
    [string]$ViewPoints,
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Game checks belong on the Windows station. Local fallback is disabled.' }
$Root = (Resolve-Path $Root).Path
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
$godot = "$Root/.tools/godot/windows/Godot_v$version-stable_mono_win64/Godot_v$version-stable_mono_win64_console.exe"
$dotnet = "$Root/.tools/dotnet/dotnet.exe"
$guard = "$Root/eng/protected_run.py"
foreach ($path in @($godot, $dotnet, $guard)) {
    if (-not (Test-Path $path)) { throw "Missing $path; run eng/setup-windows-station.ps1 first." }
}
$env:DOTNET_ROOT = "$Root/.tools/dotnet"
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_HOME = "$Root/.tools/dotnet-home"
$env:NUGET_PACKAGES = "$Root/.tools/nuget"
$env:MSBUILDUSESERVER = '0'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
# ponytail: one station run at a time; separate workers only if another PC is added.
$runLock = [IO.File]::Open("$Root/.tools/windows-station.lock", 'OpenOrCreate', 'ReadWrite', 'None')
$output = "$Root/.codex-captures/windows/$((Get-Date).ToString('yyyyMMdd-HHmmss'))-$([guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Force $output | Out-Null
$receipt = [ordered]@{ host = $env:COMPUTERNAME; mode = $Mode; scene = $Scene; headless = [bool]$Headless;
    started = (Get-Date).ToUniversalTime().ToString('o'); status = 'RUNNING'; logs = $output; humanPlaytest = 'not-run' }
function Invoke-Logged([string]$Name, [string]$Exe, [string[]]$Arguments) {
    # PS5 treats native stderr as ErrorRecord. Preserve it and decide from exit + log.
    $savedPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Exe @Arguments 2>&1 | ForEach-Object { $_.ToString() } | Tee-Object -FilePath "$output/$Name.log"; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $savedPreference }
    if ($code -ne 0) { throw "$Name exited $code; see $output/$Name.log" }
    $log = Get-Content "$output/$Name.log" -Raw
    if ($log -match '(?m)^(ERROR:|SCRIPT ERROR:)') { throw "$Name reported engine errors; inspect the log before accepting it." }
    if ($Exe -eq 'python' -and $log -notmatch 'userdata guard: original files restored by rename and verified') {
        throw "$Name did not confirm userdata restoration."
    }
}
Push-Location $Root
$savedCapture = $env:URMAN_VIEW_CAPTURE
$savedPoints = $env:URMAN_VIEW_POINTS
try {
    Remove-Item Env:URMAN_VIEW_CAPTURE, Env:URMAN_VIEW_POINTS -ErrorAction SilentlyContinue
    $receipt.commit = (& git rev-parse HEAD)
    $receipt.dirtyPaths = @(& git status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record Git source identity.' }
    $receipt.sdk = (& $dotnet --version)
    if ($LASTEXITCODE -ne 0 -or $receipt.sdk -ne (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version) { throw 'Pinned SDK is missing.' }
    $receipt.godot = (& $godot --headless --version)
    if ($LASTEXITCODE -ne 0 -or $receipt.godot -ne $manifest.godot.version) { throw 'Pinned Godot .NET is missing.' }
    if (Test-Path 'game/override.cfg') { $receipt.stationOverrideSha256 = (Get-FileHash 'game/override.cfg' -Algorithm SHA256).Hash }
    Invoke-Logged 'build' $dotnet @('build', 'game/Urman.Game.csproj', '-v', 'q', '--nologo', '--disable-build-servers')
    if ($Mode -ne 'build') {
        Invoke-Logged 'import' 'python' @($guard, '--clean', '--timeout', "$TimeoutSeconds", $godot, '--headless', '--editor', '--path', "$Root/game", '--import')
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
        Invoke-Logged 'game' 'python' $arguments
        if ($Mode -eq 'capture' -and -not (Test-Path "$output/frames/*.png")) { throw 'No capture frames were produced.' }
    }
    $receipt.status = 'PASS'
} catch {
    $receipt.status = 'FAIL'
    $receipt.error = $_.Exception.Message
    Write-Host $receipt.error
} finally {
    $receipt.finished = (Get-Date).ToUniversalTime().ToString('o')
    $receipt | ConvertTo-Json -Depth 5 | Set-Content "$output/receipt.json" -Encoding UTF8
    $env:URMAN_VIEW_CAPTURE = $savedCapture
    $env:URMAN_VIEW_POINTS = $savedPoints
    Pop-Location
    $runLock.Dispose()
}
Write-Host "Windows check: $($receipt.status); receipt: $output/receipt.json"
if ($receipt.status -ne 'PASS') { exit 1 }
