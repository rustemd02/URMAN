using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>
/// One Russian text edit of an existing localized entity (spec DLG04, unit
/// AI-20). The value is changed through <see cref="Localized.WithRussian"/>, so an
/// older default and every other translation survive; the caller writes
/// <see cref="Entity"/> through the shared EditSession, so Apply/Undo/Redo stay
/// the single path and the text id is never replaced by the text itself.
/// </summary>
public sealed record LocalizedTextChange(
    string TextId,
    string RelativePath,
    string Key,
    JsonObject Entity,
    JsonObject Value,
    IReadOnlyList<string> Warnings);

/// <summary>
/// The single operation for editing an authored text. Both the manual panel and
/// any AI change must go through it, so localisation cannot be lost by a
/// different code path: the older non-Russian default stays, the other
/// translations stay, and what is missing is reported instead of silently
/// replaced.
/// </summary>
public static class LocalizedTextEditor
{
    /// <summary>
    /// Applies a Russian edit to the text entity <paramref name="textId"/> and
    /// returns the entity to save plus the warnings an author has to see. A text
    /// that is not in the workspace is a clear error, never a new entity.
    /// </summary>
    public static LocalizedTextChange Apply(StudioWorkspace workspace, string textId, string russian)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(textId);
        ArgumentNullException.ThrowIfNull(russian);

        if (workspace.Locate(textId) is not { } address)
        {
            throw new InvalidOperationException(
                $"Текст «{textId}» не найден в рабочей области: его нельзя изменить, не создавая новую сущность.");
        }

        if (workspace.Get(textId)?.DeepClone() is not JsonObject entity)
        {
            throw new InvalidOperationException($"Текст «{textId}» не читается как объект.");
        }

        var before = entity["value"] as JsonObject;
        var warnings = new List<string>();
        if (before is null)
        {
            warnings.Add("У текста не было блока value: он создан заново.");
        }
        else
        {
            if (Localized.MissingRussian(before))
            {
                warnings.Add("Русского значения не было; оно создано.");
            }

            if (string.IsNullOrEmpty(Localized.Tatar(before)))
            {
                warnings.Add("Татарского перевода нет: он остаётся пустым и требует языковой приёмки.");
            }

            var oldDefault = (string?)(before["default"] as JsonValue);
            var oldRussian = (string?)(before["translations"]?["ru"] as JsonValue);
            if (oldDefault is not null && oldRussian is not null && !string.Equals(oldDefault, oldRussian, StringComparison.Ordinal))
            {
                warnings.Add($"Старый default «{oldDefault}» не совпадал с русским текстом и сохранён как есть.");
            }
        }

        var value = Localized.WithRussian(before, russian);
        entity["value"] = value;
        return new LocalizedTextChange(textId, address.RelativePath, address.Key, entity, value, warnings);
    }

    /// <summary>
    /// The same edit applied to an entity that is not in the workspace yet, for
    /// callers that build content themselves (a new dialogue node). Locale rules
    /// are identical, so both paths cannot drift apart.
    /// </summary>
    public static JsonObject ApplyToValue(JsonNode? currentValue, string russian, out IReadOnlyList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(russian);
        var before = currentValue as JsonObject;
        var reported = new List<string>();
        if (before is null)
        {
            reported.Add("У текста не было блока value: он создан заново.");
        }
        else if (string.IsNullOrEmpty(Localized.Tatar(before)))
        {
            reported.Add("Татарского перевода нет: он остаётся пустым и требует языковой приёмки.");
        }

        warnings = reported;
        return Localized.WithRussian(before, russian);
    }
}
