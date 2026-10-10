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

    public static void Reset() => _byAnchor = null;

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

        ApplyAuthored(placement, logicalAnchor);
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

    private static void ApplyAuthored(Node3D placement, string logicalAnchor)
    {
        if (!ByAnchor().TryGetValue(logicalAnchor, out var entity))
        {
            return; // not taken over yet: the generator still owns this placement
        }

        var parameters = entity.GetProperty("params");
        if (entity.GetProperty("id").GetString() == HeroHouseRecipe.EntityId
            && HeroHouseRecipe.Load().ModelPath is { } modelPath)
        {
            var document = new GltfDocument();
            var state = new GltfState();
            if (document.AppendFromFile(ProjectSettings.GlobalizePath(modelPath), state) != Error.Ok)
                throw new InvalidOperationException($"Не импортирован вариант дома: {modelPath}");
            var root = document.GenerateScene(state) as Node3D
                ?? throw new InvalidDataException("Вариант дома не имеет 3D-корня.");
            var component = root.Name == StyleBenchmarkInteriorFactory.ExteriorComponent ? root
                : root.FindChildren(StyleBenchmarkInteriorFactory.ExteriorComponent, nameof(Node3D), true, false).OfType<Node3D>().Single();
            if (component != root) { component.GetParent().RemoveChild(component); root.Free(); }
            foreach (var old in placement.GetChildren()) { placement.RemoveChild(old); old.Free(); }
            component.Owner = null;
            component.Position = Vector3.Zero;
            placement.AddChild(component);
            placement.SetMeta("assetSource", modelPath);
        }
        var position = AuthoredWorldPlot.ToVector3(parameters.GetProperty("position"));
        var height = placement.GlobalPosition.Y;
        if (parameters.TryGetProperty("groundingReferenceXZ", out var reference))
        {
            var point = reference.EnumerateArray().Select(value => value.GetSingle()).ToArray();
            height += (float)(Urman.Experiments.AgentBAct1.AgentBAct1HeightField.Ground(position.X, position.Z)
                - Urman.Experiments.AgentBAct1.AgentBAct1HeightField.Ground(point[0], point[1]));
        }
        placement.GlobalPosition = new Vector3(position.X, height, position.Z);
        placement.RotationDegrees = new Vector3(0f, parameters.GetProperty("yawDegrees").GetSingle(), 0f);
        placement.Scale = AuthoredWorldPlot.ToVector3(parameters.GetProperty("scale"));
        if (parameters.TryGetProperty("hidden", out var hidden) && hidden.GetBoolean())
        {
            placement.Visible = false;
        }

        // Relayout v3: a household that became a generic parcel elsewhere leaves its old
        // kit house, shed and fence behind. Retired pieces neither draw nor collide; the
        // node stays so builders that look it up by name keep working.
        if (parameters.TryGetProperty("retired", out var retired) && retired.GetBoolean())
        {
            Retire(placement);
        }

        placement.SetMeta(AuthoredWorldPlot.AuthoredIdMeta, entity.GetProperty("id").GetString()!);
        placement.SetMeta("placementOwner", "authored plot " + (PlotOverrideForTest ?? PlotPath));
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
