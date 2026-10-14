using System.Text.Json;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal sealed record FullGamePhotoWorldRouteProjection(object SessionIdentity, string ActiveSceneId);

    internal bool IsPhotoWorldSaveRestoreInProgress => _loadingSlot;

    /// <summary>
    /// New-game setup and save restoration call Main.SwitchZone before the
    /// PhotoWorld scene is ready to own the physical presentation. In that
    /// window, keep the requested runtime location but do not eagerly build
    /// and immediately discard the heavier connected village for a W01 scene.
    /// </summary>
    internal bool IsW01PresentationDeferred => (_loadingSlot || _loadPreparing)
        && _content is { HasPhotoWorlds: true }
        && ActiveSceneId is (
            "urman.fullgame:scene/pw-prologue"
            or "urman.fullgame:scene/pw-forest");

    internal sealed record W01BranchProjection(object SessionIdentity, string ActiveSceneId, bool Moved);

    /// <summary>
    /// Presentation follows the committed campaign scene only. Scene entry
    /// conditions and effects remain owned by the runtime kernel.
    /// </summary>
    internal FullGamePhotoWorldRouteProjection? CurrentFullGamePhotoWorldRouteProjection()
    {
        if (_loadingSlot || _loadPreparing || SessionIdentity is not { } session
            || _content is not { HasPhotoWorlds: true })
            return null;

        var state = session.SelectState();
        if (!state.TryGetProperty("activeScene", out var scene)
            || scene.ValueKind != JsonValueKind.String
            || scene.GetString() is not { } sceneId
            || sceneId is not (
                "urman.fullgame:scene/pw-prologue"
                or "urman.fullgame:scene/pw-niva-arrival"
                or "urman.fullgame:scene/pw-home"
                or "urman.fullgame:scene/pw-forest"))
            return null;

        return new FullGamePhotoWorldRouteProjection(session, sceneId);
    }

    /// <summary>Scene presentation reads the existing committed fact only after load validation.</summary>
    internal W01BranchProjection? CurrentW01BranchProjection()
    {
        if (_loadingSlot || _loadPreparing || SessionIdentity is not { } session
            || _content is not { HasPhotoWorlds: true })
            return null;

        var state = session.SelectState();
        if (!state.TryGetProperty("activeScene", out var scene)
            || scene.ValueKind != JsonValueKind.String
            || scene.GetString() is not { } sceneId
            || sceneId is not ("urman.fullgame:scene/pw-prologue" or "urman.fullgame:scene/pw-forest")
            || !state.TryGetProperty("photoworlds", out var world)
            || world.ValueKind != JsonValueKind.Object
            || !world.TryGetProperty("facts", out var facts)
            || facts.ValueKind != JsonValueKind.Object)
            return null;

        var moved = facts.TryGetProperty("prologue_branch_moved", out var branch)
            && branch.ValueKind == JsonValueKind.Object
            && branch.TryGetProperty("sources", out var sources)
            && sources.ValueKind == JsonValueKind.Array && sources.GetArrayLength() > 0;
        return new W01BranchProjection(session, sceneId, moved);
    }
}
