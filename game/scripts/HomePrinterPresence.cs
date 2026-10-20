using System.Linq;
using Godot;
using Urman.Core.Narrative;

namespace Urman.Godot;

/// <summary>
/// The desk printer gives the authored P-COMMON print its paper: the sheet
/// lies hidden inside the body until the authored print is completed, then it
/// slides out onto the output tray. Visibility is recomputed from the real
/// runtime state on every change and on the first frame of a loaded session,
/// so a restored save shows the same paper without replaying the motion —
/// the presence keeps no latch of its own. The staged capture fact exists
/// only for one static frame and is never set in an ordinary session.
/// </summary>
internal partial class HomePrinterPresence : Node3D
{
    internal Node3D Sheet = null!;
    internal InteractionTarget? EntryTarget;
    internal float SheetInLocalZ = .075f;
    internal float SheetOutLocalZ = .215f;

    private RuntimeBridge? _bridge;
    private bool _appliedOnce;
    private object? _projectedSession;
    private bool _projectedPrinted;
    private Tween? _sheetTween;
    private bool _captureStaged;

    public override void _Ready() => SetProcess(true);

    public override void _Process(double delta)
    {
        if (_bridge is null && !TryBindBridge())
            return;
        if (!GodotObject.IsInstanceValid(Sheet) || !Sheet.IsNodeReady()
            || _bridge!.IsPhotoBookProjectionDeferred)
            return;

        Apply();
        SetProcess(false);
    }

    private bool TryBindBridge()
    {
        if (!IsInsideTree()
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge)
            return false;

        _bridge = bridge;
        bridge.RuntimeStateChanged += Apply;
        bridge.PlayTimeBoundary += OnPlayTimeBoundary;
        return true;
    }

    private void OnPlayTimeBoundary(string boundary)
    {
        if (_captureStaged || boundary != "load-start") return;
        StopSheetTween();
        _appliedOnce = false;
        _projectedSession = null;
        SetProcess(true);
    }

    private void Apply()
    {
        if (_captureStaged || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.IsPhotoBookProjectionDeferred
            || !IsInsideTree() || !GodotObject.IsInstanceValid(this)
            || !GodotObject.IsInstanceValid(Sheet))
            return;

        var session = bridge.SessionIdentity;
        var printed = bridge.HasPrintedCommonPhoto;
        var mounted = bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "P-COMMON" && photo.Mounted);
        var commonCompleted = session is not null
            && PhotoWorldState.IsWorldCompleted(session.SelectState(), "W04");
        // The paper stays here; unfinished visits begin at the real common view.
        EntryTarget?.SetPresentationEnabled(mounted && commonCompleted);
        var sameSession = _appliedOnce && ReferenceEquals(session, _projectedSession);
        if (sameSession && printed == _projectedPrinted) return;

        var animate = sameSession && !_projectedPrinted && printed;
        StopSheetTween();
        _projectedSession = session;
        _projectedPrinted = printed;
        _appliedOnce = true;
        Sheet.Visible = printed;
        var goal = printed ? SheetOutLocalZ : SheetInLocalZ;
        // A restored session places the paper straight into the tray; only a
        // print that happens while the room is open plays the slide.
        if (!animate || Mathf.IsEqualApprox(Sheet.Position.Z, goal))
        {
            Sheet.Position = new Vector3(Sheet.Position.X, Sheet.Position.Y, goal);
            return;
        }

        _sheetTween = CreateTween();
        _sheetTween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _sheetTween.TweenProperty(Sheet, "position:z", goal, .9f);
    }

    private void StopSheetTween()
    {
        _sheetTween?.Kill();
        _sheetTween = null;
    }

    /// <summary>Static capture frame only: shows the authored sheet in the tray
    /// without claiming the ordinary print happened. Never used in gameplay.</summary>
    internal void ApplyCaptureFacts(bool sheetInTray)
    {
        StopSheetTween();
        _captureStaged = true;
        EntryTarget?.SetPresentationEnabled(false);
        Sheet.Visible = sheetInTray;
        Sheet.Position = new Vector3(Sheet.Position.X, Sheet.Position.Y,
            sheetInTray ? SheetOutLocalZ : SheetInLocalZ);
    }

    public override void _ExitTree()
    {
        StopSheetTween();
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
        {
            bridge.RuntimeStateChanged -= Apply;
            bridge.PlayTimeBoundary -= OnPlayTimeBoundary;
        }
        _bridge = null;
        _projectedSession = null;
        _appliedOnce = false;
    }
}
