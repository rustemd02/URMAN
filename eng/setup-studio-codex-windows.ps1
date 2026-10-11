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
$native = Get-Command codex.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($native) {
    $nativeSourceExe = $native.Source
} else {
    $npm = Get-Command npm.cmd -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $npm) {
        $winget = Get-Command winget.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $winget) { throw 'winget is required to install Node.js LTS; install Node.js LTS and rerun this script.' }
        & $winget.Source install --exact --id OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -ne 0) { throw "Node.js LTS installation failed (exit $LASTEXITCODE)." }
        Refresh-ProcessPath
        $npm = Get-Command npm.cmd -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if (-not $npm) { throw 'npm.cmd was not found after Node.js setup. Reopen PowerShell and rerun this script.' }

    & $npm.Source install --global '@openai/codex'
    if ($LASTEXITCODE -ne 0) { throw "Codex CLI installation failed (exit $LASTEXITCODE)." }
    $npmRootOutput = & $npm.Source root --global
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate npm global packages.' }
    $npmRoot = ([string]($npmRootOutput | Select-Object -Last 1)).Trim()
    if ([string]::IsNullOrWhiteSpace($npmRoot)) { throw 'npm returned an empty global package path.' }
    $openAiRoot = Join-Path $npmRoot '@openai'
    if (-not (Test-Path -LiteralPath $openAiRoot -PathType Container)) { throw "npm global @openai package directory was not found: $openAiRoot" }
    $nativeFiles = @(Get-ChildItem -LiteralPath $openAiRoot -Filter codex.exe -File -Recurse)
    if ($nativeFiles.Count -ne 1) { throw "Expected one native codex.exe under $openAiRoot, found $($nativeFiles.Count)." }
    $nativeSourceExe = $nativeFiles[0].FullName
}

$localAppData = [Environment]::GetEnvironmentVariable('LOCALAPPDATA', 'User')
if ([string]::IsNullOrWhiteSpace($localAppData)) { $localAppData = $env:LOCALAPPDATA }
if ([string]::IsNullOrWhiteSpace($localAppData)) { throw 'LOCALAPPDATA is unavailable for the current Windows user.' }
$nativeDirectory = Join-Path $localAppData 'Programs\OpenAI\Codex\bin'
$sourceDirectory = [IO.Path]::GetDirectoryName($nativeSourceExe)
$sourceFullPath = [IO.Path]::GetFullPath($sourceDirectory) -replace '[\\/]+$', ''
$targetFullPath = [IO.Path]::GetFullPath($nativeDirectory) -replace '[\\/]+$', ''
if (-not [string]::Equals($sourceFullPath, $targetFullPath, [StringComparison]::OrdinalIgnoreCase)) {
    New-Item -ItemType Directory -Path $nativeDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath $sourceDirectory -Force | Copy-Item -Destination $nativeDirectory -Recurse -Force
}
$nativeExe = Join-Path $nativeDirectory 'codex.exe'
if (-not (Test-Path -LiteralPath $nativeExe -PathType Leaf)) { throw "Native Codex binary was not copied to $nativeExe" }
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
Write-Host 'Sign in with your ChatGPT account in the Codex login prompt.'
& $nativeExe login
if ($LASTEXITCODE -ne 0) { throw "Codex login did not complete (exit $LASTEXITCODE)." }
$previousErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $loginStatus = (& $nativeExe login status 2>&1 | Out-String)
    $loginStatusExitCode = $LASTEXITCODE
} finally { $ErrorActionPreference = $previousErrorActionPreference }
if ($loginStatusExitCode -ne 0) { throw 'Codex login status check failed.' }
if ($loginStatus -notmatch 'Logged in using ChatGPT') { throw 'ChatGPT sign-in was not confirmed. Rerun codex.exe login and choose ChatGPT sign-in.' }
Write-Host "Done: native Codex CLI ($version) is available on PATH and ChatGPT sign-in is confirmed."
Write-Host 'Pending: URMAN Windows station and Blender setup were not checked by this script.'
