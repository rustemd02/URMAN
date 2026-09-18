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
            var scene = _bridge.ActiveSceneId;
            var log = Item("carry-log");
            var original = log.GlobalPosition;
            await StandFacing(original + Vector3.Up * log.Height * .5f, log);
            await Capture("01_log_and_yard");

            // A disposable layer-2 obstruction tests the same layer as rotated
            // facade blockers. It exists only in this test, never in the game.
            // It sits on the stance-to-target line so it blocks the aim from
            // whichever side the free-standing fixture picked.
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
            await Press("interact");
            Check(_carry.HeldItem == log && log.CollisionLayer == 0, "aimed pickup commits one held item");
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
            _player.SetModalOpen(false);

            await PutAt(Ground(original + Vector3.Right * 1.25f));
            Check(log.State == CarryableProp.CarryState.Placed && log.CollisionLayer == 1u && _carry.HeldItem is null,
                "placement restores physical body");
            await StandFacing(log.GlobalPosition + Vector3.Up * log.Height * .5f, log);
            await Press("interact");
            Check(_carry.HeldItem == log, "placed thing can be taken again");
            await PutAt(Ground(original + Vector3.Right * 1.25f));

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
            await StandFacing(woodpile.GlobalPosition, woodpile);
            await Press("interact");
            var axe = Item("carry-axe");
            Check(woodpile.Completed && !axe.IsConcealed && axe.Visible && axe.CollisionLayer == 1u,
                "second aimed clearing reveals its real object");
            await Capture("05_uncovered_axe");
            await PutAt(Ground(new(-31.05f, 0, 6.2f)));
            await StandFacing(axe.GlobalPosition + Vector3.Up * axe.Height * .5f, axe);
            await Press("interact");
            Check(_carry.HeldItem == axe, "uncovered axe can be taken");

            Check(await _bridge.SaveSlotAsync("carry-cleared-held"), "cleared yard and held object saved together");

            Check(await _bridge.LoadSlotAsync("carry-pristine"), "load pristine snapshot");
            await Frames(6);
            Check(_carry.HeldItem is null && axe.IsConcealed && !axe.Visible && !woodpile.Completed && !fence.Completed
                && log.GlobalPosition.DistanceTo(original) < .015f,
                "missing deviations reset transforms, custody, concealment and snow");
            Check(await _bridge.LoadSlotAsync("carry-cleared-held"), "load cleared/held snapshot");
            await Frames(6);
            Check(_carry.HeldItem == axe && !axe.IsConcealed && axe.Visible && axe.CollisionLayer == 0
                && woodpile.Completed && fence.Completed, "restored held find is visible and the clearing persists");
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
            GD.PrintErr($"act1-carry: FAIL {error}\nHUD: {_player?.GetNodeOrNull<Label>("Hud/InteractionPrompt")?.Text}");
        }
        finally
        {
            foreach (var action in new[] { "interact", "carry_place", "carry_rotate" }) Input.ActionRelease(action);
            if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo);
        }
        GetTree().Quit(exit);
    }

    private CarryableProp Item(string id) => _carry.Items.Single(item => item.ItemId == id);
    private InteractionTarget LightDetail(string role) => _demo!.DemoMain.ConnectedWorld!
        .FindChildren("*", "StaticBody3D", true, false).OfType<InteractionTarget>()
        .Single(node => node.HasMeta("lightGateRole") && node.GetMeta("lightGateRole").AsString() == role);
    private bool Found(string slug) => _bridge.SelectRuntimeState().GetProperty("knowledge")
        .TryGetProperty("urman.chapter1:knowledge/discovery-" + slug, out var entry)
        && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
    private YardUseTarget FindUse(string id) => _carry.GetChildren().OfType<YardTool>()
        .SelectMany(tool => tool.Targets).Single(target => target.UseId == id);
    private static Vector3 Ground(Vector3 point) => new(point.X,
        AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .025f, point.Z);

    // A fixed metre behind every target can put the eye inside a yard canopy
    // post; the physics push-out then varies per machine and the aim ray clips
    // the post instead of the target. Walk a small ring around the target and
    // keep the first stance whose line of sight actually reaches it. A target
    // with no reachable stance is a real defect and fails instead of passing on
    // a lucky offset.
    private async Task StandFacing(Vector3 target, Node? expected = null, Vector3? side = null)
    {
        foreach (var direction in Stances(side))
        foreach (var reach in Reaches)
        {
            var from = Ground(target + direction * reach);
            // The real capsule has to fit: a stance overlapping a canopy post
            // gets pushed out over the next frames and the push differs per run.
            if (!_player.CanStandAt(from)) continue;
            _player.ApplyZoneSpawn(from, 0);
            await Frames(4);
            if (!await Settle()) continue;
            Aim(target);
            await Frames(3);
            if (!SightReaches(target, expected)) continue;
            GD.Print($"act1-carry: local camera fixture {_player.GlobalPosition} target={target} side={direction} reach={reach}");
            return;
        }
        throw new InvalidOperationException($"No standing fixture reaches {target}."
            + (expected is null ? string.Empty : $" Expected {expected.Name} on the aim ray."));
    }

    // A yard fixture has to stand at a natural interacting distance, and not
    // every prop is reachable from the same one. Search the near band only:
    // the reach stays inside the coordinator's own reach of 2.7 m.
    private static readonly float[] Reaches = { 1.25f, .95f, 1.6f, 2.0f };

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
        if (expected is not null)
            return hit.Count != 0 && ReferenceEquals(hit["collider"].AsGodotObject(), expected);
        if (hit.Count == 0) return true;
        return from.DistanceTo(hit["position"].AsVector3()) >= from.DistanceTo(target) - .12f;
    }

    private void Aim(Vector3 target)
    {
        var d = target - _camera.GlobalPosition;
        _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(d.Y, new Vector2(d.X, d.Z).Length())),
            Mathf.RadToDeg(Mathf.Atan2(-d.X, -d.Z)));
    }

    private async Task PutAt(Vector3 ground)
    {
        await StandFacing(ground);
        var held = _carry.HeldItem ?? throw new InvalidOperationException("Placement fixture has no carried item.");
        Check(_carry.TryPlacement(held, out var point, out var reason), $"supported placement: {reason}");
        await Press("carry_place");
        // The preview is measured one physics frame before the commit; a
        // windowed frame can settle the capsule a couple of centimetres, so the
        // tolerance covers that without hiding a placement on the wrong support.
        Check(_carry.HeldItem is null && held.GlobalPosition.DistanceTo(point) < .06f,
            "mapped placement commits checked transform");
    }

    // The game reads interact during its physics step. A press held for a fixed
    // two frames can fall entirely between two steps on a fast windowed run and
    // is then lost, so hold the action until the coordinator has picked it up
    // (or the cap expires) and only then release it.
    private async Task Press(string action)
    {
        var previous = _carry.PendingAction;
        Input.ActionPress(action);
        for (var frame = 0; frame < 20 && ReferenceEquals(_carry.PendingAction, previous); frame++)
            await Frames(1);
        Input.ActionRelease(action);
        await Frames(2);
        await _carry.PendingAction;
        await Frames(2);
    }

    private async Task Capture(string name)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_CARRY_CAPTURE") != "1") return;
        if (RenderingServer.GetRenderingDevice() is null)
            throw new InvalidOperationException("A real rendering device is required for image evidence.");
        var directory = System.Environment.GetEnvironmentVariable("URMAN_CARRY_CAPTURE_DIR")
            ?? ProjectSettings.GlobalizePath("res://../docs/production/act1_carry_evidence_2026-09-15");
        Directory.CreateDirectory(directory);
        await Frames(2);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = Path.Combine(directory, name + ".png");
        var result = GetViewport().GetTexture().GetImage().SavePng(path);
        Check(result == Error.Ok, $"real camera capture {name}");
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private void Check(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException(name);
        _checks.Add(name);
        GD.Print($"act1-carry: check {name}");
    }
}
