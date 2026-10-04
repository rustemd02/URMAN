using Godot;

namespace Urman.Godot;

/// <summary>
/// Authorship mix owner for the ordinary village: a continuous living-village
/// layer (distant talk, TV murmur, household hum) and a dread layer (low hum
/// and distant strange birds). Weather stays the authored AmbientAudioDirector
/// bed; the debug panel can switch that blizzard bed on or off. Presentation
/// only — no story, save or knowledge state.
/// </summary>
public partial class VillageSoundMoodDirector : Node
{
    public const string VillageLayerPath = "res://assets/audio/act1/sound_mood/village_life_layer.wav";
    public const string DreadLayerPath = "res://assets/audio/act1/sound_mood/village_dread_layer.wav";
    public const string AdhanPath = "res://assets/audio/act1/sound_mood/adhan.wav";

    /// <summary>The authored ordinary-village feel: alive, not a party.</summary>
    public const float DefaultMood = .9f;
    public const float NightMood = .25f;

    private const float LayerFloorDb = -60f;
    private const float VillageFullDb = -9f;
    private const float DreadFullDb = -10f;

    private readonly AudioStreamPlayer?[] _layers = new AudioStreamPlayer?[2];
    private VillageHouseholdDirector? _households;
    private AmbientAudioDirector? _ambient;
    private bool _headless;
    private string _zone = string.Empty;
    private float _mood = DefaultMood;
    private bool _weatherEnabled = true;
    private bool _moodManual;

    public float Mood => _mood;
    public bool WeatherEnabled => _weatherEnabled;
    public float VillageLayerDb => _layers[0]?.VolumeDb ?? LayerFloorDb;
    public float DreadLayerDb => _layers[1]?.VolumeDb ?? LayerFloorDb;
    public bool VillageLayerPresent => _layers[0] is not null;
    public bool DreadLayerPresent => _layers[1] is not null;
    public bool AdhanRecordingReady => ResourceLoader.Exists(AdhanPath);
    public bool AudibleZone => _zone is "village_day" or "kara_urman_night";

    public void Initialize(VillageHouseholdDirector households, AmbientAudioDirector? ambient)
    {
        _households = households;
        _ambient = ambient;
        _headless = string.Equals(DisplayServer.GetName(), "headless", StringComparison.Ordinal);
        AddToGroup("village_sound_mood");
        for (var index = 0; index < _layers.Length; index++)
        {
            var path = index == 0 ? VillageLayerPath : DreadLayerPath;
            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            AudioSettingsService.EnsureBuses();
            var player = new AudioStreamPlayer
            {
                Name = index == 0 ? "VillageLifeLayer" : "VillageDreadLayer",
                Bus = AudioSettingsService.AmbienceBus,
                VolumeDb = LayerFloorDb,
                Autoplay = false
            };
            AddChild(player);
            var stream = ResourceLoader.Load<AudioStream>(path);
            if (stream is null)
            {
                player.QueueFree();
                continue;
            }

            ConfigureLoop(stream);
            player.Stream = stream;
            if (!_headless)
            {
                player.Play();
            }

            _layers[index] = player;
        }

        _households.Mood = _mood;
        SetMeta("villageLayerPresent", VillageLayerPresent);
        SetMeta("dreadLayerPresent", DreadLayerPresent);
        SetMeta("adhanRecordingReady", AdhanRecordingReady);
        GD.Print($"village-sound-mood: layers village={VillageLayerPresent} dread={DreadLayerPresent} adhan={AdhanRecordingReady} defaultMood={DefaultMood:0.00}");
        Apply();
    }

    public void SetMood(float mood)
    {
        var clamped = Mathf.Clamp(mood, 0f, 1f);
        _moodManual = true;
        if (Math.Abs(clamped - _mood) < .001f)
        {
            return;
        }

        _mood = clamped;
        if (_households is not null)
        {
            _households.Mood = _mood;
        }

        Apply();
    }

    /// <summary>Switches the authored weather (blizzard/wind) bed on or off.</summary>
    public void SetWeatherEnabled(bool enabled)
    {
        if (_weatherEnabled == enabled)
        {
            return;
        }

        _weatherEnabled = enabled;
        SetMeta("weatherBedEnabled", _weatherEnabled);
        Ambient()?.SetBedEnabled(_weatherEnabled);
    }

    public void ToggleWeather() => SetWeatherEnabled(!_weatherEnabled);

    public void ResetToAuthoredMix()
    {
        _mood = DefaultMood;
        _moodManual = false;
        _weatherEnabled = true;
        if (_households is not null)
        {
            _households.Mood = _mood;
        }

        Ambient()?.SetBedEnabled(true);
        Apply();
    }

    /// <summary>Reapplies the mix when the active zone changes.</summary>
    public void Tick(double delta, string zoneId, bool audible)
    {
        var zoneChanged = !string.Equals(_zone, zoneId, StringComparison.Ordinal);
        _zone = zoneId;
        if (!zoneChanged)
        {
            return;
        }

        if (!_moodManual)
        {
            _mood = string.Equals(zoneId, "kara_urman_night", StringComparison.Ordinal) ? NightMood : DefaultMood;
            if (_households is not null)
            {
                _households.Mood = _mood;
            }
        }

        Apply();
    }

    private AmbientAudioDirector? Ambient()
    {
        if (_ambient is null || !GodotObject.IsInstanceValid(_ambient))
        {
            _ambient = GetTree()?.GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        }

        return _ambient;
    }

    private void Apply()
    {
        var audible = AudibleZone;
        var night = string.Equals(_zone, "kara_urman_night", StringComparison.Ordinal);
        var presence = Mathf.Clamp((_mood - .06f) / .84f, 0f, 1f);
        var villageDb = Mathf.Lerp(LayerFloorDb, VillageFullDb, Mathf.Sqrt(presence));
        var dreadDb = Mathf.Lerp(DreadFullDb, LayerFloorDb, Mathf.Pow(_mood, .7f));
        if (night)
        {
            // At night the village stays indoors: the life layer recedes and
            // the dread layer is a little closer, without becoming a stinger.
            villageDb -= 6f;
            dreadDb += 2f;
        }

        if (!audible)
        {
            villageDb = LayerFloorDb;
            dreadDb = LayerFloorDb;
        }

        SetLayer(_layers[0], villageDb);
        SetLayer(_layers[1], dreadDb);
        SetMeta("soundMood", _mood);
        SetMeta("soundMoodZone", _zone);
        SetMeta("villageLayerDb", VillageLayerDb);
        SetMeta("dreadLayerDb", DreadLayerDb);
        SetMeta("weatherBedEnabled", _weatherEnabled);
    }

    private static void SetLayer(AudioStreamPlayer? player, float volumeDb)
    {
        if (player is null || !GodotObject.IsInstanceValid(player))
        {
            return;
        }

        player.VolumeDb = Mathf.Max(volumeDb, LayerFloorDb);
    }

    private static void ConfigureLoop(AudioStream stream)
    {
        switch (stream)
        {
            case AudioStreamWav wav:
                wav.LoopBegin = 0;
                wav.LoopEnd = checked((int)Math.Round(wav.GetLength() * wav.MixRate));
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                break;
            case AudioStreamOggVorbis ogg:
                ogg.Loop = true;
                ogg.LoopOffset = 0d;
                break;
            case AudioStreamMP3 mp3:
                mp3.Loop = true;
                mp3.LoopOffset = 0d;
                break;
        }
    }
}
