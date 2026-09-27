using System.Text.Json.Nodes;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Local production-world checks, not a timed playthrough. Starts an ordinary
/// new game, then records explicit nearby camera fixtures for physical attempts.
/// Run only with protected userdata. No narrative flags are fabricated.
/// </summary>
public partial class Act1CarryInteractionSmokeTest : Node
{
    private FirstPersonController _player = null!;
    private CarryCoordinator _carry = null!;
    private RuntimeBridge _bridge = null!;
    private Camera3D _camera = null!;
    private Act1DemoRoot? _demo;
    private readonly List<string> _checks = new();
    private int _blockedWalkCaptures;
    private string _observedMovementPrompt = string.Empty;
    private bool _observeCarryTurn;
    private Vector2 _carryTurnExpectedMotion;
    private Vector2 _carryTurnDeliveredTotal;
    private int _carryTurnInputCount;
    private int _carryTurnUnhandledCount;
    private ulong _carryTurnUnhandledProcess;
    private readonly List<object> _carryTurnEvents = new();

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            Check(await this.StartThroughMainMenuAsync(_demo), "ordinary New Game");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(4);
            _player = _demo.DemoMain.GetNode<FirstPersonController>("Player");
            _camera = _player.GetNode<Camera3D>("Head/Camera3D");
            _carry = (CarryCoordinator)GetTree().GetFirstNodeInGroup("carry_coordinator");
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            Check(!_player.ModalOpen, "debug-off normal controls after intro");
            await Frames(4);
            Check(await _bridge.SaveSlotAsync("carry-pristine"), "pristine reachable snapshot");
            var scope = System.Environment.GetEnvironmentVariable("URMAN_CARRY_SCOPE") ?? "full";
            Check(scope is "full" or "drain" or "woodpile", "explicit local proof scope: " + scope);
            if (scope == "drain")
            {
                // The same production episode starts from the just-created real
                // save. It has no dependency on the separate pickup fixtures.
                await CheckBoardDrainCrossing();
                exit = 0;
                GD.Print($"act1-carry: PASS drain scope {_checks.Count} local checks; remaining carry and snow checks were not run");
                return;
            }
            if (scope == "woodpile")
            {
                var localShovel = Item("carry-tool-shovel");
                var localSnow = FindUse("woodpile");
                var localAxe = Item("carry-axe");
                var sceneBefore = _bridge.ActiveSceneId;
                await StandFacing(localShovel.GlobalPosition + Vector3.Up * .45f);
                await Press("interact");
                Check(_carry.HeldItem == localShovel, "woodpile scope picks up the actual shovel through ordinary input");
                await ApproachWoodpileFromBathSteps(localSnow, localSnow.GlobalPosition, localShovel, localFixture: true);
                await Capture("04b_snow_over_buried_axe");
                await Press("interact");
                Check(localSnow.Completed && !localAxe.IsConcealed && localAxe.Visible && localAxe.CollisionLayer == 1u
                    && !FindUse("fence").Completed, "the aimed woodpile clearing reveals its axe without clearing the other drift");
                await Capture("05_uncovered_axe");
                await ReturnFromWoodpileBathSteps(localShovel);
                Check(_bridge.ActiveSceneId == sceneBefore, "local snow work and the real return grant no story transition");
                exit = 0;
                GD.Print($"act1-carry: PASS woodpile scope {_checks.Count} local checks; axe pickup and save/load branch were not run");
                return;
            }
            var scene = _bridge.ActiveSceneId;
            var log = Item("carry-log");
            var original = log.GlobalPosition;
            await StandFacing(original + Vector3.Up * log.Height * .5f, log);
            Act1CarryRestProof.Check(_player, _carry, Check);
            await CheckSupportFootprint();
            await Capture("01_log_and_yard");

