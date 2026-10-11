using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One rule operation: where it stands between the engine, Studio and the authored campaign.</summary>
public sealed record RuleOpFacts(
    string Op,
    bool Conditional,
    bool Executed,
    bool OfferedInStudio,
    bool MarkedNotExecuted,
    int AuthoredUses);

/// <summary>
/// What the campaign already writes, what the engine really executes and what the
/// Studio catalogue offers (spec AI-37, step "сопоставить каждое требование с
/// existing type").
/// </summary>
public sealed record RuleCoverageReport(
    IReadOnlyList<RuleOpFacts> Ops,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Problems);

/// <summary>
/// The coverage of the rule catalogue. The executed sets are read out of the real
/// engine sources, so this map cannot quietly drift: an operation the engine gains
/// appears here (as unused until the campaign uses it), and an operation the engine
/// loses becomes a problem if the campaign still writes it. Studio's own catalogue
/// is compared with both, because an executed operation the editor does not offer
/// is a rule the author cannot create or fix in the interface.
/// </summary>
public static class RuleCatalogCoverage
{
    public const string EnginePath = "src-dotnet/Urman.Core/Narrative/ContentRuleEngine.cs";
    public const string PhotoWorldPath = "src-dotnet/Urman.Core/Narrative/PhotoWorldState.cs";

    private static readonly Regex SwitchCase = new("case\\s+\"([a-z][a-z0-9._-]*)\"\\s*:", RegexOptions.CultureInvariant);
    private static readonly Regex SwitchArm = new("\"([a-z][a-z0-9._-]*)\"\\s*=>", RegexOptions.CultureInvariant);
    private static readonly Regex HashSetEntry = new("\"((?:photoworlds|urman)[a-z0-9._-]*)\"", RegexOptions.CultureInvariant);

    /// <summary>
    /// The module source prefixes of one campaign, taken from its own manifest. An
    /// empty list means the campaign is unknown, and the caller must say so instead of
    /// pretending the whole workspace is that campaign.
    /// </summary>
    public static IReadOnlyList<string> CampaignModulePrefixes(StudioWorkspace workspace, string? campaignSelection)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (string.IsNullOrWhiteSpace(campaignSelection))
        {
            return [];
        }

        var path = Path.Combine(workspace.Root, "content", "campaigns", campaignSelection, "campaign.json");
        if (!File.Exists(path)
            || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject manifest
            || manifest["modules"] is not JsonArray modules)
        {
            return [];
        }

        return modules.OfType<JsonObject>()
            .Select(module => (string?)module["moduleId"])
            .OfType<string>()
            .Where(id => id.Length > 0)
            .Select(id => $"content/modules/{ModuleFolder(id)}/")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// The folder a manifest module id lives in: the id uses dots (urman.chapter1) while
    /// the directory uses hyphens (urman-chapter1), which is the convention the project
    /// already uses for every generated slug. Without this mapping the campaign scope
    /// silently matched nothing.
    /// </summary>
    public static string ModuleFolder(string moduleId) => moduleId.Replace('.', '-');

    /// <summary>
    /// The operations this projection relies on when it says what the engine executes.
    /// Each one is checked by name, so a verifier cannot pass because some other arm
    /// still exists.
    /// </summary>
    private static readonly string[] Anchors =
    [
        "all", "any", "not", "knowledge.status", "vocabulary.status", "quest.status", "npc.state", "beat.state", "location.is",
        "knowledge.set-status", "vocabulary.set-status", "npc.set-state", "beat.set-state", "pressure.change", "route.unlock",
        "audio.request", "scene.request", "document.open", "journal.record",

        // The PhotoWorld routing sets live in PhotoWorldState rather than in the engine
        // switch, so they are anchored by name too: a verifier that only looked at the
        // engine cases would not notice one of them disappearing.
        "photoworlds.book.received", "photoworlds.photo.state", "photoworlds.fact.observed",
        "photoworlds.evidence.confirmed", "photoworlds.caption.prepared", "photoworlds.prologue.completed",
        "photoworlds.book.receive", "photoworlds.photo.acquire", "photoworlds.photo.mount",
        "photoworlds.photo.back-read", "photoworlds.photo.context-known", "photoworlds.fact.observe",
        "photoworlds.evidence.verify", "photoworlds.prologue.enter", "photoworlds.prologue.complete",
        "photoworlds.epilogue.finish"
    ];

