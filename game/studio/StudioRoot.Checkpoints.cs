using System.Text;
using Godot;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

public partial class StudioRoot
{
    private bool _checkpointBusy;

    internal IReadOnlyList<StudioCheckpointCatalogSource> GetAuthoringCatalogSources() => _sections.Values
        .Select(section => section.CheckpointCatalogSource)
        .OfType<StudioCheckpointCatalogSource>()
        .ToArray();

    private void OpenCheckpoints()
    {
        if (IsAiActionBusy) return;
        var store = new StudioCheckpointStore(Workspace, GetAuthoringCatalogSources,
            () => StudioAiPanel.ResolveRevision(this));
        var dialog = new AcceptDialog
        {
            Title = "Контрольные точки",
            OkButtonText = "Закрыть",
            MinSize = new Vector2I(780, 460)
        };
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(760, 360),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(box);
        dialog.AddChild(scroll);
        var name = new LineEdit { PlaceholderText = "Название: например, двор до перестановки" };
        var author = new LineEdit { PlaceholderText = "Автор контрольной точки" };
        box.AddChild(name);
        box.AddChild(author);
        var note = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "Локальный снимок исходников, включая черновики. Ресурсы записываются как ссылки и хеши; их файлы не копируются." };
        box.AddChild(note);
        var items = new ItemList { CustomMinimumSize = new Vector2(720, 160) };
        box.AddChild(items);
        IReadOnlyList<StudioCheckpointMetadata> checkpoints = [];
        void RefreshList()
        {
            checkpoints = store.List(out var warnings);
            items.Clear();
            foreach (var entry in checkpoints)
                items.AddItem($"{entry.Name} · {entry.CreatedAt.ToLocalTime():dd.MM HH:mm} · {entry.Author}");
            if (warnings.Count > 0) note.Text = "Часть контрольных точек недоступна или повреждена. Артефакты сохранены на месте.";
        }
        var row = new HBoxContainer();
        box.AddChild(row);
        Button(row, "Создать снимок", () =>
        {
            if (IsAiActionBusy) return;
            try
            {
                var checkpoint = store.Create(name.Text, author.Text);
                RefreshList();
                note.Text = $"Снимок «{checkpoint.Name}» сохранён: {checkpoint.FileCount} исходников. Проект не изменён.";
            }
            catch (Exception error) when (CheckpointRefusal(error))
            {
                var diagnostic = BoundedCheckpointDiagnostic(error);
                GD.PushWarning("studio-checkpoint-create: " + diagnostic);
                note.Text = "Снимок не создан. Черновики сохранены, файлы проекта не изменены. Причина: " + diagnostic;
            }
        });
        Button(row, "Посмотреть восстановление…", () =>
        {
            if (IsAiActionBusy) return;
            var selected = items.GetSelectedItems();
            if (selected.Length != 1 || selected[0] >= checkpoints.Count) { note.Text = "Выберите контрольную точку."; return; }
            if (HasPendingCheckpointDrafts()) { note.Text = "Сначала сохраните или отмените текущие черновики. Восстановление не заменяет их автоматически."; return; }
            try { PreviewCheckpoint(dialog, store, store.PrepareRestore(checkpoints[selected[0]].Id)); }
            catch (Exception error) when (CheckpointRefusal(error))
            {
                note.Text = "Восстановление недоступно: изменились источники, ресурсы или состав проекта. Текущие данные и снимок сохранены.";
            }
        });
        try { RefreshList(); }
        catch (Exception error) when (CheckpointRefusal(error)) { note.Text = "Не удалось прочитать контрольные точки."; }
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        dialog.CloseRequested += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCenteredClamped(new Vector2I(800, 460), 0.9f);
    }

    private void PreviewCheckpoint(AcceptDialog checkpointDialog, StudioCheckpointStore store, StudioCheckpointRestorePlan plan)
    {
        if (HasPendingCheckpointDrafts()) { ShowBanner("Сначала сохраните или отмените текущие черновики. Восстановление не заменяет их автоматически.", error: true); return; }
        if (plan.Writes.Count == 0) { ShowBanner("Исходники уже совпадают с контрольной точкой.", error: false); return; }
        var dialog = new ConfirmationDialog
        {
            Title = "Восстановить «" + plan.Metadata.Name + "»?",
            OkButtonText = "Восстановить",
            CancelButtonText = "Отмена",
            MinSize = new Vector2I(800, 500)
        };
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(780, 390),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(box);
        dialog.AddChild(scroll);
        box.AddChild(new Label { Text = $"Изменятся {plan.ChangedPaths.Count} исходников. Это новая отменяемая команда; снимок сохранится.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var paths = new ItemList { CustomMinimumSize = new Vector2(760, 90) };
        foreach (var path in plan.ChangedPaths) paths.AddItem(path);
        box.AddChild(paths);
        var row = new HBoxContainer();
        box.AddChild(row);
        var before = new TextEdit { Editable = false, CustomMinimumSize = new Vector2(370, 280) };
        var after = new TextEdit { Editable = false, CustomMinimumSize = new Vector2(370, 280) };
        row.AddChild(before);
        row.AddChild(after);
        void ShowDiff(int index)
        {
            var path = plan.ChangedPaths[index];
            before.Text = "Сейчас:\n" + plan.CurrentTexts[path];
            after.Text = "В контрольной точке:\n" + new UTF8Encoding(false, true).GetString(plan.Writes.Single(write => write.RelativePath == path).Bytes!);
        }
        paths.ItemSelected += index => ShowDiff((int)index);
        paths.Select(0);
        ShowDiff(0);
        dialog.Confirmed += () => { _ = RestoreCheckpointAsync(store, plan); dialog.QueueFree(); };
        dialog.Canceled += dialog.QueueFree;
        dialog.CloseRequested += dialog.QueueFree;
        checkpointDialog.Hide();
        checkpointDialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCenteredClamped(new Vector2I(820, 540), 0.9f);
    }

    private async Task RestoreCheckpointAsync(StudioCheckpointStore store, StudioCheckpointRestorePlan plan)
    {
        if (IsAiActionBusy)
        {
            ShowBanner("Восстановление не начато: дождитесь завершения текущего действия Studio и откройте снимок снова.", error: true);
            return;
        }
        if (HasPendingCheckpointDrafts())
        {
            ShowBanner("Восстановление не начато: сначала сохраните или отмените текущие черновики.", error: true);
            return;
        }
        _checkpointBusy = true;
        try
        {
            RefreshStatus();
            if (HasPendingCheckpointDrafts()) throw new InvalidOperationException("Есть черновики.");
            await store.RestoreAsync(Session, plan, () => { }, () => ReloadCheckpointSourcesAndWorldAsync(plan.ChangedPaths));
            ShowBanner("Контрольная точка восстановлена. Отмена вернёт предыдущее состояние.", error: false);
        }
        catch (Exception error) when (CheckpointRefusal(error))
        {
            ShowBanner("Восстановление не подтверждено: проверьте конфликт источников, ресурсы и восстановление транзакции. Снимок сохранён.", error: true);
        }
        finally
        {
            _checkpointBusy = false;
            if (!_closing && _current is not null && _sections.TryGetValue(_current, out var section)) section.Refresh();
            RefreshStatus();
        }
    }

    private async Task ReloadCheckpointSourcesAndWorldAsync(IReadOnlyCollection<string>? changedPaths = null)
    {
        foreach (var file in Workspace.Files.Where(file => file.ChangedOnDisk()))
        {
            if (file.Dirty) throw new InvalidOperationException("Несохранённый исходник изменился во время восстановления.");
            var result = file.PullFromDisk();
            if (result.Conflicts.Count > 0 || file.Dirty) throw new InvalidOperationException("Исходник не перечитан однозначно.");
        }
        Workspace.Reindex();
        if (Workspace.IdentityConflicts.Count > 0) throw new InvalidOperationException("Восстановленные ID неоднозначны.");
        await RefreshCheckpointCatalogSectionsAsync();
        if (changedPaths is null || changedPaths.Any(path => !IsCheckpointCatalogPath(path)))
            await ((StudioWorldSection)Section("world")).ReloadGeometryAsync();
    }

    private bool HasPendingCheckpointDrafts() => Workspace.Files.Any(file => file.Dirty || file.IsNew)
        || _sections.Values.Any(section => section.HasPendingChanges);

    private bool IsCheckpointCatalogPath(string path) => _sections.Values
        .Select(section => section.CheckpointCatalogSource)
        .Any(source => source is not null && source.Path == path
            && StudioFileTransaction.IsOwnedCatalogPath(source.Owner, source.Path));

    private Task RefreshCheckpointCatalogSectionsAsync()
    {
        foreach (var section in _sections.Values.Where(section => section.CheckpointCatalogSource is not null))
            section.Refresh();
        return Task.CompletedTask;
    }

    private Func<Task> HistoryViewBarrier(bool redo)
    {
        var paths = Session.History(redo).FirstOrDefault()?.Paths;
        // Catalogue-only history refreshes forms without rebuilding the map.
        return paths is { Count: > 0 } && paths.All(IsCheckpointCatalogPath)
            ? RefreshCheckpointCatalogSectionsAsync
            : () => ReloadCheckpointSourcesAndWorldAsync(paths);
    }

    private static bool CheckpointRefusal(Exception error) => error is IOException or UnauthorizedAccessException or InvalidDataException
        or ArgumentException or InvalidOperationException or System.Text.Json.JsonException;

    private string BoundedCheckpointDiagnostic(Exception error)
    {
        const int maxLength = 240;
        var diagnostic = error.GetType().Name + ": " + error.Message;
        diagnostic = diagnostic.Replace(Workspace.Root, "<проект>", StringComparison.OrdinalIgnoreCase);
        diagnostic = string.Concat(diagnostic.Select(character => char.IsControl(character) ? ' ' : character));
        return diagnostic.Length <= maxLength ? diagnostic : diagnostic[..(maxLength - 1)] + "…";
    }
}
