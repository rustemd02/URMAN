using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Content.Resolvers;

public enum ResolverErrorCode
{
    InvalidPack,
    DuplicateLogicalId,
    MissingAsset,
    MissingText,
    MissingVocabulary,
    MissingVariant,
    MissingVariable,
    InvalidVariable,
    InvalidLocale,
    InvalidResolvedUrl,
    InvalidAccessibility,
    WrongAssetKind
}

public sealed class ResolverException : Exception
{
    public ResolverException(
        ResolverErrorCode code,
        string message,
        IReadOnlyDictionary<string, object?>? details = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Details = details ?? ResolverDetails.Empty;
    }

    public ResolverErrorCode Code { get; }

    public IReadOnlyDictionary<string, object?> Details { get; }
}

public sealed record ResolverOrigin(string Id, string ModuleId, string SourcePointer);

public sealed record ResolverCatalogEntry(
    JsonObject Value,
    ResolverOrigin Origin,
    int ModuleIndex,
    int SourceIndex);

public sealed class ResolverCatalog
{
    private static readonly IReadOnlyDictionary<string, string> CategoryKinds =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["assets"] = "asset",
            ["capabilities"] = "capability",
            ["characters"] = "character",
            ["dialogues"] = "dialogue",
            ["documents"] = "document",
            ["knowledge"] = "knowledge",
            ["quests"] = "quest",
            ["scenes"] = "scene",
            ["texts"] = "text",
            ["vocabulary"] = "vocabulary"
        });

    private static readonly Regex LogicalIdPattern = new(
        "^([a-z][a-z0-9]*(?:[.-][a-z0-9]+)*):([a-z][a-z0-9-]*)/([a-z0-9][a-z0-9._-]*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, ResolverCatalogEntry>> _categories;

    private ResolverCatalog(IReadOnlyDictionary<string, IReadOnlyDictionary<string, ResolverCatalogEntry>> categories)
    {
        _categories = categories;
    }

    public static ResolverCatalog Create(JsonObject pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var campaign = ResolverJson.RequiredObject(pack, "campaign", "CompiledContentPack");
        var orderedModules = ResolverJson.RequiredArray(campaign, "orderedModules", "CompiledContentPack.campaign");
        var moduleOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < orderedModules.Count; index++)
        {
            var dependency = ResolverJson.RequiredObject(orderedModules[index], $"CompiledContentPack.campaign.orderedModules/{index}");
            var moduleId = ResolverJson.RequiredString(dependency, "moduleId", $"CompiledContentPack.campaign.orderedModules/{index}");
            if (!moduleOrder.TryAdd(moduleId, index))
            {
                throw ResolverErrors.Create(
                    ResolverErrorCode.InvalidPack,
                    $"Campaign contains duplicate module {moduleId}.",
                    extra: new Dictionary<string, object?>
                    {
                        ["moduleId"] = moduleId,
                        ["sourcePointer"] = $"/campaign/orderedModules/{index}/moduleId"
                    });
            }
        }

        var registries = ResolverJson.RequiredObject(pack, "registries", "CompiledContentPack");
        var categories = new Dictionary<string, IReadOnlyDictionary<string, ResolverCatalogEntry>>(StringComparer.Ordinal);
        foreach (var (category, expectedKind) in CategoryKinds)
        {
            var registry = ResolverJson.RequiredArray(registries, category, "CompiledContentPack.registries");
            var normalized = new List<ResolverCatalogEntry>(registry.Count);
            for (var sourceIndex = 0; sourceIndex < registry.Count; sourceIndex++)
            {
                var value = ResolverJson.RequiredObject(registry[sourceIndex], $"CompiledContentPack.registries.{category}[{sourceIndex}]");
                var id = ResolverJson.RequiredString(value, "id", $"CompiledContentPack.registries.{category}[{sourceIndex}]");
                var match = LogicalIdPattern.Match(id);
                var origin = ResolverErrors.Origin(category, sourceIndex, id);
                if (!match.Success || !string.Equals(match.Groups[2].Value, expectedKind, StringComparison.Ordinal))
                {
                    throw ResolverErrors.Create(
                        ResolverErrorCode.InvalidPack,
                        $"Registry entry {id} does not match {category}.",
                        origin);
                }

                var moduleId = match.Groups[1].Value;
                if (!moduleOrder.TryGetValue(moduleId, out var moduleIndex))
                {
                    throw ResolverErrors.Create(
                        ResolverErrorCode.InvalidPack,
                        $"Registry entry {id} belongs to a module outside the resolved campaign.",
                        origin);
                }

                normalized.Add(new ResolverCatalogEntry(
                    (JsonObject)value.DeepClone(),
                    origin,
                    moduleIndex,
                    sourceIndex));
            }

            var byId = new Dictionary<string, ResolverCatalogEntry>(StringComparer.Ordinal);
            foreach (var entry in normalized
                         .OrderBy(value => value.ModuleIndex)
                         .ThenBy(value => value.SourceIndex)
                         .ThenBy(value => value.Value["id"]!.GetValue<string>(), StringComparer.Ordinal))
            {
                var id = entry.Value["id"]!.GetValue<string>();
                if (byId.TryGetValue(id, out var existing))
                {
                    throw ResolverErrors.Create(
                        ResolverErrorCode.DuplicateLogicalId,
                        $"Duplicate logical ID {id}.",
                        entry.Origin,
                        new Dictionary<string, object?>
                        {
                            ["firstSourcePointer"] = existing.Origin.SourcePointer,
                            ["firstModuleId"] = existing.Origin.ModuleId
                        });
                }

                byId.Add(id, entry);
            }

            categories.Add(category, new ReadOnlyDictionary<string, ResolverCatalogEntry>(byId));
        }

        return new ResolverCatalog(
            new ReadOnlyDictionary<string, IReadOnlyDictionary<string, ResolverCatalogEntry>>(categories));
    }

    public IReadOnlyDictionary<string, ResolverCatalogEntry> Category(string category) =>
        _categories.TryGetValue(category, out var entries)
            ? entries
            : throw ResolverErrors.Create(ResolverErrorCode.InvalidPack, $"Unknown resolver category {category}.");

    public IReadOnlyList<string> Ids(string category) => Category(category).Keys.ToArray();

    public ResolverCatalogEntry Require(string category, string id, ResolverOrigin? from = null)
    {
        if (Category(category).TryGetValue(id, out var entry))
        {
            return entry;
        }

        var code = category switch
        {
            "assets" => ResolverErrorCode.MissingAsset,
            "texts" => ResolverErrorCode.MissingText,
            _ => ResolverErrorCode.MissingVocabulary
        };
        throw ResolverErrors.Create(
            code,
            $"Missing {category[..^1]} {id}.",
            from ?? ResolverErrors.AbsentOrigin(category, id),
            new Dictionary<string, object?> { ["id"] = id });
    }
}

