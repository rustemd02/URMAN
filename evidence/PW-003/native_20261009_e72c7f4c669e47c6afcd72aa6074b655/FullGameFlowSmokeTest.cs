using System.Text.Json;
using Godot;
using Urman.Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Semantic contract check for the authored PhotoWorlds campaign graph. This
/// deliberately dispatches through the same campaign conditions/effects while
/// making no claim that the future physical world anchors or ranges exist.
/// The caption step is different: it exercises the existing visible Old PC app
/// with actual player-entered text before the file is printed and mounted.
/// </summary>
public partial class FullGameFlowSmokeTest : Node
{
    private const string Prefix = "urman.fullgame:";
    private const string NamespaceId = "photoworlds-v1";
    private const string PlayerId = "urman.chapter1:character/aidar";
    private const string MaratId = "urman.chapter1:character/marat";
    private const string PrologueQuestion = Prefix + "knowledge/pw-marat-death-question-remains-open";
    private static readonly string[] ActiveQuestIds =
    [
        Prefix + "quest/pw-w01",
        Prefix + "quest/pw-w02",
        Prefix + "quest/pw-w03",
        Prefix + "quest/pw-w04",
        Prefix + "quest/pw-w05"
    ];

    public override async void _Ready()
    {
        Main? main = null;
        try
        {
            main = ResourceLoader.Load<PackedScene>("res://scenes/full_game.tscn")?.Instantiate<Main>();
            Require(main is not null, "The full-game scene could not be instantiated.");
            AddChild(main!);

            await WaitFor(() => Bridge() is { IsPhotoWorldsCampaign: true } bridge
                && bridge.ActiveSceneId == Scene("pw-prologue"),
                "The full-game pack did not enter its authored PhotoWorlds prologue.");

            var runtime = Bridge()!;
            Require(runtime.IsPhotoWorldsCampaign, "The loaded campaign pack has no PhotoWorlds catalog.");
            Require(runtime.SelectRuntimeState().GetProperty("photoworlds").GetProperty("namespaceId").GetString() == NamespaceId,
                "The campaign state namespace does not match photoworlds-v1.");

            await RunRouteAsync(runtime, skipPrologue: false, riverFirst: false, captureCaptionUiFrame: true);
            Require(await runtime.StartNewGameAsync(), "The existing New Game path could not reset the semantic route.");
            await WaitFor(() => runtime.ActiveSceneId == Scene("pw-prologue"),
                "New Game did not reapply the PhotoWorlds prologue entrypoint.");
            await RunRouteAsync(runtime, skipPrologue: true, riverFirst: true, captureCaptionUiFrame: false);

            GD.Print("fullgame-flow-smoke: SEMANTIC PASS — prologue/skip, B01 Niva stop and home tea, W01↔W02 order, W03, independent outside evidence, "
                + "Old PC caption-before-print, W04, optional W05, unresolved Marat question, epilogue and return/revisit; "
                + "PHYSICAL NOT_RUN — PW-075 file/search and printer consumers, PW-094 ordinary route, PW-098 handover/anchors/art.");
        }
        catch (Exception error)
        {
            GD.PushError("Full-game semantic flow failed: " + error);
            GetTree().Quit(1);
            return;
        }
        finally
        {
            if (main is not null && GodotObject.IsInstanceValid(main))
                await GodotSmokeCleanup.ReleaseAsync(main);
        }

        GetTree().Quit(0);
    }

