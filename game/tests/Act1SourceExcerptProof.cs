using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>Prepare an existing source through its real reader and selection controls.</summary>
public static class Act1SourceExcerptProof
{
    private const string Prefix = "urman.chapter1:";
    private const string Notice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string Register = "urman.oldpc:document/rec_marat_case_register_conflict";
    private const string Message = "urman.oldpc:document/msg_marat_saved_last_normal";
    private static readonly HashSet<ulong> VerifiedHosts = [];
    private static readonly Dictionary<(ulong Host, string Action), int> CaptureAttempts = [];
    private sealed record Excerpt(string Document, string Paragraph, string Action, string Knowledge);
    private static readonly Excerpt[] Sources =
    [
        new(Notice, "Причина закрытия дела:", "excerpt-notice-cause", "clue_notice_cause_excerpt"),
        new(Register, "Внешняя формулировка:", "excerpt-register-wording", "clue_register_wording_excerpt"),
        new(Register, "Категория:", "excerpt-register-category", "clue_register_category_excerpt"),
        new(Message, "Сегодня опять слышал", "excerpt-message-voice", "clue_message_voice_excerpt")
    ];

    public static Task RecordNoticeCauseAsync(Node host, RuntimeBridge bridge) => RecordParagraphAsync(host, bridge, Sources[0]);
    public static async Task RecordRegisterFieldsAsync(Node host, RuntimeBridge bridge)
    {
        await RecordParagraphAsync(host, bridge, Sources[1]);
        await RecordParagraphAsync(host, bridge, Sources[2]);
    }
    public static Task RecordMessageVoiceAsync(Node host, RuntimeBridge bridge) => RecordParagraphAsync(host, bridge, Sources[3]);
    public static Task RecordAsync(Node host, RuntimeBridge bridge, string documentId, string exactText)
    {
        var source = Sources.SingleOrDefault(item => item.Document == documentId
            && exactText.TrimStart().StartsWith(item.Paragraph, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("source-excerpt-proof: unsupported source paragraph");
        return RecordParagraphAsync(host, bridge, source, exactText);
    }

    private static async Task RecordParagraphAsync(Node host, RuntimeBridge bridge, Excerpt source, string? exactText = null)
    {
        var action = Prefix + "interaction/" + source.Action;
        var knowledge = Prefix + "knowledge/" + source.Knowledge;
        if (Known() && !bridge.IsInteractionAvailable(action)) return;
        Check(bridge.JournalEntries().Any(entry => entry.SourceId == source.Document), "source has not actually been read: " + source.Document);
        var tree = host.GetTree();
        var journal = tree.GetFirstNodeInGroup("journal_ui") as JournalUi
            ?? throw new InvalidOperationException("source-excerpt-proof: journal is missing");
        var scene = bridge.ActiveSceneId;
        var before = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var categoryRetry = source.Action == "excerpt-register-category" && Known();
        var retryNpcBefore = categoryRetry ? bridge.SelectRuntimeState().GetProperty("npc").GetRawText() : null;
        Check(!await bridge.DispatchInteractionAsync(action), "direct action dispatch supplied an excerpt");
        Check(before == bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText(), "rejected action changed knowledge");

        // Close existing source presentation through its own button. This leaves
        // the archive capability state and the active narrative scene intact.
        foreach (var (group, buttonPath) in new[]
        {
            ("old_pc_ui", "Screen/Computer/Layout/Header/Close"),
            ("document_ui", "Screen/Document/Layout/Header/Close")
        })
        {
            var reader = tree.GetFirstNodeInGroup(group);
            if (reader?.GetNodeOrNull<Control>("Screen")?.Visible == true)
                reader.GetNode<Button>(buttonPath).EmitSignal(Button.SignalName.Pressed);
        }
        var dialogue = tree.GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        Check(dialogue?.IsOpen != true, "caller must finish or leave the current conversation before opening a source");
        journal.Open(bridge, source.Document);
        await Frames(3);
        Check(journal.ActiveEntryId == source.Document && journal.GetNode<Control>("Screen").Visible, "source reader did not open");
        var selector = journal.GetNode<SourceExcerptSelection>("Screen/Book/Layout/WorkArea/Reader/SourceExcerptSelection");
        Check(selector.IsVisibleInTree() && bridge.GetSourceExcerptAvailability(source.Document).Available,
            "the actual source has no available selection controls: " + source.Document);
        if (!VerifiedHosts.Contains(host.GetInstanceId()))
        {
            await SourceExcerptUiProof.VerifyAsync(host, bridge, selector);
            VerifiedHosts.Add(host.GetInstanceId());
        }

        var captureKey = (host.GetInstanceId(), source.Action);
        var captureAttempt = CaptureAttempts.GetValueOrDefault(captureKey) + 1;
        CaptureAttempts[captureKey] = captureAttempt;
        try
        {
            if (categoryRetry) CheckCategoryRetryPresentation("before-wrong-selection");
            selector.GetNode<Button>("Actions/Begin").EmitSignal(Button.SignalName.Pressed);
            await Frames(2);
            Check(selector.Selecting && selector.ExcerptText.IsVisibleInTree(), "selection mode did not open");
            var editor = selector.ExcerptText;
            var paragraph = exactText ?? Regex.Split(editor.Text, @"\r?\n\s*\r?\n")
                .Select(value => value.Trim()).SingleOrDefault(value => value.StartsWith(source.Paragraph, StringComparison.Ordinal));
            Check(!string.IsNullOrEmpty(paragraph) && editor.Text.Contains(paragraph, StringComparison.Ordinal),
                "the displayed original lacks the requested complete paragraph");
            var correctText = paragraph!;
            CheckSourceFormatting(source, correctText);

            // Selecting the heading is a real mistaken attempt. The reader must
            // explain it without issuing any source fact or narrative effect.
            var wrong = editor.Text.Split('\n').First(line => !string.IsNullOrWhiteSpace(line));
            Select(editor, wrong);
            await Frames(1);
            var record = selector.GetNode<Button>("Actions/Record");
            Check(!record.Disabled, "a real nonempty selection could not be submitted");
            var feedback = selector.FeedbackText;
            record.EmitSignal(Button.SignalName.Pressed);
            for (var frame = 0; frame < 180 && selector.FeedbackText == feedback; frame++) await Frames(1);
            Check(selector.Selecting && !string.IsNullOrWhiteSpace(selector.FeedbackText)
                && before == bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText(),
                "wrong source fragment supplied knowledge or lacked an explanation");
            ObserveSelection("wrong-before-resize", wrong);
            await CaptureFeedbackAsync(host, selector, source.Action, captureAttempt, "wrong");
            ObserveSelection("wrong-after-window-restore", wrong);
            if (categoryRetry) CheckCategoryRetryPresentation("after-wrong-and-resize");

            // Cancelling does not issue progress. The public API also rejects an
            // exact string when no visible editor currently holds its selection.
            selector.GetNode<Button>("Actions/Back").EmitSignal(Button.SignalName.Pressed);
            ObserveSelection("after-back", correctText);
            Check(!(await bridge.RecordSourceExcerptAsync(source.Document, correctText)).Success,
                "an exact string bypassed the closed selection mode");
            if (categoryRetry)
            {
                journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
                await Frames(2);
                journal.Open(bridge, source.Document);
                await Frames(3);
                CheckCategoryRetryPresentation("reopened-before-correct-selection");
                await CaptureFeedbackAsync(host, selector, source.Action, captureAttempt, "retry-reopened");
                CheckCategoryRetryPresentation("reopened-after-resize");
            }
            var caretSignals = 0;
            ulong caretSignalFrame = 0;
            void CaretDelivered()
            {
                caretSignals++;
                caretSignalFrame = Engine.GetProcessFrames();
                ObserveSelection("caret-changed-delivered", correctText);
            }
            editor.CaretChanged += CaretDelivered;
            try
            {
                selector.GetNode<Button>("Actions/Begin").EmitSignal(Button.SignalName.Pressed);
                ObserveSelection("after-begin", correctText);
                Select(editor, correctText);
                ObserveSelection("after-exact-select", correctText);
                var started = Time.GetTicksMsec();
                await Frames(1);
                ObserveSelection("after-original-one-process-frame", correctText);
                // TextEdit changes the selected range synchronously, but its
                // caret_changed signal is deferred. The production button is
                // updated by that signal; one ProcessFrame is not its barrier.
                // Never reselect text or repair a lost range while waiting.
                for (var frame = 0; frame < 180 && Time.GetTicksMsec() - started < 2000
                    && selector.Selecting && editor.GetSelectedText() == correctText
                    && (caretSignals == 0 || record.Disabled || Engine.GetProcessFrames() <= caretSignalFrame); frame++)
                    await Frames(1);
                ObserveSelection("before-strict-record-check", correctText);
                Check(editor.GetSelectedText() == correctText && !record.Disabled,
                    "the actual selected source range is incorrect");
                Check(caretSignals > 0 && Engine.GetProcessFrames() > caretSignalFrame,
                    "the source selection button was inspected before its actual caret signal settled");
            }
            finally { editor.CaretChanged -= CaretDelivered; }
            record.EmitSignal(Button.SignalName.Pressed);
            for (var frame = 0; frame < 240 && (selector.Selecting || !Known()); frame++) await Frames(1);
            Check(Known() && !selector.Selecting && bridge.ActiveSceneId == scene,
                "real source selection was not retained without a scene transition");
            Check(bridge.JournalEntries().Count(entry => entry.EntryId == knowledge) == 1,
                "the source excerpt did not retain exactly one journal entry");
            Check(!await bridge.DispatchInteractionAsync(action), "the consumed reader authorization remained usable");
            CheckSavedPresentation();
            await CaptureFeedbackAsync(host, selector, source.Action, captureAttempt, "saved");
            var savedHint = selector.GetNode<Label>("Hint").Text;
            var savedKnowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
            journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await Frames(2);
            journal.Open(bridge, source.Document);
            await Frames(3);
            CheckSavedPresentation();
            Check(selector.GetNode<Label>("Hint").Text == savedHint
                && bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == savedKnowledge
                && bridge.JournalEntries().Count(entry => entry.EntryId == knowledge) == 1,
                "reopening the source lost its next-step hint or changed the saved excerpt");
            await CaptureFeedbackAsync(host, selector, source.Action, captureAttempt, "reopened");
            GD.Print("source-excerpt-proof: document=" + source.Document + " action=" + source.Action
                + "; heading refused; cancel refused; actual TextEdit selection -> Record -> one source; scene preserved; one persistent confirmation and remaining fields survive reopen");

            void CheckSavedPresentation()
            {
                var availability = bridge.GetSourceExcerptAvailability(source.Document);
                var hint = selector.GetNode<Label>("Hint");
                var begin = selector.GetNode<Button>("Actions/Begin");
                Check(hint.IsVisibleInTree() && hint.Text.Contains("сохранен", StringComparison.Ordinal)
                    && hint.Text.Contains(source.Document == Message ? "Алсу" : "Наил", StringComparison.Ordinal)
                    && !hint.Text.Contains("через журнал", StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(selector.FeedbackText)
                    && !selector.GetNode<Label>("Feedback").IsVisibleInTree(),
                    "saved source must show one useful confirmation naming its actual conversation");
                Check(begin.IsVisibleInTree() == availability.Available
                    && (!availability.Available || !begin.Disabled),
                    "remaining source fields were disabled or a redundant saved button remained visible");
                if (categoryRetry)
                    Check(!CategoryReviewRequested() && !availability.Available && availability.Recorded
                        && !bridge.IsInteractionAvailable(action)
                        && hint.Text.Contains("Вернитесь к Наиле", StringComparison.Ordinal)
                        && !hint.Text.Contains("заново", StringComparison.Ordinal),
                        "the corrected category retained its retry prompt or lost the actual return instruction");
            }

            void CheckCategoryRetryPresentation(string phase)
            {
                var availability = bridge.GetSourceExcerptAvailability(source.Document);
                var hint = selector.GetNode<Label>("Hint");
                GD.Print("source-excerpt-category-retry: " + System.Text.Json.JsonSerializer.Serialize(new {
                    phase, reviewRequested = CategoryReviewRequested(), known = Known(),
                    availability.Available, availability.Recorded, hint = hint.Text, selecting = selector.Selecting }));
                Check(Known() && CategoryReviewRequested() && availability.Recorded && availability.Available
                    && bridge.IsInteractionAvailable(action)
                    && journal.ActiveEntryId == source.Document && selector.IsVisibleInTree()
                    && hint.IsVisibleInTree() && hint.Text.Contains("Наиля", StringComparison.Ordinal)
                    && hint.Text.Contains("Категория", StringComparison.Ordinal)
                    && hint.Text.Contains("заново", StringComparison.Ordinal)
                    && hint.Text.Contains("целиком", StringComparison.Ordinal)
                    && !hint.Text.Contains("Вернитесь к Наиле", StringComparison.Ordinal),
                    "the pending category review did not ask for a fresh complete category excerpt: " + phase);
                Check(bridge.ActiveSceneId == scene
                    && bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == before
                    && bridge.SelectRuntimeState().GetProperty("npc").GetRawText() == retryNpcBefore
                    && bridge.JournalEntries().Count(entry => entry.EntryId == knowledge) == 1,
                    "reading, refusing or reopening the pending category review changed its state: " + phase);
            }

            void ObserveSelection(string phase, string expected)
            {
                var body = (RichTextLabel)editor.GetParent();
                var selected = editor.GetSelectedText();
                GD.Print("source-excerpt-selection-state: " + System.Text.Json.JsonSerializer.Serialize(new {
                    action = source.Action, phase, processFrame = Engine.GetProcessFrames(), physicsFrame = Engine.GetPhysicsFrames(),
                    selecting = selector.Selecting, editorVisible = editor.IsVisibleInTree(), recordDisabled = record.Disabled,
                    selected, expected, exact = selected == expected, editorContainsExpected = editor.Text.Contains(expected, StringComparison.Ordinal),
                    selectionFrom = new[] { editor.GetSelectionFromLine(), editor.GetSelectionFromColumn() },
                    selectionTo = new[] { editor.GetSelectionToLine(), editor.GetSelectionToColumn() },
                    caret = new[] { editor.GetCaretLine(), editor.GetCaretColumn() },
                    editorFocused = editor.HasFocus(), focusOwner = editor.GetViewport().GuiGetFocusOwner()?.GetPath().ToString(),
                    editorRect = editor.GetGlobalRect().ToString(), bodyRect = body.GetGlobalRect().ToString(),
                    editorScroll = editor.ScrollVertical, bodyScroll = body.GetVScrollBar().Value,
                    logicalViewport = editor.GetViewport().GetVisibleRect().ToString(), windowSize = DisplayServer.WindowGetSize().ToString() }));
            }
        }
        finally
        {
            journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await Frames(2);
        }

        bool Known() => bridge.SelectRuntimeState().GetProperty("knowledge").TryGetProperty(knowledge, out var entry)
            && entry.GetProperty("status").GetString() == "confirmed";
        bool CategoryReviewRequested() => bridge.SelectRuntimeState().GetProperty("npc")
            .TryGetProperty(Prefix + "character/naila", out var npc)
            && npc.TryGetProperty("category_excerpt_review_requested", out var value)
            && value.ValueKind == System.Text.Json.JsonValueKind.True;
        async Task Frames(int count)
        {
            for (var frame = 0; frame < count; frame++) await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void CheckSourceFormatting(Excerpt source, string selectedParagraph)
    {
        var cases = new (string Source, string Display)[]
        {
            ("# Имена\n\n**Габдулла Сабиров** — **1938–2009**.", "Имена\n\nГабдулла Сабиров — 1938–2009."),
            ("**Әби** и **ә**; 2 * 3; *сноска; *слово*.", "Әби и ә; 2 * 3; *сноска; *слово*."),
            ("**Строка\r\nпродолжается**", "Строка\r\nпродолжается"),
            ("**Нет конца\n\nДругой абзац**", "**Нет конца\n\nДругой абзац**"),
            ("***три***", "***три***"),
            ("****", "****"),
            ("** пробел **", "** пробел **"),
            ("\\**буквально**", "\\**буквально**"),
            ("**[Школа](doc:urman.chapter1:document/school-transport-notice)**", "[Школа](doc:urman.chapter1:document/school-transport-notice)")
        };
        foreach (var sample in cases)
        {
            var display = SourceExcerptSelection.FormatSourceText(sample.Source);
            Check(display == sample.Display && SourceExcerptSelection.FormatSourceText(display) == display,
                "plain source formatting lost literal content, links or stable paragraph boundaries");
        }

        // Only decorate a copy of the already found paragraph. The real document,
        // knowledge and reader stay unchanged; the following UI selection/Save
        // exercises the same canonical text and its existing no-duplicate checks.
        var markedSource = "**" + source.Paragraph + "**" + selectedParagraph[source.Paragraph.Length..];
        var displayed = SourceExcerptSelection.FormatSourceText(markedSource);
        Check(displayed == selectedParagraph
            && RuntimeBridge.NormalizeExcerpt(markedSource) == RuntimeBridge.NormalizeExcerpt(displayed),
            "a displayed strong-formatted field no longer matches its original source");
        Check(RuntimeBridge.NormalizeExcerpt(markedSource) != RuntimeBridge.NormalizeExcerpt(displayed + " Другой вывод"),
            "normalization accepted added words beyond the selected source");
        GD.Print("source-formatting-proof: paired strong -> unchanged canonical paragraph -> real selection/save follows; literal stars and links retained");
    }

    private static async Task CaptureFeedbackAsync(Node host, SourceExcerptSelection selector, string action, int attempt, string phase)
    {
        var output = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
        {
            GD.Print("source-excerpt-proof: feedback captures external/not-run (headless)");
            return;
        }
        Check(System.IO.Path.IsPathFullyQualified(output), "feedback capture directory must be absolute");
        System.IO.Directory.CreateDirectory(output);
        var hostName = string.Concat(host.Name.ToString().Select(character => char.IsLetterOrDigit(character) ? character : '_'));
        var windowSize = DisplayServer.WindowGetSize();
        var windowPosition = DisplayServer.WindowGetPosition();
        var windowMode = DisplayServer.WindowGetMode();
        var selecting = selector.Selecting;
        var selectedText = selector.ExcerptText.GetSelectedText();
        var editorScroll = selector.ExcerptText.ScrollVertical;
        var body = (RichTextLabel)selector.ExcerptText.GetParent();
        var bodyScroll = body.GetVScrollBar().Value;
        var feedback = selector.GetNode<Label>(phase == "wrong" ? "Feedback" : "Hint");
        var feedbackText = feedback.Text;
        Check(feedback.IsVisibleInTree() && !string.IsNullOrWhiteSpace(feedbackText), "feedback is not actually visible");
        try
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                DisplayServer.WindowSetSize(size);
                await Render("resize");
                // A background screenshot may ForceDraw synchronously. Let the
                // actual UI process its new scale and container layout first.
                await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
                await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
                await Render("layout");
                Check(selector.Selecting == selecting && selector.ExcerptText.GetSelectedText() == selectedText
                    && feedback.Text == feedbackText, "capture changed the actual source feedback or selection");
                Check(feedback.IsVisibleInTree() && feedback.GetViewportRect().Grow(1).Encloses(feedback.GetGlobalRect())
                    && feedback.GetGlobalRect().Size.Y + 1 >= feedback.GetMinimumSize().Y,
                    "source feedback is clipped at " + size);
                var path = System.IO.Path.Combine(output, $"source_excerpt_{hostName}_{action}_attempt{attempt:D2}_{phase}_{size.X}x{size.Y}.png");
                Check(!System.IO.File.Exists(path), "refusing to overwrite feedback evidence: " + path);
                using var image = host.GetViewport().GetTexture().GetImage();
                Check(!image.IsEmpty() && image.GetWidth() == size.X && image.GetHeight() == size.Y
                    && image.SavePng(path) == Error.Ok, "source feedback capture failed: " + path);
                GD.Print("source-excerpt-proof: feedback capture " + path);
                // Independent of the production screen-transform API: measure
                // actual raster pixels against the logical viewport extent.
                var logical = selector.GetViewport().GetVisibleRect();
                var rasterSize = new Vector2(image.GetWidth(), image.GetHeight());
                Check(logical.Size.X > 0 && logical.Size.Y > 0, "feedback has no logical viewport extent");
                var ratio = rasterSize / logical.Size;
                var toRaster = new Transform2D(new Vector2(ratio.X, 0), new Vector2(0, ratio.Y), -logical.Position * ratio);
                var textScale = (host.GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)
                    ?.Accessibility.TextScale ?? throw new InvalidOperationException("Feedback proof has no player settings.");
                var labels = new[] { selector.GetNode<Label>("Hint"), selector.GetNode<Label>("Feedback"),
                    body.GetParent().GetNode<Label>("Source") }.Where(label => label.IsVisibleInTree()).ToArray();
                Rect2 Bounds(Control control)
                {
                    var transform = toRaster * control.GetGlobalTransformWithCanvas();
                    var corners = new[] { Vector2.Zero, control.Size, new Vector2(control.Size.X, 0), new Vector2(0, control.Size.Y) }
                        .Select(point => transform * point).ToArray();
                    var first = new Vector2(corners.Min(point => point.X), corners.Min(point => point.Y));
                    var last = new Vector2(corners.Max(point => point.X), corners.Max(point => point.Y));
                    return new Rect2(first, last - first);
                }
                var readerBounds = Bounds((Control)body.GetParent());
                var metrics = labels.Select(label =>
                {
                    var transform = toRaster * label.GetGlobalTransformWithCanvas();
                    var actualScale = Math.Min(transform.X.Length(), transform.Y.Length());
                    return new { name = label.Name.ToString(), logicalFont = label.GetThemeFontSize("font_size"),
                        screenFontPixels = label.GetThemeFontSize("font_size") * actualScale,
                        bounds = Bounds(label), logicalHeight = label.Size.Y, minimumHeight = label.GetMinimumSize().Y };
                }).ToArray();
                GD.Print("source-excerpt-raster-typography: " + System.Text.Json.JsonSerializer.Serialize(new {
                    action, phase, size = size.ToString(), textScale, rasterSize = rasterSize.ToString(),
                    logicalViewport = logical.ToString(), rasterRatio = ratio.ToString(), readerBounds = readerBounds.ToString(),
                    labels = metrics.Select(value => new { value.name, value.logicalFont, value.screenFontPixels,
                        bounds = value.bounds.ToString(), value.logicalHeight, value.minimumHeight }),
                    oracle = "actual raster / logical viewport extent times GlobalWithCanvas; no runtime screen-transform API" }));
                Check(metrics.All(value => value.screenFontPixels >= 14 * textScale - .05
                    && readerBounds.Grow(1).Encloses(value.bounds)
                    && new Rect2(Vector2.Zero, rasterSize).Grow(1).Encloses(value.bounds)
                    && value.logicalHeight + 1 >= value.minimumHeight),
                    "source hint/error/attribution is too small or clipped in actual raster at " + size);
            }
        }
        finally
        {
            DisplayServer.WindowSetSize(windowSize);
            DisplayServer.WindowSetMode(windowMode);
            DisplayServer.WindowSetPosition(windowPosition);
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            await Render("restore");
            selector.ExcerptText.ScrollVertical = editorScroll;
            body.GetVScrollBar().Value = bodyScroll;
        }

        Task Render(string step) => Act1StateFlowProof.WaitForRenderedFrameAsync(host, $"source-excerpt-feedback/{action}/attempt{attempt}/{phase}/{step}");
    }

    private static void Select(TextEdit editor, string text)
    {
        var start = editor.Text.IndexOf(text, StringComparison.Ordinal);
        Check(start >= 0 && text.Length > 0, "selection is not part of the visible source");
        var (fromLine, fromColumn) = Position(start);
        var (toLine, toColumn) = Position(start + text.Length);
        editor.Select(fromLine, fromColumn, toLine, toColumn);
        (int Line, int Column) Position(int offset)
        {
            var prefix = editor.Text[..offset];
            var line = prefix.Count(character => character == '\n');
            var column = prefix[(prefix.LastIndexOf('\n') + 1)..].EnumerateRunes().Count();
            return (line, column);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("source-excerpt-proof: " + message);
    }
}
