using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Walking up to Mansur shows one authored Tatar greeting with its Russian
/// gloss as a subtitle; stepping away and back inside the cooldown does not
/// repeat it; no dialogue opens and the story state does not change.
/// </summary>
public partial class Act1NpcGreetingSmokeTest : Node
{
    public override async void _Ready()
    {
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            await Frames(8);
            Require(await this.StartThroughMainMenuAsync(demo), "the demo starts through the main menu");
            var main = demo.DemoMain;
            var bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            var player = main.GetNode<FirstPersonController>("Player");
            demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(3);

            var lines = bridge.TextIdsWithPrefix("urman.chapter1:text/greeting-mansur-");
            Require(lines.Count >= 3, "Mansur has authored greetings");
            var mansur = main.ConnectedWorld!.FindChild("Npc_mansur", true, false) as Node3D
                ?? throw new InvalidOperationException("Mansur is not staged.");
            var before = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();

            async Task StandAt(float distance)
            {
                var forward = mansur.GlobalBasis.Z.Normalized();
                var at = mansur.GlobalPosition + forward * distance;
                player.ApplyPortableTransform(new PlayerTransform(new(at.X, at.Y + .05f, at.Z), new(0, 0, 0)));
                await PhysicsFrames(20);
            }

            await StandAt(6f);
            Require(player.CurrentRemark.Length == 0, "no greeting from across the room");
            await StandAt(1.6f);
            var first = player.CurrentRemark;
            Require(first.StartsWith("БАБАЙ МАНСУР: «", StringComparison.Ordinal)
                && lines.Any(id => first.EndsWith(bridge.ResolveText(id), StringComparison.Ordinal)),
                "walking up to Mansur shows one of his Tatar greetings with its gloss: " + first);
            Require(first.Contains(" — ", StringComparison.Ordinal), "the greeting carries a Russian gloss");
            Require(mansur.GetMeta("lastGreeting", "").AsString() == lines[0], "greetings start at the first authored line");
            Require(!player.ModalOpen, "a greeting never opens a dialogue");

            await StandAt(6f);
            await StandAt(1.6f);
            Require(mansur.GetMeta("lastGreeting", "").AsString() == lines[0],
                "coming back inside the cooldown does not repeat or advance the greeting");
            Require(bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == before,
                "a greeting changes no knowledge");
            GD.Print($"act1-npc-greeting: PASS {first}");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("act1-npc-greeting: " + exception.Message);
            GetTree().Quit(1);
        }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
}
