using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One ordered step of an author-facing explanation: what is wrong and what to do next.</summary>
public sealed record DiagnosticStep(string Area, string Subject, string Text, bool Problem, string NextStep);

/// <summary>
/// Explains an authored object in one place (spec AI-40, step-by-step
/// diagnostics): why it may never fire, what the game will silently skip and what
/// the author should do next. It adds no new truth of its own: every step comes
/// from a checker that already exists - the routine notes, the closed catalogue of
/// rules the runtime executes, the trigger, PC-thread, cutscene and asset
/// projections, and the reference index that shows who points at the object.
/// </summary>
public static class AuthoringDiagnostics
{
    /// <summary>Everything the editor can say about one entity, ordered by area.</summary>
    public static IReadOnlyList<DiagnosticStep> ForEntity(StudioWorkspace workspace, EntityCatalog catalog, string entityId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var steps = new List<DiagnosticStep>();
        if (workspace.Get(entityId) is not JsonObject entity)
        {
            return [new DiagnosticStep("объект", entityId, $"Объект «{entityId}» не найден в проекте.", true, "Проверьте идентификатор: возможно, он пришёл из старой ссылки.")];
        }

        var kind = (string?)entity["kind"] ?? "";
        var name = catalog.NameOf(entityId);

        // 1. Rules the runtime does not execute at all.
        foreach (var (id, op) in ConditionPhrases.FindNotExecuted(workspace).Where(item => item.EntityId == entityId))
        {
            var known = ConditionPhrases.Kind(op);
            steps.Add(new DiagnosticStep("правила", name,
                $"Правило «{known?.Label ?? op}» игра не исполняет: {kind} останется таким, каким его видит игрок, без этого условия или последствия.",
                true,
                "Замените правило на исполняемое или отметьте, что нужна новая механика."));
        }

        // 2. Kind-specific checks: the same ones the panels already show.
        switch (kind)
        {
            case "npc":
                foreach (var note in Scenes.RoutineCheck.Notes(workspace, catalog).Where(note => note.EntityId == entityId))
                {
                    steps.Add(new DiagnosticStep("распорядок", name, note.Text, note.Problem,
                        note.Problem ? "Исправьте блок распорядка: иначе персонаж не придёт или не займётся нужным." : "Отметьте как принятое поведение."));
                }

                foreach (var policy in BlockPolicies(entity))
                {
                    steps.Add(new DiagnosticStep("распорядок", name, policy.Text, policy.Problem, policy.NextStep));
                }

                break;
            case "trigger":
                foreach (var problem in TriggerScopeFacts.Describe(entityId, entity).Problems)
                {
                    steps.Add(new DiagnosticStep("триггер", name, problem, true,
                        "Дополните область: без этого она не сработает или уронит директора мира."));
                }

                break;
            case "document" or "chat" or "hint":
                if (PcChat.Parse(entityId, entity) is { } chat)
                {
                    foreach (var problem in PcChat.Validate(workspace, chat))
                    {
                        steps.Add(new DiagnosticStep("сообщения ПК", name, problem, true,
                            "Дополните тред: иначе игрок увидит пустую строку или не найдёт ветку."));
                    }
                }

                break;
            case "cutscene-action":
                var cutsceneFile = workspace.Files.FirstOrDefault(file => file.Header("kind") is JsonValue value && (string?)value == "urman.cutscene");
                if (cutsceneFile is not null)
                {
                    var actors = (JsonNode.Parse(cutsceneFile.Text)?["actors"] as JsonObject)?.Select(pair => pair.Key).ToArray() ?? [];
                    var facts = CutsceneActions.Describe(workspace, entity, actors);
                    foreach (var problem in facts.Problems)
                    {
                        steps.Add(new DiagnosticStep("катсцена", name, problem, true,
                            "Поправьте действие: рантайм либо упадёт, либо молча пропустит его."));
                    }
                }

                break;
        }

        // 3. Catalogue resources: what a change would touch and whether it is publishable.
        var catalogueIds = new List<string>();
        if ((string?)entity["params"]?["catalogId"] is { Length: > 0 } single) catalogueIds.Add(single);
        if (entity["params"]?["catalogIds"] is { } many)
        {
            if (many is not JsonArray)
            {
                // A wrongly typed list must be said out loud, not read as "no resources".
                steps.Add(new DiagnosticStep("ресурс", name,
                    "Поле catalogIds не массив: ссылки на ресурсы не прочитаны, поэтому и общие использования не проверены.",
                    true,
                    "Запишите catalogIds массивом идентификаторов."));
            }
            else
            {
                catalogueIds.AddRange(many.AsArray().Select(item => (string?)item).Where(item => item is { Length: > 0 })!);
            }
        }
        if (catalogueIds.Count > 0)
        {
            var assets = AssetUsageIndex.Open(workspace.Root);
            foreach (var catalogueId in catalogueIds.Distinct(StringComparer.Ordinal))
            {
                var usage = assets.ByCatalogueId(catalogueId);
                foreach (var problem in usage.Problems)
                {
                    steps.Add(new DiagnosticStep("ресурс", name, problem, true, "Опишите источник и лицензию ресурса перед публикацией."));
                }
            }
        }

        // 4. Who points at this object: a link chain explains why it is seen at all.
        // This is information, not a fault: a placement and an ambient resident are
        // brought into the world by the director and need no reference at all, while a
        // quest step or an interaction does. The explanation says which case it is
        // instead of flagging every unreferenced object.
        var users = new ReferenceIndex(workspace).UsedBy(entityId).ToArray();
        steps.Add(new DiagnosticStep("связи", name,
            users.Length == 0
                ? "На объект никто не ссылается. Для размещённого объекта и жителя это нормально — их приводит в мир директор; для шага квеста, предмета или взаимодействия это значит, что игра его не вызовет."
                : $"На объект ссылаются: {string.Join(", ", users.Select(user => catalog.NameOf(user)))}.",
            false,
            users.Length == 0
                ? "Свяжите объект, если его должен вызвать квест, диалог или триггер."
                : "Ничего не требуется."));

        return steps;
    }

