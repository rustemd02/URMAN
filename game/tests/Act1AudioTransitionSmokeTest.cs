using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// AUDIO-009 focused smoke: switching through every Act I zone keeps exactly
/// the manifest-mapped bed audible (one active player after the crossfade
/// settles), streams follow the manifest routing, and no zone ends with two
/// looping beds.
/// </summary>
public partial class Act1AudioTransitionSmokeTest : Node
{
    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Audio transition smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await Frames(3);

        var director = GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        if (director is null)
        {
            Fail("Audio transition smoke could not find the ambience director.");
            return;
        }

        // Zone -> manifest stem file, read from the same manifest the
        // director uses (single routing source).
        var manifest = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(
            System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/audio/ambient_manifest.json")));
        var expectedFileByZone = new Dictionary<string, string>();
        foreach (var stem in manifest?["stems"]?.AsArray() ?? [])
        {
            var file = stem?["file"]?.GetValue<string>();
            foreach (var zone in stem?["zones"]?.AsArray() ?? [])
            {
                expectedFileByZone[zone!.GetValue<string>()] = file!;
            }
        }

        var expectations = new (string Zone, string Spawn)[]
        {
            ("village_day", "arrival"),
            ("house_old_pc", "entry"),
            ("fap_clinic", "waiting_room"),
            ("zirat_road", "village_side"),
            ("kara_urman_night", "village_path")
        };

        foreach (var zone in expectations)
        {
            var expectedFile = expectedFileByZone[zone.Zone];

            main.SwitchZone(zone.Zone, zone.Spawn);
            // Let the 0.65 s crossfade finish (time-based, not frame-based:
            // headless frames are not vsync-capped).
            await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
            await Frames(2);

            if (director.CurrentZoneId != zone.Zone)
            {
                Fail($"Zone {zone.Zone}: director stayed on {director.CurrentZoneId}.");
                return;
            }

            // Headless runs intentionally never start playback; the contract
            // is the routed stream: exactly one player holds a stream and it
            // is the manifest stem for this zone.
            var streamHolders = 0;
            string? holderPath = null;
            for (var index = 0; index < director.PlayerCount; index++)
            {
                var player = director.GetNode<AudioStreamPlayer>($"AmbientPlayer{index + 1}");
                if (player.Stream is not null)
                {
                    streamHolders++;
                    holderPath = player.Stream.ResourcePath;
                }
            }

            if (streamHolders != 1
                || director.CurrentStreamPath != holderPath
                || holderPath != expectedFile)
            {
                Fail($"Zone {zone.Zone}: stream routing drifted (holders={streamHolders}, path={holderPath ?? "<null>"}, expected={expectedFile}).");
                return;
            }
        }

        GD.Print("act1-audio-transitions: PASS 5 zones -> manifest-mapped single beds, no double loops");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
