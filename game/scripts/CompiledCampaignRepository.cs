using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Narrative;
using Urman.Core.Quests;
using Urman.Content.Resolvers;

namespace Urman.Godot;

public sealed record DocumentImageContent(string AssetId, string ResourcePath, string Description);

public sealed record OldPcDocumentContent(
    string Id,
    string Title,
    string BodyMarkdown,
    string Section,
    IReadOnlyList<string> SearchTerms,
    IReadOnlyList<string> SuggestedTerms,
    IReadOnlyList<string> KnowledgeRefs,
    IReadOnlyList<JsonElement> AccessConditions,
    IReadOnlyList<DocumentImageContent>? Images = null);

public sealed record ResolvedJournalEntry(
    string EntryId,
    string SourceId,
    string Title,
    string Body,
    string SourceTitle,
    IReadOnlyList<DocumentImageContent>? Images = null,
    string? Status = null);

/// <summary>
/// Read-only presentation projection of a quest objective that is currently
/// active in the runtime kernel. The journal may display it, but it never
/// creates, completes or mutates an objective.
/// </summary>
public sealed record ResolvedObjectiveEntry(
    string QuestId,
    string ObjectiveId,
    string Title);

/// <summary>
/// Read-only vocabulary definition exposed to presentation projections. The
/// learned status still lives exclusively in the runtime narrative state.
/// </summary>
public sealed record VocabularyEntryContent(
    string Id,
    string Term,
    string Language,
    string Meaning,
    string StartingKnowledge = "none");

public sealed record ResolvedVocabularyEntry(
    string Id,
    string Term,
    string Language,
    string Meaning,
    string Status)
{
    // These fields are a read-only projection of the first runtime source; the
    // vocabulary status and source remain owned by the narrative state.
    public string SourceId { get; init; } = string.Empty;
    public string SourceTitle { get; init; } = string.Empty;
    public IReadOnlyList<string> Examples { get; init; } = [];
}

public sealed record VocabularySourceContent(string Id, string Title, string Text);

public sealed record CompiledDocumentContent(
    string Id,
    string Title,
    string BodyMarkdown,
    IReadOnlyList<string> KnowledgeRefs,
    IReadOnlyList<JsonElement> AccessConditions,
    IReadOnlyList<JsonElement> OpenEffects,
    IReadOnlyList<DocumentImageContent>? Images = null);

internal sealed record JournalSourceContent(string Id, string Title, string Body,
    IReadOnlyList<DocumentImageContent>? Images = null);

public sealed record CompiledJournalActionContent(IReadOnlyList<string> SourceIds, string ResultTextId);

public sealed record CompiledInteractionContent(
    string SourceSceneId,
    string Id,
    string LabelTextId,
    JsonElement Conditions,
    JsonElement Effects,
    string? TargetSceneId,
    string? TargetDialogueId,
    string? TargetDocumentId,
    CompiledJournalActionContent? JournalAction = null,
    IReadOnlyList<string>? WorldLocations = null,
    string? TargetJournalEntryId = null);

public sealed record CompiledSceneContent(
    string Id,
    string? EntryAnchorId,
    JsonElement EntryConditions,
    JsonElement OnEnter,
    JsonElement OnExit,
    IReadOnlyList<CompiledInteractionContent> Interactions);

public sealed record CompiledDialogueChoiceContent(
    string Id,
    string TextId,
    JsonElement Conditions,
    JsonElement Effects,
    string? NextNodeId);

public sealed record CompiledDialogueNodeContent(
    string Id,
    string SpeakerRole,
    string TextId,
    JsonElement Conditions,
    JsonElement Effects,
    IReadOnlyList<CompiledDialogueChoiceContent> Choices);

public sealed record CompiledDialogueEntryContent(string NodeId, JsonElement Conditions);

public sealed record CompiledDialogueContent(
    string Id,
    string StartNodeId,
    IReadOnlyDictionary<string, CompiledDialogueNodeContent> Nodes,
    IReadOnlyList<CompiledDialogueEntryContent> EntryRoutes);

public sealed record CompiledChatChoiceContent(
    string Id,
    string TextId,
    JsonElement Requires,
    IReadOnlyList<string> Reveals,
    string? NextNodeId);

