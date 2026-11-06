using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// ACT1-UI.1 shared interface theme. The only owner of UI colour, type, plate
/// shape and screen motion: screens take a <see cref="Theme"/> from here and
/// name a type variation instead of building their own style boxes.
///
/// Direction (decision_log 2026-09-13, 2026-09-20): Persona-like composition —
/// hard skewed plates, a cut-paper offset shadow, one strong accent, narrow bold
/// display type — carried in winter Kyrlai colours: ink, snow and the viburnum
/// red of Tatar embroidery. No foreign screens or assets are copied. The style
/// guide is docs/urman_knowledge_base/ui_style_guide.md.
/// </summary>
public static class UrmanUiTheme
{
    public const string FontDirectory = "res://assets/fonts";

    // Type variations. A screen sets Control.ThemeTypeVariation to one of these.
    public const string Title = "UrmanTitle";
    public const string Heading = "UrmanHeading";
    public const string Speaker = "UrmanSpeaker";
    public const string Hint = "UrmanHint";
    public const string StatusHypothesis = "UrmanStatusHypothesis";
    public const string StatusConfirmed = "UrmanStatusConfirmed";
    public const string MenuButton = "UrmanMenuButton";
    public const string ChoiceButton = "UrmanChoiceButton";
    public const string QuietButton = "UrmanQuietButton";
    public const string Plate = "UrmanPlate";
    public const string Sheet = "UrmanSheet";
    public const string Tag = "UrmanTag";
    public const string Subtitle = "UrmanSubtitle";
    public const string SubtitlePanel = "UrmanSubtitlePanel";

    /// <summary>Colour tokens. Every colour a screen shows comes from here.</summary>
    public readonly record struct Palette(
        Color Ink, Color InkRaised, Color InkSunken, Color Line,
        Color Text, Color TextMuted, Color TextOnAccent,
        Color Accent, Color AccentDeep, Color Focus,
        Color Hypothesis, Color Confirmed, Color Disabled, Color Shade);

    // «Проявка» (author review 2026-11-06): darkroom ink, photo paper, and one
    // marking colour — the grease pencil. No glowing accent dot.
    public static readonly Palette Normal = new(
        Ink: new Color("0c0c0ed6"),
        InkRaised: new Color("17171a"),
        InkSunken: new Color("08080af0"),
        Line: new Color("ece4d324"),
        Text: new Color("ece5d6"),
        TextMuted: new Color("a59e90"),
        TextOnAccent: new Color("f3ede1"),
        Accent: new Color("c8462c"),
        AccentDeep: new Color("7a2a1b"),
        Focus: new Color("f3ede1"),
        Hypothesis: new Color("d9a24a"),
        Confirmed: new Color("a9cdb5"),
        Disabled: new Color("ece5d64d"),
        Shade: new Color("07080ac2"));

    /// <summary>Photo paper and pencil ink for tags, the speaker label and tooltips.</summary>
    public static readonly Color Paper = new("e6dcc6");
    public static readonly Color PaperInk = new("3b322a");

    public static readonly Palette HighContrast = new(
        Ink: new Color("000000"),
        InkRaised: new Color("141414"),
        InkSunken: new Color("000000"),
        Line: new Color("ffffff"),
        Text: new Color("ffffff"),
        TextMuted: new Color("ffffff"),
        TextOnAccent: new Color("ffffff"),
        Accent: new Color("d4000b"),
        AccentDeep: new Color("5c0005"),
        Focus: new Color("ffd400"),
        Hypothesis: new Color("ffd400"),
        Confirmed: new Color("7dffb8"),
        Disabled: new Color("ffffff9e"),
        Shade: new Color("000000e0"));

    public static Palette Colours(bool highContrast) => highContrast ? HighContrast : Normal;

    public static Palette Colours(AccessibilitySettingsSnapshot settings) => Colours(settings.HighContrast);

    /// <summary>Base sizes before the player's text scale.</summary>
    public static class Size
    {
        public const int Body = 18;
        public const int Small = 15;
        public const int Button = 20;
        public const int Heading = 30;
        public const int Title = 64;
        public const int Speaker = 22;
        public const int Subtitle = 22;
        public const int WorldPrompt = 24;
    }

    /// <summary>Spacing grid: every margin and gap is a multiple of 4.</summary>
    public static class Space
    {
        public const int Xs = 4;
        public const int S = 8;
        public const int M = 16;
        public const int L = 24;
        public const int Xl = 40;
    }

