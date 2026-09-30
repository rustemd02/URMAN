using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class Act1FirstPersonWalkthroughSmokeTest
{
    internal sealed record MosqueRouteResult(int VisitedPoints, float WalkedMetres, bool StableLanding);

    // The graph stores transient swept-capsule poses as well as flat landings.
    // Visit every original point with ordinary input; releasing at each 8 cm
    // sample changes the movement being tested. Facilities uses this same code.
    internal static async Task<MosqueRouteResult> FollowMosqueRouteAsync(Node host,
        FirstPersonController player, RuntimeBridge bridge, IReadOnlyList<Vector3> points,
        string label, Action<object>? observe = null, bool observeOldStops = false)
    {
        if (points.Count == 0) throw new InvalidOperationException("The mosque route has no points.");
        var revision = player.PresentationTransformRevision;
        var recoveries = player.FallRecoveries;
        var clamps = player.EdgeClamps;
        var scene = bridge.ActiveSceneId;
        var state = bridge.SelectRuntimeState();
        var knowledge = state.GetProperty("knowledge").GetRawText();
        var beats = state.GetProperty("beats").GetRawText();
        var walked = 0f;
        var visited = 0;
        var stable = false;
        void CheckControl()
        {
            // A native walk outlasts window focus, and the game then opens its own pause menu.
            // Return to the window as a player would; any other modal still fails the route.
            if (!DisplayServer.WindowIsFocused() && host.GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true } focusPause)
            {
                DisplayServer.WindowMoveToForeground();
                focusPause.Resume();
            }
            if (player.PresentationTransformRevision != revision || player.FallRecoveries != recoveries
                || player.EdgeClamps != clamps || player.ModalOpen || player.IsCrouching)
                throw new InvalidOperationException($"Mosque route changed ordinary standing control: {label}; point={visited}; "
                    + $"revision={player.PresentationTransformRevision}/{revision} falls={player.FallRecoveries}/{recoveries} "
                    + $"clamps={player.EdgeClamps}/{clamps} modal={player.ModalOpen} crouching={player.IsCrouching} at={player.GlobalPosition}.");
        }
        void CheckProgress()
        {
            CheckControl();
            var current = bridge.SelectRuntimeState();
            if (bridge.ActiveSceneId != scene || current.GetProperty("knowledge").GetRawText() != knowledge
                || current.GetProperty("beats").GetRawText() != beats)
                throw new InvalidOperationException("Physical mosque traversal granted narrative progress: " + label);
        }
        void Record(object row)
        {
            observe?.Invoke(row);
            GD.Print("walk-mosque-motion: " + JsonSerializer.Serialize(row));
        }
        async Task Physics()
        {
            var before = player.GlobalPosition;
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.PhysicsFrame);
            walked += HorizontalDistance(before, player.GlobalPosition);
            CheckControl();
        }
        async Task<bool> Settle(Vector3 target, int index, bool legacy)
        {
            var settled = 0;
            var previousY = player.GlobalPosition.Y;
            for (var frame = 0; frame < 45 && settled < 3; frame++)
            {
                await Physics();
                var y = player.GlobalPosition.Y;
                settled = player.IsOnFloor() && Math.Abs(player.Velocity.Y) < .05f && Math.Abs(y - previousY) < .002f
                    ? settled + 1 : 0;
                previousY = y;
                if (legacy) Record(MosqueMotionObservation(player, target, label, index, "old-stop-settle-" + frame));
            }
            Record(MosqueMotionObservation(player, target, label, index, legacy ? "old-stop-settled" : "final-landing"));
            return settled == 3 && HorizontalDistance(player.GlobalPosition, target) <= .09f
                && player.IsOnFloor() && player.CanStandAt(player.GlobalPosition);
        }
        CheckProgress();
        Record(new { kind = "route-start", label, observeOldStops, points = points.Select(p => p.ToString()).ToArray(),
            revision, recoveries, clamps, feet = player.GlobalPosition.ToString(), pointRadius = .09f,
            policy = observeOldStops ? "diagnostic historical stop at every original sample" : "continuous input; no point removed or reordered" });
        try
        {
            for (var index = 0; index < points.Count; index++)
            {
                var target = points[index];
                var distance = HorizontalDistance(player.GlobalPosition, target);
                var maxFrames = Math.Clamp((int)Math.Ceiling(distance / Math.Max(player.WalkSpeed, .1f) * 60.0 * 2.4), 120, 1200);
                var lastProgress = 0;
                var lastDistance = distance;
                var reached = distance <= .09f;
                var enteredMotion = !reached;
                for (var frame = 0; !reached && frame < maxFrames; frame++)
                {
                    if (!observeOldStops || frame == 0) TurnTowardTarget();
                    Input.ActionPress("move_forward");
                    await Physics();
                    distance = HorizontalDistance(player.GlobalPosition, target);
                    reached = distance <= .09f;
                    if (Math.Abs(lastDistance - distance) > .002f)
                    {
                        lastProgress = frame; lastDistance = distance;
                        if (observeOldStops && !reached) TurnTowardTarget();
                    }
                    else if (frame - lastProgress > 150) break;
                }
                void TurnTowardTarget()
                {
                    var delta = target - player.GlobalPosition;
                    player.RotationDegrees = new(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)), 0);
                }
                Record(MosqueMotionObservation(player, target, label, index, reached ? "point-visited-before-release" : "point-stall"));
                if (!reached) throw new InvalidOperationException($"Continuous mosque input could not reach {label}[{index}]: {player.GlobalPosition} -> {target}.");
                visited++;
                CheckProgress();
                if (!observeOldStops) continue;

                // Mirror the former WalkTo finally: release, two physics frames,
                // then two rendered frames, followed by WalkMosquePoint settling.
                // The last diagnostic sample may slide away: record that result
                // without calling FullWalk.Fail or disguising it as acceptance.
                Input.ActionRelease("move_forward");
                if (enteredMotion)
                {
                    await Physics(); await Physics();
                    Record(MosqueMotionObservation(player, target, label, index, "old-stop-after-two-physics"));
                    for (var frame = 0; frame < 2; frame++)
                        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
                    CheckControl();
                    RenderingServer.ForceDraw(false);
                    Record(MosqueMotionObservation(player, target, label, index, "old-stop-after-render"));
                }
                stable = await Settle(target, index, legacy: true);
                if (!stable && index != points.Count - 1)
                    throw new InvalidOperationException($"Historical stop failed before the selected diagnostic sample: {label}[{index}].");
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        if (!observeOldStops)
        {
            stable = await Settle(points[^1], points.Count - 1, legacy: false);
            if (!stable || Math.Abs(player.GlobalPosition.Y - points[^1].Y) > .08f)
                throw new InvalidOperationException($"Mosque route did not finish on its authored flat landing: {label}; feet={player.GlobalPosition}; expected={points[^1]}.");
        }
        CheckProgress();
        Record(new { kind = "route-complete", label, visited, expected = points.Count, walkedMetres = walked,
            stableLanding = stable, diagnosticOnly = observeOldStops, feet = player.GlobalPosition.ToString(),
            revision, recoveries, clamps, scene, narrativeUnchanged = true });
        return new(visited, walked, stable);
    }

    internal static object MosqueMotionObservation(FirstPersonController player, Vector3 target,
        string label, int index, string phase)
    {
        var contacts = new List<object>();
        for (var collision = 0; collision < player.GetSlideCollisionCount(); collision++)
        {
            var hit = player.GetSlideCollision(collision);
            contacts.Add(new { owner = (hit.GetCollider() as Node)?.GetPath().ToString(),
                shape = (hit.GetColliderShape() as Node)?.GetPath().ToString(),
                normal = hit.GetNormal().ToString(), position = hit.GetPosition().ToString(), depth = hit.GetDepth() });
        }
        using var query = PhysicsRayQueryParameters3D.Create(player.GlobalPosition + Vector3.Up * .20f,
            player.GlobalPosition - Vector3.Up * .45f, 3, new global::Godot.Collections.Array<Rid> { player.GetRid() });
        var support = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var body = support.Count == 0 ? null : support["collider"].AsGodotObject() as CollisionObject3D;
        var shape = body is null ? null : body.ShapeOwnerGetOwner(body.ShapeFindOwner(support["shape"].AsInt32())) as Node;
        return new { kind = "physical-route-sample", label, index, phase, physicsFrame = Engine.GetPhysicsFrames(),
            processFrame = Engine.GetProcessFrames(), target = target.ToString(), feet = player.GlobalPosition.ToString(),
            distanceXZ = HorizontalDistance(player.GlobalPosition, target), velocity = player.Velocity.ToString(),
            grounded = player.IsOnFloor(), floorNormal = player.IsOnFloor() ? player.GetFloorNormal().ToString() : null,
            forwardHeld = Input.IsActionPressed("move_forward"), standingClear = player.CanStandAt(player.GlobalPosition),
            player.StepsClimbed, player.LastStepRejection, revision = player.PresentationTransformRevision,
            recoveries = player.FallRecoveries, clamps = player.EdgeClamps, contacts,
            supportOwner = body?.GetPath().ToString(), supportShape = shape?.GetPath().ToString(),
            supportPoint = support.Count == 0 ? null : support["position"].AsVector3().ToString(),
            supportNormal = support.Count == 0 ? null : support["normal"].AsVector3().ToString() };
    }

    private async Task FollowFullMosqueRoute(FirstPersonController player, RuntimeBridge bridge,
        IReadOnlyList<Vector3> points, string label)
    {
        var result = await FollowMosqueRouteAsync(this, player, bridge, points, label);
        _walkedMeters += result.WalkedMetres;
        var directory = System.Environment.GetEnvironmentVariable("URMAN_WALK_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory)
            || RenderingServer.GetRenderingDevice() is null)
            throw new InvalidOperationException("Continuous mosque capture requires the existing absolute native evidence directory.");
        var path = Path.Combine(directory, label + "-landing.png");
        if (File.Exists(path)) throw new IOException("Refusing to replace mosque route evidence: " + path);
        await Frames(2);
        RenderingServer.ForceDraw(false);
        using var image = GetTree().Root.GetTexture().GetImage();
        if (image.SavePng(path) != Error.Ok) throw new IOException("Could not save actual mosque landing: " + path);
        GD.Print($"walk-frame: {label}-landing actual-player-position={player.GlobalPosition}");
    }
}
