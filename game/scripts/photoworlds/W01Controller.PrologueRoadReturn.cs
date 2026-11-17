using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private const float RoadChoiceAt = 34f;
    private const float RoadReturnAt = 44f;
    private readonly List<InteractionTarget> _roadReturnTargets = [];
    private readonly bool[] _roadChoiceNoticed = new bool[2];
    private CanvasLayer? _roadReturnLayer;
    private ColorRect? _roadReturnShade;
    private AudioStreamPlayer? _roadReturnSteps;
    private FirstPersonController? _roadReturnPlayer;
    private object? _roadReturnSession;
    private bool _roadReturning;
    private bool _roadReturnPlaced;
    private double _roadReturnElapsed;

    private void BuildPrologueRoadReturn(Node3D owner)
    {
        var (along, across) = PrologueStopAxes();
        var basis = new Basis(new Vector3(along.X, 0f, along.Y), Vector3.Up,
            new Vector3(across.X, 0f, across.Y));
        foreach (var side in new[] { -1f, 1f })
        {
            var distance = side * RoadChoiceAt;
            var centre = (StopRoadNear + StopRoadFar) * .5f + PrologueRoadBend(distance);
            var at = StopPoint(centre, distance);
            // A roadside reflector marks the bend; the whole road-width ray
            // target offers the return without asking the player to aim at it.
            var postAt = StopPoint(StopRoadNear - .65f + PrologueRoadBend(distance), distance);
            var post = RuralPropGeometry.Block(owner, "BendReflector" + side,
                new Vector3(.10f, .90f, .12f), postAt + Vector3.Up * .45f,
                RuralPropMaterials.Surface("wood", "bfc4c7"));
            post.Basis = basis;
            RuralPropGeometry.Block(owner, "BendReflectorBand" + side,
                new Vector3(.115f, .15f, .135f), postAt + Vector3.Up * .70f,
                RuralPropMaterials.Surface("metal", "706453"));
            var target = new InteractionTarget
            {
                Name = side < 0 ? "RoadReturnWest" : "RoadReturnEast",
                InteractionId = "urman.fullgame:local/prologue/road-return",
                Prompt = "Вернуться к остановке, к свету сторожки",
                Transform = new Transform3D(basis, at + Vector3.Up * 1.30f),
                CollisionLayer = 4u, CollisionMask = 0u
            };
            target.ConfigureRayOnly();
            target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(8f, 2.5f, .20f) } });
            target.PresentationRepeatAvailable = () => RoadReturnContext(out _, out _);
            target.PresentationRepeat = () => BeginPrologueRoadReturn();
            // Outside the thicket's bodies, whose visibility switch owns layer 1.
            // _Ready records the authored ray layer; hiding it beforehand would
            // permanently record zero and make the return prompt impossible.
            target.Ready += () => target.SetPresentationEnabled(false);
            AddChild(target);
            _roadReturnTargets.Add(target);
        }
    }

    private bool RoadReturnContext(out RuntimeBridge bridge, out FirstPersonController player)
    {
        bridge = null!; player = null!;
        if (!IsInsideTree() || IsQueuedForDeletion() || _roadReturning || _nightmareClimax
            || !_nightmareMoodApplied || _prologueHutExtinguished
            || _bridge is not { } current || !GodotObject.IsInstanceValid(current)
            || current.SessionIdentity is not { } session
            || current.CurrentW01BranchProjection() is not { ActiveSceneId: PrologueSceneId } route
            || !ReferenceEquals(route.SessionIdentity, session) || ProloguePresentationPaused()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController candidate
            || candidate.ModalOpen || candidate.VehicleControlled || candidate.IsClimbingLadder || !candidate.IsOnFloor())
            return false;
        var local = ToLocal(candidate.GlobalPosition);
        var (along, across) = PrologueStopAxes();
        var offset = new Vector2(local.X - PrologueTrailNodes[0].X, local.Z - PrologueTrailNodes[0].Z);
        var v = offset.Dot(across);
        var u = offset.Dot(along);
        var centre = (StopRoadNear + StopRoadFar) * .5f + PrologueRoadBend(v);
        // Only the actual 6 m carriageway and its immediate shoulders. The hut
        // lane and the optional older forest trail never enter this band.
        if (Mathf.Abs(v) < RoadChoiceAt - 4f || Mathf.Abs(u - centre) > 4.2f
            || Mathf.Abs(local.Y - ForestGroundHeight(local.X, local.Z)) > .5f)
            return false;
        bridge = current; player = candidate;
        return true;
    }

    private void ProcessPrologueRoadChoice(FirstPersonController player, Vector2 feet)
    {
        var available = RoadReturnContext(out _, out _);
        foreach (var target in _roadReturnTargets) target.SetPresentationEnabled(available);
        if (!available) return;
        var (_, across) = PrologueStopAxes();
        var distance = (feet - new Vector2(PrologueTrailNodes[0].X, PrologueTrailNodes[0].Z)).Dot(across);
        var side = distance < 0f ? 0 : 1;
        if (!_roadChoiceNoticed[side] && Math.Abs(distance) >= RoadChoiceAt - 2f)
        {
            _roadChoiceNoticed[side] = true;
            ShowNightmareThought("На дороге никого. Бабай велел ждать у света. Вернусь к остановке.", 4.5);
        }
        // Before either terrain edge or the enclosing slope. This is a named,
        // anticipated return walk, never an invisible wall or a fall recovery.
        if (Math.Abs(distance) >= RoadReturnAt) BeginPrologueRoadReturn();
    }

    private bool TryRoadReturnFeet(FirstPersonController player, out Vector3 feet)
    {
        feet = ToGlobal(StopPoint(.5f, 1.2f));
        var exclusions = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var exclusionsOwner = (global::Godot.Collections.Array)exclusions;
        using var query = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * 2f,
            feet - Vector3.Up, player.CollisionMask, exclusions);
        using var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() != _groundBody
            || hit["normal"].AsVector3().Dot(Vector3.Up) < .7f) return false;
        feet = hit["position"].AsVector3() + Vector3.Up * .025f;
        return player.CanStandAt(feet);
    }

    private void BeginPrologueRoadReturn()
    {
        if (!RoadReturnContext(out var bridge, out var player) || !TryRoadReturnFeet(player, out _)) return;
        _roadReturnSession = bridge.SessionIdentity;
        _roadReturnPlayer = player;
        _roadReturning = true;
        _roadReturnPlaced = false;
        _roadReturnElapsed = 0;
        foreach (var target in _roadReturnTargets) target.SetPresentationEnabled(false);
        _roadReturnLayer ??= new CanvasLayer { Name = "RoadReturnWalk", Layer = 119 };
        if (_roadReturnLayer.GetParent() is null)
        {
            AddChild(_roadReturnLayer);
            _roadReturnShade = new ColorRect { Color = Colors.Black with { A = 0f }, MouseFilter = Control.MouseFilterEnum.Ignore };
            _roadReturnLayer.AddChild(_roadReturnShade);
            _roadReturnShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        _roadReturnLayer.Visible = true;
        player.SetSessionTransition(true);
        if (DisplayServer.GetName() != "headless" && ResourceLoader.Exists(NightmareStepsStream))
        {
            _roadReturnSteps ??= UiFoley.Attach(this);
            _roadReturnSteps.Stream = ResourceLoader.Load<AudioStream>(NightmareStepsStream);
            _roadReturnSteps.VolumeDb = -15f;
            _roadReturnSteps.Play();
        }
    }

    private void ProcessPrologueRoadReturn(double delta)
    {
        if (!_roadReturning) return;
        var bridge = _bridge;
        var player = _roadReturnPlayer;
        if (bridge is null || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(_roadReturnSession, bridge.SessionIdentity) || bridge.ActiveSceneId != PrologueSceneId
            || player is null || !GodotObject.IsInstanceValid(player)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player)
            || ProloguePresentationPaused())
        { ResetPrologueRoadReturn(); return; }
        _roadReturnElapsed += Math.Clamp(delta, 0d, .1d);
        if (_roadReturnShade is { } shade)
            shade.Color = Colors.Black with { A = (float)(_roadReturnElapsed < .45
                ? _roadReturnElapsed / .45 : 1d - Math.Clamp((_roadReturnElapsed - .90) / .55, 0d, 1d)) };
        if (!_roadReturnPlaced && _roadReturnElapsed >= .45)
        {
            if (!TryRoadReturnFeet(player, out var feet)) { ResetPrologueRoadReturn(); return; }
            var toward = ToGlobal(PrologueHutPosition) - feet;
            player.ApplyZoneSpawn(feet, Mathf.RadToDeg(Mathf.Atan2(-toward.X, -toward.Z)));
            _roadReturnPlaced = true;
        }
        if (_roadReturnElapsed >= 1.45) ResetPrologueRoadReturn();
    }

    private void ResetPrologueRoadReturn()
    {
        if (_roadReturning && _roadReturnPlayer is { } player && GodotObject.IsInstanceValid(player)
            && _bridge is { } bridge && GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(_roadReturnSession, bridge.SessionIdentity)) player.SetSessionTransition(false);
        _roadReturning = false;
        _roadReturnPlayer = null;
        _roadReturnSession = null;
        if (_roadReturnLayer is { } layer) layer.Visible = false;
        _roadReturnSteps?.Stop();
        Array.Clear(_roadChoiceNoticed);
        foreach (var target in _roadReturnTargets)
            if (GodotObject.IsInstanceValid(target)) target.SetPresentationEnabled(false);
    }
}
