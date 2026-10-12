using System.Text.Json;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal sealed record W01BranchProjection(object SessionIdentity, string ActiveSceneId, bool Moved);

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
