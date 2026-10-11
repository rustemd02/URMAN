using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored vocabulary record: the word, its meaning and where the game applies it.</summary>
public sealed record VocabularyFacts(
    string VocabularyId,
    string Term,
    string Language,
    string Meaning,
    int RereadTargets,
    int ApplicationTargets,
    IReadOnlyList<string> UnresolvedTargets,
    string ReviewStatus,
    string Reviewer,
    IReadOnlyList<string> Problems);

/// <summary>
/// The vocabulary half of the spec AI-20 slice: the Tatar words the game teaches, taken from the
/// authored records. The important part is not the count but the two links the format carries and
/// the gate it declares:
///
///   * rereadTargets and applicationTargets are IDS - of scenes, quests and DOCUMENTS - so an id
///     that resolves nowhere means the word never appears where the game meant to use it. Reading
///     only JSON would report the document targets as unresolved, because a document's id lives in
///     its Markdown front-matter; this projection reads both, which is exactly the mistake my first
///     scan made;
///   * consultantReview is the LANGUAGE gate, and the records themselves say it is still pending,
///     so the projection quotes that state instead of judging the words. Writing or approving Tatar
///     is a native speaker's work and is not something a projection may invent.
/// </summary>
public static class AuthoredVocabulary
{
    public const string SchemaPath = "content/schemas/vocabulary.schema.json";

    public static IReadOnlyList<VocabularyFacts> Inspect(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var required = AuthoredDocuments.RequiredFields(root, SchemaPath);
        var ids = DeclaredIds(full);
        var records = new List<VocabularyFacts>();
        foreach (var file in EnumerateJson(full))
        {
            JsonNode? document;
            try
            {
                document = JsonNode.Parse(File.ReadAllText(file));
            }
            catch (System.Text.Json.JsonException)
            {
                continue;
            }

            var pending = new Stack<JsonNode?>();
            pending.Push(document);
            while (pending.Count > 0)
            {
                switch (pending.Pop())
                {
                    case JsonObject record:
                        if (record["id"] is JsonValue value && value.TryGetValue<string>(out var id)
                            && id is { Length: > 0 } && id.Contains(":vocabulary/", StringComparison.Ordinal))
                        {
                            records.Add(Describe(record, id, required, ids));
                        }

                        foreach (var pair in record)
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

        return records.OrderBy(record => record.VocabularyId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(string root)
    {
        var records = Inspect(root);
        var pending = records.Count(record => record.ReviewStatus is "pending" or "");
        var reviewed = records.Count - pending;
        var unresolved = records.Sum(record => record.UnresolvedTargets.Count);
        return
        [
            $"Авторских слов: {records.Count}; целей повторного чтения и применения: {records.Sum(r => r.RereadTargets + r.ApplicationTargets)}; неразрешённых целей: {unresolved}.",
            $"Языковой гейт по данным самих записей: проверено {reviewed}, ожидает проверки носителя {pending}.",
            pending == 0
                ? "Записи словаря объявлены проверенными."
                : "Ожидание проверки носителя языка — это НЕ ошибка автора и НЕ то, что проекция может закрыть: перевод и культурная точность остаются внешним гейтом, и до него словарь остаётся неподтверждённым.",
            "Значения слов здесь не оцениваются: проекция проверяет связи и обязательные поля, а не правильность татарского."
        ];
    }

    private static VocabularyFacts Describe(JsonObject record, string id, IReadOnlyList<string> required, IReadOnlySet<string> ids)
    {
        var problems = new List<string>();
        foreach (var field in required.Where(field => !record.ContainsKey(field)))
        {
            problems.Add($"{id}: нет обязательного поля «{field}» (vocabulary.schema.json).");
        }

        var unresolved = new List<string>();
        var reread = Targets(record, "rereadTargets", id, ids, unresolved, problems);
        var application = Targets(record, "applicationTargets", id, ids, unresolved, problems);
        var review = record["consultantReview"] as JsonObject;
        return new VocabularyFacts(
            id,
            (string?)record["term"] ?? "",
            (string?)record["language"] ?? "",
            Meaning(record),
            reread,
            application,
            unresolved,
            (string?)review?["status"] ?? "",
            (string?)review?["reviewer"] ?? "",
            problems);
    }

    private static int Targets(JsonObject record, string field, string id, IReadOnlySet<string> ids, List<string> unresolved, List<string> problems)
    {
        if (record[field] is not JsonArray array)
        {
            if (record.ContainsKey(field))
            {
                problems.Add($"{id}: поле «{field}» не массив.");
            }

            return 0;
        }

        var count = 0;
        foreach (var value in array)
        {
            if (value is not JsonValue item || !item.TryGetValue<string>(out var target) || target is not { Length: > 0 })
            {
                problems.Add($"{id}: в «{field}» элемент не строка.");
                continue;
            }

            count++;
            if (!ids.Contains(target))
            {
                unresolved.Add(target);
            }
        }

        return count;
    }

    private static string Meaning(JsonObject record)
    {
        var meaning = record["meaning"];
        return meaning switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonObject obj when (string?)obj["default"] is { Length: > 0 } fallback => fallback,
            _ => ""
        };
    }

    /// <summary>
    /// Every id declared in the authored content, from JSON AND from the Markdown front-matter of
    /// documents and hints: a document's id is not in any JSON file, so a JSON-only scan reports
    /// every document target as unresolved - which is precisely what my first attempt did.
    /// </summary>
    public static IReadOnlySet<string> DeclaredIds(string root)
    {
        var full = Path.GetFullPath(root);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in EnumerateJson(full))
        {
            JsonNode? document;
            try
            {
                document = JsonNode.Parse(File.ReadAllText(file));
            }
            catch (System.Text.Json.JsonException)
            {
                continue;
            }

            var pending = new Stack<JsonNode?>();
            pending.Push(document);
            while (pending.Count > 0)
            {
                switch (pending.Pop())
                {
                    case JsonObject obj:
                        if (obj["id"] is JsonValue value && value.TryGetValue<string>(out var id) && id is { Length: > 0 })
                        {
                            ids.Add(id);
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

        foreach (var folder in new[] { Path.Combine("content", "modules"), Path.Combine("game", "content", "modules") })
        {
            var path = Path.Combine(full, folder);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.AllDirectories))
            {
                if (AuthoredDocuments.FrontMatter(File.ReadAllText(file)) is { } front
                    && (string?)front["id"] is { Length: > 0 } id)
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    private static IEnumerable<string> EnumerateJson(string full) =>
        new[] { "content", Path.Combine("game", "content") }
            .Select(folder => Path.Combine(full, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
            .Where(file => !file.Contains(".compiled.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
}
