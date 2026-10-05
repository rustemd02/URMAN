using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only zone ambience owner. The runtime kernel emits narrative
/// audio requests separately; this director owns only the continuous bed that
/// follows the active physical zone and never mutates story state.
/// </summary>
public partial class AmbientAudioDirector : Node
{
    public const string ManifestPath = "res://assets/audio/ambient_manifest.json";
    public const double CrossfadeDurationSeconds = 0.65;

    private const float TargetVolumeDb = -12f;
    private const float MutedVolumeDb = -60f;
    private const float VoiceDuckDb = -6f;
    private const double VoiceDuckTransitionSeconds = 0.08;

    private readonly Dictionary<string, AmbientStem> _stemsByZone = new(StringComparer.Ordinal);
    private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[2];
    private Tween? _crossfadeTween;
    private Tween? _voiceDuckTween;
    private Tween? _bedToggleTween;
    private float _activeStemVolumeDb = TargetVolumeDb;
    private bool _bedEnabled = true;
    private bool _headless;
    private bool _lifecycleReady;
    private bool _voiceDuckActive;
    private int _activePlayerIndex = -1;
    private AudioEffectAmplify? _voiceDuckEffect;
    private int _voiceDuckEffectIndex = -1;
    private bool _physicalShelter;
    private string _requestedZone = string.Empty;
    private string? _requestedSubKey;

    /// <summary>
    /// The weather bed breathes: a slow two-sine gain drift with rare shallow
    /// dips so the blizzard stops sounding like a static loop (Widows Bay vibe
    /// brief, item 12). Applied only to the active bed player, never to the
    /// village or household players on the same bus.
    /// </summary>
    private double _breathTime;
    private double _nextDipAt = 25d;
    private double _dipStart = double.MinValue;
    private double _dipLength;
    private readonly RandomNumberGenerator _breathRandom = new();

    public string CurrentZoneId { get; private set; } = string.Empty;

    public string CurrentStemId { get; private set; } = string.Empty;

    public string CurrentStreamPath { get; private set; } = string.Empty;

    public int PlayerCount { get; private set; }

    public int ActivePlayerIndex => _activePlayerIndex;

    public double ConfiguredCrossfadeDurationSeconds => CrossfadeDurationSeconds;

    public bool VoiceDuckActive => _voiceDuckActive;

    /// <summary>
    /// Keeps the authored manifest gain while true. The debug sound panel can
    /// switch the continuous bed (the blizzard/wind weather bed) off so local
    /// village life is heard without it; switching back on restores the
    /// authored stem volume.
    /// </summary>
    public bool BedEnabled => _bedEnabled;

    public override void _Ready()
    {
        AddToGroup("ambient_audio");
        _breathRandom.Randomize();
        _headless = string.Equals(DisplayServer.GetName(), "headless", StringComparison.Ordinal);
        for (var index = 0; index < _players.Length; index++)
        {
            AudioSettingsService.EnsureBuses();
            var player = new AudioStreamPlayer
            {
                Name = $"AmbientPlayer{index + 1}",
                VolumeDb = MutedVolumeDb,
                Autoplay = false,
                Bus = AudioSettingsService.AmbienceBus
            };
            AddChild(player);
            _players[index] = player;
        }
        PlayerCount = _players.Length;
        LoadManifest();
        SetupVoiceDuckEffect();
        SetMeta("manifestPath", ManifestPath);
        SetMeta("ambientPlayerCount", PlayerCount);
        SetMeta("ambientCrossfadeSeconds", CrossfadeDurationSeconds);
        SetMeta("activePlayerIndex", _activePlayerIndex);
        SetMeta("ambientStatus", "manifest-loaded-awaiting-zone");
        _lifecycleReady = true;
    }

    public override void _ExitTree()
    {
        if (_voiceDuckEffect is not null)
        {
            _voiceDuckEffect.VolumeDb = 0f;
        }

        _lifecycleReady = false;
        _crossfadeTween?.Kill();
        _voiceDuckTween?.Kill();
        _bedToggleTween?.Kill();
        RemoveVoiceDuckEffect();
        foreach (var player in _players)
        {
            if (!IsUsablePlayer(player))
            {
                continue;
            }

            player.Stop();
            player.Stream = null;
        }

        _activePlayerIndex = -1;
        CurrentZoneId = string.Empty;
        CurrentStemId = string.Empty;
        CurrentStreamPath = string.Empty;
    }

