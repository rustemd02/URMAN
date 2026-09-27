using System.Text.Json;
using Urman.Core.Contracts;
using Urman.Core.Determinism;
using Urman.Core.Persistence;
using Xunit;

namespace Urman.Core.Tests;

public sealed class DeterminismAndSaveTests
{
    private const string CampaignFingerprint = "59ef1e6134daa23f6177de88e57d72b0a18b3d84434a36f4a2c827a3097163fd";

    [Fact]
    public void LogicalClock_AdvancesOnlyExplicitlyAndRoundTrips()
    {
        var clock = new LogicalClock(12);
        Assert.Equal(17, clock.Advance(5));
        Assert.Equal(17, LogicalClock.Restore(clock.CaptureSnapshot()).Tick);
    }

    [Fact]
    public void OwnerRngStreams_AreStableAcrossOrderingAndSnapshots()
    {
        var first = new OwnerRngStreams(42);
        var firstA = first.ForOwner("quest:a").NextUInt32();
        var firstB = first.ForOwner("quest:b").NextUInt32();

        var second = new OwnerRngStreams(42);
        var secondB = second.ForOwner("quest:b").NextUInt32();
        var secondA = second.ForOwner("quest:a").NextUInt32();

        Assert.Equal(firstA, secondA);
        Assert.Equal(firstB, secondB);
        var restored = OwnerRngStreams.Restore(first.CaptureSnapshot());
        Assert.Equal(first.ForOwner("quest:a").NextUInt32(), restored.ForOwner("quest:a").NextUInt32());
    }

