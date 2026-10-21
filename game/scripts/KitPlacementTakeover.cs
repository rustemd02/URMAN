using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Makes the generated village authored (spec WORLD15): every kit component
/// the Act I builder places has a stable logical anchor; once that anchor is
/// listed in the authored plot <see cref="PlotPath"/>, the plot owns the
/// placement's world position, yaw, scale and visibility, and the generator's
/// own values are only its proposal. A taken-over anchor missing from the plot
/// is an error, never a silent fallback to the code values.
/// With <c>URMAN_KIT_HARVEST=&lt;file&gt;</c> the builder instead records its
/// proposal for every placement — the input for taking over and for the
/// explicit "re-apply generation" comparison.
/// </summary>
public static class KitPlacementTakeover
{
    public const string PlotPath = "res://content/world/act1_village_kit.world.v1.json";
    private static Dictionary<string, JsonElement>? _byAnchor;
    private static readonly List<JsonObject> Harvest = [];
    private static readonly HashSet<string> Seen = new(StringComparer.Ordinal);

    /// <summary>Test-only override of the plot location (Studio smoke runs on a disposable workspace).</summary>
    internal static string? PlotOverrideForTest { get; set; }

    private static Dictionary<string, JsonElement> ByAnchor()
    {
        if (_byAnchor is not null) return _byAnchor;
        _byAnchor = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var path = PlotOverrideForTest ?? PlotPath;
        if (!global::Godot.FileAccess.FileExists(path)) return _byAnchor;
        using var document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(path));
        foreach (var entity in document.RootElement.GetProperty("entities").EnumerateArray())
        {
            var parameters = entity.GetProperty("params");
            _byAnchor[parameters.GetProperty("logicalAnchor").GetString()!] = entity.Clone();
        }

