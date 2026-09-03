using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Urman.Content.Validation;

public sealed class ContentSchemaValidator
{
    private readonly IReadOnlyDictionary<string, JsonSchema> _schemas;

    private ContentSchemaValidator(IReadOnlyDictionary<string, JsonSchema> schemas)
    {
        _schemas = schemas;
    }

    public static ContentSchemaValidator Load(string schemaDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaDirectory);
        var fullDirectory = Path.GetFullPath(schemaDirectory);
        var registry = new SchemaRegistry();
        var buildOptions = new BuildOptions { SchemaRegistry = registry };
        var paths = Directory.GetFiles(fullDirectory, "*.schema.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path) == "common.schema.json" ? 0 : 1)
            .ThenBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        var schemas = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            var schema = JsonSchema.FromFile(path, buildOptions);
            registry.Register(schema);
            schemas.Add(Path.GetFileName(path), schema);
        }

        return new(schemas);
    }

    public IReadOnlyList<ContentDiagnostic> Validate(string schemaFile, JsonNode instance, string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaFile);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (!_schemas.TryGetValue(schemaFile, out var schema))
        {
            return [new("MissingSchema", sourcePath, string.Empty, $"Schema {schemaFile} is not registered.")];
        }

        using var document = JsonDocument.Parse(instance.ToJsonString());
        var result = schema.Evaluate(document.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        });
        if (result.IsValid)
        {
            return [];
        }

        var diagnostics = new List<ContentDiagnostic>();
        CollectErrors(result, sourcePath, diagnostics);
        if (diagnostics.Count == 0)
        {
            diagnostics.Add(new("InvalidSchema", sourcePath, string.Empty, $"Content does not satisfy {schemaFile}."));
        }

        return diagnostics;
    }

    private static void CollectErrors(EvaluationResults result, string sourcePath, ICollection<ContentDiagnostic> diagnostics)
    {
        if (result.Errors is { Count: > 0 })
        {
            foreach (var error in result.Errors.Values.Distinct(StringComparer.Ordinal))
            {
                diagnostics.Add(new("InvalidSchema", sourcePath, result.InstanceLocation.ToString(), error));
            }
        }

        foreach (var detail in result.Details ?? [])
        {
            CollectErrors(detail, sourcePath, diagnostics);
        }
    }
}
