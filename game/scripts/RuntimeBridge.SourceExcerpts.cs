using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    // Excerpts are normalized for every paragraph a reader selects; the patterns
    // are parsed and compiled once instead of through the static Regex cache and
    // its lock per call. Same patterns, same options (none).
    private static readonly Regex ExcerptParagraphSplit = new(@"\r?\n\s*\r?\n", RegexOptions.Compiled);
    private static readonly Regex ExcerptWhitespace = new(@"\s+", RegexOptions.Compiled);

    private const string ExcerptActionPrefix = "urman.chapter1:interaction/";
    private const string ExcerptKnowledgePrefix = "urman.chapter1:knowledge/";
    private sealed record ExcerptBinding(string DocumentId, string Action, string Knowledge, string ParagraphStart);
    private static readonly ExcerptBinding[] SourceExcerptBindings =
    [
        new("urman.oldpc:document/doc_marat_official_death_notice", "excerpt-notice-cause",
            "clue_notice_cause_excerpt", "Причина закрытия дела:"),
        new("urman.oldpc:document/rec_marat_case_register_conflict", "excerpt-register-wording",
            "clue_register_wording_excerpt", "Внешняя формулировка:"),
        new("urman.oldpc:document/rec_marat_case_register_conflict", "excerpt-register-category",
            "clue_register_category_excerpt", "Категория:"),
        new("urman.oldpc:document/msg_marat_saved_last_normal", "excerpt-message-voice",
            "clue_message_voice_excerpt", "Сегодня опять слышал")
    ];

    private object? _sourceExcerptAuthorizedSession;
    private string? _sourceExcerptAuthorizedAction;
    private bool _sourceExcerptBusy;

    // The compiled interaction owns conditions, knowledge and the saved journal
    // record. Only a selection in the currently visible source may invoke it;
    // a direct world/journal dispatch cannot stand in for reading the source.
    private bool IsSourceExcerptAction(string actionId) => SourceExcerptBindings
        .Any(binding => ExcerptActionPrefix + binding.Action == actionId);

    // These four actions belong to a previously read source, even when its
    // reader is reopened later in the investigation. They must not enter the
    // presentation-only registry scene or grant access to any other action.
    private bool OwnsSourceExcerptInteraction(string actionId) => SourceExcerptBindings
        .Any(binding => ExcerptActionPrefix + binding.Action == actionId
            && TryReadExcerptDocument(binding.DocumentId, out _));

    private bool IsSourceExcerptAuthorized(string actionId) => SessionIdentity is { } session
        && ReferenceEquals(session, _sourceExcerptAuthorizedSession)
        && _sourceExcerptAuthorizedAction == actionId && !HasActiveMainMenu();

    public SourceExcerptAvailability GetSourceExcerptAvailability(string documentId)
    {
        var bindings = SourceExcerptBindings.Where(binding => binding.DocumentId == documentId).ToArray();
        if (bindings.Length == 0 || !TryReadExcerptDocument(documentId, out _))
            return new(false, false, string.Empty);

        var available = bindings.Any(binding => IsInteractionAvailable(ExcerptActionPrefix + binding.Action));
        var complete = bindings.All(binding => ExcerptRecorded(binding.Knowledge));
        // A saved excerpt can require a fresh reading after the player's
        // mistaken interpretation. The existing action owns that request.
        if (complete && bindings.Any(binding => binding.Action == "excerpt-register-category"
                && IsInteractionAvailable(ExcerptActionPrefix + binding.Action)))
            return new(available, complete, "Выписки сохранены. Наиля просила сверить поле «Категория». Выделите его заново целиком, вместе с заголовком.");
        if (bindings.Any(binding => ExcerptRecorded(binding.Knowledge)))
            return new(available, complete, SourceExcerptSavedHint(documentId, complete));
        var hint = documentId.EndsWith("msg_marat_saved_last_normal", StringComparison.Ordinal)
            ? "Подготовьте для Алсу наблюдение Марата о голосе. Выделите один абзац целиком, сохранив его слова и оговорки."
            : "Сопоставьте причину закрытия дела в справке с формулировками внутреннего реестра. Для разговора с Наилей выделите одно поле целиком, вместе с заголовком.";
        return new(available, false, available ? hint : string.Empty);
    }

    public async Task<SourceExcerptResult> RecordSourceExcerptAsync(string documentId, string selectedText)
    {
        var session = SessionIdentity;
        if (session is null || _sourceExcerptBusy || HasActiveMainMenu()
            || !SourceExcerptSelection.IsShowingSelection(this, documentId, selectedText)
            || !TryReadExcerptDocument(documentId, out var document))
            return new(false, "Откройте исходный документ и выделите фрагмент в его тексте.");

        var selection = NormalizeExcerpt(selectedText);
        if (selection.Length == 0)
            return new(false, "Сначала выделите фрагмент в тексте документа.");

        var paragraphs = ExcerptParagraphSplit.Split(document.BodyMarkdown)
            .Select(NormalizeExcerpt).Where(value => value.Length > 0).ToArray();
        var candidates = SourceExcerptBindings.Where(binding => binding.DocumentId == documentId).ToArray();
        var matched = candidates.FirstOrDefault(binding => paragraphs.Any(paragraph =>
            paragraph.StartsWith(binding.ParagraphStart, StringComparison.Ordinal)
            && string.Equals(paragraph, selection, StringComparison.Ordinal)));
        if (matched is null)
            return new(false, documentId.EndsWith("msg_marat_saved_last_normal", StringComparison.Ordinal)
                ? "В этой выписке не хватает цельного наблюдения. Выделите один абзац, не обрывая оговорку автора."
                : "Эта выписка не позволяет сопоставить поля. Выделите одну нужную строку целиком, вместе с её заголовком.");

        var actionId = ExcerptActionPrefix + matched.Action;
        if (!IsInteractionAvailable(actionId))
            return new(false, ExcerptRecorded(matched.Knowledge)
                ? "Эта строка уже сохранена в журнале."
                : "Сейчас для этой выписки ещё не хватает изученного источника.");

        _sourceExcerptBusy = true;
        _sourceExcerptAuthorizedSession = session;
        _sourceExcerptAuthorizedAction = actionId;
        try
        {
            var committed = await DispatchInteractionAsync(actionId);
            if (!ReferenceEquals(session, SessionIdentity)) return new(false, "Состояние игры изменилось; откройте источник снова.");
            return committed && ExcerptRecorded(matched.Knowledge)
                ? new(true, GetSourceExcerptAvailability(documentId).Hint)
                : new(false, "Выписка не записана. Проверьте текущий источник и попробуйте снова.");
        }
        finally
        {
            _sourceExcerptAuthorizedSession = null;
            _sourceExcerptAuthorizedAction = null;
            _sourceExcerptBusy = false;
        }
    }

    private bool TryReadExcerptDocument(string documentId, out CompiledDocumentContent document)
    {
        document = null!;
        if (SessionIdentity is null || HasActiveMainMenu()) return false;
        var source = _content.Documents.FirstOrDefault(candidate => candidate.Id == documentId);
        if (source is null || !source.AccessConditions.All(condition =>
                Urman.Core.Narrative.ContentRuleEngine.Evaluate(condition, SelectRuntimeState()))
            || !JournalEntries().Any(entry => entry.SourceId == documentId)) return false;
        document = source;
        return true;
    }

    private static string SourceExcerptSavedHint(string documentId, bool complete)
    {
        if (documentId.EndsWith("msg_marat_saved_last_normal", StringComparison.Ordinal))
            return "Выписка сохранена. Вернитесь к Алсу и обсудите наблюдение Марата о голосе.";
        if (documentId.EndsWith("rec_marat_case_register_conflict", StringComparison.Ordinal))
            return complete
                ? "Выписки сохранены. Вернитесь к Наиле и обсудите расхождение справки и реестра."
                : "Выписка сохранена. Для сопоставления с Наилей выделите в реестре ещё одно поле целиком, вместе с заголовком.";
        return "Выписка сохранена. Сравните её с полями внутреннего реестра, затем обсудите расхождение с Наилей.";
    }

    private bool ExcerptRecorded(string knowledgeId)
    {
        if (SessionIdentity is null) return false;
        var state = SelectRuntimeState();
        return state.TryGetProperty("knowledge", out var knowledge)
            && knowledge.TryGetProperty(ExcerptKnowledgePrefix + knowledgeId, out var entry)
            && entry.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
            && status.GetString() == "confirmed";
    }

    internal static string NormalizeExcerpt(string text) => ExcerptWhitespace.Replace(
        SourceExcerptSelection.FormatPlainSourceText(text).Normalize(NormalizationForm.FormC), " ")
        .Trim().TrimEnd('.', '!', '?', '…').TrimEnd();
}
