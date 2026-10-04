using Godot;

namespace Urman.Godot;

/// <summary>
/// Licensed adhan recording at the mosque's sheltered azanchi point. The
/// recording is a real muezzin field recording (provenance in the sound_mood
/// credits and game credits); nothing plays on a timer until a validated local
/// prayer calendar exists, so the minaret keeps its honest hook. The debug
/// sound panel, and any future calendar, call <see cref="TryPlayAdhan"/>.
/// </summary>
public partial class Act1ConnectedWorld
{
    private AudioStreamPlayer3D? _adhanPlayer;

    internal bool AdhanRecordingReady => ResourceLoader.Exists(VillageSoundMoodDirector.AdhanPath);

    internal bool AdhanPlaying => _adhanPlayer is { Playing: true };

    internal bool TryPlayAdhan()
    {
        if (!AdhanRecordingReady || !TryGetMosqueAdhanPoint(out var point))
        {
            return false;
        }

        if (_adhanPlayer is null || !IsInstanceValid(_adhanPlayer))
        {
            AudioSettingsService.EnsureBuses();
            _adhanPlayer = new AudioStreamPlayer3D
            {
                Name = "MosqueAdhanPlayer",
                Bus = AudioSettingsService.AmbienceBus,
                VolumeDb = -5f,
                MaxDistance = 400f,
                UnitSize = 28f,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                AttenuationFilterDb = -3f
            };
            AddChild(_adhanPlayer);
            // The call stops only at its natural end (expert brief 07); the
            // focus then releases itself.
            _adhanPlayer.Finished += () =>
            {
                SoundMood()?.SetAdhanFocus(false);
                SetMeta("adhanState", "idle");
            };
        }

        var stream = ResourceLoader.Load<AudioStream>(VillageSoundMoodDirector.AdhanPath);
        if (stream is null)
        {
            return false;
        }

        _adhanPlayer.Stop();
        _adhanPlayer.Stream = stream;
        _adhanPlayer.GlobalPosition = point.Origin;
        _adhanPlayer.Play();
        SoundMood()?.SetAdhanFocus(true);
        SetMeta("adhanState", "playing-licensed-recording");
        SetMeta("adhanSource", "mosque azanchi lantern point");
        GD.Print("mosque-adhan: licensed recorded adhan starts at the azanchi point; prayer calendar remains open");
        return true;
    }

    internal void StopAdhan()
    {
        if (_adhanPlayer is null || !IsInstanceValid(_adhanPlayer))
        {
            return;
        }

        _adhanPlayer.Stop();
        SoundMood()?.SetAdhanFocus(false);
        SetMeta("adhanState", "idle");
    }

    /// <summary>
    /// The themed owner floors its dread layer while the call sounds, so the
    /// adhan never sits under the creepy hum or owls (expert brief 07).
    /// </summary>
    private VillageSoundMoodDirector? SoundMood() =>
        GetTree()?.GetFirstNodeInGroup("village_sound_mood") as VillageSoundMoodDirector;
}
