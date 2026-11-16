using Godot;
using System.Threading.Tasks;

namespace Urman.Godot;

/// <summary>Projects the arriving player's book onto the existing table, then lets the book UI own it.</summary>
internal partial class FamilyBookTablePresence : Node
{
    private Node3D? _book;
    private Node3D? _hands;
    private RuntimeBridge? _bridge;
    private FirstPersonController? _player;
    private object? _session;
    private Transform3D _tablePose;
    private bool _carryVisible;
    private bool _placing;
    private bool _placed;
    private bool _lastBookReceived;
    private bool _handoffAwaitingPlacement;
    private string? _zone;

    public override void _Ready()
    {
        _book = GetParentOrNull<Node3D>();
        if (_book is not null)
        {
            _tablePose = _book.Transform;
            _book.Visible = false;
        }
        TryBindBridge();
    }

    public override void _Process(double delta)
    {
        TryBindBridge();
        if (_bridge is null || _book is not { } book || !GodotObject.IsInstanceValid(book)) return;
        if (_zone != _bridge.CurrentZoneId) ApplyVisibility();
        if (_placing || !_carryVisible) return;
        if (ArrivalCamera() is not { } camera) { book.Visible = false; return; }
        book.Visible = true;
        book.GlobalTransform = camera.GlobalTransform * new Transform3D(
            new Basis(Vector3.Right, Mathf.DegToRad(35f)), new Vector3(.18f, -.40f, -.72f));
    }

    private void TryBindBridge()
    {
        if (_bridge is not null || !IsInsideTree()
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge) return;
        _bridge = bridge;
        bridge.RuntimeStateChanged += ApplyVisibility;
        ApplyVisibility();
    }

    private Camera3D? ArrivalCamera()
    {
        if (_player is null || !GodotObject.IsInstanceValid(_player))
            _player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        return _player?.GetNodeOrNull<Camera3D>("Head/Camera3D") is { Current: true } camera ? camera : null;
    }

    private void ApplyVisibility()
    {
        if (!IsInsideTree() || _book is not { } book || !GodotObject.IsInstanceValid(book)
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)) return;
        if (!ReferenceEquals(_session, bridge.SessionIdentity))
        {
            _session = bridge.SessionIdentity;
            _placed = false;
            _handoffAwaitingPlacement = false;
            _lastBookReceived = bridge.CanInspectReceivedPhotoBook;
        }
        var received = bridge.CanInspectReceivedPhotoBook;
        if (!_lastBookReceived && received && _carryVisible && !_placed) _handoffAwaitingPlacement = true;
        _lastBookReceived = received;
        _zone = bridge.CurrentZoneId;
        _carryVisible = !_placed && bridge.ActiveSceneId == "urman.fullgame:scene/pw-home"
            && _zone == "house_old_pc"
            && (bridge.CanShowFamilyBookOnTable || _handoffAwaitingPlacement);
        book.Visible = _placing || _carryVisible;
        if (_carryVisible && _hands is null)
        {
            _hands = PhotoBookHands.Create();
            book.AddChild(_hands);
        }
        if (_hands is { } hands) hands.Visible = _carryVisible || _placing;
        if (!_carryVisible && !_placing) book.Transform = _tablePose;
    }

    internal async Task<bool> PlaceForArrivalAsync(RuntimeBridge bridge)
    {
        if (_placing) return false;
        if (!_carryVisible || _placed) return true;
        if (_book is not { } book || !GodotObject.IsInstanceValid(book)
            || bridge.SessionIdentity is not { } session || ArrivalCamera() is null) return false;
        if (_player?.ModalOpen == true)
        {
            _placed = true;
            _handoffAwaitingPlacement = false;
            ApplyVisibility();
            return true;
        }
        var tree = GetTree();
        var start = book.GlobalTransform;
        var destination = book.GetParent<Node3D>().GlobalTransform * _tablePose;
        _placing = true;
        var elapsed = 0.0;
        while (elapsed < .65)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
                || !GodotObject.IsInstanceValid(book) || !GodotObject.IsInstanceValid(bridge)
                || !ReferenceEquals(bridge.SessionIdentity, session)
                || bridge.IsPhotoBookProjectionDeferred || bridge.CurrentZoneId != "house_old_pc"
                || bridge.ActiveSceneId != "urman.fullgame:scene/pw-home")
            {
                _placing = false;
                _placed = true;
                _handoffAwaitingPlacement = false;
                if (GodotObject.IsInstanceValid(this) && IsInsideTree()) ApplyVisibility();
                return false;
            }
            if (tree.Paused) continue;
            elapsed += GetProcessDeltaTime();
            var progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp((float)(elapsed / .5), 0f, 1f));
            book.GlobalTransform = start.InterpolateWith(destination, progress);
            if (_hands is { } hands) hands.Visible = elapsed < .5;
        }
        _placing = false;
        _placed = true;
        _handoffAwaitingPlacement = false;
        ApplyVisibility();
        return true;
    }

    public override void _ExitTree()
    {
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= ApplyVisibility;
        _bridge = null;
        _book = null;
    }
}
