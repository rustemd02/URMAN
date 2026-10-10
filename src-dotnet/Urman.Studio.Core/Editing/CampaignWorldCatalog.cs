using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Editing;

/// <summary>An authored world plot file and the entity namespaces it carries.</summary>
public sealed record AuthoredWorldPlot(
    string RelativePath,
    int EntityCount,
    IReadOnlyList<string> Namespaces,
    IReadOnlyList<string> WorldKeys);

/// <summary>
/// A world that exists only as a specification (PhotoWorlds W01-W05 today). Spec
/// AI-13 requires such a world to be named, never shown as an editable scene.
/// </summary>
public sealed record WorldSpecification(
    string CampaignSelection,
    string WorldId,
    string Title,
    string Status,
    WorldAuthoringCapability Capability);

/// <summary>One concrete reason the checkout cannot yet satisfy a requirement of AI-13.</summary>
public sealed record CatalogBlocker(string Code, string Detail);

/// <summary>
/// Reads the campaigns and startable worlds that really exist in a checkout
/// (spec AI-13, contract <c>CampaignWorldContext</c>). It is a read-only view
/// over existing sources - the campaign manifests, the Godot entry scenes, the
/// compiled packs, the authored world plots and the photo-world specifications -
/// and keeps no registry of its own: a campaign or world that is not in those
/// files cannot be resolved at all.
/// </summary>
public sealed class CampaignWorldCatalog
{
    private static readonly Regex MainSceneLine = new(
        "^run/main_scene\\s*=\\s*\"res://([^\"]+)\"", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex CampaignResourceLine = new(
        "^\\s*CampaignResourcePath\\s*=\\s*\"([^\"]+)\"", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex EntryZoneLine = new(
        "^\\s*(?:Initial|Current)ZoneId\\s*=\\s*\"([^\"]+)\"", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex EntrySpawnLine = new(
        "^\\s*(?:Initial|Current)SpawnPointId\\s*=\\s*\"([^\"]+)\"", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex CompiledPackName = new(
        "^res://content/([^/\"]+)\\.compiled\\.v1\\.json$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, CampaignRef> _campaignsBySelection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldRef> _worldsByScene = new(StringComparer.Ordinal);

    private CampaignWorldCatalog(
        string root,
        IReadOnlyList<CampaignRef> campaigns,
        IReadOnlyList<WorldRef> worlds,
        IReadOnlyList<AuthoredWorldPlot> authoredWorldPlots,
        IReadOnlyList<WorldSpecification> specifications,
        IReadOnlyList<CatalogBlocker> blockers,
        IReadOnlyList<string> notes)
    {
        Root = root;
        Campaigns = campaigns;
        Worlds = worlds;
        AuthoredWorldPlots = authoredWorldPlots;
        Specifications = specifications;
        Blockers = blockers;
        Notes = notes;
        foreach (var campaign in campaigns)
        {
            _campaignsBySelection[campaign.Selection] = campaign;
        }

        foreach (var world in worlds)
        {
            _worldsByScene[world.ScenePath] = world;
        }
    }

    public string Root { get; }
    public IReadOnlyList<CampaignRef> Campaigns { get; }
    public IReadOnlyList<WorldRef> Worlds { get; }
    public IReadOnlyList<AuthoredWorldPlot> AuthoredWorldPlots { get; }
    public IReadOnlyList<WorldSpecification> Specifications { get; }
    public IReadOnlyList<CatalogBlocker> Blockers { get; }
    public IReadOnlyList<string> Notes { get; }

    /// <summary>Authored worlds present in the checkout (one today: urman.world:act1).</summary>
    public IReadOnlyList<string> AuthoredWorldKeys =>
        AuthoredWorldPlots.SelectMany(plot => plot.WorldKeys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Campaigns an author may start by default; archive examples are excluded.</summary>
    public IEnumerable<CampaignRef> SelectableCampaigns => Campaigns.Where(campaign => campaign.SelectableByDefault);

    /// <summary>Worlds that declare this campaign, plus worlds that declare none and can host any campaign.</summary>
    public IEnumerable<WorldRef> WorldsFor(string campaignSelection) =>
        Worlds.Where(world => string.Equals(world.CampaignSelection, campaignSelection, StringComparison.Ordinal)
            || string.IsNullOrEmpty(world.CampaignSelection));

    /// <summary>Reads the catalog from a Studio workspace (same checkout the editor has open).</summary>
    public static CampaignWorldCatalog Discover(StudioWorkspace workspace) => Discover(workspace.Root);

    /// <summary>Reads the catalog straight from a checkout root.</summary>
    public static CampaignWorldCatalog Discover(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var notes = new List<string>();
        var campaigns = ReadCampaigns(full, notes);
        var plots = ReadAuthoredWorldPlots(full, notes);
        var specifications = ReadWorldSpecifications(full, notes);
        var worlds = ReadEntryWorlds(full, plots, notes);
        var blockers = BuildBlockers(campaigns, worlds, plots, specifications);
        return new CampaignWorldCatalog(full, campaigns, worlds, plots, specifications, blockers, notes);
    }

    /// <summary>
    /// The campaign and world that may start without asking the author: the first
    /// selectable campaign and the first world that can host it. Archive examples
    /// are never returned (spec AI-13 done_when).
    /// </summary>
    public CampaignWorldContext Default()
    {
        var campaign = SelectableCampaigns.FirstOrDefault()
            ?? throw new InvalidOperationException("В этом checkout нет ни одной действующей кампании: content/campaigns содержит только архивные примеры.");
        var world = WorldsFor(campaign.Selection).FirstOrDefault()
            ?? throw new InvalidOperationException($"Для кампании «{campaign.Selection}» нет ни одной стартовой сцены мира.");
        return Resolve(campaign.Selection, world.ScenePath);
    }

    /// <summary>
    /// Pairs one real campaign with one real world scene. Unsupported input is a
    /// clear error, never a silently invented default; a scene that does not
    /// declare its campaign is marked as an explicit selection.
    /// </summary>
    public CampaignWorldContext Resolve(string campaignSelection, string scenePath, string? zoneId = null, string? spawnPointId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignSelection);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        if (!_campaignsBySelection.TryGetValue(campaignSelection, out var campaign))
        {
            throw new InvalidOperationException(
                $"В checkout нет кампании «{campaignSelection}». Есть: {string.Join(", ", Campaigns.Select(item => item.Selection))}.");
        }

        if (!_worldsByScene.TryGetValue(scenePath, out var world))
        {
            throw new InvalidOperationException(
                $"Сцена «{scenePath}» не найдена среди стартовых миров. Есть: {string.Join(", ", Worlds.Select(item => item.ScenePath))}.");
        }

        var declared = world.DeclaredCampaignResource is not null
            && string.Equals(SelectionOf(world.DeclaredCampaignResource), campaignSelection, StringComparison.Ordinal);
        var resolved = world with
        {
            CampaignSelection = campaignSelection,
            ZoneId = string.IsNullOrWhiteSpace(zoneId) ? world.ZoneId : zoneId,
            SpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? world.SpawnPointId : spawnPointId
        };
        var revision = SourceRevision(campaign, resolved);
        return new CampaignWorldContext(
            campaign,
            resolved,
            revision,
            campaign.CompiledPackPath,
            declared ? CampaignWorldContext.CampaignBindingDeclared : CampaignWorldContext.CampaignBindingExplicit);
    }

    /// <summary>SHA-256 over the campaign manifest, the entry scene and the compiled pack this context depends on.</summary>
    public string SourceRevision(CampaignRef campaign, WorldRef world)
    {
        var sceneRelative = "game/" + world.ScenePath["res://".Length..];
        return HashFiles([campaign.RelativePath, sceneRelative, campaign.CompiledPackPath]);
    }

    private string HashFiles(IEnumerable<string> relativePaths)
    {
        var builder = new StringBuilder();
        foreach (var relative in relativePaths.Order(StringComparer.Ordinal))
        {
            var full = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            builder.Append(relative).Append('\0')
                .Append(File.Exists(full) ? AtomicFile.Sha256OfFile(full) : "missing")
                .Append('\n');
        }

        return AtomicFile.Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static List<CampaignRef> ReadCampaigns(string root, List<string> notes)
    {
        var campaigns = new List<CampaignRef>();
        var folder = Path.Combine(root, "content", "campaigns");
        if (!Directory.Exists(folder))
        {
            notes.Add("Нет каталога content/campaigns — кампаний не найдено.");
            return campaigns;
        }

        foreach (var directory in Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal))
        {
            var selection = Path.GetFileName(directory);
            var relative = $"content/campaigns/{selection}/campaign.json";
            var file = Path.Combine(directory, "campaign.json");
            if (!File.Exists(file))
            {
                notes.Add($"{relative}: файла нет, каталог пропущен.");
                continue;
            }

            if (ParseObject(file, relative, notes) is not { } manifest)
            {
                continue;
            }

            var modules = (manifest["modules"] as JsonArray)?
                .Select(module => Text((module as JsonObject)?["moduleId"]))
                .Where(id => id.Length > 0)
                .ToArray() ?? [];
            var packRelative = $"game/content/{selection}.compiled.v1.json";
            var archive = selection.StartsWith("dev-", StringComparison.Ordinal);
            campaigns.Add(new CampaignRef(
                selection,
                Text(manifest["id"]) is { Length: > 0 } id ? id : selection,
                Text(manifest["exactVersion"]),
                Text(manifest["entrypoint"]),
                relative,
                modules,
                packRelative,
                File.Exists(Path.Combine(root, packRelative)),
                archive,
                archive ? "Каталог dev-*: пример или легаси, по умолчанию не стартует." : ""));
        }

        return campaigns;
    }

    private static List<AuthoredWorldPlot> ReadAuthoredWorldPlots(string root, List<string> notes)
    {
        var plots = new List<AuthoredWorldPlot>();
        var folder = Path.Combine(root, "game", "content", "world");
        if (!Directory.Exists(folder))
        {
            return plots;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.world.v1.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (ParseObject(file, relative, notes) is not { } plot)
            {
                continue;
            }

            var entities = plot["entities"] as JsonArray ?? [];
            var ids = entities
                .Select(entity => Text((entity as JsonObject)?["id"]))
                .Where(id => id.Length > 0)
                .ToArray();
            var namespaces = ids.Select(NamespaceOf).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var worldKeys = ids.Select(WorldKeyOf).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            plots.Add(new AuthoredWorldPlot(relative, entities.Count, namespaces, worldKeys));
        }

        return plots;
    }

    private static List<WorldSpecification> ReadWorldSpecifications(string root, List<string> notes)
    {
        var specifications = new List<WorldSpecification>();
        var campaignsFolder = Path.Combine(root, "content", "campaigns");
        if (!Directory.Exists(campaignsFolder))
        {
            return specifications;
        }

        foreach (var file in Directory.EnumerateFiles(campaignsFolder, "worlds.spec.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var selection = relative.Split('/').Skip(2).FirstOrDefault() ?? "";
            if (ParseObject(file, relative, notes) is not { } spec)
            {
                continue;
            }

            var notRuntime = Text(spec["kind"]) == "authoring_spec_not_runtime_content";
            foreach (var entry in (spec["worlds"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var status = Text(entry["status"]);
                if (status.Length == 0)
                {
                    status = notRuntime ? "authoring_spec_not_runtime_content" : "unknown";
                }

                specifications.Add(new WorldSpecification(
                    selection,
                    Text(entry["id"]),
                    Text(entry["title"]),
                    status,
                    status == "SPECIFIED_NOT_IMPLEMENTED" || notRuntime
                        ? WorldAuthoringCapability.SpecifiedNotImplemented
                        : WorldAuthoringCapability.PresentationOnly));
            }
        }

        return specifications;
    }

    private static List<WorldRef> ReadEntryWorlds(string root, IReadOnlyList<AuthoredWorldPlot> plots, List<string> notes)
    {
        var worlds = new List<WorldRef>();
        var scenesRoot = Path.Combine(root, "game");
        var candidates = new List<string>();
        var mainScene = ReadMainScene(root);
        if (mainScene is not null)
        {
            candidates.Add(mainScene);
        }

        var scenesFolder = Path.Combine(scenesRoot, "scenes");
        if (Directory.Exists(scenesFolder))
        {
            candidates.AddRange(Directory.EnumerateFiles(scenesFolder, "*.tscn", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(file => "res://" + Path.GetRelativePath(scenesRoot, file).Replace('\\', '/')));
        }

        var zoneTitles = ReadZoneTitles(root, notes);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scenePath in candidates)
        {
            if (!seen.Add(scenePath))
            {
                continue;
            }

            var file = Path.Combine(scenesRoot, scenePath["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file))
            {
                notes.Add($"{scenePath}: файла сцены нет, пропущена.");
                continue;
            }

            var text = File.ReadAllText(file);
            var declaredResource = CampaignResourceLine.Match(text) is { Success: true } resource ? resource.Groups[1].Value : null;
            var zone = EntryZoneLine.Match(text) is { Success: true } zoneMatch ? zoneMatch.Groups[1].Value : "";
            var spawn = EntrySpawnLine.Match(text) is { Success: true } spawnMatch ? spawnMatch.Groups[1].Value : "";
            var isMain = string.Equals(mainScene, scenePath, StringComparison.Ordinal);
            if (!isMain && declaredResource is null && zone.Length == 0)
            {
                continue;
            }

            var selection = declaredResource is not null ? SelectionOf(declaredResource) : "";
            var worldId = Path.GetFileNameWithoutExtension(scenePath);
            var evidence = new List<string>();
            var capability = WorldAuthoringCapability.AuthoredWorldPlots;
            if (isMain)
            {
                evidence.Add("Стартовая сцена проекта: game/project.godot run/main_scene.");
            }

            if (declaredResource is not null)
            {
                evidence.Add($"Сцена сама объявляет кампанию: CampaignResourcePath = {declaredResource}.");
                if (plots.Count == 0)
                {
                    capability = WorldAuthoringCapability.PresentationOnly;
                    evidence.Add("В checkout нет авторских world plot файлов.");
                }
                else
                {
                    evidence.Add($"Авторские world plot файлы checkout: {plots.Count}; принадлежат namespace {string.Join(", ", plots.SelectMany(plot => plot.Namespaces).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))}.");
                    evidence.Add("Связь этой сцены с конкретным world plot нигде не объявлена — требуется подтверждение владельца.");
                }
            }
            else
            {
                evidence.Add("Сцена не объявляет кампанию: связь задаётся кодом (RuntimeBridge/CompiledCampaignRepository), поэтому это явный выбор автора.");
                evidence.Add(plots.Count > 0
                    ? $"Авторских world plot: {plots.Count} ({string.Join(", ", plots.SelectMany(plot => plot.Namespaces).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))})."
                    : "Авторских world plot нет.");
                if (plots.Count == 0)
                {
                    capability = WorldAuthoringCapability.PresentationOnly;
                }
            }

            if (zone.Length == 0)
            {
                evidence.Add("Сцена не объявляет зону/спавн: их задаёт код, поэтому в контексте они остаются пустыми до явного выбора.");
            }

            var title = zoneTitles.TryGetValue(zone, out var zoneTitle) && zoneTitle.Length > 0 ? zoneTitle : worldId;
            worlds.Add(new WorldRef(selection, worldId, title, scenePath, zone, spawn, declaredResource, capability, evidence));
        }

        return worlds;
    }

    private static string? ReadMainScene(string root)
    {
        var project = Path.Combine(root, "game", "project.godot");
        if (!File.Exists(project))
        {
            return null;
        }

        return MainSceneLine.Match(File.ReadAllText(project)) is { Success: true } match ? "res://" + match.Groups[1].Value : null;
    }

    private static Dictionary<string, string> ReadZoneTitles(string root, List<string> notes)
    {
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        var relative = "game/content/world/location_titles.v1.json";
        var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(file) || ParseObject(file, relative, notes) is not { } document)
        {
            return titles;
        }

        foreach (var zone in (document["zones"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var id = Text(zone["zoneId"]);
            if (id.Length > 0)
            {
                titles[id] = Text(zone["title"]);
            }
        }

        return titles;
    }

    private static List<CatalogBlocker> BuildBlockers(
        IReadOnlyList<CampaignRef> campaigns,
        IReadOnlyList<WorldRef> worlds,
        IReadOnlyList<AuthoredWorldPlot> plots,
        IReadOnlyList<WorldSpecification> specifications)
    {
        var blockers = new List<CatalogBlocker>();
        var worldKeys = plots.SelectMany(plot => plot.WorldKeys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (worldKeys.Length < 2)
        {
            blockers.Add(new CatalogBlocker(
                "single-authored-world",
                $"Авторских миров в checkout: {worldKeys.Length} ({string.Join(", ", worldKeys)}). Полный done_when AI-13 «два разных действующих мира» требует второго мира с реальными авторскими данными."));
        }

        if (worlds.Any(world => string.IsNullOrEmpty(world.CampaignSelection) && world.Capability == WorldAuthoringCapability.AuthoredWorldPlots))
        {
            blockers.Add(new CatalogBlocker(
                "campaign-world-link-undeclared",
                "Связь «кампания → мир» нигде не объявлена: content/campaigns/*/campaign.json не ссылается на сцену или зону, а стартовая сцена не объявляет кампанию. До явного контракта пара выбирается автором и помечается как explicit-selection."));
        }

        var specified = specifications.Where(spec => spec.Capability == WorldAuthoringCapability.SpecifiedNotImplemented).ToArray();
        if (specified.Length > 0)
        {
            blockers.Add(new CatalogBlocker(
                "photo-worlds-specified-only",
                $"PhotoWorlds без сцены: {string.Join(", ", specified.Select(spec => $"{spec.WorldId} «{spec.Title}» ({spec.Status})"))}. Их нельзя показывать редактируемыми."));
        }

        var unverified = worlds
            .Where(world => world.CapabilityEvidence.Any(line => line.Contains("подтверждение владельца", StringComparison.Ordinal)))
            .Select(world => world.ScenePath)
            .ToArray();
        if (unverified.Length > 0)
        {
            blockers.Add(new CatalogBlocker(
                "world-capability-unverified",
                $"Capability выведена из файлов, но связь «сцена → авторский мир» нигде не объявлена, поэтому её нужно подтвердить у владельца: {string.Join(", ", unverified)}. До подтверждения такие сцены нельзя показывать как полностью редактируемые."));
        }

        if (campaigns.All(campaign => campaign.IsArchive))
        {
            blockers.Add(new CatalogBlocker("no-selectable-campaign", "Все найденные кампании помечены архивными примерами."));
        }

        return blockers;
    }

    private static JsonObject? ParseObject(string file, string relative, List<string> notes)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(file)) as JsonObject;
        }
        catch (JsonException error)
        {
            notes.Add($"{relative}: не разобран как JSON ({error.Message}); пропущен.");
            return null;
        }
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static string NamespaceOf(string entityId)
    {
        var slash = entityId.LastIndexOf('/');
        return slash <= 0 ? entityId : entityId[..slash];
    }

    /// <summary>The authored world an entity belongs to: the first id segment, e.g. urman.world:act1.</summary>
    private static string WorldKeyOf(string entityId)
    {
        var slash = entityId.IndexOf('/');
        return slash <= 0 ? entityId : entityId[..slash];
    }

    private static string SelectionOf(string resourcePath) =>
        CompiledPackName.Match(resourcePath) is { Success: true } match ? match.Groups[1].Value : "";
}
