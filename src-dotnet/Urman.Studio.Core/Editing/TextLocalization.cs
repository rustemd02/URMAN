using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored text record and the languages it really carries.</summary>
public sealed record TextLocalizationFacts(
    string TextId,
    string Default,
    string Russian,
    string? Tatar,
    bool DefaultIsRussian);

/// <summary>
/// The localization half of the spec AI-20 slice. The runtime resolves a localized value by
/// trying the requested locale, then its language part, and finally falling back to
/// <c>default</c> (ContentResolverSupport), so what a player really reads depends on which
/// languages a record carries.
///
/// Two numbers are easy to misread and are therefore separated here:
///
///   * a Russian translation that EQUALS the default is normal, not a hole: this project
///     authors in Russian and both fields change together (spec DLG04), so an identical pair
///     is the expected shape for content written in Russian;
///   * a translation that is simply ABSENT is a real gap, and filling it means writing
///     Tatar, which this projection must not invent. It reports coverage and leaves the
///     language and culture acceptance to the people who own it.
/// </summary>
public static class TextLocalization
{
    public const string Russian = "ru";
    public const string Tatar = "tt";

    public static IReadOnlyList<TextLocalizationFacts> Inspect(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var texts = new List<TextLocalizationFacts>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity)
            {
                continue;
            }

            var text = TextNode(entity);
            if (text is null || (string?)text["default"] is not { Length: > 0 } fallback)
            {
                continue;
            }

            var russian = Localized.Russian(text);
            texts.Add(new TextLocalizationFacts(
                id,
                fallback,
                russian,
                Localized.Tatar(text),
                fallback == russian));
        }

        return texts.OrderBy(text => text.TextId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// The localized value of an entity, whether it is the entity itself or one of its fields.
    /// A text record is stored either as the whole entity or as a field of a larger record, so
    /// both shapes are read instead of assuming one.
    /// </summary>
    private static JsonObject? TextNode(JsonObject entity)
    {
        if (entity["default"] is JsonValue && entity["translations"] is JsonObject)
        {
            return entity;
        }

        foreach (var pair in entity)
        {
            if (pair.Value is JsonObject nested && nested["default"] is JsonValue && nested["translations"] is JsonObject)
            {
                return nested;
            }
        }

        return null;
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace)
    {
        var texts = Inspect(workspace);
        var withTatar = texts.Count(text => text.Tatar is { Length: > 0 });
        var russianEqualsDefault = texts.Count(text => text.DefaultIsRussian);
        var russianDiffers = texts.Count - russianEqualsDefault;
        return
        [
            $"Записей с локализованным текстом: {texts.Count}; русский есть у {texts.Count(text => text.Russian.Length > 0)}; татарский — у {withTatar}.",
            $"Русский совпадает с default у {russianEqualsDefault} записей и отличается у {russianDiffers}. Совпадение — это НЕ пропуск перевода: проект пишет по-русски, и default с translations.ru меняются вместе (DLG04).",
            "Рантайм ищет локаль, затем её языковую часть, и только потом падает в default (ContentResolverSupport), поэтому отсутствующий татарский показывает игроку текст default, а не пустую строку.",
            $"Татарский заполнен у {withTatar} записей. Этот пробел — языковой и культурный гейт: придумывать перевод нельзя, его закрывают люди, владеющие языком, а не эта проекция."
        ];
    }
}
