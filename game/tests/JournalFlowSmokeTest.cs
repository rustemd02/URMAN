using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class JournalFlowSmokeTest : Node
{
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";

    public override async void _Ready()
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
        if (!journal.CurrentObjectiveText.Contains("Выяснить, что случилось с Маратом.", StringComparison.Ordinal))
        { Fail("A fresh journal revealed the later source-comparison objective."); return; }
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
            || !journal.CurrentObjectiveText.Contains("Выяснить, что случилось с Маратом.", StringComparison.Ordinal)
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
        if (!await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "official-wording", "ask-wording"))
        { Fail("Journal flow could not obtain Naila's authored record permission."); return; }
        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "open", documentId = register }));
        var before = bridge.ActiveSceneId;
        if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/contradiction_marat_official_vs_internal").GetProperty("status").GetString() == "confirmed")
        { Fail("Reading the register still completed the comparison automatically."); return; }
        journal.Open(bridge);
        if (!journal.CurrentObjectiveText.Contains("Сопоставить справку о смерти Марата", StringComparison.Ordinal))
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
        var wrong = choices.GetChildren().OfType<Button>().First();
        wrong.EmitSignal(Button.SignalName.Pressed);
        await Frames(8);
        if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/contradiction_marat_official_vs_internal").GetProperty("status").GetString() == "confirmed"
            || choices.GetChildCount() != 3)
        { Fail("A wrong hypothesis completed or locked the comparison."); return; }
        choices.GetChildren().OfType<Button>().Single(button => button.Text == bridge.ResolveText("urman.chapter1:text/compare-records-contradiction"))
            .EmitSignal(Button.SignalName.Pressed);
        await Frames(8);
        if (bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/contradiction_marat_official_vs_internal").GetProperty("status").GetString() != "confirmed"
            || bridge.ActiveSceneId != before || bridge.JournalEntries().Count != 2)
        { Fail("Journal choice did not confirm the deduction while preserving its sources and scene."); return; }
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
        GD.Print("journal-flow-smoke: key sources -> manual pair + wrong/retry/right -> shared conclusion without scene transition");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
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
