using Godot;

namespace Urman.Godot;

/// <summary>
/// The Windows XP "Luna" look of Mansur's computer, drawn in code: glossy blue
/// bars, the green start button and a Tatar tulip ornament where XP had its
/// flag. Presentation only; high contrast swaps every gradient for flat dark.
/// </summary>
public static class OldPcXp
{
    public static readonly Color LunaTop = new("3b8cf5");
    public static readonly Color LunaMid = new("1f5fd8");
    public static readonly Color LunaBottom = new("1848b4");
    public static readonly Color LunaEdge = new("0a2e8c");
    public static readonly Color StartGreen = new("3c9a3c");
    public static readonly Color StartGreenTop = new("5fc05a");
    public static readonly Color Tray = new("1398ec");
    public static readonly Color Window = new("ece9d8");
    public static readonly Color MenuBlue = new("d3e5fa");
    // Tatar ornament colours: the red, green and gold of a kalfak and a tulip.
    public static readonly Color TulipRed = new("c8323c");
    public static readonly Color TulipGreen = new("2f8f4e");
    public static readonly Color TulipGold = new("e2b33c");

    /// <summary>Vertical gradient between two colours over a rectangle.</summary>
    public static void Gradient(CanvasItem item, Rect2 rect, Color top, Color bottom)
    {
        item.DrawPolygon(
            [rect.Position, rect.Position + new Vector2(rect.Size.X, 0), rect.End, rect.Position + new Vector2(0, rect.Size.Y)],
            [top, top, bottom, bottom]);
    }

    /// <summary>A Luna bar: light gloss on top, deep blue body, dark bottom edge.</summary>
    public static void LunaBar(CanvasItem item, Rect2 rect, bool active = true)
    {
        var top = active ? LunaTop : new Color("9bb5e8");
        var mid = active ? LunaMid : new Color("7b98d8");
        var bottom = active ? LunaBottom : new Color("6c87c6");
        var split = rect.Size.Y * .38f;
        Gradient(item, new Rect2(rect.Position, new Vector2(rect.Size.X, split)), top, mid);
        Gradient(item, new Rect2(rect.Position + new Vector2(0, split), new Vector2(rect.Size.X, rect.Size.Y - split)), mid, bottom);
        item.DrawLine(rect.Position, rect.Position + new Vector2(rect.Size.X, 0), new Color(1, 1, 1, active ? .45f : .3f), 1.5f);
    }

    /// <summary>
    /// A stylised Tatar tulip: three petals on a stem with two leaves, the
    /// motif of kalfak embroidery and house carvings.
    /// </summary>
    public static void Tulip(CanvasItem item, Vector2 centre, float size, Color petal, Color leaf, Color heart)
    {
        var s = size / 2f;
        Vector2 P(float x, float y) => centre + new Vector2(x * s, y * s);
        item.DrawColoredPolygon([P(0, .15f), P(-.18f, -.55f), P(0, -.95f), P(.18f, -.55f)], petal);
        item.DrawColoredPolygon([P(-.05f, .2f), P(-.62f, -.25f), P(-.55f, -.8f), P(-.2f, -.35f)], petal);
        item.DrawColoredPolygon([P(.05f, .2f), P(.62f, -.25f), P(.55f, -.8f), P(.2f, -.35f)], petal);
        item.DrawCircle(P(0, -.05f), s * .14f, heart);
        item.DrawLine(P(0, .15f), P(0, .95f), leaf, Mathf.Max(1.5f, s * .12f));
        item.DrawColoredPolygon([P(0, .7f), P(-.55f, .35f), P(-.35f, .75f)], leaf);
        item.DrawColoredPolygon([P(0, .55f), P(.55f, .2f), P(.35f, .6f)], leaf);
    }

    /// <summary>A band of small tulips, like the edge of an embroidered towel.</summary>
    public static void OrnamentBand(CanvasItem item, Rect2 rect, float alpha = 1f)
    {
        var step = rect.Size.Y * 1.3f;
        var index = 0;
        for (var x = rect.Position.X + step / 2; x < rect.End.X; x += step, index++)
        {
            var petal = (index % 2 == 0 ? TulipRed : TulipGold) with { A = alpha };
            Tulip(item, new Vector2(x, rect.Position.Y + rect.Size.Y / 2), rect.Size.Y * .9f, petal,
                TulipGreen with { A = alpha }, new Color(1, 1, 1, alpha * .8f));
        }
    }

    public static StyleBoxFlat Box(Color background, Color border, int thickness, int margin, int radius = 3)
    {
        return new StyleBoxFlat
        {
            BgColor = background, BorderColor = border,
            BorderWidthLeft = thickness, BorderWidthRight = thickness, BorderWidthTop = thickness, BorderWidthBottom = thickness,
            ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
        };
    }
}

/// <summary>A panel painted as a Luna bar (taskbar, start-menu header or footer).</summary>
public partial class OldPcXpBar : PanelContainer
{
    public enum BarKind { Taskbar, MenuHeader, MenuFooter }
    public BarKind Kind { get; set; } = BarKind.Taskbar;
    public bool HighContrast { get; set; }

    public override void _Ready()
    {
        Resized += QueueRedraw;
        var empty = new StyleBoxEmpty();
        empty.ContentMarginLeft = empty.ContentMarginRight = Kind == BarKind.Taskbar ? 0 : 12;
        empty.ContentMarginTop = empty.ContentMarginBottom = Kind == BarKind.Taskbar ? 3 : 8;
        if (Kind == BarKind.MenuHeader) empty.ContentMarginBottom = 22;
        AddThemeStyleboxOverride("panel", empty);
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        if (HighContrast)
        {
            DrawRect(rect, new Color("0b1220"));
            DrawRect(rect, Colors.White, false, 2f);
            return;
        }
        OldPcXp.LunaBar(this, rect);
        if (Kind == BarKind.MenuHeader)
            OldPcXp.OrnamentBand(this, new Rect2(0, Size.Y - 17, Size.X, 15), .95f);
        if (Kind == BarKind.MenuFooter)
            DrawLine(Vector2.Zero, new Vector2(Size.X, 0), new Color("f4a54a"), 2f);
    }
}
