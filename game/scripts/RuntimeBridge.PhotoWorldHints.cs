namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal const string ForestBackInspectionCue = "Выйдите из поворота и оглянитесь на три ствола.";
    internal sealed record PhotoWorldHintProjection(string Key, string Question, string Area, string Action);

    /// <summary>Projects the next authored W01 forest hint without changing campaign state.</summary>
    internal PhotoWorldHintProjection? CurrentPhotoWorldHint()
    {
        if (_loadingSlot || _loadPreparing || !_content.HasPhotoWorlds
            || SessionIdentity is not { } session
            || !CanInspectReceivedPhotoBook)
            return null;

        var branch = CurrentW01BranchProjection();
        if (branch is not { ActiveSceneId: "urman.fullgame:scene/pw-forest" }
            || !ReferenceEquals(branch.SessionIdentity, session)
            || !ReferenceEquals(session, SessionIdentity))
            return null;

        string key;
        string nextInteractionId;
        if (!PhotoWorldFactObserved("forest_camera_back_seen"))
        {
            key = "camera";
            nextInteractionId = "urman.fullgame:interaction/pw-forest-group";
        }
        else if (!PhotoWorldFactObserved("forest_beyond_frame_seen"))
        {
            key = "path";
            nextInteractionId = "urman.fullgame:interaction/pw-forest-photographer";
        }
        else if (!PhotoWorldFactObserved("forest_shurale_instructions_heard"))
        {
            key = "talk";
            nextInteractionId = "urman.fullgame:interaction/pw-forest-shurale";
        }
        else if (!PhotoWorldFactObserved("forest_encounter_resolved"))
        {
            key = "bag";
            nextInteractionId = "urman.fullgame:interaction/pw-forest-bag-release";
        }
        else
        {
            key = "exit";
            nextInteractionId = "urman.fullgame:interaction/pw-forest-return";
        }

        // Do not hint an action the authored interaction rules currently reject.
        if (!IsInteractionAvailable(nextInteractionId)
            || _loadingSlot || _loadPreparing || !ReferenceEquals(session, SessionIdentity))
            return null;

        var action = ResolveText($"urman.fullgame:text/pw-hint-forest-{key}-action");
        if (key == "path")
            action += " " + ForestBackInspectionCue;

        return new PhotoWorldHintProjection(
            key,
            ResolveText($"urman.fullgame:text/pw-hint-forest-{key}-question"),
            ResolveText($"urman.fullgame:text/pw-hint-forest-{key}-area"),
            action);
    }
}
