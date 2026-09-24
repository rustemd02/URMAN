using System.Text.RegularExpressions;
using Godot;

namespace Urman.Godot;

public readonly record struct SourceExcerptAvailability(bool Available, bool Recorded, string Hint);
public readonly record struct SourceExcerptResult(bool Success, string Feedback);

/// <summary>
/// A selection mode inside an existing document reader. RuntimeBridge owns
/// source eligibility, excerpt validation and every persistent consequence.
/// </summary>
public partial class SourceExcerptSelection : VBoxContainer
{
    private RichTextLabel _body = null!;
    private Button _styleSource = null!;
    private TextEdit _editor = null!;
    private Button _begin = null!;
    private Button _record = null!;
    private Button _back = null!;
    private Label _hint = null!;
    private Label _feedback = null!;
    private Label? _sourceFooter;
    private int _hintLogicalSize;
    private int _footerBaseSize;
    private float _textScale = 1f;
    private float _screenScale = -1f;
    private RuntimeBridge? _bridge;
    private object? _session;
    private string? _documentId;
    private string _bodyVersion = string.Empty;
    private SourceExcerptAvailability _availability;
    private bool _selecting;
    private bool _submitting;
    private long _bindingGeneration;
    private long _selectionGeneration;
    private int _readerVisibleCharacters;
    private bool _readerScrollActive;

    public bool Selecting => _selecting;
    public TextEdit ExcerptText => _editor;
    public string FeedbackText => _feedback.Text;
    public event Action<bool>? SelectionModeChanged;

    // The existing readers share plain source text. Paired inline strong markers
    // are presentation syntax; literal stars, links and paragraph breaks remain.
    internal static string FormatSourceText(string text)
    {
        var headings = Regex.Replace(text, @"^#{1,6}[ \t]+", string.Empty, RegexOptions.Multiline);
        // A delimiter in another paragraph cannot close an unfinished strong span.
        var paragraphs = Regex.Split(headings, @"(\r?\n[ \t]*\r?\n)");
        for (var index = 0; index < paragraphs.Length; index += 2)
            paragraphs[index] = Regex.Replace(paragraphs[index],
                @"(?<![\\*])\*\*(?!\*)(?=\S)(.+?)(?<=\S)(?<![\\*])\*\*(?!\*)", "$1", RegexOptions.Singleline);
        return string.Concat(paragraphs);
    }

    // Paper readers keep a link's authored label. Browser routing remains in
    // FormatSourceText/OldPcUi; a machine destination is not part of an excerpt.
    internal static string FormatPlainSourceText(string text) => Regex.Replace(
        FormatSourceText(text), @"(?<![\\!])\[([^\]\r\n]+)\]\((?:doc:[^\s)]+|home|tatwiki|yalkyn|village|mail)\)", "$1");

