using Godot;

namespace Urman.Godot;

public partial class JournalUi
{
    private VBoxContainer _wordPage = null!;
    private LineEdit _wordSearch = null!;
    private OptionButton _wordStatus = null!;
    private Label _wordCount = null!;
    private ItemList _wordList = null!;
    private RichTextLabel _wordDetail = null!;
    private IReadOnlyList<ResolvedVocabularyEntry> _wordEntries = [];
    private IReadOnlyList<ResolvedVocabularyEntry> _visibleWords = [];
    private string? _activeWordId;

    private void BuildVocabularyUi()
    {
        _tabs.SetTabTitle(2, "Дела");
        _tabs.AddTab("Слова"); // Existing tab indices 0–4 remain stable.
        _wordPage = new VBoxContainer { Name = "Vocabulary", Visible = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        GetNode<VBoxContainer>("Screen/Book/Layout").AddChild(_wordPage);

        var searchRow = new HBoxContainer { Name = "SearchRow" };
        _wordPage.AddChild(searchRow);
        _wordSearch = new LineEdit { Name = "Search", PlaceholderText = "Найти слово, значение или источник…",
            ClearButtonEnabled = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        searchRow.AddChild(_wordSearch);
        _wordStatus = new OptionButton { Name = "Status", CustomMinimumSize = new Vector2(170, 0) };
        foreach (var status in new[] { "Все слова", "Гипотезы", "Подтверждено" }) _wordStatus.AddItem(status);
        _wordStatus.Select(0);
        searchRow.AddChild(_wordStatus);
        _wordCount = new Label { Name = "Count", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _wordPage.AddChild(_wordCount);

        var split = new HSplitContainer { Name = "Content", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _wordPage.AddChild(split);
        _wordList = new ItemList { Name = "Entries", CustomMinimumSize = new Vector2(220, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.All };
        split.AddChild(_wordList);
        _wordDetail = new RichTextLabel { Name = "Detail", BbcodeEnabled = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ScrollActive = true, SelectionEnabled = true, FocusMode = Control.FocusModeEnum.All };
        split.AddChild(_wordDetail);

        _wordSearch.TextChanged += _ => FilterVocabulary();
        _wordStatus.ItemSelected += _ => FilterVocabulary();
        _wordList.ItemSelected += index => ShowWord(_visibleWords[(int)index]);
        _tabs.TabChanged += index =>
        {
            _wordPage.Visible = index == 5;
            if (_wordPage.Visible && _screen.Visible) _wordSearch.GrabFocus();
        };
    }

    private void RefreshVocabularyPage()
    {
        _wordEntries = _bridge?.LearnedVocabulary() ?? [];
        FilterVocabulary();
    }

    private void FilterVocabulary()
    {
        var query = _wordSearch.Text.Trim();
        _visibleWords = _wordEntries.Where(entry =>
            (_wordStatus.Selected == 0 || (_wordStatus.Selected == 1
                ? entry.Status != "confirmed" : entry.Status == "confirmed"))
            && (query.Length == 0 || entry.Term.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Meaning.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.SourceTitle.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        _wordList.Clear();
        foreach (var entry in _visibleWords)
            _wordList.AddItem($"{(entry.Status == "confirmed" ? "✓" : "?")} {entry.Term}");
        _wordCount.Text = _wordEntries.Count == 0
            ? "Прочитанные и услышанные татарские слова появятся здесь."
            : _visibleWords.Count == 0
                ? "По запросу и фильтру слов не найдено."
                : $"Показано слов: {_visibleWords.Count} из {_wordEntries.Count} · ? гипотеза · ✓ подтверждено";
        if (_visibleWords.Count == 0)
        {
            _activeWordId = null;
            _wordDetail.Text = _wordEntries.Count == 0
                ? "Откройте документ или поговорите с жителем деревни, чтобы услышать первое слово."
                : "Попробуйте другой запрос или выберите «Все слова».";
            return;
        }
        var index = _visibleWords.ToList().FindIndex(entry => entry.Id == _activeWordId);
        if (index < 0) index = 0;
        _wordList.Select(index);
        _wordList.EnsureCurrentIsVisible();
        ShowWord(_visibleWords[index]);
    }

    private void ShowWord(ResolvedVocabularyEntry entry)
    {
        _activeWordId = entry.Id;
        var lines = new List<string>
        {
            entry.Term,
            "Статус: " + (entry.Status == "confirmed" ? "Подтверждено" : "Гипотеза — значение пока не проверено"),
            "Значение: " + entry.Meaning,
            "Первый источник: " + entry.SourceTitle
        };
        if (entry.Examples.Count > 0)
        {
            lines.Add("Примеры из первого источника:");
            lines.AddRange(entry.Examples.Select(example => "• " + example));
        }
        else lines.Add("Примеры: пока нет доступного прочитанного или услышанного контекста.");
        _wordDetail.Text = string.Join("\n", lines);
    }

    private string BuildWordSummary() => _wordEntries.Count == 0
        ? "ТАТАРСКИЕ СЛОВА\n—"
        : "ТАТАРСКИЕ СЛОВА\n" + string.Join(" · ", _wordEntries.Select(entry =>
            $"{entry.Term} — {entry.Meaning}" + (entry.Status == "confirmed" ? "" : " (услышано)")))
          + (_wordEntries.Any(entry => entry.Status != "confirmed")
              ? "\n«услышано» — Айдар слышал слово, но ещё не проверил его значение."
              : string.Empty);
}
