using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Author request 2026-10-04: loudspeakers on the main square, switchable from
/// the House of Culture, with the choice of music from the square horns or from
/// the club's own indoor speakers, plus a radio point inside the club.
///
/// The square pair runs on <see cref="AudioSettingsService.LoudspeakerBus"/>
/// (the existing street-speaker chain: reverb plus light LOFI distortion, the
/// same one the minaret adhan uses), so the cones sound like a real village PA.
/// The club pair stays on <see cref="AudioSettingsService.AmbienceBus"/>:
/// quieter and drier, as indoors.
///
/// Budget: four AudioStreamPlayer3D nodes, created once and never freed
/// (two square horns, one club music voice, one club crackle voice). At most
/// one song resource (about 1.4 MB decoded) plus the 0.6 MB crackle loop stay
/// loaded; each song is loaded on demand through ResourceLoader.CacheMode.Ignore
/// so the global resource cache never accumulates all five records, and every
/// stream is released when playback stops. Tick performs no allocations and
/// only distance/timer arithmetic, and playback is distance-culled.
///
/// The playlist reuses <see cref="ClubGramophone.Songs"/>. The sound-bank
/// credits mark those songs as permission-being-formalized; the PA must never
/// present them as licensed or cleared music.
/// </summary>
public partial class VillagePaSystem : Node3D
{
    public enum PaMode
    {
        Off,
        Square,
        Club
    }

    public const float SquareAudibleRadiusMeters = 45f;
    public const float ClubAudibleRadiusMeters = 16f;

    // A relay does not start a record instantly; a beat after the switch briefly
    // separates the modes and avoids an abrupt cut into the square horns.
    private const float ModeChangeDelaySeconds = 1.2f;
    // Resume after dialogue/menu or after the listener walked back into range.
    private const float ResumeDelaySeconds = 2.5f;
    private const float MinSongPauseSeconds = 2f;
    private const float MaxSongPauseSeconds = 4f;
    private const float FirstPlaybackOffsetMinSeconds = 1.5f;
    // Never seek into the last seconds of a record; a real PA needle drops
    // somewhere in the groove, not on the run-out.
    private const float SongTailGuardSeconds = 4f;
    private const float MutedDb = -80f;

    // Duplicated from ClubGramophone (its CracklePath is private): the same
    // project-owned vinyl loop the gramophone already validates.
    private const string CracklePath = "res://assets/audio/act1/sound_mood/vinyl_crackle.wav";
    private const string GramophoneName = "ClubGramophone";
    // Public child node names from ClubGramophone.MakeVoice, used only through
    // the public node API to mute its VolumeDb while the PA is on, because the
    // class exposes no public stop. If ClubGramophone ever gains one, the guard
    // should call it instead; if these names change, the guard degrades to a
    // no-op and the two systems can overlap (see paGramophoneGuard meta).
    private static readonly string[] GramophoneVoiceNames =
        ["ClubGramophoneMusic", "ClubGramophoneCrackle"];

    private readonly RandomNumberGenerator _random = new();
    private readonly List<(AudioStreamPlayer3D Voice, float OriginalDb)> _gramophoneVoices = [];
    private AudioStreamPlayer3D[] _squareVoices = [];
    private AudioStreamPlayer3D? _clubMusic;
    private AudioStreamPlayer3D? _clubCrackle;
    private AudioStream? _songStream;
    private AudioStreamWav? _crackleStream;
    private Node3D? _world;
    private Vector3? _squareSpeakerPoint;
    private Vector3? _clubSpeakerPoint;
    private double _now;
    private double _nextSongAt;
    private int _lastSong = -1;
    private bool _initialized;
    private bool _fixturesPending;
    private bool _gramophoneResolved;
    private bool _gramophoneMuted;
    private bool _wasPlaying;
    private bool _adhanHold;
    private string _currentSong = string.Empty;
    private string _publishedMode = string.Empty;
    private string _publishedSong = string.Empty;
    private bool _publishedListening;

