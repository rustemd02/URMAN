using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>How Studio presents one entity: a human name first, the type in words, the ID as a secondary detail.</summary>
public sealed record EntitySummary(string Id, string Name, string Kind, string KindLabel, string RelativePath, string Text);

/// <summary>
/// Human names, Russian type labels and the global search (spec UX07, UX08).
/// A search finds by name, ID, type or spoken text, and a pasted ID or
/// "id: …" line from a developer message resolves to the entity.
/// </summary>
public sealed class EntityCatalog(StudioWorkspace workspace)
{
    private static readonly Dictionary<string, string> KindLabels = new(StringComparer.Ordinal)
    {
        ["quest"] = "Квест", ["character"] = "Персонаж", ["dialogue"] = "Диалог", ["text"] = "Текст",
        ["knowledge"] = "Сведение / улика", ["scene"] = "Место", ["interaction"] = "Взаимодействие",
        ["document"] = "Документ", ["asset"] = "Ресурс", ["vocabulary"] = "Слово", ["hint"] = "Подсказка",
        ["beat"] = "Сюжетный момент", ["chat"] = "Переписка", ["capability"] = "Механика", ["fixture"] = "Проверочный набор",
        ["fence-run"] = "Забор", ["item-spawn"] = "Предмет в мире", ["marker"] = "Точка на карте", ["road-path"] = "Дорога / съезд"
    };

    public StudioWorkspace Workspace { get; } = workspace;

    public EntitySummary Describe(string id)
    {
        var entity = Workspace.Get(id) as JsonObject;
        var address = Workspace.Locate(id);
        var kind = KindOf(id, entity);
        return new(id, NameOf(id, entity), kind, KindLabels.GetValueOrDefault(kind, "Объект"), address?.RelativePath ?? "", TextOf(entity));
    }

    public string NameOf(string id) => NameOf(id, Workspace.Get(id) as JsonObject);

    /// <summary>The author's name for a quest outcome (stored with the fact names), or its ID.</summary>
    public string OutcomeName(string questId, string outcomeId) =>
        Workspace.Get($"{questId}#outcome:{outcomeId}") is JsonObject named && named["label"] is JsonValue label ? (string)label! : outcomeId;

    public string ResolveText(string? textId)
    {
        if (textId is null)
        {
            return "";
        }

        return Workspace.Get(textId) is JsonObject text && text["value"]?["default"] is JsonValue value ? (string)value! : textId;
    }

    public IReadOnlyList<EntitySummary> Search(string query, int limit = 30)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return [];
        }

        // A pasted developer line ("… ID urman.chapter1:quest/x, …") resolves directly.
        var token = query.Split([' ', ',', ';', '«', '»', '"'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.Contains(':') && Workspace.Locate(part.TrimEnd('.')) is not null)?.TrimEnd('.');
        if (token is not null)
        {
            return [Describe(token)];
        }

        var needle = query.ToLowerInvariant();
        return Workspace.EntityIds
            .Select(Describe)
            .Where(summary => $"{summary.Name}\n{summary.Id}\n{summary.KindLabel}\n{summary.Text}".ToLowerInvariant().Contains(needle, StringComparison.Ordinal))
            .OrderBy(summary => summary.Name.ToLowerInvariant().StartsWith(needle, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(summary => summary.Kind == "text" ? 1 : 0)
            .ThenBy(summary => summary.Name, StringComparer.CurrentCulture)
            .Take(limit)
            .ToArray();
    }

    public static string KindOf(string id, JsonObject? entity)
    {
        if (entity?["kind"] is JsonValue kind && kind.TryGetValue<string>(out var text) && KindLabels.ContainsKey(text))
        {
            return text;
        }

        var colon = id.IndexOf(':');
        var slash = id.IndexOf('/', colon + 1);
        return colon >= 0 && slash > colon ? id[(colon + 1)..slash] : "";
    }

    private string NameOf(string id, JsonObject? entity)
    {
        if (entity is null)
        {
            return id;
        }

        if (entity["displayName"]?["default"] is JsonValue display) return (string)display!;
        if (entity["name"] is JsonValue name) return (string)name!;
        if (entity["title"] is JsonValue title && title.TryGetValue<string>(out var titleText)) return titleText;
        if (entity["title"]?["default"] is JsonValue localized) return (string)localized!;
        if (entity["titleTextId"] is JsonValue titleId) return ResolveText((string)titleId!);
        if (entity["label"] is JsonValue factLabel) return (string)factLabel!;
        if (entity["nodes"] is JsonArray nodes && nodes.FirstOrDefault() is JsonObject first && first["textId"] is JsonValue line)
        {
            return $"Разговор «{Shorten(ResolveText((string)line!))}»";
        }

        if (entity["value"]?["default"] is JsonValue value) return Shorten((string)value!);
        return id[(id.LastIndexOf('/') + 1)..];
    }

    private static string TextOf(JsonObject? entity) =>
        entity?["value"]?["default"] is JsonValue value ? (string)value! : "";

    private static string Shorten(string text) => text.Length <= 60 ? text : text[..59] + "…";
}
