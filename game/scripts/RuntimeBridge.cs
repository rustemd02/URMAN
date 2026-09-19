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
using Urman.Core.World;

namespace Urman.Godot;

public partial class RuntimeBridge : Node
{
    private const string OldPcInstanceId = "urman.oldpc:instance/archive-hub";
    private const string WorldPropsStateKey = "world.props";
    private const string WorldPropsClaimScope = "act1-exploration";

    [Export]
    public string CurrentZoneId { get; set; } = "village_day";

    [Export]
    public string CurrentSpawnPointId { get; set; } = "arrival";

    [Export]
    public string CampaignResourcePath { get; set; } = CompiledCampaignRepository.ResourcePath;

    private RuntimeKernel? _kernel;
    internal RuntimeKernel? SessionIdentity => _loadingSlot || NeedsPhysicalRecovery ? null : _kernel;
    // Identity only: physical projections cannot dispatch through this handle.
    internal object? ProjectionSessionIdentity => _kernel;
    private CapabilityHost? _capabilities;
    private QuestCapabilitySessionOrchestrator? _questCapabilities;
    private LogicalClock _clock = new();
    private OwnerRngStreams _rngStreams = new(0x55524d41);
    private DeterministicScheduler _scheduler = new();
    private AtomicSaveGameStore? _saveStore;
    private AtomicSaveGameStore? _debugSaveStore;
    public bool IsDebugSession { get; private set; }
    private AtomicSaveGameStore? SessionSaveStore => IsDebugSession ? _debugSaveStore : _saveStore;
    private CompiledCampaignRepository _content = null!;
    private QuestRuntimeCoordinator _questCoordinator = null!;
    private long _interactionSequence;
    private double _playTimeSeconds;
    private ulong _loadingTimeSampleUsec;
    private double _loadingSecondsSinceProcess;

    // A modal reader is part of play. Only these actual lifecycle states stop
    // new accrual; the accumulated value restored from older saves is retained.
    [Flags]
    internal enum PlayTimeBlock
    {
        None = 0, NotReady = 1, Loading = 2, MainMenu = 4, Pause = 8,
        Settings = 16, Unfocused = 32, Ending = 64
    }

