using System.Text.Json;

namespace Urman.Core.Narrative;

/// <summary>
/// Validated logical PhotoWorlds authoring data embedded in a compiled campaign.
/// Node IDs describe authored targets; this catalog does not certify physical
/// placement, collision safety, or a runtime-ready world.
/// </summary>
public sealed record PhotoWorldCatalog(
    int SchemaVersion,
    string NamespaceId,
    IReadOnlyList<PhotoWorldPhotoDefinition> Photos,
    IReadOnlyList<PhotoWorldDefinition> Worlds,
    IReadOnlyList<string> RequiredAnchors)
{
    public const string RequiredNamespaceId = "photoworlds-v1";
    public const int CurrentSchemaVersion = 1;

    public static PhotoWorldCatalog Parse(JsonElement value)
    {
        try
        {
            RequireKind(value, JsonValueKind.Object, "PhotoWorlds catalog");
            var schemaVersion = RequiredInt(value, "schemaVersion");
            var namespaceId = RequiredString(value, "namespaceId");
            if (schemaVersion != CurrentSchemaVersion || namespaceId != RequiredNamespaceId)
                throw new InvalidDataException("The compiled PhotoWorlds catalog has an unsupported namespace or schema version.");

            var photos = RequiredArray(value, "photos").EnumerateArray()
                .Select(photo => new PhotoWorldPhotoDefinition(
                    RequiredString(photo, "id"),
                    RequiredString(photo, "pageId")))
                .ToArray();
            var worlds = RequiredArray(value, "worlds").EnumerateArray()
                .Select(world => new PhotoWorldDefinition(
                    RequiredString(world, "id"),
                    RequiredString(world, "photoId"),
                    RequiredString(world, "anchorId"),
                    RequiredString(world, "mainSpawnNodeId"),
                    RequiredString(world, "exitNodeId"),
                    RequiredArray(world, "nodes").EnumerateArray()
                        .Select(node => new PhotoWorldNodeDefinition(
                            RequiredString(node, "id"),
                            RequiredString(node, "role")))
                        .ToArray(),
                    RequiredArray(world, "requiredResourceIds").EnumerateArray()
                        .Select(resource => RequiredString(resource))
                        .ToArray()))
                .ToArray();
            var requiredAnchors = RequiredArray(value, "requiredAnchors").EnumerateArray()
                .Select(anchor => RequiredString(anchor))
                .ToArray();

            Validate(photos, worlds, requiredAnchors);
            return new(schemaVersion, namespaceId, photos, worlds, requiredAnchors);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or JsonException or FormatException)
        {
            throw new InvalidDataException("The compiled PhotoWorlds catalog is malformed.", exception);
        }
    }

    public PhotoWorldDefinition RequireWorld(string worldId) =>
        Worlds.SingleOrDefault(world => world.Id == worldId)
        ?? throw new KeyNotFoundException($"Unknown authored PhotoWorld {worldId}.");

    private static void Validate(
        IReadOnlyList<PhotoWorldPhotoDefinition> photos,
        IReadOnlyList<PhotoWorldDefinition> worlds,
        IReadOnlyList<string> requiredAnchors)
    {
        if (photos.Count != 13)
            throw new InvalidDataException($"The PhotoWorlds catalog must contain 13 authored photos; found {photos.Count}.");
        if (worlds.Count != 5)
            throw new InvalidDataException($"The PhotoWorlds catalog must contain 5 authored worlds; found {worlds.Count}.");

        var photoIds = new HashSet<string>(StringComparer.Ordinal);
        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var photo in photos)
        {
            if (!photoIds.Add(photo.Id))
                throw new InvalidDataException($"Duplicate PhotoId {photo.Id} in the compiled PhotoWorlds catalog.");
            if (!photo.PageId.StartsWith("PAGE-", StringComparison.Ordinal) || !pageIds.Add(photo.PageId))
                throw new InvalidDataException($"Duplicate or invalid PageId {photo.PageId} in the compiled PhotoWorlds catalog.");
        }

        var worldIds = new HashSet<string>(StringComparer.Ordinal);
        var worldPhotoIds = new HashSet<string>(StringComparer.Ordinal);
        var worldAnchors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var world in worlds)
        {
            if (!worldIds.Add(world.Id))
                throw new InvalidDataException($"Duplicate PhotoWorld ID {world.Id} in the compiled catalog.");
            if (!photoIds.Contains(world.PhotoId) || !worldPhotoIds.Add(world.PhotoId))
                throw new InvalidDataException($"PhotoWorld {world.Id} has an unknown or already-bound primary PhotoId {world.PhotoId}.");
            if (!worldAnchors.Add(world.AnchorId))
                throw new InvalidDataException($"Duplicate semantic anchor {world.AnchorId} in the compiled PhotoWorlds catalog.");

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            var nodeRoles = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var node in world.Nodes)
            {
                if (!nodeIds.Add(node.Id))
                    throw new InvalidDataException($"PhotoWorld {world.Id} repeats authored node {node.Id}.");
                nodeRoles.Add(node.Id, node.Role);
            }

            if (!nodeRoles.TryGetValue(world.MainSpawnNodeId, out var spawnRole) || spawnRole != "spawn_main")
                throw new InvalidDataException($"PhotoWorld {world.Id} main spawn {world.MainSpawnNodeId} must reference an authored spawn_main node.");
            if (!nodeRoles.TryGetValue(world.ExitNodeId, out var exitRole) || exitRole != "exit")
                throw new InvalidDataException($"PhotoWorld {world.Id} exit {world.ExitNodeId} must reference an authored exit node.");

            if (world.RequiredResourceIds.Count == 0
                || world.RequiredResourceIds.Distinct(StringComparer.Ordinal).Count() != world.RequiredResourceIds.Count)
                throw new InvalidDataException($"PhotoWorld {world.Id} requires a non-empty unique authored resource list.");
        }

        var supportPhotoCount = photos.Count - worldPhotoIds.Count;
        if (supportPhotoCount != 8)
            throw new InvalidDataException($"The 5-world catalog must leave exactly 8 support photos; found {supportPhotoCount}.");
        var authoredAnchors = requiredAnchors.ToHashSet(StringComparer.Ordinal);
        if (requiredAnchors.Distinct(StringComparer.Ordinal).Count() != requiredAnchors.Count
            || !worldAnchors.IsSubsetOf(authoredAnchors))
            throw new InvalidDataException("Required semantic anchors must be unique and include every authored world anchor.");
    }

    private static JsonElement RequiredArray(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property))
            throw new InvalidDataException($"PhotoWorlds catalog is missing required property {name}.");
        RequireKind(property, JsonValueKind.Array, $"PhotoWorlds {name}");
        return property;
    }

    private static string RequiredString(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var property))
            throw new InvalidDataException($"PhotoWorlds catalog is missing required string {name}.");
        return RequiredString(property);
    }

    private static string RequiredString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException("PhotoWorlds catalog contains an empty or non-string identifier.");
        return value.GetString()!;
    }

    private static int RequiredInt(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property) || !property.TryGetInt32(out var result))
            throw new InvalidDataException($"PhotoWorlds catalog is missing integer {name}.");
        return result;
    }

    private static void RequireKind(JsonElement value, JsonValueKind expected, string name)
    {
        if (value.ValueKind != expected)
            throw new InvalidDataException($"{name} must be a {expected}.");
    }
}

public sealed record PhotoWorldPhotoDefinition(string Id, string PageId);

public sealed record PhotoWorldDefinition(
    string Id,
    string PhotoId,
    string AnchorId,
    string MainSpawnNodeId,
    string ExitNodeId,
    IReadOnlyList<PhotoWorldNodeDefinition> Nodes,
    IReadOnlyList<string> RequiredResourceIds);

public sealed record PhotoWorldNodeDefinition(string Id, string Role);
