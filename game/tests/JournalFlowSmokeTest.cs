using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class JournalFlowSmokeTest : Node
{
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            Fail("journal-flow-smoke: " + exception);
        }
    }

    private async Task RunAsync()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Journal flow could not instantiate main.");
            return;
        }

        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var journal = GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi;
        if (bridge is null || journal is null || bridge.JournalEntries().Count != 0)
        {
            Fail("Journal did not start as an empty runtime projection.");
            return;
        }

        if (!await bridge.StartNewGameAsync())
        { Fail("Journal flow could not finish the fresh campaign entrypoint."); return; }
        journal.Open(bridge);
        if (!journal.CurrentObjectiveText.Contains("прочитать сообщение мамы", StringComparison.Ordinal)
            || journal.CurrentObjectiveText.Contains("Выяснить, что случилось с Маратом.", StringComparison.Ordinal))
        { Fail("A fresh journal did not present the current personal arrival action."); return; }
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);

        await bridge.HandleOldPcInputAsync(Input("open"));
        if (bridge.JournalEntries().Count != 1)
        {
            Fail("Opening a key evidence document did not record its readable source.");
            return;
        }

        await bridge.HandleOldPcInputAsync(Input("save"));
        await bridge.HandleOldPcInputAsync(Input("save"));
        var entries = bridge.JournalEntries();
        if (entries.Count != 1
            || entries[0].EntryId != OfficialNotice
            || entries[0].Title != "Справка о смерти Марата Н.")
        {
            Fail("The old PC save action did not create one resolved journal entry.");
            return;
        }

        journal.Open(bridge);
        if (journal.RenderedEntryCount != 1
            || journal.ActiveEntryId != OfficialNotice
            || !journal.CurrentObjectiveText.Contains("прочитать сообщение мамы", StringComparison.Ordinal)
            || !journal.LearnedVocabularyText.Contains("урман", StringComparison.Ordinal)
            || !journal.LearnedVocabularyText.Contains("лес", StringComparison.Ordinal))
        {
            Fail("Journal UI did not render the shared runtime projection, active objective and first Tatar vocabulary meaning.");
            return;
        }

        var oldPc = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        var results = oldPc?.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        if (oldPc is null || results is null)
        {
            Fail("Journal flow could not resolve the old-PC UI for save-status verification.");
            return;
        }

        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        oldPc.Open(bridge);
        var officialIndex = Enumerable.Range(0, results.ItemCount)
            .FirstOrDefault(index => results.GetItemMetadata(index).AsString() == OfficialNotice, -1);
        if (officialIndex < 0)
        {
            Fail("Old-PC UI did not expose the official notice for save-status verification.");
            return;
        }

        results.EmitSignal(ItemList.SignalName.ItemSelected, officialIndex);
        await Frames(4);
        oldPc.GetNode<Button>("Screen/Computer/Layout/Footer/Save").EmitSignal(Button.SignalName.Pressed);
        await Frames(8);
        if (!oldPc.StatusText.Contains("Откройте журнал [J]", StringComparison.Ordinal))
        {
            Fail($"Old-PC save status did not point the keyboard player to the journal: status='{oldPc.StatusText}' activeDoc='{oldPc.ActiveDocumentId ?? "<null>"}'");
            return;
        }

        oldPc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(2);

        const string register = "urman.oldpc:document/rec_marat_case_register_conflict";
        var pair = new[] { OfficialNotice, register };
        var compare = "urman.chapter1:interaction/compare-records-contradiction";
        if (await bridge.CompareJournalSourcesAsync(compare, pair))
        { Fail("Comparison accepted a source that was not found."); return; }
        const string memoryKey = "urman.chapter1:knowledge/memory_marat_childhood_photo";
        if (bridge.JournalEntries().Any(entry => entry.SourceId == memoryKey))
        { Fail("The arrival memory was supplied before its personal source action."); return; }
        // The first block isolated the empty journal and old-PC save projection.
        // Re-enter a normal fresh campaign before the arrival proof, whose
        // source-order assertions correctly forbid already knowing the notice.
        if (!await bridge.StartNewGameAsync())
        { Fail("Journal flow could not start the authored source-access route."); return; }
        await Frames(6);
        await Act1ArrivalFlowProof.CompleteAsync(this, bridge);
        if (!await Move(bridge, main, "arrival-enter-house", "house", "house_old_pc", "entry")) return;
        await bridge.HandleOldPcInputAsync(Input("open"));
        if (!await bridge.DispatchInteractionAsync("urman.chapter1:interaction/talk-gulsina")
            || !await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/gulsina_yaramyy", "home-warning")
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/gulsina_yaramyy", "home-warning", "ask-marat")
            || !await Act1FamilyMealProof.CompleteAsync(this, bridge)
            || !await Move(bridge, main, "house-to-route", "crossroad_signs_inspect", "village_day", "from_house")
            || !await bridge.DispatchInteractionAsync("urman.chapter1:interaction/talk-alsu")
            || !await Act1AlsuWalkProof.CompleteAsync(this, bridge)
            || !await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/alsu_route_context", "name-road")
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/alsu_route_context", "name-road", "ask-versions")
            || !await bridge.CompareJournalSourcesAsync("urman.chapter1:interaction/compare-versions-scope",
                new[] { OfficialNotice, "urman.chapter1:knowledge/clue_alsu_heard_versions" })
            || !await Move(bridge, main, "route-to-fap", "fap_waiting_room_day", "fap_clinic", "waiting_room")
            || !await bridge.DispatchInteractionAsync("urman.chapter1:interaction/talk-naila")
            || !await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/naila_medical_record", "official-wording")
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "official-wording", "ask-wording")
            || !bridge.IsOldPcDocumentAccessible(register)
            || !await Move(bridge, main, "fap-to-document-desk", "fap_pressure_document_desk")
            || !await Move(bridge, main, "fap-document-desk-to-official-record", "evidence-official-death")
            || !await Move(bridge, main, "official-to-internal-register", "evidence-internal-register", "house_old_pc", "entry"))
        { Fail("Journal flow did not reach the register through the authored family, account check and Naila permission."); return; }
        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "open", documentId = register }));
        var before = bridge.ActiveSceneId;
        if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/contradiction_marat_official_vs_internal").GetProperty("status").GetString() == "confirmed")
        { Fail("Reading the register still completed the comparison automatically."); return; }
        if (await bridge.CompareJournalSourcesAsync(compare, pair))
        { Fail("Two readable documents bypassed selecting their actual source fields."); return; }
        await Act1SourceExcerptProof.RecordNoticeCauseAsync(this, bridge);
        await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, bridge);
        var countBeforeRecordComparison = bridge.JournalEntries().Count;
        journal.Open(bridge);
        if (!journal.CurrentObjectiveText.Contains("Показать Наиле", StringComparison.Ordinal))
        { Fail("The journal did not offer comparison after both required sources were found."); return; }
        journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 1;
        for (var slot = 0; slot < 2; slot++)
        {
            var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot + 1}/Source");
            var index = Enumerable.Range(1, picker.ItemCount - 1).Single(index => picker.GetItemMetadata(index).AsString() == pair[slot]);
            picker.Select(index);
            picker.EmitSignal(OptionButton.SignalName.ItemSelected, index);
        }
        var choices = journal.GetNode<VBoxContainer>("Screen/Book/Layout/Comparisons/Layout/Hypotheses");
        // Press every wrong hypothesis, not just the first one: each of them must
        // leave the deduction unconfirmed and the comparison open, and a single
        // pressed branch would not prove that for its siblings.
        var correctText = bridge.ResolveText("urman.chapter1:text/compare-records-contradiction");
        var wrongTexts = choices.GetChildren().OfType<Button>().Select(button => button.Text)
            .Where(text => text != correctText).ToArray();
        if (wrongTexts.Length < 2)
        { Fail($"The comparison offered {wrongTexts.Length} wrong hypotheses; at least two are authored."); return; }
        foreach (var wrongText in wrongTexts)
        {
            var wrong = choices.GetChildren().OfType<Button>().Single(button => button.Text == wrongText);
            wrong.EmitSignal(Button.SignalName.Pressed);
            await Frames(8);
            if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/contradiction_marat_official_vs_internal").GetProperty("status").GetString() == "confirmed"
                || choices.GetChildCount() != 3)
            { Fail($"The wrong hypothesis '{wrongText}' completed or locked the comparison."); return; }
        }
        choices.GetChildren().OfType<Button>().Single(button => button.Text == bridge.ResolveText("urman.chapter1:text/compare-records-contradiction"))
            .EmitSignal(Button.SignalName.Pressed);
        await Frames(8);
        if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/clue_record_wording_mismatch").GetProperty("status").GetString() != "confirmed"
            || bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/contradiction_marat_official_vs_internal").GetProperty("status").GetString() != "hidden"
            || bridge.ActiveSceneId != before || bridge.JournalEntries().Count != countBeforeRecordComparison + 1
            || bridge.JournalEntries().Count(entry => entry.EntryId == "urman.chapter1:knowledge/clue_record_wording_mismatch") != 1)
        { Fail("Journal choice did not retain the literal mismatch separately from the unverified interpretation."); return; }
        journal._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });

        const string response = "urman.chapter1:knowledge/clue_naila_record_scope";
        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(3);
        if (!await bridge.DispatchInteractionAsync("urman.chapter1:interaction/talk-naila")
            || !await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/naila_medical_record", "follow-up")
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "follow-up", "press-contradiction")
            || bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty(response).GetProperty("status").GetString() != "hidden"
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "record-question", "show-external-wording")
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "matching-formulation", "ask-category-scope"))
        { Fail("Naila's field question did not distinguish the shared wording from the separate category before answering."); return; }
        var countBeforeScopeComparison = bridge.JournalEntries().Count;
        journal.Open(bridge);
        // Both raw documents remain readable when a spoken source is added.
        if (!bridge.JournalEntries().Any(entry => entry.EntryId == OfficialNotice && entry.Body.Contains("несчастного случая"))
            || !bridge.JournalEntries().Any(entry => entry.EntryId == response && entry.Body.Contains("Эту категорию")))
        { Fail("The journal replaced an original source with its interpretation."); return; }
        journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 1;
        for (var slot = 0; slot < 2; slot++)
        {
            var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot + 1}/Source");
            picker.Select(0);
            picker.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
        }
        var scopePair = new[] { register, response };
        for (var slot = 0; slot < 2; slot++)
        {
            var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot + 1}/Source");
            var index = Enumerable.Range(1, picker.ItemCount - 1).Single(index => picker.GetItemMetadata(index).AsString() == scopePair[slot]);
            picker.Select(index);
            picker.EmitSignal(OptionButton.SignalName.ItemSelected, index);
        }
        correctText = bridge.ResolveText("urman.chapter1:text/compare-record-scope");
        wrongTexts = choices.GetChildren().OfType<Button>().Select(button => button.Text)
            .Where(text => text != correctText).ToArray();
        if (wrongTexts.Length != 2)
        { Fail("The spoken-source comparison did not expose both recoverable interpretations."); return; }
        foreach (var wrongText in wrongTexts)
        {
            choices.GetChildren().OfType<Button>().Single(button => button.Text == wrongText)
                .EmitSignal(Button.SignalName.Pressed);
            await Frames(8);
            if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/contradiction_marat_official_vs_internal").GetProperty("status").GetString() != "hidden"
                || choices.GetChildCount() != 3)
            { Fail("An unsupported interpretation of a spoken reply closed the investigation."); return; }
        }
        choices.GetChildren().OfType<Button>().Single(button => button.Text == correctText)
            .EmitSignal(Button.SignalName.Pressed);
        await Frames(8);
        if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/contradiction_marat_official_vs_internal").GetProperty("status").GetString() != "confirmed"
            || bridge.ActiveSceneId != before || bridge.JournalEntries().Count != countBeforeScopeComparison + 1
            || bridge.JournalEntries().Count(entry => entry.EntryId == OfficialNotice) != 1
            || bridge.JournalEntries().Count(entry => entry.EntryId == register) != 1
            || bridge.JournalEntries().Count(entry => entry.EntryId == response) != 1)
        { Fail("The journal did not retain documents, spoken evidence and the separately checked conclusion."); return; }
        journal._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        journal.Open(bridge);
        if (!journal.GetNode<OptionButton>("Screen/Book/Layout/Comparisons/Layout/Source1/Source").HasFocus())
        { Fail("Reopened comparison did not focus its visible source picker."); return; }
        journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 0;
        journal.GetNode<ItemList>("Screen/Book/Layout/WorkArea/Entries").EmitSignal(ItemList.SignalName.ItemSelected, 0);
        var retainedEntry = journal.ActiveEntryId;
        journal.RefreshProjection();
        if (journal.ActiveEntryId != retainedEntry)
        { Fail("Journal refresh discarded the source being reread."); return; }
        journal._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });

        // The actual arrival helper opened the photograph through its physical
        // target. Its personal source must remain readable after the investigation.
        journal.Open(bridge, memoryKey);
        if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty(memoryKey).GetProperty("status").GetString() != "confirmed"
            || bridge.JournalEntries().Count(entry => entry.SourceId == memoryKey) != 1
            || journal.ActiveEntryId != memoryKey)
        { Fail("The earlier personal memory was lost, duplicated, or unavailable for rereading."); return; }
        journal._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });

        GD.Print("journal-flow-smoke: personal arrival and authored source access -> actual selected field excerpts -> manual pair + wrong/retry/right -> field-specific spoken answer -> separate checked conclusion without scene transition; original sources and personal memory retained once");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async Task<bool> Move(RuntimeBridge bridge, Main main, string action, string scene,
        string? zone = null, string spawn = "entry")
    {
        if (!await bridge.DispatchInteractionAsync("urman.chapter1:interaction/" + action)
            || bridge.ActiveSceneId != "urman.chapter1:scene/" + scene)
        { Fail("Journal setup could not follow the authored transition: " + action); return false; }
        if (zone is not null)
        {
            main.SwitchZone(zone, spawn);
            await Frames(3);
            if (bridge.CurrentZoneId != zone || bridge.CurrentSpawnPointId != spawn)
            { Fail("Journal setup did not switch to the authored physical zone: " + zone); return false; }
        }
        return true;
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static JsonElement Input(string type) =>
        JsonSerializer.SerializeToElement(new { type, documentId = OfficialNotice });

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
