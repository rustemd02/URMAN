using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One kind of condition or effect as the author sees it: a phrase with typed slots (spec QUEST08, ITEM04).</summary>
public sealed record RuleKind(string Op, string Label, bool IsEffect, IReadOnlyList<RuleSlot> Slots);

/// <summary>A typed slot: the picker only offers entities of this kind, so an item can never be chosen where a character belongs.</summary>
public sealed record RuleSlot(string Field, string Label, string ValueKind, IReadOnlyList<(string Value, string Label)>? Choices = null);

/// <summary>
/// The closed catalogue of conditions and effects the runtime executes
/// (content/schemas/condition-effect.schema.json), described in words. There
/// is no expression language: a rule is picked from this list and its slots
/// are filled from typed lists. Unknown operations are shown as "requires a
/// new mechanic" instead of an editable decoration.
/// </summary>
public sealed class ConditionPhrases(EntityCatalog catalog)
{
    private static readonly (string, string)[] QuestStatuses = [("inactive", "не начат"), ("active", "идёт"), ("completed", "завершён"), ("failed", "провален"), ("cancelled", "отменён")];
    private static readonly (string, string)[] KnowledgeStatuses = [("hidden", "не известно"), ("hypothesis", "догадка"), ("confirmed", "подтверждено"), ("contradicted", "опровергнуто")];

    public static readonly IReadOnlyList<RuleKind> Conditions =
    [
        new("npc.state", "Факт о персонаже", false, [new("characterId", "Персонаж", "character"), new("stateKey", "Факт", "fact"), new("value", "Значение", "value")]),
        new("quest.status", "Состояние квеста", false, [new("questId", "Квест", "quest"), new("status", "Состояние", "enum", QuestStatuses)]),
        new("knowledge.status", "Сведение / улика", false, [new("knowledgeId", "Сведение", "knowledge"), new("status", "Статус", "enum", KnowledgeStatuses)]),
        new("location.is", "Игрок в месте", false, [new("locationId", "Место", "scene")]),
        new("beat.state", "Сюжетный момент", false, [new("beatId", "Момент", "beat"), new("state", "Состояние", "enum", [("locked", "закрыт"), ("available", "доступен"), ("completed", "пройден")])]),
        new("vocabulary.status", "Татарское слово", false, [new("vocabularyId", "Слово", "vocabulary"), new("status", "Статус", "enum", [("unknown", "не встречено"), ("guessed", "угадано"), ("confirmed", "выучено")])]),
    ];

    public static readonly IReadOnlyList<RuleKind> Effects =
    [
        new("npc.set-state", "Запомнить факт о персонаже", true, [new("characterId", "Персонаж", "character"), new("stateKey", "Факт", "fact"), new("value", "Значение", "value")]),
        new("beat.set-state", "Отметить сюжетный момент", true, [new("beatId", "Момент", "beat"), new("state", "Состояние", "enum", [("available", "доступен"), ("completed", "пройден")])]),
        new("knowledge.set-status", "Подтвердить сведение", true, [new("knowledgeId", "Сведение", "knowledge"), new("status", "Статус", "enum", KnowledgeStatuses.Skip(1).ToArray())]),
        new("journal.record", "Записать в журнал", true, [new("entryId", "Запись", "knowledge"), new("sourceId", "Источник", "knowledge")]),
        new("document.open", "Открыть документ", true, [new("documentId", "Документ", "document")]),
        new("scene.request", "Запустить сцену", true, [new("sceneId", "Сцена", "scene")]),
        new("audio.request", "Проиграть звук", true, [new("assetId", "Звук", "asset")]),
    ];

    /// <summary>Operations the schema allows but Urman.Core's rule engine does not execute yet (checked 2026-09-29).</summary>
    public static readonly IReadOnlySet<string> NotExecuted = new HashSet<string>(StringComparer.Ordinal)
    {
        "time.phase", "inventory.has", "pressure.at-least", "quest.set-status", "item.transaction", "location.unlock"
    };