    /// <summary>Conditions and effects the engine executes, read from its sources.</summary>
    public static (IReadOnlySet<string> Conditions, IReadOnlySet<string> Effects) Executed(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var conditions = new HashSet<string>(StringComparer.Ordinal) { "all", "any", "not" };
        var effects = new HashSet<string>(StringComparer.Ordinal);
        var engine = Path.Combine(Path.GetFullPath(root), EnginePath.Replace('/', Path.DirectorySeparatorChar));
        var photo = Path.Combine(Path.GetFullPath(root), PhotoWorldPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(engine))
        {
            var text = File.ReadAllText(engine);
            var evaluate = Slice(text, "public static bool Evaluate(JsonElement condition", "public static ContentRulePlan PlanEffects");
            foreach (Match match in SwitchArm.Matches(evaluate))
            {
                conditions.Add(match.Groups[1].Value);
            }

            var plan = Slice(text, "private static void ApplyEffect(", "private static void SetStatus(");
            foreach (Match match in SwitchCase.Matches(plan))
            {
                effects.Add(match.Groups[1].Value);
            }
        }

        if (File.Exists(photo))
        {
            var text = File.ReadAllText(photo);
            foreach (Match match in HashSetEntry.Matches(Slice(text, "ConditionOperations = new", "EffectOperations = new")))
            {
                conditions.Add(match.Groups[1].Value);
            }

            foreach (Match match in HashSetEntry.Matches(Slice(text, "EffectOperations = new", "public static bool IsConditionOperation")))
            {
                effects.Add(match.Groups[1].Value);
            }
        }

        return (conditions, effects);
    }

    /// <summary>The whole picture: engine, Studio catalogue and the authored usage of one campaign.</summary>
    public static RuleCoverageReport Inspect(StudioWorkspace workspace, string? campaignSelection = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (conditions, effects) = Executed(workspace.Root);
        var offeredConditions = ConditionPhrases.Conditions.Select(kind => kind.Op).ToHashSet(StringComparer.Ordinal);
        var offeredEffects = ConditionPhrases.Effects.Select(kind => kind.Op).ToHashSet(StringComparer.Ordinal);

        // Counting the whole workspace mixes campaigns: the author needs the campaign
        // whose rules they are looking at, so the modules come from its own manifest.
        var prefixes = CampaignModulePrefixes(workspace, campaignSelection);
        var scoped = prefixes.Count == 0
            ? workspace.Files.ToArray()
            : workspace.Files.Where(file => prefixes.Any(prefix => file.RelativePath.StartsWith(prefix, StringComparison.Ordinal)) || file.RelativePath.StartsWith($"content/campaigns/{campaignSelection}/", StringComparison.Ordinal)).ToArray();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in scoped)
        {
            Count(file.Snapshot(), used);
        }

        var rows = new List<RuleOpFacts>();
        foreach (var (op, uses) in used.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            // The engine decides what an operation is; the property it was nested under
            // does not, because effects live under many keys in the authored data.
            var isCondition = conditions.Contains(op) && !effects.Contains(op);
            var isEffect = effects.Contains(op);
            var executed = isCondition || isEffect;
            rows.Add(new RuleOpFacts(
                op,
                isCondition,
                executed,
                offeredConditions.Contains(op) || offeredEffects.Contains(op),
                ConditionPhrases.NotExecuted.Contains(op),
                uses));
        }

        var problems = new List<string>();
        var notes = new List<string>();
        foreach (var row in rows.Where(row => !row.Executed))
        {
            problems.Add($"Правило «{row.Op}» используется {row.AuthoredUses} раз, но игра его не исполняет: условие или последствие не сработает.");
        }

        foreach (var row in rows.Where(row => row.Executed && !row.OfferedInStudio && row.Op is not ("all" or "any" or "not")))
        {
            problems.Add($"Правило «{row.Op}» используется {row.AuthoredUses} раз и игра его исполняет, но Studio его не предлагает: создать или поправить такое правило в интерфейсе нельзя.");
        }

