using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Narrative;
using Urman.Core.Quests;
using Urman.Content.Resolvers;

namespace Urman.Godot;

public sealed record OldPcDocumentContent(
    string Id,
    string Title,
    string BodyMarkdown,
    string Section,
    IReadOnlyList<string> SearchTerms,
    IReadOnlyList<string> SuggestedTerms,
    IReadOnlyList<string> KnowledgeRefs,
    IReadOnlyList<JsonElement> AccessConditions);

public sealed record ResolvedJournalEntry(
    string EntryId,
    string SourceId,
    string Title,
    string Body,
    string SourceTitle);

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
    string Meaning);

public sealed record ResolvedVocabularyEntry(
    string Id,
    string Term,
    string Language,
    string Meaning,
    string Status);

public sealed record CompiledDocumentContent(
    string Id,
    string Title,
    string BodyMarkdown,
    IReadOnlyList<string> KnowledgeRefs,
    IReadOnlyList<JsonElement> AccessConditions,
    IReadOnlyList<JsonElement> OpenEffects);

internal sealed record JournalSourceContent(string Id, string Title, string Body);

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

public sealed record CompiledDialogueContent(
    string Id,
    string StartNodeId,
    IReadOnlyDictionary<string, CompiledDialogueNodeContent> Nodes);

public sealed record CompiledQuestContent(string Id, JsonElement Definition);

public sealed class CompiledCampaignRepository
{
    public const string ResourcePath = "res://content/urman.chapter1.compiled.v1.json";

    private readonly IReadOnlyDictionary<string, OldPcDocumentContent> _oldPcById;
    private readonly IReadOnlyDictionary<string, CompiledDocumentContent> _documentsById;
    private readonly IReadOnlyDictionary<string, CompiledSceneContent> _scenesById;
    private readonly IReadOnlyDictionary<string, CompiledInteractionContent> _interactionsById;
    private readonly IReadOnlyDictionary<string, CompiledDialogueContent> _dialoguesById;
    private readonly IReadOnlyDictionary<string, CompiledQuestContent> _questsById;
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
        IReadOnlyList<CompiledQuestContent> quests,
        IReadOnlyList<JournalSourceContent> journalSources,
        TextResolver texts,
        AudioResolver audio,
        IReadOnlyDictionary<string, string> knowledgeInitialStatuses,
        IReadOnlyDictionary<string, string> vocabularyInitialStatuses,
        IReadOnlyList<VocabularyEntryContent> vocabularyEntries)
    {
        CampaignFingerprint = fingerprint;
        Entrypoint = entrypoint;
        Documents = documents;
        _documentsById = documents.ToDictionary(document => document.Id, StringComparer.Ordinal);
        OldPcDocuments = oldPcDocuments;
        _oldPcById = oldPcDocuments.ToDictionary(document => document.Id, StringComparer.Ordinal);
        _scenesById = scenes.ToDictionary(scene => scene.Id, StringComparer.Ordinal);
        _interactionsById = scenes.SelectMany(scene => scene.Interactions).ToDictionary(interaction => interaction.Id, StringComparer.Ordinal);
        _dialoguesById = dialogues.ToDictionary(dialogue => dialogue.Id, StringComparer.Ordinal);
        _questsById = quests.ToDictionary(quest => quest.Id, StringComparer.Ordinal);
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

    public string ResolveText(string textId) => _texts.Resolve(textId, "ru").Text;

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

        return new(entryId, sourceId, entry.Title, entry.Body, source.Title);
    }

    public IReadOnlyList<OldPcDocumentDescriptor> OldPcDescriptors() => OldPcDocuments
        .Select(document => new OldPcDocumentDescriptor(document.Id, document.Section))
        .ToArray();

    public JsonElement CreateInitialNarrativeState()
    {
        var state = JsonNode.Parse(NarrativeState.CreateInitial().GetRawText())!.AsObject();
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
        foreach (var quest in Quests)
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

    public static CompiledCampaignRepository Load(string resourcePath = ResourcePath)
    {
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
            .Select(ReadDocument)
            .OrderBy(document => document.Id, StringComparer.Ordinal)
            .ToArray();
        var oldPcDocuments = documents
            .Where(document => document.Id.StartsWith("urman.oldpc:document/", StringComparison.Ordinal))
            .Select(document => documentRegistry.Single(source => source.GetProperty("id").GetString() == document.Id))
            .Select(ReadOldPcDocument)
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
        var quests = root.GetProperty("registries").GetProperty("quests")
            .EnumerateArray()
            .Select(quest => new CompiledQuestContent(quest.GetProperty("id").GetString()!, quest.Clone()))
            .OrderBy(quest => quest.Id, StringComparer.Ordinal)
            .ToArray();
        var journalSources = documentRegistry.Select(ReadJournalDocument)
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
            quests,
            journalSources,
            texts,
            audio,
            knowledge,
            vocabulary,
            vocabularyEntries);
    }

    private static CompiledDocumentContent ReadDocument(JsonElement document) => new(
        document.GetProperty("id").GetString()!,
        Localized(document.GetProperty("title")),
        document.GetProperty("bodyMarkdown").GetString()!,
        Strings(document.GetProperty("knowledgeRefs")),
        document.GetProperty("accessConditions").EnumerateArray().Select(item => item.Clone()).ToArray(),
        document.GetProperty("openEffects").EnumerateArray().Select(item => item.Clone()).ToArray());

    private static VocabularyEntryContent ReadVocabulary(JsonElement vocabulary) => new(
        vocabulary.GetProperty("id").GetString()!,
        vocabulary.GetProperty("term").GetString()!,
        vocabulary.GetProperty("language").GetString()!,
        Localized(vocabulary.GetProperty("meaning")));

    private static OldPcDocumentContent ReadOldPcDocument(JsonElement document)
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
            document.GetProperty("accessConditions").EnumerateArray().Select(item => item.Clone()).ToArray());
    }

    private static JournalSourceContent ReadJournalDocument(JsonElement document) => new(
        document.GetProperty("id").GetString()!,
        Localized(document.GetProperty("title")),
        document.GetProperty("bodyMarkdown").GetString()!);

    private static JournalSourceContent ReadJournalKnowledge(JsonElement knowledge) => new(
        knowledge.GetProperty("id").GetString()!,
        Localized(knowledge.GetProperty("title")),
        Localized(knowledge.GetProperty("summary")));

    private static CompiledSceneContent ReadScene(JsonElement scene) => new(
        scene.GetProperty("id").GetString()!,
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
        return new(dialogue.GetProperty("id").GetString()!, dialogue.GetProperty("startNodeId").GetString()!, nodes);
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
