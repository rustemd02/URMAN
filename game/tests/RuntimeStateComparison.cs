using System.Text.Json.Nodes;

namespace Urman.Godot.Tests;

/// <summary>
/// Runtime-state equality for "nothing progressed" proofs. A standing NPC's
/// conversation facing (Act1ConnectedWorld.NpcStaging turns toward and away
/// from the player) is deliberately written into its world.props snapshot, so
/// a save/load or a walk past her changes that one angle without any story,
/// knowledge or position change. Everything else must still match exactly.
/// </summary>
internal static class RuntimeStateComparison
{
    /// <summary>Semantic equality: property order is not state, a restored
    /// save may enumerate the same dictionaries in a different order.</summary>
    internal static bool SameIgnoringNpcFacing(string left, string right) =>
        JsonNode.DeepEquals(StripNpcFacing(left), StripNpcFacing(right));

    private static JsonNode? StripNpcFacing(string stateJson)
    {
        var state = JsonNode.Parse(stateJson);
        if (state?["world.props"] is JsonObject props)
        {
            foreach (var (key, record) in props)
            {
                if (key.StartsWith("npc/", StringComparison.Ordinal) && record is JsonObject npc)
                    npc.Remove("yaw");
            }
        }
        return state;
    }
}
