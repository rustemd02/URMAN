param([Parameter(Mandatory=$true)][string]$RequestPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if ($env:OS -ne 'Windows_NT') { throw 'This helper requires Windows.' }
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UrmanGameUiNative {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 public delegate bool EnumProc(IntPtr h,IntPtr extra);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback,IntPtr extra);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,System.Text.StringBuilder text,int length);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint processId);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code,uint mapType);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
}
'@
[void][UrmanGameUiNative]::SetProcessDPIAware()

$script:AllowedTitles = @('URMAN','URMAN(DEBUG)','URMAN (DEBUG)','URMAN Studio','URMAN Studio(DEBUG)','URMAN Studio (DEBUG)')
$script:TargetHandle = [IntPtr]::Zero
$script:TargetPid = 0
$script:TargetVerified = $false
$script:TargetTitle = $null
$script:InitialRect = $null
$script:Request = $null
$script:ResultPath = $null
$script:ImagePath = $null
$script:ImageCaptured = $false

function Test-OwnedForeground {
    $foreground = [UrmanGameUiNative]::GetForegroundWindow()
    if ($foreground -eq $script:TargetHandle) { return $true }
    [uint32]$owner = 0
    [void][UrmanGameUiNative]::GetWindowThreadProcessId($foreground,[ref]$owner)
    return [int]$owner -eq $script:TargetPid -and
        [UrmanGameUiNative]::GetAncestor($foreground,3) -eq $script:TargetHandle
}

function Assert-TargetWindow([switch]$RequireForeground) {
    if ($script:TargetHandle -eq [IntPtr]::Zero -or -not $script:TargetVerified) {
        throw 'The requested game window is not verified.'
    }

    [uint32]$owner = 0
    [void][UrmanGameUiNative]::GetWindowThreadProcessId($script:TargetHandle,[ref]$owner)
    if ([int]$owner -ne $script:TargetPid) { throw 'The owned game window changed process.' }

    $titleBuffer = New-Object System.Text.StringBuilder(512)
    [void][UrmanGameUiNative]::GetWindowText($script:TargetHandle,$titleBuffer,512)
    if ($titleBuffer.ToString() -cne $script:TargetTitle -or $script:AllowedTitles -cnotcontains $titleBuffer.ToString()) {
        throw 'The owned game window title changed.'
    }

    if ($RequireForeground -and -not (Test-OwnedForeground)) {
        throw 'The owned game window lost foreground focus.'
    }

    $current = New-Object UrmanGameUiNative+Rect
    if (-not [UrmanGameUiNative]::GetWindowRect($script:TargetHandle,[ref]$current)) {
        throw 'The owned game window bounds are unavailable.'
    }
    if ($current.Left -ne $script:InitialRect.Left -or $current.Top -ne $script:InitialRect.Top -or
        $current.Right -ne $script:InitialRect.Right -or $current.Bottom -ne $script:InitialRect.Bottom) {
        throw 'The owned game window bounds changed during the request.'
    }
}

function Assert-InputTarget() { Assert-TargetWindow -RequireForeground }

function Release-KeySafely([byte]$Key) {
    [UrmanGameUiNative]::keybd_event($Key,0,2,[UIntPtr]::Zero)
}

function Release-PhysicalKeySafely([byte]$Key,[byte]$ScanCode) {
    # Cleanup must release a key this helper pressed even if focus or bounds changed mid-hold.
    [UrmanGameUiNative]::keybd_event($Key,$ScanCode,2,[UIntPtr]::Zero)
}

function Release-MouseSafely([uint32]$Flags) {
    [UrmanGameUiNative]::mouse_event($Flags,0,0,0,[UIntPtr]::Zero)
}

function Send-StudioChord([string]$Keys) {
    $control = $Keys.Contains('^')
    $shift = $Keys.Contains('+')
    $plain = $Keys.Replace('^','').Replace('+','')
    $vk = switch ($plain) {
        '{ENTER}' { 13 }
        '{ESC}' { 27 }
        '{DOWN}' { 40 }
        '{UP}' { 38 }
        '{SPACE}' { 32 }
        '{TAB}' { 9 }
        '{LEFT}' { 37 }
        '{RIGHT}' { 39 }
        'a' { 65 }
        'f' { 70 }
        'j' { 74 }
        'v' { 86 }
        'z' { 90 }
        default { throw 'Unsupported game UI key.' }
    }

    $controlDown = $false
    $shiftDown = $false
    $keyDown = $false
    try {
        if ($control) {
            Assert-InputTarget
            [UrmanGameUiNative]::keybd_event(17,0,0,[UIntPtr]::Zero)
            $controlDown = $true
        }
        if ($shift) {
            Assert-InputTarget
            [UrmanGameUiNative]::keybd_event(16,0,0,[UIntPtr]::Zero)
            $shiftDown = $true
        }
        Start-Sleep -Milliseconds 200
        Assert-InputTarget
        [UrmanGameUiNative]::keybd_event([byte]$vk,0,0,[UIntPtr]::Zero)
        $keyDown = $true
        Start-Sleep -Milliseconds 200
        Release-KeySafely ([byte]$vk)
        $keyDown = $false
        Start-Sleep -Milliseconds 200
    } finally {
        if ($keyDown) { Release-KeySafely ([byte]$vk) }
        if ($shiftDown) { Release-KeySafely 16 }
        if ($controlDown) { Release-KeySafely 17 }
    }
}

