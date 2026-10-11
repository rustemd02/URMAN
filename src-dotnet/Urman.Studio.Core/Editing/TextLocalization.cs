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
            if (text is null || AuthoredJson.Text(text, "default") is not { Length: > 0 } fallback)
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
    /// <summary>
    /// The texts the content ACTUALLY reads, and which of them have no Tatar translation. The
    /// inventory above counts every declared text; this counts the ones a player will see, which is
    /// the number that matters for the localisation criterion: a used text without a tt entry shows
    /// the Russian fallback, and the campaign carries a language layer precisely so that this does
    /// not happen silently.
    ///
    /// Referenced ids are collected from the authored files through the workspace, by the field
    /// names the runtime reads for text, so an id this scan misses is one the runtime would not
    /// read either.
    /// </summary>
    public static (IReadOnlyList<string> UsedWithoutTatar, int Used, int Translated) UsedCoverage(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var texts = Inspect(workspace).ToDictionary(text => text.TextId, StringComparer.Ordinal);
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not { } entity)
            {
                continue;
            }

            var pending = new Stack<JsonNode?>();
            pending.Push(entity);
            while (pending.Count > 0)
            {
                switch (pending.Pop())
                {
                    case JsonObject obj:
                        foreach (var pair in obj)
                        {
                            if (TextFields.Contains(pair.Key)
                                && pair.Value is JsonValue value
                                && value.TryGetValue<string>(out var reference)
                                && reference is { Length: > 0 }
                                && texts.ContainsKey(reference))
                            {
                                used.Add(reference);
                            }

                            pending.Push(pair.Value);
                        }

                        break;
                    case JsonArray array:
                        foreach (var item in array)
                        {
                            pending.Push(item);
                        }

                        break;
                }
            }
        }

        var missing = used.Where(id => string.IsNullOrEmpty(texts[id].Tatar)).ToArray();
        return (missing, used.Count, used.Count - missing.Length);
    }

    /// <summary>The field names the runtime reads a text id from.</summary>
    public static IReadOnlySet<string> TextFields { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "textId",
        "lineId",
        "promptTextId",
        "titleTextId",
        "bodyTextId",
        "answerTextId"
    };

    /// <summary>What the used-text coverage means for the language criterion.</summary>
    public static IReadOnlyList<string> UsedNotes(StudioWorkspace workspace)
    {
        var (missing, used, translated) = UsedCoverage(workspace);
        return
        [
            $"Текстов, которые контент действительно читает: {used}; с татарским переводом: {translated}; без него: {missing.Count}.",
            missing.Count == 0
                ? "Каждый читаемый текст имеет татарский перевод."
                : $"Без татарского перевода: {string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? " …" : "")}. Игрок увидит русский запасной вариант — это факт о контенте, а решение о переводе принимает автор и носитель языка.",
            "Перевод, его точность и культурная уместность — внешняя приёмка: проекция считает наличие, а не качество."
        ];
    }

}
