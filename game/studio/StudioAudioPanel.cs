using System.Text.Json.Nodes;
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
    private readonly List<PreviewTarget> _targets = [];
    /// <summary>One auditionable source with a stable identity: a shared clip serves several sources.</summary>
    public sealed record PreviewTarget(AudioSourceScope Scope, string SourceId, string File, double VolumeDb, bool AlwaysSilent);
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
        var previous = Selected()?.SourceId;
        _report = AudioSourceRegistry.Inspect(_studio.Workspace.Root);
        _targets.Clear();
        _source.Clear();
        foreach (var emitter in _report.Emitters)
        {
            _targets.Add(new PreviewTarget(AudioSourceScope.LocalEmitter, $"emitter:{emitter.Id}", emitter.File, emitter.VolumeDb, emitter.DayOnly && emitter.NightOnly));
            _source.AddItem($"Дом: {emitter.Label} ({emitter.Id})");
        }

        foreach (var stem in _report.Stems)
        {
            _targets.Add(new PreviewTarget(AudioSourceScope.SharedBusStem, $"stem:{stem.Id}", stem.File, stem.VolumeDb, false));
            _source.AddItem($"Шина: {stem.Id} · {stem.Zones.Count} зон");
        }

        var index = previous is null ? 0 : _targets.FindIndex(target => string.Equals(target.SourceId, previous, StringComparison.Ordinal));
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
    public PreviewTarget? Selected() =>
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

        if (target.Scope == AudioSourceScope.LocalEmitter && _report.Emitters.FirstOrDefault(emitter => $"emitter:{emitter.Id}" == target.SourceId) is { } emitter)
        {
            lines.Add($"Данные: radius {emitter.Radius:0.#} м, volumeDb {emitter.VolumeDb:0.#}, пауза {emitter.MinWait:0.#}–{emitter.MaxWait:0.#} с, {(emitter.Outdoor ? "улица" : "в доме")}{(emitter.DayOnly ? ", только днём" : "")}{(emitter.NightOnly ? ", только ночью" : "")}.");
            lines.Add($"Слышимость по зонам: {string.Join(", ", emitter.AudibleZones)}.");
        }
        else if (target.Scope == AudioSourceScope.SharedBusStem && _report.Stems.FirstOrDefault(stem => $"stem:{stem.Id}" == target.SourceId) is { } stem)
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

        // The totals in the header say how many findings exist; the author needs the
        // ones that belong to the source in front of them, including a raw value of the
        // wrong type that the projection had to read as a default.
        foreach (var problem in RawFindings(target))
        {
            lines.Add("⚠ " + problem);
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
        // Never leave the previous preview sounding while loading, refusing or failing.
        StopPlayback();
        if (Selected() is not { } target)
        {
            _action.Text = "Нечего прослушивать: список пуст.";
            return;
        }

        // A source the registry would refuse is not auditioned either, so listening
        // can never bypass the checks that protect the game.
        if (AuditionBlocked(target) is { } blocked)
        {
            _action.Text = "Прослушивание отклонено: " + blocked;
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

        // The runtime bed loads the same file without CacheMode.Ignore, so its stream is
        // cached and shared. The preview loads its own copy and changes the loop flag only
        // on that copy: auditioning must never disable looping for the running game.
        if (ResourceLoader.Load<AudioStream>(target.File, cacheMode: ResourceLoader.CacheMode.Ignore) is not { } loaded)
        {
            _action.Text = $"Запись недоступна: {target.File}";
            return;
        }

        var stream = loaded.Duplicate() as AudioStream ?? loaded;
        DisableLoop(stream);
        var plan = AudioSourceRegistry.PlanAudition(target.Scope, target.File, target.VolumeDb, "audition");
        _audition.Stream = stream;
        _audition.VolumeDb = (float)plan.VolumeDb;
        _audition.Play();
        _action.Text = $"Прослушивание: {target.File} на шине {plan.Bus}, {plan.VolumeDb:0.#} дБ, без петли, своя копия потока. Настройки игрока и игровой микс не меняются.";
    }

    /// <summary>
    /// The findings the registry produced from the RAW authored data for this source.
    /// The audition gate must use these rather than the normalized view: a malformed raw
    /// value has already become a default there and would pass every view-based check.
    /// </summary>
    private IReadOnlyList<string> RawFindings(PreviewTarget target)
    {
        if (_report is null)
        {
            return [];
        }

        var id = target.SourceId[(target.SourceId.IndexOf(':') + 1)..];
        return _report.OutOfRangeValues
            .Concat(_report.MissingRequiredFields)
            .Concat(_report.UnknownFields)
            .Where(line => line.StartsWith(id + ":", StringComparison.Ordinal) || line.Contains(": " + id + ": ", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>The reason a source must not be auditioned, or null when it may be.</summary>
    private string? AuditionBlocked(PreviewTarget target)
    {
        if (target.File.Length == 0)
        {
            return "у источника не задан файл.";
        }

        if (!double.IsFinite(target.VolumeDb))
        {
            return "уровень источника не конечное число.";
        }

        if (!ResourceLoader.Exists(target.File))
        {
            return $"файл {target.File} не найден.";
        }

        // The registry read the raw authored fields, including the ones the view below
        // had to normalize; a finding there is a refusal, not a warning.
        if (RawFindings(target) is { Count: > 0 } raw)
        {
            return string.Join(" ", raw);
        }

        // The registry's own rules decide the level and the path for BOTH scopes, so the
        // shared-bus stem is checked too: returning early for a non-emitter would leave
        // its declared level and its path unvalidated.
        var problems = new List<string>();
        problems.AddRange(AudioSourceRegistry.Validate(target.Scope, "file", JsonValue.Create(target.File)));
        problems.AddRange(AudioSourceRegistry.Validate(target.Scope, "volumeDb", JsonValue.Create(target.VolumeDb)));
        if (_report?.Emitters.FirstOrDefault(emitter => $"emitter:{emitter.Id}" == target.SourceId) is not { } emitter)
        {
            return problems.Count == 0 ? null : string.Join(" ", problems);
        }

        problems.AddRange(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "file", JsonValue.Create(emitter.File)));
        problems.AddRange(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "radius", JsonValue.Create(emitter.Radius)));
        problems.AddRange(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "volumeDb", JsonValue.Create(emitter.VolumeDb)));
        problems.AddRange(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "minWait", JsonValue.Create(emitter.MinWait)));
        problems.AddRange(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "maxWait", JsonValue.Create(emitter.MaxWait)));
        return problems.Count == 0 ? null : string.Join(" ", problems);
    }

    /// <summary>Loop off on the preview copy only; the runtime plays WAV, Ogg and MP3.</summary>
    private static void DisableLoop(AudioStream stream)
    {
        switch (stream)
        {
            case AudioStreamWav wav:
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
                break;
            case AudioStreamOggVorbis ogg:
                ogg.Loop = false;
                break;
            case AudioStreamMP3 mp3:
                mp3.Loop = false;
                break;
        }
    }

    /// <summary>Always stops the preview and releases the stream, so nothing keeps sounding.</summary>
    public void StopAudition()
    {
        StopPlayback();
        _action.Text = "Прослушивание остановлено.";
    }

    private void StopPlayback()
    {
        if (_audition is null || !IsInstanceValid(_audition))
        {
            return;
        }

        _audition.Stop();
        _audition.Stream = null;
    }
}