function Get-CheckedPoint([object]$X,[object]$Y) {
    $xValue = 0
    $yValue = 0
    if (-not [int]::TryParse([string]$X,[ref]$xValue) -or -not [int]::TryParse([string]$Y,[ref]$yValue)) {
        throw 'UI coordinates must be integers.'
    }
    $width = $script:InitialRect.Right - $script:InitialRect.Left
    $height = $script:InitialRect.Bottom - $script:InitialRect.Top
    if ($xValue -lt 0 -or $yValue -lt 0 -or $xValue -ge $width -or $yValue -ge $height) {
        throw 'UI input falls outside the owned game window.'
    }
    return @{ X = $xValue; Y = $yValue }
}

function Move-Game([object]$Action) {
    $key = ([string]$Action.key).ToLowerInvariant()
    $vk = switch ($key) {
        'w' { 87 }
        'a' { 65 }
        's' { 83 }
        'd' { 68 }
        'e' { 69 }
        default { throw 'Movement key must be w, a, s, d, or e.' }
    }
    $scanCode = [UrmanGameUiNative]::MapVirtualKey([uint32]$vk,0)
    if ($scanCode -eq 0) { throw 'The movement key has no platform scan code.' }
    $duration = 0
    if (-not [int]::TryParse([string]$Action.durationMs,[ref]$duration) -or $duration -lt 1 -or $duration -gt 2000) {
        throw 'Movement duration must be between 1 and 2000 milliseconds.'
    }

    $keyDown = $false
    try {
        Assert-InputTarget
        [UrmanGameUiNative]::keybd_event([byte]$vk,[byte]$scanCode,0,[UIntPtr]::Zero)
        $keyDown = $true
        $holdTimer = [Diagnostics.Stopwatch]::StartNew()
        while ($holdTimer.ElapsedMilliseconds -lt $duration) {
            Assert-InputTarget
            $remaining = $duration - [int]$holdTimer.ElapsedMilliseconds
            if ($remaining -gt 0) { Start-Sleep -Milliseconds ([Math]::Min(50,$remaining)) }
        }
    } finally {
        if ($keyDown) { Release-PhysicalKeySafely ([byte]$vk) ([byte]$scanCode) }
    }
}

