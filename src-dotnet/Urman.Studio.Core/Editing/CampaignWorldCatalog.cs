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
/// (spec AI-13, contract <c>CampaignWorldContext</c>). It is a read-only
/// projection over existing sources - the campaign manifests, the Godot entry
/// scenes, the compiled packs, the authored world plots and the photo-world
/// specifications - and keeps no registry of its own: nothing here becomes a
/// second source of truth for the compiler or the runtime, and no campaign or
/// world that is absent from those files can be resolved.
/// </summary>
public sealed class CampaignWorldCatalog
{
    /// <summary>The scene the project starts is always a startable world.</summary>
    private static readonly Regex MainSceneLine = new(
        "^run/main_scene\\s*=\\s*\"res://([^\"]+)\"", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex ExtResourceLine = new(
        "^\\[ext_resource\\s+path=\"([^\"]+)\"[^\\]]*\\bid=\"([^\"]+)\"", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex NodeLine = new(
        "^\\[node\\s+name=\"([^\"]+)\"([^\\]]*)\\]", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex NodeAttribute = new(
        "([A-Za-z_][A-Za-z0-9_]*)=\"([^\"]*)\"", RegexOptions.CultureInvariant);
    private static readonly Regex PropertyLine = new(
        "^([A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(.+)$", RegexOptions.CultureInvariant);
    private static readonly Regex ExtResourceReference = new(
        "^ExtResource\\(\"([^\"]+)\"\\)$", RegexOptions.CultureInvariant);
    private static readonly Regex QuotedValue = new(
        "^\"([^\"]*)\"$", RegexOptions.CultureInvariant);
    private static readonly Regex LoaderSignal = new(
        "EnableAct1ConnectedWorld\\s*=\\s*true", RegexOptions.CultureInvariant);
    private static readonly Regex CompiledPackName = new(
        "^res://content/([^/\"]+)\\.compiled\\.v1\\.json$", RegexOptions.CultureInvariant);

    /// <summary>One node of an entry scene with its own properties and script.</summary>
    private sealed record SceneNode(string Name, string Type, string? Parent, string? ScriptPath);

    /// <summary>A parsed entry scene: which node declares what, plus the loader signal if any.</summary>
    private sealed record SceneDocument(
        string? RootScriptPath,
        IReadOnlyList<string> CampaignResources,
        string ZoneId,
        string SpawnPointId,
        IReadOnlyList<string> LoaderSignals,
        IReadOnlyList<string> LoaderScripts,
        string? Ambiguity);

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

    /// <summary>
    /// Worlds that declare this campaign, plus worlds that declare none and can
    /// therefore host any campaign. A world that declares a different campaign is
    /// never offered for this one.
    /// </summary>
    public IEnumerable<WorldRef> WorldsFor(string campaignSelection) =>
        Worlds.Where(world => string.Equals(world.CampaignSelection, campaignSelection, StringComparison.Ordinal)
            || !world.CampaignDeclaredByScene);

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
    public CampaignWorldContext Default(string baseRevision = "")
    {
        var campaign = SelectableCampaigns.FirstOrDefault()
            ?? throw new InvalidOperationException("В этом checkout нет ни одной действующей кампании: content/campaigns содержит только архивные примеры.");
        var world = WorldsFor(campaign.Selection).FirstOrDefault()
            ?? throw new InvalidOperationException($"Для кампании «{campaign.Selection}» нет ни одной стартовой сцены мира.");
        return Resolve(campaign.Selection, world.ScenePath, baseRevision: baseRevision);
    }

    /// <summary>
    /// Pairs one real campaign with one real world scene. A scene that declares a
    /// different campaign is refused (its binding is real, not a choice); a scene
    /// that declares none is marked as an explicit selection. Unknown input is a
    /// clear error, never a silently invented default.
    /// </summary>
    public CampaignWorldContext Resolve(
        string campaignSelection,
        string scenePath,
        string? zoneId = null,
        string? spawnPointId = null,
        string baseRevision = "")
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

        var declared = world.DeclaredCampaignResource;
        if (declared is not null)
        {
            var declaredSelection = SelectionOf(declared);
            if (!string.Equals(declaredSelection, campaignSelection, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Сцена «{scenePath}» сама объявляет кампанию «{declaredSelection}» (CampaignResourcePath), поэтому её нельзя открыть как «{campaignSelection}». " +
                    "Выберите объявленную кампанию либо сцену без объявленной кампании.");
            }
        }

        var resolved = world with
        {
            CampaignSelection = campaignSelection,
            ZoneId = string.IsNullOrWhiteSpace(zoneId) ? world.ZoneId : zoneId,
            SpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? world.SpawnPointId : spawnPointId
        };
        var dependencies = AuthoredDependencies(campaign, resolved);
        return new CampaignWorldContext(
            campaign,
            resolved,
            HashFiles(dependencies),
            baseRevision,
            dependencies,
            campaign.CompiledPackPath,
            declared is null ? CampaignWorldContext.CampaignBindingExplicit : CampaignWorldContext.CampaignBindingDeclared);
    }

    /// <summary>
    /// The authored files a saved change in this world depends on: the campaign
    /// manifest, the entry scene and its compiled pack, the scripts that are read
    /// while resolving the loader signal, plus every world plot when the loader
    /// reads the plot directory. Changing any of them changes the content
    /// fingerprint.
    /// </summary>
    public IReadOnlyList<string> AuthoredDependencies(CampaignRef campaign, WorldRef world)
    {
        var dependencies = new List<string>
        {
            campaign.RelativePath,
            "game/" + world.ScenePath["res://".Length..]
        };
        if (File.Exists(Path.Combine(Root, campaign.CompiledPackPath.Replace('/', Path.DirectorySeparatorChar))))
        {
            dependencies.Add(campaign.CompiledPackPath);
        }

        if (world.Capability == WorldAuthoringCapability.AuthoredWorldPlots)
        {
            dependencies.AddRange(AuthoredWorldPlots.Select(plot => plot.RelativePath));
        }

        // Scripts are read while resolving the loader signal, so a change there
        // (for example turning EnableAct1ConnectedWorld off without committing)
        // must move the fingerprint as well.
        dependencies.AddRange(world.LoaderScripts.Select(script => "game/" + script["res://".Length..]));

        return dependencies.Order(StringComparer.Ordinal).ToArray();
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

            var relative = scenePath["res://".Length..];
            var file = Path.Combine(scenesRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file))
            {
                notes.Add($"{scenePath}: файла сцены нет, пропущена.");
                continue;
            }

            var document = ParseScene(scenesRoot, relative, File.ReadAllText(file), notes);
            var isMain = string.Equals(mainScene, scenePath, StringComparison.Ordinal);
            if (document is null)
            {
                continue;
            }

            if (document.Ambiguity is { } ambiguity)
            {
                notes.Add($"{scenePath}: {ambiguity} Сцена пропущена, пока объявление не станет однозначным.");
                continue;
            }

            if (!isMain && document.CampaignResources.Count == 0 && document.ZoneId.Length == 0)
            {
                continue;
            }

            var declaredResource = document.CampaignResources.Count == 1 ? document.CampaignResources[0] : null;
            var selection = declaredResource is not null ? SelectionOf(declaredResource) : "";
            var evidence = new List<string>
            {
                document.LoaderSignals.Count > 0
                    ? $"Сигнал загрузчика авторского мира: {string.Join(", ", document.LoaderSignals)}."
                    : "Сигнала загрузчика авторского мира в сцене и её скриптах нет."
            };
            if (isMain)
            {
                evidence.Add("Стартовая сцена проекта: game/project.godot run/main_scene.");
            }

            if (declaredResource is not null)
            {
                evidence.Add($"Сцена сама объявляет кампанию: CampaignResourcePath = {declaredResource}.");
            }
            else
            {
                evidence.Add("Сцена не объявляет кампанию: связь задаётся кодом, поэтому это явный выбор автора.");
            }

            if (document.ZoneId.Length == 0)
            {
                evidence.Add("Сцена не объявляет зону и спавн: их задаёт код, поэтому в контексте они остаются пустыми до явного выбора.");
            }

            var capability = document.LoaderSignals.Count > 0
                ? WorldAuthoringCapability.AuthoredWorldPlots
                : WorldAuthoringCapability.PresentationOnly;
            if (capability == WorldAuthoringCapability.PresentationOnly && plots.Count > 0)
            {
                evidence.Add($"Авторских world plot в checkout: {plots.Count}, но эта сцена их не загружает.");
            }

            if (document.RootScriptPath is null)
            {
                evidence.Add("Корневой скрипт сцены не разрешён, поэтому capability определена консервативно.");
            }

            var worldId = Path.GetFileNameWithoutExtension(scenePath);
            var title = zoneTitles.TryGetValue(document.ZoneId, out var zoneTitle) && zoneTitle.Length > 0 ? zoneTitle : worldId;
            worlds.Add(new WorldRef(selection, worldId, title, scenePath, document.ZoneId, document.SpawnPointId, declaredResource, capability, evidence, document.LoaderScripts));
        }

        return worlds;
    }

