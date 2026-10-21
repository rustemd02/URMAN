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
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] public struct MouseInput { public int dx,dy; public uint mouseData,dwFlags,time; public UIntPtr dwExtraInfo; }
 [StructLayout(LayoutKind.Sequential)] public struct Input { public uint type; public MouseInput mouse; }
 public delegate bool EnumProc(IntPtr h,IntPtr extra);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback,IntPtr extra);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,System.Text.StringBuilder text,int length);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint processId);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach,uint attachTo,bool attachState);
 [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code,uint mapType);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] public static extern bool GetClipCursor(out Rect r);
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref Point p);
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll",EntryPoint="SendMessageTimeoutW",SetLastError=true)]
 public static extern IntPtr SendMessageTimeout(IntPtr h,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
 [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint count,Input[] inputs,int inputSize);
 [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int virtualKey);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags,int x,int y,uint data,UIntPtr extra);
 [StructLayout(LayoutKind.Sequential)] public struct FileInfo {
  public uint Attributes,CreationLow,CreationHigh,AccessLow,AccessHigh,WriteLow,WriteHigh;
  public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
 }
 [DllImport("kernel32.dll",SetLastError=true)] public static extern bool GetFileInformationByHandle(IntPtr h,out FileInfo info);
 [DllImport("kernel32.dll",SetLastError=true)] public static extern bool GetHandleInformation(IntPtr h,out uint flags);
 [DllImport("kernel32.dll",SetLastError=true)] public static extern bool SetHandleInformation(IntPtr h,uint mask,uint flags);
 [DllImport("kernel32.dll")] public static extern uint GetFileType(IntPtr h);
 public static string ExecutableIdentity(IntPtr h) {
  FileInfo info;
  if (Marshal.SizeOf(typeof(FileInfo))!=52 || GetFileType(h)!=1 || !GetFileInformationByHandle(h,out info) ||
   (info.Attributes & 0x0410)!=0 || (info.IndexHigh==0 && info.IndexLow==0))
   throw new InvalidOperationException("The candidate executable hash changed.");
  return string.Format("{0:x8}:{1:x8}{2:x8}:{3:x8}{4:x8}:{5:x8}{6:x8}",
   info.Volume,info.IndexHigh,info.IndexLow,info.SizeHigh,info.SizeLow,info.WriteHigh,info.WriteLow);
 }
 public static int InputSize() { return Marshal.SizeOf(typeof(Input)); }
 public static IntPtr PackScreenPoint(int x,int y) {
  return new IntPtr(unchecked(((y & 0xffff) << 16) | (x & 0xffff)));
 }
 public static Input[] LeftClickInputs() {
  return new Input[] {
   new Input { type=0, mouse=new MouseInput { dwFlags=0x0002, dwExtraInfo=UIntPtr.Zero } },
   new Input { type=0, mouse=new MouseInput { dwFlags=0x0004, dwExtraInfo=UIntPtr.Zero } }
  };
 }
 public static Input[] LeftButtonUpInput() {
  return new Input[] { new Input { type=0, mouse=new MouseInput { dwFlags=0x0004, dwExtraInfo=UIntPtr.Zero } } };
 }
}
'@
[void][UrmanGameUiNative]::SetProcessDPIAware()

$script:AllowedTitles = @('URMAN','URMAN(DEBUG)','URMAN (DEBUG)','URMAN Studio','URMAN Studio(DEBUG)','URMAN Studio (DEBUG)')
$script:CandidatePin = $null
$script:TargetHandle = [IntPtr]::Zero
$script:TargetPid = 0
$script:TargetVerified = $false
$script:TargetTitle = $null
$script:InitialRect = $null
$script:FocusAcquisitionAttempted = $false
$script:ForegroundRelation = 'unknown'
$script:FocusPhase = 'unknown'
$script:ActivationBranches = [ordered]@{
    first_set_foreground_attempted = $false
    first_set_foreground_succeeded = $false
    first_foreground_check_owned = $false
    attach_input_attempted = $false
    attach_input_succeeded = $false
    detach_input_attempted = $false
    detach_input_succeeded = $false
    retry_set_foreground_attempted = $false
    retry_set_foreground_succeeded = $false
    retry_foreground_check_attempted = $false
    retry_foreground_check_owned = $false
    caption_activation_attempted = $false
    caption_activation_returned = $false
    caption_initial_foreground_check_attempted = $false
    caption_initial_foreground_check_owned = $false
    caption_post_position_foreground_check_attempted = $false
    caption_post_position_foreground_check_owned = $false
    caption_raise_attempted = $false
    caption_raise_succeeded = $false
    caption_click_attempted = $false
    caption_click_succeeded = $false
}
$script:PostActivationForegroundHwndIsNull = $null
$script:PostActivationForegroundHwnd = $null
$script:PostActivationForegroundPid = $null
$script:PostActivationDiagnostics = $null
$script:Request = $null
$script:ResultPath = $null
$script:ImagePath = $null
$script:ImageCaptured = $false

