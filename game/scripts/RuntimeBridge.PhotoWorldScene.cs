using System.Text.Json;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal sealed record FullGamePhotoWorldRouteProjection(object SessionIdentity, string ActiveSceneId);

    // RuntimeBridge's save-restore owner may temporarily authorize physical
    // PhotoWorld projection for this exact restored kernel while the ordinary
    // session gate remains closed. The owner must restore this field in a
    // finally block after mounting/projecting the candidate world.
    private RuntimeKernel? _photoWorldPhysicalProjectionSession;

    internal bool IsPhotoWorldPresentationDeferred =>
        _loadingSlot || _loadPreparing || NeedsPhysicalRecovery;

    /// <summary>
    /// Session identity for read-only physical projections. During a load this
    /// is non-null only for the exact kernel explicitly scoped by the restore
    /// owner; gameplay commands continue to use SessionIdentity.
    /// </summary>
    internal RuntimeKernel? PhotoWorldPresentationIdentity => IsPhotoWorldPresentationDeferred
        ? ReferenceEquals(_photoWorldPhysicalProjectionSession, _kernel) ? _kernel : null
        : SessionIdentity;

    internal bool IsPhotoWorldSaveRestoreInProgress => _loadingSlot;

    /// <summary>
    /// New-game setup and save restoration call Main.SwitchZone before the
    /// PhotoWorld scene is ready to own the physical presentation. In that
    /// window, keep the requested runtime location but do not eagerly build
    /// and immediately discard another physical zone for a photo scene.
    /// </summary>
    internal bool IsW01PresentationDeferred => (_loadingSlot || _loadPreparing)
        && _content is { HasPhotoWorlds: true }
        && ActiveSceneId is (
            "urman.fullgame:scene/pw-prologue"
            or "urman.fullgame:scene/pw-forest"
            or "urman.fullgame:scene/pw-river"
            or "urman.fullgame:scene/pw-house"
            or "urman.fullgame:scene/pw-common"
            or "urman.fullgame:scene/pw-cellar");

    internal sealed record W01BranchProjection(object SessionIdentity, string ActiveSceneId, bool Moved);

    /// <summary>
    /// Presentation follows the committed campaign scene only. Scene entry
    /// conditions and effects remain owned by the runtime kernel.
    /// </summary>
    internal FullGamePhotoWorldRouteProjection? CurrentFullGamePhotoWorldRouteProjection()
    {
        if (PhotoWorldPresentationIdentity is not { } session
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
                or "urman.fullgame:scene/pw-forest"
                or "urman.fullgame:scene/pw-river"
                or "urman.fullgame:scene/pw-house"
                or "urman.fullgame:scene/pw-common"
                or "urman.fullgame:scene/pw-cellar"
                or "urman.fullgame:scene/pw-bank-approach"
                or "urman.fullgame:scene/pw-epilogue"))
            return null;

        return new FullGamePhotoWorldRouteProjection(session, sceneId);
    }

    /// <summary>Scene presentation reads the existing committed fact only after load validation.</summary>
    internal W01BranchProjection? CurrentW01BranchProjection()
    {
        if (PhotoWorldPresentationIdentity is not { } session
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

    /// <summary>
    /// Presentation reads one committed fact anywhere the home chain is staged.
    /// The fact itself stays owned by the runtime kernel; this only projects it.
    /// </summary>
    internal bool PhotoWorldFactObserved(string factId)
    {
        if (PhotoWorldPresentationIdentity is not { } session
            || _content is not { HasPhotoWorlds: true })
            return false;

        var state = session.SelectState();
        return state.TryGetProperty("photoworlds", out var world)
            && world.ValueKind == JsonValueKind.Object
            && world.TryGetProperty("facts", out var facts)
            && facts.ValueKind == JsonValueKind.Object
            && facts.TryGetProperty(factId, out var fact)
            && fact.ValueKind == JsonValueKind.Object
            && fact.TryGetProperty("sources", out var sources)
            && sources.ValueKind == JsonValueKind.Array && sources.GetArrayLength() > 0;
    }

    internal sealed record W02WaterProjection(
        object SessionIdentity, string ActiveSceneId, bool ReedOpened, bool EncounterResolved);

    /// <summary>
    /// The summer bank reads only committed W02 facts: the reed curtain that
    /// opened and the answer that arrived. Both are presentation, never state.
    /// </summary>
    internal W02WaterProjection? CurrentW02WaterProjection()
    {
        if (PhotoWorldPresentationIdentity is not { } session
            || _content is not { HasPhotoWorlds: true })
            return null;

        var state = session.SelectState();
        if (!state.TryGetProperty("activeScene", out var scene)
            || scene.ValueKind != JsonValueKind.String
            || scene.GetString() is not { } sceneId
            || sceneId != "urman.fullgame:scene/pw-river"
            || !state.TryGetProperty("photoworlds", out var world)
            || world.ValueKind != JsonValueKind.Object
            || !world.TryGetProperty("facts", out var facts)
            || facts.ValueKind != JsonValueKind.Object)
            return null;

        static bool Observed(JsonElement facts, string factId) =>
            facts.TryGetProperty(factId, out var fact)
            && fact.ValueKind == JsonValueKind.Object
            && fact.TryGetProperty("sources", out var sources)
            && sources.ValueKind == JsonValueKind.Array && sources.GetArrayLength() > 0;

        return new W02WaterProjection(
            session, sceneId,
            Observed(facts, "water_reed_opened"),
            Observed(facts, "water_encounter_resolved"));
    }
}
