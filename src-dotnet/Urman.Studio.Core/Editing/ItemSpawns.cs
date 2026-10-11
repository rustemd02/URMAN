using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One placed thing the player can pick up, with the interaction it invokes.</summary>
public sealed record ItemSpawnFacts(
    string EntityId,
    string PlacementPath,
    string InteractionId,
    string InteractionPath,
    IReadOnlyList<string> Problems);

/// <summary>
/// The first slice of the item editor (spec AI-23): what the campaign actually has
/// where an "item" would be. The compiled pack has no items registry and the runtime
/// has no inventory system, so a placed thing is an <c>item-spawn</c> entity whose
/// <c>interactionId</c> names the authored interaction that hands it over. This slice
/// verifies that link on the real sources and states the boundary instead of inventing
/// an item format the game would not read.
/// </summary>
public static class ItemSpawns
{
    public const string SpawnKind = "item-spawn";

    /// <summary>Every authored interaction id, with the file that declares it.</summary>
    public static IReadOnlyDictionary<string, string> AuthoredInteractions(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in workspace.Files)
        {
            Collect(file.Snapshot(), file.RelativePath, found);
        }

        return found;
    }

    /// <summary>The placed pickups of the campaign and what their interaction link really does.</summary>
    public static IReadOnlyList<ItemSpawnFacts> Inspect(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var interactions = AuthoredInteractions(workspace);
        var spawns = new List<ItemSpawnFacts>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || (string?)entity["kind"] != SpawnKind)
            {
                continue;
            }

            var problems = new List<string>();
            var interactionId = (string?)entity["params"]?["interactionId"] ?? "";
            if (interactionId.Length == 0)
            {
                problems.Add("У размещения нет interactionId: подобрать предмет нечем, игра ничего не предложит.");
            }

            if (entity["params"]?["position"] is not JsonArray { Count: 3 })
            {
                problems.Add("У размещения нет позиции из трёх чисел: игра не сможет его поставить.");
            }

            var path = interactionId.Length > 0 && interactions.TryGetValue(interactionId, out var declared) ? declared : "";
            if (interactionId.Length > 0 && path.Length == 0)
            {
                problems.Add($"Взаимодействие «{interactionId}» не найдено в авторских источниках: размещение останется без действия.");
            }

            spawns.Add(new ItemSpawnFacts(id, workspace.Locate(id)?.RelativePath ?? "", interactionId, path, problems));
        }

        return spawns.OrderBy(spawn => spawn.EntityId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// The honest boundary of this area: the catalogue of facts an author must know
    /// before asking for a real item editor.
    /// </summary>
    public static IReadOnlyList<string> Notes(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var interactions = AuthoredInteractions(workspace);
        var spawns = Inspect(workspace);
        return
        [
            $"Размещённых предметов с взаимодействием: {spawns.Count}; авторских взаимодействий в проекте: {interactions.Count}.",
            "Отдельного вида «item» и реестра предметов в проекте нет: игра не читает ни одного описания предмета, а подбор реализован взаимодействием сцены. Поэтому редактор предметов не может завести новый формат, пока у него нет runtime-потребителя — это контракт владельца фундамента, а не шаг Studio.",
            "Инвентаря в рантайме тоже нет: всё, что происходит при подборе, описано условиями и последствиями взаимодействия."
        ];
    }

    /// <summary>Walks any JSON shape, because scenes are authored inside array-rooted files.</summary>
    private static void Collect(JsonNode? node, string path, Dictionary<string, string> found)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["interactions"] is JsonArray interactions)
                {
                    foreach (var interaction in interactions.OfType<JsonObject>())
                    {
                        if ((string?)interaction["id"] is { Length: > 0 } id)
                        {
                            found.TryAdd(id, path);
                        }
                    }
                }

                foreach (var pair in obj)
                {
                    Collect(pair.Value, path, found);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Collect(item, path, found);
                }

                break;
        }
    }
}
