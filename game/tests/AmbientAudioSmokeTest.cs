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

        if (!CheckZone(director, "village_day", "ambient.village-day", "village_day_ambience.wav"))
        {
            return;
        }

        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!CheckZone(director, "house_old_pc", "ambient.house-room", "house_room_tone.wav"))
        {
            return;
        }

        main.SwitchZone("kara_urman_night", "village_path");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!CheckZone(director, "kara_urman_night", "ambient.kara-urman-edge", "kara_urman_edge_ambience.wav"))
        {
            return;
        }

        GD.Print("ambient-audio-smoke: manifest + WAV import + zone switching + looping player");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private bool CheckZone(AmbientAudioDirector director, string zoneId, string stemId, string filename)
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

        return true;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
