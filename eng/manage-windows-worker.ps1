param(
    [ValidateSet('install','start','stop','disable')][string]$Action = 'start',
    [string]$Config = "$env:LOCALAPPDATA\URMAN-STATION\station-config.json",
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)
$ErrorActionPreference = 'Stop'
$name = 'URMAN Windows Test Worker'
$settings = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
if ($Action -in @('stop','disable')) {
    $active = Get-ChildItem "$($settings.data)\runs\*\status.json" -ErrorAction SilentlyContinue | Where-Object {
        (Get-Content $_.FullName -Raw | ConvertFrom-Json).status -eq 'RUNNING'
    }
    if ($active) { throw 'Wait for the active job/guard to finish before stopping the worker.' }
    Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
    # Task Scheduler returns before the old process releases its instance lock.
    # Do not let an immediate start race with that still-stopping worker.
    $stopDeadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $stillRunning = (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue).State -eq 'Running'
        $lockReleased = $false
        $probe = $null
        try {
            $probe = [IO.File]::Open((Join-Path $settings.data 'worker.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::ReadWrite)
            $probe.Lock(0, 1)
            $probe.Unlock(0, 1)
            $lockReleased = $true
        } catch [IO.IOException] {
            # The terminating worker can retain the lock after task state changes.
        } finally {
            if ($null -ne $probe) { $probe.Dispose() }
        }
        if (-not $stillRunning -and $lockReleased) { break }
        if ([DateTime]::UtcNow -ge $stopDeadline) { throw 'Worker did not release its instance lock within 10 seconds; inspect the task before restarting.' }
        Start-Sleep -Milliseconds 100
    } while ($true)
    if ($Action -eq 'disable') { Disable-ScheduledTask -TaskName $name | Out-Null }
    return
}
if ($Action -eq 'install') {
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $backgroundPython = $settings.python -replace 'python.exe$', 'pythonw.exe'
    $taskAction = New-ScheduledTaskAction -Execute $backgroundPython -Argument "`"$Root\eng\windows_station_worker.py`" --config `"$Config`"" -WorkingDirectory $Root
    $principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
    $taskSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([timespan]::Zero) -MultipleInstances IgnoreNew -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $name -Action $taskAction -Principal $principal -Trigger $trigger -Settings $taskSettings -Description 'Private URMAN checks in the logged-in user desktop session; no saved Windows password.' -Force | Out-Null
}
Enable-ScheduledTask -TaskName $name | Out-Null
Start-ScheduledTask -TaskName $name
