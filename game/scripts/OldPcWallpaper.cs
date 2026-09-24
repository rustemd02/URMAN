using Godot;

namespace Urman.Godot;

/// <summary>
/// The desktop picture, in the manner of XP's rolling green hill under a blue
/// sky, but it is this village in summer: a log house with carved blue window
/// frames and a painted gate, birches, and the minaret beyond the hill.
/// Code-drawn; not a replacement for a world asset.
/// </summary>
public partial class OldPcWallpaper : Control
{
    public override void _Ready() => Resized += QueueRedraw;

    public override void _Draw()
    {
        var w = Size.X;
        var h = Size.Y;
        Vector2 P(float x, float y) => new(w * x, h * y);

        // Sky: deep XP blue fading to a pale horizon.
        OldPcXp.Gradient(this, new Rect2(0, 0, w, h * .62f), new Color("2a64c8"), new Color("a8d0f2"));
        // Clouds: a shaded underside first, then opaque tops, so overlapping
        // puffs read as one cloud instead of stacked discs.
        var clouds = new[] { (.18f, .16f, .055f), (.24f, .14f, .07f), (.31f, .17f, .05f),
            (.62f, .1f, .045f), (.68f, .085f, .06f), (.74f, .11f, .04f), (.86f, .24f, .035f), (.9f, .22f, .045f) };
        foreach (var (cx, cy, r) in clouds) DrawCircle(P(cx, cy + .012f), h * r, new Color("d2e1f3"));
        foreach (var (cx, cy, r) in clouds) DrawCircle(P(cx, cy), h * r * .93f, new Color("f7fbff"));

        // Far ridge with the minaret, then the big green hill.
        var far = new List<Vector2> { P(0, .6f) };
        for (var i = 0; i <= 24; i++) far.Add(P(i / 24f, .56f + .025f * Mathf.Sin(i * .9f)));
        far.Add(P(1, 1)); far.Add(P(0, 1));
        DrawColoredPolygon(far.ToArray(), new Color("6f9e6a"));
        Minaret(P(.8f, .565f), h * .2f);

        var hill = new List<Vector2>();
        for (var i = 0; i <= 40; i++)
        {
            var t = i / 40f;
            hill.Add(P(t, .66f - .1f * Mathf.Sin(t * Mathf.Pi * .95f + .15f) + .03f * Mathf.Sin(t * 9f)));
        }
        hill.Add(P(1, 1)); hill.Add(P(0, 1));
        var colors = hill.Select(point => new Color("62b534").Lerp(new Color("2f7a1f"), Mathf.Clamp((point.Y / h - .55f) / .45f, 0, 1))).ToArray();
        DrawPolygon(hill.ToArray(), colors);

        foreach (var x in new[] { .07f, .11f, .9f, .94f }) Birch(P(x, .63f), h * .25f);
        House(P(.2f, .6f), h * .17f);
        // A few tulips in the grass, as on the gate.
        for (var i = 0; i < 6; i++)
            OldPcXp.Tulip(this, P(.36f + i * .045f, .71f + .012f * Mathf.Sin(i * 2.1f)), h * .03f,
                i % 2 == 0 ? OldPcXp.TulipRed : OldPcXp.TulipGold, OldPcXp.TulipGreen, Colors.White);
    }

    private void Minaret(Vector2 ground, float height)
    {
        var half = height * .045f;
        DrawRect(new Rect2(ground.X - half, ground.Y - height * .72f, half * 2, height * .72f), new Color("eef0ea"));
        DrawRect(new Rect2(ground.X - half * 1.6f, ground.Y - height * .55f, half * 3.2f, height * .04f), new Color("d8dcd2"));
        DrawColoredPolygon([new(ground.X - half * 1.2f, ground.Y - height * .72f), new(ground.X, ground.Y - height),
            new(ground.X + half * 1.2f, ground.Y - height * .72f)], new Color("3e8a5a"));
        var moon = new Vector2(ground.X, ground.Y - height * 1.05f);
        DrawCircle(moon, half * .9f, new Color("e2b33c"));
        DrawCircle(moon + new Vector2(half * .35f, -half * .15f), half * .75f, new Color("5e92d8"));
    }

    private void Birch(Vector2 ground, float height)
    {
        var trunk = height * .03f;
        DrawRect(new Rect2(ground.X - trunk, ground.Y - height, trunk * 2, height), new Color("f2f1ea"));
        for (var i = 1; i < 7; i++)
            DrawRect(new Rect2(ground.X - trunk, ground.Y - height * i / 7f, trunk * (i % 2 == 0 ? 2f : 1.2f), trunk * .6f), new Color("2d2f2c"));
        foreach (var (dx, dy, r) in new[] { (0f, -.95f, .2f), (-.12f, -.8f, .16f), (.12f, -.78f, .17f), (0f, -.68f, .15f) })
            DrawCircle(ground + new Vector2(dx * height, dy * height), height * r, new Color(.36f, .62f, .24f, .92f));
    }

    private void House(Vector2 ground, float height)
    {
        var width = height * 1.3f;
        var left = ground.X - width / 2;
        var wall = new Rect2(left, ground.Y - height * .62f, width, height * .62f);
        DrawRect(wall, new Color("8b5a33"));
        for (var y = wall.Position.Y + height * .07f; y < wall.End.Y; y += height * .07f)
            DrawLine(new(left, y), new(left + width, y), new Color("6e4426"), 1.5f);
        // Roof painted green, as roofs in the village are.
        DrawColoredPolygon([new(left - width * .08f, wall.Position.Y), new(ground.X, ground.Y - height),
            new(left + width * 1.08f, wall.Position.Y)], new Color("3c8a4f"));
        // Windows with carved blue frames (nalichniki) and a white crest.
        foreach (var fx in new[] { .22f, .6f })
        {
            var win = new Rect2(left + width * fx, wall.Position.Y + height * .14f, width * .18f, height * .26f);
            DrawRect(win.Grow(height * .03f), new Color("2f63b8"));
            DrawRect(win, new Color("cfe4f4"));
            DrawLine(win.Position + new Vector2(win.Size.X / 2, 0), win.Position + new Vector2(win.Size.X / 2, win.Size.Y), new Color("f4f1e6"), 2);
            DrawColoredPolygon([win.Position + new Vector2(-height * .03f, -height * .03f),
                win.Position + new Vector2(win.Size.X / 2, -height * .11f),
                win.Position + new Vector2(win.Size.X + height * .03f, -height * .03f)], new Color("f4f1e6"));
        }
        // A painted gate with a sun, beside the house.
        var gate = new Rect2(left + width * 1.12f, ground.Y - height * .5f, width * .5f, height * .5f);
        DrawRect(gate, new Color("2f7f58"));
        DrawArc(gate.Position + new Vector2(gate.Size.X / 2, gate.Size.Y * .45f), gate.Size.X * .22f, 0, Mathf.Tau, 20, new Color("e2b33c"), 2.5f);
        for (var r = 0; r < 8; r++)
        {
            var a = r * Mathf.Tau / 8f;
            var c = gate.Position + new Vector2(gate.Size.X / 2, gate.Size.Y * .45f);
            DrawLine(c + Vector2.FromAngle(a) * gate.Size.X * .26f, c + Vector2.FromAngle(a) * gate.Size.X * .34f, new Color("e2b33c"), 2f);
        }
    }
}
