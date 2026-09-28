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
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public bool IsOpen => _screen.Visible;

    private AudioStreamPlayer? _foley;

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
        GetViewport().SizeChanged += RefitToViewport;
        _panel.MinimumSizeChanged += RefitToViewport;
        _line.Resized += RefitToViewport;
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
        _bridge = bridge;
        _dialogue = bridge.RequireDialogue(dialogueId);
        _continue.Visible = false;
        UiFoley.Play(_foley, "ui_click");
        _screen.Visible = true;
        UrmanUiTheme.PlayOpen(_panel, _accessibility.ReducedMotion);
        SetPlayerModal(true);
        if (!await ShowNodeAsync(bridge.ResolveDialogueStartNodeId(_dialogue), applyEffects: true))
        {
            _speaker.Text = "...";
            _line.Text = "Айдару пока нечем продолжить этот разговор.";
            ClearChoices();
            _continue.Visible = true;
            RefitToViewport();
            _continue.GrabFocus();
        }
    }

    private async Task<bool> ShowNodeAsync(string nodeId, bool applyEffects)
    {
        if (_bridge is null || _dialogue is null || !_dialogue.Nodes.TryGetValue(nodeId, out var node))
        {
            return false;
        }

        if (!_bridge.EvaluateConditions(node.Conditions) || applyEffects && !await _bridge.EnterDialogueNodeAsync(_dialogue.Id, node.Id))
        {
            return false;
        }

        _node = node;
        _speaker.Text = SpeakerName(node.SpeakerRole);
        _line.Text = _bridge.ResolveText(node.TextId);
        // ACT1-LANG.2: a heard line auto-collects unknown Tatar words.
        _ = _bridge.ObserveVocabularyTextAsync(_line.Text, node.TextId);
        ClearChoices();
        foreach (var choice in node.Choices.Where(choice => _bridge.EvaluateConditions(choice.Conditions)))
        {
            var button = new Button
            {
                Text = _bridge.ResolveText(choice.TextId),
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
        if (_bridge is null || _dialogue is null || _node is null)
        {
            return;
        }

        var sourceNodeId = _node.Id;
        if (!await _bridge.ChooseDialogueAsync(_dialogue.Id, sourceNodeId, choice.Id))
        {
            return;
        }

        if (choice.NextNodeId is null)
        {
            Close();
            return;
        }

        await ShowNodeAsync(choice.NextNodeId, applyEffects: false);
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
        UiFoley.Play(_foley, "ui_click");
        _screen.Visible = false;
        _bridge = null;
        _dialogue = null;
        _node = null;
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
        "gulsina" => "ӘБИ ГӨЛСИНӘ",
        "mansur" => "БАБАЙ МАНСУР",
        "alsu" => "АЛСУ",
        "naila" => "НАИЛЯ",
        "rinat" => "РИНАТ",
        "timur" or "timur-hazrat" => "ТИМУР ХӘЗРӘТ",
        "razilya" => "РАЗИЛЯ",
        "tamara" => "ТАМАРА ГЕННАДЬЕВНА",
        _ => role.ToUpperInvariant()
    };
}
