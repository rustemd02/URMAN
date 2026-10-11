using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One complex-quest mechanism, what the engine offers for it, and its real use.</summary>
public sealed record QuestMechanismFacts(
    string Mechanism,
    bool EngineSupports,
    string EngineBasis,
    int AuthoredUses,
    string Note);

/// <summary>
/// The semantics half of the spec AI-37 slice. The unit asks for one branching/parallel
/// chain, one repeat/failure and one timer/delayed effect that keep state correctly. This
/// projection answers that question with the engine's own vocabulary and with the campaign's
/// own numbers, so a mechanism that the game CANNOT express is named as missing instead of
/// being quietly promised:
///
///   * the operation sets come from ContentRuleEngine, so a mechanism the engine gains or
///     loses changes this report;
///   * the authored counts come from the campaign, so an unused mechanism is visible as
///     unused rather than as done.
/// </summary>
public static class QuestSemantics
{
    public static IReadOnlyList<QuestMechanismFacts> Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (conditions, effects) = RuleCatalogCoverage.Executed(root);
        var authored = AuthoredOperations(workspace);

        var composition = new[] { "all", "any", "not" }.Where(conditions.Contains).ToArray();
        var retryModes = new List<string>();
        var objectives = 0;
        var failureEffects = 0;
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject quest || quest["stages"] is not JsonArray stages)
            {
                continue;
            }

            if (quest["retryPolicy"] is JsonObject policy && (string?)policy["mode"] is { Length: > 0 } mode)
            {
                retryModes.Add(mode);
            }

            foreach (var stage in stages.OfType<JsonObject>())
            {
                foreach (var objective in (stage["objectives"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    objectives++;
                    if (objective["failureEffects"] is JsonArray { Count: > 0 })
                    {
                        failureEffects++;
                    }
                }
            }
        }

        var timerOps = conditions.Concat(effects)
            .Where(op => op.Contains("time", StringComparison.OrdinalIgnoreCase)
                || op.Contains("wait", StringComparison.OrdinalIgnoreCase)
                || op.Contains("delay", StringComparison.OrdinalIgnoreCase)
                || op.Contains("timer", StringComparison.OrdinalIgnoreCase)
                || op.Contains("schedule", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return
        [
            new QuestMechanismFacts(
                "Ветвление",
                composition.Length > 0,
                composition.Length > 0 ? $"условия {string.Join(", ", composition)}" : "нет ни all/any/not",
                composition.Sum(op => authored.GetValueOrDefault(op)),
                "Ветка выражается составным условием, а не отдельной сущностью: движок исполняет all/any/not."),
            new QuestMechanismFacts(
                "Параллельность",
                objectives > 0,
                "независимые цели этапа со своими startConditions/completionConditions",
                objectives,
                "Отдельной конструкции параллельности нет: цели одного этапа продвигаются независимо, и это и есть параллельная цепочка в терминах движка."),
            new QuestMechanismFacts(
                "Повтор",
                retryModes.Count > 0,
                "retryPolicy.mode",
                retryModes.Count,
                retryModes.Count > 0
                    ? $"Режимы в кампании: {string.Join(", ", retryModes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))}. Отдельной операции «повторить» движок не имеет: повтор задаётся политикой цели/этапа."
                    : "Политики повтора нет ни у одного квеста."),
            new QuestMechanismFacts(
                "Провал",
                effects.Count > 0,
                "failureEffects у цели",
                failureEffects,
                failureEffects > 0
                    ? "Провал цели применяет свои последствия."
                    : "Ни одна цель не объявляет failureEffects: механизм поддержан движком, но в кампании не используется."),
            new QuestMechanismFacts(
                "Таймер/отложенное последствие",
                timerOps.Length > 0,
                timerOps.Length > 0 ? string.Join(", ", timerOps) : "в наборе операций нет ни одной временной",
                timerOps.Sum(op => authored.GetValueOrDefault(op)),
                "Требование таймера/отложенного последствия движок НЕ выражает: время не является условием и не запускает последствие. Пока в движке нет такой операции, это требование нельзя выполнить ни контентом, ни Studio — нужен новый механика-контракт у владельца фундамента.")
        ];
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var mechanisms = Inspect(workspace, root);
        var unsupported = mechanisms.Where(mechanism => !mechanism.EngineSupports).Select(mechanism => mechanism.Mechanism).ToArray();
        return
        [
            $"Требуемых механизмов: {mechanisms.Count}; поддержано движком: {mechanisms.Count(m => m.EngineSupports)}; не поддержано: {(unsupported.Length == 0 ? "нет" : string.Join(", ", unsupported))}.",
            "Основание каждой строки — наборы операций из исходников ContentRuleEngine и числа самой кампании, поэтому отчёт меняется вместе с движком и с контентом, а не с чьим-то обещанием.",
            unsupported.Length == 0
                ? "Все требуемые механизмы выразимы."
                : "Неподдержанный механизм — это не задача Studio: пока у него нет операции в движке, контент и интерфейс не могут его выполнить, и критерий единицы остаётся открытым.",
            "Новая операция — это изменение фундамента (движок и его состояние), то есть контракт владельца, а не шаг моего раздела."
        ];
    }

    /// <summary>How many times the campaign uses each operation, read from the authored quests.</summary>
    public static IReadOnlyDictionary<string, int> AuthoredOperations(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject quest)
            {
                continue;
            }

            Count(quest, counts);
        }

        return counts;
    }

    private static void Count(JsonNode? node, Dictionary<string, int> counts)
    {
        switch (node)
        {
            case JsonObject obj:
                if ((string?)obj["op"] is { Length: > 0 } op)
                {
                    counts[op] = counts.GetValueOrDefault(op) + 1;
                }

                foreach (var pair in obj)
                {
                    Count(pair.Value, counts);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Count(item, counts);
                }

                break;
        }
    }
    /// <summary>
    /// The three lifecycle policies every quest must declare, checked against what the reducer
    /// really does with them rather than against a guess:
    ///
    ///   * retryPolicy.mode "none" refuses a retry outright, "quest" restores the ORIGIN checkpoint
    ///     and any other value restores the current checkpoint; the attempt count is compared with
    ///     maximumAttempts and past it the reducer rejects with QuestRetryExhausted (lines 375-380);
    ///   * checkpointPolicy.mode "none" saves no checkpoint, "objective" saves one when an
    ///     objective completes, and any other value saves one when a stage is entered (lines 230,
    ///     289 and 341);
    ///   * all three policies are required by the reducer's own validation (lines 691-693), so a
    ///     missing one throws rather than being ignored.
    ///
    /// A declared maximumAttempts below one is reported because the mode and the number then
    /// contradict each other: the policy says a retry exists while the first attempt exhausts it.
    /// </summary>
    public static IReadOnlyList<string> PolicyProblems(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var problems = new List<string>();
        foreach (var quest in AuthoredQuests(workspace))
        {
            var id = (string?)quest["id"] ?? "";
            foreach (var field in new[] { "retryPolicy", "checkpointPolicy", "cancelPolicy" })
            {
                if (quest[field] is not JsonObject policy)
                {
                    problems.Add($"{id}: нет политики «{field}» — рантайм требует её через RequiredObject и упадёт при загрузке.");
                    continue;
                }

                if (field == "cancelPolicy")
                {
                    continue;
                }

                var mode = "";
                if (policy["mode"] is JsonValue modeValue && modeValue.TryGetValue<string>(out var text) && text is { Length: > 0 })
                {
                    mode = text;
                }
                else
                {
                    problems.Add($"{id}: у «{field}» нет mode — RequiredString бросит исключение при загрузке.");
                }

                if (field == "retryPolicy" && policy["maximumAttempts"] is { } attempts
                    && (!Number(attempts, out var value) || value < 1))
                {
                    problems.Add($"{id}: retryPolicy.maximumAttempts = {(attempts.ToJsonString())}, а режим «{mode}» обещает повтор: первая же попытка его исчерпает.");
                }
            }
        }

        return problems;
    }

    /// <summary>The policy configurations the campaign authors, so the distribution is visible.</summary>
    public static IReadOnlyList<string> PolicyNotes(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var configurations = AuthoredQuests(workspace)
            .GroupBy(quest => $"{(quest["retryPolicy"] as JsonObject)?["mode"]?.ToJsonString() ?? "?"}/{(quest["retryPolicy"] as JsonObject)?["maximumAttempts"]?.ToJsonString() ?? "?"} + {(quest["checkpointPolicy"] as JsonObject)?["mode"]?.ToJsonString() ?? "?"}", StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {group.Count()}");
        var problems = PolicyProblems(workspace);
        return
        [
            $"Наборы политик retry/checkpoint в кампании: {string.Join("; ", configurations)}.",
            problems.Count == 0
                ? "Каждая квеста объявляет все три политики, и числа не противоречат режимам."
                : $"Расхождений в политиках: {problems.Count} — {string.Join(" | ", problems.Take(2))}",
            "Движок не игнорирует политики: retryPolicy.mode и maximumAttempts сравниваются в QuestLifecycleReducer (375-380), checkpointPolicy.mode управляет снимками (230, 289, 341), а все три обязательны при загрузке (691-693).",
            "Идёт ли повтор на самом деле — это runtime-проверка, а не вывод из объявления."
        ];
    }

    /// <summary>The quest shapes in the workspace, by the same test the rest of this file uses.</summary>
    private static IReadOnlyList<JsonObject> AuthoredQuests(StudioWorkspace workspace)
    {
        var quests = new List<JsonObject>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is JsonObject quest && quest["stages"] is JsonArray)
            {
                quests.Add(quest);
            }
        }

        return quests;
    }

    /// <summary>Reads a number without assuming its JSON type, so a wrong type is reported.</summary>
    private static bool Number(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue json)
        {
            return false;
        }

        if (json.TryGetValue<double>(out value))
        {
            return true;
        }

        if (json.TryGetValue<int>(out var small))
        {
            value = small;
            return true;
        }

        if (json.TryGetValue<long>(out var integer))
        {
            value = integer;
            return true;
        }

        return json.TryGetValue<decimal>(out var precise) && (value = (double)precise) == value;
    }

}