function Invoke-GameAction([object]$Action) {
    switch ([string]$Action.action) {
        'click' {
            $point = Get-CheckedPoint $Action.x $Action.y
            Assert-InputTarget
            if (-not [UrmanGameUiNative]::SetCursorPos($script:InitialRect.Left + $point.X,$script:InitialRect.Top + $point.Y)) {
                throw 'Cannot position the pointer in the owned game window.'
            }
            Assert-InputTarget
            [UrmanGameUiNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
            $buttonDown = $true
            try { Start-Sleep -Milliseconds 100 }
            finally { if ($buttonDown) { Release-MouseSafely 4 } }
        }
        'wheel' {
            $point = Get-CheckedPoint $Action.x $Action.y
            $count = 0
            if (-not [int]::TryParse([string]$Action.count,[ref]$count) -or $count -lt 1 -or $count -gt 20) {
                throw 'Wheel count must be between 1 and 20.'
            }
            for ($i = 0; $i -lt $count; $i++) {
                Assert-InputTarget
                if (-not [UrmanGameUiNative]::SetCursorPos($script:InitialRect.Left + $point.X,$script:InitialRect.Top + $point.Y)) {
                    throw 'Cannot position the pointer in the owned game window.'
                }
                Assert-InputTarget
                [UrmanGameUiNative]::mouse_event(2048,0,0,[uint32]4294967176,[UIntPtr]::Zero)
                Start-Sleep -Milliseconds 150
            }
        }
        'drag' {
            $from = Get-CheckedPoint $Action.x $Action.y
            $to = Get-CheckedPoint $Action.toX $Action.toY
            Assert-InputTarget
            if (-not [UrmanGameUiNative]::SetCursorPos($script:InitialRect.Left + $from.X,$script:InitialRect.Top + $from.Y)) {
                throw 'Cannot position the pointer in the owned game window.'
            }
            Assert-InputTarget
            [UrmanGameUiNative]::mouse_event(8,0,0,0,[UIntPtr]::Zero)
            $buttonDown = $true
            try {
                for ($i = 1; $i -le 10; $i++) {
                    Assert-InputTarget
                    $x = [int]($from.X + ($to.X - $from.X) * $i / 10)
                    $y = [int]($from.Y + ($to.Y - $from.Y) * $i / 10)
                    if (-not [UrmanGameUiNative]::SetCursorPos($script:InitialRect.Left + $x,$script:InitialRect.Top + $y)) {
                        throw 'Cannot drag within the owned game window.'
                    }
                    Start-Sleep -Milliseconds 200
                }
            } finally { if ($buttonDown) { Release-MouseSafely 16 } }
        }
        'keys' { Send-StudioChord ([string]$Action.keys) }
        'text' {
            Assert-InputTarget
            [System.Windows.Forms.Clipboard]::SetText([string]$Action.text)
            Send-StudioChord '^v'
        }
        'move' { Move-Game $Action }
        default { throw 'Unsupported game UI action.' }
    }
}

function Write-Result([bool]$Ok,[string]$ErrorText) {
    if ([string]::IsNullOrWhiteSpace($script:ResultPath)) { return }
    $rectValue = $null
    if ($script:InitialRect -ne $null) {
        $rectValue = [ordered]@{
            left = $script:InitialRect.Left
            top = $script:InitialRect.Top
            width = $script:InitialRect.Right - $script:InitialRect.Left
            height = $script:InitialRect.Bottom - $script:InitialRect.Top
        }
    }
    $report = [ordered]@{
        ok = $Ok
        pid = $(if ($script:TargetVerified) { $script:TargetPid } else { $null })
        title = $script:TargetTitle
        rect = $rectValue
        image = $(if ($Ok -and $script:ImageCaptured) { $script:ImagePath } else { $null })
        error = $ErrorText
    }
    $json = ConvertTo-Json -InputObject $report -Compress -Depth 4
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $stream = [IO.File]::Open($script:ResultPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
}

try {
    $requestFullPath = (Resolve-Path -LiteralPath $RequestPath).Path
    $script:Request = Get-Content -LiteralPath $requestFullPath -Raw | ConvertFrom-Json
    if ([string]$script:Request.jobId -cnotmatch '\A[a-f0-9]{32}\z') { throw 'Request jobId must be 32 lowercase hexadecimal characters.' }
    $targetPid = 0
    if (-not [int]::TryParse([string]$script:Request.pid,[ref]$targetPid) -or $targetPid -le 0) {
        throw 'Request pid must be a positive process ID.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$script:Request.sourceRoot) -or
        -not [IO.Path]::IsPathRooted([string]$script:Request.sourceRoot)) {
        throw 'Request sourceRoot must be an absolute runs/<jobId>/source path.'
    }
    $sourceRoot = [IO.Path]::GetFullPath([string]$script:Request.sourceRoot).TrimEnd([char[]]@('\','/'))
    $expectedSuffix = '\runs\' + [string]$script:Request.jobId + '\source'
    if (-not $sourceRoot.EndsWith($expectedSuffix,[StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.Directory]::Exists($sourceRoot) -or
        -not [IO.File]::Exists((Join-Path $sourceRoot 'game/project.godot'))) {
        throw 'Request sourceRoot is not the existing source directory for this job.'
    }

    $resultPath = [IO.Path]::GetFullPath([string]$script:Request.result)
    $imagePath = [IO.Path]::GetFullPath([string]$script:Request.image)
    $outputDirectory = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'urman-codex-studio-ui'))
    if ([string]::IsNullOrWhiteSpace([string]$script:Request.result) -or
        [string]::IsNullOrWhiteSpace([string]$script:Request.image) -or
        $resultPath -eq $requestFullPath -or
        [IO.Path]::GetDirectoryName($resultPath) -ine $outputDirectory -or
        [IO.Path]::GetDirectoryName($imagePath) -ine $outputDirectory -or
        [IO.Path]::GetExtension($resultPath) -ine '.json' -or
        [IO.Path]::GetExtension($imagePath) -ine '.png' -or
        -not [IO.Directory]::Exists($outputDirectory)) {
        throw 'Request outputs must be JSON/PNG files in the owned temporary UI directory.'
    }
    if (([IO.File]::GetAttributes($outputDirectory) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        [IO.File]::Exists($resultPath) -or [IO.Directory]::Exists($resultPath)) {
        throw 'The UI output directory must not be a link, and the result must be a new file.'
    }
    $script:ResultPath = $resultPath
    $script:ImagePath = $imagePath
    if ([IO.File]::Exists($script:ImagePath)) { throw 'The requested image path already exists.' }

    $processes = @(Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $targetPid")
    if ($processes.Count -ne 1 -or $processes[0].Name -notmatch '(?i)\AGodot.*\.exe\z' -or
        [string]::IsNullOrWhiteSpace([string]$processes[0].CommandLine)) {
        throw 'The requested PID is not one live Godot process.'
    }
    $expectedGamePath = [IO.Path]::GetFullPath((Join-Path $sourceRoot 'game'))
    $pathArgumentPattern = '(?i)(?:^|\s)--path(?:\s+|=)(?:"([^"]+)"|([^\s]+))(?=\s|$)'
    $pathArguments = [regex]::Matches([string]$processes[0].CommandLine,$pathArgumentPattern)
    if ($pathArguments.Count -ne 1) { throw 'The owned Godot command must have exactly one --path argument.' }
    $actualGamePath = if ($pathArguments[0].Groups[1].Success) { $pathArguments[0].Groups[1].Value } else { $pathArguments[0].Groups[2].Value }
    if ([IO.Path]::GetFullPath($actualGamePath).TrimEnd([char[]]@('\','/')) -ine $expectedGamePath) {
        throw 'The requested Godot PID is not running this job source.'
    }

    $script:TargetPid = $targetPid
    $script:TargetVerified = $true
    $windows = New-Object System.Collections.Generic.List[object]
    $callback = [UrmanGameUiNative+EnumProc]{ param($handle,$extra)
        [uint32]$owner = 0
        [void][UrmanGameUiNative]::GetWindowThreadProcessId($handle,[ref]$owner)
        if ([int]$owner -eq $script:TargetPid) {
            $text = New-Object System.Text.StringBuilder(512)
            [void][UrmanGameUiNative]::GetWindowText($handle,$text,512)
            if ($script:AllowedTitles -ccontains $text.ToString()) {
                $windows.Add(@{ Handle = $handle; Title = $text.ToString() })
            }
        }
        return $true
    }
    [void][UrmanGameUiNative]::EnumWindows($callback,[IntPtr]::Zero)
    if ($windows.Count -ne 1) { throw 'Expected exactly one approved URMAN window for the owned Godot PID.' }

    $script:TargetHandle = $windows[0].Handle
    $script:TargetTitle = [string]$windows[0].Title
    [uint32]$windowOwner = 0
    [void][UrmanGameUiNative]::GetWindowThreadProcessId($script:TargetHandle,[ref]$windowOwner)
    if ([int]$windowOwner -ne $script:TargetPid) { throw 'The selected window is not owned by the requested PID.' }
    $initial = New-Object UrmanGameUiNative+Rect
    if (-not [UrmanGameUiNative]::GetWindowRect($script:TargetHandle,[ref]$initial)) {
        throw 'The owned game window bounds are unavailable.'
    }
    $width = $initial.Right - $initial.Left
    $height = $initial.Bottom - $initial.Top
    if ($width -lt 100 -or $height -lt 100 -or $width -gt 4096 -or $height -gt 4096) {
        throw 'The owned game window bounds are outside the supported range.'
    }
    $script:InitialRect = [pscustomobject]@{
        Left = $initial.Left; Top = $initial.Top; Right = $initial.Right; Bottom = $initial.Bottom
    }

    Assert-TargetWindow
    if (-not (Test-OwnedForeground)) {
        [void][UrmanGameUiNative]::SetForegroundWindow($script:TargetHandle)
    }
    Start-Sleep -Milliseconds 200
    Assert-InputTarget

    if ([string]$script:Request.action -eq 'sequence') {
        $actions = @($script:Request.actions)
        if ($actions.Count -lt 1) { throw 'A UI sequence must contain at least one action.' }
    } elseif ([string]$script:Request.action -eq 'capture') {
        $actions = @()
    } else {
        $actions = @($script:Request)
    }
    foreach ($action in $actions) {
        Assert-InputTarget
        Invoke-GameAction $action
        Start-Sleep -Milliseconds 700
    }

    Assert-InputTarget
    $bitmap = New-Object System.Drawing.Bitmap($width,$height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        Assert-InputTarget
        $graphics.CopyFromScreen($script:InitialRect.Left,$script:InitialRect.Top,0,0,$bitmap.Size)
        $bitmap.Save($script:ImagePath,[System.Drawing.Imaging.ImageFormat]::Png)
        $script:ImageCaptured = $true
    } finally { $graphics.Dispose(); $bitmap.Dispose() }

    Write-Result $true $null
} catch {
    if ($script:ImageCaptured -and [IO.File]::Exists($script:ImagePath)) {
        Remove-Item -LiteralPath $script:ImagePath -Force -ErrorAction SilentlyContinue
        $script:ImageCaptured = $false
    }
    try { Write-Result $false $_.Exception.Message } catch { }
    exit 1
}
