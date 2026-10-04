using Godot;

namespace Urman.Godot;

/// <summary>
/// The village club gramophone: near the House of Culture a Tatar retro record
/// plays from the prepared song bank with a vinyl crackle underneath, then the
/// next record comes after a pause. Presentation only. The songs were supplied
/// by the project author's instruction; their rights status is recorded in the
/// sound bank credits as permission-being-formalized.
/// </summary>
public partial class ClubGramophone : Node3D
{
    public const float AudibleRadiusMeters = 18f;

    public static readonly string[] Songs =
    [
        "res://assets/audio/act1/sound_mood/song_iske_kara_urman.wav",
        "res://assets/audio/act1/sound_mood/song_ellar_chakyra.wav",
        "res://assets/audio/act1/sound_mood/song_tatarskoe_selo.wav",
        "res://assets/audio/act1/sound_mood/song_tynlarsynmy.wav",
        "res://assets/audio/act1/sound_mood/song_kubalek.wav"
    ];

    private const string CracklePath = "res://assets/audio/act1/sound_mood/vinyl_crackle.wav";

    private readonly RandomNumberGenerator _random = new();
    private AudioStreamPlayer3D? _music;
    private AudioStreamPlayer3D? _crackle;
    private AudioStreamWav? _crackleStream;
    private Node3D? _anchor;
    private double _now;
    private double _nextSongAt;
    private int _lastSong = -1;

    public bool AnchorFound => _anchor is not null;
    public bool Playing => _music is { Playing: true };
    public int RecordsPlayed { get; private set; }
    public string CurrentSong { get; private set; } = string.Empty;

    public void Initialize(Node3D world)
    {
        if (_music is not null)
        {
            return;
        }

        _random.Randomize();
        AudioSettingsService.EnsureBuses();
        _music = MakeVoice("ClubGramophoneMusic", -8f, 30f);
        _crackle = MakeVoice("ClubGramophoneCrackle", -24f, 26f);
        _anchor = world.FindChild("HouseOfCulture", true, false) as Node3D
            ?? world.FindChild("*Club*", true, false) as Node3D;
        if (_anchor is not null)
        {
            _music.GlobalPosition = _anchor.GlobalPosition + new Vector3(0, 1.2f, -1f);
            _crackle.GlobalPosition = _music.GlobalPosition;
            SetMeta("anchorPath", _anchor.GetPath().ToString());
        }

        if (ResourceLoader.Exists(CracklePath) && ResourceLoader.Load<AudioStream>(CracklePath) is AudioStreamWav crackle)
        {
            crackle.LoopBegin = 0;
            crackle.LoopEnd = checked((int)Math.Round(crackle.GetLength() * crackle.MixRate));
            crackle.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            _crackleStream = crackle;
        }

        SetMeta("songCount", Songs.Length);
        GD.Print($"club-gramophone: anchor={AnchorFound} songs={Songs.Length} crackle={_crackleStream is not null}");
    }

    /// <summary>Starts the next record when the player is near the club.</summary>
    public void Tick(double delta, Vector3 listener, bool audible)
    {
        if (_music is null || _anchor is null || !IsInstanceValid(_anchor))
        {
            return;
        }

        _now += Math.Min(delta, .5);
        var distance = listener.DistanceTo(_anchor.GlobalPosition);
        if (!audible || distance > AudibleRadiusMeters)
        {
            if (_music.Playing)
            {
                _music.Stop();
            }

            if (_crackle is { Playing: true })
            {
                _crackle.Stop();
            }

            return;
        }

        if (_music.Playing || _now < _nextSongAt)
        {
            return;
        }

        var index = _random.RandiRange(0, Songs.Length - 1);
        if (index == _lastSong)
        {
            index = (index + 1) % Songs.Length;
        }

        var stream = ResourceLoader.Load<AudioStream>(Songs[index]);
        if (stream is null)
        {
            return;
        }

        if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        }

        _music.Stop();
        _music.Stream = stream;
        _music.Play();
        if (_crackle is not null && _crackleStream is not null)
        {
            _crackle.Stop();
            _crackle.Stream = _crackleStream;
            _crackle.Play();
        }

        _lastSong = index;
        RecordsPlayed++;
        CurrentSong = Songs[index];
        _nextSongAt = _now + stream.GetLength() + _random.RandfRange(4f, 12f);
        SetMeta("currentRecord", CurrentSong);
        SetMeta("recordsPlayed", RecordsPlayed);
    }

    private AudioStreamPlayer3D MakeVoice(string name, float volumeDb, float maxDistance)
    {
        var voice = new AudioStreamPlayer3D
        {
            Name = name,
            Bus = AudioSettingsService.AmbienceBus,
            VolumeDb = volumeDb,
            MaxDistance = maxDistance,
            UnitSize = 4f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            AttenuationFilterCutoffHz = 4200f,
            AttenuationFilterDb = -6f,
            MaxPolyphony = 1
        };
        AddChild(voice);
        return voice;
    }
}
