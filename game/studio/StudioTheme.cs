using Godot;

namespace Urman.Studio.App;

/// <summary>
/// Studio's own calm dark theme (spec §4.1): contrasting text, large previews,
/// restrained accents. It is separate from the game's UrmanUiTheme so editing
/// the game's UI never restyles the editor. Scale 100–150 % (UX06).
/// </summary>
public static class StudioTheme
{
    public static readonly Color Background = new("15171b");
    public static readonly Color Panel = new("1c1f24");
    public static readonly Color Raised = new("23272e");
    public static readonly Color Line = new("323843");
    public static readonly Color Text = new("e8eaee");
    public static readonly Color Muted = new("9aa3b2");
    public static readonly Color Accent = new("7fb3d5");
    public static readonly Color Ok = new("8cc49a");
    public static readonly Color Warn = new("e3b760");
    public static readonly Color Bad = new("e58b7d");
    public static readonly Color Violet = new("b9a3dc");

    public static Theme Build(float scale)
    {
        var theme = new Theme();
        var regular = ResourceLoader.Load<FontFile>("res://assets/fonts/PT_Sans-Web-Regular.ttf");
        var bold = ResourceLoader.Load<FontFile>("res://assets/fonts/PT_Sans-Web-Bold.ttf");
        theme.DefaultFont = regular;
        theme.DefaultFontSize = Mathf.RoundToInt(15 * scale);

        foreach (var type in new[] { "Label", "Button", "LineEdit", "TextEdit", "OptionButton", "Tree", "ItemList", "CheckBox", "SpinBox", "MenuButton", "TabBar" })
        {
            theme.SetColor("font_color", type, Text);
        }

        theme.SetFont("font", "HeaderLabel", bold);
        theme.SetTypeVariation("HeaderLabel", "Label");
        theme.SetFontSize("font_size", "HeaderLabel", Mathf.RoundToInt(18 * scale));
        theme.SetTypeVariation("MutedLabel", "Label");
        theme.SetColor("font_color", "MutedLabel", Muted);
        theme.SetFontSize("font_size", "MutedLabel", Mathf.RoundToInt(13 * scale));

        theme.SetStylebox("normal", "Button", Box(Raised, Line, 7));
        theme.SetStylebox("hover", "Button", Box(Raised, Accent, 7));
        theme.SetStylebox("pressed", "Button", Box(new Color("2d4a60"), new Color("4d7896"), 7));
        theme.SetStylebox("disabled", "Button", Box(Panel, Line, 7));
        theme.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), new Color("a4c9e2"), 7, 2));
        theme.SetColor("font_disabled_color", "Button", new Color(Muted, .6f));
        theme.SetTypeVariation("PrimaryButton", "Button");
        theme.SetStylebox("normal", "PrimaryButton", Box(new Color("2d4a60"), new Color("4d7896"), 7));
        theme.SetTypeVariation("NavButton", "Button");
        theme.SetStylebox("normal", "NavButton", Box(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 7));
        theme.SetStylebox("hover", "NavButton", Box(Raised, new Color(0, 0, 0, 0), 7));
        theme.SetStylebox("pressed", "NavButton", Box(Raised, Line, 7));
        theme.SetColor("font_color", "NavButton", Muted);
        theme.SetColor("font_pressed_color", "NavButton", Text);
        theme.SetConstant("h_separation", "NavButton", 10);

        theme.SetStylebox("normal", "LineEdit", Box(new Color("121418"), new Color("414958"), 6));
        theme.SetStylebox("focus", "LineEdit", Box(new Color("121418"), Accent, 6, 2));
        theme.SetStylebox("panel", "PanelContainer", Box(Panel, Line, 0, 0));
        theme.SetTypeVariation("CardPanel", "PanelContainer");
        theme.SetStylebox("panel", "CardPanel", Box(Raised, Line, 9));
        theme.SetTypeVariation("BannerPanel", "PanelContainer");
        theme.SetStylebox("panel", "BannerPanel", Box(new Color("1b242d"), new Color("3e5568"), 8));
        theme.SetTypeVariation("ErrorBanner", "PanelContainer");
        theme.SetStylebox("panel", "ErrorBanner", Box(new Color("2b1f1e"), new Color("7a4a43"), 8));
        theme.SetStylebox("panel", "Tree", Box(new Color("121418"), Line, 6));
        theme.SetColor("font_selected_color", "Tree", Text);
        theme.SetStylebox("selected", "Tree", Box(new Color("2c3440"), Accent, 4));
        theme.SetStylebox("selected_focus", "Tree", Box(new Color("2c3440"), Accent, 4));
        theme.SetStylebox("panel", "ItemList", Box(new Color("121418"), Line, 6));
        theme.SetStylebox("panel", "PopupMenu", Box(Raised, new Color("414958"), 8));
        theme.SetStylebox("panel", "PopupPanel", Box(Raised, new Color("414958"), 8));
        theme.SetStylebox("panel", "GraphEdit", Box(new Color("121418"), Line, 0));
        theme.SetStylebox("panel", "GraphNode", Box(Raised, new Color("414958"), 9));
        theme.SetStylebox("panel_selected", "GraphNode", Box(Raised, Accent, 9, 2));
        theme.SetStylebox("titlebar", "GraphNode", Box(new Color("2a3038"), new Color("414958"), 9));
        theme.SetStylebox("titlebar_selected", "GraphNode", Box(new Color("2d4a60"), Accent, 9));
        return theme;
    }

    public static StyleBoxFlat Box(Color fill, Color border, int radius, int width = 1)
    {
        var box = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        box.SetBorderWidthAll(width);
        box.SetCornerRadiusAll(radius);
        return box;
    }
}
