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
        // One walker's prints by arc length: a real stride, wandering a little.
        // They begin where the road ends, small and close together: a child's.
        // By the end the same walker's stride is longer than a man's.
        var grown = .6f;
        for (var along = StopPrintsFrom; along < PrologueRouteLength - 4f; along += (.7f + .09f * Mathf.Sin(along * .77f)) * Mathf.Lerp(.62f, 1.3f, (grown - .6f) / .85f))
        {
            grown = Mathf.Lerp(.6f, 1.45f, Mathf.SmoothStep(.12f, .93f, along / PrologueRouteLength));
            var here = PrologueRoutePoint(along, 0f);
            var next = PrologueRoutePoint(along + .5f, 0f);
            var direction = new Vector3(next.X - here.X, 0, next.Z - here.Z).Normalized();
            if (direction == Vector3.Zero) continue;
            var side = direction.Cross(Vector3.Up);
            var point = here + side * ((left ? -.12f : .12f) * grown + .1f * Mathf.Sin(along * .31f));
            point.Y = ForestGroundHeight(point.X, point.Z);
            var basis = new Basis(Vector3.Up, Mathf.Atan2(-direction.X, -direction.Z) + (left ? .11f : -.11f));
            rims.Add(new Transform3D(basis * Basis.FromScale(new Vector3(.72f * grown, 1f, 1.55f * grown)), point + Vector3.Up * .024f));
            soles.Add(new Transform3D(basis * Basis.FromScale(new Vector3(.85f * grown, 1f, 1.4f * grown)), point + basis * new Vector3(0, .028f, -.035f * grown)));
            heels.Add(new Transform3D(basis * Basis.FromScale(new Vector3(grown, 1f, grown)), point + basis * new Vector3(0, .029f, .075f * grown)));
            left = !left;
        }
        BuildTroddenTrail(trail);

        // With a drawn boot print in the texture slot, each step is that print
        // lying on the snow; without it, the two simple pressed shapes below.
        if (PrologueTextureSlot("footprint_decal", null) is { } printTexture)
        {
            var decal = new QuadMesh { Size = new Vector2(.15f, .34f), Orientation = PlaneMesh.OrientationEnum.Y };
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = decal, InstanceCount = heels.Count };
            for (var index = 0; index < heels.Count; index++)
                multi.SetInstanceTransform(index, new Transform3D(heels[index].Basis, heels[index].Origin + heels[index].Basis * new Vector3(0, .004f, -.06f)));
            trail.AddChild(new MultiMeshInstance3D
            {
                Name = "PrintDecals", Multimesh = multi,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoTexture = printTexture, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    Roughness = 1f, MetallicSpecular = 0f
                }
            });
            return;
        }
        PrologueDeepForest.AddPrintLayer(trail, "Soles", soles,
            new CylinderMesh { TopRadius = .064f, BottomRadius = .069f, Height = .004f, RadialSegments = 12 }, "8999ac");
        PrologueDeepForest.AddPrintLayer(trail, "Heels", heels,
            new CylinderMesh { TopRadius = .045f, BottomRadius = .05f, Height = .004f, RadialSegments = 10 }, "7c8ca1");


    }

    /// <summary>
    /// The path itself: not a made road, just snow that many feet have pressed
    /// down. A ragged, slightly darker ribbon lying on the ground, uneven in
    /// width, with soft broken edges.
    /// </summary>
    private void BuildTroddenTrail(Node3D owner)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var rng = new RandomNumberGenerator { Seed = 0x54524c31 };
        const float step = .45f;
        Vector3 previousLeft = default, previousRight = default, previousCentre = default;
        var previousShade = 0f;
        var first = true;
        for (var along = StopPrintsFrom - .6f; along < PrologueRouteLength - 2f; along += step)
        {
            var here = PrologueRoutePoint(along, 0f);
            var next = PrologueRoutePoint(along + .5f, 0f);
            var direction = new Vector3(next.X - here.X, 0, next.Z - here.Z).Normalized();
            if (direction == Vector3.Zero) continue;
            var side = direction.Cross(Vector3.Up);
            var half = .34f + .1f * Mathf.Sin(along * .9f) + rng.RandfRange(-.07f, .07f);
            var drift = .08f * Mathf.Sin(along * .43f);
            Vector3 OnGround(Vector3 point) => new(point.X, ForestGroundHeight(point.X, point.Z) + .016f, point.Z);
            var centre = OnGround(here + side * drift);
            var leftEdge = OnGround(here + side * (drift - half - rng.RandfRange(.05f, .22f)));
            var rightEdge = OnGround(here + side * (drift + half + rng.RandfRange(.05f, .22f)));
            var shade = rng.RandfRange(.75f, 1f);
            if (!first)
            {
                void Vertex(Vector3 point, float alpha, float tone)
                {
                    surface.SetNormal(Vector3.Up);
                    surface.SetColor(new Color(.62f * tone, .68f * tone, .78f * tone, alpha * .4f));
                    surface.AddVertex(point);
                }
                Vertex(previousLeft, 0f, previousShade); Vertex(previousCentre, .8f, previousShade); Vertex(leftEdge, 0f, shade);
                Vertex(previousCentre, .8f, previousShade); Vertex(centre, .8f, shade); Vertex(leftEdge, 0f, shade);
                Vertex(previousCentre, .8f, previousShade); Vertex(previousRight, 0f, previousShade); Vertex(centre, .8f, shade);
                Vertex(previousRight, 0f, previousShade); Vertex(rightEdge, 0f, shade); Vertex(centre, .8f, shade);
            }
            first = false;
            previousLeft = leftEdge; previousRight = rightEdge; previousCentre = centre; previousShade = shade;
        }
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 1f,
            MetallicSpecular = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        owner.AddChild(new MeshInstance3D
        {
            Name = "TroddenTrail",
            Mesh = surface.Commit(),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
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
        DevForestCaptureTick(player, GetProcessDeltaTime());
        if (_nightmareClimax && !ProloguePresentationPaused())
        {
            // The walker is held for the finale (which reads as a modal below), but
            // the torch still has to die, the eyes to open and the captions to run.
            var step = GetProcessDeltaTime();
            _prologueCaptionRemaining = Math.Max(0, _prologueCaptionRemaining - step);
            if (_prologueVoiceCaption is { } line)
                line.Visible = (_accessibility.Subtitles || _prologueCaptionThought) && _prologueCaptionRemaining > 0;
            TickDreadPresentation(player, 1f, _nightmareElapsed, (float)step);
            return;
        }
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
