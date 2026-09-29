using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // Babai switches the Niva radio on: a quiet Tatar violin under the ride. The
    // recording is the CC BY-SA 4.0 Commons track the Avyl FM station already
    // credits (assets/audio/act1/radio/credits-*.json).
    private const string RideRadioStream = "res://assets/audio/act1/radio/music-one.wav";
    private const float RideRadioVolumeDb = -8f;
    private AudioStreamPlayer? _rideRadio;

    private async void StartRideRadio()
    {
        if (_rideRadio is not null || !ResourceLoader.Exists(RideRadioStream)) return;
        // He says the line first, then the radio clicks on.
        await ToSignal(GetTree().CreateTimer(2.6), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree() || _prologueSkipRequested || _rideRadio is not null) return;
        _rideRadio = new AudioStreamPlayer
        {
            Name = "PrologueRideRadio",
            Stream = ResourceLoader.Load<AudioStream>(RideRadioStream),
            Bus = AudioSettingsService.AmbienceBus,
            VolumeDb = -60f
        };
        AddChild(_rideRadio);
        _rideRadio.Play();
        CreateTween().TweenProperty(_rideRadio, "volume_db", RideRadioVolumeDb, 4.0);
    }

    private void StopRideRadio(double fadeSeconds)
    {
        var radio = _rideRadio;
        _rideRadio = null;
        if (radio is null || !IsInstanceValid(radio)) return;
        var fade = CreateTween();
        fade.TweenProperty(radio, "volume_db", -60f, fadeSeconds);
        fade.TweenCallback(Callable.From(radio.QueueFree));
    }
}
