using Godot;

namespace Urman.Godot;

/// <summary>
/// Debug sound panel: auditions the authored village sound set (weather bed,
/// sound-mood layers, household one-shots) and sweeps the mood scale without
/// walking the whole settlement. It exists only while user://debug-zones.enabled
/// is present, opens by typing the physical keys Z-V-U-K (zvuk), holds the
/// player behind a modal and releases the cursor, but never pauses the tree so
/// every audio player, tween and layer keeps sounding while the author tunes it.
/// Presentation only: no story, save or knowledge state is touched.
/// </summary>
public partial class DebugSoundPanel : Control
{
    private const string ClipRoot = "res://assets/audio/act1/";

    private static readonly (string Label, string Clip)[] OneShots =
    [
        ("Азан у минарета", VillageSoundMoodDirector.AdhanPath),
        ("Азан (рядом)", VillageSoundMoodDirector.AdhanPath),
        ("Печка (треск)", ClipRoot + "sound_mood/stove.wav"),
        ("ТВ из-за стены", ClipRoot + "sound_mood/tv.wav"),
        ("Добрый смех", ClipRoot + "sound_mood/laughter.wav"),
        ("Гомон, разговор", ClipRoot + "sound_mood/chatter.wav"),
        ("Вороны зимой", ClipRoot + "sound_mood/crows.wav"),
        ("Топор, дрова", ClipRoot + "sound_mood/axe.wav"),
        ("Пила", ClipRoot + "sound_mood/saw.wav"),
        ("Калитка", ClipRoot + "sound_mood/gate.wav"),
        ("В доме варят", ClipRoot + "sound_mood/pot.wav"),
        ("Корова в сарае", ClipRoot + "sound_mood/cow.wav"),
        ("Детвора в снегу", ClipRoot + "sound_mood/children.wav"),
        ("Шаги по снегу", ClipRoot + "sound_mood/boots_snow.wav"),
        ("Гармонь", ClipRoot + "sound_mood/garmon.wav"),
        ("Собака", ClipRoot + "village_life/dog.wav"),
        ("Дальний лай", ClipRoot + "sound_mood/dog_distant.wav"),
        ("Тарелки", ClipRoot + "village_life/plates.wav"),
        ("Чайник", ClipRoot + "village_life/kettle.wav"),
        ("Кот", ClipRoot + "village_life/cat.wav"),
        ("Домашнее радио", ClipRoot + "village_life/music.wav"),
        ("Сова (жуть)", ClipRoot + "foley/forest/owl_tawny.wav"),
        ("Гул (жуть)", ClipRoot + "foley/forest/dread_drone.wav"),
        ("Лиса (жуть)", ClipRoot + "foley/forest/fox_scream.wav"),
        ("Волк далеко", ClipRoot + "foley/forest/wolf_far.wav")
    ];

    private static readonly Key[] CheatKeys = [Key.Z, Key.V, Key.U, Key.K];

    private readonly List<Button> _oneShotButtons = [];
    private AudioStreamPlayer? _audition;
    private CheckButton? _weatherToggle;
    private HSlider? _moodSlider;
    private Label? _moodValue;
    private Label? _status;
    private VillageSoundMoodDirector? _mood;
    private Act1ConnectedWorld? _world;
    private VillageHouseholdDirector? _households;
    private FirstPersonController? _player;
    private Input.MouseModeEnum _mouseModeBeforeOpen = Input.MouseModeEnum.Visible;
    private int _loadedOneShots;
    private int _cheatIndex;
    private bool _playerModalSet;
    private string _lastAction = string.Empty;

    public bool PanelOpen { get; private set; }

    public static void AttachIfEnabled(Node host)
    {
        if (!MainMenuUi.DebugZonesEnabled) return;
        var layer = new CanvasLayer { Name = "DebugSoundPanelLayer", Layer = 92, ProcessMode = ProcessModeEnum.Always };
        host.AddChild(layer);
        layer.AddChild(new DebugSoundPanel { Name = "DebugSoundPanel", Visible = false, ProcessMode = ProcessModeEnum.Always });
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        AudioSettingsService.EnsureBuses();
        _audition = new AudioStreamPlayer { Name = "DebugSoundAudition", Bus = AudioSettingsService.SfxBus, VolumeDb = -8f };
        AddChild(_audition);
        BuildUi();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (PanelOpen && key.PhysicalKeycode == Key.Escape)
            {
                ClosePanel();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.CtrlPressed || key.AltPressed || key.MetaPressed) return;
            if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return;
            if (key.PhysicalKeycode != CheatKeys[_cheatIndex])
            {
                _cheatIndex = key.PhysicalKeycode == CheatKeys[0] ? 1 : 0;
                return;
            }

            _cheatIndex++;
            if (_cheatIndex < CheatKeys.Length) return;
            _cheatIndex = 0;
            TogglePanel();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (PanelOpen && inputEvent.IsActionPressed("ui_cancel"))
        {
            ClosePanel();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!PanelOpen) return;
        var mood = Mood();
        if (_weatherToggle is not null) _weatherToggle.Disabled = mood is null;
        if (_moodSlider is not null) _moodSlider.Editable = mood is not null;
        RefreshStatus();
    }

