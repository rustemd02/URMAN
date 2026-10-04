using Godot;

namespace Urman.Godot;

/// <summary>
/// AUDIO-010: shared UI/world foley helper. UI hosts attach a dedicated SFX-bus
/// player; world one-shots use source-positioned AudioStreamPlayer3D nodes and
/// the same cached samples. Presentation-only — no gameplay or narrative state.
/// </summary>
public static class UiFoley
{
    private const string FoleyDir = "res://assets/audio/act1/foley";
    private const string WorldFoleyGroup = "world_foley";
    private static readonly System.Collections.Generic.Dictionary<string, AudioStream?> Cache = new();
    private static readonly System.Collections.Generic.Dictionary<string, long> LastUse = new(System.StringComparer.Ordinal);
    private const int CacheCapacity = 24;
    private static long _useCounter;

    /// <summary>Creates and attaches an SFX-bus foley player for the host UI.</summary>
    public static AudioStreamPlayer Attach(Node host)
    {
        AudioSettingsService.EnsureBuses();
        var player = new AudioStreamPlayer
        {
            Name = "UiFoleyPlayer",
            Bus = AudioSettingsService.SfxBus,
            VolumeDb = -12f
        };
        host.AddChild(player);
        return player;
    }

    /// <summary>Plays a named UI foley sample; silent in headless runs.</summary>
    public static void Play(AudioStreamPlayer? player, string sample)
    {
        // Headless runs have no audio output; the ambient director applies
        // the same guard so teardown never races a playing sample.
        if (player is null || DisplayServer.GetName() == "headless")
        {
            return;
        }

        var stream = LoadStream(sample);
        if (stream is null)
        {
            return;
        }

        player.Stream = stream;
        player.Play();
    }

    /// <summary>
    /// Plays a one-shot at the physical source. The SFX bus keeps user volume
    /// authoritative; the bounded max distance prevents a remote gate from
    /// sounding like a UI cue at the listener.
    /// </summary>
    public static void PlayWorld(Node host, Vector3 globalPosition, string sample) =>
        PlayWorld(host, globalPosition, sample, -12f, 14f, 2f);

    /// <summary>
    /// A world one-shot with its own audible range: distant calls in the
    /// prologue forest carry tens of metres, unlike a door or a gate.
    /// Returns the player so a caller may move it (something running past).
    /// </summary>
    public static AudioStreamPlayer3D? PlayWorld(Node host, Vector3 globalPosition, string sample,
        float volumeDb, float maxDistance, float unitSize)
    {
        if (DisplayServer.GetName() == "headless"
            || !GodotObject.IsInstanceValid(host)
            || !host.IsInsideTree())
        {
            return null;
        }

        var stream = LoadStream(sample);
        if (stream is null)
        {
            return null;
        }

        AudioSettingsService.EnsureBuses();
        var player = new WorldFoleyPlayer
        {
            Name = $"WorldFoley_{sample}",
            Stream = stream,
            Bus = AudioSettingsService.SfxBus,
            VolumeDb = volumeDb,
            UnitSize = unitSize,
            MaxDistance = maxDistance,
            Autoplay = false
        };
        player.AddToGroup(WorldFoleyGroup);
        host.AddChild(player);
        player.GlobalPosition = globalPosition;
        player.Finished += () => player.QueueFree();
        // An awaited interaction can finish after the pause menu opens. Keep
        // its request pending until Resume; StreamPaused cannot pause a native
        // playback that AudioStreamPlayer3D has not created yet.
        player.RequestPlay(host.GetTree().GetFirstNodeInGroup("pause_menu")
            is PauseMenuUi { IsOpen: true });
        return player;
    }

    /// <summary>Pauses or resumes active source-positioned one-shots.</summary>
    public static void SetWorldPaused(SceneTree tree, bool paused)
    {
        foreach (var node in tree.GetNodesInGroup(WorldFoleyGroup))
        {
            if (node is WorldFoleyPlayer player && GodotObject.IsInstanceValid(player)
                && !player.IsQueuedForDeletion())
            {
                player.SetWorldPaused(paused);
            }
        }
    }

    /// <summary>Stops and frees source-positioned one-shots during lifecycle resets.</summary>
    public static void StopWorld(SceneTree tree)
    {
        foreach (var node in tree.GetNodesInGroup(WorldFoleyGroup))
        {
            if (node is WorldFoleyPlayer player && GodotObject.IsInstanceValid(player))
            {
                player.Cancel();
                player.QueueFree();
            }
        }
    }

    /// <summary>Test-only, like PainterlyMaterialLibrary.ClearCacheForHeadlessTests:
    /// the static stream cache would otherwise outlive the smoke scene and be
    /// reported as a resource still in use at exit.</summary>
    public static void ClearCacheForHeadlessTests()
    {
        Cache.Clear();
        LastUse.Clear();
        _useCounter = 0;
    }

    /// <summary>
    /// Bounded: foley one-shots are touched from many call sites and the set
    /// keeps growing with new sounds, so the cache evicts its least recently
    /// used entry instead of holding every sample for the session. A stream
    /// that is still playing stays alive through its player's own reference.
    /// </summary>
    private static AudioStream? LoadStream(string sample)
    {
        if (!Cache.TryGetValue(sample, out var stream))
        {
            var path = $"{FoleyDir}/{sample}.wav";
            stream = ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
            Cache[sample] = stream;
            if (Cache.Count > CacheCapacity)
            {
                var oldest = LastUse.Where(entry => entry.Key != sample)
                    .OrderBy(entry => entry.Value).Select(entry => entry.Key).FirstOrDefault();
                if (oldest is not null)
                {
                    Cache.Remove(oldest);
                    LastUse.Remove(oldest);
                }
            }
        }

        LastUse[sample] = ++_useCounter;
        return stream;
    }
}
