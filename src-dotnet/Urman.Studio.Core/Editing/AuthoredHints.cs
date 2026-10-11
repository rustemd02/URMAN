using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored hint: the Markdown file the manifest lists and the front-matter it carries.</summary>
public sealed record AuthoredHintFacts(
    string ModuleId,
    string RelativePath,
    string HintId,
    int Level,
    int Requires,
    string PointsTo,
    IReadOnlyList<string> Problems);

/// <summary>
/// The hints half of the spec AI-24 slice, using the same authored shape as a document: a
/// Markdown file with YAML front-matter, listed in its module manifest's sourceFiles. The
/// runtime (OldPcUi.Hints) reads Level, Requires and the text, distinguishes level 1 (arrives
/// when the computer opens), level 2 and level >= 3 (waits until the player really read two
/// archive documents), and evaluates Requires through the rule engine. So a hint whose text id
/// does not resolve shows nothing, and a hint whose condition the engine does not execute never
/// appears.
///
/// The parsers and the schema-required list are shared with the document projection rather than
/// copied, because both families are the same format.
/// </summary>
public static class AuthoredHints
{
    public const string SchemaPath = "content/schemas/hint.schema.json";
    public const string ModulesFolder = "content/modules";

    /// <summary>The levels the runtime distinguishes, read from its own comparisons.</summary>
    public static IReadOnlyList<int> RuntimeLevels(string root)
    {
        var text = Read(root, "game/scripts/OldPcUi.Hints.cs");
        var levels = new SortedSet<int>();
        foreach (System.Text.RegularExpressions.Match match in LevelLiteral.Matches(text))
        {
            if (int.TryParse(match.Groups[1].Value, out var level))
            {
                levels.Add(level);
            }
        }

        return levels.ToArray();
    }

    public static IReadOnlyList<AuthoredHintFacts> Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var required = AuthoredDocuments.RequiredFields(root, SchemaPath);
        var (executedConditions, _) = RuleCatalogCoverage.Executed(root);
        var hints = new List<AuthoredHintFacts>();
        var folder = Path.Combine(full, ModulesFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(folder))
        {
            return [];
        }

        foreach (var manifestPath in Directory.EnumerateFiles(folder, "module.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var moduleFolder = Path.GetDirectoryName(manifestPath)!;
            if (JsonNode.Parse(File.ReadAllText(manifestPath)) is not JsonObject manifest)
            {
                continue;
            }

            var moduleId = AuthoredJson.TextOrNull(manifest, "moduleId") ?? Path.GetFileName(moduleFolder);
            foreach (var entry in (manifest["sourceFiles"] as JsonArray ?? []).OfType<JsonValue>()
                .Select(value => value.GetValue<string>())
                .Where(value => value.StartsWith("hints/", StringComparison.Ordinal) && value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            {
                var problems = new List<string>();
                var path = Path.Combine(moduleFolder, entry.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    hints.Add(new AuthoredHintFacts(moduleId, entry, "", 0, 0, "", [$"{moduleId}: манифест перечисляет «{entry}», а файла нет."]));
                    continue;
                }

                var frontMatter = AuthoredDocuments.FrontMatter(File.ReadAllText(path));
                if (frontMatter is null)
                {
                    hints.Add(new AuthoredHintFacts(moduleId, entry, "", 0, 0, "", [$"{moduleId}: у «{entry}» нет front-matter."]));
                    continue;
                }

                foreach (var field in required.Where(field => !frontMatter.ContainsKey(field)))
                {
                    problems.Add($"{moduleId}: у «{entry}» нет обязательного поля «{field}» (hint.schema.json).");
                }

                var level = 0;
                if (frontMatter["level"] is { } levelNode)
                {
                    if (levelNode is JsonValue levelValue && levelValue.TryGetValue<int>(out var parsed))
                    {
                        level = parsed;
                        // The SCHEMA owns the range (hint.schema.json says integer 1..3), so it is
                        // read from there instead of guessed from the runtime branches: the generic
                        // next-hint path serves every level, and only a value outside the schema's
                        // own range is a defect.
                        var range = LevelRange(root);
                        if (level < range.Minimum || level > range.Maximum)
                        {
                            problems.Add($"{moduleId}: «{entry}» уровня {level}, а схема допускает {range.Minimum}..{range.Maximum} — компилятор отвергнет такую подсказку.");
                        }
                    }
                    else
                    {
                        problems.Add($"{moduleId}: «{entry}» поле «level» не целое число — схема требует integer.");
                    }
                }

                if (AuthoredJson.Text(frontMatter, "textId") is { Length: > 0 } textId && workspace.Locate(textId) is null)
                {
                    problems.Add($"{moduleId}: «{entry}» ссылается на текст «{textId}», которого нет в проекте — подсказка покажет пустоту.");
                }

                if ((AuthoredJson.Text(frontMatter, "pointsTo")).Length == 0)
                {
                    problems.Add($"{moduleId}: «{entry}» не указывает pointsTo — поиск не подскажет, куда идти.");
                }

                var requires = frontMatter["requires"] as JsonArray ?? [];
                foreach (var rule in requires.OfType<JsonObject>())
                {
                    var op = AuthoredJson.TextOrNull(rule, "op") ?? "";
                    if (op.Length == 0)
                    {
                        problems.Add($"{moduleId}: «{entry}» в requires есть правило без op.");
                    }
                    else if (executedConditions.Count > 0 && !executedConditions.Contains(op))
                    {
                        problems.Add($"{moduleId}: «{entry}» в requires условие «{op}» движок не исполняет — подсказка никогда не появится.");
                    }
                }

                hints.Add(new AuthoredHintFacts(moduleId, entry, AuthoredJson.Text(frontMatter, "id"), level, requires.Count, AuthoredJson.Text(frontMatter, "pointsTo"), problems));
            }
        }

        return hints.OrderBy(hint => hint.RelativePath, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var hints = Inspect(workspace, root);
        var levels = RuntimeLevels(root);
        return
        [
            $"Авторских подсказок: {hints.Count}; уровни: {string.Join(", ", hints.Select(hint => hint.Level).Distinct().Order())}; рантайм различает уровни: {string.Join(", ", levels)}.",
            hints.Sum(hint => hint.Problems.Count) == 0
                ? "У каждой подсказки есть файл, все поля её схемы, разрешающийся текст, непустой pointsTo и исполняемые условия."
                : $"Проблем у подсказок: {hints.Sum(hint => hint.Problems.Count)} — {string.Join(" | ", hints.SelectMany(hint => hint.Problems).Take(3))}",
            $"Рантайм различает уровни {string.Join(", ", RuntimeLevels(root))} явными сравнениями, а прочие уровни обслуживает общий путь «первая непоказанная подсказка, условия которой выполнены»; схема допускает диапазон {LevelRange(root).Minimum}..{LevelRange(root).Maximum}. Всё это цитируется, а не переписывается.",
            "Текст подсказок и его язык — внешняя приёмка."
        ];
    }

    /// <summary>The level range hint.schema.json declares, read from the schema itself.</summary>
    public static (int Minimum, int Maximum) LevelRange(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), SchemaPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path))?["properties"]?["level"] is not JsonObject level)
        {
            return (1, int.MaxValue);
        }

        return ((int?)level["minimum"] ?? 1, (int?)level["maximum"] ?? int.MaxValue);
    }

    private static readonly System.Text.RegularExpressions.Regex LevelLiteral = new(
        "Level\\s*(?:is|>=)\\s*(\\d+)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string Read(string root, string relative)
    {
        var path = Path.Combine(Path.GetFullPath(root), relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }
}
