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
            "Инвентаря в рантайме тоже нет: всё, что происходит при подборе, описано условиями и последствиями взаимодействия.",
            workspace.EntityIds.Any(id => (string?)workspace.Get(id)?["kind"] == "pickup")
                ? "Механизм «pickup» контентом упражняется, и его поля проверяются PickupGaps."
                : "Механизм «pickup» (исполняемый рантаймом подбор с interactionId) контентом НЕ упражняется: авторских подборов ноль, а шесть размещённых предметов сделаны видом «item-spawn», который читает конкретный скрипт квеста. Первый авторский подбор уже будет проверен PickupGaps, но пока это способность без экземпляра."
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
    /// <summary>
    /// The runtime's other item-shaped kind. A pickup is executed by AuthoredWorldDirector's
    /// "pickup" case, which reads interactionId through GetProperty (absent = it throws) and
    /// optionally a catalogId for the visual, so the contract is checked here even though the
    /// campaign authors none yet: the first authored pickup is then already validated.
    /// </summary>
    public static IReadOnlyList<string> PickupGaps(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var interactions = AuthoredInteractions(workspace);
        var catalogue = SimpleItemForms.Catalog(workspace).Select(form => form.CatalogId).ToHashSet(StringComparer.Ordinal);
        var gaps = new List<string>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || (string?)entity["kind"] != "pickup")
            {
                continue;
            }

            var parameters = entity["params"] as JsonObject;
            var interaction = (string?)parameters?["interactionId"] ?? "";
            if (interaction.Length == 0)
            {
                gaps.Add($"{id}: у подбора нет interactionId — рантайм читает его через GetProperty и упадёт, не загрузив мир.");
            }
            else if (interactions.Count > 0 && !interactions.ContainsKey(interaction))
            {
                gaps.Add($"{id}: взаимодействия «{interaction}» нет среди авторских — подбор не с чем связать.");
            }

            if ((string?)parameters?["catalogId"] is { Length: > 0 } catalogId && catalogue.Count > 0 && !catalogue.Contains(catalogId))
            {
                gaps.Add($"{id}: записи «{catalogId}» нет в каталоге — рантайм напишет ошибку, и подбор останется без вида.");
            }

            if (parameters?["prompt"] is { } prompt
                && (prompt is not JsonValue promptValue || !promptValue.TryGetValue<string>(out _)))
            {
                gaps.Add($"{id}: поле «prompt» не строка — рантайм читает его текстом через GetString и упадёт на этом подборе.");
            }
        }

        return gaps;
    }

}
