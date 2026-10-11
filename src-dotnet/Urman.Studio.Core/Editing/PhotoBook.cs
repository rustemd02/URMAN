using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One photo of the family album and the links it carries.</summary>
public sealed record PhotoFacts(string PhotoId, string PageId, string Source, string Permission, int ContextSources, IReadOnlyList<string> Problems);

/// <summary>
/// The book half of the spec AI-24 slice. The album is a graph inside the campaign manifest:
/// the book names the handoff and the giver, every photo names its page, its source, its
/// permission and the context sources it is read against, every world names the photo and the
/// anchor it belongs to plus the spawn and exit nodes inside its own node list, and every piece
/// of evidence lists the facts or acquired photos that make it up. All of that is id-linked,
/// so a single wrong id means a page nobody can reach or an evidence that can never be earned.
///
/// Reference checks resolve against the workspace, which is what the editor edits.
/// </summary>
public static class PhotoBookFacts
{
    public const string CampaignsFolder = "content/campaigns";

    /// <summary>
    /// References that exist in the authored files but that the workspace index does not carry,
    /// as recorded by the MOST RECENT <see cref="Inspect"/> call. The notes read it immediately
    /// after that call, so the two cannot disagree; the projection is used one call at a time.
    /// </summary>
    public static IReadOnlyList<string> IndexingGaps { get; private set; } = [];

