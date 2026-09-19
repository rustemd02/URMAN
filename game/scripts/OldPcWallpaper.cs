using Godot;

namespace Urman.Godot;

/// <summary>A quiet, code-drawn winter wallpaper, not a replacement for a world asset.</summary>
public partial class OldPcWallpaper : Control
{
    public override void _Ready() => Resized += QueueRedraw;

    public override void _Draw()
    {
        var width = Size.X;
        var height = Size.Y;
        for (var band = 0; band < 32; band++)
        {
            var t = band / 31f;
            var color = new Color("52768b").Lerp(new Color("b3c8cb"), t);
            DrawRect(new Rect2(0, height * band / 32f, width, height / 32f + 1), color);
        }
        DrawCircle(new Vector2(width * .76f, height * .27f), height * .078f, new Color("d6ded4"));
        DrawColoredPolygon([
            new Vector2(0, height * .68f), new Vector2(width * .17f, height * .61f),
            new Vector2(width * .39f, height * .66f), new Vector2(width * .64f, height * .57f),
            new Vector2(width, height * .65f), new Vector2(width, height), new Vector2(0, height)
        ], new Color("d6deda"));
        for (var index = 0; index < 28; index++)
        {
            var x = width * (.27f + index / 38f);
            var baseY = height * (.69f + .025f * Mathf.Sin(index * 1.8f));
            var treeHeight = height * (.13f + .04f * Mathf.Sin(index * 2.7f));
            var halfWidth = treeHeight * .18f;
            DrawColoredPolygon([
                new Vector2(x, baseY - treeHeight), new Vector2(x - halfWidth, baseY),
                new Vector2(x + halfWidth, baseY)
            ], new Color("4d6970"));
            DrawLine(new Vector2(x, baseY - treeHeight * .7f), new Vector2(x, baseY + 8), new Color("4b5b61"), 2);
        }
        DrawColoredPolygon([
            new Vector2(width * .53f, height), new Vector2(width * .69f, height * .73f),
            new Vector2(width * .72f, height * .68f), new Vector2(width * .7f, height * .78f),
            new Vector2(width * .68f, height)
        ], new Color("bdcfcd"));
    }
}

