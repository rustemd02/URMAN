$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'Run this setup in Windows PowerShell.' }

function Refresh-ProcessPath {
    $entries = @($env:Path -split ';')
    foreach ($scope in @('Machine', 'User')) {
        foreach ($entry in ([Environment]::GetEnvironmentVariable('Path', $scope) -split ';')) {
            if ($entry -and $entries -notcontains $entry) { $entries += $entry }
        }
    }
    $env:Path = $entries -join ';'
}

Refresh-ProcessPath
$localAppData = [Environment]::GetEnvironmentVariable('LOCALAPPDATA', 'User')
if ([string]::IsNullOrWhiteSpace($localAppData)) { $localAppData = $env:LOCALAPPDATA }
if ([string]::IsNullOrWhiteSpace($localAppData)) { throw 'LOCALAPPDATA is unavailable for the current Windows user.' }
$nativeDirectory = Join-Path $localAppData 'Programs\OpenAI\Codex\bin'
$nativeExe = Join-Path $nativeDirectory 'codex.exe'
$installerPath = Join-Path ([IO.Path]::GetTempPath()) "urman-codex-install-$([guid]::NewGuid().ToString('N')).ps1"

function Get-CodexLoginStatus {
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = (& $nativeExe login status 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousErrorActionPreference }
    [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}

function Test-ChatGptLoginConfirmed([string] $Output) {
    $positiveStatus = $Output -match '(?im)^[ \t]*Logged in using ChatGPT(?:\s|$)'
    $negativeStatus = $Output -match '(?im)^[ \t]*Not logged in(?:\s|$)'
    return $positiveStatus -and -not $negativeStatus
}

$backupDirectory = $null
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri 'https://chatgpt.com/codex/install.ps1' -OutFile $installerPath

    # Preserve the previous copied-binary layout so the official installer
    # can create its full package junction without discarding files.
    $existingBin = Get-Item -LiteralPath $nativeDirectory -Force -ErrorAction SilentlyContinue
    if ($existingBin -and $existingBin.PSIsContainer -and
        -not ($existingBin.Attributes -band [IO.FileAttributes]::ReparsePoint) -and
        (Test-Path -LiteralPath (Join-Path $nativeDirectory 'codex.exe') -PathType Leaf)) {
        $backupCandidate = "$nativeDirectory.urman-backup-$([guid]::NewGuid().ToString('N'))"
        Move-Item -LiteralPath $nativeDirectory -Destination $backupCandidate -ErrorAction Stop
        $backupDirectory = $backupCandidate
        Write-Host "Preserved the previous managed Codex files at: $backupDirectory"
    }

    $hadInstallDir = Test-Path Env:CODEX_INSTALL_DIR
    $savedInstallDir = $env:CODEX_INSTALL_DIR
    try {
        $env:CODEX_INSTALL_DIR = $nativeDirectory
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installerPath
        $installerExitCode = $LASTEXITCODE
    } finally {
        if ($hadInstallDir) { $env:CODEX_INSTALL_DIR = $savedInstallDir }
        else { Remove-Item Env:CODEX_INSTALL_DIR -ErrorAction SilentlyContinue }
    }
    if ($installerExitCode -ne 0) { throw "Official Codex installer failed (exit $installerExitCode)." }
    if (-not (Test-Path -LiteralPath $nativeExe -PathType Leaf)) { throw "Official Codex installer did not create $nativeExe" }
} catch {
    $installerError = $_.Exception.Message
    if ($backupDirectory) {
        if (-not (Test-Path -LiteralPath $nativeDirectory)) {
            try {
                Move-Item -LiteralPath $backupDirectory -Destination $nativeDirectory -ErrorAction Stop
            } catch {
                throw "Official Codex setup failed ($installerError); the previous managed files remain at $backupDirectory because restore failed: $($_.Exception.Message)"
            }
            throw "Official Codex setup failed ($installerError); the previous managed Codex files were restored."
        }
        throw "Official Codex setup failed ($installerError); the previous managed Codex files remain at $backupDirectory."
    }
    throw
} finally {
    Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
}
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$normalizedNative = $nativeDirectory -replace '[\\/]+$', ''
$alreadyPresent = @($userPath -split ';' | Where-Object {
    $candidate = ($_.Trim().Trim('"') -replace '[\\/]+$', '')
    [string]::Equals($candidate, $normalizedNative, [StringComparison]::OrdinalIgnoreCase)
}).Count -gt 0
if (-not $alreadyPresent) {
    if ([string]::IsNullOrEmpty($userPath)) { $userPath = $nativeDirectory }
    elseif ($userPath.EndsWith(';')) { $userPath += $nativeDirectory }
    else { $userPath += ";$nativeDirectory" }
    [Environment]::SetEnvironmentVariable('Path', $userPath, 'User')
}
Refresh-ProcessPath

$version = (& $nativeExe --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw "Native Codex version check failed (exit $LASTEXITCODE)." }
Write-Host "Native Codex CLI: $version"
$loginStatus = Get-CodexLoginStatus
if ($loginStatus.ExitCode -ne 0 -or -not (Test-ChatGptLoginConfirmed $loginStatus.Output)) {
    Write-Host 'Sign in with your ChatGPT account in the Codex login prompt.'
    & $nativeExe login
    if ($LASTEXITCODE -ne 0) { throw "Codex login did not complete (exit $LASTEXITCODE)." }
    $loginStatus = Get-CodexLoginStatus
}
if ($loginStatus.ExitCode -ne 0) { throw 'Codex login status check failed.' }
if (-not (Test-ChatGptLoginConfirmed $loginStatus.Output)) { throw 'ChatGPT sign-in was not confirmed. Rerun codex.exe login and choose ChatGPT sign-in.' }
Write-Host "Done: native Codex CLI ($version) is available on PATH and ChatGPT sign-in is confirmed."
Write-Host 'Pending: URMAN Windows station and Blender setup were not checked by this script.'
