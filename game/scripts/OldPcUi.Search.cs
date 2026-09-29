using Godot;

namespace Urman.Godot;

// Global search (03_oldpc_full_system.md §2): one query over the documents the
// player may read, the delivered chat history, Aidar's own notes and the
// browser pages he really visited. Closed records never take part - a matching
// locked document only raises the same non-selectable «🔒 Закрытые записи»
// line the archive uses, and nothing in the result list exposes its title.
// The search grants no knowledge: every result opens through the ordinary
// gated path, and zero results offer three terms to try instead.
public partial class OldPcUi
{
    private const int SearchTermSuggestions = 3;
    private bool _globalSearch;
    private LineEdit _startSearch = null!;

    public bool GlobalSearchActive => _globalSearch;
    public string GlobalQuery { get; private set; } = string.Empty;

    private void BuildGlobalSearch()
    {
        var row = GetNode<Container>("Screen/Computer/Layout/SearchRow");
        DesktopButton(row, "SearchEverywhere", "Искать везде", () => RunGlobalSearch(_query.Text))
            .TooltipText = "Один запрос по документам, чатам, заметкам и интернету";
        var start = new HBoxContainer { Name = "GlobalSearch" };
        _startMenuBody.AddChild(start);
        _startSearch = new LineEdit { Name = "Query", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            PlaceholderText = "Искать везде…" };
        start.AddChild(_startSearch);
        _startSearch.TextSubmitted += _ => RunGlobalSearch(_startSearch.Text);
        DesktopButton(start, "Run", "Найти", () => RunGlobalSearch(_startSearch.Text));
    }

    public void RunGlobalSearch(string query)
    {
        _globalSearch = true;
        GlobalQuery = query.Trim();
        _countSearchMiss = true;
        LaunchApplication("archive");
        RefreshResults();
    }

    // Called from the archive's own result refresh: in global mode the same list
    // carries documents, threads, notes and history entries side by side.
    private void RefreshGlobalResults()
    {
        if (_bridge is not { } bridge) return;
        _results.Clear();
        var matches = new List<(string Label, string Meta)>();
        var hidden = false;
        foreach (var document in bridge.OldPcDocuments)
        {
            if (!MatchesGlobalQuery(document.Title, document.BodyMarkdown, GlobalQuery)) continue;
            if (!bridge.IsOldPcDocumentAccessible(document.Id)) { hidden = true; continue; }
            matches.Add(($"Документ: {document.Title}", document.Id));
        }
        foreach (var chat in bridge.OldPcChats.Where(chat => bridge.EvaluateConditions(chat.Requires)))
        {
            var thread = _desktop.Chat.FirstOrDefault(item => item.Id == SnapshotThreadId(chat));
            var history = thread is null ? string.Empty : string.Join('\n', thread.Messages.Select(message => message.Text));
            if (!MatchesGlobalQuery(chat.Title, history, GlobalQuery)) continue;
            matches.Add(($"Чат: {chat.Title}", "chat:" + chat.Id));
        }
        foreach (var note in _desktop.Files.Where(note => !note.Deleted))
        {
            if (!MatchesGlobalQuery(note.Title, note.Text, GlobalQuery)) continue;
            matches.Add(($"Заметка: {note.Title}", "note:" + note.Id));
        }
        foreach (var address in _desktop.BrowserHistory.Distinct(StringComparer.Ordinal))
        {
            if (address.StartsWith("doc:", StringComparison.Ordinal))
            {
                var document = bridge.OldPcDocuments.FirstOrDefault(item => item.Id == address[4..]);
                if (document is null) continue;
                if (!MatchesGlobalQuery(document.Title, string.Empty, GlobalQuery)) continue;
                if (!bridge.IsOldPcDocumentAccessible(document.Id)) { hidden = true; continue; }
                matches.Add(($"Интернет · история: {document.Title}", document.Id));
                continue;
            }
            var label = SiteLabel(address);
            if (!MatchesGlobalQuery(label, string.Empty, GlobalQuery)) continue;
            matches.Add(($"Интернет: {label}", "site:" + address));
        }
        foreach (var (label, meta) in matches)
        {
            _results.SetItemMetadata(_results.AddItem(label), meta);
        }
        if (hidden)
        {
            _results.SetItemSelectable(_results.AddItem("🔒 Закрытые записи"), false);
        }
        _status.Text = $"Искать везде · найдено: {matches.Count}";
        if (matches.Count == 0) ShowSearchTerms(); else { _failedSearches = 0; _countSearchMiss = false; }
    }

