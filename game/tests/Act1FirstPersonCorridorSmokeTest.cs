using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Exercises the production Act 1 interaction route: first-person camera ray,
/// physical target, mapped E input, zone transitions and document/dialogue UI.
/// Approach fixtures set the portable player transform; source comparisons also
/// call the runtime API. This does not prove pedestrian reachability, first-run
/// understanding, interest or duration. The walkthrough owns continuous walking.
/// </summary>
public partial class Act1FirstPersonCorridorSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string SavedMessage = "urman.oldpc:document/msg_marat_saved_last_normal";
    private const string BoundarySource = "urman.oldpc:document/tw_shurale_urman_boundary";
    private const string EdgeSketch = "urman.oldpc:document/doc_kara_urman_edge_sketch";
    private const float InteractionStandOff = 1.5f;

    public override async void _Ready()
    {
        try { await RunAsync(); }
        catch (Exception exception)
        {
            GD.PushError("First-person corridor exception: " + exception);
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn");
        var demo = packed?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Act 1 first-person corridor could not instantiate the demo entrypoint.");
            return;
        }

        AddChild(demo);
        await Frames(8);

        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Act 1 first-person corridor could not start through the main menu.");
            return;
        }

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = main.GetNodeOrNull<FirstPersonController>("Player");
        var ray = player?.GetNodeOrNull<RayCast3D>("Head/Camera3D/InteractionRay");
        if (bridge is null || player is null || ray is null || !demo.IntroVisible)
        {
            Fail("Act 1 first-person corridor did not expose the demo bridge, player, ray and intro.");
            return;
        }

        if (!main.EnableAct1ConnectedWorld || main.ConnectedWorld is null)
        {
            Fail("Act 1 first-person corridor did not start through the connected-world demo mode.");
            return;
        }

        // The launch smoke owns the intro-specific control wording; here we
        // dismiss it through the same mapped keyboard event as a player.
        demo._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        await Frames(2);
        if (demo.IntroVisible || player.ModalOpen)
        {
            Fail("Act 1 first-person corridor could not dismiss its intro card with E.");
            return;
        }
        // The skipped forest teaser returns its zone asynchronously; let the
        // village settle before the corridor's scripted evidence flow starts.
        var corridorBridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        for (var frame = 0; frame < 600 && corridorBridge?.CurrentZoneId != "village_day"; frame++)
            await Frames(1);
        if (corridorBridge?.CurrentZoneId != "village_day")
        {
            Fail("Act 1 first-person corridor did not settle at the village after the prologue.");
            return;
        }

        await Act1ArrivalFlowProof.CompleteAsync(this, bridge);
        await InteractAt(player, ray, Interaction("arrival-enter-house"));
        await Frames(4);
        AssertState(main, bridge, "house_old_pc", "house", "res://scenes/zones/style_benchmark_house_pc.tscn");
        AssertInteriorFacing(main, bridge, "house-entry");
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_family_avoids_marat") != "hidden")
        { Fail("Entering the house inferred the family's answer before asking about Marat."); return; }

        var mansurActor = main.ConnectedWorld?.FindChild("Npc_mansur", true, false) as Node3D;
        var mansurProxy = FindInteraction(Interaction("talk-mansur"), GetTree().Root);
        if (mansurActor is null || mansurProxy is null)
        {
            Fail("The corridor could not find both the visible Mansur actor and his separate interaction target.");
            return;
        }
        var mansurProxyTransform = mansurProxy.GlobalTransform;
        var mansurFeet = mansurActor.GlobalPosition;
        await InteractAt(player, ray, Interaction("talk-mansur"));
        await Frames(5);
        var mansurDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (mansurDialogue is null || !mansurDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical corridor did not open Mansur's request dialogue before the old PC.");
            return;
        }
        if (bridge.IsInteractionAvailable(Interaction("oldpc-power")))
        { Fail("Opening Mansur's conversation granted computer access before an answer."); return; }
        // A metadata-only check used to pass while the invisible ray target
        // rotated and the visible person did nothing. Wait for the authored
        // turn, then inspect the actor's actual facing and planted origin.
        await ToSignal(GetTree().CreateTimer(.60d), SceneTreeTimer.SignalName.Timeout);
        var mansurFacing = mansurActor.GetMeta("conversationFacing", "unset").AsString();
        var mansurDistance = mansurActor.GlobalPosition.DistanceTo(player.GlobalPosition);
        var toPlayer = player.GlobalPosition - mansurActor.GlobalPosition;
        toPlayer.Y = 0f;
        var forward = mansurActor.GlobalBasis.Z;
        forward.Y = 0f;
        var alignment = forward.Normalized().Dot(toPlayer.Normalized());
        if (mansurFacing != "towards-player" || mansurDistance > 2.9f
            || mansurActor.GetMeta("characterId", "").AsString() != "mansur"
            || alignment < Mathf.Cos(Mathf.DegToRad(3f))
            || mansurActor.GlobalPosition.DistanceTo(mansurFeet) > .001f
            || !mansurProxy.GlobalTransform.IsEqualApprox(mansurProxyTransform))
        {
            Fail($"Visible Mansur did not face the player on planted feet with a stable target: facing={mansurFacing} distance={mansurDistance:0.00}m alignment={alignment:0.000}");
            return;
        }
        GD.Print("act1-npc-facing: Mansur visible geometry faces the player; feet and interaction target stayed fixed");
        var mansurChoices = mansurDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().ToArray();
        var offerHelp = mansurChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-offer-help"));
        var askWhy = mansurChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-why"));
        if (offerHelp is null || askWhy is null)
        {
            Fail("Mansur's start node did not expose both the offer-help and state-gated question choices.");
            return;
        }
        offerHelp.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        mansurDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (mansurDialogue.IsOpen || player.ModalOpen
            || !bridge.IsInteractionAvailable(Interaction("oldpc-power")))
        {
            Fail("Mansur's dialogue did not release the player or grant old-PC access in the corridor.");
            return;
        }

        await InteractAt(player, ray, Interaction("oldpc-power"));
        await Frames(6);
        var oldPc = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        if (oldPc is null || !oldPc.GetNode<Control>("Screen").Visible || !player.ModalOpen)
        {
            Fail("Physical old-PC target did not open the archive UI in the corridor.");
            return;
        }

        if (bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Corridor house exit became available before the first old-PC clue was read.");
            return;
        }

        var oldPcResults = oldPc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        var officialNoticeIndex = Enumerable.Range(0, oldPcResults.ItemCount)
            .FirstOrDefault(index => oldPcResults.GetItemMetadata(index).AsString() == OfficialNotice, -1);
        if (officialNoticeIndex < 0)
        {
            Fail("Corridor could not locate the official Marat notice in the old-PC archive.");
            return;
        }

        oldPcResults.EmitSignal(ItemList.SignalName.ItemSelected, officialNoticeIndex);
        await Frames(8);
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_official_death_version") != "confirmed"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("The official Marat notice confirmed, but the corridor house exit unlocked before Gulsina's warning.");
            return;
        }

        oldPc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);

        if (bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Corridor house exit became available before the physical Gulsina warning dialogue.");
            return;
        }

        await InteractAt(player, ray, Interaction("talk-gulsina"));
        await Frames(5);
        var gulsinaDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (gulsinaDialogue is null || !gulsinaDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical corridor did not open Gulsina's warning dialogue before leaving the house.");
            return;
        }
        var gulsinaChoices = gulsinaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().ToArray();
        var stayForTea = gulsinaChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-stay-for-tea"));
        var askYaramyy = gulsinaChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-yaramyy"));
        if (stayForTea is null || askYaramyy is null)
        {
            Fail("Gulsina's start node did not expose both the tea alternative and guessed ярамый question.");
            return;
        }
        stayForTea.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (!await Act1FamilyMealProof.CompleteAsync(this, bridge, interactPhysically: async action =>
            { await InteractAt(player, ray, Interaction(action)); return gulsinaDialogue.IsOpen; })) return;
        var gulsinaState = bridge.SelectRuntimeState();
        if (gulsinaDialogue.IsOpen || player.ModalOpen
            || VocabularyStatus(gulsinaState, "tt_yaramyy") != "guessed"
            || !NpcState(gulsinaState, "gulsina", "warning_heard")
            || KnowledgeStatus(gulsinaState, "clue_family_avoids_marat") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Tea did not preserve Gulsina's warning or incorrectly supplied an unasked answer about Marat.");
            return;
        }

        await InteractAt(player, ray, Interaction("talk-gulsina"));
        if (!await ChooseVisibleDialogue(bridge, "choice-gulsina-marat")) return;
        gulsinaDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (gulsinaDialogue.IsOpen || player.ModalOpen
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_family_avoids_marat") != "confirmed"
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        { Fail("The actual family question did not let Aidar take the contradiction to the street."); return; }

        await InteractAt(player, ray, Interaction("house-to-route"));
        await Frames(4);
        AssertState(main, bridge, "village_day", "crossroad_signs_inspect", "res://scenes/zones/style_benchmark_day_street.tscn");
        AssertRouteFacing(main, AgentBAct1Layout.HouseDoorYawDegrees + 180f, "house-exit-to-yard");

        // The legacy benchmark sign is gone from this composition, and its
        // effectless interaction must be gone with it: keeping the box while
        // hiding the board left a prompt over empty snow. This checks the
        // player-visible property rather than IsInteractionAvailable, which only
        // evaluates content conditions and would stay true either way.
        var signTargets = main.FindChildren("*", "StaticBody3D", recursive: true, owned: false)
            .OfType<InteractionTarget>().ToArray();
        var legacySign = signTargets.FirstOrDefault(target => target.InteractionId == Interaction("village-sign"));
        if (legacySign is not null && legacySign.CollisionLayer != 0)
        {
            Fail($"The removed legacy sign still exposes a prompt on empty snow: {legacySign.GetPath()} layer={legacySign.CollisionLayer}");
            return;
        }
        var readableSign = signTargets.FirstOrDefault(target =>
            target.InteractionId == Interaction("discover-main-street-sign-reverse"));
        if (readableSign is null || readableSign.CollisionLayer == 0)
        {
            Fail("Hiding the legacy sign also removed the readable authored sign's discovery target.");
            return;
        }

        // Repeatable audit for the defect class fixed above: a live interaction
        // whose box sits on empty snow. Run with URMAN_GHOST_AUDIT=1. It reported
        // 22 live targets and none without visible geometry after the sign fix,
        // so the ghost prompt was the only one of its kind.
        if (System.Environment.GetEnvironmentVariable("URMAN_GHOST_AUDIT") == "1")
        {
            var hidden = new List<string>();
            var visibleMeshes = main.FindChildren("*", "MeshInstance3D", recursive: true, owned: false)
                .OfType<MeshInstance3D>()
                .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree())
                .Select(mesh => mesh.GlobalTransform * mesh.Mesh!.GetAabb()).ToArray();
            foreach (var target in signTargets.Where(t => t.CollisionLayer != 0))
            {
                var position = target.GlobalPosition;
                var inside = visibleMeshes.Any(bounds => bounds.Grow(0.35f).HasPoint(position));
                if (!inside) hidden.Add($"{target.Name}@{position.Snapped(Vector3.One * 0.1f)}");
            }
            GD.Print($"ghost-audit: live_targets={signTargets.Count(t => t.CollisionLayer != 0)} without_visible_geometry={hidden.Count} [{string.Join(" | ", hidden)}]");

            // Ambiguous-prompt probe: two live targets closer than a stride can
            // hand the crosshair back and forth between their prompts.
            var live = signTargets.Where(t => t.CollisionLayer != 0).ToArray();
            var close = new List<string>();
            for (var i = 0; i < live.Length; i++)
            for (var j = i + 1; j < live.Length; j++)
            {
                var gap = live[i].GlobalPosition.DistanceTo(live[j].GlobalPosition);
                if (gap < 1.5f)
                {
                    close.Add($"{live[i].Name}~{live[j].Name}@{gap:F2}m");
                }
            }
            GD.Print($"prompt-audit: live={live.Length} closer_than_1.5m={close.Count} [{string.Join(" | ", close)}]");
        }

        if (bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        {
            Fail("FAP route became available before Alsu's contradiction dialogue.");
            return;
        }
        await InteractAt(player, ray, Interaction("talk-alsu"));
        await Frames(5);
        var alsuDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (alsuDialogue is null || !alsuDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical corridor did not open Alsu's route dialogue before the FAP.");
            return;
        }
        if (bridge.IsInteractionAvailable(Interaction("route-to-fap"))
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_versions_conflict") != "hidden")
        { Fail("Alsu's greeting granted the contradiction before a question."); return; }
        alsuDialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(2);
        if (bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        { Fail("Closing Alsu's unanswered conversation unlocked the FAP."); return; }
        if (!await Act1AlsuWalkProof.CompleteAsync(this, bridge)) return;
        await InteractAt(player, ray, Interaction("talk-alsu"));
        if (!await ChooseVisibleDialogue(bridge, "choice-alsu-versions")
            || !await ChooseVisibleDialogue(bridge, "choice-alsu-go-to-naila")) return;
        if (alsuDialogue.IsOpen || player.ModalOpen
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_heard_versions") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_versions_conflict") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        { Fail("Alsu's account supplied its comparison before the player checked the two sources."); return; }
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-versions-scope"),
                new[] { OfficialNotice, ChapterPrefix + "knowledge/clue_alsu_heard_versions" }))
        { Fail("The heard account could not be compared with the official notice."); return; }
        if (alsuDialogue.IsOpen || player.ModalOpen
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_versions_conflict") != "confirmed"
            || !bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        {
            Fail("Alsu's answer about conflicting accounts did not unlock the FAP route.");
            return;
        }

        await InteractAt(player, ray, Interaction("route-to-fap"));
        await Frames(4);
        AssertState(main, bridge, "fap_clinic", "fap_waiting_room_day", "res://scenes/zones/chapter1_fap_clinic.tscn");
        AssertInteriorFacing(main, bridge, "fap-entry-to-desk");

        if (bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        {
            Fail("FAP document desk became available before Naila's dialogue.");
            return;
        }
        await InteractAt(player, ray, Interaction("talk-naila"));
        await Frames(5);
        var nailaDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (nailaDialogue is null || !nailaDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical corridor did not open Naila's dialogue before the document desk.");
            return;
        }
        var nailaQuestion = nailaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/dialogue-naila-ask-record"));
        if (nailaQuestion is null)
        { Fail("Naila's initial wording question was missing."); return; }
        nailaQuestion.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        nailaDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (nailaDialogue.IsOpen || player.ModalOpen
            || !bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        {
            Fail("Closing Naila's dialogue did not unlock the FAP document desk.");
            return;
        }

        await InteractAt(player, ray, Interaction("fap-to-document-desk"));
        await Frames(3);

        await InteractAt(player, ray, Interaction("fap-document-desk-to-official-record"));
        await Frames(6);
        AssertDocument(OfficialNotice);
        await SaveOpenDocumentToJournal(OfficialNotice);
        CloseDocument();
        await Frames(3);

        await InteractAt(player, ray, Interaction("official-leave-clinic"));
        await Frames(5);
        AssertState(main, bridge, "village_day", "evidence-official-death", "res://scenes/zones/style_benchmark_day_street.tscn");
        await InteractAt(player, ray, Interaction("official-to-internal-register"));
        await Frames(5);
        AssertState(main, bridge, "house_old_pc", "evidence-internal-register", "res://scenes/zones/style_benchmark_house_pc.tscn");
        AssertInteriorFacing(main, bridge, "return-to-house-pc");

        await InteractAt(player, ray, Interaction("oldpc-power"));
        await Frames(4);
        var archive = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        var archiveRows = archive!.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        var registerIndex = Enumerable.Range(0, archiveRows.ItemCount).Single(index => archiveRows.GetItemMetadata(index).AsString() == "urman.oldpc:document/rec_marat_case_register_conflict");
        archiveRows.EmitSignal(ItemList.SignalName.ItemSelected, registerIndex);
        await Frames(6);
        if (archive.ActiveDocumentId != "urman.oldpc:document/rec_marat_case_register_conflict")
        { Fail("The returning player could not read the internal register on the old PC."); return; }
        archive.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(2);
        if (await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { OfficialNotice, "urman.oldpc:document/rec_marat_case_register_conflict" }))
        { Fail("Reading the two documents bypassed selecting their source fields."); return; }
        await Act1SourceExcerptProof.RecordNoticeCauseAsync(this, bridge);
        await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, bridge);
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { OfficialNotice, "urman.oldpc:document/rec_marat_case_register_conflict" }))
        { Fail("The record comparison was rejected."); return; }
        var registerScene = bridge.ActiveSceneId;
        var scopePair = new[] { "urman.oldpc:document/rec_marat_case_register_conflict", ChapterPrefix + "knowledge/clue_naila_record_scope" };
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_record_wording_mismatch") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "contradiction_marat_official_vs_internal") != "hidden"
            || await bridge.DispatchInteractionAsync(Interaction("internal-register-to-rinat"))
            || await bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), scopePair))
        { Fail("A raw mismatch substituted for checking the meaning with the source holder."); return; }

        // Revisit the actual clinic through the existing physical doors. These
        // presentation repeats must preserve the investigation scene; a test
        // SwitchZone call would conceal a closed door in the player route.
        await InteractAt(player, ray, Interaction("house-to-route"));
        await InteractAt(player, ray, Interaction("route-to-fap"));
        AssertState(main, bridge, "fap_clinic", "evidence-internal-register", "res://scenes/zones/chapter1_fap_clinic.tscn");
        await InteractAt(player, ray, Interaction("talk-naila"));
        if (!await ChooseVisibleDialogue(bridge, "choice-naila-contradiction")
            || !await ChooseVisibleDialogue(bridge, "choice-naila-show-external-wording")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_naila_record_scope") != "hidden")
        { Fail("Showing matching wording answered the separate category question before it was asked."); return; }
        if (!await ChooseVisibleDialogue(bridge, "choice-naila-ask-category-scope")
            || !await ChooseVisibleDialogue(bridge, "choice-naila-check-answer")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_naila_record_scope") != "confirmed"
            || VocabularyStatus(bridge.SelectRuntimeState(), "tt_yaramyy") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "contradiction_marat_official_vs_internal") != "hidden"
            || !await bridge.SaveSlotAsync("corridor-before-record-inference")
            || await bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), new[] { OfficialNotice, ChapterPrefix + "knowledge/clue_naila_record_scope" }))
        { Fail("Naila's response was missing, awarded a conclusion automatically, or accepted the wrong source pair."); return; }
        foreach (var wrong in new[] { "compare-record-scope-cause", "compare-record-scope-lie" })
            if (!await bridge.CompareJournalSourcesAsync(Interaction(wrong), scopePair)
                || KnowledgeStatus(bridge.SelectRuntimeState(), "contradiction_marat_official_vs_internal") != "hidden")
            { Fail("An unsupported interpretation of Naila's answer awarded the investigation key."); return; }
        if (!await bridge.LoadSlotAsync("corridor-before-record-inference")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_naila_record_scope") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "contradiction_marat_official_vs_internal") != "hidden"
            || bridge.JournalEntries().Count(entry => entry.EntryId == ChapterPrefix + "knowledge/clue_naila_record_scope") != 1
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), scopePair))
        { Fail("The unfinished source check did not restore, or could not be corrected after loading."); return; }
        await Frames(4);

        foreach (var originalQuestion in new[] { "dialogue-naila-ask-record", "choice-naila-transfer" })
        {
            await InteractAt(player, ray, Interaction("talk-naila"));
            if (!await ChooseVisibleDialogue(bridge, originalQuestion)) return;
            var replyUi = (DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui");
            replyUi.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
            await Frames(4);
        }
        await InteractAt(player, ray, Interaction("official-leave-clinic"));
        await InteractAt(player, ray, Interaction("official-to-internal-register"));
        AssertState(main, bridge, "house_old_pc", "evidence-internal-register", "res://scenes/zones/style_benchmark_house_pc.tscn");
        if (bridge.ActiveSceneId != registerScene)
        { Fail("A repeat door replayed an obsolete narrative transition."); return; }

        // A learned word opens a return to the family conversation. The two
        // spoken contexts can be checked without making them a main-route key.
        await InteractAt(player, ray, Interaction("talk-gulsina"));
        if (!await ChooseVisibleDialogue(bridge, "choice-gulsina-yaramyy")) return;
        ((DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui"))
            .GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        var warningPair = new[] { ChapterPrefix + "knowledge/clue_gulsina_warning_context", ChapterPrefix + "knowledge/clue_naila_record_scope" };
        var questsBeforeWarnings = bridge.SelectRuntimeState().GetProperty("quests").GetRawText();
        foreach (var wrong in new[] { "compare-warning-agreement", "compare-warning-rule" })
            if (!await bridge.CompareJournalSourcesAsync(Interaction(wrong), warningPair)
                || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_yaramyy_contexts_distinguished") != "hidden")
            { Fail("A shared word was treated as proof of an agreement or a forest rule."); return; }
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-warning-contexts"), warningPair)
            || VocabularyStatus(bridge.SelectRuntimeState(), "tt_yaramyy") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_do_not_answer_rule") != "hidden"
            || questsBeforeWarnings != bridge.SelectRuntimeState().GetProperty("quests").GetRawText())
        { Fail("Comparing the two warnings erased vocabulary, disclosed the final rule, or became a mandatory quest gate."); return; }

        await InteractAt(player, ray, Interaction("internal-register-to-rinat"));
        await Frames(5);
        var dialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (dialogue is null || !dialogue.IsOpen)
        {
            Fail("Physical Rinat target did not open the authored dialogue UI.");
            return;
        }
        var pressureBeforeQuestion = bridge.SelectRuntimeState().GetProperty("pressure").GetDouble();
        if (RinatAlerted(bridge.SelectRuntimeState()))
        { Fail("Rinat reacted to an internal category before the player showed it."); return; }
        if (!await ChooseVisibleDialogue(bridge, "choice-rinat-accuse-coverup")) return;
        if (RinatAlerted(bridge.SelectRuntimeState())
            || bridge.SelectRuntimeState().GetProperty("pressure").GetDouble() != pressureBeforeQuestion)
        { Fail("An unsupported accusation silently substituted for presenting the actual register category."); return; }
        if (!await ChooseVisibleDialogue(bridge, "choice-rinat-return-to-wording")
            || !await ChooseVisibleDialogue(bridge, "choice-rinat-present-category")
            || !await ChooseVisibleDialogue(bridge, "choice-rinat-leave")) return;
        if (dialogue.IsOpen || !RinatAlerted(bridge.SelectRuntimeState()))
        {
            Fail("Rinat dialogue did not close cleanly or commit the alerted state.");
            return;
        }
        var pressureAfterQuestion = bridge.SelectRuntimeState().GetProperty("pressure").GetDouble();
        if (pressureAfterQuestion != pressureBeforeQuestion + 2
            || await bridge.ChooseDialogueAsync(ChapterPrefix + "dialogue/rinat_internal_register", "dangerous-category", "present-category")
            || bridge.SelectRuntimeState().GetProperty("pressure").GetDouble() != pressureAfterQuestion)
        { Fail("Presenting the register did not apply pressure exactly once."); return; }

        await InteractAt(player, ray, Interaction("internal-register-to-saved-message"));
        await Frames(6);
        AssertDocument(SavedMessage);
        CloseDocument();
        await Frames(3);

        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_message_read") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("saved-message-to-boundary-source")))
        { Fail("Reading Marat's message supplied Alsu's response or opened the next source."); return; }
        await Act1SourceExcerptProof.RecordMessageVoiceAsync(this, bridge);
        await InteractAt(player, ray, Interaction("house-to-route"));
        AssertState(main, bridge, "village_day", "evidence-saved-message", "res://scenes/zones/style_benchmark_day_street.tscn");
        await InteractAt(player, ray, Interaction("talk-alsu"));
        if (!await ChooseVisibleDialogue(bridge, "choice-alsu-show-message")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "hidden")
        { Fail("Showing the message silently chose its interpretation."); return; }
        if (!await ChooseVisibleDialogue(bridge, "choice-alsu-quote-heard-voice")
            || !await ChooseVisibleDialogue(bridge, "choice-alsu-check-original")) return;
        if (player.ModalOpen || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_was_afraid_before_death") != "confirmed")
        { Fail("Alsu's reply did not preserve the selected source or release the return to the archive."); return; }
        // This physical target uses the existing presentation repeater after
        // the register visit, preserving the later message scene.
        await InteractAt(player, ray, Interaction("official-to-internal-register"));
        AssertState(main, bridge, "house_old_pc", "evidence-saved-message", "res://scenes/zones/style_benchmark_house_pc.tscn");

        await InteractAt(player, ray, Interaction("saved-message-to-boundary-source"));
        await Frames(6);
        AssertDocument(BoundarySource);
        CloseDocument();
        await Frames(3);

        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { SavedMessage, BoundarySource }))
        { Fail("The voice comparison was rejected."); return; }
        var voiceConclusion = bridge.SelectRuntimeState();
        if (KnowledgeStatus(voiceConclusion, "clue_folklore_as_survival_rule") != "hypothesis"
            || KnowledgeStatus(voiceConclusion, "clue_voice_answer_is_dangerous_hint") != "hypothesis"
            || VocabularyStatus(voiceConclusion, "tt_tavysh") != "confirmed"
            || VocabularyStatus(voiceConclusion, "tt_javap") != "confirmed")
        {
            Fail("The voice comparison did not record both hypotheses and the two confirmed words.");
            return;
        }
        var rereadPair = new[] { "urman.oldpc:document/rec_marat_case_register_conflict", BoundarySource };
        if (KnowledgeStatus(voiceConclusion, "clue_internal_wording_reread") != "hidden"
            || voiceConclusion.GetProperty("quests").GetProperty(ChapterPrefix + "quest/quest_language_reread").GetProperty("status").GetString() == "completed"
            || await bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), rereadPair))
        { Fail("Translating two words completed the reread before returning to the source."); return; }
        if (bridge.IsOldPcDocumentAccessible(EdgeSketch) || await bridge.OpenDocumentAsync(EdgeSketch))
        { Fail("The direct sketch reader skipped applying the translated words to the record."); return; }

        // The word-specific reply was previously authored but inaccessible:
        // the first question consumed the only physical Rinat target.
        await InteractAt(player, ray, Interaction("internal-register-to-rinat"));
        if (!await ChooseVisibleDialogue(bridge, "choice-rinat-javap")) return;
        ((DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui"))
            .GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_rinat_word_reaction") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_do_not_answer_rule") != "hidden"
            || bridge.SelectRuntimeState().GetProperty("pressure").GetDouble() != pressureAfterQuestion
            || bridge.IsInteractionAvailable(Interaction("internal-register-to-rinat"))
            || bridge.ActiveSceneId != ChapterPrefix + "scene/evidence-tatarwiki-boundary")
        { Fail("Rinat's late word reaction was absent, repeated pressure, replayed the plot, or disclosed the final rule."); return; }

        // Understood vocabulary alone does not expose the sketch. Check the
        // actual archive as well as the document API before the two source
        // returns; an early row must not bypass their ordinary access gates.
        await InteractAt(player, ray, Interaction("oldpc-power"));
        await Frames(4);
        var earlySketchRow = Enumerable.Range(0, archiveRows.ItemCount)
            .FirstOrDefault(index => archiveRows.GetItemMetadata(index).AsString() == EdgeSketch, -1);
        if (earlySketchRow >= 0)
        {
            archiveRows.EmitSignal(ItemList.SignalName.ItemSelected, earlySketchRow);
            await Frames(6);
        }
        var earlySketchDisplayed = archive.ActiveDocumentId == EdgeSketch;
        archive.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        if (earlySketchDisplayed
            || bridge.IsOldPcDocumentAccessible(EdgeSketch) || await bridge.OpenDocumentAsync(EdgeSketch)
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_kara_urman_edge_is_rule_boundary") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_internal_wording_reread") != "hidden"
            || bridge.ActiveObjectives().Any(objective => objective.ObjectiveId == "follow-evidence")
            || bridge.IsInteractionAvailable(Interaction("reread-to-edge-sketch"))
            || await bridge.DispatchInteractionAsync(Interaction("edge-sketch-to-zirat-road"))
            || bridge.ActiveSceneId != ChapterPrefix + "scene/evidence-tatarwiki-boundary")
        { Fail("The archive exposed the sketch before the reread and source-holder returns."); return; }

        await InteractAt(player, ray, Interaction("boundary-source-to-reread"));
        await Frames(6);
        AssertDocument(BoundarySource);
        CloseDocument();
        await Frames(3);

        // The original source remains inspectable; learning vocabulary and
        // reopening the page do not themselves establish its application.
        if (bridge.IsInteractionAvailable(Interaction("reread-to-edge-sketch"))
            || await bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), new[] { SavedMessage, BoundarySource })
            || !bridge.JournalEntries().Single(entry => entry.EntryId == BoundarySource).Body.Contains("Почему рядом?"))
        { Fail("The reread bypassed source selection, lost the full document, or opened the next step without interpretation."); return; }
        foreach (var hypothesis in new[] { "compare-reread-compensation", "compare-reread-author" })
        {
            if (!await bridge.CompareJournalSourcesAsync(Interaction(hypothesis), rereadPair)
                || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_internal_wording_reread") != "hidden"
                || bridge.IsInteractionAvailable(Interaction("reread-to-edge-sketch")))
            { Fail("An unsupported reread interpretation awarded the conclusion or prevented correction."); return; }
        }
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), rereadPair)
            || !await bridge.LoadSlotAsync(RuntimeBridge.CheckpointSlot)
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_internal_wording_reread") != "confirmed"
            || bridge.JournalEntries().Count(entry => entry.EntryId == ChapterPrefix + "knowledge/clue_internal_wording_reread") != 1
            || bridge.IsInteractionAvailable(Interaction("reread-to-edge-sketch"))
            || await bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), rereadPair))
        { Fail("The grounded reread was not checkpointed exactly once, or it skipped the two source returns."); return; }
        await Frames(4);

        if (!await Act1SourceReturnsProof.CompleteAsync(this, bridge,
                interactPhysically: async action => { await InteractAt(player, ray, Interaction(action)); return true; })) return;
        await InteractAt(player, ray, Interaction("reread-to-edge-sketch"));
        await Frames(6);
        AssertDocument(EdgeSketch);
        CloseDocument();
        await Frames(3);

        // The map first opens a discussion of what the road can establish.
        // Its destination objective begins only after the actual conversation.
        var objectivesAfterSketch = bridge.ActiveObjectives();
        GD.Print("act1-corridor: active objectives -> " + string.Join(", ",
            objectivesAfterSketch.Select(entry => $"{entry.QuestId.Split('/')[^1]}/{entry.ObjectiveId}")));
        if (!objectivesAfterSketch.Any(entry => entry.QuestId == "urman.chapter1:quest/quest_kara_urman_cliffhanger"
                && entry.ObjectiveId == "discuss-route-intent")
            || objectivesAfterSketch.Any(entry => entry.ObjectiveId is "find-contradiction" or "apply-words" or "follow-evidence")
            || bridge.IsInteractionAvailable(Interaction("edge-sketch-to-zirat-road")))
        {
            Fail("The sketch did not expose its route discussion, retained an earlier cycle or skipped straight to the road.");
            return;
        }

        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-purpose-landmarks"), new[] { SavedMessage, EdgeSketch }))
        { Fail("The actual message and map did not support a bounded purpose for the route."); return; }
        await InteractAt(player, ray, Interaction("house-to-route"));
        await InteractAt(player, ray, Interaction("route-to-mosque"));
        if (!await ChooseVisibleDialogue(bridge, "choice-timur-register")
            || !await ChooseVisibleDialogue(bridge, "choice-timur-route-check")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_discussed") != "hidden")
        { Fail("Timur's question silently chose why Aidar intends to inspect the road."); return; }
        if (!await ChooseVisibleDialogue(bridge, "choice-timur-name-landmarks")) return;
        ((DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui"))
            ._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(4);
        if (player.ModalOpen || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_discussed") != "confirmed"
            || !bridge.ActiveObjectives().Any(entry => entry.QuestId == ChapterPrefix + "quest/quest_kara_urman_cliffhanger"
                && entry.ObjectiveId == "follow-evidence")
            || bridge.ActiveObjectives().Any(entry => entry.ObjectiveId is "find-contradiction" or "apply-words" or "discuss-route-intent"))
        { Fail("The explicit route discussion did not release the player with the current destination objective."); return; }

        await InteractAt(player, ray, Interaction("edge-sketch-to-zirat-road"));
        await Frames(4);
        AssertState(main, bridge, "zirat_road", "zirat-road", "res://scenes/zones/chapter1_zirat_road.tscn");
        AssertRouteFacing(main, 0f, "zirat-entry-to-forest");

        if (!await Act1RinatRoadsideProof.ObserveAsync(this, bridge)) return;
        await InteractAt(player, ray, Interaction("zirat-roadside-clue"));
        await Act1RouteLandmarksProof.ObserveAsync(this, bridge);
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" }))
        { Fail("The route comparison was rejected."); return; }
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_last_route_near_zirat") != "confirmed")
        {
            Fail("Corridor zirat roadside interaction did not confirm Marat's last-route clue.");
            return;
        }

        await InteractAt(player, ray, Interaction("zirat-road-to-forest"));
        await Frames(8);
        AssertState(main, bridge, "kara_urman_night", "forest-approach", "res://scenes/zones/style_benchmark_kara_urman_night.tscn");
        await InteractAt(player, ray, Interaction("forest-approach-to-forest"));
        await Frames(8);
        AssertState(main, bridge, "kara_urman_night", "forest", "res://scenes/zones/style_benchmark_kara_urman_night.tscn");
        AssertRouteFacing(main, 0f, "forest-entry-to-cliffhanger");
        for (var attempt = 0; attempt < 600
                && BeatStatus(bridge.SelectRuntimeState(), "cliffhanger-hard-cut") != "completed";
                attempt++)
        {
            if (!Act1RinatRoadsideProof.LookAtIntervention(this)) return;
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        }
        var state = bridge.SelectRuntimeState();
        if (BeatStatus(state, "cliffhanger-hard-cut") != "completed"
            || state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/clue_do_not_answer_rule").GetProperty("status").GetString() != "confirmed")
        {
            Fail("First-person corridor reached Kara-Urman without committing the Act 1 cliffhanger.");
            return;
        }

        var finalQuests = bridge.SelectRuntimeState().GetProperty("quests");
        GD.Print("act1-corridor: final quest states -> " + string.Join(", ", finalQuests.EnumerateObject()
            .Select(quest => $"{quest.Name.Split('/')[^1]}={quest.Value.GetProperty("status").GetString()}")));
        // The act must end with a clean quest log: all three investigation
        // quests completed, so the finale does not leave an unfinished goal in
        // the journal. Observed before it was asserted.
        var unfinished = finalQuests.EnumerateObject()
            .Where(quest => quest.Value.GetProperty("status").GetString() != "completed")
            .Select(quest => $"{quest.Name.Split('/')[^1]}={quest.Value.GetProperty("status").GetString()}")
            .ToArray();
        if (unfinished.Length > 0)
        {
            Fail("Act 1 ends with unfinished quests in the journal: " + string.Join(", ", unfinished));
            return;
        }
        GD.Print("act1-first-person-corridor: arrival -> old PC -> FAP document -> Rinat dialogue -> evidence documents -> zirat -> Kara-Urman cliffhanger");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task InteractAt(
        FirstPersonController player,
        RayCast3D ray,
        string interactionId)
    {
        var expected = FindInteraction(interactionId, GetTree().Root);
        if (expected is null)
        {
            Fail($"First-person corridor could not locate named InteractionTarget {interactionId} in the connected world.");
            return;
        }

        var targetPosition = expected.GlobalPosition;
        var frame = InteriorFrame(expected);
        var first = ApproachPosition(player, expected, frame);
        var standings = new List<Vector3> { first };
        // Inside the rebuilt, rotated rooms the side facing the player's last
        // spot can be behind furniture. Try the room's other sides, on floor
        // that fits the standing capsule; the real ray must still hit the target.
        if (frame != Basis.Identity)
            foreach (var reach in new[] { InteractionStandOff, 1.1f })
            foreach (var axis in new[] { Vector3.Back, Vector3.Forward, Vector3.Right, Vector3.Left,
                         (Vector3.Back + Vector3.Right).Normalized(), (Vector3.Back + Vector3.Left).Normalized(),
                         (Vector3.Forward + Vector3.Right).Normalized(), (Vector3.Forward + Vector3.Left).Normalized() })
            {
                var side = frame * axis;
                var candidate = new Vector3(targetPosition.X + side.X * reach, first.Y,
                    targetPosition.Z + side.Z * reach);
                if (candidate.DistanceTo(first) > .05f && player.CanStandAt(candidate)) standings.Add(candidate);
            }
        var tried = new List<string>();
        // Stand on the actual floor under each point: raised rooms (the mosque
        // hall) are not at the height the player arrived from.
        Vector3 OnFloor(Vector3 point)
        {
            using var down = PhysicsRayQueryParameters3D.Create(new(point.X, targetPosition.Y + 1f, point.Z),
                new(point.X, targetPosition.Y - 3f, point.Z), ~4u, new global::Godot.Collections.Array<Rid> { player.GetRid() });
            var floor = player.GetWorld3D().DirectSpaceState.IntersectRay(down);
            return floor.Count > 0 && floor["normal"].AsVector3().Y > .7f
                ? floor["position"].AsVector3() + Vector3.Up * .05f : point;
        }
        foreach (var standing in standings)
        {
            var playerPosition = OnFloor(standing);
            var cameraPosition = playerPosition + Vector3.Up * 1.7f;
            var delta = targetPosition - cameraPosition;
            var horizontal = new Vector2(delta.X, delta.Z).Length();
            var pitch = Mathf.RadToDeg(Mathf.Atan2(delta.Y, horizontal));
            var yaw = Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z));
            player.ApplyPortableTransform(new PlayerTransform(
                new(playerPosition.X, playerPosition.Y, playerPosition.Z),
                new(pitch, yaw, 0)));
            await PhysicsFrames(3);
            ray.ForceRaycastUpdate();
            // The player's own choice, which lets a live action win over a
            // repeat-only target stacked in front of it.
            if (player.PeekRayTarget() is { } reached && reached.InteractionId == interactionId) break;
            tried.Add($"{playerPosition}(from {standing})->{(ray.GetCollider() as Node)?.Name.ToString() ?? "none"}"
                + (ray.GetCollider() is InteractionTarget hitTarget
                    ? $"[{hitTarget.InteractionId} available={hitTarget.IsAvailable()} repeatOnly={hitTarget.IsRepeatOnly} live={hitTarget.IsLiveAvailable} layer={hitTarget.CollisionLayer} routing={hitTarget.GetMeta("interactionRouting", "")}]"
                    : ""));
        }
        if (player.PeekRayTarget() is not InteractionTarget target
            || target.InteractionId != interactionId || !target.IsAvailable())
        {
            var actual = ray.GetCollider() switch { InteractionTarget hit => hit.InteractionId, Node other => "blocker " + other.GetPath(), _ => "<none>" };
            var expectedInfo = $"target-node={expected.GlobalPosition} layer={expected.CollisionLayer} available={expected.IsAvailable()}";
            Fail($"First-person corridor ray expected {interactionId}, got {actual} at connected-world target {targetPosition}; player={player.GlobalPosition} rayEnabled={ray.Enabled} {expectedInfo}; tried=[{string.Join(" | ", tried)}].");
            return;
        }

        await PressInteract();
        await Frames(6);
    }

    private void AssertDocument(string documentId)
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        if (ui is null || !ui.IsOpen || ui.OpenDocumentId != documentId)
        {
            Fail($"Physical evidence interaction did not open {documentId}.");
        }
    }

    private void CloseDocument()
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        ui?.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
    }

    private async Task SaveOpenDocumentToJournal(string documentId)
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (ui is null || bridge is null || !ui.IsOpen || ui.OpenDocumentId != documentId)
        {
            Fail($"Cannot save physical document {documentId}: DocumentUi is not open on the expected record.");
            return;
        }

        ui.GetNode<Button>("Screen/Document/Layout/Footer/Save").EmitSignal(Button.SignalName.Pressed);
        await Frames(8);
        if (!bridge.JournalEntries().Any(entry => entry.EntryId == documentId))
        {
            Fail($"Physical DocumentUi save did not add {documentId} to the shared journal.");
        }
    }

    private static bool RinatAlerted(System.Text.Json.JsonElement state) =>
        state.GetProperty("npc").TryGetProperty($"{ChapterPrefix}character/rinat", out var rinat)
            && rinat.TryGetProperty("alerted", out var alerted) && alerted.GetBoolean();

    private async Task<bool> ChooseVisibleDialogue(RuntimeBridge bridge, string textId)
    {
        var ui = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        var choice = ui?.IsOpen == true
            ? ui.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren().OfType<Button>()
                .FirstOrDefault(button => button.Text == bridge.ResolveText(ChapterPrefix + "text/" + textId))
            : null;
        if (choice is null)
        { Fail("The physical conversation did not expose " + textId); return false; }
        choice.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        return true;
    }

    private static string KnowledgeStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/{localId}").GetProperty("status").GetString()!;

    private static string VocabularyStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("vocabulary").GetProperty($"{ChapterPrefix}vocabulary/{localId}").GetProperty("status").GetString()!;

    private static bool NpcState(System.Text.Json.JsonElement state, string localId, string stateKey) =>
        state.GetProperty("npc").GetProperty($"{ChapterPrefix}character/{localId}").GetProperty(stateKey).GetBoolean();

    // The house and FAP interiors sit inside their rotated village buildings,
    // so "facing into the room" is the building's yaw plus the authored local
    // yaw. Only the connected world knows that mapping.
    private void AssertInteriorFacing(Main main, RuntimeBridge bridge, string transition)
    {
        if (main.ConnectedWorld is not { } world
            || !world.TryGetWorldSpawn(bridge.CurrentZoneId, bridge.CurrentSpawnPointId, out var spawn))
        {
            Fail($"Act 1 route transition {transition} has no world spawn for {bridge.CurrentZoneId}@{bridge.CurrentSpawnPointId}.");
            return;
        }
        AssertRouteFacing(main, spawn.YawDegrees, transition);
    }

    // A beat enters the state only when something sets it; absent means not yet.
    private static string BeatStatus(System.Text.Json.JsonElement state, string localId) =>
        state.TryGetProperty("beats", out var beats) && beats.TryGetProperty($"{ChapterPrefix}beat/{localId}", out var beat)
            ? beat.GetString() ?? string.Empty : string.Empty;

    private void AssertRouteFacing(Main main, float expectedYaw, string transition)
    {
        var difference = Mathf.Abs(Mathf.PosMod(main.LastSpawnYawDegrees - expectedYaw + 180f, 360f) - 180f);
        if (difference > 2f)
        {
            Fail($"Act 1 route transition {transition} declared spawn yaw {main.LastSpawnYawDegrees:0.0}°, expected {expectedYaw:0.0}° toward the next landmark.");
        }
    }

    private void AssertState(Main main, RuntimeBridge bridge, string zone, string sceneLocalId, string scenePath)
    {
        if (main.ActiveZoneScenePath != scenePath
            || bridge.CurrentZoneId != zone
            || bridge.ActiveSceneId != $"{ChapterPrefix}scene/{sceneLocalId}")
        {
            var pause = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
            var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
            Fail($"First-person corridor state mismatch: zone={bridge.CurrentZoneId}, scene={bridge.ActiveSceneId}, path={main.ActiveZoneScenePath}; expected {zone}/{sceneLocalId}/{scenePath}; "
                + $"windowFocused={DisplayServer.WindowIsFocused()} pauseOpen={pause?.IsOpen} modal={player?.ModalOpen}.");
        }
    }

    private async Task PressInteract()
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        await PhysicsFrames(1);
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = false,
            Echo = false
        });
        await PhysicsFrames(2);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true })
                Fail("The physical corridor was interrupted by an actual pause overlay.");
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    // The house and FAP interiors are rotated with their village buildings,
    // so "stand in front of it" snaps to the room's axes, not the world's.
    private Basis InteriorFrame(InteractionTarget target)
    {
        var world = GetTree().Root.FindChild("Act1ConnectedWorld", true, false) as Act1ConnectedWorld;
        foreach (var zoneId in new[] { "house_old_pc", "fap_clinic" })
        {
            if (world?.GetZoneInstance(zoneId) is { } room && room.IsAncestorOf(target))
                return Basis.FromEuler(new(0f, room.GlobalRotation.Y, 0f));
        }
        return Basis.Identity;
    }

    private static Vector3 ApproachPosition(FirstPersonController player, InteractionTarget target, Basis frame)
    {
        if (target.InteractionId == Interaction("route-to-fap"))
            return new Vector3(target.GlobalPosition.X, player.GlobalPosition.Y, target.GlobalPosition.Z + 1.50f);
        var delta = frame.Inverse() * (player.GlobalPosition - target.GlobalPosition);
        var direction = Mathf.Abs(delta.Z) >= Mathf.Abs(delta.X)
            ? new Vector3(0f, 0f, Mathf.Sign(delta.Z))
            : new Vector3(Mathf.Sign(delta.X), 0f, 0f);
        if (direction == Vector3.Zero)
        {
            direction = new Vector3(0f, 0f, 1f);
        }
        direction = frame * direction;

        return new Vector3(
            target.GlobalPosition.X + direction.X * InteractionStandOff,
            player.GlobalPosition.Y,
            target.GlobalPosition.Z + direction.Z * InteractionStandOff);
    }

    private static InteractionTarget? FindInteraction(string interactionId, Node node)
    {
        if (node is InteractionTarget target && target.InteractionId == interactionId)
        {
            return target;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindInteraction(interactionId, child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private void Fail(string message)
    {
        // Abort the current route: continuing after a failed void assertion
        // could otherwise overwrite Quit(1) with a later success receipt.
        throw new InvalidOperationException(message);
    }
}