    /// <summary>
    /// Parses one scene into node-scoped facts. Properties are read from the node
    /// that declares them, and the loader signal is looked up in the scene body and
    /// in the scripts those nodes reference. Conflicting declarations are reported
    /// as ambiguity instead of being guessed.
    /// </summary>
    private static SceneDocument? ParseScene(string scenesRoot, string sceneRelative, string text, List<string> notes)
    {
        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in ExtResourceLine.Matches(text))
        {
            resources[match.Groups[2].Value] = match.Groups[1].Value;
        }

        var nodes = new List<(SceneNode Node, Dictionary<string, string> Properties)>();
        var nodeMatches = NodeLine.Matches(text);
        for (var index = 0; index < nodeMatches.Count; index++)
        {
            var match = nodeMatches[index];
            var bodyStart = match.Index + match.Length;
            var bodyEnd = index + 1 < nodeMatches.Count ? nodeMatches[index + 1].Index : text.Length;
            var body = text[bodyStart..bodyEnd];

            string? script = null;
            var properties = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in body.Split('\n'))
            {
                var property = PropertyLine.Match(line.Trim());
                if (!property.Success)
                {
                    continue;
                }

                var name = property.Groups[1].Value;
                var value = property.Groups[2].Value.Trim();
                if (name == "script")
                {
                    if (ExtResourceReference.Match(value) is { Success: true } reference
                        && resources.TryGetValue(reference.Groups[1].Value, out var scriptPath))
                    {
                        script = scriptPath;
                    }

                    continue;
                }

                properties[name] = value;
            }

