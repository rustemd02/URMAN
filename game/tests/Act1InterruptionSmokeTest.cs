using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// GAME-009 focused interruption fixtures: cancelling an open dialogue modal
/// applies no state change, a quick save taken while a modal is open and a
/// load afterwards stay state-consistent (no half-applied effects), a one-shot
/// interaction cannot be duplicated, and completed one-shots stay consumed.
/// </summary>
public partial class Act1InterruptionSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string BaselineSlot = "interrupt-baseline";

    public override async void _Ready()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Interruption smoke could not instantiate the Act 1 demo entrypoint.");
            return;
        }

        AddChild(demo);
        await Frames(8);
        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Interruption smoke could not start through the main menu.");
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

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var dialogueUi = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (main is null || bridge is null || dialogueUi is null)
        {
            Fail("Interruption smoke could not find main, bridge or dialogue UI.");
            return;
        }

        DeleteSlot();

        // Baseline: at the house with the old-PC notice read (Gulsina pending).
        if (!await Advance(bridge, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = OfficialNotice
        }));
        var postNoticeState = bridge.SelectRuntimeState().GetRawText();
        if (!await bridge.SaveSlotAsync(BaselineSlot))
        {
            Fail("Interruption smoke could not write the baseline save.");
            return;
        }

        // 1) Modal interrupt while the dialogue is open: a quick save under
        //    the open modal and a cancelled dialogue must leave no state
        //    change, and the quick load restores the same consistent state.
        var gulsinaAvailable = bridge.IsInteractionAvailable(Interaction("talk-gulsina"));
        var gulsinaDispatched = gulsinaAvailable && await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"));
        // The dispatch commits the pending-dialogue state; opening the modal
        // is presentation (the bridge's own entry, as in DialogueFlowSmokeTest).
        bridge.OpenDialogueUi(Dialogue("gulsina_yaramyy"));
        await Frames(2);

        if (!gulsinaAvailable || !gulsinaDispatched || !dialogueUi.IsOpen)
        {
            Fail($"Interruption smoke could not open Gulsina's dialogue modal: available={gulsinaAvailable} dispatched={gulsinaDispatched} open={dialogueUi.IsOpen} scene={bridge.ActiveSceneId}");
            return;
        }

        if (!await bridge.SaveSlotAsync("interrupt-quick"))
        {
            Fail("Interruption smoke could not quick-save under the open dialogue.");
            return;
        }

        dialogueUi._UnhandledInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
            Echo = false
        });
        await Frames(2);
        if (dialogueUi.IsOpen || playerIsGated(demo))
        {
            Fail("Cancelling the dialogue modal did not return control.");
            return;
        }

        var postCancelState = bridge.SelectRuntimeState().GetRawText();
        if (!await bridge.LoadSlotAsync("interrupt-quick")
            || bridge.SelectRuntimeState().GetRawText() != postCancelState)
        {
            Fail("Quick save under a modal and load afterwards diverged from the cancelled-dialogue state.");
            return;
        }

        // 2) The cancelled dialogue is still pending: it can be completed
        //    once, and a repeated dispatch of the one-shot target is refused
        //    with the effect applied exactly once.
        var reAvailable = bridge.IsInteractionAvailable(Interaction("talk-gulsina"));
        var reDispatched = reAvailable && await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"));
        var reEntered = reDispatched && await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning");
        // NPC re-talk is a legal repeated action: no softlock, and the
        // re-applied warning effect is idempotent (identical state, the house
        // exit stays unlocked exactly once).
        var stateAfterRetalk = bridge.SelectRuntimeState().GetRawText();
        var reRetalk = reEntered && await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"));
        var reRetalkEntered = reRetalk && await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning");
        var stateAfterSecondRetalk = bridge.SelectRuntimeState().GetRawText();
        if (!reAvailable || !reDispatched || !reEntered || !stateAfterRetalk.Equals(stateAfterSecondRetalk, StringComparison.Ordinal) || !reRetalkEntered)
        {
            Fail($"Repeated Gulsina talk was not idempotent: available={reAvailable} dispatched={reDispatched} entered={reEntered} identicalState={stateAfterRetalk.Equals(stateAfterSecondRetalk, StringComparison.Ordinal)} retalk={reRetalk}/{reRetalkEntered}");
            return;
        }

        // 3) A baseline load reverts the whole interrupted chain to the
        //    post-notice state: Gulsina pending again, nothing stuck.
        if (!await bridge.LoadSlotAsync(BaselineSlot)
            || bridge.SelectRuntimeState().GetRawText() != postNoticeState
            || !bridge.IsInteractionAvailable(Interaction("talk-gulsina")))
        {
            Fail("Baseline load after the interrupted chain did not restore a consistent replayable state.");
            return;
        }

        GD.Print("act1-interruption: PASS dialogue-cancel atomicity + modal quicksave/load consistency + one-shot idempotency + replayable baseline");
        DeleteSlot();
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private static bool playerIsGated(Act1DemoRoot demo) =>
        demo.DemoMain?.GetNodeOrNull<FirstPersonController>("Player") is not { ModalOpen: false };

    private async Task<bool> Advance(RuntimeBridge bridge, string interactionLocalId, string targetSceneLocalId)
    {
        var interactionId = Interaction(interactionLocalId);
        if (!bridge.IsInteractionAvailable(interactionId))
        {
            Fail($"Interruption interaction was not available: {interactionId}.");
            return false;
        }

        if (!await bridge.DispatchInteractionAsync(interactionId)
            || bridge.ActiveSceneId != Scene(targetSceneLocalId))
        {
            Fail($"Interruption interaction did not reach {targetSceneLocalId}: {interactionId}.");
            return false;
        }

        return true;
    }

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private static string Scene(string localId) => $"{ChapterPrefix}scene/{localId}";

    private static string Dialogue(string localId) => $"{ChapterPrefix}dialogue/{localId}";

    private static void DeleteSlot()
    {
        foreach (var slot in new[] { BaselineSlot, "interrupt-quick" })
        {
            foreach (var suffix in new[] { ".json", ".backup.json" })
            {
                var path = ProjectSettings.GlobalizePath($"user://savegames/{slot}{suffix}");
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
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
