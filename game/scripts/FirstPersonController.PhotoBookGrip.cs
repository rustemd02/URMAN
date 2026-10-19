using Godot;

namespace Urman.Godot;

public partial class FirstPersonController
{
    private const string PhotoBookGripBookPath = "res://assets/models/photoworlds/family-book-v1.glb";
    private const string PhotoBookGripHandsPath = "res://assets/models/photoworlds/book-hands-v1.glb";

    private Node3D? _photoBookGripAnchor;
    private object? _photoBookGripSession;
    private bool _photoBookGripRequested;

    internal bool HasPhotoBookGrip => _photoBookGripRequested;

    // Called by the album before its ordinary close completes, so invalid/missing
    // models can refuse without dismissing the journal. It creates and frees only
    // temporary imported instances; no player state or scene is changed.
    internal bool CanPreparePhotoBookGrip()
    {
        if (!CanRequestPhotoBookGrip(allowJournalModal: true)
            || !TryCreatePhotoBookGripModels(out var book, out var hands)) return false;
        book.Free();
        hands.Free();
        return true;
    }

    internal bool TrySetPhotoBookGrip(bool requested, object? expectedSession = null)
    {
        if (!requested)
        {
            ClearPhotoBookGrip();
            return true;
        }

        if (!CanRequestPhotoBookGrip(allowJournalModal: false)
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge
            || bridge.SessionIdentity is not { } session
            || expectedSession is not null && !ReferenceEquals(expectedSession, session)) return false;

        if (_photoBookGripRequested && _photoBookGripAnchor is { } current
            && GodotObject.IsInstanceValid(current)
            && ReferenceEquals(session, _photoBookGripSession)) return true;

        ClearPhotoBookGrip();
        if (!TryCreatePhotoBookGripModels(out var book, out var hands)) return false;

        // The inspection camera looks from (-.10,.38,.26) to (-.10,.015,0):
        // about .46 m away and 54.6° down. Preserve its world-upright book pose,
        // then move it .10 m lower and .05 m toward the player for a lower view.
        var anchor = new Node3D
        {
            Name = "RequestedClosedPhotoBookGrip",
            Position = new Vector3(0, -.11f, -.41f),
            Rotation = new Vector3(Mathf.Atan2(.365f, .26f), 0, 0)
        };
        anchor.AddChild(book);
        anchor.AddChild(hands);
        _camera.AddChild(anchor);
        ApplyPhotoBookGripStaticPose(book, "close");
        ApplyPhotoBookGripStaticPose(hands, "hold_closed");
        _photoBookGripAnchor = anchor;
        _photoBookGripSession = session;
        _photoBookGripRequested = true;
        RefreshPhotoBookGripPresentation(bridge);
        return true;
    }

    private bool CanRequestPhotoBookGrip(bool allowJournalModal)
    {
        if (_sessionTransition || (!allowJournalModal && ModalOpen) || VehicleControlled
            || IsClimbingLadder || _camera is null || !GodotObject.IsInstanceValid(_camera)) return false;
        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge
            || !bridge.CanInspectReceivedPhotoBook || bridge.SessionIdentity is null) return false;

        RefreshCarryCoordinator();
        return _carryCoordinator is not { HeldItem: not null }
            && _carryCoordinator is not { ActionInProgress: true };
    }

    private static bool TryCreatePhotoBookGripModels(out Node3D book, out Node3D hands)
    {
        book = null!;
        hands = null!;
        if (!ResourceLoader.Exists(PhotoBookGripBookPath) || !ResourceLoader.Exists(PhotoBookGripHandsPath)) return false;
        var bookScene = ResourceLoader.Load<PackedScene>(PhotoBookGripBookPath);
        var handsScene = ResourceLoader.Load<PackedScene>(PhotoBookGripHandsPath);
        if (bookScene is null || handsScene is null) return false;

        var bookNode = bookScene.Instantiate();
        if (bookNode is not Node3D bookModel)
        {
            bookNode?.Free();
            return false;
        }
        var handsNode = handsScene.Instantiate();
        if (handsNode is not Node3D handsModel)
        {
            bookModel.Free();
            handsNode?.Free();
            return false;
        }

        if (!PreparePhotoBookGripAnimation(bookModel, "close")
            || !PreparePhotoBookGripAnimation(handsModel, "hold_closed")
            || ContainsPhotoBookGripCollider(bookModel)
            || ContainsPhotoBookGripCollider(handsModel)
            || !FindPhotoBookGripGeometry(bookModel).Any()
            || !FindPhotoBookGripGeometry(handsModel).Any())
        {
            bookModel.Free();
            handsModel.Free();
            return false;
        }

        foreach (var geometry in FindPhotoBookGripGeometry(bookModel)
                     .Concat(FindPhotoBookGripGeometry(handsModel)))
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        book = bookModel;
        hands = handsModel;
        return true;
    }

    private static IEnumerable<GeometryInstance3D> FindPhotoBookGripGeometry(Node3D model) =>
        new[] { model }.OfType<GeometryInstance3D>()
            .Concat(model.FindChildren("*", nameof(GeometryInstance3D), true, false).OfType<GeometryInstance3D>());

    private static bool PreparePhotoBookGripAnimation(Node3D model, string clip)
    {
        var players = model.FindChildren("*", nameof(AnimationPlayer), true, false)
            .OfType<AnimationPlayer>().ToArray();
        if (!players.Any(player => player.HasAnimation(clip))) return false;
        foreach (var player in players)
        {
            player.Autoplay = "";
            player.Stop();
        }
        return true;
    }

    private static void ApplyPhotoBookGripStaticPose(Node3D model, string clip)
    {
        foreach (var player in model.FindChildren("*", nameof(AnimationPlayer), true, false)
                     .OfType<AnimationPlayer>())
        {
            if (!player.HasAnimation(clip)) continue;
            player.Play(clip);
            player.Seek(player.GetAnimation(clip).Length, update: true);
            player.Pause();
        }
    }

    private static bool ContainsPhotoBookGripCollider(Node3D model) => model is CollisionObject3D
        || model.FindChildren("*", nameof(CollisionObject3D), true, false).OfType<CollisionObject3D>().Any();

    private void RefreshPhotoBookGrip()
    {
        if (!_photoBookGripRequested) return;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (_sessionTransition || bridge is null || bridge.SessionIdentity is not { } session
            || !ReferenceEquals(_photoBookGripSession, session) || !bridge.CanInspectReceivedPhotoBook)
        {
            ClearPhotoBookGrip();
            return;
        }

        RefreshPhotoBookGripPresentation(bridge);
    }

    private void RefreshPhotoBookGripPresentation(RuntimeBridge bridge)
    {
        if (_photoBookGripAnchor is not { } anchor || !GodotObject.IsInstanceValid(anchor)) return;
        RefreshCarryCoordinator();
        anchor.Visible = !ModalOpen && !VehicleControlled && !IsClimbingLadder
            && _carryCoordinator is not { HeldItem: not null }
            && _carryCoordinator is not { ActionInProgress: true }
            && bridge.CanInspectReceivedPhotoBook
            && ReferenceEquals(_photoBookGripSession, bridge.SessionIdentity);
    }

    private void ClearPhotoBookGrip()
    {
        if (_photoBookGripAnchor is { } anchor && GodotObject.IsInstanceValid(anchor))
        {
            anchor.Visible = false;
            anchor.QueueFree();
        }
        _photoBookGripAnchor = null;
        _photoBookGripSession = null;
        _photoBookGripRequested = false;
    }
}
