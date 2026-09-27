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
        Refresh();
        return scroll;
    }

    public void Refresh()
    {
        if (_list is null) return;
        foreach (var child in _list.GetChildren()) child.QueueFree();
        _list.AddChild(new Label { Text = "Совместная работа", ThemeTypeVariation = "HeaderLabel" });
        _list.AddChild(new Label
        {
            Text = "Изменения, пришедшие извне (от разработчика или другого автора), объединяются по объектам и полям. Здесь — только то, где нужно ваше решение. Получение и отправка ревизий через общий репозиторий подключаются после решения автора о способе обмена.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = "MutedLabel"
        });
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
        }

        if (_log.Count > 0)
        {
            _list.AddChild(new Label { Text = "Журнал полученных изменений", ThemeTypeVariation = "HeaderLabel" });
            foreach (var line in _log.TakeLast(20).Reverse()) _list.AddChild(new Label { Text = line, ThemeTypeVariation = "MutedLabel" });
        }
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
        var columns = new HBoxContainer();
        foreach (var (title, value) in new[] { ("Было", conflict.Base), ("Моё", conflict.Mine), ("Входящее", conflict.Theirs) })
        {
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddChild(new Label { Text = title, ThemeTypeVariation = "MutedLabel" });
            column.AddChild(new Label { Text = Show(value, phrases), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(180, 0) });
            columns.AddChild(column);
        }

        box.AddChild(columns);
        var choice = _choices.GetValueOrDefault((file, conflict.Path));
        var row = new HBoxContainer();
        void Choose(ConflictChoice value) { _choices[(file, conflict.Path)] = value; Refresh(); }
        StudioRoot.Button(row, (choice == ConflictChoice.KeepMine ? "✓ " : "") + "Оставить моё", () => Choose(ConflictChoice.KeepMine));
        StudioRoot.Button(row, (choice == ConflictChoice.TakeTheirs ? "✓ " : "") + "Взять входящее", () => Choose(ConflictChoice.TakeTheirs));
        StudioRoot.Button(row, "Отложить", () => Choose(ConflictChoice.Undecided)).TooltipText = "Ваша работа сохранится; объединение подождёт";
        box.AddChild(row);
        return card;
    }

    private void Apply(string file, ExternalChange change)
    {
        var choices = change.Conflicts.ToDictionary(conflict => conflict, conflict => _choices[(file, conflict.Path)]);
        var resolved = ConflictResolution.Apply(change.Merge!, choices);
        studio.Workspace.File(file).ApplyResolution(resolved, change.TheirBytes!);
        studio.Workspace.Reindex();
        foreach (var conflict in change.Conflicts) _choices.Remove((file, conflict.Path));
        _pending.Remove(file);
        _log.Add($"{DateTime.Now:HH:mm} {file}: решено {change.Conflicts.Count} конфликт(а)");
        studio.ConflictsResolved();
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
