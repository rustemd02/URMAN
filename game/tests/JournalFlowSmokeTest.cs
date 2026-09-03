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

        await bridge.HandleOldPcInputAsync(Input("open"));
        if (bridge.JournalEntries().Count != 0)
        {
            Fail("Opening a document added it before the explicit journal action.");
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
            || !journal.CurrentObjectiveText.Contains("Сопоставить справку о смерти Марата", StringComparison.Ordinal)
            || !journal.LearnedVocabularyText.Contains("урман", StringComparison.Ordinal)
            || !journal.LearnedVocabularyText.Contains("граница старых правил", StringComparison.Ordinal))
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

        GD.Print("journal-flow-smoke: old PC save -> journal shortcut + active objective projection");
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