    /// <summary>Every rule in the project that the game would reject at run time, with where it is.</summary>
    public static IReadOnlyList<(string EntityId, string Op)> FindNotExecuted(StudioWorkspace workspace)
    {
        var found = new List<(string, string)>();
        void Scan(string id, JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["op"] is JsonValue op && op.TryGetValue<string>(out var name) && NotExecuted.Contains(name)) found.Add((id, name));
                    foreach (var (_, child) in obj) Scan(id, child);
                    break;
                case JsonArray array:
                    foreach (var child in array) Scan(id, child);
                    break;
            }
        }

        foreach (var id in workspace.EntityIds) Scan(id, workspace.Get(id));
        return found;
    }

    public string Describe(JsonObject rule)
    {
        if ((string?)(rule["op"] as JsonValue) is { } unsupported && NotExecuted.Contains(unsupported))
        {
            return $"Требуется новая механика: «{unsupported}» есть в схеме, но игра его не исполняет";
        }

        var op = (string?)rule["op"] ?? "";
        return op switch
        {
            "all" => "Все: " + string.Join(" И ", (rule["conditions"] as JsonArray ?? []).OfType<JsonObject>().Select(Describe)),
            "any" => "Любое: " + string.Join(" ИЛИ ", (rule["conditions"] as JsonArray ?? []).OfType<JsonObject>().Select(Describe)),
            "not" => "НЕ (" + (rule["condition"] is JsonObject inner ? Describe(inner) : "?") + ")",
            "npc.state" => $"{Name(rule, "characterId")}: {Fact(rule)} = {Value(rule["value"])}",
            "npc.set-state" => $"Запомнить: {Name(rule, "characterId")} — {Fact(rule)} = {Value(rule["value"])}",
            "quest.status" => $"Квест «{Name(rule, "questId")}» {Label(QuestStatuses, (string?)rule["status"])}",
            "quest.set-status" => $"Квест «{Name(rule, "questId")}» → {Label(QuestStatuses, (string?)rule["status"])}",
            "knowledge.status" => $"Сведение «{Name(rule, "knowledgeId")}»: {Label(KnowledgeStatuses, (string?)rule["status"])}",
            "knowledge.set-status" => $"Сведение «{Name(rule, "knowledgeId")}» → {Label(KnowledgeStatuses, (string?)rule["status"])}",
            "inventory.has" => $"У игрока есть «{Name(rule, "itemId")}» × {(int?)rule["minimumQuantity"] ?? 1}",
            "time.phase" => $"Сейчас {(string?)rule["phase"]}",
            "location.is" => $"Игрок в месте «{Name(rule, "locationId")}»",
            "journal.record" => $"Записать в журнал «{Name(rule, "entryId")}»",
            "document.open" => $"Открыть документ «{Name(rule, "documentId")}»",
            "scene.request" => $"Запустить сцену «{Name(rule, "sceneId")}»",
            "audio.request" => $"Проиграть «{Name(rule, "assetId")}»",
            "beat.state" => $"Момент «{Name(rule, "beatId")}»: {(string?)rule["state"]}",
            "vocabulary.status" => $"Слово «{Name(rule, "vocabularyId")}»: {(string?)rule["status"]}",
            _ => $"Требуется новая механика: {op}"
        };
    }

    public string DescribeAll(JsonArray? rules, string empty = "всегда") =>
        rules is null || rules.Count == 0 ? empty : string.Join(" И ", rules.OfType<JsonObject>().Select(Describe));

    public static RuleKind? Kind(string op) => Conditions.Concat(Effects).FirstOrDefault(kind => kind.Op == op);

    private string Name(JsonObject rule, string field) => rule[field] is JsonValue value ? catalog.NameOf((string)value!) : "?";

    private string Fact(JsonObject rule)
    {
        var key = (string?)rule["stateKey"] ?? "?";
        var labelId = FactLabelId((string?)rule["characterId"] ?? "", key);
        return catalog.Workspace.Get(labelId) is JsonObject label && label["label"] is JsonValue text ? (string)text! : key.Replace('_', ' ');
    }

    public static string FactLabelId(string characterId, string key) => $"{characterId}#{key}";

    /// <summary>Give a story fact a human name (stored in the editor-only facts file).</summary>
    public static void NameFact(EditSession session, string characterId, string key, string label)
    {
        var file = session.Workspace.FactLabels();
        var id = FactLabelId(characterId, key);
        session.Set(file.RelativePath, id, new JsonObject { ["id"] = id, ["characterId"] = characterId, ["key"] = key, ["label"] = label }, "название факта");
    }

    private static string Value(JsonNode? value) => value switch
    {
        JsonValue boolean when boolean.TryGetValue<bool>(out var flag) => flag ? "да" : "нет",
        null => "пусто",
        _ => value.ToJsonString()
    };

    private static string Label((string, string)[] labels, string? value) =>
        labels.FirstOrDefault(pair => pair.Item1 == value).Item2 ?? value ?? "?";
}

