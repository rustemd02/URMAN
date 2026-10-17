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
    internal InteractionTarget CommonFilePermissionTarget = null!;
    internal InteractionTarget HomeReturnTarget = null!;
    internal InteractionTarget HousePhotoSourceTarget = null!;
    internal InteractionTarget HousePhotoPermissionTarget = null!;
    internal InteractionTarget? TeaTarget;
    internal Node3D? Print;
    internal Node3D? HousePhotoEnvelope;
    internal Node3D? HousePhotoPrint;
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
            || !IsReady(PermissionTarget) || !IsReady(CommonFilePermissionTarget)
            || !IsReady(HomeReturnTarget) || !IsReady(HousePhotoSourceTarget)
            || !IsReady(HousePhotoPermissionTarget)
            || (TeaTarget is { } tea && !IsReady(tea)))
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
        var fileFound = bridge.PhotoWorldFactObserved("common_marat_file_found");
        var commonAcquired = bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "P-COMMON");
        var firstHomeTalk = bridge.PhotoWorldFactObserved("first_world_home_talk");
        var housePrintFound = bridge.PhotoWorldFactObserved("house_print_found");
        var houseAcquired = bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "P-HOUSE");

        var homeReturnLive = bookReceived && !firstHomeTalk
            && bridge.IsInteractionAvailable(HomeReturnTarget.InteractionId);
        var housePermissionLive = bookReceived && firstHomeTalk && housePrintFound && !houseAcquired
            && bridge.IsInteractionAvailable(HousePhotoPermissionTarget.InteractionId);
        var talkLive = bookReceived && !told && !homeReturnLive && !housePermissionLive;
        var permissionLive = bookReceived && told && found && !acquired && !homeReturnLive && !housePermissionLive;
        var sourceLive = bookReceived && !found;
        var riverLive = told && mountedAndRead;
        // The family file question only exists once the file was found on the
        // PC and the family copy is not saved yet.
        var filePermissionLive = fileFound && !commonAcquired && !homeReturnLive && !housePermissionLive;
        var houseSourceLive = bookReceived && firstHomeTalk && !housePrintFound
            && bridge.IsInteractionAvailable(HousePhotoSourceTarget.InteractionId);

        HomeReturnTarget.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-first-world-home-talk");
        HousePhotoSourceTarget.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-house-photo-source");
        HousePhotoPermissionTarget.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-house-photo-permission");
        HomeReturnTarget.SetPresentationEnabled(homeReturnLive);
        TalkTarget.SetPresentationEnabled(talkLive);
        PermissionTarget.SetPresentationEnabled(permissionLive);
        CommonFilePermissionTarget.SetPresentationEnabled(filePermissionLive);
        SourceTarget.SetPresentationEnabled(sourceLive);
        RiverEntryTarget.SetPresentationEnabled(riverLive);
        HousePhotoSourceTarget.SetPresentationEnabled(houseSourceLive);
        HousePhotoPermissionTarget.SetPresentationEnabled(housePermissionLive);
        // әби keeps her greeting whenever the chain is not asking a question.
        TeaTarget?.SetPresentationEnabled(!bookReceived || (!homeReturnLive && !talkLive
            && !permissionLive && !filePermissionLive && !housePermissionLive));

        if (HousePhotoEnvelope is { } houseEnvelope)
            houseEnvelope.Visible = bridge.ActiveSceneId == "urman.fullgame:scene/pw-home"
                && bookReceived && firstHomeTalk;

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

        if (HousePhotoPrint is { } housePrint)
        {
            var goal = firstHomeTalk ? PrintOutLocalZ : PrintInLocalZ;
            if (!_appliedOnce || Mathf.IsEqualApprox(housePrint.Position.Z, goal))
            {
                housePrint.Position = new Vector3(housePrint.Position.X, housePrint.Position.Y, goal);
            }
            else
            {
                var tween = CreateTween();
                tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(housePrint, "position:z", goal, .9f);
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
