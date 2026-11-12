using Godot;

namespace Urman.Godot;

// Book-space grip, in metres. The two poses share the album's existing clip
// contract, but never translate a whole arm when the left cover opens.
internal static class PhotoBookHands
{
    public static Node3D Create()
    {
        var root = new Node3D { Name = "BookHands_Root" };
        root.SetMeta("assetSource", "procedural-book-grip");
        // Winter, an unheated bus and a cold car: he has not taken his gloves off.
        // Dark knitted wool reads as a hand at this distance far better than bare skin did.
        var skin = Material("3d4347", 1f);
        if (W01Controller.PrologueTextureSlot("aidar_glove_knit_albedo", null) is { } knitTexture)
        {
            skin.AlbedoColor = Colors.White;
            skin.AlbedoTexture = knitTexture;
            skin.Uv1Triplanar = true;
            skin.Uv1Scale = Vector3.One * 9f;
            if (W01Controller.PrologueTextureSlot("aidar_glove_knit_normal", null) is { } knitNormal)
            {
                skin.NormalEnabled = true;
                skin.NormalTexture = knitNormal;
            }
        }
        var coat = Material("353b37", .98f);
        var knit = Material("6d6258", 1f);
        var nails = Material("3d4347", 1f);
        foreach (var open in new[] { false, true })
        {
            var pose = new Node3D { Name = open ? "OpenGrip" : "ClosedGrip", Visible = !open };
            root.AddChild(pose);
            BuildHand(pose, -1, open, skin, coat, knit, nails);
            BuildHand(pose, 1, open, skin, coat, knit, nails);
        }

        var library = new AnimationLibrary();
        foreach (var clip in new[] { "hold_closed", "hold_open", "return" })
        {
            var animation = new Animation { Length = .1f };
            foreach (var open in new[] { false, true })
            {
                var track = animation.AddTrack(Animation.TrackType.Value);
                animation.TrackSetPath(track, new NodePath($"{(open ? "OpenGrip" : "ClosedGrip")}:visible"));
                animation.ValueTrackSetUpdateMode(track, Animation.UpdateMode.Discrete);
                animation.TrackInsertKey(track, 0, open == (clip == "hold_open"));
            }
            library.AddAnimation(clip, animation);
        }
        var player = new AnimationPlayer { Name = "AnimationPlayer", RootNode = new NodePath("..") };
        player.AddAnimationLibrary("", library);
        root.AddChild(player);
        return root;
    }

    private static StandardMaterial3D Material(string color, float roughness) => new()
    {
        AlbedoColor = new Color(color), Roughness = roughness, MetallicSpecular = .22f
    };

    private static void BuildHand(Node3D parent, int side, bool open,
        Material skin, Material coat, Material knit, Material nails)
    {
        // family-book-v1: underside Y=-.019, closed top +.019, left opened
        // board top -.014625. The right page stack ends at +.013725.
        var edge = side < 0 ? (open ? -.335f : -.110f) : .110f;
        var top = open ? (side < 0 ? -.014625f : .013725f) : .019f;
        var suffix = side < 0 ? "L" : "R";
        Vector3 P(float outward, float y, float z) => new(edge + side * outward, y, z);
        using var hand = new SurfaceTool();
        hand.Begin(Mesh.PrimitiveType.Triangles);
        // Flattened palm and tapered wrist, rather than a scaled glove block.
        Sweep(hand, new[]
        {
            new Section(P(.092f, -.040f, .113f), .024f, .013f),
            new Section(P(.074f, -.034f, .090f), .029f, .016f),
            new Section(P(.045f, -.031f, .065f), .040f, .018f),
            new Section(P(.018f, -.031f, .064f), .039f, .015f),
            new Section(P(.010f, -.032f, .064f), .033f, .011f)
        }, Vector3.Back);

        // Four individually curled fingers: MCP/PIP/DIP rings have different
        // lengths and radii. Gaps remain between every finger under the board.
        float[] lengths = { .058f, .065f, .061f, .048f };
        float[] widths = { .0078f, .0082f, .0077f, .0065f };
        for (var finger = 0; finger < 4; finger++)
        {
            var z = .030f + finger * .021f;
            var length = lengths[finger];
            var radius = widths[finger];
            Sweep(hand, new[]
            {
                new Section(P(.021f, -.033f, z), radius, radius * .95f),
                new Section(P(.004f, -.036f, z), radius * 1.03f, radius * .96f),
                new Section(P(.021f - length * .54f, -.034f, z), radius * .91f, radius * .88f),
                new Section(P(.021f - length * .82f, -.029f, z), radius * .76f, radius * .72f),
                new Section(P(.021f - length, -.026f, z), radius * .55f, radius * .48f),
                new Section(P(.018f - length, -.026f, z), .0014f, .0014f)
            }, Vector3.Back);
        }

        // Thumb comes around the outer edge, then rests on the blank margin.
        // Its top and the lower fingers clamp the cover without crossing it.
        Sweep(hand, new[]
        {
            new Section(P(.058f, -.027f, .033f), .016f, .014f),
            new Section(P(.037f, -.008f, .010f), .013f, .012f),
            new Section(P(.020f, top + .010f, .004f), .010f, .009f),
            new Section(P(-.004f, top + .0088f, .009f), .0092f, .0082f),
            new Section(P(-.025f, top + .008f, .015f), .0075f, .0072f),
            new Section(P(-.030f, top + .008f, .016f), .002f, .002f)
        }, Vector3.Back);
        AddMesh(parent, $"GlovePalm.{suffix}", hand, skin);

        using var nail = new SurfaceTool();
        nail.Begin(Mesh.PrimitiveType.Triangles);
        Sweep(nail, new[]
        {
            new Section(P(-.009f, top + .0162f, .010f), .003f, .0006f),
            new Section(P(-.015f, top + .0162f, .012f), .0055f, .0009f),
            new Section(P(-.024f, top + .0148f, .015f), .0045f, .0007f),
            new Section(P(-.026f, top + .0144f, .015f), .001f, .0004f)
        }, Vector3.Back);
        AddMesh(parent, $"ThumbNail.{suffix}", nail, nails);

        // Elbows stay in front of the passenger's torso (+Z), including when
        // the open album moves the left contact 225 mm towards the centre seat.
        var bodyCentre = open ? -.1125f : 0f;
        var wrist = P(.092f, -.040f, .113f);
        var elbow = new Vector3(bodyCentre + side * .20f, -.18f, .35f);
        var shoulder = new Vector3(bodyCentre + side * .19f, .18f, .49f);
        var forearm = elbow - wrist;
        using var sleeve = new SurfaceTool();
        sleeve.Begin(Mesh.PrimitiveType.Triangles);
        Sweep(sleeve, new[]
        {
            new Section(wrist + forearm * .06f, .035f, .030f),
            new Section(wrist + forearm * .14f, .040f, .035f),
            new Section(wrist + forearm * .48f, .047f, .041f),
            new Section(elbow, .056f, .049f),
            new Section(elbow.Lerp(shoulder, .35f), .057f, .050f),
            new Section(shoulder, .060f, .053f)
        }, Vector3.Right, 14);
        AddMesh(parent, $"Sleeve.{suffix}", sleeve, coat);

        using var cuff = new SurfaceTool();
        cuff.Begin(Mesh.PrimitiveType.Triangles);
        Sweep(cuff, new[]
        {
            new Section(wrist - forearm * .025f, .028f, .023f),
            new Section(wrist + forearm * .015f, .030f, .025f),
            new Section(wrist + forearm * .09f, .034f, .029f)
        }, Vector3.Right);
        AddMesh(parent, $"CuffBand.{suffix}", cuff, knit);
    }

