using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The desk printer gives the authored P-COMMON print its paper: the sheet
/// lies hidden inside the body until the printed page is mounted, then it
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
        return true;
    }

    private void Apply()
    {
        if (_captureStaged || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.IsPhotoBookProjectionDeferred
            || !IsInsideTree() || !GodotObject.IsInstanceValid(this)
            || !GodotObject.IsInstanceValid(Sheet))
            return;

        var mounted = bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "P-COMMON" && photo.Mounted);
        EntryTarget?.SetPresentationEnabled(mounted);
        if (!mounted)
        {
            Sheet.Visible = false;
            Sheet.Position = new Vector3(Sheet.Position.X, Sheet.Position.Y, SheetInLocalZ);
            _appliedOnce = true;
            return;
        }

        Sheet.Visible = true;
        var goal = SheetOutLocalZ;
        // A restored session places the paper straight into the tray; only a
        // print that happens while the room is open plays the slide.
        if (!_appliedOnce || Mathf.IsEqualApprox(Sheet.Position.Z, goal))
        {
            Sheet.Position = new Vector3(Sheet.Position.X, Sheet.Position.Y, goal);
            _appliedOnce = true;
            return;
        }

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(Sheet, "position:z", goal, .9f);
        _appliedOnce = true;
    }

    /// <summary>Static capture frame only: shows the authored sheet in the tray
    /// without claiming the ordinary print happened. Never used in gameplay.</summary>
    internal void ApplyCaptureFacts(bool sheetInTray)
    {
        _captureStaged = true;
        EntryTarget?.SetPresentationEnabled(false);
        Sheet.Visible = sheetInTray;
        Sheet.Position = new Vector3(Sheet.Position.X, Sheet.Position.Y,
            sheetInTray ? SheetOutLocalZ : SheetInLocalZ);
    }

    public override void _ExitTree()
    {
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= Apply;
        _bridge = null;
    }
}