    private async Task RunRouteAsync(RuntimeBridge bridge, bool skipPrologue, bool riverFirst, bool captureCaptionUiFrame)
    {
        Require(bridge.ActiveSceneId == Scene("pw-prologue"), "A route did not start at the prologue.");
        AssertActiveQuestSelection(bridge.SelectRuntimeState());
        var initial = World(bridge.SelectRuntimeState());
        Require(!initial.GetProperty("book").GetProperty("received").GetBoolean(),
            "The family book was marked received before its real handoff interaction.");
        Require(Photo(initial, "P-FOREST").GetProperty("acquired").GetBoolean()
                && Photo(initial, "P-FOREST").GetProperty("mounted").GetBoolean(),
            "The authored starting print is not represented as acquired and mounted.");
        Require(!bridge.CanPreparePhotoWorldCaption("P-COMMON"),
            "A caption became available before the authorized digital file was acquired.");

        if (skipPrologue)
        {
            await InteractAsync(bridge, "pw-prologue-skip", Scene("pw-niva-arrival"));
        }
        else
        {
            await InteractAsync(bridge, "pw-prologue-photo-source");
            await InteractAsync(bridge, "pw-prologue-branch-move", Scene("pw-niva-arrival"));
        }

        Require(BeatState(bridge.SelectRuntimeState(), "pw-b01-niva-arrival") == "available",
            "The prologue exit did not expose the authored Niva stop as a separate B01 event.");
        Require(BookReceived(bridge.SelectRuntimeState()) == false,
            "Entering the Niva stop silently granted the family book without its handoff.");
        await InteractAsync(bridge, "pw-niva-stop", Scene("pw-home"));
        Require(BeatState(bridge.SelectRuntimeState(), "pw-b01-niva-arrival") == "completed",
            "Stopping the Niva did not complete the authored arrival transition.");
        await ExpectBlockedAsync(bridge, "pw-book-handoff");
        await DialogueAsync(bridge, "pw-arrival-tea-talk", "pw-arrival-tea", "greeting", "stay-for-tea");
        Require(BeatState(bridge.SelectRuntimeState(), "pw-b01-warm-arrival") == "completed"
                && !BookReceived(bridge.SelectRuntimeState()),
            "The home welcome/tea exchange was skipped or silently granted the book.");
        await ExpectBlockedAsync(bridge, "pw-enter-forest");
        await ExpectBlockedAsync(bridge, "pw-enter-river");
        await InteractAsync(bridge, "pw-book-handoff");
        Require(BookReceived(bridge.SelectRuntimeState()), "The explicit family handoff did not set book.received.");
        Require(FactObserved(bridge.SelectRuntimeState(), "prologue_branch_moved") == !skipPrologue,
            "The prologue skip was confused with the physical branch-moving action.");

        await InteractAsync(bridge, "pw-school-copy-source");
        await InteractAsync(bridge, "pw-school-copy-permission");

        if (riverFirst)
        {
            await CompleteRiverAsync(bridge);
            await ExpectBlockedAsync(bridge, "pw-enter-house");
            await PrepareFirstWorldHomeAndHousePhotoAsync(bridge);
            await CompleteHouseWorldAsync(bridge);
            await CompleteForestAsync(bridge);
        }
        else
        {
            await CompleteForestAsync(bridge);
            await PrepareRiverAsync(bridge);
            await CompleteRiverWorldAsync(bridge);
            await PrepareFirstWorldHomeAndHousePhotoAsync(bridge);
            await CompleteHouseWorldAsync(bridge);
        }

        Require(QuestStatus(bridge.SelectRuntimeState(), "pw-w01") == "completed"
                && QuestStatus(bridge.SelectRuntimeState(), "pw-w02") == "completed"
                && QuestStatus(bridge.SelectRuntimeState(), "pw-w03") == "completed",
            "W01/W02/W03 did not complete through their authored objectives.");

        await CompleteBankOutsideEvidenceAsync(bridge);
        await CompletePhotographerOutsideEvidenceAsync(bridge);
        Require(HasIndependentEvidenceSources(bridge.SelectRuntimeState(), "E-BANK-OUTSIDE")
                && HasIndependentEvidenceSources(bridge.SelectRuntimeState(), "E-PHOTOGRAPHER-OUTSIDE"),
            "Outside evidence did not retain its independent constituent sources.");
        await InteractAsync(bridge, "pw-connect-outside-notes");

        await InteractAsync(bridge, "pw-contact-sheet-source");
        await InteractAsync(bridge, "pw-contact-sheet-permission");
        await InteractAsync(bridge, "pw-common-file-source");
        await DialogueAsync(bridge, "pw-common-file-permission", "pw-common-file-permission", "permission", "accept-copy");

        var beforeCaption = Photo(World(bridge.SelectRuntimeState()), "P-COMMON");
        Require(beforeCaption.GetProperty("acquired").GetBoolean()
                && !beforeCaption.GetProperty("mounted").GetBoolean()
                && beforeCaption.GetProperty("pageId").ValueKind == JsonValueKind.Null,
            "The digital file and paper page were conflated before the caption was written.");
        await ExpectBlockedAsync(bridge, "pw-print-common-photo");
        await ExpectBlockedAsync(bridge, "pw-enter-common");
        await PrepareCaptionThroughOldPcAsync(bridge,
            skipPrologue ? "Мы нашли не готовый ответ, а место для собственной подписи." : "Этот снимок хранит общий путь, но не решает, что произошло.",
            captureCaptionUiFrame);

        var caption = World(bridge.SelectRuntimeState()).GetProperty("captions").GetProperty("P-COMMON");
        Require(caption.GetProperty("preparedById").GetString() == PlayerId
                && caption.GetProperty("sourceAuthorId").GetString() == MaratId
                && caption.GetProperty("text").GetString()?.Length > 0,
            "The visible Old PC action did not store actual player text with separate source authorship.");
        Require(!Photo(World(bridge.SelectRuntimeState()), "P-COMMON").GetProperty("mounted").GetBoolean(),
            "Caption preparation silently mounted the digital file as a paper print.");

        await InteractAsync(bridge, "pw-print-common-photo");
        var printed = Photo(World(bridge.SelectRuntimeState()), "P-COMMON");
        Require(printed.GetProperty("mounted").GetBoolean()
                && printed.GetProperty("pageId").GetString() == "PAGE-COMMON",
            "Printing did not mount the authored page for the same PhotoId.");
        await InteractAsync(bridge, "pw-enter-common", Scene("pw-common"));
        await ExpectBlockedAsync(bridge, "pw-common-horizon");
        await InteractAsync(bridge, "pw-common-three-places");
        await InteractAsync(bridge, "pw-common-horizon");
        Require(QuestStatus(bridge.SelectRuntimeState(), "pw-w04") == "completed",
            "W04 did not complete after its typed caption, mounted page and authored narrative objectives.");

        await InteractAsync(bridge, "pw-common-return", Scene("pw-bank-approach"));
        await ExpectBlockedAsync(bridge, "pw-bank-to-epilogue");
        await InteractAsync(bridge, "pw-witness-summer-leaf");
        await DialogueAsync(bridge, "pw-alsu-leaf-talk", "pw-alsu-leaf", "leaf", "agree-to-return");
        await InteractAsync(bridge, "pw-bank-to-epilogue", Scene("pw-epilogue"));
        await InteractAsync(bridge, "pw-take-now-photo");
        Require(World(bridge.SelectRuntimeState()).GetProperty("epilogue").GetProperty("finished").GetBoolean()
                && Photo(World(bridge.SelectRuntimeState()), "S-NOW").GetProperty("mounted").GetBoolean(),
            "The authored epilogue did not finish with Aidar's newly created, mounted photograph.");
        await DialogueAsync(bridge, "pw-home-epilogue-talk", "pw-home-epilogue", "reflection", "leave-open");
        await InteractAsync(bridge, "pw-epilogue-return-home", Scene("pw-home"));

        var finalState = bridge.SelectRuntimeState();
        Require(QuestStatus(finalState, "pw-w04") == "completed"
                && QuestStatus(finalState, "pw-w05") != "completed",
            "The finale required or completed the optional W05 cellar chapter.");
        Require(finalState.GetProperty("knowledge").GetProperty(PrologueQuestion).GetProperty("status").GetString() == "confirmed",
            "The epilogue did not preserve Marat's unresolved-death question as an explicit open point.");
        Require(bridge.IsInteractionAvailable(Interaction("pw-enter-forest")),
            "The player could not revisit W01 after returning home from the epilogue.");
        await InteractAsync(bridge, "pw-enter-forest", Scene("pw-forest"));
        await InteractAsync(bridge, "pw-forest-return", Scene("pw-home"));
        Require(bridge.ActiveSceneId == Scene("pw-home"), "The completed player could not return home after a free revisit.");
    }

