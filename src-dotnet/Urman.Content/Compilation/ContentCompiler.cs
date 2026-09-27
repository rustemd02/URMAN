using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Urman.Content.Validation;
using Urman.Core.Serialization;

namespace Urman.Content.Compilation;

public sealed record ContentCompilationResult(JsonObject? Pack, IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public bool IsSuccess => Pack is not null && Diagnostics.Count == 0;
}

public sealed partial class ContentCompiler
{
    private static readonly StringComparer JsLocaleComparer = StringComparer.Create(CultureInfo.GetCultureInfo("en-US"), ignoreCase: false);

    private static readonly IReadOnlyDictionary<string, string> RegistryByKind = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["asset"] = "assets",
        ["capability"] = "capabilities",
        ["character"] = "characters",
        ["chat"] = "chats",
        ["dialogue"] = "dialogues",
        ["document"] = "documents",
        ["hint"] = "hints",
        ["knowledge"] = "knowledge",
        ["quest"] = "quests",
        ["scene"] = "scenes",
        ["text"] = "texts",
        ["vocabulary"] = "vocabulary"
    };

    private static readonly string[] RegistryNames = RegistryByKind.Values
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public async Task<ContentCompilationResult> CompileAsync(
        string workspaceRoot,
        string campaignSelection,
        IReadOnlyCollection<string>? moduleSelections = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignSelection);

        var root = Path.GetFullPath(workspaceRoot);
        var diagnostics = new List<ContentDiagnostic>();
        var campaignPath = ResolveCampaignPath(root, campaignSelection);
        var campaign = await ReadObjectAsync(root, campaignPath, diagnostics, cancellationToken);
        if (campaign is null)
        {
            return new(null, diagnostics);
        }

        var schemaValidator = ContentSchemaValidator.Load(Path.Combine(root, "content", "schemas"));
        diagnostics.AddRange(schemaValidator.Validate(
            "campaign-manifest.schema.json",
            campaign,
            Relative(root, campaignPath)));
        if (diagnostics.Count > 0)
        {
            return new(null, diagnostics);
        }

        var requestedModules = moduleSelections is { Count: > 0 }
            ? moduleSelections.ToArray()
            : RequiredStrings(campaign, "modules", "moduleId").ToArray();
        var modules = new List<LoadedModule>();
        foreach (var selection in requestedModules)
        {
            var manifestPath = ResolveModulePath(root, selection);
            var manifest = await ReadObjectAsync(root, manifestPath, diagnostics, cancellationToken);
            if (manifest is not null)
            {
                modules.Add(new LoadedModule(manifestPath, manifest));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new(null, diagnostics);
        }

        ValidateModuleSelection(root, campaignPath, campaign, modules, diagnostics);
        if (diagnostics.Count > 0)
        {
            return new(null, diagnostics);
        }

        var registries = RegistryNames.ToDictionary(
            name => name,
            _ => new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var moduleFingerprintInputs = new List<JsonObject>();

        foreach (var module in modules)
        {
            var sourceInputs = new List<JsonObject>();
            foreach (var sourceFile in RequiredStrings(module.Manifest, "sourceFiles"))
            {
                var sourcePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(module.ManifestPath)!, sourceFile));
                var definitionNode = await ReadDefinitionAsync(root, sourcePath, diagnostics, cancellationToken);
                if (definitionNode is null)
                {
                    continue;
                }

                sourceInputs.Add(new JsonObject
                {
                    ["sourceFile"] = sourceFile,
                    ["normalized"] = CanonicalJson.Serialize(definitionNode)
                });

                var definitions = definitionNode is JsonArray array ? array.OfType<JsonObject>() : [definitionNode.AsObject()];
                foreach (var definition in definitions)
                {
                    var id = RequiredString(definition, "id");
                    var kind = KindFromId(id);
                    if (kind is null || !RegistryByKind.TryGetValue(kind, out var registryName))
                    {
                        diagnostics.Add(new("InvalidSchema", Relative(root, sourcePath), "/id", $"Unsupported content kind in ID {id}."));
                        continue;
                    }

                    if (!registries[registryName].TryAdd(id, definition))
                    {
                        diagnostics.Add(new("DuplicateId", Relative(root, sourcePath), "/id", $"Duplicate content ID {id}."));
                    }
                }
            }

            sourceInputs.Sort((left, right) => JsLocaleComparer.Compare(RequiredString(left, "sourceFile"), RequiredString(right, "sourceFile")));
            moduleFingerprintInputs.Add(new JsonObject
            {
                ["moduleId"] = RequiredString(module.Manifest, "moduleId"),
                ["exactVersion"] = RequiredString(module.Manifest, "exactVersion"),
                ["manifest"] = module.Manifest.DeepClone(),
                ["sources"] = new JsonArray(sourceInputs.Select(item => item.DeepClone()).ToArray())
            });
        }

        if (diagnostics.Count > 0)
        {
            return new(null, diagnostics);
        }

        ApplyTransitionOverrides(root, campaignPath, campaign, registries, diagnostics);
        if (diagnostics.Count > 0)
        {
            return new(null, diagnostics);
        }

        var campaignNode = BuildCampaign(campaign, modules, registries);
        var moduleFingerprints = moduleFingerprintInputs
            .Select(input => new JsonObject
            {
                ["moduleId"] = RequiredString(input, "moduleId"),
                ["exactVersion"] = RequiredString(input, "exactVersion"),
                ["sha256"] = CanonicalJson.Sha256(input)
            })
            .OrderBy(item => RequiredString(item, "moduleId"), JsLocaleComparer)
            .ToArray();

        var registryNode = new JsonObject();
        foreach (var registryName in RegistryNames)
        {
            registryNode[registryName] = new JsonArray(registries[registryName].Values
                .OrderBy(definition => RequiredString(definition, "id"), JsLocaleComparer)
                .Select(definition => definition.DeepClone())
                .ToArray());
        }

        var dependencyEdges = modules
            .SelectMany(module => RequiredObjects(module.Manifest, "dependencies").Select(dependency => new JsonObject
            {
                ["from"] = RequiredString(module.Manifest, "moduleId"),
                ["to"] = RequiredString(dependency, "moduleId")
            }))
            .OrderBy(edge => $"{RequiredString(edge, "from")}:{RequiredString(edge, "to")}", JsLocaleComparer)
            .ToArray();

        var fingerprintInput = new JsonObject
        {
            ["campaign"] = campaignNode.DeepClone(),
            ["moduleFingerprints"] = new JsonArray(moduleFingerprints.Select(item => item.DeepClone()).ToArray())
        };
        var pack = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packVersion"] = "1.0.0",
            ["campaign"] = campaignNode,
            ["registries"] = registryNode,
            ["dependencyGraph"] = new JsonObject
            {
                ["nodes"] = new JsonArray(modules.Select(module => RequiredString(module.Manifest, "moduleId"))
                    .Order(StringComparer.Ordinal)
                    .Select(value => (JsonNode?)JsonValue.Create(value))
                    .ToArray()),
                ["edges"] = new JsonArray(dependencyEdges.Select(edge => edge.DeepClone()).ToArray())
            },
            ["moduleFingerprints"] = new JsonArray(moduleFingerprints),
            ["campaignFingerprint"] = CanonicalJson.Sha256(fingerprintInput)
        };

        diagnostics.AddRange(schemaValidator.Validate("compiled-content-pack.schema.json", pack, Relative(root, campaignPath)));
        if (diagnostics.Count > 0)
        {
            return new(null, diagnostics);
        }

        foreach (var dialogue in registries["dialogues"].Values)
        {
            if (dialogue["entryRoutes"] is not JsonArray routes) continue;
            var nodes = RequiredObjects(dialogue, "nodes")
                .Select(node => RequiredString(node, "id")).ToHashSet(StringComparer.Ordinal);
            for (var index = 0; index < routes.Count; index++)
            {
                var nodeId = RequiredString(routes[index]!.AsObject(), "nodeId");
                if (!nodes.Contains(nodeId))
                    diagnostics.Add(new("MissingDialogueEntryNode", Relative(root, campaignPath),
                        $"/dialogues/{RequiredString(dialogue, "id")}/entryRoutes/{index}/nodeId",
                        $"Dialogue entry node {nodeId} does not exist."));
            }
        }
        foreach (var quest in registries["quests"].Values)
        {
            if (quest["stages"] is not JsonArray stages) continue;
            foreach (var problem in Urman.Core.Quests.QuestLifecycleReducer.TransitionProblems(stages))
                diagnostics.Add(new("InvalidQuestTransition", Relative(root, campaignPath),
                    $"/quests/{RequiredString(quest, "id")}/stages", problem));
        }
        return new(diagnostics.Count == 0 ? pack : null, diagnostics);
    }

    private static JsonObject BuildCampaign(
        JsonObject campaign,
        IReadOnlyCollection<LoadedModule> modules,
        IReadOnlyDictionary<string, Dictionary<string, JsonObject>> registries)
    {
        var roleBindings = new JsonObject();
        foreach (var binding in campaign["roleBindings"]!.AsObject().OrderBy(binding => binding.Key, JsLocaleComparer))
        {
            roleBindings[binding.Key] = binding.Value!.DeepClone();
        }

        var requirements = new List<JsonObject>();
        requirements.AddRange(OptionalObjects(campaign, "capabilityRequirements"));
        foreach (var module in modules)
        {
            requirements.AddRange(OptionalObjects(module.Manifest, "requires"));
        }

        foreach (var quest in registries["quests"].Values)
        {
            foreach (var stage in OptionalObjects(quest, "stages"))
            {
                foreach (var objective in OptionalObjects(stage, "objectives"))
                {
                    if (objective["capability"] is JsonObject capability)
                    {
                        requirements.Add(new JsonObject
                        {
                            ["protocolId"] = RequiredString(capability, "protocolId"),
                            ["exactVersion"] = RequiredString(capability, "exactVersion")
                        });
                    }
                }
            }
        }

        var uniqueRequirements = requirements
            .GroupBy(requirement => $"{RequiredString(requirement, "protocolId")}@{RequiredString(requirement, "exactVersion")}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(requirement => RequiredString(requirement, "protocolId"), JsLocaleComparer)
            .ThenBy(requirement => RequiredString(requirement, "exactVersion"), JsLocaleComparer)
            .Select(requirement => new JsonObject
            {
                ["protocolId"] = RequiredString(requirement, "protocolId"),
                ["exactVersion"] = RequiredString(requirement, "exactVersion")
            })
            .ToArray();

        var invariants = RequiredObjects(campaign, "invariants")
            .OrderBy(invariant => RequiredString(invariant, "id"), JsLocaleComparer)
            .Select(invariant => invariant.DeepClone())
            .ToArray();

        var result = new JsonObject
        {
            ["id"] = RequiredString(campaign, "id"),
            ["exactVersion"] = RequiredString(campaign, "exactVersion"),
            ["entrypoint"] = RequiredString(campaign, "entrypoint"),
            ["orderedModules"] = campaign["modules"]!.DeepClone(),
            ["roleBindings"] = roleBindings,
            ["capabilityRequirements"] = new JsonArray(uniqueRequirements),
            ["narrativeOrder"] = campaign["narrativeOrder"]!.DeepClone(),
            ["invariants"] = new JsonArray(invariants)
        };

        if (campaign["transitionOverrides"] is JsonArray transitionOverrides)
        {
            result["transitionOverrides"] = new JsonArray(
                transitionOverrides
                    .OfType<JsonObject>()
                    .OrderBy(overrideNode => RequiredString(overrideNode, "sourceSceneId"), JsLocaleComparer)
                    .ThenBy(overrideNode => RequiredString(overrideNode, "id"), JsLocaleComparer)
                    .Select(overrideNode => (JsonNode?)overrideNode.DeepClone())
                    .ToArray());
        }

        return result;
    }

    private static void ApplyTransitionOverrides(
        string root,
        string campaignPath,
        JsonObject campaign,
        IReadOnlyDictionary<string, Dictionary<string, JsonObject>> registries,
        ICollection<ContentDiagnostic> diagnostics)
    {
        if (campaign["transitionOverrides"] is not JsonArray overrides)
        {
            return;
        }

        var interactionIds = new HashSet<string>(
            registries["scenes"].Values
                .SelectMany(scene => (scene["interactions"] as JsonArray ?? []).OfType<JsonObject>())
                .Select(interaction => interaction["id"]?.GetValue<string>())
                .Where(id => id is not null)
                .Select(id => id!),
            StringComparer.Ordinal);

        for (var index = 0; index < overrides.Count; index++)
        {
            var pointer = $"/transitionOverrides/{index}";
            if (overrides[index] is not JsonObject overrideNode)
            {
                diagnostics.Add(new("InvalidTransitionOverride", Relative(root, campaignPath), pointer, "Transition override must be an object."));
                continue;
            }

            var sourceSceneId = ReadTransitionString(overrideNode, "sourceSceneId", root, campaignPath, pointer, diagnostics);
            var interactionId = ReadTransitionString(overrideNode, "id", root, campaignPath, pointer, diagnostics);
            var labelTextId = ReadTransitionString(overrideNode, "labelTextId", root, campaignPath, pointer, diagnostics);
            var conditions = ReadTransitionArray(overrideNode, "conditions", root, campaignPath, pointer, diagnostics);
            var effects = ReadTransitionArray(overrideNode, "effects", root, campaignPath, pointer, diagnostics);
            var targetFields = new[] { "targetSceneId", "targetDialogueId", "targetDocumentId" }
                .Where(field => overrideNode[field] is not null)
                .ToArray();

            if (targetFields.Length != 1)
            {
                diagnostics.Add(new(
                    "InvalidTransitionTarget",
                    Relative(root, campaignPath),
                    pointer,
                    "A transition override must declare exactly one target field."));
            }

            if (sourceSceneId is null || interactionId is null || labelTextId is null || conditions is null || effects is null || targetFields.Length != 1)
            {
                continue;
            }

            if (!string.Equals(KindFromId(sourceSceneId), "scene", StringComparison.Ordinal))
            {
                diagnostics.Add(new("InvalidTransitionSourceKind", Relative(root, campaignPath), $"{pointer}/sourceSceneId", $"Transition source {sourceSceneId} is not a scene ID."));
                continue;
            }

            if (!registries["scenes"].TryGetValue(sourceSceneId, out var sourceScene))
            {
                diagnostics.Add(new("MissingTransitionSourceScene", Relative(root, campaignPath), $"{pointer}/sourceSceneId", $"Transition source scene {sourceSceneId} is not present in the selected modules."));
                continue;
            }

            if (!string.Equals(KindFromId(interactionId), "interaction", StringComparison.Ordinal))
            {
                diagnostics.Add(new("InvalidTransitionInteractionId", Relative(root, campaignPath), $"{pointer}/id", $"Transition ID {interactionId} is not an interaction ID."));
                continue;
            }

            if (!interactionIds.Add(interactionId))
            {
                diagnostics.Add(new("DuplicateTransitionInteraction", Relative(root, campaignPath), $"{pointer}/id", $"Interaction ID {interactionId} already exists in the selected campaign."));
                continue;
            }

            if (!registries["texts"].ContainsKey(labelTextId))
            {
                diagnostics.Add(new("UnresolvedTransitionLabel", Relative(root, campaignPath), $"{pointer}/labelTextId", $"Transition label {labelTextId} is not present in the selected modules."));
                continue;
            }

            var targetField = targetFields[0];
            var targetId = overrideNode[targetField]!.GetValue<string>();
            var targetRegistry = targetField switch
            {
                "targetSceneId" => "scenes",
                "targetDialogueId" => "dialogues",
                "targetDocumentId" => "documents",
                _ => string.Empty
            };
            if (targetRegistry.Length == 0 || !registries[targetRegistry].ContainsKey(targetId))
            {
                diagnostics.Add(new("UnresolvedTransitionTarget", Relative(root, campaignPath), $"{pointer}/{targetField}", $"Transition target {targetId} is not present in the selected modules."));
                continue;
            }

            if (sourceScene["interactions"] is not JsonArray sourceInteractions)
            {
                diagnostics.Add(new("InvalidTransitionSourceScene", Relative(root, campaignPath), $"{pointer}/sourceSceneId", $"Source scene {sourceSceneId} has no interactions array."));
                continue;
            }

            var interaction = new JsonObject
            {
                ["id"] = interactionId,
                ["labelTextId"] = labelTextId,
                ["conditions"] = conditions.DeepClone(),
                ["effects"] = effects.DeepClone(),
                [targetField] = targetId
            };
            sourceInteractions.Add(interaction);
        }
    }

    private static string? ReadTransitionString(
        JsonObject owner,
        string property,
        string root,
        string campaignPath,
        string pointer,
        ICollection<ContentDiagnostic> diagnostics)
    {
        if (owner[property] is JsonValue value && value.TryGetValue<string>(out var result) && !string.IsNullOrWhiteSpace(result))
        {
            return result;
        }

        diagnostics.Add(new("InvalidTransitionOverride", Relative(root, campaignPath), $"{pointer}/{property}", $"Transition override property {property} must be a non-empty string."));
        return null;
    }

    private static JsonArray? ReadTransitionArray(
        JsonObject owner,
        string property,
        string root,
        string campaignPath,
        string pointer,
        ICollection<ContentDiagnostic> diagnostics)
    {
        if (owner[property] is JsonArray array)
        {
            return array;
        }

        diagnostics.Add(new("InvalidTransitionOverride", Relative(root, campaignPath), $"{pointer}/{property}", $"Transition override property {property} must be an array."));
        return null;
    }

    private static void ValidateModuleSelection(
        string root,
        string campaignPath,
        JsonObject campaign,
        IReadOnlyCollection<LoadedModule> modules,
        ICollection<ContentDiagnostic> diagnostics)
    {
        var selectedById = modules.ToDictionary(module => RequiredString(module.Manifest, "moduleId"), StringComparer.Ordinal);
        foreach (var dependency in RequiredObjects(campaign, "modules"))
        {
            var moduleId = RequiredString(dependency, "moduleId");
            if (!selectedById.TryGetValue(moduleId, out var selected))
            {
                diagnostics.Add(new("MissingModule", Relative(root, campaignPath), "/modules", $"Campaign module {moduleId} was not supplied."));
                continue;
            }

            var requiredVersion = RequiredString(dependency, "exactVersion");
            var selectedVersion = RequiredString(selected.Manifest, "exactVersion");
            if (!StringComparer.Ordinal.Equals(requiredVersion, selectedVersion))
            {
                diagnostics.Add(new("IncompatibleExactVersion", Relative(root, campaignPath), "/modules", $"Campaign requires {moduleId}@{requiredVersion}, supplied {selectedVersion}."));
            }
        }
    }

    private static async Task<JsonObject?> ReadObjectAsync(
        string root,
        string path,
        ICollection<ContentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("Expected a JSON object.");
        }
        catch (FileNotFoundException)
        {
            diagnostics.Add(new("MissingFile", Relative(root, path), string.Empty, $"Cannot read selected content file: {Relative(root, path)}."));
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new("InvalidJson", Relative(root, path), string.Empty, exception.Message));
        }

        return null;
    }

    private static async Task<JsonNode?> ReadDefinitionAsync(
        string root,
        string path,
        ICollection<ContentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken));
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                diagnostics.Add(new("InvalidJson", Relative(root, path), string.Empty, exception.Message));
                return null;
            }
        }

        try
        {
            var text = (await File.ReadAllTextAsync(path, cancellationToken)).Replace("\r\n", "\n", StringComparison.Ordinal);
            if (!text.StartsWith("---\n", StringComparison.Ordinal))
            {
                throw new FormatException("Markdown source must start with YAML frontmatter.");
            }

            var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
            if (end < 0)
            {
                throw new FormatException("Markdown frontmatter closing delimiter is missing.");
            }

            var result = new JsonObject();
            var lines = text[4..end].Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                var match = FrontmatterLine().Match(line);
                if (!match.Success)
                {
                    throw new FormatException($"Unsupported frontmatter syntax on line {index + 1}. Use flat keys and JSON arrays/objects.");
                }

                result[match.Groups[1].Value] = ParseScalar(match.Groups[2].Value);
            }

            var bodyMarkdown = text[(end + 5)..];
            if (string.IsNullOrWhiteSpace(bodyMarkdown))
            {
                throw new FormatException("Markdown body must be non-empty.");
            }

            result["bodyMarkdown"] = bodyMarkdown;
            return result;
        }
        catch (Exception exception) when (exception is IOException or FormatException or JsonException)
        {
            diagnostics.Add(new("InvalidFrontmatter", Relative(root, path), string.Empty, exception.Message));
            return null;
        }
    }

    private static JsonNode? ParseScalar(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            return JsonValue.Create(string.Empty);
        }

        if (value is "null" or "true" or "false" ||
            (value.StartsWith('"') && value.EndsWith('"')) ||
            (value.StartsWith('[') && value.EndsWith(']')) ||
            (value.StartsWith('{') && value.EndsWith('}')))
        {
            return JsonNode.Parse(value);
        }

        if (ScalarNumber().IsMatch(value))
        {
            return value.Contains('.', StringComparison.Ordinal)
                ? JsonValue.Create(decimal.Parse(value, CultureInfo.InvariantCulture))
                : JsonValue.Create(long.Parse(value, CultureInfo.InvariantCulture));
        }

        return JsonValue.Create(value.Length >= 2 && value.StartsWith('\'') && value.EndsWith('\'') ? value[1..^1] : value);
    }

    private static string ResolveCampaignPath(string root, string selection)
    {
        if (selection.Contains(Path.DirectorySeparatorChar) || selection.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(root, selection));
        }

        return Path.Combine(root, "content", "campaigns", selection, "campaign.json");
    }

    private static string ResolveModulePath(string root, string selection)
    {
        if (selection.Contains(Path.DirectorySeparatorChar) || selection.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(root, selection));
        }

        foreach (var path in Directory.GetFiles(Path.Combine(root, "content", "modules"), "module.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.GetProperty("moduleId").GetString() == selection)
            {
                return path;
            }
        }

        return Path.Combine(root, "content", "modules", selection, "module.json");
    }

    private static IEnumerable<JsonObject> RequiredObjects(JsonObject owner, string property) =>
        owner[property]!.AsArray().Select(node => node!.AsObject());

    private static IEnumerable<JsonObject> OptionalObjects(JsonObject owner, string property) =>
        owner[property] is JsonArray array ? array.Select(node => node!.AsObject()) : [];

    private static IEnumerable<string> RequiredStrings(JsonObject owner, string property, string? childProperty = null)
    {
        foreach (var node in owner[property]!.AsArray())
        {
            yield return childProperty is null ? node!.GetValue<string>() : RequiredString(node!.AsObject(), childProperty);
        }
    }

    private static string RequiredString(JsonObject owner, string property) => owner[property]!.GetValue<string>();

    private static string? KindFromId(string id)
    {
        var separator = id.IndexOf(':', StringComparison.Ordinal);
        var slash = separator < 0 ? -1 : id.IndexOf('/', separator + 1);
        return separator >= 0 && slash > separator ? id[(separator + 1)..slash] : null;
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    [GeneratedRegex("^([A-Za-z][A-Za-z0-9_-]*):\\s*(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex FrontmatterLine();

    [GeneratedRegex("^-?(?:0|[1-9]\\d*)(?:\\.\\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ScalarNumber();

    private sealed record LoadedModule(string ManifestPath, JsonObject Manifest);
}