    // The query is never a trap: case, doubled spaces, ё/е and the Tatar
    // letters versus their Cyrillic look-alikes (ә/а, ө/о, ү/у, җ/ж, ң/н, һ/х)
    // all fold to one form, so «хазрат» finds «хәзрәт» and vice versa. Both
    // spellings stay visible in the documents themselves.
    internal static string FoldSearchText(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var lastSpace = true;
        foreach (var raw in text.ToLowerInvariant())
        {
            var c = raw switch
            {
                'ә' => 'а', 'ө' => 'о', 'ү' => 'у', 'җ' => 'ж', 'ң' => 'н', 'һ' => 'х', 'ё' => 'е', 'ў' => 'у',
                _ => raw
            };
            if (char.IsWhiteSpace(c))
            {
                if (!lastSpace) builder.Append(' ');
                lastSpace = true;
                continue;
            }
            builder.Append(c);
            lastSpace = false;
        }
        return builder.ToString().TrimEnd();
    }

    private static bool MatchesGlobalQuery(string title, string body, string query)
    {
        var folded = FoldSearchText(query);
        return folded.Length > 0
            && (FoldSearchText(title).Contains(folded, StringComparison.Ordinal)
                || FoldSearchText(body).Contains(folded, StringComparison.Ordinal));
    }

    private static string SiteLabel(string address) => address switch
    {
        "home" => "Домашняя страница",
        "tatwiki" => "Татвики",
        "yalkyn" => "Ялкын · соцсеть",
        "village" => "Сайт авыла",
        "mail" => "Почта",
        _ => address
    };

    private int _failedSearches;
    private bool _countSearchMiss;

    // Stepped help, never a dead end: the first miss offers terms, the second
    // says where a word can be recalled (the notebook), the third names the
    // best candidate outright as a link.
    private void ShowSearchTerms()
    {
        if (_countSearchMiss) _failedSearches++;
        _countSearchMiss = false;
        var terms = SuggestedSearchTerms();
        _reader.BbcodeEnabled = true;
        var links = string.Join("  ", terms.Select(term => $"[url=term:{term}]{term}[/url]"));
        var reader = terms.Count == 0
            ? "Совпадений нет. Переформулируйте запрос: достаточно слова из разговора, любым регистром, можно кириллицей вместо ә, ө, ү, җ, ң, һ."
            : $"Совпадений нет. Переформулируйте запрос или попробуйте: {links}";
        if (_failedSearches >= 2)
            reader += "\n\nПодсказка: слово, которое вы слышали, записано в книжке Айдара — откройте её и найдите нужную запись.";
        if (_failedSearches >= 3 && terms.Count > 0)
            reader += $"\n\nПохоже, вы ищете: [url=term:{terms[0]}]{terms[0]}[/url]";
        _reader.Text = reader;
    }

    // Offer learned words and hints only when the current global index can
    // actually find an accessible document with them.
    private List<string> SuggestedSearchTerms()
    {
        var terms = new List<string>();
        if (_bridge is not { } bridge) return terms;
        var readableDocuments = bridge.OldPcDocuments
            .Where(document => bridge.IsOldPcDocumentAccessible(document.Id)).ToArray();
        void Offer(string? term)
        {
            var value = term?.Trim();
            if (terms.Count >= SearchTermSuggestions || string.IsNullOrEmpty(value)
                || !readableDocuments.Any(document => MatchesGlobalQuery(document.Title, document.BodyMarkdown, value)))
                return;
            if (!terms.Contains(value, StringComparer.Ordinal)) terms.Add(value);
        }
        foreach (var entry in bridge.LearnedVocabulary()) Offer(entry.Term);
        foreach (var hint in bridge.OldPcHints) Offer(hint.PointsTo);
        foreach (var document in readableDocuments)
        {
            foreach (var term in document.SuggestedTerms) Offer(term);
        }
        return terms;
    }

    // A term link in the reader re-runs the same query path; returns true when
    // the meta was a term so the ordinary document navigation stays untouched.
    private bool HandleSearchTermMeta(string meta)
    {
        if (!meta.StartsWith("term:", StringComparison.Ordinal)) return false;
        var term = meta[5..];
        _query.Text = term;
        _startSearch.Text = term;
        RunGlobalSearch(term);
        return true;
    }

    // Returns true when the row belongs to the global search and was handled.
    private bool OpenGlobalTarget(string target)
    {
        if (target.StartsWith("chat:", StringComparison.Ordinal))
        {
            _chatThreadId = target[5..];
            LaunchApplication("chat");
            RefreshChat();
            return true;
        }
        if (target.StartsWith("note:", StringComparison.Ordinal))
        {
            if (_desktop.Files.FirstOrDefault(item => item.Id == target[5..]) is not { } note) return true;
            if (note.RichText) _desktop.WriterId = note.Id; else _desktop.NoteId = note.Id;
            LaunchApplication(note.RichText ? "writer" : "notepad");
            RestoreEditor(note.RichText);
            return true;
        }
        if (target.StartsWith("site:", StringComparison.Ordinal))
        {
            LaunchApplication("browser");
            NavigateBrowser(target[5..]);
            return true;
        }
        return false;
    }
}
