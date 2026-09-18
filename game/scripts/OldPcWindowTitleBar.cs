using Godot;

namespace Urman.Godot;

public partial class OldPcWindowTitleBar : HBoxContainer
{
    private bool _active;
    public bool Active { get => _active; set { _active = value; QueueRedraw(); } }
    public override void _Ready() => Resized += QueueRedraw;
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(_active ? "235fa0" : "69869f"));
        DrawLine(Vector2.Zero, new(Size.X, 0), new Color(_active ? "7cafd7" : "a4b7c6"), 2);
    }
}

