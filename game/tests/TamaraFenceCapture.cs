using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Manual visual pass over the fence quest: the standing plot, the collapse
/// itself, every shot of the scene, a board spot and the mended fence. Writes
/// PNGs to URMAN_TAMARA_FRAMES.
/// </summary>
public partial class TamaraFenceCapture : Node
{
    private string _output = "/tmp/tamara_frames";

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _output = System.Environment.GetEnvironmentVariable("URMAN_TAMARA_FRAMES") ?? _output;
            Directory.CreateDirectory(_output);
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("no start");
            // Without this the arrival card stays over every frame below and the
            // capture shows the title panel instead of the world.
            demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            var quest = TamaraFenceQuest.Current(GetTree())!;
            var fleet = (VehicleFleet)GetTree().GetFirstNodeInGroup("vehicle_fleet");
            var niva = fleet.Vehicles.Single(vehicle => vehicle.Definition.Id == "babay-niva");

            // Standing plot from the road, the wicket side, then the fence line
            // itself: pillars, shields and the footing.
            player.GlobalPosition = new(-1.2f, .1f, -40.0f);
            player.RotationDegrees = new(0, -54f, 0);
            await Frames(20);
            await Capture("01_fence_from_road");
            player.GlobalPosition = new(1.4f, .1f, -39.6f);
            player.RotationDegrees = new(0, -47f, 0);
            await Frames(8);
            await Capture("02_wicket_close");
            player.GlobalPosition = new(-1.6f, .1f, -46.6f);
            player.RotationDegrees = new(0, -110f, 0);
            await Frames(8);
            await Capture("02b_fence_line_close");

            // Board spot at the village-edge shed.
            player.GlobalPosition = new(11.5f, .1f, -46.2f);
            player.RotationDegrees = new(0, -68f, 0);
            await Frames(8);
            await Capture("03_board_spot_shed");

            // Crash: park the Niva in the breach first so the aftermath reads.
            niva.GlobalPosition = new(.9f, 0f, -43.6f);
            niva.RotationDegrees = new(0, 78f, 0);
            niva.SetPlacementAvailability(true);
            player.SetVehicleControl(true);
            niva.Enter(player, restore: true);
            player.GlobalPosition = niva.ToGlobal(niva.Definition.Seat);
            quest.AccelerateCutsceneForTest(1.0);
            // The listener goes on before the crash: the opening shot fires as
            // the scene starts and would otherwise never be photographed.
            var shots = new System.Collections.Generic.List<string>();
            quest.CutsceneShotListenerForTest = tag => shots.Add(tag);
            quest.DebugHandleHardStop(niva, 4.6f, quest.Panels[2].Body);
            for (var i = 0; i < 600 && quest.ActiveCutscene is null; i++) await Frames(1);

            // The shields go over in the first second; three frames across it
            // are what the collapse is judged by.
            if (quest.ActiveCutscene is null) throw new InvalidOperationException("no cutscene after the crash");
            await Frames(14);
            await Capture("04a_collapse_early");
            await Frames(24);
            await Capture("04b_collapse_falling");
            await Frames(60);
            await Capture("04c_collapse_settled");
            var next = 0;
            var guard = 0;
            while (quest.ActiveCutscene is not null && guard++ < 6000)
            {
                await Frames(1);
                if (shots.Count <= next) continue;
                var tag = shots[next];
                next++;
                await Frames(14);
                await Capture($"05_shot_{tag}");
            }

            await Frames(10);
            await Capture("09_after_scene");

            // The work list while the boards are still missing.
            await CaptureJournalWorkList("11_journal_in_progress");

            // Mended fence: complete through the real interactions.
            player.SetVehicleControl(false);
            player.GlobalPosition = new(-1.5f, .1f, -44.2f);
            for (var board = 1; board <= 6; board++)
            {
                if (!await bridge.DispatchInteractionAsync(
                    $"urman.chapter1:interaction/tamara-fence-take-board-{board}"))
                {
                    throw new InvalidOperationException($"pickup {board} failed");
                }
            }
            if (!await bridge.TamaraFenceDeliverAsync()) throw new InvalidOperationException("delivery failed");
            quest.MarkRepairDeferredByLoadForTest();
            player.GlobalPosition = new(-2f, .1f, -20f);
            for (var i = 0; i < 30 && !quest.FenceRepairApplied; i++) await Frames(1);
            player.GlobalPosition = new(-1.2f, .1f, -40.0f);
            player.RotationDegrees = new(0, -54f, 0);
            await Frames(10);
            await Capture("10_repaired_fence");

            // The same page once the fence is mended.
            await CaptureJournalWorkList("12_journal_done");
            exit = 0;
            GD.Print($"tamara-fence-capture: done -> {_output}");
        }
        catch (Exception error)
        {
            GD.PushError("tamara-fence-capture failed: " + error);
        }
        finally
        {
            GetTree().Quit(exit);
        }
    }

    private async Task Capture(string name)
    {
        await Frames(2);
        var image = GetViewport().GetTexture().GetImage();
        image.Convert(Image.Format.Rgba8);
        var path = Path.Combine(_output, name + ".png");
        image.SavePng(path);
        GD.Print($"tamara-fence-capture: saved {path}");
    }

    private async Task CaptureJournalWorkList(string name)
    {
        if (GetTree().GetFirstNodeInGroup("journal_ui") is not JournalUi journal) return;
        journal.Open((RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge"));
        if (journal.FindChild("Tabs", true, false) is TabBar tabs) tabs.CurrentTab = 2;
        await Frames(8);
        await Capture(name);
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close")
            .EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(6);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
