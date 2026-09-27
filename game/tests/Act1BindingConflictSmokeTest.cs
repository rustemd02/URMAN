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

        // Axis remaps must name the stick and direction in the same HUD hint.
        using var axis = new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = 1f };
        settings.BeginRemap("interact");
        settings._UnhandledInput(axis);
        settings.GetNode<Button>("Screen/Panel/Layout/Buttons/Apply").EmitSignal(BaseButton.SignalName.Pressed);
        player._UnhandledInput(axis);
        if (settings.IsAwaitingRemap || player.InteractionHint != "[Правый стик →]")
        {
            Fail($"The interaction HUD did not explain the rebound axis: {player.InteractionHint}.");
            return;
        }
        GD.Print($"act1-binding-axis-hint: {player.InteractionHint}");
        InputBindingService.RestoreDefaults();
        player.SetModalOpen(true);
        player._UnhandledInput(new InputEventKey { PhysicalKeycode = Key.U, Pressed = false });

        // The journal header must use the same bindings as its close action.
        settings.BeginRemap("journal");
        settings._UnhandledInput(new InputEventKey { Keycode = Key.U, PhysicalKeycode = Key.U, Pressed = true });
        settings.BeginRemap("journal");
        settings._UnhandledInput(new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder, Pressed = true });
        settings.GetNode<Button>("Screen/Panel/Layout/Buttons/Apply").EmitSignal(BaseButton.SignalName.Pressed);
        settings.Close();
        await Frames(2);
        var journal = demo.DemoMain!.GetNode<JournalUi>("JournalUi");
        var bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
        journal.Open(bridge);
        var close = journal.GetNode<Button>("Screen/Book/Layout/Header/Close");
        if (!close.Text.Contains("[U]", StringComparison.Ordinal)
            || !close.Text.Contains("[LB]", StringComparison.Ordinal)
            || !close.Text.Contains("[Esc]", StringComparison.Ordinal)
            || InputBindingService.Label("journal") != "U / LB")
        { Fail($"Journal close hint ignored the applied bindings: {close.Text}"); return; }
        var captureDirectory = OS.GetEnvironment("URMAN_UI_SHOT_DIR");
        if (!string.IsNullOrWhiteSpace(captureDirectory))
        {
            await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "journal/rebound-close");
            using var image = GetViewport().GetTexture().GetImage();
            if (image.SavePng(System.IO.Path.Combine(captureDirectory, "journal_rebound_close.png")) != Error.Ok)
            { Fail("Could not capture the rebound journal header."); return; }
        }
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.U, PhysicalKeycode = Key.U, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.U, PhysicalKeycode = Key.U, Pressed = false });
        await Frames(2);
        if (journal.GetNode<Control>("Screen").Visible || player.ModalOpen)
        { Fail("The displayed rebound key did not close the journal and release control."); return; }

        // The physical document reader must advertise the same rebound action.
        const string sourceId = "urman.oldpc:document/tw_zirat_customs";
        if (!await bridge.OpenDocumentAsync(sourceId))
        { Fail("The accessible source for the document hint could not open."); return; }
        var reader = (DocumentUi)GetTree().GetFirstNodeInGroup("document_ui");
        foreach (var gamepad in new[] { false, true })
        {
            if (gamepad) player._UnhandledInput(new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder });
            else player._UnhandledInput(new InputEventKey { Keycode = Key.U, PhysicalKeycode = Key.U });
            bridge.OpenDocumentUi(sourceId);
            reader.GetNode<Button>("Screen/Document/Layout/Footer/Save").EmitSignal(BaseButton.SignalName.Pressed);
            var expectedHint = gamepad ? "Закройте документ, затем [LB]" : "Закройте документ, затем [U]";
            for (var frame = 0; frame < 120 && !reader.StatusText.Contains(expectedHint, StringComparison.Ordinal); frame++) await Frames(1);
            if (!reader.StatusText.Contains(expectedHint, StringComparison.Ordinal))
            { Fail($"Document save advertised the wrong journal binding: {reader.StatusText}"); return; }
            if (!string.IsNullOrWhiteSpace(captureDirectory))
            {
                await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "document/rebound-journal");
                using var image = GetViewport().GetTexture().GetImage();
                if (image.SavePng(System.IO.Path.Combine(captureDirectory, gamepad ? "document_gamepad_hint.png" : "document_keyboard_hint.png")) != Error.Ok)
                { Fail("Could not capture the document's journal hint."); return; }
            }
            reader.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
        }
        InputBindingService.RestoreDefaults();
        journal.Open(bridge);
        if (!close.Text.Contains(InputBindingService.ActionHint("journal", false), StringComparison.Ordinal)
            || close.Text.Contains("[U]", StringComparison.Ordinal))
        { Fail("Reopening the journal retained the old close binding after defaults were restored."); return; }
        close.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        GD.Print("act1-binding-conflict: PASS conflicting rebind + restore defaults + interaction hint + readable journal/setting labels + actual rebound close + document keyboard/gamepad save hint");
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
