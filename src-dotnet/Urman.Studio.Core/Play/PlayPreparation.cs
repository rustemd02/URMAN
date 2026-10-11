using System.Text;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;
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

    /// <summary>The compiled pack of one campaign selection, so a run follows the chosen campaign.</summary>
    public static string PackPathFor(string campaignSelection) => $"game/content/{campaignSelection}.compiled.v1.json";

    public static Task<PlayBuild> CompileAsync(string root, CancellationToken cancellationToken = default) =>
        CompileAsync(root, Campaign, cancellationToken);

    /// <summary>
    /// The hash of everything compilation reads for one campaign: its manifest, the files of
    /// its modules and the schemas. The compiled pack is deliberately NOT part of it, because
    /// the compiler writes that pack itself, so hashing it would make every run look changed
    /// while the author changed nothing. The module folder mapping is the shared one, so a
    /// module id with dots resolves the same way here as in the coverage report.
    /// </summary>
    public static string CompileInputHash(string root, string campaignSelection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var selection = string.IsNullOrWhiteSpace(campaignSelection) ? Campaign : campaignSelection;
        var files = new SortedSet<string>(StringComparer.Ordinal);
        var manifest = Path.Combine(full, "content", "campaigns", selection, "campaign.json");
        JsonObject? campaign = null;
        if (File.Exists(manifest))
        {
            files.Add(manifest);
            campaign = JsonNode.Parse(File.ReadAllText(manifest)) as JsonObject;
        }

        if (campaign?["modules"] is JsonArray modules)
        {
            foreach (var module in modules.OfType<JsonObject>()
                .Select(item => (string?)item["moduleId"])
                .OfType<string>()
                .Where(id => id.Length > 0))
            {
                var folder = Path.Combine(full, "content", "modules", RuleCatalogCoverage.ModuleFolder(module));
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    files.Add(file);
                }
            }
        }

        // The PhotoWorlds authoring specs are compiler inputs too, and they live OUTSIDE the
        // module folders, so the manifest names them. The rule is the compiler's own: a spec
        // must be a workspace-relative path that stays inside the workspace, and a rooted or
        // escaping path is skipped here exactly as the compiler refuses it. No second registry:
        // the same declaration is read, one name per spec.
        if (campaign?["photoBook"]?["authoringSpecSources"] is JsonObject specSources)
        {
            foreach (var pair in specSources)
            {
                if (pair.Value is not JsonValue value || !value.TryGetValue<string>(out var reference) || string.IsNullOrWhiteSpace(reference) || Path.IsPathRooted(reference))
                {
                    continue;
                }

                var specPath = Path.GetFullPath(Path.Combine(full, reference.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar)));
                var relativeSpec = Path.GetRelativePath(full, specPath);
                if (relativeSpec == ".." || relativeSpec.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relativeSpec))
                {
                    continue;
                }

                if (File.Exists(specPath))
                {
                    files.Add(specPath);
                }
            }
        }

        var schemas = Path.Combine(full, "content", "schemas");
        if (Directory.Exists(schemas))
        {
            foreach (var file in Directory.EnumerateFiles(schemas, "*", SearchOption.AllDirectories))
            {
                files.Add(file);
            }
        }

        using var hash = System.Security.Cryptography.SHA256.Create();
        var buffer = new MemoryStream();
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(full, file).Replace('\\', '/');
            var name = System.Text.Encoding.UTF8.GetBytes(relative);
            buffer.Write(name, 0, name.Length);
            var bytes = File.ReadAllBytes(file);
            buffer.Write(bytes, 0, bytes.Length);
        }

        buffer.Position = 0;
        return Convert.ToHexString(hash.ComputeHash(buffer)).ToLowerInvariant();
    }

    public static async Task<PlayBuild> CompileAsync(string root, string campaignSelection, CancellationToken cancellationToken = default)
    {
        var selection = string.IsNullOrWhiteSpace(campaignSelection) ? Campaign : campaignSelection;
        var result = await new ContentCompiler().CompileAsync(root, selection, [], cancellationToken);
        if (!result.IsSuccess)
        {
            return new(false, "", "", result.Diagnostics
                .Select(diagnostic => $"{diagnostic.SourcePath}{diagnostic.JsonPointer}: {diagnostic.Message}")
                .ToArray());
        }

        var text = CompiledPackText.Serialize(result.Pack!);
        var path = Path.Combine(root, PackPathFor(selection));
        if (!File.Exists(path) || File.ReadAllText(path) != text)
        {
            AtomicFile.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        }

        var fingerprint = (string)result.Pack!["campaignFingerprint"]!;
        return new(true, $"контент {fingerprint[..8]}", fingerprint, []);
    }
}
