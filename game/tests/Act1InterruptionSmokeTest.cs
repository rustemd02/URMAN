using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// GAME-009 focused interruption fixtures: cancelling an open dialogue modal
/// applies no state change, a quick save taken while a modal is open and a
/// load afterwards stay state-consistent (no half-applied effects), a one-shot
/// interaction cannot be duplicated, completed one-shots stay consumed across
/// revisits, and the final progression remains idempotent after retreat.
/// </summary>
public partial class Act1InterruptionSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string BaselineSlot = "interrupt-baseline";

    private Act1DemoRoot? _demo;
    private bool _finished;

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            await RunAsync();
            if (!_finished) throw new InvalidOperationException("The authored state setup stopped before its final assertion.");
            exit = 0;
        }
        catch (Exception error) { GD.PrintErr("act1-interruption: FAIL " + error); }
        finally
        {
            try { if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo); }
            catch (Exception cleanupError)
            {
                exit = 1;
                GD.PrintErr("act1-interruption: cleanup failed: " + cleanupError);
            }
            GetTree().Quit(exit);
        }
    }

    private async Task RunAsync()
    {
        var demo = _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
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

        await Act1ArrivalFlowProof.CompleteAsync(this, bridge);

        // Baseline: at the house with the old-PC notice read (Gulsina pending).
        if (!await Advance(bridge, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = OfficialNotice
        }));
        if (!await bridge.SaveSlotAsync(BaselineSlot))
        {
            Fail("Interruption smoke could not write the baseline save.");
            return;
        }
        // Saving flushes the world props (vehicles, doors, the companion) into
        // the runtime state; the baseline is what the slot holds, so read it after.
        var postNoticeState = bridge.SelectRuntimeState().GetRawText();

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

        // 2) The cancelled dialogue can still reach the family answer. Asking
        // the same actual question again preserves the completed effects.
        var reAvailable = bridge.IsInteractionAvailable(Interaction("talk-gulsina"));
        var reDispatched = reAvailable && await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"));
        var reEntered = reDispatched && await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning");
        var reAnswered = reEntered && await bridge.ChooseDialogueAsync(Dialogue("gulsina_yaramyy"), "home-warning", "ask-marat");
        // A real repeated choice appends its committed dialogue history. Its
        // warning, knowledge, journal and progression effects stay unchanged.
        var stateAfterRetalk = bridge.SelectRuntimeState();
        var reRetalk = reAnswered && await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"));
        var reRetalkEntered = reRetalk && await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning");
        var reRetalkAnswered = reRetalkEntered && await bridge.ChooseDialogueAsync(Dialogue("gulsina_yaramyy"), "home-warning", "ask-marat");
        var stateAfterSecondRetalk = bridge.SelectRuntimeState();
        var repeatedConsequencesStable = VerifyRepeatedGulsinaConsequences(
            stateAfterRetalk, stateAfterSecondRetalk, out var retalkStateDetail);
        GD.Print($"act1-interruption: repeated-gulsina {retalkStateDetail}");
        if (!reAvailable || !reDispatched || !reAnswered || !reRetalkAnswered
            || !repeatedConsequencesStable
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail($"Repeated Gulsina answer was not idempotent: available={reAvailable} dispatched={reDispatched} answered={reAnswered} consequencesStable={repeatedConsequencesStable} retalk={reRetalk}/{reRetalkAnswered} {retalkStateDetail}");
            return;
        }

        // 3) A baseline load reverts the whole interrupted chain to the
        //    post-notice state: Gulsina pending again, nothing stuck.
        var baselineLoaded = await bridge.LoadSlotAsync(BaselineSlot);
        var loadedState = bridge.SelectRuntimeState();
        if (!baselineLoaded
            || !RuntimeStateComparison.SameIgnoringNpcFacing(loadedState.GetRawText(), postNoticeState)
            || !bridge.IsInteractionAvailable(Interaction("talk-gulsina")))
        {
            using var expected = JsonDocument.Parse(postNoticeState);
            var drift = expected.RootElement.EnumerateObject()
                .Where(field => !loadedState.TryGetProperty(field.Name, out var now) || now.GetRawText() != field.Value.GetRawText())
                .Select(field => loadedState.TryGetProperty(field.Name, out var now)
                    ? $"{field.Name}: {field.Value.GetRawText()} -> {now.GetRawText()}" : field.Name + ": missing")
                .Concat(loadedState.EnumerateObject()
                    .Where(field => !expected.RootElement.TryGetProperty(field.Name, out _))
                    .Select(field => $"{field.Name}: added {field.Value.GetRawText()}"));
            Fail($"Baseline load after the interrupted chain did not restore a consistent replayable state: loaded={baselineLoaded} "
                + $"talk-gulsina={bridge.IsInteractionAvailable(Interaction("talk-gulsina"))} drift=[{string.Join(" | ", drift)}]");
            return;
        }

        // 4) A legal house exit remains available after a presentation-only
        // leave/re-entry. Main.SwitchZone changes the physical connected-world
        // placement, while RuntimeBridge keeps the narrative scene and state
        // authoritative; this proves the revisit does not duplicate the
        // house beat or strand the exit.
        if (!await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning")
            || !await bridge.ChooseDialogueAsync(Dialogue("gulsina_yaramyy"), "home-warning", "ask-marat")
            || !await Act1FamilyMealProof.CompleteAsync(this, bridge)
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("The replayable house state did not unlock its authored exit.");
            return;
        }

        var houseBeforeRevisit = bridge.SelectRuntimeState().GetRawText();
        main.SwitchZone("village_day", "from_house");
        await Frames(1);
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        if (main.ActiveZoneScenePath != "res://scenes/zones/style_benchmark_house_pc.tscn"
            || bridge.CurrentZoneId != "house_old_pc"
            || bridge.SelectRuntimeState().GetRawText() != houseBeforeRevisit
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("House leave/re-entry changed the runtime state or lost the available exit.");
            return;
        }

        if (!await Advance(bridge, "house-to-route", "crossroad_signs_inspect")) return;
        main.SwitchZone("village_day", "from_house");
        await Frames(1);

        // Continue through the authored FAP gate. The FAP evidence route is
        // already consumed once at the old PC; revisiting its physical zone
        // after the official-record transition must not replay or mutate the
        // narrative snapshot.
        if (!await bridge.DispatchInteractionAsync(Interaction("talk-alsu"))
            || !await Act1AlsuWalkProof.CompleteAsync(this, bridge)
            || !await bridge.EnterDialogueNodeAsync(Dialogue("alsu_route_context"), "name-road")
            || !await bridge.ChooseDialogueAsync(Dialogue("alsu_route_context"), "name-road", "ask-versions")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-versions-scope"),
                new[] { OfficialNotice, ChapterPrefix + "knowledge/clue_alsu_heard_versions" })
            || !await Advance(bridge, "route-to-fap", "fap_waiting_room_day"))
        {
            Fail("The replayable village route did not reach the authored FAP gate.");
            return;
        }

        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(1);
        if (!await bridge.DispatchInteractionAsync(Interaction("talk-naila"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "official-wording")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "official-wording", "ask-wording")
            || !await Advance(bridge, "fap-to-document-desk", "fap_pressure_document_desk")
            || !await Advance(bridge, "fap-document-desk-to-official-record", "evidence-official-death"))
        {
            Fail("The replayable FAP route did not reach the official-record beat.");
            return;
        }

        var afterFapEvidence = bridge.SelectRuntimeState().GetRawText();
        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(1);
        if (main.ActiveZoneScenePath != "res://scenes/zones/chapter1_fap_clinic.tscn"
            || bridge.CurrentZoneId != "fap_clinic"
            || bridge.SelectRuntimeState().GetRawText() != afterFapEvidence)
        {
            Fail("FAP revisit after consumed evidence changed the narrative state or failed to load the physical zone.");
            return;
        }

        // 5) Return to the house, resolve the remaining evidence, and prove
        // the zirat roadside clue stays consumed across a retreat/re-entry
        // before the final trigger is dispatched.
        if (!await Advance(bridge, "official-to-internal-register", "evidence-internal-register")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/doc_marat_official_death_notice")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/rec_marat_case_register_conflict"))
        { Fail("The record comparison sources could not be read."); return; }
        await Act1SourceExcerptProof.RecordNoticeCauseAsync(this, bridge);
        await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, bridge);
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { "urman.oldpc:document/doc_marat_official_death_notice", "urman.oldpc:document/rec_marat_case_register_conflict" })
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "follow-up")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "follow-up", "press-contradiction")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "record-question", "show-external-wording")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "matching-formulation", "ask-category-scope")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), new[] { "urman.oldpc:document/rec_marat_case_register_conflict", "urman.chapter1:knowledge/clue_naila_record_scope" }))
        { Fail("The record comparison was rejected."); return; }
        if (!await bridge.DispatchInteractionAsync(Interaction("internal-register-to-rinat"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("rinat_internal_register"), "dangerous-category")
            || !await bridge.ChooseDialogueAsync(Dialogue("rinat_internal_register"), "dangerous-category", "present-category")
            || !await Advance(bridge, "internal-register-to-saved-message", "evidence-saved-message")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/msg_marat_saved_last_normal")
            || !await Act1StateFlowProof.MessageReturnAsync(this, bridge)
            || !await Advance(bridge, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/tw_shurale_urman_boundary")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" })
            || !await Advance(bridge, "boundary-source-to-reread", "evidence-tatarwiki-reread")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), new[] { "urman.oldpc:document/rec_marat_case_register_conflict", "urman.oldpc:document/tw_shurale_urman_boundary" })
            || !await Act1SourceReturnsProof.CompleteAsync(this, bridge)
            || !await Advance(bridge, "reread-to-edge-sketch", "evidence-edge-sketch")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch")
            || !await Act1StateFlowProof.RouteDiscussionAsync(this, bridge)
            || !await Advance(bridge, "edge-sketch-to-zirat-road", "zirat-road"))
        {
            Fail("The return-to-house evidence chain did not reach the zirat road.");
            return;
        }

        main.SwitchZone("zirat_road", "village_side");
        await Frames(1);
        if (!await Act1RinatRoadsideProof.ObserveAsync(this, bridge)) return;
        var ziratClue = Interaction("zirat-roadside-clue");
        if (!bridge.IsInteractionAvailable(ziratClue)
            || !await bridge.DispatchInteractionAsync(ziratClue)
            || bridge.IsInteractionAvailable(ziratClue))
        {
            Fail("The zirat roadside clue did not become a consumed one-shot interaction.");
            return;
        }

        await Act1RouteLandmarksProof.ObserveAsync(this, bridge);
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" }))
        { Fail("The route comparison was rejected."); return; }
        var postZiratClueState = bridge.SelectRuntimeState().GetRawText();
        if (!bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest")))
        {
            Fail("The consumed zirat clue did not leave the authored forest approach available.");
            return;
        }

        main.SwitchZone("village_day", "from_forest");
        await Frames(1);
        main.SwitchZone("zirat_road", "village_side");
        await Frames(1);
        if (bridge.SelectRuntimeState().GetRawText() != postZiratClueState
            || bridge.IsInteractionAvailable(ziratClue)
            || !bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest")))
        {
            Fail("Zirat retreat/re-entry revived the consumed clue or lost the final approach.");
            return;
        }

        // The authored route now stages Kara-Urman in two steps: the zirat road
        // leads to the forest approach, and only that approach leads into the
        // forest. The corridor smoke already follows this contract.
        if (!await Advance(bridge, "zirat-road-to-forest", "forest-approach")) return;
        if (!await Act1StateFlowProof.EnterForestAsync(this, bridge)
            || !await Act1StateFlowProof.WaitForTerminalAsync(this, bridge)) return;
        var terminalState = bridge.SelectRuntimeState().GetRawText();
        if (bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
            || bridge.IsInteractionAvailable(Interaction("forest-approach-to-forest"))
            || await bridge.DispatchInteractionAsync(Interaction("forest-approach-to-forest"))
            || bridge.SelectRuntimeState().GetRawText() != terminalState)
        {
            Fail("Repeated terminal dispatch duplicated or mutated the final progression state.");
            return;
        }

        GD.Print("act1-interruption: PASS dialogue-cancel atomicity + modal quicksave/load + house/FAP revisit + zirat one-shot retreat + terminal idempotency");
        DeleteSlot();
        _finished = true;
    }

    private static bool VerifyRepeatedGulsinaConsequences(JsonElement before, JsonElement after, out string detail)
    {
        var beforeFields = before.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
        var afterFields = after.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
        var changedKeys = beforeFields.Keys.Union(afterFields.Keys, StringComparer.Ordinal)
            .Where(key => !beforeFields.TryGetValue(key, out var previous)
                || !afterFields.TryGetValue(key, out var current) || !SameJson(previous, current))
            .ToArray();
        var beforeChoices = before.GetProperty("dialogueChoices").EnumerateArray().ToArray();
        var afterChoices = after.GetProperty("dialogueChoices").EnumerateArray().ToArray();
        var expectedChoice = JsonSerializer.SerializeToElement(new
        {
            dialogueId = Dialogue("gulsina_yaramyy"),
            nodeId = "home-warning",
            choiceId = "ask-marat"
        });
        var historyAppendedOnce = afterChoices.Length == beforeChoices.Length + 1
            && beforeChoices.Select((choice, index) => SameJson(choice, afterChoices[index])).All(same => same)
            && SameJson(afterChoices[^1], expectedChoice);
        const string familyClue = ChapterPrefix + "knowledge/clue_family_avoids_marat";
        var beforeJournalCount = before.GetProperty("journal").EnumerateArray()
            .Count(entry => entry.GetProperty("sourceId").GetString() == familyClue);
        var afterJournalCount = after.GetProperty("journal").EnumerateArray()
            .Count(entry => entry.GetProperty("sourceId").GetString() == familyClue);
        var consequencesStable = changedKeys.All(key => key == "dialogueChoices");
        detail = $"changedKeys=[{string.Join(",", changedKeys)}] choices={beforeChoices.Length}->{afterChoices.Length} expectedAppend={historyAppendedOnce} familyJournal={beforeJournalCount}->{afterJournalCount} consequencesStable={consequencesStable}";
        return historyAppendedOnce && consequencesStable && beforeJournalCount == 1 && afterJournalCount == 1;
    }

    private static bool SameJson(JsonElement first, JsonElement second) =>
        JsonNode.DeepEquals(JsonNode.Parse(first.GetRawText()), JsonNode.Parse(second.GetRawText()));

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
            foreach (var suffix in new[] { ".savegame-v3.json", ".savegame-v3.backup.json" })
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

    private static void Fail(string message) => throw new InvalidOperationException(message);
}
