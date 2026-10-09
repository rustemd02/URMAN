using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;

namespace Urman.Core.Narrative;

/// <summary>
/// Typed PhotoWorlds facts stored inside the existing narrative snapshot. The
/// authored catalog is copied into that one namespace so reducers can validate
/// provenance without introducing a second save or progress owner.
/// </summary>
public static class PhotoWorldState
{
    public const string NamespaceId = "photoworlds-v1";
    public const int CurrentSnapshotSchemaVersion = 2;
    private const int PreviousSnapshotSchemaVersion = 1;
    private const int CatalogSchemaVersion = 1;
    // The runtime WorldId catalog is a later PW-007 consumer. Until that
    // catalog ships, constrain this schema to the five IDs already authored
    // by the fullgame worlds contract rather than accepting arbitrary keys.
    private static readonly HashSet<string> CurrentWorldIds = new(StringComparer.Ordinal)
    {
        "W01", "W02", "W03", "W04", "W05"
    };

    private static readonly HashSet<string> ConditionOperations = new(StringComparer.Ordinal)
    {
        "photoworlds.book.received",
        "photoworlds.photo.state",
        "photoworlds.fact.observed",
        "photoworlds.evidence.confirmed",
        "photoworlds.caption.prepared",
        "photoworlds.prologue.completed"
    };

    private static readonly HashSet<string> EffectOperations = new(StringComparer.Ordinal)
    {
        "photoworlds.book.receive",
        "photoworlds.photo.acquire",
        "photoworlds.photo.mount",
        "photoworlds.photo.back-read",
        "photoworlds.photo.context-known",
        "photoworlds.fact.observe",
        "photoworlds.evidence.verify",
        "photoworlds.prologue.enter",
        "photoworlds.prologue.complete",
        "photoworlds.epilogue.finish"
    };

    public static bool IsConditionOperation(string operation) => ConditionOperations.Contains(operation);
    public static bool IsEffectOperation(string operation) => EffectOperations.Contains(operation);

    public static JsonElement CreateInitial(
        JsonElement authoredCatalog,
        string campaignId,
        string campaignExactVersion)
    {
        RequireObject(authoredCatalog, "PhotoWorlds catalog");
        if (RequiredString(authoredCatalog, "namespaceId") != NamespaceId
            || authoredCatalog.GetProperty("schemaVersion").GetInt32() != CatalogSchemaVersion)
        {
            throw new InvalidDataException("The fullgame PhotoWorlds catalog must use photoworlds-v1 schema 1.");
        }

        if (string.IsNullOrWhiteSpace(campaignId) || string.IsNullOrWhiteSpace(campaignExactVersion))
            throw new ArgumentException("PhotoWorlds snapshots require an authored campaign ID and exact version.");
        ValidateCatalog(authoredCatalog);
        var catalog = JsonNode.Parse(authoredCatalog.GetRawText())!.AsObject();
        var photos = new JsonObject();
        foreach (var authored in authoredCatalog.GetProperty("photos").EnumerateArray())
        {
            var photoId = RequiredString(authored, "id");
            var photo = new JsonObject
            {
                ["acquired"] = false,
                ["sourceId"] = null,
                ["permissionId"] = null,
                ["mounted"] = false,
                ["pageId"] = null,
                ["backRead"] = false,
                ["contextKnown"] = false,
                ["contextSourceId"] = null
            };

            if (authored.TryGetProperty("initial", out var initial))
            {
                var acquired = OptionalBool(initial, "acquired");
                var mounted = OptionalBool(initial, "mounted");
                if (mounted && !acquired)
                    throw new InvalidDataException($"Initially mounted PhotoId {photoId} must also be acquired.");
                if (acquired)
                {
                    photo["acquired"] = true;
                    photo["sourceId"] = RequiredString(authored, "sourceId");
                    photo["permissionId"] = RequiredString(authored, "permissionId");
                }
                if (mounted)
                {
                    photo["mounted"] = true;
                    photo["pageId"] = RequiredString(authored, "pageId");
                }
            }

            photos[photoId] = photo;
        }

        var bookDefinition = authoredCatalog.GetProperty("book");
        var state = new JsonObject
        {
            ["namespaceId"] = NamespaceId,
            ["schemaVersion"] = CurrentSnapshotSchemaVersion,
            ["campaign"] = new JsonObject
            {
                ["id"] = campaignId,
                ["exactVersion"] = campaignExactVersion
            },
            ["catalog"] = catalog,
            ["book"] = new JsonObject
            {
                ["id"] = RequiredString(bookDefinition, "id"),
                ["received"] = false,
                ["sourceId"] = null,
                ["giverId"] = null
            },
            ["photos"] = photos,
            ["facts"] = new JsonObject(),
            ["evidence"] = new JsonObject(),
            ["captions"] = new JsonObject(),
            ["prologue"] = new JsonObject { ["entered"] = false, ["completed"] = false },
            ["epilogue"] = new JsonObject { ["finished"] = false },
            // No physical visit has been committed in the current graph. Null
            // means unknown/uncreated, not a fabricated safe spawn or visit.
            ["visits"] = new JsonObject { ["active"] = null, ["history"] = new JsonObject() },
            ["worlds"] = new JsonObject(),
            ["transition"] = null
        };
        return JsonSerializer.SerializeToElement(state);
    }

    /// <summary>
    /// Preflights and upgrades the PhotoWorlds namespace before a loaded
    /// RuntimeSnapshot is projected into a live session. Schema 1 is the
    /// PW-003 shape; its real narrative/provenance values are retained while
    /// not-yet-produced physical state is represented as absent/null.
    /// Unknown versions are never guessed.
    /// </summary>
    public static JsonElement PrepareSnapshotForLoad(
        JsonElement runtimeState,
        string expectedCampaignId,
        string expectedCampaignExactVersion)
    {
        try
        {
            RequireObject(runtimeState, "Runtime state");
            if (!runtimeState.TryGetProperty("photoworlds", out var persistedWorld))
                throw new InvalidDataException("The loaded campaign is missing its required PhotoWorlds state namespace.");
            RequireObject(persistedWorld, "PhotoWorlds snapshot");
            if (RequiredString(persistedWorld, "namespaceId") != NamespaceId)
                throw new InvalidDataException("The save belongs to an unsupported PhotoWorlds namespace.");

            var root = JsonNode.Parse(runtimeState.GetRawText())!.AsObject();
            var world = root["photoworlds"]!.AsObject();
            var version = RequiredInt32(persistedWorld, "schemaVersion");
            switch (version)
            {
                case PreviousSnapshotSchemaVersion:
                    ValidatePhotoWorldCore(persistedWorld);
                    MigrateV1ToV2(world, expectedCampaignId, expectedCampaignExactVersion);
                    break;
                case CurrentSnapshotSchemaVersion:
                    ValidatePhotoWorldV2(persistedWorld, expectedCampaignId, expectedCampaignExactVersion);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported PhotoWorlds snapshot schema version {version}.");
            }

            return JsonSerializer.SerializeToElement(root);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or JsonException or FormatException)
        {
            throw new InvalidDataException("The PhotoWorlds runtime snapshot is malformed.", exception);
        }
    }

