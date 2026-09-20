using System.Text.Json;
using System.Text.Json.Serialization;
using Urman.Core.Contracts;
using Urman.Core.Determinism;

namespace Urman.Core.Persistence;

public sealed record Vector3Value(double X, double Y, double Z);

public sealed record PlayerTransform(Vector3Value Position, Vector3Value RotationDegrees);

public sealed record InputBindingSnapshot(
    string Action,
    long KeyboardPhysicalKeycode,
    int? GamepadButton,
    int? GamepadAxis = null,
    double? GamepadAxisSign = null);

public sealed record AccessibilitySettingsSnapshot(
    bool ReducedMotion = false,
    bool HighContrast = false,
    double TextScale = 1.0,
    bool Subtitles = true,
    bool AudioDescriptions = true)
{
    public static AccessibilitySettingsSnapshot Default { get; } = new();
}

public sealed record GameSettingsSnapshot(
    double FieldOfView,
    double MouseSensitivity,
    bool MotionBlur,
    bool HeadBob,
    string GraphicsPreset,
    string InputDevice,
    IReadOnlyList<InputBindingSnapshot> InputBindings)
{
    public AccessibilitySettingsSnapshot Accessibility { get; init; } = AccessibilitySettingsSnapshot.Default;

    /// <summary>
    /// ACT1-LANG.5: starting Tatar knowledge chosen by the player
    /// ("none" | "some" | "fluent"). Older settings files and saves without
    /// this field mean "none", the historical behaviour.
    /// </summary>
    public string TatarLanguageLevel { get; init; } = "none";
}

public sealed record SaveGameV3(
    int SaveSchemaVersion,
    string CampaignFingerprint,
    long SavedSequence,
    RuntimeSnapshot Runtime,
    LogicalClockSnapshot Clock,
    OwnerRngStreamsSnapshot RngStreams,
    DeterministicSchedulerSnapshot Scheduler,
    IReadOnlyList<CapabilitySessionSnapshot> Capabilities,
    WorldLocationId CurrentZone,
    SpawnPointId SpawnPoint,
    PlayerTransform PlayerTransform,
    GameSettingsSnapshot Settings,
    double PlayTimeSeconds)
{
    public const int CurrentSchemaVersion = 3;
}

public sealed record SaveLoadResult(SaveGameV3 Save, bool RecoveredFromBackup);

