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
    /// the schemas and the photoBook authoring specs. It does NOT cover what the RUNTIME reads
    /// outside the module folders, and this method closes exactly that gap.
    ///
    /// The file list is not guessed from folder names: it is the UNION of the resolver's own
    /// AuthoredDependencies across every world the catalog knows, minus the generated compiled
    /// packs. That makes it cover each world's scene, its world plots and its LOADER SCRIPTS
    /// (which a folder scan of game/content missed entirely), while leaving unrelated content such
    /// as editor addon scenes out - this is the broad option (a), but taken from the owner's
    /// mapping instead of from a convention.
    ///
    /// Boundary, stated because it costs something: it spans ALL campaigns, so a change to another
    /// campaign's world also invalidates a run. That direction is chosen on purpose - a false-stale
    /// wastes a compile while a missed change launches a scene the author did not ask for. The
    /// per-world method below is the narrow one, and it is the one a pre-await/post-await guard
    /// should compare.
    /// </summary>
    public static string RuntimeSourceHash(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        return HashFiles(full, RuntimeSourceFiles(root).ToArray());
    }

    /// <summary>Every authored runtime file the catalog's worlds depend on, packs excluded.</summary>
    public static IReadOnlyList<string> RuntimeSourceFiles(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var catalog = CampaignWorldCatalog.Discover(root);
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var campaign in catalog.Campaigns)
        {
            files.Add(campaign.RelativePath);
        }

        // Pair each campaign with the worlds the CATALOG itself reports for it. Matching on
        // world.CampaignSelection alone is not enough: the default world act1_demo carries an EMPTY
        // selection, so pairing that way silently dropped its six world plots - which is exactly the
        // FALSE-FRESH this method exists to prevent, and the probe caught it.
        foreach (var campaign in catalog.Campaigns)
        {
            foreach (var world in catalog.WorldsFor(campaign.Selection))
            {
                foreach (var dependency in catalog.AuthoredDependencies(campaign, world))
                {
                    files.Add(dependency);
                }
            }
        }

        return files
            .Where(path => !path.EndsWith(".compiled.v1.json", StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>What the runtime hash covers and what it costs, for a caller that shows it.</summary>
    public static string RuntimeSourceHashNote(string root)
    {
        var files = RuntimeSourceFiles(root);
        var scenes = files.Count(path => path.EndsWith(".tscn", StringComparison.Ordinal));
        var scripts = files.Count(path => path.EndsWith(".cs", StringComparison.Ordinal));
        var plots = files.Count(path => path.Contains("/world/", StringComparison.Ordinal));
        return $"RuntimeSourceHash: {files.Count} авторских рантайм-файлов от резолвера (сцен {scenes}, скриптов-загрузчиков {scripts}, участков мира {plots}) без compiled-паков. Список — ОБЪЕДИНЕНИЕ AuthoredDependencies всех миров, поэтому ловит и сцены, и скрипты, которые скан папок пропускал.";
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

    /// <summary>The scenes the runtime loads, which live outside the module folders.</summary>
    private static IEnumerable<string> ScenesUnder(string full)
    {
        var path = Path.Combine(full, "game", "scenes");
        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.tscn", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            : [];
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