function Test-OwnedForeground {
    $foreground = [UrmanGameUiNative]::GetForegroundWindow()
    if ($script:FocusPhase -eq 'post_activation') {
        $script:PostActivationForegroundHwndIsNull = $foreground -eq [IntPtr]::Zero
        $script:PostActivationForegroundHwnd = if ($foreground -eq [IntPtr]::Zero) { $null } else { $foreground.ToInt64() }
        $script:PostActivationForegroundPid = $null
    }
    if ($foreground -eq $script:TargetHandle) {
        $script:ForegroundRelation = 'owned'
        return $true
    }
    [uint32]$owner = 0
    [void][UrmanGameUiNative]::GetWindowThreadProcessId($foreground,[ref]$owner)
    if ($script:FocusPhase -eq 'post_activation' -and $foreground -ne [IntPtr]::Zero -and $owner -ne 0) {
        $script:PostActivationForegroundPid = [long]$owner
    }
    if ($foreground -eq [IntPtr]::Zero) {
        $script:ForegroundRelation = 'other_or_none'
        return $false
    }
    if ($owner -eq 0) {
        $script:ForegroundRelation = 'unknown'
        return $false
    }
    if ([int]$owner -ne $script:TargetPid) {
        $script:ForegroundRelation = 'other_or_none'
        return $false
    }
    if ([UrmanGameUiNative]::GetAncestor($foreground,3) -eq $script:TargetHandle) {
        $script:ForegroundRelation = 'owned'
        return $true
    }
    $script:ForegroundRelation = 'same_pid_other_window'
    return $false
}

