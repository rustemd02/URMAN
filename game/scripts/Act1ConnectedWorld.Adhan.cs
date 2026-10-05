using Godot;

namespace Urman.Godot;

/// <summary>
/// Licensed adhan recording at the mosque's sheltered azanchi point. The
/// recording is a real muezzin field recording (provenance in the sound_mood
/// credits and game credits); nothing plays on a timer until a validated local
/// prayer calendar exists, so the minaret keeps its honest hook. The debug
/// sound panel, and any future calendar, call <see cref="TryPlayAdhan"/>.
///
/// Bus exclusivity (2026-10-05): while the call sounds the square PA yields
/// through its public hold (VillagePaSystem.HoldForAdhan), so the Loudspeaker
/// bus never carries a record under the call; the hold is released from the
/// Finished handler and from <see cref="StopAdhan"/>. The call itself is never
/// suppressed by the PA.
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
                Bus = AudioSettingsService.LoudspeakerBus,
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
                Pa()?.HoldForAdhan(false);
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
        // The PA yields at once (no frame of overlap); it never changes its
        // switch mode and resumes only after Finished or StopAdhan releases.
        Pa()?.HoldForAdhan(true);
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
        Pa()?.HoldForAdhan(false);
        SetMeta("adhanState", "idle");
    }

    /// <summary>
    /// The themed owner floors its dread layer while the call sounds, so the
    /// adhan never sits under the creepy hum or owls (expert brief 07).
    /// </summary>
    private VillageSoundMoodDirector? SoundMood() =>
        GetTree()?.GetFirstNodeInGroup("village_sound_mood") as VillageSoundMoodDirector;

    /// <summary>
    /// The square PA owner (group "village_pa": the same public node handle the
    /// debug panel uses). The call tells it to yield the Loudspeaker bus, and
    /// the release paths resume it; no PA record ever plays over the adhan.
    /// </summary>
    private VillagePaSystem? Pa() =>
        GetTree()?.GetFirstNodeInGroup("village_pa") as VillagePaSystem;
}
