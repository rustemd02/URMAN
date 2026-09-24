using Godot;

namespace Urman.Godot;

/// <summary>An XP Luna window caption: glossy blue when active, washed out when not.</summary>
public partial class OldPcWindowTitleBar : HBoxContainer
{
    private bool _active;
    public bool Active { get => _active; set { _active = value; QueueRedraw(); } }
    public bool HighContrast { get; set; }
    public override void _Ready() => Resized += QueueRedraw;
    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        if (HighContrast)
        {
            DrawRect(rect, new Color(_active ? "1d3f74" : "2b3140"));
            return;
        }
        OldPcXp.LunaBar(this, rect, _active);
    }
}
