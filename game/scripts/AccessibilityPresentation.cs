using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// Presentation-only accessibility contract. Runtime state and narrative data
/// never depend on these settings; Godot UI owners consume the same snapshot
/// that SaveGameV3 persists.
/// </summary>
public interface IAccessibilitySettingsTarget
{
    void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings);
}

public static class AccessibilityPresentation
{
    public const string TargetGroup = "accessibility_target";

    public static void ApplyToTree(SceneTree tree, AccessibilitySettingsSnapshot settings)
    {
        foreach (var node in tree.GetNodesInGroup(TargetGroup))
        {
            if (node is IAccessibilitySettingsTarget target)
            {
                target.ApplyAccessibilitySettings(settings);
            }
        }
    }

    public static void ApplyToControl(Control root, AccessibilitySettingsSnapshot settings)
    {
        // Scale the text metrics instead of transforming the whole layout.
        // A transformed container grows from its pivot and can push fixed
        // panels, buttons and scrollbars outside a 720p viewport.
        root.Scale = Vector2.One;
        root.SetMeta("accessibilityTextScale", settings.TextScale);
        root.SetMeta("accessibilityHighContrast", settings.HighContrast);
        root.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);

        foreach (var node in root.FindChildren("*", "Control", recursive: true, owned: false))
        {
            if (node is Label label)
            {
                ApplyFontScale(label, "font_size", (float)settings.TextScale, 16);
                label.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("e5dbc7"));
                label.AddThemeColorOverride("font_shadow_color", Colors.Black);
                label.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
                label.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
            }
            else if (node is RichTextLabel richText)
            {
                ApplyFontScale(richText, "normal_font_size", (float)settings.TextScale, 18);
                richText.AddThemeColorOverride("default_color", settings.HighContrast ? Colors.White : new Color("e5dbc7"));
            }
            else if (node is ItemList itemList)
            {
                // ItemList draws focus over its rows; keep only the outline.
                if (itemList.GetThemeStylebox("focus") is StyleBoxFlat { DrawCenter: true } focus)
                {
                    var outline = (StyleBoxFlat)focus.Duplicate();
                    outline.DrawCenter = false;
                    itemList.AddThemeStyleboxOverride("focus", outline);
                }
                ApplyFontScale(itemList, "font_size", (float)settings.TextScale, 18);
                itemList.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("e5dbc7"));
                itemList.AddThemeColorOverride("font_selected_color", settings.HighContrast ? Colors.White : new Color("f0c46b"));
            }
            else if (node is LineEdit lineEdit)
            {
                ApplyFontScale(lineEdit, "font_size", (float)settings.TextScale, 18);
            }
            else if (node is BaseButton button)
            {
                ApplyFontScale(button, "font_size", (float)settings.TextScale, 18);
                var color = settings.HighContrast ? Colors.White : new Color("e5dbc7");
                button.AddThemeColorOverride("font_color", color);
                button.AddThemeColorOverride("font_hover_color", Colors.White);
                button.AddThemeColorOverride("font_pressed_color", Colors.White);
                button.AddThemeColorOverride("font_focus_color", Colors.White);
                button.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, 0.62f));
                button.AddThemeColorOverride("font_outline_color", Colors.Black);
                button.AddThemeConstantOverride("outline_size", settings.HighContrast ? 3 : 2);
            }
        }
    }

    private static void ApplyFontScale(Control control, string property, float scale, int fallback)
    {
        var metaName = $"accessibilityBase_{property}";
        if (!control.HasMeta(metaName))
        {
            var themeSize = control.GetThemeFontSize(property);
            control.SetMeta(metaName, themeSize > 0 ? themeSize : fallback);
        }

        control.AddThemeFontSizeOverride(property,
            Mathf.RoundToInt(control.GetMeta(metaName).AsInt32() * Mathf.Clamp(scale, 0.8f, 1.6f)));
    }

}
