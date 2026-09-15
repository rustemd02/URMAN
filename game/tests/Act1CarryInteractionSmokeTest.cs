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
            await StandFacing(original + Vector3.Up * log.Height * .5f);
            await Capture("01_log_and_yard");

            // A disposable layer-2 obstruction tests the same layer as rotated
            // facade blockers. It exists only in this test, never in the game.
            var barrier = new StaticBody3D { Name = "CarryOcclusionFixture", CollisionLayer = 2u,
                CollisionMask = 0u, Position = new(original.X, original.Y + .9f, original.Z + .63f) };
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
            await StandFacing(log.GlobalPosition + Vector3.Up * log.Height * .5f);
            await Press("interact");
            Check(_carry.HeldItem == log, "placed thing can be taken again");
            await PutAt(Ground(original + Vector3.Right * 1.25f));

            var shovel = Item("carry-tool-shovel");
            await StandFacing(shovel.GlobalPosition + Vector3.Up * .45f);
            await Press("interact");
            Check(_carry.HeldItem == shovel, "shovel is physically carried, not a remote switch");
            var fence = FindUse("fence");
            var woodpile = FindUse("woodpile");
            await StandFacing(fence.GlobalPosition, Vector3.Right);
            await Capture("03_snow_before");
            await Press("interact");
            Check(fence.Completed && !woodpile.Completed, "only the aimed snow opening changes");
            var physicalDrift = _demo.DemoMain.ConnectedWorld!.FindChild("Ex05PassageDriftBarrier", true, false) as StaticBody3D;
            Check(physicalDrift?.CollisionLayer == 0, "clearing removes the actual passage barrier");
            await Capture("04_snow_cleared");
            await StandFacing(woodpile.GlobalPosition);
            await Press("interact");
            var axe = Item("carry-axe");
            Check(woodpile.Completed && !axe.IsConcealed && axe.Visible && axe.CollisionLayer == 1u,
                "second aimed clearing reveals its real object");
            await Capture("05_uncovered_axe");
            await PutAt(Ground(new(-31.05f, 0, 6.2f)));
            await StandFacing(axe.GlobalPosition + Vector3.Up * axe.Height * .5f);
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
            Check(await _bridge.StartNewGameAsync(), "new runtime session on persistent world");
            await Frames(8);
            Check(_carry.HeldItem is null && axe.IsConcealed && !woodpile.Completed && !fence.Completed,
                "New Game clears local consequences and re-registers custody");
            await StandFacing(original + Vector3.Up * log.Height * .5f);
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
    private YardUseTarget FindUse(string id) => _carry.GetChildren().OfType<YardTool>()
        .SelectMany(tool => tool.Targets).Single(target => target.UseId == id);
    private static Vector3 Ground(Vector3 point) => new(point.X,
        AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .025f, point.Z);

    private async Task StandFacing(Vector3 target, Vector3? side = null)
    {
        var from = Ground(target + (side ?? Vector3.Back) * 1.25f);
        _player.ApplyZoneSpawn(from, 0);
        await Frames(5);
        Aim(target);
        await Frames(3);
        GD.Print($"act1-carry: local camera fixture {_player.GlobalPosition} target={target}");
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
        Check(_carry.HeldItem is null && held.GlobalPosition.DistanceTo(point) < .03f, "mapped placement commits checked transform");
    }

    private async Task Press(string action)
    {
        Input.ActionPress(action);
        await Frames(2);
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
        var directory = ProjectSettings.GlobalizePath("res://../docs/production/act1_carry_evidence_2026-09-15");
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
