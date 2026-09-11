using Godot;
using Urman.Content.Resolvers;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class AudioCueUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private const double LogicalCueDurationSeconds = 2.0;
    private const double CueGapSeconds = 0.12;
    private const int HistoryLimit = 16;

    private PanelContainer _panel = null!;
    private Label _label = null!;
    private AudioStreamPlayer _player = null!;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;
    private readonly Queue<PendingCue> _pendingCues = new();
    private readonly List<string> _presentedHistory = [];
    private bool _presentationActive;
    private bool _paused;
    private double _remainingSeconds;
    private double _gapRemainingSeconds;
    private bool _voiceDuckHeld;

    public string? LastOutcomeKey { get; private set; }

    public string? LastAssetId { get; private set; }

    public string? LastPresentedText { get; private set; }

    public string? LastStartedAssetId { get; private set; }

    /// <summary>
    /// Presentation-only cue-start signal for future authored timing. It must
    /// never be used to mutate narrative state from this presentation owner.
    /// </summary>
    public event Action<string>? CueStarted;

    /// <summary>
    /// The text currently visible in the cue panel. LastPresentedText remains
    /// the latest requested cue for compatibility with runtime diagnostics;
    /// this property exposes the actual on-screen state while a short cue
    /// queue is being presented.
    /// </summary>
    public string? VisibleText => _panel is not null && _panel.Visible ? _label.Text : null;

    public bool IsPresenting => _presentationActive
        || _pendingCues.Count > 0
        || _player is not null
            && GodotObject.IsInstanceValid(_player)
            && _player.Playing;

    /// <summary>
    /// Presentation-only history for smoke tests and accessibility review.
    /// It never feeds back into the narrative kernel.
    /// </summary>
    public IReadOnlyList<string> PresentedHistory => _presentedHistory;

    public override void _Ready()
    {
        AddToGroup("audio_cue_ui");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _panel = GetNode<PanelContainer>("Panel");
        _label = GetNode<Label>("Panel/Margin/Text");
        AudioSettingsService.EnsureBuses();
        _player = GetNode<AudioStreamPlayer>("AudioPlayer");
        _player.Bus = AudioSettingsService.VoiceBus;
        _panel.Visible = false;
        ApplyAccessibilitySettings(_accessibility);
    }

    public override void _ExitTree()
    {
        ResetPresentation();
        CueStarted = null;
    }

    public override void _Process(double delta)
    {
        if (_paused)
        {
            return;
        }

        if (_presentationActive)
        {
            _remainingSeconds -= delta;
            // Audio mixing and scene frames have separate clocks. Keep the
            // tail, but bound an invalid/looping stream to one extra second.
            if (_remainingSeconds <= 0d && (!_player.Playing || _remainingSeconds <= -1d))
            {
                FinishCurrentCue();
            }

            return;
        }

        if (_gapRemainingSeconds > 0d)
        {
            _gapRemainingSeconds = Math.Max(0d, _gapRemainingSeconds - delta);
            if (_gapRemainingSeconds > 0d)
            {
                return;
            }
        }

        if (_pendingCues.Count > 0)
        {
            PresentNextCue();
        }
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (_player is not null && GodotObject.IsInstanceValid(_player))
        {
            _player.StreamPaused = paused;
        }
    }

    public void ResetPresentation()
    {
        _pendingCues.Clear();
        _presentationActive = false;
        _remainingSeconds = 0d;
        _gapRemainingSeconds = 0d;
        if (_player is not null && GodotObject.IsInstanceValid(_player))
        {
            _player.StreamPaused = false;
            _player.Stop();
            _player.Stream = null;
        }

        if (_panel is not null && GodotObject.IsInstanceValid(_panel))
        {
            _panel.Visible = false;
        }

        if (_label is not null && GodotObject.IsInstanceValid(_label))
        {
            _label.Text = string.Empty;
        }

        LastOutcomeKey = null;
        LastAssetId = null;
        LastPresentedText = null;
        LastStartedAssetId = null;
        _presentedHistory.Clear();
        ReleaseVoiceDuck();
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        if (_panel is not null)
        {
            AccessibilityPresentation.ApplyToControl(_panel, settings);
        }
    }

    public void Present(ResolvedAudio audio)
    {
        var playable = audio.Asset.MediaType.StartsWith("audio/", StringComparison.Ordinal)
            && ResourceLoader.Exists(audio.Asset.Url);
        var text = playable
            ? _accessibility.Subtitles ? audio.Captions?.Text : null
            : _accessibility.AudioDescriptions
                ? audio.NonAudioCue?.Text
                : _accessibility.Subtitles ? audio.Captions?.Text : null;

        LastOutcomeKey = audio.NonAudioCue?.OutcomeKey ?? audio.Asset.AssetId;
        LastAssetId = audio.Asset.AssetId;
        LastPresentedText = text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            _presentedHistory.Add(text);
            if (_presentedHistory.Count > HistoryLimit)
            {
                _presentedHistory.RemoveAt(0);
            }
        }

        // Keep every request serialized, including a real recording when both
        // visual accessibility channels are disabled.
        _pendingCues.Enqueue(new PendingCue(audio.Asset.AssetId, audio.Asset.Url, playable, text));
        if (!_presentationActive && _gapRemainingSeconds <= 0d && !_paused)
        {
            PresentNextCue();
        }
    }

    private void PresentNextCue()
    {
        if (_pendingCues.Count == 0 || !GodotObject.IsInstanceValid(_panel))
        {
            _presentationActive = false;
            if (GodotObject.IsInstanceValid(_panel))
            {
                _panel.Visible = false;
            }

            return;
        }

        var cue = _pendingCues.Dequeue();
        _presentationActive = true;
        if (!string.IsNullOrWhiteSpace(cue.Text))
        {
            _label.Text = cue.Text;
            _panel.Visible = true;
        }
        else
        {
            _label.Text = string.Empty;
            _panel.Visible = false;
        }

        var stream = cue.Playable ? PlayAudio(cue.AssetUrl) : null;
        if (stream is not null)
        {
            HoldVoiceDuck();
        }

        _remainingSeconds = Math.Max(
            stream is not null ? Math.Max(0d, stream.GetLength()) : 0d,
            ReadableTextDuration(cue.Text));
        LastStartedAssetId = cue.AssetId;
        CueStarted?.Invoke(cue.AssetId);
    }

    private void FinishCurrentCue()
    {
        _player.Stop();
        _player.Stream = null;
        _player.StreamPaused = false;
        _presentationActive = false;
        _remainingSeconds = 0d;
        if (GodotObject.IsInstanceValid(_panel))
        {
            _panel.Visible = false;
        }

        if (_pendingCues.Count == 0)
        {
            ReleaseVoiceDuck();
        }
        else
        {
            _gapRemainingSeconds = CueGapSeconds;
        }
    }

    private AudioStream? PlayAudio(string assetUrl)
    {
        var stream = ResourceLoader.Load<AudioStream>(assetUrl);
        if (stream is null)
        {
            return null;
        }

        _player.Stream = stream;
        _player.Play();
        return stream;
    }

    private static double ReadableTextDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0d;
        }

        return Math.Max(LogicalCueDurationSeconds, text.Trim().Length / 18d);
    }

    private void HoldVoiceDuck()
    {
        if (_voiceDuckHeld)
        {
            return;
        }

        if (GetTree().GetFirstNodeInGroup("ambient_audio") is AmbientAudioDirector ambience)
        {
            ambience.SetVoiceDuck(true);
            _voiceDuckHeld = true;
        }
    }

    private void ReleaseVoiceDuck()
    {
        if (!_voiceDuckHeld)
        {
            return;
        }

        if (GetTree().GetFirstNodeInGroup("ambient_audio") is AmbientAudioDirector ambience)
        {
            ambience.SetVoiceDuck(false);
        }

        _voiceDuckHeld = false;
    }

    private readonly record struct PendingCue(string AssetId, string AssetUrl, bool Playable, string? Text);
}
