using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>
/// Localized text as the content stores it: <c>{ "default": …, "translations": { "ru": …, "tt": … } }</c>
/// (spec DLG04). The author edits Russian; when the default is the Russian
/// text too (new content) both change together, while an older English default
/// stays as it is. Other translations are never dropped.
/// </summary>
public static class Localized
{
    public static string Russian(JsonNode? text) =>
        (string?)(text?["translations"]?["ru"] as JsonValue) ?? (string?)(text?["default"] as JsonValue) ?? "";

    public static string? Tatar(JsonNode? text) => (string?)(text?["translations"]?["tt"] as JsonValue);

    public static bool MissingRussian(JsonNode? text) => text?["translations"]?["ru"] is null;

    public static JsonObject WithRussian(JsonNode? text, string russian)
    {
        var result = text?.DeepClone() as JsonObject ?? new JsonObject();
        var oldRussian = (string?)(result["translations"]?["ru"] as JsonValue);
        var oldDefault = (string?)(result["default"] as JsonValue);
        if (oldDefault is null || oldRussian is null || oldDefault == oldRussian)
        {
            result["default"] = russian;
        }

        if (result["translations"] is not JsonObject translations)
        {
            result["translations"] = translations = new JsonObject();
        }

        translations["ru"] = russian;
        return result;
    }

    public static JsonObject WithTatar(JsonNode? text, string tatar)
    {
        var result = text?.DeepClone() as JsonObject ?? new JsonObject { ["default"] = "" };
        if (result["translations"] is not JsonObject translations)
        {
            result["translations"] = translations = new JsonObject();
        }

        if (tatar.Length == 0) translations.Remove("tt"); else translations["tt"] = tatar;
        return result;
    }
}