function New-PostActivationDiagnostics {
    $targetIsWindow = [UrmanGameUiNative]::IsWindow($script:TargetHandle)
    $targetPidQuerySucceeded = $false
    $targetPidMatchesJob = $false
    if ($targetIsWindow) {
        [uint32]$targetOwnerPid = 0
        $targetThread = [UrmanGameUiNative]::GetWindowThreadProcessId($script:TargetHandle,[ref]$targetOwnerPid)
        $targetPidQuerySucceeded = $targetThread -ne 0
        $targetPidMatchesJob = $targetPidQuerySucceeded -and [int]$targetOwnerPid -eq $script:TargetPid
    }
    return [ordered]@{
        target_is_window = [bool]$targetIsWindow
        target_pid_query_succeeded = [bool]$targetPidQuerySucceeded
        target_pid_matches_job = [bool]$targetPidMatchesJob
        foreground_hwnd_is_null = [bool]$script:PostActivationForegroundHwndIsNull
        foreground_hwnd = $script:PostActivationForegroundHwnd
        foreground_pid = $script:PostActivationForegroundPid
        activation_branches = $script:ActivationBranches
    }
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

function Get-VerifiedCaptionPoint {
    Assert-TargetWindow
    if ([UrmanGameUiNative]::IsIconic($script:TargetHandle)) { throw 'The owned game window is minimized.' }
    if (-not [UrmanGameUiNative]::IsWindowVisible($script:TargetHandle)) { throw 'The owned game window is hidden.' }

    $window = New-Object UrmanGameUiNative+Rect
    $client = New-Object UrmanGameUiNative+Rect
    if (-not [UrmanGameUiNative]::GetWindowRect($script:TargetHandle,[ref]$window) -or
        -not [UrmanGameUiNative]::GetClientRect($script:TargetHandle,[ref]$client)) {
        throw 'The owned game caption point is unavailable.'
    }
    if ($client.Right -le $client.Left -or $client.Bottom -le $client.Top) {
        throw 'The owned game caption point is unavailable.'
    }
    if ($window.Left -ne $script:InitialRect.Left -or $window.Top -ne $script:InitialRect.Top -or
        $window.Right -ne $script:InitialRect.Right -or $window.Bottom -ne $script:InitialRect.Bottom) {
        throw 'The owned game caption bounds changed.'
    }

    $clientOrigin = New-Object UrmanGameUiNative+Point
    if (-not [UrmanGameUiNative]::ClientToScreen($script:TargetHandle,[ref]$clientOrigin)) {
        throw 'The owned game caption point is unavailable.'
    }
    $width = $window.Right - $window.Left
    $height = $window.Bottom - $window.Top
    $captionHeight = $clientOrigin.Y - $window.Top
    if ($width -lt 2 -or $height -lt 2 -or $captionHeight -lt 2 -or $captionHeight -ge $height) {
        throw 'The owned game caption point is unavailable.'
    }

    $x = $window.Left + [int][Math]::Floor($width / 2.0)
    $y = $window.Top + [int][Math]::Floor($captionHeight / 2.0)
    if ($x -lt -32768 -or $x -gt 32767 -or $y -lt -32768 -or $y -gt 32767) {
        throw 'The owned game caption point is unavailable.'
    }
    return @{ X = $x; Y = $y }
}

function Assert-VerifiedCaptionOwnership([object]$Point) {
    $screenPoint = New-Object UrmanGameUiNative+Point
    $screenPoint.X = [int]$Point.X
    $screenPoint.Y = [int]$Point.Y
    $hit = [UrmanGameUiNative]::WindowFromPoint($screenPoint)
    if ($hit -eq [IntPtr]::Zero -or $hit -ne $script:TargetHandle) {
        throw 'The owned game caption hit window changed.'
    }

    [uint32]$hitOwner = 0
    $hitThread = [UrmanGameUiNative]::GetWindowThreadProcessId($hit,[ref]$hitOwner)
    if ($hitThread -eq 0 -or [int]$hitOwner -ne $script:TargetPid -or
        [UrmanGameUiNative]::GetAncestor($hit,3) -ne $script:TargetHandle) {
        throw 'The owned game caption hit owner changed.'
    }
    return $hit
}

function Assert-VerifiedCaptionHit([object]$Point) {
    $hit = Assert-VerifiedCaptionOwnership $Point
    $screenPoint = New-Object UrmanGameUiNative+Point
    $screenPoint.X = [int]$Point.X
    $screenPoint.Y = [int]$Point.Y
    $hitTestResult = [IntPtr]::Zero
    $hitTestPoint = [UrmanGameUiNative]::PackScreenPoint($screenPoint.X,$screenPoint.Y)
    $hitTestSent = [UrmanGameUiNative]::SendMessageTimeout(
        $hit,0x0084,[IntPtr]::Zero,$hitTestPoint,0x22,250,[ref]$hitTestResult)
    if ($hitTestSent -eq [IntPtr]::Zero -or $hitTestResult.ToInt64() -ne 2) {
        throw 'The owned game caption point is unavailable.'
    }
}

function Invoke-VerifiedCaptionActivation {
    $script:ActivationBranches.caption_initial_foreground_check_attempted = $true
    $script:ActivationBranches.caption_initial_foreground_check_owned = Test-OwnedForeground
    if ($script:ActivationBranches.caption_initial_foreground_check_owned) { return }

    $originalPointer = New-Object UrmanGameUiNative+Point
    if (-not [UrmanGameUiNative]::GetCursorPos([ref]$originalPointer)) {
        throw 'The owned game caption pointer is unavailable.'
    }
    $captionPoint = $null
    $pointerMoved = $false
    $buttonMayBeDown = $false
    $sendInputAttempted = $false
    try {
        $captionPoint = Get-VerifiedCaptionPoint
        if (([UrmanGameUiNative]::GetAsyncKeyState(1) -band 0x8000) -ne 0) {
            throw 'The owned game caption input was refused.'
        }
        Assert-VerifiedCaptionHit $captionPoint
        $currentPointer = New-Object UrmanGameUiNative+Point
        if (-not [UrmanGameUiNative]::GetCursorPos([ref]$currentPointer)) {
            throw 'The owned game caption pointer is unavailable.'
        }
        if ($currentPointer.X -ne $originalPointer.X -or $currentPointer.Y -ne $originalPointer.Y) {
            throw 'The pointer moved during the pre-move caption check.'
        }
        if (([UrmanGameUiNative]::GetAsyncKeyState(1) -band 0x8000) -ne 0) {
            throw 'The owned game caption input was refused.'
        }
        $pointerMoved = $true
        if (-not [UrmanGameUiNative]::SetCursorPos([int]$captionPoint.X,[int]$captionPoint.Y)) {
            throw 'The owned game caption input was refused.'
        }

        $recheckedPoint = Get-VerifiedCaptionPoint
        if ($recheckedPoint.X -ne $captionPoint.X -or $recheckedPoint.Y -ne $captionPoint.Y) {
            throw 'The owned game caption point changed after positioning.'
        }
        $currentPointer = New-Object UrmanGameUiNative+Point
        if (-not [UrmanGameUiNative]::GetCursorPos([ref]$currentPointer)) {
            throw 'The owned game caption pointer is unavailable.'
        }
        if ($currentPointer.X -ne $captionPoint.X -or $currentPointer.Y -ne $captionPoint.Y) {
            $clip = New-Object UrmanGameUiNative+Rect
            if ([UrmanGameUiNative]::GetClipCursor([ref]$clip) -and
                $clip.Right -gt $clip.Left -and $clip.Bottom -gt $clip.Top) {
                $captionOutsideClip = $captionPoint.X -lt $clip.Left -or $captionPoint.X -ge $clip.Right -or
                    $captionPoint.Y -lt $clip.Top -or $captionPoint.Y -ge $clip.Bottom
                $pointerWithinClip = $currentPointer.X -ge $clip.Left -and $currentPointer.X -le $clip.Right -and
                    $currentPointer.Y -ge $clip.Top -and $currentPointer.Y -le $clip.Bottom
                $pointerOnClipEdge = ($currentPointer.X -ge $clip.Left -and $currentPointer.X -le ($clip.Left + 1)) -or
                    ($currentPointer.X -ge ($clip.Right - 2) -and $currentPointer.X -le $clip.Right) -or
                    ($currentPointer.Y -ge $clip.Top -and $currentPointer.Y -le ($clip.Top + 1)) -or
                    ($currentPointer.Y -ge ($clip.Bottom - 2) -and $currentPointer.Y -le $clip.Bottom)
                if ($captionOutsideClip -and $pointerWithinClip -and $pointerOnClipEdge) {
                    Assert-TargetWindow
                    if ([UrmanGameUiNative]::IsIconic($script:TargetHandle)) {
                        throw 'The owned game window is minimized.'
                    }
                    if (-not [UrmanGameUiNative]::IsWindowVisible($script:TargetHandle)) {
                        throw 'The owned game window is hidden.'
                    }
                    $script:ActivationBranches.caption_raise_attempted = $true
                    $script:ActivationBranches.caption_raise_succeeded = [UrmanGameUiNative]::BringWindowToTop($script:TargetHandle)
                    if (-not $script:ActivationBranches.caption_raise_succeeded) {
                        throw 'Windows refused to raise the owned game window.'
                    }
                    return
                }
            }
            throw 'The pointer moved after caption positioning.'
        }
        $script:ActivationBranches.caption_post_position_foreground_check_attempted = $true
        $script:ActivationBranches.caption_post_position_foreground_check_owned = Test-OwnedForeground
        if ($script:ActivationBranches.caption_post_position_foreground_check_owned) { return }

        $inputs = [UrmanGameUiNative]::LeftClickInputs()
        Assert-TargetWindow
        Assert-VerifiedCaptionHit $captionPoint
        Assert-TargetWindow
        [void](Assert-VerifiedCaptionOwnership $captionPoint)
        $currentPointer = New-Object UrmanGameUiNative+Point
        if (-not [UrmanGameUiNative]::GetCursorPos([ref]$currentPointer)) {
            throw 'The owned game caption pointer is unavailable.'
        }
        if ($currentPointer.X -ne $captionPoint.X -or $currentPointer.Y -ne $captionPoint.Y) {
            throw 'The pointer moved during the caption hit test.'
        }
        if (([UrmanGameUiNative]::GetAsyncKeyState(1) -band 0x8000) -ne 0) {
            throw 'The owned game caption input was refused.'
        }
        $sendInputAttempted = $true
        $script:ActivationBranches.caption_click_attempted = $true
        $inserted = [UrmanGameUiNative]::SendInput([uint32]$inputs.Length,$inputs,[UrmanGameUiNative]::InputSize())
        $sendInputAttempted = $false
        if ($inserted -gt 0) { $buttonMayBeDown = $true }
        $script:ActivationBranches.caption_click_succeeded = $inserted -eq 2
        if ($inserted -ne 2) { throw 'The owned game caption input was refused.' }
        $buttonMayBeDown = $false
        Start-Sleep -Milliseconds 50
    } finally {
        if ($buttonMayBeDown -or $sendInputAttempted) {
            $upInput = [UrmanGameUiNative]::LeftButtonUpInput()
            $released = [UrmanGameUiNative]::SendInput(1,$upInput,[UrmanGameUiNative]::InputSize())
            if ($released -ne 1) { Release-MouseSafely 4 }
        }
        if ($pointerMoved -and $captionPoint -ne $null) {
            $currentPointer = New-Object UrmanGameUiNative+Point
            if ([UrmanGameUiNative]::GetCursorPos([ref]$currentPointer) -and
                $currentPointer.X -eq $captionPoint.X -and $currentPointer.Y -eq $captionPoint.Y) {
                [void][UrmanGameUiNative]::SetCursorPos($originalPointer.X,$originalPointer.Y)
            }
        }
    }
}

function Focus-VerifiedTarget {
    if (Test-OwnedForeground) { return }
    $script:FocusPhase = 'activation'
    $script:FocusAcquisitionAttempted = $true
    $script:ActivationBranches.first_set_foreground_attempted = $true
    $script:ActivationBranches.first_set_foreground_succeeded = [UrmanGameUiNative]::SetForegroundWindow($script:TargetHandle)
    $script:ActivationBranches.first_foreground_check_owned = Test-OwnedForeground
    if ($script:ActivationBranches.first_foreground_check_owned) { return }

    $foreground = [UrmanGameUiNative]::GetForegroundWindow()
    $currentThread = [UrmanGameUiNative]::GetCurrentThreadId()
    [uint32]$foregroundProcess = 0
    $foregroundThread = [uint32]0
    if ($foreground -ne [IntPtr]::Zero) {
        $foregroundThread = [UrmanGameUiNative]::GetWindowThreadProcessId($foreground,[ref]$foregroundProcess)
        if ($foregroundThread -eq 0 -or $currentThread -eq 0) {
            throw 'The owned game window lost foreground focus.'
        }
    }

    $attached = $false
    $activationRefused = $false
    try {
        if ($foregroundThread -ne 0 -and $foregroundThread -ne $currentThread) {
            $script:ActivationBranches.attach_input_attempted = $true
            $attached = [UrmanGameUiNative]::AttachThreadInput($currentThread,$foregroundThread,$true)
            $script:ActivationBranches.attach_input_succeeded = $attached
            if (-not $attached) { throw 'The helper could not attach to the foreground input thread.' }
        }
        if ($foreground -ne [IntPtr]::Zero -and
            [UrmanGameUiNative]::GetForegroundWindow() -ne $foreground) {
            throw 'The foreground window changed during focus acquisition.'
        }
        $script:ActivationBranches.retry_set_foreground_attempted = $true
        $retrySetForegroundSucceeded = [UrmanGameUiNative]::SetForegroundWindow($script:TargetHandle)
        $script:ActivationBranches.retry_set_foreground_succeeded = $retrySetForegroundSucceeded
        $activationRefused = -not $retrySetForegroundSucceeded
    } finally {
        # Detach before any injected input; AttachThreadInput resets shared key state.
        if ($attached) {
            $script:ActivationBranches.detach_input_attempted = $true
            $script:ActivationBranches.detach_input_succeeded = [UrmanGameUiNative]::AttachThreadInput($currentThread,$foregroundThread,$false)
            if (-not $script:ActivationBranches.detach_input_succeeded) {
                throw 'The helper could not detach from the foreground input thread.'
            }
        }
    }
    if ($activationRefused) {
        $script:ActivationBranches.retry_foreground_check_attempted = $true
        $script:ActivationBranches.retry_foreground_check_owned = Test-OwnedForeground
        if (-not $script:ActivationBranches.retry_foreground_check_owned) {
            $script:ActivationBranches.caption_activation_attempted = $true
            Invoke-VerifiedCaptionActivation
            $script:ActivationBranches.caption_activation_returned = $true
        }
    }
}

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
        'c' { 67 }
        'f' { 70 }
        'j' { 74 }
        'v' { 86 }
        'z' { 90 }
        default { throw 'Unsupported game UI key.' }
    }
    $physicalMainKey = $plain -notin @('{DOWN}','{UP}','{LEFT}','{RIGHT}')
    $scanCode = 0
    if ($physicalMainKey) {
        $scanCode = [UrmanGameUiNative]::MapVirtualKey([uint32]$vk,0)
        if ($scanCode -eq 0) { throw 'The game UI key has no platform scan code.' }
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
        [UrmanGameUiNative]::keybd_event([byte]$vk,[byte]$scanCode,0,[UIntPtr]::Zero)
        $keyDown = $true
        $holdTimer = [Diagnostics.Stopwatch]::StartNew()
        while ($holdTimer.ElapsedMilliseconds -lt 200) {
            Assert-InputTarget
            $remaining = 200 - [int]$holdTimer.ElapsedMilliseconds
            if ($remaining -gt 0) { Start-Sleep -Milliseconds ([Math]::Min(50,$remaining)) }
        }
        if ($physicalMainKey) {
            Release-PhysicalKeySafely ([byte]$vk) ([byte]$scanCode)
        } else {
            Release-KeySafely ([byte]$vk)
        }
        $keyDown = $false
        Start-Sleep -Milliseconds 200
    } finally {
        if ($keyDown) {
            if ($physicalMainKey) {
                Release-PhysicalKeySafely ([byte]$vk) ([byte]$scanCode)
            } else {
                Release-KeySafely ([byte]$vk)
            }
        }
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

function Look-Game([object]$Action) {
    $dx = 0
    $dy = 0
    if (-not [int]::TryParse([string]$Action.dx,[ref]$dx) -or
        -not [int]::TryParse([string]$Action.dy,[ref]$dy) -or
        $dx -lt -300 -or $dx -gt 300 -or $dy -lt -300 -or $dy -gt 300 -or
        ($dx -eq 0 -and $dy -eq 0)) {
        throw 'Look deltas must be non-zero integers between -300 and 300.'
    }

    Assert-InputTarget
    [UrmanGameUiNative]::mouse_event(1,$dx,$dy,0,[UIntPtr]::Zero)
    Assert-InputTarget
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
        'look' { Look-Game $Action }
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
        targetHwnd = $(if ($script:TargetHandle -ne [IntPtr]::Zero) { $script:TargetHandle.ToInt64() } else { $null })
        foregroundRelation = $script:ForegroundRelation
        focusPhase = $script:FocusPhase
        rect = $rectValue
        image = $(if ($Ok -and $script:ImageCaptured) { $script:ImagePath } else { $null })
        error = $ErrorText
    }
    if ($null -ne $script:PostActivationDiagnostics) {
        $report['postActivationDiagnostics'] = $script:PostActivationDiagnostics
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
    $candidateRequest = $script:Request.PSObject.Properties.Name -contains 'candidateRoot'
    if ($candidateRequest) {
        if ($script:Request.PSObject.Properties.Name -contains 'sourceRoot' -or
            [string]::IsNullOrWhiteSpace([string]$script:Request.candidateRoot) -or
            -not [IO.Path]::IsPathRooted([string]$script:Request.candidateRoot)) {
            throw 'The candidate executable does not match this job.'
        }
        $candidateRoot = [IO.Path]::GetFullPath([string]$script:Request.candidateRoot).TrimEnd([char[]]@('\','/'))
        $expectedSuffix = '\runs\' + [string]$script:Request.jobId + '\candidate'
        $candidateExecutable = [IO.Path]::GetFullPath((Join-Path $candidateRoot 'windows/URMAN.exe'))
        if (-not $candidateRoot.EndsWith($expectedSuffix,[StringComparison]::OrdinalIgnoreCase) -or
            -not [IO.Directory]::Exists($candidateRoot) -or -not [IO.File]::Exists($candidateExecutable)) {
            throw 'The candidate executable does not match this job.'
        }
        if (([IO.File]::GetAttributes($candidateExecutable) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'The candidate executable does not match this job.'
        }
        for ($ancestor = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($candidateExecutable));
             $null -ne $ancestor; $ancestor = $ancestor.Parent) {
            if (-not $ancestor.Exists -or ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'The candidate executable does not match this job.'
            }
        }
        $pinValue = [long]0
        if ([string]$script:Request.executableSha256 -cnotmatch '\A[a-f0-9]{64}\z' -or
            [string]$script:Request.executablePinIdentity -cnotmatch '\A[0-9a-f]{8}(?::[0-9a-f]{16}){3}\z' -or
            -not [long]::TryParse([string]$script:Request.executablePinHandle,[ref]$pinValue) -or $pinValue -le 0) {
            throw 'The candidate executable hash changed.'
        }
        $pinHandle = [IntPtr]::new($pinValue)
        [uint32]$pinFlags = 0
        if (-not [UrmanGameUiNative]::GetHandleInformation($pinHandle,[ref]$pinFlags) -or ($pinFlags -band 1) -eq 0) {
            throw 'The candidate executable hash changed.'
        }
        $script:CandidatePin = [Microsoft.Win32.SafeHandles.SafeFileHandle]::new($pinHandle,$true)
        if (-not [UrmanGameUiNative]::SetHandleInformation($pinHandle,1,0) -or
            [UrmanGameUiNative]::ExecutableIdentity($pinHandle) -cne [string]$script:Request.executablePinIdentity) {
            throw 'The candidate executable hash changed.'
        }
        $pathStream = [IO.File]::Open($candidateExecutable,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        try {
            if ([UrmanGameUiNative]::ExecutableIdentity($pathStream.SafeFileHandle.DangerousGetHandle()) -cne
                    [string]$script:Request.executablePinIdentity) {
                throw 'The candidate executable hash changed.'
            }
        } finally { $pathStream.Dispose() }
        $candidateRunnerId = 0
        if (-not [int]::TryParse([string]$script:Request.runnerPid,[ref]$candidateRunnerId) -or
            $candidateRunnerId -le 0 -or $candidateRunnerId -eq $targetPid) {
            throw 'The candidate process is not owned by the live runner.'
        }
    } else {
        if ($script:Request.PSObject.Properties.Name -contains 'runnerPid' -or
            $script:Request.PSObject.Properties.Name -contains 'executableSha256' -or
            $script:Request.PSObject.Properties.Name -contains 'executablePinHandle' -or
            $script:Request.PSObject.Properties.Name -contains 'executablePinIdentity' -or
            [string]::IsNullOrWhiteSpace([string]$script:Request.sourceRoot) -or
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
    if ($candidateRequest) {
        if ($processes.Count -ne 1 -or $processes[0].Name -ine 'URMAN.exe' -or
            [string]::IsNullOrWhiteSpace([string]$processes[0].ExecutablePath) -or
            [IO.Path]::GetFullPath([string]$processes[0].ExecutablePath) -ine $candidateExecutable) {
            throw 'The candidate executable does not match this job.'
        }
        $candidateRunner = @(Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $candidateRunnerId")
        if ($candidateRunner.Count -ne 1) { throw 'The candidate process is not owned by the live runner.' }
        $ancestorProcess = $processes[0]
        $ancestorIds = New-Object 'System.Collections.Generic.HashSet[int]'
        $runnerFound = $false
        for ($depth = 0; $depth -lt 128; $depth++) {
            $parentId = [int]$ancestorProcess.ParentProcessId
            if ($parentId -le 0 -or -not $ancestorIds.Add($parentId)) { break }
            if ($parentId -eq $candidateRunnerId) { $runnerFound = $true; break }
            $parents = @(Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $parentId")
            if ($parents.Count -ne 1) { break }
            $ancestorProcess = $parents[0]
        }
        if (-not $runnerFound) { throw 'The candidate process is not owned by the live runner.' }
    } else {
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
    if ([UrmanGameUiNative]::IsIconic($script:TargetHandle)) { throw 'The owned game window is minimized.' }
    if (-not [UrmanGameUiNative]::IsWindowVisible($script:TargetHandle)) { throw 'The owned game window is hidden.' }
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
    $script:FocusPhase = 'pre_activation'
    if (-not (Test-OwnedForeground)) {
        Focus-VerifiedTarget
    }
    Start-Sleep -Milliseconds 200
    if ($script:FocusAcquisitionAttempted) {
        $script:FocusPhase = 'post_activation'
        if (-not (Test-OwnedForeground)) {
            if ($script:ForegroundRelation -eq 'other_or_none') {
                try { $script:PostActivationDiagnostics = New-PostActivationDiagnostics } catch { }
            }
            throw 'The owned game window lost foreground focus after activation.'
        }
    }
    $script:FocusPhase = 'action'
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
} finally {
    if ($null -ne $script:CandidatePin) { $script:CandidatePin.Dispose() }
}
