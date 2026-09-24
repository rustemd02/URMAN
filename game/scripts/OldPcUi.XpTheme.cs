using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// Windows XP chrome over the generic control styling: the green start
/// button with its tulip, blue task buttons, the tray clock, XP caption
/// buttons and the two-column start menu. High contrast keeps flat colours.
/// </summary>
public partial class OldPcUi
{
    private void ApplyXpChrome(AccessibilitySettingsSnapshot settings)
    {
        var hc = settings.HighContrast;
        var scale = (float)settings.TextScale;
        if (_taskbar is OldPcXpBar bar) { bar.HighContrast = hc; bar.QueueRedraw(); }

        var start = _taskbar.GetNode<Button>("Layout/Start");
        var startFace = OldPcXp.Box(hc ? new Color("123820") : OldPcXp.StartGreen, hc ? Colors.White : new Color("2d7a2d"), 1, 6, 0);
        startFace.CornerRadiusTopRight = startFace.CornerRadiusBottomRight = 14;
        startFace.ContentMarginLeft = 44;
        startFace.ContentMarginRight = 18;
        startFace.ShadowColor = new Color(0, 0, 0, .35f);
        startFace.ShadowSize = 2;
        var startHover = (StyleBoxFlat)startFace.Duplicate();
        startHover.BgColor = hc ? new Color("1c5230") : OldPcXp.StartGreenTop;
        var startDown = (StyleBoxFlat)startFace.Duplicate();
        startDown.BgColor = hc ? new Color("0c2a18") : new Color("2c7a2c");
        start.AddThemeStyleboxOverride("normal", startFace);
        start.AddThemeStyleboxOverride("hover", startHover);
        start.AddThemeStyleboxOverride("pressed", startDown);
        start.AddThemeFontOverride("font", new FontVariation
        {
            BaseFont = ThemeDB.FallbackFont, VariationEmbolden = .9f,
            VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(-.22f, 1), Vector2.Zero)
        });
        start.AddThemeFontSizeOverride("font_size", (int)(27 * scale));
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            start.AddThemeColorOverride(color, Colors.White);
        start.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .45f));
        start.AddThemeConstantOverride("shadow_offset_x", 1);
        start.AddThemeConstantOverride("shadow_offset_y", 1);

        // Task buttons: blue, the foreground program pressed in darker blue.
        foreach (var task in _tasks.GetChildren().OfType<Button>())
        {
            var face = OldPcXp.Box(hc ? new Color("1b2a44") : new Color("3c81f3"), hc ? Colors.White : new Color("6fa3f7"), 1, 6);
            var hover = (StyleBoxFlat)face.Duplicate();
            hover.BgColor = hc ? new Color("254461") : new Color("53a0ff");
            var active = OldPcXp.Box(hc ? new Color("37465e") : new Color("1a47a8"), hc ? Colors.White : new Color("9cc2ff"), 1, 6);
            active.ShadowColor = new Color(0, 0, 0, .35f);
            active.ShadowSize = 2;
            // XP task buttons keep a fixed width and cut long titles.
            task.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            task.CustomMinimumSize = new Vector2(230 * Math.Max(1f, scale), 36);
            task.ClipText = true;
            task.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            task.Alignment = HorizontalAlignment.Left;
            task.AddThemeStyleboxOverride("normal", face);
            task.AddThemeStyleboxOverride("hover", hover);
            task.AddThemeStyleboxOverride("pressed", active);
            task.AddThemeStyleboxOverride("hover_pressed", active);
            foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                task.AddThemeColorOverride(color, Colors.White);
        }

        var tray = OldPcXp.Box(hc ? new Color("0b1220") : OldPcXp.Tray, hc ? Colors.White : new Color("0b64b9"), 1, 8, 0);
        tray.BorderWidthRight = tray.BorderWidthTop = tray.BorderWidthBottom = 0;
        _clock.AddThemeStyleboxOverride("normal", tray);
        _clock.AddThemeColorOverride("font_color", Colors.White);

        // Caption buttons: blue squares, the close button red, all white-edged.
        foreach (var window in _windows.Values)
        {
            if (window.Header is OldPcWindowTitleBar caption) caption.HighContrast = hc;
            foreach (var (name, color) in new[] { ("Minimize", "2f6cf0"), ("Maximize", "2f6cf0"), ("CloseWindow", "e0472a") })
            {
                if (window.Header.GetNodeOrNull<Button>(name) is not { } button) continue;
                var face = OldPcXp.Box(hc ? new Color("222a38") : new Color(color), Colors.White, 1, 4);
                var hover = (StyleBoxFlat)face.Duplicate();
                hover.BgColor = hc ? new Color("37465e") : new Color(name == "CloseWindow" ? "f06a48" : "5a8ef7");
                button.AddThemeStyleboxOverride("normal", face);
                button.AddThemeStyleboxOverride("hover", hover);
                button.AddThemeStyleboxOverride("pressed", face);
                button.CustomMinimumSize = new Vector2(38, 34) * Math.Max(1f, scale);
                foreach (var font in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                    button.AddThemeColorOverride(font, Colors.White);
            }
        }

        var menuFrame = OldPcXp.Box(hc ? new Color("0b1220") : Colors.White, hc ? Colors.White : OldPcXp.LunaEdge, 2, 0);
        menuFrame.CornerRadiusTopLeft = menuFrame.CornerRadiusTopRight = 8;
        _startMenu.AddThemeStyleboxOverride("panel", menuFrame);
        foreach (var name in new[] { "Header", "Footer" })
            if (_startMenu.FindChild(name, true, false) is OldPcXpBar menuBar) { menuBar.HighContrast = hc; menuBar.QueueRedraw(); }
        if (_startMenu.FindChild("Programs", true, false) is PanelContainer programs)
            programs.AddThemeStyleboxOverride("panel", OldPcXp.Box(hc ? new Color("0b1220") : Colors.White, Colors.Transparent, 0, 8, 0));
        if (_startMenu.FindChild("Places", true, false) is PanelContainer places)
        {
            var blue = OldPcXp.Box(hc ? new Color("111a2b") : OldPcXp.MenuBlue, hc ? Colors.White : new Color("95bdee"), 0, 8, 0);
            blue.BorderWidthLeft = 1;
            places.AddThemeStyleboxOverride("panel", blue);
        }
        foreach (var label in new[] { "UserName", "Greeting" })
            if (_startMenu.FindChild(label, true, false) is Label text)
            {
                text.AddThemeColorOverride("font_color", Colors.White);
                text.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .5f));
                text.AddThemeConstantOverride("shadow_offset_x", 1);
                text.AddThemeConstantOverride("shadow_offset_y", 1);
                text.AddThemeFontSizeOverride("font_size", (int)((label == "UserName" ? 28 : 18) * scale));
            }
        // Menu entries: flat, and XP's blue selection under the pointer.
        var ink = hc ? Colors.White : new Color("1a1a1a");
        foreach (var entry in _startMenu.FindChildren("Launch_*", nameof(Button), true, false).OfType<Button>())
        {
            entry.Alignment = HorizontalAlignment.Left;
            var clear = OldPcXp.Box(Colors.Transparent, Colors.Transparent, 0, 7, 2);
            var hover = OldPcXp.Box(hc ? new Color("254461") : new Color("316ac5"), Colors.Transparent, 0, 7, 2);
            entry.AddThemeStyleboxOverride("normal", clear);
            entry.AddThemeStyleboxOverride("hover", hover);
            entry.AddThemeStyleboxOverride("pressed", hover);
            entry.AddThemeStyleboxOverride("focus", hover);
            entry.AddThemeColorOverride("font_color", ink);
            entry.AddThemeColorOverride("font_hover_color", Colors.White);
            entry.AddThemeColorOverride("font_pressed_color", Colors.White);
            entry.AddThemeColorOverride("font_focus_color", Colors.White);
        }
        _startMenu.CustomMinimumSize = new Vector2(600 * Math.Max(1f, scale), 0);
        if (_startMenu.FindChild("Query", true, false) is LineEdit query)
            query.CustomMinimumSize = new Vector2(180 * Math.Max(1f, scale), 0);
        if (_startMenu.FindChild("Leave", true, false) is Button leave)
        {
            var clear = OldPcXp.Box(Colors.Transparent, Colors.Transparent, 0, 7, 3);
            var hover = OldPcXp.Box(new Color(1, 1, 1, .18f), new Color(1, 1, 1, .5f), 1, 7, 3);
            leave.AddThemeStyleboxOverride("normal", clear);
            leave.AddThemeStyleboxOverride("hover", hover);
            leave.AddThemeStyleboxOverride("pressed", hover);
            foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                leave.AddThemeColorOverride(color, Colors.White);
        }
    }
}
