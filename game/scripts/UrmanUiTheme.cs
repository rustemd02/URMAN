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

    public static readonly Palette Normal = new(
        Ink: new Color("0e1413f5"),
        InkRaised: new Color("1a2421"),
        InkSunken: new Color("080c0bf8"),
        Line: new Color("5d6a63"),
        Text: new Color("eee7d8"),
        TextMuted: new Color("aaa596"),
        TextOnAccent: new Color("fff8ec"),
        Accent: new Color("c42b32"),
        AccentDeep: new Color("6e161b"),
        Focus: new Color("f2e9d6"),
        Hypothesis: new Color("e2a93f"),
        Confirmed: new Color("9fd0b4"),
        Disabled: new Color("eee7d866"),
        Shade: new Color("050807c8"));

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

        // Label variations.
        Variation(theme, Title, "Label");
        theme.SetFont("font", Title, DisplayFont);
        theme.SetFontSize("font_size", Title, Size.Title);
        theme.SetColor("font_color", Title, c.Text);
        theme.SetColor("font_outline_color", Title, c.AccentDeep);
        theme.SetConstant("outline_size", Title, highContrast ? 0 : 6);

        Variation(theme, Heading, "Label");
        theme.SetFont("font", Heading, DisplayFont);
        theme.SetFontSize("font_size", Heading, Size.Heading);
        theme.SetColor("font_color", Heading, c.Text);

        Variation(theme, Speaker, "Label");
        theme.SetFont("font", Speaker, DisplayFont);
        theme.SetFontSize("font_size", Speaker, Size.Speaker);
        theme.SetColor("font_color", Speaker, c.TextOnAccent);
        theme.SetStylebox("normal", Speaker, PlateBox(c.Accent, skew: PlateSkew,
            shadow: c.Ink, shadowOffset: 4, marginX: Space.M, marginY: Space.Xs));

        Variation(theme, Hint, "Label");
        theme.SetFontSize("font_size", Hint, Size.Small);
        theme.SetColor("font_color", Hint, c.TextMuted);

        Variation(theme, Tag, "Label");
        theme.SetFont("font", Tag, DisplayFont);
        theme.SetFontSize("font_size", Tag, Size.Small);
        theme.SetColor("font_color", Tag, c.Ink);
        theme.SetStylebox("normal", Tag, PlateBox(c.Text, skew: PlateSkew, marginX: Space.S, marginY: 1));

        Variation(theme, StatusHypothesis, "Label");
        theme.SetFont("font", StatusHypothesis, DisplayFont);
        theme.SetFontSize("font_size", StatusHypothesis, Size.Small);
        theme.SetColor("font_color", StatusHypothesis, c.Ink);
        theme.SetStylebox("normal", StatusHypothesis, PlateBox(c.Hypothesis, skew: PlateSkew, marginX: Space.S, marginY: 1));

        Variation(theme, StatusConfirmed, "Label");
        theme.SetFont("font", StatusConfirmed, DisplayFont);
        theme.SetFontSize("font_size", StatusConfirmed, Size.Small);
        theme.SetColor("font_color", StatusConfirmed, c.Ink);
        theme.SetStylebox("normal", StatusConfirmed, PlateBox(c.Confirmed, skew: PlateSkew, marginX: Space.S, marginY: 1));

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

        // Panels: a sheet is a full reading surface; a plate is a smaller card.
        theme.SetStylebox("panel", "PanelContainer", PlateBox(c.Ink, c.Line, 1, marginX: Space.L, marginY: Space.L));
        theme.SetStylebox("panel", "Panel", PlateBox(c.Ink, c.Line, 1));
        Variation(theme, Sheet, "PanelContainer");
        theme.SetStylebox("panel", Sheet, SheetBox(c, highContrast));
        Variation(theme, Plate, "PanelContainer");
        theme.SetStylebox("panel", Plate, PlateBox(c.Ink, c.Line, highContrast ? 2 : 0, skew: PlateSkew,
            shadow: c.Accent, shadowOffset: 8, marginX: Space.Xl, marginY: Space.L));

        // Buttons.
        ButtonStyles(theme, "Button", c, highContrast, skew: 0f, font: BodyBoldFont, size: Size.Button);
        Variation(theme, MenuButton, "Button");
        ButtonStyles(theme, MenuButton, c, highContrast, skew: PlateSkew, font: DisplayFont, size: 26);
        Variation(theme, ChoiceButton, "Button");
        ButtonStyles(theme, ChoiceButton, c, highContrast, skew: 0f, font: BodyFont, size: Size.Button);
        theme.SetConstant("h_separation", ChoiceButton, Space.S);
        Variation(theme, QuietButton, "Button");
        ButtonStyles(theme, QuietButton, c, highContrast, skew: 0f, font: BodyFont, size: Size.Small);
        theme.SetStylebox("normal", QuietButton, PlateBox(Colors.Transparent, c.Line, 1, marginX: Space.S, marginY: Space.Xs));

        foreach (var type in new[] { "OptionButton", "CheckBox", "CheckButton", "MenuButton" })
        {
            ButtonStyles(theme, type, c, highContrast, skew: 0f, font: BodyFont, size: Size.Body);
        }

        // Lists, tabs and editors.
        theme.SetStylebox("panel", "ItemList", PlateBox(c.InkSunken, c.Line, 1, marginX: Space.S, marginY: Space.S));
        theme.SetStylebox("focus", "ItemList", Outline(c.Focus, focusWidth));
        theme.SetStylebox("selected", "ItemList", PlateBox(c.AccentDeep, marginX: Space.S, marginY: Space.Xs));
        theme.SetStylebox("selected_focus", "ItemList", PlateBox(c.Accent, marginX: Space.S, marginY: Space.Xs));
        theme.SetStylebox("cursor", "ItemList", Outline(c.Focus, 1));
        theme.SetStylebox("cursor_unfocused", "ItemList", Outline(c.Line, 1));
        theme.SetColor("font_color", "ItemList", c.TextMuted);
        theme.SetColor("font_hovered_color", "ItemList", c.Text);
        theme.SetColor("font_selected_color", "ItemList", c.TextOnAccent);
        theme.SetConstant("line_separation", "ItemList", Space.S);
        theme.SetConstant("v_separation", "ItemList", Space.Xs);

        theme.SetFont("font", "TabBar", DisplayFont);
        theme.SetFontSize("font_size", "TabBar", Size.Body);
        theme.SetColor("font_selected_color", "TabBar", c.TextOnAccent);
        theme.SetColor("font_unselected_color", "TabBar", c.TextMuted);
        theme.SetColor("font_hovered_color", "TabBar", c.Text);
        theme.SetStylebox("tab_selected", "TabBar", PlateBox(c.Accent, skew: PlateSkew, marginX: Space.M, marginY: Space.Xs));
        theme.SetStylebox("tab_unselected", "TabBar", PlateBox(Colors.Transparent, marginX: Space.M, marginY: Space.Xs));
        theme.SetStylebox("tab_hovered", "TabBar", PlateBox(c.InkRaised, skew: PlateSkew, marginX: Space.M, marginY: Space.Xs));
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
        theme.SetStylebox("hover", "PopupMenu", PlateBox(c.Accent, marginX: Space.S, marginY: Space.Xs));
        theme.SetColor("font_color", "PopupMenu", c.Text);
        theme.SetColor("font_hover_color", "PopupMenu", c.TextOnAccent);

        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, PlateBox(c.InkSunken, marginX: 3, marginY: 3));
            theme.SetStylebox("grabber", type, PlateBox(c.Line, marginX: 3, marginY: 3));
            theme.SetStylebox("grabber_highlight", type, PlateBox(c.Text, marginX: 3, marginY: 3));
            theme.SetStylebox("grabber_pressed", type, PlateBox(c.Accent, marginX: 3, marginY: 3));
        }

        theme.SetStylebox("slider", "HSlider", PlateBox(c.InkSunken, c.Line, 1, marginX: 2, marginY: 3));
        theme.SetStylebox("grabber_area", "HSlider", PlateBox(c.Accent, marginX: 2, marginY: 3));
        theme.SetStylebox("grabber_area_highlight", "HSlider", PlateBox(c.Accent, marginX: 2, marginY: 3));
        theme.SetStylebox("focus", "HSlider", Outline(c.Focus, focusWidth));

        theme.SetColor("font_color", "TooltipLabel", c.Text);
        theme.SetStylebox("panel", "TooltipPanel", PlateBox(c.Ink, c.Line, 1, marginX: Space.S, marginY: Space.Xs));
        return theme;
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
