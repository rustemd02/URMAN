using System.Text.Json.Nodes;

namespace Urman.Content.Resolvers;

internal static class ContentResolverSupport
{
    public static JsonArray RequiredArray(JsonObject owner, string key, ResolverOrigin origin) =>
        owner[key] as JsonArray
        ?? throw ResolverErrors.Create(
            ResolverErrorCode.InvalidPack,
            $"{origin.Id}.{key} must be an array.",
            ResolverErrors.Reference(origin, $"/{key}"));

    public static ResolvedAccessibility AccessibilityFor(JsonObject asset, ResolverOrigin origin)
    {
        var accessibility = asset["accessibility"] as JsonObject
            ?? throw ResolverErrors.Create(
                ResolverErrorCode.InvalidPack,
                $"{origin.Id}.accessibility must be an object.",
                ResolverErrors.Reference(origin, "/accessibility"));
        if (accessibility["decorative"] is not JsonValue decorativeValue
            || !decorativeValue.TryGetValue<bool>(out var decorative))
        {
            throw ResolverErrors.Create(
                ResolverErrorCode.InvalidPack,
                $"{origin.Id}.accessibility.decorative must be a boolean.",
                ResolverErrors.Reference(origin, "/accessibility/decorative"));
        }

        return new ResolvedAccessibility(
            decorative,
            OptionalString(accessibility, "altTextId", origin),
            OptionalString(accessibility, "captionTextId", origin),
            OptionalString(accessibility, "audioDescriptionTextId", origin));
    }

    public static void RequireAccessibilityField(string? value, JsonObject asset, string key, ResolverOrigin origin)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw ResolverErrors.Create(
                ResolverErrorCode.InvalidAccessibility,
                $"{asset["id"]} needs accessibility.{key}.",
                ResolverErrors.Reference(origin, $"/accessibility/{key}"));
        }
    }

    public static (string Text, string ResolvedLocale) LocalizedValue(
        JsonNode? node,
        string? locale,
        ResolverOrigin origin)
    {
        var value = node as JsonObject
            ?? throw ResolverErrors.Create(ResolverErrorCode.InvalidPack, $"{origin.Id} has an invalid localized value.", origin);
        var defaultText = value["default"]?.GetValue<string>();
        var translations = value["translations"] as JsonObject;
        if (string.IsNullOrEmpty(defaultText) || translations is null)
        {
            throw ResolverErrors.Create(ResolverErrorCode.InvalidPack, $"{origin.Id} has an invalid localized value.", origin);
        }

        if (locale is null)
        {
            return (defaultText, "default");
        }

        if (locale.Length == 0)
        {
            throw ResolverErrors.Create(
                ResolverErrorCode.InvalidLocale,
                "Locale must be a non-empty string.",
                origin,
                new Dictionary<string, object?> { ["requestedLocale"] = locale });
        }

        for (var candidate = locale; candidate.Length > 0;)
        {
            if (translations[candidate] is JsonValue translatedValue
                && translatedValue.TryGetValue<string>(out var translated)
                && !string.IsNullOrEmpty(translated))
            {
                return (translated, candidate);
            }

            var separator = candidate.LastIndexOf('-');
            candidate = separator > 0 ? candidate[..separator] : string.Empty;
        }

        return (defaultText, "default");
    }

    public static uint Fnv1A(string text)
    {
        var hash = 0x811c9dc5u;
        foreach (var character in text)
        {
            hash ^= character;
            hash = unchecked(hash * 0x01000193u);
        }

        return hash;
    }

    private static string? OptionalString(JsonObject owner, string key, ResolverOrigin origin)
    {
        if (owner[key] is null)
        {
            return null;
        }

        if (owner[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text))
        {
            return text;
        }

        throw ResolverErrors.Create(
            ResolverErrorCode.InvalidPack,
            $"{origin.Id}.accessibility.{key} must be a non-empty string.",
            ResolverErrors.Reference(origin, $"/accessibility/{key}"));
    }
}
