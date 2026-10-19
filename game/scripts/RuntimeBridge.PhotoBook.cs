using System.Text.Json;
using System.Threading;
using Godot;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal const string PhotoBookForestEntryInteractionId = "urman.fullgame:interaction/pw-enter-forest";

    internal bool CanEnterPhotoBookForest(out string reason)
    {
        reason = _loadingSlot || _loadPreparing || NeedsPhysicalRecovery || SessionIdentity is null
            ? "Дождитесь завершения перехода."
            : ReferenceEquals(_photoBookBusySession, SessionIdentity)
                ? "Дождитесь завершения действия с книгой."
                : !IsInteractionAvailable(PhotoBookForestEntryInteractionId)
                    ? "Сначала сопоставьте школьную копию со снимком «Люди у леса» и вклейте снимок."
                    : PhotoBookForestPhysicalBlockReason(requirePhysicalWorld: true) ?? string.Empty;
        return reason.Length == 0;
    }

    private string? PhotoBookForestPhysicalBlockReason(bool requirePhysicalWorld)
    {
        var world = GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
        if (world is null)
            return requirePhysicalWorld ? "Вернитесь к домашнему столу с книгой." : null;
        if (CurrentZoneId != "house_old_pc" || world.ActiveZoneId != "house_old_pc"
            || world.GetZoneInstance("house_old_pc")?.GetNodeOrNull<InteractionTarget>("PhotoWorldForestEntryTarget") is not { } anchor
            || !GodotObject.IsInstanceValid(anchor) || !anchor.IsInsideTree() || anchor.IsQueuedForDeletion()
            || anchor.InteractionId != PhotoBookForestEntryInteractionId)
            return "Вернитесь к домашнему столу с книгой.";
        if (FindPlayer() is not { } player || !player.IsInsideTree() || player.IsQueuedForDeletion())
            return "Дождитесь завершения перехода.";
        if (player.SessionTransitionActive || player.InIntro)
            return "Дождитесь завершения перехода.";
        if (GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true })
            return "Сначала закончите разговор.";
        if (player.VehicleControlled) return "Сначала выйдите из транспорта.";
        if (player.IsClimbingLadder) return "Сначала сойдите с лестницы.";
        var carry = GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
        if (carry is { ActionInProgress: true }) return "Дождитесь завершения действия с предметом.";
        if (carry is { HeldItem: not null }) return "Сначала поставьте предмет из рук.";
        if (!player.IsOnFloor() || !player.CanStandAt(player.GlobalPosition))
            return "Встаньте на свободное место у домашнего стола.";
        var toAnchor = anchor.GlobalPosition - player.GlobalPosition;
        if (toAnchor.LengthSquared() > 36f) return "Подойдите ближе к домашнему столу с книгой.";
        toAnchor.Y = 0f;
        if (toAnchor.LengthSquared() > .0001f)
        {
            var forward = -player.GlobalBasis.Z;
            forward.Y = 0f;
            if (forward.LengthSquared() < .0001f
                || forward.Normalized().Dot(toAnchor.Normalized()) < Mathf.Cos(Mathf.DegToRad(40f)))
                return "Закройте альбом и повернитесь к домашнему столу с книгой.";
        }
        return null;
    }

    internal async Task<bool> EnterPhotoBookForestAsync()
    {
        if (!CanEnterPhotoBookForest(out _) || SessionIdentity is not { } session) return false;
        _photoBookBusySession = session;
        try
        {
            return await DispatchInteractionAsync(PhotoBookForestEntryInteractionId)
                && ReferenceEquals(session, SessionIdentity) && !_loadingSlot && !_loadPreparing
                && ActiveSceneId == "urman.fullgame:scene/pw-forest";
        }
        finally
        {
            if (ReferenceEquals(_photoBookBusySession, session)) _photoBookBusySession = null;
        }
    }

    internal bool CanInspectReceivedPhotoBook => !_loadingSlot && !_loadPreparing
        && _content.HasPhotoWorlds && SessionIdentity is { } session
        && BookReceived(session.SelectState());

    internal bool CanShowFamilyBookOnTable => !_loadingSlot && !_loadPreparing
        && _content.HasPhotoWorlds && SessionIdentity is { } session
        && !BookReceived(session.SelectState());

    internal sealed record PhotoBookPhoto(
        string PhotoId,
        string Title,
        string? FrontBrief,
        string? BackTextRu,
        bool BackRead,
        bool Mounted)
    {
        public string PageId { get; init; } = string.Empty;
        public string? OriginLabel { get; init; }
        public bool CanReadBack { get; init; }
        public bool CanMount { get; init; }
    }

    private RuntimeKernel? _photoBookBusySession;

    internal bool IsPhotoBookProjectionDeferred => _loadingSlot || _loadPreparing;

    /// <summary>Projects only acquired photos from the validated live session.</summary>
    internal IReadOnlyList<PhotoBookPhoto> PhotoBookPhotos()
    {
        if (IsPhotoBookProjectionDeferred
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
                PageId = definition.PageId,
                OriginLabel = definition.ProvenanceKind switch
                {
                    "family-book-print" => "Снимок из семейной книги",
                    "family-envelope-print" => "Снимок из семейного конверта",
                    "family-yard-print" => "Семейный отпечаток",
                    "authorized-marat-file" => "Копия файла",
                    "family-box-print" => "Снимок из семейной коробки",
                    "family-pc-contact-sheet" => "Семейный контактный лист",
                    "school-album-copy" => "Копия из школьного альбома",
                    "archive-map-copy" => "Копия архивной схемы",
                    "gulsina-family-album-copy" => "Копия из семейного альбома",
                    "neighbor-voluntary-copy" => "Копия от соседа",
                    "family-authorized-copy" => "Семейная копия",
                    "family-photo-box-copy" => "Копия из семейной коробки",
                    "player-created-epilogue-photo" => "Собственный снимок",
                    _ => null
                },
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
            var sceneBeforeDispatch = ActiveSceneId;
            var result = await DispatchContentApplyAsync(
                $"photo-book:{(mount ? "mount" : "back-read")}:{photoId}:{Interlocked.Increment(ref _interactionSequence):D8}",
                conditions,
                effects,
                activeSceneId: null,
                expectedSession: session);
            if (result.Status != CommandStatus.Committed || !ReferenceEquals(session, SessionIdentity))
                return false;

            if (!_loadingSlot && !_loadPreparing && sceneBeforeDispatch is not null
                && ActiveSceneId == sceneBeforeDispatch)
                await SaveCheckpointAsync(force: true);

            return ReferenceEquals(session, SessionIdentity);
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
