using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored dialogue, checked as a graph the engine can actually walk.</summary>
public sealed record DialogueFacts(
    string DialogueId,
    int Nodes,
    int Choices,
    IReadOnlyList<string> Problems);

/// <summary>
/// The first real slice of the dialogue-answers half of the spec AI-24 slice. An authored
/// dialogue is a graph: a start node, nodes with conditions and effects, and choices that
/// point at the next node. Two failures are silent in a running game and are therefore
/// worth catching on the sources:
///
///   * a choice whose nextNodeId is not a node of this dialogue - the answer exists in the
///     data and cannot be taken;
///   * a condition or an effect whose operation the ContentRuleEngine does not execute -
///     the editor would show a rule the game never evaluates, so the branch is
///     unreachable or the world never changes.
///
/// The executed sets are read from the engine sources, so this projection cannot drift
/// from the engine the way a hardcoded list would.
/// </summary>
public static class DialogueGraph
{
    public const string SchemasPath = "content/schemas/dialogue.schema.json";

    public static IReadOnlyList<DialogueFacts> Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (executedConditions, executedEffects) = RuleCatalogCoverage.Executed(root);
        var dialogues = new List<DialogueFacts>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject dialogue || dialogue["nodes"] is not JsonArray nodes)
            {
                continue;
            }

            // A PC chat has nodes and a start node too, but it is a different record type
            // with its own schema: the project's id convention separates them, and treating
            // a chat as a dialogue would report roles the chat schema never required.
            if (!id.Contains(":dialogue/", StringComparison.Ordinal))
            {
                continue;
            }

            var problems = new List<string>();
            var roles = (dialogue["participantRoles"] as JsonArray)?.OfType<JsonValue>()
                .Select(role => role.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
            if (roles.Count == 0)
            {
                problems.Add("У диалога не объявлено ни одной роли участника: рантайм не сможет выбрать говорящего.");
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.OfType<JsonObject>())
            {
                if (AuthoredJson.Text(node, "id") is { Length: > 0 } nodeId)
                {
                    nodeIds.Add(nodeId);
                }
            }

            var start = AuthoredJson.TextOrNull(dialogue, "startNodeId") ?? "";
            if (start.Length == 0)
            {
                problems.Add("У диалога нет startNodeId: разговор не с чего начать.");
            }
            else if (!nodeIds.Contains(start))
            {
                problems.Add($"startNodeId «{start}» не найден среди узлов диалога: разговор не начнётся.");
            }

            var choices = 0;
            foreach (var node in nodes.OfType<JsonObject>())
            {
                var where = AuthoredJson.TextOrNull(node, "id") ?? "?";
                CheckRole(node, roles, where, problems);
                CheckRules(node["conditions"] as JsonArray, executedConditions, $"узел «{where}», conditions", problems, isCondition: true);
                CheckRules(node["effects"] as JsonArray, executedEffects, $"узел «{where}», effects", problems, isCondition: false);
                CheckText(workspace, node, $"узел «{where}»", problems);

                foreach (var choice in (node["choices"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    choices++;
                    var choiceId = AuthoredJson.TextOrNull(choice, "id") ?? "?";
                    var choiceWhere = $"ответ «{choiceId}» в узле «{where}»";
                    CheckRole(choice, roles, choiceWhere, problems);
                    CheckText(workspace, choice, choiceWhere, problems);
                    CheckRules(choice["conditions"] as JsonArray, executedConditions, $"{choiceWhere}, conditions", problems, isCondition: true);
                    CheckRules(choice["effects"] as JsonArray, executedEffects, $"{choiceWhere}, effects", problems, isCondition: false);

                    if (choice["nextNodeId"] is { } next && AuthoredJson.TextValue(next) is { Length: > 0 } target && !nodeIds.Contains(target))
                    {
                        problems.Add($"{choiceWhere}: nextNodeId «{target}» нет среди узлов диалога — ответ существует, но его нельзя выбрать.");
                    }
                }
            }

            dialogues.Add(new DialogueFacts(id, nodes.Count, choices, problems));
        }

        return dialogues.OrderBy(dialogue => dialogue.DialogueId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var dialogues = Inspect(workspace, root);
        var (conditions, effects) = RuleCatalogCoverage.Executed(root);
        var nodes = dialogues.Sum(dialogue => dialogue.Nodes);
        var choices = dialogues.Sum(dialogue => dialogue.Choices);
        return
        [
            $"Диалогов: {dialogues.Count}; узлов: {nodes}; ответов: {choices}. Движок исполняет условий {conditions.Count}, последствий {effects.Count}.",
            "Проверено как граф: startNodeId и каждый nextNodeId указывают на существующий узел диалога, говорящий у каждого узла и ответа объявлен в participantRoles, текст каждой реплики найден в проекте, а каждое условие и последствие названо операцией, которую ContentRuleEngine действительно исполняет.",
            "Наборы операций читаются из исходников движка, поэтому проекция не может разойтись с ним, как разошёлся бы захардкоженный список.",
            "Как разговор звучит и читается — вопрос языка, культуры и слуха, а не этой проверки.",
            "Записи тредов ПК (urman.oldpc:chat/*) в этот счёт не входят: у них своя схема без participantRoles, и смешивать их с диалогами нельзя."
        ];
    }

    private static void CheckRole(JsonObject node, IReadOnlySet<string> roles, string where, List<string> problems)
    {
        if (AuthoredJson.Text(node, "speakerRole") is not { Length: > 0 } role)
        {
            return;
        }

        if (roles.Count > 0 && !roles.Contains(role))
        {
            problems.Add($"{where}: говорящий «{role}» не объявлен в participantRoles диалога — рантайм не найдёт участника.");
        }
    }

    private static void CheckText(StudioWorkspace workspace, JsonObject node, string where, List<string> problems)
    {
        if (AuthoredJson.Text(node, "textId") is not { Length: > 0 } textId)
        {
            return;
        }

        if (workspace.Locate(textId) is null)
        {
            problems.Add($"{where}: текста «{textId}» нет в проекте — игрок увидит пустую строку.");
        }
    }

    private static void CheckRules(JsonArray? rules, IReadOnlySet<string> executed, string where, List<string> problems, bool isCondition)
    {
        if (rules is null)
        {
            return;
        }

        for (var index = 0; index < rules.Count; index++)
        {
            if (rules[index] is not JsonObject rule)
            {
                problems.Add($"{where}: правило #{index + 1} не объект.");
                continue;
            }

            var op = AuthoredJson.TextOrNull(rule, "op") ?? "";
            if (op.Length == 0)
            {
                problems.Add($"{where}: правило #{index + 1} без строкового op.");
                continue;
            }

            if (executed.Count > 0 && !executed.Contains(op))
            {
                problems.Add(isCondition
                    ? $"{where}: условие «{op}» движок не исполняет — ветка никогда не сработает."
                    : $"{where}: последствие «{op}» движок не исполняет — мир не изменится.");
            }
        }
    }
}
