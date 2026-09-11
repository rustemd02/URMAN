using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Verifies the player-facing interaction path instead of dispatching commands
/// directly: camera ray -> physical InteractionTarget -> mapped keyboard/gamepad action.
/// This is deliberately a smoke test, not a replacement for observed playtest.
/// </summary>
public partial class FirstPersonInteractionSmokeTest : Node
{
    private const string HouseScenePath = "res://scenes/zones/style_benchmark_house_pc.tscn";

    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("First-person interaction smoke could not instantiate the main scene.");
            return;
        }

        AddChild(main);
        await Frames(4);

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = main.GetNode<FirstPersonController>("Player");
        var ray = player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        if (bridge is null || bridge.ActiveSceneId != "urman.chapter1:scene/arrival_vehicle_dusk")
        {
            Fail("First-person interaction smoke did not reach the authored arrival scene.");
            return;
        }

        // This focused ray/input smoke stages at the existing exterior house
        // anchor. The full walkthrough below proves arrival discovery through
        // production movement; this test keeps its narrower ray contract.
        var houseExterior = AgentBAct1Layout.HouseExteriorSpawn;
        player.GlobalPosition = new Vector3(houseExterior.X, 0.05f, houseExterior.Z + 1.7f);
        player.RotationDegrees = Vector3.Zero;
        await PhysicsFrames(2);
        if (!AssertRayTarget(ray, "urman.chapter1:interaction/arrival-enter-house"))
        {
            return;
        }

        await PressGamepadAndRelease(JoyButton.A);
        await Frames(3);
        if (main.ActiveZoneScenePath != HouseScenePath
            || bridge.CurrentZoneId != "house_old_pc"
            || bridge.ActiveSceneId != "urman.chapter1:scene/house")
        {
            Fail($"Gamepad interaction did not enter the house: {main.ActiveZoneScenePath}, {bridge.CurrentZoneId}, {bridge.ActiveSceneId}.");
            return;
        }
        if (bridge.IsInteractionAvailable("urman.chapter1:interaction/oldpc-power"))
        {
            Fail("Old-PC access was available before Mansur gave the household request.");
            return;
        }
        if (player.CurrentInputDevice != "gamepad" || player.InteractionHint != "[A]")
        {
            Fail("Gamepad interaction did not switch the production prompt to [A].");
            return;
        }

        // The old PC is deliberately gated by the family request in the
        // authored house scene. Prove the player-facing order first: Mansur's
        // physical target -> dialogue state -> old PC availability.
        player.GlobalPosition = new Vector3(2.8f, 0.05f, 0.15f);
        player.RotationDegrees = Vector3.Zero;
        await PhysicsFrames(2);
        if (!AssertRayTarget(ray, "urman.chapter1:interaction/talk-mansur"))
        {
            return;
        }

        await PressKeyAndRelease(Key.E);
        await Frames(4);
        var mansurDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (mansurDialogue is null || !mansurDialogue.IsOpen
            || !bridge.IsInteractionAvailable("urman.chapter1:interaction/oldpc-power"))
        {
            Fail("Mansur's request did not grant the RuntimeBridge-owned old-PC access state.");
            return;
        }
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
        await Frames(3);
        mansurDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(2);

        // The house target is within 2.7 m of the production interaction ray.
        // Its body, rather than the non-colliding CRT meshes, must receive E.
        player.GlobalPosition = new Vector3(0, 0.05f, -0.65f);
        player.RotationDegrees = Vector3.Zero;
        await PhysicsFrames(2);
        if (!AssertRayTarget(ray, "urman.chapter1:interaction/oldpc-power"))
        {
            return;
        }

        await PressKeyAndRelease(Key.E);
        await Frames(3);
        var oldPc = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        if (oldPc is null || !oldPc.GetNode<Control>("Screen").Visible || !player.ModalOpen)
        {
            Fail("Physical old-PC interaction did not open the modal archive UI.");
            return;
        }

        // Close through the existing UI owner so the smoke scene leaves no
        // modal behind; keyboard/gamepad modal dismissal is covered by the UI
        // smoke path and is not conflated with the interaction-ray assertion.
        oldPc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(2);
        if (oldPc.GetNode<Control>("Screen").Visible || player.ModalOpen)
        {
            Fail("Old-PC UI owner did not release the player modal after physical interaction.");
            return;
        }

        GD.Print("first-person-interaction-smoke: ray -> HouseDoor -> gamepad A -> OldPc -> keyboard E");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private bool AssertRayTarget(RayCast3D ray, string expectedInteractionId)
    {
        if (!ray.IsColliding() || ray.GetCollider() is not InteractionTarget target)
        {
            Fail($"Interaction ray did not hit an InteractionTarget for {expectedInteractionId}.");
            return false;
        }

        if (target.InteractionId != expectedInteractionId || !target.IsAvailable())
        {
            Fail($"Interaction ray hit {target.InteractionId}, expected available {expectedInteractionId}.");
            return false;
        }

        return true;
    }

    private async Task PressKeyAndRelease(Key key)
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = true,
            Echo = false
        });
        await PhysicsFrames(1);
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = false,
            Echo = false
        });
        await PhysicsFrames(1);
    }

    private async Task PressGamepadAndRelease(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton
        {
            ButtonIndex = button,
            Pressed = true
        });
        await PhysicsFrames(1);
        Input.ParseInputEvent(new InputEventJoypadButton
        {
            ButtonIndex = button,
            Pressed = false
        });
        await PhysicsFrames(1);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
