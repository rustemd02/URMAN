using Godot;
using Urman.Core.Narrative;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// ACT1-LANG.6 acceptance: the full word cycle on the real session — a
/// chosen starting level seeds family words as hypotheses only, reading a
/// text auto-collects an unknown word with its source, the authored street
/// sign discovery confirms it through the real content path, the journal
/// shows the learned words, and save/load preserves every status.
/// </summary>
public partial class Act1VocabularyCycleSmokeTest : Node
{
    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Vocabulary cycle smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        for (var index = 0; index < 10; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null)
        {
            Fail("Vocabulary cycle smoke could not find the player.");
            return;
        }

        // ACT1-LANG.5: the player chose "some" Tatar before starting.
        player.TatarLanguageLevel = "some";
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null || !await bridge.StartNewGameAsync())
        {
            Fail("Vocabulary cycle smoke could not start a new game.");
            return;
        }

        var vocabulary = VocabularyState(bridge);
        GD.Print("act1-vocabulary-dump-after-start: " + vocabulary.ToString());
        Check(Status(vocabulary, "tt_babai") == "guessed", "starting level 'some' seeds family word бабай as guessed");
        Check(Status(vocabulary, "tt_abi") == "guessed", "starting level 'some' seeds family word әби as guessed");
        Check(Source(vocabulary, "tt_babai") == "urman.starting-knowledge:some", "seeded word records its starting-knowledge source");
        Check(Status(vocabulary, "tt_yul") == "unknown", "starting level 'some' leaves everyday word юл unknown");
        Check(Learned(bridge).Any(entry => entry.Id.EndsWith("tt_babai")), "learned vocabulary projection exposes the seeded word");

        // Use an accessible authored source so the portable card has real examples.
        const string sourceId = "urman.oldpc:document/tw_zirat_customs";
        Check(await bridge.OpenDocumentAsync(sourceId), "accessible Tatarwiki source opens through its content conditions");
        bridge.OpenDocumentUi(sourceId);
        for (var tick = 0; tick < 120 && Status(VocabularyState(bridge), "tt_yul") != "guessed"; tick++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var reader = (DocumentUi)GetTree().GetFirstNodeInGroup("document_ui");
        reader._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        vocabulary = VocabularyState(bridge);
        GD.Print("act1-vocabulary-dump-after-observe: " + vocabulary.ToString());
        Check(Status(vocabulary, "tt_yul") == "guessed", "a read line auto-collects юл as a heard hypothesis");
        Check(Source(vocabulary, "tt_yul") == sourceId, "auto-collected word records its authored source");

        // The authored street-sign discovery confirms юл through real content:
        // its physical world target is used exactly like gameplay uses it.
        var zoneManager = (Main)GetTree().GetFirstNodeInGroup("zone_manager");
        var signTarget = zoneManager.ConnectedWorld!.FindChild("Discovery_main-street-sign-reverse", true, false) as InteractionTarget;
        if (signTarget is null)
        {
            Fail("The street-sign discovery has no physical world target.");
            return;
        }
        signTarget.Interact();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        vocabulary = VocabularyState(bridge);
        GD.Print("act1-vocabulary-dump-after-confirm: " + vocabulary.ToString());
        Check(Status(vocabulary, "tt_yul") == "confirmed", "the authored discovery confirms юл through the ladder");

        // Journal projection shows learned words once the journal is opened.
        if (GetTree().GetFirstNodeInGroup("journal_ui") is JournalUi journalUi)
        {
            // The discovery opens its journal entry after the checkpoint write.
            // Do not race that deliberate navigation with the next tab selection.
            for (var tick = 0; tick < 240 && journalUi.ActiveEntryId != signTarget.JournalEntryId; tick++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(journalUi.ActiveEntryId == signTarget.JournalEntryId, "sign discovery finishes opening its source before browsing words");
            journalUi.Open(bridge);
            var learned = journalUi.LearnedVocabularyText;
            Check(learned.Contains("бабай"), "journal vocabulary line shows the seeded family word");
            Check(learned.Contains("юл"), "journal vocabulary line shows the confirmed road word");
            Check(learned.Contains("услышано"), "journal marks the unconfirmed hypothesis explicitly");
            var tabs = journalUi.GetNode<TabBar>("Screen/Book/Layout/Tabs");
            tabs.CurrentTab = 5;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var page = journalUi.GetNode<Control>("Screen/Book/Layout/Vocabulary");
            var search = page.GetNode<LineEdit>("SearchRow/Search");
            var filter = page.GetNode<OptionButton>("SearchRow/Status");
            var words = page.GetNode<ItemList>("Content/Entries");
            var detail = page.GetNode<RichTextLabel>("Content/Detail");
            GD.Print($"vocabulary-ui: tab={tabs.CurrentTab} title={tabs.GetTabTitle(5)} page={page.IsVisibleInTree()} searchFocus={search.HasFocus()} owner={GetViewport().GuiGetFocusOwner()?.GetPath()}");
            void Search(string text) { search.Text = text; search.EmitSignal(LineEdit.SignalName.TextChanged, text); }
            Check(tabs.GetTabTitle(5) == "Слова" && page.IsVisibleInTree() && search.HasFocus(),
                "portable dictionary opens from the book with visible keyboard search");
            Search("бабай");
            Check(words.ItemCount == 1 && detail.Text.Contains("Гипотеза"), "search finds an unconfirmed family word");
            Search("ЮЛ");
            var roadWord = bridge.LearnedVocabulary().Single(entry => entry.Term == "юл");
            Check(words.ItemCount == 1 && detail.Text.Contains("Подтверждено")
                && detail.Text.Contains(roadWord.SourceTitle) && roadWord.Examples.Count > 0
                && roadWord.Examples.All(example => detail.Text.Contains(example)),
                "case-insensitive search shows confirmed word, real source and its usage examples");
            filter.Select(1);
            filter.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            Check(words.ItemCount == 0 && page.GetNode<Label>("Count").Text.Contains("не найдено"),
                "hypothesis filter excludes confirmed word and explains no matches");
            filter.Select(0);
            filter.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            Search("несуществующее слово");
            Check(words.ItemCount == 0 && !detail.Text.Contains(roadWord.SourceTitle), "empty search clears stale source details");
            Search("юл");
            var accessibility = player.Accessibility with { TextScale = 1.6, HighContrast = true };
            journalUi.ApplyAccessibilitySettings(accessibility);
            for (var tick = 0; tick < 6; tick++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "portable-vocabulary-large-text");
            Check(page.GetGlobalRect().Encloses(search.GetGlobalRect())
                && page.GetGlobalRect().Encloses(detail.GetGlobalRect()), "large-text dictionary controls stay inside the page");
            var frame = OS.GetEnvironment("URMAN_VOCABULARY_FRAME");
            if (!string.IsNullOrEmpty(frame))
            {
                if (!System.IO.Path.IsPathFullyQualified(frame) || System.IO.File.Exists(frame))
                    throw new InvalidOperationException("Vocabulary frame must be an unused absolute path.");
                using var image = GetViewport().GetTexture().GetImage();
                Check(image.SavePng(frame) == Error.Ok, "current large-text vocabulary frame saved");
            }
            journalUi._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!page.IsVisibleInTree() && !player.ModalOpen, "Escape closes the dictionary and releases player control");
        }
        else
        {
            Fail("Vocabulary cycle smoke could not find the journal UI.");
            return;
        }

        // Save/load preserves every status and source.
        if (!await bridge.SaveSlotAsync("quick"))
        {
            Fail("Vocabulary cycle smoke could not save the quick slot.");
            return;
        }
        if (!await bridge.LoadSlotAsync("quick"))
        {
            Fail("Vocabulary cycle smoke could not load the quick slot.");
            return;
        }
        vocabulary = VocabularyState(bridge);
        Check(Status(vocabulary, "tt_babai") == "guessed", "save/load preserves the seeded hypothesis");
        Check(Status(vocabulary, "tt_yul") == "confirmed", "save/load preserves the confirmed word");
        player.TatarLanguageLevel = "none";
        Check(await bridge.StartNewGameAsync(), "fresh unknown-language session starts");
        var freshBook = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        freshBook.Open(bridge);
        freshBook.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 5;
        var freshSearch = freshBook.GetNode<LineEdit>("Screen/Book/Layout/Vocabulary/SearchRow/Search");
        freshSearch.Text = "";
        freshSearch.EmitSignal(LineEdit.SignalName.TextChanged, "");
        var freshWords = freshBook.GetNode<ItemList>("Screen/Book/Layout/Vocabulary/Content/Entries");
        // The authored arrival already introduces урман as a hypothesis.
        Check(freshWords.ItemCount == 1 && freshWords.GetItemText(0) == "? урман"
            && bridge.LearnedVocabulary().Count == 1,
            "empty search in a fresh game shows only its authored word, not the previous session vocabulary");

        GD.Print("act1-vocabulary-cycle: PASS seed-some / auto-hear with source / authored confirm / journal projection / save-load integrity");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private static System.Text.Json.JsonElement VocabularyState(RuntimeBridge bridge) =>
        bridge.SelectRuntimeState().GetProperty("vocabulary");

    private static string Status(System.Text.Json.JsonElement vocabulary, string word) =>
        vocabulary.GetProperty($"urman.chapter1:vocabulary/{word}").GetProperty("status").GetString()!;

    private static string Source(System.Text.Json.JsonElement vocabulary, string word) =>
        vocabulary.GetProperty($"urman.chapter1:vocabulary/{word}").GetProperty("sourceId").GetString()!;

    private static System.Collections.Generic.IReadOnlyList<ResolvedVocabularyEntry> Learned(RuntimeBridge bridge) =>
        bridge.LearnedVocabulary();

    private void Check(bool condition, string message)
    {
        if (!condition)
        {
            Fail("Vocabulary cycle contract failed: " + message);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
