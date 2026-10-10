using System.Text.Json;

namespace Urman.Godot;

public readonly record struct HeroHouseRecipe(float WindowClearWidth, float WindowClearHeight, string? ModelPath)
{
    public const string Path = "res://content/studio/hero_house.recipe.json";
    public const string EntityId = "urman.world:act1/kit/house-old-pc-babai-approach";
    public const string Component = "HeroHouse_TimberPlaster";
    public const float MinWindowClearWidth = .65f;
    public const float MaxWindowClearWidth = 1.35f;
    public const float FixedWindowClearHeight = 1.40f;
    private const string ModelPathPrefix = "res://assets/models/studio/hero-house-";

    public static HeroHouseRecipe Load(string path = Path)
    {
        using var document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(path));
        var root = document.RootElement;
        if (root.GetProperty("schema").GetString() != "house-recipe/v1"
            || root.GetProperty("entityId").GetString() != EntityId
            || root.GetProperty("component").GetString() != Component)
            throw new InvalidDataException("Hero-house recipe identity or schema is invalid.");

        var width = Number(root.GetProperty("windowClearWidth"), "windowClearWidth");
        var height = Number(root.GetProperty("windowClearHeight"), "windowClearHeight");
        var limits = root.GetProperty("limits").GetProperty("windowClearWidth");
        var min = Number(limits.GetProperty("min"), "limits.windowClearWidth.min");
        var max = Number(limits.GetProperty("max"), "limits.windowClearWidth.max");
        if (min != MinWindowClearWidth || max != MaxWindowClearWidth
            || width < min || width > max)
            throw new InvalidDataException($"windowClearWidth must be within {MinWindowClearWidth}..{MaxWindowClearWidth} metres.");
        if (MathF.Abs(height - FixedWindowClearHeight) > .0001f)
            throw new InvalidDataException("windowClearHeight is fixed at 1.40 metres by the sill/head contract.");

        var modelElement = root.GetProperty("modelPath");
        var modelPath = modelElement.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => modelElement.GetString(),
            _ => throw new InvalidDataException("modelPath must be null or a staged hero-house GLB path.")
        };
        if (modelPath is not null && (!modelPath.StartsWith(ModelPathPrefix, StringComparison.Ordinal)
            || !modelPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            || modelPath.AsSpan(ModelPathPrefix.Length).Contains('/')
            || modelPath.Contains("..", StringComparison.Ordinal)
            || modelPath.Contains('\\')))
            throw new InvalidDataException("modelPath must point to a hero-house GLB under res://assets/models/studio.");

        return new HeroHouseRecipe(width, height, modelPath);
    }

    private static float Number(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetSingle(out var value) || !float.IsFinite(value))
            throw new InvalidDataException($"{name} must be a finite number.");
        return value;
    }
}
