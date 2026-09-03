using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Content.Compilation;
using Urman.Content.Resolvers;
using Urman.Content.Validation;

namespace Urman.Content.Reporting;

public sealed record AudioAssetReadiness(
    string CampaignId,
    string AssetId,
    string? VariantId,
    string File,
    string MediaType,
    string Status,
    bool CaptionClosed,
    bool TranscriptClosed);

public sealed record AmbientStemReadiness(
    string Id,
    string File,
    bool PhysicalFileExists,
    string DeclaredStatus);

public sealed record AudioProductionReport(
    int CampaignsInspected,
    IReadOnlyList<AudioAssetReadiness> Assets,
    IReadOnlyList<AmbientStemReadiness> AmbientStems,
    IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public int LogicalReferenceCount => Assets.Count(asset => string.Equals(asset.Status, "logical-ref", StringComparison.Ordinal));

    public int AuthoredFileCount => Assets.Count(asset => string.Equals(asset.Status, "authored-file", StringComparison.Ordinal));

    public int MissingFileCount => Assets.Count(asset => string.Equals(asset.Status, "missing-file", StringComparison.Ordinal));

    public bool HasOpenAuthoring =>
        Assets.Any(asset => !string.Equals(asset.Status, "authored-file", StringComparison.Ordinal))
        || AmbientStems.Any(stem => stem.DeclaredStatus.Contains("open", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Reports the boundary between technical audio plumbing and authored release audio.
/// Logical .ref assets are intentional placeholders and therefore remain OPEN rather than
/// being reported as missing production files.
/// </summary>
public sealed class AudioProductionReporter
{
    private const string LogicalAssetMediaType = "application/vnd.urman.logical-asset-ref";

    public async Task<AudioProductionReport> InspectAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var root = Path.GetFullPath(workspaceRoot);
        var diagnostics = new List<ContentDiagnostic>();
        var assets = new List<AudioAssetReadiness>();
        var campaignsInspected = 0;
        var campaignsRoot = Path.Combine(root, "content", "campaigns");

        if (!Directory.Exists(campaignsRoot))
        {
            diagnostics.Add(new(
                "AudioCampaignsMissing",
                Relative(root, campaignsRoot),
                string.Empty,
                "The content/campaigns directory does not exist."));
        }
        else
        {
            foreach (var campaignPath in Directory.GetFiles(campaignsRoot, "campaign.json", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var compilation = await new ContentCompiler().CompileAsync(
                    root,
                    Relative(root, campaignPath),
                    cancellationToken: cancellationToken);
                diagnostics.AddRange(compilation.Diagnostics);
                if (compilation.Pack is null)
                {
                    continue;
                }

                campaignsInspected++;
                try
                {
                    _ = ManifestClosureValidator.Validate(compilation.Pack);
                    CollectAudioAssets(root, compilation.Pack, assets);
                }
                catch (ResolverException exception)
                {
                    diagnostics.Add(new(
                        "AudioManifestClosure",
                        Relative(root, campaignPath),
                        exception.Details.TryGetValue("sourcePointer", out var pointer)
                            ? pointer?.ToString() ?? string.Empty
                            : string.Empty,
                        exception.Message));
                }
            }
        }

        var ambientStems = await ReadAmbientManifestAsync(root, diagnostics, cancellationToken);
        return new(campaignsInspected, assets, ambientStems, diagnostics);
    }

    private static void CollectAudioAssets(
        string root,
        JsonObject pack,
        ICollection<AudioAssetReadiness> output)
    {
        var campaignId = pack["campaign"]?["id"]?.GetValue<string>() ?? "unknown-campaign";
        var registries = pack["registries"] as JsonObject;
        var assets = registries?["assets"] as JsonArray;
        var texts = (registries?["texts"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(node => node["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var asset in assets?.OfType<JsonObject>()
                     .Where(node => string.Equals(node["kind"]?.GetValue<string>(), "audio", StringComparison.Ordinal))
                 ?? [])
        {
            var assetId = asset["id"]?.GetValue<string>() ?? "unknown-audio-asset";
            var mediaType = asset["mediaType"]?.GetValue<string>() ?? "unknown";
            var accessibility = asset["accessibility"] as JsonObject;
            var decorative = accessibility?["decorative"]?.GetValue<bool>() ?? false;
            var captionId = accessibility?["captionTextId"]?.GetValue<string>();
            var transcriptId = accessibility?["audioDescriptionTextId"]?.GetValue<string>();
            var baseFile = asset["file"]?.GetValue<string>() ?? string.Empty;
            var variants = new List<(string? VariantId, string File)> { (null, baseFile) };
            foreach (var variant in (asset["variants"] as JsonArray ?? []).OfType<JsonObject>())
            {
                variants.Add((
                    variant["id"]?.GetValue<string>(),
                    variant["file"]?.GetValue<string>() ?? string.Empty));
            }

            foreach (var (variantId, file) in variants)
            {
                output.Add(new(
                    campaignId,
                    assetId,
                    variantId,
                    file,
                    mediaType,
                    DetermineStatus(root, file, mediaType),
                    decorative || (!string.IsNullOrEmpty(captionId) && texts.Contains(captionId)),
                    decorative || (!string.IsNullOrEmpty(transcriptId) && texts.Contains(transcriptId))));
            }
        }
    }

    private static async Task<IReadOnlyList<AmbientStemReadiness>> ReadAmbientManifestAsync(
        string root,
        ICollection<ContentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(root, "game", "assets", "audio", "ambient_manifest.json");
        if (!File.Exists(manifestPath))
        {
            diagnostics.Add(new(
                "AudioAmbientManifestMissing",
                Relative(root, manifestPath),
                string.Empty,
                "The Godot ambient audio manifest does not exist."));
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var document = JsonNode.Parse(json) as JsonObject;
            var stems = document?["stems"] as JsonArray;
            if (stems is null)
            {
                diagnostics.Add(new(
                    "AudioAmbientManifestInvalid",
                    Relative(root, manifestPath),
                    "/stems",
                    "The ambient manifest must contain a stems array."));
                return [];
            }

            var output = new List<AmbientStemReadiness>(stems.Count);
            foreach (var stem in stems.OfType<JsonObject>())
            {
                var id = stem["id"]?.GetValue<string>() ?? "unknown-ambient-stem";
                var file = stem["file"]?.GetValue<string>() ?? string.Empty;
                var declaredStatus = stem["status"]?.GetValue<string>() ?? "unspecified";
                var physicalFileExists = ResolvePhysicalPath(root, file) is { } path && File.Exists(path);
                output.Add(new(id, file, physicalFileExists, declaredStatus));
                if (!physicalFileExists)
                {
                    diagnostics.Add(new(
                        "AudioAmbientFileMissing",
                        Relative(root, manifestPath),
                        $"/stems/{output.Count - 1}/file",
                        $"Ambient stem {id} does not resolve to a physical file."));
                }
            }

            return output;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            diagnostics.Add(new(
                "AudioAmbientManifestInvalid",
                Relative(root, manifestPath),
                string.Empty,
                exception.Message));
            return [];
        }
    }

    private static string DetermineStatus(string root, string file, string mediaType)
    {
        if (string.Equals(mediaType, LogicalAssetMediaType, StringComparison.Ordinal)
            || file.EndsWith(".ref", StringComparison.OrdinalIgnoreCase))
        {
            return "logical-ref";
        }

        return ResolvePhysicalPath(root, file) is { } path && File.Exists(path)
            ? "authored-file"
            : "missing-file";
    }

    private static string? ResolvePhysicalPath(string root, string file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        var relative = file.StartsWith("res://", StringComparison.Ordinal)
            ? file[6..]
            : file;
        var gameRoot = Path.GetFullPath(Path.Combine(root, "game"));
        var candidate = Path.GetFullPath(Path.Combine(gameRoot, relative));
        return candidate.StartsWith(gameRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? candidate
            : null;
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path);
}