public sealed class SaveGameV3Codec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new WorldLocationIdJsonConverter(),
            new SpawnPointIdJsonConverter()
        }
    };

    public byte[] Encode(SaveGameV3 save)
    {
        Validate(save);
        return JsonSerializer.SerializeToUtf8Bytes(save, Options);
    }

    public SaveGameV3 Decode(ReadOnlySpan<byte> bytes, string? expectedCampaignFingerprint = null)
    {
        SaveGameV3 save;
        try
        {
            save = JsonSerializer.Deserialize<SaveGameV3>(bytes, Options)
                ?? throw new InvalidDataException("SaveGameV3 payload is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("SaveGameV3 contains invalid JSON.", exception);
        }

        Validate(save);
        if (expectedCampaignFingerprint is not null &&
            !StringComparer.Ordinal.Equals(save.CampaignFingerprint, expectedCampaignFingerprint))
        {
            throw new InvalidDataException("SaveGameV3 campaign fingerprint does not match the loaded campaign.");
        }

        return save;
    }

    private static void Validate(SaveGameV3 save)
    {
        if (save is null || save.Runtime is null || save.Clock is null || save.RngStreams is null || save.Scheduler is null ||
            save.Capabilities is null || save.PlayerTransform is null || save.PlayerTransform.Position is null ||
            save.PlayerTransform.RotationDegrees is null || save.Settings is null)
        {
            throw new InvalidDataException("SaveGameV3 is missing a required object.");
        }

        if (save.SaveSchemaVersion != SaveGameV3.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported save schema version {save.SaveSchemaVersion}.");
        }

        if (string.IsNullOrEmpty(save.CampaignFingerprint) || save.CampaignFingerprint.Length != 64 ||
            save.CampaignFingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("SaveGameV3 campaign fingerprint must be a SHA-256 hex string.");
        }

        if (save.SavedSequence != save.Runtime.EventSequence || save.SavedSequence < 0)
        {
            throw new InvalidDataException("SaveGameV3 saved sequence must equal the runtime event sequence.");
        }

        if (save.Runtime.SchemaVersion != Runtime.RuntimeKernel.SnapshotSchemaVersion ||
            save.Runtime.State.ValueKind != JsonValueKind.Object || save.Runtime.Claims is null ||
            save.Runtime.Occurrences is null || save.Clock.Tick < 0 || save.RngStreams.Streams is null ||
            save.Scheduler.Jobs is null || save.Scheduler.Fired is null)
        {
            throw new InvalidDataException("SaveGameV3 runtime or deterministic state is malformed.");
        }

        try
        {
            _ = DeterministicScheduler.Restore(save.Scheduler);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException("SaveGameV3 scheduler state is malformed.", exception);
        }

        if (save.PlayTimeSeconds < 0 || !double.IsFinite(save.PlayTimeSeconds))
        {
            throw new InvalidDataException("SaveGameV3 play time must be finite and non-negative.");
        }

        if (save.Settings.FieldOfView is < 65 or > 90 || !double.IsFinite(save.Settings.FieldOfView) ||
            save.Settings.MouseSensitivity <= 0 || !double.IsFinite(save.Settings.MouseSensitivity) ||
            string.IsNullOrWhiteSpace(save.Settings.GraphicsPreset) || string.IsNullOrWhiteSpace(save.Settings.InputDevice) ||
            save.Settings.InputBindings is null || save.Settings.Accessibility is null ||
            save.Settings.Accessibility.TextScale is < 0.8 or > 1.6 ||
            !double.IsFinite(save.Settings.Accessibility.TextScale))
        {
            throw new InvalidDataException("SaveGameV3 settings are outside supported ranges.");
        }

        var bindingActions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in save.Settings.InputBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Action)
                || binding.KeyboardPhysicalKeycode <= 0
                || binding.GamepadButton is < 0
                || binding.GamepadAxis is < 0 or > 7
                || (binding.GamepadAxisSign is not null &&
                    (!double.IsFinite(binding.GamepadAxisSign.Value) ||
                     Math.Abs(Math.Abs(binding.GamepadAxisSign.Value) - 1.0) > 0.0001))
                || !bindingActions.Add(binding.Action))
            {
                throw new InvalidDataException("SaveGameV3 input bindings are invalid or duplicated.");
            }

            if (binding.GamepadAxis is null && binding.GamepadAxisSign is not null ||
                binding.GamepadAxis is not null && binding.GamepadAxisSign is null)
            {
                throw new InvalidDataException("SaveGameV3 gamepad axis bindings must include both axis and sign.");
            }
        }


        if (string.IsNullOrWhiteSpace(save.CurrentZone.Value) || string.IsNullOrWhiteSpace(save.SpawnPoint.Value) ||
            !IsFinite(save.PlayerTransform.Position) || !IsFinite(save.PlayerTransform.RotationDegrees))
        {
            throw new InvalidDataException("SaveGameV3 world location or player transform is invalid.");
        }

        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var capability in save.Capabilities)
        {
            if (string.IsNullOrWhiteSpace(capability.CapabilityInstanceId) ||
                string.IsNullOrWhiteSpace(capability.ProtocolId) ||
                string.IsNullOrWhiteSpace(capability.ExactVersion) ||
                capability.StateSchemaVersion < 1 ||
                !instanceIds.Add(capability.CapabilityInstanceId))
            {
                throw new InvalidDataException("SaveGameV3 capability state is invalid or duplicated.");
            }
        }
    }

    private static bool IsFinite(Vector3Value value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);

    private sealed class WorldLocationIdJsonConverter : JsonConverter<WorldLocationId>
    {
        public override WorldLocationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("World location ID must be a string."));

        public override void Write(Utf8JsonWriter writer, WorldLocationId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class SpawnPointIdJsonConverter : JsonConverter<SpawnPointId>
    {
        public override SpawnPointId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("Spawn point ID must be a string."));

        public override void Write(Utf8JsonWriter writer, SpawnPointId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
