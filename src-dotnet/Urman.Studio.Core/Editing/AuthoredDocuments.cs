using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored document: the Markdown file its manifest lists and the front-matter it carries.</summary>
public sealed record AuthoredDocumentFacts(
    string ModuleId,
    string RelativePath,
    string DocumentId,
    int KnowledgeRefs,
    int AssetRefs,
    int Effects,
    IReadOnlyList<string> Problems);

/// <summary>
/// The document half of the spec AI-24 slice. A document is not an entity in a JSON array: it is
/// a Markdown file whose YAML front-matter carries the same fields the compiled record has, and
/// the module manifest lists the file among its sourceFiles. So the authored contract has three
/// links that can each fail quietly:
///
///   * the manifest naming a file that is not there;
///   * front-matter missing a field document.schema.json requires, or naming a different
///     sourceFile than the path it lives at;
///   * an access condition or an opening effect whose operation the engine does not execute, so
///     the document can never be opened or never marks anything read.
///
/// The required-field list is the schema's own, and the executed operations come from the engine
/// sources, so neither can drift from the code they describe.
/// </summary>
public static class AuthoredDocuments
{
    public const string SchemaPath = "content/schemas/document.schema.json";
    public const string ModulesFolder = "content/modules";

    /// <summary>The fields document.schema.json requires, read from the schema itself.</summary>
    public static IReadOnlyList<string> RequiredFields(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), SchemaPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject schema || schema["required"] is not JsonArray required)
        {
            return [];
        }

        return required.OfType<JsonValue>().Select(value => value.GetValue<string>()).ToArray();
    }

    public static IReadOnlyList<AuthoredDocumentFacts> Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var required = RequiredFields(root);
        var (executedConditions, executedEffects) = RuleCatalogCoverage.Executed(root);
        var documents = new List<AuthoredDocumentFacts>();
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

            var moduleId = (string?)manifest["moduleId"] ?? Path.GetFileName(moduleFolder);
            foreach (var entry in (manifest["sourceFiles"] as JsonArray ?? []).OfType<JsonValue>()
                .Select(value => value.GetValue<string>())
                .Where(value => value.StartsWith("documents/", StringComparison.Ordinal) && value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            {
                var problems = new List<string>();
                var path = Path.Combine(moduleFolder, entry.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    documents.Add(new AuthoredDocumentFacts(moduleId, entry, "", 0, 0, 0,
                        [$"{moduleId}: манифест перечисляет «{entry}», а файла нет — документ не соберётся."]));
                    continue;
                }

                var frontMatter = FrontMatter(File.ReadAllText(path));
                if (frontMatter is null)
                {
                    documents.Add(new AuthoredDocumentFacts(moduleId, entry, "", 0, 0, 0,
                        [$"{moduleId}: у «{entry}» нет front-matter — компилятор не получит ни id, ни условий открытия."]));
                    continue;
                }

                foreach (var field in required.Where(field => !frontMatter.ContainsKey(field)))
                {
                    problems.Add($"{moduleId}: у «{entry}» нет обязательного поля «{field}» (document.schema.json).");
                }

                if ((string?)frontMatter["sourceFile"] is { Length: > 0 } declared && !string.Equals(declared, entry, StringComparison.Ordinal))
                {
                    problems.Add($"{moduleId}: «{entry}» объявляет sourceFile «{declared}» — не свой путь; компилятор свяжет документ не с тем файлом.");
                }

                var knowledgeRefs = (frontMatter["knowledgeRefs"] as JsonArray ?? []).OfType<JsonValue>().Select(value => value.GetValue<string>()).ToArray();
                foreach (var reference in knowledgeRefs.Where(reference => reference.Length > 0 && workspace.Locate(reference) is null))
                {
                    problems.Add($"{moduleId}: «{entry}» ссылается на «{reference}», которого нет в проекте.");
                }

                var conditions = frontMatter["accessConditions"] as JsonArray ?? [];
                var effects = frontMatter["openEffects"] as JsonArray ?? [];
                CheckOperations(conditions, executedConditions, moduleId, entry, "accessConditions", true, problems);
                CheckOperations(effects, executedEffects, moduleId, entry, "openEffects", false, problems);

                documents.Add(new AuthoredDocumentFacts(
                    moduleId,
                    entry,
                    (string?)frontMatter["id"] ?? "",
                    knowledgeRefs.Length,
                    (frontMatter["assetRefs"] as JsonArray ?? []).Count,
                    effects.Count,
                    problems));
            }
        }

        return documents.OrderBy(document => document.RelativePath, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var documents = Inspect(workspace, root);
        var problems = documents.Sum(document => document.Problems.Count);
        var ops = documents.Count == 0 ? 0 : documents.Sum(document => document.Effects);
        return
        [
            $"Авторских документов, перечисленных манифестами модулей: {documents.Count}; последствий открытия: {ops}; проблем: {problems}.",
            problems == 0
                ? "Каждый документ существует, несёт все поля своей схемы, объявляет свой собственный sourceFile, а его ссылки на знания находятся в проекте."
                : $"Проблем у документов: {problems} — {string.Join(" | ", documents.SelectMany(document => document.Problems).Take(3))}",
            "Условия доступа и последствия открытия сверяются с наборами операций, которые исполняет движок, поэтому неисполняемое условие здесь — не стиль, а неоткрываемый документ.",
            "Текст документа и его язык — внешняя приёмка: проекция проверяет связи, а не прозу."
        ];
    }

    /// <summary>
    /// Parses the YAML front-matter of a Markdown document. The project writes JSON-compatible
    /// scalars there, so a value that starts with a bracket or a quote is parsed as JSON and the
    /// rest as a plain string.
    /// </summary>
    public static JsonObject? FrontMatter(string text)
    {
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return null;
        }

        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var result = new JsonObject();
        foreach (System.Text.RegularExpressions.Match match in FieldLine.Matches(text[3..end]))
        {
            var name = match.Groups[1].Value;
            var raw = match.Groups[2].Value.Trim();
            if (raw.StartsWith('[') || raw.StartsWith('{'))
            {
                try
                {
                    result[name] = JsonNode.Parse(raw);
                    continue;
                }
                catch (System.Text.Json.JsonException)
                {
                    // Fall through to the plain string so a malformed value is visible, not fatal.
                }
            }

            result[name] = raw.Trim('"');
        }

        return result;
    }

    private static void CheckOperations(JsonArray rules, IReadOnlySet<string> executed, string moduleId, string entry, string where, bool isCondition, List<string> problems)
    {
        if (executed.Count == 0)
        {
            return;
        }

        foreach (var rule in rules.OfType<JsonObject>())
        {
            var op = (string?)rule["op"] ?? "";
            if (op.Length == 0)
            {
                problems.Add($"{moduleId}: «{entry}» в {where} есть правило без op.");
            }
            else if (!executed.Contains(op))
            {
                problems.Add(isCondition
                    ? $"{moduleId}: «{entry}» в {where} условие «{op}» движок не исполняет — документ нельзя будет открыть."
                    : $"{moduleId}: «{entry}» в {where} последствие «{op}» движок не исполняет — открытие ничего не изменит.");
            }
        }
    }

    private static readonly Regex FieldLine = new(
        "^([A-Za-z][A-Za-z0-9]*):[ \t]*(.*)$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);
}
