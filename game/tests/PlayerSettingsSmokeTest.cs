using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class PlayerSettingsSmokeTest : Node
{
    public override void _Ready()
    {
        var player = ResourceLoader.Load<PackedScene>("res://scenes/player/first_person_player.tscn")
            ?.Instantiate<FirstPersonController>();
        if (player is null)
        {
            Fail("Player settings smoke could not instantiate the first-person controller.");
            return;
        }

        AddChild(player);
        player.ApplySettings(new GameSettingsSnapshot(
            82,
            0.28,
            true,
            true,
            "low",
            "gamepad",
            InputBindingService.Capture())
        {
            Accessibility = new AccessibilitySettingsSnapshot(
                ReducedMotion: true,
                HighContrast: true,
                TextScale: 1.25,
                Subtitles: false,
                AudioDescriptions: false)
        });
        var restored = player.CaptureSettings();
        if (restored.FieldOfView != 82
            || Math.Abs(restored.MouseSensitivity - 0.28) > 0.0001
            || !restored.MotionBlur
            || !restored.HeadBob
            || restored.GraphicsPreset != "low"
            || restored.InputDevice != "gamepad"
            || !restored.Accessibility.ReducedMotion
            || !restored.Accessibility.HighContrast
            || Math.Abs(restored.Accessibility.TextScale - 1.25) > 0.0001
            || restored.Accessibility.Subtitles
            || restored.Accessibility.AudioDescriptions
            || player.InteractionHint != "[A]"
            || player.HeadBobEnabled
            || player.GraphicsPreset != "low"
            || Math.Abs(GetViewport().Scaling3DScale - 0.75) > 0.0001
            || GetViewport().Msaa3D != Viewport.Msaa.Disabled)
        {
            Fail("Player settings were not preserved by the Godot adapter.");
            return;
        }

        player._UnhandledInput(new InputEventKey { PhysicalKeycode = Key.E, Pressed = true });
        if (player.CurrentInputDevice != "keyboard-mouse" || player.InteractionHint != "[E]")
        {
            Fail("Keyboard input did not switch the interaction hint.");
            return;
        }

        player._UnhandledInput(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true });
        if (player.CurrentInputDevice != "gamepad" || player.InteractionHint != "[A]")
        {
            Fail("Gamepad input did not switch the interaction hint.");
            return;
        }

        player._UnhandledInput(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 0.8f });
        if (player.CurrentInputDevice != "gamepad")
        {
            Fail("Gamepad axis input did not preserve the gamepad interaction hint.");
            return;
        }

        var originalBindings = InputBindingService.Capture();
        InputBindingService.RebindKeyboard("interact", Key.F);
        var rebound = InputBindingService.Capture().Single(binding => binding.Action == "interact");
        InputBindingService.Apply(originalBindings);
        if (rebound.KeyboardPhysicalKeycode != (long)Key.F
            || InputBindingService.Capture().Single(binding => binding.Action == "interact").KeyboardPhysicalKeycode != (long)Key.E)
        {
            Fail("Portable input binding capture/rebind/restore failed.");
            return;
        }

        var originalMovement = InputBindingService.Capture().Single(binding => binding.Action == "move_forward");
        InputBindingService.RebindGamepadAxis("move_forward", (int)JoyAxis.LeftX, 1f);
        var reboundMovement = InputBindingService.Capture().Single(binding => binding.Action == "move_forward");
        InputBindingService.Apply(InputBindingService.Capture().Where(binding => binding.Action != "move_forward").Append(originalMovement));
        var restoredMovement = InputBindingService.Capture().Single(binding => binding.Action == "move_forward");
        if (reboundMovement.GamepadAxis != (int)JoyAxis.LeftX
            || reboundMovement.GamepadAxisSign != 1
            || restoredMovement.GamepadAxis != originalMovement.GamepadAxis
            || restoredMovement.GamepadAxisSign != originalMovement.GamepadAxisSign)
        {
            Fail("Gamepad axis binding capture/rebind/restore failed.");
            return;
        }

        GD.Print("player-settings-smoke: SaveGameV3 settings + remapping + dynamic [E]/[A] hint");
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
