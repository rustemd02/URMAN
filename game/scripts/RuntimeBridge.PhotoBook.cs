using System.Text.Json;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal sealed record PhotoBookPhoto(
        string PhotoId,
        string Title,
        string? FrontBrief,
        string? BackTextRu,
        bool BackRead,
        bool Mounted);

    /// <summary>Projects only acquired photos from the validated live session.</summary>
    internal IReadOnlyList<PhotoBookPhoto> PhotoBookPhotos()
    {
        if (SessionIdentity is not { } session
            || !_content.HasPhotoWorlds
            || _content.PhotoWorldDefinitions is not { } catalog)
            return [];

        var state = session.SelectState();
        if (!state.TryGetProperty("photoworlds", out var photoWorlds)
            || photoWorlds.ValueKind != JsonValueKind.Object
            || !photoWorlds.TryGetProperty("photos", out var savedPhotos)
            || savedPhotos.ValueKind != JsonValueKind.Object)
            return [];

        var acquired = new List<PhotoBookPhoto>();
        foreach (var definition in catalog.Photos)
        {
            if (!savedPhotos.TryGetProperty(definition.Id, out var savedPhoto)
                || savedPhoto.ValueKind != JsonValueKind.Object
                || !savedPhoto.TryGetProperty("acquired", out var acquiredState)
                || acquiredState.ValueKind != JsonValueKind.True)
                continue;

            var backRead = savedPhoto.TryGetProperty("backRead", out var backReadState)
                && backReadState.ValueKind == JsonValueKind.True;
            var mounted = savedPhoto.TryGetProperty("mounted", out var mountedState)
                && mountedState.ValueKind == JsonValueKind.True
                && savedPhoto.TryGetProperty("pageId", out var pageState)
                && pageState.ValueKind == JsonValueKind.String
                && StringComparer.Ordinal.Equals(pageState.GetString(), definition.PageId);
            acquired.Add(new PhotoBookPhoto(
                definition.Id,
                string.IsNullOrWhiteSpace(definition.Title) ? "Найденный снимок" : definition.Title.Trim(),
                definition.FrontBrief,
                definition.BackTextRu,
                backRead,
                mounted));
        }

        return acquired;
    }
}
