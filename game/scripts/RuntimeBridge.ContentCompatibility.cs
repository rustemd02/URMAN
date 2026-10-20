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
        new(FullGameAllWorldBookReturnFingerprint, FullGameTamaraQuestFingerprint)
    ];

    private AtomicSaveGameStore CreateCampaignSaveStore(string directory)
    {
        if (!StringComparer.Ordinal.Equals(_content.CampaignId, FullGameCampaignId) ||
            (!StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameTextRevisionFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameTeaTextRevisionFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameCellarReturnFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameAllWorldBookReturnFingerprint) &&
             !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameTamaraQuestFingerprint)))
        {
            return new AtomicSaveGameStore(directory);
        }

        return new AtomicSaveGameStore(directory, fingerprintCompatibility: FullGameTextRevisionCompatibility);
    }
}
