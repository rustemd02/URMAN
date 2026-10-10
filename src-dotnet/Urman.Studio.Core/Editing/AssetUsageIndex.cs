using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One catalogue entry as Studio and the runtime see it.</summary>
public sealed record CatalogueEntry(
    string Id,
    string Name,
    string Category,
    string Kit,
    string Scene,
    string? Node,
    string Collision,
    string License);

/// <summary>One record of the asset registry: where a resource came from and what it costs.</summary>
public sealed record AssetRecord(
    string Id,
    string? Source,
    string? Derived,
    string? License,
    string? Generator,
    int? TriangleBudget,
    int? TextureBudget,
    IReadOnlyList<string> Materials,
    string? MaterialOwner,
    string? ReleaseDisposition);

/// <summary>A material name and every asset record that declares it.</summary>
public sealed record MaterialUse(string Material, IReadOnlyList<string> AssetIds);

/// <summary>
/// The answer to "what else changes if I touch this": the catalogue entries behind
/// one resource, the placements that use them, the registry provenance record and
/// the material names that other assets share.
/// </summary>
public sealed record AssetUsage(
    string Key,
    string Kind,
    IReadOnlyList<CatalogueEntry> Entries,
    IReadOnlyList<string> Placements,
    IReadOnlyList<AssetRecord> Records,
    IReadOnlyList<MaterialUse> SharedMaterials,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Problems);

/// <summary>
/// Reads the three real sources of an asset question - the catalogue, the asset
/// registry and the world placements - and reports the shared uses honestly
/// (spec AI-36). A resource is almost never local: the catalogue uses eleven GLB
/// kits for 177 entries, and twenty-five material names belong to more than one
/// asset record, so a "local" material edit can reach several kits. Nothing here
/// writes authored content; it only names what a change would touch.
/// </summary>
public sealed class AssetUsageIndex
{
    public const string CataloguePath = "game/content/world/catalog.v1.json";
    public const string RegistryPath = "assets/asset_registry.json";

    private readonly string _root;
    private readonly List<CatalogueEntry> _entries;
    private readonly List<AssetRecord> _records;
    private readonly Dictionary<string, List<string>> _placementsByCatalogueId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<AssetRecord>> _recordsByDerived = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<AssetRecord>> _recordsByMaterial = new(StringComparer.Ordinal);

    private AssetUsageIndex(string root, List<CatalogueEntry> entries, List<AssetRecord> records, Dictionary<string, List<string>> placements)
    {
        _root = root;
        _entries = entries;
        _records = records;
        _placementsByCatalogueId = placements;
        foreach (var record in records)
        {
            if (record.Derived is { Length: > 0 } derived)
            {
                if (!_recordsByDerived.TryGetValue(derived, out var list))
                {
                    _recordsByDerived[derived] = list = [];
                }

                list.Add(record);
            }

            foreach (var material in record.Materials)
            {
                if (!_recordsByMaterial.TryGetValue(material, out var list))
                {
                    _recordsByMaterial[material] = list = [];
                }

                list.Add(record);
            }
        }
    }

    public IReadOnlyList<CatalogueEntry> CatalogueEntries => _entries;
    public IReadOnlyList<AssetRecord> Records => _records;