    public override void _Process(double delta)
    {
        if (_headless || !_lifecycleReady)
        {
            return;
        }

        var player = GetActivePlayer();
        if (player is null || !IsUsablePlayer(player) || player.Stream is null || !player.Playing)
        {
            return;
        }

        // Never fight an authored fade: crossfades and the panel's on/off
        // toggle own the volume while they run.
        if (_crossfadeTween?.IsValid() == true || _bedToggleTween?.IsValid() == true)
        {
            return;
        }

        _breathTime += delta;
        var baseDb = _bedEnabled ? _activeStemVolumeDb : MutedVolumeDb;
        if (baseDb <= MutedVolumeDb + 1f)
        {
            player.VolumeDb = baseDb;
            return;
        }

        var breath = 1.5f * Mathf.Sin((float)(_breathTime * Mathf.Tau / 28d))
            + 0.7f * Mathf.Sin((float)(_breathTime * Mathf.Tau / 9.5d));

        if (_breathTime >= _nextDipAt)
        {
            _dipStart = _breathTime;
            _dipLength = 2.0 + _breathRandom.Randf();
            _nextDipAt = _breathTime + 25d + _breathRandom.Randf() * 20d;
        }

        var dip = 0f;
        var age = _breathTime - _dipStart;
        if (age >= 0d && age <= _dipLength)
        {
            var half = (float)(_dipLength * .5d);
            dip = -3f * Mathf.Max(0f, 1f - Mathf.Abs((float)age - half) / Mathf.Max(.01f, half));
        }

        player.VolumeDb = baseDb + breath + dip;
    }

    public void StopForEnding()
    {
        _crossfadeTween?.Kill();
        foreach (var player in _players)
            if (IsUsablePlayer(player)) player.Stop();
        _activePlayerIndex = -1;
        CurrentStemId = string.Empty;
        CurrentStreamPath = string.Empty;
        SetMeta("ambientStatus", "chapter-ended-silent");
    }

    public void SetZone(string zoneId) => SetZone(zoneId, subKey: null);

    // The same crossfade owner moves from outdoor wind to the licensed room
    // bed when the player physically enters a room inside the village zone.
    // Local stove/door/radio sources retain their own spatial playback.
    public void SetPhysicalShelter(bool sheltered)
    {
        if (_physicalShelter == sheltered) return;
        _physicalShelter = sheltered;
        SetMeta("physicalSheltered", sheltered);
        if (_requestedZone.Length > 0 && CurrentStemId.Length > 0)
            SetZone(_requestedZone, _requestedSubKey);
    }

    /// <summary>
    /// Temporarily lowers the ambience bus while a physical voice cue is
    /// active. The user volume remains owned by AudioSettingsService and is
    /// re-applied when the transient duck ends.
    /// </summary>
    public void SetVoiceDuck(bool enabled)
    {
        if (_voiceDuckEffect is null || _voiceDuckActive == enabled)
        {
            return;
        }

        _voiceDuckActive = enabled;
        _voiceDuckTween?.Kill();
        _voiceDuckTween = CreateTween();
        _voiceDuckTween.TweenProperty(
            _voiceDuckEffect,
            "volume_db",
            enabled ? VoiceDuckDb : 0f,
            VoiceDuckTransitionSeconds);
    }

    /// <summary>
    /// Switches the whole active continuous bed on or off (a fade to silence,
    /// not a stop) so the debug sound panel can mute the blizzard bed without
    /// losing its loop position. The manifest stem gain is never edited.
    /// </summary>
    public void SetBedEnabled(bool enabled)
    {
        if (_bedEnabled == enabled)
        {
            return;
        }

        _bedEnabled = enabled;
        SetMeta("ambientBedEnabled", _bedEnabled);
        var player = GetActivePlayer();
        if (player is null || !IsUsablePlayer(player) || player.Stream is null)
        {
            return;
        }

        var target = _bedEnabled ? _activeStemVolumeDb : MutedVolumeDb;
        _bedToggleTween?.Kill();
        if (_headless || !player.Playing)
        {
            player.VolumeDb = target;
            return;
        }

        _bedToggleTween = CreateTween();
        _bedToggleTween.TweenProperty(player, "volume_db", target, CrossfadeDurationSeconds);
    }

