using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    // Only proven state-compatible revisions may accept an exact one-way pair.
    // Unknown current fingerprints keep the store's default strict validation.
    private const string FullGameCampaignId = "urman.fullgame";
    private const string FullGameTextRevisionFingerprint =
        "0b1a4c3e32b374360f4aebda32d524362862b9f1c171b3f7ec69422f09a8cb1d";
    private const string FullGameTeaTextRevisionFingerprint =
        "6f47e78bc728d70ea36362c046d9f6c71bf76577c1db49a84f141e1cef3c451c";

    private const string FullGameCellarReturnFingerprint =
        "7ce8098560641ed5c6c3e292f58442823afe8593afd1f23bac4e193ea46e5d2f";
    private const string FullGameAllWorldBookReturnFingerprint =
        "0819edb45a8083bb69b5c01cf9923eee32de518d4ab81a3eff7e36c632a5222a";

    private const string FullGamePhysicalPrintMountFingerprint =
        "e7eb22741ae47532667aaabbb439879a9efb5ff95919716c90b4d89d93290dc5";
    private const string FullGameTamaraQuestFingerprint =
        "d172b0d0c7b25d88bce45b978e2b3352e230baee912491305c044e20b08418f2";
    private const string FullGamePhotoReturnTextFingerprint =
        "1249c9ed78a7ba4e08c8f7202f526e96c5f29e8d0b0ef56cb559ac82ef5cdce3";
    private const string FullGameEntryPromptTextFingerprint =
        "ed3f63c9550d08fe26a640ac4945bce8ae41a1d520604a57498d52a6719001ab";

    private const string FullGameNivaArrivalFingerprint =
        "d6365c9d89f50395993b7397046ff006cd5c5882bcb5f570187cef160752fae0";

    private const string FullGameArrivalSkipTextFingerprint =
        "616285e8447ea98c3ac0cdc06c6b79bc2234342952f4327f9f7b33c4e97f37c9";
    private const string FullGameShuraleReplyTextFingerprint =
        "b0b8a60bea20a44dc51dbdea24359a17a4819d37e5943f0b5c609ea07e5ef4b7";
    private const string FullGameWaterHomeAccountFingerprint =
        "b809690b74181a9813e5e54034ebc57388b65e8d4b64f546be187dbb680380e9";
    private const string FullGameNaturalDialogueFingerprint =
        "319fd5302df4c4d03f7b56ab97385a46871e2f75e47b8c5f889a205752788c1c";
    private const string FullGameForestPhotoComparisonFingerprint =
        "378d33f0f990a6b48495a81b936e2d1f81738b8f29a80e321c1e54c972c0b869";
    private const string FullGameForestEncounterDialogueFingerprint =
        "15ca5c10b2c8104f938cfefb3ea09281257b99ad8311d5b3646ae3208b66278a";
    private const string FullGamePhotographerReadingFingerprint =
        "bf01a8888a23b642a9c83f07ae0aa96c424b015f213c867b0288fae80c90d554";
    private const string FullGameCommonVisitGateFingerprint =
        "5b053ac4d7dbaf15c21df7cfa20bec960a3371e2afc8529c02f57fe570a6b49c";
    private const string FullGameAlsuMeetingGateFingerprint =
        "69cd30fe6a3bd2175deca4eb08910047df79ddac38637d9b05c6997984f156b0";
    private const string FullGameForestAccountFingerprint =
        "4f6637ef1dfa32ca3901ff203df239f2372438788a7a59741c147d50adbea3f8";
    private const string FullGameFamilyPurposeFingerprint =
        "eaf6dfe0f2e40408ad62d5a0c690b86abcd771136818f91fd5af4b923939a236";
    private const string FullGameBankPathLabelFingerprint =
        "9964e2bab600dc89687f59ce4d954eb4a20be0306fdc0ae29bfc83ecd34970a7";
    private const string FullGameForestDebriefTextFingerprint =
        "45252546936e55fc2ed3dd0a0f1acc932de81c2d6b3c3bdfe0562e0a443cbac0";
    private const string FullGameFamilyPhotoMysteryFingerprint =
        "cb879d82d9682d6fe6ecd197b502842932fc448ee463d28acc7a3d66ae541c9d";
    private const string FullGameRiverExitHintFingerprint =
        "2f926961623352380d14ea65967d819d3654678b193dbf071759b2dcee978e48";
    private const string FullGameAlsuMountedPhotoGateFingerprint =
        "e43f1b40b2789894b5ce349d10ebd8e50643e26f7584a53d812d355cc92f3dba";
    private const string FullGameHousePhotoDialogueFingerprint =
        "cdbeb28dd3f267a7b2e43248b7e040f46deb98dd40ff2d0609744ee6e3b84be3";
    private const string FullGameAlsuLeafDialogueFingerprint =
        "2e2b6ed2af43e5fede13491fa26e70c6f83f08d59cc9ea149d30eed44990860d";
    private const string FullGameTeaCompletionGateFingerprint =
        "b0896377883d7ca17d35d84a3e318021df9c870c092f83a249c7998642c67f54";
    private const string FullGameEarlyDialogueRevisionFingerprint =
        "8099af9f968add2f31e1a1f25bf814d6c3783e1b9fa3ea5abeaf12f802b6d3b2";

    private static readonly IReadOnlyCollection<SaveGameFingerprintCompatibility> FullGameTextRevisionCompatibility =
    [
        // 80266983 -> 5b0a7ccf: 18 UI texts/provides IDs only; proof in PW-025.
        new(
            "60fdd3bc0ef9c12c5c022e804b01c28e71fddf48bcf8490bc68303b593d7b7a2",
            FullGameTextRevisionFingerprint),
        // D01: three dialogue text values only; exact pack comparison in PW-043.
        new(
            "441a862cce6ad7c3fc9f8c566ba81ed03e44d32ae98b7298b7e589169b533257",
            FullGameTeaTextRevisionFingerprint),
        // W05 adds one optional early-return interaction, with no effects or saved schema changes.
        new(FullGameTeaTextRevisionFingerprint, FullGameCellarReturnFingerprint),
        // The prior accepted text-only predecessor remains readable without transitive matching.
        new("441a862cce6ad7c3fc9f8c566ba81ed03e44d32ae98b7298b7e589169b533257",
            FullGameCellarReturnFingerprint),
        // W02-W04 add optional early returns only; exact pack comparison in PW-023.
        new(FullGameCellarReturnFingerprint, FullGameAllWorldBookReturnFingerprint),
        new(FullGameTeaTextRevisionFingerprint, FullGameAllWorldBookReturnFingerprint),
        new("441a862cce6ad7c3fc9f8c566ba81ed03e44d32ae98b7298b7e589169b533257",
            FullGameAllWorldBookReturnFingerprint),
        // Only optional Tamara activation changed; prepareForLoad seeds its missing run.
        new(FullGamePhysicalPrintMountFingerprint, FullGameTamaraQuestFingerprint),
        // 447f1735 -> 04bdef92 only split print/mount; mounted legacy state stays valid (PW-079).
        new(FullGameAllWorldBookReturnFingerprint, FullGameTamaraQuestFingerprint),
        // Only the book-return label changed; the compiled state contract is identical.
        new("d10e86231a472351cbbcf5ffaa46f18820269037acecaf17678e6472c0b8e710",
            FullGamePhotoReturnTextFingerprint),
        // Two entry/inspection labels only; retain the accepted direct predecessor pairs.
        new(FullGamePhotoReturnTextFingerprint, FullGameEntryPromptTextFingerprint),
        new("d10e86231a472351cbbcf5ffaa46f18820269037acecaf17678e6472c0b8e710",
            FullGameEntryPromptTextFingerprint),
        // Effects-free D00/four texts only; existing saved-state definitions are unchanged.
        new(FullGameEntryPromptTextFingerprint, FullGameNivaArrivalFingerprint),
        new(FullGamePhotoReturnTextFingerprint, FullGameNivaArrivalFingerprint),
        new("d10e86231a472351cbbcf5ffaa46f18820269037acecaf17678e6472c0b8e710",
            FullGameNivaArrivalFingerprint),
        // One skip label only; preserve the already accepted exact predecessors.
        new(FullGameNivaArrivalFingerprint, FullGameArrivalSkipTextFingerprint),
        new(FullGameEntryPromptTextFingerprint, FullGameArrivalSkipTextFingerprint),
        new(FullGamePhotoReturnTextFingerprint, FullGameArrivalSkipTextFingerprint),
        new("d10e86231a472351cbbcf5ffaa46f18820269037acecaf17678e6472c0b8e710",
            FullGameArrivalSkipTextFingerprint),
        // D07 reply only: two localized values and generated hashes; state definitions are identical.
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameShuraleReplyTextFingerprint),
        // Optional family account and dialogue only; saved catalog, quests, photos and world rules are unchanged.
        new(FullGameShuraleReplyTextFingerprint, FullGameWaterHomeAccountFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameWaterHomeAccountFingerprint),
        // 45 dialogue/choice text values only; all saved-state definitions are identical.
        new(FullGameWaterHomeAccountFingerprint, FullGameNaturalDialogueFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameNaturalDialogueFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameNaturalDialogueFingerprint),
        // Two dialogue lines only; the saved campaign state and gates are unchanged.
        new(FullGameNaturalDialogueFingerprint, FullGameForestPhotoComparisonFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameForestPhotoComparisonFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameForestPhotoComparisonFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameForestPhotoComparisonFingerprint),
        // W01 encounter dialogue only; the save schema, facts and gates remain unchanged.
        new(FullGameForestPhotoComparisonFingerprint, FullGameForestEncounterDialogueFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameForestEncounterDialogueFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameForestEncounterDialogueFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameForestEncounterDialogueFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameForestEncounterDialogueFingerprint),
        // Existing saves retain their earned back-read flag; new copies are read explicitly.
        new(FullGameForestEncounterDialogueFingerprint, FullGamePhotographerReadingFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGamePhotographerReadingFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGamePhotographerReadingFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGamePhotographerReadingFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGamePhotographerReadingFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGamePhotographerReadingFingerprint),
        // W04 re-entry uses recorded visit state rather than the initial active quest.
        new(FullGamePhotographerReadingFingerprint, FullGameCommonVisitGateFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameCommonVisitGateFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameCommonVisitGateFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameCommonVisitGateFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameCommonVisitGateFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameCommonVisitGateFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameCommonVisitGateFingerprint),
        // Meeting eligibility changes; existing quest and visit state are retained.
        new(FullGameCommonVisitGateFingerprint, FullGameAlsuMeetingGateFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameAlsuMeetingGateFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameAlsuMeetingGateFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameAlsuMeetingGateFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameAlsuMeetingGateFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameAlsuMeetingGateFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameAlsuMeetingGateFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameAlsuMeetingGateFingerprint),
        // Journal wording no longer asserts an optional dialogue response; saved state is unchanged.
        new(FullGameAlsuMeetingGateFingerprint, FullGameForestAccountFingerprint),
        new(FullGameCommonVisitGateFingerprint, FullGameForestAccountFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameForestAccountFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameForestAccountFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameForestAccountFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameForestAccountFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameForestAccountFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameForestAccountFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameForestAccountFingerprint),
        // Three dialogue texts clarify the existing family objective; saved state is unchanged.
        new(FullGameForestAccountFingerprint, FullGameFamilyPurposeFingerprint),
        new(FullGameAlsuMeetingGateFingerprint, FullGameFamilyPurposeFingerprint),
        new(FullGameCommonVisitGateFingerprint, FullGameFamilyPurposeFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameFamilyPurposeFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameFamilyPurposeFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameFamilyPurposeFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameFamilyPurposeFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameFamilyPurposeFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameFamilyPurposeFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameFamilyPurposeFingerprint),
        // The bank exit label describes the existing transition; saved state is unchanged.
        new(FullGameFamilyPurposeFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGameForestAccountFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGameAlsuMeetingGateFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGameCommonVisitGateFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameBankPathLabelFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameBankPathLabelFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameBankPathLabelFingerprint),
        // Three wording revisions and D17 eligibility only; saved state and completed dialogue effects remain valid.
        new(FullGameRiverExitHintFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameFamilyPhotoMysteryFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameForestDebriefTextFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameBankPathLabelFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameFamilyPurposeFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameForestAccountFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameAlsuMeetingGateFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameCommonVisitGateFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameAlsuMountedPhotoGateFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameAlsuMountedPhotoGateFingerprint),
        // Existing acquired photos and story state remain valid after adding the permission dialogue.
        new(FullGameAlsuMountedPhotoGateFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameRiverExitHintFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameFamilyPhotoMysteryFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameForestDebriefTextFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameBankPathLabelFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameFamilyPurposeFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameForestAccountFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameAlsuMeetingGateFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameCommonVisitGateFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameHousePhotoDialogueFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameHousePhotoDialogueFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameHousePhotoDialogueFingerprint),
        // The leaf conversation preserves historical nodes and its persisted fact.
        new(FullGameHousePhotoDialogueFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameAlsuMountedPhotoGateFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameRiverExitHintFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameFamilyPhotoMysteryFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameForestDebriefTextFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameBankPathLabelFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameFamilyPurposeFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameForestAccountFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameAlsuMeetingGateFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameCommonVisitGateFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameAlsuLeafDialogueFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameAlsuLeafDialogueFingerprint),
        // The completed arrival beat already exists in saved progress.
        new(FullGameAlsuLeafDialogueFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameHousePhotoDialogueFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameAlsuMountedPhotoGateFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameRiverExitHintFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameFamilyPhotoMysteryFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameForestDebriefTextFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameBankPathLabelFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameFamilyPurposeFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameForestAccountFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameAlsuMeetingGateFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameCommonVisitGateFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameTeaCompletionGateFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameTeaCompletionGateFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameTeaCompletionGateFingerprint),
        // Wording changes retain the same saved story state.
        new(FullGameTeaCompletionGateFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameAlsuLeafDialogueFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameHousePhotoDialogueFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameAlsuMountedPhotoGateFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameRiverExitHintFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameFamilyPhotoMysteryFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameForestDebriefTextFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameBankPathLabelFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameFamilyPurposeFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameForestAccountFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameAlsuMeetingGateFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameCommonVisitGateFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGamePhotographerReadingFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameForestEncounterDialogueFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameForestPhotoComparisonFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameNaturalDialogueFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameWaterHomeAccountFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new(FullGameShuraleReplyTextFingerprint, FullGameEarlyDialogueRevisionFingerprint),
        new("95c93823b37f267e529aa99760089d1576f313cb0dcaa813e8aefada07dad2d0",
            FullGameEarlyDialogueRevisionFingerprint)
    ];

    private AtomicSaveGameStore CreateCampaignSaveStore(string directory)
    {
        if (!StringComparer.Ordinal.Equals(_content.CampaignId, FullGameCampaignId) ||
            (!StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameTextRevisionFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameTeaTextRevisionFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameCellarReturnFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameAllWorldBookReturnFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameTamaraQuestFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGamePhotoReturnTextFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameEntryPromptTextFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameNivaArrivalFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameArrivalSkipTextFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameShuraleReplyTextFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameWaterHomeAccountFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameNaturalDialogueFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameForestPhotoComparisonFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameForestEncounterDialogueFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGamePhotographerReadingFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameCommonVisitGateFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameAlsuMeetingGateFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameForestAccountFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameFamilyPurposeFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameBankPathLabelFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameAlsuMountedPhotoGateFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameHousePhotoDialogueFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameAlsuLeafDialogueFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameTeaCompletionGateFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameEarlyDialogueRevisionFingerprint)))
        {
            return new AtomicSaveGameStore(directory);
        }

        return new AtomicSaveGameStore(directory, fingerprintCompatibility: FullGameTextRevisionCompatibility);
    }
}
