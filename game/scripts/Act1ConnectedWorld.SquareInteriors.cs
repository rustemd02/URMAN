using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Interiors of the two enterable square buildings (design:
    // docs/production/act1_square_interiors_design_2026-09-29.md). The shell is
    // hollow: real wall thickness, real window and door openings, floors and
    // ceilings with collision. Presentation and physics only - no runtime state.

    private sealed record Opening(float X, float Sill, float Width, float Height, bool Door = false, bool Boarded = false, bool Lit = false);

    private static Material Mat(string color, string surface = "") => PainterlyMaterialLibrary.ForColor(color, surface, sheltered: true);

    private static readonly Material SquareGlass = new StandardMaterial3D
    {
        AlbedoColor = new Color(.16f, .24f, .32f, .30f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        Roughness = .15f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    // A box with collision, placed in `parent` space.
    private static MeshInstance3D SBox(Node3D parent, StaticBody3D? body, string name, Vector3 size, Vector3 at, Material material,
        Vector3? rotation = null, bool shadow = true)
    {
        var mesh = new MeshInstance3D
        {
            Name = name, Mesh = RuralPropGeometry.Box(size), Position = at, MaterialOverride = material,
            RotationDegrees = rotation ?? Vector3.Zero,
            CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(mesh);
        body?.AddChild(new CollisionShape3D { Name = name + "Shape", Position = at, RotationDegrees = rotation ?? Vector3.Zero, Shape = new BoxShape3D { Size = size } });
        return mesh;
    }

    private static StaticBody3D SBody(Node3D parent, string name)
    {
        var body = new StaticBody3D { Name = name, CollisionLayer = 2, CollisionMask = 0 };
        body.SetMeta("collisionOwner", "act1-square-interior");
        parent.AddChild(body);
        return body;
    }

    // A wall run with openings. Local x runs along the wall (centred), +z is the
    // outward face. `outer` = exterior skin material, `inner` = room-side material.
    private static Node3D SWall(Node3D building, string name, Vector3 origin, float yaw, float length, float height, float thick,
        Material outer, Material inner, Material trim, IReadOnlyList<Opening> openings)
    {
        var wall = new Node3D { Name = name, Position = origin, RotationDegrees = new Vector3(0, yaw, 0) };
        building.AddChild(wall);
        var body = SBody(wall, name + "Body");
        var skin = Mathf.Min(.10f, thick * .4f);
        void Seg(string tag, float x0, float x1, float y0, float y1)
        {
            if (x1 - x0 < .01f || y1 - y0 < .01f) return;
            var cx = (x0 + x1) * .5f; var cy = (y0 + y1) * .5f;
            SBox(wall, null, tag + "Core", new(x1 - x0, y1 - y0, thick - skin), new(cx, cy, -skin * .5f), inner);
            SBox(wall, null, tag + "Skin", new(x1 - x0, y1 - y0, skin), new(cx, cy, thick * .5f - skin * .5f), outer);
            body.AddChild(new CollisionShape3D { Name = tag + "Shape", Position = new(cx, cy, 0), Shape = new BoxShape3D { Size = new(x1 - x0, y1 - y0, thick) } });
        }
        var ordered = openings.OrderBy(o => o.X).ToList();
        // Columns: openings sharing an x-range are stacked windows.
        var columns = new List<(float X0, float X1, List<Opening> Items)>();
        foreach (var opening in ordered)
        {
            var x0 = opening.X - opening.Width * .5f; var x1 = opening.X + opening.Width * .5f;
            var hit = columns.FindIndex(c => Mathf.Abs(c.X0 - x0) < .05f && Mathf.Abs(c.X1 - x1) < .05f);
            if (hit >= 0) columns[hit].Items.Add(opening); else columns.Add((x0, x1, new List<Opening> { opening }));
        }
        var cursor = -length * .5f; var index = 0;
        foreach (var column in columns.OrderBy(c => c.X0))
        {
            Seg("Pier" + index, cursor, column.X0, 0, height);
            var y = 0f;
            foreach (var item in column.Items.OrderBy(o => o.Sill))
            {
                Seg($"Sill{index}_{item.Sill:0.0}", column.X0, column.X1, y, item.Sill);
                y = item.Sill + item.Height;
                var cx = item.X;
                if (item.Door)
                {
                    SBox(wall, null, $"DoorPostL{index}", new(.09f, item.Height, thick + .04f), new(column.X0 + .045f, item.Sill + item.Height * .5f, 0), trim);
                    SBox(wall, null, $"DoorPostR{index}", new(.09f, item.Height, thick + .04f), new(column.X1 - .045f, item.Sill + item.Height * .5f, 0), trim);
                    SBox(wall, null, $"DoorHead{index}", new(item.Width, .09f, thick + .04f), new(cx, item.Sill + item.Height - .045f, 0), trim);
                }
                else
                {
                    SBox(wall, null, $"Glass{index}_{item.Sill:0.0}", new(item.Width - .02f, item.Height - .02f, .03f), new(cx, item.Sill + item.Height * .5f, 0), SquareGlass, shadow: false);
                    SBox(wall, null, $"Mullion{index}_{item.Sill:0.0}", new(.05f, item.Height, .06f), new(cx, item.Sill + item.Height * .5f, 0), trim);
                    SBox(wall, null, $"Transom{index}_{item.Sill:0.0}", new(item.Width, .05f, .06f), new(cx, item.Sill + item.Height * .62f, 0), trim);
                    foreach (var (fx, fy, fw, fh) in new[] { (cx, item.Sill + .035f, item.Width, .07f), (cx, item.Sill + item.Height - .035f, item.Width, .07f),
                        (column.X0 + .035f, item.Sill + item.Height * .5f, .07f, item.Height), (column.X1 - .035f, item.Sill + item.Height * .5f, .07f, item.Height) })
                    {
                        SBox(wall, null, $"FrameO{index}_{fx:0.0}_{fy:0.0}", new(fw, fh, .05f), new(fx, fy, thick * .5f + .02f), trim);
                        SBox(wall, null, $"FrameI{index}_{fx:0.0}_{fy:0.0}", new(fw, fh, .04f), new(fx, fy, -thick * .5f - .015f), trim);
                    }
                    SBox(wall, null, $"SillO{index}_{item.Sill:0.0}", new(item.Width + .24f, .06f, .2f), new(cx, item.Sill - .03f, thick * .5f + .08f), Mat("e6ebef", "snow_roof"));
                    SBox(wall, null, $"SillI{index}_{item.Sill:0.0}", new(item.Width + .1f, .05f, .22f), new(cx, item.Sill - .02f, -thick * .5f - .07f), Mat("efe9dc", "wood_painted_trim"));
                    if (item.Boarded)
                        for (var plank = 0; plank < 3; plank++)
                            SBox(wall, null, $"Board{index}_{item.Sill:0.0}_{plank}", new(item.Width + .3f, .17f, .04f),
                                new(cx, item.Sill + item.Height * (.2f + plank * .3f), thick * .5f + .06f), Mat("6e5a45", "wood"), new(0, 0, plank == 1 ? -8 : 5));
                    if (item.Lit)
                        SBox(wall, null, $"WarmPane{index}_{item.Sill:0.0}", new(item.Width - .1f, item.Height - .1f, .012f), new(cx, item.Sill + item.Height * .5f, thick * .5f - .03f),
                            new StandardMaterial3D { AlbedoColor = new Color(1f, .78f, .45f), EmissionEnabled = true, Emission = new Color(1f, .7f, .4f), EmissionEnergyMultiplier = .7f }, shadow: false);
                }
            }
            Seg($"Head{index}", column.X0, column.X1, y, height);
            cursor = column.X1; index++;
        }
        Seg("PierLast", cursor, length * .5f, 0, height);
        return wall;
    }

    private static void SSlab(Node3D building, string name, Vector3 size, Vector3 at, Material material)
    {
        var body = SBody(building, name + "Body");
        body.SetMeta("footstepSurface","herringbone_parquet");
        SBox(building, body, name, size, at, material);
    }

    private static OmniLight3D SLight(Node3D parent, Vector3 at, float energy = 1.1f, float range = 8f, string color = "ffd9a0") =>
        AddSquareLight(parent, at, energy, range, color);

    private static OmniLight3D AddSquareLight(Node3D parent, Vector3 at, float energy, float range, string color)
    {
        var light = new OmniLight3D { Name = "RoomLamp", Position = at, LightColor = Color.FromHtml(color), LightEnergy = energy, OmniRange = range, ShadowEnabled = false };
        parent.AddChild(light);
        // A visible fitting so the light has a source.
        SBox(parent, null, "LampFitting", new(.42f, .07f, .42f), at + Vector3.Up * .12f, Mat("f0ead6", "plastic_abs"), shadow: false);
        return light;
    }

    // Handwritten/enamel lettering is generated pigment on a real measured carrier.
    // Metadata preserves exact author text for inspection/accessibility; no new clue is inferred from bitmap text.
    private static void SLabel(Node3D parent, string text, Vector3 at, float yaw, int fontSize, Color color, float pixel = .005f)
    {
        var lines = text.Split('\n');
        var width = Mathf.Clamp(lines.Max(l => l.Length) * fontSize * pixel * .43f, .13f, 2.6f);
        var height = Mathf.Clamp(Mathf.Max(lines.Length * fontSize * pixel * 1.1f,width/2.2f), .075f, .8f);
        CivicSurfaceLibrary.Sign(parent,text,at,yaw,new(width,height));
    }

    private static void SPicture(Node3D parent, string name, Vector3 at, float yaw, Vector2 size, string? image, string fallback, string? caption = null)
    {
        Material pigment;
        if (image is not null && ResourceLoader.Exists(image))
            pigment = new StandardMaterial3D { AlbedoTexture = ResourceLoader.Load<Texture2D>(image), Roughness = .86f };
        else if (name.StartsWith("TukayPortrait",StringComparison.Ordinal) || name == "StageTukay")
            pigment = CivicSurfaceLibrary.Face("tukay_portrait_v1_basecolor.png");
        else if (name.StartsWith("Map_",StringComparison.Ordinal) || name == "UpperMap")
            pigment = CivicSurfaceLibrary.Face("classroom_map_v1_basecolor.png");
        else if (name.StartsWith("Drawing",StringComparison.Ordinal))
        {
            var i = int.Parse(name[7..]);
            pigment = CivicSurfaceLibrary.Face("children_drawings_v1_atlas.png",2,2,i == 5 ? 3 : i % 3);
        }
        else if (name == "MuseumTowel") pigment = CivicSurfaceLibrary.Face("museum_towel_v1_basecolor.png");
        else if (name == "HonourBoard") pigment = CivicSurfaceLibrary.Face("craft_details_v1_atlas.png",2,4,5);
        else if (name == "StaffNotice") pigment = CivicSurfaceLibrary.Face("notices_v2_atlas.png",2,4,2);
        else if (name == "CanteenMenu") pigment = CivicSurfaceLibrary.Face("notices_v2_atlas.png",2,4,1);
        else if (name == "ConcertBill") pigment = CivicSurfaceLibrary.Face("notices_v2_atlas.png",2,4,3);
        else if (name == "MuseumEmptyFrame") pigment = RuralPropMaterials.Surface("plywood"); // An intentionally empty frame, not a missing painting.
        else throw new InvalidOperationException("Uninventoried civic picture: " + name);
        var frame = CivicSurfaceLibrary.FramedFace(parent,name,at,yaw,size,pigment);
        if (caption is not null) SLabel(frame,caption,new(0,-size.Y*.5f-.09f,.015f),0,20,new(.15f,.13f,.1f));
    }

    // A real folded sheet with metric UVs. A textile map can be assigned to
    // the existing material without ever baking lighting or folds into albedo.
    private static void SCurtain(Node3D parent, string name, Vector3 centre, float width, float height, Material material)
    {
        const int columns = 64;
        const int rows = 24;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(int x, int y)
        {
            var u = x / (float)columns;
            var v = y / (float)rows;
            var fold = Mathf.Sin(u * Mathf.Pi * 10f) * (.064f + .021f * (1 - v))
                + Mathf.Sin(u * Mathf.Pi * 20f) * .009f;
            var hem = Mathf.Pow(1 - v, 5) * .045f * Mathf.Pow(Mathf.Sin(u * Mathf.Pi * 5), 2);
            surface.SetUV(new Vector2(u * width * 1.25f, v * height));
            surface.AddVertex(new Vector3((u - .5f) * width, (v - .5f) * height + hem, fold));
        }
        for (var y = 0; y < rows; y++)
        for (var x = 0; x < columns; x++)
        {
            Vertex(x, y); Vertex(x + 1, y); Vertex(x + 1, y + 1);
            Vertex(x, y); Vertex(x + 1, y + 1); Vertex(x, y + 1);
        }
        surface.Index(); surface.GenerateNormals();
        parent.AddChild(new MeshInstance3D { Name = name, Position = centre,
            Mesh = surface.Commit(), MaterialOverride = material });
    }

    private static void SDesk(Node3D room, StaticBody3D body, string name, Vector3 at, float yaw, float w = 1.2f, float d = .6f, float h = .74f)
    {
        var desk = RuralPropModels.Desk(room,name,at,yaw,w,d,h);
        RuralPropGeometry.AttachMemberContacts(desk, body);
    }

    private static void SChair(Node3D room, string name, Vector3 at, float yaw, string color = "8a6a48")
        => RuralPropModels.Chair(room,name,at,yaw);

    private static void SShelf(Node3D room, StaticBody3D body, string name, Vector3 at, float yaw, float w, float h, float d = .32f, string[]? spines = null)
    {
        var shelf = new Node3D { Name = name, Position = at, RotationDegrees = new Vector3(0, yaw, 0) };
        room.AddChild(shelf);
        var wood = Mat("7d6548", "wood_furniture");
        SBox(shelf, null, "Back", new(w, h, .02f), new(0, h * .5f, -d * .5f + .01f), wood);
        foreach (var x in new[] { -w * .5f + .015f, w * .5f - .015f }) SBox(shelf, null, "Side", new(.03f, h, d), new(x, h * .5f, 0), wood);
        var shelves = Mathf.Max(3, (int)(h / .38f));
        var palette = spines ?? new[] { "8a3b34", "3f5f7a", "d1b46a", "4c6b48", "6b4f7a", "c8c0a8" };
        var rng = new RandomNumberGenerator { Seed = (ulong)name.GetHashCode() };
        for (var level = 0; level <= shelves; level++)
        {
            SBox(shelf, null, "Board", new(w, .03f, d), new(0, level * (h - .03f) / shelves + .015f, 0), wood);
            if (level == shelves) break;
            var x = -w * .5f + .06f;
            while (x < w * .5f - .1f)
            {
                var bw = rng.RandfRange(.03f, .07f); var bh = rng.RandfRange(.2f, .32f);
                SBox(shelf, null, "Book", new(bw, bh, d * .7f), new(x + bw * .5f, level * (h - .03f) / shelves + .03f + bh * .5f, .02f), Mat(palette[rng.RandiRange(0, palette.Length - 1)], "cloth"), shadow: false);
                x += bw + .004f;
            }
        }
        body.AddChild(new CollisionShape3D { Name = name + "Shape", Position = at + new Vector3(0, h * .5f, 0), RotationDegrees = new Vector3(0, yaw, 0), Shape = new BoxShape3D { Size = new(w, h, d) } });
    }

    // A local, presentation-only inspection: a short thought and, optionally, a sound.
    private void SquareLook(Node3D parent, string name, string prompt, Vector3 at, Vector3 size, string thought, string? sound = null)
    {
        var target = FacilityTarget(name, "urman.chapter1:local/square/" + name, prompt, parent, at, size);
        target.PresentationRepeatAvailable = () => FacilityExteriorActive;
        target.PresentationRepeat = () =>
        {
            if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return;
            if (sound is not null) UiFoley.PlayWorld(this, target.GlobalPosition, sound);
            player.NotifyTraversal(thought);
        };
    }
}
