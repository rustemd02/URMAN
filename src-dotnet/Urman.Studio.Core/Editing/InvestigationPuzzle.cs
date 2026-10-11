using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored puzzle step (a contradiction or a hypothesis) and its links.</summary>
public sealed record PuzzleStepFacts(
    string StepId,
    string Kind,
    string InitialStatus,
    int Sources,
    int Reveals,
    int Contradicts,
    int Unlocks,
    IReadOnlyList<string> Problems);

/// <summary>
/// The puzzle half of the spec AI-25 slice. The investigation is built from authored steps:
/// a contradiction names the sources it is drawn from, what it contradicts, what it reveals
/// and what it unlocks, and a hypothesis is the same shape with its own vocabulary. Every one
/// of those is a reference into the same content, so a link that does not resolve is a step
/// that can never be taken, and an initial status the engine does not know is silently the
/// lowest rank instead of an error:
///
///   * ContentRuleEngine ranks vocabulary statuses as confirmed 2, guessed 1 and EVERYTHING
///     ELSE 0, so a misspelled status behaves as hidden without a word;
///   * a contradiction with no source or with nothing to contradict is not a puzzle step.
/// </summary>
public static class InvestigationPuzzle
{
    public const string EnginePath = "src-dotnet/Urman.Core/Narrative/ContentRuleEngine.cs";

    /// <summary>The statuses the engine really ranks, read from its own vocabulary rank.</summary>
    public static readonly IReadOnlyList<string> Statuses = ["hidden", "guessed", "confirmed"];

    public static IReadOnlyList<PuzzleStepFacts> Inspect(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var steps = new List<PuzzleStepFacts>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject step || (string?)step["kind"] is not ("contradiction" or "hypothesis"))
            {
                continue;
            }

            var kind = (string?)step["kind"] ?? "";
            var status = (string?)step["initialStatus"] ?? "";
            var sources = References(step["sourceIds"]);
            var reveals = References(step["reveals"]);
            var contradicts = References(step["contradicts"]);
            var unlocks = References(step["unlocks"]);
            var problems = new List<string>();

            if (status.Length == 0)
            {
                problems.Add("Нет initialStatus: движок возьмёт самый низкий ранг молча.");
            }
            else if (!Statuses.Contains(status, StringComparer.Ordinal))
            {
                problems.Add($"initialStatus «{status}» движок не ранжирует: он читается как самый низкий ранг (VocabularyRank: confirmed 2, guessed 1, всё остальное 0), то есть шаг молча спрятан.");
            }

            foreach (var (name, list) in new[] { ("sourceIds", sources), ("reveals", reveals), ("contradicts", contradicts), ("unlocks", unlocks) })
            {
                foreach (var reference in list)
                {
                    if (workspace.Locate(reference) is null)
                    {
                        problems.Add($"Ссылка «{reference}» из {name} не найдена в проекте: шаг не сможет сработать.");
                    }
                }
            }

            if (kind == "contradiction" && sources.Count == 0)
            {
                problems.Add("У противоречия нет sourceIds: неясно, что именно противоречит.");
            }

            if (kind == "contradiction" && contradicts.Count == 0)
            {
                problems.Add("Противоречие ничего не опровергает: это не шаг расследования.");
            }

            // A hypothesis states a possibility rather than opening something, and the
            // authored content confirms that is the normal shape: most hypotheses carry only
            // sources. Requiring reveals of them would have been my rule, not the game's, so
            // only a CONTRADICTION is held to it.
            if (kind == "contradiction" && reveals.Count == 0)
            {
                problems.Add("Противоречие ничего не раскрывает: как шаг расследования оно ничего не открывает игроку.");
            }

            steps.Add(new PuzzleStepFacts(id, kind, status, sources.Count, reveals.Count, contradicts.Count, unlocks.Count, problems));
        }

        return steps.OrderBy(step => step.StepId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace)
    {
        var steps = Inspect(workspace);
        var contradictions = steps.Count(step => step.Kind == "contradiction");
        var hypotheses = steps.Count(step => step.Kind == "hypothesis");
        var references = steps.Sum(step => step.Sources + step.Reveals + step.Contradicts + step.Unlocks);
        return
        [
            $"Шагов расследования: {steps.Count} (противоречий {contradictions}, гипотез {hypotheses}); ссылок в них: {references}.",
            $"Статусы, которые движок ранжирует: {string.Join(", ", Statuses)}; все авторские шаги используют {string.Join(", ", steps.Select(step => step.InitialStatus).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))}.",
            "Проверяется разрешимость каждой ссылки, а от противоречия ещё и то, что оно называет источник и хоть что-то раскрывает. Гипотеза — другое: она формулирует возможность, поэтому отсутствие reveals/unlocks у неё нормально (так и написано в контенте), и требовать их было бы моим правилом, а не правилом игры.",
            "Здесь проверяется связность головоломки, а не её увлекательность: интерес и язык остаются авторской и языковой приёмкой."
        ];
    }

    private static IReadOnlyList<string> References(JsonNode? node) =>
        (node as JsonArray ?? []).OfType<JsonValue>()
            .Select(value => value.GetValue<string>())
            .Where(reference => reference.Length > 0)
            .ToArray();
}
