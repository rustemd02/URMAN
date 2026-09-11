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
    private bool _headless;
    private bool _lifecycleReady;
    private bool _voiceDuckActive;
    private int _activePlayerIndex = -1;
    private AudioEffectAmplify? _voiceDuckEffect;
    private int _voiceDuckEffectIndex = -1;

    public string CurrentZoneId { get; private set; } = string.Empty;

    public string CurrentStemId { get; private set; } = string.Empty;

    public string CurrentStreamPath { get; private set; } = string.Empty;

    public int PlayerCount { get; private set; }

    public int ActivePlayerIndex => _activePlayerIndex;

    public double ConfiguredCrossfadeDurationSeconds => CrossfadeDurationSeconds;

    public bool VoiceDuckActive => _voiceDuckActive;

    public override void _Ready()
    {
        AddToGroup("ambient_audio");
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
            player.Finished += LoopCurrentStem;
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
        RemoveVoiceDuckEffect();
        foreach (var player in _players)
        {
            if (!IsUsablePlayer(player))
            {
                continue;
            }

            player.Finished -= LoopCurrentStem;
            player.Stop();
            player.Stream = null;
        }

        _activePlayerIndex = -1;
        CurrentZoneId = string.Empty;
        CurrentStemId = string.Empty;
        CurrentStreamPath = string.Empty;
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

        var bedKey = ResolveBedKey(zoneId, subKey);
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

        var incomingIndex = _activePlayerIndex < 0 ? 0 : 1 - _activePlayerIndex;
        var incomingPlayer = GetPlayer(incomingIndex);
        if (incomingPlayer is null)
        {
            return;
        }

        _crossfadeTween?.Kill();
        ReleaseInactivePlayers(incomingPlayer, activePlayer);
        incomingPlayer.Stop();
        incomingPlayer.Stream = null;
        incomingPlayer.Stream = stream;

        var canCrossfade = !_headless && activePlayer?.Playing == true && activePlayer != incomingPlayer;
        incomingPlayer.VolumeDb = canCrossfade ? MutedVolumeDb : TargetVolumeDb;
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
        _crossfadeTween.TweenProperty(incomingPlayer, "volume_db", TargetVolumeDb, CrossfadeDurationSeconds);
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

    private void LoopCurrentStem()
    {
        var activePlayer = GetActivePlayer();
        if (!_headless && activePlayer?.Stream is not null && !activePlayer.Playing && !string.IsNullOrWhiteSpace(CurrentZoneId))
        {
            activePlayer.Play();
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

        [JsonPropertyName("loop")]
        public bool Loop { get; set; }
    }
}
