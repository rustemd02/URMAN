using System.Text.Json;

namespace Urman.Godot;

public readonly record struct HeroHouseRecipe(float WindowClearWidth, float WindowClearHeight, string? ModelPath,
    IReadOnlyDictionary<string, float>? WindowWidths = null)
{
    public const string Path = "res://content/studio/hero_house.recipe.json";
    public const string EntityId = "urman.world:act1/kit/house-old-pc-babai-approach";
    public const string Component = "HeroHouse_TimberPlaster";
    public const float MinWindowClearWidth = .65f;
    public const float MaxWindowClearWidth = 1.35f;
    public const float FixedWindowClearHeight = 1.40f;
    private const string ModelPathPrefix = "res://assets/models/studio/hero-house-";
    public static IReadOnlyList<string> WindowIds { get; } = Array.AsReadOnly(new[]
        { "Front0", "Front1", "Front2", "Rear0", "Rear1", "Left0", "Right0", "SeniOuter0" });

    public float WidthFor(string windowId)
    {
        if (!WindowIds.Contains(windowId, StringComparer.Ordinal))
            throw new InvalidDataException("Unknown hero-house window ID.");
        return WindowWidths is not null && WindowWidths.TryGetValue(windowId, out var width) ? width : WindowClearWidth;
    }

    public static HeroHouseRecipe Load(string path = Path) => Parse(global::Godot.FileAccess.GetFileAsString(path));

    public static HeroHouseRecipe Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        RejectDuplicateProperties(root);
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

        var overrides = new Dictionary<string, float>(StringComparer.Ordinal);
        if (root.TryGetProperty("windowWidths", out var windowWidths))
        {
            if (windowWidths.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("windowWidths must be an object keyed by hero-house window ID.");
            foreach (var property in windowWidths.EnumerateObject())
            {
                if (!WindowIds.Contains(property.Name, StringComparer.Ordinal))
                    throw new InvalidDataException("windowWidths contains an unknown hero-house window ID.");
                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out var exactWidth)
                    || !double.IsFinite(exactWidth) || exactWidth < .65d || exactWidth > 1.35d)
                    throw new InvalidDataException("windowWidths values must be finite numbers within 0.65..1.35 metres.");
                overrides.Add(property.Name, Number(property.Value, "windowWidths value"));
            }
        }
        return new HeroHouseRecipe(width, height, modelPath,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, float>(overrides));
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Hero-house recipe contains duplicate JSON properties.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var value in element.EnumerateArray()) RejectDuplicateProperties(value);
    }

    private static float Number(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetSingle(out var value) || !float.IsFinite(value))
            throw new InvalidDataException($"{name} must be a finite number.");
        return value;
    }
}
