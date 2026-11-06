using System;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private const double PrologueCrossingSeconds = 2.4;
    private bool _prologueSideCrackPlayed;
    private bool _prologueCrossingPlayed;
    private AudioStreamPlayer3D? _prologueCrossingSound;
    private ProloguePresence? _prologueCrossingPresence;
    private Vector2 _prologueCrossingFrom;
    private Vector2 _prologueCrossingTo;
    private double _prologueCrossingElapsed;
    private object? _prologueExitSession;
    private bool _prologueExitAttempted;
    private bool _prologueExitDispatching;
    private bool _prologueExitKnocksPlayed;
    private double _prologueExitSecondKnockDelay;
    private bool _prologueExitWakePlayed;

    private void ResetPrologueExitTracking(object? session)
    {
        if (GodotObject.IsInstanceValid(_prologueCrossingSound)) _prologueCrossingSound!.QueueFree();
        if (GodotObject.IsInstanceValid(_prologueCrossingPresence)) _prologueCrossingPresence!.QueueFree();
        _prologueCrossingSound = null;
        _prologueCrossingPresence = null;
        _prologueSideCrackPlayed = false;
        _prologueCrossingPlayed = false;
        _prologueCrossingElapsed = 0;
        _prologueExitSession = session;
        _prologueExitAttempted = false;
        _prologueExitDispatching = false;
        _prologueExitKnocksPlayed = false;
        _prologueExitSecondKnockDelay = 0;
        _prologueExitWakePlayed = false;
    }

    private void ProcessPrologueExit()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: PrologueSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            ResetPrologueExitTracking(null);
            return;
        }
        if (!ReferenceEquals(_prologueExitSession, session)) ResetPrologueExitTracking(session);
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)) return;

        var local = ToLocal(player.GlobalPosition);
        // The final path opens into a clearing wider than the packed core.
        var direction = new Vector2(PrologueF03.X - PrologueF02.X, PrologueF03.Z - PrologueF02.Z).Normalized();
        var offset = new Vector2(local.X - PrologueF03.X, local.Z - PrologueF03.Z);
        var along = offset.Dot(direction);
        var lateral = Mathf.Abs(offset.Cross(direction));
        if (!_prologueExitDispatching && (along < -5f || along > 8f || lateral > 5f))
            _prologueExitAttempted = false;

        if (GetTree().Paused || !DisplayServer.WindowIsFocused()
            || MainMenuUi.AnyUndismissed(GetTree()) || player.ModalOpen || player.SessionTransitionActive
            || player.VehicleControlled || player.IsClimbingLadder
            || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }) return;

        var onTrailGround = player.IsOnFloor() && Mathf.Abs(local.Y - ForestGroundHeight(local.X, local.Z)) <= .5f;
        ProcessPrologueForestBeats(player, new Vector2(local.X, local.Z), projection.Moved, onTrailGround);
        if (!projection.Moved || !onTrailGround) return;

        // A quiet, spatial pair points toward the opening before the familiar voice reaches it.
        if (!_prologueExitKnocksPlayed && along >= -36f && along < -24f && lateral <= 5f)
        {
            _prologueExitKnocksPlayed = true;
            _prologueExitSecondKnockDelay = .48;
            PlayPrologueExitKnock();
        }
        else if (_prologueExitSecondKnockDelay > 0)
        {
            _prologueExitSecondKnockDelay -= GetProcessDeltaTime();
            if (_prologueExitSecondKnockDelay <= 0)
            {
                _prologueExitSecondKnockDelay = 0;
                PlayPrologueExitKnock();
            }
        }

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

    private void ProcessPrologueForestBeats(FirstPersonController player, Vector2 feet, bool branchMoved, bool onTrailGround)
    {
        if (!branchMoved)
        {
            var approach = new Vector2(BranchF01.X, BranchF01.Z).Normalized();
            if (onTrailGround && !_prologueSideCrackPlayed && feet.Dot(approach) is >= 14f and <= 28f
                && Mathf.Abs(feet.Cross(approach)) <= 5f)
            {
                _prologueSideCrackPlayed = true;
                var offTrail = approach * 25f + new Vector2(-approach.Y, approach.X) * 11f;
                UiFoley.PlayWorld(this, PrologueForestPoint(offTrail, 1.1f), "forest/branch_crack", -10f, 32f, 5f);
            }
            return;
        }

        if (!_prologueCrossingPlayed)
        {
            if (!onTrailGround) return;
            var bend = new Vector2(PrologueF02.X, PrologueF02.Z);
            var approach = (bend - new Vector2(BranchF01.X, BranchF01.Z)).Normalized();
            var offset = feet - bend;
            if (offset.Dot(approach) is < -17f or > -8f || Mathf.Abs(offset.Cross(approach)) > 5f) return;

            _prologueCrossingPlayed = true;
            var side = new Vector2(-approach.Y, approach.X);
            var crossing = bend + approach * 4f;
            _prologueCrossingFrom = crossing - side * 8f;
            _prologueCrossingTo = crossing + side * 8f;
            _prologueCrossingSound = UiFoley.PlayWorld(this, PrologueForestPoint(_prologueCrossingFrom, .6f),
                "forest/runner_past", -9f, 40f, 6f);
            if (!player.ReducedMotion)
            {
                var presence = new ProloguePresence { Name = "ProloguePeripheralCrossing" };
                AddChild(presence);
                presence.GlobalPosition = PrologueForestPoint(_prologueCrossingFrom, .65f);
                presence.LookAt(player.GlobalPosition + Vector3.Up);
                presence.SetVeil(0f, 0f);
                _prologueCrossingPresence = presence;
            }
        }

        if (_prologueCrossingElapsed >= PrologueCrossingSeconds) return;
        _prologueCrossingElapsed = Math.Min(PrologueCrossingSeconds, _prologueCrossingElapsed + GetProcessDeltaTime());
        var progress = (float)(_prologueCrossingElapsed / PrologueCrossingSeconds);
        var point = _prologueCrossingFrom.Lerp(_prologueCrossingTo, progress);
        if (GodotObject.IsInstanceValid(_prologueCrossingSound))
            _prologueCrossingSound!.GlobalPosition = PrologueForestPoint(point, .6f);
        if (_prologueCrossingPresence is not { } visible || !GodotObject.IsInstanceValid(visible)) return;
        if (progress >= 1f || player.ReducedMotion)
        {
            visible.QueueFree();
            _prologueCrossingPresence = null;
            return;
        }
        visible.GlobalPosition = PrologueForestPoint(point, .65f);
        var glimpse = Mathf.Clamp((progress - .26f) / .48f, 0f, 1f);
        visible.SetVeil(Mathf.Sin(glimpse * Mathf.Pi) * .44f, (float)_prologueCrossingElapsed);
    }

    private Vector3 PrologueForestPoint(Vector2 point, float height) =>
        ToGlobal(new Vector3(point.X, ForestGroundHeight(point.X, point.Y) + height, point.Y));

    private void PlayPrologueExitKnock() =>
        UiFoley.PlayWorld(this, ToGlobal(PrologueF03 + Vector3.Up * 1.6f), "wood_tap", -16f, 60f, 14f);

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
