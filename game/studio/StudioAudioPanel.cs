using Godot;
using Urman.Godot;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.App;

/// <summary>
/// "Звук деревни" (spec AI-18, audio half): the authored sources as the real
/// producers see them - the household emitters and the shared ambience stems -
/// with a manual audition on the bus the game already uses, a mandatory stop and
/// a preview level that never exceeds the authored one.
///
/// The panel reads its fields through the single metadata owner
/// <see cref="AudioSourceRegistry"/>, so it keeps no list and no format of its
/// own. It never writes authored content and never touches
/// <c>user://audio-settings.json</c>: auditioning must not change the player's
/// saved volume or the project mix. The bus is the same
/// <see cref="AudioSettingsService.AmbienceBus"/> the runtime household voices
/// and the bed use, never a second bus.
/// </summary>
public partial class StudioAudioPanel : AcceptDialog
{
    private readonly StudioRoot _studio;
    private readonly OptionButton _source = new();
    private readonly Label _details = new();
    private readonly Label _action = new();
    private readonly Label _coverage = new();
    private readonly List<(AudioSourceScope Scope, string File, double VolumeDb, bool AlwaysSilent)> _targets = [];
    private AudioStreamPlayer? _audition;
    private AudioRegistryReport? _report;

    public StudioAudioPanel(StudioRoot studio)
    {
        _studio = studio;
        Title = "Звук деревни";
        OkButtonText = "Закрыть";
        Exclusive = false;
        MinSize = new Vector2I(560, 620);

        var root = new VBoxContainer();
        AddChild(root);
        root.AddChild(new Label { Text = "Источник", ThemeTypeVariation = "MutedLabel" });
        _source.CustomMinimumSize = new Vector2(520, 0);
        _source.ItemSelected += _ => Fill();
        root.AddChild(_source);

        var row = new HBoxContainer();
        StudioRoot.Button(row, "♪ Прослушать", Audition);
        StudioRoot.Button(row, "■ Стоп", StopAudition);
        StudioRoot.Button(row, "Обновить список", Reload);
        root.AddChild(row);

        _action.ThemeTypeVariation = "MutedLabel";
        _action.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_action);

