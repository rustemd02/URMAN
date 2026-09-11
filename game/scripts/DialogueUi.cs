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
        _foley = UiFoley.Attach(this);        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = GetNode<Control>("Screen");
        _panel = GetNode<Control>("Screen/Panel");
        _speaker = GetNode<Label>("Screen/Panel/Layout/Speaker");
        _line = GetNode<RichTextLabel>("Screen/Panel/Layout/Line");
        _choices = GetNode<VBoxContainer>("Screen/Panel/Layout/Choices");
        _continue = GetNode<Button>("Screen/Panel/Layout/Continue");
        _continue.Pressed += Close;
        GetViewport().SizeChanged += RefitToViewport;
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
        var width = Mathf.Min(_panel.GetViewportRect().Size.X - 48f, 860f * (float)_accessibility.TextScale);
        _panel.AnchorLeft = _panel.AnchorRight = .5f;
        _panel.OffsetLeft = -width * .5f;
        _panel.OffsetRight = width * .5f;
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
        SetPlayerModal(true);
        if (!await ShowNodeAsync(_dialogue.StartNodeId, applyEffects: true))
        {
            _speaker.Text = "...";
            _line.Text = "Айдару пока нечем продолжить этот разговор.";
            ClearChoices();
            _continue.Visible = true;
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
        ClearChoices();
        foreach (var choice in node.Choices.Where(choice => _bridge.EvaluateConditions(choice.Conditions)))
        {
            var button = new Button
            {
                Text = _bridge.ResolveText(choice.TextId),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.AddThemeStyleboxOverride("normal", _continue.GetThemeStylebox("normal"));
            button.AddThemeStyleboxOverride("hover", _continue.GetThemeStylebox("hover"));
            button.AddThemeStyleboxOverride("focus", _continue.GetThemeStylebox("focus"));
            button.Pressed += () => Choose(choice);
            _choices.AddChild(button);
        }
        AccessibilityPresentation.ApplyToControl(_panel, _accessibility);

        _continue.Visible = _choices.GetChildCount() == 0;
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

    private static string SpeakerName(string role) => role switch
    {
        "gulsina" => "ӘБИ ГӨЛСИНӘ",
        "mansur" => "БАБАЙ МАНСУР",
        "alsu" => "АЛСУ",
        "rinat" => "РИНАТ",
        "timur" => "ТИМУР ХӘЗРӘТ",
        _ => role.ToUpperInvariant()
    };
}
