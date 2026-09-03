using System.Text.Json;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// UIUX-007 / SAVE-006: versioned user preferences store. Holds display,
/// input and accessibility preferences only — narrative, quest and world
/// state remain RuntimeBridge/SaveGameV3-owned. A missing or unreadable file
/// is a safe default (null), never a crash.
/// </summary>
public static class UserSettingsStore
{
    private const int CurrentVersion = 1;

    private static string FilePath => ProjectSettings.GlobalizePath("user://settings.json");

    public static GameSettingsSnapshot? TryLoad()
    {
        try
        {
            if (!System.IO.File.Exists(FilePath))
            {
                return null;
            }

            var payload = JsonSerializer.Deserialize<StoredSettings>(
                System.IO.File.ReadAllBytes(FilePath));
            if (payload is null || payload.Version != CurrentVersion || payload.Settings is null)
            {
                return null;
            }

            return payload.Settings;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            GD.PushWarning($"User settings could not be loaded: {exception.Message}");
            return null;
        }
    }

    public static void Save(GameSettingsSnapshot settings)
    {
        try
        {
            var directory = ProjectSettings.GlobalizePath("user://");
            var temporaryPath = System.IO.Path.Combine(directory, $"settings.{System.Guid.NewGuid():N}.tmp");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new StoredSettings(CurrentVersion, settings));
            System.IO.File.WriteAllBytes(temporaryPath, bytes);
            System.IO.File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"User settings could not be saved: {exception.Message}");
        }
    }

    public static void Delete()
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
            GD.PushWarning($"User settings could not be removed: {exception.Message}");
        }
    }

    private sealed record StoredSettings(int Version, GameSettingsSnapshot Settings);
}
