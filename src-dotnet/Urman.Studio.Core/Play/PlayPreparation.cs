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
    /// <summary>
    /// The hash of the AUTHORED RUNTIME content a run reads, as a SEPARATE value from
    /// CompileInputHash so a caller can compare the two for what each one covers.
    ///
    /// CompileInputHash covers what the COMPILER reads: the campaign manifest, its module files,
    /// the schemas and the photoBook authoring specs. It does NOT cover the runtime content that
    /// lives OUTSIDE the module folders - game/content/world world plots, game/content/cutscenes
    /// timelines, the animation catalogue and the studio recipes - and THAT is what a change during
    /// a compile can slip past a guard that only compares CompileInputHash.
    ///
    /// This method hashes exactly those authored runtime files and, like CompileInputHash,
    /// deliberately EXCLUDES every *.compiled.v1.json: the compiler writes those itself, so hashing
    /// them would make a legitimate compile look like an authored change.
    ///
    /// Boundary, stated because it costs something: this covers ALL authored runtime content, not
    /// only the selected campaign's. A change to another campaign's world plot therefore invalidates
    /// a run of this one. That direction is chosen on purpose - a false-stale wastes a compile, while
    /// a missed change launches a scene the author did not ask for - and narrowing it to one campaign
    /// needs a mapping from a campaign or world selection to its runtime FILES, which the manifest
    /// does not carry today: it names scene IDS, not paths.
    /// </summary>
    public static string RuntimeSourceHash(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var folder in new[] { Path.Combine("game", "content") })
        {
            var path = Path.Combine(full, folder);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories))
            {
                if (file.Contains(".compiled.", StringComparison.Ordinal))
                {
                    continue;
                }

                files.Add(file);
            }
        }

        using var sha = System.Security.Cryptography.SHA256.Create();
        var writer = new MemoryStream();
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(full, file).Replace('\\', '/');
            var bytes = File.ReadAllBytes(file);
            writer.Write(System.Text.Encoding.UTF8.GetBytes(relative));
            writer.Write([0]);
            writer.Write(bytes);
            writer.Write([0]);
        }

        writer.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(writer)).ToLowerInvariant();
    }

    /// <summary>What the runtime hash covers and what it costs, for a caller that shows it.</summary>
    public static string RuntimeSourceHashNote(string root)
    {
        var count = 0;
        var path = Path.Combine(Path.GetFullPath(root), "game", "content");
        if (Directory.Exists(path))
        {
            count = Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories)
                .Count(file => !file.Contains(".compiled.", StringComparison.Ordinal));
        }

        return $"RuntimeSourceHash: {count} авторских рантайм-файлов (без compiled-паков). Покрывает контент ВНЕ папок модулей; изменение чужой кампании тоже инвалидирует прогон — это выбрано сознательно, потому что пропущенное изменение опаснее лишней сборки.";
    }

    /// <summary>
    /// The fresh fingerprint of ONE resolved world, which is what Codex asked for: take the
    /// AuthoredDependencies the resolver produced for the SELECTED world - the campaign manifest, the
    /// scene, the world plots and the loader scripts - hash their content, and EXCLUDE the generated
    /// compiled pack, because the compiler writes that itself and hashing it would make a legitimate
    /// compile look like an authored change.
    ///
    /// The point of taking it from a RESOLVED context rather than from a bare campaign selection is
    /// that a caller can resolve the same world again before and after the await and compare the two
    /// fingerprints: if the author edited a plot or a loader script while the compile ran, the second
    /// value differs and the launch must be refused.
    /// </summary>
    public static string WorldSourceFingerprint(string root, CampaignWorldContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(context);
        return HashFiles(Path.GetFullPath(root), context.AuthoredDependencies
            .Where(path => !path.EndsWith(".compiled.v1.json", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary>Resolves one world from its campaign selection and scene, for the caller to hash.</summary>
    public static CampaignWorldContext ResolveWorld(string root, string campaignSelection, string scenePath) =>
        CampaignWorldCatalog.Discover(root).Resolve(campaignSelection, scenePath);

    /// <summary>What the world fingerprint covers, for a caller that shows it.</summary>
    public static IReadOnlyList<string> WorldSourceFingerprintFiles(string root, CampaignWorldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.AuthoredDependencies
            .Where(path => !path.EndsWith(".compiled.v1.json", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string HashFiles(string full, IReadOnlyList<string> relativePaths)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var writer = new MemoryStream();
        foreach (var relative in relativePaths.Order(StringComparer.Ordinal))
        {
            writer.Write(System.Text.Encoding.UTF8.GetBytes(relative));
            writer.Write([0]);
            var path = Path.Combine(full, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                writer.Write(File.ReadAllBytes(path));
            }

            writer.Write([0]);
        }

        writer.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(writer)).ToLowerInvariant();
    }

}
