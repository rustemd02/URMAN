using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Spec A03/WORLD15: once the village's kit placements are authored data, the
/// game builds them from the plot, not from the generator's values. A
/// disposable copy of the plot moves the arrival well by 3 m and turns it,
/// and hides one woodpile; the built world must follow, with the well's own
/// blockers moving along, and nothing may report a missing placement.
/// </summary>
public partial class StudioVillageKitSmokeTest : Node
{
    private const string Well = "urman.world:act1/kit/village-day-arrival-main-street-landmark";
    private const string Woodpile = "urman.world:act1/kit/village-day-main-street-east-woodpile";
    private int _checks;

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            var source = ProjectSettings.GlobalizePath(KitPlacementTakeover.PlotPath);
            var plot = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
            foreach (var entity in plot["entities"]!.AsArray().OfType<JsonObject>())
            {
                var parameters = entity["params"]!.AsObject();
                if ((string)entity["id"]! == Well)
                {
                    parameters["position"] = new JsonArray(-1.9, 0.0, 4.6);
                    parameters["yawDegrees"] = 45.0;
                }

                if ((string)entity["id"]! == Woodpile) parameters["hidden"] = true;
            }

            var copy = ProjectSettings.GlobalizePath("user://studio-kit-smoke/act1_village_kit.world.v1.json");
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.WriteAllText(copy, plot.ToJsonString());
            KitPlacementTakeover.PlotOverrideForTest = copy;
            KitPlacementTakeover.Reset();

            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(demo), "ordinary New Game");
            await Frames(20);
            Node3D Placement(string id) => GetTree().Root.FindChildren("*", nameof(Node3D), true, false).OfType<Node3D>()
                .First(node => node.HasMeta(AuthoredWorldPlot.AuthoredIdMeta) && (string)node.GetMeta(AuthoredWorldPlot.AuthoredIdMeta) == id);
            var well = Placement(Well);
            Require(Mathf.Abs(well.GlobalPosition.X - -1.9f) < .01f && Mathf.Abs(well.GlobalPosition.Z - 4.6f) < .01f,
                $"the well stands where the plot puts it, not where the generator did (at {well.GlobalPosition})");
            Require(Mathf.Abs(well.RotationDegrees.Y - 45f) < .01f, "the plot's yaw wins");
            var blockers = well.FindChildren("AuthoredKitCollisionProxy", nameof(StaticBody3D), true, false);
            Require(blockers.Count == 0 || blockers.OfType<Node3D>().All(proxy => proxy.GetParent() == well || well.IsAncestorOf(proxy)),
                "the well's blockers are parented to it and move with it");
            Require(!Placement(Woodpile).Visible, "a hidden placement stays out of the village");
            GD.Print($"studio-village-kit-smoke: {_checks} checks passed (blockers under the well: {blockers.Count})");
            exit = 0;
        }
        catch (Exception error)
        {
            GD.PushError($"studio-village-kit-smoke: FAIL {error.Message}");
        }
        finally
        {
            KitPlacementTakeover.PlotOverrideForTest = null;
            KitPlacementTakeover.Reset();
        }

        GetTree().Quit(exit);
    }

    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
