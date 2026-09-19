using System.Linq;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Starts a normal demo session and exercises the family investigation through
/// production dialogue, archive and journal owners. Revisit actions are invoked
/// on the physical targets; the corridor/walkthrough smokes own camera and walking
/// coverage. This is a state/interaction regression, never a human-duration test.
/// Run only through eng/run-smoke-guarded.sh after the C# build.
/// </summary>
public partial class Act1Len01FamilyFlowSmokeTest : Node
{
    private const string Prefix = "urman.chapter1:";
    private const string Official = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string Register = "urman.oldpc:document/rec_marat_case_register_conflict";
    private const string Message = "urman.oldpc:document/msg_marat_saved_last_normal";
    private const string BoundaryArticle = "urman.oldpc:document/tw_shurale_urman_boundary";
    private const string EdgeSketch = "urman.oldpc:document/doc_kara_urman_edge_sketch";
    private const string Niva = "urman.oldpc:document/doc_household_misc_niva_receipt";
    private const string Electricity = "urman.oldpc:document/doc_household_electric_receipts";
    private Act1DemoRoot _demo = null!;
    private RuntimeBridge _bridge = null!;
    private DialogueUi _dialogue = null!;
    private OldPcUi _pc = null!;
    private FirstPersonController _player = null!;

    public override async void _Ready()
    {
        try
        {
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")!.Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(8);
            Require(await this.StartThroughMainMenuAsync(_demo), "Normal New Game failed.");
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            _dialogue = (DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui");
            _pc = (OldPcUi)GetTree().GetFirstNodeInGroup("old_pc_ui");
            _player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            await VerifyIntroLayout();
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(4);
            Require(!_demo.IntroVisible && !_player.ModalOpen, "Intro did not release input.");

            foreach (var node in new[] { "restraint", "route-return", "route-follow-up" })
                Require(!await _bridge.ChooseDialogueAsync(Prefix + "dialogue/timur_restraint", node, "ask-shurale-name"),
                    "An unread folklore name allowed Timur's question from " + node);
            Require(Vocabulary("tt_shurale") == "unknown", "A rejected name question taught an unread word.");
            Require(!Known("address-babai") && !Known("address-fap"),
                "New Game supplied addresses before their messages or conversations.");
            await Act1ArrivalFlowProof.CompleteAsync(this, _bridge, verifyReturn: true);
            Require(Known("address-babai") && !Known("address-fap")
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("address-babai")) == 1,
                "Reading the mother's actual message did not retain just the home address.");
            await VerifyEarlyDirections();
            if (OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs())
                .Contains("--urman-smoke-alsu-walk-only", StringComparer.Ordinal))
            {
                GD.Print("act1-alsu-walk-only: normal arrival sources; actual companion walk/refusal/return/load; explicit sourced account/address; no first-human-duration claim.");
                await GodotSmokeCleanup.ReleaseAsync(_demo);
                GetTree().Quit(0);
                return;
            }
            if (OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs())
                .Contains("--urman-smoke-village-life-only", StringComparer.Ordinal))
            {
                await VerifyVillageLife();
                await GodotSmokeCleanup.ReleaseAsync(_demo);
                GetTree().Quit(0);
                return;
            }
            await Move("arrival-enter-house", "house_old_pc", "entry");
            Require(!Known("clue_family_avoids_marat") && Beat("house-warmth-and-pause") != "completed",
                "Entering the house fabricated a family conversation.");
            await Act1FamilyMealProof.VerifyNotebookAsync(this, _bridge, pending: false, captureName: "family_home_before_invitation");
            await Talk("talk-mansur", "mansur_pc_request");
            Require(!Npc("mansur", "pc_access_granted") && !Available("oldpc-power"),
                "Opening a request granted access before Mansur answered.");
            await CloseTalk();
            await RoundTrip("len01-unanswered-request");
            Require(!Npc("mansur", "pc_access_granted") && !Known("clue_mansur_household_request"),
                "An unanswered request turned into permission after load.");

