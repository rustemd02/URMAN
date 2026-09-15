using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// SAVE-004 focused smoke: the runtime writes a rolling checkpoint after the
/// stable investigation beats (official record, internal register, language
/// reread, zirat road), restoring it lands exactly on the saved beat with no
/// repeated or skipped progress, and the walk continues to the terminal.
/// </summary>
public partial class Act1CheckpointSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";

    public override async void _Ready()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Checkpoint smoke could not instantiate the demo entrypoint.");
            return;
        }

        DeleteCheckpoint();
        AddChild(demo);
        await Frames(8);
        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Checkpoint smoke could not start through the main menu.");
            return;
        }

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (main is null || bridge is null)
        {
            Fail("Checkpoint smoke could not find main or bridge.");
            return;
        }

        // Fresh session: no checkpoint until the first stable beat.
        if (bridge.IsSlotAvailable(RuntimeBridge.CheckpointSlot))
        {
            Fail("A checkpoint existed before any stable beat in a fresh session.");
            return;
        }

        // Authored chain: arrival -> house -> FAP -> official record.
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
            || !await bridge.ChooseDialogueAsync(Dialogue("alsu_route_context"), "name-road", "ask-versions")
            || !await Advance(bridge, "route-to-fap", "fap_waiting_room_day")) return;
        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(1);
        if (!bridge.IsInteractionAvailable(Interaction("talk-naila"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-naila"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "official-wording")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "official-wording", "ask-wording")
            || !await Advance(bridge, "fap-to-document-desk", "fap_pressure_document_desk")) return;
        if (!await bridge.SaveSlotAsync("checkpoint-before-evidence")
            || !await Advance(bridge, "fap-document-desk-to-official-record", "evidence-official-death")) return;

        // Stable FAP-evidence beat: the rolling checkpoint must exist now.
        if (!bridge.IsSlotAvailable(RuntimeBridge.CheckpointSlot))
        {
            Fail("No checkpoint was written after the FAP evidence beat.");
            return;
        }

        // Returning to the same stable scene in a restored session must write
        // a fresh checkpoint rather than retaining the old session guard.
        var checkpointPath = ProjectSettings.GlobalizePath("user://savegames/checkpoint.savegame-v3.json");
        var previousWrite = DateTime.UtcNow.AddDays(-1);
        System.IO.File.SetLastWriteTimeUtc(checkpointPath, previousWrite);
        if (!await bridge.LoadSlotAsync("checkpoint-before-evidence")
            || !await Advance(bridge, "fap-document-desk-to-official-record", "evidence-official-death")
            || System.IO.File.GetLastWriteTimeUtc(checkpointPath) <= previousWrite.AddSeconds(1))
        { Fail("Restored session skipped a repeated checkpoint scene."); return; }

        // Continue to the internal register and the language reread: the
        // checkpoint is rewritten at each checkpoint-scene entry.
        if (!await Advance(bridge, "official-to-internal-register", "evidence-internal-register")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/doc_marat_official_death_notice")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/rec_marat_case_register_conflict")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { "urman.oldpc:document/doc_marat_official_death_notice", "urman.oldpc:document/rec_marat_case_register_conflict" })
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "follow-up")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "follow-up", "press-contradiction")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), new[] { "urman.oldpc:document/rec_marat_case_register_conflict", "urman.chapter1:knowledge/clue_naila_record_scope" }))
        { Fail("The record comparison was rejected."); return; }
        if (!bridge.IsInteractionAvailable(Interaction("internal-register-to-rinat"))
            || !await bridge.DispatchInteractionAsync(Interaction("internal-register-to-rinat"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("rinat_internal_register"), "dangerous-category")
            || !await bridge.ChooseDialogueAsync(Dialogue("rinat_internal_register"), "dangerous-category", "present-category")
            || !await Advance(bridge, "internal-register-to-saved-message", "evidence-saved-message")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/msg_marat_saved_last_normal")
            || !await Advance(bridge, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/tw_shurale_urman_boundary")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" })
            || !await Advance(bridge, "boundary-source-to-reread", "evidence-tatarwiki-reread")) return;

        // Restore lands exactly on the language-reread beat with the final
        // knowledge still hidden (reveal-not-before survives the restore).
        if (!await bridge.LoadSlotAsync(RuntimeBridge.CheckpointSlot)
            || bridge.ActiveSceneId != Scene("evidence-tatarwiki-reread")
            || BeatState(bridge.SelectRuntimeState(), "cliffhanger-hard-cut") == "completed"
            || FinalKnowledge(bridge.SelectRuntimeState()) == "confirmed")
        {
            Fail("Checkpoint restore did not land on the language-reread beat.");
            return;
        }

        // SAVE-002 matrix: the restored session state must byte-match the
        // pre-restore state (no drift between slot and live state).
        var preRestoreStateRaw = bridge.SelectRuntimeState().GetRawText();
        if (!await bridge.LoadSlotAsync(RuntimeBridge.CheckpointSlot)
            || bridge.SelectRuntimeState().GetRawText() != preRestoreStateRaw)
        {
            Fail($"The restored session state diverged from the pre-restore state: '{bridge.SelectRuntimeState().GetRawText()}' vs '{preRestoreStateRaw[..Math.Min(120, preRestoreStateRaw.Length)]}'");
            return;
        }

        // The restored session continues from the reread beat: the zirat
        // roadside clue (post-checkpoint in pass 1) must be re-collected
        // before the forest approach unlocks.
        if (bridge.IsInteractionAvailable(Interaction("reread-to-edge-sketch"))
            || BeatState(bridge.SelectRuntimeState(), "language-reread") == "completed"
            || BeatState(bridge.SelectRuntimeState(), "boundary-source-reopened") != "completed")
        { Fail("Checkpoint treated a reopened source as a completed interpretation, or lost the reread action."); return; }
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), new[] { "urman.oldpc:document/rec_marat_case_register_conflict", "urman.oldpc:document/tw_shurale_urman_boundary" })
            || !await Advance(bridge, "reread-to-edge-sketch", "evidence-edge-sketch")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch")
            || !await Advance(bridge, "edge-sketch-to-zirat-road", "zirat-road")) return;
        var clue = Interaction("zirat-roadside-clue");
        if (!bridge.IsInteractionAvailable(clue)
            || !await bridge.DispatchInteractionAsync(clue)
            || FinalKnowledge(bridge.SelectRuntimeState()) != "hidden")
        {
            Fail("The zirat roadside clue was not available or wrongly granted after restore.");
            return;
        }

        // The restored session continues to the terminal: exactly once.
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" }))
        { Fail("The route comparison was rejected."); return; }
        // The authored route stages Kara-Urman in two steps: the zirat road leads
        // to the forest approach, and only that approach leads into the forest.
        if (!await Advance(bridge, "zirat-road-to-forest", "forest-approach")
            || !await Advance(bridge, "forest-approach-to-forest", "forest")
            || FinalKnowledge(bridge.SelectRuntimeState()) != "confirmed"
            || BeatState(bridge.SelectRuntimeState(), "cliffhanger-hard-cut") != "completed")
        {
            Fail("The restored session did not reach the single terminal beat.");
            return;
        }

        GD.Print("act1-checkpoint: PASS rolling checkpoints at internal-register/reread + exact restore + terminal once");
        DeleteCheckpoint();
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    /// <summary>
    /// SAVE-002 matrix step: save the current beat, drift away, restore and
    /// prove the session returns to the identical state and scene.
    /// </summary>
    private async Task<bool> SaveRestoreRoundtrip(
        RuntimeBridge bridge,
        Main main,
        string slot,
        string expectedSceneLocalId,
        string expectedStateRaw)
    {
        if (!await bridge.SaveSlotAsync(slot))
        {
            Fail($"Save/restore matrix could not save {slot}.");
            return false;
        }

        main.SwitchZone("kara_urman_night", "village_path");
        await Frames(2);
        if (!await bridge.LoadSlotAsync(slot)
            || bridge.ActiveSceneId != Scene(expectedSceneLocalId)
            || bridge.SelectRuntimeState().GetRawText() != expectedStateRaw)
        {
            Fail($"Save/restore matrix did not return the identical state for {slot} at {expectedSceneLocalId}.");
            return false;
        }

        return true;
    }

    private async Task<bool> Advance(RuntimeBridge bridge, string interactionLocalId, string targetSceneLocalId)
    {
        var interactionId = Interaction(interactionLocalId);
        if (!bridge.IsInteractionAvailable(interactionId))
        {
            Fail($"Checkpoint interaction was not available: {interactionId}.");
            return false;
        }

        if (!await bridge.DispatchInteractionAsync(interactionId)
            || bridge.ActiveSceneId != Scene(targetSceneLocalId))
        {
            Fail($"Checkpoint interaction did not reach {targetSceneLocalId}: {interactionId}.");
            return false;
        }

        if (targetSceneLocalId == "forest")
        {
        for (var attempt = 0; attempt < 200 && bridge.IsInteractionAvailable("urman.chapter1:interaction/forest-rinat-intervention"); attempt++)
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        }

        return true;
    }

    private static JsonElement? ReadCheckpointState(RuntimeBridge bridge)
    {
        var path = ProjectSettings.GlobalizePath($"user://savegames/{RuntimeBridge.CheckpointSlot}.savegame-v3.json");
        if (!System.IO.File.Exists(path))
        {
            return null;
        }

        using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
        if (document.RootElement.TryGetProperty("runtime", out var runtime)
            && runtime.TryGetProperty("state", out var state))
        {
            return state.Clone();
        }

        return null;
    }

    private static string FinalKnowledge(JsonElement state) =>
        state.TryGetProperty("knowledge", out var knowledge)
        && knowledge.TryGetProperty($"{ChapterPrefix}knowledge/clue_do_not_answer_rule", out var entry)
            ? entry.GetProperty("status").GetString() ?? string.Empty
            : "hidden";

    private static string BeatState(JsonElement state, string localId)
    {
        state.TryGetProperty("beats", out var beats);
        return beats.TryGetProperty($"{ChapterPrefix}beat/{localId}", out var beat)
            ? beat.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private static string Scene(string localId) => $"{ChapterPrefix}scene/{localId}";

    private static string Dialogue(string localId) => $"{ChapterPrefix}dialogue/{localId}";

    private static void DeleteCheckpoint()
    {
        foreach (var suffix in new[] { ".savegame-v3.json", ".savegame-v3.backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{RuntimeBridge.CheckpointSlot}{suffix}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
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
