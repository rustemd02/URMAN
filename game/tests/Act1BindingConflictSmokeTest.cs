using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-008 focused smoke: rebinding a key that is already bound elsewhere
/// requires a second identical press (explicit conflict choice), the
/// conflicting action keeps its binding, and restore-defaults returns every
/// remappable action to the pristine project bindings.
/// </summary>
public partial class Act1BindingConflictSmokeTest : Node
{
    public override async void _Ready()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Binding conflict smoke could not instantiate the demo entrypoint.");
            return;
        }

        AddChild(demo);
        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Binding conflict smoke could not start through the main menu.");
            return;
        }

        demo._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        await Frames(2);

        var settings = demo.DemoMain?.GetNodeOrNull<SettingsUi>("SettingsUi");
        var player = demo.DemoMain?.GetNodeOrNull<FirstPersonController>("Player");
        if (settings is null || player is null)
        {
            Fail("Binding conflict smoke could not find settings or player.");
            return;
        }

        settings.Open(player);
        var capture = InputBindingService.Capture();
        var interactKey = (Key)(capture.First(binding => binding.Action == "interact").KeyboardPhysicalKeycode);
        var journalDefault = capture.First(binding => binding.Action == "journal").KeyboardPhysicalKeycode;
        var baselineHint = player.InteractionHint;

        // 1) Begin rebinding `journal` onto interact's key: the first press
        //    only warns; the second identical press applies the conflict.
        settings.BeginRemap("journal");
        settings._UnhandledInput(new InputEventKey
        {
            Keycode = interactKey,
            PhysicalKeycode = interactKey,
            Pressed = true,
            Echo = false
        });
        await Frames(1);
        var firstPressConsumed = settings.IsAwaitingRemap;
        settings._UnhandledInput(new InputEventKey
        {
            Keycode = interactKey,
            PhysicalKeycode = interactKey,
            Pressed = true,
            Echo = false
        });
        await Frames(1);

        if (!firstPressConsumed)
        {
            Fail("The conflicting first press did not stay in the remap flow.");
            return;
        }

        if (settings.IsAwaitingRemap)
        {
            Fail("The second identical press did not finish the conflicting rebind.");
            return;
        }

        var after = InputBindingService.Capture().ToDictionary(binding => binding.Action, binding => binding);
        if (after["journal"].KeyboardPhysicalKeycode != (long)interactKey
            || after["interact"].KeyboardPhysicalKeycode != (long)interactKey)
        {
            Fail("The confirmed conflict did not bind both actions as chosen.");
            return;
        }

        // 2) Restore defaults returns every action to the pristine bindings.
        InputBindingService.RestoreDefaults();
        var restored = InputBindingService.Capture().ToDictionary(binding => binding.Action, binding => binding);
        if (restored["journal"].KeyboardPhysicalKeycode != journalDefault
            || restored["interact"].KeyboardPhysicalKeycode != (long)interactKey)
        {
            Fail("Restore-defaults did not return the pristine bindings.");
            return;
        }

        foreach (var binding in restored.Values)
        {
            if (binding.KeyboardPhysicalKeycode == (long)Key.None && binding.GamepadButton is null && binding.GamepadAxis is null)
            {
                Fail($"Restore-defaults left {binding.Action} without any binding.");
                return;
            }
        }

        // 3) A rebind must move the on-screen interaction hint too, not only the
        //    input map. InputMap has no change signal, so the documented settings
        //    and modal boundaries are the invalidation points.
        var rebindKey = Key.U;
        settings.BeginRemap("interact");
        settings._UnhandledInput(new InputEventKey
        {
            Keycode = rebindKey,
            PhysicalKeycode = rebindKey,
            Pressed = true,
            Echo = false
        });
        await Frames(1);
        if (settings.IsAwaitingRemap)
        {
            Fail("Rebinding interact onto a free key did not finish in one press.");
            return;
        }

        player.SetModalOpen(true);
        player.SetModalOpen(false);
        var reboundHint = player.InteractionHint;
        if (reboundHint != $"[{OS.GetKeycodeString(rebindKey)}]" || reboundHint == baselineHint)
        {
            Fail($"The interaction hint did not follow the rebind: baseline={baselineHint} rebound={reboundHint}.");
            return;
        }

        InputBindingService.RestoreDefaults();
        player.SetModalOpen(true);
        player.SetModalOpen(false);
        if (player.InteractionHint != baselineHint)
        {
            Fail($"The interaction hint did not return to the default after restore: {player.InteractionHint}.");
            return;
        }

        GD.Print("act1-binding-conflict: PASS two-step conflicting rebind + conflicting action preserved + restore defaults + prompt follows the rebind");
        settings.Close();
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
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
