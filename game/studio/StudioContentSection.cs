using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Godot;
using Urman.Godot;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>
/// "Контент" (spec §14, DLG04): the story's knowledge (clues, facts,
/// hypotheses, routes, contradictions), Tatar vocabulary, documents, the old
/// PC's chats and hints, and every line of text — found by type or by words,
/// edited in Russian (and Tatar where it exists) without touching IDs.
/// Documents are shown the way the game shows them: the same Markdown
/// formatting and the same notebook paper; Tatar words the game teaches are
/// marked, and a missing translation or an overlong line is flagged.
/// </summary>
public sealed class StudioContentSection(StudioRoot studio) : IStudioSection
{
    private const int LongLine = 220;
    private static readonly (string Key, string Title)[] Kinds =
    [
        ("knowledge", "Улики и сведения"), ("vocabulary", "Татарские слова"), ("document", "Документы"),
        ("chat", "Переписка на ПК"), ("hint", "Подсказки на ПК"), ("text", "Тексты и реплики")
    ];

    private static readonly (string Value, string Text)[] KnowledgeKinds =
    [
        ("clue", "улика"), ("fact", "факт"), ("hypothesis", "гипотеза"), ("route", "путь"), ("contradiction", "противоречие"), ("pressure", "давление")
    ];

    private Control? _view;
    private ItemList _kinds = null!;
    private ItemList _list = null!;
    private LineEdit _search = null!;
    private Label _count = null!;
    private PanelContainer _page = null!;
    private Label _pageTitle = null!;
    private RichTextLabel _pageBody = null!;
    private Label _pageNote = null!;
    private string _kind = "knowledge";

    public string Key => "content";
    public string Title => "Контент";
    public Control View => _view ??= Build();

    private Control Build()
    {
        var split = new HSplitContainer { SplitOffsets = [380] };
        var left = new VBoxContainer();
        _kinds = new ItemList { AutoHeight = true };
        foreach (var (_, title) in Kinds) _kinds.AddItem(title);
        _kinds.Select(0);
        _kinds.ItemSelected += index => { _kind = Kinds[index].Key; FillList(); };
        left.AddChild(_kinds);
        _search = new LineEdit { PlaceholderText = "Найти по словам или ID…", ClearButtonEnabled = true };
        _search.TextChanged += _ => FillList();
        left.AddChild(_search);
        _count = new Label { ThemeTypeVariation = "MutedLabel" };
        left.AddChild(_count);
        _list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _list.ItemSelected += index => studio.Select((string)_list.GetItemMetadata((int)index));
        left.AddChild(_list);
        split.AddChild(left);

        // The page: a document on the game's own notebook paper.
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var centre = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _page = new PanelContainer { Theme = UrmanUiTheme.Notebook, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 420) };
        var pageBox = new VBoxContainer();
        _pageTitle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _pageTitle.AddThemeColorOverride("font_color", UrmanUiTheme.NotebookInk);
        _pageTitle.AddThemeFontSizeOverride("font_size", 26);
        _pageBody = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _pageBody.AddThemeColorOverride("default_color", UrmanUiTheme.NotebookInk);
        pageBox.AddChild(_pageTitle);
        pageBox.AddChild(_pageBody);
        _page.AddChild(pageBox);
        centre.AddChild(_page);
        _pageNote = new Label { ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        centre.AddChild(_pageNote);
        scroll.AddChild(centre);
        split.AddChild(scroll);
        FillList();
        return split;
    }

    public void Refresh()
    {
        if (_view is null) return;
        FillList();
        ShowPage();
    }

    private IEnumerable<string> IdsOf(string kind) =>
        studio.Workspace.EntityIds.Where(id => EntityCatalog.KindOf(id, null) == kind);

