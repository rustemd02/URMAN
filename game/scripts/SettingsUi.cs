using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class SettingsUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private Control _panel = null!;
    private HSlider _fov = null!;
    private Label _fovValue = null!;
    private HSlider _sensitivity = null!;
    private Label _sensitivityValue = null!;
    private CheckBox _headBob = null!;
    private CheckBox _reducedMotion = null!;
    private CheckBox _highContrast = null!;
    private CheckBox _subtitles = null!;
    private CheckBox _audioDescriptions = null!;
    private HSlider _textScale = null!;
    private Label _textScaleValue = null!;
    private OptionButton _graphics = null!;
    private VBoxContainer _bindings = null!;
    private Label _status = null!;
    private Button _save = null!;
    private Button _load = null!;
    private FirstPersonController? _player;
    private readonly Dictionary<string, Button> _bindingButtons = new(StringComparer.Ordinal);
    private IReadOnlyList<InputBindingSnapshot>? _bindingsBeforeOpen;
    private string? _awaitingAction;
    private Key? _pendingConflictKey;
    private bool _saveLoadInProgress;

    private static readonly IReadOnlyDictionary<string, string> ActionLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["move_forward"] = "Вперёд",
        ["move_backward"] = "Назад",
        ["move_left"] = "Влево",
        ["move_right"] = "Вправо",
        ["interact"] = "Взаимодействие",
        ["crouch"] = "Пригнуться",
        ["journal"] = "Журнал",
        ["pause"] = "Меню",
        ["quick_save"] = "Быстрое сохранение",
        ["quick_load"] = "Быстрая загрузка",
        ["carry_rotate"] = "Повернуть предмет",
        ["carry_place"] = "Поставить предмет"
    };

    public bool IsOpen => _screen.Visible;

    /// <summary>UIUX-008: true while a rebind is awaiting a key/button/axis.</summary>
    public bool IsAwaitingRemap => _awaitingAction is not null;

    public override void _Ready()
    {
        AddToGroup("settings_ui");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = GetNode<Control>("Screen");
        _panel = GetNode<Control>("Screen/Panel");
        _fov = GetNode<HSlider>("Screen/Panel/Layout/BodyScroll/Body/FovRow/Fov");
        _fovValue = GetNode<Label>("Screen/Panel/Layout/BodyScroll/Body/FovRow/Value");
        _sensitivity = GetNode<HSlider>("Screen/Panel/Layout/BodyScroll/Body/SensitivityRow/Sensitivity");
        _sensitivityValue = GetNode<Label>("Screen/Panel/Layout/BodyScroll/Body/SensitivityRow/Value");
        _headBob = GetNode<CheckBox>("Screen/Panel/Layout/BodyScroll/Body/HeadBob");
        _reducedMotion = GetNode<CheckBox>("Screen/Panel/Layout/BodyScroll/Body/ReducedMotion");
        _highContrast = GetNode<CheckBox>("Screen/Panel/Layout/BodyScroll/Body/HighContrast");
        _subtitles = GetNode<CheckBox>("Screen/Panel/Layout/BodyScroll/Body/Subtitles");
        _audioDescriptions = GetNode<CheckBox>("Screen/Panel/Layout/BodyScroll/Body/AudioDescriptions");
        _textScale = GetNode<HSlider>("Screen/Panel/Layout/BodyScroll/Body/TextScaleRow/TextScale");
        _textScaleValue = GetNode<Label>("Screen/Panel/Layout/BodyScroll/Body/TextScaleRow/Value");
        _graphics = GetNode<OptionButton>("Screen/Panel/Layout/BodyScroll/Body/GraphicsRow/Graphics");
        _bindings = GetNode<VBoxContainer>("Screen/Panel/Layout/BodyScroll/Body/BindingScroll/Bindings");
        _status = GetNode<Label>("Screen/Panel/Layout/Status");
        _graphics.AddItem("Низкое", 0);
        _graphics.AddItem("Среднее", 1);
        _graphics.AddItem("Высокое", 2);
        _fov.ValueChanged += value => _fovValue.Text = $"{value:0}°";
        _sensitivity.ValueChanged += value => _sensitivityValue.Text = $"{value:0.00}";
        _textScale.ValueChanged += value => _textScaleValue.Text = $"{value:0.00}×";
        BuildBindingRows();
        BuildVolumeRows();
        _save = GetNode<Button>("Screen/Panel/Layout/Buttons/Save");
        _load = GetNode<Button>("Screen/Panel/Layout/Buttons/Load");
        _save.Pressed += SaveQuickSlot;
        _load.Pressed += LoadQuickSlot;
        GetNode<Button>("Screen/Panel/Layout/Buttons/Apply").Pressed += Apply;
        GetNode<Button>("Screen/Panel/Layout/Buttons/Close").Pressed += Close;
        ApplyAccessibilitySettings(AccessibilitySettingsSnapshot.Default);
    }

    /// <summary>
    /// UIUX-011: master/ambience/voice/SFX volume rows. Values apply live to
    /// the audio buses and persist through AudioSettingsService; muted voice
    /// keeps captions working because they are visual.
    /// </summary>
    private void BuildVolumeRows()
    {
        AudioSettingsService.EnsureBuses();
        var body = GetNode<VBoxContainer>("Screen/Panel/Layout/BodyScroll/Body");
        foreach (var (busName, label) in new[]
                 {
                     (AudioSettingsService.MasterBus, "Общая громкость"),
                     (AudioSettingsService.AmbienceBus, "Окружение"),
                     (AudioSettingsService.VoiceBus, "Голос"),
                     (AudioSettingsService.SfxBus, "Эффекты")
                 })
        {
            var row = new HBoxContainer { Name = $"{busName}VolumeRow" };
            row.CustomMinimumSize = new Vector2(0, 44);
            var name = new Label
            {
                Text = label,
                CustomMinimumSize = new Vector2(180, 0)
            };
            var slider = new HSlider
            {
                Name = $"{busName}Volume",
                MinValue = 0,
                MaxValue = 1,
                Step = 0.05,
                Value = AudioSettingsService.GetVolume(busName),
                CustomMinimumSize = new Vector2(180, 0)
            };
            var value = new Label { Name = "Value" };
            value.Text = $"{slider.Value:0.00}";
            slider.ValueChanged += sliderValue =>
            {
                value.Text = $"{sliderValue:0.00}";
                AudioSettingsService.SetVolume(busName, (float)sliderValue);
            };
            row.AddChild(name);
            row.AddChild(slider);
            row.AddChild(value);
            body.AddChild(row);
        }
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings) =>
        AccessibilityPresentation.ApplyToControl(_panel, settings);

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_awaitingAction is not null)
        {
            if (inputEvent is InputEventKey { Pressed: true } key)
            {
                if (key.PhysicalKeycode == Key.Escape)
                {
                    _pendingConflictKey = null;
                    FinishRemap("Переназначение отменено.");
                }
                else if (key.PhysicalKeycode != Key.None)
                {
                    // UIUX-008: a key already bound elsewhere requires a
                    // second identical press; the conflicting action keeps
                    // its binding unless the player confirms.
                    var conflicts = InputBindingService.FindKeyboardConflicts(key.PhysicalKeycode, _awaitingAction);
                    if (conflicts.Count > 0 && _pendingConflictKey != key.PhysicalKeycode)
                    {
                        _pendingConflictKey = key.PhysicalKeycode;
                        _status.Text = $"Конфликт с «{InputBindingService.Label(conflicts[0])}». Нажмите ту же клавишу ещё раз, чтобы переназначить её сюда.";
                    }
                    else
                    {
                        _pendingConflictKey = null;
                        InputBindingService.RebindKeyboard(_awaitingAction, key.PhysicalKeycode);
                        FinishRemap("Клавиша изменена и войдёт в следующее сохранение.");
                    }
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (inputEvent is InputEventJoypadButton { Pressed: true } button)
            {
                InputBindingService.RebindGamepad(_awaitingAction, button.ButtonIndex);
                FinishRemap("Кнопка геймпада изменена и войдёт в следующее сохранение.");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (inputEvent is InputEventJoypadMotion motion && Math.Abs(motion.AxisValue) >= 0.45f)
            {
                InputBindingService.RebindGamepadAxis(_awaitingAction, (int)motion.Axis, Math.Sign(motion.AxisValue));
                FinishRemap("Ось геймпада изменена и войдёт в следующее сохранение.");
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // UIUX-005: the pause action is owned by PauseMenuUi, which stacks
        // this settings panel on top of the pause shell and closes it again.
        if (_screen.Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(FirstPersonController player)
    {
        _player = player;
        var settings = player.CaptureSettings();
        _bindingsBeforeOpen = settings.InputBindings.ToArray();
        _fov.Value = settings.FieldOfView;
        _sensitivity.Value = settings.MouseSensitivity;
        _headBob.ButtonPressed = settings.HeadBob;
        _reducedMotion.ButtonPressed = settings.Accessibility.ReducedMotion;
        _highContrast.ButtonPressed = settings.Accessibility.HighContrast;
        _textScale.Value = settings.Accessibility.TextScale;
        _subtitles.ButtonPressed = settings.Accessibility.Subtitles;
        _audioDescriptions.ButtonPressed = settings.Accessibility.AudioDescriptions;
        _graphics.Selected = settings.GraphicsPreset switch
        {
            "low" => 0,
            "high" => 2,
            _ => 1
        };
        _status.Text = "Изменения применяются кнопкой «Применить».";
        RefreshBindingLabels();
        _screen.Visible = true;
        RefreshSaveLoadAvailability();
        // UIUX-006: keyboard/gamepad entry lands on the first control so the
        // whole panel is reachable without a mouse.
        _fov.GrabFocus();
        player.SetModalOpen(true);
    }

    private async void SaveQuickSlot()
    {
        if (_saveLoadInProgress)
        {
            return;
        }

        var bridge = FindRuntimeBridge();
        if (bridge is null)
        {
            _status.Text = "Не удалось сохранить игру.";
            RefreshSaveLoadAvailability();
            return;
        }

        SetSaveLoadBusy(true);
        try
        {
            _status.Text = await bridge.SaveSlotAsync("quick")
                ? "Игра сохранена."
                : "Не удалось сохранить игру.";
        }
        finally
        {
            SetSaveLoadBusy(false);
        }
    }

    private async void LoadQuickSlot()
    {
        if (_saveLoadInProgress)
        {
            return;
        }

        var bridge = FindRuntimeBridge();
        if (bridge is null)
        {
            _status.Text = "Не удалось загрузить игру.";
            RefreshSaveLoadAvailability();
            return;
        }

        SetSaveLoadBusy(true);
        try
        {
            if (await bridge.LoadSlotAsync("quick"))
            {
                _bindingsBeforeOpen = InputBindingService.Capture();
                Close();
            }
            else
            {
                _status.Text = "Не удалось загрузить игру.";
            }
        }
        finally
        {
            SetSaveLoadBusy(false);
        }
    }

    private void Apply()
    {
        if (_player is null)
        {
            return;
        }

        var current = _player.CaptureSettings();
        var graphicsPreset = _graphics.Selected switch
        {
            0 => "low",
            2 => "high",
            _ => "medium"
        };
        _player.ApplySettings(new GameSettingsSnapshot(
            _fov.Value,
            _sensitivity.Value,
            current.MotionBlur,
            _headBob.ButtonPressed,
            graphicsPreset,
            current.InputDevice,
            InputBindingService.Capture())
        {
            Accessibility = new AccessibilitySettingsSnapshot(
                ReducedMotion: _reducedMotion.ButtonPressed,
                HighContrast: _highContrast.ButtonPressed,
                TextScale: _textScale.Value,
                Subtitles: _subtitles.ButtonPressed,
                AudioDescriptions: _audioDescriptions.ButtonPressed)
        });
        _bindingsBeforeOpen = InputBindingService.Capture();
        _status.Text = "Настройки применены. Они войдут в следующее сохранение.";
    }

    public void Close()
    {
        _awaitingAction = null;
        _pendingConflictKey = null;
        _screen.Visible = false;
        if (_bindingsBeforeOpen is not null)
        {
            InputBindingService.Apply(_bindingsBeforeOpen);
            _bindingsBeforeOpen = null;
        }
        var mainMenu = GetTree().GetFirstNodeInGroup("main_menu") as MainMenuUi;
        var pauseMenu = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
        var keepModal = mainMenu is { IsDismissed: false } || pauseMenu is { IsOpen: true };
        _player?.SetModalOpen(keepModal);
        if (keepModal)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            if (pauseMenu is { IsOpen: true }) pauseMenu.SettingsButton?.GrabFocus();
            else if (mainMenu is { IsDismissed: false }) mainMenu.SettingsButton?.GrabFocus();
        }
        _player = null;
    }

    private FirstPersonController? FindPlayer() =>
        GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;

    private RuntimeBridge? FindRuntimeBridge() =>
        GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;

    private void SetSaveLoadBusy(bool busy)
    {
        _saveLoadInProgress = busy;
        RefreshSaveLoadAvailability();
    }

    private void RefreshSaveLoadAvailability()
    {
        _save.Disabled = _saveLoadInProgress;
        _load.Disabled = _saveLoadInProgress || FindRuntimeBridge()?.IsSlotAvailable("quick") != true;
    }

    private void BuildBindingRows()
    {
        foreach (var action in InputBindingService.RemappableActions)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
            row.AddChild(new Label
            {
                Text = ActionLabels[action],
                CustomMinimumSize = new Vector2(245, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            });
            var button = new Button { CustomMinimumSize = new Vector2(210, 44) };
            button.Pressed += () => BeginRemap(action);
            row.AddChild(button);
            _bindings.AddChild(row);
            _bindingButtons.Add(action, button);
        }

        var restoreButton = new Button
        {
            Text = "Сбросить управление",
            CustomMinimumSize = new Vector2(210, 44)
        };
        restoreButton.Pressed += () =>
        {
            InputBindingService.RestoreDefaults();
            RefreshBindingLabels();
            _status.Text = "Управление сброшено к значениям по умолчанию.";
        };
        _bindings.AddChild(restoreButton);

        RefreshBindingLabels();
    }

    /// <summary>UIUX-008: the binding button handler; public so tests and
    /// future UI hosts drive the same production remap entry.</summary>
    public void BeginRemap(string action)
    {
        _pendingConflictKey = null;
        _awaitingAction = action;
        _bindingButtons[action].Text = "Нажмите клавишу, кнопку или ось…";
        _status.Text = $"Новое управление: {ActionLabels[action]}. Esc отменяет.";
    }

    private void FinishRemap(string status)
    {
        _awaitingAction = null;
        RefreshBindingLabels();
        _status.Text = status;
    }

    private void RefreshBindingLabels()
    {
        foreach (var (action, button) in _bindingButtons)
        {
            button.Text = InputBindingService.Label(action);
        }
    }
}
