using System.Linq;
using System.Text.Json;
using Godot;
using Urman.Core.Narrative;

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
    internal InteractionTarget? FamilyBoxSourceTarget;
    internal InteractionTarget? FamilyBoxPermissionTarget;
    internal InteractionTarget? CellarPhotoSourceTarget;
    internal InteractionTarget? CellarPhotoPermissionTarget;
    internal Node3D? FamilyBoxLid;
    internal Node3D? HandPhotoCard;
    internal Node3D? CellarPhotoCard;
    internal float PrintInLocalZ = .024f;
    internal float PrintOutLocalZ = .085f;
    internal float CellarPhotoInLocalZ = .055f;
    internal float CellarPhotoOutLocalZ = .17f;

    private RuntimeBridge? _bridge;
    private bool _appliedOnce;
    private InteractionTarget? _housePhotoEntryTarget;
    private InteractionTarget? _forestPhotoEntryTarget;

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
            || (FamilyBoxSourceTarget is { } familyBoxSource && !IsReady(familyBoxSource))
            || (FamilyBoxPermissionTarget is { } familyBoxPermission && !IsReady(familyBoxPermission))
            || (CellarPhotoSourceTarget is { } cellarPhotoSource && !IsReady(cellarPhotoSource))
            || (CellarPhotoPermissionTarget is { } cellarPhotoPermission && !IsReady(cellarPhotoPermission))
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
        var housePhoto = bridge.PhotoBookPhotos().FirstOrDefault(photo => photo.PhotoId == "P-HOUSE");
        var houseAcquired = housePhoto is not null;
        var handAcquired = bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "S-HAND");
        var cellarFound = bridge.PhotoWorldFactObserved("cellar_photo_found");
        var cellarAcquired = bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "P-CELLAR");
        var familyBoxOpened = bridge.PhotoWorldFactObserved("family_box_opened");
        const string photographerSourceId = "urman.fullgame:interaction/pw-photographer-photo-source";
        const string photographerPermissionId = "urman.fullgame:interaction/pw-photographer-photo-permission";
        const string photographerVerifyId = "urman.fullgame:interaction/pw-verify-photographer-evidence";
        var homeActive = bridge.ActiveSceneId == "urman.fullgame:scene/pw-home";
        var photographerFound = bridge.PhotoWorldFactObserved("photographer_copy_found");
        var photographerAcquired = bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "S-PHOTOGRAPHER");
        var photographerVerified = bridge.SessionIdentity is { } photographerSession
            && PhotoWorldState.EvaluateCondition(JsonSerializer.SerializeToElement(new
            {
                op = "photoworlds.evidence.confirmed", evidenceId = "E-PHOTOGRAPHER-OUTSIDE"
            }), photographerSession.SelectState());
        var photographerSourceLive = homeActive && !photographerFound
            && bridge.IsInteractionAvailable(photographerSourceId);
        var photographerVerifyLive = homeActive && !photographerVerified
            && bridge.IsInteractionAvailable(photographerVerifyId);

        // Both completed first-world results reuse the first return's authored
        // talk target; JournalUi owns the shared saved-state predicates.
        var forestHomeResultLive = homeActive && JournalUi.ForestHomeResultPending(bridge);
        var waterHomeResultLive = homeActive && JournalUi.WaterHomeResultPending(bridge);
        var homeReturnLive = homeActive && bookReceived
            && (!firstHomeTalk || forestHomeResultLive || waterHomeResultLive)
            && bridge.IsInteractionAvailable(HomeReturnTarget.InteractionId);
        var housePermissionLive = bookReceived && firstHomeTalk && !homeReturnLive && housePrintFound && !houseAcquired
            && bridge.IsInteractionAvailable(HousePhotoPermissionTarget.InteractionId);
        var talkLive = bookReceived && !told && !homeReturnLive && !housePermissionLive;
        var permissionLive = bookReceived && told && found && !acquired && !homeReturnLive && !housePermissionLive;
        var sourceLive = bookReceived && !found;
        // Unfinished visits begin at the real bank. Only the completed world
        // keeps its home convenience; acquisition and permission stay here.
        var waterCompleted = bridge.SessionIdentity is { } session
            && PhotoWorldState.IsWorldCompleted(session.SelectState(), "W02");
        var riverLive = told && mountedAndRead && waterCompleted;
        // The family file question only exists once the file was found on the
        // PC and the family copy is not saved yet.
        var filePermissionLive = fileFound && !commonAcquired && !homeReturnLive && !housePermissionLive;
        var houseSourceLive = bookReceived && firstHomeTalk && !housePrintFound
            && bridge.IsInteractionAvailable(HousePhotoSourceTarget.InteractionId);

        var otherHomeWindowLive = homeReturnLive || housePermissionLive || talkLive
            || permissionLive || filePermissionLive;
        // W03 may precede W02. Its album questions must not hide an unfinished
        // main-route conversation at the same NPC anchor.
        var photographerPermissionLive = homeActive && !otherHomeWindowLive
            && photographerFound && !photographerAcquired
            && bridge.IsInteractionAvailable(photographerPermissionId);
        var photographerAuthorLive = homeActive && !otherHomeWindowLive
            && !bridge.PhotoWorldFactObserved("golsina_author_conversation")
            && bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-gulsina-author-talk");
        var photographerWindowLive = photographerSourceLive || photographerPermissionLive
            || photographerAuthorLive || photographerVerifyLive;
        var cellarHomeActive = homeActive && !photographerWindowLive;
        var familyBoxSourceLive = cellarHomeActive && bookReceived && !familyBoxOpened
            && FamilyBoxSourceTarget is not null
            && bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-family-box-source");
        var familyBoxPermissionLive = cellarHomeActive && bookReceived && familyBoxOpened
            && !handAcquired && !otherHomeWindowLive
            && FamilyBoxPermissionTarget is not null
            && bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-family-box-permission");
        var cellarPhotoSourceLive = cellarHomeActive && bookReceived && handAcquired && !cellarFound
            && CellarPhotoSourceTarget is { } cellarSource
            && bridge.IsInteractionAvailable(cellarSource.InteractionId);
        var cellarPhotoPermissionLive = cellarHomeActive && bookReceived && cellarFound && !cellarAcquired
            && !otherHomeWindowLive
            && CellarPhotoPermissionTarget is { } cellarPermission
            && bridge.IsInteractionAvailable(cellarPermission.InteractionId);

        HomeReturnTarget.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-first-world-home-talk");
        HousePhotoSourceTarget.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-house-photo-source");
        HousePhotoPermissionTarget.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-house-photo-permission");
        if (FamilyBoxSourceTarget is { } familySourceTarget)
        {
            var interactionId = photographerSourceLive ? photographerSourceId
                : photographerVerifyLive ? photographerVerifyId
                : "urman.fullgame:interaction/pw-family-box-source";
            var labelId = photographerSourceLive ? "pw-label-photographer-photo-source"
                : photographerVerifyLive ? "pw-label-verify-photographer-evidence"
                : "pw-label-family-box-source";
            familySourceTarget.SetAuthoredBinding(interactionId,
                bridge.ResolveText("urman.fullgame:text/" + labelId), string.Empty);
            familySourceTarget.SetPresentationEnabled(familyBoxSourceLive || photographerSourceLive || photographerVerifyLive);
        }
        if (FamilyBoxPermissionTarget is { } familyPermissionTarget)
        {
            familyPermissionTarget.SetAuthoredBinding(photographerPermissionLive ? photographerPermissionId
                    : "urman.fullgame:interaction/pw-family-box-permission",
                bridge.ResolveText(photographerPermissionLive ? "urman.fullgame:text/pw-label-photographer-photo-permission"
                    : "urman.fullgame:text/pw-label-family-box-permission"), string.Empty);
            familyPermissionTarget.SetPresentationEnabled(familyBoxPermissionLive || photographerPermissionLive);
        }
        if (CellarPhotoSourceTarget is { } cellarSourceTarget)
        {
            cellarSourceTarget.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-cellar-photo-source");
            cellarSourceTarget.SetPresentationEnabled(cellarPhotoSourceLive);
        }
        if (CellarPhotoPermissionTarget is { } cellarPermissionTarget)
        {
            cellarPermissionTarget.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-cellar-photo-permission");
            cellarPermissionTarget.SetPresentationEnabled(cellarPhotoPermissionLive);
        }
        HomeReturnTarget.SetPresentationEnabled(homeReturnLive);
        TalkTarget.SetPresentationEnabled(talkLive);
        PermissionTarget.SetPresentationEnabled(permissionLive);
        CommonFilePermissionTarget.SetPresentationEnabled(filePermissionLive);
        SourceTarget.SetPresentationEnabled(sourceLive);
        RiverEntryTarget.SetPresentationEnabled(riverLive);
        if (_forestPhotoEntryTarget is null || !GodotObject.IsInstanceValid(_forestPhotoEntryTarget))
            _forestPhotoEntryTarget = SourceTarget.GetParent()?.GetNodeOrNull<InteractionTarget>("PhotoWorldForestEntryTarget");
        _forestPhotoEntryTarget?.SetPresentationEnabled(bridge.SessionIdentity is { } forestSession
            && PhotoWorldState.IsWorldCompleted(forestSession.SelectState(), "W01"));
        if (_housePhotoEntryTarget is null || !GodotObject.IsInstanceValid(_housePhotoEntryTarget))
            _housePhotoEntryTarget = SourceTarget.GetParent()?.GetNodeOrNull<InteractionTarget>("HousePhotoEntryTarget");
        _housePhotoEntryTarget?.SetPresentationEnabled(housePhoto is { Mounted: true, WorldCompleted: true });
        HousePhotoSourceTarget.SetPresentationEnabled(houseSourceLive);
        HousePhotoPermissionTarget.SetPresentationEnabled(housePermissionLive);
        // әби keeps her greeting whenever the chain is not asking a question.
        var teaLive = !bookReceived || (!homeReturnLive && !talkLive
            && !permissionLive && !filePermissionLive && !housePermissionLive
            && !familyBoxPermissionLive && !cellarPhotoPermissionLive);
        TeaTarget?.SetPresentationEnabled(photographerAuthorLive || (!photographerWindowLive && teaLive));

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

        if (FamilyBoxLid is { } familyLid)
        {
            var goal = familyBoxOpened || photographerFound ? -105f : 0f;
            if (!_appliedOnce || Mathf.IsEqualApprox(familyLid.RotationDegrees.X, goal))
                familyLid.RotationDegrees = new Vector3(goal, familyLid.RotationDegrees.Y, familyLid.RotationDegrees.Z);
            else
            {
                var tween = CreateTween();
                tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(familyLid, "rotation_degrees:x", goal, .7f);
            }
        }

        if (HandPhotoCard is { } handCard)
            handCard.Visible = cellarHomeActive && handAcquired;

        if (CellarPhotoCard is { } cellarCard)
        {
            cellarCard.Visible = cellarHomeActive && handAcquired;
            var goal = cellarFound || cellarAcquired ? CellarPhotoOutLocalZ : CellarPhotoInLocalZ;
            if (!_appliedOnce || Mathf.IsEqualApprox(cellarCard.Position.Z, goal))
                cellarCard.Position = new Vector3(cellarCard.Position.X, cellarCard.Position.Y, goal);
            else
            {
                var tween = CreateTween();
                tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(cellarCard, "position:z", goal, .9f);
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
