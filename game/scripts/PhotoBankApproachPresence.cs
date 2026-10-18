using Godot;

namespace Urman.Godot;

/// <summary>
/// Projects the committed bank-route interactions onto their real village
/// witnesses. Narrative facts and evidence remain owned by RuntimeBridge.
/// </summary>
public partial class PhotoBankApproachPresence : Node3D
{
    private const string HomeSceneId = "urman.fullgame:scene/pw-home";
    private const string BankSceneId = "urman.fullgame:scene/pw-bank-approach";
    private const string EnterBankInteractionId = "urman.fullgame:interaction/pw-enter-bank-approach";
    private const string WinterPilesInteractionId = "urman.fullgame:interaction/pw-winter-piles-inspected";
    private const string VerifyBankInteractionId = "urman.fullgame:interaction/pw-verify-bank-evidence";
    private const string BankReturnInteractionId = "urman.fullgame:interaction/pw-bank-return";
    private const string LeafWitnessInteractionId = "urman.fullgame:interaction/pw-witness-summer-leaf";
    private const string AlsuLeafInteractionId = "urman.fullgame:interaction/pw-alsu-leaf-talk";
    private const string WinterPilesFactId = "winter_piles_inspected";
    private const string LeafWitnessFactId = "outside_leaf_witnessed";
    private const string AlsuLeafDialogueId = "urman.fullgame:dialogue/pw-alsu-leaf";

    private RuntimeBridge? _bridge;
    private Node3D? _bankApproach;
    private Node3D? _bankEntryWitness;
    private Node3D? _oldPilesWitness;
    private Node3D? _bankReturnWitness;
    private Node3D? _leafWitness;
    private InteractionTarget? _winterPilesTarget;
    private InteractionTarget? _verifyBankTarget;
    private InteractionTarget? _bankReturnTarget;
    private InteractionTarget? _leafWitnessTarget;
    private InteractionTarget? _alsuLeafTarget;
    private InteractionTarget? _enterBankTarget;
    private InteractionTarget? _legacyAlsuTarget;
    private Node3D? _alsuActor;
    private bool _legacyAlsuSuppressed;
    private bool _bankCollisionsEnabled;
    private bool _markersChecked;
    private bool _warnedMissingMarkers;

    public override void _Ready() => SetProcess(true);

    public override void _Process(double delta)
    {
        if (!TryBindBridge() || !EnsureBankTargets())
        {
            DeactivateBankPresentation();
            return;
        }
        ApplyProjection();
    }

    private bool TryBindBridge()
    {
        if (_bridge is { } current && GodotObject.IsInstanceValid(current)) return true;
        _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        return _bridge is not null;
    }

    private bool EnsureBankTargets()
    {
        if (_bankApproach is null || !GodotObject.IsInstanceValid(_bankApproach))
            _bankApproach = GetParent() as Node3D;
        if (_bankApproach is null) return false;

        _oldPilesWitness ??= _bankApproach.GetNodeOrNull<Node3D>("OldPilesWitness");
        _bankReturnWitness ??= _bankApproach.GetNodeOrNull<Node3D>("BankReturnWitness");
        _leafWitness ??= _bankApproach.GetNodeOrNull<Node3D>("LeafWitness");
        var bankEntry = _bankApproach.GetNodeOrNull<Node3D>("BankEntry");
        _bankEntryWitness ??= bankEntry;
        if ((!_markersChecked && (_bankEntryWitness is null || _oldPilesWitness is null || _bankReturnWitness is null
                || _leafWitness is null || bankEntry is null)) && !_warnedMissingMarkers)
        {
            GD.PushWarning("PhotoBankApproachPresence is waiting for BankEntry, OldPilesWitness, BankReturnWitness, and LeafWitness markers.");
            _warnedMissingMarkers = true;
        }
        _markersChecked = _bankEntryWitness is not null && _oldPilesWitness is not null && _bankReturnWitness is not null
            && _leafWitness is not null && bankEntry is not null;

        if (_enterBankTarget is null && _bankEntryWitness is { } entry)
            _enterBankTarget = CreateTarget("EnterBankApproachTarget", EnterBankInteractionId,
                "urman.fullgame:text/pw-label-enter-bank-approach", entry, .58f, .82f);
        if (_winterPilesTarget is null && _oldPilesWitness is { } piles)
            _winterPilesTarget = CreateTarget("WinterPilesTarget", WinterPilesInteractionId,
                "urman.fullgame:text/pw-label-winter-piles-inspected", piles, .52f, .30f);
        if (_verifyBankTarget is null && _oldPilesWitness is { } verifyPiles)
            _verifyBankTarget = CreateTarget("VerifyBankEvidenceTarget", VerifyBankInteractionId,
                "urman.fullgame:text/pw-label-verify-bank-evidence", verifyPiles, .52f, .30f);
        if (_bankReturnTarget is null && _bankReturnWitness is { } bankReturn)
            _bankReturnTarget = CreateTarget("BankReturnTarget", BankReturnInteractionId,
                "urman.fullgame:text/pw-label-bank-return", bankReturn, .55f, .82f);
        if (_leafWitnessTarget is null && _leafWitness is { } leaf)
            _leafWitnessTarget = CreateTarget("SummerLeafWitnessTarget", LeafWitnessInteractionId,
                "urman.fullgame:text/pw-label-witness-summer-leaf", leaf, .40f, .02f);

        EnsureAlsuTarget();
        return _enterBankTarget is not null && _winterPilesTarget is not null && _verifyBankTarget is not null
            && _bankReturnTarget is not null && _leafWitnessTarget is not null;
    }