    /// <summary>The book's own records of one campaign, or empty when it has no book.</summary>
    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (photos, worlds, facts, evidence, anchors, problems) = Inspect(workspace, root);
        var usedAnchors = worlds.Select(world => world.Anchor).ToHashSet(StringComparer.Ordinal);
        var unused = anchors.Where(anchor => !usedAnchors.Contains(anchor)).Order(StringComparer.Ordinal).ToArray();
        return
        [
            $"Книга: фотографий {photos.Count}, миров {worlds.Count}, фактов {facts.Count}, улик {evidence.Count}, обязательных якорей {anchors.Count}; проблем {problems.Count}.",
            problems.Count == 0
                ? "Все ссылки книги разрешаются: страницы, источники, разрешения, контекст, фото мира, якоря, точки входа и выхода, а также состав улик."
                : $"Ссылок, которых нет в проекте: {problems.Count} — {string.Join(" | ", problems.Take(3))}",
            IndexingGaps.Count == 0
                ? "Нет ссылок, которые лежали бы в файлах, но не были видны рабочему пространству."
                : $"Ссылок, которые ЕСТЬ в авторских файлах, но НЕ индексируются рабочим пространством: {IndexingGaps.Count}. Это пробел общего индексатора, а не ошибка автора: интеракций в индексе {0}. Список ведёт не автор, а владелец индексатора.",
            unused.Length == 0
                ? "Каждый обязательный якорь используется каким-нибудь миром."
                : $"Якоря, которые не использует ни один мир: {string.Join(", ", unused)}. Это не обязательно ошибка: якорь может принадлежать миру, которого в списке книги ещё нет (эпилог назван в narrativeOrder и требуется инвариантом), — решает автор.",
            "Проверяется связность книги, а не её содержание: тексты, язык и культурная приёмка остаются за людьми."
        ];
    }

    /// <summary>
    /// Every id declared anywhere in the authored content. A reference that resolves here but
    /// not in the workspace is an INDEXING gap of the shared workspace, not an authoring
    /// mistake: the record exists in the files and the editor cannot see it. Reporting the two
    /// apart is the difference between a defect and a false alarm.
    /// </summary>
    public static IReadOnlySet<string> AuthoredIds(string root)
    {
        var full = Path.GetFullPath(root);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var folder in new[] { "content", Path.Combine("game", "content") })
        {
            var path = Path.Combine(full, folder);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories))
            {
                if (file.Contains(".compiled.", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    Collect(JsonNode.Parse(File.ReadAllText(file)), ids);
                }
                catch (System.Text.Json.JsonException)
                {
                    // A file Studio cannot parse is not this projection's subject.
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Sorts one unresolved reference: a genuine absence is a problem, while an id that exists
    /// in the authored files but not in the workspace index is recorded as an indexing gap.
    /// </summary>
    private static void Classify(StudioWorkspace workspace, IReadOnlySet<string> authored, string where, string reference, List<string> problems, List<string> indexing)
    {
        if (workspace.Locate(reference) is not null)
        {
            return;
        }

        if (authored.Contains(reference))
        {
            indexing.Add($"{where} ссылается на «{reference}»: запись ЕСТЬ в авторских файлах, но рабочее пространство её не индексирует, поэтому редактор не может её открыть. Это пробел индексатора, а не ошибка автора.");
            return;
        }

        problems.Add($"{where} ссылается на «{reference}», которого нет ни в проекте, ни в файлах.");
    }

    /// <summary>
    /// Iterative on purpose: a recursive walk over the authored worlds overflowed the stack,
    /// because a plot file nests entities inside entities. An explicit stack cannot.
    /// </summary>
    private static void Collect(JsonNode? node, HashSet<string> ids)
    {
        var pending = new Stack<JsonNode?>();
        pending.Push(node);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case JsonObject obj:
                    // TryGetValue instead of a string cast: casting a numeric id throws, and a
                    // world file really does contain numeric ids next to string ones.
                    if (obj["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var id) && id is { Length: > 0 })
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

    public static (IReadOnlyList<PhotoFacts> Photos, IReadOnlyList<PhotoWorld> Worlds, IReadOnlyList<string> Facts, IReadOnlyList<string> Evidence, IReadOnlyList<string> Anchors, IReadOnlyList<string> Problems) Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var full = Path.GetFullPath(root);
        var authored = AuthoredIds(root);
        var indexing = new List<string>();
        var photos = new List<PhotoFacts>();
        var worlds = new List<PhotoWorld>();
        var facts = new List<string>();
        var evidence = new List<string>();
        var anchors = new List<string>();
        var problems = new List<string>();
        var folder = Path.Combine(full, CampaignsFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(folder))
        {
            return ([], [], [], [], [], ["Каталога кампаний нет: книгу проверять нечем."]);
        }

        foreach (var file in Directory.EnumerateFiles(folder, "campaign.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject manifest || manifest["photoBook"] is not JsonObject book)
            {
                continue;
            }

            var campaign = (string?)manifest["id"] ?? Path.GetFileName(Path.GetDirectoryName(file)) ?? "";
            var pages = new HashSet<string>(StringComparer.Ordinal);
            var photoIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var photo in (book["photos"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var id = (string?)photo["id"] ?? "";
                var page = (string?)photo["pageId"] ?? "";
                var local = new List<string>();
                if (id.Length == 0)
                {
                    local.Add("Фотография без id.");
                }
                else if (!photoIds.Add(id))
                {
                    local.Add($"Фотография «{id}» повторяется.");
                }

                if (page.Length == 0)
                {
                    local.Add($"У фотографии «{id}» нет pageId: страницу не найти.");
                }
                else if (!pages.Add(page))
                {
                    local.Add($"Страница «{page}» повторяется: две фотографии претендуют на одну страницу.");
                }

                foreach (var field in new[] { "sourceId", "permissionId" })
                {
                    if ((string?)photo[field] is { Length: > 0 } reference)
                    {
                        Classify(workspace, authored, $"{campaign}: «{id}».{field}", reference, local, indexing);
                    }
                }

                var context = (photo["contextSourceIds"] as JsonArray ?? []).OfType<JsonValue>().Select(value => value.GetValue<string>()).ToArray();
                foreach (var reference in context.Where(reference => reference.Length > 0))
                {
                    Classify(workspace, authored, $"{campaign}: «{id}» в contextSourceIds", reference, local, indexing);
                }

                photos.Add(new PhotoFacts(id, page, (string?)photo["sourceId"] ?? "", (string?)photo["permissionId"] ?? "", context.Length, local));
            }

            foreach (var world in (book["worlds"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var id = (string?)world["id"] ?? "";
                var photo = (string?)world["photoId"] ?? "";
                var anchor = (string?)world["anchorId"] ?? "";
                var spawn = (string?)world["mainSpawnNodeId"] ?? "";
                var exit = (string?)world["exitNodeId"] ?? "";
                var nodes = (world["nodes"] as JsonArray ?? []).OfType<JsonObject>()
                    .Select(node => (string?)node["id"])
                    .OfType<string>()
                    .ToArray();
                var local = new List<string>();
                if (photo.Length > 0 && !photoIds.Contains(photo))
                {
                    local.Add($"{campaign}: мир «{id}» ссылается на фотографию «{photo}», которой нет в книге.");
                }

                if (nodes.Distinct(StringComparer.Ordinal).Count() != nodes.Length)
                {
                    local.Add($"{campaign}: у мира «{id}» повторяются id узлов.");
                }

                foreach (var (name, node) in new[] { ("mainSpawnNodeId", spawn), ("exitNodeId", exit) })
                {
                    if (node.Length > 0 && !nodes.Contains(node, StringComparer.Ordinal))
                    {
                        local.Add($"{campaign}: «{id}».{name} = «{node}» не объявлен среди узлов этого мира.");
                    }
                }

                worlds.Add(new PhotoWorld(id, photo, anchor, spawn, exit, nodes.Length, local));
            }

            anchors.AddRange((book["requiredAnchors"] as JsonArray ?? []).OfType<JsonValue>().Select(value => value.GetValue<string>()));
            facts.AddRange((book["facts"] as JsonArray ?? []).OfType<JsonObject>().Select(fact => (string?)fact["id"] ?? ""));
            var factIds = facts.ToHashSet(StringComparer.Ordinal);
            foreach (var fact in (book["facts"] as JsonArray ?? []).OfType<JsonObject>())
            {
                foreach (var reference in (fact["sourceIds"] as JsonArray ?? []).OfType<JsonValue>().Select(value => value.GetValue<string>()))
                {
                    if (reference.Length > 0)
                    {
                        var local = new List<string>();
                        Classify(workspace, authored, $"{campaign}: факт «{(string?)fact["id"]}»", reference, local, indexing);
                        problems.AddRange(local);
                    }
                }
            }

            foreach (var item in (book["evidence"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var id = (string?)item["id"] ?? "";
                var constituents = (item["constituents"] as JsonArray ?? []).OfType<JsonObject>().ToArray();
                evidence.Add(id);
                foreach (var constituent in constituents)
                {
                    var kind = (string?)constituent["kind"] ?? "";
                    var reference = (string?)constituent["id"] ?? "";
                    var known = kind switch
                    {
                        "fact" => factIds.Contains(reference),
                        "photo-acquired" => photoIds.Contains(reference),
                        _ => true
                    };
                    if (!known)
                    {
                        problems.Add($"{campaign}: улика «{id}» состоит из {kind} «{reference}», которого нет среди объявленных.");
                    }
                }

                if ((int?)item["minimumDistinctSources"] is { } minimum && minimum > constituents.Length)
                {
                    problems.Add($"{campaign}: улика «{id}» требует {minimum} источников, а объявлено {constituents.Length}: её нельзя собрать.");
                }
            }
        }

        problems.AddRange(worlds.SelectMany(world => world.Problems));
        problems.AddRange(photos.SelectMany(photo => photo.Problems));
        IndexingGaps = indexing;
        return (photos, worlds, facts, evidence, anchors, problems);
    }
}

/// <summary>One PhotoWorld of the book: its photo, its anchor and its own node chain.</summary>
public sealed record PhotoWorld(string WorldId, string PhotoId, string Anchor, string SpawnNode, string ExitNode, int Nodes, IReadOnlyList<string> Problems);