    [Fact]
    public async Task AtomicStore_UsesBackupWhenNewestPrimaryIsCorrupt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"urman-save-test-{Guid.NewGuid():N}");
        var store = new AtomicSaveGameStore(directory);
        try
        {
            await store.SaveAsync("slot-1", CreateSave("house", 1), TestContext.Current.CancellationToken);
            await store.SaveAsync("slot-1", CreateSave("forest", 2), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(store.SlotPath("slot-1"), "{corrupt", TestContext.Current.CancellationToken);

            var loaded = await store.LoadAsync("slot-1", CampaignFingerprint, TestContext.Current.CancellationToken);

            Assert.True(loaded.RecoveredFromBackup);
            Assert.Equal("house", loaded.Save.CurrentZone.Value);
            Assert.Equal(1, loaded.Save.SavedSequence);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("\"fieldOfView\": 75", "\"fieldOfView\": 999")]
    [InlineData("\"currentZone\": \"forest\"", "\"currentZone\": \"\"")]
    [InlineData("\"spawnPoint\": \"entry\"", "\"spawnPoint\": \"bad location\"")]
    public async Task AtomicStore_UsesBackupWhenNewestPrimaryIsSemanticallyInvalid(string original, string malformed)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"urman-save-test-{Guid.NewGuid():N}");
        var store = new AtomicSaveGameStore(directory);
        try
        {
            await store.SaveAsync("slot-1", CreateSave("house", 1), TestContext.Current.CancellationToken);
            await store.SaveAsync("slot-1", CreateSave("forest", 2), TestContext.Current.CancellationToken);
            var invalid = await File.ReadAllTextAsync(store.SlotPath("slot-1"), TestContext.Current.CancellationToken);
            Assert.Contains(original, invalid, StringComparison.Ordinal);
            invalid = invalid.Replace(original, malformed, StringComparison.Ordinal);
            await File.WriteAllTextAsync(store.SlotPath("slot-1"), invalid, TestContext.Current.CancellationToken);

            var loaded = await store.LoadAsync("slot-1", CampaignFingerprint, TestContext.Current.CancellationToken);

            Assert.True(loaded.RecoveredFromBackup);
            Assert.Equal("house", loaded.Save.CurrentZone.Value);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Codec_RejectsWrongCampaignFingerprint()
    {
        var codec = new SaveGameV3Codec();
        var bytes = codec.Encode(CreateSave("house", 1));

        Assert.Throws<InvalidDataException>(() => codec.Decode(bytes, new string('0', 64)));
    }

    [Fact]
    public void Codec_RoundTripsInputBindingsAndRejectsDuplicateActions()
    {
        var codec = new SaveGameV3Codec();
        var save = CreateSave("house", 1);

        var restored = codec.Decode(codec.Encode(save), CampaignFingerprint);

        var binding = Assert.Single(restored.Settings.InputBindings);
        Assert.Equal("interact", binding.Action);
        Assert.Equal(69, binding.KeyboardPhysicalKeycode);
        Assert.Equal(0, binding.GamepadButton);
        Assert.Null(binding.GamepadAxis);
        Assert.Null(binding.GamepadAxisSign);

        var duplicated = save with
        {
            Settings = save.Settings with { InputBindings = [binding, binding] }
        };
        Assert.Throws<InvalidDataException>(() => codec.Encode(duplicated));
    }

    [Fact]
    public void Codec_RejectsMalformedGamepadAxisBindings()
    {
        var codec = new SaveGameV3Codec();
        var save = CreateSave("house", 1);

        var missingSign = save with
        {
            Settings = save.Settings with
            {
                InputBindings = [new InputBindingSnapshot("interact", 69, null, 2, null)]
            }
        };
        Assert.Throws<InvalidDataException>(() => codec.Encode(missingSign));

        var invalidSign = save with
        {
            Settings = save.Settings with
            {
                InputBindings = [new InputBindingSnapshot("interact", 69, null, 2, 0.5)]
            }
        };
        Assert.Throws<InvalidDataException>(() => codec.Encode(invalidSign));
    }

    [Fact]
    public void Codec_RoundTripsAccessibilitySettings()
    {
        var codec = new SaveGameV3Codec();
        var save = CreateSave("house", 1) with
        {
            Settings = CreateSave("house", 1).Settings with
            {
                Accessibility = new AccessibilitySettingsSnapshot(
                    ReducedMotion: true,
                    HighContrast: true,
                    TextScale: 1.35,
                    Subtitles: false,
                    AudioDescriptions: false)
            }
        };

        var restored = codec.Decode(codec.Encode(save), CampaignFingerprint);

        Assert.True(restored.Settings.Accessibility.ReducedMotion);
        Assert.True(restored.Settings.Accessibility.HighContrast);
        Assert.Equal(1.35, restored.Settings.Accessibility.TextScale);
        Assert.False(restored.Settings.Accessibility.Subtitles);
        Assert.False(restored.Settings.Accessibility.AudioDescriptions);
    }

    [Fact]
    public void Codec_RejectsUnsupportedAccessibilityTextScale()
    {
        var save = CreateSave("house", 1) with
        {
            Settings = CreateSave("house", 1).Settings with
            {
                Accessibility = new AccessibilitySettingsSnapshot(TextScale: 1.7)
            }
        };

        Assert.Throws<InvalidDataException>(() => new SaveGameV3Codec().Encode(save));
    }

    [Fact]
    public void Codec_RoundTripsDeterministicSchedulerWithoutPersistingLeases()
    {
        var scheduler = new DeterministicScheduler();
        scheduler.Schedule(new(
            "job/forest-voice",
            "scene/kara-urman",
            42,
            JsonSerializer.SerializeToElement(new { command = "voice.play" })));
        _ = scheduler.ClaimDue(42);
        var codec = new SaveGameV3Codec();
        var save = CreateSave("forest", 3) with { Scheduler = scheduler.CaptureSnapshot() };

        var restored = codec.Decode(codec.Encode(save), CampaignFingerprint);
        var reoffered = Assert.Single(DeterministicScheduler.Restore(restored.Scheduler).ClaimDue(42));

        Assert.Equal("job/forest-voice", reoffered.JobId);
        Assert.Equal("scheduled:job/forest-voice:42", reoffered.OccurrenceId);
    }

    [Fact]
    public async Task AtomicStore_FailsSafelyWhenBothPrimaryAndBackupAreUnreadable()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"urman-save-test-{Guid.NewGuid():N}");
        var store = new AtomicSaveGameStore(directory);
        try
        {
            await store.SaveAsync("slot-1", CreateSave("house", 1), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(store.SlotPath("slot-1"), "{corrupt", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(store.BackupPath("slot-1"), "null", TestContext.Current.CancellationToken);

            await Assert.ThrowsAnyAsync<InvalidDataException>(
                () => store.LoadAsync("slot-1", CampaignFingerprint, TestContext.Current.CancellationToken));

            // A never-written slot is a disabled fresh Continue: the store
            // must refuse it with the same safe failure, never fabricate a
            // new session silently.
            await Assert.ThrowsAnyAsync<InvalidDataException>(
                () => store.LoadAsync("slot-never-written", CampaignFingerprint, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AtomicStore_RecoversBackupWhenPrimaryIsTruncated()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"urman-save-test-{Guid.NewGuid():N}");
        var store = new AtomicSaveGameStore(directory);
        try
        {
            await store.SaveAsync("slot-1", CreateSave("house", 1), TestContext.Current.CancellationToken);
            await store.SaveAsync("slot-1", CreateSave("forest", 2), TestContext.Current.CancellationToken);

            // Simulate an interrupted write: the newest primary is cut short
            // mid-payload rather than being malformed JSON from the start.
            var payload = await File.ReadAllBytesAsync(store.SlotPath("slot-1"), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(store.SlotPath("slot-1"), payload[..(payload.Length / 2)], TestContext.Current.CancellationToken);

            var loaded = await store.LoadAsync("slot-1", CampaignFingerprint, TestContext.Current.CancellationToken);

            Assert.True(loaded.RecoveredFromBackup);
            // The store keeps one backup generation: after two saves the
            // backup holds the FIRST beat ("house"), while the primary held
            // the second ("forest") before the simulated interruption.
            Assert.Equal("house", loaded.Save.CurrentZone.Value);
            Assert.Equal(1, loaded.Save.SavedSequence);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StoreRoundTrip_RestoresKernelStateAndSequenceForBothBeats()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"urman-save-test-{Guid.NewGuid():N}");
        var store = new AtomicSaveGameStore(directory);
        var handlers = new Dictionary<string, RuntimeCommandHandler>
        {
            ["clue.confirm"] = (_, _) => new(
                Effects:
                [
                    new(StateEffectOperation.Set, "clue", JsonSerializer.SerializeToElement("confirmed"))
                ],
                // EventSequence counts committed events, so each beat emits
                // exactly one to make the restored sequence deterministic.
                Events:
                [
                    new("knowledge.changed", JsonSerializer.SerializeToElement(new { id = "clue" }))
                ],
                Claims: [],
                Value: JsonSerializer.SerializeToElement(new { accepted = true })),
            ["rule.confirm"] = (_, _) => new(
                Effects:
                [
                    new(StateEffectOperation.Set, "rule", JsonSerializer.SerializeToElement("confirmed"))
                ],
                Events:
                [
                    new("knowledge.changed", JsonSerializer.SerializeToElement(new { id = "rule" }))
                ],
                Claims: [],
                Value: JsonSerializer.SerializeToElement(new { accepted = true }))
        };
        var codec = new SaveGameV3Codec();
        try
        {
            // Beat 1: one committed interaction; Beat 2: a second one on a
            // fresh kernel, mirroring SaveGameV3 capture/restore ownership.
            using var kernelA = new Runtime.RuntimeKernel(JsonSerializer.SerializeToElement(new { }), handlers);
            _ = await kernelA.DispatchAsync(
                new GameCommand("occurrence:1", "clue.confirm", JsonSerializer.SerializeToElement(new { })),
                TestContext.Current.CancellationToken);
            await store.SaveAsync("slot-1", SnapshotSave(kernelA.CaptureSnapshot()), TestContext.Current.CancellationToken);

            using var kernelB = new Runtime.RuntimeKernel(JsonSerializer.SerializeToElement(new { }), handlers);
            _ = await kernelB.DispatchAsync(
                new GameCommand("occurrence:1", "clue.confirm", JsonSerializer.SerializeToElement(new { })),
                TestContext.Current.CancellationToken);
            _ = await kernelB.DispatchAsync(
                new GameCommand("occurrence:2", "rule.confirm", JsonSerializer.SerializeToElement(new { })),
                TestContext.Current.CancellationToken);
            await store.SaveAsync("slot-1", SnapshotSave(kernelB.CaptureSnapshot()), TestContext.Current.CancellationToken);

            // Current primary restores beat 2: state, sequence and occurrence
            // dedupe all survive the store+codec round trip.
            var loadedB = await store.LoadAsync("slot-1", CampaignFingerprint, TestContext.Current.CancellationToken);
            using var restoredB = Runtime.RuntimeKernel.Restore(loadedB.Save.Runtime, handlers);
            Assert.True(
                restoredB.CaptureSnapshot().EventSequence == 2,
                $"runtime seq={loadedB.Save.Runtime.EventSequence} saved={loadedB.Save.SavedSequence} state={loadedB.Save.Runtime.State.GetRawText()}");
            Assert.Equal("confirmed", restoredB.SelectState().GetProperty("clue").GetString());
            var replay = await restoredB.DispatchAsync(
                new GameCommand("occurrence:1", "clue.confirm", JsonSerializer.SerializeToElement(new { })),
                TestContext.Current.CancellationToken);
            Assert.NotEqual(CommandStatus.Committed, replay.Status);
            Assert.Equal(2, restoredB.CaptureSnapshot().EventSequence);

            // The backup still holds beat 1: clue known, final rule unknown,
            // sequence 1 — the reveal-not-before ordering survives round trip.
            var backupBytes = await File.ReadAllBytesAsync(store.BackupPath("slot-1"), TestContext.Current.CancellationToken);
            var loadedA = codec.Decode(backupBytes, CampaignFingerprint);
            using var restoredA = Runtime.RuntimeKernel.Restore(loadedA.Runtime, handlers);
            Assert.Equal(1, restoredA.CaptureSnapshot().EventSequence);
            Assert.Equal("confirmed", restoredA.SelectState().GetProperty("clue").GetString());
            Assert.False(restoredA.SelectState().TryGetProperty("rule", out _));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static SaveGameV3 SnapshotSave(RuntimeSnapshot snapshot) =>
        new(
            SaveGameV3.CurrentSchemaVersion,
            CampaignFingerprint,
            snapshot.EventSequence,
            snapshot,
            new LogicalClockSnapshot(120),
            new OwnerRngStreamsSnapshot(42, []),
            new DeterministicSchedulerSnapshot([], []),
            [],
            new WorldLocationId("village_day"),
            new SpawnPointId("arrival"),
            new PlayerTransform(new(0, 1.7, 0), new(0, 0, 0)),
            new GameSettingsSnapshot(
                75,
                0.2,
                false,
                true,
                "medium",
                "keyboard-mouse",
                [new InputBindingSnapshot("interact", 69, 0)]),
            300);

    private static SaveGameV3 CreateSave(string zone, long sequence)
    {
        var state = JsonSerializer.SerializeToElement(new { location = zone });
        return new(
            SaveGameV3.CurrentSchemaVersion,
            CampaignFingerprint,
            sequence,
            new RuntimeSnapshot(Runtime.RuntimeKernel.SnapshotSchemaVersion, state, [], [], sequence),
            new LogicalClockSnapshot(120),
            new OwnerRngStreamsSnapshot(42, []),
            new DeterministicSchedulerSnapshot([], []),
            [],
            new WorldLocationId(zone),
            new SpawnPointId("entry"),
            new PlayerTransform(new(0, 1.7, 0), new(0, 0, 0)),
            new GameSettingsSnapshot(
                75,
                0.2,
                false,
                true,
                "medium",
                "keyboard-mouse",
                [new InputBindingSnapshot("interact", 69, 0)]),
            300);
    }
}
