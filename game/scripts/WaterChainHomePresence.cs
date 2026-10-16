using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The home side of the water print chain. Exactly one affordance stays live
/// at the family table and one at әби, so the envelope, the place name, the
/// permission, the mounted page and the open print never shadow each other on
/// the interaction ray. Facts, photo state and scene routing remain authored;
/// this owner only projects them onto the physical props.
/// </summary>
internal partial class WaterChainHomePresence : Node3D
{
    internal InteractionTarget SourceTarget = null!;
    internal InteractionTarget RiverEntryTarget = null!;
    internal InteractionTarget TalkTarget = null!;
    internal InteractionTarget PermissionTarget = null!;
    internal InteractionTarget? TeaTarget;
    internal Node3D? Print;
    internal float PrintInLocalZ = .024f;
    internal float PrintOutLocalZ = .085f;

    private RuntimeBridge? _bridge;
    private bool _appliedOnce;

    public override void _Ready() => SetProcess(true);

    public override void _Process(double delta)
    {
        if (_bridge is null && !TryBindBridge())
            return;
        // The zone's own targets capture their ray layer in _Ready; the first
        // presentation pass waits for that so no window is written too early.
        if (!IsReady(SourceTarget) || !IsReady(RiverEntryTarget) || !IsReady(TalkTarget)
            || !IsReady(PermissionTarget) || (TeaTarget is { } tea && !IsReady(tea)))
            return;

        Apply();
        SetProcess(false);
    }

    private static bool IsReady(InteractionTarget target) =>
        GodotObject.IsInstanceValid(target) && target.IsNodeReady();

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
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !IsInsideTree() || !GodotObject.IsInstanceValid(this))
            return;

        var bookReceived = bridge.CanInspectReceivedPhotoBook;
        var told = bridge.PhotoWorldFactObserved("home_water_place_told");
        var found = bridge.PhotoWorldFactObserved("water_photo_found");
        var water = bridge.PhotoBookPhotos().FirstOrDefault(photo => photo.PhotoId == "P-WATER");
        var acquired = water is not null;
        var mountedAndRead = water is { Mounted: true, BackRead: true };

        var talkLive = bookReceived && !told;
        var permissionLive = bookReceived && told && found && !acquired;
        var sourceLive = bookReceived && !found;
        var riverLive = told && mountedAndRead;

        TalkTarget.SetPresentationEnabled(talkLive);
        PermissionTarget.SetPresentationEnabled(permissionLive);
        SourceTarget.SetPresentationEnabled(sourceLive);
        RiverEntryTarget.SetPresentationEnabled(riverLive);
        // әби keeps her greeting whenever the chain is not asking a question.
        TeaTarget?.SetPresentationEnabled(!bookReceived || (!talkLive && !permissionLive));

        if (Print is { } print)
        {
            var goal = found ? PrintOutLocalZ : PrintInLocalZ;
            if (!_appliedOnce || Mathf.IsEqualApprox(print.Position.Z, goal))
            {
                print.Position = new Vector3(print.Position.X, print.Position.Y, goal);
            }
            else
            {
                var tween = CreateTween();
                tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(print, "position:z", goal, .9f);
            }
        }

        _appliedOnce = true;
    }

    public override void _ExitTree()
    {
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= Apply;
        _bridge = null;
    }
}