    private async Task PrepareRiverAsync(RuntimeBridge bridge)
    {
        await DialogueAsync(bridge, "pw-water-place-talk", "pw-water-place", "place", "ask-name");
        await InteractAsync(bridge, "pw-water-photo-source");
        await InteractAsync(bridge, "pw-water-photo-permission");
        await InteractAsync(bridge, "pw-mount-water-photo");
    }

    private async Task CompleteRiverAsync(RuntimeBridge bridge)
    {
        await PrepareRiverAsync(bridge);
        await CompleteRiverWorldAsync(bridge);
    }

    private async Task CompleteRiverWorldAsync(RuntimeBridge bridge)
    {
        await InteractAsync(bridge, "pw-enter-river", Scene("pw-river"));
        await InteractAsync(bridge, "pw-water-old-approach");
        await InteractAsync(bridge, "pw-water-bypass");
        await InteractAsync(bridge, "pw-water-damp-print");
        Require(QuestStatus(bridge.SelectRuntimeState(), "pw-w02") == "completed",
            "W02 did not complete after its three authored observations.");
        await InteractAsync(bridge, "pw-water-return", Scene("pw-home"));
    }

    private async Task CompleteForestAsync(RuntimeBridge bridge)
    {
        await InteractAsync(bridge, "pw-enter-forest", Scene("pw-forest"));
        await InteractAsync(bridge, "pw-forest-group");
        await InteractAsync(bridge, "pw-forest-photographer");
        await InteractAsync(bridge, "pw-forest-shurale");
        Require(QuestStatus(bridge.SelectRuntimeState(), "pw-w01") == "completed",
            "W01 did not complete after its authored observation and conversation sequence.");
        await InteractAsync(bridge, "pw-forest-return", Scene("pw-home"));
    }

