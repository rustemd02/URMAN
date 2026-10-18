using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    // 80266983 -> 5b0a7ccf adds only 18 UI texts/provides IDs; all gameplay
    // registries and dependency data match. Exact pack proof lives in PW-025.
    // A different current fingerprint deliberately disables this one-way pair.
    private const string FullGameCampaignId = "urman.fullgame";
    private const string FullGameTextRevisionFingerprint =
        "0b1a4c3e32b374360f4aebda32d524362862b9f1c171b3f7ec69422f09a8cb1d";

    private static readonly IReadOnlyCollection<SaveGameFingerprintCompatibility> FullGameTextRevisionCompatibility =
    [
        new(
            "60fdd3bc0ef9c12c5c022e804b01c28e71fddf48bcf8490bc68303b593d7b7a2",
            FullGameTextRevisionFingerprint)
    ];

    private AtomicSaveGameStore CreateCampaignSaveStore(string directory)
    {
        if (!StringComparer.Ordinal.Equals(_content.CampaignId, FullGameCampaignId) ||
            !StringComparer.Ordinal.Equals(_content.CampaignFingerprint, FullGameTextRevisionFingerprint))
        {
            return new AtomicSaveGameStore(directory);
        }

        return new AtomicSaveGameStore(directory, fingerprintCompatibility: FullGameTextRevisionCompatibility);
    }
}
