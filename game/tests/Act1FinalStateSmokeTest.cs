using System.Text.Json;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// STATE-005 focused final-state fixtures: the terminal knowledge
/// `clue_do_not_answer_rule` cannot be confirmed before Rinat's forest-edge
/// intervention, a pre-forest save restores with no final knowledge, the
/// ending completes exactly once after restore, and a post-terminal repeat
/// dispatch cannot duplicate the terminal beat.
/// </summary>
public partial class Act1FinalStateSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string MatrixSlot = "final-state-matrix";

    public override async void _Ready()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Final-state smoke could not instantiate the Act 1 demo entrypoint.");
            return;
        }

        AddChild(demo);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Final-state smoke could not start through the main menu.");
            return;
        }

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null)
        {
            Fail("Final-state smoke could not find the runtime bridge.");
            return;
        }

        DeleteSlot();

        // Authored chain to the zirat road, mirroring the campaign gates.
        if (!await Advance(bridge, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = OfficialNotice
        }));
        if (!bridge.IsInteractionAvailable(Interaction("talk-gulsina"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning")
            || !await Advance(bridge, "house-to-route", "crossroad_signs_inspect")) return;
        main.SwitchZone("village_day", "from_house");
        await Frames(1);
        if (!bridge.IsInteractionAvailable(Interaction("talk-alsu"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-alsu"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("alsu_route_context"), "name-road")
            || !await Advance(bridge, "route-to-fap", "fap_waiting_room_day")) return;
        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(1);
        if (!bridge.IsInteractionAvailable(Interaction("talk-naila"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-naila"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "official-wording")
            || !await Advance(bridge, "fap-to-document-desk", "fap_pressure_document_desk")
            || !await Advance(bridge, "fap-document-desk-to-official-record", "evidence-official-death")
            || !await Advance(bridge, "official-to-internal-register", "evidence-internal-register")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        if (!bridge.IsInteractionAvailable(Interaction("internal-register-to-rinat"))
            || !await bridge.DispatchInteractionAsync(Interaction("internal-register-to-rinat"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("rinat_internal_register"), "dangerous-category")
            || !await Advance(bridge, "internal-register-to-saved-message", "evidence-saved-message")
            || !await Advance(bridge, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")
            || !await Advance(bridge, "boundary-source-to-reread", "evidence-tatarwiki-reread")
            || !await Advance(bridge, "reread-to-edge-sketch", "evidence-edge-sketch")
            || !await Advance(bridge, "edge-sketch-to-zirat-road", "zirat-road")) return;
        main.SwitchZone("zirat_road", "village_side");
        await Frames(1);

        // 1) Before the roadside clue the forest approach is locked and an
        //    out-of-order dispatch is rejected without state change.
        var postRinatState = bridge.SelectRuntimeState();
        if (bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
            || await bridge.DispatchInteractionAsync(Interaction("zirat-road-to-forest"))
            || FinalKnowledge(postRinatState) != "hidden"
            || BeatIsCompleted(postRinatState, "cliffhanger-hard-cut"))
        {
            Fail("The forest approach was not gated by the zirat roadside clue.");
            return;
        }

        if (!bridge.IsInteractionAvailable(Interaction("zirat-roadside-clue"))
            || !await bridge.DispatchInteractionAsync(Interaction("zirat-roadside-clue"))
            || FinalKnowledge(bridge.SelectRuntimeState()) != "hidden"
            || bridge.IsInteractionAvailable(Interaction("zirat-roadside-clue"))
            || await bridge.DispatchInteractionAsync(Interaction("zirat-roadside-clue")))
        {
            Fail("The zirat roadside clue did not confirm once without granting final knowledge.");
            return;
        }

        // 2) Pre-forest save: no final knowledge, cliffhanger not completed.
        if (!await bridge.SaveSlotAsync(MatrixSlot))
        {
            Fail("Final-state smoke could not write the pre-forest snapshot.");
            return;
        }

        // 3) Enter the forest: the single terminal beat completes once.
        if (!bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
            || !await Advance(bridge, "zirat-road-to-forest", "forest")
            || FinalKnowledge(bridge.SelectRuntimeState()) != "confirmed"
            || !BeatIsCompleted(bridge.SelectRuntimeState(), "cliffhanger-hard-cut"))
        {
            Fail("The forest entry did not complete the single terminal beat.");
            return;
        }

        // 4) Restore the pre-forest save: the final knowledge is gone again
        //    (the save predates the reveal; nothing leaks through the load).
        if (!await bridge.LoadSlotAsync(MatrixSlot)
            || FinalKnowledge(bridge.SelectRuntimeState()) != "hidden"
            || BeatIsCompleted(bridge.SelectRuntimeState(), "cliffhanger-hard-cut")
            || bridge.ActiveSceneId != Scene("zirat-road"))
        {
            Fail("Loading the pre-forest save did not restore the pre-reveal state exactly.");
            return;
        }

        // 5) Complete the ending again after the restore: exactly once.
        if (!await Advance(bridge, "zirat-road-to-forest", "forest")
            || FinalKnowledge(bridge.SelectRuntimeState()) != "confirmed"
            || !BeatIsCompleted(bridge.SelectRuntimeState(), "cliffhanger-hard-cut"))
        {
            Fail("The restored session could not complete the terminal beat once more.");
            return;
        }

        // 6) Post-terminal repeat: the forest approach is scene-locked and
        //    cannot duplicate the terminal beat or mutate the state.
        var terminalState = bridge.SelectRuntimeState().GetRawText();
        if (bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
            || await bridge.DispatchInteractionAsync(Interaction("zirat-road-to-forest"))
            || bridge.SelectRuntimeState().GetRawText() != terminalState)
        {
            Fail("A post-terminal forest dispatch mutated the terminal state.");
            return;
        }

        GD.Print("act1-final-state: PASS gated approach + pre-reveal save/load + single terminal beat + idempotent post-terminal");
        DeleteSlot();
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task<bool> Advance(RuntimeBridge bridge, string interactionLocalId, string targetSceneLocalId)
    {
        var interactionId = Interaction(interactionLocalId);
        if (!bridge.IsInteractionAvailable(interactionId))
        {
            Fail($"Final-state interaction was not available: {interactionId}.");
            return false;
        }

        if (!await bridge.DispatchInteractionAsync(interactionId)
            || bridge.ActiveSceneId != Scene(targetSceneLocalId))
        {
            Fail($"Final-state interaction did not reach {targetSceneLocalId}: {interactionId}.");
            return false;
        }

        return true;
    }

    private static string FinalKnowledge(JsonElement state) =>
        state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/clue_do_not_answer_rule").GetProperty("status").GetString()!;

    private static bool BeatIsCompleted(JsonElement state, string localId) =>
        state.TryGetProperty("beats", out var beats)
        && beats.TryGetProperty($"{ChapterPrefix}beat/{localId}", out var beat)
        && beat.GetString() == "completed";

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private static string Scene(string localId) => $"{ChapterPrefix}scene/{localId}";

    private static string Dialogue(string localId) => $"{ChapterPrefix}dialogue/{localId}";

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void DeleteSlot()
    {
        foreach (var suffix in new[] { ".json", ".backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{MatrixSlot}{suffix}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
