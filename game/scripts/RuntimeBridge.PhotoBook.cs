using System.Text.Json;
using System.Threading;
using Godot;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal enum PhotoBookReturnOutcome
    {
        Failed,
        ReturnedAndSaved,
        ReturnedSaveFailed
    }

    internal const string PhotoBookForestEntryInteractionId = "urman.fullgame:interaction/pw-enter-forest";
    internal const string PhotoBookForestReturnInteractionId = "urman.fullgame:interaction/pw-forest-book-return";

    internal const string PhotoBookCellarEntryInteractionId = "urman.fullgame:interaction/pw-enter-cellar";
    internal const string PhotoBookCellarReturnInteractionId = "urman.fullgame:interaction/pw-cellar-book-return";
    internal const string PhotoBookRiverReturnInteractionId = "urman.fullgame:interaction/pw-river-book-return";
    internal const string PhotoBookHouseReturnInteractionId = "urman.fullgame:interaction/pw-house-book-return";
    internal const string PhotoBookCommonReturnInteractionId = "urman.fullgame:interaction/pw-common-book-return";

    internal bool IsPhotoBookReturnScene => CurrentPhotoBookReturnInteractionId is not null;
    private string? CurrentPhotoBookReturnInteractionId => ActiveSceneId switch
    {
        "urman.fullgame:scene/pw-forest" => PhotoBookForestReturnInteractionId,
        "urman.fullgame:scene/pw-river" => PhotoBookRiverReturnInteractionId,
        "urman.fullgame:scene/pw-house" => PhotoBookHouseReturnInteractionId,
        "urman.fullgame:scene/pw-common" => PhotoBookCommonReturnInteractionId,
        "urman.fullgame:scene/pw-cellar" => PhotoBookCellarReturnInteractionId,
        _ => null
    };

    private static string? PhotoVisitWorldForInteraction(string interactionId) => interactionId switch
    {
        PhotoBookForestEntryInteractionId or PhotoBookForestReturnInteractionId
            or "urman.fullgame:interaction/pw-forest-return" => "W01",
        "urman.fullgame:interaction/pw-enter-river" or PhotoBookRiverReturnInteractionId
            or "urman.fullgame:interaction/pw-water-return" => "W02",
        "urman.fullgame:interaction/pw-enter-house" or PhotoBookHouseReturnInteractionId
            or "urman.fullgame:interaction/pw-house-return" => "W03",
        "urman.fullgame:interaction/pw-enter-common" or PhotoBookCommonReturnInteractionId
            or "urman.fullgame:interaction/pw-common-return" => "W04",
        PhotoBookCellarEntryInteractionId or PhotoBookCellarReturnInteractionId
            or "urman.fullgame:interaction/pw-cellar-return" => "W05",
        _ => null
    };

    private static bool IsPhotoVisitEntry(string interactionId) => interactionId is
        PhotoBookForestEntryInteractionId or "urman.fullgame:interaction/pw-enter-river"
        or "urman.fullgame:interaction/pw-enter-house" or "urman.fullgame:interaction/pw-enter-common"
        or PhotoBookCellarEntryInteractionId;

    private static string? PhotoBookEntryInteractionForPhoto(string photoId) => photoId switch
    {
        "P-FOREST" => PhotoBookForestEntryInteractionId,
        "P-WATER" => "urman.fullgame:interaction/pw-enter-river",
        "P-HOUSE" => "urman.fullgame:interaction/pw-enter-house",
        "P-COMMON" => "urman.fullgame:interaction/pw-enter-common",
        "P-CELLAR" => PhotoBookCellarEntryInteractionId,
        _ => null
    };

    private bool IsCompletedPhotoBookEntry(string interactionId) => IsPhotoVisitEntry(interactionId)
        && _kernel is { } session && PhotoVisitWorldForInteraction(interactionId) is { } worldId
        && PhotoWorldState.IsWorldCompleted(session.SelectState(), worldId);

    internal bool CanReturnFromPhotoBook(out string reason)
    {
        reason = _loadingSlot || _loadPreparing || NeedsPhysicalRecovery || SessionIdentity is null
            ? "Дождитесь завершения перехода."
            : ReferenceEquals(_photoBookBusySession, SessionIdentity)
                ? "Дождитесь завершения действия с книгой."
                : CurrentPhotoBookReturnInteractionId is not { } returnId || !IsInteractionAvailable(returnId)
                    ? "Вернуться через книгу можно из открытого снимка."
                    : PhotoBookReturnPhysicalBlockReason(requirePhysicalWorld: true) ?? string.Empty;
        return reason.Length == 0;
    }

    private string? PhotoBookReturnPhysicalBlockReason(bool requirePhysicalWorld)
    {
        var main = GetTree().GetFirstNodeInGroup("zone_manager") as Main;
        if (main is null) return requirePhysicalWorld ? "Дождитесь завершения перехода." : null;
        var forest = main.GetNodeOrNull<W01Controller>("ZoneHost/W01");
        var river = main.GetNodeOrNull<W02Controller>("ZoneHost/W02");
        var house = main.GetNodeOrNull<W03Controller>("ZoneHost/W03");
        var common = main.GetNodeOrNull<W04Controller>("ZoneHost/W04");
        var cellar = main.GetNodeOrNull<W05Controller>("ZoneHost/W05");
        var presentationReady = ActiveSceneId switch
        {
            "urman.fullgame:scene/pw-forest" => main.ActiveZoneScenePath == "res://scenes/photoworlds/W01/W01.tscn"
                && forest is not null && GodotObject.IsInstanceValid(forest) && forest.IsInsideTree() && !forest.IsQueuedForDeletion(),
            "urman.fullgame:scene/pw-river" => main.ActiveZoneScenePath == "res://scenes/photoworlds/W02/W02.tscn"
                && river is not null && GodotObject.IsInstanceValid(river) && river.IsInsideTree() && !river.IsQueuedForDeletion(),
            "urman.fullgame:scene/pw-house" => main.ActiveZoneScenePath == "res://scenes/photoworlds/W03/W03.tscn"
                && house is not null && GodotObject.IsInstanceValid(house) && house.IsInsideTree() && !house.IsQueuedForDeletion(),
            "urman.fullgame:scene/pw-common" => main.ActiveZoneScenePath == "res://scenes/photoworlds/W04/W04.tscn"
                && common is not null && GodotObject.IsInstanceValid(common) && common.IsInsideTree() && !common.IsQueuedForDeletion(),
            "urman.fullgame:scene/pw-cellar" => main.ActiveZoneScenePath == "res://scenes/photoworlds/W05/W05.tscn"
                && cellar is not null && GodotObject.IsInstanceValid(cellar) && cellar.IsInsideTree() && !cellar.IsQueuedForDeletion(),
            _ => false
        };
        if (!presentationReady || FindPlayer() is not { } player || !player.IsInsideTree() || player.IsQueuedForDeletion()
            || player.SessionTransitionActive || player.InIntro)
            return "Дождитесь завершения перехода.";
        if (GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true })
            return "Сначала закончите разговор.";
        if (player.VehicleControlled) return "Сначала выйдите из транспорта.";
        if (player.IsClimbingLadder) return "Сначала сойдите с лестницы.";
        var carry = GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
        if (carry is { ActionInProgress: true } || (ActiveSceneId == "urman.fullgame:scene/pw-forest" && forest!.PhotoBookReturnActionInProgress)
            || (ActiveSceneId == "urman.fullgame:scene/pw-river" && river!.PhotoBookReturnActionInProgress)
            || (ActiveSceneId == "urman.fullgame:scene/pw-house" && house!.PhotoBookReturnActionInProgress)
            || (ActiveSceneId == "urman.fullgame:scene/pw-common" && common!.PhotoBookReturnActionInProgress)
            || (ActiveSceneId == "urman.fullgame:scene/pw-cellar" && cellar!.ActionInProgress))
            return "Сначала закончите действие в снимке.";
        if (carry is { HeldItem: not null }) return "Сначала поставьте предмет из рук.";
        if (!player.IsOnFloor() || !player.CanStandAt(player.GlobalPosition))
            return "Встаньте на свободное ровное место.";
        return null;
    }

    internal async Task<PhotoBookReturnOutcome> ReturnFromPhotoBookAsync()
    {
        if (!CanReturnFromPhotoBook(out _) || SessionIdentity is not { } session
            || CurrentPhotoBookReturnInteractionId is not { } returnId)
            return PhotoBookReturnOutcome.Failed;
        _photoBookBusySession = session;
        try
        {
            if (!await DispatchInteractionAsync(returnId)
                || !ReferenceEquals(session, SessionIdentity) || _loadingSlot || _loadPreparing
                || ActiveSceneId != "urman.fullgame:scene/pw-home")
                return PhotoBookReturnOutcome.Failed;

            return await CompletePhotoBookHomeReturnAsync(session);
        }
        finally
        {
            if (ReferenceEquals(_photoBookBusySession, session)) _photoBookBusySession = null;
        }
    }

    private async Task<PhotoBookReturnOutcome> CompletePhotoBookHomeReturnAsync(RuntimeKernel session)
    {
        // Main projects its committed destination from the deferred state
        // notification. Save the actual home pose, never the old photo-world pose.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree() || IsQueuedForDeletion() || _loadingSlot || _loadPreparing
            || !ReferenceEquals(session, SessionIdentity) || ActiveSceneId != "urman.fullgame:scene/pw-home")
            return PhotoBookReturnOutcome.Failed;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree() || IsQueuedForDeletion() || _loadingSlot || _loadPreparing
            || !ReferenceEquals(session, SessionIdentity) || ActiveSceneId != "urman.fullgame:scene/pw-home"
            || GetTree().GetFirstNodeInGroup("zone_manager") is not Main { ConnectedWorld: { } world }
            || world.ActiveZoneId != CurrentZoneId
            || FindPlayer() is not { SessionTransitionActive: false } player
            || !player.CanStandAt(player.GlobalPosition))
            return PhotoBookReturnOutcome.Failed;

        var checkpointSaved = false;
        try
        {
            checkpointSaved = await SaveCheckpointAsync(force: true);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Photo book return checkpoint failed: {exception.GetType().Name}");
        }

        if (!ReferenceEquals(session, SessionIdentity) || _loadingSlot || _loadPreparing
            || ActiveSceneId != "urman.fullgame:scene/pw-home")
            return PhotoBookReturnOutcome.Failed;
        return checkpointSaved
            ? PhotoBookReturnOutcome.ReturnedAndSaved
            : PhotoBookReturnOutcome.ReturnedSaveFailed;
    }

    internal bool CanEnterPhotoBookCellar(out string reason) => CanEnterPhotoBookPhoto("P-CELLAR", out reason);
    internal Task<bool> EnterPhotoBookCellarAsync() => EnterPhotoBookPhotoAsync("P-CELLAR");
    internal bool CanEnterPhotoBookForest(out string reason) => CanEnterPhotoBookPhoto("P-FOREST", out reason);
    internal Task<bool> EnterPhotoBookForestAsync() => EnterPhotoBookPhotoAsync("P-FOREST");

    internal bool CanEnterPhotoBookPhoto(string photoId, out string reason)
    {
        var session = SessionIdentity;
        if (_loadingSlot || _loadPreparing || NeedsPhysicalRecovery || session is null)
            reason = "Дождитесь завершения перехода.";
        else if (ReferenceEquals(_photoBookBusySession, session))
            reason = "Дождитесь завершения действия с книгой.";
        else if (PhotoBookEntryInteractionForPhoto(photoId) is not { } interactionId)
            reason = "В этот снимок нельзя войти.";
        else if (!IsInteractionAvailable(interactionId))
        {
            var state = session.SelectState();
            reason = photoId == "P-CELLAR"
                ? "Получите разрешённый снимок подполья и семейный комментарий, затем вклейте снимок в книгу."
                : !BookReceived(state)
                ? "Примите семейную книгу у Гөлсинә."
                : photoId == "P-FOREST" && !PhotoFlag(state, "P-FOREST", "mounted")
                    ? "Вклейте снимок «Люди у леса»."
                    : photoId == "P-FOREST" && !PhotoFlag(state, "S-SCHOOL", "acquired")
                        ? "Получите разрешённую копию школьного альбома в старой школе."
                        : PhotoBookForestPhysicalBlockReason(requirePhysicalWorld: true, interactionId)
                            ?? "Сейчас вход в этот снимок недоступен.";
        }
        else if (!PhotoBookPhotos().Any(photo => photo.PhotoId == photoId && photo.Mounted))
            reason = "Вклейте снимок на его разворот в семейной книге.";
        else
            reason = PhotoBookForestPhysicalBlockReason(requirePhysicalWorld: true, interactionId) ?? string.Empty;
        return reason.Length == 0;
    }

    private string? PhotoBookForestPhysicalBlockReason(bool requirePhysicalWorld, string interactionId = PhotoBookForestEntryInteractionId)
    {
        if (_kernel is not { } session || PhotoVisitWorldForInteraction(interactionId) is not { } worldId
            || !PhotoWorldState.TryGetVisitOrigin(session.SelectState(), worldId, out var originZone, out var originAnchor, out _))
            return "Дождитесь завершения перехода.";
        var riverOrigin = originAnchor == "PW_RIVER_ENTRY";
        var commonOrigin = originAnchor == "PW_COMMON_ENTRY";
        var houseOrigin = originAnchor == "PW_HOME_PHOTO_ENTRY";
        var forestOrigin = originAnchor == "PW_FOREST_ENTRY";
        var cellarOrigin = originAnchor == "PW_CELLAR_ENTRY";
        var returnToOrigin = forestOrigin ? "Вернитесь к опушке у трёх стволов со снимка."
            : cellarOrigin ? "Вернитесь к двери сухих сеней семейного дома."
                : houseOrigin ? "Вернитесь во двор семейного дома к наружному окну со снимка."
                    : commonOrigin ? "Вернитесь к месту общего снимка у берега и найдите три ориентира."
                        : riverOrigin ? "Вернитесь к старому подходу к мосткам на берегу."
                            : "Вернитесь к домашнему столу с книгой.";
        var world = GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
        if (world is null)
            return requirePhysicalWorld ? returnToOrigin : null;
        var revisit = IsCompletedPhotoBookEntry(interactionId);
        Node3D anchor;
        Vector3? facingPoint = null;
        if (riverOrigin || commonOrigin || houseOrigin || forestOrigin || cellarOrigin)
        {
            if (CurrentZoneId != originZone || world.ActiveZoneId != originZone
                || GetTree().GetFirstNodeInGroup("zone_manager") is not Main main
                || !main.TryGetPhotoVisitOriginAnchor(originZone, originAnchor, out var outdoorAnchor))
                return returnToOrigin;
            var targetName = commonOrigin ? "CommonPhotoEntryTarget" : "RiverPhotoEntryTarget";
            var markerChildTarget = houseOrigin ? "HousePhotoEntryTarget"
                : forestOrigin ? "ForestPhotoEntryTarget"
                    : cellarOrigin ? "CellarPhotoEntryTarget" : null;
            var target = markerChildTarget is not null
                ? outdoorAnchor.GetNodeOrNull<InteractionTarget>(markerChildTarget)
                : world.GetNodeOrNull<InteractionTarget>(
                    "Act1CoreWorldGreybox/PhotoBankApproach/PhotoBankApproachPresence/" + targetName);
            if (target is null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree() || target.IsQueuedForDeletion()
                || !target.IsVisibleInTree() || target.InteractionId != interactionId)
                return returnToOrigin;
            anchor = outdoorAnchor;
            if (markerChildTarget is not null) facingPoint = target.GlobalPosition;
        }
        else
        {
            var anchorName = revisit ? "PhotoWorldForestEntryTarget" : interactionId switch
            {
                "urman.fullgame:interaction/pw-enter-house" => "HousePhotoEntryTarget",
                "urman.fullgame:interaction/pw-enter-common" => "CommonPhotoEntryTarget",
                _ => "PhotoWorldForestEntryTarget"
            };
            var anchorInteraction = revisit || interactionId == PhotoBookCellarEntryInteractionId
                ? PhotoBookForestEntryInteractionId : interactionId;
            if (CurrentZoneId != originZone || world.ActiveZoneId != originZone
                || world.GetZoneInstance(originZone)?.GetNodeOrNull<InteractionTarget>(anchorName) is not { } homeAnchor
                || !GodotObject.IsInstanceValid(homeAnchor) || !homeAnchor.IsInsideTree() || homeAnchor.IsQueuedForDeletion()
                || homeAnchor.InteractionId != anchorInteraction)
                return returnToOrigin;
            anchor = homeAnchor;
        }
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
            return forestOrigin ? "Встаньте на свободное место у трёх стволов на опушке."
                : cellarOrigin ? "Встаньте на свободное место перед дверью сухих сеней."
                    : houseOrigin ? "Встаньте на свободное место во дворе у наружного окна семейного дома."
                        : commonOrigin ? "Встаньте на свободное место у точки общего снимка на берегу."
                            : riverOrigin ? "Встаньте на свободное место у старого подхода к мосткам."
                                : "Встаньте на свободное место у домашнего стола.";
        var toAnchor = anchor.GlobalPosition - player.GlobalPosition;
        if (toAnchor.LengthSquared() > 36f) return forestOrigin
            ? "Подойдите ближе к трём стволам на опушке."
            : cellarOrigin ? "Подойдите ближе к двери сухих сеней семейного дома."
                : houseOrigin ? "Подойдите ближе к наружному окну семейного дома со стороны двора."
                    : commonOrigin ? "Подойдите ближе к месту общего снимка у берега."
                        : riverOrigin ? "Подойдите ближе к старому подходу к мосткам на берегу."
                            : "Подойдите ближе к домашнему столу с книгой.";
        var toFacing = facingPoint is { } point ? point - player.GlobalPosition : toAnchor;
        toFacing.Y = 0f;
        if (!revisit && toFacing.LengthSquared() > .0001f)
        {
            var forward = -player.GlobalBasis.Z;
            forward.Y = 0f;
            if (forward.LengthSquared() < .0001f
                || forward.Normalized().Dot(toFacing.Normalized()) < Mathf.Cos(Mathf.DegToRad(40f)))
                return forestOrigin ? "Закройте альбом и повернитесь к трём стволам на опушке."
                    : cellarOrigin ? "Закройте альбом и повернитесь к двери сухих сеней."
                        : houseOrigin ? "Закройте альбом и повернитесь к наружному окну семейного дома."
                            : commonOrigin ? "Закройте альбом и повернитесь к месту общего снимка у берега."
                                : riverOrigin ? "Закройте альбом и повернитесь к старому подходу к мосткам."
                                    : "Закройте альбом и повернитесь к домашнему столу с книгой.";
        }
        return null;
    }

    internal async Task<bool> EnterPhotoBookPhotoAsync(string photoId)
    {
        if (!CanEnterPhotoBookPhoto(photoId, out _) || SessionIdentity is not { } session
            || PhotoBookEntryInteractionForPhoto(photoId) is not { } interactionId
            || PhotoVisitWorldForInteraction(interactionId) is not { } worldId
            || !PhotoWorldState.TryGetVisitRoute(worldId, out var sceneId, out _)) return false;
        _photoBookBusySession = session;
        try
        {
            return await DispatchInteractionAsync(interactionId)
                && ReferenceEquals(session, SessionIdentity) && !_loadingSlot && !_loadPreparing
                && ActiveSceneId == sceneId;
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
        public bool WorldCompleted { get; init; }
        public bool HasResumePoint { get; init; }
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
            var worldId = PhotoBookEntryInteractionForPhoto(definition.Id) is { } entryInteractionId
                ? PhotoVisitWorldForInteraction(entryInteractionId) : null;
            var worldCompleted = worldId is not null && PhotoWorldState.IsWorldCompleted(state, worldId);
            var hasResumePoint = !worldCompleted && worldId is not null
                && PhotoWorldState.TryGetVisitRoute(worldId, out _, out var entryNode)
                && PhotoWorldState.TryGetVisitStartNode(state, worldId, out var startNode)
                && startNode != entryNode;
            var photo = new PhotoBookPhoto(
                definition.Id,
                string.IsNullOrWhiteSpace(definition.Title) ? "Найденный снимок" : definition.Title.Trim(),
                definition.FrontBrief,
                definition.BackTextRu,
                backRead,
                mounted)
            {
                PageId = definition.PageId,
                WorldCompleted = worldCompleted,
                HasResumePoint = hasResumePoint,
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
