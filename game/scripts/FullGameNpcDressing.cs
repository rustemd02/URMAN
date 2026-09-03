using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only placement of the project-original Blender character kit.
/// The kit supplies authored low-poly silhouettes and LODs; this owner only
/// places them in a zone. Dialogue, progression and physics remain elsewhere.
/// </summary>
public static class FullGameNpcDressing
{
    private sealed record NpcSpec(
        string Id,
        string Prefix,
        string Role,
        Vector3 Position,
        float Yaw);

    public static void Build(Node3D root, string zoneId)
    {
        var host = new Node3D { Name = "NpcPresentation" };
        host.SetMeta("zoneId", zoneId);
        host.SetMeta("status", "generated-character-kit-v1");
        host.SetMeta("assetSource", GeneratedCharacterKitDressing.ScenePath);
        host.SetMeta("ownership", "presentation-only");
        host.SetMeta("collisionPolicy", "no physics body; interaction targets remain authored layer 1");
        host.SetMeta("assetStatus", "project-original GLB face/clothing detail pass; Godot Idle/Tension playback integrated; expression/audio polish remains open");

        var specs = SpecsFor(zoneId);
        host.SetMeta("npcCount", specs.Length);
        var defaultClip = zoneId is "fullgame_act4_pact" or "fullgame_act5_boundary"
            ? "Tension"
            : "Idle";
        host.SetMeta("defaultAnimationClip", defaultClip);
        foreach (var spec in specs)
        {
            var npc = GeneratedCharacterKitDressing.Attach(host, spec.Id, spec.Prefix, spec.Position);
            if (!GeneratedCharacterKitDressing.PlayClip(npc, defaultClip))
            {
                npc.QueueFree();
                throw new InvalidOperationException($"Generated character '{spec.Prefix}' could not play the zone presentation clip '{defaultClip}'.");
            }
            npc.Name = $"Npc_{spec.Id}";
            npc.RotationDegrees = new Vector3(0, spec.Yaw, 0);
            npc.SetMeta("role", spec.Role);
            npc.SetMeta("presentationStatus", "generated-character-kit-v1");
            npc.SetMeta("interactionOwnership", "none");
            npc.SetMeta("collisionLayer", 0);
            npc.SetMeta("assetStatus", $"project-original low-poly GLB with face/clothing detail and Godot Idle/Tension playback; zone clip={defaultClip}; expression/audio polish open");
        }

        root.AddChild(host);
    }

    private static NpcSpec[] SpecsFor(string zoneId) => zoneId switch
    {
        "fullgame_act2_house" =>
        [
            new("mansur-babay", "Mansur", "family elder", new(-2.85f, 0, -5.35f), 12),
            new("gulsina", "Gulsina", "family keeper", new(2.15f, 0, -5.7f), -18)
        ],
        "fullgame_act2_river" =>
        [new("alsu", "Alsu", "village witness", new(-0.35f, 0, -5.45f), 8)],
        "fullgame_act2_mosque" =>
        [new("timur-hazrat", "TimurHazrat", "imam and cultural anchor", new(-0.75f, 0, -5.65f), 0)],
        "fullgame_act2_council" =>
        [
            new("council-elder", "CouncilElder", "council elder", new(-2.05f, 0, -5.85f), 4),
            new("council-witness", "CouncilWitness", "silent witness", new(2.15f, 0, -5.8f), -8)
        ],
        "fullgame_act3_archive" =>
        [new("naila", "Naila", "archive keeper", new(-1.65f, 0, -3.85f), 12)],
        "fullgame_act3_soviet" =>
        [new("archive-clerk", "ArchiveClerk", "records clerk", new(2.55f, 0, -3.55f), -12)],
        "fullgame_act4_pact" =>
        [new("pact-keeper", "PactKeeper", "keeper of the pact", new(2.65f, 0, -6.05f), -8)],
        _ => []
    };
}