    private void BuildUi()
    {
        var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            Name = "Panel",
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(760, 0)
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(.05f, .06f, .07f, .93f),
            BorderColor = new Color(.45f, .5f, .55f, .85f)
        };
        style.SetCornerRadiusAll(14);
        style.SetBorderWidthAll(1);
        panel.AddThemeStyleboxOverride("panel", style);
        center.AddChild(panel);

        var margin = new MarginContainer { Name = "Margin", MouseFilter = MouseFilterEnum.Ignore };
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        panel.AddChild(margin);

        var layout = new VBoxContainer { Name = "Layout", MouseFilter = MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 8);
        margin.AddChild(layout);

        var title = new Label { Name = "Title", Text = "Отладка звука — деревня", MouseFilter = MouseFilterEnum.Ignore };
        title.AddThemeFontSizeOverride("font_size", 20);
        layout.AddChild(title);

        var hint = new Label { Name = "Hint", MouseFilter = MouseFilterEnum.Ignore,
            Text = "Читкод: набери zvuk (Z-V-U-K). Панель доступна только при debug-zones.enabled.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(.62f, .67f, .72f));
        layout.AddChild(hint);

        var weatherToggle = new CheckButton { Name = "WeatherToggle", Text = WeatherLabel(true) };
        weatherToggle.Toggled += pressed =>
        {
            weatherToggle.Text = WeatherLabel(pressed);
            Mood()?.SetWeatherEnabled(pressed);
        };
        layout.AddChild(weatherToggle);
        _weatherToggle = weatherToggle;

        var moodRow = new HBoxContainer { Name = "MoodRow", MouseFilter = MouseFilterEnum.Ignore };
        moodRow.AddChild(new Label { Text = "Настроение деревни: жуткая ←→ добрая",
            MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var moodSlider = new HSlider
        {
            Name = "MoodSlider",
            MinValue = 0,
            MaxValue = 1,
            Step = .01,
            Value = VillageSoundMoodDirector.DefaultMood,
            CustomMinimumSize = new Vector2(220, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        var moodValue = new Label { Name = "MoodValue", MouseFilter = MouseFilterEnum.Ignore,
            Text = $"{VillageSoundMoodDirector.DefaultMood:0.00}" };
        moodSlider.ValueChanged += value =>
        {
            moodValue.Text = $"{value:0.00}";
            Mood()?.SetMood((float)value);
        };
        moodRow.AddChild(moodSlider);
        moodRow.AddChild(moodValue);
        layout.AddChild(moodRow);
        _moodSlider = moodSlider;
        _moodValue = moodValue;

        var grid = new GridContainer { Name = "OneShots", Columns = 3, MouseFilter = MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 6);
        layout.AddChild(grid);
        foreach (var (label, clip) in OneShots)
        {
            var button = new Button
            {
                Name = $"OneShot{_oneShotButtons.Count}_{Path.GetFileNameWithoutExtension(clip)}",
                Text = label,
                Disabled = !ResourceLoader.Exists(clip),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 34)
            };
            button.Pressed += () => TriggerOneShot(label, clip);
            grid.AddChild(button);
            _oneShotButtons.Add(button);
            if (!button.Disabled) _loadedOneShots++;
        }

        var status = new Label { Name = "Status", MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        status.AddThemeFontSizeOverride("font_size", 12);
        layout.AddChild(status);
        _status = status;

        var actions = new HBoxContainer { Name = "Actions", MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.End };
        var reset = new Button { Name = "Reset", Text = "Сбросить к обычной деревне" };
        reset.Pressed += ResetToAuthored;
        var close = new Button { Name = "Close", Text = "Закрыть", CustomMinimumSize = new Vector2(140, 0) };
        close.Pressed += ClosePanel;
        actions.AddChild(reset);
        actions.AddChild(close);
        layout.AddChild(actions);
    }

    private void TogglePanel()
    {
        if (PanelOpen) ClosePanel();
        else OpenPanel();
    }

    private void OpenPanel()
    {
        if (PanelOpen) return;
        _mouseModeBeforeOpen = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        // The tree keeps running so the living-village layers, their tweens and
        // every one-shot stay audible while the author tunes them. Only the
        // player is held by a modal so nothing walks away under the panel.
        if (!_playerModalSet && Player() is { ModalOpen: false } player)
        {
            _playerModalSet = true;
            player.SetModalOpen(true);
        }
        PanelOpen = true;
        Visible = true;
        SetMeta("debugSoundPanelOpen", true);
        RefreshFromDirectors();
        RefreshStatus();
    }

    private void ClosePanel()
    {
        if (!PanelOpen) return;
        PanelOpen = false;
        Visible = false;
        SetMeta("debugSoundPanelOpen", false);
        if (_playerModalSet && Player() is { } player)
        {
            player.SetModalOpen(false);
        }

        _playerModalSet = false;
        var pauseMenuOpen = GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true };
        Input.MouseMode = pauseMenuOpen ? Input.MouseModeEnum.Visible : _mouseModeBeforeOpen;
    }

    private void ResetToAuthored()
    {
        var mood = Mood();
        mood?.ResetToAuthoredMix();
        RefreshFromDirectors();
        _lastAction = mood is null ? "Режиссёр звука не найден" : "Исходный микс деревни";
    }

    private void RefreshFromDirectors()
    {
        var mood = Mood();
        if (_weatherToggle is not null)
        {
            _weatherToggle.Disabled = mood is null;
            _weatherToggle.ButtonPressed = mood?.WeatherEnabled ?? true;
            _weatherToggle.Text = WeatherLabel(_weatherToggle.ButtonPressed);
        }

        if (_moodSlider is not null)
        {
            _moodSlider.Editable = mood is not null;
            _moodSlider.Value = mood?.Mood ?? VillageSoundMoodDirector.DefaultMood;
        }

        if (_moodValue is not null && _moodSlider is not null)
            _moodValue.Text = $"{_moodSlider.Value:0.00}";
    }

    private void RefreshStatus()
    {
        var mood = Mood();
        var world = World();
        var households = Households();
        var mode = world is null ? "нет мира" : "в игре";
        var moodText = mood is null ? "—" : $"{mood.Mood:0.00}";
        var villageDb = mood is null ? "—" : $"{mood.VillageLayerDb:0.0}";
        var dreadDb = mood is null ? "—" : $"{mood.DreadLayerDb:0.0}";
        var weather = mood is null ? "—" : mood.WeatherEnabled ? "вкл" : "выкл";
        var voices = households is null ? "—" : $"{households.PlayingVoiceCount}";
        var adhan = world is null ? "—"
            : world.AdhanPlaying ? "играет"
            : world.AdhanRecordingReady ? "готов" : "нет записи";
        var suffix = _lastAction.Length == 0 ? string.Empty : $" · {_lastAction}";
        if (_status is not null)
            _status.Text = $"Мир: {mode} · Настроение: {moodText} · Деревня/жуть: {villageDb}/{dreadDb} дБ"
                + $" · Погода: {weather} · Голоса рядом: {voices} · Азан: {adhan}"
                + $" · Записей у кнопок: {_loadedOneShots}/{_oneShotButtons.Count}{suffix}";
    }

    private void TriggerOneShot(string label, string clip)
    {
        var world = World();
        if (world is not null && string.Equals(label, "Азан у минарета", StringComparison.Ordinal))
        {
            if (world.TryPlayAdhan())
            {
                _lastAction = "Азан: запись у минарета";
                return;
            }
        }

        Audition(clip);
    }

    private void Audition(string clip)
    {
        // Headless runs have no audio output; the director applies the same guard.
        if (DisplayServer.GetName() == "headless" || _audition is null || !IsInstanceValid(_audition)) return;
        var stream = ResourceLoader.Load<AudioStream>(clip);
        if (stream is null)
        {
            _lastAction = $"Запись недоступна: {Path.GetFileName(clip)}";
            return;
        }

        if (stream is AudioStreamWav wav) wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        _audition.Stream = stream;
        _audition.Play();
        _lastAction = $"Прослушивание: {Path.GetFileName(clip)}";
    }

    private VillageSoundMoodDirector? Mood()
    {
        if (_mood is not null && IsInstanceValid(_mood)) return _mood;
        _mood = GetTree().GetFirstNodeInGroup("village_sound_mood") as VillageSoundMoodDirector;
        return _mood;
    }

    private Act1ConnectedWorld? World()
    {
        if (_world is not null && IsInstanceValid(_world)) return _world;
        _world = GetTree().Root.FindChild("Act1ConnectedWorld", true, false) as Act1ConnectedWorld;
        return _world;
    }

    private VillageHouseholdDirector? Households()
    {
        if (_households is not null && IsInstanceValid(_households)) return _households;
        _households = World()?.GetNodeOrNull<VillageHouseholdDirector>("InhabitedVillage");
        return _households;
    }

    private FirstPersonController? Player()
    {
        if (_player is not null && IsInstanceValid(_player)) return _player;
        _player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        return _player;
    }

    private static string WeatherLabel(bool enabled) => $"Буран/ветер — {(enabled ? "включён" : "выключен")}";
}