public sealed record CompiledChatNodeContent(
    string Id,
    string From,
    string TextId,
    JsonElement Requires,
    IReadOnlyList<string> Reveals,
    IReadOnlyList<CompiledChatChoiceContent> Choices,
    string? NextNodeId);

public sealed record CompiledChatContent(
    string Id,
    string Title,
    string Presence,
    JsonElement Requires,
    string StartNodeId,
    string FreeTextReply,
    IReadOnlyDictionary<string, CompiledChatNodeContent> Nodes);

public sealed record CompiledHintContent(
    string Id,
    int Level,
    JsonElement Requires,
    string PointsTo,
    string TextId);

public sealed record CompiledQuestContent(string Id, JsonElement Definition);

public sealed class CompiledCampaignRepository
{
    public const string ResourcePath = "res://content/urman.chapter1.compiled.v1.json";

    private readonly IReadOnlyDictionary<string, OldPcDocumentContent> _oldPcById;
    private readonly IReadOnlyDictionary<string, CompiledDocumentContent> _documentsById;
    private readonly IReadOnlyDictionary<string, CompiledSceneContent> _scenesById;
    private readonly IReadOnlyDictionary<string, CompiledInteractionContent> _interactionsById;
    private readonly IReadOnlyDictionary<string, CompiledDialogueContent> _dialoguesById;
    private readonly IReadOnlyDictionary<string, CompiledChatContent> _chatsById;

    private readonly IReadOnlyDictionary<string, CompiledQuestContent> _questsById;
    private readonly IReadOnlyList<CompiledQuestContent> _runtimeQuests;
    private readonly IReadOnlyDictionary<string, JournalSourceContent> _journalSourcesById;
    private readonly TextResolver _texts;
    private readonly AudioResolver _audio;
    private readonly IReadOnlyDictionary<string, string> _knowledgeInitialStatuses;
    private readonly IReadOnlyDictionary<string, string> _vocabularyInitialStatuses;