    private void EnsureAlsuTarget()
    {
        var world = GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
        var village = world?.GetZoneInstance("village_day");
        var actor = village?.GetNodeOrNull<Node3D>("Act1NpcPresentation/Npc_alsu");
        var legacyTarget = village?.GetNodeOrNull<InteractionTarget>("AlsuNpc");
        if (actor is null || legacyTarget is null)
        {
            RestoreLegacyAlsuTarget();
            _alsuActor = null;
            _legacyAlsuTarget = null;
            return;
        }

        if (!ReferenceEquals(actor, _alsuActor) || !ReferenceEquals(legacyTarget, _legacyAlsuTarget))
        {
            RestoreLegacyAlsuTarget();
            _alsuActor = actor;
            _legacyAlsuTarget = legacyTarget;
        }

        if (_alsuLeafTarget is null && _bankReturnWitness is { } bankReturn)
            _alsuLeafTarget = CreateTarget("AlsuLeafTalkTarget", AlsuLeafInteractionId,
                "urman.fullgame:text/pw-label-alsu-leaf-talk", bankReturn, .52f, .82f,
                AlsuLeafDialogueId);
    }

    private InteractionTarget? CreateTarget(
        string name,
        string interactionId,
        string labelTextId,
        Node3D? witness,
        float radius,
        float verticalOffset,
        string dialogueId = "")
    {
        if (witness is null || !GodotObject.IsInstanceValid(witness)) return null;
        var target = new InteractionTarget
        {
            Name = name,
            InteractionId = interactionId,
            Prompt = string.Empty,
            DialogueId = dialogueId,
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
        target.GlobalPosition = witness.GlobalPosition + Vector3.Up * verticalOffset;
        target.SetPresentationEnabled(false);
        return target;
    }

    private void ApplyProjection()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;

        var route = bridge.CurrentFullGamePhotoWorldRouteProjection();
        var session = bridge.SessionIdentity;
        var connectedWorld = GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
        var villagePhotoWorldRouteActive = bridge.IsPhotoWorldsCampaign
            && string.Equals(bridge.CurrentZoneId, "village_day", System.StringComparison.Ordinal)
            && route is not null && session is not null
            && ReferenceEquals(route.SessionIdentity, session)
            && string.Equals(bridge.ActiveSceneId, route.ActiveSceneId, System.StringComparison.Ordinal)
            && route.ActiveSceneId is HomeSceneId or BankSceneId;
        var homeRouteActive = villagePhotoWorldRouteActive
            && string.Equals(route!.ActiveSceneId, HomeSceneId, System.StringComparison.Ordinal);
        var bankRouteActive = villagePhotoWorldRouteActive
            && string.Equals(route!.ActiveSceneId, BankSceneId, System.StringComparison.Ordinal);
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var interactionWindowOpen = player is not null
            && !player.ModalOpen && !player.VehicleControlled
            && !MainMenuUi.AnyUndismissed(GetTree());

        if (_bankApproach is { } bankApproach && GodotObject.IsInstanceValid(bankApproach))
            bankApproach.Visible = villagePhotoWorldRouteActive;
        SetBankCollisionLayers(villagePhotoWorldRouteActive);

        SetTarget(_enterBankTarget, bridge, "urman.fullgame:text/pw-label-enter-bank-approach",
            homeRouteActive && interactionWindowOpen && IsInteractionReady(bridge, EnterBankInteractionId));

        SetTarget(_winterPilesTarget, bridge, "urman.fullgame:text/pw-label-winter-piles-inspected",
            bankRouteActive && interactionWindowOpen && !bridge.PhotoWorldFactObserved(WinterPilesFactId)
                && IsInteractionReady(bridge, WinterPilesInteractionId));
        SetTarget(_verifyBankTarget, bridge, "urman.fullgame:text/pw-label-verify-bank-evidence",
            bankRouteActive && interactionWindowOpen && IsInteractionReady(bridge, VerifyBankInteractionId));
        SetTarget(_bankReturnTarget, bridge, "urman.fullgame:text/pw-label-bank-return",
            bankRouteActive && interactionWindowOpen && IsInteractionReady(bridge, BankReturnInteractionId));
        SetTarget(_leafWitnessTarget, bridge, "urman.fullgame:text/pw-label-witness-summer-leaf",
            bankRouteActive && interactionWindowOpen && !bridge.PhotoWorldFactObserved(LeafWitnessFactId)
                && IsInteractionReady(bridge, LeafWitnessInteractionId));

        var alsuReady = _alsuActor is { } actor && GodotObject.IsInstanceValid(actor)
            && actor.IsVisibleInTree()
            && actor.GetNodeOrNull<AlsuStreetWalkPresentation>("AlsuStreetWalk") is { PhysicalAccessReady: true };
        var alsuTalkReady = bankRouteActive && interactionWindowOpen && alsuReady
            && IsInteractionReady(bridge, AlsuLeafInteractionId);
        if (_alsuLeafTarget is { } alsuTarget && GodotObject.IsInstanceValid(alsuTarget)
            && _alsuActor is { } movingActor && GodotObject.IsInstanceValid(movingActor))
        {
            alsuTarget.GlobalPosition = movingActor.GlobalPosition + Vector3.Up * .84f;
            SetTarget(alsuTarget, bridge, "urman.fullgame:text/pw-label-alsu-leaf-talk", alsuTalkReady);
        }

        if (bankRouteActive)
            SuppressLegacyAlsuTarget();
        else if (connectedWorld?.ActiveZoneId == "village_day")
            RestoreLegacyAlsuTarget();
    }

