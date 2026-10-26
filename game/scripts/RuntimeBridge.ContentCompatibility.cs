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
            FullGameShuraleReplyTextFingerprint)
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
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameShuraleReplyTextFingerprint)))
        {
            return new AtomicSaveGameStore(directory);
        }

        return new AtomicSaveGameStore(directory, fingerprintCompatibility: FullGameTextRevisionCompatibility);
    }
}
