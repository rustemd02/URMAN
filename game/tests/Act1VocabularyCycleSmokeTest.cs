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

        // ACT1-LANG.2: reading a line that contains юл auto-collects it.
        await bridge.ObserveVocabularyTextAsync(
            "Мансур обмолвился: «юл» — и указал в сторону дороги.", "urman.chapter1:smoke/vocabulary-cycle-line");
        vocabulary = VocabularyState(bridge);
        GD.Print("act1-vocabulary-dump-after-observe: " + vocabulary.ToString());
        Check(Status(vocabulary, "tt_yul") == "guessed", "a read line auto-collects юл as a heard hypothesis");
        Check(Source(vocabulary, "tt_yul") == "urman.chapter1:smoke/vocabulary-cycle-line", "auto-collected word records its source");

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
            journalUi.Open(bridge);
            var learned = journalUi.LearnedVocabularyText;
            Check(learned.Contains("бабай"), "journal vocabulary line shows the seeded family word");
            Check(learned.Contains("юл"), "journal vocabulary line shows the confirmed road word");
            Check(learned.Contains("услышано"), "journal marks the unconfirmed hypothesis explicitly");
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
