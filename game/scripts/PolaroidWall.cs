using Godot;

namespace Urman.Godot;

/// <summary>
/// «Проявка» (author review 2026-11-06): small polaroids from the game's own
/// photo book are laid over the main menu art and the loading screen. A new
/// print develops every few seconds while the oldest slides away; now and then
/// an outdoor print shows, for a moment while it develops, a figure standing
/// far back in the frame that is gone once the image has settled.
/// Presentation only: it reads textures and writes no state.
/// </summary>
public partial class PolaroidWall : Control
{
    private sealed record Photo(string Path, string Caption, bool Outdoors);

    private static readonly Photo[] Photos =
    {
        new("res://assets/textures/photobook/p-forest-front-v2.png", "у леса", true),
        new("res://assets/textures/photobook/p-water-front-v1.png", "у старых мостков", true),
        new("res://assets/textures/photobook/p-house-front-v1.png", "во дворе", true),
        new("res://assets/textures/photobook/s-garden-front-v1.png", "в саду", true),
        new("res://assets/textures/photobook/s-now-front-v1.png", "дома", false),
        new("res://assets/textures/photobook/s-school-front-v2.png", "копия из альбома", false),
    };

    private const string DevelopShader = """
        shader_type canvas_item;
        uniform float develop : hint_range(0.0, 1.0) = 0.0;
        void fragment() {
            vec3 t = texture(TEXTURE, UV).rgb;
            float g = dot(t, vec3(0.3, 0.59, 0.11));
            vec3 paper = vec3(0.93, 0.9, 0.83);
            float d = smoothstep(0.0, 1.0, develop);
            vec3 latent = mix(paper, vec3(g), 0.12);
            vec3 mid = mix(paper, mix(vec3(g), t, 0.35), 0.6);
            vec3 c = d < 0.5 ? mix(latent, mid, d * 2.0) : mix(mid, t * vec3(1.0, 0.97, 0.92), (d - 0.5) * 2.0);
            COLOR = vec4(c, 1.0);
        }
        """;

    /// <summary>Slot centres in parent-relative units (0..1) and their tilt in degrees.</summary>
    public Vector3[] Slots { get; set; } = { new(.62f, .26f, -7f), new(.8f, .36f, 5f), new(.67f, .64f, -2f) };
    public float PrintWidth { get; set; } = 250f;
    public double Interval { get; set; } = 5.0;
    public int MaxVisible { get; set; } = 3;
    /// <summary>Every Nth outdoor print carries the figure (0 = never).</summary>
    public int FigureEvery { get; set; } = 3;
    public bool ReducedMotion { get; set; }