    /// <summary>Current switch position: off, the square horns, or the club pair.</summary>
    public PaMode Mode { get; private set; } = PaMode.Off;

    public bool Initialized => _initialized;
    public bool IsListening => AnyPlaying();
    public string CurrentSong => _currentSong;

    /// <summary>The fixed player pool: two square horns, club music, club crackle.</summary>
    public int SpeakerCount => _squareVoices.Length + 2;

    /// <summary>The playlist source; reused from the club gramophone, never duplicated.</summary>
    public int SongCount => ClubGramophone.Songs.Length;

    /// <summary>
    /// Creates the fixed player pool and lets the connected world mount the
    /// switch, horns and radio fixtures (see
    /// <c>Act1ConnectedWorld.BuildVillagePaFixtures</c>). Idempotent, like
    /// ClubGramophone.Initialize; safe before the world is fully built (the
    /// fixture pass is retried from Tick).
    /// </summary>
    public void Initialize(Node3D world)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _world = world;
        _random.Randomize();
        AudioSettingsService.EnsureBuses();
        AddToGroup("village_pa");

        _squareVoices =
        [
            MakeVoice("VillagePaSquareHornA", AudioSettingsService.LoudspeakerBus, -2f, SquareAudibleRadiusMeters, 9f),
            MakeVoice("VillagePaSquareHornB", AudioSettingsService.LoudspeakerBus, -2f, SquareAudibleRadiusMeters, 9f)
        ];
        _clubMusic = MakeVoice("VillagePaClubMusic", AudioSettingsService.AmbienceBus, -9f, ClubAudibleRadiusMeters, 2.5f);
        _clubCrackle = MakeVoice("VillagePaClubCrackle", AudioSettingsService.AmbienceBus, -25f, ClubAudibleRadiusMeters, 2.5f);

        // Conservative default anchors on the club, replaced by the fixture
        // builder with the actual mounted horn/radio positions.
        if (world.FindChild("HouseOfCulture", true, false) is Node3D club)
        {
            PlaceSquareSpeakers(
                club.ToGlobal(new Vector3(-3.4f, 3.35f, 6.42f)),
                club.ToGlobal(new Vector3(3.4f, 3.35f, 6.42f)));
            ConfigureClubSpeaker(club.ToGlobal(new Vector3(0f, 1.4f, -1.9f)));
        }

