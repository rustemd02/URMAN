using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Godot;
using Urman.Core.Capabilities;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Contracts;
using Urman.Core.Determinism;
using Urman.Core.Narrative;
using Urman.Core.Persistence;
using Urman.Core.Quests;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge : Node
{
    private const string OldPcInstanceId = "urman.oldpc:instance/archive-hub";

    [Export]
    public string CurrentZoneId { get; set; } = "village_day";

    [Export]
    public string CurrentSpawnPointId { get; set; } = "arrival";

    [Export]
    public string CampaignResourcePath { get; set; } = CompiledCampaignRepository.ResourcePath;

    private RuntimeKernel? _kernel;
    private CapabilityHost? _capabilities;
    private QuestCapabilitySessionOrchestrator? _questCapabilities;
    private LogicalClock _clock = new();
    private OwnerRngStreams _rngStreams = new(0x55524d41);
    private DeterministicScheduler _scheduler = new();
    private AtomicSaveGameStore? _saveStore;
    private CompiledCampaignRepository _content = null!;
    private QuestRuntimeCoordinator _questCoordinator = null!;
    private long _interactionSequence;
    private double _playTimeSeconds;
    private bool _runtimeStateNotificationQueued;

    /// <summary>
    /// Presentation-only invalidation for physical interaction targets. The
    /// kernel remains the sole state owner; consumers only learn that their
    /// cached availability should be refreshed on the main thread.
    /// </summary>
    public event Action? RuntimeStateChanged;

    public override void _Ready()
    {
        AddToGroup("runtime_bridge");
        _content = CompiledCampaignRepository.Load(CampaignResourcePath);
        _questCoordinator = new QuestRuntimeCoordinator(_content);
        _saveStore = new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://savegames"));
        CreateNewSession();
        _ = InitializeEntrypointAsync();
    }

    public override void _Process(double delta)
    {
        _playTimeSeconds += delta;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("quick_save"))
        {
            QuickSave();
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent.IsActionPressed("quick_load"))
        {
            QuickLoad();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        _runtimeStateNotificationQueued = false;
        RuntimeStateChanged = null;
        _questCapabilities = null;
        _capabilities?.Dispose();
        _capabilities = null;
        _kernel?.Dispose();
        _kernel = null;
    }

    public async void QuickSave()
    {
        _ = await SaveSlotAsync("quick");
    }

    public async Task<bool> SaveSlotAsync(string slot)
    {
        if (_kernel is null || _capabilities is null || _saveStore is null || FindPlayer() is not { } player)
        {
            GD.PushWarning("Quick save is unavailable before the runtime and player are ready.");
            return false;
        }

        try
        {
            var runtime = _kernel.CaptureSnapshot();
            var save = new SaveGameV3(
                SaveGameV3.CurrentSchemaVersion,
                _content.CampaignFingerprint,
                runtime.EventSequence,
                runtime,
                _clock.CaptureSnapshot(),
                _rngStreams.CaptureSnapshot(),
                _scheduler.CaptureSnapshot(),
                _capabilities.CaptureAll(),
                new WorldLocationId(CurrentZoneId),
                new SpawnPointId(CurrentSpawnPointId),
                player.CapturePortableTransform(),
                player.CaptureSettings(),
                _playTimeSeconds);
            await _saveStore.SaveAsync(slot, save);
            GD.Print($"SaveGameV3 written to {_saveStore.SlotPath(slot)}");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"SaveGameV3 write failed: {exception.Message}");
            return false;
        }
    }

    public bool IsSlotAvailable(string slot) =>
        _saveStore is not null
        && (File.Exists(_saveStore.SlotPath(slot)) || File.Exists(_saveStore.BackupPath(slot)));

    /// <summary>
    /// SAVE-003 Continue contract: a slot is loadable when the atomic store
    /// holds a primary or backup payload for it. The load itself re-verifies
    /// the campaign fingerprint and fails safely (state untouched) when the
    /// payload is invalid, so callers can gate a Continue action on this
    /// query without a second save-format owner.
    /// </summary>
    public bool HasLoadableSlot(string slot) => IsSlotAvailable(slot);

    /// <summary>
    /// SAVE-003 New Game contract: begin a fresh narrative session without
    /// deleting any existing slot (Continue must keep working) and without
    /// wiping the player's live user settings. The runtime kernel,
    /// capabilities, logical clock, RNG streams and scheduler are rebuilt
    /// exactly as at first boot, the campaign entrypoint is re-applied, and
    /// the player is placed at the canonical arrival spawn.
    /// </summary>
    public async Task<bool> StartNewGameAsync()
    {
        var player = FindPlayer();
        if (_content is null || player is null)
        {
            GD.PushWarning("New game is unavailable before the runtime and player are ready.");
            return false;
        }

        var preservedSettings = player.CaptureSettings();
        CreateNewSession();
        CurrentZoneId = "village_day";
        CurrentSpawnPointId = "arrival";
        await InitializeEntrypointAsync();

        if (GetTree().GetFirstNodeInGroup("zone_manager") is Main main)
        {
            main.SwitchZone(CurrentZoneId, CurrentSpawnPointId);
        }

        player.ApplySettings(preservedSettings);
        QueueRuntimeStateChanged();
        GD.Print("SaveGameV3 new game session started; existing slots untouched.");
        return true;
    }

    public async void QuickLoad()
    {
        _ = await LoadSlotAsync("quick");
    }

    public async Task<bool> LoadSlotAsync(string slot)
    {
        if (_saveStore is null || FindPlayer() is not { } player)
        {
            GD.PushWarning("Quick load is unavailable before the runtime and player are ready.");
            return false;
        }

        try
        {
            var result = await _saveStore.LoadAsync(slot, _content.CampaignFingerprint);
            RestoreSession(result.Save);
            if (GetTree().GetFirstNodeInGroup("zone_manager") is Main main)
            {
                main.SwitchZone(result.Save.CurrentZone.Value, result.Save.SpawnPoint.Value);
            }

            player.ApplyPortableTransform(result.Save.PlayerTransform);
            player.ApplySettings(result.Save.Settings);
            GD.Print(result.RecoveredFromBackup
                ? "SaveGameV3 restored from the last working backup."
                : "SaveGameV3 restored.");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"SaveGameV3 load failed: {exception.Message}");
            return false;
        }
    }

    public const string CheckpointSlot = "checkpoint";

    /// <summary>
    /// SAVE-004 checkpoint policy: after each of the four stable
    /// investigation beats the runtime writes one rolling checkpoint slot.
    /// No autosave inside dialogue/document/transition states — the
    /// checkpoint fires only when an interaction commit lands the campaign
    /// on one of these scenes.
    /// </summary>
    private static readonly string[] CheckpointScenes =
    [
        "urman.chapter1:scene/evidence-official-death",
        "urman.chapter1:scene/evidence-internal-register",
        "urman.chapter1:scene/evidence-tatarwiki-reread",
        "urman.chapter1:scene/zirat-road"
    ];

    private string? _lastCheckpointScene;
    private bool _checkpointBusy;

    public async Task<bool> DispatchInteractionAsync(string interactionId)
    {
        if (_kernel is null)
        {
            return false;
        }

        if (_content.TryGetInteraction(interactionId, out var interaction))
        {
            var dispatched = await DispatchCompiledInteractionAsync(interaction);
            if (dispatched)
            {
                await SaveCheckpointAsync();
            }

            return dispatched;
        }

        GD.PushWarning($"Ignoring interaction that is not present in the compiled campaign: {interactionId}");
        return false;
    }

    private async Task SaveCheckpointAsync()
    {
        if (_checkpointBusy || _content is null || ActiveSceneId is not { } scene)
        {
            return;
        }

        if (!CheckpointScenes.Contains(scene) || _lastCheckpointScene == scene)
        {
            return;
        }

        _checkpointBusy = true;
        try
        {
            if (await SaveSlotAsync(CheckpointSlot))
            {
                _lastCheckpointScene = scene;
                GD.Print($"checkpoint: auto-saved at {scene}");
            }
        }
        finally
        {
            _checkpointBusy = false;
        }
    }

    public void SetWorldLocation(string zoneId, string spawnPointId)
    {
        _ = new WorldLocationId(zoneId);
        _ = new SpawnPointId(spawnPointId);
        CurrentZoneId = zoneId;
        CurrentSpawnPointId = spawnPointId;
        QueueRuntimeStateChanged();
    }

    public IReadOnlyList<OldPcDocumentContent> OldPcDocuments => _content.OldPcDocuments;

    /// <summary>
    /// Read-only projection of vocabulary already encountered in the shared
    /// narrative state. The UI can show the first Tatar layer without creating
    /// a second dictionary or changing runtime state.
    /// </summary>
    public IReadOnlyList<ResolvedVocabularyEntry> LearnedVocabulary()
    {
        if (_kernel is null)
        {
            return [];
        }

        var state = _kernel.SelectState();
        if (!state.TryGetProperty("vocabulary", out var vocabulary)
            || vocabulary.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return _content.VocabularyEntries
            .Where(entry => vocabulary.TryGetProperty(entry.Id, out var value)
                && value.ValueKind == JsonValueKind.Object
                && value.TryGetProperty("status", out var status)
                && status.GetString() is "guessed" or "confirmed")
            .Select(entry => new ResolvedVocabularyEntry(
                entry.Id,
                entry.Term,
                entry.Language,
                entry.Meaning,
                vocabulary.GetProperty(entry.Id).GetProperty("status").GetString()!))
            .ToArray();
    }

    public JsonElement SelectRuntimeState() => _kernel?.SelectState()
        ?? throw new InvalidOperationException("Runtime kernel is unavailable.");

    public string? ActiveSceneId => _kernel?.SelectState().TryGetProperty("activeScene", out var activeScene) == true
        ? activeScene.GetString()
        : null;

    public bool IsInteractionAvailable(string interactionId)
    {
        if (_kernel is null || !_content.TryGetInteraction(interactionId, out var interaction))
        {
            return false;
        }

        var state = _kernel.SelectState();
        if (!state.TryGetProperty("activeScene", out var activeScene)
            || activeScene.GetString() != interaction.SourceSceneId
            || !ContentRuleEngine.EvaluateAll(interaction.Conditions, state))
        {
            return false;
        }

        return interaction.TargetSceneId is null
            || ContentRuleEngine.EvaluateAll(_content.RequireScene(interaction.TargetSceneId).EntryConditions, state);
    }

    public CompiledDialogueContent RequireDialogue(string dialogueId) => _content.RequireDialogue(dialogueId);

    public CompiledSceneContent RequireScene(string sceneId) => _content.RequireScene(sceneId);

    public CompiledDocumentContent RequireDocument(string documentId) => _content.RequireDocument(documentId);

    public string ResolveText(string textId) => _content.ResolveText(textId);

    public IReadOnlyList<ResolvedJournalEntry> JournalEntries()
    {
        if (_kernel is null)
        {
            return [];
        }

        return _kernel.SelectState().GetProperty("journal").EnumerateArray()
            .Select(item => _content.ResolveJournalEntry(
                item.GetProperty("entryId").GetString()!,
                item.GetProperty("sourceId").GetString()!))
            .ToArray();
    }

    /// <summary>
    /// Returns the active objective titles already derived by the runtime
    /// quest state. This is a presentation projection for JournalUi; it does
    /// not dispatch commands, add journal entries or become a second state
    /// owner.
    /// </summary>
    public IReadOnlyList<ResolvedObjectiveEntry> ActiveObjectives()
    {
        if (_kernel is null)
        {
            return [];
        }

        var state = _kernel.SelectState();
        if (!state.TryGetProperty("quests", out var quests)
            || quests.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var result = new List<ResolvedObjectiveEntry>();
        foreach (var quest in _content.Quests)
        {
            if (!quests.TryGetProperty(quest.Id, out var instance)
                || instance.ValueKind != JsonValueKind.Object
                || !instance.TryGetProperty("status", out var questStatus)
                || questStatus.GetString() != QuestStatuses.Active
                || !instance.TryGetProperty("stageIndex", out var stageIndexValue)
                || !instance.TryGetProperty("objectives", out var objectiveStates))
            {
                continue;
            }

            var stageIndex = stageIndexValue.GetInt32();
            var stages = quest.Definition.GetProperty("stages");
            if (stageIndex < 0 || stageIndex >= stages.GetArrayLength())
            {
                continue;
            }

            var stage = stages[stageIndex];
            var stageId = stage.GetProperty("id").GetString()!;
            if (!objectiveStates.TryGetProperty(stageId, out var stageStates))
            {
                continue;
            }

            foreach (var objective in stage.GetProperty("objectives").EnumerateArray())
            {
                var objectiveId = objective.GetProperty("id").GetString()!;
                if (!stageStates.TryGetProperty(objectiveId, out var objectiveState)
                    || !objectiveState.TryGetProperty("status", out var status)
                    || status.GetString() != ObjectiveStatuses.Active)
                {
                    continue;
                }

                result.Add(new(
                    quest.Id,
                    objectiveId,
                    ResolveText(objective.GetProperty("titleTextId").GetString()!)));
            }
        }

        return result;
    }

    public bool EvaluateConditions(JsonElement conditions) =>
        _kernel is not null && ContentRuleEngine.EvaluateAll(conditions, _kernel.SelectState());

    public async Task<bool> EnterDialogueNodeAsync(string dialogueId, string nodeId)
    {
        var dialogue = _content.RequireDialogue(dialogueId);
        var node = dialogue.Nodes.TryGetValue(nodeId, out var found)
            ? found
            : throw new KeyNotFoundException($"Unknown dialogue node {dialogueId}/{nodeId}.");
        var result = await DispatchContentApplyAsync(
            $"dialogue-node:{dialogueId}:{nodeId}:{Interlocked.Increment(ref _interactionSequence):D8}",
            node.Conditions,
            node.Effects,
            activeSceneId: null);
        return result.Status == CommandStatus.Committed;
    }

    public async Task<bool> OpenDocumentAsync(string documentId)
    {
        if (_kernel is null)
        {
            return false;
        }

        var document = _content.RequireDocument(documentId);
        var conditions = JsonSerializer.SerializeToElement(document.AccessConditions);
        var effects = document.OpenEffects.Select(item => item.Clone()).ToList();
        effects.Add(JsonSerializer.SerializeToElement(new { op = "document.open", documentId }));
        var result = await DispatchContentApplyAsync(
            $"document-open:{documentId}:{Interlocked.Increment(ref _interactionSequence):D8}",
            conditions,
            JsonSerializer.SerializeToElement(effects),
            activeSceneId: null);
        return result.Status == CommandStatus.Committed;
    }

    public async Task<bool> RecordJournalEntryAsync(string entryId, string sourceId)
    {
        if (_kernel is null)
        {
            return false;
        }

        var result = await _kernel.DispatchAsync(new GameCommand(
            $"journal-record:{entryId}:{Interlocked.Increment(ref _interactionSequence):D8}",
            NarrativeCommandHandlers.JournalRecord,
            JsonSerializer.SerializeToElement(new { entryId, sourceId })));
        if (result.Status == CommandStatus.Committed)
        {
            PresentRuntimeEvents(result.Events);
            QueueRuntimeStateChanged();
        }

        return result.Status == CommandStatus.Committed;
    }

    public void OpenDocumentUi(string documentId)
    {
        if (GetTree().GetFirstNodeInGroup("document_ui") is DocumentUi documentUi)
        {
            documentUi.Open(this, _content.RequireDocument(documentId));
        }
    }

    public async Task<bool> ChooseDialogueAsync(string dialogueId, string nodeId, string choiceId)
    {
        var dialogue = _content.RequireDialogue(dialogueId);
        var node = dialogue.Nodes[nodeId];
        var choice = node.Choices.Single(item => item.Id == choiceId);
        var conditions = Elements(choice.Conditions).ToList();
        var effects = Elements(choice.Effects).ToList();
        if (choice.NextNodeId is not null)
        {
            var target = dialogue.Nodes[choice.NextNodeId];
            conditions.AddRange(Elements(target.Conditions));
            effects.AddRange(Elements(target.Effects));
        }

        var result = await DispatchContentApplyAsync(
            $"dialogue-choice:{dialogueId}:{nodeId}:{choiceId}:{Interlocked.Increment(ref _interactionSequence):D8}",
            JsonSerializer.SerializeToElement(conditions),
            JsonSerializer.SerializeToElement(effects),
            activeSceneId: null,
            dialogueChoice: new JsonObject
            {
                ["dialogueId"] = dialogueId,
                ["nodeId"] = nodeId,
                ["choiceId"] = choiceId
            });
        return result.Status == CommandStatus.Committed;
    }

    public async Task<CommandDispatchResult> DispatchQuestLifecycleAsync(
        string occurrenceId,
        string questId,
        string lifecycleType,
        string? objectiveId = null)
    {
        if (_kernel is null)
        {
            throw new InvalidOperationException("Runtime kernel is unavailable.");
        }

        var result = await _kernel.DispatchAsync(_questCoordinator.Command(
            occurrenceId,
            questId,
            lifecycleType,
            objectiveId));
        if (result.Status == CommandStatus.Committed)
        {
            ReconcileQuestCapabilitySessions();
            QueueRuntimeStateChanged();
        }

        return result;
    }

    public void OpenDialogueUi(string dialogueId)
    {
        if (GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi dialogueUi)
        {
            dialogueUi.Open(this, dialogueId);
        }
    }

    public bool IsOldPcDocumentAccessible(string documentId)
    {
        if (_kernel is null)
        {
            return false;
        }

        var state = _kernel.SelectState();
        return _content.RequireOldPcDocument(documentId).AccessConditions.All(condition => ContentRuleEngine.Evaluate(condition, state));
    }

    public JsonElement OldPcState() => _capabilities?.Capture(OldPcInstanceId).State
        ?? throw new InvalidOperationException("Old PC capability is unavailable.");

    public void OpenOldPcUi()
    {
        if (GetTree().GetFirstNodeInGroup("old_pc_ui") is OldPcUi oldPcUi)
        {
            oldPcUi.Open(this);
        }
    }

    public async Task<JsonElement> HandleOldPcInputAsync(JsonElement input)
    {
        if (_kernel is null || _capabilities is null)
        {
            throw new InvalidOperationException("Old PC capability is unavailable.");
        }

        var inputType = input.GetProperty("type").GetString()
            ?? throw new ArgumentException("Old PC input type is missing.");
        if (inputType == "section")
        {
            return _capabilities.Handle(OldPcInstanceId, input);
        }

        OldPcDocumentContent? document = null;
        if (inputType is "open" or "save")
        {
            document = _content.RequireOldPcDocument(input.GetProperty("documentId").GetString()!);
            if (inputType == "open" && !IsOldPcDocumentAccessible(document.Id))
            {
                throw new InvalidOperationException("Этот документ пока недоступен: не хватает найденных связей или понятого слова.");
            }
        }

        var capabilityState = OldPcState();
        var sequence = capabilityState.GetProperty("nextActionSequence").GetInt64();
        var commandType = inputType switch
        {
            "search" => "oldpc.search",
            "open" => "oldpc.document.open",
            "save" => "oldpc.document.save",
            _ => throw new ArgumentException($"Unknown old PC input {inputType}.")
        };
        var payload = inputType switch
        {
            "search" => JsonSerializer.SerializeToElement(new
            {
                capabilityInstanceId = OldPcInstanceId,
                query = input.GetProperty("query").GetString() ?? string.Empty
            }),
            "open" => JsonSerializer.SerializeToElement(new
            {
                capabilityInstanceId = OldPcInstanceId,
                documentId = document!.Id,
                knowledgeRefs = document.KnowledgeRefs,
                openEffects = _content.RequireDocument(document.Id).OpenEffects
            }),
            "save" => JsonSerializer.SerializeToElement(new
            {
                capabilityInstanceId = OldPcInstanceId,
                documentId = document!.Id,
                knowledgeRefs = document.KnowledgeRefs
            }),
            _ => throw new ArgumentException($"Unknown old PC input {inputType}.")
        };
        var command = new GameCommand($"{OldPcInstanceId}:oldpc.{inputType}:{sequence}", commandType, payload);
        var result = await _kernel.DispatchAsync(command);
        if (result.Status != CommandStatus.Committed)
        {
            throw new InvalidOperationException(result.Error?.Message ?? "Old PC command was rejected.");
        }

        await ReconcileQuestsAsync();
        PresentRuntimeEvents(result.Events);
        QueueRuntimeStateChanged();

        return _capabilities.Handle(OldPcInstanceId, input);
    }

    private void CreateNewSession()
    {
        var kernel = new RuntimeKernel(_content.CreateInitialNarrativeState(), CreateHandlers());
        var capabilities = CreateCapabilities();
        var questCapabilities = new QuestCapabilitySessionOrchestrator(capabilities);
        try
        {
            questCapabilities.Reconcile(kernel.SelectState(), kernel.CaptureSnapshot().Claims);
        }
        catch
        {
            capabilities.Dispose();
            kernel.Dispose();
            throw;
        }

        _questCapabilities = null;
        ReplaceKernel(kernel);
        ReplaceCapabilities(capabilities);
        _questCapabilities = questCapabilities;
        _clock = new LogicalClock();
        _rngStreams = new OwnerRngStreams(0x55524d41);
        _scheduler = new DeterministicScheduler();
        _playTimeSeconds = 0;
        _interactionSequence = 0;
    }

    private async Task InitializeEntrypointAsync()
    {
        if (_kernel is null)
        {
            return;
        }

        var scene = _content.RequireScene(_content.Entrypoint);
        var result = await DispatchContentApplyAsync(
            $"campaign-entry:{scene.Id}",
            scene.EntryConditions,
            scene.OnEnter,
            scene.Id);
        if (result.Status != CommandStatus.Committed)
        {
            GD.PushError($"Campaign entrypoint rejected: {result.Error?.Message}");
        }
    }

    private async Task<bool> DispatchCompiledInteractionAsync(CompiledInteractionContent interaction)
    {
        if (_kernel is null)
        {
            return false;
        }

        var state = _kernel.SelectState();
        if (!state.TryGetProperty("activeScene", out var activeSceneValue) ||
            activeSceneValue.GetString() != interaction.SourceSceneId)
        {
            GD.Print($"interaction-rejected: {interaction.Id} is not owned by the active scene");
            return false;
        }

        var conditions = Elements(interaction.Conditions).ToList();
        var effects = new List<JsonElement>();
        effects.AddRange(Elements(_content.RequireScene(interaction.SourceSceneId).OnExit));

        effects.AddRange(Elements(interaction.Effects));
        if (interaction.TargetSceneId is not null)
        {
            var target = _content.RequireScene(interaction.TargetSceneId);
            conditions.AddRange(Elements(target.EntryConditions));
            effects.AddRange(Elements(target.OnEnter));
        }

        var result = await DispatchContentApplyAsync(
            $"content-interaction:{interaction.Id}:{Interlocked.Increment(ref _interactionSequence):D8}",
            JsonSerializer.SerializeToElement(conditions),
            JsonSerializer.SerializeToElement(effects),
            interaction.TargetSceneId);
        if (result.Status != CommandStatus.Committed)
        {
            GD.PushWarning($"Interaction {interaction.Id} rejected: {result.Error?.Code} {result.Error?.Message}");
            return false;
        }

        foreach (var gameEvent in result.Events)
        {
            GD.Print($"[{gameEvent.Sequence}] {gameEvent.Type}: {interaction.Id}");
        }

        return true;
    }

    private async Task<CommandDispatchResult> DispatchContentApplyAsync(
        string occurrenceId,
        JsonElement conditions,
        JsonElement effects,
        string? activeSceneId,
        JsonObject? dialogueChoice = null)
    {
        if (_kernel is null)
        {
            throw new InvalidOperationException("Runtime kernel is unavailable.");
        }

        var payload = new JsonObject
        {
            ["conditions"] = JsonNode.Parse(conditions.GetRawText()),
            ["effects"] = JsonNode.Parse(effects.GetRawText())
        };
        if (activeSceneId is not null)
        {
            payload["activeSceneId"] = activeSceneId;
        }

        if (dialogueChoice is not null)
        {
            payload["dialogueChoice"] = dialogueChoice.DeepClone();
        }

        var result = await _kernel.DispatchAsync(new GameCommand(
            occurrenceId,
            NarrativeCommandHandlers.ContentApply,
            JsonSerializer.SerializeToElement(payload)));
        if (result.Status == CommandStatus.Committed)
        {
            await ReconcileQuestsAsync();
            PresentRuntimeEvents(result.Events);
            QueueRuntimeStateChanged();
        }

        return result;
    }

    private static IEnumerable<JsonElement> Elements(JsonElement array) =>
        array.EnumerateArray().Select(item => item.Clone());

    private void RestoreSession(SaveGameV3 save)
    {
        var kernel = RuntimeKernel.Restore(save.Runtime, CreateHandlers());
        var capabilities = CreateCapabilities(save.Capabilities);
        var questCapabilities = new QuestCapabilitySessionOrchestrator(capabilities);
        try
        {
            questCapabilities.Reconcile(save.Runtime.State, save.Runtime.Claims, save.Capabilities);
        }
        catch
        {
            capabilities.Dispose();
            kernel.Dispose();
            throw;
        }

        _questCapabilities = null;
        ReplaceKernel(kernel);
        ReplaceCapabilities(capabilities);
        _questCapabilities = questCapabilities;
        _clock = LogicalClock.Restore(save.Clock);
        _rngStreams = OwnerRngStreams.Restore(save.RngStreams);
        _scheduler = DeterministicScheduler.Restore(save.Scheduler);
        CurrentZoneId = save.CurrentZone.Value;
        CurrentSpawnPointId = save.SpawnPoint.Value;
        _playTimeSeconds = save.PlayTimeSeconds;
        _interactionSequence = save.Runtime.Occurrences.Count;
        QueueRuntimeStateChanged();
    }

    private void QueueRuntimeStateChanged()
    {
        if (_runtimeStateNotificationQueued || !IsInsideTree())
        {
            return;
        }

        _runtimeStateNotificationQueued = true;
        CallDeferred(nameof(FlushRuntimeStateChanged));
    }

    private void FlushRuntimeStateChanged()
    {
        _runtimeStateNotificationQueued = false;
        if (IsInsideTree())
        {
            RuntimeStateChanged?.Invoke();
        }
    }

    private Dictionary<string, RuntimeCommandHandler> CreateHandlers()
    {
        var handlers = NarrativeCommandHandlers.Create();
        handlers.Add("world.interact", HandleWorldInteraction);
        handlers.Add("oldpc.search", HandleOldPcPassthrough);
        handlers.Add("oldpc.document.open", HandleOldPcOpen);
        handlers.Add("oldpc.document.save", HandleOldPcSave);
        handlers.Add("quest.lifecycle", _questCoordinator.Handle);
        return handlers;
    }

    private async Task ReconcileQuestsAsync()
    {
        if (_kernel is null)
        {
            return;
        }

        await _questCoordinator.ReconcileAsync(
            _kernel,
            prefix => $"{prefix}:{Interlocked.Increment(ref _interactionSequence):D8}");
        ReconcileQuestCapabilitySessions();
    }

    private void ReconcileQuestCapabilitySessions()
    {
        if (_kernel is null || _questCapabilities is null)
        {
            return;
        }

        var runtime = _kernel.CaptureSnapshot();
        _questCapabilities.Reconcile(runtime.State, runtime.Claims);
    }

    private void PresentRuntimeEvents(IEnumerable<GameEvent> events)
    {
        if (GetTree().GetFirstNodeInGroup("audio_cue_ui") is not AudioCueUi audioCueUi)
        {
            return;
        }

        foreach (var gameEvent in events.Where(item => item.Type == "runtime.audio.requested"))
        {
            var assetId = gameEvent.Payload.GetProperty("assetId").GetString()
                ?? throw new InvalidOperationException("Audio request event is missing assetId.");
            audioCueUi.Present(_content.ResolveAudio(assetId, $"runtime-event:{gameEvent.Sequence}"));
        }
    }

    private CapabilityHost CreateCapabilities(IReadOnlyList<CapabilitySessionSnapshot>? snapshots = null)
    {
        var host = new CapabilityHost(new CapabilityRegistry([new OldPcCapabilityProvider(_content.OldPcDescriptors())]));
        var snapshot = snapshots?.SingleOrDefault(item => item.CapabilityInstanceId == OldPcInstanceId);
        host.Create(
            OldPcInstanceId,
            OldPcCapabilityProvider.Protocol,
            "1.0.0",
            JsonSerializer.SerializeToElement(new { moduleId = OldPcCapabilityProvider.ModuleId }),
            snapshot);
        host.Start(OldPcInstanceId);
        return host;
    }

    private void ReplaceKernel(RuntimeKernel kernel)
    {
        _kernel?.Dispose();
        _kernel = kernel;
    }

    private void ReplaceCapabilities(CapabilityHost capabilities)
    {
        _capabilities?.Dispose();
        _capabilities = capabilities;
    }

    private FirstPersonController? FindPlayer() =>
        GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;

    private static CommandPlan HandleWorldInteraction(GameCommand command, RuntimeCommandContext context)
    {
        var interactionId = command.Payload.GetProperty("interactionId").GetString()
            ?? throw new InvalidOperationException("Interaction ID is missing.");
        return new(
            Effects:
            [
                new(StateEffectOperation.Set, "lastInteraction", JsonSerializer.SerializeToElement(interactionId)),
                new(StateEffectOperation.Increment, "interactionCount", Delta: 1)
            ],
            Events:
            [
                new("world.interaction.completed", JsonSerializer.SerializeToElement(new { interactionId }))
            ]);
    }

    private static CommandPlan HandleOldPcPassthrough(GameCommand command, RuntimeCommandContext context) => new(
        Events: [new(command.Type, command.Payload.Clone())]);

    private static CommandPlan HandleOldPcOpen(GameCommand command, RuntimeCommandContext context)
    {
        var documentId = command.Payload.GetProperty("documentId").GetString()!;
        var knowledge = JsonNode.Parse(context.State.GetProperty("knowledge").GetRawText())!.AsObject();
        foreach (var knowledgeId in command.Payload.GetProperty("knowledgeRefs").EnumerateArray().Select(item => item.GetString()!))
        {
            knowledge[knowledgeId] = new JsonObject { ["status"] = "confirmed" };
        }

        var effects = new List<StateEffect>();
        var events = new List<EventDraft>();
        if (command.Payload.TryGetProperty("openEffects", out var openEffects))
        {
            var documentEffects = openEffects.EnumerateArray()
                .Select(item => item.Clone())
                .ToList();
            documentEffects.Add(JsonSerializer.SerializeToElement(new { op = "document.open", documentId }));
            var planned = ContentRuleEngine.PlanEffects(JsonSerializer.SerializeToElement(documentEffects), context.State);
            effects.AddRange(planned.Effects);
            events.AddRange(planned.Events);
        }

        effects.Add(new(StateEffectOperation.Set, "knowledge", JsonSerializer.SerializeToElement(knowledge)));
        events.Add(new("oldpc.document.opened", command.Payload.Clone()));
        return new(
            Effects: effects,
            Events: events);
    }

    private static CommandPlan HandleOldPcSave(GameCommand command, RuntimeCommandContext context)
    {
        var documentId = command.Payload.GetProperty("documentId").GetString()!;
        var journal = JsonNode.Parse(context.State.GetProperty("journal").GetRawText())!.AsArray();
        if (!journal.OfType<JsonObject>().Any(item => item["entryId"]?.GetValue<string>() == documentId))
        {
            journal.Add(new JsonObject { ["entryId"] = documentId, ["sourceId"] = documentId });
        }

        return new(
            Effects: [new(StateEffectOperation.Set, "journal", JsonSerializer.SerializeToElement(journal))],
            Events: [new("oldpc.document.saved", command.Payload.Clone())]);
    }

}
