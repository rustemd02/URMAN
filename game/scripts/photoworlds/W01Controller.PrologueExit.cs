using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private Node3D? _prologueTracks;
    private Label? _prologueVoiceCaption;
    private object? _prologueExitSession;
    private bool _prologueExitAttempted;
    private bool _prologueExitDispatching;
    private bool _prologueFirstCall;
    private bool _prologueMittenCue;
    private bool _prologueAnswer;
    private bool _prologueWakeCue;
    private double _prologueCaptionRemaining;
    private double _prologueWakeRemaining;
    private static Vector2 PrologueMittenPoint => new(BranchF01.X + 3.5f, BranchF01.Z + 6f);

    private void BuildPrologueTracks()
    {
        var trail = new Node3D { Name = "NightmareFootprints", Visible = false };
        _prologueTracks = trail;
        AddChild(trail);
        var rims = new List<Transform3D>();
        var soles = new List<Transform3D>();
        var heels = new List<Transform3D>();
        var left = true;
        for (var i = 1; i < PrologueRouteNodes.Length; i++)
        {
            var start = PrologueRouteNodes[i - 1];
            var end = PrologueRouteNodes[i];
            var direction = new Vector3(end.X - start.X, 0, end.Z - start.Z).Normalized();
            var side = direction.Cross(Vector3.Up);
            var length = new Vector2(end.X - start.X, end.Z - start.Z).Length();
            var lastPrint = i == PrologueRouteNodes.Length - 1 ? length - 4f : length;
            for (var along = i == 1 ? 4f : 1f; along < lastPrint; along += .76f + .11f * Mathf.Sin(along * .77f))
            {
                var point = start + direction * along + side * ((left ? -.11f : .11f) + .24f * Mathf.Sin(along * .17f));
                point.Y = ForestGroundHeight(point.X, point.Z);
                var basis = new Basis(Vector3.Up, Mathf.Atan2(-direction.X, -direction.Z) + (left ? .11f : -.11f));
                rims.Add(new Transform3D(basis * Basis.FromScale(new Vector3(.72f, 1f, 1.55f)), point + Vector3.Up * .014f));
                soles.Add(new Transform3D(basis * Basis.FromScale(new Vector3(.85f, 1f, 1.4f)), point + basis * new Vector3(0, .018f, -.035f)));
                heels.Add(new Transform3D(basis, point + basis * new Vector3(0, .019f, .075f)));
                left = !left;
            }
        }

        PrologueDeepForest.AddPrintLayer(trail, "SnowRims", rims,
            new CylinderMesh { TopRadius = .085f, BottomRadius = .1f, Height = .005f, RadialSegments = 12 }, "a9b6c5");
        PrologueDeepForest.AddPrintLayer(trail, "Soles", soles,
            new CylinderMesh { TopRadius = .064f, BottomRadius = .069f, Height = .004f, RadialSegments = 12 }, "8999ac");
        PrologueDeepForest.AddPrintLayer(trail, "Heels", heels,
            new CylinderMesh { TopRadius = .045f, BottomRadius = .05f, Height = .004f, RadialSegments = 10 }, "7c8ca1");

        PrologueDeepForest.AddMitten(trail, new Vector3(PrologueMittenPoint.X,
            ForestGroundHeight(PrologueMittenPoint.X, PrologueMittenPoint.Y) + .046f, PrologueMittenPoint.Y),
            new Vector3(84f, -22f, 12f));
    }

    private void BuildPrologueVoiceCaption(Node layer)
    {
        _prologueVoiceCaption = new Label
        {
            Name = "ForestVoiceCaption",
            ThemeTypeVariation = UrmanUiTheme.Subtitle,
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -290f,
            OffsetRight = 290f,
            OffsetTop = -160f,
            OffsetBottom = -92f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _prologueVoiceCaption.AddThemeConstantOverride("outline_size", 6);
        layer.AddChild(_prologueVoiceCaption);
    }

    private void ResetPrologueExitTracking(object? session)
    {
        if (_prologueExitSession is not null) PrologueVoice.StopSpatial(this);
        _prologueExitSession = session;
        _prologueExitAttempted = false;
        _prologueExitDispatching = false;
        _prologueFirstCall = false;
        _prologueMittenCue = false;
        _prologueAnswer = false;
        _prologueWakeCue = false;
        _prologueCaptionRemaining = 0;
        _prologueWakeRemaining = 0;
        if (_prologueVoiceCaption is { } caption) caption.Hide();
    }

    private void ProcessPrologueExit()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: PrologueSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            if (_prologueTracks is { } tracks) tracks.Hide();
            ResetPrologueExitTracking(null);
            return;
        }
        if (_prologueTracks is { } activeTracks) activeTracks.Show();
        if (!ReferenceEquals(_prologueExitSession, session)) ResetPrologueExitTracking(session);
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)) return;

        var local = ToLocal(player.GlobalPosition);
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
            || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true })
        {
            if (_prologueVoiceCaption is { } hidden) hidden.Hide();
            return;
        }

        var delta = GetProcessDeltaTime();
        _prologueCaptionRemaining = Math.Max(0, _prologueCaptionRemaining - delta);
        _prologueWakeRemaining = Math.Max(0, _prologueWakeRemaining - delta);
        if (_prologueVoiceCaption is { } caption)
            caption.Visible = _accessibility.Subtitles && _prologueCaptionRemaining > 0;

        var onTrailGround = player.IsOnFloor() && Mathf.Abs(local.Y - ForestGroundHeight(local.X, local.Z)) <= .5f;
        if (!onTrailGround) return;
        var feet = new Vector2(local.X, local.Z);
        if (!projection.Moved)
        {
            var approach = new Vector2(BranchF01.X, BranchF01.Z).Normalized();
            if (!_prologueFirstCall && feet.Dot(approach) is >= 8f and <= 36f && Mathf.Abs(feet.Cross(approach)) <= 5f)
            {
                _prologueFirstCall = true;
                var inTrees = feet + approach * 17f + new Vector2(-approach.Y, approach.X) * 8f;
                PlayPrologueForestVoice(inTrees, "forest-name-call", "«Айдар…»", -4f);
            }
            return;
        }

        if (!_prologueMittenCue && feet.DistanceTo(PrologueMittenPoint) < 6f)
        {
            _prologueMittenCue = true;
            var source = ToGlobal(new Vector3(PrologueMittenPoint.X,
                ForestGroundHeight(PrologueMittenPoint.X, PrologueMittenPoint.Y) + 3.4f, PrologueMittenPoint.Y));
            UiFoley.PlayWorld(this, source, "forest/branch_crack", -12f, 20f, 4f);
        }

        if (!_prologueAnswer && along >= -55f && along < -17f && lateral <= 8f)
        {
            _prologueAnswer = true;
            var behind = feet - direction * 9f + new Vector2(direction.Y, -direction.X) * 7f;
            PlayPrologueForestVoice(behind, "forest-come-here", "«Айдар. Кил монда…»\n(Айдар, иди сюда…)", -2f);
        }

        if (_prologueExitAttempted || _prologueExitDispatching || lateral > 4f || along < -2.5f || along > 7f)
            return;

        if (!_prologueWakeCue)
        {
            // The prints stop here. The last voice is right beside Aidar, before the car reveals its source.
            _prologueWakeCue = true;
            PrologueVoice.StopSpatial(this);
            var beside = feet + new Vector2(direction.Y, -direction.X) * 1.1f;
            _prologueWakeRemaining = Math.Clamp(PlayPrologueForestVoice(beside, "forest-babai-name-v2", "«Айдар… Айдар…»", -3f) * .65, 1.3, 2.4);
        }
        if (_prologueWakeRemaining > 0) return;

        _prologueExitAttempted = true;
        _prologueExitDispatching = true;
        _ = DispatchPrologueExitAsync(bridge, session);
    }

    private double PlayPrologueForestVoice(Vector2 point, string clip, string subtitle, float volume)
    {
        var source = ToGlobal(new Vector3(point.X, ForestGroundHeight(point.X, point.Y) + 1.4f, point.Y));
        var duration = PrologueVoice.PlayAt(this, source, clip, volume, 36f);
        if (_prologueVoiceCaption is { } caption)
        {
            caption.Text = subtitle;
            _prologueCaptionRemaining = Math.Max(2.4, duration);
            caption.Visible = _accessibility.Subtitles;
        }
        return duration;
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