        _details.ThemeTypeVariation = "MutedLabel";
        _details.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(520, 380), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _details.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_details);
        root.AddChild(scroll);

        _coverage.ThemeTypeVariation = "MutedLabel";
        _coverage.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_coverage);

        VisibilityChanged += () =>
        {
            if (!Visible)
            {
                StopAudition();
            }
        };
        Reload();
    }

    /// <summary>Re-reads both authored sources and rebuilds the list, keeping the current item when it still exists.</summary>
    public void Reload()
    {
        var previous = Selected()?.File;
        _report = AudioSourceRegistry.Inspect(_studio.Workspace.Root);
        _targets.Clear();
        _source.Clear();
        foreach (var emitter in _report.Emitters)
        {
            _targets.Add((AudioSourceScope.LocalEmitter, emitter.File, emitter.VolumeDb, emitter.DayOnly && emitter.NightOnly));
            _source.AddItem($"Дом: {emitter.Label} ({emitter.Id})");
        }

        foreach (var stem in _report.Stems)
        {
            _targets.Add((AudioSourceScope.SharedBusStem, stem.File, stem.VolumeDb, false));
            _source.AddItem($"Шина: {stem.Id} · {stem.Zones.Count} зон");
        }

        var index = previous is null ? 0 : _targets.FindIndex(target => string.Equals(target.File, previous, StringComparison.Ordinal));
        if (_targets.Count > 0)
        {
            _source.Selected = Math.Clamp(index < 0 ? 0 : index, 0, _targets.Count - 1);
        }

        _coverage.Text =
            $"Источников: {_targets.Count} ({_report.Emitters.Count} событий дома, {_report.Stems.Count} подложек шины). "
            + $"Неизвестных полей: {_report.UnknownFields.Count}, пропущенных обязательных: {_report.MissingRequiredFields.Count}, вне правил: {_report.OutOfRangeValues.Count}. "
            + $"Рукописный список в игровой debug-панели не покрывает {_report.EmittersNotAuditionable.Count} клипов каталога и содержит {_report.AuditionTargetsOutsideCatalog.Count} записей вне каталога.";
        Fill();
    }

    /// <summary>The one source auditioned by the game-feel check; null when nothing is listed.</summary>
    public (AudioSourceScope Scope, string File, double VolumeDb, bool AlwaysSilent)? Selected() =>
        _source.Selected >= 0 && _source.Selected < _targets.Count ? _targets[_source.Selected] : null;

    private void Fill()
    {
        if (_report is null || Selected() is not { } target)
        {
            _details.Text = "В этом checkout не найдено ни одного авторского звукового источника.";
            return;
        }

        var lines = new List<string>
        {
            target.Scope == AudioSourceScope.LocalEmitter
                ? "Локальный эмиттер: библиотечное событие дома, а не размещённая точка. Дом выбирается порядком массива, поэтому событие слышно в одном-двух реальных домах."
                : "Общая шина: непрерывная подложка, выбранная по зоне; её читает AmbientAudioDirector.",
            $"Файл: {target.File}",
            $"Роль в игре: {(target.Scope == AudioSourceScope.LocalEmitter ? "VillageHouseholdDirector (одноразовый клип, LoopMode.Disabled, радиус и паузы из данных)" : "AmbientAudioDirector (зацикленная подложка на шине Ambience)")}",
            $"Шина прослушивания: {AudioSettingsService.AmbienceBus} — та же, что у реальных голосов домов и подложки."
        };

        if (target.Scope == AudioSourceScope.LocalEmitter && _report.Emitters.FirstOrDefault(emitter => emitter.File == target.File) is { } emitter)
        {
            lines.Add($"Данные: radius {emitter.Radius:0.#} м, volumeDb {emitter.VolumeDb:0.#}, пауза {emitter.MinWait:0.#}–{emitter.MaxWait:0.#} с, {(emitter.Outdoor ? "улица" : "в доме")}{(emitter.DayOnly ? ", только днём" : "")}{(emitter.NightOnly ? ", только ночью" : "")}.");
            lines.Add($"Слышимость по зонам: {string.Join(", ", emitter.AudibleZones)}.");
        }
        else if (target.Scope == AudioSourceScope.SharedBusStem && _report.Stems.FirstOrDefault(stem => stem.File == target.File) is { } stem)
        {
            lines.Add($"Данные: зоны {string.Join(", ", stem.Zones)}, volumeDb {(stem.VolumeDeclared ? stem.VolumeDb.ToString("0.#") : $"{stem.VolumeDb:0.#} (по умолчанию, поля нет)")}, loop {stem.Loop}.");
            if (stem.SharedWith.Count > 0)
            {
                lines.Add($"Этот же файл используют ещё {stem.SharedWith.Count} подложек: {string.Join(", ", stem.SharedWith)}.");
            }
        }

        if (target.AlwaysSilent)
        {
            lines.Add("Внимание: у события одновременно dayOnly и nightOnly — игра не проиграет его никогда.");
        }

        var plan = AudioSourceRegistry.PlanAudition(target.Scope, target.File, target.VolumeDb, "ручное прослушивание в Studio");
        lines.Add($"Прослушивание: громкость {plan.VolumeDb:0.#} дБ (не выше авторской и не выше {AudioSourceRegistry.PreviewCeilingDb:0.#} дБ), без петли, со обязательным стопом.");
        var withoutFrameEffect = AudioSourceRegistry.FieldsWithoutFrameEffect
            .Where(field => field.Scope == target.Scope)
            .Select(field => field.Path)
            .ToArray();
        if (withoutFrameEffect.Length > 0)
        {
            lines.Add($"Поля, которые игра не читает: {string.Join(", ", withoutFrameEffect)}.");
        }

        _details.Text = string.Join("\n", lines);
    }

    /// <summary>Plays the selected source once on the shared bus; the preview never loops.</summary>
    public void Audition()
    {
        if (Selected() is not { } target)
        {
            _action.Text = "Нечего прослушивать: список пуст.";
            return;
        }

        // A headless run has no audio output; the runtime directors apply the same guard.
        if (DisplayServer.GetName() == "headless")
        {
            _action.Text = "Headless-прогон: звук не выводится.";
            return;
        }

        if (_audition is null || !IsInstanceValid(_audition))
        {
            AudioSettingsService.EnsureBuses();
            _audition = new AudioStreamPlayer { Name = "StudioAudioAudition", Bus = AudioSettingsService.AmbienceBus };
            AddChild(_audition);
        }

        if (ResourceLoader.Load<AudioStream>(target.File) is not { } stream)
        {
            _action.Text = $"Запись недоступна: {target.File}";
            return;
        }

        if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        }

        var plan = AudioSourceRegistry.PlanAudition(target.Scope, target.File, target.VolumeDb, "audition");
        _audition.Stream = stream;
        _audition.VolumeDb = (float)plan.VolumeDb;
        _audition.Play();
        _action.Text = $"Прослушивание: {target.File} на шине {plan.Bus}, {plan.VolumeDb:0.#} дБ, без петли. Настройки игрока не меняются.";
    }

    /// <summary>Always stops the preview and releases the stream, so nothing keeps sounding.</summary>
    public void StopAudition()
    {
        if (_audition is null || !IsInstanceValid(_audition))
        {
            _action.Text = "Прослушивание остановлено.";
            return;
        }

        _audition.Stop();
        _audition.Stream = null;
        _action.Text = "Прослушивание остановлено.";
    }
}
