using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored answer of a PC node, with the conditions the runtime evaluates.</summary>
public sealed record PcChatChoice(
    string Id,
    string TextId,
    string? NextNodeId,
    IReadOnlyList<string> Requires);

/// <summary>One authored node of a PC thread, as the runtime walks it.</summary>
public sealed record PcChatNode(
    string Id,
    string From,
    string TextId,
    string? NextNodeId,
    IReadOnlyList<PcChatChoice> Choices,
    IReadOnlyList<string> Reveals,
    IReadOnlyList<string> Requires);

/// <summary>
/// An authored PC thread or hint (spec AI-24, old-PC content). The Studio content
/// section edited a thread's Markdown body but never showed or checked the thread
/// itself, so a broken link, a line nobody resolves or a branch nobody reaches
/// was invisible to the author.
/// </summary>
public sealed record PcChatContent(
    string EntityId,
    string Presence,
    string StartNodeId,
    string FreeTextReply,
    bool FreeTextReplyDeclared,
    IReadOnlyList<string> Requires,
    IReadOnlyList<PcChatNode> Nodes,
    int Level,
    bool LevelIsInteger,
    bool IsHint,
    string PointsTo,
    string TextId,
    IReadOnlyList<string> AuthoredFields,
    IReadOnlyList<string> Malformed);

/// <summary>
/// The PC thread or hint as the runtime reads it, parsed strictly: a missing field,
/// an explicit null, a wrong type and an empty string are four different findings,
/// and a malformed node or answer is named instead of being dropped, because the
/// runtime reads node/choice conditions and the hint's text and search target. The
/// text ids stay the single identity, so jumping from a node to its line is
/// navigation, not a second editor.
/// </summary>
public static class PcChat
{
    /// <summary>The id shape both a node and an answer require in chat.schema.json.</summary>
    private static readonly Regex IdPattern = new("^[a-z][a-z0-9._-]*$", RegexOptions.CultureInvariant);
    private const string IdPatternText = "строчная латинская буква, дальше строчные буквы, цифры, точка, подчёркивание или дефис";

    public const string RuntimePath = "game/scripts/OldPcUi.Chat.cs";
    public const string HintRuntimePath = "game/scripts/OldPcUi.Search.cs";
    public const string HintSchemaPath = "content/schemas/hint.schema.json";

