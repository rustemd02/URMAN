using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urman.Studio.Core.AI;

/// <summary>The real campaign/world view that scopes one AI authoring request.</summary>
public sealed record HouseAuthoringEnvironment(
    string BaseRevision,
    string CampaignId,
    string CampaignVersion,
    string WorldId,
    string StateId,
    string ScenePath,
    string ZoneId,
    string SpawnPointId,
    JsonElement Camera,
    IReadOnlyDictionary<string, string> DependencyHashes)
{
    private JsonElement _camera = CloneCamera(Camera);
    private IReadOnlyDictionary<string, string> _dependencyHashes = Copy(DependencyHashes);

    public JsonElement Camera
    {
        get => _camera;
        init => _camera = CloneCamera(value);
    }

    public IReadOnlyDictionary<string, string> DependencyHashes
    {
        get => _dependencyHashes;
        init => _dependencyHashes = Copy(value);
    }

    internal HouseAuthoringEnvironment Freeze() => this with
    {
        Camera = Camera,
        DependencyHashes = DependencyHashes
    };

    private static JsonElement CloneCamera(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined ? default : value.Clone();

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> values) =>
        new ReadOnlyDictionary<string, string>(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
}

/// <summary>Builds and hashes the fixed, bounded Studio v2 request envelope.</summary>
public static class AuthoringRequestV2
{
    public const string Kind = "urman.studio.authoring-request";
    public const int SchemaVersion = 2;
    public const string ProjectId = "github:rustemd02/URMAN";
    private const string OperationId = "house.windows.resize";
    private const string SharedAssetPolicy = "create-instance-variant";
    private static readonly string[] AllWindowIds =
    [
        "Front0", "Front1", "Front2", "Rear0", "Rear1", "Left0", "Right0", "SeniOuter0"
    ];
    private static readonly JsonSerializerOptions WireJsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>Serializes the v2 envelope after confirming its bound snapshot hash.</summary>
    public static string Serialize(HouseWindowRequest request)
    {
        HouseWindowProposalValidator.ValidateRequest(request);
        if (request.Environment is null)
            throw new ArgumentException("A v2 authoring request requires a frozen environment.", nameof(request));

        var bytes = JsonSerializer.SerializeToUtf8Bytes(Build(request, request.SnapshotHash), WireJsonOptions);
        EnsureWithinLimit(bytes, nameof(request));
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Describes exactly the frozen v2 envelope that will be sent to the provider.</summary>
    public static string Describe(HouseWindowRequest request)
    {
        using var document = JsonDocument.Parse(Serialize(request));
        var root = document.RootElement;
        var project = root.GetProperty("project");
        var selection = root.GetProperty("selection");
        var camera = selection.GetProperty("camera");
        var entityIds = ReadStringArray(selection.GetProperty("entityIds"));
        var partIds = ReadStringArray(selection.GetProperty("partIds"));
        var sources = root.GetProperty("sources").EnumerateArray().ToArray();
        var sourcesDifferingOrWithoutBase = sources.Count(source =>
        {
            var baseHash = source.GetProperty("baseHash");
            return baseHash.ValueKind == JsonValueKind.Null
                || !string.Equals(baseHash.GetString(), source.GetProperty("currentHash").GetString(), StringComparison.Ordinal);
        });
        var sourcesWithoutBase = sources.Count(source => source.GetProperty("baseHash").ValueKind == JsonValueKind.Null);
        var dependencyCount = root.GetProperty("dependencies").EnumerateObject().Count();
        var resourceSets = root.GetProperty("resourceSets");
        var readPaths = ReadStringArray(resourceSets.GetProperty("read"));
        var writePaths = ReadStringArray(resourceSets.GetProperty("write"));
        var sourceDescriptions = sources.Select(DescribeSource).ToArray();

        return string.Join('\n',
            $"Цели: сущностей {entityIds.Length}; частей окон {partIds.Length}; ID сущностей: {string.Join(", ", entityIds)}; ID частей: {string.Join(", ", partIds)}.",
            $"Контекст: кампания {project.GetProperty("campaignId").GetString()} ({project.GetProperty("campaignVersion").GetString()}); мир {project.GetProperty("worldId").GetString()}; состояние {project.GetProperty("stateId").GetString()}; сцена {project.GetProperty("scenePath").GetString()}; зона {project.GetProperty("zoneId").GetString()}; точка {project.GetProperty("spawnPointId").GetString()}.",
            $"Камера/viewport: {DescribeCamera(camera)}; изображение не включено.",
            $"Источники: {sources.Length}; отличаются от сохранённой базы или без неё: {sourcesDifferingOrWithoutBase} (без базы: {sourcesWithoutBase}). База — сохранённые байты при загрузке Studio, не Git-ревизия; хеши относятся к полным файлам, не к фрагментам data.",
            $"  {string.Join("\n  ", sourceDescriptions)}",
            $"Зависимости только для чтения: {dependencyCount}; переданы только пути и SHA-256, содержимое не приложено.",
            $"Входные ресурсы snapshot: {string.Join(", ", readPaths)}.",
            $"Файлы, которые Studio изменит после «Применить»: {string.Join(", ", writePaths)}.",
            $"Request ID: {root.GetProperty("requestId").GetString()}; отпечаток запроса SHA-256: {project.GetProperty("snapshotHash").GetString()}.");
    }

    /// <summary>Hashes the v2 envelope with only project.snapshotHash blanked.</summary>
    public static string ComputeSnapshotHash(HouseWindowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Environment is null)
            throw new ArgumentException("A v2 authoring request requires a frozen environment.", nameof(request));

        HouseWindowProposalValidator.ValidateRequestForSnapshotHash(request);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Build(request, string.Empty), WireJsonOptions);
        EnsureWithinLimit(bytes, nameof(request));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    internal static void ValidateEnvironment(HouseAuthoringEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.BaseRevision is null || !IsRevision(environment.BaseRevision))
            throw new ArgumentException("BaseRevision must be a full 40- or 64-character hexadecimal revision.", nameof(environment));

        ValidateActualString(environment.CampaignId, nameof(environment.CampaignId), 256);
        ValidateActualString(environment.CampaignVersion, nameof(environment.CampaignVersion), 128);
        ValidateActualString(environment.WorldId, nameof(environment.WorldId), 256);
        ValidateActualString(environment.StateId, nameof(environment.StateId), 512);
        ValidateActualString(environment.ZoneId, nameof(environment.ZoneId), 256);
        ValidateActualString(environment.SpawnPointId, nameof(environment.SpawnPointId), 256);
        if (!IsSafeScenePath(environment.ScenePath))
            throw new ArgumentException("ScenePath must be a safe project-relative res:// path.", nameof(environment));

        if (environment.DependencyHashes is null || environment.DependencyHashes.Count == 0)
            throw new ArgumentException("DependencyHashes must contain the real generation dependencies.", nameof(environment));

        foreach (var (path, hash) in environment.DependencyHashes)
        {
            if (!IsSafeRelativePath(path) || !IsLowercaseSha256(hash))
                throw new ArgumentException("DependencyHashes must map safe relative paths to lowercase SHA-256 hashes.", nameof(environment));
        }

        ValidateCamera(environment.Camera);
    }

    private static RequestEnvelope Build(HouseWindowRequest request, string snapshotHash)
    {
        var environment = request.Environment ?? throw new ArgumentException("A v2 authoring request requires a frozen environment.", nameof(request));
        var partIds = GetTargetPartIds(request);
        var excludedPartIds = Array.AsReadOnly(AllWindowIds.Where(id => !partIds.Contains(id, StringComparer.Ordinal)).ToArray());
        var targetSetHash = ComputeTargetSetHash(request.EntityId, partIds);
        var dependencies = new SortedDictionary<string, string>(
            environment.DependencyHashes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var currentWidths = new SortedDictionary<string, decimal>(
            request.CurrentWidths!.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var sources = Array.AsReadOnly(request.Sources!.OrderBy(source => source.Path, StringComparer.Ordinal).ToArray());
        var readResources = environment.DependencyHashes.Keys
            .Append(HouseWindowProposalValidator.WorldSourcePath)
            .Append(HouseWindowProposalValidator.RecipeSourcePath)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var writeResources = Array.AsReadOnly(new[]
        {
            HouseWindowProposalValidator.RecipeSourcePath,
            "game/assets/models/studio/hero-house-" + request.RequestId + ".glb",
            "game/assets/models/studio/hero-house-" + request.RequestId + ".glb.manifest.json",
            "assets/source/blender/studio/hero-house-" + request.RequestId + ".blend"
        }.Order(StringComparer.Ordinal).ToArray());

        return new RequestEnvelope(
            Kind,
            SchemaVersion,
            request.RequestId,
            request.Prompt,
            new ProjectDetails(
                ProjectId,
                environment.BaseRevision,
                environment.CampaignId,
                environment.CampaignVersion,
                environment.WorldId,
                environment.StateId,
                environment.ScenePath,
                environment.ZoneId,
                environment.SpawnPointId,
                snapshotHash),
            new SelectionDetails("entity-parts", Array.AsReadOnly(new[] { request.EntityId }), partIds, environment.Camera),
            new ScopeDetails(
                "instances",
                Array.AsReadOnly(new[] { request.EntityId }),
                Array.Empty<string>(),
                excludedPartIds,
                targetSetHash,
                SharedAssetPolicy),
            sources,
            new ReadOnlyDictionary<string, string>(dependencies),
            new ResourceSets(Array.AsReadOnly(readResources), writeResources),
            Array.AsReadOnly(new[] { new Capability(OperationId, 1) }),
            new Constraints(
                Array.AsReadOnly(new[] { "entityId", "address", "doorPortal", "storyBindings" }),
                RulesVersion: 1,
                KeepHeight: true,
                MustPreserveOthers: true,
                AllowExecutableChanges: false),
            "Проёмы и все части выбранных окон согласованы; остальные дома не изменены",
            new Parameters(request.CurrentWidth, new ReadOnlyDictionary<string, decimal>(currentWidths)));
    }

    private static IReadOnlyList<string> GetTargetPartIds(HouseWindowRequest request)
    {
        if (request.PartId is not null)
            return Array.AsReadOnly(new[] { request.PartId });

        if (request.PartIds is not null)
            return Array.AsReadOnly(request.PartIds.ToArray());

        return Array.AsReadOnly(AllWindowIds.ToArray());
    }

    private static string ComputeTargetSetHash(string entityId, IReadOnlyList<string> partIds)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new TargetSetIdentity(entityId, partIds), WireJsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool IsRevision(string value) =>
        value.Length is 40 or 64 && value.All(Uri.IsHexDigit);

    private static bool IsLowercaseSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidateActualString(string? value, string name, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || value.Any(char.IsControl)
            || normalized is not null && new[] { "unknown", "placeholder", "todo", "tbd", "null", "none", "n/a" }
                .Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{name} must be an actual non-placeholder value.", name);
        }
    }

    private static bool IsSafeScenePath(string? value) =>
        value is not null && value.StartsWith("res://", StringComparison.Ordinal)
        && IsSafeRelativePath(value["res://".Length..]);

    private static bool IsSafeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.Contains('\\')
            || value.Contains(':')
            || value.Any(char.IsControl))
        {
            return false;
        }

        var segments = value.Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not ("." or ".."));
    }

    private static void ValidateCamera(JsonElement camera)
    {
        if (camera.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Camera must be the captured Studio camera record.", "Camera");

        RequireExactProperties(camera, "projection", "position", "rotationDegrees", "fov", "size", "viewport");
        if (!camera.TryGetProperty("projection", out var projection)
            || projection.ValueKind != JsonValueKind.String
            || projection.GetString() is not ("perspective" or "orthogonal" or "frustum"))
            throw new ArgumentException("Camera projection is unsupported.", "Camera");

        ValidateVectorObject(camera.GetProperty("position"), "position");
        ValidateVectorObject(camera.GetProperty("rotationDegrees"), "rotationDegrees");
        var fov = ReadFiniteNumber(camera.GetProperty("fov"), "fov");
        var size = ReadFiniteNumber(camera.GetProperty("size"), "size");
        if (fov <= 0 || fov >= 180 || size <= 0)
            throw new ArgumentException("Camera fov and size must be positive finite values, with fov below 180 degrees.", "Camera");

        var viewport = camera.GetProperty("viewport");
        RequireExactProperties(viewport, "width", "height");
        var width = ReadFiniteNumber(viewport.GetProperty("width"), "viewport.width");
        var height = ReadFiniteNumber(viewport.GetProperty("height"), "viewport.height");
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Camera viewport dimensions must be positive finite values.", "Camera");
    }

    private static void ValidateVectorObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Camera {name} must be a finite xyz object.", "Camera");

        RequireExactProperties(value, "x", "y", "z");
        _ = ReadFiniteNumber(value.GetProperty("x"), $"{name}.x");
        _ = ReadFiniteNumber(value.GetProperty("y"), $"{name}.y");
        _ = ReadFiniteNumber(value.GetProperty("z"), $"{name}.z");
    }

    private static void RequireExactProperties(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Camera fields must be JSON objects.", "Camera");

        var names = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (names.Length != expected.Length || names.Distinct(StringComparer.Ordinal).Count() != expected.Length
            || expected.Any(name => !value.TryGetProperty(name, out _)))
            throw new ArgumentException("Camera has missing, duplicate, or unsupported fields.", "Camera");
    }

    private static double ReadFiniteNumber(JsonElement value, string name)
    {
        if (!IsFiniteNumber(value))
            throw new ArgumentException($"Camera {name} must be a finite number.", "Camera");

        return value.GetDouble();
    }

    private static bool IsFiniteNumber(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number);

    private static string[] ReadStringArray(JsonElement array) => array.EnumerateArray()
        .Select(value => value.GetString() ?? throw new InvalidOperationException("The v2 envelope contains a non-string resource path."))
        .ToArray();

    private static string DescribeSource(JsonElement source)
    {
        var path = source.GetProperty("path").GetString()!;
        var entityKey = source.GetProperty("entityKey").GetString();
        var baseHash = source.GetProperty("baseHash");
        var currentHash = source.GetProperty("currentHash").GetString();
        var semantic = string.Equals(entityKey, "recipe", StringComparison.Ordinal)
            ? "полная запись recipe"
            : "фрагмент выбранной сущности, не весь файл";
        var status = baseHash.ValueKind == JsonValueKind.Null
            ? "без базы при загрузке Studio"
            : string.Equals(baseHash.GetString(), currentHash, StringComparison.Ordinal)
                ? "байты совпадают с базой"
                : "байты отличаются от базы";
        return $"{path}: {semantic}; {status}";
    }

    private static string DescribeCamera(JsonElement camera)
    {
        var position = camera.GetProperty("position");
        var rotation = camera.GetProperty("rotationDegrees");
        var viewport = camera.GetProperty("viewport");
        return $"{camera.GetProperty("projection").GetString()}, позиция {FormatVector(position)}, поворот° {FormatVector(rotation)}, " +
               $"FOV {FormatNumber(camera.GetProperty("fov"))}, size {FormatNumber(camera.GetProperty("size"))}, " +
               $"viewport {FormatNumber(viewport.GetProperty("width"))}×{FormatNumber(viewport.GetProperty("height"))}";
    }

    private static string FormatVector(JsonElement vector) =>
        $"({FormatNumber(vector.GetProperty("x"))}, {FormatNumber(vector.GetProperty("y"))}, {FormatNumber(vector.GetProperty("z"))})";

    private static string FormatNumber(JsonElement value) => value.GetDouble().ToString("G", CultureInfo.InvariantCulture);

    private static void EnsureWithinLimit(byte[] bytes, string parameterName)
    {
        if (bytes.Length > HouseWindowProposalValidator.MaximumSerializedRequestBytes)
            throw new ArgumentException("The serialized v2 authoring request exceeds 256 KiB.", parameterName);
    }

    private sealed record RequestEnvelope(
        [property: JsonPropertyName("kind"), JsonPropertyOrder(0)] string Kind,
        [property: JsonPropertyName("schemaVersion"), JsonPropertyOrder(1)] int SchemaVersion,
        [property: JsonPropertyName("requestId"), JsonPropertyOrder(2)] string RequestId,
        [property: JsonPropertyName("instruction"), JsonPropertyOrder(3)] string Instruction,
        [property: JsonPropertyName("project"), JsonPropertyOrder(4)] ProjectDetails Project,
        [property: JsonPropertyName("selection"), JsonPropertyOrder(5)] SelectionDetails Selection,
        [property: JsonPropertyName("scope"), JsonPropertyOrder(6)] ScopeDetails Scope,
        [property: JsonPropertyName("sources"), JsonPropertyOrder(7)] IReadOnlyList<HouseWindowSource> Sources,
        [property: JsonPropertyName("dependencies"), JsonPropertyOrder(8)] IReadOnlyDictionary<string, string> Dependencies,
        [property: JsonPropertyName("resourceSets"), JsonPropertyOrder(9)] ResourceSets ResourceSets,
        [property: JsonPropertyName("capabilities"), JsonPropertyOrder(10)] IReadOnlyList<Capability> Capabilities,
        [property: JsonPropertyName("constraints"), JsonPropertyOrder(11)] Constraints Constraints,
        [property: JsonPropertyName("expectedResult"), JsonPropertyOrder(12)] string ExpectedResult,
        [property: JsonPropertyName("parameters"), JsonPropertyOrder(13)] Parameters Parameters);

    private sealed record ProjectDetails(
        [property: JsonPropertyName("id"), JsonPropertyOrder(0)] string Id,
        [property: JsonPropertyName("baseRevision"), JsonPropertyOrder(1)] string BaseRevision,
        [property: JsonPropertyName("campaignId"), JsonPropertyOrder(2)] string CampaignId,
        [property: JsonPropertyName("campaignVersion"), JsonPropertyOrder(3)] string CampaignVersion,
        [property: JsonPropertyName("worldId"), JsonPropertyOrder(4)] string WorldId,
        [property: JsonPropertyName("stateId"), JsonPropertyOrder(5)] string StateId,
        [property: JsonPropertyName("scenePath"), JsonPropertyOrder(6)] string ScenePath,
        [property: JsonPropertyName("zoneId"), JsonPropertyOrder(7)] string ZoneId,
        [property: JsonPropertyName("spawnPointId"), JsonPropertyOrder(8)] string SpawnPointId,
        [property: JsonPropertyName("snapshotHash"), JsonPropertyOrder(9)] string SnapshotHash);

    private sealed record SelectionDetails(
        [property: JsonPropertyName("kind"), JsonPropertyOrder(0)] string Kind,
        [property: JsonPropertyName("entityIds"), JsonPropertyOrder(1)] IReadOnlyList<string> EntityIds,
        [property: JsonPropertyName("partIds"), JsonPropertyOrder(2)] IReadOnlyList<string> PartIds,
        [property: JsonPropertyName("camera"), JsonPropertyOrder(3)] JsonElement Camera);

    private sealed record ScopeDetails(
        [property: JsonPropertyName("mode"), JsonPropertyOrder(0)] string Mode,
        [property: JsonPropertyName("resolvedIds"), JsonPropertyOrder(1)] IReadOnlyList<string> ResolvedIds,
        [property: JsonPropertyName("excludedIds"), JsonPropertyOrder(2)] IReadOnlyList<string> ExcludedIds,
        [property: JsonPropertyName("excludedPartIds"), JsonPropertyOrder(3)] IReadOnlyList<string> ExcludedPartIds,
        [property: JsonPropertyName("targetSetHash"), JsonPropertyOrder(4)] string TargetSetHash,
        [property: JsonPropertyName("sharedAssetPolicy"), JsonPropertyOrder(5)] string SharedAssetPolicy);

    private sealed record Capability(
        [property: JsonPropertyName("name"), JsonPropertyOrder(0)] string Name,
        [property: JsonPropertyName("version"), JsonPropertyOrder(1)] int Version);

    private sealed record Constraints(
        [property: JsonPropertyName("protected"), JsonPropertyOrder(0)] IReadOnlyList<string> Protected,
        [property: JsonPropertyName("rulesVersion"), JsonPropertyOrder(1)] int RulesVersion,
        [property: JsonPropertyName("keepHeight"), JsonPropertyOrder(2)] bool KeepHeight,
        [property: JsonPropertyName("mustPreserveOthers"), JsonPropertyOrder(3)] bool MustPreserveOthers,
        [property: JsonPropertyName("allowExecutableChanges"), JsonPropertyOrder(4)] bool AllowExecutableChanges);

    private sealed record ResourceSets(
        [property: JsonPropertyName("read"), JsonPropertyOrder(0)] IReadOnlyList<string> Read,
        [property: JsonPropertyName("write"), JsonPropertyOrder(1)] IReadOnlyList<string> Write);

    private sealed record Parameters(
        [property: JsonPropertyName("currentWidth"), JsonPropertyOrder(0)] decimal CurrentWidth,
        [property: JsonPropertyName("currentWidths"), JsonPropertyOrder(1)] IReadOnlyDictionary<string, decimal> CurrentWidths);

    private sealed record TargetSetIdentity(
        [property: JsonPropertyName("entityId"), JsonPropertyOrder(0)] string EntityId,
        [property: JsonPropertyName("partIds"), JsonPropertyOrder(1)] IReadOnlyList<string> PartIds);
}