    private async Task PrepareFirstWorldHomeAndHousePhotoAsync(RuntimeBridge bridge)
    {
        await DialogueAsync(bridge, "pw-first-world-home-talk", "pw-first-world-home", "home", "answer-place");
        await InteractAsync(bridge, "pw-house-photo-source");
        await InteractAsync(bridge, "pw-house-photo-permission");
        await InteractAsync(bridge, "pw-mount-house-photo");
    }

    private async Task CompleteHouseWorldAsync(RuntimeBridge bridge)
    {
        await InteractAsync(bridge, "pw-enter-house", Scene("pw-house"));
        await InteractAsync(bridge, "pw-house-shutter");
        await InteractAsync(bridge, "pw-house-two-views");
        await InteractAsync(bridge, "pw-house-camera-position");
        Require(QuestStatus(bridge.SelectRuntimeState(), "pw-w03") == "completed",
            "W03 did not complete after its authored camera-position sequence.");
        await InteractAsync(bridge, "pw-house-return", Scene("pw-home"));
    }

    private async Task CompleteBankOutsideEvidenceAsync(RuntimeBridge bridge)
    {
        await InteractAsync(bridge, "pw-enter-bank-approach", Scene("pw-bank-approach"));
        await ExpectBlockedAsync(bridge, "pw-verify-bank-evidence");
        await InteractAsync(bridge, "pw-bank-copy-source");
        await InteractAsync(bridge, "pw-bank-copy-permission");
        await ExpectBlockedAsync(bridge, "pw-verify-bank-evidence");
        await InteractAsync(bridge, "pw-winter-piles-inspected");
        await InteractAsync(bridge, "pw-verify-bank-evidence");
        Require(EvidenceConfirmed(bridge.SelectRuntimeState(), "E-BANK-OUTSIDE"),
            "The bank outside evidence did not resolve its three typed constituents.");
        await InteractAsync(bridge, "pw-bank-return", Scene("pw-home"));
    }