            string type = "";
            string? parent = null;
            foreach (Match attribute in NodeAttribute.Matches(match.Groups[2].Value))
            {
                switch (attribute.Groups[1].Value)
                {
                    case "type":
                        type = attribute.Groups[2].Value;
                        break;
                    case "parent":
                        parent = attribute.Groups[2].Value;
                        break;
                }
            }

            nodes.Add((new SceneNode(match.Groups[1].Value, type, parent, script), properties));
        }

        if (nodes.Count == 0)
        {
            notes.Add($"{sceneRelative}: узлов не найдено, сцена не разобрана.");
            return null;
        }

        var roots = nodes.Where(node => node.Node.Parent is null).ToArray();
        var rootScript = roots.Length == 1 ? roots[0].Node.ScriptPath : null;

        var campaignResources = nodes
            .Select(node => node.Properties.TryGetValue("CampaignResourcePath", out var value) ? Unquote(value) : null)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var ambiguity = campaignResources.Length > 1
            ? $"сцена объявляет несколько разных CampaignResourcePath: {string.Join(", ", campaignResources)}."
            : null;

        var zone = ReadDeclared(nodes, "InitialZoneId", "CurrentZoneId");
        var spawn = ReadDeclared(nodes, "InitialSpawnPointId", "CurrentSpawnPointId");
        if (ambiguity is null && zone.Conflicting)
        {
            ambiguity = $"сцена объявляет разные InitialZoneId: {string.Join(", ", zone.Values)}.";
        }

        if (ambiguity is null && spawn.Conflicting)
        {
            ambiguity = $"сцена объявляет разные InitialSpawnPointId: {string.Join(", ", spawn.Values)}.";
        }

        var signals = new List<string>();
        var loaderScripts = new List<string>();
        if (LoaderSignal.IsMatch(text))
        {
            signals.Add($"{sceneRelative}: EnableAct1ConnectedWorld = true");
        }

        foreach (var script in nodes.Select(node => node.Node.ScriptPath).Where(path => path is not null).Distinct(StringComparer.Ordinal))
        {
            loaderScripts.Add(script!);
            var scriptRelative = script!["res://".Length..];
            var scriptFile = Path.Combine(scenesRoot, scriptRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(scriptFile))
            {
                continue;
            }

            var scriptText = File.ReadAllText(scriptFile);
            var match = LoaderSignal.Match(scriptText);
            if (match.Success)
            {
                var line = scriptText[..match.Index].Count(character => character == '\n') + 1;
                signals.Add($"game/{scriptRelative}:{line}");
            }
        }

        return new SceneDocument(rootScript, campaignResources, zone.Value, spawn.Value, signals, loaderScripts, ambiguity);
    }

    /// <summary>Reads a zone/spawn declaration, preferring the Initial form and reporting conflicts.</summary>
    private static (string Value, bool Conflicting, IReadOnlyList<string> Values) ReadDeclared(
        IReadOnlyList<(SceneNode Node, Dictionary<string, string> Properties)> nodes,
        string initialKey,
        string currentKey)
    {
        foreach (var key in new[] { initialKey, currentKey })
        {
            var values = nodes
                .Select(node => node.Properties.TryGetValue(key, out var value) ? Unquote(value) : null)
                .Where(value => !string.IsNullOrEmpty(value))
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (values.Length > 0)
            {
                return (values[0], values.Length > 1, values);
            }
        }

        return ("", false, []);
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
                $"Авторских миров в checkout: {worldKeys.Length} ({string.Join(", ", worldKeys)}). Полный done_when AI-13 требует двух независимо авторируемых миров с runtime consumers и изолированными правками; две стартовые сцены за два мира не считаются."));
        }

        if (worlds.Any(world => !world.CampaignDeclaredByScene))
        {
            blockers.Add(new CatalogBlocker(
                "campaign-world-link-undeclared",
                "Связь «кампания → мир» для сцены без CampaignResourcePath нигде не объявлена: campaign.json не ссылается на сцену или зону. До явного контракта такая пара выбирается автором и помечается как explicit-selection."));
        }

        var specified = specifications.Where(spec => spec.Capability == WorldAuthoringCapability.SpecifiedNotImplemented).ToArray();
        if (specified.Length > 0)
        {
            blockers.Add(new CatalogBlocker(
                "photo-worlds-specified-only",
                $"PhotoWorlds без сцены и runtime consumer: {string.Join(", ", specified.Select(spec => $"{spec.WorldId} «{spec.Title}» ({spec.Status})"))}. Их нельзя показывать редактируемыми."));
        }

        if (!worlds.Any(world => world.Capability == WorldAuthoringCapability.AuthoredWorldPlots))
        {
            blockers.Add(new CatalogBlocker(
                "no-authored-world-loader",
                "Ни одна стартовая сцена не даёт положительного сигнала загрузчика авторского мира, поэтому ни один мир нельзя открыть как редактируемый."));
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

    private static string Unquote(string value) =>
        QuotedValue.Match(value) is { Success: true } match ? match.Groups[1].Value : value;

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
