using System;
using System.Globalization;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// A bounded, test-only physical walkthrough of the Act 1 demo. Unlike the
/// deterministic corridor smoke, main-route movement uses production input and
/// CharacterBody3D collision, with the first-person camera ray for interactions.
/// Separate labelled edge fixtures write positions; URMAN_WALK_MAIN_ROUTE_ONLY=1
/// omits those fixtures and starts the physical route at the normal arrival.
/// It is evidence of a traversable build, not a substitute for a human pass.
/// </summary>
public partial class Act1FirstPersonWalkthroughSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const float InteractionStandOff = 1.5f;

    private float _lookPitch;
    private float _walkedMeters;
    private bool _failed;
    private readonly System.Collections.Generic.List<Vector3> _mosqueReturnRoute = new();

    public override async void _Ready()
    {
        try { await RunAsync(); }
        catch (Exception exception) { Fail("Physical walkthrough exception: " + exception); }
    }

    private async Task RunAsync()
    {
        var mainRouteOnly = OS.GetEnvironment("URMAN_WALK_MAIN_ROUTE_ONLY") == "1";
        _addressLifecycleOnly = OS.GetEnvironment("URMAN_WALK_ADDRESS_LIFECYCLE_ONLY") == "1";
        _addressNaturalQueueOnly = OS.GetEnvironment("URMAN_WALK_ADDRESS_NATURAL_QUEUE_ONLY") == "1";
        if (_addressLifecycleOnly && _addressNaturalQueueOnly)
            throw new InvalidOperationException("Selected diagnostic and ordinary address queue coverage are separate scopes.");
        _addressLifecycleOnly |= _addressNaturalQueueOnly;
        if (_addressLifecycleOnly && !mainRouteOnly)
            throw new InvalidOperationException("Address lifecycle coverage requires the ordinary main route without local fixtures.");
        if (_addressLifecycleOnly && OS.GetEnvironment("URMAN_ADDRESS_SUPPORT_DIAGNOSTICS") != "1")
            throw new InvalidOperationException("Address lifecycle coverage requires live per-query support diagnostics.");
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn");
        var demo = packed?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Act 1 walkthrough could not instantiate the demo entrypoint.");
            return;
        }

        AddChild(demo);
        await Frames(8);

        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Act 1 walkthrough could not start through the main menu.");
            return;
        }

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = main.GetNodeOrNull<FirstPersonController>("Player");
        var ray = player?.GetNodeOrNull<RayCast3D>("Head/Camera3D/InteractionRay");
        if (bridge is null || player is null || ray is null || !demo.IntroVisible)
        {
            Fail("Act 1 walkthrough did not expose the demo bridge, player, ray and intro.");
            return;
        }

        if (!main.EnableAct1ConnectedWorld || main.ConnectedWorld is null)
        {
            Fail("Act 1 walkthrough did not start through the connected-world demo mode.");
            return;
        }

        // The prologue-watch mode needs the teaser's natural path, so the
        // ordinary one-press skip below would cut it short before the mode
        // could observe anything.
        if (OS.GetEnvironment("URMAN_DISCOVERY_ROUTE_ONLY") == "bath-door")
        {
            // R125: opening the bathhouse door while standing on its porch.
            // Spawn on the authored bath entry approach (the same fixture the
            // carry smoke uses), climb the real treads to the door, aim the
            // ordinary interaction ray at the leaf and require the door to
            // actually toggle - not a stronger-press advice.
            var bath = main.ConnectedWorld?.GetNodeOrNull<Node3D>("Act1CoreWorldGreybox/BabaiBathhouse");
            if (bath is null)
            { Fail("bath-door: the bathhouse is missing from the world."); return; }
            var approach = bath.GetMeta("entryApproach").AsVector3();
            player.ApplyZoneSpawn(approach with { Y = approach.Y + .05f }, 0);
            await Frames(6);
            var clear = bath.GetMeta("entryClearDoor").AsVector3();
            if (!await WalkTo(player, clear with { Y = player.GlobalPosition.Y }, "bath-door-landing")) return;
            var target = main.ConnectedWorld!.FindChild("BathEntranceUse", true, false) as InteractionTarget
                ?? (InteractionTarget)GetTree().Root.FindChild("BathEntranceUse", true, false)!;
            var hinge = bath.GetNode<Node3D>("BathEntranceHinge");
            var yawBefore = hinge.RotationDegrees.Y;
            // Try the real ray from a natural standing look at the leaf's
            // centre; a miss here is the defect the author reported.
            AimAt(player, target.GlobalPosition);
            await Frames(4);
            var rayHit = ray.GetCollider() == target;
            Input.ActionPress("interact");
            await PhysicsFrames(2);
            Input.ActionRelease("interact");
            await Frames(10);
            var moved = Mathf.Abs(hinge.RotationDegrees.Y - yawBefore) > 1f;
            GD.Print($"act1-bath-door: rayHit={rayHit} moved={moved} yaw {yawBefore:F0}->{hinge.RotationDegrees.Y:F0} meta={target.GetMeta("lastDoorActionResult").AsString()}");
            if (!moved)
            { Fail($"bath-door: standing on the porch did not open the door (rayHit={rayHit})."); return; }
            GD.Print("act1-bath-door: PASS porch-open");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
            return;
        }

        if (OS.GetEnvironment("URMAN_DISCOVERY_ROUTE_ONLY") == "prologue-watch")
        {
            await RunAsyncPrologueWatch(demo, player);
            return;
        }

        await SkipPrologueLikePlayer(demo);
        demo._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        await Frames(2);
        if (demo.IntroVisible || player.ModalOpen)
        {
            Fail("Act 1 walkthrough could not dismiss the intro card with E.");
            return;
        }
        var settleBridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        for (var frame = 0; frame < 600 && settleBridge?.CurrentZoneId != "village_day"; frame++)
            await Frames(1);

        // Narrow mode still starts through the ordinary menu/arrival and walks
        // every metre. It verifies the optional loop without replaying dialogue.
        if (OS.GetEnvironment("URMAN_DISCOVERY_ROUTE_ONLY") == "rear-house")
        {
            foreach (var point in AgentBAct1Layout.WalkChain.Skip(1).Take(3).Concat(AgentBAct1Layout.HousePathAxis.Skip(1)))
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"rear-house-approach-{point.X}-{point.Y}")) return;
            // The authored Babai house volume closed the old west rear corridor
            // and the workshop board-rest stand now fills the north strip. The
            // rear yard (bathhouse, woodpile, the minaret view) is reached
            // through the EX13 service gap, opened by shovelling its snow drift:
            // take the yard shovel, clear the drift, pass through the opened
            // gap, around the fence end to the rear corner discovery, and back
            // through the same opened passage (standability grid, 2026-09-28).
            var carry = GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
            var shovel = carry?.Items.FirstOrDefault(item => item.ItemId == "carry-tool-shovel");
            var fenceUse = carry?.GetChildren().OfType<YardTool>()
                .SelectMany(tool => tool.Targets).FirstOrDefault(target => target.UseId == "fence");
            if (carry is null || shovel is null || fenceUse is null)
            { Fail("rear-house requires the yard shovel and its fence drift use."); return; }
            foreach (var point in new Vector2[] { new(-25.2f, 2.6f), new(-26.05f, 2.6f), new(-26.05f, .2f), new(-26.8f, .75f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"rear-house-yard-{point.X}-{point.Y}")) return;
            AimAt(player, shovel.GlobalPosition + Vector3.Up * .45f);
            await PhysicsFrames(2);
            Input.ActionPress("interact");
            await PhysicsFrames(2);
            Input.ActionRelease("interact");
            await Frames(6);
            if (carry.HeldItem != shovel)
            { Fail($"rear-house could not take the yard shovel through ordinary input: held={carry.HeldItem?.ItemId}."); return; }
            foreach (var point in new Vector2[] { new(-26.05f, .2f), new(-25.9f, -2.3f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"rear-house-drift-approach-{point.X}-{point.Y}")) return;
            AimAt(player, fenceUse.GlobalPosition);
            await PhysicsFrames(2);
            Input.ActionPress("interact");
            await PhysicsFrames(2);
            Input.ActionRelease("interact");
            await Frames(10);
            if (!fenceUse.Completed)
            { Fail("rear-house could not clear the service-gap drift with the held shovel."); return; }
            var physicalDrift = main.ConnectedWorld?.FindChild("Ex05PassageDriftBarrier", true, false) as StaticBody3D;
            if (physicalDrift?.CollisionLayer != 0)
            { Fail("rear-house cleared the drift but its physical barrier is still enabled."); return; }
            // Free both hands for the longer north walk: aim at the real
            // ground ahead and use the ordinary placement input.
            var ahead = player.GlobalPosition + player.GlobalBasis.Z.Normalized() * 1.1f;
            AimAt(player, new(ahead.X, AgentBAct1HeightField.CollisionGround(ahead.X, ahead.Z) - .1f, ahead.Z));
            await PhysicsFrames(2);
            Input.ActionPress("carry_place");
            await PhysicsFrames(2);
            Input.ActionRelease("carry_place");
            await Frames(6);
            if (carry.HeldItem is not null)
            { Fail($"rear-house could not set the shovel down: held={carry.HeldItem.ItemId}."); return; }
            // The opened service gap is walked through to the lower street
            // side and back: the EX13 chain's short passage works both ways.
            foreach (var point in new Vector2[] { new(-24.3f, -2.85f), new(-25.4f, -2.85f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"rear-house-gap-{point.X}-{point.Y}")) return;
            GD.Print($"act1-discovery-walk: PASS loop=yard-service-gap mode=physical-characterbody-walk distance={_walkedMeters:F2}m no-player-teleport=true ex13-shovel-clear-gap-open=true");
            // The rear minaret-view discovery is reached through the yard's
            // south band behind the house and the south-fence opening at
            // x=-32.5 (verified by the standability grid, 2026-09-28).
            // Through the rear neighbour gate (the deliberate 2026-09-28
            // passage) and down the west field into the rear field.
            foreach (var point in new Vector2[] { new(-30.5f, -.8f), new(-32.3f, -1.6f), new(-33.4f, -2.2f),
                         new(-34.4f, -2.7f), new(-35.2f, -3.0f), new(-36.0f, -3.25f), new(-36.4f, -4.4f),
                         new(-36.5f, -6.0f), new(-36.5f, -7.2f), new(-35.8f, -8.8f), new(-34.6f, -12.0f),
                         new(-34.9f, -13.4f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"rear-house-rear-{point.X}-{point.Y}")) return;
            var sceneBefore = bridge.ActiveSceneId;
            if (!await InteractAt(player, ray, Interaction("discover-house-exterior-rear-minaret-view"))) return;
            (GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi)?.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await Frames(35);
            if (bridge.ActiveSceneId != sceneBefore)
            { Fail("The rear minaret view discovery changed the scene."); return; }
            foreach (var point in new Vector2[] { new(-34.6f, -12.0f), new(-35.8f, -8.8f), new(-36.5f, -7.2f),
                         new(-36.5f, -6.0f), new(-36.4f, -4.4f), new(-36.0f, -3.25f), new(-35.2f, -3.0f),
                         new(-34.4f, -2.7f), new(-33.4f, -2.2f), new(-32.3f, -1.6f), new(-30.5f, -.8f),
                         new(-25.9f, -2.3f), new(-26.2f, -2.85f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"rear-house-return-{point.X}-{point.Y}")) return;
            GD.Print($"act1-discovery-walk: PASS loop=rear-minaret-view mode=physical-characterbody-walk distance={_walkedMeters:F2}m no-player-teleport=true");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
            return;
        }

        if (OS.GetEnvironment("URMAN_DISCOVERY_ROUTE_ONLY") == "babai-side")
        {
            foreach (var point in AgentBAct1Layout.WalkChain.Skip(1).Take(3).Concat(AgentBAct1Layout.HousePathAxis.Skip(1)))
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"yard-approach-{point.X}-{point.Y}")) return;
            var gate = main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("BabaiYardSideGateCollision");
            if (gate.CollisionLayer != 1) { Fail("Babai side board starts without its closed collider."); return; }
            if (!await WalkTo(player, new(-24f, player.GlobalPosition.Y, -.7f), "yard-board-approach")) return;
            var sceneBefore = bridge.ActiveSceneId;
            if (!await InteractAt(player, ray, Interaction("discover-babai-yard-loose-side-gate-board"))) return;
            (GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi)?.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await Frames(35);
            if (gate.CollisionLayer != 0 || bridge.ActiveSceneId != sceneBefore)
            { Fail("Babai side board failed to clear its collider while preserving the scene."); return; }
            foreach (var point in new Vector2[] { new(-24.35f,-.70f), new(-26.05f,-.70f), new(-26.05f,.20f), new(-26.05f,2.20f), new(-25.20f,2.60f), new(-24.42f,2.40f), new(-24.42f,1.70f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"yard-side-loop-{point.X}-{point.Y}")) return;
            GD.Print($"act1-discovery-walk: PASS loop=babai-side mode=physical-characterbody-walk distance={_walkedMeters:F2}m no-player-teleport=true");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
            return;
        }


        if (OS.GetEnvironment("URMAN_DISCOVERY_ROUTE_ONLY") == "d14-wall-probe")
        {
            // ACT1-TECH.2 diagnostic: the author reported falling through when
            // pressing against the babai house wall near D14 X~29.8 Z~2.6 (the
            // sign conflict is preserved in the review; the physical wall here
            // is the house volume around x=-29.8, z=2.6). Walk the ordinary
            // route into the yard, press into that spot from eight headings
            // with production input, then verify the ordinary way back out.
            foreach (var point in AgentBAct1Layout.WalkChain.Skip(1).Take(3).Concat(AgentBAct1Layout.HousePathAxis.Skip(1)))
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"d14-approach-{point.X}-{point.Y}")) return;
            foreach (var point in new Vector2[] { new(-25.2f, 2.6f), new(-26.4f, 2.6f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"d14-yard-{point.X}-{point.Y}")) return;
            var target = new Vector3(-29.8f, 0f, 2.6f);
            target.Y = (float)AgentBAct1HeightField.CollisionGround(target.X, target.Z);
            GD.Print($"act1-d14-debug: grid_env='{OS.GetEnvironment("URMAN_D14_GRID")}' route_env='{OS.GetEnvironment("URMAN_DISCOVERY_ROUTE_ONLY")}'");
            if (OS.GetEnvironment("URMAN_D14_GRID") == "1")
            {
                var probeLine = "act1-d14-points:";
                foreach (var z in new[] { -3.6f, -4.0f, -4.4f, -4.8f, -5.2f, -5.6f, -6.0f })
                    foreach (var x in new[] { -25.4f, -25.6f, -25.8f, -26.0f, -26.2f, -26.4f, -26.6f })
                        probeLine += $" {(x)},{z}={(player.CanStandAt(new(x, (float)AgentBAct1HeightField.CollisionGround(x, z) + .05f, z)) ? 1 : 0)}";
                GD.Print(probeLine);
                var bandLine = "act1-d14-band:";
                foreach (var x in new[] { -26.5f, -27.5f, -28.5f, -29.5f, -30.5f, -31.5f, -32.5f })
                    bandLine += $" {x}={(player.CanStandAt(new(x, (float)AgentBAct1HeightField.CollisionGround(x, -5.2f) + .05f, -5.2f)) ? 1 : 0)}";
                GD.Print(bandLine);
                var line = "act1-d14-terrain:";
                for (var z = -2.5f; z >= -7.0f; z -= .5f)
                {
                    line += $" z={z:F1}:";
                    for (var x = -24.8f; x <= -22.2f; x += .6f)
                        line += $" {x:F1}={(float)AgentBAct1HeightField.Ground(x, z):F2}/{AgentBAct1HeightField.CollisionGround(x, z):F2}";
                }
                GD.Print(line);
                var bath = main.ConnectedWorld?.GetNodeOrNull<Node3D>("Act1CoreWorldGreybox/BabaiBathhouse");
                if (bath is not null)
                    GD.Print($"act1-d14-debug: bath_pos={bath.GlobalPosition} entryApproach={bath.GetMeta("entryApproach").AsVector3()}");
                var rear = FindInteraction(Interaction("discover-house-exterior-rear-minaret-view"), GetTree().Root);
                if (rear is not null)
                    GD.Print($"act1-d14-debug: rear_discovery_pos={rear.GlobalPosition}");
                var row = "act1-d14-grid: z\\x";
                for (var x = -34f; x <= -25.5f; x += .5f) row += $" {x,5:F1}";
                GD.Print(row);
                for (var z = 1.2f; z >= -10.4f; z -= .4f)
                {
                    row = $"act1-d14-grid: {z,4:F1}";
                    for (var x = -37.5f; x <= -32.5f; x += .5f)
                        row += player.CanStandAt(new(x, (float)AgentBAct1HeightField.CollisionGround(x, z) + .05f, z)) ? "    ." : "   #";
                    GD.Print(row);
                }
                await GodotSmokeCleanup.ReleaseAsync(demo);
                GetTree().Quit(0);
                return;
            }
            // The reported spot is the narrow strip between the house north
            // wall and the yard fence: stands stay inside the yard only.
            var probes = new (Vector2 stand, string name)[]
            {
                (new(-29.8f, .4f), "south-face"),
                (new(-26.9f, 2.2f), "gate-strip"),
                (new(-34.2f, .8f), "west-end"),
                (new(-29.0f, 1.1f), "diagonal"),
                (new(-27.6f, .4f), "east-corner"),
                (new(-31.9f, .4f), "west-corner"),
            };
            var worstBelow = 0f;
            var stuckHeadings = new System.Collections.Generic.List<string>();
            foreach (var (standPoint, name) in probes)
            {
                var stand = new Vector3(standPoint.X, player.GlobalPosition.Y, standPoint.Y);
                if (name == "west-end")
                {
                    // The workshop shed blocks the straight line; the strip is
                    // walked the same way the rear-house route walks it.
                    if (!await WalkTo(player, new(-29f, stand.Y, 2.6f), $"d14-stand-path-{name}", reportFailure: false))
                    { Fail($"d14-wall-probe could not reach the strip waypoint for {name} from {player.GlobalPosition}."); return; }
                }
                if (!await WalkTo(player, stand, $"d14-stand-{name}", reportFailure: false))
                { Fail($"d14-wall-probe could not even reach stand {name} inside the yard."); return; }
                SetYaw(player, YawTo(player.GlobalPosition, target));
                Input.ActionPress("move_forward");
                var pressedBelow = 0f;
                try
                {
                    for (var frame = 0; frame < 150; frame++)
                    {
                        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                        var ground = (float)AgentBAct1HeightField.Ground(player.GlobalPosition.X, player.GlobalPosition.Z);
                        pressedBelow = Mathf.Max(pressedBelow, ground - player.GlobalPosition.Y);
                    }
                }
                finally { Input.ActionRelease("move_forward"); }
                worstBelow = Mathf.Max(worstBelow, pressedBelow);
                var escapeTarget = name == "west-end" ? new Vector3(-29f, stand.Y, 2.6f) : stand;
                var escaped = await WalkTo(player, escapeTarget, $"d14-escape-{name}", reportFailure: false);
                if (!escaped) stuckHeadings.Add(name);
                GD.Print($"act1-d14-wall-probe: stand={name} pressed_below_ground={pressedBelow:F3}m escaped={escaped} recoveries={player.FallRecoveries}");
            }
            var reproduced = worstBelow > .60f || stuckHeadings.Count > 0 || player.FallRecoveries > 0;
            if (reproduced)
            {
                Fail($"d14-wall-probe: reproduced fall/stuck at the author's wall spot: worst_below_ground={worstBelow:F2}m stuck_headings=[{string.Join(',', stuckHeadings)}] fall_recoveries={player.FallRecoveries}.");
                return;
            }
            foreach (var point in new Vector2[] { new(-26.4f, 2.6f), new(-25.2f, 2.6f), new(-24f, 1.2f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"d14-return-{point.X}-{point.Y}")) return;
            GD.Print($"act1-d14-wall-probe: PASS verdict=not-reproduced worst_below_ground={worstBelow:F2}m fall_recoveries={player.FallRecoveries} return_walk=ok");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
            return;
        }

        // Edge probes: each deliberately places the player three metres inside an
        // authored edge and holds course outward with real movement. Terrain may
        // block some edges on its own (a ridge does stop the south one), so the
        // assertion is the invariant that matters: the player never leaves the
        // world window, never ends up below ground and never needs a recovery.
        if (!mainRouteOnly)
        {
        var probeStart = player.GlobalPosition;
        var edgeClampTotal = 0;
        var edgeFramesOutside = 0;
        var edgeWorstBelow = 0f;
        var edges = new (string Name, Vector3 Inside, float Yaw)[]
        {
            ("south", new Vector3(0f, 0f, AgentBAct1HeightField.MinZ + 3f), 0f),
            ("north", new Vector3(0f, 0f, AgentBAct1HeightField.MaxZ - 3f), 180f),
            ("west", new Vector3(AgentBAct1HeightField.MinX + 3f, 0f, -20f), 90f),
            ("east", new Vector3(AgentBAct1HeightField.MaxX - 3f, 0f, -20f), 270f)
        };
        foreach (var (edgeName, inside, yaw) in edges)
        {
            var ground = (float)AgentBAct1HeightField.Ground(inside.X, inside.Z);
            player.GlobalPosition = new Vector3(inside.X, ground + .05f, inside.Z);
            player.Velocity = Vector3.Zero;
            await PhysicsFrames(3);
            var clampsBefore = player.EdgeClamps;
            SetYaw(player, yaw);
            Input.ActionPress("move_forward");
            try
            {
                for (var frame = 0; frame < 300; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    var position = player.GlobalPosition;
                    if (position.X < AgentBAct1HeightField.MinX - .05f || position.X > AgentBAct1HeightField.MaxX + .05f
                        || position.Z < AgentBAct1HeightField.MinZ - .05f || position.Z > AgentBAct1HeightField.MaxZ + .05f)
                    {
                        edgeFramesOutside++;
                    }

                    var edgeGround = (float)AgentBAct1HeightField.Ground(position.X, position.Z);
                    edgeWorstBelow = Mathf.Max(edgeWorstBelow, edgeGround - position.Y);
                }
            }
            finally
            {
                Input.ActionRelease("move_forward");
                await PhysicsFrames(2);
            }

            var engaged = player.EdgeClamps - clampsBefore;
            edgeClampTotal += engaged;
            GD.Print($"act1-world-edge-probe: edge={edgeName} clamp_engaged={engaged} position={player.GlobalPosition}");
        }

        // Direct proof of the net itself: drop the player six metres outside the
        // window and require the next frames to bring him back inside and on the
        // ground. That is exactly the state the author was left in when he walked
        // off the end of the world.
        var outsideGround = (float)AgentBAct1HeightField.Ground(0f, AgentBAct1HeightField.MinZ - 8f);
        player.GlobalPosition = new Vector3(0f, outsideGround + .05f, AgentBAct1HeightField.MinZ - 6f);
        player.Velocity = Vector3.Zero;
        await PhysicsFrames(4);
        var recovered = player.GlobalPosition;
        if (recovered.Z < AgentBAct1HeightField.MinZ + 2f
            || recovered.Y < (float)AgentBAct1HeightField.Ground(recovered.X, recovered.Z) - .60f)
        {
            Fail($"Fall recovery did not return the player inside the world: position={recovered}.");
            return;
        }

        GD.Print(
            $"act1-world-edge-probe: PASS edges=4 clamp_engagements={edgeClampTotal} frames_outside_window={edgeFramesOutside} "
            + $"worst_below_ground={edgeWorstBelow:F2}m outside_world_recovered_to={recovered}");

        if (edgeFramesOutside > 0 || edgeWorstBelow > .60f || player.FallRecoveries > 0)
        {
            Fail(
                $"World edge probe failed: frames_outside_window={edgeFramesOutside} worst_below_ground={edgeWorstBelow:F2}m "
                + $"fall_recoveries={player.FallRecoveries}.");
            return;
        }

        // The probe moved the player on purpose; put him back where the route left
        // him, otherwise the next leg starts from the world edge.
        player.GlobalPosition = probeStart;
        player.Velocity = Vector3.Zero;
        await PhysicsFrames(3);
        }

        if (!await CompletePhysicalArrival(player, ray, bridge)) return;

        // Arrival -> house. Keep the authored signpost clear, then resolve the
        // door's connected-world position from its named interaction target.
        var arrivalTarget = FindInteraction(Interaction("arrival-enter-house"), GetTree().Root);
        if (arrivalTarget is null)
        {
            Fail("Act 1 walkthrough could not locate the connected-world arrival door.");
            return;
        }

        var expectedHouseExterior = AgentBAct1Layout.HouseDoorPortalCenter;
        if (Mathf.Abs(arrivalTarget.GlobalPosition.X - expectedHouseExterior.X) > 0.01f
            || Mathf.Abs(arrivalTarget.GlobalPosition.Z - expectedHouseExterior.Z) > 0.01f)
        {
            Fail($"Act 1 walkthrough HouseDoor is not at the declared house exterior anchor: target={arrivalTarget.GlobalPosition}, expected={expectedHouseExterior}.");
            return;
        }

        // Follow the existing authored pedestrian chain onto the house path,
        // then use its declared axis through the open yard gate; a single
        // diagonal walk would cut across the terrain outside the collision-
        // safe route envelope.
        for (var index = 1; index <= 3; index++)
        {
            var pathPoint = AgentBAct1Layout.WalkChain[index];
            if (!await WalkTo(
                    player,
                    new Vector3(pathPoint.X, player.GlobalPosition.Y, pathPoint.Y),
                    $"arrival-house-path-{index}"))
            {
                return;
            }
        }

        for (var index = 0; index < AgentBAct1Layout.HousePathAxis.Length; index++)
        {
            var pathPoint = AgentBAct1Layout.HousePathAxis[index];
            if (!await WalkTo(
                    player,
                    new Vector3(pathPoint.X, player.GlobalPosition.Y, pathPoint.Y),
                    $"arrival-house-axis-{index}"))
            {
                return;
            }
        }

        foreach (var point in new Vector2[] { new(-24.42f,2.4f), new(-25.2f,2.6f), new(-26.05f,2.2f), new(-26.05f,.2f) })
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"house-yard-entry-{point.X}-{point.Y}")) return;
        var houseApproach = AgentBAct1Layout.HouseDoorApproach;
        if (!await WalkTo(player, houseApproach, "house-door-approach")) return;
        if (_addressLifecycleOnly && !PrepareAddressLifecycle(main.ConnectedWorld, player)) return;
        if (!await InteractAt(player, ray, Interaction("arrival-enter-house"))) return;
        await Frames(4);
        AssertState(main, bridge, "house_old_pc", "house", "res://scenes/zones/style_benchmark_house_pc.tscn");
        if (HasFailed()) return;
        if (_addressLifecycleOnly && !_addressNaturalQueueOnly) _addressLifecycleAudit!.SetPhysicsProcess(true);
        if (_addressNaturalQueueOnly && !BeginNaturalAddressIndoor(main.ConnectedWorld, bridge, player)) return;

        // House -> Mansur -> old PC -> street. The household request is a
        // real player-facing gate: find Mansur with the camera ray, close the
        // dialogue through its existing UI owner, then approach the computer.
        if (!await InteractAt(player, ray, Interaction("talk-mansur")))
        {
            return;
        }
        await Frames(5);
        var mansurDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (mansurDialogue is null || !mansurDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open Mansur's request dialogue.");
            return;
        }
        if (bridge.IsInteractionAvailable(Interaction("oldpc-power")))
        { Fail("Opening Mansur's conversation granted computer access before an answer."); return; }

        var mansurChoices = mansurDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().ToArray();
        var offerHelp = mansurChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-offer-help"));
        var askWhy = mansurChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-why"));
        if (offerHelp is null || askWhy is null)
        {
            Fail("Mansur's start node did not expose both the offer-help and state-gated question choices.");
            return;
        }
        offerHelp.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        // The reply now offers a real follow-up choice. Escape is the ordinary
        // way to leave it; a hidden Continue button must not complete the UI.
        if (!await CloseDialogue(player)) return;
        if (mansurDialogue.IsOpen || player.ModalOpen
            || !bridge.IsInteractionAvailable(Interaction("oldpc-power")))
        {
            Fail("Closing Mansur's dialogue did not release the player or grant old-PC access.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("oldpc-power")))
        {
            return;
        }
        await Frames(6);
        var oldPc = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        if (oldPc is null || !oldPc.GetNode<Control>("Screen").Visible || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open the old-PC archive UI.");
            return;
        }

        if (bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("House exit became available before the first old-PC clue was read.");
            return;
        }

        var oldPcResults = oldPc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        var officialNoticeIndex = Enumerable.Range(0, oldPcResults.ItemCount)
            .FirstOrDefault(index => oldPcResults.GetItemMetadata(index).AsString() == OfficialNotice, -1);
        if (officialNoticeIndex < 0)
        {
            Fail("Physical walkthrough could not locate the official Marat notice in the old-PC archive.");
            return;
        }

        oldPcResults.EmitSignal(ItemList.SignalName.ItemSelected, officialNoticeIndex);
        await Frames(8);
        var oldPcState = bridge.SelectRuntimeState();
        if (KnowledgeStatus(oldPcState, "clue_marat_official_death_version") != "confirmed"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("The official Marat notice confirmed, but the house exit unlocked before Gulsina's warning.");
            return;
        }

        oldPc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);

        if (bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("House exit became available before the physical Gulsina warning dialogue.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("talk-gulsina")))
        {
            return;
        }
        await Frames(5);
        var gulsinaDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (gulsinaDialogue is null || !gulsinaDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open Gulsina's warning dialogue before leaving the house.");
            return;
        }
        var gulsinaChoices = gulsinaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().ToArray();
        var stayForTea = gulsinaChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-stay-for-tea"));
        var askYaramyy = gulsinaChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-yaramyy"));
        if (stayForTea is null || askYaramyy is null)
        {
            Fail("Gulsina's start node did not expose both the tea alternative and guessed ярамый question.");
            return;
        }
        stayForTea.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (!await Act1FamilyMealProof.CompleteAsync(this, bridge,
            interactPhysically: action => InteractAt(player, ray, Interaction(action)))) return;
        var gulsinaState = bridge.SelectRuntimeState();
        if (gulsinaDialogue.IsOpen || player.ModalOpen
            || VocabularyStatus(gulsinaState, "tt_yaramyy") != "guessed"
            || !NpcState(gulsinaState, "gulsina", "warning_heard")
            || KnowledgeStatus(gulsinaState, "clue_family_avoids_marat") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Tea did not preserve Gulsina's warning or incorrectly supplied an unasked answer about Marat.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("talk-gulsina"))) return;
        await Frames(5);
        var askMarat = gulsinaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-marat"));
        if (askMarat is null) { Fail("Gulsina's real family question was unavailable on return."); return; }
        askMarat.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        gulsinaDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (gulsinaDialogue.IsOpen || player.ModalOpen
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_family_avoids_marat") != "confirmed"
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        { Fail("The family answer did not unlock the walk to Alsu."); return; }

        if (_addressLifecycleOnly && !await VerifyAddressLifecycleIndoors(main.ConnectedWorld, bridge, player)) return;
        if (!await InteractAt(player, ray, Interaction("house-to-route")))
        {
            return;
        }
        await Frames(4);
        AssertState(main, bridge, "village_day", "crossroad_signs_inspect", "res://scenes/zones/style_benchmark_day_street.tscn");
        if (HasFailed()) return;

        if (HorizontalDistance(player.GlobalPosition, houseApproach) > .3f)
        { Fail("Leaving the house did not return to its physical door."); return; }
        if (_addressLifecycleOnly)
        {
            if (!await CompleteAddressLifecycleOutside(main.ConnectedWorld, bridge, player)) return;
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
            return;
        }
        foreach (var point in new Vector2[] { new(-26.05f,.2f), new(-26.05f,2.2f), new(-25.2f,2.6f), new(-24.42f,2.4f) }
                     .Concat(AgentBAct1Layout.HousePathAxis.Reverse())
                     .Concat(new Vector2[] { new(-.1f,-2.8f), new(-.1f,3.7f) }))
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"house-exit-street-{point.X}-{point.Y}")) return;

        // The street conversation is a physical detective beat, not an
        // optional hint: Alsu's account must confirm the contradiction before
        // the FAP route becomes available.
        if (bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        {
            Fail("FAP route became available before Alsu's contradiction dialogue.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("talk-alsu")))
        {
            return;
        }
        await Frames(5);
        var alsuDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (alsuDialogue is null || !alsuDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open Alsu's route dialogue before the FAP.");
            return;
        }
        if (!await Act1AlsuWalkProof.CompleteAsync(this, bridge)
            || !await InteractAt(player, ray, Interaction("talk-alsu"))) return;
        if (!await ChooseVisibleDialogue(bridge, "choice-alsu-versions")
            || !await ChooseVisibleDialogue(bridge, "choice-alsu-go-to-naila")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_versions_conflict") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        { Fail("Alsu's account silently performed the first source comparison."); return; }
        if (!await CompareVisibleSources(bridge, "compare-versions-scope",
                OfficialNotice, ChapterPrefix + "knowledge/clue_alsu_heard_versions")) return;
        if (alsuDialogue.IsOpen || player.ModalOpen
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_versions_conflict") != "confirmed"
            || !bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        {
            Fail("Closing Alsu's dialogue did not confirm the Marat contradiction or unlock the FAP route.");
            return;
        }

        // Street -> FAP is an actual walk along the authored branch, not an
        // interaction at the main-road sign followed by a clinic teleport.
        var fapEntry = FindInteraction(Interaction("route-to-fap"), GetTree().Root);
        var fapDoor = fapEntry?.HasMeta("authoredDoorSurface") == true
            ? GetNodeOrNull<MeshInstance3D>(fapEntry.GetMeta("authoredDoorSurface").AsString()) : null;
        if (fapEntry is null || fapDoor?.Mesh is null
            || fapEntry.GlobalPosition.DistanceTo(fapDoor.GlobalTransform * fapDoor.Mesh.GetAabb().GetCenter()) > .25f)
        {
            Fail("FAP transition does not correspond to the actual clinic door surface.");
            return;
        }
        // Alsu now remains at the actual conversation turn (.05,-8.8).
        // The former direct segment to (0,-10) walked into her physical body.
        // Round the west side on the existing street; retain the same route
        // corner on later FAP visits and returns instead of crossing her again.
        var stoppedAlsu = AlsuStreetWalkPresentation.Current(GetTree());
        if (stoppedAlsu is null || !stoppedAlsu.Arrived || !stoppedAlsu.PhysicalAccessReady)
        { Fail("The FAP walk requires the actual stopped companion at the completed street turn."); return; }
        var alsuTurnPosition = stoppedAlsu.Actor.GlobalPosition;
        var turnRevision = player.PresentationTransformRevision;
        var turnRecoveries = player.FallRecoveries;
        var turnClamps = player.EdgeClamps;
        GD.Print($"walk-alsu-turn-bypass: actor={alsuTurnPosition} player={player.GlobalPosition} "
            + "west-corners=(-1.2,-8),(-1.2,-10) ordinary-standing-input=true");
        foreach (var point in new Vector2[] { new(-1.2f, -8f), new(-1.2f, -10f) })
        {
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y),
                    $"fap-alsu-bypass-{point.X}-{point.Y}")) return;
            if (player.IsCrouching || !player.CanStandAt(player.GlobalPosition)
                || player.PresentationTransformRevision != turnRevision || player.FallRecoveries != turnRecoveries
                || player.EdgeClamps != turnClamps || stoppedAlsu.Actor.GlobalPosition.DistanceTo(alsuTurnPosition) > .002f)
            { Fail("The physical FAP bypass changed standing clearance, the companion or the player transform owner."); return; }
        }
        foreach (var point in AgentBAct1Layout.FapBranchAxis.SkipLast(1))
        {
            if (!await WalkTo(player, new Vector3(point.X, player.GlobalPosition.Y, point.Y),
                    $"fap-branch-{point.X}-{point.Y}")) return;
        }
        if (!await InteractAt(player, ray, Interaction("route-to-fap")))
        {
            return;
        }
        await Frames(4);
        AssertState(main, bridge, "fap_clinic", "fap_waiting_room_day", "res://scenes/zones/chapter1_fap_clinic.tscn");
        if (HasFailed()) return;

        // Naila's authored dialogue gates the document desk. Prove the desk is
        // unavailable first, then use the same physical dialogue UI path.
        if (bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        {
            Fail("FAP document desk became available before Naila's dialogue.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("talk-naila")))
        {
            return;
        }
        await Frames(5);
        var nailaDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (nailaDialogue is null || !nailaDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open Naila's dialogue before the document desk.");
            return;
        }
        var nailaQuestion = nailaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/dialogue-naila-ask-record"));
        if (nailaQuestion is null)
        { Fail("Naila's initial wording question was missing."); return; }
        nailaQuestion.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        nailaDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (nailaDialogue.IsOpen || player.ModalOpen
            || !bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        {
            Fail("Closing Naila's dialogue did not unlock the FAP document desk.");
            return;
        }

        // FAP medical card -> street -> house. The document is opened and closed
        // through the same UI path, but no state is injected by the test.
        if (!await InteractAt(player, ray, Interaction("fap-to-document-desk"))
            || !await InteractAt(player, ray, Interaction("fap-document-desk-to-official-record")))
        {
            return;
        }
        await Frames(5);
        AssertDocument("urman.oldpc:document/doc_marat_medical_card");
        CloseDocument();
        await Frames(3);

        if (!await InteractAt(player, ray, Interaction("official-leave-clinic")))
            return;
        await Frames(5);
        AssertState(main, bridge, "village_day", "evidence-official-death", "res://scenes/zones/style_benchmark_day_street.tscn");
        if (HasFailed()) return;
        foreach (var point in AgentBAct1Layout.FapBranchAxis.Reverse().Skip(1))
        {
            if (!await WalkTo(player, new Vector3(point.X, player.GlobalPosition.Y, point.Y),
                $"fap-return-{point.X}-{point.Y}")) return;
            if (!mainRouteOnly && point == new Vector2(23f, -25f))
            {
                // Physically enter and leave the actual neighboring holding;
                // presentation-camera checkpoints alone cannot prove access.
                // The holding's side house (EastStreetMidFacade at 25.3,-19.2,
                // footprint roughly x 22.6..28, z -22.2..-16.2) carries a real
                // wall collider now, so the loop rounds its west and north
                // corners instead of crossing a building the old walk-through
                // could pass straight through.
                foreach (var yardPoint in new Vector2[]
                         { new(19f, -22.5f), new(20.4f, -19.6f), new(20.4f, -14.6f),
                           new(24f, -14.2f), new(23f, -12f), new(21f, -8.5f),
                           new(23f, -12f), new(24f, -14.2f), new(20.4f, -14.6f),
                           new(20.4f, -19.6f), new(19f, -22.5f), point })
                {
                    if (!await WalkTo(player, new(yardPoint.X, player.GlobalPosition.Y, yardPoint.Y),
                        $"east-holding-walk-{yardPoint.X}-{yardPoint.Y}")) return;
                    if (yardPoint == new Vector2(23f, -12f))
                    {
                        var position = player.GlobalPosition;
                        var ground = AgentBAct1HeightField.Ground(position.X, position.Z);
                        GD.Print($"yard-ground-check: player_y={position.Y:F3} ground_y={ground:F3}");
                        if (Math.Abs(position.Y - ground) > .12)
                        {
                            Fail("Yard movement does not follow the shared terrain surface.");
                            return;
                        }
                    }
                }
            }
        }
        // Persist the actual street position, not a fresh clinic/house spawn.
        var returnPosition = player.GlobalPosition;
        if (!await bridge.SaveSlotAsync("walk-fap-return")
            || !await bridge.LoadSlotAsync("walk-fap-return"))
        {
            Fail("Could not save and resume the FAP return walk.");
            return;
        }
        await Frames(5);
        AssertState(main, bridge, "village_day", "evidence-official-death", "res://scenes/zones/style_benchmark_day_street.tscn");
        if (player.GlobalPosition.DistanceTo(returnPosition) > 0.15f
            || !bridge.IsInteractionAvailable(Interaction("official-to-internal-register")))
        {
            Fail("FAP return resume lost the street position or house interaction.");
            return;
        }
        foreach (var point in AgentBAct1Layout.HousePathAxis.Prepend(new Vector2(-1.2f, -10f)))
        {
            if (!await WalkTo(player, new Vector3(point.X, player.GlobalPosition.Y, point.Y),
                $"house-return-{point.X}-{point.Y}")) return;
        }
        foreach (var point in new Vector2[] { new(-24.42f,2.4f), new(-25.2f,2.6f), new(-26.05f,2.2f), new(-26.05f,.2f), new(houseApproach.X,houseApproach.Z) })
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"house-return-yard-{point.X}-{point.Y}")) return;
        if (!await InteractAt(player, ray, Interaction("official-to-internal-register")))
        {
            return;
        }
        await Frames(5);
        AssertState(main, bridge, "house_old_pc", "evidence-internal-register", "res://scenes/zones/style_benchmark_house_pc.tscn");
        if (HasFailed()) return;

        // The second house visit checks that the physical route remains usable
        // after the evidence transition. Rinat and the shared archive steps
        // are kept here because they are the Act 1 state gates for the forest.
        if (!await InteractAt(player, ray, Interaction("oldpc-power"))) return;
        await Frames(4);
        var archive = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        var archiveRows = archive!.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        var registerIndex = Enumerable.Range(0, archiveRows.ItemCount).Single(index => archiveRows.GetItemMetadata(index).AsString() == "urman.oldpc:document/rec_marat_case_register_conflict");
        archiveRows.EmitSignal(ItemList.SignalName.ItemSelected, registerIndex);
        await Frames(6);
        if (archive.ActiveDocumentId != "urman.oldpc:document/rec_marat_case_register_conflict")
        { Fail("The returning player could not read the internal register on the old PC."); return; }
        archive.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(2);
        await Act1SourceExcerptProof.RecordNoticeCauseAsync(this, bridge);
        await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, bridge);
        if (!await CompareVisibleSources(bridge, "compare-records-contradiction",
                OfficialNotice, "urman.oldpc:document/rec_marat_case_register_conflict"))
        { Fail("The record comparison was rejected."); return; }

        // Return with a new question, preserving the investigation phase at
        // both doors. The outside legs use the same tested footpaths as the
        // first visit, without inserting a direct SwitchZone shortcut.
        if (!await InteractAt(player, ray, Interaction("house-to-route"))) return;
        foreach (var point in new Vector2[] { new(-26.05f,.2f), new(-26.05f,2.2f), new(-25.2f,2.6f), new(-24.42f,2.4f) }
                     .Concat(AgentBAct1Layout.HousePathAxis.Reverse())
                     .Append(new Vector2(-1.2f, -10f))
                     .Concat(AgentBAct1Layout.FapBranchAxis.SkipLast(1)))
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"naila-question-outward-{point.X}-{point.Y}")) return;
        if (!await InteractAt(player, ray, Interaction("route-to-fap"))
            || !await InteractAt(player, ray, Interaction("talk-naila"))
            || !await ChooseVisibleDialogue(bridge, "choice-naila-contradiction"))
        { Fail("The physical return did not present Naila's pending record question."); return; }
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_naila_record_scope") != "hidden")
        { Fail("Naila's question supplied an answer before checking the two fields."); return; }
        if (!await ChooseVisibleDialogue(bridge, "choice-naila-show-external-wording")
            || !await ChooseVisibleDialogue(bridge, "choice-naila-ask-category-scope")
            || !await ChooseVisibleDialogue(bridge, "choice-naila-check-answer")
            || !await CompareVisibleSources(bridge, "compare-record-scope",
                "urman.oldpc:document/rec_marat_case_register_conflict", ChapterPrefix + "knowledge/clue_naila_record_scope")
            || !await InteractAt(player, ray, Interaction("official-leave-clinic")))
        { Fail("The physical return to Naila did not resolve the record question."); return; }
        foreach (var point in AgentBAct1Layout.FapBranchAxis.Reverse().Skip(1)
                     .Append(new Vector2(-1.2f, -10f))
                     .Concat(AgentBAct1Layout.HousePathAxis)
                     .Concat(new Vector2[] { new(-24.42f,2.4f), new(-25.2f,2.6f), new(-26.05f,2.2f), new(-26.05f,.2f), new(houseApproach.X,houseApproach.Z) }))
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"naila-question-return-{point.X}-{point.Y}")) return;
        if (!await InteractAt(player, ray, Interaction("official-to-internal-register"))) return;
        AssertState(main, bridge, "house_old_pc", "evidence-internal-register", "res://scenes/zones/style_benchmark_house_pc.tscn");
        if (HasFailed()) return;

        if (!await InteractAt(player, ray, Interaction("internal-register-to-rinat")))
        {
            return;
        }
        await Frames(5);
        var dialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (dialogue is null || !dialogue.IsOpen)
        {
            Fail("Physical walkthrough did not open the Rinat dialogue.");
            return;
        }
        if (!await ChooseVisibleDialogue(bridge, "choice-rinat-present-category")
            || !await ChooseVisibleDialogue(bridge, "choice-rinat-leave")) return;

        if (!await InteractAt(player, ray, Interaction("internal-register-to-saved-message")))
        {
            return;
        }
        await Frames(5);
        AssertDocument("urman.oldpc:document/msg_marat_saved_last_normal");
        if (HasFailed()) return;
        CloseDocument();
        await Frames(3);

        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_message_read") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_was_afraid_before_death") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("saved-message-to-boundary-source")))
        { Fail("Reading Marat's message silently answered the question for Alsu."); return; }
        await Act1SourceExcerptProof.RecordMessageVoiceAsync(this, bridge);
        if (!await InteractAt(player, ray, Interaction("house-to-route"))) return;
        foreach (var point in new Vector2[] { new(-26.05f,.2f), new(-26.05f,2.2f), new(-25.2f,2.6f), new(-24.42f,2.4f) }
                     .Concat(AgentBAct1Layout.HousePathAxis.Reverse()))
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"alsu-message-outward-{point.X}-{point.Y}")) return;
        AssertState(main, bridge, "village_day", "evidence-saved-message", "res://scenes/zones/style_benchmark_day_street.tscn");
        if (HasFailed() || !await InteractAt(player, ray, Interaction("talk-alsu"))
            || !await ChooseVisibleDialogue(bridge, "choice-alsu-show-message")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "hidden")
        { Fail("Showing the message silently chose its interpretation."); return; }
        if (!await ChooseVisibleDialogue(bridge, "choice-alsu-quote-heard-voice")
            || !await ChooseVisibleDialogue(bridge, "choice-alsu-check-original")) return;
        if (player.ModalOpen
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_was_afraid_before_death") != "confirmed")
        { Fail("Alsu's bounded reply did not preserve the source and release the physical return."); return; }
        foreach (var point in AgentBAct1Layout.HousePathAxis
                     .Concat(new Vector2[] { new(-24.42f,2.4f), new(-25.2f,2.6f), new(-26.05f,2.2f), new(-26.05f,.2f), new(houseApproach.X,houseApproach.Z) }))
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"alsu-message-return-{point.X}-{point.Y}")) return;
        if (!await InteractAt(player, ray, Interaction("official-to-internal-register"))) return;
        AssertState(main, bridge, "house_old_pc", "evidence-saved-message", "res://scenes/zones/style_benchmark_house_pc.tscn");
        if (HasFailed()) return;

        foreach (var interaction in new[]
        {
            "saved-message-to-boundary-source",
            "boundary-source-to-reread",
            "reread-to-edge-sketch"
        })
        {
            if (interaction == "boundary-source-to-reread"
                && !await CompareVisibleSources(bridge, "compare-voice-link",
                    "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary"))
            { Fail("The voice comparison was rejected."); return; }
            if (interaction == "reread-to-edge-sketch"
                && !await CompareVisibleSources(bridge, "compare-reread-response",
                    "urman.oldpc:document/rec_marat_case_register_conflict", "urman.oldpc:document/tw_shurale_urman_boundary"))
            { Fail("The reread comparison was rejected."); return; }
            if (interaction == "reread-to-edge-sketch"
                && !await Act1SourceReturnsProof.CompleteAsync(this, bridge,
                    interactPhysically: action => InteractAt(player, ray, Interaction(action)))) return;
            if (!await InteractAt(player, ray, Interaction(interaction)))
            {
                return;
            }
            await Frames(5);
            AssertDocument(interaction == "reread-to-edge-sketch"
                ? "urman.oldpc:document/doc_kara_urman_edge_sketch"
                : "urman.oldpc:document/tw_shurale_urman_boundary");
            if (HasFailed()) return;
            CloseDocument();
            await Frames(3);
        }

        if (bridge.IsInteractionAvailable(Interaction("edge-sketch-to-zirat-road")))
        { Fail("Reading the sketch bypassed explaining the intended route to Timur."); return; }
        if (!await CompareVisibleSources(bridge, "compare-route-purpose-landmarks",
                "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/doc_kara_urman_edge_sketch")
            || !await InteractAt(player, ray, Interaction("house-to-route"))) return;
        foreach (var point in new Vector2[] { new(-26.05f,.2f), new(-26.05f,2.2f), new(-25.2f,2.6f), new(-24.42f,2.4f) }
                     .Concat(AgentBAct1Layout.HousePathAxis.Reverse()).Append(AgentBAct1Layout.MainRoadAxis[4]))
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"timur-route-outward-{point.X}-{point.Y}")) return;
        if (!await InteractAt(player, ray, Interaction("route-to-mosque"))
            || !await ChooseVisibleDialogue(bridge, "choice-timur-register")
            || !await ChooseVisibleDialogue(bridge, "choice-timur-route-check")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_discussed") != "hidden")
        { Fail("Timur's question silently chose the reason for walking to the boundary."); return; }
        if (!await ChooseVisibleDialogue(bridge, "choice-timur-name-landmarks")
            || !await CloseDialogue(player)) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_discussed") != "confirmed")
        { Fail("The visible route-intent answer did not complete the conversation with Timur."); return; }
        if (!bridge.IsInteractionAvailable(Interaction("edge-sketch-to-zirat-road"))
            || bridge.CurrentZoneId != "village_day" || bridge.ActiveSceneId != ChapterPrefix + "scene/evidence-edge-sketch")
        { Fail("The explicit route discussion did not leave the reached sketch and street departure available."); return; }
        if (!await LeaveMosque(player, main.ConnectedWorld)) return;
        // Follow the inhabited street to its actual roadside transition. This
        // traversal checks the connected space; it is not a duration estimate.
        foreach (var point in AgentBAct1Layout.MainRoadAxis.Skip(4).Take(4))
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"timur-to-zirat-street-{point.X}-{point.Y}")) return;

        if (!await InteractAt(player, ray, Interaction("edge-sketch-to-zirat-road"), maxTransitionDistance: 2.5f))
        {
            return;
        }
        await Frames(4);
        AssertState(main, bridge, "zirat_road", "zirat-road", "res://scenes/zones/chapter1_zirat_road.tscn");
        if (HasFailed()) return;

        // Inspect the last inhabited holding via its open gate and side seni,
        // then return to the cemetery route without teleporting the player.
        if (!mainRouteOnly)
        foreach (var point in new Vector2[]
                 { new(0f, -59.5f), new(-3.8f, -59.5f), new(-12f, -59.5f),
                   new(-19f, -63.2f), new(-24f, -64.5f), new(-25f, -68.3f),
                   new(-28.6f, -67.4f), new(-25f, -68.3f), new(-24f, -64.5f),
                   new(-19f, -63.2f), new(-12f, -59.5f), new(-3.8f, -59.5f), new(0f, -59.5f) })
        {
            if (!await WalkTo(player, new Vector3(point.X, player.GlobalPosition.Y, point.Y),
                    $"zirat-holding-{point.X}-{point.Y}")) return;
        }

        // This is intentionally a long physical walk: it catches a broken
        // floor, a wrong collision layer or a bad spawn that teleport-based
        // tests cannot see.
        if (!await InteractAt(player, ray, RinatPresencePresentation.ObservationId)) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_rinat_at_roadside") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_do_not_answer_rule") != "hidden")
        { Fail("The actual walk to Rinat did not record his presence independently of the final warning."); return; }
        if (!await InteractAt(player, ray, Interaction("zirat-roadside-clue")))
        {
            return;
        }

        if (bridge.IsInteractionAvailable(Interaction("compare-route-match"))
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_sketch_field_landmarks") != "hidden")
        { Fail("Reading the tag supplied the unobserved ditch/fence relationship."); return; }
        if (!await InteractAt(player, ray, Interaction("observe-sketch-landmarks"))) return;

        // The physical target opens its source reader after the awaited save.
        // Finish reading that actual result before opening the comparison tab.
        var landmarksJournal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        const string landmarksSource = "urman.chapter1:knowledge/clue_sketch_field_landmarks";
        for (var frame = 0; frame < 180 && (!landmarksJournal.GetNode<Control>("Screen").Visible
                || landmarksJournal.ActiveEntryId != landmarksSource); frame++) await Frames(1);
        if (!landmarksJournal.GetNode<Control>("Screen").Visible || landmarksJournal.ActiveEntryId != landmarksSource
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_sketch_field_landmarks") != "confirmed")
        { Fail("The physical landmark observation did not open its actual source reader."); return; }
        landmarksJournal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (player.ModalOpen) { Fail("Closing the landmark source did not release the physical player."); return; }

        if (!await CompareVisibleSources(bridge, "compare-route-match",
                "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks"))
        { Fail("The route comparison was rejected."); return; }
        var ziratState = bridge.SelectRuntimeState();
        if (KnowledgeStatus(ziratState, "clue_marat_last_route_near_zirat") != "confirmed")
        {
            Fail("Physical walkthrough inspected the zirat roadside clue without confirming Marat's last-route clue.");
            return;
        }

        var ziratClueTarget = FindInteraction(Interaction("zirat-roadside-clue"), GetTree().Root);
        if (ziratClueTarget is null
            || bridge.IsInteractionAvailable(Interaction("zirat-roadside-clue"))
            || ziratClueTarget.IsAvailable()
            || ziratClueTarget.CollisionLayer != 0
            || ziratClueTarget.CollisionMask != 0)
        {
            Fail("Confirmed zirat clue remained available or collidable after dispatch.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("zirat-road-to-forest")))
        {
            return;
        }
        await Frames(8);
        AssertState(main, bridge, "kara_urman_night", "forest-approach", "res://scenes/zones/style_benchmark_kara_urman_night.tscn");
        if (HasFailed()) return;
        if (!mainRouteOnly && !await ProbeEightHeadings(player)) return;
        foreach (var point in AgentBAct1Layout.KaraRoadAxis.Skip(3).SkipLast(1).Append(new Vector2(.6f, -121f)))
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"forest-approach-{point.X}-{point.Y}")) return;
        if (!await InteractAt(player, ray, Interaction("forest-approach-to-forest"))) return;
        await Frames(8);
        AssertState(main, bridge, "kara_urman_night", "forest", "res://scenes/zones/style_benchmark_kara_urman_night.tscn");
        if (HasFailed()) return;

        for (var attempt = 0; attempt < 600 && !CliffhangerComplete(bridge); attempt++)
        {
            if (!Act1RinatRoadsideProof.LookAtIntervention(this)) return;
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        }
        var state = bridge.SelectRuntimeState();
        var endingAudio = GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi;
        if (!CliffhangerComplete(bridge)
            || KnowledgeStatus(state, "clue_do_not_answer_rule") != "confirmed"
            || endingAudio?.IsPresenting != false)
        {
            Fail("Physical walkthrough reached Kara-Urman without committing the Act 1 cliffhanger.");
            return;
        }



        GD.Print($"act1-first-person-walkthrough: PASS mode=physical-characterbody-walk (real movement/ray/input; distinct from the capture harness's presentation waypoint audit) distance={_walkedMeters:F2}m final-zone={bridge.CurrentZoneId} cliffhanger=completed");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task<bool> ProbeEightHeadings(FirstPersonController player)
    {
        if (player.ModalOpen)
        { Fail("Eight-heading probe requires the playable night approach before its final trigger."); return false; }
        var headingProbeStart = player.GlobalPosition;
        var walkedTrace = new System.Collections.Generic.List<Vector3> { headingProbeStart };
        var worstBelowGround = 0f;
        var outsideWindow = 0;
        for (var heading = 0; heading < 8; heading++)
        {
            SetYaw(player, heading * 45f);
            Input.ActionPress("move_forward");
            try
            {
                for (var frame = 0; frame < 240; frame++)
                {
                    var before = player.GlobalPosition;
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    var position = player.GlobalPosition;
                    _walkedMeters += HorizontalDistance(before, position);
                    if (frame % 15 == 0 && HorizontalDistance(walkedTrace[^1], position) > .30f)
                        walkedTrace.Add(position);
                    if (position.X < AgentBAct1HeightField.MinX - .05f || position.X > AgentBAct1HeightField.MaxX + .05f
                        || position.Z < AgentBAct1HeightField.MinZ - .05f || position.Z > AgentBAct1HeightField.MaxZ + .05f)
                        outsideWindow++;
                    var ground = (float)AgentBAct1HeightField.Ground(position.X, position.Z);
                    worstBelowGround = Mathf.Max(worstBelowGround, ground - position.Y);
                }
            }
            finally
            {
                Input.ActionRelease("move_forward");
                await PhysicsFrames(2);
            }
        }
        if (outsideWindow > 0 || worstBelowGround > .60f || player.FallRecoveries > 0 || player.ModalOpen)
        {
            Fail($"Eight-heading edge walk failed: frames_outside_window={outsideWindow} "
                + $"worst_below_ground={worstBelowGround:F2}m fall_recoveries={player.FallRecoveries} "
                + $"modal={player.ModalOpen} from={headingProbeStart}.");
            return false;
        }
        // Retrace positions actually reached by CharacterBody movement. This
        // returns from the optional probe without writing the player's transform.
        // A body that slid down a bank cannot always climb the same face back;
        // a player would walk out another way. Retrace first, and if a leg is
        // too steep walk straight back to the probe start. Only when both fail
        // is the player actually stuck at the edge.
        for (var index = walkedTrace.Count - 1; index >= 0; index--)
        {
            if (await WalkTo(player, walkedTrace[index], $"edge-probe-return-{index}", reportFailure: false)) continue;
            var retrace = _lastWalkFailure;
            if (await WalkTo(player, headingProbeStart, "edge-probe-return-direct", reportFailure: false)) break;
            var direct = _lastWalkFailure;
            // A body on the river ice climbs out by the plank steps on the
            // village bank and comes back over the culvert, as a player would.
            if (await WalkOutOfRiver(player, headingProbeStart)) break;
            Fail($"Edge probe left the player stuck (possible soft-lock): retrace {retrace}; direct {direct}; river exit {_lastWalkFailure}.");
            return false;
        }
        GD.Print($"act1-world-edge-probe: headings=8 before-final-trigger frames_outside_window={outsideWindow} "
            + $"worst_below_ground={worstBelowGround:F2}m edge_clamps_observed={player.EdgeClamps}; returned by physical walk");
        return true;
    }

    private static bool CliffhangerComplete(RuntimeBridge bridge) =>
        bridge.SelectRuntimeState().GetProperty("beats")
            .TryGetProperty(ChapterPrefix + "beat/cliffhanger-hard-cut", out var beat)
        && beat.GetString() == "completed";

    private string _lastWalkFailure = string.Empty;

    private Task<bool> WalkOutOfRiver(FirstPersonController player, Vector3 probeStart)
    {
        // Relayout v3: the river became a deep gorge fenced along both rims. A probe
        // that ends up inside it found a hole in the rim fence; there is no walk out.
        var position = player.GlobalPosition;
        _lastWalkFailure = Math.Abs(position.Z - (float)AgentBAct1HeightField.RiverMeander(position.X)) > AgentBAct1HeightField.GorgeHalfWidth
            ? "not in the gorge" : $"walked past the gorge rim fence at {position}";
        return Task.FromResult(false);
    }

    private async Task RunAsyncPrologueWatch(Act1DemoRoot demo, FirstPersonController player)
    {
            // N2.1 natural prologue path: the teaser runs without any press,
            // the night forest is the first image, the cue cuts to black with
            // babai's caption, and one press then dismisses the arrival card.
            string? bridgeZone = null;
            for (var settle = 0; settle < 90; settle++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                bridgeZone = (GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge)?.CurrentZoneId;
                if (bridgeZone == "kara_urman_night") break;
            }
            if (bridgeZone != "kara_urman_night")
            { Fail($"Prologue should open in the night forest, got zone={bridgeZone} player={player.GlobalPosition}."); return; }
            GD.Print($"act1-prologue-watch: night zone entered, player={player.GlobalPosition}");
            if (!demo.IntroVisible)
            { Fail("Prologue teaser must keep the intro surface blocking menus."); return; }
            Input.ActionPress("move_forward");
            var sawNightZone = false;
            // Headless frames run faster than wall clock; budget real time
            // for the teaser walk, the cue, the 21s ride and the fade.
            var deadline = Time.GetTicksMsec() + 240_000u;
            var sawDeepForest = false;
            var cueStarted = 0UL;
            var cueCaptured = false;
            var nightCaptured = false;
            var captureDirectory = OS.GetEnvironment("URMAN_PROLOGUE_WATCH_CAPTURE");
            async Task CaptureCue(string name)
            {
                if (string.IsNullOrEmpty(captureDirectory) || DisplayServer.GetName() == "headless") return;
                Directory.CreateDirectory(captureDirectory);
                DisplayServer.WindowMoveToForeground();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(captureDirectory, name + ".png"));
                GD.Print("act1-prologue-watch: captured " + name);
            }
            try
            {
                while (Time.GetTicksMsec() < deadline)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    // 2026-09-29: the teaser has its own deep-forest location;
                    // look along the track ahead, as a walking player would.
                    if (demo.DeepForest is { } forest && IsInstanceValid(forest))
                    {
                        sawDeepForest = true;
                        var local = player.GlobalPosition - PrologueDeepForest.Origin;
                        var aheadZ = local.Z - 4f;
                        var detour = Mathf.Abs(aheadZ - PrologueDeepForest.FallenSpruceZ) < 9f ? 7f : 0f;
                        var dx = PrologueDeepForest.TrackX(aheadZ) + detour - local.X;
                        if (!player.ModalOpen) player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-dx, 4f)));
                    }
                    if (!nightCaptured && sawDeepForest && !player.ModalOpen)
                    { nightCaptured = true; await CaptureCue("forest_walk"); }
                    if (demo.DeepForest?.FindChild("PeripheralForestPresence", true, false) is Node3D presence && presence.IsInsideTree())
                    {
                        if (cueStarted == 0) cueStarted = Time.GetTicksMsec();
                        if (!cueCaptured && Time.GetTicksMsec() - cueStarted >= 1900)
                        { cueCaptured = true; await CaptureCue("peripheral_presence"); }
                    }
                    var zone = (GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge)?.CurrentZoneId;
                    sawNightZone |= zone == "kara_urman_night";
                    // The walk covers the authored stretch and the cue fires;
                    // the loop ends when the zone returns to the village.
                    if (zone == "village_day" && sawNightZone) break;
                }
            }
            finally { Input.ActionRelease("move_forward"); }
            if (!sawDeepForest)
            { Fail("The teaser did not use its own deep-forest location."); return; }
            await CaptureCue("niva_handover");
            // N2.2: the Niva ride follows the teaser inside the same block;
            // answer the calibration with the second choice ("some") through
            // the real dialogue UI, then verify its guessed vocabulary.
            var rideDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
            DialogueUi? calib = null;
            var calibDeadline = Time.GetTicksMsec() + 120_000u;
            while (Time.GetTicksMsec() < calibDeadline && calib is null)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (rideDialogue is { IsOpen: true }) calib = rideDialogue;
            }
            if (calib is null)
            { Fail("The Niva ride did not open the language calibration dialogue."); return; }
            var answerButtons = calib.GetNode("Screen/Panel/Layout/Choices").GetChildren().OfType<Button>().ToArray();
            if (answerButtons.Length < 4)
            { Fail($"The calibration dialogue lost its choices: {answerButtons.Length}."); return; }
            if (!calib.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text.Contains("татарча", StringComparison.Ordinal))
            { Fail("Babai's calibration question must open in Tatar."); return; }
            answerButtons[1].EmitSignal(Button.SignalName.Pressed);
            // The reply offers keep / simpler / more Tatar; keep the answer.
            Button? keep = null;
            for (var reveal = 0; reveal < 60 && keep is null; reveal++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                keep = calib.GetNode("Screen/Panel/Layout/Choices").GetChildren().OfType<Button>()
                    .FirstOrDefault(button => button.Text == vocabBridgeForKeep()?.ResolveText("urman.chapter1:text/prologue-niva-level-keep"));
            }
            RuntimeBridge? vocabBridgeForKeep() => GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            if (keep is null) { Fail("The calibration reply did not offer to keep the chosen level."); return; }
            keep.EmitSignal(Button.SignalName.Pressed);
            for (var close = 0; close < 90 && calib.IsOpen; close++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (calib.IsOpen)
            { Fail("The calibration reply did not close the dialogue."); return; }
            var vocabBridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            var vocabState = vocabBridge?.SelectRuntimeState();
            if (vocabState is not { ValueKind: System.Text.Json.JsonValueKind.Object } state
                || !state.TryGetProperty("vocabulary", out var vocabProbe)
                || vocabProbe.ValueKind != System.Text.Json.JsonValueKind.Object)
            { Fail("The runtime state has no vocabulary registry after the calibration."); return; }
            if (!vocabProbe.TryGetProperty("urman.chapter1:vocabulary/tt_yul", out var yul)
                || yul.GetProperty("status").GetString() != "guessed")
            { Fail("The 'some' calibration answer did not seed its household vocabulary as guessed."); return; }
            // The answer also sets the profile density; the same content id
            // then reads with authored Tatar, while the fabula id is unchanged.
            if (player.TatarLanguageLevel != "some"
                || !vocabBridge!.ResolveText("urman.chapter1:text/dialogue-gulsina-warning").Contains("Утыр", StringComparison.Ordinal))
            { Fail($"The 'some' answer did not adapt the family dialogue (level={player.TatarLanguageLevel})."); return; }
            // tt_urman is legitimately seeded by the arrival scene's own
            // onEnter; tt_tavysh is not, so it probes the level boundary.
            if (vocabProbe.TryGetProperty("urman.chapter1:vocabulary/tt_tavysh", out var tavysh)
                && tavysh.GetProperty("status").GetString() == "guessed")
            { Fail("The 'some' calibration answer leaked a literary word beyond its level."); return; }
            // A02: the watched ride hands control straight to the village; there
            // is no separate arrival card after it (the skip path shares this).
            var handoverDeadline = Time.GetTicksMsec() + 300_000u;
            while (Time.GetTicksMsec() < handoverDeadline && demo.PrologueActive)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            for (var settle = 0; settle < 12; settle++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var finalZone = (GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge)?.CurrentZoneId;
            GD.Print($"act1-prologue-watch: handover intro={demo.IntroVisible} prologue={demo.PrologueActive} modal={player.ModalOpen} zone={finalZone} at={player.GlobalPosition}");
            if (!sawNightZone || finalZone != "village_day")
            { Fail($"Prologue natural path did not return to the village: sawNight={sawNightZone} zone={finalZone}."); return; }
            if (demo.PrologueActive || demo.IntroVisible || player.ModalOpen)
            { Fail("The watched prologue did not hand control to the player in the village."); return; }
            if (player.GlobalPosition.DistanceTo(new Vector3(0, player.GlobalPosition.Y, 9)) > 3f)
            { Fail($"Player did not resume at the arrival after the prologue: {player.GlobalPosition}."); return; }
            GD.Print("act1-prologue-watch: PASS night-first natural-cue niva-language village-handover arrival-resume");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
            return;
    }

    private async Task<bool> WalkTo(FirstPersonController player, Vector3 destination, string label, float arrivalRadius = .30f,
        bool reportFailure = true)
    {
        var start = player.GlobalPosition;
        var initialDistance = HorizontalDistance(player.GlobalPosition, destination);
        if (initialDistance <= arrivalRadius)
        {
            return true;
        }

        SetYaw(player, YawTo(player.GlobalPosition, destination));
        var maxFrames = Math.Clamp((int)Math.Ceiling(initialDistance / Math.Max(player.WalkSpeed, 0.1f) * 60.0 * 2.4), 120, 1200);
        Input.ActionPress("move_forward");
        var lastBlockingShape = string.Empty;
        try
        {
            var lastProgressFrame = 0;
            var lastDistance = initialDistance;
            for (var frame = 0; frame < maxFrames; frame++)
            {
                var before = player.GlobalPosition;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                for (var i = 0; i < player.GetSlideCollisionCount(); i++)
                {
                    var hit = player.GetSlideCollision(i);
                    lastBlockingShape = $"{(hit.GetCollider() as Node)?.GetPath()} shape={(hit.GetColliderShape() as Node)?.GetPath()} normal={hit.GetNormal()} position={hit.GetPosition()} velocity={player.Velocity} modal={player.ModalOpen}";
                }
                _walkedMeters += HorizontalDistance(before, player.GlobalPosition);
                var currentDistance = HorizontalDistance(player.GlobalPosition, destination);
                if (currentDistance <= arrivalRadius)
                {
                    return true;
                }

                // The connected-world exterior terrain can hold the body on a
                // slope contact for a few frames; only a long no-progress run
                // is a real stall. Nudge the yaw toward the target so the
                // slide direction follows the remaining vector.
                if (Math.Abs(lastDistance - currentDistance) > 0.002f)
                {
                    lastProgressFrame = frame;
                    lastDistance = currentDistance;
                    SetYaw(player, YawTo(player.GlobalPosition, destination));
                }
                else if (frame - lastProgressFrame > 150)
                {
                    break;
                }
            }
        }
        finally
        {
            Input.ActionRelease("move_forward");
            await PhysicsFrames(2);
            // Optional evidence from the real walked positions, never camera
            // teleportation. Ordinary headless smoke pays no rendering cost.
            var captureDirectory = System.Environment.GetEnvironmentVariable("URMAN_WALK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(captureDirectory))
            {
                if (RenderingServer.GetRenderingDevice() is null
                    || !System.IO.Path.IsPathFullyQualified(captureDirectory)
                    || !System.IO.Directory.Exists(captureDirectory))
                {
                    Fail("Walk capture requires a rendering device and an existing absolute output directory.");
                }
                else
                {
                    await Frames(2);
                    RenderingServer.ForceDraw(false);
                    using var image = GetTree().Root.GetTexture().GetImage();
                    var frameName = label.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                    if (image.SavePng(System.IO.Path.Combine(captureDirectory, frameName + ".png")) != Error.Ok)
                        Fail($"Could not save physical walk frame {label}.");
                    GD.Print($"walk-frame: {label} actual-player-position={player.GlobalPosition}");
                    foreach (var node in GetTree().Root.FindChildren("*", "MeshInstance3D", true, false))
                    {
                        var mesh = (MeshInstance3D)node;
                        var meshName = mesh.Name.ToString();
                        if (!mesh.IsVisibleInTree() || mesh.Mesh is null
                            || !new[] { "Fence", "Gate", "Wall", "Door", "Foundation", "Roof" }
                                .Any(kind => meshName.Contains(kind, StringComparison.OrdinalIgnoreCase))) continue;
                        foreach (var h in new[] { .4f, .8f, 1.2f, 1.6f })
                        {
                            var from = mesh.ToLocal(start + Vector3.Up * h);
                            var to = mesh.ToLocal(player.GlobalPosition + Vector3.Up * h);
                            if (!mesh.GetAabb().IntersectsSegment(from, to)) continue;
                            // A wall AABB includes its deliberate doorway;
                            // only an actual triangle crossing is evidence.
                            var faces = mesh.Mesh.GetFaces();
                            var crossed = false;
                            for (var triangle = 0; triangle + 2 < faces.Length; triangle += 3)
                            {
                                if (Geometry3D.SegmentIntersectsTriangle(from, to, faces[triangle],
                                        faces[triangle + 1], faces[triangle + 2]).VariantType == Variant.Type.Nil) continue;
                                crossed = true;
                                break;
                            }
                            if (crossed)
                            {
                                GD.Print($"walk-obstacle: {label} height={h} mesh={mesh.GetPath()}");
                                break;
                            }
                        }
                    }
                }
            }
        }

        _lastWalkFailure = $"could not reach {label}: start={start}, target={destination}, actual={player.GlobalPosition}, remaining={HorizontalDistance(player.GlobalPosition, destination):F2}m, blocker={lastBlockingShape}";
        if (reportFailure) Fail("Physical walkthrough " + _lastWalkFailure + ".");
        return false;
    }

    private async Task<bool> InteractAt(
        FirstPersonController player,
        RayCast3D ray,
        string interactionId,
        float? maxTransitionDistance = null,
        Vector3? approachOverride = null)
    {
        var expected = FindInteraction(interactionId, GetTree().Root);
        if (expected is null)
        {
            Fail($"Physical walkthrough could not locate named InteractionTarget {interactionId} in the connected world.");
            return false;
        }

        if (interactionId == Interaction("route-to-mosque"))
        {
            var main = GetTree().GetFirstNodeInGroup("zone_manager") as Main;
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            if (main?.ConnectedWorld is not { } world || bridge is null)
            { Fail("The physical mosque interaction has no connected world or runtime owner."); return false; }
            if (!await ApproachMosque(player, ray, bridge, world)) return false;
            approachOverride = player.GlobalPosition;
        }
        var targetPosition = expected.GlobalPosition;
        var approach = approachOverride ?? ApproachPosition(player, expected);
        if (expected.GetParent() is StyleBenchmarkZone
            { ZoneKind: StyleBenchmarkZone.BenchmarkKind.HouseOldPc } house
            && expected.Position.DistanceTo(StyleBenchmarkInteriorFactory.PcAnchor) < .50f)
        {
            // A cardinal stand-off from the rotated computer put the returning
            // player behind Gulsina and walked straight through her body. Use
            // the furnished room's central aisle for every interaction at this
            // physical desk, including the later message/article/sketch visits.
            var floorY = house.ToLocal(player.GlobalPosition).Y;
            foreach (var local in new[] { new Vector3(-.15f, floorY, 1.10f),
                new Vector3(StyleBenchmarkInteriorFactory.PcAnchor.X, floorY, -1.15f) })
            {
                var point = house.ToGlobal(local);
                if (!player.CanStandAt(point))
                { Fail($"The computer aisle does not fit a standing player: local={local}, world={point}."); return false; }
                if (!await WalkTo(player, point, $"computer-aisle-{local.X:F2}-{local.Z:F2}")) return false;
            }
            approach = player.GlobalPosition;
        }
        if (interactionId == Interaction("talk-naila") && expected.GetParent() is StyleBenchmarkZone
            { ZoneKind: StyleBenchmarkZone.BenchmarkKind.FapClinic } clinic)
        {
            // Approach the actual reception from the central waiting aisle.
            // The former world-X offset crossed both the bench and the counter.
            var floorY = clinic.ToLocal(player.GlobalPosition).Y;
            foreach (var local in new[] { new Vector3(0, floorY, 1.25f), new Vector3(1f, floorY, 1.25f) })
            {
                var point = clinic.ToGlobal(local);
                if (!player.CanStandAt(point))
                { Fail($"The actual reception aisle does not fit a standing player: local={local}, world={point}."); return false; }
                if (!await WalkTo(player, point, $"naila-reception-aisle-{local.X:F2}-{local.Z:F2}")) return false;
            }
            approach = player.GlobalPosition;
        }
        if (expected.HasMeta("observationReferenceEye") && expected.HasMeta("observationLookAt"))
        {
            var reference = expected.GetMeta("observationReferenceEye").AsVector3();
            approach = new Vector3(reference.X, player.GlobalPosition.Y, reference.Z);
            targetPosition = expected.GetMeta("observationLookAt").AsVector3();
        }
        if (interactionId is "urman.chapter1:interaction/view-arrival-message"
            or "urman.chapter1:interaction/view-arrival-photo" or "urman.chapter1:interaction/arrival-answer-mother")
            approach = new Vector3(3.60f, player.GlobalPosition.Y, 5.05f);
        if (interactionId == Interaction("route-to-fap"))
            approach = new Vector3(expected.GlobalPosition.X, player.GlobalPosition.Y, expected.GlobalPosition.Z + 1.50f);
        if (expected.Name == "HouseExit" && expected.GetParent() is StyleBenchmarkZone
            { ZoneKind: StyleBenchmarkZone.BenchmarkKind.HouseOldPc } room)
        {
            // The house is rotated in the connected world. A world-cardinal
            // stand-off landed on the storage chest, while the diagonal to
            // the door crossed Gulsina. Walk the actual central and door aisles.
            var floorY = room.ToLocal(player.GlobalPosition).Y;
            foreach (var local in new[] { new Vector3(-.15f, floorY, 1.10f),
                new Vector3(StyleBenchmarkInteriorFactory.DoorX, floorY, 1.10f),
                StyleBenchmarkInteriorFactory.Entry with { Y = floorY } })
            {
                var point = room.ToGlobal(local);
                if (!player.CanStandAt(point))
                { Fail($"The actual house exit aisle does not fit a standing player: local={local}, world={point}."); return false; }
                if (!await WalkTo(player, point, $"house-exit-aisle-{local.X:F2}-{local.Z:F2}")) return false;
            }
            approach = player.GlobalPosition;
        }
        if (!await WalkTo(player, approach, $"approach-{interactionId}"))
        {
            return false;
        }

        AimAt(player, targetPosition);
        await PhysicsFrames(3);
        ray.ForceRaycastUpdate();
        var actualCamera = player.GetNode<Camera3D>("Head/Camera3D");
        var rayDirection = (ray.GlobalBasis * ray.TargetPosition).Normalized();
        if (ray.GlobalPosition.DistanceTo(actualCamera.GlobalPosition) > .001f
            || rayDirection.Dot(-actualCamera.GlobalBasis.Z.Normalized()) < .9999f)
        { Fail("The physical interaction ray does not follow the actual camera forward axis."); return false; }
        if (!ray.IsColliding() || ray.GetCollider() is not InteractionTarget target
            || target.InteractionId != interactionId || !target.IsAvailable())
        {
            var collider = ray.GetCollider();
            var actual = collider is InteractionTarget hit
                ? hit.InteractionId
                : collider is GodotObject objectHit
                    ? $"<{objectHit.GetType().Name}:{objectHit.GetInstanceId()}>"
                    : "<none>";
            var expectedInfo = expected is null
                ? "target-node=<none>"
                : $"target-node={expected.GlobalPosition} layer={expected.CollisionLayer} available={expected.IsAvailable()}";
            Fail($"Physical walkthrough ray expected {interactionId}, got {actual}, colliding={ray.IsColliding()}; player={player.GlobalPosition}, connected-world target={targetPosition}, yaw={player.RotationDegrees.Y:F1}, pitch={_lookPitch:F1}, ray={ray.TargetPosition}, {expectedInfo}.");
            return false;
        }

        var beforeInteraction = player.GlobalPosition;
        await PressInteract();
        await Frames(6);
        if (maxTransitionDistance is { } limit)
        {
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            if (bridge is null || string.IsNullOrEmpty(expected.TargetZoneId))
            { Fail("The measured transition has no runtime or physical destination: " + interactionId); return false; }
            for (var frame = 0; frame < 180 && bridge.CurrentZoneId != expected.TargetZoneId; frame++)
                await Frames(1);
            if (bridge.CurrentZoneId != expected.TargetZoneId)
            { Fail("The measured transition did not reach its authored destination: " + interactionId); return false; }
            await PhysicsFrames(2);
            var transitionDistance = HorizontalDistance(beforeInteraction, player.GlobalPosition);
            if (transitionDistance > limit)
            {
                Fail($"The roadside transition moved the player {transitionDistance:F2}m, beyond {limit:F2}m: "
                    + $"{interactionId} from={beforeInteraction} to={player.GlobalPosition}.");
                return false;
            }
            GD.Print($"walk-transition: {interactionId} horizontal-displacement={transitionDistance:F2}m "
                + $"limit={limit:F2}m from={beforeInteraction} to={player.GlobalPosition}");
        }
        return true;
    }

    private async Task<bool> CompletePhysicalArrival(FirstPersonController player, RayCast3D ray, RuntimeBridge bridge)
    {
        foreach (var (action, document) in new[]
                 { ("view-arrival-message", "arrival-mother-message"), ("view-arrival-photo", "arrival-photo-evidence") })
        {
            if (!await InteractAt(player, ray, Interaction(action))) return false;
            AssertDocument(ChapterPrefix + "document/" + document);
            if (HasFailed()) return false;
            CloseDocument();
            await Frames(3);
        }
        if (bridge.IsInteractionAvailable(Interaction("arrival-enter-house")))
        { Fail("Physical arrival skipped the personal answer after reading the two sources."); return false; }
        if (!await InteractAt(player, ray, Interaction("arrival-answer-mother"))
            || !await ChooseVisibleDialogue(bridge, "arrival-reply-help-babai")) return false;
        (GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi)?._UnhandledInput(
            new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(3);
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "arrival_reply_help_babai") != "confirmed"
            || !bridge.IsInteractionAvailable(Interaction("arrival-enter-house")) || player.ModalOpen)
        { Fail("The visible arrival choice did not open the house route and release the player."); return false; }
        GD.Print("act1-physical-arrival: ordinary walk to bench; phone/photo through camera ray and mapped input; visible personal answer");
        return true;
    }

    private async Task<bool> ApproachMosque(FirstPersonController player, RayCast3D ray,
        RuntimeBridge bridge, Act1ConnectedWorld world)
    {
        var room = world.GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex/MosqueInterior");
        if (world.FacilityInteriorAt(player.GlobalPosition) == "mosque")
        {
            if (_mosqueReturnRoute.Count >= 2) return true;
            Fail("The first mosque dialogue began inside without its actual approach.");
            return false;
        }
        var registry = bridge.NotebookSettlement;
        if (registry is null || !registry.TryResolve("ADR-MOSQUE", out var address)
            || !bridge.KnownAddressIds().Contains(address.AddressId)
            || bridge.LocatedAddressIds().Contains(address.AddressId)
            || await bridge.RememberAddressAsync(address.AddressId))
        { Fail("The ordinary mosque return lacks heard-only directions or read a distant plate."); return false; }
        ObserveMosqueAccess("before-heard-load");
        var before = player.GlobalPosition;
        if (!await bridge.SaveSlotAsync("walk-mosque-heard") || !await bridge.LoadSlotAsync("walk-mosque-heard"))
        { Fail("The actually reached heard-only mosque return could not be saved and resumed."); return false; }
        await PhysicsFrames(4);
        ObserveMosqueAccess("after-heard-load");
        if (player.GlobalPosition.DistanceTo(before) > .15f
            || !bridge.KnownAddressIds().Contains(address.AddressId) || bridge.LocatedAddressIds().Contains(address.AddressId))
        { Fail("Loading heard directions moved the walking player or located the unread mosque."); return false; }

        // Use the common, physics-verified access graph only as test input. It is
        // never drawn in the player's notebook, and it cannot grant knowledge.
        // The verifier walks the whole village queue at ~2 ms per physics frame
        // and restarts after a load; the mosque can be late in that queue.
        for (var frame = 0; frame < 3000
            && registry.AccessPoints[address.AccessId].State.StartsWith("pending", StringComparison.Ordinal); frame++)
            await PhysicsFrames(1);
        var access = registry.AccessPoints[address.AccessId];
        var origin = registry.Graph.Nodes.Values.OrderBy(node =>
            node.Position.DistanceXZ(new(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z))).FirstOrDefault();
        if (access.State != "verified" || origin is null
            || origin.Position.DistanceXZ(new(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z)) > .60)
        {
            var verifier = world.GetNodeOrNull<AddressAccessVerifier>("AddressAccessVerification");
            Fail($"The reached street has no verified mosque access: {access.State}; verifier {verifier?.DescribeProgress() ?? "missing"}");
            return false;
        }
        var route = registry.DiagnosticRoute(origin.Id, address.AddressId, SettlementTravelMode.Foot)
            .Select(point => new Vector3((float)point.X, (float)point.Y, (float)point.Z)).ToArray();
        var approach = room.GetMeta("entryApproachPath").AsVector3Array();
        if (route.Length < 2 || approach.Length < 2 || route[^1].DistanceTo(approach[^1]) > .12f)
        { Fail("The common graph does not end at the actual authored mosque stair landing."); return false; }
        var knowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var scene = bridge.ActiveSceneId;
        _mosqueReturnRoute.Clear();
        _mosqueReturnRoute.Add(player.GlobalPosition);
        _mosqueReturnRoute.AddRange(route);
        var stepsBefore = player.StepsClimbed;
        await FollowFullMosqueRoute(player, bridge, route, "mosque-access");
        if (Math.Abs(player.GlobalPosition.Y - room.GlobalPosition.Y) > .08f
            || player.StepsClimbed <= stepsBefore || bridge.LocatedAddressIds().Contains(address.AddressId))
        { Fail("The actual mosque climb failed or approaching automatically read its address."); return false; }
        var sign = FindInteraction("urman.address:read/" + address.AddressId, GetTree().Root);
        if (sign?.GetParent() is not AddressSignVisualComponent plate || plate.AddressId != address.AddressId)
        { Fail("The heard mosque address has no matching real plate target."); return false; }
        if (!await InteractAt(player, ray, sign.InteractionId, approachOverride: player.GlobalPosition)) return false;
        for (var frame = 0; frame < 180 && !bridge.LocatedAddressIds().Contains(address.AddressId); frame++) await Frames(1);
        if (!bridge.LocatedAddressIds().Contains(address.AddressId)
            || bridge.NotebookAddresses().Count(entry => entry.EntryId == "notebook/address/" + address.AddressId) != 1
            || bridge.JournalEntries().Count(entry => entry.EntryId == ChapterPrefix + "knowledge/address-mosque") != 1)
        { Fail("The real mosque plate press did not retain one matching spoken and located address."); return false; }
        if (!await InteractAt(player, ray, sign.InteractionId, approachOverride: player.GlobalPosition)
            || !await bridge.SaveSlotAsync("walk-mosque-plate") || !await bridge.LoadSlotAsync("walk-mosque-plate"))
        { Fail("The actual repeated mosque plate read could not be saved and resumed."); return false; }
        await PhysicsFrames(4);
        if (!bridge.LocatedAddressIds().Contains(address.AddressId)
            || bridge.NotebookAddresses().Count(entry => entry.EntryId == "notebook/address/" + address.AddressId) != 1)
        { Fail("Repeating or loading the read plate lost or duplicated its address."); return false; }

        var entrance = FindInteraction("urman.chapter1:local/mosque/entrance", GetTree().Root);
        var hinge = room.GetNode<Node3D>("MosqueEntranceHinge");
        if (entrance is null || Math.Abs(hinge.RotationDegrees.Y) > .5f)
        { Fail("The first physical mosque visit does not start at its closed manual entrance."); return false; }
        var actionBefore = entrance.GetMeta("lastDoorActionNumber", 0).AsInt32();
        if (!await InteractAt(player, ray, entrance.InteractionId, approachOverride: player.GlobalPosition)) return false;
        for (var frame = 0; frame < 180 && Math.Abs(hinge.RotationDegrees.Y + 95) > .5f; frame++) await PhysicsFrames(1);
        if (Math.Abs(hinge.RotationDegrees.Y + 95) > .5f
            || entrance.GetMeta("lastDoorActionNumber", 0).AsInt32() != actionBefore + 1)
        { Fail("One mapped door press did not open the actual full inward mosque leaf."); return false; }
        foreach (var (point, label) in new[]
            { (new Vector3(4.65f, 0, -.25f), "doorway"), (new Vector3(3.65f, 0, 0), "vestibule"),
              (new Vector3(1.20f, 0, 0), "timur-hall") })
            if (!await WalkMosquePoint(player, room.ToGlobal(point), "mosque-enter-" + label)) return false;
        if (world.FacilityInteriorAt(player.GlobalPosition) != "mosque"
            || bridge.ActiveSceneId != scene || bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() != knowledge)
        { Fail("Reading the address or entering the real mosque supplied an unasked narrative answer."); return false; }
        GD.Print($"walk-mosque: heard/load -> verified graph walked -> physical plate/E/repeat/load -> manual door/E -> hall; address={registry.FormatAddress(address.AddressId)}; steps={player.StepsClimbed - stepsBefore}; no narrative auto-credit");
        return true;

        void ObserveMosqueAccess(string phase)
        {
            // Read the live owner's cached result and the exact support ray used
            // by TrySupport. This observation never runs or commits an audit.
            var current = registry.AccessPoints[address.AccessId];
            var authored = world.AddressApproachPath(address.AccessId);
            var target = authored.Count == 0 ? Act1ConnectedWorld.AddressVector(current.Position) : authored[0];
            using var query = PhysicsRayQueryParameters3D.Create(target + Vector3.Up * .55f,
                target - Vector3.Up * .75f, 3,
                new global::Godot.Collections.Array<Rid> { player.GetRid() });
            var hit = world.GetWorld3D().DirectSpaceState.IntersectRay(query);
            var owner = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as Node;
            var floor = hit.Count == 0 ? "none" : hit["position"].AsVector3().ToString();
            var normal = hit.Count == 0 ? "none" : hit["normal"].AsVector3().ToString();
            GD.Print($"walk-mosque-access-observation: phase={phase} zone={world.ActiveZoneId} "
                + $"exterior={world.GetMeta("activeExteriorAtmosphere", false).AsBool()} "
                + $"access={address.AccessId} cachedState={current.State} approachStart={target} targetY={target.Y:R} "
                + $"floor={floor} normal={normal} supportOwner={owner?.GetPath().ToString() ?? "none"} "
                + $"playerFeet={player.GlobalPosition} grounded={player.IsOnFloor()} "
                + $"standingClear={player.CanStandAt(player.GlobalPosition)}");
        }
    }

    private async Task<bool> LeaveMosque(FirstPersonController player, Act1ConnectedWorld world)
    {
        var room = world.GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex/MosqueInterior");
        if (world.FacilityInteriorAt(player.GlobalPosition) != "mosque" || _mosqueReturnRoute.Count < 2)
        { Fail("The spoken route has no actual mosque entrance walk to retrace."); return false; }
        foreach (var (point, label) in new[]
            { (new Vector3(3.65f, 0, 0), "vestibule"), (new Vector3(4.65f, 0, -.25f), "doorway"),
              (new Vector3(6.65f, 0, -.25f), "landing") })
            if (!await WalkMosquePoint(player, room.ToGlobal(point), "mosque-leave-" + label)) return false;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge
            ?? throw new InvalidOperationException("The actual mosque return lost its runtime bridge.");
        await FollowFullMosqueRoute(player, bridge, _mosqueReturnRoute.AsEnumerable().Reverse().ToArray(), "mosque-return");
        if (world.FacilityInteriorAt(player.GlobalPosition).Length != 0)
        { Fail("The actual mosque return did not regain the village street."); return false; }
        return true;
    }

    private async Task<bool> WalkMosquePoint(FirstPersonController player, Vector3 point, string label)
    {
        var revision = player.PresentationTransformRevision;
        var recoveries = player.FallRecoveries;
        var clamps = player.EdgeClamps;
        if (!await WalkTo(player, point, label, arrivalRadius: .09f)) return false;
        var settled = 0;
        var previousY = player.GlobalPosition.Y;
        for (var frame = 0; frame < 45 && settled < 3; frame++)
        {
            await PhysicsFrames(1);
            var y = player.GlobalPosition.Y;
            settled = player.IsOnFloor() && Math.Abs(player.Velocity.Y) < .05f && Math.Abs(y - previousY) < .002f
                ? settled + 1 : 0;
            previousY = y;
        }
        if (settled < 3 || HorizontalDistance(player.GlobalPosition, point) > .09f
            || player.PresentationTransformRevision != revision || player.FallRecoveries != recoveries
            || player.EdgeClamps != clamps || player.ModalOpen)
        { Fail($"The mosque walk did not settle without transform recovery: {label}; feet={player.GlobalPosition}; expected={point}."); return false; }
        return true;
    }

    private void AimAt(FirstPersonController player, Vector3 target)
    {
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        // The ordinary head is offset forward by 18 cm and changes height while
        // crouching. Re-evaluate its real origin after each look rotation.
        for (var iteration = 0; iteration < 6; iteration++)
        {
            var delta = target - camera.GlobalPosition;
            var horizontal = new Vector2(delta.X, delta.Z).Length();
            var yaw = Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z));
            var pitch = Mathf.RadToDeg(Mathf.Atan2(delta.Y, Math.Max(horizontal, .001f)));
            player.ApplySmokeLook(pitch, yaw);
            _lookPitch = pitch;
        }
    }

    private static Vector3 ApproachPosition(FirstPersonController player, InteractionTarget target)
    {
        var delta = player.GlobalPosition - target.GlobalPosition;
        var direction = Mathf.Abs(delta.Z) >= Mathf.Abs(delta.X)
            ? new Vector3(0f, 0f, Mathf.Sign(delta.Z))
            : new Vector3(Mathf.Sign(delta.X), 0f, 0f);
        if (direction == Vector3.Zero)
        {
            direction = new Vector3(0f, 0f, 1f);
        }

        return new Vector3(
            target.GlobalPosition.X + direction.X * InteractionStandOff,
            player.GlobalPosition.Y,
            target.GlobalPosition.Z + direction.Z * InteractionStandOff);
    }

    private void SetYaw(FirstPersonController player, float yaw)
    {
        player.RotationDegrees = new Vector3(0, yaw, 0);
    }

    private void AssertState(Main main, RuntimeBridge bridge, string zone, string sceneLocalId, string scenePath)
    {
        if (main.ActiveZoneScenePath != scenePath
            || bridge.CurrentZoneId != zone
            || bridge.ActiveSceneId != $"{ChapterPrefix}scene/{sceneLocalId}")
        {
            Fail($"Physical walkthrough state mismatch: zone={bridge.CurrentZoneId}, scene={bridge.ActiveSceneId}, path={main.ActiveZoneScenePath}; expected {zone}/{sceneLocalId}/{scenePath}.");
        }
    }

    private async Task<bool> CloseDialogue(FirstPersonController player)
    {
        var ui = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (ui?.IsOpen == true)
            ui._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(4);
        if (ui?.IsOpen == true || player.ModalOpen)
        { Fail("Closing the visible dialogue did not release physical movement."); return false; }
        return true;
    }

    private async Task<bool> CompareVisibleSources(RuntimeBridge bridge, string action, params string[] sourceIds)
    {
        var journal = GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (journal is null || player is null || player.ModalOpen || sourceIds.Length != 2)
        { Fail("The physical route cannot open its two-source journal comparison: " + action); return false; }
        journal.Open(bridge);
        await Frames(3);
        journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 1;
        for (var slot = 0; slot < 2; slot++)
        {
            var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot + 1}/Source");
            var index = Enumerable.Range(1, picker.ItemCount - 1).FirstOrDefault(i =>
                picker.GetItemMetadata(i).AsString() == sourceIds[slot], -1);
            if (index < 1) { Fail("A real comparison picker lacks its read source: " + sourceIds[slot]); return false; }
            picker.Select(index);
            picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
        }
        await Frames(3);
        var button = journal.GetNode<VBoxContainer>("Screen/Book/Layout/Comparisons/Layout/Hypotheses").GetChildren()
            .OfType<Button>().SingleOrDefault(candidate => candidate.IsVisibleInTree() && !candidate.Disabled
                && candidate.Text == bridge.ResolveText(ChapterPrefix + "text/" + action));
        if (button is null) { Fail("The visible journal has no available comparison: " + action); return false; }
        button.EmitSignal(Button.SignalName.Pressed);
        var firstPicker = journal.GetNode<OptionButton>("Screen/Book/Layout/Comparisons/Layout/Source1/Source");
        for (var frame = 0; frame < 180 && firstPicker.Disabled; frame++) await Frames(1);
        if (firstPicker.Disabled) { Fail("The journal comparison did not finish: " + action); return false; }
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        if (player.ModalOpen) { Fail("The journal retained movement after comparison: " + action); return false; }
        return true;
    }

    private async Task<bool> ChooseVisibleDialogue(RuntimeBridge bridge, string textId)
    {
        var ui = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        var choice = ui?.IsOpen == true
            ? ui.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren().OfType<Button>()
                .FirstOrDefault(button => button.IsVisibleInTree() && !button.Disabled
                    && button.Text == bridge.ResolveText(ChapterPrefix + "text/" + textId))
            : null;
        if (choice is null)
        { Fail("The physical conversation did not expose " + textId); return false; }
        choice.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        return true;
    }

    private void AssertDocument(string documentId)
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        if (ui is null || !ui.IsOpen || ui.OpenDocumentId != documentId)
        {
            Fail($"Physical walkthrough did not open {documentId}.");
        }
    }

    private void CloseDocument()
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        ui?.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
    }

    private async Task PressInteract()
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        await PhysicsFrames(1);
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = false,
            Echo = false
        });
        await PhysicsFrames(2);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true })
                throw new InvalidOperationException("The physical walk was interrupted by an actual pause overlay.");
        }
    }

    // The forest teaser now opens a new game (A02 N2.1); a player skips it with
    // Esc and then confirms the arrival card, so the smoke does the same.
    private async Task SkipPrologueLikePlayer(Act1DemoRoot demo)
    {
        if (!demo.PrologueActive) return;
        demo._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
        for (var frame = 0; frame < 1200 && demo.PrologueActive; frame++) await Frames(1);
        for (var frame = 0; frame < 600 && !demo.IntroVisible; frame++) await Frames(1);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private bool TryReadRoutePoints(InteractionTarget target, out Vector2[] points)
    {
        points = [];
        if (!target.HasMeta("routePoints"))
        {
            Fail($"Physical walkthrough target {target.InteractionId} has no routePoints metadata.");
            return false;
        }

        var raw = target.GetMeta("routePoints").AsString();
        var encodedPoints = raw.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (encodedPoints.Length < 2)
        {
            Fail($"Physical walkthrough target {target.InteractionId} has fewer than two route points: '{raw}'.");
            return false;
        }

        points = new Vector2[encodedPoints.Length];
        for (var index = 0; index < encodedPoints.Length; index++)
        {
            var coordinates = encodedPoints[index].Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (coordinates.Length != 2
                || !float.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)
                || !float.IsFinite(x)
                || !float.IsFinite(z))
            {
                Fail($"Physical walkthrough target {target.InteractionId} has invalid route point '{encodedPoints[index]}'.");
                points = [];
                return false;
            }
            points[index] = new Vector2(x, z);
        }
        return true;
    }

    private async Task<bool> WalkRoutePoints(
        FirstPersonController player,
        Vector2[] route,
        string label)
    {
        for (var index = 0; index < route.Length; index++)
        {
            var point = route[index];
            if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"{label}-{index}"))
            {
                return false;
            }
        }
        return true;
    }

    private static InteractionTarget? FindInteraction(string interactionId, Node node)
    {
        if (node is InteractionTarget target && target.InteractionId == interactionId)
        {
            return target;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindInteraction(interactionId, child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to) =>
        new Vector2(from.X - to.X, from.Z - to.Z).Length();

    private static float YawTo(Vector3 from, Vector3 to) =>
        Mathf.RadToDeg(Mathf.Atan2(-(to.X - from.X), -(to.Z - from.Z)));

    private static string KnowledgeStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/{localId}").GetProperty("status").GetString()!;

    private static string VocabularyStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("vocabulary").GetProperty($"{ChapterPrefix}vocabulary/{localId}").GetProperty("status").GetString()!;

    private static bool NpcState(System.Text.Json.JsonElement state, string localId, string stateKey) =>
        state.GetProperty("npc").GetProperty($"{ChapterPrefix}character/{localId}").GetProperty(stateKey).GetBoolean();

    private bool HasFailed() => _failed;

    private void Fail(string message)
    {
        _failed = true;
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
