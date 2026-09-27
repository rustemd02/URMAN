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

        if (await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/mansur_pc_request", "ask-for-help", "ask-kept-draft")
            || await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/mansur_pc_request", "draft-folder-question", "name-saved-folder")
            || await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/rinat_internal_register", "category-reply", "ask-compensation-records"))
        { Fail("Unread archive sources were bypassed through a direct source-holder choice."); return; }

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
        if (!await CheckLayoutAsync(dialogueUi, player, "gulsina_choices", shortReply: false)) return;
        stayForTea.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        dialogueUi._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        if (!dialogueUi.IsOpen || !player.ModalOpen
            || dialogueUi.GetNode<Button>("Screen/Panel/Layout/Continue").Visible)
        {
            Fail("Mapped interact skipped the unanswered family invitation or chose an unseen source.");
            return;
        }
        var deferMeal = dialogueUi.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren()
            .OfType<Button>().SingleOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-tea-later"));
        if (deferMeal is null) { Fail("An unread-source home invitation offered no natural refusal."); return; }
        deferMeal.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (dialogueUi.IsOpen || player.ModalOpen)
        { Fail("Deferring the meal retained the conversation modal."); return; }

        // EX14.4: one existing resident notices the player's own reading at her
        // house. The question waits for that knowledge, names its source (she
        // keeps the paint under the step) and adds no new story fact.
        bridge.OpenDialogueUi("urman.chapter1:dialogue/gulsina_yaramyy");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var paintBefore = dialogueUi.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-porch-paint"));
        if (paintBefore is not null)
        {
            Fail("Gulsina offered the porch paint question before the player had read it.");
            return;
        }
        dialogueUi._UnhandledInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
            Echo = false
        });
        if (!await bridge.DispatchInteractionAsync("urman.chapter1:interaction/discover-babai-yard-porch-paint-tin"))
        {
            Fail("The authored porch paint reading was rejected.");
            return;
        }
        bridge.OpenDialogueUi("urman.chapter1:dialogue/gulsina_yaramyy");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var paintButton = dialogueUi.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-porch-paint"));
        if (paintButton is null)
        {
            Fail("Gulsina's porch paint question did not appear after the player read the tin.");
            return;
        }
        paintButton.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (dialogueUi.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
            != bridge.ResolveText("urman.chapter1:text/dialogue-gulsina-porch-paint-reply"))
        {
            Fail("Gulsina's porch paint reply did not match its authored line.");
            return;
        }
        if (!await CheckLayoutAsync(dialogueUi, player, "gulsina_porch_paint_reply", shortReply: true)) return;
        dialogueUi._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        if (dialogueUi.IsOpen || player.ModalOpen)
        {
            Fail("Gulsina's porch paint reaction did not release the player.");
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
        if (bridge.SelectRuntimeState().GetProperty("npc").TryGetProperty("urman.chapter1:character/mansur", out var mansurState)
            && mansurState.TryGetProperty("pc_access_granted", out var automaticAccess) && automaticAccess.GetBoolean())
        { Fail("Cancelling Mansur's first request still granted computer access."); return; }
        bridge.OpenDialogueUi(mansurDialogue);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var offerHelp = mansurChoices.GetChildren().OfType<Button>()
            .Single(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-offer-help"));
        offerHelp.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        dialogueUi._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        if (!bridge.SelectRuntimeState().GetProperty("npc").GetProperty("urman.chapter1:character/mansur")
                .GetProperty("pc_access_granted").GetBoolean())
        { Fail("Mansur's explicit help response did not grant computer access."); return; }
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

        if (!bridge.SelectRuntimeState().GetProperty("npc")
                .GetProperty("urman.chapter1:character/mansur")
                .GetProperty("mittens_asked").GetBoolean())
        { Fail("Asking Mansur about the sled did not record that the question was already asked."); return; }

        bridge.OpenDialogueUi(mansurDialogue);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var againText = bridge.ResolveText("urman.chapter1:text/choice-mansur-blue-mittens-again");
        var againButton = mansurChoices.GetChildren().OfType<Button>().SingleOrDefault(button => button.Text == againText);
        if (againButton is null
            || mansurChoices.GetChildren().OfType<Button>().Any(button => button.Text == memoryText))
        { Fail("Mansur's return visit did not offer the follow-up question about the paint instead of the first one."); return; }
        againButton.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (dialogueUi.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
            != bridge.ResolveText("urman.chapter1:text/dialogue-mansur-blue-mittens-again-reply")
            || !dialogueUi.GetNode<Button>("Screen/Panel/Layout/Continue").Visible)
        { Fail("Mansur's return visit did not present the closing reply about the paint."); return; }
        dialogueUi._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
        if (dialogueUi.IsOpen || player.ModalOpen)
        { Fail("Mansur's closing reply did not release the player."); return; }

        GD.Print("dialogue-flow-smoke: Gulsina vocabulary, her discovery-gated porch paint reaction + Rinat choice + discovery-gated Mansur memory and its return visit through production UI");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async System.Threading.Tasks.Task<bool> CheckLayoutAsync(DialogueUi dialogueUi, FirstPersonController player,
        string state, bool shortReply)
    {
        var original = player.Accessibility;
        var output = System.Environment.GetEnvironmentVariable("URMAN_DIALOGUE_FRAMES");
        if (!string.IsNullOrEmpty(output)
            && (!System.IO.Path.IsPathFullyQualified(output) || !System.IO.Directory.Exists(output)
                || DisplayServer.GetName() == "headless"))
        {
            Fail("URMAN_DIALOGUE_FRAMES requires an existing absolute directory and native rendering.");
            return false;
        }

        try
        {
            foreach (var scale in new[] { 1.0, 1.6 })
            {
                var settings = original with { TextScale = scale };
                player.ApplyAccessibilitySettings(settings);
                AccessibilityPresentation.ApplyToTree(GetTree(), settings);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                var panel = dialogueUi.GetNode<PanelContainer>("Screen/Panel");
                var line = dialogueUi.GetNode<RichTextLabel>("Screen/Panel/Layout/Line");
                var choices = dialogueUi.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices");
                var continueButton = dialogueUi.GetNode<Button>("Screen/Panel/Layout/Continue");
                var panelRect = panel.GetGlobalRect();
                var viewportRect = dialogueUi.GetViewport().GetVisibleRect();
                var actions = choices.GetChildren().OfType<Button>().Where(button => button.Visible).ToArray();
                if (!dialogueUi.IsOpen || !viewportRect.Encloses(panelRect)
                    || !panelRect.Encloses(line.GetGlobalRect())
                    || actions.Any(button => !panelRect.Encloses(button.GetGlobalRect()))
                    || (continueButton.Visible && !panelRect.Encloses(continueButton.GetGlobalRect()))
                    || (shortReply && (!continueButton.Visible || actions.Length != 0
                        || line.Size.Y - line.GetContentHeight() > 32f))
                    || (!shortReply && (actions.Length == 0 || continueButton.Visible)))
                {
                    Fail($"Dialogue layout escaped the viewport or expanded a short reply: {state}, scale={scale}, panel={panelRect}.");
                    return false;
                }

                if (string.IsNullOrEmpty(output)) continue;
                var path = System.IO.Path.Combine(output, $"{state}_scale_{(scale == 1.0 ? "1_0" : "1_6")}.png");
                if (System.IO.File.Exists(path))
                {
                    Fail("Dialogue capture already exists: " + path);
                    return false;
                }
                await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "dialogue/" + state + "/" + scale);
                using var image = GetViewport().GetTexture().GetImage();
                if (image.IsEmpty() || image.SavePng(path) != Error.Ok)
                {
                    Fail("Dialogue capture failed: " + path);
                    return false;
                }
                GD.Print("dialogue-capture: " + path);
            }
            return true;
        }
        finally
        {
            player.ApplyAccessibilitySettings(original);
            AccessibilityPresentation.ApplyToTree(GetTree(), original);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
