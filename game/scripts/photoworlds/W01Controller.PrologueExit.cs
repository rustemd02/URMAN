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
    private bool _prologueEndNoticed;
    private bool _prologueExitDispatching;
    private bool _prologueMittenCue;
    private double _prologueCaptionRemaining;
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
        if (!ReferenceEquals(_nightmareSession, session)) ResetNightmare(session);
        _prologueExitSession = session;
        _prologueEndNoticed = false;
        _prologueExitDispatching = false;
        _prologueMittenCue = false;
        _prologueCaptionRemaining = 0;
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
        if (ProloguePresentationPaused() || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder)
        {
            StopParallelSteps();
            if (_prologueVoiceCaption is { } hidden) hidden.Hide();
            return;
        }

        var delta = GetProcessDeltaTime();
        _prologueCaptionRemaining = Math.Max(0, _prologueCaptionRemaining - delta);
        if (_prologueVoiceCaption is { } caption)
            caption.Visible = (_accessibility.Subtitles || _prologueCaptionThought) && _prologueCaptionRemaining > 0;
        if (_nightmareClimax)
        {
            // The walk is parked, but the torch still has to die and the eyes to open.
            TickDreadPresentation(player, 1f, _nightmareElapsed, (float)delta);
            return;
        }

        var local = ToLocal(player.GlobalPosition);
        var feet = new Vector2(local.X, local.Z);
        ProcessPrologueNightmare(player, feet, projection.Moved, delta);
        if (!player.IsOnFloor() || Mathf.Abs(local.Y - ForestGroundHeight(local.X, local.Z)) > .5f || !projection.Moved)
            return;

        if (!_prologueMittenCue && feet.DistanceTo(PrologueMittenPoint) < 3f && _prologueCaptionRemaining <= 0)
        {
            _prologueMittenCue = true;
            ShowNightmareThought("Детская варежка…", 3);
        }
        var direction = new Vector2(PrologueF03.X - PrologueF02.X, PrologueF03.Z - PrologueF02.Z).Normalized();
        var offset = feet - new Vector2(PrologueF03.X, PrologueF03.Z);
        if (!_prologueEndNoticed && Mathf.Abs(offset.Cross(direction)) < 4f && offset.Dot(direction) is > -10f and < 3f)
        {
            _prologueEndNoticed = true;
        }
        // Reaching the end of the prints is enough: what waits there does not
        // need to be asked.
        if (Mathf.Abs(offset.Cross(direction)) < 5f && offset.Dot(direction) is > -4.5f and < 4f)
            RunNightmareClimaxAsync(bridge, session, player);
    }

    private bool BeginPrologueEndInspection()
    {
        if (_nightmareClimax || _prologueExitDispatching || ProloguePresentationPaused()
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: PrologueSceneId, Moved: true }
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor())
            return false;
        var local = ToLocal(player.GlobalPosition);
        var feet = new Vector2(local.X, local.Z);
        var direction = new Vector2(PrologueF03.X - PrologueF02.X, PrologueF03.Z - PrologueF02.Z).Normalized();
        var offset = feet - new Vector2(PrologueF03.X, PrologueF03.Z);
        if (Mathf.Abs(offset.Cross(direction)) > 4f || offset.Dot(direction) is < -8f or > 3f)
            return false;
        RunNightmareClimaxAsync(bridge, session, player);
        return false;
    }

    private double PlayPrologueForestVoice(Vector2 point, string clip, string subtitle, float volume)
    {
        var source = ToGlobal(new Vector3(point.X, ForestGroundHeight(point.X, point.Y) + 1.4f, point.Y));
        var duration = PrologueVoice.PlayAt(this, source, clip, volume, 36f);
        if (_prologueVoiceCaption is { } caption)
        {
            _prologueCaptionThought = false;
            caption.Text = subtitle;
            _prologueCaptionRemaining = Math.Max(2.4, duration);
            caption.Visible = _accessibility.Subtitles;
        }
        return duration;
    }
}