    private void FillList()
    {
        if (_list is null) return;
        var needle = _search.Text.Trim().ToLowerInvariant();
        var items = IdsOf(_kind).Select(id => (Id: id, Summary: studio.Catalog.Describe(id)))
            .Where(item => needle.Length == 0 || $"{item.Summary.Name}\n{item.Id}\n{item.Summary.Text}".ToLowerInvariant().Contains(needle, StringComparison.Ordinal))
            .OrderBy(item => item.Summary.Name, StringComparer.CurrentCulture).ToArray();
        _list.Clear();
        foreach (var (id, summary) in items.Take(600))
        {
            var entity = studio.Workspace.Get(id);
            var flag = Problems(entity).Any() ? "⚠ " : "";
            var sub = _kind == "knowledge" ? $"  · {KnowledgeKindText((string?)entity?["kind"])}" : _kind == "vocabulary" ? $"  · {Localized.Russian(entity?["meaning"])}" : "";
            _list.AddItem($"{flag}{summary.Name}{sub}");
            _list.SetItemMetadata(_list.ItemCount - 1, id);
            _list.SetItemTooltip(_list.ItemCount - 1, id);
            if (id == studio.Selection) _list.Select(_list.ItemCount - 1);
        }

        _count.Text = items.Length > 600 ? $"Показаны 600 из {items.Length} — уточните поиск" : $"Найдено: {items.Length}";
    }

    public bool Reveal(string id)
    {
        var kind = EntityCatalog.KindOf(id, null);
        var index = Array.FindIndex(Kinds, item => item.Key == kind);
        if (index < 0 || studio.Workspace.Get(id) is null) return false;
        _kind = kind;
        if (_view is not null)
        {
            _kinds.Select(index);
            _search.Text = "";
            FillList();
        }

        studio.Select(id);
        return true;
    }

    // ---- the page ------------------------------------------------------------------

    private void ShowPage()
    {
        if (_page is null) return;
        var id = studio.Selection;
        var entity = id is null ? null : studio.Workspace.Get(id) as JsonObject;
        if (entity is null || Array.FindIndex(Kinds, item => item.Key == EntityCatalog.KindOf(id!, null)) < 0)
        {
            _pageTitle.Text = "Выберите запись слева";
            _pageBody.Text = "";
            _pageNote.Text = "Здесь запись выглядит так, как её увидит игрок: документ — на странице блокнота, улика — карточкой в книжке.";
            return;
        }

        var kind = EntityCatalog.KindOf(id!, null);
        switch (kind)
        {
            case "document" or "chat" or "hint":
                _pageTitle.Text = Localized.Russian(entity["title"]);
                _pageBody.Text = MarkWords(SourceExcerptSelection.FormatSourceText((string?)entity[MarkdownSource.BodyKey] ?? ""));
                _pageNote.Text = "Форматирование и бумага — те же, что в игре. Подчёркнуты татарские слова, которые игра подхватывает при чтении.";
                break;
            case "knowledge":
                _pageTitle.Text = Localized.Russian(entity["title"]);
                _pageBody.Text = MarkWords(Localized.Russian(entity["summary"]));
                _pageNote.Text = $"Так запись появится в книжке Айдара ({KnowledgeKindText((string?)entity["kind"])}).";
                break;
            case "vocabulary":
                _pageTitle.Text = (string?)entity["term"] ?? id!;
                _pageBody.Text = $"[i]{Localized.Russian(entity["meaning"])}[/i]";
                _pageNote.Text = "Карточка словаря в книжке.";
                break;
            default:
                _pageTitle.Text = "";
                _pageBody.Text = MarkWords(Localized.Russian(entity["value"]));
                _pageNote.Text = (string?)entity["purpose"] is { } purpose ? $"Назначение: {purpose}" : "";
                break;
        }

        var problems = Problems(entity).ToArray();
        if (problems.Length > 0) _pageNote.Text += "\n⚠ " + string.Join("\n⚠ ", problems);
    }

