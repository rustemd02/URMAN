using System.Text.Json;
using System.Threading;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal bool CanInspectReceivedPhotoBook => !_loadingSlot && !_loadPreparing
        && _content.HasPhotoWorlds && SessionIdentity is { } session
        && BookReceived(session.SelectState());

    internal sealed record PhotoBookPhoto(
        string PhotoId,
        string Title,
        string? FrontBrief,
        string? BackTextRu,
        bool BackRead,
        bool Mounted)
    {
        public bool CanReadBack { get; init; }
        public bool CanMount { get; init; }
    }

    private RuntimeKernel? _photoBookBusySession;

    /// <summary>Projects only acquired photos from the validated live session.</summary>
    internal IReadOnlyList<PhotoBookPhoto> PhotoBookPhotos()
    {
        if (_loadingSlot || _loadPreparing
            || SessionIdentity is not { } session
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
            var photo = new PhotoBookPhoto(
                definition.Id,
                string.IsNullOrWhiteSpace(definition.Title) ? "Найденный снимок" : definition.Title.Trim(),
                definition.FrontBrief,
                definition.BackTextRu,
                backRead,
                mounted)
            {
                CanReadBack = !ReferenceEquals(_photoBookBusySession, session)
                    && CanReadPhotoBack(definition.Id, definition.BackTextRu, state),
                CanMount = !ReferenceEquals(_photoBookBusySession, session)
                    && TryBuildPhotoMountRequest(definition, state, out _, out _)
            };
            acquired.Add(photo);
        }

        return acquired;
    }

    internal Task<bool> ReadPhotoBookBackAsync(string photoId) => ApplyPhotoBookActionAsync(photoId, mount: false);

    internal Task<bool> MountPhotoBookPhotoAsync(string photoId) => ApplyPhotoBookActionAsync(photoId, mount: true);

    private async Task<bool> ApplyPhotoBookActionAsync(string photoId, bool mount)
    {
        if (_loadingSlot || _loadPreparing || !_content.HasPhotoWorlds
            || SessionIdentity is not { } session
            || ReferenceEquals(_photoBookBusySession, session)
            || _content.PhotoWorldDefinitions is not { } catalog
            || catalog.Photos.FirstOrDefault(photo => photo.Id == photoId) is not { } definition)
            return false;

        _photoBookBusySession = session;
        try
        {
            var state = session.SelectState();
            if (!ReferenceEquals(session, SessionIdentity) || _loadingSlot || _loadPreparing)
                return false;

            JsonElement conditions;
            JsonElement effects;
            if (mount)
            {
                if (!TryBuildPhotoMountRequest(definition, state, out conditions, out effects))
                    return false;
            }
            else
            {
                if (!CanReadPhotoBack(photoId, definition.BackTextRu, state))
                    return false;

                var readConditions = new List<object>
                {
                    new { op = "photoworlds.photo.state", photoId, state = "acquired" },
                    new { op = "not", condition = new { op = "photoworlds.photo.state", photoId, state = "back-read" } }
                };
                if (IsBookHandoffRequired(photoId) && !BookReceived(state))
                    return false;
                if (IsBookHandoffRequired(photoId))
                    readConditions.Add(new { op = "photoworlds.book.received" });
                conditions = JsonSerializer.SerializeToElement(readConditions);
                effects = JsonSerializer.SerializeToElement(new[] { new { op = "photoworlds.photo.back-read", photoId } });
            }

            if (_loadingSlot || _loadPreparing || !ReferenceEquals(session, SessionIdentity))
                return false;
            var result = await DispatchContentApplyAsync(
                $"photo-book:{(mount ? "mount" : "back-read")}:{photoId}:{Interlocked.Increment(ref _interactionSequence):D8}",
                conditions,
                effects,
                activeSceneId: null,
                expectedSession: session);
            return result.Status == CommandStatus.Committed && ReferenceEquals(session, SessionIdentity);
        }
        finally
        {
            if (ReferenceEquals(_photoBookBusySession, session))
                _photoBookBusySession = null;
        }
    }

    private bool CanReadPhotoBack(string photoId, string? backTextRu, JsonElement state) =>
        !string.IsNullOrWhiteSpace(backTextRu)
        && PhotoFlag(state, photoId, "acquired")
        && !PhotoFlag(state, photoId, "backRead")
        && (!IsBookHandoffRequired(photoId) || BookReceived(state));

    private bool TryBuildPhotoMountRequest(
        Urman.Core.Narrative.PhotoWorldPhotoDefinition definition,
        JsonElement state,
        out JsonElement conditions,
        out JsonElement effects)
    {
        conditions = default;
        effects = default;
        if (!PhotoFlag(state, definition.Id, "acquired")
            || PhotoFlag(state, definition.Id, "mounted")
            || (IsBookHandoffRequired(definition.Id) && !BookReceived(state)))
            return false;

        var authoredRoutes = new List<(CompiledInteractionContent Interaction, JsonElement Effect)>();
        foreach (var interaction in _content.Interactions)
        {
            if (interaction.Effects.ValueKind != JsonValueKind.Array) continue;
            foreach (var effect in interaction.Effects.EnumerateArray())
            {
                if (effect.ValueKind != JsonValueKind.Object
                    || !effect.TryGetProperty("op", out var operation)
                    || operation.GetString() != "photoworlds.photo.mount"
                    || !effect.TryGetProperty("photoId", out var effectPhotoId)
                    || effectPhotoId.GetString() != definition.Id)
                    continue;
                authoredRoutes.Add((interaction, effect.Clone()));
            }
        }

        foreach (var route in authoredRoutes)
        {
            if (!route.Effect.TryGetProperty("pageId", out var routePageId)
                || routePageId.GetString() != definition.PageId
                || !OwnsInteraction(route.Interaction, state))
                continue;

            var routeConditions = PhotoMountConditions(
                route.Interaction,
                definition.Id,
                requiresBookHandoff: IsBookHandoffRequired(definition.Id));
            if (!ContentRuleEngine.EvaluateAll(routeConditions, state))
                continue;

            conditions = routeConditions;
            effects = JsonSerializer.SerializeToElement(new[] { route.Effect });
            return true;
        }

        // A photo with any authored mount route stays governed by that route,
        // even when its current source ownership or conditions are unavailable.
        if (authoredRoutes.Count > 0 || !BookReceived(state))
            return false;

        conditions = PhotoMountConditions(null, definition.Id, requiresBookHandoff: true);
        effects = JsonSerializer.SerializeToElement(new[]
        {
            new { op = "photoworlds.photo.mount", photoId = definition.Id, pageId = definition.PageId }
        });
        return true;
    }

    private static JsonElement PhotoMountConditions(
        CompiledInteractionContent? route,
        string photoId,
        bool requiresBookHandoff)
    {
        var conditions = route is null ? new List<JsonElement>() : Elements(route.Conditions).ToList();
        if (route is { WorldLocations: null })
            conditions.Add(JsonSerializer.SerializeToElement(new { op = "location.is", locationId = route.SourceSceneId }));
        conditions.Add(JsonSerializer.SerializeToElement(new { op = "photoworlds.photo.state", photoId, state = "acquired" }));
        conditions.Add(JsonSerializer.SerializeToElement(new
        {
            op = "not",
            condition = new { op = "photoworlds.photo.state", photoId, state = "mounted" }
        }));
        if (requiresBookHandoff)
            conditions.Add(JsonSerializer.SerializeToElement(new { op = "photoworlds.book.received" }));
        return JsonSerializer.SerializeToElement(conditions);
    }

    private bool IsBookHandoffRequired(string photoId) =>
        TryGetRawPhotoDefinition(photoId, out var definition)
        && definition.TryGetProperty("requiresBookHandoff", out var required)
        && required.ValueKind == JsonValueKind.True;

    private bool TryGetRawPhotoDefinition(string photoId, out JsonElement definition)
    {
        var catalog = _content.PhotoWorldCatalog;
        if (catalog.ValueKind == JsonValueKind.Object
            && catalog.TryGetProperty("photos", out var photos)
            && photos.ValueKind == JsonValueKind.Array)
        {
            foreach (var photo in photos.EnumerateArray())
            {
                if (photo.TryGetProperty("id", out var id)
                    && id.ValueKind == JsonValueKind.String
                    && StringComparer.Ordinal.Equals(id.GetString(), photoId))
                {
                    definition = photo;
                    return true;
                }
            }
        }

        definition = default;
        return false;
    }

    private static bool PhotoFlag(JsonElement state, string photoId, string property) =>
        state.TryGetProperty("photoworlds", out var photoWorlds)
        && photoWorlds.ValueKind == JsonValueKind.Object
        && photoWorlds.TryGetProperty("photos", out var photos)
        && photos.ValueKind == JsonValueKind.Object
        && photos.TryGetProperty(photoId, out var photo)
        && photo.ValueKind == JsonValueKind.Object
        && photo.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static bool BookReceived(JsonElement state) =>
        state.TryGetProperty("photoworlds", out var photoWorlds)
        && photoWorlds.ValueKind == JsonValueKind.Object
        && photoWorlds.TryGetProperty("book", out var book)
        && book.ValueKind == JsonValueKind.Object
        && book.TryGetProperty("received", out var received)
        && received.ValueKind == JsonValueKind.True;
}