    private async Task CompletePhotographerOutsideEvidenceAsync(RuntimeBridge bridge)
    {
        await InteractAsync(bridge, "pw-photographer-photo-source");
        await InteractAsync(bridge, "pw-photographer-photo-permission");
        await ExpectBlockedAsync(bridge, "pw-verify-photographer-evidence");
        await DialogueAsync(bridge, "pw-gulsina-author-talk", "pw-gulsina-author", "author", "record-authorship");
        await InteractAsync(bridge, "pw-verify-photographer-evidence");
        Require(EvidenceConfirmed(bridge.SelectRuntimeState(), "E-PHOTOGRAPHER-OUTSIDE"),
            "The photographer outside evidence did not resolve its three typed constituents.");
    }

    private async Task PrepareCaptionThroughOldPcAsync(RuntimeBridge bridge, string text, bool captureUiFrame)
    {
        var ui = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        Require(ui is not null, "The existing Old PC UI is unavailable in the full-game scene.");
        bridge.OpenOldPcUi();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ui!.NewPersonalFile(rich: false);
        var editor = ui.GetNode<TextEdit>("Screen/App_notepad/Content/Text");
        var prepare = ui.GetNode<Button>("Screen/App_notepad/Content/PreparePhotoCaption");
        editor.Text = text;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(ui.GetNode<Control>("Screen").Visible
                && prepare.Visible && !prepare.Disabled,
            "The visible caption action did not enable for the acquired permissioned file.");
        if (captureUiFrame)
            await CaptureCaptionUiFrameAsync(ui, editor, prepare);
        prepare.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFor(() => World(bridge.SelectRuntimeState()).GetProperty("captions").TryGetProperty("P-COMMON", out _),
            "The visible Old PC action did not commit the actual caption text.");
        Require(editor.Text == text, "The Old PC editor changed the caption text during submission.");
        ui.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CaptureCaptionUiFrameAsync(OldPcUi ui, TextEdit editor, Button prepare)
    {
        if (DisplayServer.GetName() == "headless") return;
        Require(ui.GetNode<Control>("Screen/App_notepad").IsVisibleInTree()
                && editor.IsVisibleInTree() && prepare.IsVisibleInTree() && !prepare.Disabled,
            "The caption controls were not visible for the native visual evidence frame.");
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "fullgame/caption-file-authoring-before-print");
        Require(ui.GetNode<Control>("Screen/App_notepad").IsVisibleInTree()
                && editor.IsVisibleInTree() && prepare.IsVisibleInTree(),
            "The actual caption layout closed before native frame readback.");
        using var image = GetViewport().GetTexture().GetImage();
        Require(!image.IsEmpty(), "The native caption UI readback returned no viewport pixels.");
        var width = image.GetWidth();
        var height = image.GetHeight();
        var png = image.SavePngToBuffer();
        Require(width > 0 && height > 0 && png.Length > 0, "The native caption UI could not be encoded as PNG.");
        GD.Print("fullgame-caption-ui-png:" + JsonSerializer.Serialize(new
        {
            frameTag = "caption-file-authoring-before-print",
            activeSceneId = Scene("pw-home"),
            engineFramesDrawn = Engine.GetFramesDrawn(),
            width,
            height,
            editorTextLength = editor.Text.Length,
            captionButtonVisible = prepare.IsVisibleInTree(),
            pngBase64 = Convert.ToBase64String(png)
        }));
    }