    /// <summary>Horizontal skew of plates. Negative leans the top edge right.</summary>
    public const float PlateSkew = -0.16f;

    public const double MotionSeconds = 0.14;

    private static Theme? _normal;
    private static Theme? _highContrast;
    private static Theme? _notebook;
    private static FontFile? _body;
    private static FontFile? _bodyBold;
    private static FontFile? _italic;
    private static FontFile? _display;
    private static FontFile? _displayRegular;

    public static Font BodyFont => _body ??= LoadFont("PT_Sans-Web-Regular.ttf");
    public static Font BodyBoldFont => _bodyBold ??= LoadFont("PT_Sans-Web-Bold.ttf");
    public static Font ItalicFont => _italic ??= LoadFont("PT_Sans-Web-Italic.ttf");
    public static Font DisplayFont => _display ??= LoadFont("PT_Sans-Narrow-Web-Bold.ttf");
    public static Font DisplayRegularFont => _displayRegular ??= LoadFont("PT_Sans-Narrow-Web-Regular.ttf");

    private static Font? _title, _titleRegular, _hand, _handBold;

    /// <summary>Cormorant Garamond: menus, titles, headings (voices of the game).</summary>
    public static Font TitleFont => _title ??= Variable("CormorantGaramond-Variable.ttf", 600, DisplayFont);
    public static Font TitleRegularFont => _titleRegular ??= Variable("CormorantGaramond-Variable.ttf", 500, DisplayRegularFont);
    /// <summary>Caveat: what Aidar writes himself — notes, captions, the speaker's paper label.</summary>
    public static Font HandFont => _hand ??= Variable("Caveat-Variable.ttf", 600, ItalicFont);
    public static Font HandBoldFont => _handBold ??= Variable("Caveat-Variable.ttf", 700, ItalicFont);

    // Variable OFL fonts; until the editor import has run they fall back to PT Sans.
    private static Font Variable(string file, int weight, Font fallback)
    {
        var path = $"{FontDirectory}/{file}";
        if (!ResourceLoader.Exists(path) || ResourceLoader.Load<FontFile>(path) is not { } font) return fallback;
        var tag = TextServerManager.GetPrimaryInterface().NameToTag("wght");
        var variation = new FontVariation { BaseFont = font };
        variation.VariationOpentype = new global::Godot.Collections.Dictionary { { tag, weight } };
        variation.Fallbacks = new global::Godot.Collections.Array<Font> { fallback };
        return variation;
    }

    public static Theme For(AccessibilitySettingsSnapshot settings) => For(settings.HighContrast);

    public static bool IsShared(Theme theme) =>
        ReferenceEquals(theme, _normal) || ReferenceEquals(theme, _highContrast);

    public static Theme For(bool highContrast) => highContrast
        ? _highContrast ??= Build(HighContrast, highContrast: true)
        : _normal ??= Build(Normal, highContrast: false);

    /// <summary>
    /// World documents are an old Soviet squared exercise book in a brown
    /// cover, not the game's ink interface: dark brown ink on paper, buttons
    /// as cover-board tabs. Deliberately not <see cref="IsShared"/>, so the
    /// accessibility pass recolours the document node by node.
    /// </summary>
    public static readonly Color NotebookInk = new("34291c");
    public static readonly Color NotebookCover = new("4a3121");
    public static readonly Color NotebookPaper = new("e7dcc1");

    public static Theme Notebook => _notebook ??= BuildNotebook();

