using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>Projects the epilogue actions onto the occupied home and its actual door.</summary>
public partial class PhotoWorldEpiloguePresence : Node3D
{
    private const string HomeZoneId = "house_old_pc";
    private const string EpilogueSceneId = "urman.fullgame:scene/pw-epilogue";
    private const string TakeNowInteractionId = "urman.fullgame:interaction/pw-take-now-photo";
    private const string FamilyTalkInteractionId = "urman.fullgame:interaction/pw-home-epilogue-talk";
    private const string ReturnHomeInteractionId = "urman.fullgame:interaction/pw-epilogue-return-home";
    private const string TakeNowLabelId = "urman.fullgame:text/pw-label-take-now-photo";
    private const string FamilyTalkLabelId = "urman.fullgame:text/pw-label-home-epilogue-talk";
    private const string ReturnHomeLabelId = "urman.fullgame:text/pw-label-epilogue-return-home";
    private const string NowPhotoFrontPath = "res://assets/textures/photobook/s-now-front-v1.png";

    private RuntimeBridge? _bridge;
    private InteractionTarget? _takeNowTarget;
    private InteractionTarget? _familyTalkTarget;
    private InteractionTarget? _returnHomeTarget;
    private Node3D? _cameraStance;
    private MeshInstance3D? _table;
    private MeshInstance3D? _teaCup;
    private Node3D? _gulsina;
    private Node3D? _mansur;
    private InteractionTarget? _gulsinaBodyTarget;
    private InteractionTarget? _mansurBodyTarget;
    private InteractionTarget? _houseExit;
    private CollisionObject3D? _tableCollider;
    private bool _worldReferencesResolved;
    private bool _warnedMissingWorldObjects;

    internal static void Attach(StyleBenchmarkZone home)
    {
        home.AddChild(new PhotoWorldEpiloguePresence { Name = nameof(PhotoWorldEpiloguePresence) });
    }

    public override void _Ready() => SetProcess(true);

    public override void _Process(double delta)
    {
        if (!ResolveWorldReferences())
        {
            SetTargetsEnabled(false, false, false);
            return;
        }

        if (!TryBindBridge())
        {
            SetTargetsEnabled(false, false, false);
            return;
        }

        var bridge = _bridge!;
        var epilogueCurrent = IsEpilogueCurrent(bridge, out _);
        var interactionWindowOpen = IsInteractionWindowOpen();
        var hasNowPhoto = HasNowPhoto(bridge);

        ConfigureTarget(_takeNowTarget, bridge, TakeNowLabelId);
        ConfigureTarget(_familyTalkTarget, bridge, FamilyTalkLabelId);
        ConfigureTarget(_returnHomeTarget, bridge, ReturnHomeLabelId);

        var takeNowReady = epilogueCurrent && interactionWindowOpen && !hasNowPhoto
            && ResourceLoader.Exists(NowPhotoFrontPath)
            && IsInteractionReady(bridge, TakeNowInteractionId);
        if (_takeNowTarget is { } takeNow && GodotObject.IsInstanceValid(takeNow))
        {
            takeNow.PresentationGateHint = "Встаньте у стола и наведите камеру на близких и чай.";
            takeNow.SetPresentationEnabled(takeNowReady);
        }

        var familyTalkReady = epilogueCurrent && interactionWindowOpen && hasNowPhoto
            && IsInteractionReady(bridge, FamilyTalkInteractionId);
        var returnHomeReady = epilogueCurrent && interactionWindowOpen && hasNowPhoto
            && IsInteractionReady(bridge, ReturnHomeInteractionId);
        _familyTalkTarget?.SetPresentationEnabled(familyTalkReady);
        _returnHomeTarget?.SetPresentationEnabled(returnHomeReady);
    }

