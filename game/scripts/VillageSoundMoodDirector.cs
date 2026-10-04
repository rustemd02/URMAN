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
    private const float VillageFullDb = -3f;
    private const float DreadDayFullDb = -10f;
    private const float NightVillageFullDb = -12f;
    private const float NightDreadFullDb = -4f;

    private readonly AudioStreamPlayer?[] _layers = new AudioStreamPlayer?[2];
    private VillageHouseholdDirector? _households;
    private AmbientAudioDirector? _ambient;
    private bool _headless;
    private string _zone = string.Empty;
    private bool _audible;
    private float _mood = DefaultMood;
    private bool _weatherEnabled = true;
    private bool _moodManual;
    private bool _adhanFocus;

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

    /// <summary>
    /// While a real adhan plays from the minaret, the dread layer yields (the
    /// expert brief: the call must never sit under the creepy hum or owls).
    /// The village life layer keeps its level.
    /// </summary>
    public void SetAdhanFocus(bool active)
    {
        if (_adhanFocus == active)
        {
            return;
        }

        _adhanFocus = active;
        SetMeta("adhanFocus", _adhanFocus);
        Apply();
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

    /// <summary>Reapplies the mix when the active zone or audibility changes.</summary>
    public void Tick(double delta, string zoneId, bool audible)
    {
        var zoneChanged = !string.Equals(_zone, zoneId, StringComparison.Ordinal);
        var audibilityChanged = _audible != audible;
        _zone = zoneId;
        _audible = audible;
        if (!zoneChanged && !audibilityChanged)
        {
            return;
        }

        if (!_moodManual && zoneChanged)
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
        var audible = _audible && AudibleZone;
        var night = string.Equals(_zone, "kara_urman_night", StringComparison.Ordinal);
        var presence = Mathf.Clamp((_mood - .06f) / .84f, 0f, 1f);
        var villageShape = Mathf.Sqrt(presence);
        float villageDb;
        float dreadDb;
        if (night)
        {
            // Night: the village recedes indoors while the dread layer sits a
            // little above the wind, so the empty street still reads as tense.
            villageDb = Mathf.Lerp(LayerFloorDb, NightVillageFullDb, villageShape);
            dreadDb = Mathf.Lerp(NightDreadFullDb, -12f, presence);
        }
        else
        {
            villageDb = Mathf.Lerp(LayerFloorDb, VillageFullDb, villageShape);
            dreadDb = Mathf.Lerp(DreadDayFullDb, LayerFloorDb, presence);
        }

        // Interiors, dialogue, the prologue and the debug panel's modal all
        // silence the layers; the weather bed owns its own shelter routing.
        if (!audible)
        {
            villageDb = LayerFloorDb;
            dreadDb = LayerFloorDb;
        }

        if (_adhanFocus)
        {
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
