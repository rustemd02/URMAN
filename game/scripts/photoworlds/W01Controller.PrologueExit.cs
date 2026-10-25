using System;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private object? _prologueExitSession;
    private bool _prologueExitAttempted;
    private bool _prologueExitDispatching;
    private bool _prologueExitWakePlayed;

    private void ResetPrologueExitTracking(object? session)
    {
        _prologueExitSession = session;
        _prologueExitAttempted = false;
        _prologueExitDispatching = false;
        _prologueExitWakePlayed = false;
    }

    private void ProcessPrologueExit()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: PrologueSceneId, Moved: true } projection
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            ResetPrologueExitTracking(null);
            return;
        }
        if (!ReferenceEquals(_prologueExitSession, session)) ResetPrologueExitTracking(session);
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)) return;

        // The final authored path opens into a clearing wider than the packed core.
        // Arrival follows that path, rather than requiring aim at an invisible ray box.
        var local = ToLocal(player.GlobalPosition);
        var direction = new Vector2(PrologueF03.X - PrologueF02.X, PrologueF03.Z - PrologueF02.Z).Normalized();
        var offset = new Vector2(local.X - PrologueF03.X, local.Z - PrologueF03.Z);
        var along = offset.Dot(direction);
        var lateral = Mathf.Abs(offset.Cross(direction));
        if (!_prologueExitDispatching && (along < -5f || along > 8f || lateral > 5f))
            _prologueExitAttempted = false;

        if (GetTree().Paused || !DisplayServer.WindowIsFocused()
            || MainMenuUi.AnyUndismissed(GetTree()) || player.ModalOpen || player.SessionTransitionActive
            || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor()
            || Mathf.Abs(local.Y - ForestGroundHeight(local.X, local.Z)) > .5f
            || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }) return;

        if (!_prologueExitWakePlayed && along >= -12f && along < -2.5f && lateral <= 4f)
        {
            _prologueExitWakePlayed = true;
            PrologueVoice.PlayAt(this, ToGlobal(PrologueF03 + Vector3.Up * 1.6f), "forest-babai-name-v2", -5f, 22f);
        }
        if (_prologueExitAttempted || _prologueExitDispatching || lateral > 4f || along < -2.5f || along > 6f)
            return;

        _prologueExitAttempted = true;
        _prologueExitDispatching = true;
        _ = DispatchPrologueExitAsync(bridge, session);
    }

    private async Task DispatchPrologueExitAsync(RuntimeBridge bridge, object session)
    {
        try
        {
            await bridge.DispatchInteractionAsync(PrologueEndInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W01 F03 prologue exit dispatch failed: {error.Message}");
        }
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || !ReferenceEquals(_prologueExitSession, session)) return;
        if (!GodotObject.IsInstanceValid(bridge) || !ReferenceEquals(bridge.SessionIdentity, session)
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: PrologueSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            ResetPrologueExitTracking(null);
            return;
        }
        // A rejected attempt stays latched until the player leaves and re-enters.
        _prologueExitDispatching = false;
    }
}
