using Godot;

namespace Urman.Godot;

/// <summary>Small authored desktop glyphs, drawn at the UI scale without font-symbol fallbacks.</summary>
public partial class OldPcDesktopIcon : Control
{
    public string Kind { get; set; } = "files";
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0, Size / 48f);
        var outline = new Color("334650");
        var cream = new Color("eee9d8");
        switch (Kind)
        {
            case "files":
                Box(4, 5, 36, 27, "bbbfb5", outline);
                Box(8, 9, 28, 18, "346489", outline);
                DrawLine(new(11, 24), new(32, 12), new Color("91bbc5"), 3);
                Box(18, 32, 10, 5, "9da498", outline);
                Box(8, 38, 32, 5, "d4d4c5", outline);
                break;
            case "archive":
                Box(5, 11, 37, 31, "c69b49", outline);
                Box(6, 7, 17, 8, "dfbc6e", outline);
                DrawColoredPolygon([new(7, 20), new(44, 16), new(39, 42), new(5, 42)], new Color("efd08a"));
                DrawPolyline([new(7, 20), new(44, 16), new(39, 42), new(5, 42), new(7, 20)], outline, 1.5f);
                break;
            case "browser":
                DrawCircle(new(24, 24), 19, new Color("377ca3"));
                DrawArc(new(24, 24), 19, 0, Mathf.Tau, 32, outline, 1.5f);
                DrawLine(new(5, 24), new(43, 24), cream, 1.5f);
                DrawArc(new(24, 24), 11, 0, Mathf.Tau, 28, cream, 1.2f);
                DrawLine(new(24, 5), new(24, 43), cream, 1.2f);
                DrawArc(new(24, 12), 25, .40f, 2.74f, 24, new Color("9dc7d2"), 1.4f);
                break;
            case "pictures":
                Box(3, 8, 42, 32, "e8e3d4", outline);
                Box(7, 12, 34, 24, "7cabbc", outline);
                DrawCircle(new(32, 18), 4, new Color("f4d58b"));
                DrawColoredPolygon([new(7, 35), new(18, 21), new(27, 32), new(32, 26), new(41, 35)], new Color("426958"));
                break;
            case "trash":
                DrawColoredPolygon([new(11, 13), new(38, 13), new(34, 43), new(15, 43)], new Color("b9cccb"));
                DrawPolyline([new(11, 13), new(38, 13), new(34, 43), new(15, 43), new(11, 13)], outline, 1.5f);
                Box(9, 9, 31, 5, "d7dfd6", outline);
                Box(19, 4, 11, 5, "bac8c5", outline);
                for (var x = 18; x <= 31; x += 6) DrawLine(new(x, 18), new(x, 38), new Color("6b8b91"), 1.5f);
                break;
            case "chat":
                Box(4, 8, 40, 26, "e8e3d4", outline);
                DrawColoredPolygon([new(12, 34), new(24, 34), new(14, 43)], new Color("e8e3d4"));
                DrawPolyline([new(12, 34), new(14, 43), new(24, 34)], outline, 1.5f);
                for (var y = 15; y <= 27; y += 6) DrawLine(new(11, y), new(37, y), new Color("91a2a0"), 1.6f);
                break;
            case "person":
                DrawCircle(new(24, 17), 10, new Color("92adb6"));
                DrawColoredPolygon([new(6, 44), new(9, 32), new(17, 27), new(31, 27), new(40, 32), new(43, 44)], new Color("648494"));
                break;
            default:
                Box(8, 4, 30, 39, Kind == "writer" ? "eee8d5" : "fff4cc", outline);
                Box(8, 4, 30, 6, "739faf", outline);
                for (var y = 16; y < 38; y += 6) DrawLine(new(13, y), new(31, y), new Color("91a2a0"), 1.4f);
                if (Kind == "writer")
                    DrawLine(new(33, 12), new(20, 37), new Color("b65b3b"), 4);
                break;
        }
    }
    private void Box(float x, float y, float width, float height, string fill, Color border)
    {
        var rect = new Rect2(x, y, width, height);
        DrawRect(rect, new Color(fill));
        DrawRect(rect, border, false, 1.5f);
    }
}

