using System.Text.Json;
using System.Text.Json.Nodes;
using Markdig;
using Urman.Content.Compilation;
using Urman.Core.Contracts;

namespace Urman.Content.Validation;

public sealed record ContentDiagnostic(string Code, string SourcePath, string JsonPointer, string Message);

public sealed record ContentValidationResult(
    IReadOnlyList<ContentDiagnostic> Diagnostics,
    int CheckedModules,
    int CheckedCampaigns)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public sealed class ContentWorkspaceValidator
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .Build();

    public async Task<ContentValidationResult> ValidateAsync(string workspaceRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var contentRoot = Path.Combine(Path.GetFullPath(workspaceRoot), "content");
        var diagnostics = new List<ContentDiagnostic>();

        if (!Directory.Exists(contentRoot))
        {
            diagnostics.Add(new("WorkspaceMissing", contentRoot, string.Empty, "The content directory does not exist."));
            return new(diagnostics, 0, 0);
        }

        var moduleFiles = Directory.GetFiles(Path.Combine(contentRoot, "modules"), "module.json", SearchOption.AllDirectories);
        var campaignFiles = Directory.GetFiles(Path.Combine(contentRoot, "campaigns"), "campaign.json", SearchOption.AllDirectories);
        var schemaValidator = ContentSchemaValidator.Load(Path.Combine(contentRoot, "schemas"));

        foreach (var file in Directory.GetFiles(contentRoot, "*.json", SearchOption.AllDirectories))
        {
            await ParseJsonAsync(file, diagnostics, cancellationToken);
        }

        foreach (var file in moduleFiles)
        {
            await ValidateModuleManifestAsync(file, schemaValidator, diagnostics, cancellationToken);
        }

        foreach (var file in campaignFiles)
        {
            await ValidateCampaignManifestAsync(file, schemaValidator, diagnostics, cancellationToken);
        }

        foreach (var file in Directory.GetFiles(contentRoot, "*.md", SearchOption.AllDirectories))
        {
            var body = await File.ReadAllTextAsync(file, cancellationToken);
            _ = Markdown.Parse(body, MarkdownPipeline);
            if (string.IsNullOrWhiteSpace(body))
            {
                diagnostics.Add(new("EmptyMarkdown", Relative(workspaceRoot, file), string.Empty, "Markdown source must not be empty."));
            }
        }

        foreach (var campaignFile in campaignFiles)
        {
            var compilation = await new ContentCompiler().CompileAsync(
                workspaceRoot,
                Relative(workspaceRoot, campaignFile),
                cancellationToken: cancellationToken);
            diagnostics.AddRange(compilation.Diagnostics);
        }

        return new(diagnostics, moduleFiles.Length, campaignFiles.Length);
    }

    private static async Task ParseJsonAsync(string file, ICollection<ContentDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(file);
            using var _ = await JsonDocument.ParseAsync(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new("InvalidJson", file, string.Empty, exception.Message));
        }
    }

    private static async Task ValidateModuleManifestAsync(
        string file,
        ContentSchemaValidator schemaValidator,
        ICollection<ContentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        using var document = await ReadJsonAsync(file, diagnostics, cancellationToken);
        if (document is null)
        {
            return;
        }

        RequireString(document.RootElement, "moduleId", file, diagnostics);
        RequireExactVersion(document.RootElement, "exactVersion", file, diagnostics);
        RequirePositiveSchemaVersion(document.RootElement, file, diagnostics);
        foreach (var diagnostic in schemaValidator.Validate(
                     "module-manifest.schema.json",
                     JsonNode.Parse(document.RootElement.GetRawText())!,
                     file))
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static async Task ValidateCampaignManifestAsync(
        string file,
        ContentSchemaValidator schemaValidator,
        ICollection<ContentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        using var document = await ReadJsonAsync(file, diagnostics, cancellationToken);
        if (document is null)
        {
            return;
        }

        RequireString(document.RootElement, "id", file, diagnostics);
        RequireExactVersion(document.RootElement, "exactVersion", file, diagnostics);
        RequirePositiveSchemaVersion(document.RootElement, file, diagnostics);

        if (!document.RootElement.TryGetProperty("entrypoint", out var entrypoint) || entrypoint.ValueKind != JsonValueKind.String)
        {
            diagnostics.Add(new("RequiredProperty", file, "/entrypoint", "Campaign entrypoint is required."));
        }
        else
        {
            try
            {
                _ = new ContentId(entrypoint.GetString()!);
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(new("InvalidContentId", file, "/entrypoint", exception.Message));
            }
        }

        RequireNonEmptyArray(document.RootElement, "modules", file, diagnostics);
        RequireNonEmptyArray(document.RootElement, "narrativeOrder", file, diagnostics);
        foreach (var diagnostic in schemaValidator.Validate(
                     "campaign-manifest.schema.json",
                     JsonNode.Parse(document.RootElement.GetRawText())!,
                     file))
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static async Task<JsonDocument?> ReadJsonAsync(string file, ICollection<ContentDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(file);
            return await JsonDocument.ParseAsync(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new("InvalidJson", file, string.Empty, exception.Message));
            return null;
        }
    }

    private static void RequirePositiveSchemaVersion(JsonElement root, string file, ICollection<ContentDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("schemaVersion", out var value) || !value.TryGetInt32(out var version) || version < 1)
        {
            diagnostics.Add(new("RequiredProperty", file, "/schemaVersion", "schemaVersion must be a positive integer."));
        }
    }

    private static void RequireString(JsonElement root, string propertyName, string file, ICollection<ContentDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            diagnostics.Add(new("RequiredProperty", file, $"/{propertyName}", $"{propertyName} must be a non-empty string."));
        }
    }

    private static void RequireExactVersion(JsonElement root, string propertyName, string file, ICollection<ContentDiagnostic> diagnostics)
    {
        RequireString(root, propertyName, file, diagnostics);
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var parts = value.GetString()!.Split('.');
            if (parts.Length != 3 || parts.Any(part => !int.TryParse(part, out _)))
            {
                diagnostics.Add(new("InvalidExactVersion", file, $"/{propertyName}", "Version must be an exact major.minor.patch value."));
            }
        }
    }

    private static void RequireNonEmptyArray(JsonElement root, string propertyName, string file, ICollection<ContentDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            diagnostics.Add(new("RequiredProperty", file, $"/{propertyName}", $"{propertyName} must be a non-empty array."));
        }
    }

    private static string Relative(string workspaceRoot, string path) => Path.GetRelativePath(Path.GetFullPath(workspaceRoot), path);
}
