using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Collaboration;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>
/// "Совместная работа": changes that arrived from outside Studio (a developer
/// edit, another author's revision) are merged by entity and field; where the
/// same field changed on both sides the author decides with base / mine /
/// incoming shown in words (spec COLLAB03, COLLAB07, A18). Nothing local is
/// replaced before every decision is made.
/// </summary>
public sealed class StudioCollabSection(StudioRoot studio) : IStudioSection
{
    private readonly Dictionary<string, ExternalChange> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<(string File, string Path), ConflictChoice> _choices = [];
    private readonly List<string> _log = [];
    private Control? _view;
    private VBoxContainer _list = null!;
    private string _receiveBranch = "main";
    private string? _receiveMessage;
    private GitIncomingRevision? _incoming;
    private CancellationTokenSource? _receiveCancellation;

    public string Key => "collab";
    public string Title => "Совместная работа";
    public Control View => _view ??= Build();
    public int PendingCount => _pending.Values.Sum(change => change.Conflicts.Count);

    /// <summary>A conflict already waiting for the author about exactly this disk content.</summary>
    public bool IsPendingSame(AuthoredFile file) =>
        _pending.TryGetValue(file.RelativePath, out var change) && change.TheirBytes is { } bytes
        && File.Exists(file.FullPath) && AtomicFile.Sha256(bytes) == AtomicFile.Sha256OfFile(file.FullPath);

    public void Report(ExternalChange change)
    {
        foreach (var key in _choices.Keys.Where(key => key.File == change.RelativePath).ToArray()) _choices.Remove(key);
        if (change.Conflicts.Count > 0)
        {
            _pending[change.RelativePath] = change;
        }
        else
        {
            _pending.Remove(change.RelativePath);
            _log.Add($"{DateTime.Now:HH:mm} {change.RelativePath}: получено {(change.AutoMerged > 0 ? $"и объединено автоматически ({change.AutoMerged})" : "и перечитано")}");
        }

        Refresh();
        studio.ShowProperties();
    }