    private async Task DialogueAsync(RuntimeBridge bridge, string interaction, string dialogue,
        string node, string choice)
    {
        await InteractAsync(bridge, interaction);
        var dialogueId = Prefix + "dialogue/" + dialogue;
        Require(await bridge.EnterDialogueNodeAsync(dialogueId, node),
            $"The authored dialogue node could not be entered: {dialogueId}/{node}.");
        Require(await bridge.ChooseDialogueAsync(dialogueId, node, choice),
            $"The authored dialogue choice could not be applied: {dialogueId}/{node}/{choice}.");
    }

    private async Task InteractAsync(RuntimeBridge bridge, string localId, string? targetSceneId = null)
    {
        var id = Interaction(localId);
        Require(bridge.IsInteractionAvailable(id), $"Authored interaction is not available: {id} in {bridge.ActiveSceneId}.");
        Require(await bridge.DispatchSemanticInteractionForSmokeAsync(id), $"Authored interaction was rejected: {id}.");
        if (targetSceneId is not null)
            Require(bridge.ActiveSceneId == targetSceneId, $"{id} did not transition to {targetSceneId}; actual={bridge.ActiveSceneId}.");
    }

    private async Task ExpectBlockedAsync(RuntimeBridge bridge, string localId)
    {
        var id = Interaction(localId);
        Require(!bridge.IsInteractionAvailable(id), $"A prerequisite gate opened too early: {id}.");
        Require(!await bridge.DispatchSemanticInteractionForSmokeAsync(id), $"A blocked interaction mutated state: {id}.");
    }

    private async Task WaitFor(Func<bool> predicate, string error)
    {
        for (var frame = 0; frame < 180; frame++)
        {
            if (predicate()) return;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        throw new InvalidOperationException(error);
    }

    private RuntimeBridge? Bridge() => GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;

    private static void AssertActiveQuestSelection(JsonElement state)
    {
        var quests = state.GetProperty("quests").EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).ToArray();
        var expected = ActiveQuestIds.Order(StringComparer.Ordinal).ToArray();
        Require(quests.SequenceEqual(expected), "The runtime initialized old/retired quests or omitted one of W01–W05.");
    }

    private static JsonElement World(JsonElement state) => state.GetProperty("photoworlds");
    private static JsonElement Photo(JsonElement world, string id) => world.GetProperty("photos").GetProperty(id);
    private static JsonElement Evidence(JsonElement state, string id) => World(state).GetProperty("evidence").GetProperty(id);
    private static bool EvidenceConfirmed(JsonElement state, string id) => Evidence(state, id).GetProperty("status").GetString() == "confirmed";
    private static bool HasIndependentEvidenceSources(JsonElement state, string id)
    {
        var evidence = Evidence(state, id);
        var sourceIds = evidence.GetProperty("sourceIds").EnumerateArray().Select(item => item.GetString()!).ToArray();
        return evidence.GetProperty("constituents").GetArrayLength() == 3
            && sourceIds.Length >= 3
            && sourceIds.Distinct(StringComparer.Ordinal).Count() == sourceIds.Length;
    }
    private static bool BookReceived(JsonElement state) => World(state).GetProperty("book").GetProperty("received").GetBoolean();
    private static string BeatState(JsonElement state, string localId)
    {
        var beat = state.GetProperty("beats").GetProperty(Prefix + "beat/" + localId);
        return beat.ValueKind == JsonValueKind.String ? beat.GetString()! : beat.GetProperty("state").GetString()!;
    }
    private static bool FactObserved(JsonElement state, string id) =>
        World(state).GetProperty("facts").TryGetProperty(id, out var fact) && fact.GetProperty("sources").GetArrayLength() > 0;
    private static string QuestStatus(JsonElement state, string localId) =>
        state.GetProperty("quests").GetProperty(Prefix + "quest/" + localId).GetProperty("status").GetString()!;
    private static string Interaction(string localId) => Prefix + "interaction/" + localId;
    private static string Scene(string localId) => Prefix + "scene/" + localId;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
