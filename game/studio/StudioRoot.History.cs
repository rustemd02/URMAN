using Godot;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.App;

public partial class StudioRoot
{
    private void OpenHistory()
    {
        var dialog = new AcceptDialog { Title = "История изменений", OkButtonText = "Закрыть" };
        var box = new VBoxContainer();
        dialog.AddChild(box);
        box.AddChild(new Label
        {
            Text = "История текущего сеанса и безопасно восстановленные файловые шаги пилота. Полная история смешанных изменений после перезапуска не восстановлена. Сверху — следующий шаг.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        if (!string.IsNullOrEmpty(_historyStartupWarning))
            box.AddChild(new Label { Text = _historyStartupWarning, AutowrapMode = TextServer.AutowrapMode.WordSmart });

        var lists = new HBoxContainer();
        box.AddChild(lists);
        var undoBox = new VBoxContainer();
        var redoBox = new VBoxContainer();
        lists.AddChild(undoBox);
        lists.AddChild(redoBox);
        undoBox.AddChild(new Label { Text = "Применённые шаги — отмена" });
        redoBox.AddChild(new Label { Text = "Отменённые шаги — повтор" });
        var applied = new ItemList { CustomMinimumSize = new Vector2(370, 220) };
        var redo = new ItemList { CustomMinimumSize = new Vector2(370, 220) };
        undoBox.AddChild(applied);
        redoBox.AddChild(redo);
        var details = new TextEdit { Editable = false, CustomMinimumSize = new Vector2(760, 180) };
        box.AddChild(details);
        IReadOnlyList<EditHistoryStep> undoSteps = [];
        IReadOnlyList<EditHistoryStep> redoSteps = [];
        void ShowStep(EditHistoryStep step)
        {
            details.Text = step.Label + "\nИзменений сущностей: " + step.EntityChangeCount
                + "\nФайловая транзакция: " + (step.TransactionId ?? "нет")
                + "\nИсходники:\n" + string.Join("\n", step.Paths);
        }
        applied.ItemSelected += index =>
        {
            redo.DeselectAll();
            if (index >= 0 && index < undoSteps.Count) ShowStep(undoSteps[(int)index]);
        };
        redo.ItemSelected += index =>
        {
            applied.DeselectAll();
            if (index >= 0 && index < redoSteps.Count) ShowStep(redoSteps[(int)index]);
        };
        var actions = new HBoxContainer();
        box.AddChild(actions);
        var undoButton = Button(actions, "Отменить следующий шаг", () => { if (Session.CanUndo) Undo(); });
        var redoButton = Button(actions, "Повторить следующий шаг", () => { if (Session.CanRedo) Redo(); });
        void RefreshHistory()
        {
            undoSteps = Session.History();
            redoSteps = Session.History(redo: true);
            applied.Clear();
            redo.Clear();
            foreach (var step in undoSteps) applied.AddItem(step.Label);
            foreach (var step in redoSteps) redo.AddItem(step.Label);
            undoButton.Disabled = !Session.CanUndo;
            redoButton.Disabled = !Session.CanRedo;
            details.Text = "Выберите шаг для просмотра. Кнопки выполняют только следующий шаг, независимо от выбранной строки.";
        }
        var subscribed = true;
        void Disconnect()
        {
            if (!subscribed) return;
            subscribed = false;
            Session.Changed -= RefreshHistory;
        }
        void Close()
        {
            Disconnect();
            dialog.QueueFree();
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        dialog.CloseRequested += Close;
        dialog.TreeExiting += Disconnect;
        Session.Changed += RefreshHistory;
        AddChild(dialog);
        RefreshHistory();
        dialog.PopupCentered(new Vector2I(820, 580));
    }
}