    private Control Build()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_list);
        studio.TreeExiting += () => _receiveCancellation?.Cancel();
        Refresh();
        return scroll;
    }

    public void Refresh()
    {
        if (_list is null || !GodotObject.IsInstanceValid(_list)) return;
        foreach (var child in _list.GetChildren()) child.QueueFree();
        _list.AddChild(new Label { Text = "Совместная работа", ThemeTypeVariation = "HeaderLabel" });
        _list.AddChild(new Label
        {
            Text = "Изменения файлов объединяются по объектам и полям. Общую версию можно получить для просмотра: текущая работа останется на месте. Объединение и отправка общей версии ещё не подключены.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = "MutedLabel"
        });
        var receive = new HBoxContainer();
        _list.AddChild(receive);
        var branch = new LineEdit { Text = _receiveBranch, PlaceholderText = "Ветка общей версии", Editable = _receiveCancellation is null };
        branch.TextChanged += text => _receiveBranch = text;
        receive.AddChild(branch);
        var fetch = StudioRoot.Button(receive, "Получить для просмотра", () => _ = ReceiveAsync());
        fetch.Disabled = _receiveCancellation is not null;
        var cancel = StudioRoot.Button(receive, "Остановить получение", () => _receiveCancellation?.Cancel());
        cancel.Disabled = _receiveCancellation is null;
        if (_receiveMessage is not null)
            _list.AddChild(new Label { Text = _receiveMessage, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        if (_incoming is not null)
            StudioRoot.Button(_list, "Посмотреть полученную версию…", ShowIncomingRevision);
        if (_pending.Count == 0)
        {
            _list.AddChild(new Label { Text = "✓ Конфликтов нет.", ThemeTypeVariation = "MutedLabel" });
        }

        var phrases = new ConditionPhrases(studio.Catalog);
        foreach (var (file, change) in _pending)
        {
            _list.AddChild(new Label { Text = $"Файл изменён вне Studio: {file} · объединено само: {change.AutoMerged}, нужно решить: {change.Conflicts.Count}", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            foreach (var conflict in change.Conflicts)
            {
                _list.AddChild(Card(file, conflict, phrases));
            }

            var decided = change.Conflicts.All(conflict => _choices.GetValueOrDefault((file, conflict.Path)) != ConflictChoice.Undecided);
            var apply = StudioRoot.Button(_list, decided ? "Применить решения" : "Решены не все вопросы", () => Apply(file, change));
            apply.Disabled = !decided;
            apply.ThemeTypeVariation = decided ? "PrimaryButton" : "";
            StudioRoot.Button(_list, "Обновить сравнение", () => RefreshDiskComparison(file));
        }

        if (_log.Count > 0)
        {
            _list.AddChild(new Label { Text = "Журнал полученных изменений", ThemeTypeVariation = "HeaderLabel" });
            foreach (var line in _log.TakeLast(20).Reverse()) _list.AddChild(new Label { Text = line, ThemeTypeVariation = "MutedLabel" });
        }
    }

    private async Task ReceiveAsync()
    {
        if (_receiveCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        _receiveCancellation = cancellation;
        _receiveMessage = "Получаю общую версию. Можно продолжать локальное редактирование.";
        Refresh();
        try
        {
            _incoming = await new GitRevisionInbox(studio.Workspace).ReceiveAsync(_receiveBranch, cancellation.Token);
            _receiveMessage = $"Получена версия: {_incoming.Files.Count} исходников для просмотра, {_incoming.ExcludedPaths.Count} прочих путей исключено. Ничего не применено.";
        }
        catch (OperationCanceledException) { _receiveMessage = "Получение остановлено. Локальная работа остаётся на месте; ранее полученная версия остаётся доступной."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { _receiveMessage = "Не удалось получить версию: проверьте сеть, доступ к общему репозиторию и название ветки. Локальная работа и ранее полученная версия остаются на месте."; }
        finally { _receiveCancellation = null; Refresh(); }
    }

    private void ShowIncomingRevision()
    {
        if (_incoming is not { } incoming) return;
        var dialog = new AcceptDialog { Title = "Полученная версия — просмотр", OkButtonText = "Закрыть" };
        var box = new VBoxContainer();
        dialog.AddChild(box);
        box.AddChild(new Label
        {
            Text = $"Ветка: {incoming.Branch}\nПередача: {incoming.OperationId}\nОбщая база: {incoming.BaseRevision}\nПолученная версия: {incoming.IncomingRevision}\nМоя версия — снимок текущего файла или черновика в момент выбора; он не обновляется автоматически. Ничего не применяется.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        var paths = new ItemList { CustomMinimumSize = new Vector2(1080, 80) };
        foreach (var file in incoming.Files) paths.AddItem(file.RelativePath);
        box.AddChild(paths);
        var row = new HBoxContainer();
        box.AddChild(row);
        var before = new TextEdit { Editable = false, WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(360, 200) };
        var mine = new TextEdit { Editable = false, WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(360, 200) };
        var after = new TextEdit { Editable = false, WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(360, 200) };
        row.AddChild(before);
        row.AddChild(mine);
        row.AddChild(after);
        before.Text = "Общая база:\nВыберите файл.";
        mine.Text = "Моя версия:\nВыберите файл.";
        after.Text = "Получено:\nВыберите файл.";
        var mergeSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        mergeSummary.Text = incoming.Files.Count == 0 ? "Нет доступных файлов для сравнения." : "Выберите файл для семантического сравнения.";
        box.AddChild(mergeSummary);
        var conflictScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(1080, 140),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var conflictCards = new VBoxContainer();
        conflictCards.AddThemeConstantOverride("separation", 8);
        conflictCards.AddChild(new Label { Text = incoming.Files.Count == 0 ? "Нет файлов для сравнения." : "Выберите файл для просмотра конфликтов." });
        conflictScroll.AddChild(conflictCards);
        box.AddChild(conflictScroll);
        var phrases = new ConditionPhrases(studio.Catalog);
        var reviewChoices = new Dictionary<(string File, string Path), ConflictChoice>();
        GitIncomingFile? selectedFile = null;
        string? selectedMineText = null;
        MergeResult? selectedPreview = null;
        var selectedRegistered = false;
        Button apply = null!;
        apply = StudioRoot.Button(box, "Добавить объединение в локальный черновик", ApplySelected);
        apply.ThemeTypeVariation = "PrimaryButton";
        apply.Disabled = true;

        void ClearConflictCards()
        {
            foreach (var child in conflictCards.GetChildren())
            {
                conflictCards.RemoveChild(child);
                child.QueueFree();
            }
        }

        void UpdateApplyState()
        {
            var file = selectedFile;
            var preview = selectedPreview;
            var reason = "Готово: объединение попадёт в черновик одним отменяемым действием. Автосохранения и отправки нет.";
            if (file is null || preview is null || selectedMineText is null)
                reason = "Выберите файл для семантического сравнения.";
            else if (!selectedRegistered)
                reason = "Применение недоступно: источник не зарегистрирован в Studio; он доступен только для просмотра.";
            else if (file.IncomingText is null)
                reason = "Применение удаления целого файла пока не поддерживается; доступен только просмотр.";
            else if (preview.Merged is null)
                reason = "Не удалось собрать объединённый источник; доступен только просмотр.";
            var undecided = file is not null && preview is not null
                && preview.Conflicts.Any(conflict => reviewChoices.GetValueOrDefault((file.RelativePath, conflict.Path)) == ConflictChoice.Undecided);
            if (undecided && selectedRegistered && file?.IncomingText is not null && preview?.Merged is not null)
                reason = "Выберите «Оставить моё» или «Взять входящее» для каждого конфликта.";
            apply.Disabled = file is null || preview is null || selectedMineText is null || !selectedRegistered
                || file.IncomingText is null || preview.Merged is null || undecided;
            mergeSummary.Text = preview is null
                ? reason
                : $"Автоматически объединяемых значений: {preview.AutoMerged}. Конфликтов: {preview.Conflicts.Count}. {reason}";
        }

        void RefreshConflictCards()
        {
            ClearConflictCards();
            if (selectedPreview is null)
            {
                conflictCards.AddChild(new Label { Text = "Выберите файл для просмотра конфликтов." });
                return;
            }

            if (selectedPreview.Conflicts.Count == 0)
            {
                conflictCards.AddChild(new Label { Text = "Семантических конфликтов нет." });
                return;
            }

            foreach (var conflict in selectedPreview.Conflicts)
            {
                var key = (selectedFile!.RelativePath, conflict.Path);
                var choice = reviewChoices.GetValueOrDefault(key, ConflictChoice.Undecided);
                conflictCards.AddChild(IncomingConflictCard(conflict, choice, phrases, value =>
                {
                    if (value == ConflictChoice.Undecided) reviewChoices.Remove(key);
                    else reviewChoices[key] = value;
                    RefreshConflictCards();
                    UpdateApplyState();
                }));
            }
        }

        void Select(int index)
        {
            if ((uint)index >= (uint)incoming.Files.Count) return;
            var file = incoming.Files[index];
            selectedFile = file;
            selectedMineText = null;
            selectedPreview = null;
            selectedRegistered = studio.Workspace.HasFile(file.RelativePath);
            foreach (var key in reviewChoices.Keys.Where(key => key.File == file.RelativePath).ToArray()) reviewChoices.Remove(key);
            before.Text = "Общая база:\n" + (file.BaseText ?? "Файл отсутствовал");
            mine.Text = "Моя версия:\nСчитываю текущий локальный снимок…";
            after.Text = "Получено:\n" + (file.IncomingText ?? "Файл удалён в полученной версии");
            mergeSummary.Text = "Семантическое сравнение рассчитывается для выбранного файла…";
            RefreshConflictCards();
            UpdateApplyState();
            try
            {
                var local = selectedRegistered
                    ? studio.Workspace.File(file.RelativePath)
                    : LoadIncomingReviewFallback(file.RelativePath);
                selectedMineText = local.Text;
                mine.Text = "Моя версия (снимок при выборе):\n" + selectedMineText;
                selectedPreview = local.PreviewMerge(file.BaseText, file.IncomingText);
                RefreshConflictCards();
                UpdateApplyState();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
                or InvalidOperationException or KeyNotFoundException or FormatException or System.Text.Json.JsonException)
            {
                if (selectedMineText is null)
                    mine.Text = "Моя версия:\nЛокальный источник не удалось прочитать.";
                selectedPreview = null;
                RefreshConflictCards();
                mergeSummary.Text = "Семантическое сравнение недоступно: локальный источник или одна из версий отсутствует либо некорректна. Ничего не применено. " + error.Message;
                apply.Disabled = true;
            }
        }

        void InvalidateReview(string message)
        {
            selectedFile = null;
            selectedMineText = null;
            selectedPreview = null;
            selectedRegistered = false;
            reviewChoices.Clear();
            paths.DeselectAll();
            before.Text = "Общая база:\nСравнение недействительно.";
            mine.Text = "Моя версия:\nСравнение недействительно.";
            after.Text = "Получено:\nСравнение недействительно.";
            ClearConflictCards();
            mergeSummary.Text = message;
            apply.Disabled = true;
        }

        void ApplySelected()
        {
            if (apply.Disabled || selectedFile is null || selectedMineText is null || selectedPreview is null) return;
            var file = selectedFile;
            var expectedText = selectedMineText;
            var preview = selectedPreview;
            try
            {
                var decisions = preview.Conflicts.ToDictionary(conflict => conflict, conflict =>
                    reviewChoices.GetValueOrDefault((file.RelativePath, conflict.Path), ConflictChoice.Undecided));
                var resolved = ConflictResolution.Apply(preview, decisions);
                if (!studio.Workspace.HasFile(file.RelativePath))
                {
                    InvalidateReview("Объединение не применено: источник больше не зарегистрирован в Studio. Выберите файл заново.");
                    return;
                }

                var current = studio.Workspace.File(file.RelativePath);
                if (current.Text != expectedText)
                {
                    InvalidateReview("Объединение не применено: локальный черновик изменился после снимка. Выберите файл заново.");
                    return;
                }

                var mergedText = current.RenderMergedSource(resolved);
                studio.Session.ReplaceDraft(file.RelativePath, expectedText, mergedText, "Объединить полученную версию в черновик");
                reviewChoices.Clear();
                studio.Refresh();
                dialog.QueueFree();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
                or InvalidOperationException or KeyNotFoundException or FormatException or System.Text.Json.JsonException)
            {
                InvalidateReview("Объединение не применено: снимок устарел или источник нельзя безопасно изменить. Выберите файл заново. " + error.Message);
            }
        }

        paths.ItemSelected += index => Select((int)index);
        if (incoming.Files.Count > 0) { paths.Select(0); Select(0); }
        if (incoming.ExcludedPaths.Count > 0)
        {
            var excluded = new TextEdit { Editable = false, CustomMinimumSize = new Vector2(1120, 60), Text = "Исключённые пути:\n" + string.Join("\n", incoming.ExcludedPaths) };
            box.AddChild(excluded);
        }
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        dialog.CloseRequested += dialog.QueueFree;
        studio.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(1180, 720));
    }

    private AuthoredFile LoadIncomingReviewFallback(string relativePath)
    {
        StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, relativePath);
        return AuthoredFile.Load(studio.Workspace.Root, relativePath);
    }

    private Control IncomingConflictCard(MergeConflict conflict, ConflictChoice choice, ConditionPhrases phrases, Action<ConflictChoice> decide)
    {
        var card = new PanelContainer { ThemeTypeVariation = "CardPanel" };
        var box = new VBoxContainer();
        card.AddChild(box);
        var (entity, field) = Describe(conflict.Path);
        box.AddChild(new Label
        {
            Text = conflict.Kind switch
            {
                MergeConflictKind.DeleteEdit => $"«{entity}» удалён с одной стороны и изменён с другой",
                MergeConflictKind.Order => $"Порядок в «{entity}» изменён по-разному",
                _ => $"«{entity}»: {field} изменено по-разному"
            },
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        box.AddChild(ComparisonColumns(conflict.Base, conflict.Mine, conflict.Theirs, phrases));
        var row = new HBoxContainer();
        StudioRoot.Button(row, (choice == ConflictChoice.KeepMine ? "✓ " : "") + "Оставить моё", () => decide(ConflictChoice.KeepMine));
        StudioRoot.Button(row, (choice == ConflictChoice.TakeTheirs ? "✓ " : "") + "Взять входящее", () => decide(ConflictChoice.TakeTheirs));
        StudioRoot.Button(row, "Отложить", () => decide(ConflictChoice.Undecided)).TooltipText = "Пока не принимать решение";
        box.AddChild(row);
        return card;
    }

    private Control Card(string file, MergeConflict conflict, ConditionPhrases phrases)
    {
        var card = new PanelContainer { ThemeTypeVariation = "CardPanel" };
        var box = new VBoxContainer();
        card.AddChild(box);
        var (entity, field) = Describe(conflict.Path);
        box.AddChild(new Label
        {
            Text = conflict.Kind switch
            {
                MergeConflictKind.DeleteEdit => $"«{entity}» удалён с одной стороны и изменён с другой",
                MergeConflictKind.Order => $"Порядок в «{entity}» изменён по-разному",
                _ => $"«{entity}»: {field} изменено по-разному"
            },
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        box.AddChild(ComparisonColumns(conflict.Base, conflict.Mine, conflict.Theirs, phrases));
        var choice = _choices.GetValueOrDefault((file, conflict.Path));
        var row = new HBoxContainer();
        void Choose(ConflictChoice value) { _choices[(file, conflict.Path)] = value; Refresh(); }
        StudioRoot.Button(row, (choice == ConflictChoice.KeepMine ? "✓ " : "") + "Оставить моё", () => Choose(ConflictChoice.KeepMine));
        StudioRoot.Button(row, (choice == ConflictChoice.TakeTheirs ? "✓ " : "") + "Взять входящее", () => Choose(ConflictChoice.TakeTheirs));
        StudioRoot.Button(row, "Отложить", () => Choose(ConflictChoice.Undecided)).TooltipText = "Ваша работа сохранится; объединение подождёт";
        box.AddChild(row);
        return card;
    }

    private HBoxContainer ComparisonColumns(JsonNode? @base, JsonNode? mine, JsonNode? theirs, ConditionPhrases phrases)
    {
        var columns = new HBoxContainer();
        foreach (var (title, value) in new[] { ("Было", @base), ("Моё", mine), ("Входящее", theirs) })
        {
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddChild(new Label { Text = title, ThemeTypeVariation = "MutedLabel" });
            column.AddChild(new Label { Text = Show(value, phrases), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(180, 0) });
            columns.AddChild(column);
        }

        return columns;
    }

    private void Apply(string file, ExternalChange change)
    {
        try
        {
            var choices = change.Conflicts.ToDictionary(conflict => conflict, conflict => _choices[(file, conflict.Path)]);
            var resolved = ConflictResolution.Apply(change.Merge!, choices);
            studio.Workspace.File(file).ApplyResolution(resolved, change.TheirBytes!, change.MineText);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            studio.ShowBanner($"Объединение не применено: {error.Message} Текущий черновик, ожидающее изменение и решения сохранены.", error: true);
            return;
        }

        studio.Workspace.Reindex();
        foreach (var conflict in change.Conflicts) _choices.Remove((file, conflict.Path));
        _pending.Remove(file);
        _log.Add($"{DateTime.Now:HH:mm} {file}: решено {change.Conflicts.Count} конфликт(а)");
        studio.ConflictsResolved();
    }

    private void RefreshDiskComparison(string file)
    {
        try
        {
            StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, file);
            var change = studio.Workspace.File(file).PullFromDisk();
            studio.Workspace.Reindex();
            Report(change);
            studio.ConflictsResolved();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
            or ArgumentException or System.Text.Json.JsonException)
        {
            studio.ShowBanner("Не удалось обновить сравнение; черновик и прежние решения сохранены. " + error.Message, error: true);
        }
    }

    private (string Entity, string Field) Describe(string path)
    {
        var start = path.IndexOf("[id=", StringComparison.Ordinal);
        if (start < 0)
        {
            var list = path.Trim('/') switch
            {
                "" => "весь файл",
                "stages" => "этапы квеста",
                "objectives" => "цели этапа",
                var other => other
            };
            return (list, "значение");
        }
        var end = path.IndexOf(']', start);
        var id = path[(start + 4)..end];
        var field = path[(end + 1)..].Trim('/') switch
        {
            "value/default" or "value/translations/ru" => "текст",
            "params/position" => "положение",
            "name" => "название",
            var other when other.Length == 0 => "объект целиком",
            var other => other.Replace('/', ' ')
        };
        return (studio.Catalog.NameOf(id), field);
    }

    private string Show(JsonNode? value, ConditionPhrases phrases) => value switch
    {
        null => "— удалено —",
        JsonValue text when text.TryGetValue<string>(out var s) => s,
        JsonArray { Count: 3 } point when point.All(item => item is JsonValue number && number.TryGetValue<double>(out _)) =>
            $"x {(double)point[0]!:0.##}, z {(double)point[2]!:0.##}",
        // The incoming side of an order conflict is a sequence of IDs; showing
        // the IDs themselves would say nothing about who moved where.
        JsonArray order when order.Count > 0 && order.All(item => (string?)item is not null) =>
            string.Join(" → ", order.Select(item => studio.Catalog.NameOf((string)item!))),
        JsonObject rule when rule["op"] is not null => phrases.Describe(rule),
        JsonObject entity when entity["value"]?["default"] is JsonValue text => (string)text!,
        _ => value.ToJsonString().Length > 160 ? value.ToJsonString()[..159] + "…" : value.ToJsonString()
    };

    public bool Reveal(string id) => false;

    public void DecideAllForTest(ConflictChoice choice)
    {
        foreach (var (file, change) in _pending) foreach (var conflict in change.Conflicts) _choices[(file, conflict.Path)] = choice;
        Refresh();
    }

    public void ApplyAllForTest()
    {
        foreach (var (file, change) in _pending.ToArray()) Apply(file, change);
    }

    public void FillProperties(StudioProperties panel)
    {
        panel.Header("Совместная работа", PendingCount == 0 ? "Конфликтов нет" : $"Нужно решить: {PendingCount}");
        panel.Text("Отмена ваших действий после получения чужих изменений не откатывает чужую работу: если объект уже изменён, отмена сообщит об этом.", muted: true);
    }
}