    /// <summary>Registry records no catalogue entry uses; informational, not a problem.</summary>
    public IReadOnlyList<string> RecordsOutsideCatalogue
    {
        get
        {
            var used = _entries.Select(entry => "game/" + entry.Scene["res://".Length..]).ToHashSet(StringComparer.Ordinal);
            return _records.Where(record => record.Derived is null || !used.Contains(record.Derived)).Select(record => record.Id).Order(StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Material names that more than one asset record declares: a local edit there is shared.</summary>
    public IReadOnlyList<MaterialUse> SharedMaterialNames =>
        _recordsByMaterial.Where(pair => pair.Value.Count > 1)
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new MaterialUse(pair.Key, pair.Value.Select(record => record.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
            .ToArray();

    public static AssetUsageIndex Open(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var entries = ReadCatalogue(full);
        var records = ReadRegistry(full);
        var placements = ReadPlacements(full);
        return new AssetUsageIndex(full, entries, records, placements);
    }

    /// <summary>What one catalogue entry touches.</summary>
    public AssetUsage ByCatalogueId(string catalogueId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogueId);
        var entry = _entries.FirstOrDefault(item => string.Equals(item.Id, catalogueId, StringComparison.Ordinal));
        if (entry is null)
        {
            return Problem(catalogueId, "catalogue", [$"Каталожной записи «{catalogueId}» нет: размещение с таким id игра не построит."]);
        }

        return Describe(catalogueId, "catalogue", [entry]);
    }

    /// <summary>What one source scene (a GLB kit) touches.</summary>
    public AssetUsage ByScene(string scenePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        var scene = scenePath.StartsWith("res://", StringComparison.Ordinal) ? scenePath : "res://" + scenePath.TrimStart('/');
        var entries = _entries.Where(item => string.Equals(item.Scene, scene, StringComparison.Ordinal)).ToArray();
        return entries.Length == 0
            ? Problem(scene, "scene", [$"Ни одна каталожная запись не использует сцену «{scene}»."])
            : Describe(scene, "scene", entries);
    }

    /// <summary>What one material name touches, including the assets that share it.</summary>
    public AssetUsage ByMaterial(string material)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(material);
        var records = _recordsByMaterial.TryGetValue(material, out var list) ? list : [];
        if (records.Count == 0)
        {
            return Problem(material, "material", [$"Ни один ресурс реестра не объявляет материал «{material}»."]);
        }

        var notes = new List<string>();
        var problems = new List<string>();
        if (records.Count > 1)
        {
            notes.Add($"Материал «{material}» объявлен у {records.Count} ресурсов: правка затронет их все, а не только выбранный объект.");
        }

        if (LooksLikeDescription(material))
        {
            notes.Add("Это строка похожа на описание набора материалов, а не на имя одного материала: поле materials в реестре заполнено не единообразно.");
        }

        foreach (var record in records.Where(record => record.MaterialOwner is null))
        {
            problems.Add($"У ресурса «{record.Id}» не указан владелец материала: перед публикацией нужно понять, кто отвечает за эту фактуру.");
        }

        return new AssetUsage(material, "material", EntriesOf(records), [], records, [new MaterialUse(material, records.Select(record => record.Id).ToArray())], notes, problems);
    }

    /// <summary>Findings an author must fix or accept before publishing a resource.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        foreach (var (catalogueId, placements) in _placementsByCatalogueId.Where(pair => !_entries.Any(entry => string.Equals(entry.Id, pair.Key, StringComparison.Ordinal))))
        {
            problems.Add($"Размещения {string.Join(", ", placements)} ссылаются на каталожную запись «{catalogueId}», которой нет.");
        }

        // Only the resources the catalogue can really place are checked: a provenance
        // record for an unrelated texture is not this unit's business.
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scene in _entries.Select(entry => entry.Scene).Distinct(StringComparer.Ordinal))
        {
            var key = "game/" + scene["res://".Length..];
            if (!File.Exists(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar))))
            {
                problems.Add($"Сцена «{scene}» не найдена на диске: каталожные записи на неё не построятся.");
            }

            if (!_recordsByDerived.TryGetValue(key, out var records))
            {
                problems.Add($"Для сцены «{scene}» нет записи в реестре ресурсов: источник и лицензия не описаны, публиковать нельзя.");
                continue;
            }

            foreach (var record in records)
            {
                reachable.Add(record.Id);
                if (record.Derived is { Length: > 0 } derived && !File.Exists(Path.Combine(_root, derived.Replace('/', Path.DirectorySeparatorChar))))
                {
                    problems.Add($"Ресурс «{record.Id}»: файл «{derived}» не найден на диске.");
                }

                if (record.ReleaseDisposition is { } disposition && disposition.Contains("exclude-from-act1-release", StringComparison.Ordinal))
                {
                    problems.Add($"Ресурс «{record.Id}» помечен как исключаемый из релиза: {disposition}");
                }
            }
        }

