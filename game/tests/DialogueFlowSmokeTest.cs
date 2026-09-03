using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class DialogueFlowSmokeTest : Node
{
    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Dialogue flow could not instantiate main.");
            return;
        }

        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null)
        {
            Fail("Dialogue flow could not find runtime bridge.");
            return;
        }

        if (!await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/gulsina_yaramyy", "home-warning"))
        {
            Fail("Gulsina dialogue was rejected.");
            return;
        }

        var state = bridge.SelectRuntimeState();
        if (state.GetProperty("vocabulary").GetProperty("urman.chapter1:vocabulary/tt_yaramyy").GetProperty("status").GetString() != "guessed")
        {
            Fail("Dialogue effects did not update shared vocabulary state.");
            return;
        }

        var dialogueUi = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (dialogueUi is null || player is null)
        {
            Fail("Dialogue flow could not find the production dialogue UI and player.");
            return;
        }

        bridge.OpenDialogueUi("urman.chapter1:dialogue/gulsina_yaramyy");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        dialogueUi._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        if (dialogueUi.IsOpen || player.ModalOpen)
        {
            Fail("The mapped keyboard interact action did not continue dialogue through the Continue button path.");
            return;
        }

        bridge.OpenDialogueUi("urman.chapter1:dialogue/gulsina_yaramyy");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        dialogueUi._UnhandledInput(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.A,
            Pressed = true
        });
        if (dialogueUi.IsOpen || player.ModalOpen)
        {
            Fail("The mapped gamepad interact action did not continue dialogue through the Continue button path.");
            return;
        }

        bridge.OpenDialogueUi("urman.chapter1:dialogue/gulsina_yaramyy");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        dialogueUi._UnhandledInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
            Echo = false
        });
        if (dialogueUi.IsOpen || player.ModalOpen)
        {
            Fail("ui_cancel no longer closes the dialogue modal.");
            return;
        }

        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = "urman.oldpc:document/doc_marat_official_death_notice"
        }));
        if (!await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/rinat_no_key", "deflect") ||
            !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/rinat_no_key", "deflect", "show-official"))
        {
            Fail("Rinat dialogue choice was rejected after finding the official record.");
            return;
        }

        state = bridge.SelectRuntimeState();
        if (!state.GetProperty("npc").GetProperty("urman.chapter1:character/rinat").GetProperty("saw_official_record").GetBoolean() ||
            state.GetProperty("dialogueChoices").GetArrayLength() != 1)
        {
            Fail("Rinat dialogue did not commit NPC state and choice history atomically.");
            return;
        }

        GD.Print("dialogue-flow-smoke: Gulsina vocabulary + Rinat authored choice through shared kernel state");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
