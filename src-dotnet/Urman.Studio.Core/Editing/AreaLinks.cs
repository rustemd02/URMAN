using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One place the authored logic names as its location, and who declares that place.</summary>
public sealed record AreaLinkFacts(
    string LocationId,
    int Uses,
    string DeclaredBy,
    string UsedBy,
    IReadOnlyList<string> Problems);

/// <summary>
/// The world-area half of the spec AI-21 slice. A quest, a dialogue or a fact can name the
/// place it happens in with a location.is condition, and that place is declared by a campaign
/// manifest's narrativeOrder. Two things then decide whether the condition can ever be true:
///
///   * the place must be declared at all - an unknown location id is a branch that never runs;
///   * it must be declared by the campaign that also owns the module using it - a reference
///     across campaigns is true only when both campaigns are loaded together, and silently
///     false in a single-campaign run.
///
/// The module-to-campaign map comes from the manifests, so this report follows the content.
/// </summary>
public static class AreaLinks
{
    public const string CampaignsFolder = "content/campaigns";

    public static IReadOnlyList<AreaLinkFacts> Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var campaigns = Campaigns(full);
        var byModule = campaigns
            .SelectMany(campaign => campaign.Modules.Select(module => (module, campaign.Id)))
            .GroupBy(pair => pair.module, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Id).ToArray(), StringComparer.Ordinal);

        var uses = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity)
            {
                continue;
            }

            var module = ModuleOf(workspace.Locate(id)?.RelativePath);
            Collect(entity, id, module, uses);
        }

        var links = new List<AreaLinkFacts>();
        foreach (var (locationId, sources) in uses.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var declaredBy = campaigns
                .Where(campaign => campaign.Locations.Contains(locationId))
                .Select(campaign => campaign.Id)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var usedBy = sources.Select(source => source.Split('|')[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var problems = new List<string>();
            if (declaredBy.Length == 0)
            {
                problems.Add($"Место «{locationId}» не объявлено ни одним манифестом кампании: условие location.is не станет истинным.");
            }

            foreach (var module in sources.Select(source => source.Split('|')[1]).Distinct(StringComparer.Ordinal))
            {
                if (module.Length == 0)
                {
                    continue;
                }

                if (!byModule.TryGetValue(module, out var owners))
                {
                    problems.Add($"Модуль «{module}» не перечислен ни одним манифестом: неизвестно, в какой кампании эта ссылка действует.");
                    continue;
                }

                if (declaredBy.Length > 0 && !owners.Any(declaredBy.Contains))
                {
                    problems.Add($"Ссылку использует модуль «{module}» кампании {string.Join("/", owners)}, а место объявлено в {string.Join("/", declaredBy)}: в прогоне одной кампании условие ложно.");
                }
            }

            links.Add(new AreaLinkFacts(locationId, sources.Count, string.Join(", ", declaredBy), string.Join(", ", usedBy), problems));
        }

        return links;
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var links = Inspect(workspace, root);
        var problems = links.Sum(link => link.Problems.Count);
        return
        [
            $"Ссылок на места (location.is): {links.Count}; использований: {links.Sum(link => link.Uses)}; проблем: {problems}.",
            "Каждое место сверяется с narrativeOrder манифестов, а модуль-потребитель — с их списком modules, поэтому ссылка из чужой кампании видна как ссылка из чужой кампании, а не как рабочая.",
            "Триггеров как отдельной сущности в кампании нет: привязка «триггер → область» возможна только через эти условия и через рантайм, который читает текущее место.",
            "Проверяется только разрешимость и принадлежность ссылки: правильность сюжетной привязки — авторская приёмка."
        ];
    }

    private static void Collect(JsonNode? node, string entityId, string module, Dictionary<string, List<string>> uses)
    {
        switch (node)
        {
            case JsonObject obj:
                if (AuthoredJson.Text(obj, "op") == "location.is" && AuthoredJson.Text(obj, "locationId") is { Length: > 0 } locationId)
                {
                    if (!uses.TryGetValue(locationId, out var list))
                    {
                        uses[locationId] = list = [];
                    }

                    list.Add($"{entityId}|{module}");
                }

                foreach (var pair in obj)
                {
                    Collect(pair.Value, entityId, module, uses);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Collect(item, entityId, module, uses);
                }

                break;
        }
    }

    /// <summary>The module folder a source path belongs to, empty when the path is outside them.</summary>
    private static string ModuleOf(string? relativePath)
    {
        const string prefix = "content/modules/";
        if (relativePath is null || !relativePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return "";
        }

        var rest = relativePath[prefix.Length..];
        var separator = rest.IndexOf('/');
        return separator < 0 ? rest : rest[..separator];
    }

    private sealed record CampaignManifest(string Id, IReadOnlyList<string> Modules, IReadOnlySet<string> Locations);

    private static IReadOnlyList<CampaignManifest> Campaigns(string root)
    {
        var folder = Path.Combine(root, CampaignsFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var campaigns = new List<CampaignManifest>();
        foreach (var file in Directory.EnumerateFiles(folder, "campaign.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject manifest)
            {
                continue;
            }

            var id = AuthoredJson.TextOrNull(manifest, "id") ?? Path.GetFileName(Path.GetDirectoryName(file)) ?? "";
            var modules = (manifest["modules"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(module => (AuthoredJson.Text(module, "moduleId")).Replace('.', '-'))
                .Where(module => module.Length > 0)
                .ToArray();
            var locations = new HashSet<string>(StringComparer.Ordinal);
            if (manifest["narrativeOrder"] is JsonArray order)
            {
                foreach (var item in order.OfType<JsonValue>())
                {
                    if (item.GetValue<string>() is { Length: > 0 } location)
                    {
                        locations.Add(location);
                    }
                }
            }

            campaigns.Add(new CampaignManifest(id, modules, locations));
        }

        return campaigns;
    }
}
