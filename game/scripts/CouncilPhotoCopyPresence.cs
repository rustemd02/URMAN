using Godot;

namespace Urman.Godot;

/// <summary>Projects the authored S-BANK source and permission actions onto the existing council desk.</summary>
public partial class CouncilPhotoCopyPresence : Node3D
{
    private const string BankSceneId = "urman.fullgame:scene/pw-bank-approach";
    private const string SourceInteractionId = "urman.fullgame:interaction/pw-bank-copy-source";
    private const string PermissionInteractionId = "urman.fullgame:interaction/pw-bank-copy-permission";
    private const string SourceLabelId = "urman.fullgame:text/pw-label-bank-copy-source";
    private const string PermissionLabelId = "urman.fullgame:text/pw-label-bank-copy-permission";
    private const string BankCopyFoundFactId = "bank_copy_found";
    private const string FrontTexturePath = "res://assets/textures/photobook/s-bank-front-v1.png";

    private RuntimeBridge? _bridge;
    private Node3D _desk = null!;
    private Node3D _print = null!;
    private InteractionTarget _sourceTarget = null!;
    private InteractionTarget _permissionTarget = null!;

    internal Node3D Desk => _desk;
    internal InteractionTarget SourceTarget => _sourceTarget;
    internal InteractionTarget PermissionTarget => _permissionTarget;

    internal static CouncilPhotoCopyPresence Create(Node3D desk)
    {
        var presence = new CouncilPhotoCopyPresence { Name = "CouncilPhotoCopyPresence" };
        presence._desk = desk;
        presence.CreatePhysicalCopy();
        presence._sourceTarget = presence.CreateTarget(presence._print, "SBankCopySourceTarget",
            SourceInteractionId, new(0, .009f, 0));
        presence._permissionTarget = presence.CreateTarget(desk, "SBankCopyPermissionTarget",
            PermissionInteractionId, new(.49f, .806f, .02f));
        return presence;
    }

    public override void _Ready()
    {
        // Desk-owned targets may have entered the tree before this sibling.
        // Restore their configured ray layer after their own Ready callbacks.
        _sourceTarget.ConfigureRayOnly();
        _permissionTarget.ConfigureRayOnly();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!TryBindBridge() || !IsBankSceneCurrent())
        {
            _print.Visible = false;
            _sourceTarget.SetPresentationEnabled(false);
            _permissionTarget.SetPresentationEnabled(false);
            return;
        }

        _print.Visible = true;
        var bridge = _bridge!;
        _sourceTarget.Prompt = bridge.ResolveText(SourceLabelId);
        _permissionTarget.Prompt = bridge.ResolveText(PermissionLabelId);

        var interactionWindowOpen = IsInteractionWindowOpen();
        var copyFound = bridge.PhotoWorldFactObserved(BankCopyFoundFactId);
        SetTarget(_sourceTarget,
            interactionWindowOpen && !copyFound && IsInteractionReady(bridge, SourceInteractionId));
        SetTarget(_permissionTarget,
            interactionWindowOpen && copyFound && !HasAcquiredSBank(bridge)
                && IsInteractionReady(bridge, PermissionInteractionId));
    }

    private void CreatePhysicalCopy()
    {
        var texture = ResourceLoader.Load<Texture2D>(FrontTexturePath)
            ?? throw new System.InvalidOperationException("The published S-BANK front is missing: " + FrontTexturePath);
        _print = new Node3D { Name = "SBankAuthorizedCopy", Position = new(.26f, .764f, .02f), Visible = false };
        _print.SetMeta("photoId", "S-BANK");
        _print.SetMeta("assetSource", FrontTexturePath);
        _desk.AddChild(_print);

        var paper = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("e8dfc8"),
            Roughness = 1,
            MetallicSpecular = 0
        };
        _print.AddChild(new MeshInstance3D
        {
            Name = "PrintedCardBody",
            Mesh = new BoxMesh { Size = new(.15f, .003f, .10f) },
            MaterialOverride = paper
        });
        var front = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            AlbedoColor = Colors.White,
            Roughness = 1,
            MetallicSpecular = 0,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
        _print.AddChild(new MeshInstance3D
        {
            Name = "PrintedCardFront",
            Position = new(0, .0016f, 0),
            RotationDegrees = new(-90, 0, 0),
            Mesh = new QuadMesh { Size = new(.15f, .10f) },
            MaterialOverride = front
        });
    }

    private InteractionTarget CreateTarget(Node3D parent, string name, string interactionId, Vector3 at)
    {
        var target = new InteractionTarget
        {
            Name = name,
            InteractionId = interactionId,
            Position = at,
            Prompt = string.Empty,
            CollisionLayer = 4,
            CollisionMask = 0
        };
        target.ConfigureRayOnly();
        target.AddChild(new CollisionShape3D
        {
            Name = name + "Ray",
            Shape = new SphereShape3D { Radius = .075f }
        });
        target.PresentationGate = () => IsBankRouteCurrent()
            && _bridge is { } bridge
            && (interactionId == SourceInteractionId
                ? !bridge.PhotoWorldFactObserved(BankCopyFoundFactId)
                : bridge.PhotoWorldFactObserved(BankCopyFoundFactId) && !HasAcquiredSBank(bridge));
        target.SetMeta("photoId", "S-BANK");
        target.SetMeta("sourceInteractionId", SourceInteractionId);
        target.SetMeta("permissionInteractionId", PermissionInteractionId);
        target.SetMeta("runtimeStateOwner", "RuntimeBridge campaign action; no local state write");
        parent.AddChild(target);
        target.SetPresentationEnabled(false);
        return target;
    }

    private bool TryBindBridge()
    {
        if (_bridge is { } current && GodotObject.IsInstanceValid(current)) return true;
        _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        return _bridge is not null;
    }

    private bool IsBankRouteCurrent()
        => IsBankSceneCurrent() && IsInteractionWindowOpen();

    private bool IsBankSceneCurrent()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !bridge.IsPhotoWorldsCampaign || bridge.CurrentZoneId != "village_day")
            return false;

        var route = bridge.CurrentFullGamePhotoWorldRouteProjection();
        var session = bridge.SessionIdentity;
        return route is not null && session is not null
            && ReferenceEquals(route.SessionIdentity, session)
            && route.ActiveSceneId == BankSceneId
            && bridge.ActiveSceneId == route.ActiveSceneId;
    }

    private bool IsInteractionWindowOpen() => !MainMenuUi.AnyUndismissed(GetTree())
        && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: false, VehicleControlled: false };

    private static bool IsInteractionReady(RuntimeBridge bridge, string interactionId) =>
        bridge.IsInteractionAvailable(interactionId) && bridge.CanPhysicallyUseInteraction(interactionId);

    private static bool HasAcquiredSBank(RuntimeBridge bridge)
    {
        foreach (var photo in bridge.PhotoBookPhotos())
            if (photo.PhotoId == "S-BANK") return true;
        return false;
    }

    private static void SetTarget(InteractionTarget target, bool enabled) =>
        target.SetPresentationEnabled(enabled);
}
