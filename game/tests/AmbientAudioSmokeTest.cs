using Godot;

namespace Urman.Godot.Tests;

public partial class AmbientAudioSmokeTest : Node
{
    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn");
        if (packed is null)
        {
            Fail("Ambient audio smoke could not load the main scene.");
            return;
        }

        var main = packed.Instantiate<Main>();
        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var director = GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        if (director is null)
        {
            Fail("Ambient audio smoke could not find the zone ambience director.");
            return;
        }

        if (director.PlayerCount != 2 || Math.Abs(director.ConfiguredCrossfadeDurationSeconds - AmbientAudioDirector.CrossfadeDurationSeconds) > 0.001)
        {
            Fail($"Ambient audio crossfade contract failed: players={director.PlayerCount}, duration={director.ConfiguredCrossfadeDurationSeconds:0.###}.");
            return;
        }

        // AUDIO-003: the arrival spawn selects the arrival sub-zone bed.
        if (!await CheckZone(director, "village_day", "ambient.village-arrival", "kara_urman_edge_ambience.wav"))
        {
            return;
        }

        // A spawn without a sub-zone bed falls back to the plain zone bed.
        main.SwitchZone("village_day", "default");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await CheckZone(director, "village_day", "ambient.village-day", "kara_urman_edge_ambience.wav"))
        {
            return;
        }

        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await CheckZone(director, "house_old_pc", "ambient.house-room", "house_room_tone.wav"))
        {
            return;
        }

        main.SwitchZone("kara_urman_night", "village_path");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await CheckZone(director, "kara_urman_night", "ambient.kara-urman-edge", "kara_urman_edge_ambience.wav"))
        {
            return;
        }

        foreach (var route in new[]
        {
            (Zone: "village_day", Spawn: "from_house", Stem: "ambient.village-yard", File: "kara_urman_edge_ambience.wav"),
            (Zone: "village_day", Spawn: "from_forest", Stem: "ambient.village-return", File: "kara_urman_edge_ambience.wav"),
            (Zone: "fap_clinic", Spawn: "waiting_room", Stem: "ambient.fap-institutional", File: "fap_institutional.wav"),
            (Zone: "zirat_road", Spawn: "village_side", Stem: "ambient.zirat-wind", File: "zirat_wind.wav")
        })
        {
            main.SwitchZone(route.Zone, route.Spawn);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await CheckZone(director, route.Zone, route.Stem, route.File)) return;
        }

        director.SetPhysicalShelter(true);
        if (!await CheckZone(director, "zirat_road", "ambient.house-room", "house_room_tone.wav")) return;
        director.SetPhysicalShelter(false);
        if (!await CheckZone(director, "zirat_road", "ambient.zirat-wind", "zirat_wind.wav")) return;

        GD.Print("ambient-audio-smoke: PASS all 8 Act I beds + native loop ranges + zone switching; mixer seam checked when windowed");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async Task<bool> CheckZone(AmbientAudioDirector director, string zoneId, string stemId, string filename)
    {
        var streamPath = director.CurrentStreamPath;
        if (director.CurrentZoneId != zoneId || director.CurrentStemId != stemId ||
            !streamPath.EndsWith(filename, StringComparison.Ordinal) ||
            !ResourceLoader.Exists(streamPath) ||
            director.ActivePlayerIndex < 0 || director.ActivePlayerIndex > 1 ||
            (director.GetMeta("ambientStatus").AsString() != "godot-audiostream-playing" &&
             director.GetMeta("ambientStatus").AsString() != "godot-audiostream-ready-headless") ||
            (DisplayServer.GetName() != "headless" && zoneId != "village_day" &&
             director.GetMeta("ambientTransition").AsString() != "crossfade"))
        {
            Fail($"Ambient audio contract failed for {zoneId}: {director.CurrentZoneId}/{director.CurrentStemId}/{streamPath}.");
            return false;
        }

        var player = director.GetNode<AudioStreamPlayer>($"AmbientPlayer{director.ActivePlayerIndex + 1}");
        if (player.Stream is not AudioStreamWav wav
            || wav.LoopMode != AudioStreamWav.LoopModeEnum.Forward
            || wav.LoopBegin != 0
            || wav.LoopEnd <= 0
            || Math.Abs(wav.LoopEnd / (double)wav.MixRate - wav.GetLength()) > 1d / wav.MixRate)
        {
            Fail($"Ambient bed {stemId} has no native whole-file loop; a Finished/Play restart leaves a gap at the seam.");
            return false;
        }

        var expectedDb = ExpectedStemVolumeDb(stemId);
        if (DisplayServer.GetName() == "headless" && Math.Abs(player.VolumeDb - expectedDb) > .01f)
        {
            Fail($"Ambient shelter gain failed for {stemId}: {player.VolumeDb} instead of {expectedDb} dB.");
            return false;
        }

        if (DisplayServer.GetName() != "headless")
        {
            var deadline = Time.GetTicksMsec() + 500;
            while (!player.Playing && Time.GetTicksMsec() < deadline)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!player.Playing)
            {
                Fail($"Ambient bed {stemId} did not start, so its mixer seam cannot be checked.");
                return false;
            }

            var finished = 0;
            void OnFinished() => finished++;
            player.Finished += OnFinished;
            player.Seek((float)Math.Max(0d, wav.GetLength() - .12d));
            await ToSignal(GetTree().CreateTimer(.35d), SceneTreeTimer.SignalName.Timeout);
            player.Finished -= OnFinished;
            if (!player.Playing || finished != 0 || player.GetPlaybackPosition() >= wav.GetLength() - .12d)
            {
                Fail($"Ambient bed {stemId} did not wrap inside the mixer (playing={player.Playing}, finished={finished}, position={player.GetPlaybackPosition():0.000}).");
                return false;
            }
        }

        return true;
    }

    private static readonly Dictionary<string, float> StemVolumes = LoadStemVolumes();

    private static Dictionary<string, float> LoadStemVolumes()
    {
        // The manifest is the single source of truth for bed gain; a tuning
        // pass must not require editing this test again.
        var volumes = new Dictionary<string, float>(StringComparer.Ordinal);
        var json = System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/audio/ambient_manifest.json"));
        if (System.Text.Json.Nodes.JsonNode.Parse(json)?["stems"]?.AsArray() is not { } stems)
        {
            return volumes;
        }

        foreach (var stem in stems)
        {
            if (stem?["id"]?.GetValue<string>() is not { Length: > 0 } id)
            {
                continue;
            }

            volumes[id] = stem["volumeDb"] is { } value ? (float)value.GetValue<double>() : -12f;
        }

        return volumes;
    }

    private static float ExpectedStemVolumeDb(string stemId) =>
        StemVolumes.TryGetValue(stemId, out var db) ? db : -12f;

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