    private readonly record struct Section(Vector3 Centre, float Width, float Depth);
    private readonly record struct Vertex(Vector3 Point, Vector3 Normal, Vector2 Uv);

    // A capped elliptical loft with smooth normals and explicit clockwise
    // winding, including mirrored hands. Each hand is only four draw surfaces.
    private static void Sweep(SurfaceTool tool, Section[] rings, Vector3 reference, int sides = 12)
    {
        var points = new Vector3[rings.Length, sides];
        var axes = new Vector3[rings.Length];
        for (var ring = 0; ring < rings.Length; ring++)
        {
            var axis = (rings[Math.Min(ring + 1, rings.Length - 1)].Centre
                - rings[Math.Max(ring - 1, 0)].Centre).Normalized();
            axes[ring] = axis;
            var across = reference.Slide(axis).Normalized();
            if (across.LengthSquared() < .01f) across = Vector3.Up.Slide(axis).Normalized();
            var depth = axis.Cross(across).Normalized();
            for (var side = 0; side < sides; side++)
            {
                var angle = Mathf.Tau * side / sides;
                points[ring, side] = rings[ring].Centre
                    + across * (Mathf.Cos(angle) * rings[ring].Width)
                    + depth * (Mathf.Sin(angle) * rings[ring].Depth);
            }
        }

        var vertices = new Vertex[rings.Length, sides];
        for (var ring = 0; ring < rings.Length; ring++)
        for (var side = 0; side < sides; side++)
        {
            var along = points[Math.Min(ring + 1, rings.Length - 1), side]
                - points[Math.Max(ring - 1, 0), side];
            var around = points[ring, (side + 1) % sides] - points[ring, (side + sides - 1) % sides];
            var normal = along.Cross(around).Normalized();
            if (normal.Dot(points[ring, side] - rings[ring].Centre) < 0) normal = -normal;
            vertices[ring, side] = new(points[ring, side], normal,
                new(side / (float)sides, ring / (float)(rings.Length - 1)));
        }
        for (var ring = 0; ring < rings.Length - 1; ring++)
        for (var side = 0; side < sides; side++)
        {
            var next = (side + 1) % sides;
            Triangle(tool, vertices[ring, side], vertices[ring + 1, side], vertices[ring + 1, next]);
            Triangle(tool, vertices[ring, side], vertices[ring + 1, next], vertices[ring, next]);
        }
        foreach (var ring in new[] { 0, rings.Length - 1 })
        {
            var normal = axes[ring] * (ring == 0 ? -1 : 1);
            for (var side = 0; side < sides; side++)
                Triangle(tool, new(rings[ring].Centre, normal, Vector2.Zero),
                    new(points[ring, side], normal, Vector2.Zero),
                    new(points[ring, (side + 1) % sides], normal, Vector2.Zero));
        }
    }

    private static void Triangle(SurfaceTool tool, Vertex a, Vertex b, Vertex c)
    {
        if ((b.Point - a.Point).Cross(c.Point - a.Point).Dot(a.Normal + b.Normal + c.Normal) > 0)
            (b, c) = (c, b);
        foreach (var vertex in new[] { a, b, c })
        {
            tool.SetNormal(vertex.Normal);
            tool.SetUV(vertex.Uv);
            tool.AddVertex(vertex.Point);
        }
    }

    private static void AddMesh(Node3D parent, string name, SurfaceTool tool, Material material)
    {
        tool.Index();
        parent.AddChild(new MeshInstance3D
        {
            Name = name, Mesh = tool.Commit(), MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }
}