            await Talk("talk-gulsina", "gulsina_yaramyy");
            Require(HasChoice("choice-gulsina-marat") && !Known("clue_marat_official_death_version"),
                "The family question is still hidden until the official document.");
            await CloseTalk();
            Require(await Act1FamilyMealProof.CompleteAsync(this, _bridge, verifyReturn: true),
                "The real home pause did not preserve its source, human response and return.");
            Require(Beat("house-warmth-and-pause") == "completed" && !Known("clue_family_avoids_marat"),
                "Accepting the meal did not play the home beat, or invented a reply about Marat.");
            if (OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs())
                .Contains("--urman-smoke-family-home-only", StringComparer.Ordinal))
            {
                await VerifyAlternativeFamilyStart();
                GD.Print("act1-family-home-only: normal arrival; photo and silent family replies; refusal/interruption/load; actual notebook invitation; early warning without Naila knowledge; preserved-world outside return; one completed meal. Technical coverage only.");
                await GodotSmokeCleanup.ReleaseAsync(_demo);
                GetTree().Quit(0);
                return;
            }

            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-why");
            await CloseTalk();
            Require(Npc("mansur", "pc_access_granted") && Known("clue_mansur_household_request")
                && !Known("clue_marat_official_death_version"), "The domestic request did not grant just the requested access.");

            await OpenPc();
            await Search("нет-такого-слова-len01");
            Require(SelectableResults() == 0 && !Known("clue_household_niva_list"), "An empty search granted its target.");
            await Search("квитанци");
            Require(HasDocument(Niva) && HasDocument(Electricity), "Receipt search lost the two meaningful candidates.");
            await ReadFromPc(Electricity);
            await ClosePc();
            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-check-household-list");
            Require(!HasChoice("choice-mansur-niva-repairs"), "A solution was selectable before finding the Niva list.");
            await Choose("choice-mansur-electric-receipts");
            Require(!Npc("mansur", "household_help_done"), "An unrelated receipt completed the request.");
            await CloseTalk();
            await RoundTrip("len01-wrong-receipt");
            Require(Known("clue_household_electric_receipts") && !Known("clue_household_niva_list")
                && !Npc("mansur", "household_help_done"), "Load lost the wrong-source state or silently completed the task.");

            await OpenPc();
            await Search("Нива");
            await ReadFromPc(Niva);
            Require(!Npc("mansur", "household_help_done"), "Merely opening the list completed the return conversation.");
            await ClosePc();
            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-check-household-list");
            await Choose("choice-mansur-niva-wiring");
            Require(!Npc("mansur", "household_help_done"), "Reading the wiring instruction backwards was accepted.");
            await CloseTalk();

            await OpenPc();
            await Search("Нива");
            await ReadFromPc(Niva);
            await ClosePc();
            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-check-household-list");
            await Choose("choice-mansur-niva-repairs");
            await CloseTalk();
            Require(Npc("mansur", "household_help_done") && Known("clue_household_request_checked")
                && !Known("clue_family_avoids_marat"), "The checked household request fabricated a Marat clue.");
            await RoundTrip("len01-checked-list");
            await Talk("talk-mansur", "mansur_pc_request");
            Require(!HasChoice("choice-mansur-check-household-list"), "A completed request restarted after load.");
            await CloseTalk();
            Require(_bridge.SelectRuntimeState().GetProperty("presentation").GetProperty("openedDocumentIds")
                .EnumerateArray().Count(id => id.GetString() == Niva) == 1, "Rereading duplicated the document history.");

            // Direct opening and broad searches remain possible; neither may
            // fabricate the human answers that the first investigation needs.
            await OpenPc();
            await Search("Марат");
            Require(!HasDocument(Register) && !HasDocument(Message), "Search exposed later sources before their real prerequisites.");
            await ReadFromPc(Official);
            await ClosePc();
            Require(!Known("clue_family_avoids_marat") && !Available("house-to-route"),
                "Opening the official notice bypassed the actual family response.");
            Require(!await _bridge.OpenDocumentAsync(Register), "The direct document reader bypassed Naila's permission.");
            var registerRejected = false;
            try { await _bridge.HandleOldPcInputAsync(Input("open", Register)); }
            catch (InvalidOperationException) { registerRejected = true; }
            Require(registerRejected && !Known("clue_marat_case_boundary_marker"), "Direct old-PC opening bypassed register access.");

            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-marat");
            await CloseTalk();
            Require(Known("clue_family_notice_by_phone") && Known("clue_family_avoids_marat") && Available("house-to-route"),
                "Showing the actual notice did not record Mansur's answer as an alternative family path.");
            var familySources = new[] { Official, Knowledge("clue_family_notice_by_phone") };
            Require(!await _bridge.CompareJournalSourcesAsync(Interaction("compare-family-call-scope"), new[] { Niva, Official }),
                "Unrelated documents supported a family conclusion.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-family-call-erased"), familySources)
                && !Known("clue_notice_call_not_request"), "A false erased-record hypothesis was accepted as evidence.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-family-call-scope"), familySources)
                && Known("clue_notice_call_not_request") && !_bridge.IsOldPcDocumentAccessible(Message),
                "The family comparison failed or unlocked Marat's later message without the central contradiction.");
            await RoundTrip("len01-family-comparison");

            await Move("house-to-route", "village_day", "from_house");
            await Talk("talk-alsu", "alsu_route_context");
            await Choose("choice-alsu-versions");
            await CloseTalk();
            Require(Known("address-fap")
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("address-fap")) == 1,
                "Alsu's spoken address did not create one notebook entry.");
            await Talk("talk-alsu", "alsu_route_context");
            await Choose("choice-alsu-fap-address");
            await Choose("choice-alsu-address-number");
            await CloseTalk();
            Require(!Available("route-to-fap") && !Known("clue_marat_versions_conflict")
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("address-fap")) == 1,
                "Asking for directions duplicated the address or substituted for the investigation.");
            Require(Known("clue_alsu_heard_versions") && !Known("clue_marat_versions_conflict")
                && !Available("route-to-fap"), "A secondhand account supplied its own verification.");
            var versions = new[] { Official, Knowledge("clue_alsu_heard_versions") };
            Require(!await _bridge.CompareJournalSourcesAsync(Interaction("compare-versions-scope"), new[] { Official, Niva }),
                "A household receipt substituted for Alsu's actual account.");
            foreach (var wrong in new[] { "compare-versions-diagnoses", "compare-versions-departure" })
                Require(await _bridge.CompareJournalSourcesAsync(Interaction(wrong), versions)
                    && !Known("clue_marat_versions_conflict") && !Available("route-to-fap"),
                    "An unsupported account opened the main route: " + wrong);
            await RoundTrip("len01-unverified-versions");
            Require(Known("address-babai") && Known("address-fap")
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("address-babai")) == 1
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("address-fap")) == 1,
                "Loading lost or duplicated a spoken address.");
            Require(Known("clue_alsu_heard_versions") && !Known("clue_marat_versions_conflict")
                && !Available("route-to-fap"), "Loading a heard account turned it into a checked one.");
            await Talk("talk-alsu", "alsu_route_context");
            await Choose("choice-alsu-saw-departure");
            await CloseTalk();
            Require(!Known("clue_marat_versions_conflict"), "Asking Alsu to confirm a rumor replaced comparing the notice.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-versions-scope"), versions)
                && Available("route-to-fap"), "The bounded question to Naila did not open after checking both sources.");
            await Move("route-to-fap", "fap_clinic", "waiting_room");
            await Talk("talk-naila", "naila_medical_record");
            Require(!HasChoice("choice-naila-desk-light") && !Npc("naila", "desk_light_noticed"),
                "Naila reacted to a light that was never turned on.");
            await CloseTalk();
            Require(await _bridge.DispatchInteractionAsync(Interaction("discover-fap-interior-repaired-desk-object")),
                "The optional desk-lamp action failed.");
            await Frames(3);
            await Talk("talk-naila", "naila_medical_record");
            Require(HasChoice("choice-naila-desk-light") && !Npc("naila", "desk_light_noticed"),
                "An unanswered greeting credited the local help.");
            await CloseTalk();
            await RoundTrip("len01-lamp-unacknowledged");
            Require(Known("discovery-fap-interior-repaired-desk-object") && !Npc("naila", "desk_light_noticed")
                && _demo.DemoMain.ConnectedWorld!.GetZoneInstance("fap_clinic")!
                    .GetNode<OmniLight3D>("DiscoveryRepairedLamp/RepairedDeskLight").IsVisibleInTree(),
                "Save/load lost the actual light or invented Naila's acknowledgement.");
            Require(await Act1LocalReactionProof.CompleteAsync(this, _bridge, "lamp", verifyReturn: false),
                "Naila's actual lamp acknowledgement failed its visible reply and repeat guards.");
            await RoundTrip("len01-lamp-acknowledged");
            Require(Npc("naila", "desk_light_noticed") && !Npc("naila", "record_access_granted"),
                "The optional acknowledgement granted unrelated record access or was lost after loading.");
            await Talk("talk-naila", "naila_medical_record");
            Require(!HasChoice("choice-naila-desk-light") && !Npc("naila", "record_access_granted")
                && HasChoice("choice-naila-family-request-entry"),
                "The checked family question was lost on load, or Naila auto-granted access.");
            await Choose("choice-naila-family-request-entry");
            Require(Npc("naila", "record_access_granted") && Known("clue_naila_request_entry_scope")
                && !Known("clue_naila_record_scope") && !Known("clue_marat_case_boundary_marker"),
                "Naila's answer about requests invented her separate response to the internal category.");
            await CloseTalk();

            Require(await _bridge.DispatchInteractionAsync(Interaction("fap-to-document-desk")), "FAP desk did not unlock after a concrete question.");
            Require(await _bridge.DispatchInteractionAsync(Interaction("fap-document-desk-to-official-record")), "FAP notice transition failed.");
            await Move("official-to-internal-register", "house_old_pc", "entry");
            await OpenPc();
            await Search("реестр");
            await ReadFromPc(Register);
            await ClosePc();
            Require(!Known("contradiction_marat_official_vs_internal"), "Opening the register completed the human verification.");
            Require(!await _bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { Official, Register })
                && !Known("clue_notice_cause_excerpt") && !Known("clue_register_wording_excerpt")
                && !Known("clue_register_category_excerpt"), "Reading supplied the player's unselected fields.");
            await Act1SourceExcerptProof.RecordNoticeCauseAsync(this, _bridge);
            await RoundTrip("len01-notice-excerpt-only");
            Require(Known("clue_notice_cause_excerpt") && !Known("clue_register_wording_excerpt")
                && !await _bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { Official, Register }),
                "Loading one excerpt invented the missing field or enabled the comparison.");
            await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, _bridge);
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { Official, Register }),
                "The first wording comparison failed.");
            var beforeRevisit = _bridge.ActiveSceneId;
            await Revisit("HouseExit");
            await Revisit("RoadToFap");
            Require(_bridge.ActiveSceneId == beforeRevisit && _bridge.CurrentZoneId == "fap_clinic",
                "Returning to Naila rewound the active evidence scene.");
            Require(Npc("naila", "desk_light_noticed")
                && _demo.DemoMain.ConnectedWorld!.GetZoneInstance("fap_clinic")!
                    .GetNode<OmniLight3D>("DiscoveryRepairedLamp/RepairedDeskLight").IsVisibleInTree(),
                "The optional light or its acknowledged help was lost when returning from the house.");
            await Talk("talk-naila", "naila_medical_record");
            await Choose("choice-naila-contradiction");
            Require(!Known("clue_record_shared_formulation") && !Known("clue_naila_record_scope")
                && Npc("naila", "record_question_pending")
                && !await _bridge.EnterDialogueNodeAsync(Prefix + "dialogue/naila_medical_record", "separate-record"),
                "Asking about an unexamined category supplied Naila's explanation.");
            await Choose("choice-naila-category-as-diagnosis");
            Require(Status("hypothesis_category_is_diagnosis") == "hypothesis"
                && Known("clue_register_category_excerpt") && Npc("naila", "category_excerpt_review_requested")
                && !HasChoice("choice-naila-review-record-fields")
                && !Known("clue_naila_record_scope") && !_bridge.IsOldPcDocumentAccessible(Message),
                "Calling the category a diagnosis advanced the investigation.");
            await CloseTalk();
            await RoundTrip("len01-category-misread");
            Require(Status("hypothesis_category_is_diagnosis") == "hypothesis"
                && Npc("naila", "record_question_pending") && !Known("clue_naila_record_scope"),
                "Load erased the mistaken reading or replaced it with an answer.");
            await Revisit("OfficialRecordExitToStreet");
            await Revisit("ReturnToHouseRegister");
            await OpenPc();
            await Search("Марат");
            await ReadFromPc(Official);
            await ReadFromPc(Register);
            await ClosePc();
            Require(Status("hypothesis_category_is_diagnosis") == "hypothesis"
                && Npc("naila", "category_excerpt_review_requested")
                && !Known("clue_record_shared_formulation") && !Known("clue_naila_record_scope"),
                "Rereading alone answered the pending question to Naila.");
            await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, _bridge);
            Require(!Npc("naila", "category_excerpt_review_requested")
                && Status("hypothesis_category_is_diagnosis") == "hypothesis"
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("clue_register_category_excerpt")) == 1,
                "Re-extracting the actual category duplicated the source or answered for Naila.");
            await RoundTrip("len01-category-excerpt-rechecked");
            await Revisit("HouseExit");
            await Revisit("RoadToFap");
            await Talk("talk-naila", "naila_medical_record");
            Require(HasChoice("choice-naila-review-record-fields") && !HasChoice("choice-naila-ask-category-scope"),
                "The returned conversation lost the unresolved diagnosis mistake.");
            await Choose("choice-naila-review-record-fields");
            await Choose("choice-naila-show-external-wording");
            Require(Status("hypothesis_category_is_diagnosis") == "contradicted"
                && Known("clue_record_shared_formulation") && !Known("clue_naila_record_scope"),
                "Selecting the matching fields skipped the distinct question about the category.");
            await CloseTalk();
            await RoundTrip("len01-matching-fields");
            await Talk("talk-naila", "naila_medical_record");
            Require(HasChoice("choice-naila-ask-category-scope") && !HasChoice("choice-naila-category-as-diagnosis"),
                "The pending field-specific question was lost or the corrected mistake returned.");
            await Choose("choice-naila-ask-category-scope");
            Require(Known("clue_naila_record_scope") && !Npc("naila", "record_question_pending")
                && !Known("contradiction_marat_official_vs_internal") && _bridge.ActiveSceneId == beforeRevisit,
                "The actual answer either failed to close its question or supplied the unchecked conclusion.");
            await CloseTalk();
            var scopeSources = new[] { Register, Knowledge("clue_naila_record_scope") };
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope-lie"), scopeSources)
                && !Known("contradiction_marat_official_vs_internal"), "A refusal was promoted to proof of a lie.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), scopeSources)
                && Known("contradiction_marat_official_vs_internal"), "The actual Naila answer could not be checked against the register.");
            await Revisit("OfficialRecordExitToStreet");
            await Revisit("ReturnToHouseRegister");
            Require(_bridge.ActiveSceneId == beforeRevisit && _bridge.CurrentZoneId == "house_old_pc",
                "Returning home rewound the active evidence scene.");

            Require(!Known("clue_gulsina_warning_context"), "The initial home greeting supplied an unanswered warning question.");
            await Talk("talk-gulsina", "gulsina_yaramyy");
            await Choose("choice-gulsina-naila-word");
            Require(_dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text.Contains("Я за Наилю отвечать не стану", StringComparison.Ordinal),
                "The later warning question did not show Gulsina's source-specific answer.");
            await CloseTalk();
            Require(Known("clue_gulsina_warning_context"), "Gulsina's new contextual answer was not recorded.");
            await CheckHomeWarningNote();
            var warnings = new[] { Knowledge("clue_gulsina_warning_context"), Knowledge("clue_naila_record_scope") };
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-warning-agreement"), warnings)
                && !Known("clue_yaramyy_contexts_distinguished"), "A shared word proved agreement between two people.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-warning-rule"), warnings)
                && !Known("clue_do_not_answer_rule"), "The ordinary word revealed the finale's rule.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-warning-contexts"), warnings),
                "The two actual warnings could not be distinguished.");
            await RoundTrip("len01-warning-contexts");
            Require(Known("clue_yaramyy_contexts_distinguished") && !Known("clue_do_not_answer_rule"),
                "Loading the context comparison lost it or revealed the finale.");
            await CheckHomeWarningNote();

            await Talk("internal-register-to-rinat", "rinat_internal_register");
            Require(!Npc("rinat", "alerted") && !_bridge.IsOldPcDocumentAccessible(Message),
                "Rinat's greeting bypassed his response to the category.");
            await Choose("choice-rinat-present-category");
            await CloseTalk();
            Require(Status("clue_voice_answer_is_dangerous_hint") == "hidden",
                "Rinat's category response inferred a voice from an unread message.");
            Require(await _bridge.DispatchInteractionAsync(Interaction("internal-register-to-saved-message")),
                "The saved-message step did not open after Rinat answered.");
            await OpenPc();
            await Search("Марат");
            await ReadFromPc(Message);
            await ClosePc();
            var messageScene = _bridge.ActiveSceneId;
            var messageSources = new[] { Message, Knowledge("clue_alsu_heard_versions") };
            Require(Known("clue_marat_message_read") && !Known("clue_marat_was_afraid_before_death")
                && !Known("clue_alsu_message_reply") && !_bridge.IsOldPcDocumentAccessible(BoundaryArticle)
                && !await _bridge.OpenDocumentAsync(BoundaryArticle)
                && !Available("saved-message-to-boundary-source")
                && !await _bridge.DispatchInteractionAsync(Interaction("excerpt-message-voice"))
                && !Known("clue_message_voice_excerpt"),
                "Opening the message, an unrelated pair or the other reader bypassed its source check.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-message-seen"), messageSources),
                "The plausible sighting hypothesis could not be considered.");
            await RoundTrip("len01-message-misread");
            Require(Status("hypothesis_message_proves_sighting") == "hypothesis"
                && !Known("clue_message_question_prepared") && !_bridge.IsOldPcDocumentAccessible(BoundaryArticle),
                "The mistaken message reading disappeared on load or opened the article.");
            await Revisit("HouseExit");
            await Talk("talk-alsu", "alsu_route_context");
            Require(HasChoice("choice-alsu-show-message") && !Known("clue_alsu_message_reply"),
                "Alsu's new greeting supplied an unheard answer.");
            await Choose("choice-alsu-show-message");
            Require(!HasChoice("choice-alsu-quote-heard-voice") && !HasChoice("choice-alsu-correct-seen-person"),
                "Alsu supplied the source check before the player prepared the actual phrase.");
            await Choose("choice-alsu-quote-seen-person");
            Require(!Known("clue_marat_was_afraid_before_death") && !Known("clue_alsu_message_reply"),
                "Alsu's objection to an overstatement counted as the grounded conclusion.");
            await CloseTalk();
            await RoundTrip("len01-message-question-pending");
            await Revisit("ReturnToHouseRegister");
            await OpenPc();
            await Search("Марат");
            await ReadFromPc(Message);
            await ClosePc();
            await Act1SourceExcerptProof.RecordMessageVoiceAsync(this, _bridge);
            Require(Known("clue_message_question_prepared") && Known("clue_message_voice_excerpt") && !Known("clue_alsu_message_reply")
                && Status("hypothesis_message_proves_sighting") == "hypothesis"
                && !_bridge.IsOldPcDocumentAccessible(BoundaryArticle),
                "Preparing the accurate quotation silently corrected a spoken mistake or skipped the return.");
            await RoundTrip("len01-message-source-checked");
            await Revisit("HouseExit");
            await Talk("talk-alsu", "alsu_route_context");
            Require(HasChoice("choice-alsu-correct-seen-person") && !HasChoice("choice-alsu-quote-heard-voice"),
                "The resumed conversation lost its explicit correction of the earlier overstatement.");
            var beforeMessageTerminal = _bridge.SelectRuntimeState();
            Require(await _bridge.EnterDialogueNodeAsync(Prefix + "dialogue/alsu_route_context", "message-bounded-reply")
                && beforeMessageTerminal.GetProperty("knowledge").GetRawText() == _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText()
                && beforeMessageTerminal.GetProperty("journal").GetRawText() == _bridge.SelectRuntimeState().GetProperty("journal").GetRawText(),
                "Entering Alsu's terminal text without the explicit correction supplied her account.");
            await Choose("choice-alsu-correct-seen-person");
            Require(await Act1LocalReactionProof.VerifyVisibleReplyAsync(this, _bridge, "dialogue-alsu-message-bounded-reply")
                && HasChoice("choice-alsu-street-questions") && HasChoice("choice-alsu-check-original"),
                "The corrected message silently credited Alsu while hiding her actual reply or return choices.");
            await CloseTalk();
            await RoundTrip("len01-message-corrected");
            Require(Status("hypothesis_message_proves_sighting") == "contradicted"
                && Known("clue_alsu_message_reply") && Known("clue_marat_was_afraid_before_death")
                && _bridge.IsOldPcDocumentAccessible(BoundaryArticle) && _bridge.ActiveSceneId == messageScene
                && !Known("clue_do_not_answer_rule")
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("clue_alsu_message_reply")) == 1,
                "The corrected response lost its source, duplicated it, rewound the route or revealed the finale.");
            await Talk("talk-alsu", "alsu_route_context");
            Require(HasChoice("choice-alsu-versions") && !HasChoice("choice-alsu-show-message"),
                "The completed message conversation replayed its request or erased the original questions.");
            await CloseTalk();
            await Revisit("ReturnToHouseRegister");
            Require(await _bridge.DispatchInteractionAsync(Interaction("saved-message-to-boundary-source")),
                "The checked message and Alsu's actual response did not expose the archive lead.");
            Require(Status("clue_folklore_as_survival_rule") == "hidden"
                && Vocabulary("tt_javap") == "unknown" && Vocabulary("tt_tavysh") == "unknown",
                "Entering the article scene taught unread words or interpreted the voice.");
            await OpenPc();
            await Search("Шүрәле");
            await ReadFromPc(BoundaryArticle);
            await ClosePc();
            Require(Vocabulary("tt_javap") == "guessed" && Vocabulary("tt_tavysh") == "guessed"
                && Vocabulary("tt_shurale") == "guessed"
                && Status("discovery-kara-branch-profile") == "hidden"
                && _bridge.SelectRuntimeState().GetProperty("presentation").GetProperty("openedDocumentIds")
                    .EnumerateArray().Any(id => id.GetString() == BoundaryArticle)
                && Status("clue_folklore_as_survival_rule") == "hidden",
                "Reading the article failed to expose its words, or did the source comparison automatically.");
            await CheckRevision("compare-voice-echo", "hypothesis_voice_explained_as_echo", "revise-echo",
                new[] { Message, BoundaryArticle }, Message, BoundaryArticle, "compare-voice-link");
            await CheckRevision("compare-voice-creature", "hypothesis_heading_identifies_voice", "revise-creature",
                new[] { Message, BoundaryArticle }, BoundaryArticle, Message, "compare-voice-link");
            Require(Vocabulary("tt_javap") == "guessed" && Vocabulary("tt_tavysh") == "guessed"
                && !Known("clue_do_not_answer_rule") && !_bridge.IsOldPcDocumentAccessible(EdgeSketch),
                "Revising unsupported certainty taught the translation, the finale or the route automatically.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { Message, BoundaryArticle }),
                "The message and actual article could not be compared.");
            var rereadTarget = _demo.DemoMain.ConnectedWorld?.FindChild("BoundarySourceToReread", true, false) as InteractionTarget;
            Require(rereadTarget?.IsAvailable() == true && rereadTarget.DocumentId == BoundaryArticle,
                "The reread target lost its real source attachment.");
            rereadTarget!.Interact();
            var documentUi = (DocumentUi)GetTree().GetFirstNodeInGroup("document_ui");
            for (var frame = 0; frame < 180 && !documentUi.IsOpen; frame++) await Frames(1);
            Require(documentUi.IsOpen && documentUi.OpenDocumentId == BoundaryArticle
                && Beat("boundary-source-reopened") == "completed",
                "The production reread target advanced without presenting the original article.");
            documentUi._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
            await Frames(3);
            Require(!_player.ModalOpen, "Rereading the source retained a modal after close.");
            Require(!_bridge.IsOldPcDocumentAccessible(EdgeSketch)
                && await _bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), new[] { Register, BoundaryArticle }),
                "The sketch skipped applying the translated words to the register.");
            if (!await Act1SourceReturnsProof.CompleteAsync(this, _bridge, verifyReturn: true)) return;
            Require(await _bridge.DispatchInteractionAsync(Interaction("reread-to-edge-sketch")),
                "The source returns and interpreted register did not open the sketch step.");
            Require(!Known("clue_kara_urman_edge_is_rule_boundary")
                && Status("clue_mansur_allowed_pc_access_deliberately") == "hypothesis",
                "Entering the sketch read it or changed the bounded inference about Mansur.");
            await OpenPc();
            await Search("кромка");
            await ReadFromPc(EdgeSketch);
            await DocumentImageUiProof.VerifyArchiveAsync(this, _bridge, EdgeSketch, "len01_edge_sketch");
            await ClosePc();
            Require(!Available("edge-sketch-to-zirat-road") && !Known("clue_route_check_discussed"),
                "Reading a map replaced the main-route conversation.");
            Require(!await _bridge.CompareJournalSourcesAsync(Interaction("compare-route-purpose-landmarks"), new[] { EdgeSketch, Register }),
                "The route purpose accepted the register in place of Marat's message.");

            var sketchScene = _bridge.ActiveSceneId;
            await Revisit("HouseExit");
            await Talk("route-to-mosque", "timur_restraint");
            Require(!Known("clue_timur_warns_against_marat_path") && !HasChoice("choice-timur-route-check"),
                "Timur's greeting supplied his unheard warning or skipped the account of the register.");
            Require(Vocabulary("tt_shurale") == "guessed" && HasChoice("choice-timur-shurale"),
                "The actual article did not make its name available to ask about without an optional discovery.");
            await CloseTalk();
            await RoundTrip("len01-unanswered-timur");
            Require(!Known("clue_timur_warns_against_marat_path") && !Available("edge-sketch-to-zirat-road"),
                "An unanswered greeting became a conversation after load.");
            Require(Vocabulary("tt_shurale") == "guessed", "Abandoning the question confirmed the folklore name.");
            await TalkToTimurTarget();
            await VerifyShuraleReply();
            await CloseTalk();
            await RoundTrip("len01-shurale-name-heard");
            Require(Vocabulary("tt_shurale") == "confirmed" && !Known("clue_timur_warns_against_marat_path")
                && !Known("clue_do_not_answer_rule") && !Available("edge-sketch-to-zirat-road"),
                "The word's saved meaning replaced the route discussion or taught a creature rule.");
            await TalkToTimurTarget();
            await Choose("choice-timur-register");
            Require(Known("clue_timur_warns_against_marat_path") && !Available("edge-sketch-to-zirat-road"),
                "The warning was not tied to its spoken reply, or completed the entire plan.");
            await CloseTalk();
            await TalkToTimurTarget();
            await VerifyShuraleReply();
            await CloseTalk();
            await TalkToTimurTarget();
            await Choose("choice-timur-route-check");
            Require(!HasChoice("choice-timur-name-landmarks"), "Timur supplied a purpose before I compared the sources.");
            await Choose("choice-timur-marat-waiting");
            await CloseTalk();
            await RoundTrip("len01-route-hope");
            Require(!Known("clue_route_check_intent") && !Known("clue_route_check_discussed")
                && !Available("edge-sketch-to-zirat-road"), "A hopeful answer or its save completed the route plan.");
            await Talk("route-to-mosque", "timur_restraint");
            await Choose("choice-timur-route-check");
            await Choose("choice-timur-repeat-marat");
            await CloseTalk();
            Require(!Known("clue_route_check_discussed") && !Known("clue_do_not_answer_rule"),
                "The refusal either completed the conversation or taught the finale's rule.");
            foreach (var wrong in new[] { "compare-route-purpose-alive", "compare-route-purpose-summon" })
                Require(await _bridge.CompareJournalSourcesAsync(Interaction(wrong), new[] { Message, EdgeSketch })
                    && !Known("clue_route_check_intent") && !Available("edge-sketch-to-zirat-road"),
                    "A wished-for outcome substituted for checking the route: " + wrong);

            await Revisit("ReturnToHouseRegister");
            await OpenPc();
            await Search("Шүрәле");
            await ReadFromPc(BoundaryArticle);
            Require(Vocabulary("tt_javap") == "confirmed" && Vocabulary("tt_tavysh") == "confirmed"
                && Vocabulary("tt_shurale") == "confirmed",
                "Rereading a document downgraded its previously understood words.");
            await Search("Марат");
            await ReadFromPc(Message);
            await Search("кромка");
            await ReadFromPc(EdgeSketch);
            await ClosePc();
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-route-purpose-landmarks"), new[] { Message, EdgeSketch })
                && Known("clue_route_check_intent") && !Available("edge-sketch-to-zirat-road"),
                "The checked purpose failed, or substituted for returning to the conversation.");
            await RoundTrip("len01-route-purpose");
            Require(Known("clue_route_check_intent") && !Known("clue_route_check_discussed"),
                "Load conflated a prepared answer with a spoken one.");
            await Revisit("HouseExit");
            await Talk("route-to-mosque", "timur_restraint");
            await Choose("choice-timur-route-check");
            await Choose("choice-timur-name-landmarks");
            await CloseTalk();
            Require(Known("clue_route_check_discussed") && Available("edge-sketch-to-zirat-road")
                && _bridge.ActiveSceneId == sketchScene && !Known("clue_do_not_answer_rule"),
                "The concrete route conversation lost the investigation, failed to enable departure, or spoiled the finale.");
            await RoundTrip("len01-route-discussed");
            Require(Known("clue_route_check_discussed") && Available("edge-sketch-to-zirat-road")
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("clue_route_check_discussed")) == 1,
                "The completed plan did not survive load as a single source.");
            await TalkToTimurTarget();
            Require(!HasChoice("choice-timur-register") && !HasChoice("choice-timur-route-check")
                && HasChoice("choice-timur-grandparents"),
                "A completed route conversation replayed its request or removed Timur's other questions.");
            await VerifyShuraleReply();
            await CloseTalk();

            await Move("edge-sketch-to-zirat-road", "zirat_road", "village_side");
            Require(await Act1RinatRoadsideProof.ObserveAsync(this, _bridge, verifyReturn: true),
                "Rinat's visible roadside step did not survive the ordinary runtime and save path.");
            Require(!Known("clue_zirat_roadside_marks") && !Known("clue_sketch_field_landmarks") && !Known("clue_marat_last_route_near_zirat")
                && !Available("zirat-road-to-forest"), "Entering the road fabricated a field observation.");
            var roadMarks = Knowledge("clue_zirat_roadside_marks");
            var fieldPair = new[] { EdgeSketch, roadMarks };
            await Act1RouteLandmarksProof.ObserveAsync(this, _bridge);
            Require(Known("clue_sketch_field_landmarks") && !Known("clue_zirat_roadside_marks")
                && !Known("clue_marat_last_route_near_zirat") && !Available("zirat-road-to-forest")
                && !await _bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), fieldPair),
                "Looking over the ditch and fence supplied the unread notches or the complete route.");
            await RoundTrip("len01-landmarks-before-tag");
            Require(Known("clue_sketch_field_landmarks") && !Known("clue_zirat_roadside_marks")
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("clue_sketch_field_landmarks")) == 1
                && !Known("clue_marat_last_route_near_zirat") && !Available("zirat-road-to-forest"),
                "The saved field view was lost, duplicated or turned into a read roadside tag.");
            var routeJournal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
            routeJournal.Open(_bridge, EdgeSketch);
            await Frames(3);
            Require(routeJournal.ActiveEntryId == EdgeSketch, "The route sketch could not be revisited between observations.");
            routeJournal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await Frames(3);
            Require(!_bridge.JournalActions(fieldPair).Any(action => action.Id == Interaction("compare-route-match"))
                && !await _bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), fieldPair)
                && !Known("clue_zirat_roadside_marks") && !Known("clue_marat_last_route_near_zirat"),
                "Loading or rereading the sketch bypassed the separate inspection of the tag.");
            Require(await _bridge.DispatchInteractionAsync(Interaction("zirat-roadside-clue")),
                "The reachable main road observation failed.");
            await CheckRevision("compare-route-center", "hypothesis_route_enters_zirat", "revise-inside",
                new[] { EdgeSketch, roadMarks }, EdgeSketch, roadMarks, "compare-route-match");
            await CheckRevision("compare-route-marat-proof", "hypothesis_tag_identifies_marat", "revise-identity",
                new[] { EdgeSketch, roadMarks }, roadMarks, EdgeSketch, "compare-route-match");
            Require(!Known("clue_marat_last_route_near_zirat") && !Available("zirat-road-to-forest")
                && !Known("clue_do_not_answer_rule"), "Correcting a wrong route granted the actual comparison or finale.");
            await CompareInJournal("compare-route-match", new[] { EdgeSketch, roadMarks });
            Require(Known("clue_marat_last_route_near_zirat") && Available("zirat-road-to-forest")
                && !Known("clue_sketch_tag_base_checked") && !Known("clue_sketch_place_compared"),
                "The corrected field comparison failed or required optional tag-base exploration.");
            GD.Print("route-source-order-proof: field view -> save/load -> sketch revisit -> tag -> two revised hypotheses -> main-route comparison");

            await VerifyAlternativeFamilyStart();

            GD.Print("act1-len01-family-flow: normal start; unanswered permission; domestic wrong source/reread/return; family and Alsu source checks; Naila field question/misreading/reread/correction; message source/overstatement/Alsu return; voice and route hypotheses corrected against original sources through journal UI; production article reread presentation; preserved-world revisits; unearned-source rejection; Timur greeting/doubt/refusal/reread/return; distinct saved investigation stages; clean new game. Technical coverage only.");
            await GodotSmokeCleanup.ReleaseAsync(_demo);
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("act1-len01-family-flow: " + exception);
            if (GodotObject.IsInstanceValid(_demo)) await GodotSmokeCleanup.ReleaseAsync(_demo);
            GetTree().Quit(1);
        }
    }


    private async Task VerifyAlternativeFamilyStart()
    {
        // A second ordinary New Game proves reset and the formerly missing
        // early question without seeding knowledge, inventory or debug state.
        Require(await _bridge.StartNewGameAsync(), "The normal session reset failed.");
        await Frames(6);
        if (_demo.IntroVisible)
        {
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(4);
        }
        await Act1ArrivalFlowProof.CompleteAsync(this, _bridge, reply: "keep-silent");
        Require(Status("clue_mansur_allowed_pc_access_deliberately") == "hidden"
            && Status("clue_village_has_internal_compensation_system") == "hidden"
            && !Known("clue_mansur_unsent_note_read") && !Known("clue_accounting_fragment_read")
            && !Known("clue_rinat_accounting_question_heard"),
            "New Game retained the prior source returns or their interpretations.");
        await Move("arrival-enter-house", "house_old_pc", "entry");
        Require(!Known("clue_household_niva_list") && !Npc("mansur", "household_help_done")
            && !Known("clue_naila_record_scope") && !Known("clue_alsu_heard_versions")
            && !Known("clue_marat_versions_conflict") && !Known("clue_route_check_intent")
            && !Known("clue_timur_warns_against_marat_path") && !Known("clue_route_check_discussed")
            && !Npc("naila", "desk_light_noticed") && !Known("discovery-fap-interior-repaired-desk-object")
            && !Known("clue_record_shared_formulation") && Status("hypothesis_category_is_diagnosis") == "hidden"
            && !Known("clue_marat_message_read") && !Known("clue_message_question_prepared")
            && !Known("clue_notice_cause_excerpt") && !Known("clue_register_wording_excerpt")
            && !Known("clue_register_category_excerpt") && !Known("clue_message_voice_excerpt")
            && !Npc("naila", "category_excerpt_review_requested")
            && !Known("clue_alsu_message_reply") && Status("hypothesis_message_proves_sighting") == "hidden"
            && Status("hypothesis_voice_explained_as_echo") == "hidden" && Status("hypothesis_heading_identifies_voice") == "hidden"
            && Status("hypothesis_route_enters_zirat") == "hidden" && Status("hypothesis_tag_identifies_marat") == "hidden"
            && !Npc("naila", "record_question_pending") && !Npc("alsu", "message_shown")
            && !Known("clue_rinat_at_roadside") && !Known("clue_sketch_field_landmarks")
            && !Known("clue_zirat_roadside_marks") && !Known("clue_marat_last_route_near_zirat"),
            "A new game retained investigation results.");
        await Talk("talk-gulsina", "gulsina_yaramyy");
        await Choose("choice-gulsina-marat");
        await CloseTalk();
        Require(Known("clue_family_avoids_marat") && !Known("clue_marat_official_death_version")
            && !Available("house-to-route") && Beat("house-warmth-and-pause") != "completed",
            "The early family question fabricated the meal, required the notice or bypassed finding it.");
        Require(!Known("clue_gulsina_warning_context"), "New Game or a question about Marat fabricated the warning explanation.");
        await Talk("talk-gulsina", "gulsina_yaramyy");
        await Choose("choice-gulsina-yaramyy");
        Require(_dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text.Contains("Мне спокойнее, когда ты дома", StringComparison.Ordinal),
            "The early warning explanation was replaced with the later Naila conversation.");
        await CloseTalk();
        Require(!Known("clue_naila_record_scope") && !Known("clue_yaramyy_contexts_distinguished"),
            "The early domestic warning fabricated an unread medical conversation or comparison.");
        await RoundTrip("len01-early-warning");
        await CheckHomeWarningNote();
        await Talk("talk-gulsina", "gulsina_yaramyy");
        await Choose("choice-gulsina-yaramyy");
        await CloseTalk();
        await CheckHomeWarningNote();
        await Talk("talk-mansur", "mansur_pc_request");
        await Choose("choice-mansur-offer-help");
        await CloseTalk();
        await OpenPc();
        await Search("Марат");
        await ReadFromPc(Official);
        await ClosePc();
        Require(!Available("house-to-route"), "Reading the notice bypassed the still-unfinished family pause.");
        Require(!Known("family_home_pause") && !Known("memory_gulsina_childhood_table")
            && await Act1FamilyMealProof.CompleteAsync(this, _bridge, verifyReturn: true, topic: "silent", verifyExteriorReturn: true)
            && !Known("memory_gulsina_childhood_table") && Known("clue_marat_official_death_version")
            && Available("house-to-route"),
            "The alternative home conversation inherited a photograph discussion or lost the earned departure.");
    }

    private async Task VerifyEarlyDirections()
    {
        Require(!Known("clue_marat_official_death_version") && !Known("clue_alsu_heard_versions"),
            "Early directions started after a fabricated source or conversation.");
        var scene = _bridge.ActiveSceneId;
        Require(await Act1AlsuWalkProof.CompleteAsync(this, _bridge, verifyReturn: true),
            "The actual street meeting failed before the early directions.");
        await Talk("talk-alsu", "alsu_route_context");
        Require(!HasChoice("choice-alsu-fap-address") && HasChoice("choice-alsu-babai-address"),
            "Alsu offered an unheard FAP address or forgot the actual mother's message.");
        await Choose("choice-alsu-versions");
        var spoken = _dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text;
        Require(!spoken.Contains("смотрит на лист", StringComparison.Ordinal)
            && !spoken.Contains("В справке", StringComparison.Ordinal),
            "The early conversation pretended that Aidar had already brought the official source.");
        await CloseTalk();
        var account = _bridge.JournalEntries().Single(entry => entry.EntryId == Knowledge("clue_alsu_heard_versions"));
        Require(!account.Body.Contains("стоят в одной строке", StringComparison.Ordinal)
            && !Known("clue_marat_official_death_version") && !Known("clue_marat_versions_conflict")
            && !Available("route-to-fap") && _bridge.ActiveSceneId == scene,
            "A heard account revealed the unread notice or replaced the source comparison.");
        await Talk("talk-alsu", "alsu_route_context");
        await Choose("choice-alsu-fap-address");
        await Choose("choice-alsu-address-number");
        var fapAddress = _bridge.ResolveWorldText("{address:ADR-FAP}");
        var addressReply = _dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text;
        Require(!fapAddress.Contains("{address:", StringComparison.Ordinal)
            && addressReply.Contains(fapAddress, StringComparison.Ordinal),
            "The repeated directions did not resolve the same stable address as the world registry.");
        await CloseTalk();
        await RoundTrip("len01-early-directions");
        Require(Known("address-fap") && !Known("clue_marat_official_death_version")
            && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("address-fap")) == 1,
            "An early address was lost, duplicated or turned into official evidence after load.");
        await Talk("talk-alsu", "alsu_route_context");
        await Choose("choice-alsu-babai-address");
        Require(_dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
                .Contains(_bridge.ResolveWorldText("{address:ADR-BABAI}"), StringComparison.Ordinal),
            "The return-home answer disagreed with the saved home address.");
        await CloseTalk();
        GD.Print("len01-early-address: real conversation before notice; hearsay has no unread wording; repeat and load retain one address; main comparison remains required");
    }

    // Opt-in local regression through the existing LEN scene. It exercises real
    // target bindings and visible readers; it does not measure physical traversal
    // or a first player's duration and does not grant a future-act scene.
    private async Task VerifyVillageLife()
    {
        const string faridaPost = "urman.oldpc:document/social_farida_school_album";
        var scene = _bridge.ActiveSceneId;
        var beats = _bridge.SelectRuntimeState().GetProperty("beats").GetRawText();
        var documents = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi
            ?? throw new InvalidOperationException("Village life has no document reader.");
        var docIds = new[] { "arrival-stop-timetable", "shop-account-book", "school-transport-notice", "school-class-photo", "school-staff-note",
            "council-village-plan", "council-sabantuy-poster", "council-photo-album", "zirat-family-links" };
        Require(!Known("school-class-photo-read") && !Known("sabirov-family-linked")
            && !_bridge.IsOldPcDocumentAccessible(faridaPost), "Village life began with unread family sources.");
        Require(!await _bridge.ChooseDialogueAsync(Prefix + "dialogue/village_shop", "greeting", "school-photo")
            && !await _bridge.ChooseDialogueAsync(Prefix + "dialogue/village_shop", "greeting", "sabirov-family"),
            "A direct question bypassed the school photograph or family sources.");
        var knowledgeBeforeTimetable = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        await ReadWorldDocument("arrival-stop-timetable", "Школьный подвоз идёт отдельно");
        Require(_bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == knowledgeBeforeTimetable
            && _bridge.ActiveSceneId == scene
            && _bridge.SelectRuntimeState().GetProperty("beats").GetRawText() == beats,
            "Reading the public bus timetable supplied story knowledge or advanced the arrival.");
        await ShopTalk();
        Require(!HasChoice("shop-photo-choice") && !HasChoice("shop-family-choice"),
            "Razilya offered a memory before its source was found.");
        await TerminalHasNoOutcome("directions");
        Require(!Known("address-school") && !Known("address-council"),
            "Entering the directions reply supplied addresses before asking the question.");
        await Choose("shop-directions-choice");
        await CloseTalk();
        Require(Known("address-shop") && Known("address-school") && Known("address-council"),
            "The spoken civic addresses did not reach the notebook.");
        var propsBeforeLedger = _bridge.SelectWorldProps().GetRawText();
        await ReadWorldDocument("shop-account-book", "Страница Мансура");
        Require(_bridge.SelectWorldProps().GetRawText() == propsBeforeLedger,
            "Looking at the account book created a purchase or changed item custody.");
        await ReadWorldDocument("school-transport-notice", "детей осталось мало");
        await ReadWorldDocument("school-class-photo", "Семеро детей");
        Require(!Known("shop-school-photo-memory"), "Opening a photograph invented Razilya's recognition.");
        await DocumentImageUiProof.VerifyPaperAndJournalAsync(this, _bridge,
            Prefix + "document/school-class-photo", "village_school_photo");
        await TerminalHasNoOutcome("school-photo");
        await ShopTalk();
        Require(HasChoice("shop-photo-choice"), "The found photograph did not open its specific human question.");
        await CloseTalk();
        await RoundTrip("len01-school-question-abandoned");
        Require(!Known("shop-school-photo-memory"), "Abandoning or loading answered the photograph question.");
        await ShopTalk();
        await Choose("shop-photo-choice");
        await CloseTalk();
        Require(Known("shop-school-photo-memory"), "Razilya's actual reply did not record her recollection.");
        await ReadWorldDocument("school-staff-note", "Фәридә Габдулловна Сабирова");
        Require(_bridge.IsOldPcDocumentAccessible(faridaPost),
            "The school source did not expose its linked local post on the computer.");
        await ReadWorldDocument("council-village-plan", "Сельсовет и клуб");
        await ReadWorldDocument("council-sabantuy-poster", "лето 2005");
        await ReadWorldDocument("council-photo-album", "Папа и мама");
        await DocumentImageUiProof.VerifyPaperAndJournalAsync(this, _bridge,
            Prefix + "document/council-photo-album", "village_council_album");
        var familyPair = new[] { Prefix + "document/school-staff-note", Prefix + "document/council-photo-album" };
        Require(!Known("sabirov-family-linked")
            && !await _bridge.CompareJournalSourcesAsync(Interaction("compare-sabirov-family"),
                new[] { Prefix + "document/school-staff-note", Prefix + "document/council-sabantuy-poster" }),
            "Opening two documents or a similar unrelated source fabricated their family link.");
        await CompareInJournal("compare-sabirov-authorship", familyPair);
        Require(!Known("sabirov-family-linked"), "A mistaken attribution of every photograph was accepted.");
        await RoundTrip("len01-family-attribution-unresolved");
        Require(!Known("sabirov-family-linked"), "Loading supplied the missing family comparison.");
        await CompareInJournal("compare-sabirov-family", familyPair);
        Require(Known("sabirov-family-linked"), "Comparing the complete names did not retain the family link.");
        await ShopTalk();
        Require(!HasChoice("shop-family-choice"), "Razilya heard that Aidar visited the zirat before he read its names.");
        await CloseTalk();
        await ReadWorldDocument("zirat-family-links", "1938–2009");
        await TerminalHasNoOutcome("sabirov-family");
        await ShopTalk();
        Require(HasChoice("shop-family-choice"), "The related grave names did not open their specific conversation.");
        await CloseTalk();
        await RoundTrip("len01-family-memory-unanswered");
        Require(!Known("shop-sabirov-family-memory"), "A greeting or save answered the family question.");
        await ShopTalk();
        await Choose("shop-family-choice");
        await CloseTalk();
        Require(Known("shop-sabirov-family-memory"), "The actual family reply was not retained.");
        await RoundTrip("len01-village-life-completed");
        await ShopTalk();
        await Choose("shop-family-choice");
        await CloseTalk();
        foreach (var id in docIds)
            Require(_bridge.JournalEntries().Count(entry => entry.EntryId == Prefix + "document/" + id) == 1,
                "A local source was lost or duplicated after loading: " + id);
        foreach (var id in new[] { "shop-school-photo-memory", "sabirov-family-linked", "shop-sabirov-family-memory" })
            Require(Known(id) && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge(id)) == 1,
                "The local human consequence was lost or duplicated: " + id);
        Require(_bridge.ActiveSceneId == scene
            && _bridge.SelectRuntimeState().GetProperty("beats").GetRawText() == beats
            && !Known("clue_marat_official_death_version") && !Known("clue_do_not_answer_rule")
            && !Known("clue_marat_last_route_near_zirat"),
            "Optional civic sources advanced the main investigation, final route or story scene.");
        Require(await _bridge.StartNewGameAsync(), "New Game failed after the civic source cycle.");
        await Frames(6);
        foreach (var id in new[] { "address-shop", "address-school", "address-council", "school-class-photo-read",
                     "school-staff-note-read", "sabirov-family-linked", "shop-sabirov-family-memory" })
            Require(!Known(id), "New Game inherited a civic discovery: " + id);
        Require(!_bridge.IsOldPcDocumentAccessible(faridaPost), "New Game inherited the gated family post.");
        GD.Print("act1-village-life: actual target/document/dialogue/journal owners; photo recognition, wrong family attribution, source gate, refusal/return, single saved consequences and New Game; traversal, cultural and human-duration acceptance remain separate");

        async Task TerminalHasNoOutcome(string nodeId)
        {
            var before = _bridge.SelectRuntimeState();
            var knowledge = before.GetProperty("knowledge").GetRawText();
            var journal = before.GetProperty("journal").GetRawText();
            Require(await _bridge.EnterDialogueNodeAsync(Prefix + "dialogue/village_shop", nodeId),
                "The found source did not satisfy the shop reply's stable prerequisites: " + nodeId);
            var after = _bridge.SelectRuntimeState();
            Require(after.GetProperty("knowledge").GetRawText() == knowledge
                && after.GetProperty("journal").GetRawText() == journal,
                "Entering a shop reply without choosing its question granted an outcome: " + nodeId);
        }

        InteractionTarget Target(string action) => _demo.DemoMain.ConnectedWorld!
            .FindChildren("*", "", true, false).OfType<InteractionTarget>()
            .SingleOrDefault(target => target.InteractionId == Interaction(action))
            ?? throw new InvalidOperationException("No unique physical civic target: " + action);

        async Task ShopTalk()
        {
            var target = Target("village-shop-greeting");
            Require(target.IsAvailable() && target.DialogueId == Prefix + "dialogue/village_shop",
                "The physical shopkeeper is unavailable or bound to the wrong conversation.");
            target.Interact();
            for (var frame = 0; frame < 180 && !_dialogue.IsOpen; frame++) await Frames(1);
            await Frames(3);
            Require(_dialogue.IsOpen && _player.ModalOpen, "The shop target did not open its actual dialogue.");
        }

        async Task ReadWorldDocument(string id, string expectedText)
        {
            var target = Target(id);
            Require(target.IsAvailable() && target.DocumentId == Prefix + "document/" + id,
                "The physical source is unavailable or has the wrong document: " + id);
            target.Interact();
            for (var frame = 0; frame < 180 && !documents.IsOpen; frame++) await Frames(1);
            Require(documents.IsOpen && documents.OpenDocumentId == Prefix + "document/" + id && _player.ModalOpen,
                "The civic target failed to open the production reader: " + id);
            var body = documents.GetNode<RichTextLabel>("Screen/Document/Layout/Reader/Body").Text;
            Require(body.Contains(expectedText, StringComparison.Ordinal) && !body.Contains("{address:", StringComparison.Ordinal),
                "The real reader lost source text or displayed unresolved address tokens: " + id);
            documents.GetNode<Button>("Screen/Document/Layout/Footer/Save").EmitSignal(Button.SignalName.Pressed);
            for (var frame = 0; frame < 180 && !_bridge.JournalEntries().Any(entry => entry.EntryId == Prefix + "document/" + id); frame++)
                await Frames(1);
            Require(_bridge.JournalEntries().Count(entry => entry.EntryId == Prefix + "document/" + id) == 1,
                "The visible Save action did not retain one actual source: " + id);
            documents.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await Frames(3);
            Require(!documents.IsOpen && !_player.ModalOpen, "Closing the civic reader retained player input.");
        }
    }

    private async Task VerifyIntroLayout()
    {
        var originalSize = DisplayServer.WindowGetSize();
        var preferences = _player.Accessibility;
        try
        {
            foreach (var sample in new[] { (1280, 720, 1.6), (1920, 1080, 1.0) })
            {
                DisplayServer.WindowSetSize(new Vector2I(sample.Item1, sample.Item2));
                AccessibilityPresentation.ApplyToTree(GetTree(), preferences with { TextScale = sample.Item3 });
                await Frames(5);
                var panel = _demo.FindChild("IntroPanel", true, false) as Control;
                Require(_demo.IntroVisible && _player.ModalOpen && panel is not null && panel.IsVisibleInTree(),
                    "The ordinary arrival intro disappeared before confirmation.");
                var labels = panel!.FindChildren("*", "Label", true, false).OfType<Label>()
                    .Where(label => label.IsVisibleInTree()).ToArray();
                var text = string.Join(" ", labels.Select(label => label.Text));
                Require(text.Contains("скамье") && text.Contains("телефон") && text.Contains("фото Марата"),
                    "The ordinary intro does not name the first personal source and its location.");
                foreach (var control in labels.Cast<Control>().Prepend(panel!))
                {
                    var rect = control.GetGlobalRect();
                    var size = control.GetViewportRect().Size;
                    Require(rect.Position.X >= -1 && rect.Position.Y >= -1
                        && rect.End.X <= size.X + 1 && rect.End.Y <= size.Y + 1,
                        "The arrival intro is clipped at large text: " + control.GetPath());
                }
                var output = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
                if (!string.IsNullOrEmpty(output))
                {
                    var path = System.IO.Path.Combine(output, $"arrival_intro_{sample.Item1}x{sample.Item2}.png");
                    Require(!System.IO.File.Exists(path), "Arrival intro capture would replace an existing proof.");
                    System.IO.Directory.CreateDirectory(output);
                    RenderingServer.ForceDraw(false);
                    using var shot = GetViewport().GetTexture().GetImage();
                    Require(!shot.IsEmpty() && shot.SavePng(path) == Error.Ok, "Arrival intro capture failed.");
                }
                GD.Print($"arrival-intro-ui: requested={sample.Item1}x{sample.Item2} actual={DisplayServer.WindowGetSize()} textScale={sample.Item3}; source hint and panel bounds");
            }
        }
        finally
        {
            DisplayServer.WindowSetSize(originalSize);
            AccessibilityPresentation.ApplyToTree(GetTree(), preferences);
            await Frames(4);
        }
    }

    private async Task CheckRevision(string attempt, string hypothesis, string correction, string[] sourcePair,
        string evidence, string wrongEvidence, string mainComparison)
    {
        Require(Status(hypothesis) == "hidden", "An unchosen error was already recorded: " + hypothesis);
        await CompareInJournal(attempt, sourcePair);
        Require(Status(hypothesis) == "hypothesis"
            && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge(hypothesis)) == 1,
            "A proposed explanation did not leave exactly one revisitable hypothesis.");
        await CheckJournalStatus(Knowledge(hypothesis), "Версия");
        Require(!await _bridge.CompareJournalSourcesAsync(Interaction(mainComparison), sourcePair)
            && !await _bridge.CompareJournalSourcesAsync(Interaction(attempt), sourcePair)
            && !await _bridge.CompareJournalSourcesAsync(Interaction(correction), new[] { Knowledge(hypothesis), wrongEvidence }),
            "Clicking a different answer, repeating the error, or an unrelated source silently revised the hypothesis.");
        await RoundTrip("len01-pending-" + correction);
        Require(Status(hypothesis) == "hypothesis"
            && !await _bridge.CompareJournalSourcesAsync(Interaction(mainComparison), sourcePair),
            "Load silently replaced a player's pending interpretation.");
        var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        journal.Open(_bridge, evidence);
        await Frames(3);
        Require(journal.ActiveEntryId == evidence, "The evidence could not be reread in place.");
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Require(Status(hypothesis) == "hypothesis", "Opening or closing the source revised the player's interpretation.");
        await CompareInJournal(correction, new[] { Knowledge(hypothesis), evidence });
        Require(Status(hypothesis) == "contradicted"
            && !_bridge.JournalActions(new[] { Knowledge(hypothesis), evidence }).Any()
            && _bridge.JournalActions(sourcePair).Any(action => action.Id == Interaction(mainComparison)),
            "The source correction failed to preserve its revision and restore the bounded comparison.");
        await RoundTrip("len01-revised-" + correction);
        Require(Status(hypothesis) == "contradicted"
            && _bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge(hypothesis)) == 1,
            "A corrected interpretation duplicated or changed after load.");
        await CheckJournalStatus(Knowledge(hypothesis), "Пересмотрено");
    }

    private async Task CheckHomeWarningNote()
    {
        var entryId = Knowledge("clue_gulsina_warning_context");
        Require(_bridge.JournalEntries().Count(entry => entry.EntryId == entryId) == 1,
            "Returning to the warning created duplicate entries.");
        var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        journal.Open(_bridge, entryId);
        await Frames(3);
        var body = journal.GetNode<RichTextLabel>("Screen/Book/Layout/WorkArea/Reader/Body").Text;
        Require(journal.ActiveEntryId == entryId && body.Contains("к лесу вечером", StringComparison.Ordinal)
            && !body.Contains("«Я знаю, ты не маленький", StringComparison.Ordinal)
            && !body.Contains("Наил", StringComparison.Ordinal),
            "The shared home note attributed an unheard quotation or another person's conversation to Gulsina.");
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Require(!_player.ModalOpen, "The home warning reader retained input after closing.");
    }

    private async Task CheckJournalStatus(string entryId, string status)
    {
        var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        journal.Open(_bridge, entryId);
        await Frames(3);
        Require(journal.ActiveEntryId == entryId
            && journal.GetNode<Label>("Screen/Book/Layout/WorkArea/Reader/Source").Text.Contains(status, StringComparison.Ordinal),
            "The journal presents an interpretation without its current status: " + entryId + " / " + status);
        var output = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
        if (!string.IsNullOrEmpty(output) && entryId == Knowledge("hypothesis_voice_explained_as_echo"))
        {
            var stage = status == "Версия" ? "proposed" : "revised";
            var path = System.IO.Path.Combine(output, "len01_hypothesis_echo_" + stage + ".png");
            Require(!System.IO.File.Exists(path), "Journal status capture would replace an existing proof.");
            System.IO.Directory.CreateDirectory(output);
            RenderingServer.ForceDraw(false);
            using var shot = GetViewport().GetTexture().GetImage();
            Require(!shot.IsEmpty() && shot.SavePng(path) == Error.Ok, "Journal status capture failed.");
        }
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
    }

    private async Task CompareInJournal(string action, string[] sources)
    {
        var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        journal.Open(_bridge);
        await Frames(3);
        journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 1;
        for (var slot = 0; slot < 2; slot++)
        {
            var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot + 1}/Source");
            var index = Enumerable.Range(1, picker.ItemCount - 1)
                .FirstOrDefault(index => picker.GetItemMetadata(index).AsString() == sources[slot], -1);
            Require(index > 0, "The comparison source is absent from the actual picker: " + sources[slot]);
            picker.Select(index);
            picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
        }
        await Frames(3);
        var button = journal.GetNode<VBoxContainer>("Screen/Book/Layout/Comparisons/Layout/Hypotheses")
            .GetChildren().OfType<Button>().SingleOrDefault(button => !button.Disabled
                && button.Text == _bridge.ResolveText(Prefix + "text/" + action));
        Require(button is not null, "The actual journal UI cannot perform " + action);
        button!.EmitSignal(Button.SignalName.Pressed);
        var firstPicker = journal.GetNode<OptionButton>("Screen/Book/Layout/Comparisons/Layout/Source1/Source");
        for (var frame = 0; frame < 180 && firstPicker.Disabled; frame++) await Frames(1);
        Require(!firstPicker.Disabled, "The comparison did not finish its transaction: " + action);
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Require(!_player.ModalOpen, "The journal retained input after a source comparison.");
    }

    private async Task TalkToTimurTarget()
    {
        var target = _demo.DemoMain.ConnectedWorld!.FindChildren("*", "", true, false)
            .OfType<InteractionTarget>().SingleOrDefault(candidate => candidate.InteractionId == Interaction("route-to-mosque"));
        Require(target?.IsAvailable() == true, "Timur's actual world target is unavailable for the read source.");
        target!.Interact();
        for (var frame = 0; frame < 180 && !_dialogue.IsOpen; frame++) await Frames(1);
        await Frames(3);
        Require(_dialogue.IsOpen && _player.ModalOpen, "Timur's actual target failed to open the conversation.");
    }

    private async Task VerifyShuraleReply()
    {
        Require(_dialogue.IsOpen && HasChoice("choice-timur-shurale"), "Timur lost the read name on a conversation return.");
        var before = _bridge.SelectRuntimeState();
        var wordBefore = Vocabulary("tt_shurale");
        Require(await _bridge.EnterDialogueNodeAsync(Prefix + "dialogue/timur_restraint", "shurale-reply")
            && Vocabulary("tt_shurale") == wordBefore,
            "Entering a reply directly confirmed the name without an explicit question.");
        await Choose("choice-timur-shurale");
        var expected = _bridge.ResolveText(Prefix + "text/dialogue-timur-shurale-reply");
        var line = _dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line");
        for (var frame = 0; frame < 180 && line.Text != expected; frame++) await Frames(1);
        Require(Vocabulary("tt_shurale") == "confirmed", "The explicit name question failed to confirm its cautious meaning.");
        Require(_dialogue.IsOpen && line.IsVisibleInTree() && line.Text == expected
            && _dialogue.GetNode<Button>("Screen/Panel/Layout/Continue").Visible,
            "The name question's committed vocabulary hid its authored terminal reply: " + line.Text);
        var after = _bridge.SelectRuntimeState();
        foreach (var field in new[] { "knowledge", "journal", "pressure", "beats" })
            Require(before.GetProperty(field).GetRawText() == after.GetProperty(field).GetRawText(),
                "The folklore name question changed unrelated investigation state: " + field);
    }

    private async Task Talk(string interaction, string dialogue)
    {
        Require(Available(interaction), "Conversation is unavailable: " + interaction);
        _bridge.OpenDialogueUi(Prefix + "dialogue/" + dialogue);
        await Frames(5);
        Require(_dialogue.IsOpen && _player.ModalOpen, "Conversation UI failed to open: " + dialogue);
    }

    private bool HasChoice(string text) => _dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
        .GetChildren().OfType<Button>().Any(button => !button.Disabled && button.Text == _bridge.ResolveText(Prefix + "text/" + text));

    private async Task Choose(string text)
    {
        var choice = _dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().SingleOrDefault(button => !button.Disabled
                && button.Text == _bridge.ResolveText(Prefix + "text/" + text));
        Require(choice is not null, "Missing dialogue choice: " + text);
        choice!.EmitSignal(Button.SignalName.Pressed);
        await Frames(5);
    }

    private async Task CloseTalk()
    {
        _dialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(3);
        Require(!_dialogue.IsOpen && !_player.ModalOpen, "Closing dialogue did not release input.");
    }

    private async Task OpenPc()
    {
        Require(Available("oldpc-power"), "The physical computer is not available.");
        _bridge.OpenOldPcUi();
        await Frames(4);
        Require(_pc.GetNode<Control>("Screen").Visible && _player.ModalOpen, "Archive UI failed to open.");
    }

    private async Task Search(string query)
    {
        var field = _pc.GetNode<LineEdit>("Screen/Computer/Layout/SearchRow/Query");
        field.Text = query;
        field.EmitSignal(LineEdit.SignalName.TextSubmitted, query);
        await Frames(6);
    }

    private ItemList Results => _pc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
    private int SelectableResults() => Enumerable.Range(0, Results.ItemCount).Count(Results.IsItemSelectable);
    private bool HasDocument(string id) => Enumerable.Range(0, Results.ItemCount)
        .Any(index => Results.IsItemSelectable(index) && Results.GetItemMetadata(index).AsString() == id);

    private async Task ReadFromPc(string id)
    {
        var index = Enumerable.Range(0, Results.ItemCount)
            .FirstOrDefault(index => Results.IsItemSelectable(index) && Results.GetItemMetadata(index).AsString() == id, -1);
        Require(index >= 0, "The archive search cannot open " + id);
        Results.EmitSignal(ItemList.SignalName.ItemSelected, index);
        await Frames(6);
        Require(_pc.ActiveDocumentId == id, "The archive opened the wrong document: " + id);
    }

    private async Task ClosePc()
    {
        _pc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Require(!_pc.GetNode<Control>("Screen").Visible && !_player.ModalOpen, "Closing the archive did not release input.");
    }

    private async Task Move(string interaction, string zone, string spawn)
    {
        var world = _demo.DemoMain.ConnectedWorld
            ?? throw new InvalidOperationException("The LEN01 transition has no connected world.");
        Require(world.TryGetWorldSpawn(zone, spawn, out var destination),
            "The test requested an unmapped destination before dispatch: " + zone + "@" + spawn);
        Require(await _bridge.DispatchInteractionAsync(Interaction(interaction)), "Transition rejected: " + interaction);
        _demo.DemoMain.SwitchZone(zone, spawn);
        await Frames(5);
        Require(_bridge.CurrentZoneId == zone && _bridge.CurrentSpawnPointId == spawn
            && world.ActiveZoneId == zone && _player.GlobalPosition.DistanceTo(destination.Position) < 1f,
            "The transition did not place the player in its actual destination: " + zone + "@" + spawn);
    }

    private async Task Revisit(string targetName)
    {
        var target = _demo.DemoMain.ConnectedWorld?.FindChild(targetName, true, false) as InteractionTarget;
        Require(target?.PresentationRepeatAvailable?.Invoke() == true, "Physical revisit is unavailable: " + targetName);
        target!.PresentationRepeat!.Invoke();
        await Frames(6);
    }

    private async Task RoundTrip(string slot)
    {
        Require(!_player.ModalOpen, "Save scenario accidentally retained a modal.");
        var zone = _bridge.CurrentZoneId;
        var spawn = _bridge.CurrentSpawnPointId;
        var scene = _bridge.ActiveSceneId;
        Require(await _bridge.SaveSlotAsync(slot), "Save failed: " + slot);
        Require(await _bridge.LoadSlotAsync(slot), "Load failed: " + slot);
        await Frames(6);
        Require(_bridge.CurrentZoneId == zone && _bridge.CurrentSpawnPointId == spawn
            && _bridge.ActiveSceneId == scene && _demo.DemoMain.ConnectedWorld?.ActiveZoneId == zone,
            "Save/load restored a different logical scene or physical zone: " + slot);
    }

    private bool Available(string id) => _bridge.IsInteractionAvailable(Interaction(id));
    private bool Known(string id) => Status(id) == "confirmed";
    private string? Status(string id) => _bridge.SelectRuntimeState().GetProperty("knowledge")
        .GetProperty(Knowledge(id)).GetProperty("status").GetString();
    private string? Vocabulary(string id) => _bridge.SelectRuntimeState().GetProperty("vocabulary")
        .GetProperty(Prefix + "vocabulary/" + id).GetProperty("status").GetString();
    private string? Beat(string id)
    {
        // Like ContentRuleEngine.BeatStateEquals, an untouched beat has no
        // record. Reading it must not create a completion or crash the test.
        var beats = _bridge.SelectRuntimeState().GetProperty("beats");
        if (!beats.TryGetProperty(Prefix + "beat/" + id, out var beat)) return null;
        return beat.ValueKind == JsonValueKind.String ? beat.GetString() : beat.GetProperty("state").GetString();
    }
    private bool Npc(string id, string key) => _bridge.SelectRuntimeState().GetProperty("npc").TryGetProperty(Prefix + "character/" + id, out var npc)
        && npc.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    private static string Knowledge(string id) => Prefix + "knowledge/" + id;
    private static string Interaction(string id) => Prefix + "interaction/" + id;
    private static JsonElement Input(string type, string documentId) => JsonSerializer.SerializeToElement(new { type, documentId });
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}

