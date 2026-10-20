using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class DialogueUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private Control _panel = null!;
    private Label _speaker = null!;
    private RichTextLabel _line = null!;
    private VBoxContainer _choices = null!;
    private Button _continue = null!;
    private RuntimeBridge? _bridge;
    private CompiledDialogueContent? _dialogue;
    private CompiledDialogueNodeContent? _node;
    private object? _dialogueSessionIdentity;
    private long _openingRevision;
    private bool _choicePending;
    private Viewport? _refitViewport;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public bool IsOpen => _screen.Visible;
    public string? CurrentDialogueId => IsOpen && _node is not null
        && _dialogueSessionIdentity is { } session
        && ReferenceEquals(session, _bridge?.SessionIdentity)
        ? _dialogue?.Id
        : null;
    public string? CurrentSpeakerRole => CurrentDialogueId is not null ? _node?.SpeakerRole : null;
    public string? CurrentNodeId => CurrentDialogueId is not null ? _node?.Id : null;

    /// <summary>Raised after the runtime accepted a dialogue answer (dialogue id, choice id).</summary>
    internal static event Action<string, string>? ChoiceAccepted;

    internal void CloseIfDialogue(string id)
    {
        if (_dialogue?.Id == id) Close();
    }

    private AudioStreamPlayer? _foley;

    public override void _EnterTree() => BindRefitSignals();

    public override void _ExitTree()
    {
        _openingRevision++;
        _choicePending = false;
        UnbindRefitSignals();
    }

    public override void _Ready()
    {
        AddToGroup("dialogue_ui");
        _foley = UiFoley.Attach(this);
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = GetNode<Control>("Screen");
        _panel = GetNode<Control>("Screen/Panel");
        _speaker = GetNode<Label>("Screen/Panel/Layout/Speaker");
        _line = GetNode<RichTextLabel>("Screen/Panel/Layout/Line");
        _choices = GetNode<VBoxContainer>("Screen/Panel/Layout/Choices");
        _continue = GetNode<Button>("Screen/Panel/Layout/Continue");
        _continue.Pressed += Close;
        _continue.MouseEntered += () =>
        {
            if (_screen.Visible && _continue.IsVisibleInTree() && !_continue.HasFocus()) _continue.GrabFocus();
        };
        BindRefitSignals();
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            ApplyAccessibilitySettings(player.Accessibility);
        }
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        AccessibilityPresentation.ApplyToControl(_panel, settings);
        RefitToViewport();
    }

    private void RefitToViewport()
    {
        if (!IsInsideTree() || _panel is null || _line is null || _choices is null)
        {
            return;
        }

        var viewport = _panel.GetViewportRect().Size;
        var scale = Mathf.Clamp((float)_accessibility.TextScale, 0.8f, 1.6f);
        var width = Mathf.Max(1f, Mathf.Min(viewport.X - 48f, 860f * scale));
        // Grow only as far as this line and its actions need. Long replies
        // retain the existing scrollable text area and bounded panel height.
        var extraChoicesHeight = Mathf.Max(0, _choices.GetChildCount() - 3) * (44f * scale + 8f);
        var maximumHeight = Mathf.Max(1f, Mathf.Min(viewport.Y - 96f, 400f + 160f * (scale - 1f) + extraChoicesHeight));
        var height = Mathf.Min(maximumHeight, _panel.GetCombinedMinimumSize().Y + _line.GetContentHeight());
        _panel.AnchorLeft = _panel.AnchorRight = .5f;
        _panel.AnchorTop = _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = -width * .5f;
        _panel.OffsetRight = width * .5f;
        _panel.OffsetTop = -height - 48f;
        _panel.OffsetBottom = -48f;
    }

    private void BindRefitSignals()
    {
        if (!IsInsideTree() || _panel is null || _line is null || _refitViewport is not null)
        {
            return;
        }

        _refitViewport = GetViewport();
        _refitViewport.SizeChanged += RefitToViewport;
        _panel.MinimumSizeChanged += RefitToViewport;
        _line.Resized += RefitToViewport;
    }

    private void UnbindRefitSignals()
    {
        if (_refitViewport is null)
        {
            return;
        }

        _refitViewport.SizeChanged -= RefitToViewport;
        if (_panel is not null) _panel.MinimumSizeChanged -= RefitToViewport;
        if (_line is not null) _line.Resized -= RefitToViewport;
        _refitViewport = null;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!_screen.Visible)
        {
            return;
        }

        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("interact")
            && inputEvent is not InputEventKey { Echo: true })
        {
            if (_continue.Visible)
            {
                _continue.EmitSignal(Button.SignalName.Pressed);
            }

            GetViewport().SetInputAsHandled();
        }
    }

    public async void Open(RuntimeBridge bridge, string dialogueId)
    {
        if (bridge.SessionIdentity is not { } session) return;
        var dialogue = bridge.RequireDialogue(dialogueId);
        var revision = ++_openingRevision;
        _choicePending = false;
        _bridge = bridge;
        _dialogueSessionIdentity = session;
        _dialogue = dialogue;
        _node = null;
        ClearChoices();
        _speaker.Text = string.Empty;
        _line.Text = "…";
        _continue.Visible = false;
        _continue.Disabled = false;
        UiFoley.Play(_foley, "ui_click");
        _screen.Visible = true;
        UrmanUiTheme.PlayOpen(_panel, _accessibility.ReducedMotion);
        SetPlayerModal(true);
        try
        {
            if (await ShowNodeAsync(bridge.ResolveDialogueStartNodeId(dialogue), applyEffects: true, revision)
                || !OwnsOpening(revision, bridge, session)) return;
            ShowUnavailableLine("Айдару пока нечем продолжить этот разговор.");
        }
        catch (Exception error)
        {
            GD.PushWarning($"Dialogue opening failed: {error.Message}");
            if (OwnsOpening(revision, bridge, session))
                ShowUnavailableLine("Не удалось открыть разговор. Закройте окно и попробуйте ещё раз.");
        }
    }

    private bool IsCurrentSession(RuntimeBridge bridge, object session) =>
        IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
        && IsInstanceValid(bridge) && bridge.IsInsideTree() && !bridge.IsQueuedForDeletion()
        && ReferenceEquals(session, bridge.SessionIdentity);

    private bool OwnsOpening(long revision, RuntimeBridge bridge, object session) =>
        IsCurrentSession(bridge, session) && revision == _openingRevision && _screen.Visible
        && ReferenceEquals(bridge, _bridge) && ReferenceEquals(session, _dialogueSessionIdentity);

    private void ShowUnavailableLine(string text)
    {
        _speaker.Text = "…";
        _line.Text = text;
        ClearChoices();
        _continue.Visible = true;
        RefitToViewport();
        _continue.GrabFocus();
    }

    private async Task<bool> ShowNodeAsync(string nodeId, bool applyEffects, long revision)
    {
        if (_bridge is not { } bridge || _dialogue is not { } dialogue
            || _dialogueSessionIdentity is not { } session || !OwnsOpening(revision, bridge, session)
            || !dialogue.Nodes.TryGetValue(nodeId, out var node)) return false;

        if (!bridge.EvaluateConditions(node.Conditions)
            || applyEffects && !await bridge.EnterDialogueNodeAsync(dialogue.Id, node.Id)
            || !OwnsOpening(revision, bridge, session)) return false;

        _node = node;
        _speaker.Text = SpeakerName(node.SpeakerRole);
        _line.Text = bridge.ResolveText(node.TextId);
        // Synthetic preview voice, when a clip exists for this text id (silent otherwise).
        PrologueVoice.PlayText(this, node.TextId, bridge.TatarLanguageLevel);
        // ACT1-LANG.2: a heard line auto-collects unknown Tatar words.
        _ = bridge.ObserveVocabularyTextAsync(_line.Text, node.TextId);
        ClearChoices();
        foreach (var choice in node.Choices.Where(choice => bridge.EvaluateConditions(choice.Conditions)))
        {
            var button = new Button
            {
                Text = bridge.ResolveText(choice.TextId),
                ThemeTypeVariation = UrmanUiTheme.ChoiceButton,
                Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.Pressed += () => Choose(choice);
            // A keyboard-focused answer and a mouse-hovered answer must never
            // read as two active choices: the shared theme draws focus as the
            // hover plate, so hover has to claim the single focus slot.
            button.MouseEntered += () =>
            {
                if (_screen.Visible && button.IsVisibleInTree() && !button.HasFocus()) button.GrabFocus();
            };
            _choices.AddChild(button);
        }
        _choices.Visible = _choices.GetChildCount() > 0;
        _continue.Visible = !_choices.Visible;
        AccessibilityPresentation.ApplyToControl(_panel, _accessibility);
        RefitToViewport();

        (_choices.GetChildCount() > 0 ? (Control)_choices.GetChild(0) : _continue).GrabFocus();
        return true;
    }

    private async void Choose(CompiledDialogueChoiceContent choice)
    {
        if (_choicePending || _bridge is not { } bridge || _dialogue is not { } dialogue
            || _node is not { } sourceNode || _dialogueSessionIdentity is not { } session) return;
        var revision = _openingRevision;
        if (!OwnsOpening(revision, bridge, session)) return;
        _choicePending = true;
        SetChoicesPending(true);
        try
        {
            if (!await bridge.ChooseDialogueAsync(dialogue.Id, sourceNode.Id, choice.Id)) return;
            // The answer can commit after Esc; its event belongs to that runtime session.
            if (!IsCurrentSession(bridge, session)) return;
            ChoiceAccepted?.Invoke(dialogue.Id, choice.Id);
            if (!OwnsOpening(revision, bridge, session)) return;
            if (choice.NextNodeId is null) Close();
            else await ShowNodeAsync(choice.NextNodeId, applyEffects: false, revision);
        }
        catch (Exception error)
        {
            GD.PushWarning($"Dialogue answer failed: {error.Message}");
            if (OwnsOpening(revision, bridge, session))
                ShowUnavailableLine("Не удалось продолжить разговор. Закройте окно и попробуйте ещё раз.");
        }
        finally
        {
            if (OwnsOpening(revision, bridge, session))
            {
                _choicePending = false;
                SetChoicesPending(false);
                if (_choices.Visible && _choices.GetChildCount() > 0)
                    ((Control)_choices.GetChild(0)).GrabFocus();
                else if (_continue.Visible) _continue.GrabFocus();
            }
        }
    }

    private void SetChoicesPending(bool pending)
    {
        foreach (var child in _choices.GetChildren())
            if (child is Button button) button.Disabled = pending;
        _continue.Disabled = pending;
    }

    private void ClearChoices()
    {
        _choices.Visible = false;
        foreach (var child in _choices.GetChildren())
        {
            _choices.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void Close()
    {
        _openingRevision++;
        _choicePending = false;
        PrologueVoice.Stop();
        UiFoley.Play(_foley, "ui_click");
        _screen.Visible = false;
        _bridge = null;
        _dialogue = null;
        _node = null;
        _dialogueSessionIdentity = null;
        SetPlayerModal(false);
    }

    private void SetPlayerModal(bool open)
    {
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            player.SetModalOpen(open);
        }
    }

    internal static string SpeakerName(string role) => role switch
    {
        "aidar" => "АЙДАР",
        "gulsina" => "ӘБИ ГӨЛСИНӘ",
        "mansur" => "БАБАЙ МАНСУР",
        "alsu" => "АЛСУ",
        "naila" => "НАИЛЯ",
        "rinat" => "РИНАТ",
        "timur" or "timur-hazrat" => "ТИМУР ХӘЗРӘТ",
        "razilya" => "РАЗИЛЯ",
        "school_staff" => "СОТРУДНИЦА ШКОЛЫ",
        "shurale" => "Шурале",
        "tamara" => "ТАМАРА ГЕННАДЬЕВНА",
        _ => role.ToUpperInvariant()
    };
}