    private CompiledCampaignRepository(
        string fingerprint,
        string entrypoint,
        IReadOnlyList<CompiledDocumentContent> documents,
        IReadOnlyList<OldPcDocumentContent> oldPcDocuments,
        IReadOnlyList<CompiledSceneContent> scenes,
        IReadOnlyList<CompiledDialogueContent> dialogues,
        IReadOnlyList<CompiledChatContent> chats,
        IReadOnlyList<CompiledHintContent> hints,
        IReadOnlyList<CompiledQuestContent> quests,
        IReadOnlyList<JournalSourceContent> journalSources,
        TextResolver texts,
        AudioResolver audio,
        IReadOnlyDictionary<string, string> knowledgeInitialStatuses,
        IReadOnlyDictionary<string, string> vocabularyInitialStatuses,
        IReadOnlyList<VocabularyEntryContent> vocabularyEntries,
        JsonElement photoWorldCatalog = default,
        IReadOnlyList<string>? activeQuestIds = null)
    {
        CampaignFingerprint = fingerprint;
        Entrypoint = entrypoint;
        Documents = documents;
        _documentsById = documents.ToDictionary(document => document.Id, StringComparer.Ordinal);
        OldPcDocuments = oldPcDocuments;
        _oldPcById = oldPcDocuments.ToDictionary(document => document.Id, StringComparer.Ordinal);
        _scenesById = scenes.ToDictionary(scene => scene.Id, StringComparer.Ordinal);
        PhotoWorldCatalog = photoWorldCatalog;
        if (HasPhotoWorlds) ValidatePhotoWorldAnchors(PhotoWorldCatalog, scenes);
        _interactionsById = scenes.SelectMany(scene => scene.Interactions).ToDictionary(interaction => interaction.Id, StringComparer.Ordinal);
        _dialoguesById = dialogues.ToDictionary(dialogue => dialogue.Id, StringComparer.Ordinal);
        Chats = chats;
        _chatsById = chats.ToDictionary(chat => chat.Id, StringComparer.Ordinal);
        Hints = hints;
        foreach (var chat in chats)
        foreach (var node in chat.Nodes.Values)
        {
            if (node.NextNodeId is { } followUp && !chat.Nodes.ContainsKey(followUp))
                throw new InvalidDataException($"Chat {chat.Id} node {node.Id} leads to unknown node {followUp}.");
            foreach (var choice in node.Choices)
            {
                if (choice.NextNodeId is { } next && !chat.Nodes.ContainsKey(next))
                    throw new InvalidDataException($"Chat {chat.Id} choice {choice.Id} leads to unknown node {next}.");
            }
        }
        _questsById = quests.ToDictionary(quest => quest.Id, StringComparer.Ordinal);
        if (activeQuestIds is null)
        {
            _runtimeQuests = _questsById.Values.OrderBy(quest => quest.Id, StringComparer.Ordinal).ToArray();
        }
        else
        {
            if (activeQuestIds.Count == 0 || activeQuestIds.Distinct(StringComparer.Ordinal).Count() != activeQuestIds.Count)
                throw new InvalidDataException("The compiled activeQuestIds list must be non-empty and unique.");
            _runtimeQuests = activeQuestIds.Select(id => _questsById.TryGetValue(id, out var quest)
                    ? quest
                    : throw new InvalidDataException($"Compiled campaign selects unknown active quest {id}."))
                .ToArray();
        }
        _journalSourcesById = journalSources.ToDictionary(source => source.Id, StringComparer.Ordinal);
        _texts = texts;
        _audio = audio;
        _knowledgeInitialStatuses = knowledgeInitialStatuses;
        _vocabularyInitialStatuses = vocabularyInitialStatuses;
        VocabularyEntries = vocabularyEntries;
        foreach (var interaction in _interactionsById.Values)
        {
            if (interaction.WorldLocations is { } locations
                && (locations.Count == 0 || locations.Distinct(StringComparer.Ordinal).Count() != locations.Count
                    || locations.Any(location => !Act1WorldLayout.ContainsZone(location))
                    || interaction.TargetSceneId is not null || interaction.JournalAction is not null))
                throw new InvalidDataException($"Invalid world interaction {interaction.Id}.");
            if (interaction.TargetJournalEntryId is { } entryId)
            {
                var record = interaction.Effects.EnumerateArray().FirstOrDefault(effect =>
                    effect.GetProperty("op").GetString() == "journal.record"
                    && effect.GetProperty("entryId").GetString() == entryId);
                if (!_journalSourcesById.ContainsKey(entryId) || record.ValueKind == JsonValueKind.Undefined
                    || !_journalSourcesById.ContainsKey(record.GetProperty("sourceId").GetString()!)
                    || interaction.TargetSceneId is not null || interaction.TargetDialogueId is not null
                    || interaction.TargetDocumentId is not null || interaction.JournalAction is not null)
                    throw new InvalidDataException($"Journal target {entryId} must be recorded by {interaction.Id}.");
            }
        }
        foreach (var action in JournalActions)
        {
            var journal = action.JournalAction!;
            if (journal.SourceIds.Count != 2 || journal.SourceIds.Distinct(StringComparer.Ordinal).Count() != 2
                || journal.SourceIds.Any(id => !_journalSourcesById.ContainsKey(id))
                || action.TargetSceneId is not null || action.TargetDialogueId is not null || action.TargetDocumentId is not null)
                throw new InvalidDataException($"Invalid journal action {action.Id}.");
            _ = ResolveText(action.LabelTextId);
            _ = ResolveText(journal.ResultTextId);
        }
    }

    public string CampaignFingerprint { get; }

    public string Entrypoint { get; }

    public JsonElement PhotoWorldCatalog { get; }

    public bool HasPhotoWorlds => PhotoWorldCatalog.ValueKind == JsonValueKind.Object;

    public string? PhotoWorldNamespaceId => HasPhotoWorlds
        ? PhotoWorldCatalog.GetProperty("namespaceId").GetString()
        : null;

    public IReadOnlyList<OldPcDocumentContent> OldPcDocuments { get; }

    public IReadOnlyList<VocabularyEntryContent> VocabularyEntries { get; }

    public IReadOnlyList<CompiledDocumentContent> Documents { get; }

    public CompiledDocumentContent RequireDocument(string documentId) =>
        _documentsById.TryGetValue(documentId, out var document)
            ? document
            : throw new KeyNotFoundException($"Unknown compiled document {documentId}.");

    public OldPcDocumentContent RequireOldPcDocument(string documentId) =>
        _oldPcById.TryGetValue(documentId, out var document)
            ? document
            : throw new KeyNotFoundException($"Unknown old PC document {documentId}.");