    /// <summary>Rejects malformed/currently unsupported state before kernel restoration or save.</summary>
    public static void ValidateCurrentSnapshot(
        JsonElement runtimeState,
        string? expectedCampaignId = null,
        string? expectedCampaignExactVersion = null,
        bool requireNamespace = false)
    {
        try
        {
            RequireObject(runtimeState, "Runtime state");
            if (!runtimeState.TryGetProperty("photoworlds", out var world))
            {
                if (requireNamespace)
                    throw new InvalidDataException("The active campaign is missing its PhotoWorlds state namespace.");
                return;
            }
            RequireObject(world, "PhotoWorlds snapshot");
            ValidatePhotoWorldV2(world, expectedCampaignId, expectedCampaignExactVersion);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or JsonException or FormatException)
        {
            throw new InvalidDataException("The PhotoWorlds runtime snapshot is malformed.", exception);
        }
    }

    private static void MigrateV1ToV2(JsonObject world, string campaignId, string campaignExactVersion)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || string.IsNullOrWhiteSpace(campaignExactVersion))
            throw new InvalidDataException("A schema-1 PhotoWorlds save requires the loaded campaign identity for migration.");

        // Schema 1 had only narrative/photo provenance. Keep every saved value
        // and add only empty/unknown physical domains; do not infer a visit,
        // action, observation, completion, seed, or safe node.
        world["schemaVersion"] = CurrentSnapshotSchemaVersion;
        world["campaign"] = new JsonObject
        {
            ["id"] = campaignId,
            ["exactVersion"] = campaignExactVersion
        };
        world["visits"] = new JsonObject { ["active"] = null, ["history"] = new JsonObject() };
        world["worlds"] = new JsonObject();
        world["transition"] = null;

        ValidatePhotoWorldV2(JsonSerializer.SerializeToElement(world), campaignId, campaignExactVersion);
    }

    private static void ValidatePhotoWorldV2(
        JsonElement world,
        string? expectedCampaignId,
        string? expectedCampaignExactVersion)
    {
        if (RequiredString(world, "namespaceId") != NamespaceId
            || RequiredInt32(world, "schemaVersion") != CurrentSnapshotSchemaVersion)
            throw new InvalidDataException("The PhotoWorlds snapshot namespace or schema version is unsupported.");

        var campaign = ObjectProperty(world, "campaign");
        var campaignId = RequiredString(campaign, "id");
        var campaignExactVersion = RequiredString(campaign, "exactVersion");
        if ((expectedCampaignId is not null && !StringComparer.Ordinal.Equals(campaignId, expectedCampaignId))
            || (expectedCampaignExactVersion is not null
                && !StringComparer.Ordinal.Equals(campaignExactVersion, expectedCampaignExactVersion)))
            throw new InvalidDataException("The PhotoWorlds snapshot campaign identity does not match the loaded campaign.");

        ValidatePhotoWorldCore(world);
        ValidateVisitState(world, campaignId, campaignExactVersion);
    }

    private static void ValidatePhotoWorldCore(JsonElement world)
    {
        var catalog = ObjectProperty(world, "catalog");
        if (RequiredString(catalog, "namespaceId") != NamespaceId
            || RequiredInt32(catalog, "schemaVersion") != CatalogSchemaVersion)
            throw new InvalidDataException("The embedded PhotoWorlds catalog has an unsupported schema.");
        ValidateCatalog(catalog);

        var bookDefinition = ObjectProperty(catalog, "book");
        var book = ObjectProperty(world, "book");
        if (RequiredString(book, "id") != RequiredString(bookDefinition, "id"))
            throw new InvalidDataException("The PhotoWorlds snapshot book does not match its authored catalog.");
        var bookReceived = RequiredBoolean(book, "received");
        var bookSource = NullableString(book, "sourceId");
        var bookGiver = NullableString(book, "giverId");
        if (bookReceived
                ? bookSource != RequiredString(bookDefinition, "handoffSourceId")
                    || bookGiver != RequiredString(bookDefinition, "giverId")
                : bookSource is not null || bookGiver is not null)
            throw new InvalidDataException("The PhotoWorlds book handoff provenance is inconsistent.");

        var photoDefinitions = catalog.GetProperty("photos").EnumerateArray()
            .ToDictionary(item => RequiredString(item, "id"), StringComparer.Ordinal);
        var photos = ObjectProperty(world, "photos");
        if (photos.EnumerateObject().Count() != photoDefinitions.Count
            || photos.EnumerateObject().Any(item => !photoDefinitions.ContainsKey(item.Name)))
            throw new InvalidDataException("The PhotoWorlds photo state does not match its authored catalog.");
        foreach (var (photoId, definition) in photoDefinitions)
        {
            var photo = ObjectProperty(photos, photoId);
            var acquired = RequiredBoolean(photo, "acquired");
            var mounted = RequiredBoolean(photo, "mounted");
            var backRead = RequiredBoolean(photo, "backRead");
            var contextKnown = RequiredBoolean(photo, "contextKnown");
            var sourceId = NullableString(photo, "sourceId");
            var permissionId = NullableString(photo, "permissionId");
            var pageId = NullableString(photo, "pageId");
            var contextSourceId = NullableString(photo, "contextSourceId");
            if (acquired
                    ? sourceId != RequiredString(definition, "sourceId")
                        || permissionId != RequiredString(definition, "permissionId")
                    : sourceId is not null || permissionId is not null)
                throw new InvalidDataException($"PhotoId {photoId} acquisition provenance is inconsistent.");
            if (mounted
                    ? !acquired || pageId != RequiredString(definition, "pageId")
                    : pageId is not null)
                throw new InvalidDataException($"PhotoId {photoId} mount state is inconsistent.");
            if (backRead && !acquired)
                throw new InvalidDataException($"PhotoId {photoId} cannot be back-read before acquisition.");
            var knownContextSource = contextSourceId is not null
                && definition.GetProperty("contextSourceIds").EnumerateArray()
                    .Any(item => item.GetString() == contextSourceId);
            if (contextKnown
                    ? !acquired || !knownContextSource
                    : contextSourceId is not null)
                throw new InvalidDataException($"PhotoId {photoId} context provenance is inconsistent.");
        }

        var captions = ObjectProperty(world, "captions");
        foreach (var captionProperty in captions.EnumerateObject())
        {
            var photoId = captionProperty.Name;
            var caption = captionProperty.Value;
            if (!photoDefinitions.TryGetValue(photoId, out var definition)
                || !definition.GetProperty("captionable").GetBoolean())
                throw new InvalidDataException($"PhotoId {photoId} cannot carry a prepared caption.");
            var value = caption;
            RequireObject(value, "PhotoWorlds prepared caption");
            var text = RequiredString(value, "text");
            if (text.Length > 280
                || RequiredString(value, "preparedById") != RequiredString(catalog, "playerId")
                || RequiredString(value, "sourceAuthorId") != RequiredString(definition, "sourceAuthorId")
                || RequiredString(value, "sourceId") != RequiredString(definition, "sourceId")
                || RequiredString(value, "pageId") != RequiredString(definition, "pageId")
                || !photos.GetProperty(photoId).GetProperty("acquired").GetBoolean())
                throw new InvalidDataException($"PhotoId {photoId} prepared caption provenance is inconsistent.");
        }

        ValidateFacts(world, catalog);
        ValidateEvidence(world, catalog);
        var prologue = ObjectProperty(world, "prologue");
        var prologueEntered = RequiredBoolean(prologue, "entered");
        var prologueCompleted = RequiredBoolean(prologue, "completed");
        if (prologueCompleted && !prologueEntered)
            throw new InvalidDataException("A completed PhotoWorlds prologue must have an authored entry event.");
        _ = RequiredBoolean(ObjectProperty(world, "epilogue"), "finished");
    }

    private static void ValidateFacts(JsonElement world, JsonElement catalog)
    {
        var definitions = catalog.GetProperty("facts").EnumerateArray()
            .ToDictionary(item => RequiredString(item, "id"), StringComparer.Ordinal);
        var facts = ObjectProperty(world, "facts");
        foreach (var factProperty in facts.EnumerateObject())
        {
            var factId = factProperty.Name;
            var fact = factProperty.Value;
            if (!definitions.TryGetValue(factId, out var definition))
                throw new InvalidDataException($"Unknown PhotoWorlds fact {factId} in the snapshot.");
            RequireObject(fact, "PhotoWorlds fact state");
            var sources = fact.GetProperty("sources");
            if (sources.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"Fact {factId} sources must be an array.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in sources.EnumerateArray())
            {
                var sourceId = RequiredString(source, "sourceId");
                if (!definition.GetProperty("sourceIds").EnumerateArray().Any(item => item.GetString() == sourceId)
                    || RequiredString(source, "provenanceKind") != RequiredString(definition, "provenanceKind")
                    || !seen.Add(sourceId))
                    throw new InvalidDataException($"Fact {factId} has invalid or duplicate provenance.");
            }
        }
    }

    private static void ValidateEvidence(JsonElement world, JsonElement catalog)
    {
        var definitions = catalog.GetProperty("evidence").EnumerateArray()
            .ToDictionary(item => RequiredString(item, "id"), StringComparer.Ordinal);
        var worldObject = WorldObject(world);
        var evidence = ObjectProperty(world, "evidence");
        foreach (var evidenceProperty in evidence.EnumerateObject())
        {
            var evidenceId = evidenceProperty.Name;
            var value = evidenceProperty.Value;
            if (!definitions.TryGetValue(evidenceId, out var definition))
                throw new InvalidDataException($"Unknown PhotoWorlds evidence {evidenceId} in the snapshot.");
            var saved = value;
            RequireObject(saved, "PhotoWorlds evidence state");
            if (RequiredString(saved, "status") != "confirmed")
                throw new InvalidDataException($"PhotoWorlds evidence {evidenceId} has an unsupported status.");
            var constituents = saved.GetProperty("constituents");
            var authored = definition.GetProperty("constituents");
            if (constituents.ValueKind != JsonValueKind.Array || constituents.GetArrayLength() != authored.GetArrayLength())
                throw new InvalidDataException($"PhotoWorlds evidence {evidenceId} has incomplete constituents.");
            var resolvedSources = new HashSet<string>(StringComparer.Ordinal);
            var seenConstituents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var constituent in constituents.EnumerateArray())
            {
                var kind = RequiredString(constituent, "kind");
                var id = RequiredString(constituent, "id");
                var sourceId = RequiredString(constituent, "sourceId");
                var constituentKey = $"{kind}\0{id}";
                var matchesDefinition = authored.EnumerateArray().Any(item =>
                    RequiredString(item, "kind") == kind && RequiredString(item, "id") == id);
                var matchesState = ResolveConstituentCandidates(worldObject, kind, id)
                    .Any(candidate => RequiredString(candidate, "sourceId") == sourceId);
                if (!matchesDefinition || !matchesState || !seenConstituents.Add(constituentKey)
                    || !resolvedSources.Add(sourceId))
                    throw new InvalidDataException($"PhotoWorlds evidence {evidenceId} has unsupported source provenance.");
            }
            var sourceIds = saved.GetProperty("sourceIds");
            var persistedSources = sourceIds.ValueKind == JsonValueKind.Array
                ? sourceIds.EnumerateArray().Select(item =>
                {
                    if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                        throw new InvalidDataException($"PhotoWorlds evidence {evidenceId} source IDs must be non-empty strings.");
                    return item.GetString()!;
                }).ToArray()
                : [];
            if (sourceIds.ValueKind != JsonValueKind.Array
                || persistedSources.Length != persistedSources.Distinct(StringComparer.Ordinal).Count()
                || !persistedSources.ToHashSet(StringComparer.Ordinal).SetEquals(resolvedSources)
                || resolvedSources.Count < definition.GetProperty("minimumDistinctSources").GetInt32())
                throw new InvalidDataException($"PhotoWorlds evidence {evidenceId} does not retain its independent sources.");
        }
    }

    private static void ValidateVisitState(JsonElement world, string campaignId, string campaignExactVersion)
    {
        var visits = ObjectProperty(world, "visits");
        var active = RequiredProperty(visits, "active");
        var visitIds = new HashSet<string>(StringComparer.Ordinal);
        if (active.ValueKind != JsonValueKind.Null)
        {
            ValidateVisit(active, active: true, campaignId, campaignExactVersion);
            if (!visitIds.Add(RequiredString(active, "visitId")))
                throw new InvalidDataException("The active PhotoWorlds visit ID is duplicated.");
        }
        var history = ObjectProperty(visits, "history");
        foreach (var visitProperty in history.EnumerateObject())
        {
            var visitId = visitProperty.Name;
            var visit = visitProperty.Value;
            if (!visitIds.Add(visitId))
                throw new InvalidDataException($"PhotoWorlds visit ID {visitId} is duplicated across active/history state.");
            ValidateVisit(visit, active: false, campaignId, campaignExactVersion);
            if (visitId != RequiredString(visit, "visitId"))
                throw new InvalidDataException("PhotoWorlds visit history keys must match their stable visit IDs.");
        }

        var worlds = ObjectProperty(world, "worlds");
        foreach (var worldProperty in worlds.EnumerateObject())
        {
            var worldId = worldProperty.Name;
            var value = worldProperty.Value;
            RequireObject(value, "PhotoWorlds world state");
            if (!CurrentWorldIds.Contains(worldId))
                throw new InvalidDataException($"Unknown PhotoWorlds WorldId {worldId} in the snapshot.");
            if (RequiredString(value, "worldId") != worldId)
                throw new InvalidDataException("PhotoWorlds world state keys must match their stable WorldIds.");
            var seed = RequiredProperty(value, "seed");
            if (seed.ValueKind != JsonValueKind.Null) _ = RequiredUInt32(value, "seed");
            _ = NullableString(value, "safeNodeId");
            _ = ObjectProperty(value, "actions");
            _ = ObjectProperty(value, "observations");
            _ = ObjectProperty(value, "deltas");
        }

        if (active.ValueKind == JsonValueKind.Object
            && (RequiredString(active, "status") is "active" or "returning"))
        {
            var activeWorldId = RequiredString(active, "worldId");
            if (!worlds.TryGetProperty(activeWorldId, out var activeWorld)
                || NullableString(activeWorld, "safeNodeId") != RequiredString(active, "safeNodeId")
                || RequiredProperty(activeWorld, "seed").ValueKind != JsonValueKind.Number)
                throw new InvalidDataException("An active PhotoWorlds visit requires its saved world state, seed, and safe node.");
            _ = RequiredUInt32(activeWorld, "seed");
        }

        var transition = RequiredProperty(world, "transition");
        if (transition.ValueKind != JsonValueKind.Null)
        {
            RequireObject(transition, "PhotoWorlds transition state");
            _ = RequiredString(transition, "transitionId");
            var visitId = RequiredString(transition, "visitId");
            var kind = RequiredString(transition, "kind");
            var phase = RequiredString(transition, "phase");
            var targetWorldId = RequiredString(transition, "targetWorldId");
            var failureCode = NullableString(transition, "failureCode");
            if (active.ValueKind != JsonValueKind.Object
                || visitId != RequiredString(active, "visitId"))
                throw new InvalidDataException("A PhotoWorlds transition must belong to the active visit.");

            var activeStatus = RequiredString(active, "status");
            var transitionIsCoherent = kind switch
            {
                "enter" when phase is "preparing" or "loading" or "destination-ready" => activeStatus == "prepared",
                "enter" when phase == "photo-active" => activeStatus == "active",
                "return" when phase is "returning" or "village-ready" => activeStatus == "returning",
                "enter" or "return" when phase == "failed" => failureCode is not null
                    && activeStatus == (kind == "enter" ? "prepared" : "returning"),
                _ => false
            };
            if (!transitionIsCoherent || (phase == "failed") != (failureCode is not null))
                throw new InvalidDataException("The PhotoWorlds transition kind, phase, failure, and visit lifecycle are inconsistent.");
            if (kind == "enter"
                    ? targetWorldId != RequiredString(active, "worldId")
                    : targetWorldId != RequiredString(RequiredProperty(active, "returnTicket"), "originWorldId"))
                throw new InvalidDataException("The PhotoWorlds transition target does not match its visit ticket.");
        }
    }

    private static void ValidateVisit(JsonElement visit, bool active, string campaignId, string campaignExactVersion)
    {
        RequireObject(visit, "PhotoWorlds visit record");
        _ = RequiredString(visit, "visitId");
        var worldId = RequiredString(visit, "worldId");
        if (!CurrentWorldIds.Contains(worldId))
            throw new InvalidDataException($"Unknown PhotoWorlds visit WorldId {worldId}.");
        var status = RequiredString(visit, "status");
        if (active
                ? status is not ("prepared" or "active" or "returning")
                : status is not ("returned" or "aborted"))
            throw new InvalidDataException("The PhotoWorlds visit lifecycle status is invalid for its collection.");
        var safeNodeId = NullableString(visit, "safeNodeId");
        if ((status is "active" or "returning") && safeNodeId is null)
            throw new InvalidDataException("An active or returning PhotoWorlds visit requires a confirmed semantic safe-node ID.");
        var ticket = RequiredProperty(visit, "returnTicket");
        if (ticket.ValueKind != JsonValueKind.Null) ValidateReturnTicket(ticket, campaignId, campaignExactVersion);
        else if (status is "prepared" or "active" or "returning")
            throw new InvalidDataException("A live PhotoWorlds visit requires its serialized return ticket.");
    }

    private static void ValidateReturnTicket(JsonElement ticket, string campaignId, string campaignExactVersion)
    {
        RequireObject(ticket, "PhotoWorlds ReturnTicket");
        _ = RequiredString(ticket, "ticketId");
        if (RequiredString(ticket, "campaignId") != campaignId
            || RequiredString(ticket, "contentVersion") != campaignExactVersion)
            throw new InvalidDataException("The PhotoWorlds ReturnTicket campaign identity does not match its snapshot.");
        _ = RequiredString(ticket, "originWorldId");
        _ = RequiredString(ticket, "originAnchorId");
        _ = RequiredString(ticket, "originSafeNode");
        _ = RequiredString(ticket, "movementMode");
        _ = RequiredString(ticket, "originProfileId");
        _ = FiniteNumber(ticket, "rotationY");
        _ = FiniteNumber(ticket, "pitch");
        var position = ObjectProperty(ticket, "position");
        _ = FiniteNumber(position, "x");
        _ = FiniteNumber(position, "y");
        _ = FiniteNumber(position, "z");
        var questContext = ObjectProperty(ticket, "activeQuestContext");
        var audioSnapshot = ObjectProperty(ticket, "originAudioSnapshot");
        _ = questContext;
        _ = audioSnapshot;
        var carryables = RequiredProperty(ticket, "placedCarryableIds");
        if (carryables.ValueKind != JsonValueKind.Array
            || carryables.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())))
            throw new InvalidDataException("PhotoWorlds ReturnTicket carryable IDs must be a string array.");
        if (RequiredInt64(ticket, "preparedAtRevision") < 0)
            throw new InvalidDataException("PhotoWorlds ReturnTicket revision must be non-negative.");
        var worldClock = ObjectProperty(ticket, "worldClock");
        if (RequiredInt64(worldClock, "tick") < 0)
            throw new InvalidDataException("PhotoWorlds ReturnTicket logical clock tick must be non-negative.");
    }

    public static bool EvaluateCondition(JsonElement condition, JsonElement state)
    {
        var operation = RequiredString(condition, "op");
        var world = World(state);
        return operation switch
        {
            "photoworlds.book.received" => world.GetProperty("book").GetProperty("received").GetBoolean(),
            "photoworlds.photo.state" => PhotoStateIs(world, RequiredString(condition, "photoId"), RequiredString(condition, "state")),
            "photoworlds.fact.observed" => HasFact(world, RequiredString(condition, "factId")),
            "photoworlds.evidence.confirmed" => HasEvidence(world, RequiredString(condition, "evidenceId")),
            "photoworlds.caption.prepared" => CaptionPrepared(world, RequiredString(condition, "photoId"),
                condition.TryGetProperty("sourceAuthorId", out var author) ? author.GetString() : null),
            "photoworlds.prologue.completed" => world.GetProperty("prologue").GetProperty("completed").GetBoolean(),
            _ => throw new ArgumentException($"Unknown PhotoWorlds condition operation {operation}.")
        };
    }

    public static void ApplyEffect(
        JsonElement effect,
        JsonObject staged,
        ICollection<EventDraft> events)
    {
        var world = RequireWorld(staged);
        var operation = RequiredString(effect, "op");
        switch (operation)
        {
            case "photoworlds.book.receive":
                ReceiveBook(world, effect, events);
                break;
            case "photoworlds.photo.acquire":
                AcquirePhoto(world, effect, events);
                break;
            case "photoworlds.photo.mount":
                MountPhoto(world, effect, events);
                break;
            case "photoworlds.photo.back-read":
                ReadPhotoBack(world, effect, events);
                break;
            case "photoworlds.photo.context-known":
                LearnPhotoContext(world, effect, events);
                break;
            case "photoworlds.fact.observe":
                ObserveFact(world, effect, events);
                break;
            case "photoworlds.evidence.verify":
                VerifyEvidence(world, effect, events);
                break;
            case "photoworlds.prologue.enter":
                SetPrologue(world, entered: true, events);
                break;
            case "photoworlds.prologue.complete":
                if (!world["prologue"]!["entered"]!.GetValue<bool>())
                    throw new InvalidOperationException("The prologue cannot complete before its authored entry event.");
                SetPrologue(world, entered: false, events);
                break;
            case "photoworlds.epilogue.finish":
                if (world["epilogue"]!["finished"]!.GetValue<bool>()) break;
                var state = JsonSerializer.SerializeToElement(world);
                if (!QuestCompleted(staged, "urman.fullgame:quest/pw-w04")
                    || !HasFact(state, "alsu_meeting_agreed")
                    || !HasFact(state, "outside_leaf_witnessed")
                    || !PhotoStateIs(state, "S-NOW", "acquired"))
                    throw new InvalidOperationException("The PhotoWorlds epilogue requires W04, the shared leaf witness, and Aidar's new photo.");
                world["epilogue"]!["finished"] = true;
                events.Add(new("photoworlds.epilogue.finished", JsonSerializer.SerializeToElement(new { campaignId = NamespaceId })));
                break;
            default:
                throw new ArgumentException($"Unknown PhotoWorlds effect operation {operation}.");
        }
    }

    public static JsonElement PrepareCaption(
        JsonElement state,
        string photoId,
        string captionText,
        string preparedById)
    {
        var world = JsonNode.Parse(World(state).GetRawText())!.AsObject();
        var authored = PhotoDefinition(world, photoId);
        if (!Bool(authored, "captionable"))
            throw new InvalidOperationException($"PhotoId {photoId} does not accept a player-prepared caption.");
        var photo = PhotoState(world, photoId);
        if (!Bool(photo, "acquired"))
            throw new InvalidOperationException("A caption can be prepared only for an acquired, permissioned photo file.");
        if (RequiredString(world["catalog"]!.AsObject(), "playerId") != RequireContentId(preparedById))
            throw new InvalidOperationException("The campaign caption producer must record the actual player character.");
        if (string.IsNullOrWhiteSpace(captionText) || captionText.Length > 280)
            throw new ArgumentException("A prepared caption must contain 1–280 actual characters.", nameof(captionText));

        var sourceAuthorId = RequiredString(authored, "sourceAuthorId");
        var captions = world["captions"]!.AsObject();
        captions[photoId] = new JsonObject
        {
            ["text"] = captionText,
            ["preparedById"] = preparedById,
            ["sourceAuthorId"] = sourceAuthorId,
            ["sourceId"] = RequiredString(authored, "sourceId"),
            ["pageId"] = RequiredString(authored, "pageId")
        };
        return JsonSerializer.SerializeToElement(world);
    }

    public static bool CanPrepareCaption(JsonElement state, string photoId)
    {
        if (!state.TryGetProperty("photoworlds", out var world) || world.ValueKind != JsonValueKind.Object)
            return false;
        if (!world.GetProperty("photos").TryGetProperty(photoId, out var photo)) return false;
        if (!photo.GetProperty("acquired").GetBoolean()) return false;
        var authored = PhotoDefinition(WorldObject(world), photoId);
        return authored.TryGetProperty("captionable", out var captionable) && captionable.ValueKind == JsonValueKind.True;
    }

    private static void ValidateCatalog(JsonElement catalog)
    {
        _ = RequireContentId(RequiredString(catalog, "playerId"));
        var book = catalog.GetProperty("book");
        _ = RequireContentId(RequiredString(book, "handoffSourceId"));
        _ = RequireContentId(RequiredString(book, "giverId"));
        var photoIds = new HashSet<string>(StringComparer.Ordinal);
        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        var photoSources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var photo in catalog.GetProperty("photos").EnumerateArray())
        {
            var id = RequiredString(photo, "id");
            if (!photoIds.Add(id)) throw new InvalidDataException($"Duplicate PhotoId {id} in PhotoWorlds catalog.");
            var page = RequiredString(photo, "pageId");
            if (!pageIds.Add(page)) throw new InvalidDataException($"Duplicate PageId {page} in PhotoWorlds catalog.");
            var sourceId = RequireContentId(RequiredString(photo, "sourceId"));
            photoSources.Add(id, sourceId);
            _ = RequireContentId(RequiredString(photo, "permissionId"));
            if (photo.TryGetProperty("sourceAuthorId", out var author)) _ = RequireContentId(author.GetString()!);
            foreach (var source in photo.GetProperty("contextSourceIds").EnumerateArray()) _ = RequireContentId(source.GetString()!);
            if (photo.GetProperty("captionable").GetBoolean() && !photo.TryGetProperty("sourceAuthorId", out _))
                throw new InvalidDataException($"Captionable PhotoId {id} must have a catalogued source author.");
            if (photo.TryGetProperty("initial", out var initial)
                && OptionalBool(initial, "mounted") && !OptionalBool(initial, "acquired"))
                throw new InvalidDataException($"Initially mounted PhotoId {id} must also be acquired.");
        }

        var factIds = new HashSet<string>(StringComparer.Ordinal);
        var factSources = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var fact in catalog.GetProperty("facts").EnumerateArray())
        {
            var id = RequiredString(fact, "id");
            if (!factIds.Add(id)) throw new InvalidDataException("Duplicate PhotoWorlds fact ID.");
            var sources = fact.GetProperty("sourceIds").EnumerateArray()
                .Select(source => RequireContentId(source.GetString()!)).ToArray();
            if (sources.Length == 0 || sources.Distinct(StringComparer.Ordinal).Count() != sources.Length)
                throw new InvalidDataException("A PhotoWorlds fact requires unique authored provenance sources.");
            factSources.Add(id, sources);
        }

        var evidenceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evidence in catalog.GetProperty("evidence").EnumerateArray())
        {
            if (!evidenceIds.Add(RequiredString(evidence, "id"))) throw new InvalidDataException("Duplicate PhotoWorlds evidence ID.");
            var minimumDistinctSources = evidence.GetProperty("minimumDistinctSources").GetInt32();
            var constituents = evidence.GetProperty("constituents").EnumerateArray().ToArray();
            if (minimumDistinctSources < 2 || minimumDistinctSources > constituents.Length)
                throw new InvalidDataException("Outside evidence requires enough independent typed constituents.");
            var seenConstituents = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<string[]>();
            foreach (var constituent in constituents)
            {
                var kind = RequiredString(constituent, "kind");
                var id = RequiredString(constituent, "id");
                if (!seenConstituents.Add($"{kind}:{id}"))
                    throw new InvalidDataException($"Outside evidence repeats constituent {kind}:{id}.");
                if (kind == "fact")
                {
                    if (!factSources.TryGetValue(id, out var sources))
                        throw new InvalidDataException($"Outside evidence references unknown fact {id}.");
                    candidates.Add(sources);
                }
                else if (kind is "photo-acquired" or "photo-back-read")
                {
                    if (!photoSources.TryGetValue(id, out var source))
                        throw new InvalidDataException($"Outside evidence references unknown PhotoId {id}.");
                    candidates.Add([source]);
                }
                else if (kind != "fact")
                {
                    throw new InvalidDataException($"Unknown outside-evidence constituent kind {kind}.");
                }
            }

            if (!HasDistinctSourceAssignment(candidates, 0, new HashSet<string>(StringComparer.Ordinal)))
                throw new InvalidDataException($"Outside evidence {RequiredString(evidence, "id")} cannot resolve each constituent from an independent source.");
        }

        if (catalog.TryGetProperty("requiredAnchors", out var anchors)
            && anchors.EnumerateArray().Select(item => item.GetString()).Distinct(StringComparer.Ordinal).Count() != anchors.GetArrayLength())
            throw new InvalidDataException("PhotoWorlds semantic anchors must be unique.");
    }

    private static void ReceiveBook(JsonObject world, JsonElement effect, ICollection<EventDraft> events)
    {
        var bookDefinition = world["catalog"]!["book"]!.AsObject();
        var sourceId = RequireContentId(RequiredString(effect, "sourceId"));
        var giverId = RequireContentId(RequiredString(effect, "giverId"));
        if (sourceId != RequiredString(bookDefinition, "handoffSourceId") || giverId != RequiredString(bookDefinition, "giverId"))
            throw new InvalidOperationException("The book must be received through its authored family handoff.");
        var book = world["book"]!.AsObject();
        if (Bool(book, "received")) return;
        book["received"] = true;
        book["sourceId"] = sourceId;
        book["giverId"] = giverId;
        events.Add(new("photoworlds.book.received", JsonSerializer.SerializeToElement(new { bookId = RequiredString(book, "id"), sourceId, giverId })));
    }

    private static void AcquirePhoto(JsonObject world, JsonElement effect, ICollection<EventDraft> events)
    {
        var photoId = RequiredString(effect, "photoId");
        var authored = PhotoDefinition(world, photoId);
        var sourceId = RequireContentId(RequiredString(effect, "sourceId"));
        var permissionId = RequireContentId(RequiredString(effect, "permissionId"));
        if (sourceId != RequiredString(authored, "sourceId") || permissionId != RequiredString(authored, "permissionId"))
            throw new InvalidOperationException($"PhotoId {photoId} acquisition source or permission does not match its authored catalog.");
        RequireBookIfNeeded(world, authored, photoId);
        var photo = PhotoState(world, photoId);
        if (Bool(photo, "acquired"))
        {
            if (String(photo, "sourceId") != sourceId || String(photo, "permissionId") != permissionId)
                throw new InvalidOperationException($"PhotoId {photoId} is already owned from a different provenance source.");
            return;
        }
        photo["acquired"] = true;
        photo["sourceId"] = sourceId;
        photo["permissionId"] = permissionId;
        events.Add(new("photoworlds.photo.acquired", JsonSerializer.SerializeToElement(new { photoId, sourceId, permissionId })));
    }

    private static void MountPhoto(JsonObject world, JsonElement effect, ICollection<EventDraft> events)
    {
        var photoId = RequiredString(effect, "photoId");
        var pageId = RequiredString(effect, "pageId");
        var authored = PhotoDefinition(world, photoId);
        var photo = PhotoState(world, photoId);
        if (!Bool(photo, "acquired")) throw new InvalidOperationException($"PhotoId {photoId} cannot be mounted before acquisition.");
        if (Bool(authored, "requiresBookHandoff") && !Bool(world["book"]!.AsObject(), "received"))
            throw new InvalidOperationException("A family print cannot be mounted before the book is handed to the player.");
        if (pageId != RequiredString(authored, "pageId"))
            throw new InvalidOperationException($"PhotoId {photoId} cannot be mounted on unrelated PageId {pageId}.");
        if (Bool(photo, "mounted"))
        {
            if (String(photo, "pageId") != pageId) throw new InvalidOperationException($"PhotoId {photoId} is already mounted on another page.");
            return;
        }
        photo["mounted"] = true;
        photo["pageId"] = pageId;
        events.Add(new("photoworlds.photo.mounted", JsonSerializer.SerializeToElement(new { photoId, pageId })));
    }

    private static void ReadPhotoBack(JsonObject world, JsonElement effect, ICollection<EventDraft> events)
    {
        var photoId = RequiredString(effect, "photoId");
        RequireBookIfNeeded(world, PhotoDefinition(world, photoId), photoId);
        var photo = PhotoState(world, photoId);
        if (!Bool(photo, "acquired")) throw new InvalidOperationException($"PhotoId {photoId} has not been acquired.");
        if (Bool(photo, "backRead")) return;
        photo["backRead"] = true;
        events.Add(new("photoworlds.photo.back-read", JsonSerializer.SerializeToElement(new { photoId, sourceId = String(photo, "sourceId") })));
    }

    private static void LearnPhotoContext(JsonObject world, JsonElement effect, ICollection<EventDraft> events)
    {
        var photoId = RequiredString(effect, "photoId");
        var sourceId = RequireContentId(RequiredString(effect, "sourceId"));
        var authored = PhotoDefinition(world, photoId);
        RequireBookIfNeeded(world, authored, photoId);
        if (!authored.GetProperty("contextSourceIds").EnumerateArray().Any(value => value.GetString() == sourceId))
            throw new InvalidOperationException($"Source {sourceId} is not an authored context source for PhotoId {photoId}.");
        var photo = PhotoState(world, photoId);
        if (!Bool(photo, "acquired")) throw new InvalidOperationException($"PhotoId {photoId} has not been acquired.");
        if (Bool(photo, "contextKnown")) return;
        photo["contextKnown"] = true;
        photo["contextSourceId"] = sourceId;
        events.Add(new("photoworlds.photo.context-known", JsonSerializer.SerializeToElement(new { photoId, sourceId })));
    }

    private static void ObserveFact(JsonObject world, JsonElement effect, ICollection<EventDraft> events)
    {
        var factId = RequiredString(effect, "factId");
        var sourceId = RequireContentId(RequiredString(effect, "sourceId"));
        var factDefinition = FactDefinition(world, factId);
        var provenanceKind = RequiredString(factDefinition, "provenanceKind");
        if (!factDefinition.GetProperty("sourceIds").EnumerateArray().Any(value => value.GetString() == sourceId))
            throw new InvalidOperationException($"Source {sourceId} is not an authored provenance source for fact {factId}.");
        var facts = world["facts"]!.AsObject();
        var fact = facts[factId] as JsonObject ?? new JsonObject { ["sources"] = new JsonArray() };
        var sources = fact["sources"]!.AsArray();
        if (!sources.OfType<JsonObject>().Any(item => String(item, "sourceId") == sourceId))
        {
            sources.Add(new JsonObject { ["sourceId"] = sourceId, ["provenanceKind"] = provenanceKind });
            facts[factId] = fact;
            events.Add(new("photoworlds.fact.observed", JsonSerializer.SerializeToElement(new { factId, sourceId, provenanceKind })));
        }
    }

    private static void VerifyEvidence(JsonObject world, JsonElement effect, ICollection<EventDraft> events)
    {
        var evidenceId = RequiredString(effect, "evidenceId");
        var definition = EvidenceDefinition(world, evidenceId);
        var evidenceMap = world["evidence"]!.AsObject();
        if (evidenceMap[evidenceId] is JsonObject current && String(current, "status") == "confirmed") return;

        var proof = definition.GetProperty("constituents").EnumerateArray()
            .Select((constituent, index) => (Index: index, Candidates: ResolveConstituentCandidates(world,
                RequiredString(constituent, "kind"), RequiredString(constituent, "id"))))
            .ToArray();
        if (proof.Any(item => item.Candidates.Count == 0))
            throw new InvalidOperationException($"Outside evidence {evidenceId} is missing one or more typed constituents.");
        var orderedProof = proof.OrderBy(item => item.Candidates.Count).ThenBy(item => item.Index).ToArray();
        var selected = new JsonObject[proof.Length];
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        if (!ResolveIndependentSources(orderedProof, 0, selected, sourceIds))
            throw new InvalidOperationException($"Outside evidence {evidenceId} cannot resolve each constituent from an independent source.");
        var resolved = new JsonArray(selected.Select(item => (JsonNode?)item.DeepClone()).ToArray());
        if (sourceIds.Count < definition.GetProperty("minimumDistinctSources").GetInt32())
            throw new InvalidOperationException($"Outside evidence {evidenceId} does not meet its independent source threshold.");

        evidenceMap[evidenceId] = new JsonObject
        {
            ["status"] = "confirmed",
            ["constituents"] = resolved,
            ["sourceIds"] = new JsonArray(sourceIds.Order(StringComparer.Ordinal).Select(source => (JsonNode?)JsonValue.Create(source)).ToArray())
        };
        events.Add(new("photoworlds.evidence.confirmed", JsonSerializer.SerializeToElement(new { evidenceId, sourceIds = sourceIds.Order(StringComparer.Ordinal).ToArray() })));
    }

    private static IReadOnlyList<JsonObject> ResolveConstituentCandidates(JsonObject world, string kind, string id)
    {
        if (kind == "fact")
        {
            var facts = world["facts"]!.AsObject();
            if (facts[id] is not JsonObject fact) return [];
            return fact["sources"]!.AsArray().OfType<JsonObject>().OrderBy(item => String(item, "sourceId"), StringComparer.Ordinal)
                .Select(source => new JsonObject
                {
                    ["kind"] = kind,
                    ["id"] = id,
                    ["sourceId"] = String(source, "sourceId"),
                    ["provenanceKind"] = String(source, "provenanceKind")
                }).ToArray();
        }

        if (kind is "photo-acquired" or "photo-back-read")
        {
            if (!world["photos"]!.AsObject().ContainsKey(id)) return [];
            var photo = PhotoState(world, id);
            var required = kind == "photo-acquired" ? "acquired" : "backRead";
            if (!Bool(photo, required)) return [];
            var definition = PhotoDefinition(world, id);
            return [new JsonObject
            {
                ["kind"] = kind,
                ["id"] = id,
                ["sourceId"] = String(photo, "sourceId"),
                ["permissionId"] = String(photo, "permissionId"),
                ["provenanceKind"] = RequiredString(definition, "provenanceKind")
            }];
        }

        return [];
    }

    private static bool ResolveIndependentSources(
        IReadOnlyList<(int Index, IReadOnlyList<JsonObject> Candidates)> proof,
        int position,
        JsonObject[] selected,
        HashSet<string> usedSources)
    {
        if (position == proof.Count) return true;
        var item = proof[position];
        foreach (var candidate in item.Candidates)
        {
            var sourceId = RequiredString(candidate, "sourceId");
            if (!usedSources.Add(sourceId)) continue;
            selected[item.Index] = candidate;
            if (ResolveIndependentSources(proof, position + 1, selected, usedSources)) return true;
            selected[item.Index] = null!;
            usedSources.Remove(sourceId);
        }
        return false;
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

    private static void RequireBookIfNeeded(JsonObject world, JsonElement authoredPhoto, string photoId)
    {
        if (Bool(authoredPhoto, "requiresBookHandoff") && !Bool(world["book"]!.AsObject(), "received"))
            throw new InvalidOperationException($"PhotoId {photoId} cannot be used before the family book is handed to the player.");
    }

    private static void SetPrologue(JsonObject world, bool entered, ICollection<EventDraft> events)
    {
        var prologue = world["prologue"]!.AsObject();
        if (entered)
        {
            if (Bool(prologue, "entered")) return;
            prologue["entered"] = true;
            events.Add(new("photoworlds.prologue.entered", JsonSerializer.SerializeToElement(new { campaignId = NamespaceId })));
            return;
        }
        if (Bool(prologue, "completed")) return;
        prologue["completed"] = true;
        events.Add(new("photoworlds.prologue.completed", JsonSerializer.SerializeToElement(new { campaignId = NamespaceId })));
    }

    private static bool PhotoStateIs(JsonElement world, string photoId, string state)
    {
        var photo = world.GetProperty("photos").TryGetProperty(photoId, out var actual) ? actual : default;
        return state switch
        {
            "acquired" => photo.ValueKind == JsonValueKind.Object && photo.GetProperty("acquired").GetBoolean(),
            "mounted" => photo.ValueKind == JsonValueKind.Object && photo.GetProperty("mounted").GetBoolean(),
            "back-read" => photo.ValueKind == JsonValueKind.Object && photo.GetProperty("backRead").GetBoolean(),
            "context-known" => photo.ValueKind == JsonValueKind.Object && photo.GetProperty("contextKnown").GetBoolean(),
            _ => throw new ArgumentException($"Unknown PhotoWorlds photo state {state}.")
        };
    }

    private static bool HasFact(JsonElement world, string id) =>
        world.GetProperty("facts").TryGetProperty(id, out var fact)
        && fact.TryGetProperty("sources", out var sources)
        && sources.ValueKind == JsonValueKind.Array && sources.GetArrayLength() > 0;

    private static bool HasEvidence(JsonElement world, string id) =>
        world.GetProperty("evidence").TryGetProperty(id, out var evidence)
        && evidence.TryGetProperty("status", out var status) && status.GetString() == "confirmed";

    private static bool CaptionPrepared(JsonElement world, string photoId, string? requiredSourceAuthorId)
    {
        if (!world.GetProperty("captions").TryGetProperty(photoId, out var caption)) return false;
        var text = caption.GetProperty("text").GetString();
        var preparedBy = caption.GetProperty("preparedById").GetString();
        var sourceAuthor = caption.GetProperty("sourceAuthorId").GetString();
        var photo = PhotoState(world, photoId);
        return !string.IsNullOrWhiteSpace(text)
            && preparedBy == RequiredString(world.GetProperty("catalog"), "playerId")
            && sourceAuthor == RequiredString(PhotoDefinition(world, photoId), "sourceAuthorId")
            && (requiredSourceAuthorId is null || sourceAuthor == requiredSourceAuthorId)
            && photo.GetProperty("acquired").GetBoolean();
    }

    private static JsonObject PhotoState(JsonObject world, string photoId) =>
        world["photos"]![photoId]?.AsObject() ?? throw new InvalidDataException($"Unknown PhotoId {photoId} in the runtime snapshot.");

    private static JsonElement PhotoState(JsonElement world, string photoId) =>
        world.GetProperty("photos").TryGetProperty(photoId, out var photo)
            ? photo
            : throw new InvalidDataException($"Unknown PhotoId {photoId} in the runtime snapshot.");

    private static JsonElement PhotoDefinition(JsonObject world, string photoId) => CatalogEntry(world, "photos", "id", photoId);
    private static JsonElement PhotoDefinition(JsonElement world, string photoId) => CatalogEntry(world, "photos", "id", photoId);
    private static JsonElement FactDefinition(JsonObject world, string factId) => CatalogEntry(world, "facts", "id", factId);
    private static JsonElement EvidenceDefinition(JsonObject world, string evidenceId) => CatalogEntry(world, "evidence", "id", evidenceId);

    private static JsonElement CatalogEntry(JsonObject world, string collection, string key, string value)
    {
        var worldElement = JsonSerializer.SerializeToElement(world);
        return CatalogEntry(worldElement, collection, key, value);
    }

    private static JsonElement CatalogEntry(JsonElement world, string collection, string key, string value)
    {
        var catalog = world.GetProperty("catalog");
        foreach (var item in catalog.GetProperty(collection).EnumerateArray())
            if (item.TryGetProperty(key, out var actual) && actual.GetString() == value) return item;
        throw new InvalidDataException($"Unknown PhotoWorlds {collection} entry {value}.");
    }

    private static bool QuestCompleted(JsonObject state, string questId) =>
        state["quests"] is JsonObject quests
        && quests[questId] is JsonObject quest
        && String(quest, "status") == "completed";

    private static JsonElement World(JsonElement state) =>
        state.TryGetProperty("photoworlds", out var world) && world.ValueKind == JsonValueKind.Object
            ? world
            : throw new InvalidDataException("The active campaign has no PhotoWorlds snapshot namespace.");

    private static JsonObject RequireWorld(JsonObject state) =>
        state["photoworlds"] as JsonObject ?? throw new InvalidDataException("The active campaign has no PhotoWorlds snapshot namespace.");

    private static JsonObject WorldObject(JsonElement world) =>
        JsonNode.Parse(world.GetRawText())!.AsObject();

    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException($"{label} must be an object.");
    }

    private static JsonElement ObjectProperty(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{property} must be an object in the PhotoWorlds snapshot.");
        return result;
    }

    private static JsonElement RequiredProperty(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result)
            ? result
            : throw new InvalidDataException($"{property} is required in the PhotoWorlds snapshot.");

    private static bool RequiredBoolean(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"{property} must be a boolean in the PhotoWorlds snapshot.");
        return result.GetBoolean();
    }

    private static string? NullableString(JsonElement value, string property)
    {
        var result = RequiredProperty(value, property);
        return result.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String when !string.IsNullOrWhiteSpace(result.GetString()) => result.GetString(),
            _ => throw new InvalidDataException($"{property} must be a non-empty string or null in the PhotoWorlds snapshot.")
        };
    }

    private static int RequiredInt32(JsonElement value, string property)
    {
        var result = RequiredProperty(value, property);
        return result.ValueKind == JsonValueKind.Number && result.TryGetInt32(out var parsed)
            ? parsed
            : throw new InvalidDataException($"{property} must be an integer in the PhotoWorlds snapshot.");
    }

    private static long RequiredInt64(JsonElement value, string property)
    {
        var result = RequiredProperty(value, property);
        return result.ValueKind == JsonValueKind.Number && result.TryGetInt64(out var parsed)
            ? parsed
            : throw new InvalidDataException($"{property} must be an integer in the PhotoWorlds snapshot.");
    }

    private static uint RequiredUInt32(JsonElement value, string property)
    {
        var result = RequiredProperty(value, property);
        return result.ValueKind == JsonValueKind.Number && result.TryGetUInt32(out var parsed)
            ? parsed
            : throw new InvalidDataException($"{property} must be an unsigned 32-bit integer in the PhotoWorlds snapshot.");
    }

    private static double FiniteNumber(JsonElement value, string property)
    {
        var result = RequiredProperty(value, property);
        if (result.ValueKind != JsonValueKind.Number || !result.TryGetDouble(out var parsed) || !double.IsFinite(parsed))
            throw new InvalidDataException($"{property} must be finite in the PhotoWorlds snapshot.");
        return parsed;
    }

    private static string RequiredString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(result.GetString())
            ? result.GetString()!
            : throw new ArgumentException($"{property} must be a non-empty string.");

    private static string RequiredString(JsonObject value, string property) =>
        value[property] is JsonValue item && item.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text : throw new ArgumentException($"{property} must be a non-empty string.");

    private static string? String(JsonObject value, string property) =>
        value[property] is JsonValue item && item.TryGetValue<string>(out var text) ? text : null;

    private static bool Bool(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.True;

    private static bool Bool(JsonObject value, string property) =>
        value[property] is JsonValue item && item.TryGetValue<bool>(out var result) && result;

    private static bool OptionalBool(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.True;

    private static string RequireContentId(string value) => new ContentId(value).Value;
}
