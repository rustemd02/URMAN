using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urman.Content.Compilation;

/// <summary>
/// The on-disk text of a compiled content pack. The CLI and URMAN Studio both
/// write packs through this, so a pack compiled from either is byte-identical.
/// </summary>
public static class CompiledPackText
{
    public static string Serialize(JsonNode pack) =>
        pack.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
}
