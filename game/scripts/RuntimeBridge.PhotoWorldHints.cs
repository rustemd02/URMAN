namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal const string ForestBackInspectionCue = "Выйдите из поворота и оглянитесь на три ствола.";
    internal sealed record PhotoWorldHintProjection(string Key, string Question, string Area, string Action);

    internal PhotoWorldHintProjection? CurrentPhotoWorldHint()
    {
        if (_loadingSlot || _loadPreparing || !_content.HasPhotoWorlds
            || SessionIdentity is not { } session)
            return null;

        var route = CurrentFullGamePhotoWorldRouteProjection();
        if (route is null || !ReferenceEquals(route.SessionIdentity, session)
            || !ReferenceEquals(session, SessionIdentity))
            return null;

        if (route.ActiveSceneId == "urman.fullgame:scene/pw-prologue")
        {
            var branch = CurrentW01BranchProjection();
            if (branch is not { ActiveSceneId: "urman.fullgame:scene/pw-prologue" }
                || !ReferenceEquals(branch.SessionIdentity, session))
                return null;

            var key = branch.Moved ? "prologue-exit" : "prologue-branch";
            var interactionId = branch.Moved
                ? "urman.fullgame:interaction/pw-prologue-f03-end"
                : "urman.fullgame:interaction/pw-prologue-branch-move";
            if (!IsInteractionAvailable(interactionId)
                || _loadingSlot || _loadPreparing || !ReferenceEquals(session, SessionIdentity))
                return null;

            return new PhotoWorldHintProjection(
                key,
                ResolveText($"urman.fullgame:text/pw-hint-{key}-question"),
                ResolveText($"urman.fullgame:text/pw-hint-{key}-area"),
                ResolveText($"urman.fullgame:text/pw-hint-{key}-action"));
        }

        if (!CanInspectReceivedPhotoBook) return null;

        if (route.ActiveSceneId == "urman.fullgame:scene/pw-forest")
        {
            var branch = CurrentW01BranchProjection();
            if (branch is not { ActiveSceneId: "urman.fullgame:scene/pw-forest" }
                || !ReferenceEquals(branch.SessionIdentity, session))
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

        (string Key, string TextKey, string InteractionId, string ActionTextId)? Next(
            string key, string textKey, string factId, string interactionId, string actionTextId) =>
            PhotoWorldFactObserved(factId)
                ? null
                : (key, textKey, interactionId, actionTextId);

        (string Key, string TextKey, string InteractionId, string ActionTextId)? cue = route.ActiveSceneId switch
        {
            "urman.fullgame:scene/pw-river" =>
                Next("river-reflection", "pw-hint-river-reflection", "water_reflection_relation_seen",
                    "urman.fullgame:interaction/pw-water-reflection", "urman.fullgame:text/pw-label-water-reflection")
                ?? Next("river-reed", "pw-hint-river-reed", "water_reed_opened",
                    "urman.fullgame:interaction/pw-water-bypass", "urman.fullgame:text/pw-label-water-bypass")
                ?? Next("river-approach", "pw-hint-river-approach", "water_old_approach_seen",
                    "urman.fullgame:interaction/pw-water-old-approach", "urman.fullgame:text/pw-label-water-old-approach")
                ?? Next("river-encounter", "pw-hint-river-encounter", "water_encounter_resolved",
                    "urman.fullgame:interaction/pw-water-encounter", "urman.fullgame:text/pw-label-water-encounter")
                ?? Next("river-damp-print", "pw-hint-river-damp-print", "water_damp_print",
                    "urman.fullgame:interaction/pw-water-damp-print", "urman.fullgame:text/pw-label-water-damp-print")
                ?? ("river-exit", "pw-hint-river-exit", "urman.fullgame:interaction/pw-water-return", "urman.fullgame:text/pw-label-water-return"),
            "urman.fullgame:scene/pw-house" =>
                Next("house-shutter", "pw-hint-house-shutter", "house_shutter_opened",
                    "urman.fullgame:interaction/pw-house-shutter", "urman.fullgame:text/pw-label-house-shutter")
                ?? Next("house-door-left", "pw-hint-house-doors", "house_door_left_seen",
                    "urman.fullgame:interaction/pw-house-door-left", "urman.fullgame:text/pw-label-house-door-left")
                ?? Next("house-door-right", "pw-hint-house-doors", "house_door_right_seen",
                    "urman.fullgame:interaction/pw-house-door-right", "urman.fullgame:text/pw-label-house-door-right")
                ?? Next("house-two-views", "pw-hint-house-doors", "house_two_views_seen",
                    "urman.fullgame:interaction/pw-house-two-views", "urman.fullgame:text/pw-label-house-two-views")
                ?? Next("house-camera", "pw-hint-house-camera", "house_camera_position_seen",
                    "urman.fullgame:interaction/pw-house-camera-position", "urman.fullgame:text/pw-label-house-camera-position")
                ?? Next("house-cloth", "pw-hint-house-cloth", "cloth_cleared",
                    "urman.fullgame:interaction/pw-house-cloth", "urman.fullgame:text/pw-label-house-cloth")
                ?? Next("house-frame", "pw-hint-house-frame", "house_frame_seen",
                    "urman.fullgame:interaction/pw-house-frame", "urman.fullgame:text/pw-label-house-frame")
                ?? ("house-exit", "pw-hint-house-exit", "urman.fullgame:interaction/pw-house-return", "urman.fullgame:text/pw-label-house-return"),
            "urman.fullgame:scene/pw-common" =>
                Next("common-places", "pw-hint-common-places", "common_three_relations_seen",
                    "urman.fullgame:interaction/pw-common-three-places", "urman.fullgame:text/pw-label-common-three-places")
                ?? Next("common-cloth", "pw-hint-common-cloth", "common_view_uncovered",
                    "urman.fullgame:interaction/pw-common-cloth", "urman.fullgame:text/pw-label-common-cloth")
                ?? Next("common-horizon", "pw-hint-common-horizon", "common_horizon_seen",
                    "urman.fullgame:interaction/pw-common-horizon", "urman.fullgame:text/pw-label-common-horizon")
                ?? Next("common-note", "pw-hint-common-note", "common_note_finished",
                    "urman.fullgame:interaction/pw-common-note", "urman.fullgame:text/pw-label-common-note")
                ?? ("common-exit", "pw-hint-common-exit", "urman.fullgame:interaction/pw-common-return", "urman.fullgame:text/pw-label-common-return"),
            "urman.fullgame:scene/pw-cellar" =>
                Next("cellar-order", "pw-hint-cellar-order", "cellar_household_pattern_seen",
                    "urman.fullgame:interaction/pw-cellar-order", "urman.fullgame:text/pw-label-cellar-order")
                ?? Next("cellar-object", "pw-hint-cellar-object", "cellar_box_returned",
                    "urman.fullgame:interaction/pw-cellar-object", "urman.fullgame:text/pw-label-cellar-object")
                ?? Next("cellar-response", "pw-hint-cellar-response", "cellar_response_seen",
                    "urman.fullgame:interaction/pw-cellar-response", "urman.fullgame:text/pw-label-cellar-response")
                ?? Next("cellar-niche", "pw-hint-cellar-niche", "cellar_niche_seen",
                    "urman.fullgame:interaction/pw-cellar-niche", "urman.fullgame:text/pw-label-cellar-niche")
                ?? ("cellar-exit", "pw-hint-cellar-exit", "urman.fullgame:interaction/pw-cellar-return", "urman.fullgame:text/pw-label-cellar-return"),
            _ => null
        };

        if (cue is not { } selected || !IsInteractionAvailable(selected.InteractionId)
            || _loadingSlot || _loadPreparing || !ReferenceEquals(session, SessionIdentity))
            return null;

        return new PhotoWorldHintProjection(
            selected.Key,
            ResolveText($"urman.fullgame:text/{selected.TextKey}-question"),
            ResolveText($"urman.fullgame:text/{selected.TextKey}-area"),
            ResolveText(selected.ActionTextId));
    }
}