/// <summary>A story fact: one character-bound key, the unit that joins map objects, dialogue answers and quest steps.</summary>
public sealed record StoryFact(string CharacterId, string Key);

/// <summary>Who writes a fact and who reads it, collected across all content (the quest ↔ map link of spec TRIG03, A06).</summary>
public sealed record FactUse(string EntityId, string Where, bool Writes);

public sealed class FactIndex
{
    private readonly Dictionary<StoryFact, List<FactUse>> _uses = [];

    public FactIndex(StudioWorkspace workspace)
    {
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is JsonObject entity)
            {
                Collect(id, entity, "");
            }
        }
    }

    public IReadOnlyList<FactUse> Uses(StoryFact fact) => _uses.TryGetValue(fact, out var uses) ? uses : [];

    /// <summary>Facts an interaction (inside a scene) writes; used to link a map object to the steps that wait for it.</summary>
    public IEnumerable<StoryFact> WrittenBy(string interactionOrEntityId) =>
        _uses.Where(pair => pair.Value.Any(use => use.Writes && use.Where.Contains(interactionOrEntityId, StringComparison.Ordinal)))
            .Select(pair => pair.Key);

    /// <summary>IDs of interactions (inside scenes) and dialogues that write this fact.</summary>
    public IEnumerable<string> WritingSources(StoryFact fact) =>
        Uses(fact).Where(use => use.Writes)
            .SelectMany(use => use.Where.Split(" › ", StringSplitOptions.RemoveEmptyEntries).Where(part => part.Contains(":interaction/", StringComparison.Ordinal)).DefaultIfEmpty(use.EntityId))
            .Distinct(StringComparer.Ordinal);

    public IEnumerable<StoryFact> ReadBy(string entityId, string wherePrefix = "") =>
        _uses.Where(pair => pair.Value.Any(use => !use.Writes && use.EntityId == entityId && use.Where.StartsWith(wherePrefix, StringComparison.Ordinal)))
            .Select(pair => pair.Key);

    private void Collect(string entityId, JsonNode node, string where)
    {
        switch (node)
        {
            case JsonObject obj:
                var op = (string?)(obj["op"] as JsonValue);
                if (op is "npc.state" or "npc.set-state" && obj["characterId"] is JsonValue character && obj["stateKey"] is JsonValue key)
                {
                    var fact = new StoryFact((string)character!, (string)key!);
                    if (!_uses.TryGetValue(fact, out var list)) _uses[fact] = list = [];
                    list.Add(new(entityId, where, op == "npc.set-state"));
                }

                var label = obj["id"] is JsonValue id && id.TryGetValue<string>(out var text) ? text : null;
                foreach (var (name, child) in obj)
                {
                    if (child is not null) Collect(entityId, child, label is null ? $"{where} › {name}" : $"{where} › {label} › {name}");
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    if (child is not null) Collect(entityId, child, where);
                }

                break;
        }
    }
}