    private static bool IsInteractionReady(RuntimeBridge bridge, string interactionId) =>
        bridge.IsInteractionAvailable(interactionId) && bridge.CanPhysicallyUseInteraction(interactionId);

    private void SetBankCollisionLayers(bool enabled)
    {
        if (!IsInsideTree() || _bankCollisionsEnabled == enabled) return;
        foreach (var node in GetTree().GetNodesInGroup("photo_bank_approach_collision"))
        {
            if (node is CollisionObject3D collision && GodotObject.IsInstanceValid(collision)
                && _bankApproach is { } owner && GodotObject.IsInstanceValid(owner)
                && owner.IsAncestorOf(collision))
                collision.CollisionLayer = enabled ? 1u : 0u;
        }

        _bankCollisionsEnabled = enabled;
    }

    private void DeactivateBankPresentation()
    {
        if (_bankApproach is { } bankApproach && GodotObject.IsInstanceValid(bankApproach))
            bankApproach.Visible = false;
        SetBankCollisionLayers(false);
        foreach (var target in new[]
        {
            _enterBankTarget, _winterPilesTarget, _verifyBankTarget,
            _bankReturnTarget, _leafWitnessTarget, _alsuLeafTarget
        })
        {
            if (target is { } current && GodotObject.IsInstanceValid(current) && !current.IsQueuedForDeletion())
                current.SetPresentationEnabled(false);
        }
        RestoreLegacyAlsuTarget();
    }

    private static void SetTarget(InteractionTarget? target, RuntimeBridge bridge, string labelTextId, bool enabled)
    {
        if (target is null || !GodotObject.IsInstanceValid(target) || target.IsQueuedForDeletion()) return;
        target.Prompt = bridge.ResolveText(labelTextId);
        target.SetPresentationEnabled(enabled);
    }

    private void SuppressLegacyAlsuTarget()
    {
        if (_legacyAlsuTarget is not { } target || !GodotObject.IsInstanceValid(target)
            || target.IsQueuedForDeletion()) return;
        target.SetPresentationEnabled(false);
        _legacyAlsuSuppressed = true;
    }

    private void RestoreLegacyAlsuTarget()
    {
        if (_legacyAlsuSuppressed && _legacyAlsuTarget is { } target
            && GodotObject.IsInstanceValid(target) && !target.IsQueuedForDeletion())
            target.SetPresentationEnabled(true);
        _legacyAlsuSuppressed = false;
    }

    public override void _ExitTree()
    {
        DeactivateBankPresentation();
        _bridge = null;
    }
}
