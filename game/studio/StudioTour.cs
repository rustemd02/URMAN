using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>
/// The built-in first acquaintance (spec UX09): four short steps — place an
/// object, undo, link an event in a quest, try it in the game. It can be
/// skipped and started again from the navigation. Whether it was finished is
/// a personal preference in .urman-studio, not project data.
/// </summary>
public partial class StudioTour : PanelContainer
{
    private static readonly (string Anchor, string Text)[] Steps =
    [
        ("Nav_world", "Это «Мир». Внизу — каталог: выберите, например, «Колодец», и щёлкните по земле. Полупрозрачная рамка показывает, где он встанет; Escape отменяет."),
        ("UndoButton", "Передумали? «Отменить» или ⌘Z убирает последнее действие целиком — даже перетаскивание или целый новый квест."),
        ("Nav_quests", "«Квесты»: «Новый квест из шаблона» создаёт персонажа, диалог с выбором и последствия в мире. Условия собираются фразами, без кода."),
        ("PlayButton", "«Играть отсюда» открывает настоящую игру рядом с выбранным объектом. Ваши обычные сохранения не затрагиваются."),
    ];

    private readonly StudioRoot _studio;
    private readonly Label _text = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(340, 0) };
    private readonly Button _next = new();
    private int _step;

    public StudioTour(StudioRoot studio)
    {
        _studio = studio;
        ThemeTypeVariation = "BannerPanel";
        ZIndex = 50;
        var box = new VBoxContainer();
        AddChild(box);
        box.AddChild(_text);
        var row = new HBoxContainer();
        _next.Pressed += Next;
        _next.ThemeTypeVariation = "PrimaryButton";
        row.AddChild(_next);
        StudioRoot.Button(row, "Пропустить", Finish);
        box.AddChild(row);
    }

    public static string PreferencesPath(string root) => Path.Combine(root, ".urman-studio", "preferences.json");

    public static bool Done(string root) =>
        File.Exists(PreferencesPath(root)) && (bool?)JsonNode.Parse(File.ReadAllText(PreferencesPath(root)))?["tourDone"] == true;

    public void Start()
    {
        _step = 0;
        Visible = true;
        Show(_step);
    }

    private void Show(int step)
    {
        _text.Text = $"Знакомство {step + 1}/{Steps.Length}. {Steps[step].Text}";
        _next.Text = step < Steps.Length - 1 ? "Дальше" : "Готово";
        if (_studio.FindChild(Steps[step].Anchor, true, false) is Control anchor)
        {
            var rect = anchor.GetGlobalRect();
            var size = GetCombinedMinimumSize();
            var viewport = _studio.GetViewportRect().Size;
            // Beside a navigation item, below a toolbar button: never over the thing it explains.
            var beside = rect.Position.X < 240;
            Position = new Vector2(
                Mathf.Clamp(beside ? rect.End.X + 12 : rect.Position.X, 8, viewport.X - size.X - 8),
                Mathf.Clamp(beside ? rect.Position.Y : rect.End.Y + 8, 8, viewport.Y - size.Y - 8));
        }
    }

    private void Next()
    {
        if (++_step >= Steps.Length)
        {
            Finish();
            return;
        }

        Show(_step);
    }

    private void Finish()
    {
        Visible = false;
        var path = PreferencesPath(_studio.Workspace.Root);
        var preferences = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsObject() : new JsonObject();
        preferences["tourDone"] = true;
        AtomicFile.WriteAllText(path, preferences.ToJsonString());
    }
}
