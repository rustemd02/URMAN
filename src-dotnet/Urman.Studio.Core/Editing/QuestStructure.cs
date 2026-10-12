using Urman.Core.Quests;
using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored quest, checked as a structure the runtime can run.</summary>
public sealed record QuestFacts(
    string QuestId,
    int Stages,
    int Objectives,
    IReadOnlyList<string> Problems);

/// <summary>
/// The second slice of the quest-editor half of the spec AI-19 slice. A quest is a structure,
/// not a paragraph: stages hold objectives, every objective carries start and completion
/// conditions, completion and failure effects, and the quest carries its outcomes and its
/// retry, checkpoint and cancellation policies. Two things are checkable on the sources and
/// both fail silently at runtime:
///
///   * an objective whose title text or whose referenced entity (a knowledge fact, a beat, a
///     vocabulary word, a character) does not exist - the condition can never become true, so
///     the objective never starts or never completes;
///   * a rule whose operation the ContentRuleEngine does not execute - the engine simply
///     never evaluates it.
///
/// The executed sets are read from the engine sources, so this projection cannot drift from
/// the engine, and every reference is resolved through the same workspace the editor uses.
/// </summary>
public static class QuestStructure
{
    /// <summary>
    /// The reference fields a rule op can carry that must name an AUTHORED entity. A beat
    /// is deliberately absent: "beat.set-state" writes into the runtime's beats map
    /// (SetMapValue) and "beat.state" simply reads false when the beat was never set, so a
    /// beat id that exists nowhere in the content is normal, not a defect.
    /// </summary>
    private static readonly string[] ReferenceFields = ["knowledgeId", "vocabularyId", "characterId", "questId"];

