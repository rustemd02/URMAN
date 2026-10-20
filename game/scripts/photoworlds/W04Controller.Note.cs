using System;
using Godot;
using Urman.Core.Persistence;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class W04Controller : IAccessibilitySettingsTarget
{
    private const string DefaultCommonNote = "Это не весь вид";
    private CanvasLayer? _commonNoteLayer;
    private ColorRect _commonNoteScreen = null!;
    private PanelContainer _commonNotePanel = null!;
    private Label _commonNotePriorCaption = null!;
    private Label _commonNoteStatus = null!;
    private LineEdit _commonNoteText = null!;
    private Button _commonNoteDefault = null!;
    private Button _commonNoteConfirm = null!;
    private Button _commonNoteClose = null!;
    private FirstPersonController? _commonNotePlayer;
    private RuntimeBridge? _commonNoteBridge;
    private RuntimeKernel? _commonNoteSession;
    private string? _commonNoteVisit;
    private bool _commonNoteSaving;
    private bool _commonNoteReadOnly;
    private bool _commonNoteRetrySave;
    private object? _commonNoteRequest;
    private Viewport? _commonNoteViewport;
    private AccessibilitySettingsSnapshot _commonNoteAccessibility = AccessibilitySettingsSnapshot.Default;

    private bool CommonNoteOpen => _commonNoteLayer is { } layer && GodotObject.IsInstanceValid(layer) && layer.Visible;

    private bool BeginCommonNote()
    {
        if (CommonNoteOpen || !CanBeginCommonAction(NoteInteractionId, out var bridge, out _)
            || !bridge.TryGetCommonNoteVisit(out var session, out var visit)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player)
            return false;
        BuildCommonNoteEditor();
        _commonNoteBridge = bridge;
        _commonNoteSession = session;
        _commonNoteVisit = visit;
        _commonNoteRequest = new object();
        _commonNotePlayer = player;
        _commonNoteReadOnly = bridge.PhotoWorldFactObserved(NoteFactId);
        _commonNoteText.PlaceholderText = _commonNoteReadOnly ? string.Empty : DefaultCommonNote;
        var prior = bridge.PreparedCommonCaptionText();
        _commonNotePriorCaption.Text = "Моя подпись, подготовленная на ПК:\n"
            + (string.IsNullOrEmpty(prior) ? "Подпись в этом сохранении недоступна." : prior);
        _commonNoteText.Text = _commonNoteReadOnly ? bridge.CommonFrameNoteText() : DefaultCommonNote;
        _commonNoteRetrySave = _commonNoteReadOnly && !string.IsNullOrEmpty(_commonNoteText.Text);
        _commonNoteStatus.Text = _commonNoteReadOnly
            ? string.IsNullOrEmpty(_commonNoteText.Text)
                ? "В этой старой записи текст пометы не сохранился."
                : "Моя помета внесена в книгу и доступна на развороте «Общий кадр»."
            : "Можно оставить готовую помету или написать свою строку — от 1 до 120 знаков.";
        _commonNoteConfirm.Visible = !_commonNoteReadOnly || _commonNoteRetrySave;
        _commonNoteDefault.Visible = !_commonNoteReadOnly;
        _commonNoteConfirm.Text = _commonNoteRetrySave ? "Сохранить игру" : "Записать помету";
        _commonNoteClose.Text = _commonNoteReadOnly ? "Закрыть" : "Отмена";
        _commonNoteSaving = false;
        RefreshCommonNoteControls();
        ApplyAccessibilitySettings(player.Accessibility);
        bridge.PlayTimeBoundary += OnCommonNoteBoundary;
        _commonNoteLayer!.Visible = true;
        player.SetModalOpen(true);
        if (_commonNoteReadOnly) _commonNoteClose.GrabFocus();
        else _commonNoteText.GrabFocus();
        return false;
    }

    private void BuildCommonNoteEditor()
    {
        if (_commonNoteLayer is not null) return;
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _commonNoteLayer = new CanvasLayer { Name = "CommonFrameNote", Layer = 80, Visible = false };
        AddChild(_commonNoteLayer);
        _commonNoteScreen = new ColorRect { MouseFilter = Control.MouseFilterEnum.Stop };
        _commonNoteLayer.AddChild(_commonNoteScreen);
        _commonNoteScreen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _commonNotePanel = new PanelContainer { ThemeTypeVariation = UrmanUiTheme.Plate };
        _commonNoteScreen.AddChild(_commonNotePanel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 20);
        _commonNotePanel.AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true
        };
        layout.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(content);
        content.AddChild(new Label { Text = "Помета к общему кадру", ThemeTypeVariation = UrmanUiTheme.Heading,
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _commonNotePriorCaption = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart,
            FocusMode = Control.FocusModeEnum.All };
        content.AddChild(_commonNotePriorCaption);
        content.AddChild(new Label { Text = "Моя итоговая помета", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _commonNoteText = new LineEdit { Name = "FinalNote", MaxLength = 120,
            PlaceholderText = DefaultCommonNote, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddChild(_commonNoteText);
        _commonNoteStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart,
            FocusMode = Control.FocusModeEnum.All };
        content.AddChild(_commonNoteStatus);
        _commonNoteDefault = new Button { Text = "Вернуть готовую помету" };
        layout.AddChild(_commonNoteDefault);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 12);
        layout.AddChild(actions);
        _commonNoteConfirm = new Button { Text = "Записать помету", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _commonNoteClose = new Button { Text = "Отмена", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actions.AddChild(_commonNoteConfirm);
        actions.AddChild(_commonNoteClose);
        _commonNoteDefault.Pressed += () => { _commonNoteText.Text = DefaultCommonNote; _commonNoteText.GrabFocus(); };
        _commonNoteText.TextChanged += _ => RefreshCommonNoteControls();
        _commonNoteText.TextSubmitted += _ => ConfirmCommonNote();
        _commonNoteConfirm.Pressed += ConfirmCommonNote;
        _commonNoteClose.Pressed += () => CloseCommonNote();
        _commonNoteViewport = GetViewport();
        _commonNoteViewport.SizeChanged += RefitCommonNote;
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _commonNoteAccessibility = settings;
        if (_commonNoteLayer is null) return;
        _commonNoteScreen.Color = (settings.HighContrast ? UrmanUiTheme.HighContrast : UrmanUiTheme.Normal).Shade;
        _commonNotePanel.Theme = UrmanUiTheme.For(settings);
        AccessibilityPresentation.ApplyToControl(_commonNotePanel, settings);
        RefitCommonNote();
    }

    private void RefitCommonNote()
    {
        if (_commonNoteLayer is null || !IsInsideTree()) return;
        var viewport = GetViewport().GetVisibleRect().Size;
        var scale = (float)_commonNoteAccessibility.TextScale;
        var size = new Vector2(Math.Max(1, Math.Min(viewport.X - 32, 740 * scale)),
            Math.Max(1, Math.Min(viewport.Y - 32, 460 * scale)));
        _commonNotePanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _commonNotePanel.OffsetLeft = -size.X / 2;
        _commonNotePanel.OffsetRight = size.X / 2;
        _commonNotePanel.OffsetTop = -size.Y / 2;
        _commonNotePanel.OffsetBottom = size.Y / 2;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!CommonNoteOpen || !inputEvent.IsActionPressed("ui_cancel")) return;
        if (!_commonNoteSaving) CloseCommonNote();
        GetViewport().SetInputAsHandled();
    }

    private bool OtherCommonNoteModalOpen() => MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("document_ui") is DocumentUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("journal_ui")?.GetNodeOrNull<Control>("Screen") is { Visible: true }
        || GetTree().GetFirstNodeInGroup("old_pc_ui")?.GetNodeOrNull<Control>("Screen") is { Visible: true };

    private bool CommonNoteContextCurrent() => IsInsideTree() && !IsQueuedForDeletion()
        && _commonNoteBridge is { } bridge && GodotObject.IsInstanceValid(bridge)
        && ReferenceEquals(_bridge, bridge) && ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
        && _commonNoteSession is { } session && bridge.IsCommonNoteVisitCurrent(session, _commonNoteVisit)
        && _commonNotePlayer is { } player && GodotObject.IsInstanceValid(player)
        && ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player)
        && player.ModalOpen && !player.VehicleControlled && !player.IsClimbingLadder
        && !OtherCommonNoteModalOpen();

    private void ProcessCommonNote()
    {
        if (CommonNoteOpen && !CommonNoteContextCurrent()) CloseCommonNote();
    }

    private void ProjectCommonNote(bool visible)
    {
        if (CommonNoteOpen && (!visible || !CommonNoteContextCurrent())) CloseCommonNote();
    }

    private void OnCommonNoteBoundary(string boundary)
    {
        if (boundary == "load-start") CloseCommonNote();
    }

    private void RefreshCommonNoteControls()
    {
        if (_commonNoteLayer is null) return;
        _commonNoteText.Editable = !_commonNoteSaving && !_commonNoteReadOnly;
        _commonNoteDefault.Disabled = _commonNoteSaving || _commonNoteReadOnly;
        _commonNoteClose.Disabled = _commonNoteSaving;
        _commonNoteConfirm.Disabled = _commonNoteSaving
            || !RuntimeBridge.TryNormalizeCommonNote(_commonNoteText.Text, out _);
    }

    private async void ConfirmCommonNote()
    {
        if (!CommonNoteOpen || _commonNoteSaving || (_commonNoteReadOnly && !_commonNoteRetrySave)) return;
        if (!CommonNoteContextCurrent()) { CloseCommonNote(); return; }
        if (!RuntimeBridge.TryNormalizeCommonNote(_commonNoteText.Text, out var text))
        {
            _commonNoteStatus.Text = "Нужна одна строка длиной от 1 до 120 знаков.";
            return;
        }
        var bridge = _commonNoteBridge!;
        var session = _commonNoteSession!;
        var visit = _commonNoteVisit;
        var request = _commonNoteRequest;
        _commonNoteSaving = true;
        RefreshCommonNoteControls();
        _commonNoteStatus.Text = "Записываю помету…";
        RuntimeBridge.CommonNoteSaveResult result;
        try
        {
            result = await bridge.ConfirmCommonFrameNoteAsync(session, visit, text);
        }
        catch (Exception error)
        {
            if (!GodotObject.IsInstanceValid(this) || !CommonNoteOpen || !CommonNoteContextCurrent()
                || !ReferenceEquals(request, _commonNoteRequest)) return;
            GD.PushWarning($"W04 note could not be saved: {error.Message}");
            result = bridge.PhotoWorldFactObserved(NoteFactId) && bridge.CommonFrameNoteText() == text
                ? RuntimeBridge.CommonNoteSaveResult.CommittedNotSaved : RuntimeBridge.CommonNoteSaveResult.Rejected;
        }
        if (!GodotObject.IsInstanceValid(this) || !CommonNoteOpen || !CommonNoteContextCurrent()
            || !ReferenceEquals(request, _commonNoteRequest)
            || !ReferenceEquals(session, _commonNoteSession) || visit != _commonNoteVisit) return;
        _commonNoteSaving = false;
        if (result == RuntimeBridge.CommonNoteSaveResult.Stale) { CloseCommonNote(); return; }
        var committed = result is RuntimeBridge.CommonNoteSaveResult.Saved or RuntimeBridge.CommonNoteSaveResult.CommittedNotSaved;
        _commonNoteReadOnly = committed;
        _commonNoteText.Text = committed ? text : _commonNoteText.Text;
        _commonNoteDefault.Visible = !committed;
        _commonNoteClose.Text = committed ? "Закрыть" : "Отмена";
        _commonNoteStatus.Text = result switch
        {
            RuntimeBridge.CommonNoteSaveResult.Saved => "Помета сохранена. Она осталась на развороте книги.",
            RuntimeBridge.CommonNoteSaveResult.CommittedNotSaved => "Помета появилась в книге, но сохранить игру не удалось. Повторите сохранение.",
            _ => "Помета не принята. Текст остался здесь — можно попробовать снова."
        };
        // A failed checkpoint retries the same confirmed text, without another authored action.
        _commonNoteRetrySave = result == RuntimeBridge.CommonNoteSaveResult.CommittedNotSaved;
        _commonNoteConfirm.Visible = result != RuntimeBridge.CommonNoteSaveResult.Saved;
        _commonNoteConfirm.Text = committed ? "Повторить сохранение" : "Записать помету";
        RefreshCommonNoteControls();
        _commonNoteClose.GrabFocus();
    }

    private void CloseCommonNote()
    {
        if (!CommonNoteOpen) return;
        var bridge = _commonNoteBridge;
        var player = _commonNotePlayer;
        var releaseModal = IsInsideTree() && bridge is not null && GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(_commonNoteSession, bridge.ProjectionSessionIdentity)
            && player is not null && GodotObject.IsInstanceValid(player)
            && ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player)
            && !OtherCommonNoteModalOpen();
        if (bridge is not null && GodotObject.IsInstanceValid(bridge)) bridge.PlayTimeBoundary -= OnCommonNoteBoundary;
        _commonNoteLayer!.Visible = false;
        _commonNoteBridge = null;
        _commonNoteSession = null;
        _commonNoteVisit = null;
        _commonNoteRequest = null;
        _commonNotePlayer = null;
        _commonNoteSaving = false;
        _commonNoteRetrySave = false;
        if (releaseModal) player!.SetModalOpen(false);
    }

    private void ExitCommonNote()
    {
        CloseCommonNote();
        if (_commonNoteViewport is { } viewport && GodotObject.IsInstanceValid(viewport))
            viewport.SizeChanged -= RefitCommonNote;
        _commonNoteViewport = null;
    }
}
