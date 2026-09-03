using Godot;

namespace Urman.Godot;

/// <summary>
/// AUDIO-010: shared UI foley helper. Hosts attach a dedicated SFX-bus
/// player; sounds are project-original procedural samples played through a
/// small cache. Presentation-only — no gameplay or narrative state.
/// </summary>
public static class UiFoley
{
    private const string FoleyDir = "res://assets/audio/act1/foley";
    private static readonly System.Collections.Generic.Dictionary<string, AudioStream> Cache = new();

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

    /// <summary>Plays a named foley sample; silent in headless runs.</summary>
    public static void Play(AudioStreamPlayer? player, string sample)
    {
        // Headless runs have no audio output; the ambient director applies
        // the same guard so teardown never races a playing sample.
        if (player is null || DisplayServer.GetName() == "headless")
        {
            return;
        }

        if (!Cache.TryGetValue(sample, out var stream))
        {
            var path = $"{FoleyDir}/{sample}.wav";
            stream = ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
            Cache[sample] = stream!;
        }

        if (stream is null)
        {
            return;
        }

        player.Stream = stream;
        player.Play();
    }
}