    public CompiledSceneContent RequireScene(string sceneId) =>
        _scenesById.TryGetValue(sceneId, out var scene)
            ? scene
            : throw new KeyNotFoundException($"Unknown compiled scene {sceneId}.");

    public bool TryGetInteraction(string interactionId, out CompiledInteractionContent interaction) =>
        _interactionsById.TryGetValue(interactionId, out interaction!);

    public IReadOnlyList<CompiledInteractionContent> JournalActions => _interactionsById.Values
        .Where(action => action.JournalAction is not null).ToArray();

    public IReadOnlyList<CompiledChatContent> Chats { get; }

    public IReadOnlyList<CompiledHintContent> Hints { get; }

    public CompiledChatContent RequireChat(string chatId) =>
        _chatsById.TryGetValue(chatId, out var chat)
            ? chat
            : throw new KeyNotFoundException($"Unknown compiled chat {chatId}.");

    public CompiledDialogueContent RequireDialogue(string dialogueId) =>
        _dialoguesById.TryGetValue(dialogueId, out var dialogue)
            ? dialogue
            : throw new KeyNotFoundException($"Unknown compiled dialogue {dialogueId}.");

    public CompiledQuestContent RequireQuest(string questId) =>
        _questsById.TryGetValue(questId, out var quest)
            ? quest
            : throw new KeyNotFoundException($"Unknown compiled quest {questId}.");

    public IReadOnlyList<CompiledQuestContent> Quests => _questsById.Values
        .OrderBy(quest => quest.Id, StringComparer.Ordinal)
        .ToArray();

    /// <summary>Quest definitions active in this campaign; legacy packs without
    /// an explicit selection retain the previous all-quests behavior.</summary>
    public IReadOnlyList<CompiledQuestContent> RuntimeQuests => _runtimeQuests;

    public string ResolveText(string textId) => _texts.Resolve(textId, "ru").Text;

    private HashSet<string>? _textIdSet;

    /// <summary>
    /// Adaptive Tatar density (ACT1-LANG): an authored variant is a separate
    /// text id "&lt;id&gt;.lvl-some" / "&lt;id&gt;.lvl-fluent". It changes only how a line
    /// is phrased; the base id stays the fabula and the save/knowledge owner.
    /// A line without a variant resolves to the base text for every level.
    /// </summary>
    public string ResolveText(string textId, string languageLevel)
    {
        if (languageLevel is "some" or "fluent")
        {
            var variant = $"{textId}.lvl-{languageLevel}";
            if (TextIdSet.Contains(variant)) return _texts.Resolve(variant, "ru").Text;
        }
        return _texts.Resolve(textId, "ru").Text;
    }

    // ResolverCatalog.Ids(category) copies every id into a fresh array on each call,
    // and the text set is authored content that does not change while a repository
    // lives. The id list, its set and the prefix queries therefore each get one pass
    // over it: TextIdsWithPrefix used to run a full filter, sort and array build on
    // every resident greeting, and ResolveVocabularySource built a fresh array just
    // to ask whether one id is present.
    private IReadOnlyList<string>? _textIds;
    private readonly Dictionary<string, IReadOnlyList<string>> _textIdsByPrefix = new(StringComparer.Ordinal);

    private IReadOnlyList<string> TextIds => _textIds ??= _texts.Ids().ToArray();

    private HashSet<string> TextIdSet => _textIdSet ??= TextIds.ToHashSet(StringComparer.Ordinal);

