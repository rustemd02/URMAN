using Godot;
using Urman.Content.Resolvers;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class AudioCueUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private const double CueDurationSeconds = 2.0;
    private const double CueGapSeconds = 0.12;
    private const int HistoryLimit = 16;

    private PanelContainer _panel = null!;
    private Label _label = null!;
    private AudioStreamPlayer _player = null!;
    private ulong _presentationSequence;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;
    private readonly Queue<PendingCue> _pendingCues = new();
    private readonly List<string> _presentedHistory = [];
    private bool _presentationActive;

    public string? LastOutcomeKey { get; private set; }

    public string? LastAssetId { get; private set; }

    public string? LastPresentedText { get; private set; }

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
        _player = GetNode<AudioStreamPlayer>("AudioPlayer");
        _panel.Visible = false;
        ApplyAccessibilitySettings(_accessibility);
    }

    public override void _ExitTree()
    {
        _presentationSequence++;
        _pendingCues.Clear();
        _presentationActive = false;
        if (_player is not null && GodotObject.IsInstanceValid(_player))
        {
            _player.Stop();
            _player.Stream = null;
        }
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
        if (string.IsNullOrWhiteSpace(text))
        {
            // A real recording may still be audible when subtitles and audio
            // descriptions are both disabled. Logical refs have no stream,
            // so there is nothing to enqueue in that fallback case.
            if (playable)
            {
                PlayAudio(audio.Asset.Url);
            }

            return;
        }

        LastOutcomeKey = audio.NonAudioCue?.OutcomeKey ?? audio.Asset.AssetId;
        LastAssetId = audio.Asset.AssetId;
        LastPresentedText = text;
        _presentedHistory.Add(text);
        if (_presentedHistory.Count > HistoryLimit)
        {
            _presentedHistory.RemoveAt(0);
        }

        _pendingCues.Enqueue(new PendingCue(audio.Asset.Url, playable, text));
        if (!_presentationActive)
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
        _label.Text = cue.Text;
        _panel.Visible = true;
        if (cue.Playable)
        {
            PlayAudio(cue.AssetUrl);
        }

        _presentationSequence++;
        _ = HideAfterDelayAsync(_presentationSequence);
    }

    private async Task HideAfterDelayAsync(ulong sequence)
    {
        await ToSignal(GetTree().CreateTimer(CueDurationSeconds), SceneTreeTimer.SignalName.Timeout);
        if (sequence != _presentationSequence || !GodotObject.IsInstanceValid(_panel))
        {
            return;
        }

        _panel.Visible = false;
        _presentationActive = false;
        if (_pendingCues.Count == 0)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(CueGapSeconds), SceneTreeTimer.SignalName.Timeout);
        if (sequence == _presentationSequence && GodotObject.IsInstanceValid(_panel))
        {
            PresentNextCue();
        }
    }

    private void PlayAudio(string assetUrl)
    {
        var stream = ResourceLoader.Load<AudioStream>(assetUrl);
        if (stream is null)
        {
            return;
        }

        _player.Stream = stream;
        _player.Play();
    }

    private readonly record struct PendingCue(string AssetUrl, bool Playable, string Text);
}