    internal double PlayTimeSeconds => _playTimeSeconds;
    internal event Action<string>? PlayTimeBoundary;
    private bool _checkpointAfterLadder;
    private bool _runtimeStateNotificationQueued;
    private AudioCueUi? _audioCueUi;
    private object? _rinatInterventionPendingSession;
    private object? _rinatInterventionAuthorizedSession;
    private long _rinatPresentationGeneration;

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
        _debugSaveStore = new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://debug-savegames"));
        CreateNewSession();
        _ = InitializeEntrypointAsync();
        CallDeferred(nameof(AttachAudioCueUi));
    }

    public override void _Process(double delta)
    {
        SampleLoadingTime();
        if (CapturePlayTimeBlocks() == PlayTimeBlock.None)
            _playTimeSeconds += Math.Max(0, delta - _loadingSecondsSinceProcess);
        _loadingSecondsSinceProcess = 0;
        if (_checkpointAfterLadder && !_loadingSlot && !_checkpointBusy
            && FindPlayer() is { IsClimbingLadder: false } player && player.IsOnFloor())
        {
            _checkpointAfterLadder = false;
            _ = SaveCheckpointAsync(force: true);
        }
    }

    internal PlayTimeBlock CapturePlayTimeBlocks()
    {
        var blocks = PlayTimeBlock.None;
        if (_kernel is null || FindPlayer() is null || NeedsPhysicalRecovery) blocks |= PlayTimeBlock.NotReady;
        if (_loadingSlot || _loadPreparing) blocks |= PlayTimeBlock.Loading;
        var demo = GetParent()?.GetParent() as Act1DemoRoot;
        if (demo?.MainMenuVisible == true || (demo is null && HasActiveMainMenu()))
            blocks |= PlayTimeBlock.MainMenu;
        if (demo?.DemoEnded == true) blocks |= PlayTimeBlock.Ending;
        if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true })
            blocks |= PlayTimeBlock.Pause;
        if (GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true })
            blocks |= PlayTimeBlock.Settings;
        if (!DisplayServer.WindowIsFocused()) blocks |= PlayTimeBlock.Unfocused;
        return blocks;
    }

    private void SampleLoadingTime()
    {
        if (!_loadingSlot && !_loadPreparing) return;
        var now = Time.GetTicksUsec();
        _loadingSecondsSinceProcess += (now - _loadingTimeSampleUsec) / 1_000_000.0;
        _loadingTimeSampleUsec = now;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (FindPlayer()?.ModalOpen != false) return;
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
        if (_audioCueUi is not null) _audioCueUi.CueStarted -= OnAudioCueStarted;
        _audioCueUi = null;
        _runtimeStateNotificationQueued = false;
        RuntimeStateChanged = null;
        PlayTimeBoundary = null;
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
        // Capture the store with this session before awaiting disk I/O. A menu
        // transition must never redirect an in-flight debug save into player slots.
        var store = SessionSaveStore;
        if (_loadingSlot || _loadPreparing || NeedsPhysicalRecovery || _kernel is null || _capabilities is null || store is null || FindPlayer() is not { } player)
        {
            GD.PushWarning("Quick save is unavailable before the runtime and player are ready.");
            return false;
        }

        if (player.SaveBlockReason is { } reason)
        {
            player.NotifyTraversal(reason);
            return false;
        }

        try
        {
            var session = _kernel;
            if (GetTree().GetFirstNodeInGroup("vehicle_fleet") is VehicleFleet fleet
                && !await fleet.FlushForSaveAsync()) return false;
            if (!ReferenceEquals(session, _kernel) || _loadingSlot || _loadPreparing) return false;
            if (GetTree().GetFirstNodeInGroup("act1_connected_world") is Act1ConnectedWorld world
                && !await world.FlushFacilitiesForSaveAsync()) return false;
            if (!ReferenceEquals(session, _kernel) || _loadingSlot || _loadPreparing) return false;
            if (AlsuStreetWalkPresentation.SessionOwner(GetTree()) is { } companion
                && !await companion.FlushForSaveAsync()) return false;
            if (!ReferenceEquals(session, _kernel) || _loadingSlot || _loadPreparing) return false;
            var save = CaptureSessionSave(player);
            await store.SaveAsync(slot, save);
            GD.Print($"SaveGameV3 written to {store.SlotPath(slot)}");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"SaveGameV3 write failed: {exception.Message}");
            return false;
        }
    }

    private SaveGameV3 CaptureSessionSave(FirstPersonController player)
    {
        if (_kernel is null || _capabilities is null)
            throw new InvalidOperationException("The runtime session is not ready for a snapshot.");
        var runtime = _kernel.CaptureSnapshot();
        return new SaveGameV3(
            SaveGameV3.CurrentSchemaVersion, _content.CampaignFingerprint,
            runtime.EventSequence, runtime, _clock.CaptureSnapshot(),
            _rngStreams.CaptureSnapshot(), _scheduler.CaptureSnapshot(),
            _capabilities.CaptureAll(), new WorldLocationId(CurrentZoneId),
            new SpawnPointId(CurrentSpawnPointId), player.CapturePortableTransform(),
            player.CaptureSettings(), _playTimeSeconds);
    }

    public bool IsSlotAvailable(string slot) => StoreHasSlot(SessionSaveStore, slot);

    // Main-menu Continue always describes the player's game, including after
    // returning from a debug visit. Pause/F9 use the current session's store.
    public bool IsPlayerSlotAvailable(string slot) => StoreHasSlot(_saveStore, slot);

    private static bool StoreHasSlot(AtomicSaveGameStore? store, string slot) =>
        store is not null && (File.Exists(store.SlotPath(slot)) || File.Exists(store.BackupPath(slot)));

    /// <summary>
    /// SAVE-003 Continue contract: a slot is loadable when the atomic store
    /// holds a primary or backup payload for it. The load itself re-verifies
    /// the campaign fingerprint and fails safely (state untouched) when the
    /// payload is invalid, so callers can gate a Continue action on this
    /// query without a second save-format owner.
    /// </summary>
    public bool HasLoadableSlot(string slot) => IsSlotAvailable(slot);

    public async Task<(string Slot, string Description)?> FindContinueAsync()
    {
        if (_saveStore is null) return null;
        (string Slot, string Description)? newest = null;
        var latestTime = DateTime.MinValue;
        foreach (var slot in new[] { "quick", CheckpointSlot })
        {
            try
            {
                var loaded = await _saveStore.LoadAsync(slot, _content.CampaignFingerprint);
                var path = loaded.RecoveredFromBackup ? _saveStore.BackupPath(slot) : _saveStore.SlotPath(slot);
                var time = File.GetLastWriteTimeUtc(path);
                if (time < latestTime) continue;
                latestTime = time;
                var place = loaded.Save.CurrentZone.Value switch
                {
                    "house_old_pc" => "Дом", "fap_clinic" => "ФАП",
                    "zirat_road" => "Дорога к зирату", "kara_urman_night" => "Кромка леса", _ => "Кара-Урман"
                };
                newest = (slot, $"{place} · {time.ToLocalTime():dd.MM HH:mm}");
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                // The store validates both primary and backup; an incompatible
                // or damaged slot stays untouched and is not offered to Continue.
            }
        }
        return newest;
    }

    /// <summary>
    /// SAVE-003 New Game contract: begin a fresh narrative session without
    /// deleting any existing slot (Continue must keep working) and without
    /// wiping the player's live user settings. The runtime kernel,
    /// capabilities, logical clock, RNG streams and scheduler are rebuilt
    /// exactly as at first boot, the campaign entrypoint is re-applied, and
    /// the player is placed at the canonical arrival spawn.
    /// </summary>
    public Task<bool> StartNewGameAsync() => StartSessionAsync(debugSession: false);

    public Task<bool> StartDebugSessionAsync() => StartSessionAsync(debugSession: true);

    private async Task<bool> StartSessionAsync(bool debugSession)
    {
        var player = FindPlayer();
        if (_loadingSlot || _loadPreparing || _content is null || player is null)
        {
            GD.PushWarning("New game is unavailable before the runtime and player are ready.");
            return false;
        }

        if (NeedsPhysicalRecovery) return await StartRecoverySessionAsync(debugSession, player);
        return await CreateFreshSessionAsync(debugSession, player);
    }

    private async Task<bool> CreateFreshSessionAsync(bool debugSession, FirstPersonController player)
    {
        ResetAudioCuePresentation();
        var preservedSettings = player.CaptureSettings();
        IsDebugSession = debugSession;
        CreateNewSession();
        CurrentZoneId = "village_day";
        CurrentSpawnPointId = "arrival";
        await InitializeEntrypointAsync();

        if (GetTree().GetFirstNodeInGroup("zone_manager") is Main main)
        {
            main.SwitchZone(CurrentZoneId, CurrentSpawnPointId);
        }

        await ProjectPhysicalWorldAsync();
        player.ApplySettings(preservedSettings);
        QueueRuntimeStateChanged();
        GD.Print(debugSession
            ? "SaveGameV3 debug session started; saves use debug-savegames."
            : "SaveGameV3 new game session started; existing slots untouched.");
        return true;
    }

    public async void QuickLoad()
    {
        _ = await LoadSlotAsync("quick");
    }

    public Task<bool> LoadSlotAsync(string slot) => LoadFromStoreAsync(slot, SessionSaveStore, IsDebugSession);

    public Task<bool> LoadPlayerSlotAsync(string slot) => LoadFromStoreAsync(slot, _saveStore, debugSession: false);

    private async Task<bool> LoadFromStoreAsync(string slot, AtomicSaveGameStore? store, bool debugSession)
    {
        if (_loadingSlot || _loadPreparing || store is null || FindPlayer() is not { } player)
        {
            GD.PushWarning("Quick load is unavailable before the runtime and player are ready.");
            return false;
        }

        var sessionBeforeLoad = _kernel;
        var loadSucceeded = false;
        SaveGameV3? previousSave = _physicalRecoverySave;
        var previousDebugSession = IsDebugSession;
        var projectionApplied = false;
        var rolledBack = false;
        // Reserve the load while live owners finish their normal commits. Input,
        // save and new-game are blocked without opening dispatch during projection.
        _loadPreparing = true;
        player.SetSessionTransition(true);
        _loadingTimeSampleUsec = Time.GetTicksUsec();
        PlayTimeBoundary?.Invoke("load-start");
        CancelRinatPresentation();
        // A load is a presentation boundary even while the atomic store is
        // reading. Do not let a pre-load world one-shot leak into the result.
        UiFoley.StopWorld(GetTree());
        var audio = GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi;
        var wasPaused = audio?.IsPaused ?? false;
        audio?.SetPaused(true);
        try
        {
            var result = await store.LoadAsync(slot, _content.CampaignFingerprint);
            // Preserve the live physical owners through the same snapshot used
            // by SaveSlotAsync. This rollback is in memory and never writes a slot.
            if (!NeedsPhysicalRecovery)
            {
                if (GetTree().GetFirstNodeInGroup("vehicle_fleet") is VehicleFleet fleet
                    && !await fleet.FlushForSaveAsync())
                    throw new InvalidOperationException("The current vehicle state could not be captured before loading.");
                if (GetTree().GetFirstNodeInGroup("act1_connected_world") is Act1ConnectedWorld world
                    && !await world.FlushFacilitiesForSaveAsync())
                    throw new InvalidOperationException("The current facility state could not be captured before loading.");
                if (AlsuStreetWalkPresentation.SessionOwner(GetTree()) is { } companion
                    && !await companion.FlushForSaveAsync())
                    throw new InvalidOperationException("Alsu's current physical position could not be captured before loading.");
            }
            if (!ReferenceEquals(sessionBeforeLoad, _kernel))
                throw new InvalidOperationException("The runtime session changed while reading the save.");
            previousSave ??= CaptureSessionSave(player);
            _loadingSlot = true;
            _loadPreparing = false;
            projectionApplied = true;
            await RestoreLoadedWorldAsync(result.Save, debugSession, player);
            NeedsPhysicalRecovery = false;
            _physicalRecoverySave = null;
            // Current profile settings remain authoritative across story loads.
            loadSucceeded = true;
            GD.Print(result.RecoveredFromBackup
                ? "SaveGameV3 restored from the last working backup."
                : "SaveGameV3 restored.");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"SaveGameV3 load failed: {exception.Message}");
            if (projectionApplied && previousSave is not null)
            {
                try
                {
                    // Begin the old projection in this continuation, before
                    // physics can advance at a rejected saved vehicle position.
                    await RestoreLoadedWorldAsync(previousSave, previousDebugSession, player);
                    NeedsPhysicalRecovery = false;
                    _physicalRecoverySave = null;
                    rolledBack = true;
                    GD.Print("SaveGameV3 rejected the loaded placement; the previous session was restored.");
                }
                catch (Exception rollbackException)
                {
                    NeedsPhysicalRecovery = true;
                    _physicalRecoverySave = previousSave;
                    GD.PushError($"SaveGameV3 could not restore the previous physical placement: {rollbackException.Message}");
                }
            }
            return false;
        }
        finally
        {
            // A short async load may begin and end between two process frames.
            // Subtract that measured interval from the next accrual as well.
            SampleLoadingTime();
            _loadingSlot = false;
            _loadPreparing = false;
            player.SetSessionTransition(NeedsPhysicalRecovery);
            QueueRuntimeStateChanged();
            PlayTimeBoundary?.Invoke(loadSucceeded ? "load-restored" : "load-failed");
            if (audio is not null && IsInstanceValid(audio)) audio.SetPaused(wasPaused || NeedsPhysicalRecovery);
            if (NeedsPhysicalRecovery) ShowPhysicalRecoveryMenu();
            if (loadSucceeded)
            {
                ReplayIncompleteFinale();
            }
            else if (!NeedsPhysicalRecovery && (rolledBack || ReferenceEquals(sessionBeforeLoad, _kernel))
                && ActiveSceneId == "urman.chapter1:scene/forest" && !HasActiveMainMenu()
                && IsInteractionAvailable("urman.chapter1:interaction/forest-rinat-intervention"))
            {
                // A failed read kept the original world, but deliberately
                // cancelled its pending presentation. Reconstruct only cues
                // from that reachable scene, never its state-changing effects.
                ResetAudioCuePresentation();
                if (audio is not null && IsInstanceValid(audio)) audio.SetPaused(wasPaused);
                ReplayIncompleteFinale();
            }
        }
    }

    private async Task RestoreLoadedWorldAsync(SaveGameV3 save, bool debugSession, FirstPersonController player)
    {
        ResetAudioCuePresentation();
        RestoreSession(save);
        IsDebugSession = debugSession;
        if (GetTree().GetFirstNodeInGroup("zone_manager") is Main main)
            main.SwitchZone(save.CurrentZone.Value, save.SpawnPoint.Value);
        player.ApplyPortableTransform(save.PlayerTransform);
        await ProjectPhysicalWorldAsync();
        if (!player.VehicleControlled && !player.CanCrouchAt(player.GlobalPosition)
            && GetTree().GetFirstNodeInGroup("act1_connected_world") is Act1ConnectedWorld restoredWorld
            && restoredWorld.TryResolveLegacyMosqueCarpetFeet(player, out var carpetFeet))
        {
            var original = player.CapturePortableTransform();
            player.ApplyPortableTransform(new PlayerTransform(new(carpetFeet.X, carpetFeet.Y, carpetFeet.Z), original.RotationDegrees));
            // Reproject the same saved owners, including an actually held item,
            // at the corrected feet. No slot or narrative state is rewritten.
            await ProjectPhysicalWorldAsync();
            if (GetTree().GetFirstNodeInGroup("carry_coordinator") is CarryCoordinator { HeldItem: not null, HasValidHeldPose: false })
                throw new InvalidDataException("The loaded held item is blocked above the restored mosque carpet.");
            GD.Print($"act1-loaded-floor-migration: mosque-timber-to-carpet from={original.Position} to={carpetFeet}");
        }
        // A valid small-space save may require crouching. Reject the placement
        // only when even that real capsule cannot fit after the world is restored.
        if (!player.VehicleControlled && !player.CanCrouchAt(player.GlobalPosition))
            throw new InvalidDataException("The loaded player's physical position is blocked.");
    }

    private async Task ProjectPhysicalWorldAsync()
    {
        if (GetTree().GetFirstNodeInGroup("act1_connected_world") is Act1ConnectedWorld world)
            world.ProjectLoadedPhysicalState();
        // Stage every saved body before querying their contacts. The fleet's
        // existing physics barrier publishes its scene transforms; a companion
        // query before that barrier could collide with a previous chassis pose.
        var companion = AlsuStreetWalkPresentation.SessionOwner(GetTree());
        companion?.ProjectLoadedPhysicalState(deferValidation: true);
        if (GetTree().GetFirstNodeInGroup("vehicle_fleet") is VehicleFleet fleet
            && !await fleet.CompleteLoadedProjectionAsync())
            throw new InvalidDataException(fleet.ProjectionFailure ?? "The loaded vehicle placement could not be verified.");
        if (companion is not null) await companion.CompleteLoadedPhysicalProjectionAsync();
    }

    public const string CheckpointSlot = "checkpoint";

    /// <summary>
    /// SAVE-004 writes one rolling checkpoint at stable investigation beats.
    /// Committed world consequences and excerpts selected in an existing
    /// source also save immediately, including while its reader remains open.
    /// Merely opening a reader or choosing a dialogue line does not force a save.
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
    private bool _loadingSlot;
    private bool _loadPreparing;

    // Pose is checked at the instant of the attempt, not cached with narrative
    // state. Technical runtimes without a connected world retain their contract.
    internal bool CanPhysicallyUseInteraction(string interactionId)
    {
        if (_loadingSlot || _loadPreparing || NeedsPhysicalRecovery) return false;
        if (!HasRequiredWorldState(interactionId)) return false;
        if (interactionId == "urman.chapter1:interaction/talk-alsu"
            && AlsuStreetWalkPresentation.SessionOwner(GetTree()) is { PhysicalAccessReady: false }) return false;
        if (IsSourceExcerptAction(interactionId)) return IsSourceExcerptAuthorized(interactionId);
        if (interactionId == RinatPresencePresentation.ObservationId)
            return RinatPresencePresentation.Current(GetTree())?.CanObserveRoadside() == true;
        if (interactionId == "urman.chapter1:interaction/forest-rinat-intervention")
            return SessionIdentity is { } session && ReferenceEquals(session, _rinatInterventionAuthorizedSession);
        if (interactionId == "urman.chapter1:interaction/mark-underdeck-quiet")
        {
            if (CurrentZoneId is not ("village_day" or "zirat_road" or "kara_urman_night")) return false;
        }
        var world = GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
        return world is null || (world.CanUseSmallSpaceInteraction(interactionId)
            && world.CanUseObservationInteraction(interactionId)
            && world.CanUseFacilityInteraction(interactionId)
            && world.CanUseShopSupplyInteraction(interactionId));
    }

    public async Task<bool> DispatchInteractionAsync(string interactionId)
    {
        if (_kernel is null || !CanPhysicallyUseInteraction(interactionId))
        {
            return false;
        }

        if (_content.TryGetInteraction(interactionId, out var interaction))
        {
            var dispatched = await DispatchCompiledInteractionAsync(interaction);
            if (dispatched)
            {
                await SaveCheckpointAsync(force: (interaction.WorldLocations is not null || IsSourceExcerptAction(interaction.Id))
                    && interaction.Effects.GetArrayLength() > 0);
            }

            return dispatched;
        }

        GD.PushWarning($"Ignoring interaction that is not present in the compiled campaign: {interactionId}");
        return false;
    }

    private async Task SaveCheckpointAsync(bool force = false)
    {
        if (_loadingSlot || _checkpointBusy || _content is null || ActiveSceneId is not { } scene)
        {
            return;
        }

        if (!force && (!CheckpointScenes.Contains(scene) || _lastCheckpointScene == scene))
        {
            return;
        }

        if (FindPlayer()?.IsClimbingLadder == true)
        {
            _checkpointAfterLadder = true;
            return;
        }
        _checkpointBusy = true;
        var session = _kernel;
        try
        {
            if (await SaveSlotAsync(CheckpointSlot) && ReferenceEquals(session, _kernel))
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

    public IReadOnlyList<OldPcDocumentContent> OldPcDocuments => _content.OldPcDocuments
        .Select(document => document with
        {
            Title = ResolveWorldText(document.Title),
            BodyMarkdown = ResolveWorldText(document.BodyMarkdown)
        }).ToArray();

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

    /// <summary>
    /// The placement deviations of the carryable props and the cleared snow
    /// volumes, keyed by prop id. A missing key means the prop still sits where
    /// the authored layout put it, so the authored transforms stay in
    /// Act1ConnectedWorld and are never duplicated into the save.
    /// </summary>
    public JsonElement SelectWorldProps()
    {
        var state = SelectRuntimeState();
        var props = state.TryGetProperty(WorldPropsStateKey, out var existing)
            ? JsonNode.Parse(existing.GetRawText())!.AsObject()
            : new JsonObject();
        return JsonSerializer.SerializeToElement(props);
    }

    public string? ActiveSceneId => _kernel?.SelectState().TryGetProperty("activeScene", out var activeScene) == true
        ? activeScene.GetString()
        : null;

    public bool IsWorldInteraction(string interactionId) =>
        _content.TryGetInteraction(interactionId, out var interaction) && interaction.WorldLocations is not null;

    // A consequence recorded by the physical mechanism is a saved prerequisite,
    // not a camera condition. Availability and dispatch must use the same owner.
    private bool HasRequiredWorldState(string interactionId) =>
        interactionId != "urman.chapter1:interaction/mark-underdeck-quiet"
        || (_kernel is not null && YardMechanism.Flag(SelectWorldProps(), "yard/loose-footboard"));

    public bool IsInteractionAvailable(string interactionId)
    {
        if (_kernel is null || !HasRequiredWorldState(interactionId)
            || !_content.TryGetInteraction(interactionId, out var interaction) || interaction.JournalAction is not null)
        {
            return false;
        }

        var state = _kernel.SelectState();
        if (!OwnsInteraction(interaction, state)
            || !ContentRuleEngine.EvaluateAll(interaction.Conditions, state))
        {
            return false;
        }

        return interaction.TargetSceneId is null
            || ContentRuleEngine.EvaluateAll(_content.RequireScene(interaction.TargetSceneId).EntryConditions, state);
    }

    public IReadOnlyList<CompiledInteractionContent> JournalActions(IReadOnlyCollection<string> sourceIds)
    {
        if (_kernel is null || sourceIds.Count != 2 || sourceIds.Distinct(StringComparer.Ordinal).Count() != 2)
            return [];
        var found = JournalEntries().Select(entry => entry.SourceId).ToHashSet(StringComparer.Ordinal);
        if (sourceIds.Any(id => !found.Contains(id))) return [];
        return _content.JournalActions.Where(action => action.JournalAction!.SourceIds.All(sourceIds.Contains)
            && EvaluateConditions(action.Conditions)).ToArray();
    }

    public async Task<bool> CompareJournalSourcesAsync(string actionId, IReadOnlyCollection<string> sourceIds)
    {
        var action = JournalActions(sourceIds).FirstOrDefault(action => action.Id == actionId);
        if (action is null) return false;
        var result = await DispatchContentApplyAsync(
            $"journal-compare:{action.Id}:{Interlocked.Increment(ref _interactionSequence):D8}",
            action.Conditions, action.Effects, activeSceneId: null, journalSources: action.JournalAction!.SourceIds);
        if (result.Status != CommandStatus.Committed) return false;
        if (action.Effects.GetArrayLength() > 0) await SaveCheckpointAsync(force: true);
        return true;
    }

    public CompiledDialogueContent RequireDialogue(string dialogueId) => _content.RequireDialogue(dialogueId);

    public string ResolveDialogueStartNodeId(CompiledDialogueContent dialogue) =>
        dialogue.EntryRoutes.FirstOrDefault(route => EvaluateConditions(route.Conditions))?.NodeId
        ?? dialogue.StartNodeId;

    public CompiledSceneContent RequireScene(string sceneId) => _content.RequireScene(sceneId);

    public CompiledDocumentContent RequireDocument(string documentId)
    {
        var document = _content.RequireDocument(documentId);
        return document with
        {
            Title = ResolveWorldText(document.Title),
            BodyMarkdown = ResolveWorldText(document.BodyMarkdown)
                + (documentId == "urman.chapter1:document/shop-account-book" ? "\n\n" + ShopLedgerText() : string.Empty)
        };
    }

    public string ResolveText(string textId) => ResolveWorldText(_content.ResolveText(textId));

    // The world registry owns displayed addresses. Save and quest references
    // stay as authored IDs; readers resolve again after a street-name change.
    public string ResolveWorldText(string text) =>
        (GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld)
            ?.AddressRegistry?.ResolveText(text) ?? text;

    public IReadOnlyList<ResolvedJournalEntry> JournalEntries()
    {
        if (_kernel is null)
        {
            return [];
        }

        var state = _kernel.SelectState();
        var knowledge = state.GetProperty("knowledge");
        return state.GetProperty("journal").EnumerateArray()
            .Select(item =>
            {
                var entry = _content.ResolveJournalEntry(item.GetProperty("entryId").GetString()!,
                    item.GetProperty("sourceId").GetString()!);
                entry = entry with
                {
                    Title = ResolveWorldText(entry.Title),
                    Body = ResolveWorldText(entry.Body)
                        + (entry.EntryId == "urman.chapter1:document/shop-account-book" ? "\n\n" + ShopLedgerText() : string.Empty),
                    SourceTitle = ResolveWorldText(entry.SourceTitle)
                };
                // Preserve the authored source and history. Only knowledge cards
                // carry a live conclusion status; a document remains a source.
                return knowledge.TryGetProperty(entry.EntryId, out var fact)
                    && fact.TryGetProperty("status", out var status)
                    ? entry with { Status = status.GetString() } : entry;
            })
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
        var session = SessionIdentity;
        var dialogue = _content.RequireDialogue(dialogueId);
        var node = dialogue.Nodes.TryGetValue(nodeId, out var found)
            ? found
            : throw new KeyNotFoundException($"Unknown dialogue node {dialogueId}/{nodeId}.");
        var result = await DispatchContentApplyAsync(
            $"dialogue-node:{dialogueId}:{nodeId}:{Interlocked.Increment(ref _interactionSequence):D8}",
            node.Conditions,
            node.Effects,
            activeSceneId: null);
        if (result.Status == CommandStatus.Committed && ReferenceEquals(session, SessionIdentity))
            await RememberDialogueSpeakerAsync(dialogueId, node);
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
        // Merely entering an evidence scene does not mean the source was read.
        // Both physical documents and the old-PC reader confirm their raw
        // evidence here, inside the same access-checked transaction as opening.
        var effects = document.KnowledgeRefs.Select(knowledgeId =>
            JsonSerializer.SerializeToElement(new { op = "knowledge.set-status", knowledgeId, status = "confirmed" }))
            .Concat(document.OpenEffects.Select(item => item.Clone())).ToList();
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
            documentUi.Open(this, RequireDocument(documentId));
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
        if (inputType is "section" or "desktop")
        {
            return _capabilities.Handle(OldPcInstanceId, input);
        }

        OldPcDocumentContent? document = null;
        if (inputType is "open" or "save")
        {
            document = _content.RequireOldPcDocument(input.GetProperty("documentId").GetString()!);
            if (!IsOldPcDocumentAccessible(document.Id))
            {
                throw new InvalidOperationException("Этот документ пока недоступен: не хватает найденных связей или понятого слова.");
            }
            if (inputType == "save" && !WasDocumentOpened(_kernel.SelectState(), document.Id))
                throw new InvalidOperationException("Сначала откройте документ и прочитайте его.");
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

    private bool OwnsInteraction(CompiledInteractionContent interaction, JsonElement state) =>
        IsSourceExcerptAction(interaction.Id)
            ? OwnsSourceExcerptInteraction(interaction.Id)
            : interaction.WorldLocations is { } locations
            ? locations.Contains(CurrentZoneId, StringComparer.Ordinal)
            : state.TryGetProperty("activeScene", out var scene) && scene.GetString() == interaction.SourceSceneId;

    private async Task<bool> DispatchCompiledInteractionAsync(CompiledInteractionContent interaction)
    {
        if (_kernel is null || interaction.JournalAction is not null)
        {
            return false;
        }

        var state = _kernel.SelectState();
        if (!OwnsInteraction(interaction, state))
        {
            GD.Print($"interaction-rejected: {interaction.Id} is not owned by the active scene");
            return false;
        }

        var conditions = Elements(interaction.Conditions).ToList();
        var effects = new List<JsonElement>();
        if (interaction.WorldLocations is null && state.GetProperty("activeScene").GetString() == interaction.SourceSceneId)
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
        JsonObject? dialogueChoice = null,
        IReadOnlyList<string>? journalSources = null)
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

        if (journalSources is not null)
        {
            payload["journalSources"] = JsonSerializer.SerializeToNode(journalSources);
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
        handlers.Add("world.custody", HandleWorldCustody);
        handlers.Add("world.props", HandleWorldPropsSet);
        handlers.Add("world.props.register", HandleWorldPropsRegister);
        handlers.Add("oldpc.search", HandleOldPcPassthrough);
        handlers.Add("oldpc.document.open", HandleOldPcOpen);
        handlers.Add("oldpc.document.save", HandleOldPcSave);
        handlers.Add("shop.purchase", HandleShopPurchase);
        handlers.Add("shop.use", HandleShopUse);
        handlers.Add("bathhouse.ignite", HandleBathIgnition);
        handlers.Add("npc.alsu.walk-checkpoint", HandleAlsuWalkCheckpoint);
        handlers.Add("quest.lifecycle", _questCoordinator.Handle);
        return handlers;
    }

    /// <summary>
    /// EX00 follow-up wiring, at the point named in
    /// act1_ex00_state_contract_design_2026-09-15.md. Ownership goes through
    /// CustodyStore.PlanBatch, so an item can never be claimed twice and a
    /// rejected transition produces no partial effect. The placement
    /// deviation, when one is supplied, rides in the same plan: one atomic
    /// dispatch, one snapshot, no second save path.
    /// </summary>
    private static CommandPlan HandleWorldCustody(GameCommand command, RuntimeCommandContext context)
    {
        var payload = command.Payload;
        var operations = payload.GetProperty("operations");
        var claimOwnerId = payload.TryGetProperty("claimOwnerId", out var owner) && owner.ValueKind == JsonValueKind.String
            ? owner.GetString()!
            : command.ActionOccurrenceId;
        var plan = CustodyStore.PlanBatch(context.State, operations, claimOwnerId, WorldPropsClaimScope);
        if (plan.Rejection is not null)
        {
            return plan;
        }

        // The custody plan rejected the batch by planting a non-finite
        // increment; recognise it and leave the batch alone.
        if (plan.Effects is null || plan.Effects.Any(effect => effect.Operation == StateEffectOperation.Increment))
        {
            return plan;
        }

        var custody = plan.Effects.FirstOrDefault(effect => effect.Key == CustodyStore.DefaultStateKey)?.Value;
        if (custody is { ValueKind: JsonValueKind.Array } items
            && items.EnumerateArray().Count(item => item.GetProperty("custodyOwnerId").GetString() == "player") > 1)
            return new CommandPlan(Rejection: new("carry-hands-full", "Put down the carried object before taking another one."));

        if (!payload.TryGetProperty("placement", out var placement)) return plan;

        var effects = plan.Effects.ToList();
        effects.Add(new StateEffect(StateEffectOperation.Set, WorldPropsStateKey,
            WritePlacement(context.State, placement)));
        return plan with { Effects = effects };
    }

    /// <summary>
    /// A props write with no ownership change: tool results and cleared snow.
    /// Kept separate from the custody handler because CustodyStore requires at
    /// least one operation and a tool use moves nothing.
    /// </summary>
    private static CommandPlan HandleWorldPropsSet(GameCommand command, RuntimeCommandContext context)
    {
        var placement = command.Payload.GetProperty("placement");
        return new CommandPlan(
            Effects: [new StateEffect(StateEffectOperation.Set, WorldPropsStateKey,
                WritePlacement(context.State, placement))],
            Events: [new("world.props.changed", JsonSerializer.SerializeToElement(
                placement.EnumerateArray().Select(record => record.GetProperty("propId").GetString()!).ToArray()))]);
    }

    private static CommandPlan HandleWorldPropsRegister(GameCommand command, RuntimeCommandContext context)
    {
        var itemIds = command.Payload.GetProperty("itemIds").EnumerateArray()
            .Select(item => item.GetString()!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var items = ReadCustodyItems(context.State);
        var existing = items.Select(item => item["itemId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var added = itemIds.Where(id => !existing.Contains(id))
            .Select(id => (JsonNode)new JsonObject
            {
                ["itemId"] = id,
                ["custodyOwnerId"] = "world",
                ["condition"] = new JsonObject()
            })
            .ToArray();
        if (added.Length == 0)
        {
            // Registration is idempotent: a loaded session keeps the ownership
            // the player actually left behind.
            return new CommandPlan(Effects: []);
        }

        var next = new JsonArray(items.Select(item => (JsonNode?)item.DeepClone()).ToArray());
        foreach (var item in added)
        {
            next.Add(item);
        }

        return new CommandPlan(
            Effects: [new StateEffect(StateEffectOperation.Set, CustodyStore.DefaultStateKey,
                JsonSerializer.SerializeToElement(next))],
            Events: [new("world.custody.registered", JsonSerializer.SerializeToElement(new { itemIds = added.Select(item => item["itemId"]!.GetValue<string>()).ToArray() }))]);
    }

    private static List<JsonObject> ReadCustodyItems(JsonElement state)
    {
        if (!state.TryGetProperty(CustodyStore.DefaultStateKey, out var stored) || stored.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return stored.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => JsonNode.Parse(item.GetRawText())!.AsObject())
            .ToList();
    }

    /// <summary>
    /// Merges one placement record into the deviation map. Only the fields the
    /// caller actually supplies are written, so a "held" transition can be
    /// recorded without inventing a world position for the prop.
    /// </summary>
    private static JsonElement WritePlacement(JsonElement state, JsonElement placement)
    {
        var props = state.TryGetProperty(WorldPropsStateKey, out var stored) && stored.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(stored.GetRawText())!.AsObject()
            : new JsonObject();
        foreach (var record in placement.EnumerateArray())
        {
            var propId = record.GetProperty("propId").GetString()!;
            var entry = props[propId] as JsonObject ?? new JsonObject();
            foreach (var field in record.EnumerateObject())
            {
                if (field.Name == "propId")
                {
                    continue;
                }

                entry[field.Name] = JsonNode.Parse(field.Value.GetRawText());
            }

            props[propId] = entry;
        }

        return JsonSerializer.SerializeToElement(props);
    }

    /// <summary>
    /// EX01/EX02/EX03/EX05 state entry point for the world. Ownership and, when
    /// given, the placement deviation land in one atomic dispatch.
    /// </summary>
    public async Task<bool> DispatchWorldCustodyAsync(JsonNode operations, JsonNode? placement = null,
        string claimOwnerId = "act1-exploration")
    {
        var session = SessionIdentity;
        if (session is null)
        {
            return false;
        }

        var payload = new JsonObject
        {
            ["operations"] = operations,
            ["claimOwnerId"] = claimOwnerId
        };
        if (placement is not null)
        {
            payload["placement"] = placement;
        }

        var result = await session.DispatchAsync(new GameCommand(
            $"world.custody:{Interlocked.Increment(ref _interactionSequence):D8}",
            "world.custody",
            JsonSerializer.SerializeToElement(payload)));
        if (!ReferenceEquals(session, SessionIdentity)) return false;
        if (result.Status == CommandStatus.Committed)
        {
            QueueRuntimeStateChanged();
        }

        return result.Status == CommandStatus.Committed;
    }

    /// <summary>
    /// Records props results that do not move ownership - a tool use, a cleared
    /// snow volume. Cosmetic player tracks stay out of this: SnowTrampleField
    /// keeps its own session-only contract.
    /// </summary>
    public async Task<bool> DispatchWorldPropsAsync(JsonNode placement)
    {
        var session = SessionIdentity;
        if (session is null)
        {
            return false;
        }

        var result = await session.DispatchAsync(new GameCommand(
            $"world.props:{Interlocked.Increment(ref _interactionSequence):D8}",
            "world.props",
            JsonSerializer.SerializeToElement(new JsonObject { ["placement"] = placement })));
        if (!ReferenceEquals(session, SessionIdentity)) return false;
        if (result.Status == CommandStatus.Committed)
        {
            QueueRuntimeStateChanged();
        }

        return result.Status == CommandStatus.Committed;
    }

    /// <summary>
    /// Registers the authored carryable set with the ownership layer. Called
    /// once when the connected world builds; idempotent across loads.
    /// </summary>
    public async Task RegisterWorldItemsAsync(IEnumerable<string> itemIds)
    {
        var session = SessionIdentity;
        if (session is null)
        {
            return;
        }

        var result = await session.DispatchAsync(new GameCommand(
            $"world.props.register:{Interlocked.Increment(ref _interactionSequence):D8}",
            "world.props.register",
            JsonSerializer.SerializeToElement(new { itemIds = itemIds.ToArray() })));
        if (!ReferenceEquals(session, SessionIdentity)) return;
        if (result.Status == CommandStatus.Committed)
        {
            QueueRuntimeStateChanged();
        }
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

    private void ResetAudioCuePresentation()
    {
        CancelRinatPresentation();
        UiFoley.StopWorld(GetTree());
        if (GetTree().GetFirstNodeInGroup("audio_cue_ui") is AudioCueUi audioCueUi)
        {
            audioCueUi.ResetPresentation();
        }
    }

    private void AttachAudioCueUi()
    {
        if (_audioCueUi is not null || !IsInsideTree()) return;
        _audioCueUi = GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi;
        if (_audioCueUi is not null) _audioCueUi.CueStarted += OnAudioCueStarted;
    }

    private void OnAudioCueStarted(string assetId)
    {
        if (assetId == "urman.chapter1:asset/audio-rinat-interruption")
            CallDeferred(nameof(CommitRinatIntervention));
    }

    private bool HasActiveMainMenu() => GetTree().GetNodesInGroup("main_menu")
        .OfType<MainMenuUi>().Any(menu => IsInstanceValid(menu) && !menu.IsQueuedForDeletion() && !menu.IsDismissed);

    internal bool CanPresentRinatIntervention(object session) => IsInsideTree()
        && ReferenceEquals(session, SessionIdentity) && CurrentZoneId == "kara_urman_night"
        && ActiveSceneId == "urman.chapter1:scene/forest" && !HasActiveMainMenu()
        && _audioCueUi?.LastStartedAssetId == "urman.chapter1:asset/audio-rinat-interruption";

    internal void CancelRinatPresentation()
    {
        _rinatPresentationGeneration++;
        _rinatInterventionPendingSession = null;
        _rinatInterventionAuthorizedSession = null;
        if (IsInsideTree()) RinatPresencePresentation.Current(GetTree())?.CancelIntervention();
    }

    private async void CommitRinatIntervention()
    {
        const string actionId = "urman.chapter1:interaction/forest-rinat-intervention";
        // A reset/load clears LastStartedAssetId before this deferred callback.
        if (_audioCueUi?.LastStartedAssetId != "urman.chapter1:asset/audio-rinat-interruption"
            || !IsInteractionAvailable(actionId)) return;
        var session = SessionIdentity;
        if (session is null || ReferenceEquals(session, _rinatInterventionPendingSession)) return;
        if (!CanPresentRinatIntervention(session)) return;
        var generation = _rinatPresentationGeneration;
        _rinatInterventionPendingSession = session;
        try
        {
            var presentation = RinatPresencePresentation.Current(GetTree());
            if (presentation is null)
            {
                GD.PushError("Rinat's finale requires his visible scene presentation.");
                return;
            }
            if (!await presentation.PresentRinatInterventionAsync(session)
                || generation != _rinatPresentationGeneration || !CanPresentRinatIntervention(session)) return;
            // A cue-start callback is not an experienced intervention. Let the
            // actual stop gesture and authored voice/caption finish before the
            // rule and hard cut can commit, including after a mid-scene load.
            while (_audioCueUi?.IsPresenting == true)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (generation != _rinatPresentationGeneration || !CanPresentRinatIntervention(session)) return;
            }
            if (generation != _rinatPresentationGeneration || !CanPresentRinatIntervention(session)) return;
            _rinatInterventionAuthorizedSession = session;
            if (await DispatchInteractionAsync(actionId) && ReferenceEquals(session, SessionIdentity))
                await SaveCheckpointAsync(force: true);
        }
        catch (Exception exception)
        {
            GD.PushError("Rinat's intervention presentation failed: " + exception);
        }
        finally
        {
            if (generation == _rinatPresentationGeneration)
            {
                if (ReferenceEquals(session, _rinatInterventionAuthorizedSession)) _rinatInterventionAuthorizedSession = null;
                if (ReferenceEquals(session, _rinatInterventionPendingSession)) _rinatInterventionPendingSession = null;
            }
        }
    }

    private void ReplayIncompleteFinale()
    {
        if (ActiveSceneId != "urman.chapter1:scene/forest"
            || !IsInteractionAvailable("urman.chapter1:interaction/forest-rinat-intervention")) return;
        AttachAudioCueUi();
        // Reconstruct presentation from authored data after loading mid-scene;
        // never reapply onEnter state effects or replay an already completed ending.
        foreach (var effect in _content.RequireScene(ActiveSceneId).OnEnter.EnumerateArray())
            if (effect.GetProperty("op").GetString() == "audio.request")
                _audioCueUi?.Present(_content.ResolveAudio(effect.GetProperty("assetId").GetString()!, "restored-finale"));
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
        _checkpointAfterLadder = false;
        _kernel?.Dispose();
        _kernel = kernel;
        _lastCheckpointScene = null;
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
        // Use the same ordered transaction as the physical document reader:
        // declared source facts first, then authored effects and the open event.
        // Replacing knowledge from the original snapshot after PlanEffects erased
        // facts created by openEffects (including both source-return read facts).
        var documentEffects = command.Payload.GetProperty("knowledgeRefs").EnumerateArray()
            .Select(item => JsonSerializer.SerializeToElement(new
            {
                op = "knowledge.set-status", knowledgeId = item.GetString()!, status = "confirmed"
            })).ToList();
        if (command.Payload.TryGetProperty("openEffects", out var openEffects))
            documentEffects.AddRange(openEffects.EnumerateArray().Select(item => item.Clone()));
        documentEffects.Add(JsonSerializer.SerializeToElement(new { op = "document.open", documentId }));
        var planned = ContentRuleEngine.PlanEffects(JsonSerializer.SerializeToElement(documentEffects), context.State);
        var events = planned.Events.ToList();
        events.Add(new("oldpc.document.opened", command.Payload.Clone()));
        return new(
            Effects: planned.Effects,
            Events: events);
    }

    private static bool WasDocumentOpened(JsonElement state, string documentId) =>
        state.TryGetProperty("presentation", out var presentation)
        && presentation.TryGetProperty("openedDocumentIds", out var opened)
        && opened.EnumerateArray().Any(item => item.GetString() == documentId);

    private CommandPlan HandleOldPcSave(GameCommand command, RuntimeCommandContext context)
    {
        var documentId = command.Payload.GetProperty("documentId").GetString()!;
        // Check again at the runtime transaction boundary: UI availability is
        // not evidence that this source was actually accessible and opened.
        var source = _content.RequireOldPcDocument(documentId);
        if (!source.AccessConditions.All(condition => ContentRuleEngine.Evaluate(condition, context.State))
            || !WasDocumentOpened(context.State, documentId))
            return new CommandPlan(Rejection: new("oldpc-unread-source", "Сначала откройте доступный документ."));
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
