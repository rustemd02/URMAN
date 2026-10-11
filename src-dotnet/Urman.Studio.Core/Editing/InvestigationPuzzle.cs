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
            if (workspace.Get(id) is not JsonObject step || AuthoredJson.Text(step, "kind") is not ("contradiction" or "hypothesis"))
            {
                continue;
            }

            var kind = AuthoredJson.TextOrNull(step, "kind") ?? "";
            var status = AuthoredJson.TextOrNull(step, "initialStatus") ?? "";
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
    /// <summary>
    /// The narrative-pressure counter, mirrored from ContentRuleEngine's "pressure.change" arm. The
    /// engine reads the delta with GetValue&lt;int&gt; and throws when it is not an integer, then clamps
    /// the result into 0..3, so a delta whose magnitude exceeds the whole range can never be applied
    /// as written: the author asks for more change than the counter can hold, and the clamp hides it.
    ///
    /// The authored knowledge record with kind "pressure" is NOT this counter: it is a knowledge
    /// entry the player can read, while the counter is a number the effects move. Reporting them as
    /// one thing would be wrong, so the notes say which is which.
    /// </summary>
    public static IReadOnlyList<string> PressureGaps(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var gaps = new List<string>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not { } entity)
            {
                continue;
            }

            var pending = new Stack<JsonNode?>();
            pending.Push(entity);
            while (pending.Count > 0)
            {
                switch (pending.Pop())
                {
                    case JsonObject obj:
                        if (AuthoredJson.Text(obj, "op") == "pressure.change")
                        {
                            var where = AuthoredJson.Text(obj, "$where") is { Length: > 0 } scope ? scope : id;
                            if (obj["delta"] is not JsonValue delta || !delta.TryGetValue<int>(out var value))
                            {
                                gaps.Add($"{where}: pressure.change без целого delta — движок бросает ArgumentException «pressure.change delta must be an integer».");
                            }
                            else if (value > 3 || value < -3)
                            {
                                gaps.Add($"{where}: pressure.change delta = {value}, а счётчик ограничен 0..3 и молча зажимается: такое изменение НИКОГДА не применится целиком, и автор увидит меньше, чем просил.");
                            }
                        }

                        foreach (var pair in obj)
                        {
                            pending.Push(pair.Value);
                        }

                        break;
                    case JsonArray array:
                        foreach (var item in array)
                        {
                            pending.Push(item);
                        }

                        break;
                }
            }
        }

        return gaps;
    }

    /// <summary>What the pressure counter is, and what it is not.</summary>
    public static IReadOnlyList<string> PressureNotes(StudioWorkspace workspace, string root)
    {
        var gaps = PressureGaps(workspace);
        var changes = 0;
        var entries = 0;
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is JsonObject entity)
            {
                if (AuthoredJson.Text(entity, "kind") == "pressure")
                {
                    entries++;
                }

                var pending = new Stack<JsonNode?>();
                pending.Push(entity);
                while (pending.Count > 0)
                {
                    switch (pending.Pop())
                    {
                        case JsonObject obj:
                            if (AuthoredJson.Text(obj, "op") == "pressure.change")
                            {
                                changes++;
                            }

                            foreach (var pair in obj)
                            {
                                pending.Push(pair.Value);
                            }

                            break;
                        case JsonArray array:
                            foreach (var item in array)
                            {
                                pending.Push(item);
                            }

                            break;
                    }
                }
            }
        }

        return
        [
            $"Изменений счётчика давления в контенте: {changes}; записей знания с видом «pressure»: {entries}.",
            gaps.Count == 0
                ? "Каждое изменение давления — целое и укладывается в диапазон счётчика 0..3."
                : $"Проблем с давлением: {gaps.Count} — {string.Join(" | ", gaps.Take(2))}",
            "Счётчик давления и запись знания с видом «pressure» — РАЗНЫЕ вещи: счётчик двигают эффекты (pressure.change, диапазон 0..3), а запись знания игрок читает. Смешивать их нельзя, и проекция их не смешивает.",
            "Как давление ощущается игроком — слуховая и визуальная приёмка, не эта проверка."
        ];
    }

    /// <summary>
    /// The beat checks, the mirror of the fact-gate check: a beat id is a RUNTIME state key that an
    /// effect creates, so a condition on a beat nobody ever writes can never become true. Verified
    /// from the engine: beat.set-state writes into the beats map by beatId (ContentRuleEngine lines
    /// 75-76) and beat.state reads it back (line 262). The reverse case - a beat written and never
    /// read - is REPORTED and not judged, because a beat may exist for the journal or for a later
    /// act that is not authored yet.
    /// </summary>
    public static IReadOnlyList<string> BeatGaps(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (writes, reads) = Beats(workspace);
        if (reads.Count == 0 && writes.Count == 0)
        {
            return ["Сюжетных моментов не найдено: сверять нечего, и пустой ответ здесь НЕ означает, что условия верны."];
        }

        return reads.Where(beat => !writes.Contains(beat))
            .Select(beat => $"Условие beat.state читает момент «{beat}», который НИКТО не записывает через beat.set-state: условие не станет истинным никогда, и это не ошибка автора в опечатке, а отсутствующий шаг сюжета.")
            .ToArray();
    }

    /// <summary>What the beat graph looks like today.</summary>
    public static IReadOnlyList<string> BeatNotes(StudioWorkspace workspace)
    {
        var (writes, reads) = Beats(workspace);
        var writeOnly = writes.Where(beat => !reads.Contains(beat)).Order(StringComparer.Ordinal).ToArray();
        var gaps = BeatGaps(workspace);
        return
        [
            $"Сюжетных моментов: записывается {writes.Count}, читается {reads.Count}; записаны, но не прочитаны: {writeOnly.Length}.",
            gaps.Count == 0 || gaps[0].StartsWith("Сюжетных моментов не найдено", StringComparison.Ordinal)
                ? "Каждое условие по моменту имеет запись: недостижимых условий нет."
                : $"Недостижимых условий по моментам: {gaps.Count} — {string.Join(" | ", gaps.Take(2))}",
            writeOnly.Length == 0
                ? "Каждый записанный момент кем-то читается."
                : $"Записаны и не прочитаны: {string.Join(", ", writeOnly.Take(6))}{(writeOnly.Length > 6 ? " …" : "")}. Это НЕ дефект: момент может существовать для журнала или для будущего акта — решает автор.",
            "Момент — это ключ состояния, который создаёт эффект, а не авторская запись с id: поэтому «нет объявления» здесь нормально, а «нет записи» — нет."
        ];
    }

    /// <summary>The beat ids the content writes and reads.</summary>
    public static (IReadOnlySet<string> Writes, IReadOnlySet<string> Reads) Beats(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var writes = new HashSet<string>(StringComparer.Ordinal);
        var reads = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not { } entity)
            {
                continue;
            }

            var pending = new Stack<JsonNode?>();
            pending.Push(entity);
            while (pending.Count > 0)
            {
                switch (pending.Pop())
                {
                    case JsonObject obj:
                        if (obj["op"] is JsonValue op && op.TryGetValue<string>(out var operation)
                            && obj["beatId"] is JsonValue beat && beat.TryGetValue<string>(out var beatId)
                            && beatId is { Length: > 0 })
                        {
                            if (operation == "beat.set-state")
                            {
                                writes.Add(beatId);
                            }
                            else if (operation == "beat.state")
                            {
                                reads.Add(beatId);
                            }
                        }

                        foreach (var pair in obj)
                        {
                            pending.Push(pair.Value);
                        }

                        break;
                    case JsonArray array:
                        foreach (var item in array)
                        {
                            pending.Push(item);
                        }

                        break;
                }
            }
        }

        return (writes, reads);
    }

}