    /// <summary>
    /// AUDIO-003: a logical zone may carry sub-zone beds keyed
    /// `zoneId@subKey` in the manifest (e.g. `village_day@from_house`); the
    /// plain zone bed remains the fallback when no sub-key matches.
    /// </summary>
    public void SetZone(string zoneId, string? subKey)
    {
        if (!_lifecycleReady)
        {
            return;
        }

        _requestedZone = zoneId;
        _requestedSubKey = subKey;
        var bedKey = _physicalShelter ? ResolveBedKey("house_old_pc", null) : ResolveBedKey(zoneId, subKey);
        if (!_stemsByZone.TryGetValue(bedKey, out var stem))
        {
            throw new InvalidOperationException($"Ambient audio manifest has no stem for zone '{zoneId}'.");
        }

        var activePlayer = GetActivePlayer();
        if (string.Equals(CurrentZoneId, zoneId, StringComparison.Ordinal)
            && string.Equals(CurrentStemId, stem.Id, StringComparison.Ordinal)
            && activePlayer?.Playing == true)
        {
            return;
        }

        var stream = ResourceLoader.Load<AudioStream>(stem.File);
        if (stream is null)
        {
            throw new InvalidOperationException($"Ambient audio stem could not be loaded: {stem.File}.");
        }
        ConfigureNativeLoop(stream, stem);

        var incomingIndex = _activePlayerIndex < 0 ? 0 : 1 - _activePlayerIndex;
        var incomingPlayer = GetPlayer(incomingIndex);
        if (incomingPlayer is null)
        {
            return;
        }

        _crossfadeTween?.Kill();
        _bedToggleTween?.Kill();
        ReleaseInactivePlayers(incomingPlayer, activePlayer);
        incomingPlayer.Stop();
        incomingPlayer.Stream = null;
        incomingPlayer.Stream = stream;
        _activeStemVolumeDb = stem.VolumeDb;

        var audibleStemDb = _bedEnabled ? stem.VolumeDb : MutedVolumeDb;
        var canCrossfade = !_headless && activePlayer?.Playing == true && activePlayer != incomingPlayer;
        incomingPlayer.VolumeDb = canCrossfade ? MutedVolumeDb : audibleStemDb;
        if (!_headless)
        {
            incomingPlayer.Play();
        }

        CurrentZoneId = zoneId;
        CurrentStemId = stem.Id;
        CurrentStreamPath = stem.File;
        _activePlayerIndex = incomingIndex;
        SetMeta("currentZoneId", CurrentZoneId);
        SetMeta("currentStemId", CurrentStemId);
        SetMeta("currentStreamPath", CurrentStreamPath);
        SetMeta("loop", stem.Loop);
        SetMeta("activePlayerIndex", _activePlayerIndex);
        SetMeta("ambientTransition", canCrossfade ? "crossfade" : "initial-or-headless");
        SetMeta("ambientStatus", _headless ? "godot-audiostream-ready-headless" : "godot-audiostream-playing");

        if (!canCrossfade)
        {
            ReleasePlayer(activePlayer);
            return;
        }

        _crossfadeTween = CreateTween();
        _crossfadeTween.SetParallel(true);
        _crossfadeTween.TweenProperty(activePlayer, "volume_db", MutedVolumeDb, CrossfadeDurationSeconds);
        _crossfadeTween.TweenProperty(incomingPlayer, "volume_db", audibleStemDb, CrossfadeDurationSeconds);
        _crossfadeTween.SetParallel(false);
        _crossfadeTween.TweenCallback(Callable.From(() => ReleasePlayer(activePlayer)));
    }

    private string ResolveBedKey(string zoneId, string? subKey)
    {
        if (!string.IsNullOrWhiteSpace(subKey)
            && _stemsByZone.ContainsKey($"{zoneId}@{subKey}"))
        {
            return $"{zoneId}@{subKey}";
        }

        return zoneId;
    }