        _fixturesPending = world is Act1ConnectedWorld;
        SetMeta("paSpeakerCount", SpeakerCount);
        SetMeta("paSongCount", SongCount);
        SetMeta("paRights", "ClubGramophone.Songs rights are permission-being-formalized; not licensed");
        SetMeta("paGramophoneGuard", "idle");
        PublishState(force: true);
        GD.Print($"village-pa: speakers={SpeakerCount} songs={SongCount} bus={AudioSettingsService.LoudspeakerBus}");
    }

    /// <summary>
    /// One frame of the PA. <paramref name="audible"/> is the ordinary audio
    /// gate supplied by the owner (zone, modal, flyover); <paramref name="indoors"/>
    /// is the world's physical-interior flag (<c>_physicalInterior.Length &gt; 0</c>).
    /// Neither changes the switch mode, only whether sound is rendered now:
    /// the square horns stop inside any physical interior (the club's own
    /// speakers remain the indoor exception, as designed), and both modes hold
    /// while the adhan sounds (<see cref="HoldForAdhan"/>) so the call never
    /// shares the Loudspeaker bus with a record.
    /// </summary>
    public void Tick(double delta, Vector3 listener, bool audible, bool indoors)
    {
        if (!_initialized)
        {
            return;
        }

        EnsureFixtures();
        _now += Math.Min(delta, .5);
        KeepGramophoneSilent();

        if (Mode == PaMode.Off)
        {
            StopPlayback();
            return;
        }

        if (AdhanYields() || (indoors && Mode == PaMode.Square))
        {
            if (StopPlayback())
            {
                _nextSongAt = _now + ResumeDelaySeconds;
            }

            return;
        }

        if (!audible)
        {
            if (StopPlayback())
            {
                _nextSongAt = _now + ResumeDelaySeconds;
            }

            return;
        }

        var anchor = Mode == PaMode.Square ? _squareSpeakerPoint : _clubSpeakerPoint;
        var radius = Mode == PaMode.Square ? SquareAudibleRadiusMeters : ClubAudibleRadiusMeters;
        if (anchor is null || listener.DistanceSquaredTo(anchor.Value) > radius * radius)
        {
            if (StopPlayback())
            {
                _nextSongAt = _now + ResumeDelaySeconds;
            }

            return;
        }

        var playing = AnyPlaying();
        if (playing)
        {
            _wasPlaying = true;
            return;
        }

        if (_wasPlaying)
        {
            // The record ended: the 2-4 s pause between songs is part of the
            // presentation, so publish the "not listening" state once.
            _wasPlaying = false;
            PublishState();
        }

        if (_now < _nextSongAt)
        {
            return;
        }

        StartNextSong();
    }

    /// <summary>
    /// The switch cycle: off -> square horns -> club speakers -> off. Stops the
    /// current record and asks the gramophone guard to follow the new mode.
    /// </summary>
    public PaMode CycleMode()
    {
        if (!_initialized)
        {
            return Mode;
        }

        Mode = Mode switch
        {
            PaMode.Off => PaMode.Square,
            PaMode.Square => PaMode.Club,
            _ => PaMode.Off
        };
        StopPlayback();
        if (Mode != PaMode.Off)
        {
            _nextSongAt = _now + ModeChangeDelaySeconds;
        }

        KeepGramophoneSilent();
        PublishState(force: true);
        return Mode;
    }

    /// <summary>
    /// Mutual exclusion with the licensed adhan (expert brief 07: the call must
    /// never sit under anything). While held, the PA stops its record without
    /// changing <see cref="Mode"/> and does not start the next song; the club
    /// gramophone is muted through the same guard the switch uses. The adhan
    /// owner calls this with true when a validated call starts and false from
    /// its Finished handler and StopAdhan; the call itself always wins, the PA
    /// only yields and resumes after the release. AdhanPlaying is also read
    /// live, so a caller that forgets the hold cannot overlap either.
    /// </summary>
    public void HoldForAdhan(bool hold)
    {
        if (!_initialized || _adhanHold == hold)
        {
            return;
        }

        _adhanHold = hold;
        SetMeta("paAdhanHold", _adhanHold);
        if (_adhanHold)
        {
            if (StopPlayback())
            {
                _nextSongAt = _now + ResumeDelaySeconds;
            }
        }

        KeepGramophoneSilent();
    }

    /// <summary>True while the adhan owns the Loudspeaker bus.</summary>
    private bool AdhanYields() =>
        _adhanHold
        || (_world is Act1ConnectedWorld connected && IsInstanceValid(connected) && connected.AdhanPlaying);

    /// <summary>Fixture pass from the world: the two horn mouth positions.</summary>
    internal void ConfigureSquareSpeakers(Vector3 hornA, Vector3 hornB)
    {
        PlaceSquareSpeakers(hornA, hornB);
    }

    /// <summary>Fixture pass from the world: the club's indoor speaker position.</summary>
    internal void ConfigureClubSpeaker(Vector3 at)
    {
        _clubSpeakerPoint = at;
        if (_clubMusic is not null)
        {
            _clubMusic.GlobalPosition = at;
        }

        if (_clubCrackle is not null)
        {
            _clubCrackle.GlobalPosition = at;
        }
    }

    public override void _ExitTree()
    {
        StopPlayback();
        if (_gramophoneMuted)
        {
            Mode = PaMode.Off;
            KeepGramophoneSilent();
        }
    }

    private void EnsureFixtures()
    {
        if (!_fixturesPending)
        {
            return;
        }

        if (_world is not Act1ConnectedWorld connected || !connected.IsBuilt)
        {
            return;
        }

        _fixturesPending = false;
        connected.BuildVillagePaFixtures(this);
    }

    private void PlaceSquareSpeakers(Vector3 hornA, Vector3 hornB)
    {
        if (_squareVoices.Length == 2)
        {
            _squareVoices[0].GlobalPosition = hornA;
            _squareVoices[1].GlobalPosition = hornB;
        }

        _squareSpeakerPoint = (hornA + hornB) * .5f;
    }

    // Chooses the next record without an immediate repeat and drops the needle
    // at a random point of the groove. Loads exactly one song (uncached so the
    // five records never accumulate), disables its loop and starts every horn
    // of the current mode from the same offset. The crackle layer exists only
    // under the club speakers: indoors vinyl, not the street.
    private void StartNextSong()
    {
        if (SongCount == 0)
        {
            _nextSongAt = _now + MaxSongPauseSeconds;
            return;
        }

        var index = _random.RandiRange(0, SongCount - 1);
        if (index == _lastSong)
        {
            index = (index + 1) % SongCount;
        }

        var path = ClubGramophone.Songs[index];
        var stream = ResourceLoader.Load<AudioStream>(path, string.Empty, ResourceLoader.CacheMode.Ignore);
        if (stream is null || stream.GetLength() <= FirstPlaybackOffsetMinSeconds)
        {
            GD.PushWarning($"village-pa: record cannot be played: {path}");
            _nextSongAt = _now + MaxSongPauseSeconds;
            return;
        }

        if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        }

        _songStream = stream;
        _lastSong = index;
        _currentSong = path;
        var length = stream.GetLength();
        var offset = (float)_random.RandfRange(
            FirstPlaybackOffsetMinSeconds,
            Math.Max(FirstPlaybackOffsetMinSeconds + .5f, (float)length - SongTailGuardSeconds));

        if (Mode == PaMode.Square)
        {
            _clubMusic?.Stop();
            _clubMusic?.Stream = null;
            _clubCrackle?.Stop();
            _clubCrackle?.Stream = null;
            _crackleStream = null;
            foreach (var voice in _squareVoices)
            {
                voice.Stream = _songStream;
                voice.Play(offset);
            }
        }
        else
        {
            foreach (var voice in _squareVoices)
            {
                voice.Stop();
                voice.Stream = null;
            }

            if (_clubMusic is not null)
            {
                _clubMusic.Stream = _songStream;
                _clubMusic.Play(offset);
            }

            if (LoadCrackle() is { } crackle && _clubCrackle is not null)
            {
                _clubCrackle.Stream = crackle;
                _clubCrackle.Play(_random.RandfRange(0f, (float)crackle.GetLength()));
            }
        }

        _nextSongAt = _now + Math.Max(1f, length - offset)
            + _random.RandfRange(MinSongPauseSeconds, MaxSongPauseSeconds);
        _wasPlaying = true;
        PublishState();
    }

    private AudioStreamWav? LoadCrackle()
    {
        if (_crackleStream is not null)
        {
            return _crackleStream;
        }

        if (!ResourceLoader.Exists(CracklePath)
            || ResourceLoader.Load<AudioStreamWav>(CracklePath, string.Empty, ResourceLoader.CacheMode.Ignore) is not { } crackle)
        {
            return null;
        }

        crackle.LoopBegin = 0;
        crackle.LoopEnd = checked((int)Math.Round(crackle.GetLength() * crackle.MixRate));
        crackle.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        _crackleStream = crackle;
        return crackle;
    }

    // Stops every voice and releases the resident streams. Returns true when
    // anything actually had to stop, so callers can schedule the resume beat
    // without publishing state every frame.
    private bool StopPlayback()
    {
        if (!AnyPlaying() && _songStream is null && _crackleStream is null)
        {
            return false;
        }

        foreach (var voice in _squareVoices)
        {
            voice.Stop();
            voice.Stream = null;
        }

        if (_clubMusic is not null)
        {
            _clubMusic.Stop();
            _clubMusic.Stream = null;
        }

        if (_clubCrackle is not null)
        {
            _clubCrackle.Stop();
            _clubCrackle.Stream = null;
        }

        _songStream = null;
        _crackleStream = null;
        _currentSong = string.Empty;
        _wasPlaying = false;
        PublishState();
        return true;
    }

    private bool AnyPlaying()
    {
        foreach (var voice in _squareVoices)
        {
            if (voice.Playing)
            {
                return true;
            }
        }

        return _clubMusic is { Playing: true };
    }

    // The club gramophone and the PA are two owners of the same village sound.
    // While the PA is switched on (either mode), or while the adhan holds the
    // bus (the record must not sit under the call even with the switch off),
    // the gramophone is muted through the public VolumeDb of its two voices,
    // and the original levels are restored afterwards. Deliberately not
    // Stop(): the gramophone's own Tick would restart a record every frame
    // while its schedule is in the past, which would stutter instead of
    // silencing.
    private void KeepGramophoneSilent()
    {
        var mute = Mode != PaMode.Off || AdhanYields();
        if (mute && !_gramophoneResolved)
        {
            ResolveGramophone();
        }

        if (_gramophoneVoices.Count == 0 || _gramophoneMuted == mute)
        {
            return;
        }

        foreach (var (voice, originalDb) in _gramophoneVoices)
        {
            if (IsInstanceValid(voice))
            {
                voice.VolumeDb = mute ? MutedDb : originalDb;
            }
        }

        _gramophoneMuted = mute;
        SetMeta("paGramophoneGuard", mute ? "muted-while-on" : "released");
    }

    private void ResolveGramophone()
    {
        _gramophoneResolved = true;
        if (_world?.FindChild(GramophoneName, true, false) is not Node gramophone)
        {
            GD.PushWarning("village-pa: ClubGramophone not found; the PA cannot silence it (no public stop API).");
            SetMeta("paGramophoneGuard", "unresolved");
            return;
        }

        foreach (var name in GramophoneVoiceNames)
        {
            if (gramophone.GetNodeOrNull<AudioStreamPlayer3D>(name) is { } voice)
            {
                _gramophoneVoices.Add((voice, voice.VolumeDb));
            }
        }

        if (_gramophoneVoices.Count == 0)
        {
            GD.PushWarning("village-pa: ClubGramophone voices not found by name; overlap is possible.");
            SetMeta("paGramophoneGuard", "unresolved");
        }
        else
        {
            SetMeta("paGramophoneGuard", "armed");
        }
    }

    private AudioStreamPlayer3D MakeVoice(string name, string bus, float volumeDb, float maxDistance, float unitSize)
    {
        var voice = new AudioStreamPlayer3D
        {
            Name = name,
            Bus = bus,
            VolumeDb = volumeDb,
            MaxDistance = maxDistance,
            UnitSize = unitSize,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            AttenuationFilterCutoffHz = bus == AudioSettingsService.LoudspeakerBus ? 5200f : 3600f,
            AttenuationFilterDb = -6f,
            MaxPolyphony = 1
        };
        AddChild(voice);
        return voice;
    }

    // Meta contract for a future smoke assertion: paMode / paSong / paListening
    // are written only on actual changes, so Tick stays allocation-free.
    private void PublishState(bool force = false)
    {
        var mode = Mode switch
        {
            PaMode.Square => "square",
            PaMode.Club => "club",
            _ => "off"
        };
        var listening = AnyPlaying();
        var song = listening ? _currentSong : string.Empty;
        if (force || !string.Equals(_publishedMode, mode, StringComparison.Ordinal))
        {
            _publishedMode = mode;
            SetMeta("paMode", mode);
        }

        if (force || !string.Equals(_publishedSong, song, StringComparison.Ordinal))
        {
            _publishedSong = song;
            SetMeta("paSong", song);
        }

        if (force || _publishedListening != listening)
        {
            _publishedListening = listening;
            SetMeta("paListening", listening);
        }
    }
}
