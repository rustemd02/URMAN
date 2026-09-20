using Godot;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Focused proof of the contextual hints: the gentle note arrives in the system
/// thread, the archive line names a term on an empty search, nothing repeats
/// after a reopen, a hint writes no knowledge, and the direct level stays closed
/// until the player has really read two records.
/// </summary>
public partial class OldPcHintsSmokeTest : Node
{
    private const string GentleHint = "urman.oldpc:hint/ask-naila-registry";
    private const string DirectionHint = "urman.oldpc:hint/compare-dates";
    private const string DirectHint = "urman.oldpc:hint/boundary-not-dates";
    private const string Slot = "oldpc-hints-proof";
    private readonly List<string> _checks = [];

    public override async void _Ready()
    {
        Main? main = null;
        var exit = 1;
        try
        {
            main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")!.Instantiate<Main>();
            AddChild(main);
            await Frames(3);
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge
                ?? throw new InvalidOperationException("Missing shared runtime.");
            var ui = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi
                ?? throw new InvalidOperationException("Missing existing old-PC UI.");
            ui.Open(bridge);
            ui.ApplyAccessibilitySettings(AccessibilitySettingsSnapshot.Default);
            await Frames(4);

            Check(ui.SeenHintIds.Contains(GentleHint), "the level-1 hint is recorded on the first visit: " + string.Join(",", ui.SeenHintIds));
            var self = ui.ChatThreads.Single(thread => thread.Id == "self");
            Check(self.Messages.Any(message => message.Text.Contains("Наилю", StringComparison.Ordinal)),
                "the hint arrives as a note in «Заметки Айдара»: " + string.Join(" | ", self.Messages.Select(message => message.Text)));
            Check(!ui.SeenHintIds.Contains(DirectHint) && !ui.SeenHintIds.Contains(DirectionHint),
                "a fresh reader gets neither the direction nor the direct hint");

            var notesAfterFirstVisit = self.Messages.Count;
            ui.Open(bridge);
            await Frames(4);
            Check(ui.SeenHintIds.Count == 1
                && ui.ChatThreads.Single(thread => thread.Id == "self").Messages.Count == notesAfterFirstVisit,
                "reopening the computer repeats neither the hint nor the note");

            // Read the official notice through the ordinary archive flow; that
            // knowledge is what the level-2 hint waits for.
            var query = ui.GetNode<LineEdit>("Screen/Computer/Layout/SearchRow/Query");
            var search = ui.GetNode<Button>("Screen/Computer/Layout/SearchRow/Search");
            query.Text = "Марат";
            search.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            var results = ui.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
            var noticeRow = Enumerable.Range(0, results.ItemCount)
                .FirstOrDefault(index => results.GetItemMetadata(index).AsString()
                    == "urman.oldpc:document/doc_marat_official_death_notice", -1);
            Check(noticeRow >= 0, "the official notice is reachable through the ordinary archive search");
            results.EmitSignal(ItemList.SignalName.ItemSelected, noticeRow);
            await Frames(8);
            Check(bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText()
                    .Contains("clue_marat_official_death_version", StringComparison.Ordinal),
                "reading the notice is what grants its knowledge");

            // Baselines after the document read: the notice itself earns knowledge
            // and a journal record, and the hint must add neither.
            var knowledgeAfterNotice = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
            var journalAfterNotice = bridge.JournalEntries().Count;
            query.Text = "ццц";
            search.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            var reader = ui.GetNode<RichTextLabel>("Screen/Computer/Layout/WorkArea/ReaderArea/Reader");
            Check(reader.GetParsedText().Contains("Подсказка:", StringComparison.Ordinal)
                && reader.GetParsedText().Contains("учёт", StringComparison.Ordinal),
                "an empty search shows the direction hint and the term to try: " + reader.GetParsedText().Split('\n')[^1]);
            Check(ui.SeenHintIds.Contains(DirectionHint), "the archive carrier records the hint it showed");
            Check(!ui.SeenHintIds.Contains(DirectHint) && ui.FruitlessSearches > 0,
                $"the direct hint still waits for two read records ({ui.FruitlessSearches} fruitless searches)");

            Check(bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == knowledgeAfterNotice,
                "the hint itself adds no knowledge");
            Check(bridge.JournalEntries().Count == journalAfterNotice,
                "the hint adds no journal entry: " + string.Join(",", bridge.JournalEntries().Select(entry => entry.EntryId)));

            Check(await bridge.SaveSlotAsync(Slot), "the hints save with the desktop snapshot");
            Check(await bridge.LoadSlotAsync(Slot), "the hints load back");
            await Frames(6);
            Check(ui.SeenHintIds.Contains(GentleHint) && ui.SeenHintIds.Contains(DirectionHint),
                "the shown hints survive the load without repeating");

            exit = 0;
            GD.Print($"oldpc-hints-smoke: PASS {_checks.Count} checks; no human duration or art claim");
        }
        catch (Exception error)
        {
            GD.PrintErr($"oldpc-hints-smoke: FAIL {error}");
        }
        finally
        {
            if (main is not null) await GodotSmokeCleanup.ReleaseAsync(main);
            GetTree().Quit(exit);
        }
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private void Check(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException(name);
        _checks.Add(name);
        GD.Print($"oldpc-hints: check {name}");
    }
}
