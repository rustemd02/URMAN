using System.Text;
using Urman.Content.Compilation;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Play;

/// <summary>The compiled revision a test run will use, or why it cannot start.</summary>
public sealed record PlayBuild(bool Ready, string RevisionLabel, string CampaignFingerprint, IReadOnlyList<string> Problems);

/// <summary>
/// Everything "Play from here" does before the game starts (spec PLAY01,
/// QUEST14): the author's current files are compiled by the one content
/// compiler into the pack the game loads. A draft that does not compile blocks
/// the run with its problems; an older pack is never started silently.
/// </summary>
public static class PlayPreparation
{
    public const string Campaign = "urman.chapter1";
    public const string PackPath = "game/content/urman.chapter1.compiled.v1.json";

    public static Task<PlayBuild> CompileAsync(string root, CancellationToken cancellationToken = default) =>
        CompileAsync(root, Campaign, PackPath, cancellationToken);

    public static Task<PlayBuild> CompileAsync(
        string root,
        CampaignWorldContext campaignContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaignContext);
        return CompileAsync(root, campaignContext.Campaign.Selection, campaignContext.PackPath, cancellationToken);
    }

    private static async Task<PlayBuild> CompileAsync(
        string root,
        string campaign,
        string packPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(campaign) || campaign is "." or ".."
            || campaign.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_')
            || packPath != $"game/content/{campaign}.compiled.v1.json")
            throw new ArgumentException("Некорректный путь пакета кампании.", nameof(packPath));
        StudioFileTransaction.EnsureNoLinks(root, packPath);
        var result = await new ContentCompiler().CompileAsync(root, campaign, [], cancellationToken);
        if (!result.IsSuccess)
        {
            return new(false, "", "", result.Diagnostics
                .Select(diagnostic => $"{diagnostic.SourcePath}{diagnostic.JsonPointer}: {diagnostic.Message}")
                .ToArray());
        }

        var text = CompiledPackText.Serialize(result.Pack!);
        StudioFileTransaction.EnsureNoLinks(root, packPath);
        var path = Path.Combine(root, packPath);
        if (!File.Exists(path) || File.ReadAllText(path) != text)
        {
            AtomicFile.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        }

        var fingerprint = (string)result.Pack!["campaignFingerprint"]!;
        return new(true, $"контент {fingerprint[..8]}", fingerprint, []);
    }
}