internal static class ResolverErrors
{
    public static ResolverOrigin Origin(string category, int index, string? id, string suffix = "") =>
        new(id ?? string.Empty, ModuleIdFromLogicalId(id) ?? "unknown-module", $"/registries/{category}/{index}{suffix}");

    public static ResolverOrigin AbsentOrigin(string category, string id) =>
        new(id, ModuleIdFromLogicalId(id) ?? "unknown-module", $"/registries/{category}");

    public static ResolverOrigin Reference(ResolverOrigin origin, string suffix) =>
        origin with { SourcePointer = $"{origin.SourcePointer}{suffix}" };

    public static ResolverException Create(
        ResolverErrorCode code,
        string message,
        ResolverOrigin? origin = null,
        IReadOnlyDictionary<string, object?>? extra = null,
        Exception? innerException = null)
    {
        var details = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (origin is not null)
        {
            details["id"] = origin.Id;
            details["moduleId"] = origin.ModuleId;
            details["sourcePointer"] = origin.SourcePointer;
        }

        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                details[key] = value;
            }
        }

        return new ResolverException(code, message, new ReadOnlyDictionary<string, object?>(details), innerException);
    }

    private static string? ModuleIdFromLogicalId(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var separator = id.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 && id.IndexOf('/', separator) >= 0 ? id[..separator] : null;
    }
}

internal static class ResolverDetails
{
    public static readonly IReadOnlyDictionary<string, object?> Empty =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
}

internal static class ResolverJson
{
    public static JsonObject RequiredObject(JsonObject owner, string key, string label) =>
        owner[key] as JsonObject
        ?? throw ResolverErrors.Create(
            ResolverErrorCode.InvalidPack,
            $"{label}.{key} must be an object.",
            extra: new Dictionary<string, object?> { ["sourcePointer"] = $"{label}/{key}" });

    public static JsonObject RequiredObject(JsonNode? node, string label) =>
        node as JsonObject
        ?? throw ResolverErrors.Create(
            ResolverErrorCode.InvalidPack,
            $"{label} must be an object.",
            extra: new Dictionary<string, object?> { ["sourcePointer"] = label });

    public static JsonArray RequiredArray(JsonObject owner, string key, string label) =>
        owner[key] as JsonArray
        ?? throw ResolverErrors.Create(
            ResolverErrorCode.InvalidPack,
            $"{label}.{key} must be an array.",
            extra: new Dictionary<string, object?> { ["sourcePointer"] = $"{label}/{key}" });

    public static string RequiredString(JsonObject owner, string key, string label)
    {
        if (owner[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text))
        {
            return text;
        }

        throw ResolverErrors.Create(
            ResolverErrorCode.InvalidPack,
            $"{label}.{key} must be a non-empty string.",
            extra: new Dictionary<string, object?> { ["sourcePointer"] = $"{label}/{key}" });
    }

    public static string RequiredString(JsonObject owner, string key, ResolverOrigin origin) =>
        owner[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text)
            ? text
            : throw ResolverErrors.Create(
                ResolverErrorCode.InvalidPack,
                $"{origin.Id}.{key} must be a non-empty string.",
                ResolverErrors.Reference(origin, $"/{key}"));
}
