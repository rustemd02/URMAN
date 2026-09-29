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

    // Review idea 2: near the forest edge the station drowns in static and,
    // once, something like "Ай…" comes through; babai switches the radio off
    // without looking and without a word. No caption, no second time.
    private async void RadioCatchesName()
    {
        var radio = _rideRadio;
        if (radio is null || !IsInstanceValid(radio) || !ResourceLoader.Exists(RadioEvpStream))
        {
            StopRideRadio(9);
            return;
        }
        // After babai's line about the fields.
        await ToSignal(GetTree().CreateTimer(5.5), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree() || _prologueSkipRequested || !IsInstanceValid(radio) || _rideRadio != radio) return;
        CreateTween().TweenProperty(radio, "volume_db", -26f, 1.4);
        var evp = new AudioStreamPlayer
        {
            Name = "PrologueRadioStatic",
            Stream = ResourceLoader.Load<AudioStream>(RadioEvpStream),
            Bus = AudioSettingsService.AmbienceBus,
            VolumeDb = -7f
        };
        AddChild(evp);
        evp.Play();
        await ToSignal(GetTree().CreateTimer(3.25), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree()) return;
        if (_rideNiva is not null && IsInstanceValid(_rideNiva))
            UiFoley.PlayWorld(this, _rideNiva.ToGlobal(new Vector3(0, 1.05f, -.55f)), "ui_click", -4f, 6f, 2f);
        _rideRadio = null;
        if (IsInstanceValid(radio)) radio.QueueFree();
        if (IsInstanceValid(evp)) evp.QueueFree();
    }

    private const string RadioEvpStream = "res://assets/audio/act1/foley/forest/radio_evp.wav";
}