    public static SourceExcerptSelection Attach(RichTextLabel body, Button styleSource)
    {
        var selection = new SourceExcerptSelection
        {
            Name = "SourceExcerptSelection", _body = body, _styleSource = styleSource,
            Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var parent = body.GetParent();
        parent.AddChild(selection);
        parent.MoveChild(selection, body.GetIndex() + 1);
        return selection;
    }

    public override void _Ready()
    {
        AddToGroup("source_excerpt_selection");
        SetProcessInput(false);
        AddThemeConstantOverride("separation", 5);
        _editor = new TextEdit
        {
            Name = "SourceExcerptText", Visible = false, Editable = false,
            SelectingEnabled = true, CaretMultiple = false,
            CaretDrawWhenEditableDisabled = true, DeselectOnFocusLossEnabled = false,
            DragAndDropSelectionEnabled = false, ContextMenuEnabled = false,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            ScrollFitContentHeight = false, ScrollFitContentWidth = false,
            FocusMode = FocusModeEnum.All
        };
        _body.AddChild(_editor);
        _editor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _editor.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _editor.AddThemeStyleboxOverride("read_only", new StyleBoxEmpty());
        _editor.CaretChanged += RefreshButtons;
        _editor.GuiInput += SelectionInput;
        _hint = new Label { Name = "Hint", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_hint);
        var actions = new HFlowContainer { Name = "Actions" };
        AddChild(actions);
        _begin = AddButton(actions, "Begin", "Выделить фрагмент", BeginSelection);
        _back = AddButton(actions, "Back", "К чтению [Esc]", EndSelection);
        _record = AddButton(actions, "Record", "Выписать выделенное", RecordSelection);
        _feedback = new Label
        {
            Name = "Feedback", Visible = false, FocusMode = FocusModeEnum.All,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        AddChild(_feedback);
        // Only the source attribution beside this reader belongs to this
        // presentation adjustment; other notebook labels keep their theme.
        _sourceFooter = _body.GetParent().GetNodeOrNull<Label>("Source");
        _footerBaseSize = _sourceFooter?.GetThemeFontSize("font_size") ?? 16;
        _body.VisibilityChanged += ReaderVisibilityChanged;
        ApplyPresentation();
    }

    public void Bind(RuntimeBridge? bridge, string? documentId)
    {
        if (_bridge != bridge || _documentId != documentId || _bodyVersion != _body.Text
            || !ReferenceEquals(_session, bridge?.SessionIdentity))
        {
            Clear();
            _bridge = bridge;
            _session = bridge?.SessionIdentity;
            _documentId = documentId;
            _bodyVersion = _body.Text;
            if (_bridge is not null) _bridge.RuntimeStateChanged += RefreshAvailability;
        }
        RefreshAvailability();
    }

    public void Clear()
    {
        _bindingGeneration++;
        if (_bridge is not null) _bridge.RuntimeStateChanged -= RefreshAvailability;
        _bridge = null;
        _session = null;
        _documentId = null;
        _availability = default;
        _submitting = false;
        EndSelection();
        if (_feedback is not null) _feedback.Text = string.Empty;
        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_bridge is not null) _bridge.RuntimeStateChanged -= RefreshAvailability;
        if (IsInstanceValid(_body)) _body.VisibilityChanged -= ReaderVisibilityChanged;
    }

    private void RefreshAvailability()
    {
        if (_bridge is not null && !ReferenceEquals(_session, _bridge.SessionIdentity))
        {
            Clear();
            return;
        }
        var next = _bridge is not null && _documentId is not null
            ? _bridge.GetSourceExcerptAvailability(_documentId) : default;
        if (next != _availability)
        {
            _feedback.Text = string.Empty;
            if (!_submitting) _editor.Deselect();
        }
        _availability = next;
        if (!next.Available && _selecting && !_submitting) EndSelection();
        _hint.Text = next.Hint ?? string.Empty;
        if (_selecting) _hint.Text += "\nВыделите текст мышью или Shift + стрелками. Tab — к кнопке.";
        Visible = _body.Visible && (next.Available || next.Recorded || !string.IsNullOrWhiteSpace(next.Hint));
        RefreshButtons();
    }

    private void ReaderVisibilityChanged()
    {
        // The existing image/text tabs remain the owner of body visibility.
        if (!_body.IsVisibleInTree() && _selecting) EndSelection();
        RefreshAvailability();
    }

    private void BeginSelection()
    {
        RefreshAvailability();
        if (!_availability.Available || _submitting || !_body.IsVisibleInTree()) return;
        _readerVisibleCharacters = _body.VisibleCharacters;
        _readerScrollActive = _body.ScrollActive;
        _editor.Text = _body.GetParsedText();
        _editor.Deselect();
        _body.VisibleCharacters = 0;
        _body.ScrollActive = false;
        _selecting = true;
        _selectionGeneration++;
        SelectionModeChanged?.Invoke(true);
        _editor.Visible = true;
        _feedback.Text = string.Empty;
        SetProcessInput(true);
        RefreshAvailability();
        _editor.GrabFocus();
    }

    private void EndSelection()
    {
        if (!_selecting) return;
        _selecting = false;
        _selectionGeneration++;
        SelectionModeChanged?.Invoke(false);
        SetProcessInput(false);
        _editor.Visible = false;
        _editor.Deselect();
        _body.VisibleCharacters = _readerVisibleCharacters;
        _body.ScrollActive = _readerScrollActive;
        _hint.Text = _availability.Hint ?? string.Empty;
        RefreshButtons();
        if (_body.IsVisibleInTree()) _body.GrabFocus();
    }

    public override void _Input(InputEvent input)
    {
        if (!_selecting || !IsVisibleInTree() || !input.IsActionPressed("ui_cancel")) return;
        EndSelection();
        GetViewport().SetInputAsHandled();
    }

    private void SelectionInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            Callable.From(RefreshButtons).CallDeferred();
            return;
        }
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Tab)
        {
            (key.ShiftPressed || _record.Disabled ? _back : _record).GrabFocus();
            _editor.AcceptEvent();
        }
        else if ((key.CtrlPressed || key.MetaPressed) && key.Keycode is Key.Enter or Key.KpEnter)
        {
            RecordSelection();
            _editor.AcceptEvent();
        }
        else Callable.From(RefreshButtons).CallDeferred();
    }

    private async void RecordSelection()
    {
        if (_submitting || !_selecting || !IsVisibleInTree()
            || _bridge is not { } bridge || _documentId is not { } documentId) return;
        var selectedText = _editor.GetSelectedText();
        if (string.IsNullOrWhiteSpace(selectedText)) return;
        var session = bridge.SessionIdentity;
        if (session is null || !ReferenceEquals(_session, session)) { RefreshAvailability(); return; }
        if (!bridge.GetSourceExcerptAvailability(documentId).Available) { RefreshAvailability(); return; }
        var generation = _bindingGeneration;
        var selectionGeneration = _selectionGeneration;
        _submitting = true;
        RefreshButtons();
        try
        {
            var result = await bridge.RecordSourceExcerptAsync(documentId, selectedText);
            if (!IsInsideTree() || generation != _bindingGeneration
                || selectionGeneration != _selectionGeneration || !_selecting
                || !ReferenceEquals(session, bridge.SessionIdentity)) return;
            if (result.Success) EndSelection();
            RefreshAvailability();
            // The saved state and next conversation live in the persistent hint.
            // Transient feedback is reserved for a refused or failed selection.
            _feedback.Text = result.Success ? string.Empty : result.Feedback;
            _feedback.Visible = !string.IsNullOrWhiteSpace(_feedback.Text);
        }
        catch (Exception exception)
        {
            GD.PushError("Source excerpt failed: " + exception);
            if (IsInsideTree() && generation == _bindingGeneration
                && selectionGeneration == _selectionGeneration && _selecting
                && ReferenceEquals(session, bridge.SessionIdentity))
            {
                _feedback.Text = "Не удалось сохранить выписку. Попробуйте ещё раз.";
                _feedback.Visible = true;
            }
        }
        finally
        {
            if (IsInsideTree() && generation == _bindingGeneration
                && ReferenceEquals(session, bridge.SessionIdentity))
            {
                _submitting = false;
                RefreshButtons();
            }
        }
    }

    private void RefreshButtons()
    {
        _begin.Visible = !_selecting && _availability.Available;
        _begin.Disabled = !_availability.Available || _submitting;
        _back.Visible = _record.Visible = _selecting;
        _record.Disabled = _submitting || !_availability.Available || string.IsNullOrWhiteSpace(_editor.GetSelectedText());
        _feedback.Visible = !string.IsNullOrWhiteSpace(_feedback.Text);
    }

    internal static bool IsShowingSelection(RuntimeBridge bridge, string documentId, string selectedText)
    {
        if (!bridge.IsInsideTree() || bridge.SessionIdentity is not { } session) return false;
        return bridge.GetTree().GetNodesInGroup("source_excerpt_selection")
            .OfType<SourceExcerptSelection>().Any(selection => selection._bridge == bridge
                && ReferenceEquals(selection._session, session) && selection._documentId == documentId
                && selection._selecting && selection.IsVisibleInTree() && selection._editor.IsVisibleInTree()
                && string.Equals(selection._editor.GetSelectedText(), selectedText, StringComparison.Ordinal));
    }

    public void ApplyPresentation()
    {
        var font = _body.GetThemeFont("normal_font");
        var size = _body.GetThemeFontSize("normal_font_size");
        var ink = _body.GetThemeColor("default_color");
        _textScale = 1f;
        for (Node? ancestor = this; ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (!ancestor.HasMeta("accessibilityTextScale")) continue;
            _textScale = (float)ancestor.GetMeta("accessibilityTextScale").AsDouble();
            break;
        }
        _hintLogicalSize = Math.Max(14, size - 4);
        _editor.AddThemeFontOverride("font", font);
        _editor.AddThemeFontSizeOverride("font_size", size);
        _editor.AddThemeColorOverride("font_color", ink);
        _editor.AddThemeColorOverride("font_readonly_color", ink);
        _editor.AddThemeColorOverride("font_selected_color", ink);
        _editor.AddThemeColorOverride("selection_color", new Color(.72f, .55f, .25f, .45f));
        _editor.AddThemeColorOverride("caret_color", ink);
        _editor.AddThemeStyleboxOverride("focus", new StyleBoxFlat
        {
            DrawCenter = false, BorderColor = new Color(ink.R, ink.G, ink.B, .55f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1
        });
        foreach (var label in new[] { _hint, _feedback })
        {
            label.AddThemeFontOverride("font", font);
            label.AddThemeColorOverride("font_color", ink);
            label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
            label.AddThemeConstantOverride("shadow_offset_x", 0);
            label.AddThemeConstantOverride("shadow_offset_y", 0);
        }
        foreach (var button in new[] { _begin, _back, _record })
        {
            button.AddThemeFontOverride("font", font);
            button.AddThemeFontSizeOverride("font_size", Math.Max(16, size - 2));
            foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_disabled_color", "font_outline_color" })
                button.AddThemeColorOverride(color, _styleSource.GetThemeColor(color));
            button.AddThemeConstantOverride("outline_size", _styleSource.GetThemeConstant("outline_size"));
            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
                button.AddThemeStyleboxOverride(state, _styleSource.GetThemeStylebox(state));
        }
        RefreshScreenTypography(force: true);
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) RefreshScreenTypography();
    }

    private void RefreshScreenTypography(bool force = false)
    {
        if (_hint is null) return;
        // The root viewport stretch is missing from CanvasItem's popup-base
        // transform when embedded subwindows are enabled. Use the actual
        // viewport-to-window transform, also used by ordinary pointer input.
        // A headless display has no screen: its placeholder window is tiny, and
        // measuring against it asked for 420 px text in automated checks.
        var transform = DisplayServer.GetName() == "headless"
            ? GetGlobalTransformWithCanvas()
            : GetViewport().GetScreenTransform() * GetGlobalTransformWithCanvas();
        var scale = Math.Max(.01f, Math.Min(transform.X.Length(), transform.Y.Length()));
        if (!force && Math.Abs(scale - _screenScale) < .0001f) return;
        _screenScale = scale;
        var minimum = Mathf.CeilToInt(14f * _textScale / scale);
        foreach (var label in new[] { _hint, _feedback })
            label.AddThemeFontSizeOverride("font_size", Math.Max(_hintLogicalSize, minimum));
        if (_sourceFooter is not null)
            _sourceFooter.AddThemeFontSizeOverride("font_size",
                Math.Max(Mathf.RoundToInt(_footerBaseSize * _textScale), minimum));
    }

    private static Button AddButton(Node parent, string name, string text, Action action)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 40) };
        parent.AddChild(button);
        button.Pressed += action;
        return button;
    }
}
