using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>
/// UIUX-011: owns the Act I audio bus set (Master, Ambience, Voice, SFX),
/// per-bus volume and persistence. Presentation owners (AmbientAudioDirector,
/// AudioCueUi, future SFX players) only route their players to the buses;
/// this service holds no narrative state. Muted buses keep captions working
/// (AudioCueUi captions are visual).
/// </summary>
public static class AudioSettingsService
{
    public const string MasterBus = "Master";
    public const string AmbienceBus = "Ambience";
    public const string VoiceBus = "Voice";
    public const string SfxBus = "SFX";

    private const int CurrentVersion = 1;
    private const float MuteVolumeLinear = 0.001f;

    private static readonly string[] RoutedBuses = [AmbienceBus, VoiceBus, SfxBus];
    private static Dictionary<string, float>? _volumes;

    private static string FilePath => ProjectSettings.GlobalizePath("user://audio-settings.json");

    /// <summary>Idempotently creates the routed buses and applies stored volumes.</summary>
    public static void EnsureBuses()
    {
        foreach (var busName in RoutedBuses)
        {
            if (AudioServer.GetBusIndex(busName) != -1)
            {
                continue;
            }

            var index = AudioServer.BusCount;
            AudioServer.AddBus(index);
            AudioServer.SetBusName(index, busName);
            AudioServer.SetBusSend(index, MasterBus);
        }

        _volumes ??= LoadFile() ?? DefaultVolumes();
        foreach (var (busName, volume) in _volumes)
        {
            ApplyToServer(busName, volume);
        }
    }

    public static float GetVolume(string busName)
    {
        EnsureBuses();
        return _volumes!.TryGetValue(busName, out var volume) ? volume : 1f;
    }

    public static void SetVolume(string busName, float volume)
    {
        EnsureBuses();
        var clamped = Math.Clamp(volume, 0f, 1f);
        _volumes![busName] = clamped;
        ApplyToServer(busName, clamped);
        SaveFile();
    }

    private static void ApplyToServer(string busName, float volume)
    {
        var index = AudioServer.GetBusIndex(busName);
        if (index == -1)
        {
            return;
        }

        AudioServer.SetBusMute(index, volume < MuteVolumeLinear);
        AudioServer.SetBusVolumeDb(index, volume < MuteVolumeLinear ? -80f : 20f * MathF.Log10(MathF.Max(volume, MuteVolumeLinear)));
    }

    private static Dictionary<string, float> DefaultVolumes() => new()
    {
        [MasterBus] = 1f,
        [AmbienceBus] = 1f,
        [VoiceBus] = 1f,
        [SfxBus] = 1f
    };

    private static Dictionary<string, float>? LoadFile()
    {
        try
        {
            if (!System.IO.File.Exists(FilePath))
            {
                return null;
            }

            var payload = JsonSerializer.Deserialize<StoredVolumes>(
                System.IO.File.ReadAllBytes(FilePath));
            if (payload is null || payload.Version != CurrentVersion || payload.Volumes is null)
            {
                return null;
            }

            var volumes = DefaultVolumes();
            foreach (var (busName, volume) in payload.Volumes)
            {
                if (volumes.ContainsKey(busName))
                {
                    volumes[busName] = Math.Clamp(volume, 0f, 1f);
                }
            }

            return volumes;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Audio settings could not be loaded: {exception.Message}");
            return null;
        }
    }

    private static void SaveFile()
    {
        try
        {
            var temporaryPath = System.IO.Path.Combine(
                ProjectSettings.GlobalizePath("user://"),
                $"audio-settings.{System.Guid.NewGuid():N}.tmp");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                new StoredVolumes(CurrentVersion, _volumes!));
            System.IO.File.WriteAllBytes(temporaryPath, bytes);
            System.IO.File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Audio settings could not be saved: {exception.Message}");
        }
    }

    public static void DeleteFile()
    {
        try
        {
            if (System.IO.File.Exists(FilePath))
            {
                System.IO.File.Delete(FilePath);
            }
        }
        catch (IOException exception)
        {
            GD.PushWarning($"Audio settings could not be removed: {exception.Message}");
        }
    }

    private sealed record StoredVolumes(int Version, Dictionary<string, float> Volumes);
}