        return _byAnchor;
    }

    public static void Reset()
    {
        _byAnchor = null;
        Seen.Clear();
    }

    /// <summary>Apply the authored placement (if taken over) to a freshly placed component, then record the proposal when harvesting.</summary>
    public static void Apply(Node3D placement, string componentName, string assetSource, string logicalAnchor)
    {
        Seen.Add(logicalAnchor);
        var harvestPath = System.Environment.GetEnvironmentVariable("URMAN_KIT_HARVEST");
        if (harvestPath is { Length: > 0 })
        {
            Record(placement, componentName, assetSource, logicalAnchor, harvestPath);
            return;
        }

        ApplyAuthored(placement, componentName, logicalAnchor);
        if (System.Environment.GetEnvironmentVariable("URMAN_KIT_RESULT") is { Length: > 0 } resultPath)
        {
            Record(placement, componentName, assetSource, logicalAnchor, resultPath);
        }
    }

    private static void Record(Node3D placement, string componentName, string assetSource, string logicalAnchor, string path)
    {
            var at = placement.GlobalPosition;
            Harvest.Add(new JsonObject
            {
                ["logicalAnchor"] = logicalAnchor,
                ["component"] = componentName,
                ["placement"] = placement.Name.ToString(),
                ["assetSource"] = assetSource,
                ["position"] = new JsonArray(Math.Round(at.X, 4), Math.Round(placement.Position.Y, 4), Math.Round(at.Z, 4)),
                ["yawDegrees"] = Math.Round(placement.RotationDegrees.Y, 3),
                ["scale"] = new JsonArray(Math.Round(placement.Scale.X, 4), Math.Round(placement.Scale.Y, 4), Math.Round(placement.Scale.Z, 4)),
                ["parentYaw"] = Math.Round(((Node3D)placement.GetParent()).GlobalRotationDegrees.Y, 3)
            });
            System.IO.File.WriteAllText(path, new JsonArray(Harvest.Select(item => (JsonNode?)item.DeepClone()).ToArray()).ToJsonString());
    }

    private static void ApplyAuthored(Node3D placement, string componentName, string logicalAnchor)
    {
        if (!ByAnchor().TryGetValue(logicalAnchor, out var entity))
        {
            return; // not taken over yet: the generator still owns this placement
        }

        var parameters = entity.GetProperty("params");
        var position = AuthoredWorldPlot.ToVector3(parameters.GetProperty("position"));
        var height = placement.GlobalPosition.Y;
        if (parameters.TryGetProperty("groundingReferenceXZ", out var reference))
        {
            var point = reference.EnumerateArray().Select(value => value.GetSingle()).ToArray();
            height += (float)(Urman.Experiments.AgentBAct1.AgentBAct1HeightField.Ground(position.X, position.Z)
                - Urman.Experiments.AgentBAct1.AgentBAct1HeightField.Ground(point[0], point[1]));
        }
        var yawDegrees = parameters.GetProperty("yawDegrees").GetSingle();
        var scale = AuthoredWorldPlot.ToVector3(parameters.GetProperty("scale"));
        var hidden = parameters.TryGetProperty("hidden", out var hiddenValue) && hiddenValue.GetBoolean();
        var retired = parameters.TryGetProperty("retired", out var retiredValue) && retiredValue.GetBoolean();

        if (entity.GetProperty("id").GetString() == HeroHouseRecipe.EntityId
            && HeroHouseRecipe.Load().ModelPath is { } modelPath)
        {
            var root = LoadAuthoredGltfScene(modelPath);
            var component = root.Name == StyleBenchmarkInteriorFactory.ExteriorComponent ? root
                : root.FindChildren(StyleBenchmarkInteriorFactory.ExteriorComponent, nameof(Node3D), true, false).OfType<Node3D>().Single();
            Act1ConnectedWorld.ClearExtractedSceneOwnership(component);
            if (component != root) { component.GetParent().RemoveChild(component); root.Free(); }
            foreach (var old in placement.GetChildren()) { placement.RemoveChild(old); old.Free(); }
            component.Position = Vector3.Zero;
            placement.AddChild(component);
            placement.SetMeta("assetSource", modelPath);
        }
        else if (parameters.TryGetProperty("modelPath", out _))
        {
            ApplyNonHeroDwellingVariant(placement, componentName, logicalAnchor, entity, parameters);
        }

        placement.GlobalPosition = new Vector3(position.X, height, position.Z);
        placement.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
        placement.Scale = scale;
        if (hidden)
        {
            placement.Visible = false;
        }

        // Relayout v3: a household that became a generic parcel elsewhere leaves its old
        // kit house, shed and fence behind. Retired pieces neither draw nor collide; the
        // node stays so builders that look it up by name keep working.
        if (retired)
        {
            Retire(placement);
        }

        placement.SetMeta(AuthoredWorldPlot.AuthoredIdMeta, entity.GetProperty("id").GetString()!);
        placement.SetMeta("placementOwner", "authored plot " + (PlotOverrideForTest ?? PlotPath));
    }

    private static Node3D LoadAuthoredGltfScene(string modelPath)
    {
        if (ResourceLoader.Exists(modelPath))
        {
            // Exported GLBs are remapped to imported PackedScenes inside the PCK;
            // the raw source file is not necessarily present beside the executable.
            var packed = ResourceLoader.Load<PackedScene>(modelPath)
                ?? throw new InvalidOperationException($"Не импортирован вариант дома: {modelPath}");
            var instance = packed.Instantiate();
            if (instance is not Node3D root)
            {
                instance?.Free();
                throw new InvalidDataException("Вариант дома не имеет 3D-корня.");
            }

            return root;
        }

        // Studio may have just staged an immutable model path before Godot's
        // importer has created its PackedScene; retain the source-tree hotload.
        using var document = new GltfDocument();
        using var state = new GltfState();
        if (document.AppendFromFile(ProjectSettings.GlobalizePath(modelPath), state) != Error.Ok)
            throw new InvalidOperationException($"Не импортирован вариант дома: {modelPath}");
        var generated = document.GenerateScene(state);
        if (generated is not Node3D generatedRoot)
        {
            generated?.Free();
            throw new InvalidDataException("Вариант дома не имеет 3D-корня.");
        }

        return generatedRoot;
    }

    private static void ApplyNonHeroDwellingVariant(
        Node3D placement, string componentName, string logicalAnchor, JsonElement entity, JsonElement parameters)
    {
        var entityId = entity.GetProperty("id").GetString()!;
        if (entity.GetProperty("kind").GetString() != "kit-placement"
            || !entityId.StartsWith("urman.world:act1/kit/", StringComparison.Ordinal)
            || !parameters.TryGetProperty("logicalAnchor", out var anchor)
            || anchor.ValueKind != JsonValueKind.String || anchor.GetString() != logicalAnchor)
            throw new InvalidDataException($"House-variant override is not attached to a known Act I kit placement: {entityId}.");

        if (!parameters.TryGetProperty("component", out var target)
            || target.ValueKind != JsonValueKind.String || target.GetString() != componentName
            || componentName is not ("VillageParcel_VariantA_TimberGable_Dwelling"
                or "VillageParcel_VariantB_PlasterAnnex_Dwelling"
                or "VillageParcel_VariantC_BanyaYard_Dwelling"))
            throw new InvalidDataException($"House-variant override component does not match placement {entityId}.");

        if (!AuthoredWorldDirector.TryReadHouseVariantModelPath(parameters, entityId, out var modelPath)
            || modelPath is null)
            throw new InvalidDataException($"House-variant model path is invalid for {entityId}.");

        Node3D? importedRoot = null;
        Node3D? replacement = null;
        try
        {
            var root = LoadAuthoredGltfScene(modelPath);
            importedRoot = root;
            var matches = root.Name.ToString() == componentName
                ? new[] { root }
                : root.FindChildren(componentName, nameof(Node3D), true, false).OfType<Node3D>().ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException($"В модели найдено {matches.Length} компонентов {componentName}; ожидался ровно один.");

            replacement = matches[0];
            var parcelName = componentName[..^"_Dwelling".Length];
            if (replacement != root && replacement.GetParent()?.Name.ToString() != parcelName)
                throw new InvalidDataException($"Компонент {componentName} расположен не в ожидаемом семействе {parcelName}.");

            var basis = Act1ConnectedWorld.ComposeImportedAncestorBasis(replacement);
            Act1ConnectedWorld.ClearExtractedSceneOwnership(replacement);
            replacement.Transform = new Transform3D(basis, Vector3.Zero);
            if (replacement != root)
            {
                replacement.GetParent()!.RemoveChild(replacement);
                root.Free();
                importedRoot = null;
            }

            var previous = placement.GetChildren().ToArray();
            foreach (var child in previous) placement.RemoveChild(child);
            try
            {
                placement.AddChild(replacement);
                placement.SetMeta("assetSource", modelPath);
            }
            catch
            {
                if (replacement.GetParent() == placement) placement.RemoveChild(replacement);
                foreach (var child in previous) placement.AddChild(child);
                throw;
            }

            foreach (var child in previous) child.Free();
            replacement = null;
            importedRoot = null;
        }
        catch
        {
            if (replacement is not null && GodotObject.IsInstanceValid(replacement))
            {
                if (replacement.GetParent() == placement) placement.RemoveChild(replacement);
                if (replacement.GetParent() is null) replacement.Free();
            }
            if (importedRoot is not null && GodotObject.IsInstanceValid(importedRoot)) importedRoot.Free();
            throw;
        }
    }

    public const string RetiredMeta = "retiredByPlot";

    internal static void Retire(Node3D placement)
    {
        placement.Visible = false;
        placement.SetMeta(RetiredMeta, true);
        foreach (var shape in placement.FindChildren("*", nameof(CollisionShape3D), true, false).OfType<CollisionShape3D>())
            shape.Disabled = true;
        foreach (var body in placement.FindChildren("*", "", true, false).OfType<CollisionObject3D>().Prepend(placement as CollisionObject3D))
        {
            if (body is null) continue;
            body.CollisionLayer = 0;
            body.CollisionMask = 0;
        }
    }

    /// <summary>Anchors listed in the plot that the builder never placed: the generator changed and the author must decide.</summary>
    public static IReadOnlyList<string> Unmatched() =>
        ByAnchor().Keys.Where(anchor => !Seen.Contains(anchor)).Order(StringComparer.Ordinal).ToArray();
}