    // Tatar words the game teaches, underlined where they occur in the text.
    private string MarkWords(string text)
    {
        foreach (var term in IdsOf("vocabulary").Select(id => (string?)studio.Workspace.Get(id)?["term"]).OfType<string>().Where(term => term.Length > 2).Distinct())
        {
            text = Regex.Replace(text, $@"(?<![\p{{L}}]){Regex.Escape(term)}(?![\p{{L}}])", match => $"[u]{match.Value}[/u]", RegexOptions.IgnoreCase);
        }

        return text;
    }

    private static IEnumerable<string> Problems(JsonNode? entity)
    {
        if (entity is null) yield break;
        foreach (var field in new[] { "title", "summary", "value", "meaning" })
        {
            if (entity[field] is JsonObject text)
            {
                if (Localized.MissingRussian(text) && Regex.IsMatch((string?)text["default"] ?? "", "[A-Za-z]{4}")) yield return $"нет русского текста ({FieldName(field)})";
                if (field == "value" && Localized.Russian(text).Length > LongLine) yield return $"длинная реплика: {Localized.Russian(text).Length} знаков — проверьте в игре перенос";
            }
        }

        if ((string?)entity["consultantReview"]?["status"] == "pending") yield return "ждёт проверки консультанта по татарскому";
    }

    private static string FieldName(string field) => field switch { "title" => "название", "summary" => "описание", "value" => "текст", _ => "значение" };

    private static string KnowledgeKindText(string? kind) => KnowledgeKinds.FirstOrDefault(item => item.Value == kind).Text ?? kind ?? "сведение";

    // ---- properties ----------------------------------------------------------------