    private static Theme BuildNotebook()
    {
        var theme = new Theme { DefaultFont = BodyFont, DefaultFontSize = Size.Body };
        theme.SetColor("font_color", "Label", NotebookInk);
        theme.SetColor("font_shadow_color", "Label", Colors.Transparent);
        theme.SetColor("default_color", "RichTextLabel", NotebookInk);
        theme.SetFont("normal_font", "RichTextLabel", BodyFont);
        theme.SetFont("bold_font", "RichTextLabel", BodyBoldFont);
        theme.SetFont("italics_font", "RichTextLabel", ItalicFont);

        theme.SetFont("font", "Button", BodyBoldFont);
        theme.SetFontSize("font_size", "Button", Size.Body);
        theme.SetColor("font_color", "Button", NotebookPaper);
        foreach (var state in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            theme.SetColor(state, "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", NotebookInk with { A = .55f });
        theme.SetStylebox("normal", "Button", PlateBox(NotebookCover, marginX: Space.M, marginY: Space.S));
        theme.SetStylebox("hover", "Button", PlateBox(NotebookCover.Darkened(.25f), NotebookInk, 2, marginX: Space.M, marginY: Space.S));
        theme.SetStylebox("pressed", "Button", PlateBox(NotebookCover.Darkened(.45f), NotebookInk, 2, marginX: Space.M, marginY: Space.S));
        theme.SetStylebox("hover_pressed", "Button", PlateBox(NotebookCover.Darkened(.45f), NotebookInk, 2, marginX: Space.M, marginY: Space.S));
        var focus = Outline(NotebookInk, 3);
        focus.ExpandMarginLeft = focus.ExpandMarginRight = focus.ExpandMarginTop = focus.ExpandMarginBottom = 3;
        theme.SetStylebox("focus", "Button", focus);
        theme.SetStylebox("disabled", "Button", PlateBox(NotebookCover with { A = .28f }, marginX: Space.M, marginY: Space.S));

        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, PlateBox(NotebookInk with { A = .12f }, marginX: 2, marginY: 2));
            theme.SetStylebox("grabber", type, PlateBox(NotebookCover with { A = .7f }, marginX: 2, marginY: 2));
            theme.SetStylebox("grabber_highlight", type, PlateBox(NotebookCover, marginX: 2, marginY: 2));
            theme.SetStylebox("grabber_pressed", type, PlateBox(NotebookInk, marginX: 2, marginY: 2));
        }
        foreach (var type in new[] { "LineEdit", "TextEdit" })
        {
            theme.SetStylebox("normal", type, new StyleBoxEmpty());
            theme.SetStylebox("read_only", type, new StyleBoxEmpty());
            theme.SetColor("font_color", type, NotebookInk);
            theme.SetColor("font_readonly_color", type, NotebookInk);
            theme.SetColor("selection_color", type, new Color(.72f, .55f, .25f, .45f));
        }
        return theme;
    }

    private static FontFile LoadFont(string file) =>
        ResourceLoader.Load<FontFile>($"{FontDirectory}/{file}")
        ?? throw new InvalidOperationException($"UI font is missing: {FontDirectory}/{file}");

    /// <summary>A hard, optionally skewed plate with a cut-paper offset shadow.</summary>
    public static StyleBoxFlat PlateBox(Color background, Color border = default, int borderWidth = 0,
        float skew = 0f, Color shadow = default, int shadowOffset = 0, int marginX = Space.M, int marginY = Space.S)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            Skew = new Vector2(skew, 0),
            ShadowColor = shadow,
            ShadowSize = shadowOffset > 0 ? 1 : 0,
            ShadowOffset = new Vector2(shadowOffset, shadowOffset),
            ContentMarginLeft = marginX,
            ContentMarginRight = marginX,
            ContentMarginTop = marginY,
            ContentMarginBottom = marginY,
            AntiAliasing = true
        };
        box.SetBorderWidthAll(borderWidth);
        // A skewed plate needs its slanted corners outside the text column.
        if (skew != 0f)
        {
            box.ExpandMarginLeft = box.ExpandMarginRight = Space.S;
        }
        return box;
    }

    private static Theme Build(Palette c, bool highContrast)
    {
        var theme = new Theme
        {
            DefaultFont = BodyFont,
            DefaultFontSize = Size.Body
        };
        var focusWidth = highContrast ? 3 : 2;

        // Plain text.
        theme.SetColor("font_color", "Label", c.Text);
        theme.SetColor("font_shadow_color", "Label", new Color(0, 0, 0, highContrast ? 1f : .55f));
        theme.SetConstant("shadow_offset_x", "Label", highContrast ? 2 : 1);
        theme.SetConstant("shadow_offset_y", "Label", highContrast ? 2 : 1);
        theme.SetConstant("line_spacing", "Label", 3);
        theme.SetColor("default_color", "RichTextLabel", c.Text);
        theme.SetFont("normal_font", "RichTextLabel", BodyFont);
        theme.SetFont("bold_font", "RichTextLabel", BodyBoldFont);
        theme.SetFont("italics_font", "RichTextLabel", ItalicFont);
        theme.SetConstant("line_separation", "RichTextLabel", 4);

        // Label variations. «Проявка»: titles in Cormorant, notes and labels in
        // Aidar's hand on photo paper; high contrast keeps the plain plates.
        Variation(theme, Title, "Label");
        theme.SetFont("font", Title, highContrast ? DisplayFont : TitleFont);
        theme.SetFontSize("font_size", Title, highContrast ? Size.Title : Size.Title + 12);
        theme.SetColor("font_color", Title, c.Text);
        theme.SetColor("font_outline_color", Title, c.AccentDeep);
        theme.SetConstant("outline_size", Title, 0);

        Variation(theme, Heading, "Label");
        theme.SetFont("font", Heading, highContrast ? DisplayFont : TitleFont);
        theme.SetFontSize("font_size", Heading, highContrast ? Size.Heading : Size.Heading + 4);
        theme.SetColor("font_color", Heading, c.Text);

        Variation(theme, Speaker, "Label");
        if (highContrast)
        {
            theme.SetFont("font", Speaker, DisplayFont);
            theme.SetFontSize("font_size", Speaker, Size.Speaker);
            theme.SetColor("font_color", Speaker, c.TextOnAccent);
            theme.SetStylebox("normal", Speaker, PlateBox(c.Accent, marginX: Space.M, marginY: Space.Xs));
        }
        else
        {
            // The speaker's name on a strip of photo paper, written by hand.
            theme.SetFont("font", Speaker, HandBoldFont);
            theme.SetFontSize("font_size", Speaker, Size.Speaker + 6);
            theme.SetColor("font_color", Speaker, PaperInk);
            theme.SetColor("font_shadow_color", Speaker, Colors.Transparent);
            theme.SetStylebox("normal", Speaker, PaperBox(marginX: Space.M + 2, marginY: 0));
        }

        Variation(theme, Hint, "Label");
        theme.SetFontSize("font_size", Hint, Size.Small);
        theme.SetColor("font_color", Hint, c.TextMuted);

        Variation(theme, Tag, "Label");
        theme.SetFont("font", Tag, highContrast ? DisplayFont : HandBoldFont);
        theme.SetFontSize("font_size", Tag, highContrast ? Size.Small : Size.Small + 5);
        theme.SetColor("font_color", Tag, highContrast ? c.Ink : PaperInk);
        theme.SetColor("font_shadow_color", Tag, Colors.Transparent);
        theme.SetStylebox("normal", Tag, highContrast ? PlateBox(c.Text, marginX: Space.S, marginY: 1) : PaperBox(Space.S, 0));

        // Hypotheses and confirmations are pencil notes, told apart by ink colour.
        Variation(theme, StatusHypothesis, "Label");
        theme.SetFont("font", StatusHypothesis, highContrast ? DisplayFont : HandBoldFont);
        theme.SetFontSize("font_size", StatusHypothesis, highContrast ? Size.Small : Size.Small + 6);
        theme.SetColor("font_color", StatusHypothesis, highContrast ? c.Ink : c.Hypothesis);
        theme.SetStylebox("normal", StatusHypothesis, highContrast
            ? PlateBox(c.Hypothesis, marginX: Space.S, marginY: 1) : new StyleBoxEmpty());

        Variation(theme, StatusConfirmed, "Label");
        theme.SetFont("font", StatusConfirmed, highContrast ? DisplayFont : HandBoldFont);
        theme.SetFontSize("font_size", StatusConfirmed, highContrast ? Size.Small : Size.Small + 6);
        theme.SetColor("font_color", StatusConfirmed, highContrast ? c.Ink : c.Confirmed);
        theme.SetStylebox("normal", StatusConfirmed, highContrast
            ? PlateBox(c.Confirmed, marginX: Space.S, marginY: 1) : new StyleBoxEmpty());

        // Subtitles: calm ink band over the world, no skew and no accent, so a
        // caption never reads as a menu or a choice.
        Variation(theme, Subtitle, "Label");
        theme.SetFontSize("font_size", Subtitle, Size.Subtitle);
        theme.SetColor("font_color", Subtitle, c.Text);
        theme.SetColor("font_shadow_color", Subtitle, Colors.Black);
        theme.SetConstant("shadow_offset_x", Subtitle, 2);
        theme.SetConstant("shadow_offset_y", Subtitle, 2);
        Variation(theme, SubtitlePanel, "PanelContainer");
        theme.SetStylebox("panel", SubtitlePanel, PlateBox(c.Shade, highContrast ? c.Line : default,
            highContrast ? 2 : 0, marginX: Space.L, marginY: Space.M));

        // Panels: one quiet darkroom panel everywhere — translucent ink, a hairline
        // edge, no skew, no accent spine, no cut-paper shadow.
        theme.SetStylebox("panel", "PanelContainer", PanelBox(c, highContrast, Space.L, Space.L));
        theme.SetStylebox("panel", "Panel", PanelBox(c, highContrast, Space.M, Space.S));
        Variation(theme, Sheet, "PanelContainer");
        theme.SetStylebox("panel", Sheet, highContrast ? SheetBox(c, highContrast) : PanelBox(c, false, Space.Xl, Space.L));
        Variation(theme, Plate, "PanelContainer");
        theme.SetStylebox("panel", Plate, PanelBox(c, highContrast, Space.Xl, Space.L));

        // Buttons. High contrast keeps solid plates; the normal palette draws words
        // that gain a pencil underline on hover/focus and a ring when pressed.
        if (highContrast)
        {
            ButtonStyles(theme, "Button", c, true, skew: 0f, font: BodyBoldFont, size: Size.Button);
            Variation(theme, MenuButton, "Button");
            ButtonStyles(theme, MenuButton, c, true, skew: 0f, font: DisplayFont, size: 26);
            Variation(theme, ChoiceButton, "Button");
            ButtonStyles(theme, ChoiceButton, c, true, skew: 0f, font: BodyFont, size: Size.Button);
            Variation(theme, QuietButton, "Button");
            ButtonStyles(theme, QuietButton, c, true, skew: 0f, font: BodyFont, size: Size.Small);
        }
        else
        {
            PencilButtonStyles(theme, "Button", c, BodyFont, Size.Button, framed: true);
            Variation(theme, MenuButton, "Button");
            PencilButtonStyles(theme, MenuButton, c, TitleRegularFont, 32, framed: false);
            Variation(theme, ChoiceButton, "Button");
            PencilButtonStyles(theme, ChoiceButton, c, BodyFont, Size.Button, framed: false);
            Variation(theme, QuietButton, "Button");
            PencilButtonStyles(theme, QuietButton, c, BodyFont, Size.Small, framed: false);
        }
        theme.SetConstant("h_separation", ChoiceButton, Space.S);

        foreach (var type in new[] { "OptionButton", "CheckBox", "CheckButton", "MenuButton" })
        {
            if (highContrast) ButtonStyles(theme, type, c, true, skew: 0f, font: BodyFont, size: Size.Body);
            else PencilButtonStyles(theme, type, c, BodyFont, Size.Body, framed: type == "OptionButton");
        }

        // Lists, tabs and editors.
        theme.SetStylebox("panel", "ItemList", PlateBox(c.InkSunken, c.Line, 1, marginX: Space.S, marginY: Space.S));
        theme.SetStylebox("focus", "ItemList", Outline(c.Focus, focusWidth));
        theme.SetStylebox("selected", "ItemList", highContrast ? PlateBox(c.AccentDeep, marginX: Space.S, marginY: Space.Xs)
            : PencilMarkStyleBox.Create(PencilMarkStyleBox.MarkKind.Underline, c.Accent with { A = .6f }, Space.S, Space.Xs, new Color(1, 1, 1, .04f)));
        theme.SetStylebox("selected_focus", "ItemList", highContrast ? PlateBox(c.Accent, marginX: Space.S, marginY: Space.Xs)
            : PencilMarkStyleBox.Create(PencilMarkStyleBox.MarkKind.Underline, c.Accent, Space.S, Space.Xs, new Color(1, 1, 1, .06f)));
        theme.SetStylebox("cursor", "ItemList", Outline(c.Focus, 1));
        theme.SetStylebox("cursor_unfocused", "ItemList", Outline(c.Line, 1));
        theme.SetColor("font_color", "ItemList", c.TextMuted);
        theme.SetColor("font_hovered_color", "ItemList", c.Text);
        theme.SetColor("font_selected_color", "ItemList", c.TextOnAccent);
        theme.SetConstant("line_separation", "ItemList", Space.S);
        theme.SetConstant("v_separation", "ItemList", Space.Xs);

        theme.SetFont("font", "TabBar", highContrast ? DisplayFont : TitleFont);
        theme.SetFontSize("font_size", "TabBar", highContrast ? Size.Body : Size.Body + 6);
        theme.SetColor("font_selected_color", "TabBar", c.TextOnAccent);
        theme.SetColor("font_unselected_color", "TabBar", c.TextMuted);
        theme.SetColor("font_hovered_color", "TabBar", c.Text);
        if (highContrast)
        {
            theme.SetStylebox("tab_selected", "TabBar", PlateBox(c.Accent, marginX: Space.M, marginY: Space.Xs));
            theme.SetStylebox("tab_hovered", "TabBar", PlateBox(c.InkRaised, marginX: Space.M, marginY: Space.Xs));
        }
        else
        {
            theme.SetStylebox("tab_selected", "TabBar", PencilMarkStyleBox.Create(PencilMarkStyleBox.MarkKind.Underline, c.Accent, Space.M, Space.Xs + 2));
            theme.SetStylebox("tab_hovered", "TabBar", PencilMarkStyleBox.Create(PencilMarkStyleBox.MarkKind.None, c.Accent, Space.M, Space.Xs + 2));
        }
        theme.SetStylebox("tab_unselected", "TabBar", PlateBox(Colors.Transparent, marginX: Space.M, marginY: Space.Xs + 2));
        theme.SetStylebox("tab_focus", "TabBar", Outline(c.Focus, focusWidth));
        theme.SetConstant("h_separation", "TabBar", Space.S);

        foreach (var type in new[] { "LineEdit", "TextEdit" })
        {
            theme.SetStylebox("normal", type, PlateBox(c.InkSunken, c.Line, 1, marginX: Space.S, marginY: Space.S));
            theme.SetStylebox("focus", type, Outline(c.Focus, focusWidth));
            theme.SetStylebox("read_only", type, PlateBox(c.InkSunken, c.Line, 1, marginX: Space.S, marginY: Space.S));
            theme.SetColor("font_color", type, c.Text);
            theme.SetColor("font_placeholder_color", type, c.TextMuted);
            theme.SetColor("caret_color", type, c.Focus);
            theme.SetColor("selection_color", type, c.AccentDeep);
        }

        theme.SetStylebox("panel", "PopupMenu", PlateBox(c.Ink, c.Line, 1, marginX: Space.S, marginY: Space.S));
        theme.SetStylebox("hover", "PopupMenu", highContrast ? PlateBox(c.Accent, marginX: Space.S, marginY: Space.Xs)
            : PencilMarkStyleBox.Create(PencilMarkStyleBox.MarkKind.Underline, c.Accent, Space.S, Space.Xs, new Color(1, 1, 1, .05f)));
        theme.SetColor("font_color", "PopupMenu", c.Text);
        theme.SetColor("font_hover_color", "PopupMenu", c.TextOnAccent);

        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, PlateBox(c.InkSunken, marginX: 3, marginY: 3));
            theme.SetStylebox("grabber", type, PlateBox(c.Line, marginX: 3, marginY: 3));
            theme.SetStylebox("grabber_highlight", type, PlateBox(c.Text, marginX: 3, marginY: 3));
            theme.SetStylebox("grabber_pressed", type, PlateBox(c.Accent, marginX: 3, marginY: 3));
        }

        // A thin pencil line with a paper-coloured filled part (the author liked these).
        var track = PlateBox(highContrast ? c.InkSunken : new Color("3a3631"), highContrast ? c.Line : default,
            highContrast ? 1 : 0, marginX: 0, marginY: highContrast ? 3 : 1);
        track.SetCornerRadiusAll(2);
        theme.SetStylebox("slider", "HSlider", track);
        var filled = PlateBox(highContrast ? c.Accent : c.Text, marginX: 0, marginY: highContrast ? 3 : 1);
        filled.SetCornerRadiusAll(2);
        theme.SetStylebox("grabber_area", "HSlider", filled);
        theme.SetStylebox("grabber_area_highlight", "HSlider", filled);
        theme.SetStylebox("focus", "HSlider", highContrast ? Outline(c.Focus, focusWidth)
            : PencilMarkStyleBox.Create(PencilMarkStyleBox.MarkKind.Underline, c.Accent, 0, Space.S));

        // Tooltips are a scrap of photo paper.
        theme.SetColor("font_color", "TooltipLabel", highContrast ? c.Text : PaperInk);
        theme.SetStylebox("panel", "TooltipPanel", highContrast
            ? PlateBox(c.Ink, c.Line, 1, marginX: Space.S, marginY: Space.Xs)
            : PaperBox(Space.M, Space.S));
        return theme;
    }

    /// <summary>The single panel of the interface: translucent ink, hairline edge, soft corners.</summary>
    private static StyleBoxFlat PanelBox(Palette c, bool highContrast, int marginX, int marginY)
    {
        if (highContrast) return PlateBox(c.Ink, c.Line, 2, marginX: marginX, marginY: marginY);
        var box = PlateBox(c.Ink, c.Line, 1, marginX: marginX, marginY: marginY);
        box.SetCornerRadiusAll(3);
        box.ShadowColor = new Color(0, 0, 0, .45f);
        box.ShadowSize = 24;
        return box;
    }

    /// <summary>A strip of photo paper: speaker names, tags, tooltips.</summary>
    public static StyleBoxFlat PaperBox(int marginX, int marginY)
    {
        var box = PlateBox(Paper, marginX: marginX, marginY: marginY);
        box.ShadowColor = new Color(0, 0, 0, .35f);
        box.ShadowSize = 6;
        box.ShadowOffset = new Vector2(0, 3);
        return box;
    }

    private static void PencilButtonStyles(Theme theme, string type, Palette c, Font font, int size, bool framed)
    {
        var marginX = framed ? Space.M + 2 : Space.S;
        var marginY = framed ? Space.S : Space.Xs + 2;
        var frame = framed ? c.Line : Colors.Transparent;
        var fill = framed ? new Color(0, 0, 0, .18f) : Colors.Transparent;
        theme.SetFont("font", type, font);
        theme.SetFontSize("font_size", type, size);
        theme.SetColor("font_color", type, framed ? c.Text : c.TextMuted);
        foreach (var state in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            theme.SetColor(state, type, c.TextOnAccent);
        theme.SetColor("font_disabled_color", type, c.Disabled);
        theme.SetConstant("outline_size", type, 0);
        StyleBox Box(PencilMarkStyleBox.MarkKind mark) =>
            PencilMarkStyleBox.Create(mark, c.Accent, marginX, marginY, fill, frame);
        theme.SetStylebox("normal", type, Box(PencilMarkStyleBox.MarkKind.None));
        theme.SetStylebox("hover", type, Box(PencilMarkStyleBox.MarkKind.Underline));
        theme.SetStylebox("focus", type, PencilMarkStyleBox.Create(PencilMarkStyleBox.MarkKind.Underline, c.Accent, marginX, marginY));
        theme.SetStylebox("pressed", type, Box(PencilMarkStyleBox.MarkKind.Ring));
        theme.SetStylebox("hover_pressed", type, Box(PencilMarkStyleBox.MarkKind.Ring));
        theme.SetStylebox("disabled", type, PencilMarkStyleBox.Create(PencilMarkStyleBox.MarkKind.None, c.Accent, marginX, marginY,
            Colors.Transparent, framed ? c.Line with { A = c.Line.A * .5f } : Colors.Transparent));
    }

    /// <summary>
    /// HUD text drawn straight over the 3D world (interaction prompt, Aidar's
    /// remarks). It has no plate, so it keeps a black outline in both palettes;
    /// colour and type still come from the theme tokens.
    /// </summary>
    public static void ApplyWorldText(Label label, AccessibilitySettingsSnapshot settings, int baseSize)
    {
        var c = Colours(settings);
        label.AddThemeFontOverride("font", BodyBoldFont);
        label.AddThemeFontSizeOverride("font_size",
            Mathf.RoundToInt(baseSize * Mathf.Clamp((float)settings.TextScale, 0.8f, 1.6f)));
        label.AddThemeColorOverride("font_color", c.Text);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", settings.HighContrast ? 5 : 4);
        label.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
        label.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
    }

    /// <summary>The reticle is a HUD mark, drawn in the reading colour.</summary>
    public static Color ReticleColour(AccessibilitySettingsSnapshot settings) =>
        settings.HighContrast ? HighContrast.Text : Normal.Text with { A = .96f };

    private static StyleBoxFlat SheetBox(Palette c, bool highContrast)
    {
        var box = PlateBox(c.Ink, c.Line, highContrast ? 2 : 1, marginX: Space.Xl, marginY: Space.L);
        // The accent spine on the left edge marks a reading sheet.
        box.BorderColor = highContrast ? c.Line : c.Accent;
        box.BorderWidthLeft = 6;
        return box;
    }

    private static void ButtonStyles(Theme theme, string type, Palette c, bool highContrast, float skew, Font font, int size)
    {
        var focusWidth = highContrast ? 3 : 2;
        var marginX = skew != 0f ? Space.L : Space.M;
        theme.SetFont("font", type, font);
        theme.SetFontSize("font_size", type, size);
        theme.SetColor("font_color", type, c.Text);
        theme.SetColor("font_hover_color", type, c.TextOnAccent);
        theme.SetColor("font_pressed_color", type, c.TextOnAccent);
        theme.SetColor("font_focus_color", type, c.TextOnAccent);
        theme.SetColor("font_hover_pressed_color", type, c.TextOnAccent);
        theme.SetColor("font_disabled_color", type, c.Disabled);
        theme.SetColor("font_outline_color", type, Colors.Black);
        theme.SetConstant("outline_size", type, highContrast ? 3 : 0);
        theme.SetStylebox("normal", type, PlateBox(c.InkRaised, highContrast ? c.Line : default,
            highContrast ? 1 : 0, skew, marginX: marginX));
        // Hover/focus: the plate turns accent red and jumps right on a black
        // cut-paper shadow — the one strong motion cue of the interface.
        var active = PlateBox(c.Accent, c.Focus, focusWidth, skew, c.Ink, 5, marginX: marginX);
        active.ContentMarginLeft = marginX + Space.S;
        active.ContentMarginRight = Math.Max(Space.S, marginX - Space.S);
        theme.SetStylebox("hover", type, active);
        theme.SetStylebox("focus", type, Outline(c.Focus, focusWidth, skew));
        theme.SetStylebox("pressed", type, PlateBox(c.AccentDeep, c.Focus, focusWidth, skew, marginX: marginX));
        theme.SetStylebox("hover_pressed", type, PlateBox(c.AccentDeep, c.Focus, focusWidth, skew, marginX: marginX));
        theme.SetStylebox("disabled", type, PlateBox(c.InkSunken, c.Line, 1, skew, marginX: marginX));
    }

    private static StyleBoxFlat Outline(Color colour, int width, float skew = 0f)
    {
        var box = PlateBox(Colors.Transparent, colour, width, skew);
        box.DrawCenter = false;
        return box;
    }

    private static void Variation(Theme theme, string variation, string baseType) =>
        theme.SetTypeVariation(variation, baseType);

    /// <summary>
    /// A focused button is drawn with the accent "active" plate as well, so
    /// keyboard and gamepad focus read exactly like mouse hover.
    /// </summary>
    public static void BindFocusAsHover(Button button)
    {
        if (button.HasMeta("urmanFocusBound")) return;
        button.SetMeta("urmanFocusBound", true);
        button.FocusEntered += () => ApplyFocusedPlate(button, true);
        button.FocusExited += () => ApplyFocusedPlate(button, false);
    }

    private static void ApplyFocusedPlate(Button button, bool focused)
    {
        if (!GodotObject.IsInstanceValid(button)) return;
        if (focused) button.AddThemeStyleboxOverride("normal", button.GetThemeStylebox("hover"));
        else button.RemoveThemeStyleboxOverride("normal");
    }

    /// <summary>
    /// Screen entry: a short fade with a slight 98→100% grow. Position is never
    /// animated, so a screen that refits its layout while opening still ends
    /// exactly where its layout puts it. Reduced motion keeps only the fade.
    /// </summary>
    public static void PlayOpen(Control panel, bool reducedMotion)
    {
        if (!panel.IsInsideTree()) return;
        if (panel.HasMeta("urmanOpenTween") && panel.GetMeta("urmanOpenTween").AsGodotObject() is Tween previous
            && GodotObject.IsInstanceValid(previous))
        {
            previous.Kill();
        }
        panel.Modulate = new Color(1, 1, 1, 0);
        panel.Scale = Vector2.One;
        var tween = panel.CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(panel, "modulate:a", 1f, MotionSeconds);
        if (!reducedMotion)
        {
            panel.Scale = new Vector2(.98f, .98f);
            tween.TweenProperty(panel, "scale", Vector2.One, MotionSeconds);
        }
        panel.SetMeta("urmanOpenTween", tween);
    }
}
