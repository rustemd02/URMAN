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
        root.Scale = Vector2.One * (float)settings.TextScale;
        root.SetMeta("accessibilityTextScale", settings.TextScale);
        root.SetMeta("accessibilityHighContrast", settings.HighContrast);
        root.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);

        foreach (var node in root.FindChildren("*", "Control", recursive: true, owned: false))
        {
            if (node is Label label)
            {
                label.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("e5dbc7"));
                label.AddThemeColorOverride("font_shadow_color", Colors.Black);
                label.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
                label.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
            }
            else if (node is RichTextLabel richText)
            {
                richText.AddThemeColorOverride("default_color", settings.HighContrast ? Colors.White : new Color("e5dbc7"));
            }
            else if (node is Button button)
            {
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
}
