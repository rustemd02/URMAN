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
    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root, string? campaignSelection)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (photos, worlds, facts, evidence, anchors, problems) = Inspect(workspace, root, campaignSelection);
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
            UnreachableGates(workspace, root, campaignSelection) is { Count: > 0 } unreachable
                ? $"Условий книги, которые не могут стать истинными: {unreachable.Count} — они ссылаются на факты, фотографии или улики вне каталогов кампании, а HasFact/HasEvidence/PhotoStateIs молча возвращают false."
                : "Все условия книги ссылаются на факты, фотографии и улики из каталогов кампании.",
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

    public static (IReadOnlyList<PhotoFacts> Photos, IReadOnlyList<PhotoWorld> Worlds, IReadOnlyList<string> Facts, IReadOnlyList<string> Evidence, IReadOnlyList<string> Anchors, IReadOnlyList<string> Problems) Inspect(StudioWorkspace workspace, string root, string? campaignSelection)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        // Reset on EVERY call, before any early return: otherwise a missing manifest or a bookless
        // campaign would leave the previous campaign's indexing diagnostics in place, and the next
        // reader would take another campaign's gap list for this one's.
        IndexingGaps = [];
        // The book belongs to ONE campaign: reading every manifest would mix each campaign's
        // facts and photos into one scope, which is exactly the defect this signature prevents.
        if (string.IsNullOrWhiteSpace(campaignSelection))
        {
            return ([], [], [], [], [], ["Кампания не выбрана: книга не определена, и смешивать кампании нельзя."]);
        }
        var full = Path.GetFullPath(root);
        var authored = AuthoredIds(root);
        var indexing = new List<string>();
        var photos = new List<PhotoFacts>();
        var worlds = new List<PhotoWorld>();
        var facts = new List<string>();
        var evidence = new List<string>();
        var anchors = new List<string>();
        var problems = new List<string>();
        var file = Path.Combine(full, CampaignsFolder.Replace('/', Path.DirectorySeparatorChar), campaignSelection, "campaign.json");
        if (!File.Exists(file))
        {
            return ([], [], [], [], [], [$"Манифеста кампании «{campaignSelection}» нет: книгу проверять нечем."]);
        }

        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject manifest || manifest["photoBook"] is not JsonObject book)
            {
                return ([], [], [], [], [], [$"У кампании «{campaignSelection}» нет книги: проверять нечего."]);
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
            // The candidate sources the compiler itself uses (ContentCompiler lines 405-435): a
            // fact offers its sourceIds and a photo offers the sourceId it was acquired from, so
            // two photos of one source cannot prove an evidence twice.
            var factSources = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var fact in (book["facts"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if ((string?)fact["id"] is not { Length: > 0 } factId)
                {
                    continue;
                }

                factSources[factId] = (fact["sourceIds"] as JsonArray ?? []).OfType<JsonValue>()
                    .Select(value => value.GetValue<string>())
                    .Where(value => value.Length > 0)
                    .ToArray();
            }

            var photoSources = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var photo in (book["photos"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if ((string?)photo["id"] is { Length: > 0 } sourcePhotoId && (string?)photo["sourceId"] is { Length: > 0 } photoSource)
                {
                    photoSources[sourcePhotoId] = photoSource;
                }
            }
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
                var candidates = new List<string[]>();
                foreach (var constituent in constituents)
                {
                    var kind = (string?)constituent["kind"] ?? "";
                    var reference = (string?)constituent["id"] ?? "";
                    switch (kind)
                    {
                        case "fact":
                            if (!factSources.TryGetValue(reference, out var factCandidate))
                            {
                                problems.Add($"{campaign}: улика «{id}» ссылается на неизвестный факт «{reference}» (UnknownPhotoWorldFact).");
                                candidates.Add([]);
                            }
                            else
                            {
                                candidates.Add(factCandidate);
                            }

                            break;
                        case "photo-acquired":
                        case "photo-back-read":
                            // Both photo kinds resolve through the photo id, exactly as the
                            // compiler does; photo-back-read was the one I used to let pass.
                            if (!photoSources.TryGetValue(reference, out var photoCandidate))
                            {
                                problems.Add($"{campaign}: улика «{id}» ссылается на неизвестное фото «{reference}» ({kind}, UnknownPhotoWorldPhoto).");
                                candidates.Add([]);
                            }
                            else
                            {
                                candidates.Add([photoCandidate]);
                            }

                            break;
                        default:
                            // The compiler gives an unknown kind NO candidates, so it cannot
                            // prove anything. Treating it as known was my own leniency.
                            problems.Add($"{campaign}: улика «{id}» состоит из вида «{kind}», которого компилятор не знает: такой элемент не даёт источников, и улику нельзя доказать.");
                            candidates.Add([]);
                            break;
                    }
                }

                var distinctAvailable = candidates.SelectMany(candidate => candidate).Distinct(StringComparer.Ordinal).Count();
                if ((int?)item["minimumDistinctSources"] is { } minimum && minimum > distinctAvailable)
                {
                    problems.Add($"{campaign}: улика «{id}» требует {minimum} различных источников, а их доступно {distinctAvailable}: её нельзя собрать.");
                }

                if (candidates.Count == 0 || !HasDistinctSourceAssignment(candidates, 0, []))
                {
                    problems.Add($"{campaign}: улику «{id}» нельзя доказать различными источниками (UnprovablePhotoWorldEvidence).");
                }
            }
        }

        problems.AddRange(worlds.SelectMany(world => world.Problems));
        problems.AddRange(photos.SelectMany(photo => photo.Problems));
        IndexingGaps = indexing;
        return (photos, worlds, facts, evidence, anchors, problems);
    }
    /// <summary>
    /// The compiler's own rule (ContentCompiler.HasDistinctSourceAssignment, consulted at lines
    /// 831-839): every constituent must receive a source, and no source may be used twice.
    /// Mirrored here rather than reimplemented differently, so the projection and the compiler
    /// cannot disagree about whether an evidence is provable.
    /// </summary>
    private static bool HasDistinctSourceAssignment(IReadOnlyList<string[]> candidates, int index, HashSet<string> used)
    {
        if (index == candidates.Count)
        {
            return true;
        }

        foreach (var source in candidates[index])
        {
            if (!used.Add(source))
            {
                continue;
            }

            if (HasDistinctSourceAssignment(candidates, index + 1, used))
            {
                return true;
            }

            used.Remove(source);
        }

        return false;
    }

    /// <summary>
    /// The book gates that can never open. All three accessors of PhotoWorldState read the world
    /// state with TryGetProperty - HasFact, HasEvidence and PhotoStateIs - so a condition naming
    /// an id that is not in the campaign's catalog simply returns false: no error, no warning,
    /// just a gate that never passes. The catalogs are the campaign's photoBook photos, evidence
    /// and facts, which is also what its ValidateFacts and ValidateEvidence enforce, and the
    /// notes say where unknown names come from so the author can decide which side is stale.
    /// </summary>
    public static IReadOnlyList<string> UnreachableGates(StudioWorkspace workspace, string root, string? campaignSelection)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var (photos, _, facts, evidence, _, _) = Inspect(workspace, root, campaignSelection);
        var photoCatalog = photos.Select(photo => photo.PhotoId).ToHashSet(StringComparer.Ordinal);
        var factCatalog = facts.ToHashSet(StringComparer.Ordinal);
        var evidenceCatalog = evidence.ToHashSet(StringComparer.Ordinal);
        if (factCatalog.Count == 0 && photoCatalog.Count == 0 && evidenceCatalog.Count == 0)
        {
            // Not a gate: "nothing to compare with" is a reason to say nothing, not a finding. The
            // notes carry that state; returning it here made it look like an unreachable gate.
            return [];
        }

        var gates = new List<string>();
        foreach (var (field, catalog, accessor, op, label) in new[]
        {
            ("factId", factCatalog, "HasFact", "photoworlds.fact.observed", "факт"),
            ("photoId", photoCatalog, "PhotoStateIs", "photoworlds.photo.state", "фотографию"),
            ("evidenceId", evidenceCatalog, "HasEvidence", "photoworlds.evidence.confirmed", "улику")
        })
        {
            // ONLY the selected campaign's own files: the catalogs above are that campaign's, so a
            // whole-workspace scan would read another campaign's conditions and call its fact ids
            // unreachable, which is exactly the false positive this scoping removes.
            foreach (var reference in ReferencedIds(root, campaignSelection, op, field))
            {
                if (!catalog.Contains(reference))
                {
                    gates.Add($"Условие {op} ссылается на {label} «{reference}», которого нет в каталоге книги ({catalog.Count}): {accessor} читает состояние через TryGetProperty, поэтому условие просто НИКОГДА не станет истинным — без ошибки и без предупреждения.");
                }
            }
        }

        return gates;
    }

    /// <summary>
    /// Every factId the authored content reads in a fact.observed condition, with the entity that
    /// reads it, so a gate is reported with the place it stands in.
    /// </summary>
    public static IReadOnlyList<string> ReferencedIds(StudioWorkspace workspace, string op, string field)
        => ReferencedIds(workspace?.Root ?? "", null, op, field);

    /// <summary>
    /// The values one operation reads from one field, scoped to ONE campaign: its manifest and the
    /// files of the modules that manifest names, resolved through the shared module-folder mapping.
    /// A null or empty selection keeps the whole workspace, which is what the older callers wanted.
    /// </summary>
    public static IReadOnlyList<string> ReferencedIds(string root, string? campaignSelection, string op, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var document in CampaignDocuments(root, campaignSelection))
        {
            var pending = new Stack<JsonNode?>();
            pending.Push(document);
            while (pending.Count > 0)
            {
                switch (pending.Pop())
                {
                    case JsonObject obj:
                        // TryGetValue, not a cast: a field holding an object or an array throws on
                        // the cast, and a projection must report rather than take the Studio down.
                        if (obj["op"] is JsonValue opValue && opValue.TryGetValue<string>(out var operation) && operation == op
                            && obj[field] is JsonValue referenceValue && referenceValue.TryGetValue<string>(out var reference)
                            && reference is { Length: > 0 })
                        {
                            ids.Add(reference);
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

        return ids.ToArray();
    }

    /// <summary>
    /// The JSON documents that belong to one campaign: its own manifest plus every file of every
    /// module it names. The module folder mapping is the shared one, so this resolves the same way
    /// the compiler's input hash does. An empty selection means the whole authored content, which
    /// keeps the unscoped callers working.
    /// </summary>
    public static IReadOnlyList<JsonNode?> CampaignDocuments(string root, string? campaignSelection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var documents = new List<JsonNode?>();
        if (string.IsNullOrWhiteSpace(campaignSelection))
        {
            foreach (var file in EnumerateJson(full))
            {
                documents.Add(Parse(file));
            }

            return documents;
        }

        var manifestPath = Path.Combine(full, "content", "campaigns", campaignSelection, "campaign.json");
        if (!File.Exists(manifestPath))
        {
            return documents;
        }

        var manifest = Parse(manifestPath);
        documents.Add(manifest);
        if (manifest?["modules"] is not JsonArray modules)
        {
            return documents;
        }

        foreach (var moduleId in modules.OfType<JsonObject>()
            .Select(item => (string?)item["moduleId"])
            .OfType<string>()
            .Where(id => id.Length > 0))
        {
            var folder = Path.Combine(full, "content", "modules", RuleCatalogCoverage.ModuleFolder(moduleId));
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Where(file => !file.Contains(".compiled.", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal))
            {
                documents.Add(Parse(file));
            }
        }

        return documents;
    }

    private static IEnumerable<string> EnumerateJson(string full) =>
        new[] { "content", Path.Combine("game", "content") }
            .Select(folder => Path.Combine(full, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
            .Where(file => !file.Contains(".compiled.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    private static JsonNode? Parse(string file)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(file));
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

}

/// <summary>One PhotoWorld of the book: its photo, its anchor and its own node chain.</summary>
public sealed record PhotoWorld(string WorldId, string PhotoId, string Anchor, string SpawnNode, string ExitNode, int Nodes, IReadOnlyList<string> Problems);
