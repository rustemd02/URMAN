using Godot;

namespace Urman.Godot;

/// <summary>Presentation-only visibility binding for the PhotoWorlds table book.</summary>
internal partial class FamilyBookTablePresence : Node
{
    private Node3D? _book;
    private RuntimeBridge? _bridge;

    public override void _Ready()
    {
        _book = GetParentOrNull<Node3D>();
        if (_book is not null) _book.Visible = false;
        SetProcess(true);
        TryBindBridge();
    }

    public override void _Process(double delta) => TryBindBridge();

    private void TryBindBridge()
    {
        if (_bridge is not null)
        {
            SetProcess(false);
            return;
        }
        if (!IsInsideTree()
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge)
            return;

        _bridge = bridge;
        bridge.RuntimeStateChanged += ApplyVisibility;
        SetProcess(false);
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (!IsInsideTree()
            || _book is not { } book || !GodotObject.IsInstanceValid(book)
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge))
            return;

        book.Visible = bridge.CanShowFamilyBookOnTable;
    }

    public override void _ExitTree()
    {
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= ApplyVisibility;
        _bridge = null;
        _book = null;
    }
}