            // A disposable layer-2 obstruction tests the same layer as rotated
            // facade blockers. It exists only in this test, never in the game.
            // It sits on the actual stance-to-target line so it blocks the aim
            // from whichever side the ring fixture picked.
            var approach = (_player.GlobalPosition - original) with { Y = 0 };
            approach = approach.Normalized();
            var aim = original + Vector3.Up * log.Height * .5f;
            var sight = (aim - _camera.GlobalPosition).Normalized();
            var barrier = new StaticBody3D { Name = "CarryOcclusionFixture", CollisionLayer = 2u,
                CollisionMask = 0u, Position = aim - sight * .63f,
                RotationDegrees = new Vector3(0f, Mathf.RadToDeg(Mathf.Atan2(sight.X, sight.Z)), 0f) };
            barrier.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(1f, 1.8f, .08f) } });
            AddChild(barrier);
            await Frames(3);
            await Press("interact");
            Check(_carry.HeldItem is null && log.State == CarryableProp.CarryState.World,
                "layer-2 wall blocks pickup and aim");
            barrier.QueueFree();
            await Frames(3);
            await CheckNarrowPickupOpening(log, approach);
            await Press("interact");
            Check(_carry.HeldItem == log && log.CollisionLayer == 0, "aimed pickup commits one held item");
            var pickupPrompt = _player.GetNode<Label>("Hud/InteractionPrompt").Text;
            Check(pickupPrompt.Contains(InputBindingService.ActionHint("carry_place", _player.CurrentInputDevice == "gamepad"), StringComparison.Ordinal)
                && !pickupPrompt.Contains("Подойдите с открытой стороны", StringComparison.Ordinal),
                "successful retry immediately replaces the pickup refusal with the ordinary carry controls: " + pickupPrompt);
            await Capture("02_held_log");

            var beforeRejected = _bridge.SelectWorldProps().GetRawText();
            var duplicate = await _bridge.DispatchWorldCustodyAsync(new JsonArray
            {
                new JsonObject { ["op"]="transfer", ["itemId"]="carry-crate", ["fromOwnerId"]="world", ["toOwnerId"]="player" }
            }, new JsonArray { new JsonObject { ["propId"]="carry-crate", ["state"]="held" } });
            Check(!duplicate && beforeRejected == _bridge.SelectWorldProps().GetRawText(),
                "second simultaneous ownership rejected atomically");

            _player.SetModalOpen(true);
            await Press("carry_place");
            await Press("carry_rotate");
            await Press("interact");
            Check(_carry.HeldItem == log && beforeRejected == _bridge.SelectWorldProps().GetRawText(),
                "modal blocks take/place/rotate without hidden progress");
            EnsureNoPauseShell("release the intentional modal fixture");
            _player.SetModalOpen(false);

            // The right-hand ground is beneath the bath entry landing. Use
            // the open yard on the left; the ordinary full-volume placement
            // must still succeed before adding the narrow test obstruction.
            var placementGround = Ground(original + Vector3.Left * 1.25f);
            await CheckNarrowPlacementOpening(log, placementGround);
            await PutAt(placementGround);
            Check(log.State == CarryableProp.CarryState.Placed && log.CollisionLayer == 1u && _carry.HeldItem is null,
                "placement restores physical body");
            await StandFacing(log.GlobalPosition + Vector3.Up * log.Height * .5f, log);
            await Press("interact");
            Check(_carry.HeldItem == log, "placed thing can be taken again");
            await PutAt(placementGround);

            var bucket = Item("carry-bucket");
            var bucketRest = bucket.GlobalPosition;
            await StandFacing(bucketRest + Vector3.Up * bucket.Height * .5f);
            await Press("interact");
            Check(_carry.HeldItem == bucket && bucket.Class == CarryableProp.ItemClass.Bucket
                && CustodyOwner(bucket.ItemId) == "player",
                "the bucket also leaves its actual authored rest through the shared full-volume pickup");
            await PutAt(Ground(bucketRest));
            Check(bucket.State == CarryableProp.CarryState.Placed && bucket.CollisionLayer == 1u
                && CustodyOwner(bucket.ItemId) == "world", "setting the bucket back restores its real body and world custody");
            await Capture("02b_bucket_back_on_ground");

            await CheckPortableCrateStand();
            await CheckCrateAccessAlternatives();
            await CheckBoardDrainCrossing();

            await CheckSnowGapBlocked();
            var shovel = Item("carry-tool-shovel");
            await StandFacing(shovel.GlobalPosition + Vector3.Up * .45f, shovel);
            await Press("interact");
            Check(_carry.HeldItem == shovel, "shovel is physically carried, not a remote switch");
            var fence = FindUse("fence");
            var woodpile = FindUse("woodpile");
            await StandFacing(fence.GlobalPosition, fence, Vector3.Right);
            await Capture("03_snow_before");
            await Press("interact");
            Check(fence.Completed && !woodpile.Completed, "only the aimed snow opening changes");
            var physicalDrift = _demo.DemoMain.ConnectedWorld!.FindChild("Ex05PassageDriftBarrier", true, false) as StaticBody3D;
            Check(physicalDrift?.CollisionLayer == 0, "clearing removes the actual passage barrier");
            await Capture("04_snow_cleared");
            await WalkLocal(new(-26.2f, 0, -2.85f), "walk through the cleared snow gap with the same shovel");
            await WalkLocal(new(-23.95f, 0, -2.85f), "return through the same cleared service gap");
            await ApproachWoodpileFromBathSteps(woodpile, woodpile.GlobalPosition, shovel, localFixture: true);
            await Capture("04b_snow_over_buried_axe");
            await Press("interact");
            var axe = Item("carry-axe");
            Check(woodpile.Completed && !axe.IsConcealed && axe.Visible && axe.CollisionLayer == 1u,
                "second aimed clearing reveals its real object");
            await Capture("05_uncovered_axe");
            await ReturnFromWoodpileBathSteps(shovel);
            var bath = _demo.DemoMain.ConnectedWorld!.GetNode<Node3D>("Act1CoreWorldGreybox/BabaiBathhouse");
            Aim(Ground(bath.GetMeta("entryApproach").AsVector3() + bath.GlobalBasis.Z.Normalized()));
            await Frames(4);
            Check(_carry.TryPlacement(shovel, out var shovelRest, out var shovelReason),
                "the real ground beyond the lower stair supports the complete shovel: " + shovelReason);
            await Press("carry_place");
            Check(_carry.HeldItem is null && shovel.State == CarryableProp.CarryState.Placed
                && shovel.GlobalPosition.DistanceTo(shovelRest) < .03f, "ordinary placement frees both hands outside the bath steps");
            await ApproachWoodpileFromBathSteps(axe, axe.GlobalPosition + Vector3.Up * axe.Height * .5f, null, localFixture: false);
            await Press("interact");
            Check(_carry.HeldItem == axe, "uncovered axe can be taken");
            await ReturnFromWoodpileBathSteps(axe);
            Check(await _bridge.SaveSlotAsync("carry-cleared-held"), "cleared yard and held object saved together");

            Check(await _bridge.LoadSlotAsync("carry-pristine"), "load pristine snapshot");
            await Frames(6);
            Check(_carry.HeldItem is null && axe.IsConcealed && !axe.Visible && !woodpile.Completed && !fence.Completed
                && log.GlobalPosition.DistanceTo(original) < .015f && physicalDrift?.CollisionLayer == 1u,
                "missing deviations reset transforms, custody, concealment and snow");
            Check(await _bridge.LoadSlotAsync("carry-cleared-held"), "load cleared/held snapshot");
            await Frames(6);
            Check(_carry.HeldItem == axe && !axe.IsConcealed && axe.Visible && axe.CollisionLayer == 0
                && woodpile.Completed && fence.Completed && physicalDrift?.CollisionLayer == 0u,
                "restored held find is visible and the physical snow clearing persists");
            await Capture("06_loaded_held_axe");
            Check(_bridge.ActiveSceneId == scene, "optional physical work grants no story transition");

            // EX06: the portable lantern is a light, not a scanner. Both lit
            // details are authored marks in the world; in the dark the ordinary
            // interaction offers nothing, and a lit lantern - carried or left
            // standing on a support - makes the reading available.
            // Free both hands for the lamp. The axe goes back down at the first
            // yard spot with real room, since the earlier fixtures already
            // occupy the exact places used before.
            var dropped = false;
            foreach (var spot in new[]
                     {
                         new Vector3(-30.0f, 0f, 7.6f), new Vector3(-31.6f, 0f, 1.2f),
                         new Vector3(-29.4f, 0f, -1.4f), new Vector3(-33.4f, 0f, 7.2f)
                     })
            {
                await StandFacing(Ground(spot));
                var carried = _carry.HeldItem;
                if (carried is null || !_carry.TryPlacement(carried, out _, out _)) continue;
                await Press("carry_place");
                if (_carry.HeldItem is null) { dropped = true; break; }
            }
            Check(dropped, "hands free before the lamp");
            var lantern = Item("carry-lantern");
            var tin = LightDetail("paint tin at the entrance step");
            var joint = LightDetail("street wall scratch joint");
            await StandFacing(tin.GlobalPosition, tin);
            Check(tin.HeldByGate && !tin.IsAvailable(), "an unlit authored detail offers no reading");
            await Press("interact");
            Check(!Found("babai-yard-porch-paint-tin"), "the dark reading grants no knowledge");
            Check(_player.GetNodeOrNull<Label>("Hud/InteractionPrompt")?.Text
                == _bridge.ResolveText("urman.chapter1:text/needs-light"), "the dark reading names what is missing");

            await StandFacing(lantern.GlobalPosition + Vector3.Up * lantern.Height * .5f, lantern);
            await Press("interact");
            Check(_carry.HeldItem == lantern, "the lamp is physically carried");
            await StandFacing(Ground(new(-30.2f, 0f, 2.2f)));
            _player.ApplySmokeLook(-42f, _player.RotationDegrees.Y);
            await Frames(3);
            await Press("interact");
            Check(lantern.LightOn && lantern.State == CarryableProp.CarryState.Held,
                "ordinary interact switches the carried lamp");

            await Capture("07_lantern_dark_step");
            await StandFacing(tin.GlobalPosition, tin);
            Check(tin.IsAvailable(), "a lit lantern opens the authored reading");
            await Capture("08_lantern_lit_detail");
            await Press("interact");
            await Frames(6);
            Check(Found("babai-yard-porch-paint-tin"), "the lit detail grants its authored knowledge");
            Check(_bridge.JournalEntries().Any(entry => entry.EntryId
                    == "urman.chapter1:knowledge/discovery-babai-yard-porch-paint-tin"),
                "the lit reading writes one journal card");

            // A wall between the lamp and an unread mark must not read through
            // it. The check uses the second detail: once a reading is taken the
            // authored condition closes it, so it could never reopen.
            await StandFacing(joint.GlobalPosition, joint);
            Check(joint.IsAvailable(), "the carried lamp opens the second reading");
            var line = new Vector3(joint.GlobalPosition.X - lantern.GlobalPosition.X, 0f,
                joint.GlobalPosition.Z - lantern.GlobalPosition.Z).Normalized();
            var shade = new StaticBody3D { Name = "LanternOcclusionFixture", CollisionLayer = 2u, CollisionMask = 0u,
                Position = (lantern.GlobalPosition + joint.GlobalPosition) * .5f,
                RotationDegrees = new Vector3(0f, Mathf.RadToDeg(Mathf.Atan2(line.X, line.Z)), 0f) };
            shade.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(1.6f, 1.6f, .1f) } });
            AddChild(shade);
            await Frames(3);
            Check(!joint.IsAvailable(), "a wall between the lamp and the mark blocks the reading");
            shade.QueueFree();
            // Queued deletion is applied on the idle frame, which need not fall
            // between two physics frames in a headless run.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Frames(3);
            Check(joint.IsAvailable(), "removing the wall restores the reading");

            // Left on the ground the lamp keeps burning and frees both hands
            // for the second, further reading.
            var outward = _player.GlobalPosition - joint.GlobalPosition;
            outward.Y = 0f;
            await PutAt(Ground(joint.GlobalPosition + outward.Normalized() * 1.2f));
            Check(_carry.HeldItem is null && lantern.LightOn && lantern.State == CarryableProp.CarryState.Placed,
                "a lamp left standing keeps burning with free hands");
            await StandFacing(joint.GlobalPosition, joint);
            Check(joint.IsAvailable(), "the standing lamp lights the second reading");
            await Capture("09_lantern_left_standing");
            await Press("interact");
            await Frames(6);
            Check(Found("babai-yard-entry-wall-joint"), "the second lit reading grants its knowledge");

            Check(await _bridge.SaveSlotAsync("carry-light"), "lit yard saved with both readings");
            Check(await _bridge.LoadSlotAsync("carry-light"), "load the lit snapshot");
            await Frames(6);
            Check(Found("babai-yard-porch-paint-tin") && Found("babai-yard-entry-wall-joint")
                && lantern.LightOn && lantern.State == CarryableProp.CarryState.Placed,
                "both light readings and the standing lamp survive the save/load");

            Check(await _bridge.StartNewGameAsync(), "new runtime session on persistent world");
            await Frames(8);
            Check(_carry.HeldItem is null && axe.IsConcealed && !woodpile.Completed && !fence.Completed
                && !Found("babai-yard-porch-paint-tin") && !Found("babai-yard-entry-wall-joint"),
                "New Game clears local consequences and re-registers custody");
            await StandFacing(original + Vector3.Up * log.Height * .5f, log);
            await Press("interact");
            Check(_carry.HeldItem == log, "pickup still works after New Game registration");
            exit = 0;
            GD.Print($"act1-carry: PASS {_checks.Count} meaningful local checks; no human duration/art claim");
        }
        catch (Exception error)
        {
            GD.PrintErr($"act1-carry: FAIL {error}\nHUD: {_player?.GetNodeOrNull<Label>("Hud/InteractionPrompt")?.Text}\n{_carry?.DescribeAim()}");
        }
        finally
        {
            foreach (var action in new[] { "interact", "carry_place", "carry_rotate", "move_forward", "move_backward" }) Input.ActionRelease(action);
            if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo);
            GetTree().Quit(exit);
        }
    }

    private CarryableProp Item(string id) => _carry.Items.Single(item => item.ItemId == id);
    private InteractionTarget LightDetail(string role) => _demo!.DemoMain.ConnectedWorld!
        .FindChildren("*", "StaticBody3D", true, false).OfType<InteractionTarget>()
        .Single(node => node.HasMeta("lightGateRole") && node.GetMeta("lightGateRole").AsString() == role);
    private bool Found(string slug) => _bridge.SelectRuntimeState().GetProperty("knowledge")
        .TryGetProperty("urman.chapter1:knowledge/discovery-" + slug, out var entry)
        && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";

    private async Task CheckSnowGapBlocked()
    {
        var drift = _demo!.DemoMain.ConnectedWorld!.FindChild("Ex05PassageDriftBarrier", true, false) as StaticBody3D;
        Check(drift is not null && drift.CollisionLayer == 1u && _carry.HeldItem is null,
            "the unworked service drift has its real body and the blocking attempt has empty hands");
        var start = Ground(new(-23.95f, 0, -2.85f));
        Check(_player.CanStandAt(start), "the snow gap has a clear supported street approach");
        _player.ApplyZoneSpawn(start, 90);
        await Frames(6);
        _player.ApplySmokeLook(0, 90);
        var revision = _player.PresentationTransformRevision;
        var before = _bridge.SelectWorldProps().GetRawText();
        var touchedDrift = false;
        Input.ActionPress("move_forward");
        try
        {
            for (var frame = 0; frame < 55; frame++)
            {
                await Frames(1);
                for (var contact = 0; contact < _player.GetSlideCollisionCount(); contact++)
                    touchedDrift |= _player.GetSlideCollision(contact).GetCollider() == drift;
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        await Frames(3);
        Check(touchedDrift && _player.GlobalPosition.X > -25.28f
            && _player.PresentationTransformRevision == revision && _bridge.SelectWorldProps().GetRawText() == before,
            $"the visible snow mound physically blocks the uncleared service gap without moving a prop: {_player.GlobalPosition}; drift contact={touchedDrift}");
        await WalkLocal(start, "step back from the snow drift before fetching the shovel");
    }

    private async Task CheckNarrowPickupOpening(CarryableProp prop, Vector3 approach)
    {
        // The ray fits through this visible slot while the unscaled item's
        // body does not. The fixture changes neither custody nor the prop pose.
        var fixture = new Node3D { Name = "CarryNarrowOpeningFixture",
            Position = prop.GlobalPosition + approach * .32f,
            Rotation = new(0, Mathf.Atan2(approach.X, approach.Z), 0) };
        foreach (var side in new[] { -1, 1 })
        {
            var size = new Vector3(.50f, 2.20f, .04f);
            var jamb = new StaticBody3D { Name = "VisibleJamb" + side, CollisionLayer = 2u,
                CollisionMask = 0u, Position = new(side * .285f, 1.10f, 0) };
            jamb.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            jamb.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("ad8653") } });
            fixture.AddChild(jamb);
        }
        AddChild(fixture);
        try
        {
            await Frames(3);
            using var ray = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition,
                _camera.GlobalPosition - _camera.GlobalBasis.Z * 3f, 7u);
            ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
            var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            Check(hit.Count > 0 && hit["collider"].AsGodotObject() == prop,
                "the narrow opening leaves the actual pickup ray on the visible prop");
            var before = _bridge.SelectWorldProps().GetRawText();
            var pose = prop.GlobalTransform;
            await Press("interact");
            Check(_carry.HeldItem is null && prop.State == CarryableProp.CarryState.World
                && prop.GlobalTransform.IsEqualApprox(pose) && CustodyOwner(prop.ItemId) == "world"
                && _bridge.SelectWorldProps().GetRawText() == before,
                "an opening narrower than the prop rejects pickup without moving it or transferring custody");
            await Capture("01b_narrow_pickup_opening_refused");
        }
        finally
        {
            fixture.QueueFree();
            await Frames(3);
        }
    }

    private async Task CheckNarrowPlacementOpening(CarryableProp prop, Vector3 target)
    {
        // Leave space for a separate obstruction between two clear full-body
        // poses. The ordinary placement probe sits at a fixed 1.15 m and the
        // fenced yard refuses a farther stance, so the log is turned across the
        // view until its long side fits the measured gap; the assertion below
        // still fails honestly when the yard cannot host the fixture.
        await StandFacing(target, distance: 1.65f);
        Check(_carry.HeldItem == prop && _carry.HasValidHeldPose,
            "the real held log stays validly held before the narrow fixture");
        var destination = Vector3.Zero;
        var reason = string.Empty;
        Check(_carry.TryPlacement(prop, out destination, out reason), "record the unchanged supported placement: " + reason);
        for (var turn = 0; turn < 12 && !NarrowFixtureFits(prop, destination); turn++)
        {
            await Press("carry_rotate");
            Check(_carry.TryPlacement(prop, out destination, out reason),
                "the held log keeps its supported placement while it turns: " + reason);
        }
        var direction = ((destination - _camera.GlobalPosition) with { Y = 0 }).Normalized();
        var heldDistance = (prop.GlobalPosition - _camera.GlobalPosition).Dot(direction);
        var destinationDistance = (destination - _camera.GlobalPosition).Dot(direction);
        var room = destinationDistance - heldDistance;
        Check(room > 2f * ProjectedHalfExtent(prop, direction) + .06f,
            $"the fixture fits strictly between the held pose and destination: held={heldDistance:F4} destination={destinationDistance:F4} room={room:F4} extent={ProjectedHalfExtent(prop, direction):F4} prop={prop.Size} camera={_camera.GlobalPosition}");
        var centre = _camera.GlobalPosition + direction * ((heldDistance + destinationDistance) * .5f);
        centre.Y = Math.Min(prop.GlobalPosition.Y, destination.Y) - .05f;
        var fixture = new Node3D { Name = "CarryNarrowPlacementFixture", Position = centre,
            Rotation = new(0, Mathf.Atan2(direction.X, direction.Z), 0) };
        foreach (var side in new[] { -1, 1 })
        {
            var size = new Vector3(.50f, 2.20f, .04f);
            var jamb = new StaticBody3D { Name = "VisiblePlacementJamb" + side, CollisionLayer = 2u,
                CollisionMask = 0u, Position = new(side * .285f, 1.10f, 0) };
            jamb.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            jamb.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("ad8653") } });
            fixture.AddChild(jamb);
        }
        AddChild(fixture);
        try
        {
            await Frames(3);
            var exclude = new global::Godot.Collections.Array<Rid> { prop.GetRid(), _player.GetRid() };
            using var shape = new BoxShape3D { Size = prop.Size };
            using var volume = new PhysicsShapeQueryParameters3D
            { Shape = shape, CollisionMask = 3u, Exclude = exclude, Margin = .001f };
            var space = _player.GetWorld3D().DirectSpaceState;
            foreach (var feet in new[] { prop.GlobalPosition, destination })
            {
                volume.Transform = new(prop.GlobalBasis, feet + Vector3.Up * prop.Height * .5f);
                Check(space.IntersectShape(volume, 1).Count == 0,
                    "the narrow placement fixture leaves the full held and destination bodies clear");
            }
            using var ray = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition,
                destination + Vector3.Up * .035f, 3u, exclude);
            var sight = space.IntersectRay(ray);
            Check(sight.Count == 0 || sight["position"].AsVector3().DistanceTo(destination) <= .10f,
                "the actual placement sightline passes through the visible slot");
            volume.Transform = new(prop.GlobalBasis, prop.GlobalPosition + Vector3.Up * prop.Height * .5f);
            volume.Motion = destination - prop.GlobalPosition;
            var fractions = space.CastMotion(volume);
            Check(fractions.Length == 2 && fractions[0] < .95f,
                "the independent full body cast hits a jamb between the two clear endpoint poses");
            Check(!_carry.TryPlacement(prop, out var refusedPoint, out reason)
                && refusedPoint.DistanceTo(destination) < .005f && reason == "Предмет не проходит к этой опоре",
                "only the blocked transfer path refuses the same supported destination: " + reason);
            await CheckPlacementPreview(false, reason);
            var before = _bridge.SelectWorldProps().GetRawText();
            var pose = prop.GlobalTransform;
            await Press("carry_place");
            Check(_carry.HeldItem == prop && prop.State == CarryableProp.CarryState.Held
                && prop.GlobalPosition.DistanceTo(pose.Origin) < .015f && prop.GlobalBasis.IsEqualApprox(pose.Basis)
                && CustodyOwner(prop.ItemId) == "player" && _bridge.SelectWorldProps().GetRawText() == before,
                "mapped placement through the narrow slot preserves the held pose, custody and saved props");
            await Capture("02a_narrow_placement_refused");
        }
        finally
        {
            fixture.QueueFree();
            await Frames(3);
        }
        Check(_carry.TryPlacement(prop, out var clearPoint, out reason)
            && clearPoint.DistanceTo(destination) < .005f,
            "removing only the narrow fixture restores the same ordinary placement: " + reason);
    }

    private async Task CheckBoardDrainCrossing()
    {
        Check(await _bridge.LoadSlotAsync("carry-pristine"), "board crossing begins from the ordinary pristine save");
        await Frames(7);
        var crossing = _demo!.DemoMain.ConnectedWorld!.FindChild("BabaiDrainBoardCrossing", true, false) as Node3D
            ?? throw new InvalidOperationException("The production board drain is missing.");
        var board = Item("carry-board");
        var authoredBoard = board.GlobalPosition;
        var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var scene = _bridge.ActiveSceneId;
        Vector3 Point(string key) => crossing.GetMeta(key).AsVector3();
        var start = Point("pickupApproach");
        var near = Point("nearApproach");
        var far = Point("farApproach");
        var bearing = Point("bearingPlane");
        Check(_player.CanStandAt(start), "the drain starts from a supported, clear nearby stance");
        _player.ApplyZoneSpawn(start, 0);
        await Frames(6);
        GD.Print($"act1-carry: explicit drain start fixture {start}; pickup, approach, crossing and return use normal input");
        await WalkLocal(near, "approach the drain before placing its board");
        var withoutBoard = await WalkLocal(far, "observe the same drain crossing without a board", observeObstruction: true);
        GD.Print($"act1-carry-drain-alternative: crossedWithoutBoard={withoutBoard}");
        await Capture("07e_drain_without_board");
        if (!withoutBoard)
        {
            // The board's drying battens occupy the other end of the bed.
            await WalkLocal(crossing.ToGlobal(new(-2.5f, 0, 0)), "leave the open end of the drain opposite its drying battens");
            await WalkLocal(crossing.ToGlobal(new(-2.5f, 0, -2.7f)), "go around the near bank on ordinary ground");
        }
        await WalkLocal(near, "return from the unbridged drain");
        await WalkLocal(start, "return to the board's original pickup approach");
        Aim(board.GlobalPosition + Vector3.Up * board.Height * .5f);
        await Press("interact");
        Check(_carry.HeldItem == board, "take the single board from its visible drying battens");
        // The other use (workshop trestles) leaves this same board at yaw zero.
        // Reach that angle and recover the drain's equivalent longitudinal axis
        // using only the existing fifteen-degree rotation input.
        for (var turn = 0; turn < 3; turn++) await Press("carry_rotate");
        Check(Math.Abs(Mathf.Wrap(board.YawDegrees, -180f, 180f)) < .01f,
            "the board can take the workshop's zero-degree orientation in open space");
        await Capture("08_board_held_zero_yaw");
        for (var turn = 0; turn < 9; turn++) await Press("carry_rotate");
        Check(Math.Abs(Mathf.Wrap(board.YawDegrees - crossing.GlobalRotationDegrees.Y, -90f, 90f)) < .01f,
            "normal rotation recovers the crossing axis after the board's other use");
        await Capture("08_board_held_crossing_yaw");
        await WalkLocal(near, "carry the board to the near drain approach");
        await WalkLocal(crossing.ToGlobal(new(0, 0, -1.35f)), "walk up the stone bank with the board");

        Aim(Point("unsupportedAim"));
        await Frames(3);
        var beforeBadAttempt = _bridge.SelectWorldProps().GetRawText();
        Check(!_carry.TryPlacement(board, out _, out _), "an offset plank with one end in the bed is refused");
        await Press("carry_place");
        Check(_carry.HeldItem == board && _bridge.SelectWorldProps().GetRawText() == beforeBadAttempt,
            "the failed crossing attempt keeps the held board and its saved state");
        Aim(Point("placementAim"));
        await Frames(3);
        Check(_carry.TryPlacement(board, out var rest, out var reason), "the two actual banks accept the ordinary board: " + reason);
        await Press("carry_place");
        Check(_carry.HeldItem is null && board.State == CarryableProp.CarryState.Placed
            && board.AssemblyKey.Length == 0 && board.GlobalPosition.DistanceTo(rest) < .03f
            && Math.Abs(board.GlobalPosition.Y - bearing.Y - .006f) < .02f,
            "free placement makes a physical crossing without a new assembly or quest flag");
        foreach (var end in new[] { -1, 1 })
        foreach (var side in new[] { -1, 1 })
        {
            var foot = board.GlobalPosition + board.GlobalBasis * new Vector3(side * board.Size.X * .43f, 0, end * board.Size.Z * .44f);
            using var ray = PhysicsRayQueryParameters3D.Create(foot + Vector3.Up * .03f, foot - Vector3.Up * .10f, 3u);
            ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid(), board.GetRid() };
            var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var collider = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as Node;
            var expectedBank = crossing.ToLocal(foot).Z < 0 ? "NearStoneBank" : "FarStoneBank";
            Check(collider?.GetParent()?.Name == expectedBank
                && hit["normal"].AsVector3().Y > .95f,
                $"board end {end}, corner {side} rests on its own visible bank");
        }
        await Capture("08a_board_drain_from_approach");
        await WalkLocal(board.GlobalPosition, "walk onto the plank over the open drain bed");
        Check(_player.IsOnFloor() && _carry.IsSupportingSomething(board)
            && Math.Abs(_player.GlobalPosition.Y - board.GlobalPosition.Y - board.Height) < .06f,
            "the actual plank supports the player above the channel");
        var savedFoot = _player.GlobalPosition;
        var savedBoard = board.GlobalPosition;
        var beforeUnderfoot = _bridge.SelectWorldProps().GetRawText();
        Aim(board.GlobalPosition + board.GlobalBasis.Z * .25f + Vector3.Up * board.Height);
        await Press("interact");
        Check(_carry.HeldItem is null && _bridge.SelectWorldProps().GetRawText() == beforeUnderfoot,
            "the player cannot remove the crossing from under their own feet");
        Check(await _bridge.SaveSlotAsync("carry-drain-on-board"), "save the placed board and supported player midway across");
        await Capture("08b_board_drain_underfoot");
        await WalkLocal(far, "leave the board over the opposite flush approach");
        // Go around the north end of the visible Frontage_18 fence; the old
        // straight waypoint at (-7.3, 11.3) requested walking through it.
        var route = new[] { Point("destinationApproach"), Ground(new(-14.8f, 0, 11.3f)),
            Ground(new(-10.8f, 0, 11.3f)), Ground(new(-10.8f, 0, 9f)), Ground(new(-5.3f, 0, 9f)) };
        foreach (var point in route) await WalkLocal(point, "follow the household path north of the existing lateral dwelling");
        await Capture("08c_board_far_side_path");
        Check(board.GlobalPosition.DistanceTo(savedBoard) < .02f, "the crossing remains behind after reaching the far-side household path");
        foreach (var point in route.Reverse().Skip(1)) await WalkLocal(point, "return from the existing service yard");
        await WalkLocal(far, "return to the far bank without moving the plank");
        await WalkLocal(board.GlobalPosition, "cross back on the same real plank");
        await WalkLocal(near, "return down the near bank");

        Check(await _bridge.LoadSlotAsync("carry-drain-on-board"), "reload the snapshot made over the channel");
        await Frames(7);
        Check(_player.GlobalPosition.DistanceTo(savedFoot) < .07f && board.GlobalPosition.DistanceTo(savedBoard) < .02f
            && _player.IsOnFloor() && _carry.IsSupportingSomething(board),
            "loading restores the player's physical support and the placed crossing together");
        await WalkLocal(near, "walk off the restored crossing");
        await WalkLocal(crossing.ToGlobal(new(0, 0, -1.35f)), "approach the empty board from the bank");
        Aim(board.GlobalPosition + Vector3.Up * board.Height * .5f);
        await Press("interact");
        Check(_carry.HeldItem == board, "the empty crossing can be dismantled and its one board reused");
        await WalkLocal(near, "carry the removed plank down the bank");
        var directReturn = await WalkLocal(start, "try the direct return beside the visible gate post",
            ordinaryMouseLook: true, observeObstruction: true);
        if (!directReturn)
        {
            var request = _player.Transform.Basis * Vector3.Forward * _player.WalkSpeed;
            var refusal = _carry.DescribeBlockedHeldMovement(request, (float)_player.GetPhysicsProcessDeltaTime());
            Check(refusal.Contains("Col_Gate_HouseA1_Post", StringComparison.Ordinal),
                "the observed refusal identifies the real gate post, not an unknown movement blocker");
            Check(_observedMovementPrompt.Contains("Предмет упирается", StringComparison.Ordinal),
                "ordinary HUD explains the carried object's obstruction and the available retreat");
            await RetreatWithBoard(board, .9f);
            await WalkLocal(start, "return the same reusable plank after an ordinary backward retreat", ordinaryMouseLook: true);
        }
        // The old raised centre is now air. Aim at the visible ground between
        // the battens; the ordinary footprint query finds their two real tops.
        Aim(Ground(authoredBoard));
        await Frames(3);
        Check(_carry.TryPlacement(board, out var stored, out reason), "the original battens also accept ordinary replacement: " + reason);
        await Press("carry_place");
        Check(_carry.HeldItem is null && board.GlobalPosition.DistanceTo(stored) < .02f,
            "one physical board is set down after return, with no duplicate in the channel");
        Check(await _bridge.SaveSlotAsync("carry-drain-returned"), "save the dismantled crossing and returned plank");
        Check(await _bridge.LoadSlotAsync("carry-drain-returned"), "restore the returned object state");
        await Frames(7);
        Check(board.GlobalPosition.DistanceTo(stored) < .02f && _carry.HeldItem is null
            && _bridge.ActiveSceneId == scene && _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == knowledge,
            "return, reuse and save grant no unseen story knowledge or scene transition");
        Check(await _bridge.LoadSlotAsync("carry-pristine"), "restore pristine carryables before the independent snow checks");
        await Frames(7);
    }

    private async Task CheckCrateAccessAlternatives()
    {
        var crate = Item("carry-gap-crate");
        var authored = crate.GlobalPosition;
        var authoredBasis = crate.GlobalBasis;
        var entry = new Vector3(41.5f, 0, -24.6f);
        var goal = new Vector3(40.8f, 0, -28.4f);
        var around = new[] { new Vector3(43.2f, 0, -24.6f), new Vector3(43.2f, 0, -28.4f),
            new Vector3(41.5f, 0, -28.4f), goal };
        foreach (var moveFirst in new[] { false, true })
        {
            Check(await _bridge.LoadSlotAsync("carry-pristine"), "each crate access order starts from the ordinary pristine save");
            await Frames(7);
            Check(crate.GlobalPosition.DistanceTo(authored) < .02f && crate.GlobalBasis.IsEqualApprox(authoredBasis)
                && crate.State == CarryableProp.CarryState.World && !crate.HasOwnDeviation
                && !_bridge.SelectWorldProps().TryGetProperty(crate.ItemId, out _)
                && CustodyOwner(crate.ItemId) == "world",
                "a pristine save has no legacy coordinate deviation and restores the supported exterior crate with world custody");
            CheckCrateBaseSupport(crate);
            var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
            var activeScene = _bridge.ActiveSceneId;
            var start = Ground(entry);
            Check(_player.CanStandAt(start), "the crate access route starts on supported ordinary ground");
            _player.ApplyZoneSpawn(start, 0);
            await Frames(6);
            GD.Print($"act1-carry: explicit local start fixture {start}; subsequent alternative paths use normal input");
            Aim(crate.GlobalPosition + Vector3.Up * crate.Height * .5f);
            await Frames(3);
            await Capture(moveFirst ? "07d_crate_before_carrying" : "07c_crate_before_walking_around");

            // Reproduce the barrier with the player's own capsule. A ray or
            // distance test alone would not prove that this crate blocks access.
            Aim(goal + Vector3.Up * 1.5f);
            var touchedCrate = false;
            Input.ActionPress("move_forward");
            try
            {
                for (var frame = 0; frame < 50; frame++)
                {
                    await Frames(1);
                    for (var contact = 0; contact < _player.GetSlideCollisionCount(); contact++)
                        touchedCrate |= _player.GetSlideCollision(contact).GetCollider() == crate;
                }
            }
            finally { Input.ActionRelease("move_forward"); }
            await Frames(3);
            Check(touchedCrate && _player.GlobalPosition.Z > authored.Z - .20f
                && crate.GlobalPosition.DistanceTo(authored) < .02f,
                "the unmoved crate physically prevents the direct crossing");
            await WalkLocal(entry, "return from the blocked direct attempt");

            if (moveFirst)
            {
                await MoveAccessCrate(crate);
                await WalkLocal(goal, "cross the gap after carrying its crate aside");
                await WalkLocal(entry, "return through the cleared gap");
            }
            foreach (var point in around) await WalkLocal(point, "walk through the open snow beside the service timber path");
            if (!moveFirst) Check(crate.GlobalPosition.DistanceTo(authored) < .02f,
                "the longer walking alternative succeeds with the crate left in the gap");
            var savedCrate = crate.GlobalPosition;
            var savedCrateBasis = crate.GlobalBasis;
            var savedProps = _bridge.SelectWorldProps().GetRawText();
            var savedCustody = CustodyOwner(crate.ItemId);
            Check(await _bridge.SaveSlotAsync("carry-gap-alternative"), "save at the common destination between solutions");
            Check(await _bridge.LoadSlotAsync("carry-gap-alternative"), "load between the two access solutions");
            await Frames(7);
            Check(crate.GlobalPosition.DistanceTo(savedCrate) < .02f && crate.GlobalBasis.IsEqualApprox(savedCrateBasis)
                && _bridge.SelectWorldProps().GetRawText() == savedProps && CustodyOwner(crate.ItemId) == savedCustody
                && new Vector2(_player.GlobalPosition.X - goal.X, _player.GlobalPosition.Z - goal.Z).Length() < .15f,
                "save restores the reached exterior destination and the complete crate pose, deviation and custody");
            foreach (var point in around.Reverse().Skip(1)) await WalkLocal(point, "return through the same open snow beside the crate");
            await WalkLocal(entry, "return to the original access decision");
            if (!moveFirst)
            {
                await MoveAccessCrate(crate);
                await WalkLocal(goal, "use the direct gap after first learning the walking alternative");
                await WalkLocal(entry, "return through the now cleared gap");
            }
            Check(_bridge.ActiveSceneId == activeScene
                && _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == knowledge,
                "both access orders and the common destination grant no unobserved story knowledge");
            await Capture(moveFirst ? "07b_crate_then_bypass" : "07a_bypass_then_crate");
        }
        Check(await _bridge.LoadSlotAsync("carry-pristine"), "restore the yard before independent shovel checks");
        await Frames(7);
    }

    private async Task MoveAccessCrate(CarryableProp crate)
    {
        await WalkLocal(new(41.5f, 0, -25.65f), "approach the service-path crate on foot");
        Aim(crate.GlobalPosition + Vector3.Up * crate.Height * .5f);
        await Frames(3);
        await Press("interact");
        Check(_carry.HeldItem == crate, "take the actual barrier crate with the normal aimed interaction");
        await WalkLocal(new(41.5f, 0, -24.1f), "carry the crate away from the timber approach");
        foreach (var candidate in new[] { new Vector3(43.0f, 0, -23.5f), new Vector3(42.9f, 0, -23.1f),
                     new Vector3(42.7f, 0, -23.5f) })
        {
            Aim(Ground(candidate));
            await Frames(3);
            if (!_carry.TryPlacement(crate, out var pose, out _)) continue;
            await Press("carry_place");
            Check(_carry.HeldItem is null && crate.GlobalPosition.DistanceTo(pose) < .03f,
                "the moved crate is set down on a real supported surface");
            await WalkLocal(new(41.5f, 0, -24.6f), "return to the timber approach with empty hands");
            return;
        }
        throw new InvalidOperationException("The access crate has no safe ordinary nearby placement: " + _carry.DescribeAim());
    }

    private async Task<bool> WalkLocal(Vector3 goal, string label, bool ordinaryMouseLook = false, bool observeObstruction = false)
    {
        _observedMovementPrompt = string.Empty;
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var start = _player.GlobalPosition;
        var arrived = false;
        var stalledFrames = 0;
        var lastMovingPosition = start;
        var contacts = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            for (var frame = 0; frame < 600; frame++)
            {
                var delta = goal - _player.GlobalPosition;
                if (new Vector2(delta.X, delta.Z).Length() < .10f) { arrived = true; break; }
                var requestedYaw = Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z));
                if (ordinaryMouseLook)
                {
                    // Exercise the normal mouse event path at the same physical
                    // turn and route that previously jammed the held board. A
                    // rapid look must not teleport the prop or disable its sweep.
                    if (frame == 0)
                        Check(Input.MouseMode == Input.MouseModeEnum.Captured && !_player.ModalOpen
                            && _player.MouseSensitivity > 0, "the return turn uses captured ordinary mouse input");
                    await TurnCarryWithOrdinaryMouse(requestedYaw, label, frame == 0);
                }
                else _player.ApplySmokeLook(0, requestedYaw);
                Input.ActionPress("move_forward");
                await Frames(1);
                for (var contact = 0; contact < _player.GetSlideCollisionCount(); contact++)
                {
                    var hit = _player.GetSlideCollision(contact);
                    if (hit.GetNormal().Y > .9f) continue;
                    var body = hit.GetCollider() as Node;
                    var shape = hit.GetColliderShape() as Node;
                    contacts.Add($"body={body?.GetPath()} shape={shape?.GetPath()} normal={hit.GetNormal()} at={hit.GetPosition()}");
                }
                if (_player.GlobalPosition.DistanceTo(lastMovingPosition) > .02f)
                { stalledFrames = 0; lastMovingPosition = _player.GlobalPosition; }
                else
                {
                    stalledFrames++;
                    if (_carry.HeldItem is not null && stalledFrames is 3 or 30 or 89)
                    {
                        var speed = _player.IsCrouching ? _player.WalkSpeed * .58f : _player.WalkSpeed;
                        var requested = _player.Transform.Basis * Vector3.Forward * speed;
                        GD.Print($"act1-carry-held-movement: label={label} stalled={stalledFrames} feet={_player.GlobalPosition} goal={goal} "
                            + $"input={Input.GetVector("move_left", "move_right", "move_forward", "move_backward")} velocity={_player.Velocity} "
                            + $"camera={_camera.GlobalTransform} " + _carry.DescribeBlockedHeldMovement(requested, (float)_player.GetPhysicsProcessDeltaTime()));
                    }
                    if (stalledFrames >= 90)
                    {
                        // Observe while the actual input is still held. A
                        // delayed screenshot must not expire a correct hint.
                        _observedMovementPrompt = _player.GetNode<Label>("Hud/InteractionPrompt").Text;
                        GD.Print($"act1-carry-observed-obstruction-hud: {_observedMovementPrompt}");
                        break;
                    }
                }
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        await Frames(3);
        if (!arrived)
        {
            try { await Capture(++_blockedWalkCaptures == 1 ? "09_carry_walk_blocked" : $"09_carry_walk_blocked_{_blockedWalkCaptures:00}"); }
            catch (Exception captureError) { GD.Print("act1-carry-blocked-capture: " + captureError); }
        }
        Check((arrived || observeObstruction) && revision == _player.PresentationTransformRevision && recoveries == _player.FallRecoveries
            && clamps == _player.EdgeClamps,
            $"{label}: actual input {start} -> {_player.GlobalPosition}; no teleport/recovery; goal {goal}"
            + (!arrived && observeObstruction ? "; observed refusal, destination NOT reached" : string.Empty)
            + (arrived ? string.Empty : $"; contacts {string.Join(" | ", contacts.TakeLast(6))}; step {_player.LastStepRejection}"));
        return arrived;
    }

    public override void _Input(InputEvent inputEvent) => ObserveCarryTurn("input", inputEvent);
    public override void _UnhandledInput(InputEvent inputEvent) => ObserveCarryTurn("unhandled", inputEvent);

    private void ObserveCarryTurn(string stage, InputEvent inputEvent)
    {
        if (!_observeCarryTurn || inputEvent is not InputEventMouseMotion motion) return;
        if (stage == "input") _carryTurnDeliveredTotal += motion.ScreenRelative;
        if (motion.ScreenRelative.IsEqualApprox(_carryTurnExpectedMotion))
        {
            if (stage == "input") _carryTurnInputCount++;
            else { _carryTurnUnhandledCount++; _carryTurnUnhandledProcess = Engine.GetProcessFrames(); }
        }
        if (_carryTurnEvents.Count < 8)
            _carryTurnEvents.Add(new { stage, relative = motion.Relative.ToString(), screenRelative = motion.ScreenRelative.ToString(),
                process = Engine.GetProcessFrames(), physics = Engine.GetPhysicsFrames(),
                yaw = _player.RotationDegrees.Y, pitch = _player.CapturePortableTransform().RotationDegrees.X });
        // Observe only: the real FirstPersonController still owns the event.
    }

    private async Task TurnCarryWithOrdinaryMouse(float requestedYaw, string label, bool firstTurn)
    {
        var before = _player.CapturePortableTransform();
        var yawDelta = Mathf.Wrap(requestedYaw - _player.RotationDegrees.Y, -180f, 180f);
        var pitch = (float)before.RotationDegrees.X;
        if (Math.Abs(yawDelta) < .0001f && Math.Abs(pitch) < .0001f) return;
        var started = Time.GetTicksMsec(); var startedProcess = Engine.GetProcessFrames();
        var startedPhysics = Engine.GetPhysicsFrames(); var frames = 0; var ready = false;
        _carryTurnExpectedMotion = new(-yawDelta / _player.MouseSensitivity, pitch / _player.MouseSensitivity);
        _carryTurnDeliveredTotal = Vector2.Zero;
        _carryTurnInputCount = 0; _carryTurnUnhandledCount = 0; _carryTurnUnhandledProcess = 0;
        _carryTurnEvents.Clear(); _observeCarryTurn = true;
        try
        {
            var center = (Vector2)DisplayServer.WindowGetSize() * .5f;
            using var motion = new InputEventMouseMotion { Position = center, GlobalPosition = center,
                Relative = _carryTurnExpectedMotion, ScreenRelative = _carryTurnExpectedMotion };
            Input.ParseInputEvent(motion);
            // One PhysicsFrame is not an input-delivery barrier. Do not enqueue
            // another copy of the turn while the first event is still pending.
            do
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); frames++;
                EnsureNoPauseShell("deliver the ordinary carry turn");
                var actualPitch = (float)_player.CapturePortableTransform().RotationDegrees.X;
                ready = _carryTurnInputCount == 1 && _carryTurnUnhandledCount == 1
                    && Engine.GetProcessFrames() > _carryTurnUnhandledProcess
                    && Math.Abs(Mathf.Wrap(_player.RotationDegrees.Y - requestedYaw, -180f, 180f)) < .15f
                    && Math.Abs(actualPitch) < .15f;
            } while (!ready && Time.GetTicksMsec() - started < 2000 && frames < 180);
            ready &= _carryTurnDeliveredTotal.IsEqualApprox(_carryTurnExpectedMotion);
        }
        finally
        {
            _observeCarryTurn = false;
            if (firstTurn || !ready)
                GD.Print("act1-carry-ordinary-turn: " + System.Text.Json.JsonSerializer.Serialize(new {
                    label, ready, requestedYaw, yawDelta, expectedScreenRelative = _carryTurnExpectedMotion.ToString(),
                    deliveredScreenTotal = _carryTurnDeliveredTotal.ToString(), delivered = _carryTurnInputCount,
                    unhandled = _carryTurnUnhandledCount, startedProcess, startedPhysics,
                    unhandledProcess = _carryTurnUnhandledProcess, process = Engine.GetProcessFrames(), physics = Engine.GetPhysicsFrames(),
                    frames, milliseconds = Time.GetTicksMsec() - started, before, after = _player.CapturePortableTransform(),
                    focus = GetWindow().HasFocus(), mouseMode = Input.MouseMode.ToString(), modal = _player.ModalOpen,
                    vehicle = _player.VehicleControlled, treePaused = GetTree().Paused, canProcess = _player.CanProcess(),
                    held = _carry.HeldItem?.GlobalTransform.ToString(), validHeld = _carry.HasValidHeldPose,
                    events = _carryTurnEvents.ToArray() }));
        }
        if (!ready || firstTurn)
            Check(ready, "one ordinary mouse event is delivered and applies the requested carry turn within the unchanged .15 degree tolerance");
    }

    private async Task RetreatWithBoard(CarryableProp board, float distance)
    {
        var start = _player.GlobalPosition;
        var look = _player.RotationDegrees.Y;
        var boardYaw = board.YawDegrees;
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var reached = false;
        try
        {
            Input.ActionPress("move_backward");
            for (var frame = 0; frame < 240; frame++)
            {
                await Frames(1);
                Check(_carry.HeldItem == board && _carry.HasValidHeldPose,
                    "ordinary retreat retains the same physically valid board");
                var displacement = _player.GlobalPosition - start;
                if (new Vector2(displacement.X, displacement.Z).Length() >= distance)
                { reached = true; break; }
            }
        }
        finally { Input.ActionRelease("move_backward"); }
        await Frames(3);
        await Capture("09a_board_backward_retreat");
        Check(reached && _player.IsOnFloor() && revision == _player.PresentationTransformRevision
            && recoveries == _player.FallRecoveries && clamps == _player.EdgeClamps
            && Mathf.Abs(Mathf.Wrap(_player.RotationDegrees.Y - look, -180f, 180f)) < .01f
            && Mathf.Abs(Mathf.Wrap(board.YawDegrees - boardYaw, -180f, 180f)) < .01f,
            $"ordinary backward input moves the board around the post: {start} -> {_player.GlobalPosition}; no new pose, turn or recovery");
    }

    private string? CustodyOwner(string itemId) => _bridge.SelectRuntimeState().GetProperty("world.custody")
        .EnumerateArray().Single(item => item.GetProperty("itemId").GetString() == itemId)
        .GetProperty("custodyOwnerId").GetString();

    private void CheckCrateBaseSupport(CarryableProp crate)
    {
        var exclude = new global::Godot.Collections.Array<Rid> { crate.GetRid(), _player.GetRid() };
        foreach (var sx in new[] { -.48f, .48f })
        foreach (var sz in new[] { -.48f, .48f })
        {
            var corner = crate.GlobalTransform * new Vector3(crate.Size.X * sx, 0, crate.Size.Z * sz);
            var query = PhysicsRayQueryParameters3D.Create(corner + Vector3.Up * .08f, corner + Vector3.Down * .10f, 3u, exclude);
            var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
            var body = hit.Count > 0 ? hit["collider"].AsGodotObject() as Node : null;
            var gap = hit.Count > 0 ? corner.Y - hit["position"].AsVector3().Y : float.PositiveInfinity;
            Check(body?.Name.ToString() == "FapServicePathCollision" && gap >= -.004f && gap <= .020f,
                $"authored crate corner rests on the actual service planks: {corner}, body {body?.GetPath()}, gap {gap:F4}m");
        }
    }

    private async Task CheckPortableCrateStand()
    {
        var crate = Item("carry-crate");
        var lamp = Item("carry-lantern");
        foreach (var destination in new[] { new Vector3(-27.0f, 0, 4.5f), new Vector3(-27.8f, 0, 6.4f) })
        {
            await StandFacing(crate.GlobalPosition + Vector3.Up * crate.Height * .5f);
            await Press("interact");
            Check(_carry.HeldItem == crate, "carry the reusable crate stand to another place");
            if (destination.X == -27.0f)
            {
                await StandFacing(Ground(new(-30.4f, 0, 6.6f)));
                var beforeUnevenAttempt = _bridge.SelectWorldProps().GetRawText();
                Check(!_carry.TryPlacement(crate, out _, out var unevenReason)
                    && unevenReason == "Край предмета останется без опоры",
                    "the previously rejected sloping patch still requires a level support");
                await CheckPlacementPreview(false, unevenReason);
                await Capture("07_preview_uneven_crate");
                await Press("carry_place");
                Check(_carry.HeldItem == crate && beforeUnevenAttempt == _bridge.SelectWorldProps().GetRawText(),
                    "refused uneven placement preserves the carried crate before trying a level patch");
            }
            await PutAt(Ground(destination), verifyPreview: destination.X == -27.0f);
            await StandFacing(lamp.GlobalPosition + Vector3.Up * lamp.Height * .5f);
            await Press("interact");
            Check(_carry.HeldItem == lamp, "take the same lamp for the moved stand");
            await StandFacing(crate.GlobalPosition + Vector3.Up * crate.Height);
            Check(_carry.TryPlacement(lamp, out var lampRest, out var reason), $"crate lid supports lamp: {reason}");
            await Press("carry_place");
            Check(_carry.HeldItem is null && lamp.GlobalPosition.DistanceTo(lampRest) < .03f
                && Math.Abs(lamp.GlobalPosition.Y - crate.GlobalPosition.Y - crate.Height) < .025f,
                "lamp rests on the visible crate lid using ordinary placement");
            var before = _bridge.SelectWorldProps().GetRawText();
            await StandFacing(crate.GlobalPosition + Vector3.Up * crate.Height * .35f);
            await Press("interact");
            Check(_carry.HeldItem is null && before == _bridge.SelectWorldProps().GetRawText(),
                "occupied portable stand cannot be removed from under its lamp");
            Check(await _bridge.SaveSlotAsync("carry-portable-stand"), "crate and resting lamp save together");
            Check(await _bridge.LoadSlotAsync("carry-portable-stand"), "restore the moved portable stand");
            await Frames(6);
            Check(lamp.GlobalPosition.DistanceTo(lampRest) < .03f && crate.CollisionLayer == 1u
                && lamp.CollisionLayer == 1u && _carry.IsSupportingSomething(crate),
                "loaded stand retains visible support, lamp position and physical ownership");
            await StandFacing(lamp.GlobalPosition + Vector3.Up * lamp.Height * .5f);
            await Press("interact");
            Check(_carry.HeldItem == lamp, "remove the supported thing before moving the crate again");
            // The former temporary patch is now inside the authored bathhouse.
            // Reuse the clear standing patch proved beside the portable stand;
            // PutAt still checks the lamp's full support and swept placement.
            await PutAt(Ground(new(-27f, 0, 5.75f)));
        }
    }

    private async Task CheckSupportFootprint()
    {
        // A disposable collision fixture probes the end of the real board.
        // It never enters runtime custody or changes an authored item's pose;
        // this checks support geometry, not a reachable investigation state.
        var board = Item("carry-board");
        var at = board.GlobalPosition + board.GlobalBasis * new Vector3(0, 0, board.Size.Z * .40f)
            - Vector3.Up * .106f;
        var support = CarryableProp.Create("fixture-end-support", "test support", CarryableProp.ItemClass.Light,
            at, 0, "705943", "wood", CarryableProp.ItemKind.Log, new(.13f, .10f, .13f));
        AddChild(support);
        try
        {
            await Frames(3);
            Check(_carry.IsSupportingSomething(support), "off-centre board end keeps its support occupied");
            support.GlobalPosition += board.GlobalBasis.X * (board.Size.X + .30f);
            await Frames(3);
            Check(!_carry.IsSupportingSomething(support), "adjacent support is not falsely treated as under the board");
        }
        finally
        {
            support.QueueFree();
            await Frames(2);
        }
    }

    private YardUseTarget FindUse(string id) => _carry.GetChildren().OfType<YardTool>()
        .SelectMany(tool => tool.Targets).Single(target => target.UseId == id);
    private static Vector3 Ground(Vector3 point) => new(point.X,
        AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .025f, point.Z);

    private void DescribeWoodpileTerrainCandidates(Vector3 target)
    {
        var space = _player.GetWorld3D().DirectSpaceState;
        var excluded = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        using var shape = new CapsuleShape3D { Radius = _player.BodyRadius, Height = _player.StandingBodyHeight - .015f };
        using var query = new PhysicsShapeQueryParameters3D { Shape = shape, CollisionMask = _player.CollisionMask,
            Exclude = excluded, Margin = .002f };
        foreach (var direction in new[] { Vector3.Forward, Vector3.Back, Vector3.Left, Vector3.Right,
            new Vector3(-1, 0, -1).Normalized(), new Vector3(1, 0, -1).Normalized(),
            new Vector3(-1, 0, 1).Normalized(), new Vector3(1, 0, 1).Normalized() })
        {
            var ground = Ground(target + direction * 1.25f);
            query.Transform = new(Basis.Identity, ground + Vector3.Up * (_player.StandingBodyHeight * .5f + .01f));
            var contacts = space.IntersectShape(query, 16).Select(hit =>
                $"{(hit["collider"].AsGodotObject() as Node)?.GetPath()} shape={hit["shape"].AsInt32()}").ToArray();
            using var ray = PhysicsRayQueryParameters3D.Create(ground + Vector3.Up * 2.5f, ground - Vector3.Up * .15f, 3u, excluded);
            var support = space.IntersectRay(ray);
            var supportPoint = support.Count > 0 ? support["position"].AsVector3() : ground;
            GD.Print($"act1-carry-woodpile-candidate: terrainFeet={ground} direction={direction} terrainFits={_player.CanStandAt(ground)} "
                + $"contacts=[{string.Join(" | ", contacts)}] firstSurface={(support.Count > 0 ? (support["collider"].AsGodotObject() as Node)?.GetPath().ToString() : "none")} "
                + $"surfacePoint={supportPoint} raisedFits={support.Count > 0 && _player.CanStandAt(supportPoint + Vector3.Up * .025f)} diagnosticOnly=true");
        }
    }

    private async Task ApproachWoodpileFromBathSteps(Node3D target, Vector3 aimPoint, CarryableProp? carried, bool localFixture)
    {
        var bath = _demo!.DemoMain.ConnectedWorld!.GetNode<Node3D>("Act1CoreWorldGreybox/BabaiBathhouse");
        var outside = bath.GetMeta("entryApproach").AsVector3();
        var clear = bath.GetMeta("entryClearDoor").AsVector3();
        var bypass = bath.GetMeta("entryDoorBypass").AsVector3Array();
        Check(bypass.Length == 3 && _carry.HeldItem == carried, "woodpile approach uses the existing bath stair route and actual carried item");
        DescribeWoodpileTerrainCandidates(aimPoint);
        if (localFixture)
        {
            Check(_player.CanStandAt(outside), "the named ground approach below the bath steps fits the actual standing body");
            GD.Print($"act1-carry: explicit local fixture at existing bath entryApproach={outside}; upper landing reached only by walking");
            _player.ApplyZoneSpawn(outside, 0);
            await Frames(6);
        }
        else Check(new Vector2(_player.GlobalPosition.X - outside.X, _player.GlobalPosition.Z - outside.Z).Length() < .12f,
            "return for the axe begins where the previous real descent ended");
        await SettleWoodpileFeet();
        Check(Math.Abs(_player.GlobalPosition.Y - Ground(_player.GlobalPosition).Y) < .06f,
            "woodpile route begins on the real ground below the existing lower stair");
        await WalkLocal(clear, "carry the tool up the existing bath stair flight", ordinaryMouseLook: true);
        await WalkLocal(bypass[0], "follow the existing lower turn toward the supported side landing", ordinaryMouseLook: true);
        await WalkLocal(bypass[1], "reach the woodpile view by walking onto the actual upper landing", ordinaryMouseLook: true);
        await SettleWoodpileFeet();
        CheckWoodpileLandingSupport(bath);
        Check(_carry.HeldItem == carried && (carried is null || _carry.HasValidHeldPose),
            "the stair approach preserves actual custody and the complete held-object pose");
        Aim(aimPoint);
        await Frames(4);
        using var sight = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition,
            _camera.GlobalPosition - _camera.GlobalBasis.Z * 2.7f, 7u,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(sight);
        GD.Print($"act1-carry-woodpile-landing: feet={_player.GlobalPosition} bathLocal={bath.ToLocal(_player.GlobalPosition)} "
            + $"camera={_camera.GlobalTransform} target={target.GetPath()} targetPoint={aimPoint} centerDistance={_camera.GlobalPosition.DistanceTo(aimPoint)} "
            + $"rayOwner={(hit.Count > 0 ? (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() : "none")} held={_carry.HeldItem?.ItemId} validHeld={_carry.HasValidHeldPose}");
        await Capture(target is YardUseTarget ? "04a_woodpile_actual_landing" : "05a_axe_actual_landing");
        Check(hit.Count > 0 && hit["collider"].AsGodotObject() == target,
            "the real 2.7m interaction ray from the walked landing reaches the actual snow or axe before any stair collider");
    }

    private async Task ReturnFromWoodpileBathSteps(CarryableProp carried)
    {
        var bath = _demo!.DemoMain.ConnectedWorld!.GetNode<Node3D>("Act1CoreWorldGreybox/BabaiBathhouse");
        var bypass = bath.GetMeta("entryDoorBypass").AsVector3Array();
        await WalkLocal(bypass[0], "return from the woodpile along the same supported side of the bath steps", ordinaryMouseLook: true);
        await WalkLocal(bath.GetMeta("entryClearDoor").AsVector3(), "return to the original lower stair flight with the same carried item", ordinaryMouseLook: true);
        await WalkLocal(bath.GetMeta("entryApproach").AsVector3(), "descend the actual bath stairs back to their ground approach", ordinaryMouseLook: true);
        await SettleWoodpileFeet();
        Check(_carry.HeldItem == carried && _carry.HasValidHeldPose
            && Math.Abs(_player.GlobalPosition.Y - Ground(_player.GlobalPosition).Y) < .06f,
            "the woodpile return ends grounded with the same real item and valid full-body carry pose");
    }

    private async Task SettleWoodpileFeet()
    {
        var stable = 0;
        for (var frame = 0; frame < 30 && stable < 3; frame++)
        {
            await Frames(1);
            stable = _player.IsOnFloor() && Math.Abs(_player.Velocity.Y) < .05f ? stable + 1 : 0;
        }
        var canStand = _player.CanStandAt(_player.GlobalPosition);
        Check(stable == 3 && canStand, "woodpile route settles on a real support with an unobstructed standing capsule"
            + $" (stableFrames={stable} canStand={canStand} feet={_player.GlobalPosition} velocity={_player.Velocity} onFloor={_player.IsOnFloor()})");
    }

    private void CheckWoodpileLandingSupport(Node3D bath)
    {
        using var ray = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Down, 3u,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        foreach (var x in new[] { -.10f, 0, .10f }) foreach (var z in new[] { -.18f, 0, .18f })
        {
            var point = _player.GlobalPosition + bath.GlobalBasis.X.Normalized() * x + bath.GlobalBasis.Z.Normalized() * z;
            ray.From = point + Vector3.Up * .20f; ray.To = point - Vector3.Up * .25f;
            var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var owner = hit.Count > 0 ? hit["collider"].AsGodotObject() as Node : null;
            var floor = hit.Count > 0 ? hit["position"].AsVector3() : Vector3.Zero;
            GD.Print($"act1-carry-woodpile-foot: offset=({x},{z}) owner={owner?.GetPath()} floor={floor}");
            Check(owner is not null && owner.Name.ToString().StartsWith("BathEntryLanding", StringComparison.Ordinal)
                && hit["normal"].AsVector3().Y > .98f && Math.Abs(floor.Y - bath.GlobalPosition.Y) < .02f,
                "both feet stand on the real upper landing, not terrain beneath it or a selected roof");
        }
    }

    // Local reproduction, not a traversal measurement. A fixed metre behind
    // every target can put the eye inside a yard canopy post; the physics
    // push-out then varies per machine and the aim ray clips the post instead
    // of the target. Walk a small ring around the target and keep the first
    // supported stance whose line of sight actually reaches it; never disable a
    // production collider to make an item reachable. A target with no reachable
    // stance is a real defect and fails instead of passing on a lucky offset.
    private async Task StandFacing(Vector3 target, Node? expected = null, Vector3? side = null, float distance = 1.25f)
    {
        var refusals = new List<string>();
        foreach (var direction in Stances(side))
        foreach (var reach in Reaches(distance))
        {
            var from = Ground(target + direction * reach);
            // The real capsule has to fit: a stance overlapping a canopy post
            // gets pushed out over the next frames and the push differs per run.
            if (!_player.CanStandAt(from)) { refusals.Add($"no-stance dir={direction} reach={reach:F2}"); continue; }
            _player.ApplyZoneSpawn(from, 0);
            await Frames(4);
            if (!await Settle()) { refusals.Add($"unsettled dir={direction} reach={reach:F2}"); continue; }
            Aim(target);
            await Frames(3);
            if (!SightReaches(target, expected)) { refusals.Add($"blocked dir={direction} reach={reach:F2}"); continue; }
            GD.Print($"act1-carry: local camera fixture {_player.GlobalPosition} target={target} side={direction} reach={reach}");
            return;
        }
        throw new InvalidOperationException($"No standing fixture reaches {target}."
            + (expected is null ? string.Empty : $" Expected {expected.Name} on the aim ray.")
            + $" Refusals ({refusals.Count}): {string.Join("; ", refusals.Take(8))}");
    }

    // A yard fixture has to stand at a natural interacting distance, and not
    // every prop is reachable from the same one. Search the near band only:
    // every reach stays inside the coordinator's own reach of 2.7 m.
    private static IEnumerable<float> Reaches(float preferred)
    {
        yield return preferred;
        foreach (var reach in new[] { 1.25f, .95f, 1.6f, 2.0f })
            if (!Mathf.IsEqualApprox(reach, preferred)) yield return reach;
    }

    // A stance teleports the capsule a couple of centimetres above its ground;
    // placing before the fall finishes moves the held item after the placement
    // point was measured. Wait for a grounded, still player instead.
    private async Task<bool> Settle()
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            await Frames(1);
            if (_player.IsOnFloor() && Mathf.Abs(_player.Velocity.Y) < .5f) return true;
        }
        return false;
    }

    private static IEnumerable<Vector3> Stances(Vector3? preferred)
    {
        if (preferred is { } first) yield return first.Normalized();
        yield return Vector3.Back;
        yield return Vector3.Right;
        yield return Vector3.Left;
        yield return Vector3.Forward;
        yield return new Vector3(1f, 0f, 1f).Normalized();
        yield return new Vector3(-1f, 0f, 1f).Normalized();
        yield return new Vector3(1f, 0f, -1f).Normalized();
        yield return new Vector3(-1f, 0f, -1f).Normalized();
    }

    // Reachable means the first thing on the segment to the target is the
    // target itself; a wall or post standing closer rejects the stance. An
    // empty segment is clear: the fixture targets float slightly above their
    // support, so a segment to one can legitimately end in open air.
    private bool SightReaches(Vector3 target, Node? expected)
    {
        var from = _camera.GlobalPosition;
        var ray = PhysicsRayQueryParameters3D.Create(from, target, 7u);
        ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (hit.Count == 0) return expected is null;
        var collider = hit["collider"].AsGodotObject();
        if (expected is not null)
            // A prop may own a separate body node for its shape.
            return collider is Node node
                && (ReferenceEquals(node, expected) || expected.IsAncestorOf(node));
        // Aiming at the centre of a body hits its own near face first; the
        // near face sits up to the body's own size in front of that point.
        if (collider is CarryableProp prop
            && prop.GlobalPosition.DistanceTo(target) <= prop.Size.Length() * .51f) return true;
        if (collider is YardUseTarget use && use.GlobalPosition.DistanceTo(target) < .025f) return true;
        return from.DistanceTo(hit["position"].AsVector3()) >= from.DistanceTo(target) - .12f;
    }

    private void Aim(Vector3 target)
    {
        // Turning the real head also moves its anatomical forward offset. Solve
        // against the resulting camera pose, then the checks use its actual ray.
        for (var i = 0; i < 6; i++)
        {
            var d = target - _camera.GlobalPosition;
            _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(d.Y, new Vector2(d.X, d.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-d.X, -d.Z)));
        }
    }

    // The narrow-placement fixture fits only when the held and destination
    // bodies leave it a slot. Both bodies use the prop's real basis, so measure
    // its half extent along the placement direction instead of its diagonal.
    private static float ProjectedHalfExtent(CarryableProp prop, Vector3 direction)
    {
        var half = prop.Size * .5f;
        var basis = prop.GlobalBasis;
        return Math.Abs(basis.X.Dot(direction)) * half.X
            + Math.Abs(basis.Y.Dot(direction)) * half.Y
            + Math.Abs(basis.Z.Dot(direction)) * half.Z;
    }

    private bool NarrowFixtureFits(CarryableProp prop, Vector3 destination)
    {
        var direction = ((destination - _camera.GlobalPosition) with { Y = 0 }).Normalized();
        var room = (destination - _camera.GlobalPosition).Dot(direction)
            - (prop.GlobalPosition - _camera.GlobalPosition).Dot(direction);
        // The disposable fixture is .04 m thick; the margin keeps it clear of
        // both full bodies without assuming a particular prop length.
        return room > 2f * ProjectedHalfExtent(prop, direction) + .06f;
    }

    private async Task PutAt(Vector3 ground, bool verifyPreview = false)
    {
        await StandFacing(ground);
        var held = _carry.HeldItem ?? throw new InvalidOperationException("Placement fixture has no carried item.");
        Check(_carry.TryPlacement(held, out var point, out var reason), $"supported placement: {reason}");
        if (verifyPreview)
        {
            await CheckPlacementPreview(true);
            await Capture("07_preview_supported_crate");
        }
        await Press("carry_place");
        // The preview is measured one physics frame before the commit; a
        // windowed frame can settle the capsule a couple of centimetres, so the
        // tolerance covers that without hiding a placement on the wrong support.
        Check(_carry.HeldItem is null && held.GlobalPosition.DistanceTo(point) < .06f,
            "mapped placement commits checked transform");
    }

    private async Task CheckPlacementPreview(bool valid, string reason = "")
    {
        var held = _carry.HeldItem;
        var before = _bridge.SelectWorldProps().GetRawText();
        var prompt = _player.GetNode<Label>("Hud/InteractionPrompt");
        var binding = InputBindingService.ActionHint("carry_place", _player.CurrentInputDevice == "gamepad");
        bool Matches() => prompt.Text.Contains(binding, StringComparison.Ordinal)
            && prompt.Text.Contains(valid ? "поставить здесь" : "нельзя поставить:", StringComparison.Ordinal)
            && (valid || prompt.Text.Contains(reason, StringComparison.Ordinal));
        // An earlier action may still be showing its short-lived feedback.
        // Wait for the real HUD; no click or direct coordinator call refreshes it.
        var deadline = Time.GetTicksMsec() + 2400;
        while (!Matches() && Time.GetTicksMsec() < deadline) await Frames(1);
        Check(Matches() && _carry.HeldItem == held && _bridge.SelectWorldProps().GetRawText() == before,
            $"the ordinary HUD previews {(valid ? "supported placement" : "the concrete refusal reason")} before input without a saved-state change: {prompt.Text}");
    }

    private async Task Press(string action)
    {
        var wasModal = _player.ModalOpen;
        var observedPresses = _carry.InteractionPresses;
        // The game reads interact during its physics step. A press held for a
        // fixed two frames can fall entirely between two steps on a fast
        // windowed run and is then lost, so hold the action until the
        // coordinator has picked it up (or the cap expires) and only then
        // release it.
        var previous = _carry.PendingAction;
        Input.ActionPress(action);
        for (var frame = 0; frame < 20 && ReferenceEquals(_carry.PendingAction, previous); frame++)
            await Frames(1);
        Input.ActionRelease(action);
        await Frames(2);
        await _carry.PendingAction;
        await Frames(2);
        if (action == "interact" && !wasModal && observedPresses == _carry.InteractionPresses)
            throw new InvalidOperationException("The interact press never reached the ordinary carry input owner: " + _carry.DescribeAim());
    }

    private async Task Capture(string name)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_CARRY_CAPTURE") != "1") return;
        if (RenderingServer.GetRenderingDevice() is null)
            throw new InvalidOperationException("A real rendering device is required for image evidence.");
        var configured = System.Environment.GetEnvironmentVariable("URMAN_CARRY_OUTPUT");
        var directory = string.IsNullOrWhiteSpace(configured)
            ? ProjectSettings.GlobalizePath("res://../docs/production/act1_carry_evidence_2026-09-15") : configured;
        if (!Path.IsPathFullyQualified(directory)) throw new InvalidOperationException("URMAN_CARRY_OUTPUT must be an absolute new evidence path.");
        Directory.CreateDirectory(directory);
        var isolateSurface = (name is "08a_board_drain_from_approach" or "08b_board_drain_underfoot")
            && System.Environment.GetEnvironmentVariable("URMAN_CARRY_SURFACE_ISOLATE") == "1";
        if (isolateSurface)
        {
            // Let the existing step/head smoothing finish without changing its
            // pose. Isolation compares the same camera, not two moving views.
            var stable = 0;
            for (var frame = 0; frame < 90 && stable < 4; frame++)
            {
                var before = _camera.GlobalTransform;
                await Frames(1);
                stable = _camera.GlobalPosition.DistanceTo(before.Origin) < .00001f
                    && _camera.GlobalBasis.IsEqualApprox(before.Basis) ? stable + 1 : 0;
            }
            Check(stable >= 4, "surface isolation begins after ordinary camera smoothing settles");
        }
        await Frames(2);
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "carry/" + name);
        EnsureNoPauseShell("capture the actual game view");
        var path = Path.Combine(directory, name + ".png");
        if (File.Exists(path)) throw new IOException($"Refusing to overwrite previous evidence: {path}");
        GD.Print("act1-carry-capture-save-begin: " + path);
        using var baseline = GetViewport().GetTexture().GetImage();
        var result = baseline.SavePng(path);
        Check(result == Error.Ok, $"real camera capture {name}");
        if (name is not ("08a_board_drain_from_approach" or "08b_board_drain_underfoot")) return;
        var strip = Act1VisibleSurfaceProbe.LogCapturedStrip(this, _camera, name, baseline);
        if (!isolateSurface) return;
        var baselineCamera = _camera.GlobalTransform;
        var evidenceState = _bridge.SelectRuntimeState().GetRawText();
        var candidateIndex = 0;
        foreach (var mesh in strip.Candidates)
        {
            var originalVisibility = mesh.Visible;
            var owner = mesh.GetPath().ToString();
            var isolatedCameraStable = false;
            try
            {
                mesh.Visible = false;
                await Frames(2);
                await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "carry/surface-isolate/" + name + "/" + candidateIndex);
                using var isolated = GetViewport().GetTexture().GetImage();
                var isolatedPath = Path.Combine(directory, name + "_surface_isolate_" + candidateIndex.ToString("00") + ".png");
                if (File.Exists(isolatedPath)) throw new IOException("Refusing to overwrite previous surface evidence: " + isolatedPath);
                Check(isolated.SavePng(isolatedPath) == Error.Ok, "isolated actual surface capture " + name + "/" + candidateIndex);
                GD.Print($"act1-visible-strip-isolation-owner: {name} index={candidateIndex} mesh={owner} file={isolatedPath} cameraDelta={_camera.GlobalPosition.DistanceTo(baselineCamera.Origin)}");
                isolatedCameraStable = RecordSurfaceCameraComparison(name + "/isolated/" + candidateIndex, baselineCamera);
                Act1VisibleSurfaceProbe.LogIsolatedPixels(name, owner, baseline, isolated, strip);
            }
            finally
            {
                if (GodotObject.IsInstanceValid(mesh)) mesh.Visible = originalVisibility;
                await Frames(2);
                await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "carry/surface-restored/" + name + "/" + candidateIndex);
                using var restored = GetViewport().GetTexture().GetImage();
                Act1VisibleSurfaceProbe.LogIsolatedPixels(name + "/restored", owner, baseline, restored, strip);
            }
            var restoredCameraStable = RecordSurfaceCameraComparison(name + "/restored/" + candidateIndex, baselineCamera);
            Check(isolatedCameraStable && restoredCameraStable,
                "surface isolation preserves the observed camera pose");
            Check(_bridge.SelectRuntimeState().GetRawText() == evidenceState, "surface isolation changes no runtime progress or saved object state");
            candidateIndex++;
        }
    }

    private bool RecordSurfaceCameraComparison(string label, Transform3D reference)
    {
        var actual = _camera.GlobalTransform;
        var positionDelta = actual.Origin.DistanceTo(reference.Origin);
        var axisDeltas = new Vector3((actual.Basis.X - reference.Basis.X).Length(),
            (actual.Basis.Y - reference.Basis.Y).Length(), (actual.Basis.Z - reference.Basis.Z).Length());
        var beforeQuaternion = reference.Basis.GetRotationQuaternion();
        var afterQuaternion = actual.Basis.GetRotationQuaternion();
        // At a 1e-4 rad threshold, acos(dot) on float quaternions may reject an
        // unchanged pose. Unit-axis chord distances retain that small-angle
        // tolerance, also detect scale changes, and are exactly zero for equal
        // stored axes. Keep the former angle and its self comparison as evidence.
        var stable = positionDelta < .002f && Math.Max(axisDeltas.X, Math.Max(axisDeltas.Y, axisDeltas.Z)) < .0001f;
        GD.Print($"act1-visible-strip-camera: {label} stable={stable} positionDelta={positionDelta} axisDeltas={axisDeltas} "
            + $"quaternionAngle={afterQuaternion.AngleTo(beforeQuaternion)} beforeQuaternionSelfAngle={beforeQuaternion.AngleTo(beforeQuaternion)} "
            + $"afterQuaternionSelfAngle={afterQuaternion.AngleTo(afterQuaternion)} reference={reference} actual={actual}");
        return stable;
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            EnsureNoPauseShell("continue the local carry proof");
        }
    }

    private void EnsureNoPauseShell(string operation)
    {
        if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true })
            throw new InvalidOperationException($"Cannot {operation}: the real pause menu is open. " + _carry?.DescribeAim());
    }

    private void Check(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException(name);
        _checks.Add(name);
        GD.Print($"act1-carry: check {name}");
    }
}

