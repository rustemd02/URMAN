using System.Text;
using Urman.Content.Compilation;
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

    public static async Task<PlayBuild> CompileAsync(string root, CancellationToken cancellationToken = default)
    {
        var result = await new ContentCompiler().CompileAsync(root, Campaign, [], cancellationToken);
        if (!result.IsSuccess)
        {
            return new(false, "", "", result.Diagnostics
                .Select(diagnostic => $"{diagnostic.SourcePath}{diagnostic.JsonPointer}: {diagnostic.Message}")
                .ToArray());
        }

        var text = CompiledPackText.Serialize(result.Pack!);
        var path = Path.Combine(root, PackPath);
        if (!File.Exists(path) || File.ReadAllText(path) != text)
        {
            AtomicFile.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        }

        var fingerprint = (string)result.Pack!["campaignFingerprint"]!;
        return new(true, $"контент {fingerprint[..8]}", fingerprint, []);
    }
}