    public static IReadOnlyList<QuestFacts> Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (executedConditions, executedEffects) = RuleCatalogCoverage.Executed(root);
        var quests = new List<QuestFacts>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject quest || quest["stages"] is not JsonArray stages)
            {
                continue;
            }

            var problems = new List<string>();
            CheckText(workspace, quest["titleTextId"], $"квест «{id}» заголовок", problems);

            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
            var objectives = 0;
            for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
            {
                if (stages[stageIndex] is not JsonObject stage)
                {
                    problems.Add($"Этап #{stageIndex + 1} не объект: движок его не увидит.");
                    continue;
                }

                var stageId = AuthoredJson.Text(stage, "id");
                if (stageId.Length == 0)
                {
                    problems.Add($"Этап #{stageIndex + 1} без id.");
                }
                else if (!stageIds.Add(stageId))
                {
                    problems.Add($"Этап «{stageId}» повторяется: движок различит их только по порядку.");
                }

                if (stage["objectives"] is not JsonArray declared || declared.Count == 0)
                {
                    problems.Add($"У этапа «{stageId}» нет ни одной цели: этап нельзя ни начать, ни завершить.");
                    continue;
                }

                for (var objectiveIndex = 0; objectiveIndex < declared.Count; objectiveIndex++)
                {
                    if (declared[objectiveIndex] is not JsonObject objective)
                    {
                        problems.Add($"Цель #{objectiveIndex + 1} этапа «{stageId}» не объект.");
                        continue;
                    }

                    objectives++;
                    var objectiveId = AuthoredJson.Text(objective, "id");
                    var where = objectiveId.Length > 0 ? $"цель «{objectiveId}»" : $"цель #{objectiveIndex + 1} этапа «{stageId}»";
                    if (objectiveId.Length == 0)
                    {
                        problems.Add($"{where} без id.");
                    }
                    else if (!objectiveIds.Add(objectiveId))
                    {
                        problems.Add($"Цель «{objectiveId}» повторяется внутри квеста: их нельзя различить по id.");
                    }

                    CheckText(workspace, objective["titleTextId"], $"{where}, заголовок", problems);
                    CheckRules(workspace, objective["startConditions"] as JsonArray, executedConditions, $"{where}, startConditions", true, problems);
                    CheckRules(workspace, objective["completionConditions"] as JsonArray, executedConditions, $"{where}, completionConditions", true, problems);
                    CheckRules(workspace, objective["completionEffects"] as JsonArray, executedEffects, $"{where}, completionEffects", false, problems);
                    CheckRules(workspace, objective["failureEffects"] as JsonArray, executedEffects, $"{where}, failureEffects", false, problems);
                }
            }

            if (quest["cancelPolicy"] is JsonObject cancel)
            {
                CheckRules(workspace, cancel["effects"] as JsonArray, executedEffects, "cancelPolicy.effects", false, problems);
            }

            var modes = new List<string>();
            foreach (var (name, expected) in new[]
            {
                ("retryPolicy", new[] { "none", "stage", "objective" }),
                ("checkpointPolicy", new[] { "none", "stage", "objective" })
            })
            {
                if (quest[name] is JsonObject policy)
                {
                    var mode = AuthoredJson.Text(policy, "mode");
                    modes.Add($"{name}={mode}");
                    if (mode.Length > 0 && !expected.Contains(mode, StringComparer.Ordinal))
                    {
                        problems.Add($"{name}.mode «{mode}» не поддерживается: движок понимает {string.Join(", ", expected)}.");
                    }
                }
            }

            // Stage transitions are the one part of a quest the runtime validates itself, and the
            // COMPILER calls exactly this method (ContentCompiler -> QuestLifecycleReducer.
            // TransitionProblems), so Studio reuses it instead of writing a second set of rules that
            // could drift. It covers a stage or transition id defined twice, a transition naming an
            // unknown objective, and a transition targeting an unknown stage.
            foreach (var problem in QuestLifecycleReducer.TransitionProblems(stages))
            {
                problems.Add($"{id}: {problem}");
            }

            quests.Add(new QuestFacts(id, stages.Count, objectives, problems));
        }

        return quests.OrderBy(quest => quest.QuestId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var quests = Inspect(workspace, root);
        var (conditions, effects) = RuleCatalogCoverage.Executed(root);
        return
        [
            $"Квестов: {quests.Count}; этапов: {quests.Sum(q => q.Stages)}; целей: {quests.Sum(q => q.Objectives)}. Движок исполняет условий {conditions.Count}, последствий {effects.Count}.",
            "Проверено: у квеста и каждой цели найден текст заголовка, этапы и цели не повторяют id, у этапа есть хотя бы одна цель, а каждая ссылка внутри условия или последствия на АВТОРСКУЮ сущность (knowledgeId, vocabularyId, characterId, questId) существует в проекте — иначе условие никогда не станет истинным.",
            "Ссылка на beat намеренно не проверяется: beat — это состояние рантайма, «beat.set-state» записывает его в карту движка, а «beat.state» до первой записи просто читает false, поэтому beat с любым id допустим.",
            "Политики retry/checkpoint сверяются со значениями, которые понимает движок; cancelPolicy.effects проверяются теми же наборами операций.",
            "Замысла квеста это не проверяет: интересность, темп и язык остаются авторской и языковой приёмкой."
        ];
    }

    private static void CheckText(StudioWorkspace workspace, JsonNode? node, string where, List<string> problems)
    {
        if (AuthoredJson.TextValue(node) is not { Length: > 0 } textId)
        {
            return;
        }

        if (workspace.Locate(textId) is null)
        {
            problems.Add($"{where}: текста «{textId}» нет в проекте — игрок увидит пустую строку.");
        }
    }

    private static void CheckRules(StudioWorkspace workspace, JsonArray? rules, IReadOnlySet<string> executed, string where, bool isCondition, List<string> problems)
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

            var op = AuthoredJson.Text(rule, "op");
            if (op.Length == 0)
            {
                problems.Add($"{where}: правило #{index + 1} без строкового op.");
                continue;
            }

            if (executed.Count > 0 && !executed.Contains(op))
            {
                problems.Add(isCondition
                    ? $"{where}: условие «{op}» движок не исполняет — цель не начнётся или не завершится."
                    : $"{where}: последствие «{op}» движок не исполняет — мир не изменится.");
            }

            foreach (var field in ReferenceFields)
            {
                if (AuthoredJson.Text(rule, field) is not { Length: > 0 } reference)
                {
                    continue;
                }

                if (workspace.Locate(reference) is null)
                {
                    problems.Add($"{where}: правило «{op}» ссылается на «{reference}» ({field}), которого нет в проекте — условие никогда не станет истинным.");
                }
            }
        }
    }
}
