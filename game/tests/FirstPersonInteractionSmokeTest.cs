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

        await Act1ArrivalFlowProof.CompleteAsync(this, bridge);

        // Stage on the physical approach normal of the authored GLB portal,
        // rather than the legacy yard anchor. The target and player Y values
        // both come from the shared Agent B heightfield contract.
        player.GlobalPosition = AgentBAct1Layout.HouseDoorApproach;
        player.RotationDegrees = new Vector3(0f, AgentBAct1Layout.HouseDoorYawDegrees, 0f);
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
        if (!await StandFacing(player, "urman.chapter1:interaction/talk-mansur", 1.3f)
            || !AssertRayTarget(ray, "urman.chapter1:interaction/talk-mansur"))
        {
            return;
        }

        await PressKeyAndRelease(Key.E);
        await Frames(4);
        var mansurDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        // Since the family conversations gained alternatives, opening Mansur's
        // request only asks; access is the player's answer (offer-help below).
        if (mansurDialogue is null || !mansurDialogue.IsOpen
            || bridge.IsInteractionAvailable("urman.chapter1:interaction/oldpc-power"))
        {
            Fail("Mansur's request did not open, or granted old-PC access before the player offered help.");
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
        if (!bridge.IsInteractionAvailable("urman.chapter1:interaction/oldpc-power"))
        {
            Fail("Offering help did not grant the RuntimeBridge-owned old-PC access state.");
            return;
        }
        mansurDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(2);

        // Approach the desk and look down at the tabletop-sized CRT, within
        // the production ray's 2.7 m reach. Its physical target must receive E.
        if (!await StandFacing(player, "urman.chapter1:interaction/oldpc-power", 1.2f)
            || !AssertRayTarget(ray, "urman.chapter1:interaction/oldpc-power"))
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

    // The house interior was rebuilt (StyleBenchmarkInteriorFactory), so fixed
    // room coordinates go stale. Stand on the entry side of the actual target,
    // at a reach the production ray covers, and look straight at it.
    private async Task<bool> StandFacing(FirstPersonController player, string interactionId, float standOff)
    {
        var target = GetTree().Root.FindChildren("*", "", true, false)
            .OfType<InteractionTarget>().FirstOrDefault(candidate => candidate.InteractionId == interactionId);
        if (target is null)
        {
            Fail($"First-person interaction smoke could not locate {interactionId}.");
            return false;
        }

        var house = target.GetParent<Node>();
        while (house is not null && house.SceneFilePath != HouseScenePath) house = house.GetParent();
        var entry = house is Node3D room ? room.ToGlobal(StyleBenchmarkInteriorFactory.Entry) : player.GlobalPosition;
        var away = entry - target.GlobalPosition;
        away.Y = 0f;
        away = away.LengthSquared() > 0.0001f ? away.Normalized() : Vector3.Back;
        var stand = target.GlobalPosition + away * standOff;
        player.GlobalPosition = new Vector3(stand.X, entry.Y, stand.Z);
        player.RotationDegrees = Vector3.Zero;
        await PhysicsFrames(1);
        var toward = target.GlobalPosition - player.GetNode<Camera3D>("Head/Camera3D").GlobalPosition;
        player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(toward.Y, new Vector2(toward.X, toward.Z).Length())),
            Mathf.RadToDeg(Mathf.Atan2(-toward.X, -toward.Z)));
        await PhysicsFrames(2);
        return true;
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