        return problems;
    }

    private AssetUsage Describe(string key, string kind, IReadOnlyList<CatalogueEntry> entries)
    {
        var placements = entries.SelectMany(entry => _placementsByCatalogueId.TryGetValue(entry.Id, out var list) ? list : []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var records = EntriesOf(entries);
        var notes = new List<string>();
        var problems = new List<string>();

        if (kind == "scene")
        {
            notes.Add($"Эта сцена обслуживает {entries.Count} каталожных записей и {placements.Length} размещений в мире: правка ресурса затронет их все.");
        }

        foreach (var record in records.Where(record => record.ReleaseDisposition is null))
        {
            problems.Add($"У ресурса «{record.Id}» не указано решение о публикации: сначала нужно решить, входит ли он в релиз.");
        }

        var materials = records.SelectMany(record => record.Materials).Distinct(StringComparer.Ordinal).ToArray();
        var shared = SharedMaterialNames.Where(use => materials.Contains(use.Material, StringComparer.Ordinal)).ToArray();
        if (shared.Length > 0)
        {
            notes.Add($"Общие материалы: {string.Join(", ", shared.Select(use => $"{use.Material} (ещё у {use.AssetIds.Count - 1})"))}. Локальная правка такого материала выйдет за пределы одного объекта.");
        }

        if (records.Count == 0)
        {
            problems.Add("Ни одной записи реестра: происхождение и лицензия ресурса не описаны.");
        }

        return new AssetUsage(key, kind, entries, placements, records, shared, notes, problems);
    }

    private IReadOnlyList<AssetRecord> EntriesOf(IReadOnlyList<CatalogueEntry> entries) =>
        entries.Select(entry => _recordsByDerived.TryGetValue("game/" + entry.Scene["res://".Length..], out var records) ? records : [])
            .SelectMany(records => records)
            .DistinctBy(record => record.Id, StringComparer.Ordinal)
            .OrderBy(record => record.Id, StringComparer.Ordinal)
            .ToArray();

    private IReadOnlyList<CatalogueEntry> EntriesOf(IReadOnlyList<AssetRecord> records) =>
        records.SelectMany(record => record.Derived is { Length: > 0 } derived
                ? _entries.Where(entry => string.Equals("game/" + entry.Scene["res://".Length..], derived, StringComparison.Ordinal))
                : [])
            .DistinctBy(entry => entry.Id, StringComparer.Ordinal)
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .ToArray();

    private static AssetUsage Problem(string key, string kind, IReadOnlyList<string> problems) =>
        new(key, kind, [], [], [], [], [], problems);

    private static List<CatalogueEntry> ReadCatalogue(string root)
    {
        var entries = new List<CatalogueEntry>();
        var path = Path.Combine(root, CataloguePath.Replace('/', Path.DirectorySeparatorChar));
        if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject document || document["entities"] is not JsonArray entities)
        {
            return entries;
        }

        foreach (var entry in entities.OfType<JsonObject>())
        {
            entries.Add(new CatalogueEntry(
                Text(entry["id"]),
                Text(entry["name"]),
                Text(entry["category"]),
                Text(entry["kit"]),
                Text(entry["source"]?["scene"]),
                Text(entry["source"]?["node"]) is { Length: > 0 } node ? node : null,
                Text(entry["collision"]),
                Text(entry["license"])));
        }

        return entries;
    }

    private static List<AssetRecord> ReadRegistry(string root)
    {
        var records = new List<AssetRecord>();
        var path = Path.Combine(root, RegistryPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject document || document["assets"] is not JsonArray assets)
        {
            return records;
        }

        foreach (var asset in assets.OfType<JsonObject>())
        {
            records.Add(new AssetRecord(
                Text(asset["id"]),
                Text(asset["source"]) is { Length: > 0 } source ? source : null,
                Text(asset["derived"]) is { Length: > 0 } derived ? derived : null,
                Text(asset["license"]) is { Length: > 0 } license ? license : null,
                Text(asset["generator"]) is { Length: > 0 } generator ? generator : null,
                Integer(asset["triangleBudget"]),
                Integer(asset["textureBudget"]),
                (asset["materials"] as JsonArray ?? []).Select(Text).Where(material => material.Length > 0).ToArray(),
                Text(asset["materialOwner"]) is { Length: > 0 } owner ? owner : null,
                Text(asset["releaseDisposition"]) is { Length: > 0 } disposition ? disposition : null));
        }

        return records;
    }

    private static Dictionary<string, List<string>> ReadPlacements(string root)
    {
        var placements = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var folder = Path.Combine(root, "game", "content", "world");
        if (!Directory.Exists(folder))
        {
            return placements;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.world.v1.json").Order(StringComparer.Ordinal))
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject plot || plot["entities"] is not JsonArray entities)
            {
                continue;
            }

            foreach (var entity in entities.OfType<JsonObject>())
            {
                var id = Text(entity["id"]);
                var parameters = entity["params"] as JsonObject;
                var single = Text(parameters?["catalogId"]);
                var referenced = new List<string>();
                if (single.Length > 0)
                {
                    referenced.Add(single);
                }

                foreach (var many in (parameters?["catalogIds"] as JsonArray ?? []).Select(Text).Where(value => value.Length > 0))
                {
                    referenced.Add(many);
                }

                foreach (var catalogueId in referenced.Distinct(StringComparer.Ordinal))
                {
                    if (!placements.TryGetValue(catalogueId, out var list))
                    {
                        placements[catalogueId] = list = [];
                    }

                    list.Add(id);
                }
            }
        }

        return placements;
    }

    /// <summary>A registry entry whose materials field carries prose instead of names.</summary>
    private static bool LooksLikeDescription(string material) =>
        material.Contains(';', StringComparison.Ordinal) || material.Contains(" по ", StringComparison.Ordinal) || material.Length > 48;

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static int? Integer(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<int>(out var small))
        {
            return small;
        }

        return value.TryGetValue<long>(out var integer) ? (int)integer : null;
    }
}