    private bool ResolveWorldReferences()
    {
        if (_worldReferencesResolved)
            return _table is { } table && GodotObject.IsInstanceValid(table)
                && _teaCup is { } teaCup && GodotObject.IsInstanceValid(teaCup)
                && _gulsina is { } gulsina && GodotObject.IsInstanceValid(gulsina)
                && _mansur is { } mansur && GodotObject.IsInstanceValid(mansur)
                && _houseExit is { } houseExit && GodotObject.IsInstanceValid(houseExit);

        if (GetParent() is not StyleBenchmarkZone home)
            return false;

        var furniture = home.GetNodeOrNull<Node3D>("GeneratedHouseInteriorAct1");
        _table = furniture?.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().FirstOrDefault(mesh => mesh.Name == "HouseInterior_TableTop_LOD0");
        _teaCup = home.GetNodeOrNull<MeshInstance3D>("TeaCup");
        _gulsina = home.FindChild("Npc_gulsina", true, false) as Node3D;
        _mansur = home.FindChild("Npc_mansur", true, false) as Node3D;
        _gulsinaBodyTarget = home.GetNodeOrNull<InteractionTarget>("GulsinaNpc");
        _mansurBodyTarget = home.GetNodeOrNull<InteractionTarget>("MansurNpc");
        _houseExit = home.GetNodeOrNull<InteractionTarget>("HouseExit");
        _tableCollider = home.GetNodeOrNull<CollisionObject3D>("HouseInteriorFurnitureCollision");

        if (_table is null || _table.Mesh is null || _teaCup is null || _gulsina is null || _mansur is null
            || _gulsinaBodyTarget is null || _mansurBodyTarget is null || _houseExit is null || _tableCollider is null)
        {
            if (!_warnedMissingWorldObjects)
            {
                GD.PushWarning("PhotoWorldEpiloguePresence is waiting for the existing family table, tea, actors, and house door.");
                _warnedMissingWorldObjects = true;
            }
            return false;
        }

        var vertices = _table.Mesh.GetFaces().Select(vertex => _table.ToGlobal(vertex)).ToArray();
        if (vertices.Length == 0)
            return false;

        var top = vertices.Max(vertex => vertex.Y);
        var topVertices = vertices.Where(vertex => Mathf.Abs(vertex.Y - top) <= .003f).ToArray();
        if (topVertices.Length == 0)
            return false;

        var minX = topVertices.Min(vertex => vertex.X);
        var maxX = topVertices.Max(vertex => vertex.X);
        var maxZ = topVertices.Max(vertex => vertex.Z);
        var tablePoint = new Vector3(minX + Mathf.Min(.50f, (maxX - minX) * .35f), top + .035f, maxZ - .08f);
        var stance = new Vector3(minX - 1.9f, home.GlobalPosition.Y, maxZ + .45f);

        _cameraStance = new Node3D { Name = "SNowCameraStance", Position = home.ToLocal(stance) };
        AddChild(_cameraStance);
        var takeNowTarget = CreateTarget("TakeNowPhotoTarget", TakeNowInteractionId, tablePoint, .11f);
        var familyTalkTarget = CreateTarget("HomeEpilogueTalkTarget", FamilyTalkInteractionId,
            _gulsinaBodyTarget.GlobalPosition, .24f, "urman.fullgame:dialogue/pw-home-epilogue");
        var returnHomeTarget = CreateTarget("EpilogueReturnHomeTarget", ReturnHomeInteractionId,
            _houseExit.GlobalPosition, .22f);
        _takeNowTarget = takeNowTarget;
        _familyTalkTarget = familyTalkTarget;
        _returnHomeTarget = returnHomeTarget;
        takeNowTarget.PresentationGate = CanFrameFamilyTablePhoto;
        takeNowTarget.PresentationGateHint = "Встаньте у стола и наведите камеру на близких и чай.";
        takeNowTarget.SetMeta("physicalSource", "existing family tea table surface; kernel action owns S-NOW");
        familyTalkTarget.SetMeta("physicalSource", "existing Gulsina at the family tea table");
        returnHomeTarget.SetMeta("physicalSource", "existing closed house door and threshold");
        _worldReferencesResolved = true;
        return true;
    }

    private InteractionTarget CreateTarget(string name, string interactionId, Vector3 globalPosition,
        float radius, string dialogueId = "")
    {
        var target = new InteractionTarget
        {
            Name = name,
            InteractionId = interactionId,
            DialogueId = dialogueId,
            Prompt = string.Empty,
            CollisionLayer = 4u,
            CollisionMask = 0u
        };
        target.ConfigureRayOnly();
        target.AddChild(new CollisionShape3D
        {
            Name = name + "Ray",
            Shape = new SphereShape3D { Radius = radius }
        });
        AddChild(target);
        target.GlobalPosition = globalPosition;
        target.SetPresentationEnabled(false);
        return target;
    }