    /// <summary>The whole project's problems in the same words, bounded for reading.</summary>
    public static IReadOnlyList<DiagnosticStep> ForWorkspace(StudioWorkspace workspace, EntityCatalog catalog, int limit = 60)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(catalog);
        var steps = new List<DiagnosticStep>();
        foreach (var (entityId, op) in ConditionPhrases.FindNotExecuted(workspace))
        {
            steps.Add(new DiagnosticStep("правила", catalog.NameOf(entityId),
                $"Правило «{ConditionPhrases.Kind(op)?.Label ?? op}» игра не исполняет.",
                true,
                "Замените правило или заведите новую механику."));
        }

        foreach (var note in Scenes.RoutineCheck.Notes(workspace, catalog).Where(note => note.Problem))
        {
            steps.Add(new DiagnosticStep("распорядок", catalog.NameOf(note.EntityId), note.Text, true, "Поправьте блок расписания."));
        }

        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity)
            {
                continue;
            }

            if ((string?)entity["kind"] == "trigger")
            {
                foreach (var problem in TriggerScopeFacts.Describe(id, entity).Problems)
                {
                    steps.Add(new DiagnosticStep("триггер", catalog.NameOf(id), problem, true, "Дополните область."));
                }
            }
        }

        return steps.Take(limit).ToArray();
    }

    /// <summary>
    /// The blocked-route policy: the runtime understands three values, and a typo
    /// silently means "go directly to the place", which looks like the story
    /// skipping the walk.
    /// </summary>
    private static IEnumerable<DiagnosticStep> BlockPolicies(JsonObject entity)
    {
        if (entity["params"]?["schedule"] is not JsonArray blocks)
        {
            yield break;
        }

        foreach (var block in blocks.OfType<JsonObject>())
        {
            var name = (string?)block["name"] ?? (string?)block["id"] ?? "";
            if (block["onBlocked"] is not JsonValue policy || !policy.TryGetValue<string>(out var value))
            {
                continue;
            }

            if (value is not ("stay" or "safe-point" or "go-directly"))
            {
                yield return new DiagnosticStep("распорядок", name,
                    $"Политика «{value}» неизвестна рантайму: он пойдёт по ветке по умолчанию и просто встанет на место, пропустив путь.",
                    true,
                    "Выберите «stay», «safe-point» или «go-directly».");
            }
            else if (value == "safe-point" && block["safePoint"] is not JsonArray { Count: 3 })
            {
                yield return new DiagnosticStep("распорядок", name,
                    "Выбрана безопасная точка, но она не задана: рантайм уйдёт в ветку по умолчанию.",
                    true,
                    "Задайте safePoint из трёх чисел или смените политику.");
            }
        }
    }
    /// <summary>
    /// Which runtime code consumes which authored kind, taken from the sources rather than
    /// remembered: the director's switch cases, the quest script's EntitiesOfKind call, the
    /// takeover's anchor lookup and the settlement registry's address lookup. A kind that
    /// appears in none of them is reported as "no consumer found" - a fact about the code that
    /// was read, not a proof that nothing anywhere reads it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> KindConsumers(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var consumers = new Dictionary<string, string>(StringComparer.Ordinal);
        var director = Read(full, "game/scripts/AuthoredWorldDirector.cs");
        foreach (System.Text.RegularExpressions.Match match in DirectorCase.Matches(director))
        {
            consumers[match.Groups[1].Value] = "AuthoredWorldDirector (case в switch по kind)";
        }

        var quest = Read(full, "game/scripts/TamaraFenceQuest.cs");
        foreach (System.Text.RegularExpressions.Match match in EntitiesOfKind.Matches(quest))
        {
            consumers.TryAdd(match.Groups[1].Value, "TamaraFenceQuest (читает EntitiesOfKind)");
        }

        if (Read(full, "game/scripts/KitPlacementTakeover.cs").Contains("logicalAnchor", StringComparison.Ordinal))
        {
            consumers.TryAdd("kit-placement", "KitPlacementTakeover (ищет по logicalAnchor)");
        }

        if (Read(full, "game/scripts/SettlementRegistry.cs").Length > 0)
        {
            consumers.TryAdd("parcel", "SettlementRegistry / адресные привязки");
        }

        return consumers;
    }

    /// <summary>
    /// The kinds the campaign authors that the consumer map above does not claim. This is a
    /// statement about the SEARCH, not about the world: several of these kinds (a clue, a fact,
    /// a contradiction, a route, an image) are read by ID through the knowledge, document and
    /// vocabulary registries rather than by their kind, so "not found by kind" must never be
    /// read as "nothing reads it".
    /// </summary>
    public static IReadOnlyList<string> UnconsumedKinds(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var consumers = KindConsumers(root);
        var kinds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is { } entity && (string?)entity["kind"] is { Length: > 0 } kind)
            {
                kinds.Add(kind);
            }
        }

        return kinds.Where(kind => !consumers.ContainsKey(kind)).ToArray();
    }

    private static readonly System.Text.RegularExpressions.Regex DirectorCase = new(
        @"case\s+""([a-z-]+)""\s*:", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex EntitiesOfKind = new(
        @"EntitiesOfKind\(\s*""([a-z-]+)""", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string Read(string full, string relative)
    {
        var path = Path.Combine(full, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    /// <summary>What the consumer map does and does not say.</summary>
    public static IReadOnlyList<string> ConsumerNotes(StudioWorkspace workspace, string root)
    {
        var consumers = KindConsumers(root);
        var unconsumed = UnconsumedKinds(workspace, root);
        return
        [
            $"Видов с найденным потребителем: {consumers.Count}; видов без потребителя по имени: {unconsumed.Count}.",
            consumers.Count == 0
                ? "Ни один потребитель не прочитан из исходников: сверять нечем."
                : "Потребители прочитаны из исходников: case в AuthoredWorldDirector, EntitiesOfKind в скрипте квеста, поиск по logicalAnchor в KitPlacementTakeover и адресный поиск SettlementRegistry.",
            unconsumed.Count == 0
                ? "Каждый авторский вид кем-то назван."
                : $"Виды без потребителя ПО ИМЕНИ: {string.Join(", ", unconsumed)}. Это утверждение о поиске, а не о мире: часть из них читается по ID через реестры знаний, документов и словаря, поэтому «не найдено по виду» нельзя читать как «никто не читает».",
            "Директор рантайма для неизвестного вида пишет предупреждение «has kind with no executor; it is shown only in Studio» — это его собственный ответ, и он не заменяется догадкой."
        ];
    }

}
