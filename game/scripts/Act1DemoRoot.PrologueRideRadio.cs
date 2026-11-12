using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // Existing licensed violin recording; the receiver alone gets this narrow
    // speaker response, leaving babai's voice and the ambience bus untouched.
    private const string RideRadioStream = "res://assets/audio/act1/radio/music-one.wav";
    private const string RadioEvpStream = "res://assets/audio/act1/foley/forest/radio_evp.wav";
    private const string RideReceiverBus = "PrologueRideReceiver";
    private const float RideRadioVolumeDb = -14f;
    private AudioStreamPlayer? _rideRadio, _rideRadioHiss, _rideRadioInterference;
    private AudioEffectLowPassFilter? _rideRadioFilter;
    private float _rideRadioStartDelay = -1f, _rideRadioFade, _rideReceptionElapsed = -1f;

    private void StartRideRadio()
    {
        if (_rideRadio is not null || _rideRadioStartDelay >= 0f || !ResourceLoader.Exists(RideRadioStream)) return;
        _rideRadioStartDelay = 2.6f;
    }

    private void EnsureRideReceiver()
    {
        AudioSettingsService.EnsureBuses();
        var bus = AudioServer.GetBusIndex(RideReceiverBus);
        if (bus < 0)
        {
            AudioServer.AddBus();
            bus = AudioServer.BusCount - 1;
            AudioServer.SetBusName(bus, RideReceiverBus);
            AudioServer.SetBusSend(bus, AudioSettingsService.AmbienceBus);
            AudioServer.AddBusEffect(bus, new AudioEffectLowPassFilter { CutoffHz = 4200f, Resonance = .5f });
            AudioServer.AddBusEffect(bus, new AudioEffectHighPassFilter { CutoffHz = 180f, Resonance = .5f });
        }
        _rideRadioFilter = AudioServer.GetBusEffect(bus, 0) as AudioEffectLowPassFilter;
        if (_rideRadioFilter is { } filter) filter.CutoffHz = 4200f;
    }

    private void SetRideRadioPaused(bool paused)
    {
        void Pause(AudioStreamPlayer? player)
        {
            if (player is not null && IsInstanceValid(player)) player.StreamPaused = paused;
        }
        Pause(_rideRadio);
        Pause(_rideRadioHiss);
        Pause(_rideRadioInterference);
    }

    private void UpdateRideRadio(float delta)
    {
        delta = Mathf.Clamp(delta, 0f, .1f);
        if (_rideRadioStartDelay >= 0f)
        {
            _rideRadioStartDelay -= delta;
            if (_rideRadioStartDelay <= 0f)
            {
                _rideRadioStartDelay = -1f;
                if (_rideNiva is null || !IsInstanceValid(_rideNiva)) return;
                EnsureRideReceiver();
                _rideRadio = new AudioStreamPlayer { Name = "PrologueRideRadio", Bus = RideReceiverBus,
                    Stream = ResourceLoader.Load<AudioStream>(RideRadioStream), VolumeDb = -60f };
                AddChild(_rideRadio);
                _rideRadioFade = 0f;
                _rideRadio.Play();
            }
        }
        if (_rideRadio is not { } radio || !IsInstanceValid(radio)) return;
        _rideRadioFade = Mathf.Min(1f, _rideRadioFade + delta / 3f);
        var signal = 1f;
        if (_rideReceptionElapsed >= 0f)
        {
            _rideReceptionElapsed += delta;
            var t = _rideReceptionElapsed - 5.5f;
            if (t >= 0f)
            {
                if (_rideRadioHiss is null)
                {
                    _rideRadioHiss = new AudioStreamPlayer { Name = "PrologueRadioHiss", Bus = RideReceiverBus,
                        Stream = BuildRideReceiverHiss(), VolumeDb = -60f };
                    AddChild(_rideRadioHiss);
                    _rideRadioHiss.Play();
                    // Retain the existing one-time ambiguous sound, deep in the
                    // receiver noise rather than a loud full-band crackle sting.
                    if (ResourceLoader.Exists(RadioEvpStream))
                    {
                        _rideRadioInterference = new AudioStreamPlayer { Name = "PrologueRadioStatic", Bus = RideReceiverBus,
                            Stream = ResourceLoader.Load<AudioStream>(RadioEvpStream), VolumeDb = -32f };
                        AddChild(_rideRadioInterference);
                        _rideRadioInterference.Play();
                    }
                }
                var loss = Mathf.SmoothStep(0f, 3.2f, t);
                var flutter = .5f + .5f * Mathf.Sin(t * 5.2f + .45f * Mathf.Sin(t * 2.1f));
                signal = Mathf.Lerp(1f, .045f, loss) * Mathf.Lerp(1f, .55f + .45f * flutter, loss);
                if (_rideRadioFilter is { } filter) filter.CutoffHz = Mathf.Lerp(4200f, 1300f, loss);
                _rideRadioHiss.VolumeDb = -31f + Mathf.LinearToDb(Mathf.Max(.001f, Mathf.SmoothStep(0f, .8f, t)));
                if (t >= 3.6f)
                {
                    if (_rideNiva is { } niva && IsInstanceValid(niva))
                        UiFoley.PlayWorld(this, niva.ToGlobal(new Vector3(0, 1.05f, -.55f)), "ui_click", -14f, 6f, 2f);
                    StopRideRadio(.22);
                    return;
                }
            }
        }
        radio.VolumeDb = RideRadioVolumeDb + Mathf.LinearToDb(Mathf.Max(.001f, signal * _rideRadioFade));
    }

    // Band-limited by the receiver bus. Continuous low-level noise has no
    // impulses or clipped sample edges, unlike a full-volume crackle effect.
    private static AudioStreamWav BuildRideReceiverHiss()
    {
        const int rate = 22050, samples = rate * 4;
        var data = new byte[samples * 2];
        var random = new Random(1917);
        for (var index = 0; index < samples; index++)
        {
            var edge = Math.Min(1f, Math.Min(index, samples - 1 - index) / 220f);
            var value = (short)((random.NextDouble() * 2.0 - 1.0) * 11000 * edge);
            data[index * 2] = (byte)(value & 255);
            data[index * 2 + 1] = (byte)((value >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate, Stereo = false, Data = data };
    }

    private void StopRideRadio(double fadeSeconds)
    {
        _rideRadioStartDelay = _rideReceptionElapsed = -1f;
        foreach (var player in new[] { _rideRadio, _rideRadioHiss, _rideRadioInterference })
        {
            if (player is null || !IsInstanceValid(player)) continue;
            player.StreamPaused = false;
            var fade = player.CreateTween();
            fade.TweenProperty(player, "volume_db", -60f, fadeSeconds);
            fade.TweenCallback(Callable.From(player.QueueFree));
        }
        _rideRadio = _rideRadioHiss = _rideRadioInterference = null;
    }

    private void RadioCatchesName()
    {
        if (_rideRadio is not null && _rideReceptionElapsed < 0f) _rideReceptionElapsed = 0f;
    }
}
