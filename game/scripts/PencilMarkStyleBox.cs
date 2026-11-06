using Godot;

namespace Urman.Godot;

/// <summary>
/// «Проявка» UI (author review 2026-11-06): a selection is marked the way a
/// photographer marks a contact sheet — a grease-pencil stroke under the word,
/// or a loose ring round it when pressed. No glowing dot, no filled plate.
/// The stroke wobbles a little, seeded by the box size, so two neighbouring
/// marks are never identical but one mark does not shimmer between frames.
/// Optionally draws a quiet panel/frame behind the content as well.
/// </summary>
public partial class PencilMarkStyleBox : StyleBox
{
    public enum MarkKind { None, Underline, Ring }

    [Export] public MarkKind Mark { get; set; } = MarkKind.Underline;
    [Export] public Color MarkColor { get; set; } = new("c8462c");
    [Export] public float MarkWidth { get; set; } = 2.6f;
    [Export] public Color Background { get; set; } = Colors.Transparent;
    [Export] public Color FrameColor { get; set; } = Colors.Transparent;
    [Export] public float CornerRadius { get; set; } = 3f;

    public static PencilMarkStyleBox Create(MarkKind mark, Color markColor, int marginX, int marginY,
        Color background = default, Color frame = default)
    {
        var box = new PencilMarkStyleBox
        {
            Mark = mark,
            MarkColor = markColor,
            Background = background,
            FrameColor = frame
        };
        box.ContentMarginLeft = box.ContentMarginRight = marginX;
        box.ContentMarginTop = box.ContentMarginBottom = marginY;
        return box;
    }

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        if (Background.A > 0f || FrameColor.A > 0f)
        {
            var panel = new StyleBoxFlat
            {
                BgColor = Background,
                BorderColor = FrameColor,
                DrawCenter = Background.A > 0f,
                AntiAliasing = true
            };
            panel.SetBorderWidthAll(FrameColor.A > 0f ? 1 : 0);
            panel.SetCornerRadiusAll(Mathf.RoundToInt(CornerRadius));
            panel.Draw(toCanvasItem, rect);
        }
        if (Mark == MarkKind.None || MarkColor.A <= 0f) return;

        var seed = (uint)(Mathf.RoundToInt(rect.Size.X) * 73856093 ^ Mathf.RoundToInt(rect.Size.Y) * 19349663);
        var rng = new RandomNumberGenerator { Seed = seed };
        var points = Mark == MarkKind.Underline ? Underline(rect, rng) : Ring(rect, rng);
        RenderingServer.CanvasItemAddPolyline(toCanvasItem, points, new[] { MarkColor }, MarkWidth, true);
        if (Mark == MarkKind.Underline)
        {
            // A second, lighter pass: a real pencil rarely lands once.
            var second = Underline(rect, rng, inset: .08f, drop: 3.2f);
            RenderingServer.CanvasItemAddPolyline(toCanvasItem, second,
                new[] { MarkColor with { A = MarkColor.A * .5f } }, MarkWidth * .55f, true);
        }
    }

    private Vector2[] Underline(Rect2 rect, RandomNumberGenerator rng, float inset = 0f, float drop = 0f)
    {
        var left = rect.Position.X + (float)ContentMarginLeft * .5f + rect.Size.X * inset;
        var right = rect.End.X - (float)ContentMarginRight * .5f - rect.Size.X * inset * .4f;
        var y = rect.End.Y - Mathf.Max(2f, (float)ContentMarginBottom * .35f) + drop;
        var tilt = rng.RandfRange(-2.2f, 1.2f);
        const int segments = 14;
        var points = new Vector2[segments + 1];
        for (var index = 0; index <= segments; index++)
        {
            var t = index / (float)segments;
            var x = Mathf.Lerp(left, right, t);
            // A slight belly and a lift at the end, like a quick hand stroke.
            var wobble = Mathf.Sin(t * Mathf.Pi) * -1.6f + Mathf.Sin(t * 9f + rng.Randf() * .6f) * .5f;
            points[index] = new Vector2(x, y + wobble + tilt * t - (t > .9f ? (t - .9f) * 18f : 0f));
        }
        return points;
    }

    private static Vector2[] Ring(Rect2 rect, RandomNumberGenerator rng)
    {
        var centre = rect.GetCenter();
        var rx = rect.Size.X * .5f + 8f;
        var ry = rect.Size.Y * .5f + 5f;
        const int segments = 40;
        var start = rng.RandfRange(2.6f, 3.4f);
        // A little more than a full turn, never closing exactly.
        var sweep = Mathf.Tau * 1.08f;
        var points = new Vector2[segments + 1];
        for (var index = 0; index <= segments; index++)
        {
            var t = index / (float)segments;
            var a = start + sweep * t;
            var grow = 1f + t * .07f + Mathf.Sin(t * 7f) * .02f;
            points[index] = centre + new Vector2(Mathf.Cos(a) * rx * grow, Mathf.Sin(a) * ry * grow);
        }
        return points;
    }
}
