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

        ValidateActiveQuestIds(root, campaignPath, campaign, registries["quests"], diagnostics);
        await ValidatePhotoWorldCatalogAsync(root, campaignPath, campaign, registries, diagnostics, cancellationToken);
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

        if (campaign["photoBook"] is JsonObject photoBook)
        {
            var compiledPhotoBook = photoBook.DeepClone().AsObject();
            // These paths are build-time validation inputs. The compiled
            // runtime catalog is self-contained and never resolves docs files.
            compiledPhotoBook.Remove("authoringSpecSources");
            result["photoBook"] = compiledPhotoBook;
        }

        if (campaign["activeQuestIds"] is JsonArray activeQuestIds)
        {
            result["activeQuestIds"] = activeQuestIds.DeepClone();
        }

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

    private static void ValidateActiveQuestIds(
        string root,
        string campaignPath,
        JsonObject campaign,
        IReadOnlyDictionary<string, JsonObject> quests,
        ICollection<ContentDiagnostic> diagnostics)
    {
        if (campaign["activeQuestIds"] is not JsonArray activeQuestIds) return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < activeQuestIds.Count; index++)
        {
            var questId = activeQuestIds[index]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(questId)) continue;
            if (!seen.Add(questId))
            {
                diagnostics.Add(new("DuplicateActiveQuest", Relative(root, campaignPath), $"/activeQuestIds/{index}", $"Active quest ID {questId} appears more than once."));
            }
            if (!quests.ContainsKey(questId))
            {
                diagnostics.Add(new("UnknownActiveQuest", Relative(root, campaignPath), $"/activeQuestIds/{index}", $"Active quest {questId} is not present in the selected modules."));
            }
        }
    }

    private static async Task ValidatePhotoWorldCatalogAsync(
        string root,
        string campaignPath,
        JsonObject campaign,
        IReadOnlyDictionary<string, Dictionary<string, JsonObject>> registries,
        ICollection<ContentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (campaign["photoBook"] is not JsonObject catalog) return;
        var pointer = "/photoBook";
        var allIds = registries.Values.SelectMany(registry => registry.Keys).ToHashSet(StringComparer.Ordinal);
        foreach (var scene in registries["scenes"].Values)
            foreach (var interaction in scene["interactions"] as JsonArray ?? [])
                if (interaction?["id"] is JsonValue interactionId && interactionId.TryGetValue<string>(out var id))
                    allIds.Add(id);
        var photoSources = new Dictionary<string, string>(StringComparer.Ordinal);
        var factSources = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var photos = catalog["photos"] as JsonArray ?? [];
        var facts = catalog["facts"] as JsonArray ?? [];
        var evidence = catalog["evidence"] as JsonArray ?? [];

        void RequireReference(JsonNode? node, string field, string category = "")
        {
            var id = node?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id)) return;
            var valid = category == "character"
                ? registries["characters"].ContainsKey(id)
                : allIds.Contains(id);
            if (!valid)
                diagnostics.Add(new("UnknownPhotoWorldReference", Relative(root, campaignPath), $"{pointer}/{field}",
                    $"PhotoWorlds {field} references unknown content ID {id}."));
        }

        if (catalog["book"] is JsonObject book)
        {
            RequireReference(book["handoffSourceId"], "book/handoffSourceId");
            RequireReference(book["giverId"], "book/giverId", "character");
        }

        for (var index = 0; index < photos.Count; index++)
        {
            if (photos[index] is not JsonObject photo) continue;
            var id = photo["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id)) continue;
            var sourceId = photo["sourceId"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(sourceId)) photoSources[id] = sourceId;
            RequireReference(photo["sourceId"], $"photos/{index}/sourceId");
            RequireReference(photo["permissionId"], $"photos/{index}/permissionId");
            if (photo["sourceAuthorId"] is { } author)
                RequireReference(author, $"photos/{index}/sourceAuthorId", "character");
            if (photo["contextSourceIds"] is JsonArray contextSources)
                for (var sourceIndex = 0; sourceIndex < contextSources.Count; sourceIndex++)
                    RequireReference(contextSources[sourceIndex], $"photos/{index}/contextSourceIds/{sourceIndex}");
        }

        for (var index = 0; index < facts.Count; index++)
        {
            if (facts[index] is not JsonObject fact) continue;
            var id = fact["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id)) continue;
            var sources = (fact["sourceIds"] as JsonArray ?? [])
                .Select(node => node?.GetValue<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray();
            factSources[id] = sources;
            for (var sourceIndex = 0; sourceIndex < (fact["sourceIds"] as JsonArray ?? []).Count; sourceIndex++)
                RequireReference(fact["sourceIds"]![sourceIndex], $"facts/{index}/sourceIds/{sourceIndex}");
        }

        for (var index = 0; index < evidence.Count; index++)
        {
            if (evidence[index] is not JsonObject item) continue;
            var evidenceId = item["id"]?.GetValue<string>() ?? $"#{index}";
            var constituents = item["constituents"] as JsonArray ?? [];
            var candidateSources = new List<string[]>();
            for (var constituentIndex = 0; constituentIndex < constituents.Count; constituentIndex++)
            {
                if (constituents[constituentIndex] is not JsonObject constituent) continue;
                var kind = constituent["kind"]?.GetValue<string>();
                var id = constituent["id"]?.GetValue<string>();
                string[] sources = [];
                if (kind == "fact" && id is not null)
                {
                    if (factSources.TryGetValue(id, out var factCandidates))
                        sources = factCandidates;
                    else
                        diagnostics.Add(new("UnknownPhotoWorldFact", Relative(root, campaignPath), $"{pointer}/evidence/{index}/constituents/{constituentIndex}",
                            $"Evidence {evidenceId} references unknown fact {id}."));
                }
                else if ((kind is "photo-acquired" or "photo-back-read") && id is not null)
                {
                    if (!photoSources.TryGetValue(id, out var source))
                        diagnostics.Add(new("UnknownPhotoWorldPhoto", Relative(root, campaignPath), $"{pointer}/evidence/{index}/constituents/{constituentIndex}",
                            $"Evidence {evidenceId} references unknown PhotoId {id}."));
                    else sources = [source];
                }

                candidateSources.Add(sources);
            }

            if (candidateSources.Count == 0 || !HasDistinctSourceAssignment(candidateSources, 0, new HashSet<string>(StringComparer.Ordinal)))
                diagnostics.Add(new("UnprovablePhotoWorldEvidence", Relative(root, campaignPath), $"{pointer}/evidence/{index}",
                    $"Evidence {evidenceId} cannot assign a distinct authored source to each typed constituent."));
        }

        await ValidateAuthoredPhotoWorldDefinitionsAsync(
            root, campaignPath, catalog, photos, diagnostics, cancellationToken);
    }

    private static async Task ValidateAuthoredPhotoWorldDefinitionsAsync(
        string root,
        string campaignPath,
        JsonObject catalog,
        JsonArray photos,
        ICollection<ContentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        const string pointer = "/photoBook";
        if (catalog["worlds"] is not JsonArray authoredWorlds)
        {
            diagnostics.Add(new("MissingPhotoWorldCatalog", Relative(root, campaignPath), $"{pointer}/worlds",
                "The full PhotoWorlds catalog must explicitly author its five world definitions."));
            return;
        }

        var photoSpecs = await ReadPhotoWorldSpecAsync(root, campaignPath, catalog, "photos", diagnostics, cancellationToken);
        var worldSpecs = await ReadPhotoWorldSpecAsync(root, campaignPath, catalog, "worlds", diagnostics, cancellationToken);
        var assetSpecs = await ReadPhotoWorldSpecAsync(root, campaignPath, catalog, "assets", diagnostics, cancellationToken);
        if (photoSpecs is null || worldSpecs is null || assetSpecs is null)
            return;

        var sourcePhotos = IndexSpecRecords(root, campaignPath, photoSpecs, "photos", "/photos", "InvalidPhotoWorldPhotoSpec", diagnostics);
        var sourceWorlds = IndexSpecRecords(root, campaignPath, worldSpecs, "worlds", "/worlds", "InvalidPhotoWorldWorldSpec", diagnostics);
        var sourceAssets = IndexSpecRecords(root, campaignPath, assetSpecs, "assets", "/assets", "InvalidPhotoWorldAssetSpec", diagnostics);
        if (sourcePhotos is null || sourceWorlds is null || sourceAssets is null)
            return;

        if (!HasSpecVersion(photoSpecs, 1) || !HasSpecVersion(assetSpecs, 1)
            || !HasSpecVersion(worldSpecs, 1)
            || ReadOptionalString(worldSpecs, "kind") != "authoring_spec_not_runtime_content")
        {
            diagnostics.Add(new("UnsupportedPhotoWorldSpecVersion", Relative(root, campaignPath), $"{pointer}/authoringSpecSources",
                "The referenced PhotoWorlds specs must use the supported v1 authoring contracts."));
            return;
        }

        if (photos.Count != 13 || sourcePhotos.Count != 13)
            diagnostics.Add(new("InvalidPhotoWorldPhotoCount", Relative(root, campaignPath), $"{pointer}/photos",
                $"The campaign and referenced photo spec must each contain 13 photos (campaign={photos.Count}, spec={sourcePhotos.Count})."));
        if (authoredWorlds.Count != 5 || sourceWorlds.Count != 5)
            diagnostics.Add(new("InvalidPhotoWorldCount", Relative(root, campaignPath), $"{pointer}/worlds",
                $"The campaign and referenced world spec must each contain 5 worlds (campaign={authoredWorlds.Count}, spec={sourceWorlds.Count})."));

        var campaignPhotos = IndexAuthoredRecords(root, campaignPath, photos, "id", $"{pointer}/photos", "DuplicatePhotoWorldPhoto", diagnostics);
        var campaignWorlds = IndexAuthoredRecords(root, campaignPath, authoredWorlds, "id", $"{pointer}/worlds", "DuplicatePhotoWorldWorld", diagnostics);
        if (campaignPhotos is null || campaignWorlds is null)
            return;

        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < photos.Count; index++)
        {
            if (photos[index] is not JsonObject photo || ReadOptionalString(photo, "pageId") is not { } pageId)
            {
                diagnostics.Add(new("InvalidPhotoWorldPageId", Relative(root, campaignPath), $"{pointer}/photos/{index}/pageId",
                    "Each PhotoId must declare its stable PageId."));
                continue;
            }
            if (!pageIds.Add(pageId))
                diagnostics.Add(new("DuplicatePhotoWorldPageId", Relative(root, campaignPath), $"{pointer}/photos/{index}/pageId",
                    $"PageId {pageId} is assigned to more than one photo."));
        }

        CompareIdSets(root, campaignPath, $"{pointer}/photos", sourcePhotos.Keys, campaignPhotos.Keys, "PhotoId", diagnostics);
        CompareIdSets(root, campaignPath, $"{pointer}/worlds", sourceWorlds.Keys, campaignWorlds.Keys, "WorldId", diagnostics);

        var sourceWorldByPhoto = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (worldId, sourceWorld) in sourceWorlds)
        {
            var photoId = ReadOptionalString(sourceWorld, "photo_id");
            if (photoId is null)
            {
                diagnostics.Add(new("InvalidPhotoWorldWorldSpec", Relative(root, campaignPath), $"{pointer}/worlds/{worldId}/photo_id",
                    $"World spec {worldId} is missing its primary photo reference."));
                continue;
            }
            if (!sourceWorldByPhoto.TryAdd(photoId, worldId))
                diagnostics.Add(new("DuplicatePhotoWorldPrimaryPhoto", Relative(root, campaignPath), $"{pointer}/worlds/{worldId}/photoId",
                    $"PhotoId {photoId} is bound to more than one world in the referenced specs."));
        }

        foreach (var (photoId, sourcePhoto) in sourcePhotos)
        {
            var declaredWorldId = ReadOptionalString(sourcePhoto, "world_id");
            if (sourcePhoto["world_id"] is JsonValue worldValue && worldValue.TryGetValue<string>(out _))
            {
                if (declaredWorldId is null || !sourceWorlds.ContainsKey(declaredWorldId)
                    || !sourceWorldByPhoto.TryGetValue(photoId, out var mappedWorldId)
                    || mappedWorldId != declaredWorldId)
                    diagnostics.Add(new("BrokenPhotoWorldPhotoMap", Relative(root, campaignPath), $"{pointer}/photos/{photoId}",
                        $"PhotoId {photoId} has an inconsistent world_id mapping in photos.spec.json and worlds.spec.json."));
            }
            else if (sourcePhoto["world_id"] is not null)
            {
                diagnostics.Add(new("InvalidPhotoWorldPhotoSpec", Relative(root, campaignPath), $"{pointer}/photos/{photoId}/world_id",
                    $"PhotoId {photoId} world_id must be a WorldId or null."));
            }
            else if (sourceWorldByPhoto.ContainsKey(photoId))
            {
                diagnostics.Add(new("BrokenPhotoWorldPhotoMap", Relative(root, campaignPath), $"{pointer}/photos/{photoId}/world_id",
                    $"Primary PhotoId {photoId} must declare its world_id in photos.spec.json."));
            }
        }

        var assetIds = sourceAssets.Keys.ToHashSet(StringComparer.Ordinal);
        var worldAnchorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (worldId, authoredWorld) in campaignWorlds)
        {
            if (!sourceWorlds.TryGetValue(worldId, out var sourceWorld))
                continue;

            RequireExactString(root, campaignPath, authoredWorld, "photoId", sourceWorld, "photo_id", worldId, "PhotoId", diagnostics);
            RequireExactString(root, campaignPath, authoredWorld, "anchorId", sourceWorld, "anchor_id", worldId, "semantic anchor", diagnostics);
            RequireExactString(root, campaignPath, authoredWorld, "mainSpawnNodeId", sourceWorld, "main_spawn", worldId, "main spawn node", diagnostics);
            RequireExactString(root, campaignPath, authoredWorld, "exitNodeId", sourceWorld, "exit", worldId, "exit node", diagnostics);
            if (ReadOptionalString(authoredWorld, "anchorId") is { } anchorId && !worldAnchorIds.Add(anchorId))
                diagnostics.Add(new("DuplicatePhotoWorldAnchor", Relative(root, campaignPath), $"{pointer}/worlds/{worldId}/anchorId",
                    $"Semantic anchor {anchorId} is assigned to more than one world."));

            var sourceNodes = IndexSpecRecords(root, campaignPath, sourceWorld, "nodes", $"{pointer}/worlds/{worldId}/nodes",
                "InvalidPhotoWorldNodeSpec", diagnostics);
            var authoredNodes = IndexAuthoredRecords(root, campaignPath, authoredWorld["nodes"] as JsonArray ?? [], "id",
                $"{pointer}/worlds/{worldId}/nodes", "DuplicatePhotoWorldNode", diagnostics);
            if (sourceNodes is not null && authoredNodes is not null)
            {
                CompareIdSets(root, campaignPath, $"{pointer}/worlds/{worldId}/nodes", sourceNodes.Keys, authoredNodes.Keys, "NodeId", diagnostics);
                foreach (var (nodeId, authoredNode) in authoredNodes)
                {
                    if (sourceNodes.TryGetValue(nodeId, out var sourceNode))
                        RequireExactString(root, campaignPath, authoredNode, "role", sourceNode, "role", $"{worldId}/{nodeId}", "node role", diagnostics);
                }
                RequireAuthoredNodeRole(root, campaignPath, sourceNodes, ReadOptionalString(sourceWorld, "main_spawn"),
                    "spawn_main", worldId, diagnostics);
                RequireAuthoredNodeRole(root, campaignPath, sourceNodes, ReadOptionalString(sourceWorld, "exit"),
                    "exit", worldId, diagnostics);
            }

            var sourceResourceIds = StringArray(sourceWorld["assets"] as JsonArray);
            var authoredResourceIds = StringArray(authoredWorld["requiredResourceIds"] as JsonArray);
            if (sourceResourceIds is null || authoredResourceIds is null
                || sourceResourceIds.Distinct(StringComparer.Ordinal).Count() != sourceResourceIds.Count
                || authoredResourceIds.Distinct(StringComparer.Ordinal).Count() != authoredResourceIds.Count
                || !sourceResourceIds.ToHashSet(StringComparer.Ordinal).SetEquals(authoredResourceIds))
            {
                diagnostics.Add(new("InvalidPhotoWorldResources", Relative(root, campaignPath), $"{pointer}/worlds/{worldId}/requiredResourceIds",
                    $"World {worldId} must retain the exact unique required-resource IDs from its referenced authoring spec."));
            }

            foreach (var resourceId in authoredResourceIds ?? [])
                if (!assetIds.Contains(resourceId))
                    diagnostics.Add(new("UnknownPhotoWorldResource", Relative(root, campaignPath), $"{pointer}/worlds/{worldId}/requiredResourceIds",
                        $"World {worldId} requires unknown asset requirement {resourceId}. The asset spec records requirements, not runtime availability."));
        }

        var requiredAnchors = StringArray(catalog["requiredAnchors"] as JsonArray);
        if (requiredAnchors is null || requiredAnchors.Distinct(StringComparer.Ordinal).Count() != requiredAnchors.Count
            || !worldAnchorIds.IsSubsetOf(requiredAnchors.ToHashSet(StringComparer.Ordinal)))
            diagnostics.Add(new("InvalidPhotoWorldAnchors", Relative(root, campaignPath), $"{pointer}/requiredAnchors",
                "Required anchors must be unique and include every authored world anchor."));

        foreach (var photoId in sourceWorldByPhoto.Keys)
            if (!campaignPhotos.ContainsKey(photoId))
                diagnostics.Add(new("MissingPhotoWorldPrimaryPhoto", Relative(root, campaignPath), $"{pointer}/photos",
                    $"The primary world PhotoId {photoId} is not present in the campaign photo catalog."));
    }

    private static async Task<JsonObject?> ReadPhotoWorldSpecAsync(
        string root,
        string campaignPath,
        JsonObject catalog,
        string specName,
        ICollection<ContentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var referenceNode = (catalog["authoringSpecSources"] as JsonObject)?[specName];
        var reference = referenceNode is JsonValue referenceValue && referenceValue.TryGetValue<string>(out var sourcePath)
            ? sourcePath : null;
        var pointer = $"/photoBook/authoringSpecSources/{specName}";
        if (string.IsNullOrWhiteSpace(reference) || Path.IsPathRooted(reference))
        {
            diagnostics.Add(new("InvalidPhotoWorldSpecReference", Relative(root, campaignPath), pointer,
                $"A root-relative {specName} spec source is required."));
            return null;
        }

        var normalizedReference = reference.Replace('\\', '/');
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedReference.Replace('/', Path.DirectorySeparatorChar)));
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            diagnostics.Add(new("InvalidPhotoWorldSpecReference", Relative(root, campaignPath), pointer,
                "PhotoWorlds authoring specs must resolve within the selected workspace."));
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
            return JsonNode.Parse(json) as JsonObject
                ?? throw new JsonException("Expected an object root for the PhotoWorlds spec.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            diagnostics.Add(new("InvalidPhotoWorldSpec", Relative(root, campaignPath), pointer,
                $"Cannot read referenced {specName} authoring spec {relativePath}: {exception.Message}"));
            return null;
        }
    }

    private static Dictionary<string, JsonObject>? IndexSpecRecords(
        string root,
        string campaignPath,
        JsonObject owner,
        string property,
        string pointer,
        string diagnosticCode,
        ICollection<ContentDiagnostic> diagnostics)
    {
        if (owner[property] is not JsonArray records)
        {
            diagnostics.Add(new(diagnosticCode, Relative(root, campaignPath), pointer, $"Referenced authoring spec is missing array {property}."));
            return null;
        }

        var indexed = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        for (var index = 0; index < records.Count; index++)
        {
            if (records[index] is not JsonObject record || ReadOptionalString(record, "id") is not { } id)
            {
                diagnostics.Add(new(diagnosticCode, Relative(root, campaignPath), $"{pointer}/{index}", "Each authoring record must have a non-empty string id."));
                continue;
            }
            if (!indexed.TryAdd(id, record))
                diagnostics.Add(new(diagnosticCode, Relative(root, campaignPath), $"{pointer}/{index}/id", $"Duplicate authoring ID {id}."));
        }
        return indexed;
    }

    private static Dictionary<string, JsonObject>? IndexAuthoredRecords(
        string root,
        string campaignPath,
        JsonArray records,
        string idProperty,
        string pointer,
        string diagnosticCode,
        ICollection<ContentDiagnostic> diagnostics)
    {
        var indexed = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        for (var index = 0; index < records.Count; index++)
        {
            if (records[index] is not JsonObject record || ReadOptionalString(record, idProperty) is not { } id)
            {
                diagnostics.Add(new(diagnosticCode, Relative(root, campaignPath), $"{pointer}/{index}", $"Each catalog record must have a non-empty string {idProperty}."));
                continue;
            }
            if (!indexed.TryAdd(id, record))
                diagnostics.Add(new(diagnosticCode, Relative(root, campaignPath), $"{pointer}/{index}/{idProperty}", $"Duplicate catalog ID {id}."));
        }
        return indexed;
    }

    private static bool HasSpecVersion(JsonObject spec, int expected) =>
        spec["version"] is JsonValue version && version.TryGetValue<int>(out var value) && value == expected;

    private static string? ReadOptionalString(JsonObject owner, string property) =>
        owner[property] is JsonValue value && value.TryGetValue<string>(out var result) && !string.IsNullOrWhiteSpace(result)
            ? result : null;

    private static List<string>? StringArray(JsonArray? array)
    {
        if (array is null) return null;
        var result = new List<string>(array.Count);
        foreach (var node in array)
        {
            if (node is not JsonValue value || !value.TryGetValue<string>(out var item) || string.IsNullOrWhiteSpace(item))
                return null;
            result.Add(item);
        }
        return result;
    }

    private static void CompareIdSets(
        string root,
        string campaignPath,
        string pointer,
        IEnumerable<string> sourceIds,
        IEnumerable<string> authoredIds,
        string idKind,
        ICollection<ContentDiagnostic> diagnostics)
    {
        var sourceSet = sourceIds.ToHashSet(StringComparer.Ordinal);
        var authoredSet = authoredIds.ToHashSet(StringComparer.Ordinal);
        foreach (var missing in sourceSet.Except(authoredSet, StringComparer.Ordinal))
            diagnostics.Add(new($"MissingPhotoWorld{idKind}", Relative(root, campaignPath), pointer, $"Required {idKind} {missing} is missing from the campaign catalog."));
        foreach (var extra in authoredSet.Except(sourceSet, StringComparer.Ordinal))
            diagnostics.Add(new($"UnknownPhotoWorld{idKind}", Relative(root, campaignPath), pointer, $"Campaign catalog contains unknown {idKind} {extra}."));
    }

    private static void RequireExactString(
        string root,
        string campaignPath,
        JsonObject authored,
        string authoredProperty,
        JsonObject source,
        string sourceProperty,
        string ownerId,
        string fieldName,
        ICollection<ContentDiagnostic> diagnostics)
    {
        var authoredValue = ReadOptionalString(authored, authoredProperty);
        var sourceValue = ReadOptionalString(source, sourceProperty);
        if (authoredValue is null || sourceValue is null || authoredValue != sourceValue)
            diagnostics.Add(new("PhotoWorldCatalogMismatch", Relative(root, campaignPath), $"/photoBook/worlds/{ownerId}/{authoredProperty}",
                $"PhotoWorld {ownerId} {fieldName} must match its referenced authored spec ({sourceProperty})."));
    }

    private static void RequireAuthoredNodeRole(
        string root,
        string campaignPath,
        IReadOnlyDictionary<string, JsonObject> sourceNodes,
        string? nodeId,
        string requiredRole,
        string worldId,
        ICollection<ContentDiagnostic> diagnostics)
    {
        if (nodeId is null || !sourceNodes.TryGetValue(nodeId, out var node)
            || ReadOptionalString(node, "role") != requiredRole)
            diagnostics.Add(new("InvalidPhotoWorldNodeRole", Relative(root, campaignPath), $"/photoBook/worlds/{worldId}",
                $"World {worldId} must reference an authored {requiredRole} node. This validates the logical node contract only; physical safety remains unverified."));
    }

    private static bool HasDistinctSourceAssignment(IReadOnlyList<string[]> candidates, int index, HashSet<string> used)
    {
        if (index == candidates.Count) return true;
        foreach (var source in candidates[index])
        {
            if (!used.Add(source)) continue;
            if (HasDistinctSourceAssignment(candidates, index + 1, used)) return true;
            used.Remove(source);
        }
        return false;
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