    private bool CanFrameFamilyTablePhoto()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !IsEpilogueCurrent(bridge, out var session)
            || !ResourceLoader.Exists(NowPhotoFrontPath)
            || !IsInteractionReady(bridge, TakeNowInteractionId)
            || _cameraStance is not { } stance || !GodotObject.IsInstanceValid(stance)
            || _takeNowTarget is not { } target || !GodotObject.IsInstanceValid(target))
            return false;

        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player) || player.ModalOpen || player.VehicleControlled
            || player.IsClimbingLadder || !player.IsOnFloor()
            || player.GlobalPosition.DistanceTo(stance.GlobalPosition) > .70f
            || player.GetNodeOrNull<Camera3D>("Head/Camera3D") is not { Current: true } camera
            || player.GetNodeOrNull<RayCast3D>("Head/Camera3D/InteractionRay") is not { } interactionRay)
            return false;

        if (MainMenuUi.AnyUndismissed(GetTree()))
            return false;

        interactionRay.ForceRaycastUpdate();
        if (!ReferenceEquals(interactionRay.GetCollider(), target))
            return false;

        var tablePoint = target.GlobalPosition;
        var gulsinaPoint = _gulsina!.GlobalPosition + Vector3.Up * 1.35f;
        var mansurPoint = _mansur!.GlobalPosition + Vector3.Up * 1.35f;
        var teaPoint = _teaCup!.GlobalPosition + Vector3.Up * .04f;
        if (!camera.IsPositionInFrustum(tablePoint) || !camera.IsPositionInFrustum(teaPoint)
            || !camera.IsPositionInFrustum(gulsinaPoint) || !camera.IsPositionInFrustum(mansurPoint))
            return false;

        var exclusions = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        var space = GetWorld3D().DirectSpaceState;
        if (!WitnessFramedAndVisible(camera, tablePoint, _tableCollider, space, exclusions)
            || !WitnessFramedAndVisible(camera, teaPoint, _tableCollider, space, exclusions)
            || !WitnessFramedAndVisible(camera, gulsinaPoint, _gulsinaBodyTarget, space, exclusions)
            || !WitnessFramedAndVisible(camera, mansurPoint, _mansurBodyTarget, space, exclusions))
            return false;

        return ReferenceEquals(session, bridge.SessionIdentity);
    }

    private static bool WitnessFramedAndVisible(Camera3D camera, Vector3 point,
        CollisionObject3D? expectedCollider, PhysicsDirectSpaceState3D space,
        global::Godot.Collections.Array<Rid> exclusions)
    {
        using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, point, 3u, exclusions);
        using var hit = space.IntersectRay(ray);
        if (hit.Count == 0)
            return true;
        return expectedCollider is not null
            && hit["collider"].AsGodotObject() is CollisionObject3D actual
            && ReferenceEquals(actual, expectedCollider);
    }

    private bool TryBindBridge()
    {
        if (_bridge is { } current && GodotObject.IsInstanceValid(current))
            return true;
        _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        return _bridge is not null;
    }

    private static bool IsEpilogueCurrent(RuntimeBridge bridge, out object session)
    {
        session = null!;
        if (!GodotObject.IsInstanceValid(bridge) || !bridge.IsPhotoWorldsCampaign
            || !string.Equals(bridge.CurrentZoneId, HomeZoneId, StringComparison.Ordinal)
            || bridge.SessionIdentity is not { } currentSession
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { } route
            || !ReferenceEquals(route.SessionIdentity, currentSession)
            || !string.Equals(route.ActiveSceneId, EpilogueSceneId, StringComparison.Ordinal)
            || !string.Equals(bridge.ActiveSceneId, route.ActiveSceneId, StringComparison.Ordinal))
            return false;

        session = currentSession;
        return true;
    }

    private bool IsInteractionWindowOpen() => !MainMenuUi.AnyUndismissed(GetTree())
        && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController
            { ModalOpen: false, VehicleControlled: false, IsClimbingLadder: false };

    private static bool IsInteractionReady(RuntimeBridge bridge, string interactionId) =>
        bridge.IsInteractionAvailable(interactionId) && bridge.CanPhysicallyUseInteraction(interactionId);

    private static bool HasNowPhoto(RuntimeBridge bridge) => bridge.PhotoBookPhotos()
        .Any(photo => string.Equals(photo.PhotoId, "S-NOW", StringComparison.Ordinal));

    private static void ConfigureTarget(InteractionTarget? target, RuntimeBridge bridge, string labelId)
    {
        if (target is { } current && GodotObject.IsInstanceValid(current) && !current.IsQueuedForDeletion())
            current.Prompt = bridge.ResolveText(labelId);
    }

    private void SetTargetsEnabled(bool takeNow, bool familyTalk, bool returnHome)
    {
        _takeNowTarget?.SetPresentationEnabled(takeNow);
        _familyTalkTarget?.SetPresentationEnabled(familyTalk);
        _returnHomeTarget?.SetPresentationEnabled(returnHome);
    }

    public override void _ExitTree()
    {
        SetTargetsEnabled(false, false, false);
        _bridge = null;
    }
}