internal static class Act1CarryRestProof
{
    internal static void Check(FirstPersonController player, CarryCoordinator carry, Action<bool, string> require)
    {
        // Read-only physics probes of every affected authored instance. These
        // use the actual scene contacts, not the generator's height formula,
        // and neither move items nor inject custody or progress.
        var items = carry.Items.Where(item => item.HasMeta("authoredPlacementOwner")
            && item.GetMeta("authoredPlacementOwner").AsString() == "yard terrain footprint").ToArray();
        require(items.Length == 6, "six terrain-resting carry items use the shared authored support owner");
        var space = player.GetWorld3D().DirectSpaceState;
        foreach (var prop in items)
        {
            var exclude = new global::Godot.Collections.Array<Rid> { prop.GetRid(), player.GetRid() };
            using var shape = new BoxShape3D { Size = prop.Size };
            using var volume = new PhysicsShapeQueryParameters3D
            {
                Shape = shape, Transform = prop.GlobalTransform.TranslatedLocal(Vector3.Up * prop.Height * .5f),
                CollisionMask = 3u, Exclude = exclude, Margin = .001f
            };
            var overlaps = space.IntersectShape(volume, 8);
            var bodies = string.Join(", ", overlaps.Select(hit => (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString()));
            require(overlaps.Count == 0, $"{prop.ItemId} authored full body is clear of terrain, woodpile and neighbouring solids: {bodies}; pose={prop.GlobalTransform}");
            var minGap = float.PositiveInfinity;
            var maxGap = float.NegativeInfinity;
            var xSteps = Math.Max(2, Mathf.CeilToInt(prop.Size.X / .23f));
            var zSteps = Math.Max(2, Mathf.CeilToInt(prop.Size.Z / .23f));
            for (var ix = 0; ix <= xSteps; ix++)
            for (var iz = 0; iz <= zSteps; iz++)
            {
                var foot = prop.GlobalTransform * new Vector3(prop.Size.X * (-.5f + ix / (float)xSteps), 0,
                    prop.Size.Z * (-.5f + iz / (float)zSteps));
                using var ray = PhysicsRayQueryParameters3D.Create(foot + Vector3.Up * .08f,
                    foot + Vector3.Down * .10f, 3u, exclude);
                var hit = space.IntersectRay(ray);
                var body = hit.Count > 0 ? hit["collider"].AsGodotObject() as Node : null;
                var gap = hit.Count > 0 ? foot.Y - hit["position"].AsVector3().Y : float.PositiveInfinity;
                if (body?.Name.ToString() != "AgentB_TerrainCollision" || gap < -.002f || gap > .025f)
                    require(false, $"{prop.ItemId} actual ground under footprint sample {ix},{iz}: foot={foot}, body={body?.GetPath()}, gap={gap:F4}m");
                minGap = Math.Min(minGap, gap);
                maxGap = Math.Max(maxGap, gap);
            }
            require(minGap <= .006f, $"{prop.ItemId} has actual ground support across {(xSteps + 1) * (zSteps + 1)} footprint samples, gaps {minGap:F4}–{maxGap:F4}m");
        }
    }
}