    /// <summary>The hint rules the schema states, read from the schema itself.</summary>
    public static (int? MinimumLevel, int? MaximumLevel, int? MaximumPointsTo) HintSchemaBounds(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), HintSchemaPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject schema || schema["properties"] is not JsonObject properties)
        {
            return (null, null, null);
        }

        return (Bound(properties["level"], "minimum"), Bound(properties["level"], "maximum"), Bound(properties["pointsTo"], "maxLength"));
    }

    private static int? Bound(JsonNode? property, string name) =>
        property?[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    public static PcChatContent Parse(string entityId, JsonObject? entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var malformed = new List<string>();
        var nodes = new List<PcChatNode>();
        if (entity?["nodes"] is JsonArray authored)
        {
            for (var index = 0; index < authored.Count; index++)
            {
                if (authored[index] is not JsonObject node)
                {
                    malformed.Add($"Узел #{index + 1} не объект: рантайм не прочитает его поля.");
                    continue;
                }

                var where = $"узел #{index + 1}";
                var choices = new List<PcChatChoice>();
                if (node["choices"] is { } rawChoices)
                {
                    if (rawChoices is not JsonArray choiceArray)
                    {
                        malformed.Add($"У {where} «{Label(node)}» поле choices не массив: рантайм увидит ноль ответов.");
                    }
                    else
                    {
                        for (var c = 0; c < choiceArray.Count; c++)
                        {
                            var choiceWhere = $"вариант #{c + 1} {where} «{Label(node)}»";
                            if (choiceArray[c] is not JsonObject choice)
                            {
                                malformed.Add($"У {where} «{Label(node)}» {choiceWhere}: не объект.");
                                continue;
                            }

                            choices.Add(new PcChatChoice(
                                Required(choice, "id", choiceWhere, malformed),
                                Required(choice, "textId", choiceWhere, malformed),
                                Nullable(choice["nextNodeId"], $"{choiceWhere}, nextNodeId", malformed),
                                Ops(choice["requires"], $"{choiceWhere}, requires", malformed)));
                        }
                    }
                }

                if (!node.ContainsKey("nextNodeId"))
                {
                    malformed.Add($"У {where} «{Label(node)}» нет поля nextNodeId, а схема тредов его требует (допуская null).");
                }

                if (!node.ContainsKey("reveals"))
                {
                    malformed.Add($"У {where} «{Label(node)}» нет поля reveals, а схема тредов его требует (можно пустой массив).");
                }

                if (!node.ContainsKey("choices"))
                {
                    malformed.Add($"У {where} «{Label(node)}» нет поля choices, а схема тредов его требует (можно пустой массив).");
                }

                if (!node.ContainsKey("requires"))
                {
                    malformed.Add($"У {where} «{Label(node)}» нет поля requires, а схема тредов его требует (можно пустой массив).");
                }

                nodes.Add(new PcChatNode(
                    Required(node, "id", where, malformed),
                    Required(node, "from", where, malformed),
                    Required(node, "textId", where, malformed),
                    Nullable(node["nextNodeId"], $"{where}, nextNodeId", malformed),
                    choices,
                    Strings(node["reveals"]),
                    Ops(node["requires"], $"{where}, requires", malformed)));

                if (choices.Count > 3)
                {
                    malformed.Add($"У {where} «{Label(node)}» {choices.Count} ответов, а схема разрешает не больше трёх.");
                }
            }
        }
        else if (entity?["nodes"] is not null)
        {
            malformed.Add("Поле nodes не массив: рантайм увидит пустой тред.");
        }

        var isHint = entityId.Contains(":hint/", StringComparison.Ordinal);
        return new PcChatContent(
            entityId,
            Text(entity?["presence"], "presence", malformed),
            Required(entity, "startNodeId", "тред", malformed, required: !isHint),
            Text(entity?["freeTextReply"], "freeTextReply", malformed),
            entity?["freeTextReply"] is not null,
            Ops(entity?["requires"], "requires", malformed),
            nodes,
            (int)Number(entity?["level"]),
            entity?["level"] is JsonValue levelValue && IsIntegral(levelValue),
            isHint,
            isHint ? Required(entity, "pointsTo", "подсказка", malformed) : "",
            isHint ? Required(entity, "textId", "подсказка", malformed) : "",
            entity is null ? [] : entity.Select(pair => pair.Key).ToArray(),
            malformed);
    }

    /// <summary>Everything that would leave the player with a broken or invisible thread.</summary>
    public static IReadOnlyList<string> Validate(StudioWorkspace workspace, PcChatContent chat)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(chat);
        var problems = new List<string>(chat.Malformed);
        foreach (var op in chat.Requires.Concat(chat.Nodes.SelectMany(node => node.Requires)).Concat(chat.Nodes.SelectMany(node => node.Choices).SelectMany(choice => choice.Requires)).Distinct(StringComparer.Ordinal))
        {
            if (ConditionPhrases.Kind(op) is null)
            {
                problems.Add($"Условие «{op}» игра не исполняет: эта ветка или подсказка может не появиться.");
            }
        }

        if (chat.IsHint)
        {
            return Hint(workspace, chat, problems);
        }

        if (chat.Nodes.Count == 0)
        {
            problems.Add("У треда нет ни одного узла: открывать нечего.");
            return problems;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in chat.Nodes.Where(node => node.Id.Length > 0))
        {
            if (!ids.Add(node.Id))
            {
                problems.Add($"Узел «{node.Id}» объявлен дважды: вторая ветка перекроет первую.");
            }

            if (!IdPattern.IsMatch(node.Id))
            {
                problems.Add($"id узла «{node.Id}» не совпадает со схемой тредов: {IdPatternText}");
            }

            if (node.NextNodeId is { Length: > 0 } target && !IdPattern.IsMatch(target))
            {
                problems.Add($"nextNodeId «{target}» узла «{node.Id}» не совпадает со схемой тредов: {IdPatternText}");
            }

            foreach (var choice in node.Choices.Where(choice => choice.Id.Length > 0).Where(choice => !IdPattern.IsMatch(choice.Id)))
            {
                problems.Add($"id ответа «{choice.Id}» узла «{node.Id}» не совпадает со схемой тредов: {IdPatternText}");
            }
        }

        if (chat.StartNodeId.Length == 0 || !ids.Contains(chat.StartNodeId))
        {
            problems.Add($"Стартовый узел «{chat.StartNodeId}» не найден: рантайм откроет тред и не сможет показать ни одной строки.");
        }

        foreach (var node in chat.Nodes)
        {
            if (node.TextId.Length > 0 && workspace.Locate(node.TextId) is null)
            {
                problems.Add($"Реплика «{node.TextId}» узла «{node.Id}» не найдена в проекте: игрок увидит пустоту.");
            }

            foreach (var target in new[] { node.NextNodeId }.Concat(node.Choices.Select(choice => choice.NextNodeId)).Where(target => target is { Length: > 0 }))
            {
                if (!ids.Contains(target!))
                {
                    problems.Add($"Узел «{node.Id}» ведёт в «{target}», которого нет: ветка оборвётся.");
                }
            }

            foreach (var choice in node.Choices.Where(choice => choice.TextId.Length > 0))
            {
                if (workspace.Locate(choice.TextId) is null)
                {
                    problems.Add($"Ответ «{choice.TextId}» узла «{node.Id}» не найден в проекте: игрок увидит пустую кнопку.");
                }
            }

            // reveals are the short labels the chat shows, not content ids: the schema
            // wants a label without a colon ("^[^:]+$", up to 64 characters).
            foreach (var reveal in node.Reveals)
            {
                if (reveal.Contains(':', StringComparison.Ordinal) || reveal.Length > 64)
                {
                    problems.Add($"Подпись «{reveal}» узла «{node.Id}» не совпадает со схемой: двоеточие в подписи запрещено, длина — до 64 символов.");
                }
            }
        }

        if (chat.FreeTextReplyDeclared && chat.FreeTextReply.Length > 0 && workspace.Locate(chat.FreeTextReply) is null)
        {
            problems.Add($"Свободный ответ «{chat.FreeTextReply}» не найден в проекте.");
        }

        foreach (var unreachable in Unreachable(chat))
        {
            problems.Add($"Узел «{unreachable}» недостижим из стартового: авторская строка, которую игрок не увидит.");
        }

        return problems;
    }

    /// <summary>
    /// A hint is not a thread: the runtime resolves its single text, offers its
    /// search target and reads its level, and the schema forbids every other field,
    /// so a field the schema does not allow is a real finding rather than a note.
    /// </summary>
    private static IReadOnlyList<string> Hint(StudioWorkspace workspace, PcChatContent chat, List<string> problems)
    {
        var schemaPath = Path.Combine(workspace.Root, HintSchemaPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(schemaPath) || JsonNode.Parse(File.ReadAllText(schemaPath)) is not JsonObject schema)
        {
            problems.Add($"Нет {HintSchemaPath}: требования к подсказке не проверены.");
            return problems;
        }

        // Every field the schema requires is named separately, so a missing one is not
        // hidden behind a later failure.
        if (schema["required"] is JsonArray required)
        {
            foreach (var field in required.Select(item => (string?)item).OfType<string>().Where(field => !chat.AuthoredFields.Contains(field, StringComparer.Ordinal)))
            {
                problems.Add($"У подсказки нет обязательного поля «{field}»: контент не пройдёт проверку схемы.");
            }
        }

        var (minimumLevel, maximumLevel, maximumPointsTo) = HintSchemaBounds(workspace.Root);
        if (chat.AuthoredFields.Contains("level"))
        {
            if (!chat.LevelIsInteger)
            {
                problems.Add("Уровень подсказки должен быть целым числом: схема объявляет его целым.");
            }
            else if (minimumLevel is { } min && chat.Level < min)
            {
                problems.Add($"Уровень подсказки {chat.Level} меньше допустимого {min}: такая подсказка не появится в порядке показа.");
            }
            else if (maximumLevel is { } max && chat.Level > max)
            {
                problems.Add($"Уровень подсказки {chat.Level} больше допустимого {max}: схема такую подсказку не примет.");
            }
        }

        if (chat.TextId.Length == 0)
        {
            problems.Add("У подсказки не задан textId.");
        }
        else if (workspace.Get(chat.TextId) is not JsonObject textEntity)
        {
            problems.Add($"Текст подсказки «{chat.TextId}» не найден в проекте: игрок увидит пустоту.");
        }
        else if (!string.Equals(EntityCatalog.KindOf(chat.TextId, textEntity), "text", StringComparison.Ordinal))
        {
            problems.Add($"«{chat.TextId}» — не текст, а сущность вида «{EntityCatalog.KindOf(chat.TextId, textEntity)}»: рантайм возьмёт из неё поле value и покажет пустоту.");
        }

        if (chat.PointsTo.Length == 0)
        {
            problems.Add("У подсказки не задана цель поиска: строка «Попробуйте поиск» останется пустой, и поиск не предложит слово.");
        }
        else if (chat.PointsTo.Contains(':', StringComparison.Ordinal))
        {
            problems.Add($"Цель поиска «{chat.PointsTo}» содержит двоеточие: схема требует подпись без двоеточия.");
        }
        else if (maximumPointsTo is { } longest && chat.PointsTo.Length > longest)
        {
            problems.Add($"Цель поиска длиннее {longest} символов: схема такую подпись не примет.");
        }

        foreach (var field in FieldsOutsideHintSchema(workspace, chat))
        {
            problems.Add($"Поле «{field}» схема подсказки не разрешает: такая запись не пройдёт проверку контента.");
        }

        return problems;
    }

    /// <summary>
    /// The fields the real hint schema allows, read from the schema itself, so the
    /// forbidden-field finding follows the contract instead of a copied list.
    /// </summary>
    private static IReadOnlyList<string> FieldsOutsideHintSchema(StudioWorkspace workspace, PcChatContent chat)
    {
        var path = Path.Combine(workspace.Root, HintSchemaPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)
            || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject schema
            || schema["properties"] is not JsonObject properties)
        {
            return [];
        }

        if (schema["additionalProperties"] is JsonValue value && value.TryGetValue<bool>(out var allowed) && allowed)
        {
            return [];
        }

        var known = properties.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        return chat.AuthoredFields.Where(field => !known.Contains(field)).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Nodes the runtime can never reach from the authored start node.</summary>
    public static IReadOnlyList<string> Unreachable(PcChatContent chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        if (chat.Nodes.Count == 0 || chat.StartNodeId.Length == 0)
        {
            return [];
        }

        var byId = new Dictionary<string, PcChatNode>(StringComparer.Ordinal);
        foreach (var node in chat.Nodes.Where(node => node.Id.Length > 0))
        {
            byId.TryAdd(node.Id, node);
        }

        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        if (byId.ContainsKey(chat.StartNodeId))
        {
            pending.Push(chat.StartNodeId);
        }

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!reached.Add(current) || !byId.TryGetValue(current, out var node))
            {
                continue;
            }

            foreach (var target in new[] { node.NextNodeId }.Concat(node.Choices.Select(choice => choice.NextNodeId)))
            {
                if (target is { Length: > 0 } && byId.ContainsKey(target))
                {
                    pending.Push(target);
                }
            }
        }

        return chat.Nodes.Select(node => node.Id).Where(id => id.Length > 0 && !reached.Contains(id)).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>The thread in words: who it belongs to, how it starts and how it ends.</summary>
    public static IReadOnlyList<string> Outline(PcChatContent chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        if (chat.IsHint)
        {
            var when = chat.Requires.Count == 0
                ? "появляется по мере чтения архива, отдельных условий нет"
                : $"появляется при условиях: {string.Join(", ", chat.Requires)}";
            var search = chat.PointsTo.Length == 0 ? "цель поиска не задана" : $"поиск подскажет: {chat.PointsTo}";
            return
            [
                $"Подсказка уровня {chat.Level}: {when}; {search}. Текст берётся из «{chat.TextId}».",
                "Показывается один раз на сохранение: список виденных лежит в сохранении, вмещает 64 подсказки и вытесняет самые старые — после этого старая подсказка может появиться снова."
            ];
        }

        var ends = chat.Nodes.Count(node => (node.NextNodeId is null or { Length: 0 }) && node.Choices.All(choice => choice.NextNodeId is null or { Length: 0 }));
        var choices = chat.Nodes.Sum(node => node.Choices.Count);
        return
        [
            $"Тред ({PresenceWord(chat.Presence)}): узлов {chat.Nodes.Count}, вариантов ответа {choices}, концовок {ends}, старт «{chat.StartNodeId}».",
            chat.FreeTextReplyDeclared
                ? "Свободный ввод включён: ответ игрока идёт в отдельную строку."
                : "Свободный ввод не задан: игрок выбирает только готовые ответы."
        ];
    }

    /// <summary>
    /// Source-presence check: the runtime thread walker still reads the fields this
    /// projection relies on, and the hint path still reads its search target. It is
    /// not runtime proof of the semantics.
    /// </summary>
    public static IReadOnlyList<string> VerifyAgainstRuntime(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var problems = new List<string>();
        var path = Path.Combine(Path.GetFullPath(root), RuntimePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            problems.Add($"Нет файла {RuntimePath}: связь с потребителем не проверена.");
            return problems;
        }

        var text = File.ReadAllText(path);
        foreach (var field in new[] { "Presence", "StartNodeId", "FreeTextReply", "NextNodeId", "Requires", "TextId", "From", "Choices" })
        {
            if (!Regex.IsMatch(text, "\\." + field + "\\b"))
            {
                problems.Add($"Поле «{field}» больше не читается в {RuntimePath}: проекция устарела.");
            }
        }

        var hint = Path.Combine(Path.GetFullPath(root), HintRuntimePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(hint) && !Regex.IsMatch(File.ReadAllText(hint), "\\.PointsTo\\b"))
        {
            problems.Add($"Цель поиска подсказки больше не читается в {HintRuntimePath}: проекция устарела.");
        }

        return problems;
    }

    /// <summary>The same words the runtime shows in its thread list.</summary>
    private static string PresenceWord(string presence) => presence switch
    {
        "recently" => "был(а) недавно",
        "offline" => "не в сети",
        "oneself" => "записи Айдара",
        "" => "присутствие не задано",
        _ => $"«{presence}»"
    };

    private static string Label(JsonObject node) =>
        node["id"] is JsonValue value && value.TryGetValue<string>(out var id) && id.Length > 0 ? id : "без id";

    /// <summary>A string field the runtime requires, with the four cases told apart.</summary>
    private static string Required(JsonObject? owner, string name, string where, List<string> malformed, bool required = true)
    {
        if (owner is null || !owner.ContainsKey(name))
        {
            if (required)
            {
                malformed.Add($"У «{where}» нет поля «{name}».");
            }

            return "";
        }

        var node = owner[name];
        if (node is null)
        {
            malformed.Add($"У «{where}» поле «{name}» равно null.");
            return "";
        }

        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            malformed.Add($"У «{where}» поле «{name}» не строка.");
            return "";
        }

        if (text.Length == 0 && required)
        {
            malformed.Add($"У «{where}» поле «{name}» пустое.");
        }

        return text;
    }

    private static string Text(JsonNode? node, string where, List<string> malformed)
    {
        if (node is null)
        {
            return "";
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        malformed.Add($"Поле «{where}» не строка.");
        return "";
    }

    private static string? Nullable(JsonNode? node, string where, List<string> malformed)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text.Length > 0 ? text : null;
        }

        malformed.Add($"Поле «{where}» не строка.");
        return null;
    }

    private static IReadOnlyList<string> Strings(JsonNode? node)
    {
        var values = new List<string>();
        if (node is not JsonArray array)
        {
            return values;
        }

        foreach (var item in array)
        {
            if (item is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0)
            {
                values.Add(text);
            }
        }

        return values;
    }

    /// <summary>The condition operations of a rule list, each one named for its diagnostics.</summary>
    private static IReadOnlyList<string> Ops(JsonNode? node, string where, List<string> malformed)
    {
        var ops = new List<string>();
        if (node is null)
        {
            return ops;
        }

        if (node is not JsonArray rules)
        {
            malformed.Add($"Поле «{where}» не массив правил.");
            return ops;
        }

        for (var index = 0; index < rules.Count; index++)
        {
            if (rules[index] is not JsonObject rule)
            {
                malformed.Add($"Правило #{index + 1} в «{where}» не объект.");
                continue;
            }

            if (rule["op"] is JsonValue op && op.TryGetValue<string>(out var name) && name.Length > 0)
            {
                ops.Add(name);
            }
            else
            {
                malformed.Add($"Правило #{index + 1} в «{where}» без строкового op.");
            }
        }

        return ops;
    }

    /// <summary>Whether the JSON number is integral: the hint level is an integer in the schema.</summary>
    private static bool IsIntegral(JsonValue value)
    {
        if (value.TryGetValue<int>(out _) || value.TryGetValue<long>(out _))
        {
            return true;
        }

        if (value.TryGetValue<decimal>(out var precise))
        {
            return decimal.Truncate(precise) == precise;
        }

        if (value.TryGetValue<double>(out var number))
        {
            return double.IsFinite(number) && Math.Abs(number - Math.Round(number)) < 1e-9;
        }

        return value.TryGetValue<float>(out var single) && float.IsFinite(single) && Math.Abs(single - MathF.Round(single)) < 1e-6f;
    }

    private static double Number(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return 0d;
        }

        if (value.TryGetValue<double>(out var number))
        {
            return number;
        }

        if (value.TryGetValue<float>(out var single))
        {
            return single;
        }

        if (value.TryGetValue<int>(out var small))
        {
            return small;
        }

        return value.TryGetValue<long>(out var integer) ? integer : 0d;
    }
}
