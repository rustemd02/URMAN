using System.Text.Json;
using Godot;
using Urman.Godot;

namespace Urman.Godot.Tests;

public partial class FullGameFlowSmokeTest : Node
{
    private const string Prefix = "urman.fullgame:";
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";

    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/full_game.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Full-game scene could not be instantiated.");
            return;
        }

        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null || bridge.ActiveSceneId != ChapterScene("arrival_vehicle_dusk"))
        {
            Fail("Full-game campaign did not start at the authored Chapter 1 arrival entrypoint.");
            return;
        }

        if (main.ActiveZoneScenePath != "res://scenes/zones/style_benchmark_day_street.tscn")
        {
            Fail($"Full-game campaign did not load the Chapter 1 arrival zone: {main.ActiveZoneScenePath}.");
            return;
        }

        if (!await AdvanceChapterOne(bridge, main, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = OfficialNotice
        }));
        if (ChapterKnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_official_death_version") != "confirmed"
            || bridge.IsInteractionAvailable(ChapterInteraction("house-to-route")))
        {
            Fail("Full-game Chapter 1 old-PC clue unlocked the house exit before Gulsina's warning dialogue.");
            return;
        }
        if (bridge.IsInteractionAvailable(ChapterInteraction("house-to-route"))
            || !bridge.IsInteractionAvailable(ChapterInteraction("talk-gulsina"))
            || !await bridge.DispatchInteractionAsync(ChapterInteraction("talk-gulsina"))
            || !await bridge.EnterDialogueNodeAsync(ChapterDialogue("gulsina_yaramyy"), "home-warning")
            || !bridge.IsInteractionAvailable(ChapterInteraction("house-to-route")))
        {
            Fail("Full-game Chapter 1 Gulsina warning dialogue did not unlock the house exit through the shared runtime path.");
            return;
        }
        if (!await AdvanceChapterOne(bridge, main, "house-to-route", "crossroad_signs_inspect")) return;
        main.SwitchZone("village_day", "from_house");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (bridge.IsInteractionAvailable(ChapterInteraction("route-to-fap"))
            || !bridge.IsInteractionAvailable(ChapterInteraction("talk-alsu"))
            || !await bridge.DispatchInteractionAsync(ChapterInteraction("talk-alsu"))
            || !await bridge.EnterDialogueNodeAsync(ChapterDialogue("alsu_route_context"), "name-road")
            || !bridge.IsInteractionAvailable(ChapterInteraction("route-to-fap")))
        {
            Fail("Full-game Chapter 1 Alsu route dialogue did not unlock the FAP route through the shared runtime path.");
            return;
        }
        if (!await AdvanceChapterOne(bridge, main, "route-to-fap", "fap_waiting_room_day")) return;
        main.SwitchZone("fap_clinic", "waiting_room");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (bridge.IsInteractionAvailable(ChapterInteraction("fap-to-document-desk"))
            || !bridge.IsInteractionAvailable(ChapterInteraction("talk-naila"))
            || !await bridge.DispatchInteractionAsync(ChapterInteraction("talk-naila"))
            || !await bridge.EnterDialogueNodeAsync(ChapterDialogue("naila_medical_record"), "official-wording")
            || !await bridge.ChooseDialogueAsync(ChapterDialogue("naila_medical_record"), "official-wording", "ask-wording")
            || !bridge.IsInteractionAvailable(ChapterInteraction("fap-to-document-desk")))
        {
            Fail("Full-game Chapter 1 flow could not apply Naila's authored medical-record dialogue gate.");
            return;
        }
        if (!await AdvanceChapterOne(bridge, main, "fap-to-document-desk", "fap_pressure_document_desk")) return;
        if (!await AdvanceChapterOne(bridge, main, "fap-document-desk-to-official-record", "evidence-official-death")) return;
        if (!await AdvanceChapterOne(bridge, main, "official-to-internal-register", "evidence-internal-register")) return;
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await bridge.OpenDocumentAsync("urman.oldpc:document/doc_marat_official_death_notice");
        await bridge.OpenDocumentAsync("urman.oldpc:document/rec_marat_case_register_conflict");
        await bridge.CompareJournalSourcesAsync(ChapterInteraction("compare-records-contradiction"), new[] { "urman.oldpc:document/doc_marat_official_death_notice", "urman.oldpc:document/rec_marat_case_register_conflict" });
        if (!bridge.IsInteractionAvailable(ChapterInteraction("internal-register-to-rinat"))
            || !await bridge.DispatchInteractionAsync(ChapterInteraction("internal-register-to-rinat"))
            || !await bridge.EnterDialogueNodeAsync(ChapterDialogue("rinat_internal_register"), "dangerous-category"))
        {
            Fail("Full-game Chapter 1 flow could not apply Rinat's authored internal-register dialogue.");
            return;
        }

        if (!await AdvanceChapterOne(bridge, main, "internal-register-to-saved-message", "evidence-saved-message")) return;
        if (!await AdvanceChapterOne(bridge, main, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")) return;
        await bridge.OpenDocumentAsync("urman.oldpc:document/msg_marat_saved_last_normal");
        await bridge.OpenDocumentAsync("urman.oldpc:document/tw_shurale_urman_boundary");
        await bridge.CompareJournalSourcesAsync(ChapterInteraction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" });
        if (!await AdvanceChapterOne(bridge, main, "boundary-source-to-reread", "evidence-tatarwiki-reread")) return;
        if (!await AdvanceChapterOne(bridge, main, "reread-to-edge-sketch", "evidence-edge-sketch")) return;
        await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch");
        if (!await AdvanceChapterOne(bridge, main, "edge-sketch-to-zirat-road", "zirat-road")) return;
        main.SwitchZone("zirat_road", "village_side");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var ziratClue = ChapterInteraction("zirat-roadside-clue");
        if (!bridge.IsInteractionAvailable(ziratClue)
            || !await bridge.DispatchInteractionAsync(ziratClue)
            || !await bridge.CompareJournalSourcesAsync(ChapterInteraction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" })
            || ChapterKnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_last_route_near_zirat") != "confirmed")
        {
            Fail("Full-game Chapter 1 zirat roadside interaction did not confirm Marat's last-route clue.");
            return;
        }
        // The authored route stages Kara-Urman in two steps, like the corridor,
        // checkpoint and interruption smokes already follow.
        if (!await AdvanceChapterOne(bridge, main, "zirat-road-to-forest", "forest-approach")) return;
        if (!await AdvanceChapterOne(bridge, main, "forest-approach-to-forest", "forest")) return;
        main.SwitchZone("kara_urman_night", "village_path");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        for (var attempt = 0; attempt < 200 && bridge.IsInteractionAvailable("urman.chapter1:interaction/forest-rinat-intervention"); attempt++)
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        var chapterState = bridge.SelectRuntimeState();
        if (bridge.CurrentZoneId != "kara_urman_night"
            || ChapterKnowledgeStatus(chapterState, "clue_do_not_answer_rule") != "confirmed"
            || ChapterBeatState(chapterState, "cliffhanger-hard-cut") != "completed")
        {
            Fail("Full-game campaign did not preserve the Chapter 1 cliffhanger state before the Acts 2 transition.");
            return;
        }

        const string chapterTransitionId = "urman.fullgame:interaction/chapter1-forest-to-act2-house";
        var chapterTransition = FindZoneInteraction(main, chapterTransitionId);
        if (chapterTransition is null
            || chapterTransition.TargetZoneId != "fullgame_act2_house"
            || !bridge.IsInteractionAvailable(chapterTransitionId)
            || !await bridge.DispatchInteractionAsync(chapterTransitionId)
            || bridge.ActiveSceneId != Scene("act2-house"))
        {
            Fail("Data-driven Chapter 1 -> Act 2 transition was not available after the forest cliffhanger.");
            return;
        }

        main.SwitchZone("fullgame_act2_house", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // Scope note, printed before the check so the log explains it even on failure:
        // this run completes Act I only. Acts II-V are deliberately not implemented
        // (the handover states that compiling both campaigns is not an instruction to
        // build the later acts), so the Act 2 entrypoint legitimately has no dressing,
        // authored layout, generated kit or NPC presentation yet. The check below is
        // intentionally left failing rather than skipped: a skipped whole-game harness
        // would quietly claim a full-game walk it never performed.
        GD.Print("full-game-flow-smoke: Act I route and the data-driven Chapter 1 -> Act 2 transition verified; "
            + "Acts II-V content is out of scope for this run and is not built, so the walk stops here by design");
        if (!HasProductionDressing(main) || !HasAuthoredInteractionLayout(main)
            || !HasGeneratedKit(main, "act2-family-house")
            || !HasNpcPresentation(main, "fullgame_act2_house"))
        {
            Fail("Full-game Act 2 entrypoint has no zone-specific dressing, authored interaction layout, generated house kit, or NPC presentation.");
            return;
        }

        var firstNpc = main.GetNode<Node3D>("ZoneHost")
            .GetChild(0)
            .GetNode<Node3D>("ProductionDressing/NpcPresentation")
            .GetChildren()
            .OfType<Node3D>()
            .FirstOrDefault(node => node.Name.ToString().StartsWith("Npc_", StringComparison.Ordinal));
        if (firstNpc is null
            || !GeneratedCharacterKitDressing.PlayClip(firstNpc, "Tension")
            || firstNpc.GetMeta("animationClip").AsString() != $"{firstNpc.GetMeta("characterPrefix").AsString()}_Tension"
            || !GeneratedCharacterKitDressing.PlayClip(firstNpc, "Idle"))
        {
            Fail("Godot could not switch a project-original NPC between authored Tension and Idle clips.");
            return;
        }

        if (main.ActiveZoneScenePath != "res://scenes/zones/fullgame/act2_house.tscn")
        {
            Fail($"Full-game entrypoint did not load its authored scene wrapper: {main.ActiveZoneScenePath}.");
            return;
        }

        var interactionRay = main.GetNode<FirstPersonController>("Player")
            .GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        if (interactionRay.CollisionMask != 1)
        {
            Fail("Interaction ray must remain on layer 1 so provisional kit colliders cannot steal document targets.");
            return;
        }

        if (!await Advance(bridge, main, "act2-house-to-river", "act2-river", "fullgame_act2_river")) return;
        if (!await Advance(bridge, main, "act2-river-to-mosque", "act2-mosque", "fullgame_act2_mosque")) return;
        if (!await Dialogue(bridge, main, "timur-counsel", "counsel")) return;
        if (!await Advance(bridge, main, "act2-mosque-to-council", "act2-council", "fullgame_act2_council")) return;
        if (!await Dialogue(bridge, main, "alsu-confession", "confession")) return;
        if (!await Dialogue(bridge, main, "council-voice", "council")) return;
        if (!await Advance(bridge, main, "act2-council-to-archive", "act3-archive", "fullgame_act3_archive")) return;
        if (!await Dialogue(bridge, main, "naila-record", "record")) return;
        if (!await Advance(bridge, main, "act3-archive-to-soviet", "act3-soviet", "fullgame_act3_soviet")) return;
        if (!await Document(bridge, main, "act3-soviet-document", "baranov-1967")) return;
        if (!await Advance(bridge, main, "act3-soviet-to-water", "act3-suanasy", "fullgame_act3_water")) return;
        if (!await Dialogue(bridge, main, "suanasy", "water")) return;
        if (!await Advance(bridge, main, "act3-water-to-tukay", "act4-tukay", "fullgame_act4_tukay")) return;
        if (!await Document(bridge, main, "act4-tukay-document", "tukai-1913")) return;
        if (!await Dialogue(bridge, main, "tukay", "notebook")) return;
        if (!await Advance(bridge, main, "act4-tukay-to-1552", "act4-1552", "fullgame_act4_1552")) return;
        if (!await Document(bridge, main, "act4-1552-document", "kazan-1552")) return;
        if (!await Advance(bridge, main, "act4-1552-to-pact", "act4-pact", "fullgame_act4_pact")) return;
        if (!await Document(bridge, main, "act4-pact-document", "pact-ledger")) return;
        if (!await Dialogue(bridge, main, "pact-keeper", "keeper")) return;
        if (!await Advance(bridge, main, "act4-pact-to-boundary", "act5-boundary", "fullgame_act5_boundary")) return;
        if (!await Dialogue(bridge, main, "aidar-final", "decision")) return;
        if (!await Advance(bridge, main, "act5-boundary-to-epilogue", "act5-epilogue", "fullgame_act5_epilogue")) return;

        var state = bridge.SelectRuntimeState();
        if (KnowledgeStatus(state, "act5_pact_unjust") != "confirmed"
            || KnowledgeStatus(state, "act5_protection_lost") != "confirmed"
            || KnowledgeStatus(state, "act5_truth_price") != "confirmed"
            || BeatState(state, "canonical-tragic-ending") != "completed")
        {
            Fail("Full-game flow reached the epilogue without the canonical tragic state.");
            return;
        }

        GD.Print("full-game-flow-smoke: 46 authored narrative entries -> Chapter 1 -> Acts 2-5 walkable zones -> canonical tragic epilogue");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async Task<bool> AdvanceChapterOne(RuntimeBridge bridge, Main main, string interactionLocalId, string targetSceneLocalId)
    {
        var interactionId = ChapterInteraction(interactionLocalId);
        if (!bridge.IsInteractionAvailable(interactionId)
            || !await bridge.DispatchInteractionAsync(interactionId)
            || bridge.ActiveSceneId != ChapterScene(targetSceneLocalId))
        {
            Fail($"Full-game Chapter 1 interaction did not reach {targetSceneLocalId}: {interactionId}.");
            return false;
        }

        return true;
    }

    private async Task<bool> Advance(RuntimeBridge bridge, Main main, string interactionLocalId, string targetSceneLocalId, string targetZone)
    {
        var interactionId = Interaction(interactionLocalId);
        var physicalTarget = FindPhysicalInteraction(main, interactionId);
        if (physicalTarget is null
            || physicalTarget.TargetZoneId != targetZone
            || !bridge.IsInteractionAvailable(interactionId)
            || !await bridge.DispatchInteractionAsync(interactionId)
            || bridge.ActiveSceneId != Scene(targetSceneLocalId))
        {
            Fail($"Full-game interaction did not reach {targetSceneLocalId}: {interactionId}.");
            return false;
        }

        main.SwitchZone(targetZone, "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!HasProductionDressing(main) || !HasAuthoredInteractionLayout(main))
        {
            Fail($"Full-game zone has no zone-specific dressing or authored interaction layout: {targetZone}.");
            return false;
        }

        if (!HasNpcPresentation(main, targetZone))
        {
            Fail($"Full-game zone has no deterministic NPC presentation contract: {targetZone}.");
            return false;
        }

        if (targetZone == "fullgame_act3_soviet" && !HasGeneratedKit(main, "act3-soviet-old-pc"))
        {
            Fail("Full-game Soviet archive zone has no generated old-PC/table kit with LOD ranges.");
            return false;
        }

        if (targetZone == "fullgame_act5_boundary" && !HasGeneratedKit(main, "act5-boundary-forest"))
        {
            Fail("Full-game boundary zone has no generated forest kit with LOD ranges.");
            return false;
        }

        if (targetZone == "fullgame_act5_epilogue" && !HasGeneratedKit(main, "act5-epilogue-house"))
        {
            Fail("Full-game epilogue has no authored HouseA presentation kit with LOD ranges.");
            return false;
        }

        if (!string.Equals(main.ActiveZoneScenePath, ExpectedScenePath(targetZone), StringComparison.Ordinal))
        {
            Fail($"Full-game transition loaded the wrong authored scene wrapper for {targetZone}: {main.ActiveZoneScenePath}.");
            return false;
        }

        return true;
    }

    private static string ExpectedScenePath(string zoneId) => zoneId switch
    {
        "fullgame_act2_house" => "res://scenes/zones/fullgame/act2_house.tscn",
        "fullgame_act2_river" => "res://scenes/zones/fullgame/act2_river.tscn",
        "fullgame_act2_mosque" => "res://scenes/zones/fullgame/act2_mosque.tscn",
        "fullgame_act2_council" => "res://scenes/zones/fullgame/act2_council.tscn",
        "fullgame_act3_archive" => "res://scenes/zones/fullgame/act3_archive.tscn",
        "fullgame_act3_soviet" => "res://scenes/zones/fullgame/act3_soviet.tscn",
        "fullgame_act3_water" => "res://scenes/zones/fullgame/act3_water.tscn",
        "fullgame_act4_tukay" => "res://scenes/zones/fullgame/act4_tukay.tscn",
        "fullgame_act4_1552" => "res://scenes/zones/fullgame/act4_1552.tscn",
        "fullgame_act4_pact" => "res://scenes/zones/fullgame/act4_pact.tscn",
        "fullgame_act5_boundary" => "res://scenes/zones/fullgame/act5_boundary.tscn",
        "fullgame_act5_epilogue" => "res://scenes/zones/fullgame/act5_epilogue.tscn",
        _ => throw new ArgumentOutOfRangeException(nameof(zoneId), zoneId, "Unknown full-game zone.")
    };

    private async Task<bool> Dialogue(RuntimeBridge bridge, Main main, string dialogueLocalId, string nodeId)
    {
        var dialogueId = $"{Prefix}dialogue/{dialogueLocalId}";
        var interactionId = $"{Prefix}interaction/{DialogueInteraction(dialogueLocalId)}";
        if (FindPhysicalInteraction(main, interactionId) is null || !bridge.IsInteractionAvailable(interactionId))
        {
            Fail($"Full-game dialogue interaction is unavailable: {interactionId}.");
            return false;
        }

        if (!await bridge.DispatchInteractionAsync(interactionId)
            || !await bridge.EnterDialogueNodeAsync(dialogueId, nodeId))
        {
            Fail($"Full-game dialogue did not apply: {dialogueId}/{nodeId}.");
            return false;
        }

        return true;
    }

    private async Task<bool> Document(RuntimeBridge bridge, Main main, string documentInteractionLocalId, string documentLocalId)
    {
        var interactionId = Interaction(documentInteractionLocalId);
        var documentId = $"{Prefix}document/{documentLocalId}";
        var physicalTarget = FindPhysicalInteraction(main, interactionId);
        if (physicalTarget is null
            || physicalTarget.DocumentId != documentId
            || !bridge.IsInteractionAvailable(interactionId)
            || !await bridge.DispatchInteractionAsync(interactionId)
            || !await bridge.OpenDocumentAsync(documentId)
            || !bridge.SelectRuntimeState().GetProperty("presentation").GetProperty("openedDocumentIds").EnumerateArray().Any(item => item.GetString() == documentId)
            || !await bridge.RecordJournalEntryAsync(documentId, documentId)
            || !bridge.JournalEntries().Any(entry => entry.EntryId == documentId))
        {
            Fail($"Full-game physical document did not open and record: {documentId}.");
            return false;
        }

        return true;
    }

    private static InteractionTarget? FindPhysicalInteraction(Main main, string interactionId)
    {
        var host = main.GetNode<Node3D>("ZoneHost");
        if (host.GetChildCount() != 1 || host.GetChild(0) is not FullGameZone zone)
        {
            return null;
        }

        return zone.GetChildren()
            .OfType<InteractionTarget>()
            .SingleOrDefault(target => target.InteractionId == interactionId);
    }

    private static InteractionTarget? FindZoneInteraction(Main main, string interactionId)
    {
        var host = main.GetNode<Node3D>("ZoneHost");
        if (host.GetChildCount() != 1 || host.GetChild(0) is not Node3D zone)
        {
            return null;
        }

        return zone.GetChildren()
            .OfType<InteractionTarget>()
            .SingleOrDefault(target => target.InteractionId == interactionId);
    }

    private static bool HasProductionDressing(Main main)
    {
        var host = main.GetNode<Node3D>("ZoneHost");
        return host.GetChildCount() == 1
               && host.GetChild(0) is FullGameZone zone
               && zone.GetNodeOrNull<Node3D>("ProductionDressing") is not null;
    }

    private static bool HasAuthoredInteractionLayout(Main main)
    {
        var host = main.GetNode<Node3D>("ZoneHost");
        return host.GetChildCount() == 1
               && host.GetChild(0) is FullGameZone zone
               && zone.GetChildren()
                   .OfType<InteractionTarget>()
                   .All(target => target.GetMeta("presentationLayout").AsString() == "authored-zone-anchor");
    }

    private static bool HasNpcPresentation(Main main, string zoneId)
    {
        var host = main.GetNode<Node3D>("ZoneHost");
        if (host.GetChildCount() != 1 || host.GetChild(0) is not FullGameZone zone)
        {
            return false;
        }

        var npcHost = zone.GetNodeOrNull<Node3D>("ProductionDressing/NpcPresentation");
        if (npcHost is null
            || npcHost.GetMeta("zoneId").AsString() != zoneId
            || npcHost.GetMeta("status").AsString() != "generated-character-kit-v1"
            || npcHost.GetMeta("assetSource").AsString() != GeneratedCharacterKitDressing.ScenePath
            || npcHost.GetMeta("ownership").AsString() != "presentation-only"
            || !npcHost.GetMeta("collisionPolicy").AsString().Contains("no physics body", StringComparison.Ordinal))
        {
            return false;
        }

        var npcs = npcHost.GetChildren()
            .OfType<Node3D>()
            .Where(node => node.Name.ToString().StartsWith("Npc_", StringComparison.Ordinal))
            .ToArray();
        var declaredCount = int.TryParse(npcHost.GetMeta("npcCount").ToString(), out var parsed)
            ? parsed
            : -1;
        var expectedCount = zoneId switch
        {
            "fullgame_act2_house" => 2,
            "fullgame_act2_river" => 1,
            "fullgame_act2_mosque" => 1,
            "fullgame_act2_council" => 2,
            "fullgame_act3_archive" => 1,
            "fullgame_act3_soviet" => 1,
            "fullgame_act4_pact" => 1,
            _ => 0
        };

        var expectedClip = zoneId is "fullgame_act4_pact" or "fullgame_act5_boundary"
            ? "Tension"
            : "Idle";
        var contract = npcHost.GetMeta("defaultAnimationClip").AsString() == expectedClip
                       && declaredCount == expectedCount
                       && npcs.Length == expectedCount
                       && npcs.All(npc => npc.GetMeta("presentationStatus").AsString() == "generated-character-kit-v1"
                                         && npc.GetMeta("interactionOwnership").AsString() == "none"
                                         && npc.GetMeta("collisionLayer").AsInt32() == 0
                                         && npc.GetMeta("generatedCharacterStatus").AsString() == "godot-visibility-ranges-integrated"
                                         && npc.GetMeta("animationStatus").AsString() == $"godot-animationplayer-{expectedClip.ToLowerInvariant()}-playing"
                                         && npc.GetMeta("animationClip").AsString() == $"{npc.GetMeta("characterPrefix").AsString()}_{expectedClip}"
                                         && npc.GetMeta("animationClips").AsString().Contains($"{npc.GetMeta("characterPrefix").AsString()}_Tension", StringComparison.Ordinal)
                                         && npc.GetMeta("anchorReference").AsString().EndsWith("_Anchor", StringComparison.Ordinal)
                                         && npc.FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false).Count == 0
                                         && HasCharacterLodContract(npc, expectedClip));
        if (!contract)
        {
            GD.Print($"full-game-npc-debug zone={zoneId} hostChildren={host.GetChildCount()} npcHost={npcHost.Name} npcHostChildren={npcHost.GetChildCount()} declared={declaredCount} expected={expectedCount} actual={npcs.Length}");
            for (var childIndex = 0; childIndex < npcHost.GetChildCount(); childIndex++)
            {
                var child = npcHost.GetChild(childIndex);
                GD.Print($"full-game-npc-debug child={childIndex} name={child.Name} type={child.GetType().Name} class={child.GetClass()}");
            }
            foreach (var npc in npcs)
            {
                var meshes = npc.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
                    .OfType<MeshInstance3D>()
                    .ToArray();
                GD.Print($"full-game-npc-debug npc={npc.Name} status={npc.GetMeta("presentationStatus").AsString()} generated={npc.GetMeta("generatedCharacterStatus").AsString()} anchor={npc.GetMeta("anchorReference").AsString()} collisionObjects={npc.FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false).Count} meshes={meshes.Length} visible={meshes.Count(mesh => mesh.Visible)} shader={meshes.Count(mesh => mesh.Visible && mesh.MaterialOverride is ShaderMaterial)}");
                foreach (var mesh in meshes.Where(mesh => mesh.Visible))
                {
                    GD.Print($"full-game-npc-debug mesh={mesh.Name} lodBegin={mesh.VisibilityRangeBegin} lodEnd={mesh.VisibilityRangeEnd} fade={mesh.VisibilityRangeFadeMode} material={mesh.MaterialOverride?.GetType().Name ?? "null"}");
                }
            }
        }

        return contract;
    }

    private static bool HasCharacterLodContract(Node3D npc, string expectedClip)
    {
        var visibleMeshes = npc
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Visible)
            .ToArray();
        var lod0 = visibleMeshes.Where(mesh => mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal)).ToArray();
        var lod1 = visibleMeshes.Where(mesh => mesh.Name.ToString().Contains("_LOD1", StringComparison.Ordinal)).ToArray();
        var hasFaceLandmarks = visibleMeshes.Any(mesh => mesh.Name.ToString().Contains("FaceEye", StringComparison.Ordinal))
                               && visibleMeshes.Any(mesh => mesh.Name.ToString().Contains("FaceNose", StringComparison.Ordinal));
        var hasLayeredClothing = visibleMeshes.Any(mesh => mesh.Name.ToString().Contains("Sleeve", StringComparison.Ordinal))
                                 && visibleMeshes.Any(mesh => mesh.Name.ToString().Contains("Trouser", StringComparison.Ordinal))
                                 && visibleMeshes.Any(mesh => mesh.Name.ToString().Contains("Boot", StringComparison.Ordinal));
        var clothingMeshes = visibleMeshes
            .Where(mesh => mesh.GetMeta("painterlyMaterial").AsString() == "cloth")
            .ToArray();
        var hasTexturedClothing = clothingMeshes.Length > 0
                                  && clothingMeshes.All(mesh => mesh.MaterialOverride is ShaderMaterial shader
                                                                 && shader.GetShaderParameter("has_albedo_texture").AsBool());
        return lod0.Length > 0
               && lod1.Length > 0
               && hasFaceLandmarks
               && hasLayeredClothing
               && hasTexturedClothing
               && HasAnimationContract(npc, expectedClip)
               && lod0.All(mesh => Mathf.IsEqualApprox(mesh.VisibilityRangeEnd, 18f)
                                   && Mathf.IsEqualApprox(mesh.VisibilityRangeEndMargin, 2f)
                                   && mesh.VisibilityRangeFadeMode == GeometryInstance3D.VisibilityRangeFadeModeEnum.Self)
               && lod1.All(mesh => Mathf.IsEqualApprox(mesh.VisibilityRangeBegin, 14f)
                                   && Mathf.IsEqualApprox(mesh.VisibilityRangeBeginMargin, 2f)
                                   && Mathf.IsEqualApprox(mesh.VisibilityRangeEnd, 48f)
                                   && Mathf.IsEqualApprox(mesh.VisibilityRangeEndMargin, 4f)
                                   && mesh.VisibilityRangeFadeMode == GeometryInstance3D.VisibilityRangeFadeModeEnum.Self)
               && visibleMeshes.All(mesh => mesh.MaterialOverride is ShaderMaterial);
    }

    private static bool HasAnimationContract(Node3D npc, string expectedClip)
    {
        var prefix = npc.GetMeta("characterPrefix").AsString();
        var idle = $"{prefix}_Idle";
        var tension = $"{prefix}_Tension";
        var player = npc
            .FindChildren("*", nameof(AnimationPlayer), recursive: true, owned: false)
            .OfType<AnimationPlayer>()
            .FirstOrDefault(candidate => candidate.HasAnimation(idle) && candidate.HasAnimation(tension));
        var expectedAnimation = expectedClip == "Tension" ? tension : idle;
        return player is not null
               && player.IsPlaying()
               && player.CurrentAnimation == expectedAnimation;
    }

    private static bool HasGeneratedKit(Main main, string variant)
    {
        var host = main.GetNode<Node3D>("ZoneHost");
        if (host.GetChildCount() != 1 || host.GetChild(0) is not FullGameZone zone)
        {
            return false;
        }

        var kit = zone.GetNodeOrNull<Node3D>("ProductionDressing/GeneratedModularKit");
        if (kit is null
            || kit.GetMeta("variant").AsString() != variant
            || kit.GetMeta("generatedKitStatus").AsString() != "godot-visibility-ranges-integrated")
        {
            return false;
        }

        var expectedAnchor = variant switch
        {
            "act2-family-house" => "HouseA_Walls_LOD0",
            "act3-soviet-old-pc" => "TableA_Top_LOD0",
            "act5-boundary-forest" => "PineA_Trunk_LOD0",
            "act5-epilogue-house" => "HouseA_Walls_LOD0",
            _ => string.Empty
        };
        if (kit.GetMeta("anchorReference").AsString() != expectedAnchor)
        {
            return false;
        }

        var collisionProxy = kit.GetNodeOrNull<StaticBody3D>("KitCollisionProxy");
        if (collisionProxy is null
            || collisionProxy.CollisionLayer != 2
            || collisionProxy.GetChildren().OfType<CollisionShape3D>().Count() == 0
            || kit.GetMeta("collisionPolicy").AsString().Contains("provisional layer-2 kit colliders", StringComparison.Ordinal) == false)
        {
            return false;
        }

        var visibleMeshes = kit
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Visible)
            .ToArray();
        var requiredSurfaceOwners = variant switch
        {
            "act2-family-house" => new[] { "wood_facade", "wood_fence", "stone", "wood_prop", "wood_bark" },
            "act3-soviet-old-pc" => new[] { "wood_furniture" },
            "act5-boundary-forest" => new[] { "bark_pine", "foliage", "wood_fence", "cloth" },
            "act5-epilogue-house" => new[] { "wood_facade", "wood_bark" },
            _ => Array.Empty<string>()
        };
        var materialOwners = visibleMeshes
            .Select(mesh => mesh.GetMeta("painterlyMaterial").AsString())
            .ToHashSet(StringComparer.Ordinal);
        if (requiredSurfaceOwners.Any(owner => !materialOwners.Contains(owner)))
        {
            return false;
        }

        var requiredModuleFamilies = variant switch
        {
            "act2-family-house" => new[] { (Prefix: "WellA_", Lod0: 7), (Prefix: "WoodpileA_", Lod0: 4) },
            "act5-boundary-forest" => new[] { (Prefix: "GateA_", Lod0: 4) },
            "act5-epilogue-house" => new[] { (Prefix: "WoodpileA_", Lod0: 4) },
            _ => Array.Empty<(string Prefix, int Lod0)>()
        };
        if (requiredModuleFamilies.Any(family => !HasModuleFamily(kit, family.Prefix, family.Lod0)))
        {
            return false;
        }
        if (variant == "act5-boundary-forest" && !HasGateMarkerClearance(zone, kit))
        {
            return false;
        }

        var lod0 = visibleMeshes.Where(mesh => mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal)).ToArray();
        var lod1 = visibleMeshes.Where(mesh => mesh.Name.ToString().Contains("_LOD1", StringComparison.Ordinal)).ToArray();
        var hiddenColliders = kit
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().EndsWith("-col", StringComparison.Ordinal))
            .ToArray();

        return lod0.Length > 0
               && lod1.Length > 0
               && lod0.All(mesh => Mathf.IsEqualApprox(mesh.VisibilityRangeEnd, 24f)
                                   && Mathf.IsEqualApprox(mesh.VisibilityRangeEndMargin, 3f)
                                   && mesh.VisibilityRangeFadeMode == GeometryInstance3D.VisibilityRangeFadeModeEnum.Self)
               && lod1.All(mesh => Mathf.IsEqualApprox(mesh.VisibilityRangeBegin, 18f)
                                   && Mathf.IsEqualApprox(mesh.VisibilityRangeBeginMargin, 3f)
                                   && Mathf.IsEqualApprox(mesh.VisibilityRangeEnd, 72f)
                                   && Mathf.IsEqualApprox(mesh.VisibilityRangeEndMargin, 6f)
                                   && mesh.VisibilityRangeFadeMode == GeometryInstance3D.VisibilityRangeFadeModeEnum.Self)
               && visibleMeshes.All(mesh => mesh.MaterialOverride is ShaderMaterial)
               && hiddenColliders.All(mesh => !mesh.Visible)
               && kit.GetMeta("collisionPolicy").AsString().Contains("interaction targets remain layer 1", StringComparison.Ordinal);
    }

    private static bool HasModuleFamily(Node3D kit, string prefix, int expectedLod0Count)
    {
        var meshes = kit
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        var lod0 = meshes.Count(mesh => mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal));
        var lod1 = meshes.Count(mesh => mesh.Name.ToString().Contains("_LOD1", StringComparison.Ordinal));
        return lod0 == expectedLod0Count
               && lod1 == expectedLod0Count
               && meshes.All(mesh => mesh.GetMeta("presentationOwnership").AsString() == "presentation-only"
                                     && mesh.GetMeta("collisionPolicy").AsString().Contains("no physics", StringComparison.Ordinal));
    }

    private static bool HasGateMarkerClearance(FullGameZone zone, Node3D kit)
    {
        var markerIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "urman.fullgame:interaction/act5-aidar-choice",
            "urman.fullgame:interaction/act5-boundary-to-epilogue"
        };
        var markerTargets = zone.GetChildren()
            .OfType<InteractionTarget>()
            .Where(target => markerIds.Contains(target.InteractionId))
            .ToArray();
        var gateMeshes = kit
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Visible && mesh.Name.ToString().StartsWith("GateA_", StringComparison.Ordinal))
            .ToArray();
        if (markerTargets.Length != markerIds.Count || gateMeshes.Length == 0)
        {
            return false;
        }

        var minHorizontalDistance = gateMeshes
            .SelectMany(mesh => markerTargets.Select(target =>
            {
                var delta = mesh.GlobalPosition - target.GlobalPosition;
                return new Vector2(delta.X, delta.Z).Length();
            }))
            .Min();
        var gateBehindMarkers = gateMeshes.All(mesh => markerTargets.All(target =>
            mesh.GlobalPosition.Z < target.GlobalPosition.Z - 1.0f));
        kit.SetMeta("gateMarkerClearanceMeters", minHorizontalDistance);
        kit.SetMeta("gateMarkerOcclusionPolicy", "GateA behind both authored Act 5 interaction markers by >=1m");
        GD.Print($"full-game-module-qa: GateA marker clearance={minHorizontalDistance:0.000}m; behind_markers={gateBehindMarkers}");
        return minHorizontalDistance >= 1.0f && gateBehindMarkers;
    }

    private static string DialogueInteraction(string dialogueLocalId) => dialogueLocalId switch
    {
        "timur-counsel" => "act2-timur",
        "alsu-confession" => "act2-alsu",
        "council-voice" => "act2-council-voice",
        "naila-record" => "act3-naila",
        "suanasy" => "act3-water-voice",
        "tukay" => "act4-tukay-voice",
        "pact-keeper" => "act4-keeper",
        "aidar-final" => "act5-aidar-choice",
        _ => throw new ArgumentOutOfRangeException(nameof(dialogueLocalId), dialogueLocalId, null)
    };

    private static string KnowledgeStatus(JsonElement state, string localId) =>
        state.GetProperty("knowledge").GetProperty($"{Prefix}knowledge/{localId}").GetProperty("status").GetString()!;

    private static string ChapterKnowledgeStatus(JsonElement state, string localId) =>
        state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/{localId}").GetProperty("status").GetString()!;

    private static string BeatState(JsonElement state, string localId) =>
        state.GetProperty("beats").GetProperty($"{Prefix}beat/{localId}").GetString()!;

    private static string ChapterBeatState(JsonElement state, string localId) =>
        state.GetProperty("beats").GetProperty($"{ChapterPrefix}beat/{localId}").GetString()!;

    private static string Interaction(string localId) => $"{Prefix}interaction/{localId}";

    private static string Scene(string localId) => $"{Prefix}scene/{localId}";

    private static string ChapterInteraction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private static string ChapterScene(string localId) => $"{ChapterPrefix}scene/{localId}";

    private static string ChapterDialogue(string localId) => $"{ChapterPrefix}dialogue/{localId}";

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