    private readonly List<Control> _live = new();
    private readonly RandomNumberGenerator _rng = new();
    private Shader? _shader;
    private double _elapsed;
    private int _next;
    private int _spawned;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _shader = new Shader { Code = DevelopShader };
        _rng.Randomize();
        _next = _rng.RandiRange(0, Photos.Length - 1);
    }

    private bool _started;

    public override void _Process(double delta)
    {
        // Slots are relative to the laid-out size, so the first prints wait for layout.
        if (!_started)
        {
            if (Size.X < 1f) return;
            _started = true;
            for (var index = 0; index < MaxVisible; index++) Spawn(stagger: index * .35);
            return;
        }
        _elapsed += delta;
        if (_elapsed < Interval) return;
        _elapsed = 0;
        Spawn(stagger: 0);
    }

    private void Spawn(double stagger)
    {
        var photo = NextPhoto();
        if (photo is null) return;
        if (_live.Count >= MaxVisible) Retire(_live[0]);
        var slot = Slots[_spawned % Slots.Length];
        _spawned++;
        var print = BuildPrint(photo, out var image, out var figure);
        AddChild(print);
        _live.Add(print);
        var size = print.CustomMinimumSize;
        var centre = new Vector2(slot.X * Size.X, slot.Y * Size.Y);
        print.Position = centre - size * .5f + new Vector2(0, ReducedMotion ? 0 : -26);
        print.PivotOffset = size * .5f;
        print.RotationDegrees = slot.Z + _rng.RandfRange(-2f, 2f);
        print.Modulate = Colors.White with { A = 0 };

        var showFigure = figure is not null && FigureEvery > 0 && photo.Outdoors && _spawned % FigureEvery == 0 && !ReducedMotion;
        var arrive = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        arrive.TweenProperty(print, "modulate:a", 1f, .9).SetDelay(stagger);
        if (!ReducedMotion) arrive.TweenProperty(print, "position:y", centre.Y - size.Y * .5f, 1.2).SetDelay(stagger);
        var material = (ShaderMaterial)image.Material;
        if (ReducedMotion) material.SetShaderParameter("develop", 1f);
        else
            CreateTween().TweenMethod(Callable.From<float>(value =>
            {
                if (GodotObject.IsInstanceValid(material)) material.SetShaderParameter("develop", value);
            }), 0f, 1f, 4.2).SetDelay(stagger + .2);
        if (showFigure && figure is not null)
        {
            // It is there while the image is half-formed, and not when it is sharp.
            var seen = CreateTween();
            seen.TweenInterval(stagger + 1.4);
            seen.TweenProperty(figure, "modulate:a", .85f, .5);
            seen.TweenInterval(.9);
            seen.TweenProperty(figure, "modulate:a", 0f, .9);
        }
    }

    private Photo? NextPhoto()
    {
        for (var attempt = 0; attempt < Photos.Length; attempt++)
        {
            var photo = Photos[_next % Photos.Length];
            _next++;
            if (ResourceLoader.Exists(photo.Path)) return photo;
        }
        return null;
    }

    private void Retire(Control print)
    {
        _live.Remove(print);
        if (!GodotObject.IsInstanceValid(print)) return;
        var leave = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        leave.TweenProperty(print, "modulate:a", 0f, .9);
        if (!ReducedMotion)
        {
            leave.TweenProperty(print, "position:y", print.Position.Y + 34f, 1.0);
            leave.TweenProperty(print, "rotation_degrees", print.RotationDegrees + 9f, 1.0);
        }
        leave.Chain().TweenCallback(Callable.From(print.QueueFree));
    }

    private Control BuildPrint(Photo photo, out TextureRect image, out Control? figure)
    {
        var pad = PrintWidth * .048f;
        var imageSide = PrintWidth - pad * 2f;
        var height = pad + imageSide + PrintWidth * .19f;
        var print = new Control { Name = "Polaroid", CustomMinimumSize = new Vector2(PrintWidth, height), Size = new Vector2(PrintWidth, height), MouseFilter = MouseFilterEnum.Ignore };

        var paper = new Panel { MouseFilter = MouseFilterEnum.Ignore, Size = print.Size };
        var paperBox = new StyleBoxFlat { BgColor = new Color("efe8da"), ShadowColor = new Color(0, 0, 0, .55f), ShadowSize = 18, ShadowOffset = new Vector2(0, 10) };
        paper.AddThemeStyleboxOverride("panel", paperBox);
        print.AddChild(paper);

        image = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(photo.Path),
            Position = new Vector2(pad, pad),
            Size = new Vector2(imageSide, imageSide),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = _shader }
        };
        ((ShaderMaterial)image.Material).SetShaderParameter("develop", 0f);
        print.AddChild(image);

        figure = null;
        if (photo.Outdoors)
        {
            var standing = new PolaroidFigureMark { MouseFilter = MouseFilterEnum.Ignore, Modulate = Colors.White with { A = 0 } };
            var h = imageSide * .13f;
            standing.Size = new Vector2(h * .32f, h);
            standing.Position = new Vector2(pad + imageSide * _rng.RandfRange(.56f, .84f), pad + imageSide * .6f - h);
            print.AddChild(standing);
            figure = standing;
        }

        var tape = new ColorRect
        {
            Color = new Color(.89f, .82f, .63f, .82f),
            Size = new Vector2(PrintWidth * .36f, PrintWidth * .1f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        tape.Position = new Vector2((PrintWidth - tape.Size.X) * .5f, -tape.Size.Y * .45f);
        tape.PivotOffset = tape.Size * .5f;
        tape.RotationDegrees = _rng.RandfRange(-5f, 3f);
        print.AddChild(tape);

        var caption = new Label
        {
            Text = photo.Caption,
            Position = new Vector2(pad * 1.2f, pad + imageSide + PrintWidth * .02f),
            Size = new Vector2(imageSide, PrintWidth * .15f),
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        caption.AddThemeFontOverride("font", UrmanUiTheme.HandBoldFont);
        caption.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(PrintWidth * .1f));
        caption.AddThemeColorOverride("font_color", new Color("4b4137"));
        caption.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        print.AddChild(caption);
        return print;
    }
}

/// <summary>A small dark standing figure: head, narrow shoulders, long legs. Never detailed.</summary>
internal sealed partial class PolaroidFigureMark : Control
{
    public override void _Draw()
    {
        var w = Size.X;
        var h = Size.Y;
        var ink = new Color(.04f, .045f, .055f);
        DrawCircle(new Vector2(w * .5f, h * .08f), w * .2f, ink);
        DrawColoredPolygon(new[]
        {
            new Vector2(w * .3f, h * .15f), new Vector2(w * .7f, h * .15f), new Vector2(w * .8f, h * .55f),
            new Vector2(w * .62f, h * .56f), new Vector2(w * .6f, h), new Vector2(w * .52f, h),
            new Vector2(w * .5f, h * .62f), new Vector2(w * .47f, h), new Vector2(w * .39f, h),
            new Vector2(w * .38f, h * .56f), new Vector2(w * .2f, h * .55f)
        }, ink);
    }
}