internal static class Act1FamilyMealProof
{
    private const string Prefix = "urman.chapter1:";
    private const string DialogueId = Prefix + "dialogue/gulsina_yaramyy";
    private const string MealBeat = Prefix + "beat/house-warmth-and-pause";

    // Exercises the existing table conversation. The physical walkthrough may
    // provide its mapped-interaction callback; this helper never moves the player.
    internal static async Task<bool> CompleteAsync(Node owner, RuntimeBridge bridge,
        bool verifyReturn = false, string topic = "photo",
        Func<string, Task<bool>>? interactPhysically = null, bool verifyExteriorReturn = false)
    {
        try
        {
            var tree = owner.GetTree();
            var dialogue = tree.GetFirstNodeInGroup("dialogue_ui") as DialogueUi
                ?? throw new InvalidOperationException("Home pause has no dialogue UI.");
            var player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController
                ?? throw new InvalidOperationException("Home pause has no player.");
            var scene = bridge.ActiveSceneId;
            Check(topic is "photo" or "help" or "silent", "Unknown home conversation topic.");
            if (Completed()) return true; // Existing saved completed beats remain valid.
            Check(bridge.CurrentZoneId == "house_old_pc", "Home pause started outside the actual house.");
            var familyBefore = KnowledgeStatus("clue_family_avoids_marat");

            if (verifyReturn)
            {
                Check(!await bridge.ChooseDialogueAsync(DialogueId, "tea-photo-reply", "accept-meal"),
                    "A direct meal choice bypassed the invitation and human reply.");
                Check(!await bridge.ChooseDialogueAsync(DialogueId, "stay-for-tea-reply", "tea-" + topic),
                    "A direct personal answer bypassed the invitation.");
                await TerminalHasNoOutcome("tea-finished-reply");
            }

            await Talk();
            if (HasChoice("choice-gulsina-stay-for-tea"))
                await Choose("choice-gulsina-stay-for-tea");
            Check(!Completed() && !Known("family_home_pause"),
                "The invitation automatically supplied the completed home pause.");

            if (verifyReturn)
            {
                await Choose("choice-gulsina-tea-later");
                Check(!dialogue.IsOpen && !player.ModalOpen && !Completed(),
                    "Declining the meal retained input or completed it.");
                await RoundTrip("len01-meal-deferred");
                await VerifyNotebookAsync(owner, bridge, pending: true, captureName: "family_home_deferred_inside_" + topic);
                if (verifyExteriorReturn)
                {
                    await Revisit("HouseExit", "village_day");
                    await VerifyNotebookAsync(owner, bridge, pending: true, captureName: "family_home_deferred_outside_" + topic);
                    await RoundTripOutside("len01-meal-deferred-outside");
                    await VerifyNotebookAsync(owner, bridge, pending: true);
                    await Revisit("ReturnToHouseRegister", "house_old_pc");
                }
                await Talk();
                Check(HasChoice("choice-gulsina-stay-for-tea"),
                    "Returning after refusal did not restore the invitation.");
                await Choose("choice-gulsina-stay-for-tea");
                await Close();
                await RoundTrip("len01-meal-invitation-abandoned");
                await Talk();
                Check(HasChoice("choice-gulsina-tea-" + topic) && !Completed(),
                    "Loading an interrupted invitation lost its actual question or completed it.");
                await TerminalHasNoOutcome("tea-" + topic + "-reply");
            }

            if (HasChoice("choice-gulsina-tea-" + topic))
                await Choose("choice-gulsina-tea-" + topic);
            Check(HasChoice("choice-gulsina-accept-meal") && !Completed()
                && dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
                    == bridge.ResolveText(Prefix + "text/dialogue-gulsina-tea-" + topic),
                "The chosen personal topic omitted its actual reply or completed the meal.");
            if (topic == "photo")
                Check(Known("memory_gulsina_childhood_table"),
                    "Showing the found photograph omitted Gulsina's distinct recollection.");

            if (verifyReturn)
            {
                await Close();
                await RoundTrip("len01-meal-reply-unfinished");
                Check(!Completed() && !Known("family_home_pause"),
                    "Saving an answered topic substituted for accepting the meal.");
                await VerifyNotebookAsync(owner, bridge, pending: true);
                await Talk();
                Check(HasChoice("choice-gulsina-accept-meal")
                    && dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
                        == bridge.ResolveText(Prefix + "text/dialogue-gulsina-tea-" + topic),
                    "The returning conversation forgot which personal source had been discussed.");
                await TerminalHasNoOutcome("tea-finished-reply");
            }

            await Choose("choice-gulsina-accept-meal");
            Check(Completed() && Known("family_home_pause")
                && dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
                    == bridge.ResolveText(Prefix + "text/dialogue-gulsina-tea-finished"),
                "The explicit acceptance did not preserve the visible family response.");
            await Close();
            Check(KnowledgeStatus("clue_family_avoids_marat") == familyBefore
                && bridge.ActiveSceneId == scene && !Known("clue_do_not_answer_rule"),
                "The meal invented a reply about Marat, changed the investigation scene or revealed the finale.");
            if (verifyReturn)
            {
                await RoundTrip("len01-meal-completed");
                await VerifyNotebookAsync(owner, bridge, pending: false, captureName: "family_home_completed_" + topic);
                await Talk();
                Check(!HasChoice("choice-gulsina-stay-for-tea") && HasChoice("choice-gulsina-tea-again"),
                    "A completed home pause restarted or prevented a natural return.");
                await Choose("choice-gulsina-tea-again");
                await Close();
            }
            foreach (var id in topic == "photo"
                         ? new[] { "family_home_pause", "memory_gulsina_childhood_table" }
                         : new[] { "family_home_pause" })
                Check(bridge.JournalEntries().Count(entry => entry.EntryId == Prefix + "knowledge/" + id) == 1,
                    "The family conversation lost or duplicated its actual memory: " + id);
            GD.Print("family-meal-proof: actual invitation, source-dependent personal reply, explicit meal acceptance;"
                + " topic=" + topic + "; decline/return/load=" + verifyReturn
                + "; no timer, movement automation or human-duration claim");
            return true;

            async Task Talk()
            {
                if (dialogue.IsOpen) return;
                if (interactPhysically is not null)
                    Check(await interactPhysically("talk-gulsina"), "The physical route could not approach Gulsina.");
                else
                {
                    var main = tree.GetFirstNodeInGroup("zone_manager") as Main
                        ?? throw new InvalidOperationException("Home pause has no existing world.");
                    var target = main.FindChildren("*", "", true, false).OfType<InteractionTarget>()
                        .SingleOrDefault(item => item.InteractionId == Prefix + "interaction/talk-gulsina");
                    Check(target is not null && target.IsAvailable() && target.DialogueId == DialogueId,
                        "The actual Gulsina target is unavailable or bound to another dialogue.");
                    target!.Interact();
                }
                for (var frame = 0; frame < 180 && !dialogue.IsOpen; frame++) await Frames(1);
                await Frames(3);
                Check(dialogue.IsOpen && player.ModalOpen, "The actual home conversation did not open.");
            }
            bool HasChoice(string id) => Buttons().Any(button => button.Text == bridge.ResolveText(Prefix + "text/" + id));
            System.Collections.Generic.IEnumerable<Button> Buttons() =>
                dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren().OfType<Button>().Where(button => !button.Disabled);
            async Task Choose(string id)
            {
                var button = Buttons().SingleOrDefault(candidate => candidate.Text == bridge.ResolveText(Prefix + "text/" + id));
                Check(button is not null, "Missing visible home conversation choice: " + id);
                button!.EmitSignal(Button.SignalName.Pressed);
                await Frames(5);
            }
            async Task Close()
            {
                if (dialogue.IsOpen)
                    dialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
                await Frames(3);
                Check(!dialogue.IsOpen && !player.ModalOpen, "The home conversation retained input.");
            }
            async Task TerminalHasNoOutcome(string id)
            {
                var before = bridge.SelectRuntimeState();
                Check(await bridge.EnterDialogueNodeAsync(DialogueId, id), "Personal source prerequisites missing: " + id);
                var after = bridge.SelectRuntimeState();
                foreach (var key in new[] { "knowledge", "journal", "beats" })
                    Check(before.GetProperty(key).GetRawText() == after.GetProperty(key).GetRawText(),
                        "Entering a reply without choosing its question changed " + key + ": " + id);
            }
            async Task RoundTrip(string slot)
            {
                Check(!player.ModalOpen && await bridge.SaveSlotAsync(slot) && await bridge.LoadSlotAsync(slot),
                    "Home conversation save/load failed: " + slot);
                await Frames(6);
                Check(bridge.ActiveSceneId == scene && bridge.CurrentZoneId == "house_old_pc",
                    "Loading the home conversation changed the reached scene or location.");
            }
            async Task Revisit(string name, string zone)
            {
                var main = tree.GetFirstNodeInGroup("zone_manager") as Main
                    ?? throw new InvalidOperationException("The family return has no connected world.");
                var target = main.FindChildren("*", "", true, false).OfType<InteractionTarget>()
                    .SingleOrDefault(item => item.Name == name);
                Check(target?.IsAvailable() == true, "The earned physical return is unavailable: " + name);
                target!.Interact();
                await Frames(6);
                Check(bridge.CurrentZoneId == zone && bridge.ActiveSceneId == scene && !Completed(),
                    "A physical family return changed the investigation or silently finished the meal: " + name);
            }
            async Task RoundTripOutside(string slot)
            {
                Check(!player.ModalOpen && await bridge.SaveSlotAsync(slot) && await bridge.LoadSlotAsync(slot),
                    "Saving the deferred family visit outside failed.");
                await Frames(6);
                Check(bridge.CurrentZoneId == "village_day" && bridge.ActiveSceneId == scene && !Completed(),
                    "Loading the outside return forgot the unfinished family conversation.");
            }
            bool Completed() => bridge.SelectRuntimeState().GetProperty("beats").TryGetProperty(MealBeat, out var item)
                && (item.ValueKind == JsonValueKind.String ? item.GetString() == "completed"
                    : item.TryGetProperty("state", out var state) && state.GetString() == "completed");
            string? KnowledgeStatus(string id) => bridge.SelectRuntimeState().GetProperty("knowledge")
                .GetProperty(Prefix + "knowledge/" + id).GetProperty("status").GetString();
            bool Known(string id) => KnowledgeStatus(id) == "confirmed";
            async Task Frames(int count)
            {
                for (var frame = 0; frame < count; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }
        catch (Exception exception)
        {
            GD.PushError("family-meal-proof: " + exception);
            owner.GetTree().Quit(1);
            return false;
        }
    }

    internal static async Task VerifyNotebookAsync(Node owner, RuntimeBridge bridge, bool pending, string? captureName = null)
    {
        var tree = owner.GetTree();
        var journal = tree.GetFirstNodeInGroup("journal_ui") as JournalUi
            ?? throw new InvalidOperationException("Family reminder has no existing notebook.");
        var player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController
            ?? throw new InvalidOperationException("Family reminder has no player.");
        Check(!player.ModalOpen, "A different modal obstructed the family reminder check.");
        journal.Open(bridge);
        journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 2;
        for (var frame = 0; frame < 3; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var label = journal.GetNode<Label>("Screen/Book/Layout/Overview/Contents/Objective");
        var inside = bridge.ResolveText(Prefix + "text/objective-family-home-inside");
        var outside = bridge.ResolveText(Prefix + "text/objective-family-home-outside");
        Check(label.IsVisibleInTree() && (pending
                ? label.Text.Contains(bridge.CurrentZoneId == "house_old_pc" ? inside : outside, StringComparison.Ordinal)
                : !label.Text.Contains(inside, StringComparison.Ordinal) && !label.Text.Contains(outside, StringComparison.Ordinal)),
            "The actual notebook lost, leaked or retained the family reminder: pending=" + pending + "; zone=" + bridge.CurrentZoneId);
        if (pending)
            Check(bridge.JournalEntries().Count(entry => entry.EntryId == Prefix + "knowledge/family_home_invitation") == 1
                && !label.Text.Contains("{address:", StringComparison.Ordinal),
                "The family invitation was missing, duplicated or displayed an unresolved address.");
        var output = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
        if (captureName is not null && !string.IsNullOrEmpty(output))
        {
            Check(System.IO.Path.IsPathFullyQualified(output), "Family notebook captures require an absolute evidence directory.");
            var path = System.IO.Path.Combine(output, captureName + ".png");
            Check(!System.IO.File.Exists(path), "A family notebook capture would replace historical evidence.");
            System.IO.Directory.CreateDirectory(output);
            await Act1StateFlowProof.WaitForRenderedFrameAsync(owner, captureName);
            using var shot = owner.GetViewport().GetTexture().GetImage();
            Check(!shot.IsEmpty() && shot.SavePng(path) == Error.Ok, "The actual family notebook capture failed.");
            GD.Print("family-notebook-rendered-capture: " + path);
        }
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        for (var frame = 0; frame < 3; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Check(!player.ModalOpen, "Closing the family notebook reminder retained player input.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal static class Act1ArrivalFlowProof
{
    private const string Prefix = "urman.chapter1:";

    // Uses production document/choice owners from an ordinary New Game. This is
    // state and presentation coverage; walking and noticing remain separate checks.
    internal static async Task CompleteAsync(Node owner, RuntimeBridge bridge,
        string reply = "help-babai", bool verifyReturn = false)
    {
        var tree = owner.GetTree();
        var player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController
            ?? throw new InvalidOperationException("Arrival proof has no player.");
        var documents = tree.GetFirstNodeInGroup("document_ui") as DocumentUi
            ?? throw new InvalidOperationException("Arrival proof has no document reader.");
        var dialogue = tree.GetFirstNodeInGroup("dialogue_ui") as DialogueUi
            ?? throw new InvalidOperationException("Arrival proof has no dialogue reader.");
        const string replyDialogue = Prefix + "dialogue/arrival_mother_reply";
        var selected = reply == "keep-silent" ? "arrival_reply_kept_silent" : "arrival_reply_help_babai";
        var other = reply == "keep-silent" ? "arrival_reply_help_babai" : "arrival_reply_kept_silent";
        Check(reply is "help-babai" or "keep-silent", "Unknown arrival reply.");
        Check(!Known("arrival_mother_message_read") && !Known("memory_marat_childhood_photo")
            && !Known("memory_marat_kazansky_ne_otstavay") && !Known(selected) && !Known(other),
            "New Game fabricated or retained the personal arrival sources.");
        Check(!bridge.IsInteractionAvailable(Interaction("arrival-enter-house"))
            && !await bridge.DispatchInteractionAsync(Interaction("arrival-enter-house"))
            && !await bridge.ChooseDialogueAsync(replyDialogue, "reply", reply),
            "The ordinary house transition or a reply bypassed the unread personal sources.");

        var photoFirst = reply == "keep-silent";
        if (photoFirst)
        {
            await Read("view-arrival-photo", "arrival-photo-evidence");
            Check(Known("memory_marat_childhood_photo") && !Known("arrival_mother_message_read")
                && !bridge.IsInteractionAvailable(Interaction("arrival-answer-mother"))
                && !await bridge.ChooseDialogueAsync(replyDialogue, "reply", reply),
                "Reading the photo first fabricated the message or allowed a premature reply.");
        }
        await Read("view-arrival-message", "arrival-mother-message");
        Check(Known("arrival_mother_message_read")
            && (photoFirst || (!Known("memory_marat_childhood_photo")
                && !bridge.IsInteractionAvailable(Interaction("arrival-answer-mother"))
                && !await bridge.ChooseDialogueAsync(replyDialogue, "reply", reply))),
            "Reading the message fabricated the photo memory or allowed a premature reply.");
        if (verifyReturn && !photoFirst)
        {
            await RoundTrip("len01-arrival-message-only");
            Check(Known("arrival_mother_message_read") && !Known("memory_marat_childhood_photo")
                && !bridge.IsInteractionAvailable(Interaction("arrival-enter-house")),
                "Loading after the message completed an unread photograph or the arrival.");
        }

        if (!photoFirst) await Read("view-arrival-photo", "arrival-photo-evidence");
        Check(Known("memory_marat_childhood_photo") && Known("memory_marat_kazansky_ne_otstavay")
            && !Known("clue_marat_official_death_version") && !Known("clue_do_not_answer_rule"),
            "The photograph failed to establish the personal phrase or supplied later evidence.");
        Check(bridge.IsInteractionAvailable(Interaction("arrival-answer-mother"))
            && !bridge.IsInteractionAvailable(Interaction("arrival-enter-house")),
            "The two read sources did not expose the reply before the door.");
        if (verifyReturn)
        {
            await Read("view-arrival-photo", "arrival-photo-evidence");
            await DocumentImageUiProof.VerifyPaperAndJournalAsync(owner, bridge,
                Prefix + "document/arrival-photo-evidence", "len01_arrival_photo");
            await RoundTrip("len01-arrival-before-reply");
        }

        await OpenReply();
        Check(!Known(selected) && !Known(other)
            && !bridge.IsInteractionAvailable(Interaction("arrival-enter-house")),
            "Opening the reply dialogue silently selected an answer.");
        if (verifyReturn)
        {
            await CloseDialogue();
            await RoundTrip("len01-arrival-reply-abandoned");
            Check(!Known(selected) && !Known(other)
                && bridge.IsInteractionAvailable(Interaction("arrival-answer-mother"))
                && !bridge.IsInteractionAvailable(Interaction("arrival-enter-house")),
                "Abandoning and loading the reply either locked the return or supplied an answer.");
            await OpenReply();
        }
        var choiceText = bridge.ResolveText(Prefix + "text/arrival-reply-" + reply);
        var choices = dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().Where(button => !button.Disabled).ToArray();
        Check(choices.Length == 2, "The personal reply did not offer the two authored responses.");
        var button = choices.SingleOrDefault(candidate => candidate.Text == choiceText);
        Check(button is not null, "The personal reply is absent from the visible UI: " + reply);
        button!.EmitSignal(Button.SignalName.Pressed);
        for (var frame = 0; frame < 180 && !Known(selected); frame++) await Frames(1);
        await Frames(3);
        Check(Known(selected) && !Known(other), "The selected personal response did not persist distinctly.");
        var replyLine = dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text;
        var homeAddress = bridge.ResolveWorldText("{address:ADR-BABAI}");
        Check(replyLine.Contains("Пора найти дом", StringComparison.Ordinal)
            && !homeAddress.Contains("{address:", StringComparison.Ordinal)
            && replyLine.Contains(homeAddress, StringComparison.Ordinal),
            "The response did not name the same home address as the registry before independent navigation.");
        await CloseDialogue();
        Check(bridge.IsInteractionAvailable(Interaction("arrival-enter-house"))
            && !bridge.IsInteractionAvailable(Interaction("arrival-answer-mother"))
            && !await bridge.ChooseDialogueAsync(replyDialogue, "reply", reply),
            "The completed arrival failed to open the door or allowed a second response.");
        Check(bridge.JournalEntries().Count(entry => entry.EntryId == Prefix + "document/arrival-mother-message") == 1
            && bridge.JournalEntries().Count(entry => entry.EntryId == Prefix + "knowledge/memory_marat_childhood_photo") == 1
            && bridge.JournalEntries().Count(entry => entry.EntryId == Prefix + "knowledge/" + selected) == 1,
            "Rereading or replying duplicated a personal source or response.");
        if (verifyReturn)
        {
            await RoundTrip("len01-arrival-completed");
            Check(Known(selected) && !Known(other)
                && bridge.IsInteractionAvailable(Interaction("arrival-enter-house")),
                "A completed personal response was lost on load.");
        }
        Check(!player.ModalOpen, "Arrival source/response readers retained player input.");
        GD.Print("arrival-flow: source UI, remembered phrase, distinct " + reply + ", explicit house continuation");

        async Task Read(string action, string document)
        {
            var target = Target(action);
            Check(target.IsAvailable() && target.DocumentId == Prefix + "document/" + document,
                "The physical arrival source is unavailable or bound to the wrong document: " + action);
            target.Interact();
            for (var frame = 0; frame < 180 && !documents.IsOpen; frame++) await Frames(1);
            Check(documents.IsOpen && documents.OpenDocumentId == Prefix + "document/" + document && player.ModalOpen,
                "The arrival source did not open its actual reader: " + action);
            var body = documents.GetNode<RichTextLabel>("Screen/Document/Layout/Reader/Body").Text;
            Check(body.Contains(document == "arrival-photo-evidence" ? "Казанский, не отставай" : "Ты уже доехал"),
                "The source UI omitted its personal content: " + document);
            documents._UnhandledInput(Escape());
            await Frames(3);
            Check(!documents.IsOpen && !player.ModalOpen, "Closing an arrival source did not release input.");
        }

        async Task OpenReply()
        {
            var target = Target("arrival-answer-mother");
            Check(target.IsAvailable() && target.DialogueId == replyDialogue,
                "The phone did not expose the actual reply after both read sources.");
            target.Interact();
            for (var frame = 0; frame < 180 && !dialogue.IsOpen; frame++) await Frames(1);
            await Frames(3);
            Check(dialogue.IsOpen && player.ModalOpen, "The actual arrival reply did not open.");
        }

        async Task CloseDialogue()
        {
            dialogue._UnhandledInput(Escape());
            await Frames(3);
            Check(!dialogue.IsOpen && !player.ModalOpen, "Closing the arrival reply did not release input.");
        }

        async Task RoundTrip(string slot)
        {
            Check(!player.ModalOpen, "An arrival save retained a modal.");
            Check(await bridge.SaveSlotAsync(slot) && await bridge.LoadSlotAsync(slot),
                "Arrival save/load failed: " + slot);
            await Frames(6);
        }

        InteractionTarget Target(string action)
        {
            var main = tree.GetFirstNodeInGroup("zone_manager") as Main
                ?? throw new InvalidOperationException("Arrival proof has no world owner.");
            return Descendants(main).OfType<InteractionTarget>().SingleOrDefault(target => target.InteractionId == Interaction(action))
                ?? throw new InvalidOperationException("Arrival has no unique physical target: " + action);
        }

        bool Known(string localId) => bridge.SelectRuntimeState().GetProperty("knowledge")
            .GetProperty(Prefix + "knowledge/" + localId).GetProperty("status").GetString() == "confirmed";
        async Task Frames(int count)
        {
            for (var frame = 0; frame < count; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static System.Collections.Generic.IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static InputEventKey Escape() => new() { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true };
    private static string Interaction(string action) => Prefix + "interaction/" + action;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal static class Act1SourceReturnsProof
{
    private const string Prefix = "urman.chapter1:";
    private const string Note = "urman.oldpc:document/msg_mansur_unsent_note";
    private const string Fragment = "urman.oldpc:document/rec_internal_accounting_damaged";
    private const string Register = "urman.oldpc:document/rec_marat_case_register_conflict";
    private const string Sketch = "urman.oldpc:document/doc_kara_urman_edge_sketch";
    private const string Intent = "clue_mansur_allowed_pc_access_deliberately";
    private const string Accounting = "clue_village_has_internal_compensation_system";
    private const string RinatAnswer = "clue_rinat_accounting_question_heard";

    // Uses production targets and modal owners in the real house. It proves
    // authored source/choice persistence; physical traversal and interest remain
    // separate checks. Both sources keep their ordinary access conditions.
    internal static async Task<bool> CompleteAsync(Node owner, RuntimeBridge bridge,
        bool verifyReturn = false, bool accountingFirst = false, bool askIntentAgain = false,
        Func<string, Task<bool>>? interactPhysically = null)
    {
        try
        {
            var tree = owner.GetTree();
            var main = tree.GetFirstNodeInGroup("zone_manager") as Main
                ?? throw new InvalidOperationException("Source returns have no world owner.");
            var player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController
                ?? throw new InvalidOperationException("Source returns have no player.");
            var pc = tree.GetFirstNodeInGroup("old_pc_ui") as OldPcUi
                ?? throw new InvalidOperationException("Source returns have no archive UI.");
            var dialogue = tree.GetFirstNodeInGroup("dialogue_ui") as DialogueUi
                ?? throw new InvalidOperationException("Source returns have no dialogue UI.");
            var journal = tree.GetFirstNodeInGroup("journal_ui") as JournalUi
                ?? throw new InvalidOperationException("Source returns have no journal UI.");
            var scene = bridge.ActiveSceneId;
            var pressure = bridge.SelectRuntimeState().GetProperty("pressure").GetDouble();
            Check(bridge.CurrentZoneId == "house_old_pc" && !player.ModalOpen
                && Known("clue_internal_wording_reread") && Known("pressure_council_attention_1")
                && Status(Intent) == "hidden" && Status(Accounting) == "hidden" && !Known(RinatAnswer),
                "Source returns started outside the reached house investigation or with fabricated prior results.");
            Check(!bridge.IsOldPcDocumentAccessible(Sketch)
                && !await bridge.OpenDocumentAsync(Sketch)
                && !await bridge.DispatchInteractionAsync(Interaction("reread-to-edge-sketch"))
                && !await bridge.ChooseDialogueAsync(Prefix + "dialogue/mansur_pc_request", "draft-folder-question", "name-saved-folder")
                && !await bridge.ChooseDialogueAsync(Prefix + "dialogue/rinat_internal_register", "category-reply", "ask-compensation-records"),
                "Unread sources were bypassed through a document, scene transition or direct dialogue choice.");
            Check(ObjectiveActive("find-owner-draft") && ObjectiveActive("find-damaged-fragment"),
                "The reached reread did not expose both independent source searches in the live journal objectives.");
            Check(!MosqueAddressHeard()
                && !await bridge.ChooseDialogueAsync(Prefix + "dialogue/mansur_pc_request", "follow-up", "ask-mosque-address"),
                "Mansur's future directions were recorded before their actual conversation.");

            // A technical caller that previously used the raw document API still
            // obtains access through Mansur's existing household reply.
            if (!bridge.IsInteractionAvailable(Interaction("oldpc-power")))
            {
                await Talk("talk-mansur");
                await Choice("choice-mansur-why");
                await CloseDialogue();
            }
            Check(bridge.IsInteractionAvailable(Interaction("oldpc-power")), "Mansur's real access reply did not unlock the physical computer.");

            if (accountingFirst) { await AccountingReturn(); await MansurReturn(); }
            else { await MansurReturn(); await AccountingReturn(); }
            Check(Status(Intent) == "hypothesis" && Status(Accounting) == "hypothesis"
                && Known(RinatAnswer) && bridge.IsOldPcDocumentAccessible(Sketch)
                && bridge.IsInteractionAvailable(Interaction("reread-to-edge-sketch"))
                && bridge.ActiveSceneId == scene && !Known("clue_kara_urman_edge_is_rule_boundary")
                && !Known("clue_do_not_answer_rule") && !player.ModalOpen
                && bridge.SelectRuntimeState().GetProperty("pressure").GetDouble() == pressure,
                "Source returns skipped the unread map, changed the scene, supplied the final rule or repeated pressure.");
            Check(!new[] { "find-owner-draft", "question-owner-draft", "find-damaged-fragment",
                    "compare-damaged-fragment", "question-damaged-fragment" }.Any(ObjectiveActive),
                "The completed source returns retained an incomplete journal objective.");
            foreach (var entry in new[] { Note, Fragment, Knowledge(Intent), Knowledge(Accounting), Knowledge(RinatAnswer) })
                Check(bridge.JournalEntries().Count(candidate => candidate.EntryId == entry) == 1,
                    "Source returns lost or duplicated a journal entry: " + entry);
            if (verifyReturn)
            {
                await RoundTrip("len01-source-returns-completed");
                Check(Status(Intent) == "hypothesis" && Status(Accounting) == "hypothesis"
                    && Known(RinatAnswer) && bridge.IsOldPcDocumentAccessible(Sketch),
                    "Loading the completed returns changed hypotheses or hid the reached sketch.");
            }
            GD.Print("source-returns-proof: two actual archive sources, explicit Mansur reply, bounded accounting comparison,"
                + " actual Rinat question, hypothesis labels; order=" + (accountingFirst ? "accounting-first" : "note-first")
                + "; no repeated pressure or early rule; traversal, interest and duration remain separate");
            return true;

            async Task MansurReturn()
            {
                await ReadPc("Мансур", Note);
                Check(Known("clue_mansur_unsent_note_read"),
                    "The actual archive reader recorded Mansur's document but lost its authored read effect: "
                    + SourceState("clue_mansur_unsent_note_read", Intent));
                Check(Status(Intent) == "hidden", "Opening the unsent note silently interpreted Mansur's intent: "
                    + SourceState("clue_mansur_unsent_note_read", Intent));
                Check(!MosqueAddressHeard(), "Reading the note invented an unspoken address of the mosque.");
                Check(!bridge.IsOldPcDocumentAccessible(Sketch), "Opening the unsent note prematurely unlocked the sketch: "
                    + SourceState("clue_mansur_unsent_note_read", Intent));
                Check(!ObjectiveActive("find-owner-draft") && ObjectiveActive("question-owner-draft"),
                    "Reading Mansur's note did not replace its search objective with the actual human question.");
                if (verifyReturn)
                    foreach (var replyNode in new[] { "draft-named-reply", "draft-intent-reply" })
                        await TerminalHasNoOutcome("mansur_pc_request", replyNode);
                await Talk("talk-mansur");
                await Choice("choice-mansur-ask-draft");
                Check(dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text.Contains("Какую папку ты открыл", StringComparison.Ordinal)
                    && Status(Intent) == "hidden", "Mansur's authored folder question was omitted or auto-answered.");
                if (verifyReturn)
                {
                    await Choice("choice-mansur-draft-later");
                    await CloseDialogue();
                    await RoundTrip("len01-mansur-note-unanswered");
                    Check(Status(Intent) == "hidden" && !bridge.IsOldPcDocumentAccessible(Sketch),
                        "Abandoning the question or loading supplied Mansur's answer.");
                    Check(!MosqueAddressHeard(), "Abandoning Mansur's question silently recorded his later directions.");
                    await Talk("talk-mansur");
                    await Choice("choice-mansur-ask-draft");
                }
                await Choice(askIntentAgain ? "choice-mansur-ask-intent" : "choice-mansur-name-folder");
                var replyTextId = askIntentAgain ? "dialogue-mansur-draft-question-result" : "dialogue-mansur-draft-name-result";
                await AwaitReply(replyTextId);
                Check(Status(Intent) == "hypothesis", "Mansur's explicit answer did not leave the bounded inference: "
                    + SourceState("clue_mansur_unsent_note_read", Intent));
                CheckVisibleReply(replyTextId, "mansur_pc_request/" + (askIntentAgain ? "draft-intent-reply" : "draft-named-reply"),
                    SourceState("clue_mansur_unsent_note_read", Intent));
                CheckMosqueAddressHeardOnly();
                if (bridge.NotebookSettlement is { } settlement)
                {
                    var id = settlement.CanonicalAddressId("ADR-MOSQUE")!;
                    Check(dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
                        .Contains(settlement.FormatAddress(id), StringComparison.Ordinal),
                        "Mansur's spoken address does not resolve to the existing mosque registry entry.");
                }
                await CloseDialogue();
                await StatusLabel(Intent);
                Check(!await bridge.ChooseDialogueAsync(Prefix + "dialogue/mansur_pc_request", "draft-folder-question", "name-saved-folder"),
                    "Mansur's completed inference could be answered a second time.");
                if (!Known(RinatAnswer)) Check(!bridge.IsOldPcDocumentAccessible(Sketch),
                    "Mansur's note bypassed the pending damaged-source question.");
                if (verifyReturn)
                {
                    await RoundTrip("len01-mosque-address-heard");
                    CheckMosqueAddressHeardOnly();
                    await RepeatMosqueAddress();
                    await CheckLegacyMosqueAddressExtension();
                }
            }

            bool MosqueAddressHeard() => bridge.SelectRuntimeState().GetProperty("knowledge")
                .TryGetProperty(Knowledge("address-mosque"), out var address)
                && address.GetProperty("status").GetString() == "confirmed";

            void CheckMosqueAddressHeardOnly()
            {
                Check(MosqueAddressHeard()
                    && bridge.JournalEntries().Count(entry => entry.EntryId == Knowledge("address-mosque")) == 1,
                    "The actual directions lost or duplicated their single notebook source.");
                if (bridge.NotebookSettlement is not { } registry) return;
                var id = registry.CanonicalAddressId("ADR-MOSQUE")
                    ?? throw new InvalidOperationException("The existing mosque has no stable address alias.");
                Check(bridge.KnownAddressIds().Contains(id) && !bridge.LocatedAddressIds().Contains(id)
                    && !registry.MapForKnownAddresses(bridge.LocatedAddressIds()).Buildings.Any(building => building.AddressId == id),
                    "Hearing the mosque address automatically located its house or access on the map.");
            }

            async Task RepeatMosqueAddress()
            {
                await Talk("talk-mansur");
                await Choice("choice-mansur-mosque-address");
                await AwaitReply("dialogue-mansur-mosque-address");
                CheckMosqueAddressHeardOnly();
                await CloseDialogue();
            }

            async Task CheckLegacyMosqueAddressExtension()
            {
                // A declared compatibility projection of the reached snapshot:
                // older content had this same completed Mansur conversation but
                // neither of the new address records. No progress flag is added.
                var suffix = Guid.NewGuid().ToString("N");
                var originalSlot = "len01-mosque-extension-source-" + suffix;
                Check(await bridge.SaveSlotAsync(originalSlot), "The reached address extension source could not be saved.");
                var codec = new Urman.Core.Persistence.SaveGameV3Codec();
                var originalPath = ProjectSettings.GlobalizePath("user://savegames/" + originalSlot + ".savegame-v3.json");
                var original = codec.Decode(File.ReadAllBytes(originalPath));
                var state = System.Text.Json.Nodes.JsonNode.Parse(original.Runtime.State.GetRawText())!;
                ((System.Text.Json.Nodes.JsonObject)state["knowledge"]!).Remove(Knowledge("address-mosque"));
                var entries = (System.Text.Json.Nodes.JsonArray)state["journal"]!;
                for (var i = entries.Count - 1; i >= 0; i--)
                    if (entries[i]?["sourceId"]?.GetValue<string>() == Knowledge("address-mosque")) entries.RemoveAt(i);
                var priorContentState = original with { Runtime = original.Runtime with
                    { State = System.Text.Json.JsonSerializer.SerializeToElement(state) } };
                var compatibilitySlot = "len01-mosque-before-address-extension-" + suffix;
                var path = ProjectSettings.GlobalizePath("user://savegames/" + compatibilitySlot + ".savegame-v3.json");
                using (var file = new FileStream(path, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
                    file.Write(codec.Encode(priorContentState));
                Check(await bridge.LoadSlotAsync(compatibilitySlot), "The old address-record absence could not be loaded.");
                await Frames(6);
                Check(Status(Intent) == "hypothesis" && !MosqueAddressHeard()
                    && !bridge.JournalEntries().Any(entry => entry.EntryId == Knowledge("address-mosque")),
                    "Loading the pre-extension records lost the old conclusion or invented new spoken directions.");
                await TerminalHasNoOutcome("mansur_pc_request", "mosque-address-reply");
                Check(!MosqueAddressHeard(), "Opening the repeat reply granted the unasked address.");
                await RepeatMosqueAddress();
                await RoundTrip("len01-mosque-extension-restored");
                CheckMosqueAddressHeardOnly();
                GD.Print("source-returns-proof: mosque address heard only after actual Mansur choice; repeat/load retain one record; declared prior-extension record absence requires new manual question; no map location granted");
            }

            async Task AccountingReturn()
            {
                await ReadPc("компенсация", Fragment);
                Check(Known("clue_accounting_fragment_read"),
                    "The actual archive reader recorded the damaged document but lost its authored read effect: "
                    + SourceState("clue_accounting_fragment_read", Accounting));
                Check(Status(Accounting) == "hidden" && !Known(RinatAnswer),
                    "Opening damaged prose fabricated an interpretation or the human response: "
                    + SourceState("clue_accounting_fragment_read", Accounting));
                Check(!bridge.IsOldPcDocumentAccessible(Sketch), "Opening the damaged source prematurely unlocked the sketch: "
                    + SourceState("clue_accounting_fragment_read", Accounting));
                Check(!ObjectiveActive("find-damaged-fragment") && ObjectiveActive("compare-damaged-fragment"),
                    "Reading the damaged source did not expose its comparison in the live journal objectives.");
                Check(!await bridge.CompareJournalSourcesAsync(Interaction("compare-accounting-scope"),
                        new[] { Fragment, "urman.oldpc:document/doc_marat_official_death_notice" })
                    && !await bridge.ChooseDialogueAsync(Prefix + "dialogue/rinat_internal_register", "category-reply", "ask-compensation-records"),
                    "An unrelated pairing or a question without comparison supplied the result.");
                if (verifyReturn)
                {
                    await Compare("compare-accounting-same-case");
                    Check(Status(Accounting) == "hidden" && !Known(RinatAnswer),
                        "The unsupported identification of the damaged case was accepted.");
                    await RoundTrip("len01-damaged-case-unresolved");
                    Check(Status(Accounting) == "hidden" && !bridge.IsOldPcDocumentAccessible(Sketch),
                        "Loading an unresolved comparison supplied the correct inference.");
                }
                await Compare("compare-accounting-scope");
                Check(Status(Accounting) == "hypothesis" && !Known(RinatAnswer)
                    && !bridge.IsOldPcDocumentAccessible(Sketch),
                    "The comparison confirmed a world mechanism or silently answered Rinat's question.");
                Check(!ObjectiveActive("compare-damaged-fragment") && ObjectiveActive("question-damaged-fragment"),
                    "Comparing the damaged source did not expose the pending question to Rinat.");
                await StatusLabel(Accounting);
                if (verifyReturn) await RoundTrip("len01-accounting-question-pending");
                if (verifyReturn) await TerminalHasNoOutcome("rinat_internal_register", "accounting-question-reply");
                await Talk("internal-register-to-rinat");
                Check(!Known(RinatAnswer), "Opening Rinat's follow-up automatically asked about the fragment.");
                if (verifyReturn)
                {
                    await CloseDialogue();
                    await RoundTrip("len01-rinat-accounting-abandoned");
                    Check(!Known(RinatAnswer), "Abandoning Rinat's follow-up supplied his answer.");
                    await Talk("internal-register-to-rinat");
                }
                await Choice("choice-rinat-accounting");
                await AwaitReply("dialogue-rinat-accounting");
                Check(Known(RinatAnswer) && Status(Accounting) == "hypothesis",
                    "Rinat's explicit answer was not recorded or confirmed an unsupported mechanism: "
                    + SourceState("clue_accounting_fragment_read", Accounting));
                CheckVisibleReply("dialogue-rinat-accounting", "rinat_internal_register/accounting-question-reply",
                    SourceState("clue_accounting_fragment_read", Accounting));
                await CloseDialogue();
                Check(!await bridge.ChooseDialogueAsync(Prefix + "dialogue/rinat_internal_register", "category-reply", "ask-compensation-records")
                    && bridge.SelectRuntimeState().GetProperty("pressure").GetDouble() == pressure,
                    "Repeating the new question changed social pressure or repeated its answer.");
                if (Status(Intent) != "hypothesis") Check(!bridge.IsOldPcDocumentAccessible(Sketch),
                    "The accounting question bypassed Mansur's missing source episode.");
            }

            async Task TerminalHasNoOutcome(string dialogueId, string nodeId)
            {
                var before = bridge.SelectRuntimeState();
                var knowledgeBefore = before.GetProperty("knowledge").GetRawText();
                var journalBefore = before.GetProperty("journal").GetRawText();
                Check(await bridge.EnterDialogueNodeAsync(Prefix + "dialogue/" + dialogueId, nodeId),
                    "The read source did not satisfy its terminal reply's stable prerequisites: " + nodeId);
                var after = bridge.SelectRuntimeState();
                Check(after.GetProperty("knowledge").GetRawText() == knowledgeBefore
                    && after.GetProperty("journal").GetRawText() == journalBefore
                    && after.GetProperty("pressure").GetDouble() == pressure,
                    "Entering a terminal reply without choosing an answer granted progress: " + dialogueId + "/" + nodeId);
            }

            async Task AwaitReply(string textId)
            {
                var expectedLine = bridge.ResolveText(Prefix + "text/" + textId);
                for (var frame = 0; frame < 180
                    && dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text != expectedLine; frame++)
                    await Frames(1);
            }

            void CheckVisibleReply(string textId, string expectedNode, string state)
            {
                var line = dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line");
                var expectedLine = bridge.ResolveText(Prefix + "text/" + textId);
                Check(dialogue.IsOpen && line.IsVisibleInTree() && line.Text == expectedLine
                    && dialogue.GetNode<Button>("Screen/Panel/Layout/Continue").IsVisibleInTree(),
                    "The committed answer did not show its authored terminal reply: expectedNode=" + expectedNode
                    + "; expectedTextId=" + textId + "; open=" + dialogue.IsOpen + "; " + state
                    + "; actualLine=" + line.Text);
            }

            async Task ReadPc(string query, string id)
            {
                var knowledgeBeforeRead = bridge.SelectRuntimeState().GetProperty("knowledge").EnumerateObject()
                    .ToDictionary(entry => entry.Name, entry => entry.Value.GetProperty("status").GetString());
                var target = Target("oldpc-power");
                Check(target.IsAvailable(), "The actual house computer is unavailable.");
                if (interactPhysically is null) target.Interact();
                else Check(await interactPhysically("oldpc-power"),
                    "The physical route could not approach and use the computer through mapped input.");
                for (var frame = 0; frame < 180 && !pc.GetNode<Control>("Screen").Visible; frame++) await Frames(1);
                Check(pc.GetNode<Control>("Screen").Visible && player.ModalOpen, "The actual computer target did not open its reader.");
                var field = pc.GetNode<LineEdit>("Screen/Computer/Layout/SearchRow/Query");
                field.Text = query;
                field.EmitSignal(LineEdit.SignalName.TextSubmitted, query);
                var results = pc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
                var index = -1;
                for (var frame = 0; frame < 180 && index < 0; frame++)
                {
                    await Frames(1);
                    index = Enumerable.Range(0, results.ItemCount).FirstOrDefault(i =>
                        results.IsItemSelectable(i) && results.GetItemMetadata(i).AsString() == id, -1);
                }
                Check(index >= 0, "The ordinary archive search did not find the reachable source: " + id);
                results.EmitSignal(ItemList.SignalName.ItemSelected, index);
                for (var frame = 0; frame < 180 && pc.ActiveDocumentId != id; frame++) await Frames(1);
                await Frames(3);
                Check(pc.ActiveDocumentId == id && bridge.JournalEntries().Any(entry => entry.EntryId == id),
                    "The archive did not read and record its selected source: " + id);
                var readKnowledge = id == Note ? Knowledge("clue_mansur_unsent_note_read") : Knowledge("clue_accounting_fragment_read");
                var knowledgeAfterRead = bridge.SelectRuntimeState().GetProperty("knowledge");
                foreach (var entry in knowledgeBeforeRead.Where(entry => entry.Key != readKnowledge))
                    Check(knowledgeAfterRead.TryGetProperty(entry.Key, out var current)
                        && current.GetProperty("status").GetString() == entry.Value,
                        "Reading one archive source changed unrelated reached knowledge: " + id + " -> " + entry.Key);
                pc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
                await Frames(3);
                Check(!player.ModalOpen, "The archive retained player input.");
            }

            async Task Talk(string action)
            {
                var target = Target(action);
                Check(target.IsAvailable(), "The actual source-holder target is unavailable: " + action);
                if (interactPhysically is null) target.Interact();
                else Check(await interactPhysically(action),
                    "The physical route could not approach and speak through mapped input: " + action);
                for (var frame = 0; frame < 180 && !dialogue.IsOpen; frame++) await Frames(1);
                await Frames(3);
                Check(dialogue.IsOpen && player.ModalOpen, "The source-holder target did not open its dialogue: " + action);
            }
            async Task Choice(string textId)
            {
                var button = dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren()
                    .OfType<Button>().SingleOrDefault(candidate => !candidate.Disabled
                        && candidate.Text == bridge.ResolveText(Prefix + "text/" + textId));
                Check(button is not null, "The source-holder choice is missing from the actual UI: " + textId);
                button!.EmitSignal(Button.SignalName.Pressed);
                await Frames(5);
            }
            async Task CloseDialogue()
            {
                if (dialogue.IsOpen) dialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
                await Frames(3);
                Check(!dialogue.IsOpen && !player.ModalOpen, "Closing the source-holder dialogue retained input.");
            }
            async Task Compare(string action)
            {
                journal.Open(bridge);
                await Frames(3);
                journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 1;
                var sourceIds = new[] { Register, Fragment };
                for (var slot = 0; slot < 2; slot++)
                {
                    var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot + 1}/Source");
                    var index = Enumerable.Range(1, picker.ItemCount - 1).FirstOrDefault(i =>
                        picker.GetItemMetadata(i).AsString() == sourceIds[slot], -1);
                    Check(index > 0, "A read source is missing from the journal picker.");
                    picker.Select(index);
                    picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
                }
                await Frames(3);
                var button = journal.GetNode<VBoxContainer>("Screen/Book/Layout/Comparisons/Layout/Hypotheses")
                    .GetChildren().OfType<Button>().SingleOrDefault(candidate => !candidate.Disabled
                        && candidate.Text == bridge.ResolveText(Prefix + "text/" + action));
                Check(button is not null, "The actual journal comparison is unavailable: " + action);
                button!.EmitSignal(Button.SignalName.Pressed);
                var firstPicker = journal.GetNode<OptionButton>("Screen/Book/Layout/Comparisons/Layout/Source1/Source");
                for (var frame = 0; frame < 180 && firstPicker.Disabled; frame++) await Frames(1);
                Check(!firstPicker.Disabled, "The journal comparison did not finish its transaction.");
                journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
                await Frames(3);
                Check(!player.ModalOpen, "Closing a comparison retained input.");
            }
            async Task StatusLabel(string id)
            {
                journal.Open(bridge, Knowledge(id));
                await Frames(3);
                Check(journal.ActiveEntryId == Knowledge(id)
                    && journal.GetNode<Label>("Screen/Book/Layout/WorkArea/Reader/Source").Text.Contains("Версия", StringComparison.Ordinal),
                    "The journal displayed an uncertain interpretation as a confirmed fact.");
                journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
                await Frames(3);
            }
            async Task RoundTrip(string slot)
            {
                var zone = bridge.CurrentZoneId;
                var spawn = bridge.CurrentSpawnPointId;
                Check(!player.ModalOpen && await bridge.SaveSlotAsync(slot) && await bridge.LoadSlotAsync(slot),
                    "Source return save/load failed: " + slot);
                await Frames(6);
                Check(bridge.ActiveSceneId == scene && bridge.CurrentZoneId == zone && bridge.CurrentSpawnPointId == spawn
                    && (main.ConnectedWorld is null || main.ConnectedWorld.ActiveZoneId == zone),
                    "Source return load changed the reached scene or physical zone.");
            }
            InteractionTarget Target(string action) => Descendants(main).OfType<InteractionTarget>()
                .SingleOrDefault(target => target.InteractionId == Interaction(action))
                ?? throw new InvalidOperationException("The house has no unique physical target: " + action);
            string? Status(string id) => bridge.SelectRuntimeState().GetProperty("knowledge").GetProperty(Knowledge(id)).GetProperty("status").GetString();
            bool Known(string id) => Status(id) == "confirmed";
            bool ObjectiveActive(string id) => bridge.ActiveObjectives().Any(objective => objective.ObjectiveId == id);
            string SourceState(string readId, string interpretationId) =>
                "read=" + Status(readId) + "; interpretation=" + Status(interpretationId)
                + "; rinat-answer=" + Status(RinatAnswer)
                + "; sketch-access=" + bridge.IsOldPcDocumentAccessible(Sketch)
                + "; objectives=" + string.Join(",", bridge.ActiveObjectives().Select(objective => objective.ObjectiveId));
            async Task Frames(int count)
            {
                for (var frame = 0; frame < count; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }
        catch (Exception exception)
        {
            GD.PushError("source-returns-proof: " + exception);
            owner.GetTree().Quit(1);
            return false;
        }
    }

    private static System.Collections.Generic.IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static string Knowledge(string id) => Prefix + "knowledge/" + id;
    private static string Interaction(string id) => Prefix + "interaction/" + id;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
