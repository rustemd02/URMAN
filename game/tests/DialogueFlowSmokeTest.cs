using System.Linq;
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
        var firstChoices = dialogueUi.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().ToArray();
        var stayForTea = firstChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-stay-for-tea"));
        var askYaramyyWhy = firstChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-yaramyy"));
        if (stayForTea is null || askYaramyyWhy is null)
        {
            Fail("Gulsina's start node did not expose both the tea alternative and the guessed ярамый question.");
            return;
        }
        stayForTea.EmitSignal(Button.SignalName.Pressed);
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
        var secondChoices = dialogueUi.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().ToArray();
        var yaramyyChoice = secondChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-yaramyy"));
        if (yaramyyChoice is null)
        {
            Fail("Gulsina's guessed ярамый question was not available as the second early branch.");
            return;
        }
        yaramyyChoice.EmitSignal(Button.SignalName.Pressed);
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
        var dialogueChoices = state.GetProperty("dialogueChoices");
        var sawRinatOfficialChoice = dialogueChoices.EnumerateArray().Any(choice =>
            choice.GetProperty("dialogueId").GetString() == "urman.chapter1:dialogue/rinat_no_key"
            && choice.GetProperty("choiceId").GetString() == "show-official");
        if (!state.GetProperty("npc").GetProperty("urman.chapter1:character/rinat").GetProperty("saw_official_record").GetBoolean() ||
            !sawRinatOfficialChoice)
        {
            Fail("Rinat dialogue did not commit NPC state and choice history atomically.");
            return;
        }

        const string mansurDialogue = "urman.chapter1:dialogue/mansur_pc_request";
        const string memoryChoice = "remember-blue-mittens";
        if (await bridge.ChooseDialogueAsync(mansurDialogue, "ask-for-help", memoryChoice))
        { Fail("Mansur remembered the sled before Aidar found it."); return; }
        bridge.OpenDialogueUi(mansurDialogue);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var memoryText = bridge.ResolveText("urman.chapter1:text/choice-mansur-blue-mittens");
        var mansurChoices = dialogueUi.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices");
        if (mansurChoices.GetChildren().OfType<Button>().Any(button => button.Text == memoryText)
            || dialogueUi.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text != bridge.ResolveText("urman.chapter1:text/dialogue-mansur-pc-request")
            || !mansurChoices.GetChildren().OfType<Button>().Any(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-offer-help")))
        { Fail("Mansur's first request changed or exposed the undiscovered sled question."); return; }
        dialogueUi._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        main.SwitchZone("village_day", "entry");
        if (!await bridge.DispatchInteractionAsync("urman.chapter1:interaction/discover-babai-yard-sled-repair"))
        { Fail("The authored sled discovery was rejected."); return; }
        bridge.OpenDialogueUi(mansurDialogue);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var memoryButton = mansurChoices.GetChildren().OfType<Button>().SingleOrDefault(button => button.Text == memoryText);
        if (memoryButton is null
            || dialogueUi.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text != bridge.ResolveText("urman.chapter1:text/dialogue-mansur-follow-up")
            || mansurChoices.GetChildren().OfType<Button>().Any(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-offer-help")))
        { Fail("Mansur's repeat request replayed exposition, retained completed PC help or lost the new discovery question."); return; }
        memoryButton.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (dialogueUi.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
            != bridge.ResolveText("urman.chapter1:text/dialogue-mansur-blue-mittens-reply")
            || !dialogueUi.GetNode<Button>("Screen/Panel/Layout/Continue").Visible)
        { Fail("The optional sled question did not present Mansur's terminal reply."); return; }
        dialogueUi._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
        if (dialogueUi.IsOpen || player.ModalOpen)
        { Fail("Mansur's sled reply did not release the player."); return; }

        GD.Print("dialogue-flow-smoke: Gulsina vocabulary + Rinat choice + discovery-gated Mansur memory through production UI");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