    private void LoadManifest()
    {
        if (!global::Godot.FileAccess.FileExists(ManifestPath))
        {
            throw new InvalidOperationException($"Ambient audio manifest is missing: {ManifestPath}.");
        }

        var source = global::Godot.FileAccess.GetFileAsString(ManifestPath);
        var manifest = JsonSerializer.Deserialize<AmbientManifest>(source, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (manifest?.Stems is null || manifest.Stems.Count == 0)
        {
            throw new InvalidOperationException("Ambient audio manifest contains no stems.");
        }

        foreach (var stem in manifest.Stems)
        {
            if (string.IsNullOrWhiteSpace(stem.Id) || string.IsNullOrWhiteSpace(stem.File) || stem.Zones is null || stem.Zones.Count == 0)
            {
                throw new InvalidOperationException("Ambient audio manifest contains an incomplete stem entry.");
            }

            if (!float.IsFinite(stem.VolumeDb) || stem.VolumeDb < MutedVolumeDb || stem.VolumeDb > 0f)
                throw new InvalidOperationException($"Ambient stem has invalid volumeDb: {stem.Id}.");

            foreach (var zone in stem.Zones)
            {
                if (!_stemsByZone.TryAdd(zone, stem))
                {
                    throw new InvalidOperationException($"Ambient audio manifest assigns more than one stem to zone '{zone}'.");
                }
            }
        }

        SetMeta("stemCount", manifest.Stems.Count);
        SetMeta("zoneCount", _stemsByZone.Count);
    }

    private void SetupVoiceDuckEffect()
    {
        var busIndex = AudioServer.GetBusIndex(AudioSettingsService.AmbienceBus);
        if (busIndex == -1)
        {
            return;
        }

        _voiceDuckEffect = new AudioEffectAmplify { VolumeDb = 0f };
        _voiceDuckEffectIndex = AudioServer.GetBusEffectCount(busIndex);
        AudioServer.AddBusEffect(busIndex, _voiceDuckEffect, _voiceDuckEffectIndex);
    }

    private void RemoveVoiceDuckEffect()
    {
        var busIndex = AudioServer.GetBusIndex(AudioSettingsService.AmbienceBus);
        if (_voiceDuckEffect is not null
            && busIndex != -1
            && _voiceDuckEffectIndex >= 0
            && _voiceDuckEffectIndex < AudioServer.GetBusEffectCount(busIndex))
        {
            AudioServer.RemoveBusEffect(busIndex, _voiceDuckEffectIndex);
        }

        _voiceDuckEffect = null;
        _voiceDuckEffectIndex = -1;
        _voiceDuckActive = false;
    }

    private static void ConfigureNativeLoop(AudioStream stream, AmbientStem stem)
    {
        // The manifest owns these ambience resources. Restarting from Finished
        // left a main-thread-sized hole after each prepared seamless WAV, and
        // could also cut off the outgoing bed during a zone crossfade. Keep the
        // loop inside the mixer so its seam does not depend on scene frames.
        switch (stream)
        {
            case AudioStreamWav wav:
                wav.LoopBegin = 0;
                wav.LoopEnd = checked((int)Math.Round(wav.GetLength() * wav.MixRate));
                wav.LoopMode = stem.Loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
                break;
            case AudioStreamOggVorbis ogg:
                ogg.Loop = stem.Loop;
                ogg.LoopOffset = 0d;
                break;
            case AudioStreamMP3 mp3:
                mp3.Loop = stem.Loop;
                mp3.LoopOffset = 0d;
                break;
            default:
                throw new InvalidOperationException($"Ambient audio stem has no supported native loop: {stem.File}.");
        }
    }

    private AudioStreamPlayer? GetActivePlayer() => _lifecycleReady && _activePlayerIndex >= 0 && _activePlayerIndex < _players.Length
        ? GetPlayer(_activePlayerIndex)
        : null;

    private AudioStreamPlayer? GetPlayer(int index) => index >= 0 && index < _players.Length && IsUsablePlayer(_players[index])
        ? _players[index]
        : null;

    private void ReleaseInactivePlayers(AudioStreamPlayer incomingPlayer, AudioStreamPlayer? activePlayer)
    {
        foreach (var player in _players)
        {
            if (!IsUsablePlayer(player) || ReferenceEquals(player, incomingPlayer) || ReferenceEquals(player, activePlayer))
            {
                continue;
            }

            ReleasePlayer(player);
        }
    }

    private static void ReleasePlayer(AudioStreamPlayer? player)
    {
        if (!IsUsablePlayer(player))
        {
            return;
        }

        player!.Stop();
        player.Stream = null;
        player.VolumeDb = MutedVolumeDb;
    }

    private static bool IsUsablePlayer(AudioStreamPlayer? player) =>
        player is not null && GodotObject.IsInstanceValid(player);

    private sealed class AmbientManifest
    {
        [JsonPropertyName("stems")]
        public List<AmbientStem> Stems { get; set; } = [];
    }

    private sealed class AmbientStem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("file")]
        public string File { get; set; } = string.Empty;

        [JsonPropertyName("zones")]
        public List<string> Zones { get; set; } = [];

        [JsonPropertyName("volumeDb")]
        public float VolumeDb { get; set; } = TargetVolumeDb;

        [JsonPropertyName("loop")]
        public bool Loop { get; set; }
    }
}
