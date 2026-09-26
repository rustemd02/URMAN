using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    public TamaraFenceQuest? TamaraFence { get; private set; }

    /// <summary>
    /// Built after the shared street frontages: Tamara Gennadievna's plot is
    /// excluded from them and owns its breakable fence on the road shoulder.
    /// </summary>
    private void BuildTamaraFenceQuest()
    {
        if (TamaraFence is not null) return;
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var village = _zoneInstances["village_day"];
        TamaraFence = TamaraFenceQuest.Build(this, core, village);
    }
}
