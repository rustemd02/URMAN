using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored node of a PC thread, as the runtime walks it.</summary>
public sealed record PcChatNode(
    string Id,
    string From,
    string TextId,
    string? NextNodeId,
    IReadOnlyList<(string TextId, string? NextNodeId)> Choices,
    IReadOnlyList<string> Reveals);

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
    bool IsHint);

/// <summary>
/// The PC thread as the runtime reads it: presence, start node, node graph and the
/// lines each node resolves. Everything here is a read-only projection plus the
/// checks an author needs; the text ids stay the single identity, so jumping from a
/// node to its line is navigation, not a second editor.
/// </summary>
public static class PcChat
{
    public const string RuntimePath = "game/scripts/OldPcUi.Chat.cs";

    public static PcChatContent Parse(string entityId, JsonObject? entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var nodes = new List<PcChatNode>();
        if (entity?["nodes"] is JsonArray authored)
        {
            foreach (var node in authored.OfType<JsonObject>())
            {
                var choices = new List<(string TextId, string? NextNodeId)>();
                foreach (var choice in (node["choices"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    choices.Add((Text(choice["textId"]), Nullable(choice["nextNodeId"])));
                }

                nodes.Add(new PcChatNode(
                    Text(node["id"]),
                    Text(node["from"]),
                    Text(node["textId"]),
                    Nullable(node["nextNodeId"]),
                    choices,
                    (node["reveals"] as JsonArray ?? []).Select(Text).Where(value => value.Length > 0).ToArray()));
            }
        }

        var requires = (entity?["requires"] as JsonArray ?? []).OfType<JsonObject>().Select(rule => Text(rule["op"])).Where(op => op.Length > 0).ToArray();
        var isHint = entityId.Contains(":hint/", StringComparison.Ordinal);
        return new PcChatContent(
            entityId,
            Text(entity?["presence"]),
            Text(entity?["startNodeId"]),
            Text(entity?["freeTextReply"]),
            entity?["freeTextReply"] is not null,
            requires,
            nodes,
            (int)Number(entity?["level"]),
            isHint);
    }

    /// <summary>Everything that would leave the player with a broken or invisible thread.</summary>
    public static IReadOnlyList<string> Validate(StudioWorkspace workspace, PcChatContent chat)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(chat);
        var problems = new List<string>();
        foreach (var op in chat.Requires)
        {
            if (ConditionPhrases.Kind(op) is null)
            {
                problems.Add($"Требование «{op}» игра не исполняет: подсказка или тред может не появиться.");
            }
        }

        if (chat.IsHint)
        {
            return problems;
        }

        if (chat.Nodes.Count == 0)
        {
            problems.Add("У треда нет ни одного узла: открывать нечего.");
            return problems;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in chat.Nodes)
        {
            if (node.Id.Length == 0)
            {
                problems.Add("У узла нет id: на него нельзя сослаться.");
            }
            else if (!ids.Add(node.Id))
            {
                problems.Add($"Узел «{node.Id}» объявлен дважды: вторая ветка перекроет первую.");
            }
        }

        if (chat.StartNodeId.Length == 0 || !ids.Contains(chat.StartNodeId))
        {
            problems.Add($"Стартовый узел «{chat.StartNodeId}» не найден: рантайм откроет тред и не сможет показать ни одной строки.");
        }

        foreach (var node in chat.Nodes)
        {
            foreach (var (text, target) in Targets(node))
            {
                if (target is { Length: > 0 } && !ids.Contains(target))
                {
                    problems.Add($"Узел «{node.Id}» ведёт в «{target}», которого нет: ветка оборвётся.");
                }

                if (text is { Length: > 0 } && workspace.Locate(text) is null)
                {
                    problems.Add($"Строка «{text}» узла «{node.Id}» не найдена в проекте: игрок увидит пустоту.");
                }
            }

            if (node.TextId.Length > 0 && workspace.Locate(node.TextId) is null)
            {
                problems.Add($"Реплика «{node.TextId}» узла «{node.Id}» не найдена в проекте: игрок увидит пустоту.");
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

    /// <summary>Nodes the runtime can never reach from the authored start node.</summary>
    public static IReadOnlyList<string> Unreachable(PcChatContent chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        if (chat.Nodes.Count == 0 || chat.StartNodeId.Length == 0)
        {
            return [];
        }

        // Duplicate ids are one of the problems Validate reports, so this walk must
        // survive them: keep the first declaration instead of throwing.
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

            foreach (var (_, target) in Targets(node))
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
            return [$"Подсказка уровня {chat.Level}: {when}."];
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
    /// Checks that the fields this projection relies on are still read by the
    /// runtime thread walker, so the outline cannot drift away from the game.
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
        foreach (var field in new[] { "Presence", "StartNodeId", "FreeTextReply", "NextNodeId" })
        {
            if (!Regex.IsMatch(text, "\\." + field + "\\b"))
            {
                problems.Add($"Поле «{field}» больше не читается в {RuntimePath}: проекция устарела.");
            }
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

    private static IEnumerable<(string? TextId, string? NextNodeId)> Targets(PcChatNode node)
    {
        yield return (node.TextId, node.NextNodeId);
        foreach (var (text, target) in node.Choices)
        {
            yield return (text, target);
        }
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static string? Nullable(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

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