    /// <summary>Authored text ids that start with a prefix, in id order.</summary>
    public IReadOnlyList<string> TextIdsWithPrefix(string prefix)
    {
        if (_textIdsByPrefix.TryGetValue(prefix, out var cached)) return cached;
        var ids = TextIds.Where(id => id.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        _textIdsByPrefix[prefix] = ids;
        return ids;
    }

    /// <summary>
    /// Resolves only a source that is present in the compiled campaign. This is
    /// intentionally a read-only lookup: an unknown/starting-knowledge source
    /// must not manufacture an example or reveal a locked document.
    /// </summary>
    public VocabularySourceContent? ResolveVocabularySource(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return null;
        if (_documentsById.TryGetValue(sourceId, out var document))
            return new(sourceId, document.Title, document.BodyMarkdown);
        if (TextIdSet.Contains(sourceId))
            return new(sourceId, "Реплика в разговоре", _texts.Resolve(sourceId, "ru").Text);

        var separator = sourceId.LastIndexOf(':');
        if (separator <= 0 || separator == sourceId.Length - 1) return null;
        var dialogueId = sourceId[..separator];
        var nodeId = sourceId[(separator + 1)..];
        if (!_dialoguesById.TryGetValue(dialogueId, out var dialogue)
            || !dialogue.Nodes.TryGetValue(nodeId, out var node)) return null;

        return new(sourceId, $"Диалог · {node.SpeakerRole}", _texts.Resolve(node.TextId, "ru").Text);
    }

    public ResolvedAudio ResolveAudio(string assetId, string outcomeKey) =>
        _audio.Resolve(assetId, new AudioResolveOptions(Locale: "ru", OutcomeKey: outcomeKey));

    public ResolvedJournalEntry ResolveJournalEntry(string entryId, string sourceId)
    {
        if (!_journalSourcesById.TryGetValue(entryId, out var entry))
        {
            throw new KeyNotFoundException($"Unknown journal entry source {entryId}.");
        }

        if (!_journalSourcesById.TryGetValue(sourceId, out var source))
        {
            throw new KeyNotFoundException($"Unknown journal source {sourceId}.");
        }

        return new(entryId, sourceId, entry.Title, entry.Body, source.Title, entry.Images ?? source.Images);
    }

    public IReadOnlyList<OldPcDocumentDescriptor> OldPcDescriptors() => OldPcDocuments
        .Select(document => new OldPcDocumentDescriptor(document.Id, document.Section))
        .ToArray();

    public JsonElement CreateInitialNarrativeState()
    {
        var state = JsonNode.Parse(NarrativeState.CreateInitial().GetRawText())!.AsObject();
        if (HasPhotoWorlds)
            state["photoworlds"] = JsonNode.Parse(PhotoWorldState.CreateInitial(PhotoWorldCatalog).GetRawText());
        var knowledge = state["knowledge"]!.AsObject();
        foreach (var (id, status) in _knowledgeInitialStatuses)
        {
            knowledge[id] = new JsonObject { ["status"] = status };
        }

        var vocabulary = state["vocabulary"]!.AsObject();
        foreach (var (id, status) in _vocabularyInitialStatuses)
        {
            vocabulary[id] = new JsonObject { ["status"] = status };
        }

        state["interactionCount"] = 0;
        state["lastInteraction"] = null;
        var quests = state["quests"]!.AsObject();
        foreach (var quest in RuntimeQuests)
        {
            var currentState = JsonSerializer.SerializeToElement(state);
            var run = QuestLifecycleReducer.CreateRun(
                quest.Definition,
                $"instance:{quest.Id}",
                evaluateCondition: ContentRuleEngine.Evaluate,
                evaluationContext: currentState);
            quests[quest.Id] = JsonNode.Parse(run.Instance.GetRawText());
        }

        return JsonSerializer.SerializeToElement(state);
    }

    /// <summary>Test-only: a smoke test compiles its own pack (URMAN Studio template runs) and points the game at it.</summary>
    internal static string? PackOverrideForTest { get; set; }

    public static CompiledCampaignRepository Load(string resourcePath = ResourcePath)
    {
        if (resourcePath == ResourcePath && PackOverrideForTest is { } overridePath)
        {
            resourcePath = overridePath;
        }

        using var file = global::Godot.FileAccess.Open(resourcePath, global::Godot.FileAccess.ModeFlags.Read);
        if (file is null)
        {
            throw new FileNotFoundException($"Compiled campaign is missing: {resourcePath}.");
        }

        var source = file.GetAsText();
        using var pack = JsonDocument.Parse(source);
        var root = pack.RootElement;
        var packNode = JsonNode.Parse(source)!.AsObject();
        var texts = new TextResolver(packNode);
        var assets = new AssetResolver(packNode, resolveFileUrl: (assetFile, _) =>
            assetFile.StartsWith("res://", StringComparison.Ordinal)
                ? assetFile : $"res://assets/{assetFile.TrimStart('/')}");
        var audio = new AudioResolver(
            packNode,
            textResolver: texts,
            resolveFileUrl: (file, _) => file.StartsWith("res://", StringComparison.Ordinal)
                ? file
                : $"res://assets/{file.TrimStart('/')}");
        var documentRegistry = root.GetProperty("registries").GetProperty("documents")
            .EnumerateArray().Select(document => document.Clone()).ToArray();
        var knowledgeRegistry = root.GetProperty("registries").GetProperty("knowledge")
            .EnumerateArray().Select(knowledge => knowledge.Clone()).ToArray();
        var documents = documentRegistry
            .Select(document => ReadDocument(document, assets, texts))
            .OrderBy(document => document.Id, StringComparer.Ordinal)
            .ToArray();
        var oldPcDocuments = documents
            .Where(document => document.Id.StartsWith("urman.oldpc:document/", StringComparison.Ordinal))
            .Select(document => documentRegistry.Single(source => source.GetProperty("id").GetString() == document.Id))
            .Select(document => ReadOldPcDocument(document, assets, texts))
            .OrderBy(document => document.Id, StringComparer.Ordinal)
            .ToArray();
        var scenes = root.GetProperty("registries").GetProperty("scenes")
            .EnumerateArray()
            .Select(ReadScene)
            .OrderBy(scene => scene.Id, StringComparer.Ordinal)
            .ToArray();
        var knowledge = InitialStatuses(root, "knowledge");
        var vocabulary = InitialStatuses(root, "vocabulary");
        var vocabularyEntries = root.GetProperty("registries").GetProperty("vocabulary")
            .EnumerateArray()
            .Select(ReadVocabulary)
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .ToArray();
        var dialogues = root.GetProperty("registries").GetProperty("dialogues")
            .EnumerateArray()
            .Select(ReadDialogue)
            .ToArray();
        var chats = root.GetProperty("registries").GetProperty("chats")
            .EnumerateArray()
            .Select(ReadChat)
            .OrderBy(chat => chat.Id, StringComparer.Ordinal)
            .ToArray();
        var hints = root.GetProperty("registries").GetProperty("hints")
            .EnumerateArray()
            .Select(hint => new CompiledHintContent(
                hint.GetProperty("id").GetString()!,
                hint.GetProperty("level").GetInt32(),
                hint.GetProperty("requires").Clone(),
                hint.GetProperty("pointsTo").GetString()!,
                hint.GetProperty("textId").GetString()!))
            .OrderBy(hint => hint.Level)
            .ThenBy(hint => hint.Id, StringComparer.Ordinal)
            .ToArray();
        var quests = root.GetProperty("registries").GetProperty("quests")
            .EnumerateArray()
            .Select(quest => new CompiledQuestContent(quest.GetProperty("id").GetString()!, quest.Clone()))
            .OrderBy(quest => quest.Id, StringComparer.Ordinal)
            .ToArray();
        var campaign = root.GetProperty("campaign");
        var photoWorldCatalog = campaign.TryGetProperty("photoBook", out var photoBook) ? photoBook.Clone() : default;
        IReadOnlyList<string>? activeQuestIds = campaign.TryGetProperty("activeQuestIds", out var activeQuests)
            ? activeQuests.EnumerateArray().Select(item => item.GetString()
                ?? throw new InvalidDataException("A compiled activeQuestIds item must be a content ID.")).ToArray()
            : null;
        var journalSources = documentRegistry.Select(document => ReadJournalDocument(document, assets, texts))
            .Concat(knowledgeRegistry.Select(ReadJournalKnowledge))
            .OrderBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();
        return new(
            root.GetProperty("campaignFingerprint").GetString()!,
            root.GetProperty("campaign").GetProperty("entrypoint").GetString()!,
            documents,
            oldPcDocuments,
            scenes,
            dialogues,
            chats,
            hints,
            quests,
            journalSources,
            texts,
            audio,
            knowledge,
            vocabulary,
            vocabularyEntries,
            photoWorldCatalog,
            activeQuestIds);
    }

    private static CompiledDocumentContent ReadDocument(JsonElement document, AssetResolver assets, TextResolver texts) => new(
        document.GetProperty("id").GetString()!,
        Localized(document.GetProperty("title")),
        document.GetProperty("bodyMarkdown").GetString()!,
        Strings(document.GetProperty("knowledgeRefs")),
        document.GetProperty("accessConditions").EnumerateArray().Select(item => item.Clone()).ToArray(),
        document.GetProperty("openEffects").EnumerateArray().Select(item => item.Clone()).ToArray(),
        ReadImages(document, assets, texts));

    private static VocabularyEntryContent ReadVocabulary(JsonElement vocabulary) => new(
        vocabulary.GetProperty("id").GetString()!,
        vocabulary.GetProperty("term").GetString()!,
        vocabulary.GetProperty("language").GetString()!,
        Localized(vocabulary.GetProperty("meaning")),
        vocabulary.TryGetProperty("startingKnowledge", out var startingKnowledge)
            ? startingKnowledge.GetString() ?? "none"
            : "none");

    private static OldPcDocumentContent ReadOldPcDocument(JsonElement document, AssetResolver assets, TextResolver texts)
    {
        var localizedTitle = Localized(document.GetProperty("title"));
        var oldPc = document.GetProperty("oldPc");
        return new(
            document.GetProperty("id").GetString()!,
            localizedTitle,
            document.GetProperty("bodyMarkdown").GetString()!,
            oldPc.GetProperty("pcSection").GetString()!,
            Strings(oldPc.GetProperty("searchTerms")),
            Strings(oldPc.GetProperty("suggestedTerms")),
            Strings(document.GetProperty("knowledgeRefs")),
            document.GetProperty("accessConditions").EnumerateArray().Select(item => item.Clone()).ToArray(),
            ReadImages(document, assets, texts));
    }

    private static JournalSourceContent ReadJournalDocument(JsonElement document, AssetResolver assets, TextResolver texts) => new(
        document.GetProperty("id").GetString()!,
        Localized(document.GetProperty("title")),
        document.GetProperty("bodyMarkdown").GetString()!, ReadImages(document, assets, texts));

    // Images follow the same compiled manifest as the document. Merely resolving
    // them never opens a record, teaches a word or changes the player's journal.
    private static IReadOnlyList<DocumentImageContent> ReadImages(JsonElement document,
        AssetResolver assets, TextResolver texts) => document.GetProperty("assetRefs")
        .EnumerateArray().Select(reference => assets.Resolve(reference.GetString()!))
        .Where(asset => asset.Kind == "image" && asset.MediaType.StartsWith("image/", StringComparison.Ordinal))
        .Select(asset => new DocumentImageContent(asset.AssetId, asset.Url,
            asset.Accessibility.AltTextId is { } alt ? texts.Resolve(alt, "ru").Text : string.Empty))
        .ToArray();

    private static JournalSourceContent ReadJournalKnowledge(JsonElement knowledge) => new(
        knowledge.GetProperty("id").GetString()!,
        Localized(knowledge.GetProperty("title")),
        Localized(knowledge.GetProperty("summary")));

    private static CompiledSceneContent ReadScene(JsonElement scene) => new(
        scene.GetProperty("id").GetString()!,
        scene.TryGetProperty("entryAnchorId", out var entryAnchor) ? entryAnchor.GetString() : null,
        scene.GetProperty("entryConditions").Clone(),
        scene.GetProperty("onEnter").Clone(),
        scene.GetProperty("onExit").Clone(),
        scene.GetProperty("interactions").EnumerateArray().Select(interaction => new CompiledInteractionContent(
        scene.GetProperty("id").GetString()!,
        interaction.GetProperty("id").GetString()!,
            interaction.GetProperty("labelTextId").GetString()!,
            interaction.GetProperty("conditions").Clone(),
            interaction.GetProperty("effects").Clone(),
            interaction.TryGetProperty("targetSceneId", out var sceneId) ? sceneId.GetString() : null,
            interaction.TryGetProperty("targetDialogueId", out var dialogueId) ? dialogueId.GetString() : null,
            interaction.TryGetProperty("targetDocumentId", out var documentId) ? documentId.GetString() : null,
            interaction.TryGetProperty("journalAction", out var journal)
                ? new CompiledJournalActionContent(journal.GetProperty("sourceIds").EnumerateArray().Select(id => id.GetString()!).ToArray(),
                    journal.GetProperty("resultTextId").GetString()!) : null,
            interaction.TryGetProperty("worldLocations", out var locations)
                ? locations.EnumerateArray().Select(location => location.GetString()!).ToArray() : null,
            interaction.TryGetProperty("targetJournalEntryId", out var journalEntry) ? journalEntry.GetString() : null)).ToArray());

    private static void ValidatePhotoWorldAnchors(JsonElement catalog, IReadOnlyList<CompiledSceneContent> scenes)
    {
        var bindings = scenes.Where(scene => scene.EntryAnchorId is not null)
            .GroupBy(scene => scene.EntryAnchorId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var anchor in catalog.GetProperty("requiredAnchors").EnumerateArray().Select(item => item.GetString()!))
            if (!bindings.TryGetValue(anchor, out var owners) || owners.Length != 1)
                throw new InvalidDataException($"PhotoWorlds semantic anchor {anchor} must bind exactly one compiled scene.");
    }

    private static CompiledChatContent ReadChat(JsonElement chat)
    {
        var nodes = chat.GetProperty("nodes").EnumerateArray()
            .Select(node => new CompiledChatNodeContent(
                node.GetProperty("id").GetString()!,
                node.GetProperty("from").GetString()!,
                node.GetProperty("textId").GetString()!,
                node.GetProperty("requires").Clone(),
                node.GetProperty("reveals").EnumerateArray().Select(term => term.GetString()!).ToArray(),
                node.GetProperty("choices").EnumerateArray().Select(choice => new CompiledChatChoiceContent(
                    choice.GetProperty("id").GetString()!,
                    choice.GetProperty("textId").GetString()!,
                    choice.GetProperty("requires").Clone(),
                    choice.GetProperty("reveals").EnumerateArray().Select(term => term.GetString()!).ToArray(),
                    choice.GetProperty("nextNodeId").ValueKind == JsonValueKind.Null
                        ? null : choice.GetProperty("nextNodeId").GetString())).ToArray(),
                node.GetProperty("nextNodeId").ValueKind == JsonValueKind.Null
                    ? null : node.GetProperty("nextNodeId").GetString()))
            .ToDictionary(node => node.Id, StringComparer.Ordinal);
        var id = chat.GetProperty("id").GetString()!;
        var start = chat.GetProperty("startNodeId").GetString()!;
        if (!nodes.ContainsKey(start))
            throw new InvalidDataException($"Chat {id} starts at unknown node {start}.");
        return new(id, Localized(chat.GetProperty("title")), chat.GetProperty("presence").GetString()!,
            chat.GetProperty("requires").Clone(), start, chat.GetProperty("freeTextReply").GetString()!, nodes);
    }

    private static CompiledDialogueContent ReadDialogue(JsonElement dialogue)
    {
        var nodes = dialogue.GetProperty("nodes").EnumerateArray()
            .Select(node => new CompiledDialogueNodeContent(
                node.GetProperty("id").GetString()!,
                node.GetProperty("speakerRole").GetString()!,
                node.GetProperty("textId").GetString()!,
                node.GetProperty("conditions").Clone(),
                node.GetProperty("effects").Clone(),
                node.GetProperty("choices").EnumerateArray().Select(choice => new CompiledDialogueChoiceContent(
                    choice.GetProperty("id").GetString()!,
                    choice.GetProperty("textId").GetString()!,
                    choice.GetProperty("conditions").Clone(),
                    choice.GetProperty("effects").Clone(),
                    choice.TryGetProperty("nextNodeId", out var nextNodeId) ? nextNodeId.GetString() : null)).ToArray()))
            .ToDictionary(node => node.Id, StringComparer.Ordinal);
        var entries = dialogue.TryGetProperty("entryRoutes", out var routes)
            ? routes.EnumerateArray().Select(route => new CompiledDialogueEntryContent(
                route.GetProperty("nodeId").GetString()!, route.GetProperty("conditions").Clone())).ToArray()
            : [];
        if (entries.Any(entry => !nodes.ContainsKey(entry.NodeId)))
            throw new InvalidDataException($"Dialogue {dialogue.GetProperty("id").GetString()} has an unknown entry node.");
        return new(dialogue.GetProperty("id").GetString()!, dialogue.GetProperty("startNodeId").GetString()!, nodes, entries);
    }

    private static IReadOnlyDictionary<string, string> InitialStatuses(JsonElement root, string registry) =>
        root.GetProperty("registries").GetProperty(registry).EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetString()!,
            item => item.GetProperty("initialStatus").GetString()!,
            StringComparer.Ordinal);

    private static IReadOnlyList<string> Strings(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static string Localized(JsonElement value)
    {
        var translations = value.GetProperty("translations");
        return translations.TryGetProperty("ru", out var russian)
            ? russian.GetString()!
            : value.GetProperty("default").GetString()!;
    }
}
