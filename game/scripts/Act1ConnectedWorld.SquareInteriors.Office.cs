using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // The former sovkhoz office, opposite babai's gate (author request 2026-11-13:
    // "an interior of your own invention, very interesting and very beautiful").
    //
    // The idea: the office closed years ago and nobody told the building. A
    // two-storey hall with a harvest panel over the stair; a cashier's window
    // and ledgers; a red corner with its honour board; upstairs a director's
    // room where the lamp is on and the tea is hot, and a radio room whose
    // switchboard has one line more than the village has loudspeakers.
    //
    // Presentation and physics only: no quest, document, journal or save state.

    private const string OfficeTextureFolder = "res://assets/textures/office/";

    /// <summary>An optional generated picture: <c>name.png</c> in res://assets/textures/office/ replaces the built relief.</summary>
    private static Texture2D? OfficeTextureSlot(string name)
    {
        var path = OfficeTextureFolder + name + ".png";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    /// <summary>Lettering in the game's own type on a small painted plate (or, handwritten, straight on what carries it).</summary>
    private static void OfficeLabel(Node3D parent, string text, Vector3 at, float yaw, float letter, bool plate = true, bool hand = false)
    {
        var holder = new Node3D { Name = "Label_" + text, Position = at, RotationDegrees = new Vector3(0, yaw, 0) };
        parent.AddChild(holder);
        if (plate)
            holder.AddChild(new MeshInstance3D
            {
                Name = "Plate", Mesh = new BoxMesh { Size = new Vector3(text.Length * letter * .7f + letter * 1.3f, letter * 2f, .01f) },
                MaterialOverride = Mat("e6dfcc", "plaster"), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        holder.AddChild(new Label3D
        {
            Name = "Text", Text = text, Font = hand ? UrmanUiTheme.HandFont : UrmanUiTheme.DisplayFont, FontSize = 96,
            PixelSize = letter * (hand ? 1.6f : 1.25f) / 96f, Position = new Vector3(0, 0, .007f), Modulate = new Color(.16f, .13f, .1f),
            OutlineSize = 0, Shaded = true, DoubleSided = false, AlphaCut = Label3D.AlphaCutMode.Discard,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        });
    }

    private static StandardMaterial3D OfficeGlow(string color, float energy) => new()
    {
        AlbedoColor = Color.FromHtml(color), EmissionEnabled = true, Emission = Color.FromHtml(color),
        EmissionEnergyMultiplier = energy, Roughness = .6f
    };

    private void BuildOfficeInterior(Node3D office, Material outer, Material trim)
    {
        const float w = 14f, d = 9f, fh = 3.2f, h = fh * 2f, t = .35f;
        const float part = 3f;                    // the two partitions stand at x = ±part
        var ix = w * .5f - t; var iz = d * .5f - t;
        var cream = Mat("ece3cb", "wall_institution");
        var roomWall = PainterlyMaterialLibrary.ForDadoWall("ece3cb", "6d8a7b", 1.45f);
        var hallWall = PainterlyMaterialLibrary.ForDadoWall("e6dcc0", "7a5f47", 1.6f);
        office.SetMeta("footstepSurface", "herringbone_parquet");
        office.SetMeta("woodCreak", true);
        office.SetMeta("interiorHalfSize", new Vector2(ix, iz));
        office.SetMeta("interiorCeiling", h);
        office.SetMeta("storeys", 2);
        office.SetMeta("interior", "two-storey sovkhoz office: double-height hall with a harvest panel, cashier, red corner, director's room, radio room");
        InteriorReflectionProbes.Add(office, "OfficeReflectionProbe", new(0, h * .5f, 0), new(ix * 2, h, iz * 2));

        // ---- Shell: real walls with real openings -------------------------------------------------
        var front = new List<Opening>
        {
            new(0, 0, 1.7f, 2.45f, Door: true), new(0, 3.9f, 1.7f, 2.0f, Lit: true),
            new(-2.0f, 3.9f, .9f, 2.0f, Lit: true), new(2.0f, 3.9f, .9f, 2.0f, Lit: true)
        };
        var back = new List<Opening> { new(0, 1.0f, 1.4f, 1.4f) };
        foreach (var x in new[] { -5.4f, 5.4f })
        {
            front.Add(new(x, .9f, 1.15f, 1.5f, Lit: true));
            front.Add(new(x, fh + .9f, 1.15f, 1.5f, Lit: true));
        }
        foreach (var x in new[] { -5.5f, -4.0f, 4.0f, 5.5f })
        {
            back.Add(new(x, .9f, 1.0f, 1.5f));
            back.Add(new(x, fh + .9f, 1.0f, 1.5f));
        }
        var side = new List<Opening>();
        foreach (var x in new[] { -2f, 2f })
        {
            side.Add(new(x, .9f, 1.1f, 1.5f));
            side.Add(new(x, fh + .9f, 1.1f, 1.5f));
        }
        var frontWall = SWall(office, "FrontWall", new(0, 0, d * .5f - t * .5f), 0, w, h, t, outer, roomWall, trim, front);
        SWall(office, "BackWall", new(0, 0, -d * .5f + t * .5f), 180, w, h, t, outer, roomWall, trim, back);
        SWall(office, "EastWall", new(w * .5f - t * .5f, 0, 0), 90, d - t * 2, h, t, outer, roomWall, trim, side);
        SWall(office, "WestWall", new(-w * .5f + t * .5f, 0, 0), -90, d - t * 2, h, t, outer, roomWall, trim, side);
        SBox(office, null, "OfficeCeiling", new(ix * 2, .08f, iz * 2), new(0, h - .04f, 0), cream, shadow: false);

        // Partitions: the skin faces the hall. Wall-local x runs along building -z (west) / +z (east).
        var westLow = SWall(office, "WestPartitionLow", new(-part, 0, 0), 90, iz * 2, fh, .16f, hallWall, roomWall, trim,
            new List<Opening> { new(3.45f, 0, 1.0f, 2.1f, Door: true) });
        var hallUpper = Mat("e6dcc0", "wall_institution");
        var westUp = SWall(office, "WestPartitionUp", new(-part, fh, 0), 90, iz * 2, fh, .16f, hallUpper, cream, trim,
            new List<Opening> { new(3.45f, 0, 1.0f, 2.1f, Door: true) });
        var eastLow = SWall(office, "EastPartitionLow", new(part, 0, 0), -90, iz * 2, fh, .16f, hallWall, roomWall, trim,
            new List<Opening> { new(3.0f, 0, 1.0f, 2.1f, Door: true), new(.9f, 1.0f, .9f, .78f, Door: true) });
        var eastUp = SWall(office, "EastPartitionUp", new(part, fh, 0), -90, iz * 2, fh, .16f, hallUpper, cream, trim,
            new List<Opening> { new(-3.45f, 0, 1.0f, 2.1f, Door: true) });
        foreach (var wall in new[] { westLow, eastLow }) ToneWallSkins(wall);

        // Floors. The hall is painted boards; the rooms keep their parquet.
        var hallBoards = (StandardMaterial3D)CivicSurfaceLibrary.Parquet().Duplicate();
        hallBoards.AlbedoColor = new Color(.86f, .58f, .46f);
        var hallHalf = part - .08f;
        CivicSurfaceLibrary.Floor(office, "HallFloor", new(hallHalf * 2, iz * 2), new(0, .002f, 0)).MaterialOverride = hallBoards;
        var wingWidth = ix - part - .08f; var wingX = (ix + part + .08f) * .5f;
        foreach (var sign in new[] { -1f, 1f })
        {
            var tag = sign < 0 ? "West" : "East";
            CivicSurfaceLibrary.Floor(office, tag + "GroundFloor", new(wingWidth, iz * 2), new(sign * wingX, .002f, 0));
            SSlab(office, tag + "UpperSlab", new(wingWidth + .16f, .2f, iz * 2), new(sign * (wingX - .04f), fh - .1f, 0), cream);
            CivicSurfaceLibrary.Floor(office, tag + "UpperFloor", new(wingWidth, iz * 2), new(sign * wingX, fh + .002f, 0));
        }
        const float galleryEdge = -2.75f;
        SSlab(office, "Gallery", new(hallHalf * 2, .2f, galleryEdge + iz), new(0, fh - .1f, (galleryEdge - iz) * .5f), Mat("8a6a4a", "wood_furniture"));
        CivicSurfaceLibrary.Floor(office, "GalleryFloor", new(hallHalf * 2, galleryEdge + iz), new(0, fh + .002f, (galleryEdge - iz) * .5f)).MaterialOverride = hallBoards;

        // Everything below is furnishing: one root, so distance can drop it as a whole.
        var room = new Node3D { Name = "OfficeFurnishing" };
        office.AddChild(room);
        var body = SBody(room, "OfficeFurnitureBody");
        var lamps = new List<VisualInstance3D>();
        MeshInstance3D B(string name, Vector3 size, Vector3 at, Material material, Vector3? rotation = null) =>
            SBox(room, null, name, size, at, material, rotation, shadow: false);
        MeshInstance3D Solid(string name, Vector3 size, Vector3 at, Material material) => SBox(room, body, name, size, at, material);
        MeshInstance3D Round(string name, float radius, float height, Vector3 at, Material material, Vector3? rotation = null, float top = -1f)
        {
            var mesh = new MeshInstance3D
            {
                Name = name, Position = at, RotationDegrees = rotation ?? Vector3.Zero, MaterialOverride = material,
                Mesh = new CylinderMesh { BottomRadius = radius, TopRadius = top < 0f ? radius : top, Height = height, RadialSegments = 14, Rings = 1 },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            room.AddChild(mesh);
            return mesh;
        }
        MeshInstance3D Ball(string name, float radius, Vector3 at, Material material, Vector3? scale = null)
        {
            var mesh = new MeshInstance3D
            {
                Name = name, Position = at, Scale = scale ?? Vector3.One, MaterialOverride = material,
                Mesh = new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = 10, Rings = 6 },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            room.AddChild(mesh);
            return mesh;
        }
        void Lamp(Vector3 at, float energy, float range, string color = "ffd9a0") => lamps.Add(SLight(room, at, energy, range, color));
        // Somebody waters these. Nobody has seen who.
        var terracotta = Mat("a35a3c", "plaster"); var leaf = Mat("3f6b3a", "fabric"); var petal = Mat("c2372f", "fabric");
        void Geranium(string name, Vector3 at)
        {
            Round(name + "Pot", .075f, .12f, at + new Vector3(0, .06f, 0), terracotta, top: .095f);
            Ball(name + "Leaves", .12f, at + new Vector3(0, .2f, 0), leaf, new Vector3(1.15f, .62f, 1.15f));
            Ball(name + "LeavesHigh", .08f, at + new Vector3(.03f, .29f, -.02f), leaf, new Vector3(1f, .7f, 1f));
            Ball(name + "FlowerA", .035f, at + new Vector3(-.05f, .36f, .02f), petal);
            Ball(name + "FlowerB", .03f, at + new Vector3(.06f, .33f, -.04f), petal);
        }
        var wood = Mat("7d6548", "wood_furniture"); var darkWood = Mat("5a4433", "wood_furniture");
        var steel = Mat("5f6668", "metal"); var paper = Mat("e9e2cf", "paper"); var brass = Mat("b08d57", "metal");

        BuildOfficeHall(room, body, B, Solid, Round, Ball, Lamp, hallHalf, iz, fh, h, galleryEdge);
        BuildOfficeAccounts(room, body, B, Solid, Round, Lamp, Geranium, part, ix, iz, fh);
        BuildOfficeRedCorner(room, body, B, Solid, Round, Ball, Lamp, Geranium, part, ix, iz, fh);
        BuildOfficeDirector(room, body, B, Solid, Round, Ball, Lamp, Geranium, part, ix, iz, fh);
        BuildOfficeRadioRoom(room, body, B, Solid, Round, Ball, Lamp, Geranium, part, ix, iz, fh);

        // Doors: the entrance pair opens outward, every room door into its room.
        SquareInteriorDoor(frontWall, "SquareOfficeEntranceL", "square/office/entrance-l", 0, 1.7f, 2.45f, t, 1, -1, .75f);
        SquareInteriorDoor(frontWall, "SquareOfficeEntranceR", "square/office/entrance-r", 0, 1.7f, 2.45f, t, 1, 1, .75f);
        SquareInteriorDoor(westLow, "SquareOfficeRedCornerDoor", "square/office/red-corner", 3.45f, 1.0f, 2.1f, .16f, -1, -1);
        SquareInteriorDoor(westUp, "SquareOfficeDirectorDoor", "square/office/director", 3.45f, 1.0f, 2.1f, .16f, -1, -1);
        SquareInteriorDoor(eastLow, "SquareOfficeAccountsDoor", "square/office/accounts", 3.0f, 1.0f, 2.1f, .16f, -1, 1);
        SquareInteriorDoor(eastUp, "SquareOfficeRadioDoor", "square/office/radio", -3.45f, 1.0f, 2.1f, .16f, -1, 1);
        SquareLightSwitch(office, "SquareOfficeLightSwitch", "office-light", new(1.3f, 1.42f, iz - .0125f), lamps);
        EnsureFacilityTick();

        // Nothing inside is drawn or lit from across the square.
        foreach (var piece in room.FindChildren("*", nameof(GeometryInstance3D), true, false).OfType<GeometryInstance3D>())
        {
            piece.VisibilityRangeEnd = 46f;
            piece.VisibilityRangeEndMargin = 4f;
        }
        foreach (var light in room.FindChildren("*", nameof(OmniLight3D), true, false).OfType<OmniLight3D>())
        {
            light.DistanceFadeEnabled = true;
            light.DistanceFadeBegin = 36f;
            light.DistanceFadeLength = 8f;
        }
    }

    private delegate MeshInstance3D OfficeBox(string name, Vector3 size, Vector3 at, Material material, Vector3? rotation = null);
    private delegate MeshInstance3D OfficeSolid(string name, Vector3 size, Vector3 at, Material material);
    private delegate MeshInstance3D OfficeRound(string name, float radius, float height, Vector3 at, Material material, Vector3? rotation = null, float top = -1f);
    private delegate MeshInstance3D OfficeBall(string name, float radius, Vector3 at, Material material, Vector3? scale = null);
    private delegate void OfficeLamp(Vector3 at, float energy, float range, string color = "ffd9a0");
    private delegate void OfficePlant(string name, Vector3 at);

    // ---- The hall: stair, gallery, the harvest panel ------------------------------------------------

    private void BuildOfficeHall(Node3D room, StaticBody3D body, OfficeBox B, OfficeSolid Solid, OfficeRound Round, OfficeBall Ball,
        OfficeLamp Lamp, float hallHalf, float iz, float fh, float h, float galleryEdge)
    {
        var wood = Mat("8a6a4a", "wood_furniture"); var worn = Mat("6f553c", "wood_furniture"); var darkWood = Mat("5a4433", "wood_furniture");
        var cream = Mat("ece3cb", "wall_institution"); var brass = Mat("b08d57", "metal"); var paper = Mat("e9e2cf", "paper");

        // The stair climbs the west side of the hall to the gallery: twenty risers of 16 cm.
        const int steps = 20; const float rise = 3.2f / steps, tread = .28f, stairWidth = 1.2f;
        var stairX = -hallHalf + stairWidth * .5f; var openX = stairX + stairWidth * .5f;
        var bottomZ = galleryEdge + steps * tread;
        for (var i = 0; i < steps; i++)
        {
            var z = bottomZ - (i + .5f) * tread;
            B("Tread" + i, new(stairWidth, .04f, tread + .025f), new(stairX, (i + 1) * rise - .02f, z), i % 7 == 3 ? worn : wood);
            B("Riser" + i, new(stairWidth, rise - .04f, .022f), new(stairX, i * rise + (rise - .04f) * .5f, bottomZ - i * tread - .011f), darkWood);
        }
        // What the feet meet is one smooth incline under the treads, so no step can catch a boot.
        var run = bottomZ + .15f - galleryEdge; var length = Mathf.Sqrt(run * run + fh * fh);
        var pitch = Mathf.RadToDeg(Mathf.Atan2(fh, run));
        var normal = new Vector3(0, run / length, fh / length);
        var middle = new Vector3(stairX, fh * .5f, (bottomZ + .15f + galleryEdge) * .5f);
        var slope = new Vector3(pitch, 0, 0);
        body.AddChild(new CollisionShape3D { Name = "StairIncline", Position = middle - normal * .1f, RotationDegrees = slope,
            Shape = new BoxShape3D { Size = new(stairWidth, .2f, length) } });
        body.AddChild(new CollisionShape3D { Name = "StairGuard", Position = middle + normal * .55f + new Vector3(stairWidth * .5f + .03f, 0, 0), RotationDegrees = slope,
            Shape = new BoxShape3D { Size = new(.06f, 1.1f, length) } });
        B("StairSoffit", new(stairWidth, .05f, length), middle - normal * .2f, cream, slope);
        foreach (var x in new[] { stairX - stairWidth * .5f + .02f, openX })
            B("Stringer" + x, new(.045f, .36f, length + .1f), new Vector3(x, middle.Y, middle.Z) - normal * .06f, darkWood, slope);
        var railFoot = new Vector3(openX, .95f, bottomZ - tread * .5f); var railHead = new Vector3(openX, fh + .95f, galleryEdge);
        RuralPropGeometry.Tube(room, "StairHandrail", railFoot, railHead, .03f, darkWood);
        for (var i = 0; i < steps; i += 2)
            B("StairBaluster" + i, new(.03f, .93f, .03f), new(openX, (i + 1) * rise + .465f, bottomZ - (i + .5f) * tread), wood);
        B("NewelFoot", new(.1f, 1.15f, .1f), new(openX, .575f, bottomZ + .02f), darkWood);
        Ball("NewelFootCap", .07f, new(openX, 1.2f, bottomZ + .02f), brass);
        // The gallery rail, from the head of the stair to the east wall.
        var railLength = hallHalf - openX;
        var railMiddle = (hallHalf + openX) * .5f;
        RuralPropGeometry.Tube(room, "GalleryHandrail", new(openX, fh + .95f, galleryEdge + .04f), new(hallHalf, fh + .95f, galleryEdge + .04f), .03f, darkWood);
        for (var x = openX; x < hallHalf; x += .22f)
            B("GalleryBaluster" + x, new(.03f, .93f, .03f), new(x, fh + .465f, galleryEdge + .04f), wood);
        B("NewelHead", new(.1f, 1.15f, .1f), new(openX, fh + .575f, galleryEdge + .04f), darkWood);
        body.AddChild(new CollisionShape3D { Name = "GalleryRail", Position = new(railMiddle, fh + .5f, galleryEdge + .04f),
            Shape = new BoxShape3D { Size = new(railLength, 1f, .07f) } });
        B("GalleryFascia", new(hallHalf * 2, .26f, .04f), new(0, fh - .1f, galleryEdge + .02f), darkWood);

        // The harvest panel fills the back wall above the gallery.
        BuildOfficeHarvestPanel(room, new Vector3(.3f, fh + 1.62f, -iz + .012f));
        var spot = new SpotLight3D
        {
            Name = "PanelLight", Position = new(.3f, h - .5f, -.6f), LightColor = new Color(1f, .9f, .74f), LightEnergy = 2.4f,
            SpotRange = 7.5f, SpotAngle = 46f, ShadowEnabled = false
        };
        room.AddChild(spot);
        spot.LookAtFromPosition(spot.Position, new Vector3(.3f, fh + 1.5f, -iz), Vector3.Up);
        SquareLook(room, "OfficeHarvestPanel", "Рассмотреть панно", new(.3f, fh + 1.2f, -iz + .4f), new(4.6f, 2.2f, .7f),
            "Панно «Урожай»: солнце, поле, деревня у реки. Лес выложен тёмной смальтой, и его здесь больше, чем поля. Справа одно дерево выше остальных — и стоит оно не в лесу, а уже на поле.");

        // A chandelier of the seventies over the open part of the hall.
        var hub = new Vector3(.8f, h - 1.05f, .6f);
        Round("ChandelierRod", .012f, 1f, hub + Vector3.Up * .5f, brass);
        Ball("ChandelierHub", .09f, hub, brass);
        var bulb = OfficeGlow("ffe2b0", 2.6f);
        for (var arm = 0; arm < 8; arm++)
        {
            var angle = arm * Mathf.Tau / 8f;
            var reach = arm % 2 == 0 ? .62f : .42f;
            var tip = hub + new Vector3(Mathf.Cos(angle) * reach, arm % 2 == 0 ? -.12f : .1f, Mathf.Sin(angle) * reach);
            RuralPropGeometry.Tube(room, "ChandelierArm" + arm, hub, tip, .008f, brass, 8);
            Ball("ChandelierBulb" + arm, .05f, tip, bulb);
        }
        var glow = new OmniLight3D { Name = "ChandelierLight", Position = hub - Vector3.Up * .15f, LightColor = new Color(1f, .86f, .64f),
            LightEnergy = 1.5f, OmniRange = 9.5f, ShadowEnabled = GraphicsQuality.Preset == "high", LightSize = .3f };
        glow.AddToGroup(GraphicsQuality.SoftShadowLampGroup);
        room.AddChild(glow);
        Lamp(new(.6f, fh - .52f, -3.4f), .7f, 4.5f);          // under the gallery

        // The cashier's window, on the east side: a counter worn pale by sleeves, a brass grille.
        B("KassaCounter", new(.4f, .04f, 1.02f), new(hallHalf - .06f, 1.0f, .9f), wood);
        for (var bar = 0; bar < 6; bar++)
            B("KassaGrille" + bar, new(.012f, .5f, .012f), new(hallHalf + .02f, 1.5f, .52f + bar * .152f), brass);
        B("KassaGrilleRail", new(.014f, .014f, .86f), new(hallHalf + .02f, 1.25f, .9f), brass);
        OfficeLabel(room, "КАССА", new(hallHalf - .012f, 2.02f, .9f), -90, 0.11f);
        OfficeLabel(room, "БУХГАЛТЕРИЯ", new(hallHalf - .012f, 2.3f, 3.0f), -90, 0.075f);
        OfficeLabel(room, "КРАСНЫЙ УГОЛОК", new(-hallHalf + .012f, 2.3f, -3.45f), 90, 0.075f);
        OfficeLabel(room, "ДИРЕКТОР", new(-hallHalf + .012f, fh + 2.3f, -3.45f), 90, 0.075f);
        OfficeLabel(room, "РАДИОУЗЕЛ", new(hallHalf - .012f, fh + 2.3f, -3.45f), -90, 0.075f);
        SquareLook(room, "OfficeKassa", "Заглянуть в кассу", new(hallHalf - .2f, 1.35f, .9f), new(.6f, .8f, 1f),
            "Окошко кассы. На прилавке мелочь, сложенная столбиками, — её пересчитали и не убрали. Пыли на монетах нет.");

        // The notice board: the sheets have faded to nothing; the pins are new.
        B("NoticeBoard", new(.03f, 1.0f, 1.7f), new(hallHalf - .02f, 1.55f, -1.3f), Mat("8a6f4d", "wood"));
        B("NoticeBoardFrameTop", new(.045f, .05f, 1.78f), new(hallHalf - .025f, 2.07f, -1.3f), darkWood);
        B("NoticeBoardFrameBottom", new(.045f, .05f, 1.78f), new(hallHalf - .025f, 1.03f, -1.3f), darkWood);
        var sheets = new[] { (-.55f, .2f, .3f, .42f, 3f), (-.1f, .12f, .21f, .3f, -4f), (.3f, .22f, .3f, .21f, 2f), (.58f, -.12f, .2f, .28f, -2f), (-.42f, -.22f, .28f, .2f, 5f), (.1f, -.2f, .21f, .3f, 1f) };
        for (var sheet = 0; sheet < sheets.Length; sheet++)
        {
            var (z, y, sw, sh, tilt) = sheets[sheet];
            B("NoticeSheet" + sheet, new(.004f, sh, sw), new(hallHalf - .04f, 1.55f + y, -1.3f + z), paper, new Vector3(tilt, 0, 0));
            Ball("NoticePin" + sheet, .012f, new(hallHalf - .048f, 1.55f + y + sh * .5f - .03f, -1.3f + z), sheet == 2 ? OfficeGlow("c2372f", .2f) : brass);
        }
        OfficeLabel(room, "ОБЪЯВЛЕНИЯ", new(hallHalf - .012f, 2.22f, -1.3f), -90, 0.085f);
        SquareLook(room, "OfficeNotices", "Прочитать объявления", new(hallHalf - .25f, 1.55f, -1.3f), new(.6f, 1.1f, 1.7f),
            "Листки выцвели добела. Читается один, приколотый красной кнопкой: «Собрание переносится». Ни даты, ни места. Кнопка новая.");
        RuralPropModels.Bench(room, "HallBench", new(hallHalf - .36f, 0, -1.3f), -90, 1.7f);
        body.AddChild(new CollisionShape3D { Name = "HallBenchContact", Position = new(hallHalf - .36f, .43f, -1.3f), Shape = new BoxShape3D { Size = new(.42f, .09f, 1.7f) } });

        // The clock over the cashier stopped at seventeen minutes past four.
        var clockAt = new Vector3(hallHalf - .03f, 2.72f, .9f);
        Round("ClockCase", .2f, .05f, clockAt, darkWood, new Vector3(0, 0, 90));
        Round("ClockFace", .17f, .052f, clockAt - new Vector3(.004f, 0, 0), Mat("efe8d5", "plaster"), new Vector3(0, 0, 90));
        B("ClockHour", new(.008f, .1f, .014f), clockAt + new Vector3(-.034f, -.02f, .045f), Mat("1e1c1a", "metal"), new Vector3(128f, 0, 0));
        B("ClockMinute", new(.008f, .15f, .01f), clockAt + new Vector3(-.036f, .01f, -.07f), Mat("1e1c1a", "metal"), new Vector3(-102f, 0, 0));

        // A ficus by the door, taller than a man and green in January.
        var pot = new Vector3(-.2f, 0, iz - .55f);
        pot.X = hallHalf - .5f;
        Round("FicusTub", .2f, .42f, pot + Vector3.Up * .21f, Mat("6f5a45", "wood"), top: .24f);
        body.AddChild(new CollisionShape3D { Name = "FicusTubContact", Position = pot + Vector3.Up * .25f, Shape = new CylinderShape3D { Radius = .24f, Height = .5f } });
        RuralPropGeometry.Tube(room, "FicusTrunk", pot + Vector3.Up * .4f, pot + new Vector3(-.05f, 1.75f, -.04f), .022f, Mat("5d4a38", "wood_bark"), 8);
        var foliage = Mat("2f5a34", "fabric");
        var rng = new RandomNumberGenerator { Seed = 0x4f464631 };
        for (var clump = 0; clump < 12; clump++)
            Ball("FicusLeaves" + clump, rng.RandfRange(.14f, .22f),
                pot + new Vector3(rng.RandfRange(-.3f, .2f), rng.RandfRange(1.25f, 2.0f), rng.RandfRange(-.28f, .2f)), foliage,
                new Vector3(1f, rng.RandfRange(.85f, 1.1f), 1f));
        B("DoorMat", new(1.5f, .012f, .8f), new(0, .008f, iz - .55f), Mat("4a3d33", "fabric"));
        RuralPropModels.Radiator(room, "HallRadiator", new(0, .13f, -iz + .19f), 1.3f);
    }

    /// <summary>
    /// "Harvest": a relief of flat coloured pieces, as such panels were set. Built
    /// from geometry until a generated picture is dropped into the texture slot.
    /// </summary>
    private static void BuildOfficeHarvestPanel(Node3D room, Vector3 at)
    {
        var panel = new Node3D { Name = "HarvestPanel", Position = at };
        room.AddChild(panel);
        const float width = 5.0f, height = 2.5f;
        void Piece(string name, Vector3 size, Vector3 position, string color, float turn = 0f, Mesh? mesh = null)
        {
            panel.AddChild(new MeshInstance3D
            {
                Name = name, Mesh = mesh ?? new BoxMesh { Size = size }, Position = position, RotationDegrees = new Vector3(0, 0, turn),
                MaterialOverride = Mat(color, "plaster"), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        var frame = "5a4433";
        Piece("FrameTop", new(width + .16f, .08f, .06f), new(0, height * .5f + .04f, .03f), frame);
        Piece("FrameBottom", new(width + .16f, .08f, .06f), new(0, -height * .5f - .04f, .03f), frame);
        Piece("FrameLeft", new(.08f, height, .06f), new(-width * .5f - .04f, 0, .03f), frame);
        Piece("FrameRight", new(.08f, height, .06f), new(width * .5f + .04f, 0, .03f), frame);
        if (OfficeTextureSlot("harvest_mosaic") is { } picture)
        {
            panel.AddChild(new MeshInstance3D
            {
                Name = "Picture", Mesh = new QuadMesh { Size = new Vector2(width, height) }, Position = new Vector3(0, 0, .012f),
                MaterialOverride = new StandardMaterial3D { AlbedoTexture = picture, Roughness = .8f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
            return;
        }
        const float horizon = .12f;
        // Sky in three courses, dawn at the bottom.
        Piece("SkyHigh", new(width, .46f, .02f), new(0, height * .5f - .23f, .01f), "35557c");
        Piece("SkyMid", new(width, .42f, .02f), new(0, height * .5f - .67f, .01f), "6f93b0");
        Piece("SkyLow", new(width, height * .5f - .88f - horizon, .02f), new(0, (height * .5f - .88f + horizon) * .5f, .01f), "e3c58c");
        // The sun and its rays.
        var sun = new Vector3(-.5f, horizon + .52f, .03f);
        for (var ray = 0; ray < 16; ray++)
        {
            var angle = ray * 360f / 16f;
            var away = new Vector3(Mathf.Cos(Mathf.DegToRad(angle)), Mathf.Sin(Mathf.DegToRad(angle)), 0f);
            if (sun.Y + away.Y * .9f < horizon) continue;
            Piece("Ray" + ray, new(ray % 2 == 0 ? .5f : .3f, .035f, .02f), sun + away * (ray % 2 == 0 ? .72f : .58f), "e9b84a", angle);
        }
        Piece("Sun", default, sun + new Vector3(0, 0, .012f), "f0c24f", 0f,
            new CylinderMesh { TopRadius = .36f, BottomRadius = .36f, Height = .04f, RadialSegments = 28, Rings = 1 });
        panel.GetNode<MeshInstance3D>("Sun").RotationDegrees = new Vector3(90, 0, 0);
        // The field: bands of grain and furrows running to the sun.
        var bands = new[] { "c99a34", "ddb24a", "b98a2e", "8fa24f", "d6a93a", "a8872f", "7c9a4a", "c99a34" };
        var bandHeight = (horizon + height * .5f) / bands.Length;
        for (var band = 0; band < bands.Length; band++)
            Piece("Field" + band, new(width, bandHeight + .004f, .024f), new(0, horizon - (band + .5f) * bandHeight, .012f), bands[band]);
        for (var furrow = -5; furrow <= 5; furrow++)
        {
            if (furrow == 0) continue;
            var foot = new Vector2(furrow * .52f - .5f, -height * .5f + .04f);
            var head = new Vector2(sun.X + furrow * .05f, horizon - .03f);
            var along = head - foot;
            Piece("Furrow" + furrow, new(along.Length(), .016f, .02f), new((foot.X + head.X) * .5f, (foot.Y + head.Y) * .5f, .03f), "6d5424",
                Mathf.RadToDeg(Mathf.Atan2(along.Y, along.X)));
        }
        // The river and the village on it.
        var bend = new[] { (1.0f, -.25f, 18f), (1.45f, -.42f, 26f), (1.85f, -.66f, 34f), (2.2f, -.95f, 42f) };
        foreach (var (x, y, turn) in bend)
            Piece("River" + x, new(.56f, .1f, .02f), new(x, y, .034f), "5f93b8", -turn);
        var houses = new[] { (-1.95f, -.3f, .26f), (-1.6f, -.42f, .3f), (-1.2f, -.33f, .24f), (-2.25f, -.5f, .28f) };
        foreach (var (x, y, size) in houses)
        {
            Piece("House" + x, new(size, size * .62f, .03f), new(x, y, .04f), "efe6d2");
            Piece("Roof" + x, default, new(x, y + size * .31f + size * .18f, .04f), "9a4a3a", 0f, new PrismMesh { Size = new Vector3(size * 1.2f, size * .36f, .03f) });
        }
        // The forest along the horizon — and it has come down further on the right than a forest should.
        var rng = new RandomNumberGenerator { Seed = 0x50414e31 };
        for (var x = -width * .5f + .12f; x < width * .5f - .08f; x += rng.RandfRange(.2f, .3f))
        {
            var edge = Mathf.Abs(x) / (width * .5f);
            var tall = Mathf.Lerp(.34f, .86f, edge * edge) + rng.RandfRange(-.06f, .1f);
            var foot = horizon - (x > .9f ? (x - .9f) * .34f : 0f);
            Piece("Tree" + x, default, new(x, foot + tall * .5f - .02f, .046f), rng.Randf() < .5f ? "1f3a2d" : "27483a", 0f,
                new PrismMesh { Size = new Vector3(rng.RandfRange(.26f, .36f), tall, .03f) });
        }
        // One of them stands apart, out in the grain. Two pale chips near the top never took the dye.
        var lone = new Vector3(1.62f, -.18f, .052f);
        Piece("LoneTree", default, lone + new Vector3(0, .66f, 0), "16291f", 0f, new PrismMesh { Size = new Vector3(.2f, 1.5f, .03f) });
        Piece("LoneChipA", new(.022f, .022f, .01f), lone + new Vector3(-.022f, 1.03f, .018f), "d9d2b8");
        Piece("LoneChipB", new(.022f, .022f, .01f), lone + new Vector3(.024f, 1.03f, .018f), "d9d2b8");
    }

    // ---- Accounts and the cashier (ground floor, east) ------------------------------------------------

    private void BuildOfficeAccounts(Node3D room, StaticBody3D body, OfficeBox B, OfficeSolid Solid, OfficeRound Round, OfficeLamp Lamp,
        OfficePlant Geranium, float part, float ix, float iz, float fh)
    {
        var x0 = part + .08f;
        var wood = Mat("7d6548", "wood_furniture"); var darkWood = Mat("5a4433", "wood_furniture");
        var steel = Mat("5f6668", "metal"); var paper = Mat("e9e2cf", "paper"); var brass = Mat("b08d57", "metal");
        var green = Mat("2f4b3c", "metal");
        AddOfficeRoomLights(Lamp, (x0 + ix) * .5f, fh - .62f);
        // The cashier's place, facing the window to the hall.
        SDesk(room, body, "CashierDesk", new(x0 + .55f, 0, .9f), 90, 1.2f, .62f);
        SChair(room, "CashierChair", new(x0 + 1.2f, 0, .9f), -90);
        Solid("CashBox", new(.3f, .1f, .22f), new(x0 + .5f, .8f, 1.25f), green);
        B("CashBoxLid", new(.3f, .012f, .22f), new(x0 + .5f, .93f, 1.36f), green, new Vector3(-62f, 0, 0));
        for (var stack = 0; stack < 5; stack++)
            Round("CoinStack" + stack, .012f, .02f + stack % 3 * .012f, new(x0 + .22f, .76f + (.02f + stack % 3 * .012f) * .5f, .62f + stack * .035f), brass);
        // The abacus: ten wires, the beads pushed as the last sum left them.
        var abacus = new Vector3(x0 + .55f, .765f, .55f);
        B("AbacusFrame", new(.26f, .02f, .36f), abacus, darkWood);
        for (var wire = 0; wire < 8; wire++)
        {
            var z = abacus.Z - .15f + wire * .043f;
            var split = .04f + (wire * 37 % 5) * .03f;
            B("AbacusBeadsLeft" + wire, new(split, .022f, .026f), new(abacus.X - .12f + split * .5f, abacus.Y + .02f, z), wire % 4 == 1 ? Mat("1e1c1a", "wood") : wood);
            B("AbacusBeadsRight" + wire, new(.2f - split, .022f, .026f), new(abacus.X + .12f - (.2f - split) * .5f, abacus.Y + .02f, z), wood);
        }
        // Two accountants' desks under the east windows, ledgers squared to the edge.
        foreach (var (z, tag) in new[] { (-2f, "A"), (2f, "B") })
        {
            SDesk(room, body, "AccountDesk" + tag, new(ix - .5f, 0, z), 90, 1.3f, .68f);
            SChair(room, "AccountChair" + tag, new(ix - 1.2f, 0, z), 90);
            for (var book = 0; book < 4; book++)
                B($"Ledger{tag}{book}", new(.24f, .035f, .33f), new(ix - .42f, .775f + book * .037f, z + .36f), Mat(book % 2 == 0 ? "3f5f4a" : "6b3b34", "cloth"), new Vector3(0, book * 3f - 4f, 0));
            B("OpenLedger" + tag, new(.4f, .012f, .3f), new(ix - .55f, .762f, z - .1f), paper);
            Geranium("AccountsGeranium" + tag, new(ix + .08f, .9f, z));
            RuralPropModels.Radiator(room, "AccountsRadiator" + tag, new(ix - .19f, .13f, z), 1.0f, 90);
        }
        // The green-shaded lamp is on.
        var lampAt = new Vector3(ix - .3f, .76f, -2.42f);
        Round("DeskLampFoot", .07f, .02f, lampAt + Vector3.Up * .01f, brass);
        Round("DeskLampStem", .01f, .3f, lampAt + Vector3.Up * .16f, brass);
        B("DeskLampShade", new(.26f, .07f, .12f), lampAt + new Vector3(-.06f, .33f, 0), OfficeGlow("2f6b4a", .5f));
        room.AddChild(new OmniLight3D { Name = "DeskLampGlow", Position = lampAt + new Vector3(-.1f, .26f, 0), LightColor = new Color(1f, .9f, .66f),
            LightEnergy = .55f, OmniRange = 2.2f, ShadowEnabled = false });
        SquareLook(room, "OfficeLedger", "Прочитать ведомость", new(ix - .55f, .85f, -2.1f), new(.7f, .4f, .7f),
            "Ведомость по трудодням. Последняя графа у всех одна: «выбыл». У троих вместо причины — прочерк, и прочерки сделаны другими чернилами.");
        // The safe stands open.
        var safe = new Vector3(ix - .4f, 0, -iz + .42f);
        Solid("SafeBack", new(.7f, 1.2f, .04f), safe + new Vector3(0, .6f, -.28f), green);
        Solid("SafeLeft", new(.04f, 1.2f, .6f), safe + new Vector3(-.33f, .6f, 0), green);
        Solid("SafeRight", new(.04f, 1.2f, .6f), safe + new Vector3(.33f, .6f, 0), green);
        Solid("SafeTop", new(.7f, .04f, .6f), safe + new Vector3(0, 1.18f, 0), green);
        B("SafeShelf", new(.62f, .02f, .5f), safe + new Vector3(0, .62f, -.02f), steel);
        B("SafeDoor", new(.66f, 1.14f, .05f), safe + new Vector3(-.58f, .6f, .5f), green, new Vector3(0, -68f, 0));
        Round("SafeWheel", .09f, .02f, safe + new Vector3(-.66f, .66f, .56f), brass, new Vector3(90, -68f, 0));
        B("SafeReceipt", new(.2f, .004f, .1f), safe + new Vector3(.04f, .634f, .06f), paper, new Vector3(0, 14f, 0));
        SquareLook(room, "OfficeSafe", "Заглянуть в сейф", safe + new Vector3(-.1f, .75f, .35f), new(.9f, 1.2f, .8f),
            "Сейф не заперт и пуст. На полке одна квитанция — за электричество, оплачена в этом месяце. Кто-то до сих пор платит за свет в закрытой конторе.");
        SShelf(room, body, "AccountsArchive", new(x0 + 1.35f, 0, -iz + .18f), 0, 2.2f, 2.05f, .32f,
            new[] { "6f6a5c", "5c6659", "7a705e", "4f584f", "837a66", "5f5a4e" });
        Geranium("AccountsGeraniumFront", new(5.4f, .9f, iz + .08f));
    }

    private static void AddOfficeRoomLights(OfficeLamp lamp, float x, float y)
    {
        lamp(new(x, y, -2.1f), .62f, 5f, "ffcf9a");
        lamp(new(x, y, 2.1f), .62f, 5f, "ffcf9a");
    }

    // ---- The red corner (ground floor, west) ----------------------------------------------------------

    private void BuildOfficeRedCorner(Node3D room, StaticBody3D body, OfficeBox B, OfficeSolid Solid, OfficeRound Round, OfficeBall Ball,
        OfficeLamp Lamp, OfficePlant Geranium, float part, float ix, float iz, float fh)
    {
        var x1 = -part - .08f; var centre = (x1 - ix) * .5f;
        var red = Mat("8f2a25", "fabric"); var darkWood = Mat("5a4433", "wood_furniture"); var paper = Mat("e9e2cf", "paper");
        var brass = Mat("b08d57", "metal");
        AddOfficeRoomLights(Lamp, centre, fh - .62f);
        // The presidium table under its red cloth, at the front of the room.
        SDesk(room, body, "PresidiumTable", new(centre, 0, iz - 1.0f), 0, 2.4f, .7f);
        B("PresidiumCloth", new(2.5f, .012f, .8f), new(centre, .75f, iz - 1.0f), red);
        B("PresidiumClothFall", new(2.5f, .46f, .012f), new(centre, .52f, iz - 1.4f), red);
        var glass = new StandardMaterial3D { AlbedoColor = new Color(.78f, .88f, .9f, .38f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = .08f };
        Round("Carafe", .05f, .2f, new(centre - .3f, .86f, iz - 1.0f), glass, top: .028f);
        Round("CarafeWater", .044f, .1f, new(centre - .3f, .815f, iz - 1.0f), Mat("9fc2d6", "plaster"));
        foreach (var x in new[] { -.14f, -.02f })
            Round("Tumbler" + x, .028f, .08f, new(centre + x, .8f, iz - .9f), glass);
        for (var seat = 0; seat < 3; seat++)
            SChair(room, "PresidiumChair" + seat, new(centre - .8f + seat * .8f, 0, iz - .45f), 180);
        // Rows for the meeting. One chair has been turned to face the window.
        for (var row = 0; row < 3; row++)
        for (var seat = 0; seat < 4; seat++)
        {
            if (row == 2 && seat == 0) continue;
            var turned = row == 1 && seat == 3;
            SChair(room, $"MeetingChair{row}_{seat}", new(-ix + .95f + seat * .68f, 0, 1.4f - row * .95f), turned ? 100 : 0);
        }
        // The honour board: the photographs were taken down, the plates were left.
        var board = new Vector3(-ix + .03f, 1.75f, 0f);
        B("HonourBoard", new(.04f, 1.25f, 2.56f), board, Mat("7a2f2a", "fabric"));
        B("HonourBoardRailTop", new(.055f, .05f, 2.64f), board + new Vector3(.005f, .65f, 0), darkWood);
        B("HonourBoardRailBottom", new(.055f, .05f, 2.64f), board + new Vector3(.005f, -.65f, 0), darkWood);
        for (var row = 0; row < 2; row++)
        for (var column = 0; column < 5; column++)
        {
            var at = board + new Vector3(.024f, .26f - row * .56f, -1.0f + column * .5f);
            B($"HonourPatch{row}_{column}", new(.004f, .34f, .26f), at, Mat("a84a42", "fabric"));
            B($"HonourPlate{row}_{column}", new(.006f, .045f, .22f), at + new Vector3(.002f, -.21f, 0), brass);
        }
        OfficeLabel(room, "ДОСКА ПОЧЁТА", new(-ix + .012f, 2.56f, 0f), 90, 0.1f);
        // Under the last plate, a shelf, a glass, one carnation. The water is clear.
        var shelf = board + new Vector3(.1f, -.74f, 1.0f);
        B("CarnationShelf", new(.16f, .02f, .24f), shelf, darkWood);
        Round("CarnationGlass", .03f, .1f, shelf + new Vector3(0, .06f, 0), glass);
        RuralPropGeometry.Tube(room, "CarnationStem", shelf + new Vector3(0, .02f, 0), shelf + new Vector3(.02f, .34f, .03f), .004f, Mat("3f6b3a", "fabric"), 6);
        Ball("CarnationHead", .032f, shelf + new Vector3(.02f, .35f, .03f), Mat("c2372f", "fabric"), new Vector3(1f, .8f, 1f));
        SquareLook(room, "OfficeHonourBoard", "Осмотреть доску почёта", board + new Vector3(.35f, -.1f, 0), new(.7f, 1.5f, 2.8f),
            "Фотографии сняли — на сукне остались тёмные прямоугольники и латунные таблички. Под крайней стоит стакан с гвоздикой. Вода чистая, цветок свежий.");
        // The banner, rolled and stood in the corner.
        var pole = new Vector3(-ix + .25f, 0, iz - .25f);
        RuralPropGeometry.Tube(room, "BannerPole", pole, pole + new Vector3(.12f, 2.5f, -.1f), .018f, darkWood, 8);
        SCurtain(room, "BannerCloth", pole + new Vector3(.1f, 1.75f, -.06f), .34f, 1.3f, red);
        Round("BannerFinial", .03f, .12f, pole + new Vector3(.123f, 2.56f, -.103f), brass, top: .004f);
        // An empty plinth: whatever stood here was carried out carefully.
        Solid("Plinth", new(.46f, 1.05f, .46f), new(x1 - .4f, .525f, iz - .4f), Mat("d9d2c0", "plaster"));
        B("PlinthCap", new(.52f, .05f, .52f), new(x1 - .4f, 1.075f, iz - .4f), Mat("c9bfa9", "plaster"));
        foreach (var z in new[] { -2f, 2f })
        {
            Geranium("RedCornerGeranium" + z, new(-ix - .08f, .9f, z));
            RuralPropModels.Radiator(room, "RedCornerRadiator" + z, new(-ix + .19f, .13f, z), 1.0f, 90);
        }
        Geranium("RedCornerGeraniumFront", new(-5.4f, .9f, iz + .08f));
    }

    // ---- The director's room (upper floor, west) ------------------------------------------------------

    private void BuildOfficeDirector(Node3D room, StaticBody3D body, OfficeBox B, OfficeSolid Solid, OfficeRound Round, OfficeBall Ball,
        OfficeLamp Lamp, OfficePlant Geranium, float part, float ix, float iz, float fh)
    {
        var x1 = -part - .08f; var centre = (x1 - ix) * .5f;
        var darkWood = Mat("5a4433", "wood_furniture"); var paper = Mat("e9e2cf", "paper"); var brass = Mat("b08d57", "metal");
        var black = Mat("1e1c1a", "plastic_abs");
        AddOfficeRoomLights(Lamp, centre, fh * 2f - .46f);
        B("Runner", new(1.0f, .012f, 5.6f), new(centre, fh + .008f, -.6f), Mat("7a2f2a", "fabric"));
        B("RunnerEdgeL", new(.08f, .013f, 5.6f), new(centre - .44f, fh + .009f, -.6f), Mat("3f5f4a", "fabric"));
        B("RunnerEdgeR", new(.08f, .013f, 5.6f), new(centre + .44f, fh + .009f, -.6f), Mat("3f5f4a", "fabric"));
        // The desk across the room, the long table for those summoned set against it.
        SDesk(room, body, "DirectorDesk", new(centre, fh, 2.5f), 0, 1.9f, .85f);
        SChair(room, "DirectorChair", new(centre, fh, 3.25f), 180);
        SDesk(room, body, "MeetingTable", new(centre, fh, .95f), 90, 2.2f, .8f);
        for (var seat = 0; seat < 3; seat++)
        {
            SChair(room, "MeetingWest" + seat, new(centre - .75f, fh, .25f + seat * .7f), -90);
            SChair(room, "MeetingEast" + seat, new(centre + .75f, fh, .25f + seat * .7f), 90);
        }
        var top = fh + .76f;
        // The lamp is on.
        var lampAt = new Vector3(centre - .7f, top, 2.45f);
        Round("DirectorLampFoot", .08f, .02f, lampAt + Vector3.Up * .01f, brass);
        Round("DirectorLampStem", .011f, .34f, lampAt + Vector3.Up * .18f, brass);
        B("DirectorLampShade", new(.3f, .08f, .14f), lampAt + new Vector3(.06f, .37f, 0), OfficeGlow("2f6b4a", .55f));
        room.AddChild(new OmniLight3D { Name = "DirectorLampGlow", Position = lampAt + new Vector3(.12f, .28f, 0), LightColor = new Color(1f, .9f, .66f),
            LightEnergy = .7f, OmniRange = 2.6f, ShadowEnabled = false });
        // The telephone, the calendar, and the tea.
        B("PhoneBase", new(.2f, .07f, .16f), new(centre + .65f, top + .035f, 2.4f), black);
        B("PhoneHandset", new(.22f, .035f, .05f), new(centre + .65f, top + .09f, 2.4f), black);
        Round("PhoneDial", .045f, .012f, new(centre + .65f, top + .075f, 2.33f), Mat("d8d2c4", "plastic_abs"), new Vector3(70, 0, 0));
        B("CalendarBlock", new(.09f, .1f, .03f), new(centre + .25f, top + .05f, 2.2f), paper, new Vector3(-14f, 0, 0));
        B("OpenFolder", new(.44f, .008f, .32f), new(centre - .05f, top + .004f, 2.5f), Mat("b9a77c", "paper"));
        B("FolderSheet", new(.2f, .004f, .29f), new(centre - .15f, top + .01f, 2.5f), paper, new Vector3(0, 2.5f, 0));
        var tea = new Vector3(centre + .3f, top, 2.62f);
        Round("TeaHolder", .036f, .075f, tea + Vector3.Up * .04f, brass);
        Round("TeaGlass", .031f, .1f, tea + Vector3.Up * .05f, new StandardMaterial3D { AlbedoColor = new Color(.62f, .3f, .1f, .8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = .1f });
        room.AddChild(new CpuParticles3D
        {
            Name = "TeaSteam", Position = tea + Vector3.Up * .11f, Amount = 10, Lifetime = 2.2f, Direction = Vector3.Up, Spread = 12f,
            InitialVelocityMin = .05f, InitialVelocityMax = .1f, Gravity = new Vector3(.01f, .03f, 0), ScaleAmountMin = .6f, ScaleAmountMax = 1.3f,
            Mesh = new QuadMesh
            {
                Size = new Vector2(.05f, .05f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 1f, 1f, .1f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                }
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        SquareLook(room, "OfficeDirectorDesk", "Осмотреть стол", new(centre, top + .2f, 2.5f), new(1.9f, .6f, .9f),
            "Лампа включена. Чай в стакане горячий, от него идёт пар. В перекидном календаре — сегодняшнее число. А на стуле пыль, ровная, как снег: на него давно никто не садился.");
        // Where the portrait hung, the paint kept its colour.
        B("PortraitGhost", new(.62f, .82f, .004f), new(-3.95f, fh + 2.05f, iz - .004f), Mat("f3ecd6", "plaster"));
        Round("PortraitNail", .004f, .03f, new(-3.95f, fh + 2.52f, iz - .02f), Mat("3a3a3a", "metal"), new Vector3(90, 0, 0));
        // The map of the sovkhoz lands, between the west windows.
        BuildOfficeLandsMap(room, new Vector3(-ix + .014f, fh + 1.72f, 0));
        SquareLook(room, "OfficeLandsMap", "Рассмотреть карту", new(-ix + .4f, fh + 1.6f, 0), new(.8f, 1.7f, 2.5f),
            "Карта земель совхоза. Карандашом, год за годом, отмечен край леса: 1987, 1996, 2004, 2012, 2019. Каждая линия ближе к деревне. Последняя — красная, без года — идёт уже по огородам.");
        // Grain samples by year, on a shelf along the hall wall.
        var shelfX = x1 - .14f;
        B("SampleShelf", new(.22f, .03f, 2.3f), new(shelfX, fh + 1.3f, 1.6f), darkWood);
        foreach (var z in new[] { .6f, 2.6f })
            B("SampleBracket" + z, new(.2f, .16f, .03f), new(shelfX, fh + 1.21f, z), darkWood);
        var jar = new StandardMaterial3D { AlbedoColor = new Color(.82f, .9f, .9f, .32f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = .08f };
        for (var sample = 0; sample < 7; sample++)
        {
            var at = new Vector3(shelfX, fh + 1.315f, .68f + sample * .3f);
            Round("SampleJar" + sample, .06f, .2f, at + Vector3.Up * .1f, jar);
            Round("SampleLid" + sample, .062f, .014f, at + Vector3.Up * .207f, brass);
            if (sample < 6) Round("SampleGrain" + sample, .054f, .15f - sample * .018f, at + Vector3.Up * ((.15f - sample * .018f) * .5f + .004f), Mat(sample % 2 == 0 ? "c99a34" : "b98a2e", "plaster"));
            B("SampleTag" + sample, new(.004f, .04f, .07f), at + new Vector3(.062f, .07f, 0), paper);
        }
        SquareLook(room, "OfficeGrainSamples", "Осмотреть банки", new(shelfX + .2f, fh + 1.4f, 1.6f), new(.6f, .5f, 2.3f),
            "Образцы зерна по годам. В каждой банке меньше, чем в предыдущей. Последняя пустая, но бирка на ней уже есть — на этот год.");
        Geranium("DirectorGeraniumFront", new(-5.4f, fh + .9f, iz + .08f));
        foreach (var z in new[] { -2f, 2f })
            RuralPropModels.Radiator(room, "DirectorRadiator" + z, new(-ix + .19f, fh + .13f, z), 1.0f, 90);
        SShelf(room, body, "DirectorBookcase", new(centre, fh, -iz + .18f), 0, 2.0f, 2.1f, .32f,
            new[] { "6b3b34", "3f5f4a", "7a705e", "4a4f63", "837a66", "8a3b34" });
    }

    /// <summary>The lands map: plots, the village, the river, and the forest's edge pencilled in year after year.</summary>
    private static void BuildOfficeLandsMap(Node3D room, Vector3 at)
    {
        var map = new Node3D { Name = "LandsMap", Position = at, RotationDegrees = new Vector3(0, 90, 0) };
        room.AddChild(map);
        const float width = 2.3f, height = 1.5f;
        void Piece(string name, Vector3 size, Vector3 position, string color, float turn = 0f)
        {
            map.AddChild(new MeshInstance3D
            {
                Name = name, Mesh = new BoxMesh { Size = size }, Position = position, RotationDegrees = new Vector3(0, 0, turn),
                MaterialOverride = Mat(color, "paper"), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        foreach (var (name, size, position) in new[]
        {
            ("FrameTop", new Vector3(width + .1f, .05f, .03f), new Vector3(0, height * .5f + .025f, .012f)),
            ("FrameBottom", new Vector3(width + .1f, .05f, .03f), new Vector3(0, -height * .5f - .025f, .012f)),
            ("FrameLeft", new Vector3(.05f, height, .03f), new Vector3(-width * .5f - .025f, 0, .012f)),
            ("FrameRight", new Vector3(.05f, height, .03f), new Vector3(width * .5f + .025f, 0, .012f))
        })
            Piece(name, size, position, "5a4433");
        if (OfficeTextureSlot("lands_map") is { } picture)
        {
            map.AddChild(new MeshInstance3D
            {
                Name = "Picture", Mesh = new QuadMesh { Size = new Vector2(width, height) }, Position = new Vector3(0, 0, .006f),
                MaterialOverride = new StandardMaterial3D { AlbedoTexture = picture, Roughness = .9f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
            return;
        }
        Piece("Sheet", new(width, height, .006f), new(0, 0, .003f), "e6dcc0");
        // Plots, tinted by crop as the agronomist's office tinted them.
        var plots = new[]
        {
            (-.78f, .2f, .62f, .4f, "d8c98f"), (-.1f, .24f, .66f, .34f, "c9d3a0"), (.62f, .2f, .7f, .4f, "e0c9a0"),
            (-.82f, -.2f, .56f, .34f, "c9d3a0"), (.7f, -.22f, .56f, .36f, "d8c98f"), (-.6f, -.56f, .9f, .3f, "e0c9a0"), (.55f, -.58f, .86f, .26f, "c9d3a0")
        };
        foreach (var (x, y, w, h, color) in plots)
            Piece("Plot" + x + y, new(w, h, .004f), new(x, y, .008f), color);
        // The forest holds the whole top of the sheet.
        Piece("Forest", new(width, .32f, .004f), new(0, height * .5f - .16f, .009f), "4f6b4d");
        // The river and the village in the middle of the sheet.
        foreach (var (x, y, turn) in new[] { (-1.0f, -.42f, 12f), (-.62f, -.36f, 6f), (-.24f, -.34f, -2f), (.14f, -.38f, -12f), (.5f, -.46f, -8f), (.9f, -.5f, -3f) })
            Piece("River" + x, new(.42f, .03f, .004f), new(x, y, .011f), "6f9fc0", turn);
        var rng = new RandomNumberGenerator { Seed = 0x4d415031 };
        for (var house = 0; house < 26; house++)
            Piece("House" + house, new(.035f, .028f, .004f), new(rng.RandfRange(-.36f, .34f), rng.RandfRange(-.3f, -.02f), .012f), "5a4433", rng.RandfRange(-12f, 12f));
        // The pencil lines. Each is the edge of the forest in its year.
        var years = new[] { ("1987", .5f), ("1996", .4f), ("2004", .29f), ("2012", .17f), ("2019", .06f) };
        foreach (var (year, y) in years)
        {
            for (var part = 0; part < 6; part++)
                Piece($"Edge{year}_{part}", new(.37f, .006f, .003f), new(-.92f + part * .365f, y + .02f * Mathf.Sin(part * 1.7f + y * 20f), .014f), "3a3a3a",
                    6f * Mathf.Sin(part * 2.3f + y * 31f));
            OfficeLabel(map, year, new Vector3(width * .5f - .13f, y + .04f, .012f), 0, .045f, plate: false, hand: true);
        }
        for (var part = 0; part < 6; part++)
            Piece("EdgeNow" + part, new(.37f, .011f, .003f), new(-.92f + part * .365f, -.1f + .02f * Mathf.Sin(part * 2.1f), .016f), "b0302a",
                7f * Mathf.Sin(part * 1.9f + 1f));
    }

    // ---- The radio room (upper floor, east) -----------------------------------------------------------

    private void BuildOfficeRadioRoom(Node3D room, StaticBody3D body, OfficeBox B, OfficeSolid Solid, OfficeRound Round, OfficeBall Ball,
        OfficeLamp Lamp, OfficePlant Geranium, float part, float ix, float iz, float fh)
    {
        var x0 = part + .08f; var centre = (x0 + ix) * .5f;
        var darkWood = Mat("5a4433", "wood_furniture"); var paper = Mat("e9e2cf", "paper"); var brass = Mat("b08d57", "metal");
        var cabinet = Mat("4a554f", "metal"); var black = Mat("1e1c1a", "plastic_abs"); var steel = Mat("8a9092", "metal");
        var amber = OfficeGlow("ffb347", 2.2f); var redLamp = OfficeGlow("ff4030", 2.8f); var dead = Mat("5a3a30", "plastic_abs");
        Lamp(new(centre, fh * 2f - .46f, -2.4f), .4f, 4.5f, "ffcf9a");
        // Three cabinets of the broadcast rack between the east windows. They are warm.
        for (var unit = 0; unit < 3; unit++)
        {
            var z = -.66f + unit * .66f;
            Solid("Rack" + unit, new(.45f, 1.9f, .62f), new(ix - .24f, fh + .95f, z), cabinet);
            var face = ix - .47f;
            for (var row = 0; row < 3; row++)
            {
                var y = fh + 1.62f - row * .46f;
                B($"RackPanel{unit}_{row}", new(.006f, .38f, .54f), new(face, y, z), Mat("3c4641", "metal"));
                Round($"RackDial{unit}_{row}", .07f, .012f, new(face - .008f, y + .04f, z - .1f), Mat("d8d2c4", "plastic_abs"), new Vector3(0, 0, 90));
                B($"RackNeedle{unit}_{row}", new(.004f, .06f, .006f), new(face - .017f, y + .06f, z - .1f), Mat("b0302a", "metal"), new Vector3(18f + (unit * 3 + row) * 23f % 70f, 0, 0));
                for (var lamp = 0; lamp < 3; lamp++)
                    Ball($"RackLamp{unit}_{row}_{lamp}", .014f, new(face - .01f, y - .1f, z + .06f + lamp * .06f), (unit + row + lamp) % 3 == 0 ? dead : amber);
                Round($"RackKnob{unit}_{row}", .022f, .03f, new(face - .018f, y + .05f, z + .14f), black, new Vector3(0, 0, 90));
            }
        }
        room.AddChild(new OmniLight3D { Name = "RackGlow", Position = new(ix - .9f, fh + 1.2f, 0), LightColor = new Color(1f, .62f, .25f),
            LightEnergy = .8f, OmniRange = 3.4f, ShadowEnabled = false });
        // The operator's desk: microphone, headphones, a tape that is turning.
        SDesk(room, body, "OperatorDesk", new(ix - 1.35f, fh, 0), 90, 1.5f, .7f);
        SChair(room, "OperatorChair", new(ix - 2.05f, fh, .1f), 90);
        var top = fh + .76f;
        var mic = new Vector3(ix - 1.2f, top, -.45f);
        Round("MicFoot", .06f, .02f, mic + Vector3.Up * .01f, black);
        Round("MicStem", .008f, .22f, mic + Vector3.Up * .12f, steel);
        Round("MicHead", .026f, .08f, mic + new Vector3(-.03f, .25f, 0), steel, new Vector3(0, 0, 62f));
        var deck = new Vector3(ix - 1.3f, top, .32f);
        B("TapeDeck", new(.42f, .12f, .36f), deck + Vector3.Up * .06f, Mat("6f6a5c", "metal"));
        var reels = new[] { Round("ReelSupply", .085f, .014f, deck + new Vector3(0, .13f, -.09f), steel), Round("ReelTake", .085f, .014f, deck + new Vector3(0, .13f, .09f), steel) };
        foreach (var reel in reels)
        {
            var spoke = new MeshInstance3D { Name = "Spokes", Mesh = new BoxMesh { Size = new Vector3(.15f, .016f, .02f) }, MaterialOverride = black,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            reel.AddChild(spoke);
            reel.AddChild(new MeshInstance3D { Name = "SpokesAcross", Mesh = new BoxMesh { Size = new Vector3(.02f, .016f, .15f) }, MaterialOverride = black,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            // The building is assembled before it enters the tree; the reels start once it is in.
            reel.TreeEntered += () => reel.CreateTween().SetLoops().TweenProperty(reel, "rotation:y", Mathf.Tau, 5.5).AsRelative();
        }
        Ball("DeckLamp", .012f, deck + new Vector3(-.2f, .09f, -.14f), redLamp);
        Round("HeadphoneL", .04f, .025f, new(ix - 1.5f, top + .03f, -.12f), black, new Vector3(0, 0, 90));
        Round("HeadphoneR", .04f, .025f, new(ix - 1.38f, top + .03f, -.12f), black, new Vector3(0, 0, 90));
        B("HeadphoneBand", new(.14f, .012f, .02f), new(ix - 1.44f, top + .075f, -.12f), black);
        SquareLook(room, "OfficeTapeDeck", "Послушать", deck + new Vector3(-.1f, .2f, 0), new(.8f, .5f, .9f),
            "Аппаратура тёплая, лампы горят. Катушки крутятся: идёт запись. Или передача. В наушниках — тишина, но не пустая: так молчит открытая линия.", "forest/radio_evp");
        // The switchboard on the hall wall: four lines for the village and a fifth.
        var board = new Vector3(x0 + .02f, fh + 1.5f, 0);
        B("Switchboard", new(.04f, .7f, 1.5f), board, cabinet);
        var lines = new[] { "ШКОЛА", "КЛУБ", "ФЕРМА", "ПЛОЩАДЬ" };
        for (var line = 0; line < 5; line++)
        {
            var z = -.56f + line * .28f;
            var live = line == 4;
            B("Toggle" + line, new(.03f, .07f, .016f), board + new Vector3(.035f, -.02f, z), steel, new Vector3(0, 0, live ? -28f : 28f));
            Ball("LineLamp" + line, .016f, board + new Vector3(.03f, .2f, z), live ? redLamp : dead);
            if (!live) OfficeLabel(room, lines[line], board + new Vector3(.024f, -.2f, z), 90, 0.036f);
        }
        // The fifth was never engraved. Somebody wrote it on a strip of plaster and stuck it on.
        B("HandwrittenStrip", new(.004f, .05f, .2f), board + new Vector3(.024f, -.2f, .56f), paper, new Vector3(3f, 0, 0));
        OfficeLabel(room, "ЛЕС", board + new Vector3(.027f, -.2f, .56f), 90, .05f, plate: false, hand: true);
        SquareLook(room, "OfficeSwitchboard", "Осмотреть щиток", board + new Vector3(.3f, 0, 0), new(.6f, .9f, 1.6f),
            "Щиток трансляции: «ШКОЛА», «КЛУБ», «ФЕРМА», «ПЛОЩАДЬ». Пятая линия подписана от руки на пластыре: «ЛЕС». Только её тумблер стоит в положении «вкл».", "forest/radio_evp");
        // A horn loudspeaker on its bracket, turned to the window.
        var horn = new Vector3(ix - .5f, fh + 2.35f, 2.9f);
        Round("Horn", .05f, .34f, horn, steel, new Vector3(0, 0, 90), top: .2f);
        B("HornBracket", new(.3f, .03f, .03f), horn + new Vector3(.2f, -.12f, 0), black);
        // A cot against the back wall. The blanket is folded; the pillow is dented.
        var cot = new Vector3(centre + .2f, fh, -iz + .52f);
        Solid("CotFrame", new(1.9f, .3f, .78f), cot + Vector3.Up * .2f, steel);
        B("CotMattress", new(1.86f, .1f, .74f), cot + Vector3.Up * .4f, Mat("8c8474", "fabric"));
        B("CotBlanket", new(.7f, .08f, .7f), cot + new Vector3(.5f, .49f, 0), Mat("5c6b7a", "fabric"));
        Ball("CotPillow", .2f, cot + new Vector3(-.68f, .5f, 0), Mat("e3ddd0", "fabric"), new Vector3(1.1f, .35f, 1.5f));
        var flask = new Vector3(x0 + .35f, fh, -iz + .35f);
        Solid("Stool", new(.34f, .44f, .34f), flask + Vector3.Up * .22f, darkWood);
        Round("Thermos", .045f, .28f, flask + new Vector3(0, .58f, 0), Mat("7a2f2a", "metal"));
        Round("ThermosCup", .04f, .06f, flask + new Vector3(.1f, .47f, .06f), steel);
        SquareLook(room, "OfficeCot", "Осмотреть раскладушку", cot + new Vector3(0, .6f, .3f), new(1.9f, .7f, 1f),
            "Раскладушка. Одеяло сложено по-армейски, на подушке — вмятина от головы. Кто-то здесь ночует. И, судя по термосу, не первую зиму.");
        Geranium("RadioGeraniumFront", new(5.4f, fh + .9f, iz + .08f));
        foreach (var z in new[] { -2f, 2f })
            RuralPropModels.Radiator(room, "RadioRadiator" + z, new(ix - .19f, fh + .13f, z > 0 ? 2.75f : -2.75f), .7f, 90);
    }
}