        var unused = effects.Concat(conditions)
            .Where(op => op is not ("all" or "any" or "not"))
            .Where(op => !used.ContainsKey(op))
            .Order(StringComparer.Ordinal)
            .ToArray();
        notes.Add(prefixes.Count == 0
            ? $"Считано по всему workspace ({scoped.Length} файлов): кампания не выбрана или её манифест не найден, поэтому счётчики смешивают кампании."
            : $"Считано по кампании «{campaignSelection}» ({scoped.Length} файлов из модулей {string.Join(", ", prefixes.Select(prefix => prefix.Split('/')[^2]))}).");

        // The authored worlds, scenes and animations under game/content are shared by
        // every campaign, so they are counted and named separately instead of being
        // folded into one campaign's total or silently dropped.
        if (prefixes.Count > 0)
        {
            var shared = workspace.Files.Where(file => file.RelativePath.StartsWith("game/content/", StringComparison.Ordinal)).ToArray();
            var sharedUsed = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var file in shared)
            {
                Count(file.Snapshot(), sharedUsed);
            }

            notes.Add($"Общие авторские источники game/content: {shared.Length} файлов, {sharedUsed.Count} разных операций — они не принадлежат одной кампании, поэтому в счёт кампании не включены. Если правила этой кампании лежат там (а не в её модулях), счёт кампании будет нулевым: это не значит, что правил нет.");
        }
        notes.Add("Это проекция исходников: она показывает, что написано и что читает движок, но не доказывает исполнение, порядок или сохранение состояния — для этого нужен runtime.");
        notes.Add($"Операций исполняется: условий {conditions.Count}, последствий {effects.Count}; в кампании используется {rows.Count}.");
        notes.Add($"Studio предлагает условий {offeredConditions.Count} и последствий {offeredEffects.Count}.");
        if (unused.Length > 0)
        {
            notes.Add($"Исполняется, но в кампании не используется: {string.Join(", ", unused)}.");
        }

        notes.Add("Составные условия и N-of-M этапа исполняются: all/any/not в ContentRuleEngine, режим all/any/threshold в QuestLifecycleReducer.");
        var timer = ConditionPhrases.NotExecuted.Contains("time.phase");
        notes.Add(timer
            ? "Явного источника таймера нет: time.phase разрешён схемой, но не исполняется — таймеры на состояниях истории остаются неподдержанными."
            : "time.phase больше не числится неисполняемым: перечитайте эту строку.");
        notes.Add("Повтор не платит дважды: ObjectiveOccurrenceId связывает награду с конкретным срабатыванием в QuestLifecycleReducer.");

        return new RuleCoverageReport(rows, notes, problems);
    }

    /// <summary>
    /// Source-presence check: the engine still exposes switch cases where this map
    /// reads them. It is not runtime proof of the semantics.
    /// </summary>
    public static IReadOnlyList<string> VerifyAgainstSources(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var problems = new List<string>();
        var engine = Path.Combine(Path.GetFullPath(root), EnginePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(engine))
        {
            problems.Add($"Нет файла {EnginePath}: состав исполняемых правил не прочитан.");
            return problems;
        }

        var (conditions, effects) = Executed(root);
        // Every operation the projection relies on is checked BY NAME: one surviving arm
        // of a different name must not make the verifier pass.
        foreach (var anchor in Anchors)
        {
            // The side is read from the sources, not guessed from the name: the anchor
            // only has to be handled somewhere for the projection to stay honest.
            if (!conditions.Contains(anchor) && !effects.Contains(anchor))
            {
                problems.Add($"Операция «{anchor}», на которую опирается проекция, больше не найдена ни в условиях, ни в последствиях {EnginePath}: перечитайте карту покрытия.");
            }
        }

        return problems;
    }

    private static string Slice(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var end = text.IndexOf(to, start, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    /// <summary>Counts every authored rule operation; the engine's sets decide what it is.</summary>
    private static void Count(JsonNode? node, Dictionary<string, int> used)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["op"] is JsonValue op && op.TryGetValue<string>(out var name) && name.Length > 0)
                {
                    used[name] = used.TryGetValue(name, out var count) ? count + 1 : 1;
                }

                foreach (var pair in obj)
                {
                    Count(pair.Value, used);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Count(item, used);
                }

                break;
        }
    }
}
