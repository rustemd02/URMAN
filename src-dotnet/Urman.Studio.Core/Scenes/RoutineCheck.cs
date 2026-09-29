using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.Core.Scenes;

/// <summary>One thing the author should see about a routine or a scene's hold on a character.</summary>
public sealed record RoutineNote(string EntityId, string BlockId, string Text, bool Problem);

/// <summary>
/// What Studio shows next to a character's routine (spec NPC02, NPC03, CINE09):
/// blocks that can never come (an earlier block has no condition), activities
/// missing from the animation catalogue, two characters sent to the same spot
/// by blocks that can hold together, and scenes that take the character — the
/// routine yields to them. Two scenes over one actor are named with the
/// policy the second one uses.
/// </summary>
public static class RoutineCheck
{
    public const string AnimationCatalogPath = "game/content/animations/catalog.v1.json";
    private const double SameSpotMetres = .8;

    public static IReadOnlyList<RoutineNote> Notes(StudioWorkspace workspace, EntityCatalog catalog)
    {
        var notes = new List<RoutineNote>();
        var motions = workspace.HasFile(AnimationCatalogPath)
            ? workspace.File(AnimationCatalogPath).Keys().ToHashSet(StringComparer.Ordinal)
            : [];
        var characters = new List<(string Id, string? Character, JsonArray Blocks)>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is JsonObject { } entity && (string?)entity["kind"] == "npc" && entity["params"]?["schedule"] is JsonArray blocks)
            {
                characters.Add((id, (string?)entity["params"]?["characterId"], blocks));
            }
        }

        foreach (var (id, _, blocks) in characters)
        {
            var always = (string?)null;
            foreach (var block in blocks.OfType<JsonObject>())
            {
                var blockId = (string?)block["id"] ?? "";
                var name = (string?)block["name"] ?? blockId;
                if (always is not null)
                {
                    notes.Add(new(id, blockId, $"«{name}» никогда не наступит: выше стоит «{always}» без условия", true));
                }
                else if (block["when"] is not JsonArray { Count: > 0 })
                {
                    always = name;
                }

                foreach (var motion in new[] { (string?)block["motion"], (string?)block["walkMotion"] }.OfType<string>())
                {
                    if (motions.Count > 0 && !motions.Contains(motion))
                    {
                        notes.Add(new(id, blockId, $"«{name}»: занятия {motion} нет в библиотеке анимаций", true));
                    }
                }

                if (block["onBlocked"] is JsonValue policy && (string?)policy == "safe-point" && block["safePoint"] is not JsonArray { Count: 3 })
                {
                    notes.Add(new(id, blockId, $"«{name}»: выбрана безопасная точка, но она не задана", true));
                }

                if (block["follow"] is null && block["place"] is not JsonArray { Count: 3 })
                {
                    notes.Add(new(id, blockId, $"«{name}»: не задано место", true));
                }
            }
        }

        for (var a = 0; a < characters.Count; a++)
        {
            for (var b = a + 1; b < characters.Count; b++)
            {
                foreach (var first in characters[a].Blocks.OfType<JsonObject>())
                {
                    foreach (var second in characters[b].Blocks.OfType<JsonObject>())
                    {
                        if (first["follow"] is not null || second["follow"] is not null) continue;
                        if (Place(first) is not { } p || Place(second) is not { } q) continue;
                        if (Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Z - q.Z) * (p.Z - q.Z)) >= SameSpotMetres) continue;
                        if (Exclusive(first, second)) continue;
                        notes.Add(new(characters[a].Id, (string?)first["id"] ?? "",
                            $"«{(string?)first["name"]}» и «{catalog.NameOf(characters[b].Id)}: {(string?)second["name"]}» могут одновременно поставить двоих в одну точку", true));
                    }
                }
            }
        }

        // Scenes: which characters each one takes, and two scenes over one actor.
        var holders = new Dictionary<string, List<(string Scene, string Policy)>>(StringComparer.Ordinal);
        foreach (var file in workspace.Files.Where(file => file.Header("kind") is JsonValue kind && (string?)kind == "urman.cutscene"))
        {
            var scene = (string?)file.Header("id") ?? file.RelativePath;
            var policy = (string?)file.Header("occupancy") ?? "wait";
            if (file.Header("actors") is not JsonObject actors) continue;
            foreach (var (_, character) in actors)
            {
                if ((string?)character is not { } characterId) continue;
                (holders.TryGetValue(characterId, out var list) ? list : holders[characterId] = []).Add((scene, policy));
            }
        }

        foreach (var (characterId, scenes) in holders)
        {
            foreach (var (id, _, _) in characters.Where(item => item.Character == characterId))
            {
                foreach (var (scene, _) in scenes)
                {
                    notes.Add(new(id, "", $"Сцена «{catalog.NameOf(scene)}» занимает персонажа; распорядок уступает и продолжается после неё", false));
                }
            }

            if (scenes.Count > 1)
            {
                notes.Add(new(scenes[1].Scene, "",
                    $"«{catalog.NameOf(characterId)}» нужен сценам «{catalog.NameOf(scenes[0].Scene)}» и «{catalog.NameOf(scenes[1].Scene)}»; если они совпадут, вторая {(scenes[1].Policy == "cancel" ? "не начнётся" : "подождёт окончания первой")}", scenes[1].Policy is not ("wait" or "cancel")));
            }
        }

        return notes;
    }

    private static (double X, double Z)? Place(JsonObject block) =>
        block["place"] is JsonArray { Count: 3 } place ? ((double)place[0]!, (double)place[2]!) : null;

    // Two blocks cannot hold together only when one requires a status of a
    // quest or story moment that the other requires to be different.
    private static bool Exclusive(JsonObject first, JsonObject second)
    {
        var left = Requirements(first).ToArray();
        var right = Requirements(second).ToArray();
        return left.Any(l => right.Any(r => l.Key == r.Key && l.Value != r.Value));
    }

    private static IEnumerable<(string Key, string Value)> Requirements(JsonObject block)
    {
        foreach (var rule in (block["when"] as JsonArray ?? []).OfType<JsonObject>())
        {
            switch ((string?)rule["op"])
            {
                case "quest.status":
                    yield return ($"quest:{(string?)rule["questId"]}", (string?)rule["status"] ?? "");
                    break;
                case "beat.state":
                    yield return ($"beat:{(string?)rule["beatId"]}", (string?)rule["state"] ?? (string?)rule["phase"] ?? "");
                    break;
                case "npc.state":
                    yield return ($"npc:{(string?)rule["characterId"]}/{(string?)rule["stateKey"]}", rule["value"]?.ToJsonString() ?? "");
                    break;
            }
        }
    }
}