    public void FillProperties(StudioProperties panel)
    {
        ShowPage();
        if (studio.Selection is not { } id || studio.Workspace.Get(id) is not JsonObject entity || Array.FindIndex(Kinds, item => item.Key == EntityCatalog.KindOf(id, null)) < 0)
        {
            panel.Header("Контент", "Ничего не выбрано");
            panel.Text("Слева — типы записей и поиск. Правка русского текста не меняет ID: ссылки квестов, диалогов и мира не рвутся.", muted: true);
            return;
        }

        var summary = studio.Catalog.Describe(id);
        panel.Header(summary.Name, summary.KindLabel);
        panel.IdLine(id);
        void SetField(string[] path, JsonNode? value, string label) => studio.Session.SetField(id, path, value, label);
        void LocalizedField(string label, string field, bool multiline)
        {
            var current = Localized.Russian(entity[field]);
            if (multiline) MultiLine(panel, label, current, text => SetField([field], Localized.WithRussian(entity[field], text), label.ToLowerInvariant()));
            else panel.TextField(label, current, text => SetField([field], Localized.WithRussian(entity[field], text), label.ToLowerInvariant()));
            if (Localized.Tatar(entity[field]) is { } tatar)
            {
                panel.TextField(label + " — по-татарски", tatar, text => SetField([field], Localized.WithTatar(entity[field], text), label.ToLowerInvariant() + " (тат.)"));
            }
        }

        switch (summary.Kind)
        {
            case "knowledge":
                LocalizedField("Название", "title", multiline: false);
                LocalizedField("Как записано в книжке", "summary", multiline: true);
                var kind = new OptionButton();
                foreach (var (value, text) in KnowledgeKinds)
                {
                    kind.AddItem(text);
                    if (value == (string?)entity["kind"]) kind.Select(kind.ItemCount - 1);
                }

                kind.ItemSelected += index => SetField(["kind"], KnowledgeKinds[index].Value, "вид сведения");
                panel.Field("Вид", kind);
                panel.Text($"В начале игры: {((string?)entity["initialStatus"] == "hidden" ? "скрыто" : (string?)entity["initialStatus"])}", muted: true);
                panel.Links("Откуда узнаётся", (entity["sourceIds"] as JsonArray ?? []).Select(source => (string?)source).OfType<string>()
                    .Select(source => ($"{studio.Catalog.Describe(source).KindLabel} «{studio.Catalog.NameOf(source)}»", source)));
                break;
            case "vocabulary":
                panel.TextField("Слово", (string?)entity["term"] ?? "", text => SetField(["term"], text, "слово"));
                LocalizedField("Значение", "meaning", multiline: false);
                panel.Text($"Начальное знание: {entity["startingKnowledge"]} · проверка консультанта: {entity["consultantReview"]?["status"]}", muted: true);
                if ((string?)entity["consultantReview"]?["notes"] is { Length: > 0 } notes) panel.Text(notes, muted: true);
                panel.Links("Где перечитывается и применяется", (entity["rereadTargets"] as JsonArray ?? []).Concat(entity["applicationTargets"] as JsonArray ?? [])
                    .Select(target => (string?)target).OfType<string>().Select(target => ($"{studio.Catalog.Describe(target).KindLabel} «{studio.Catalog.NameOf(target)}»", target)));
                break;
            case "document" or "chat" or "hint":
                LocalizedField("Заголовок", "title", multiline: false);
                MultiLine(panel, "Текст (Markdown: # заголовок, **жирный**, > цитата)", (string?)entity[MarkdownSource.BodyKey] ?? "", text =>
                    SetField([MarkdownSource.BodyKey], text.EndsWith('\n') ? text : text + "\n", "текст документа"), minHeight: 320);
                if (entity["accessConditions"] is JsonArray access)
                {
                    panel.AddChild(new Label { Text = "Когда доступен:", ThemeTypeVariation = "MutedLabel" });
                    panel.AddChild(new StudioRuleEditor(studio, access, effects: false, rules => SetField(["accessConditions"], rules, "доступ к документу")));
                }

                if (entity["openEffects"] is JsonArray effects)
                {
                    panel.AddChild(new Label { Text = "Что происходит при чтении:", ThemeTypeVariation = "MutedLabel" });
                    panel.AddChild(new StudioRuleEditor(studio, effects, effects: true, rules => SetField(["openEffects"], rules, "действия при чтении")));
                }

                if (entity["oldPc"]?["searchTerms"] is JsonArray terms)
                {
                    panel.TextField("Находится поиском на ПК по словам (через запятую)", string.Join(", ", terms.Select(term => (string?)term)), text =>
                        SetField(["oldPc", "searchTerms"], new JsonArray(text.Split(',').Select(part => part.Trim()).Where(part => part.Length > 0).Select(part => (JsonNode?)part).ToArray()), "слова поиска"));
                }

                break;
            default:
                LocalizedField("Текст", "value", multiline: true);
                break;
        }

        foreach (var problem in Problems(entity)) panel.Text("⚠ " + problem);
        panel.Links("Где используется", new ReferenceIndex(studio.Workspace).UsedBy(id)
            .Select(user => ($"{studio.Catalog.Describe(user).KindLabel} «{studio.Catalog.NameOf(user)}»", user)));
        var advanced = panel.Advanced();
        advanced.AddChild(new Label { Text = $"Файл: {summary.RelativePath}", ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.Arbitrary });
    }

    /// <summary>A multi-line field that commits when focus leaves; the typed text is never dropped (UX03).</summary>
    private static void MultiLine(StudioProperties panel, string label, string value, Action<string> commit, float minHeight = 110)
    {
        var edit = panel.Field(label, new TextEdit { Text = value, WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(0, minHeight) });
        edit.FocusExited += () =>
        {
            if (edit.Text != value) commit(edit.Text);
        };
    }

    /// <summary>Test hook: select a kind tab and return the listed IDs.</summary>
    public IReadOnlyList<string> ListForTest(string kind, string search = "")
    {
        _ = View;
        _kind = kind;
        _kinds.Select(Array.FindIndex(Kinds, item => item.Key == kind));
        _search.Text = search;
        FillList();
        return Enumerable.Range(0, _list.ItemCount).Select(index => (string)_list.GetItemMetadata(index)).ToArray();
    }

    public string PageTextForTest => _pageTitle.Text + "\n" + _pageBody.Text;
}
