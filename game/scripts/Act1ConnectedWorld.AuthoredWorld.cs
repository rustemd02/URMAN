using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    public AuthoredWorldDirector? AuthoredWorld { get; private set; }

    /// <summary>
    /// Authored world content from URMAN Studio (generic plots under
    /// res://content/world). Built last, so authored objects sit on top of the
    /// finished street and their interactions join the village zone.
    /// </summary>
    private void BuildAuthoredWorld()
    {
        if (AuthoredWorld is not null) return;
        AuthoredWorld = AuthoredWorldDirector.Build(GetNode<Node3D>("Act1CoreWorldGreybox"), _zoneInstances["village_day"]);
        foreach (var anchor in KitPlacementTakeover.Unmatched())
        {
            GD.PushError($"authored-world: the plot owns placement {anchor}, but the village builder no longer places it; decide in URMAN Studio.");
        }
    }
}
