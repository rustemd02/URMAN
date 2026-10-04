using System;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Settlement north is +Z (NorthContinuousWinterRoads), east is +X.
    // Fictional Kara-Urman uses Kazan as a regional proxy, not a claimed real village location.
    // Initial great-circle bearing: atan2(sin Δλ cos φ2,
    // cos φ1 sin φ2 − sin φ1 cos φ2 cos Δλ), Kazan 55.79/49.12 → Kaaba 21.4225/39.8262.
    internal const float MosqueQiblaBearingDegrees = 195.1725f;
    private Vector3 MosqueLocalQibla => _mosqueRoom!.GlobalBasis.Inverse() *
        new Vector3(Mathf.Sin(Mathf.DegToRad(MosqueQiblaBearingDegrees)), 0,
            Mathf.Cos(Mathf.DegToRad(MosqueQiblaBearingDegrees)));
    private Node3D? _mosqueAdhanAnchor;

    private void BuildMosqueDecoratedCeiling()
    {
        var room = _mosqueRoom!;
        // Actual stair opening: no opaque ceiling across the climb into the roof minaret.
        AddVisualBox(room, "MosqueCeiling", new(9.1f, .12f, 9.15f), new(-2.075f, 3.80f, 0), "e5dfc9", "wood_painted_trim");
        AddVisualBox(room, "MosqueVestibuleSouthCeiling", new(2.75f, .12f, 4.20f), new(3.85f, 3.80f, 2.47f), "e5dfc9", "wood_painted_trim");
        foreach (var x in new[] { -5.9f, -3.8f, -1.7f, .4f })
            AddVisualBox(room, "MosqueCeilingTimberBeam" + x, new(.12f, .16f, 9.10f), new(x, 3.68f, 0), "946f45", "wood_furniture");
        AddVisualBox(room, "MosqueVestibuleNorthCeiling", new(2.75f, .12f, 1.45f), new(3.85f, 3.80f, -3.85f), "e5dfc9", "wood_painted_trim");
        foreach (var z in new[] { -4.44f, 4.44f })
        {
            AddVisualBox(room, "MosqueCornice" + z, new(11.8f, .13f, .10f), new(-.7f, 3.62f, z), "ddca91", "wood_furniture");
            AddVisualBox(room, "MosqueDado" + z, new(11.8f, .12f, .035f), new(-.7f, .98f, z), "688777", "wood_painted_green");
        }
        AddVisualBox(room, "MosqueWestDado", new(.035f, .12f, 8.95f), new(-6.58f, .98f, 0), "688777", "wood_painted_green");
        // A restrained pendant fixture, warm light and metal arms without invented sacred text.
        var chandelier = new Node3D { Name = "MosqueChandelier", Position = new(-.5f, 3.35f, -.9f) };
        room.AddChild(chandelier);
        FacilityRod(chandelier, "Suspension", Vector3.Zero, new(0, .36f, 0), .018f, "b7a578");
        for (var i = 0; i < 6; i++)
        {
            var a = Mathf.Tau * i / 6;
            var end = new Vector3(Mathf.Cos(a) * .55f, -.16f, Mathf.Sin(a) * .55f);
            FacilityRod(chandelier, "Arm" + i, Vector3.Zero, end, .014f, "b7a578");
            DiscoveryCylinder(chandelier, "FrostedShade" + i, .075f, .12f, .16f, end, "eadbc0");
        }
    }

    private void BuildMosqueWashCorner()
    {
        var room = _mosqueRoom!;
        // Sink stays on the east wall, clear of the seat/ray at Z2.638 and of every shoe shelf.
        FacilitySolid(room, "MosqueWashCabinet", new(.50f, .79f, .80f), new(4.94f, .395f, 1.60f), "d7dfd2", "wood_painted_trim");
        FacilityVessel(room, "MosqueWashBasin", new(4.91f, .84f, 1.60f), .22f, .10f, "d4dad6", false);
        FacilityRod(room, "MosqueWaterRiser", new(5.15f, .16f, 1.50f), new(5.15f, 1.11f, 1.50f), .018f, "a3aaa8");
        FacilityRod(room, "MosqueTapSpout", new(5.15f, 1.10f, 1.50f), new(4.94f, 1.10f, 1.60f), .016f, "a3aaa8");
        FacilityRod(room, "MosqueBasinDrain", new(4.91f, .77f, 1.60f), new(5.15f, .11f, 1.60f), .027f, "a3aaa8");
        AddVisualBox(room, "MosqueWashTowel", new(.02f, .49f, .33f), new(5.18f, 1.22f, 2.14f), "ded8c2", "cloth_towel");
        FacilityRod(room, "MosqueTowelRail", new(5.15f, 1.48f, 1.93f), new(5.15f, 1.48f, 2.34f), .014f, "898e88");
    }

    private MeshInstance3D BuildMosquePrayerAndLibrary()
    {
        var room = _mosqueRoom!;
        room.SetMeta("worldTrueNorth", new Vector3(0, 0, 1));
        room.SetMeta("worldEast", Vector3.Right);
        room.SetMeta("qiblaRegionalProxy", "Kazan 55.79N 49.12E; fictional village has no surveyed coordinate");
        room.SetMeta("qiblaBearingDegrees", MosqueQiblaBearingDegrees);
        room.SetMeta("qiblaWorldDirection", new Vector3(Mathf.Sin(Mathf.DegToRad(MosqueQiblaBearingDegrees)), 0, Mathf.Cos(Mathf.DegToRad(MosqueQiblaBearingDegrees))));
        // A 2 mm textile lies below the door's 12 mm clearance and its 2 mm sweep margin. Stable support IDs remain.
        var carpet = FacilitySolid(room, "MosquePrayerCarpet", new(5.3f, .002f, 6.05f), new(-.50f, .001f, -.95f), "38655f", "fabric");
        ApplyMosquePrayerCarpet(carpet);
        var qibla = MosqueLocalQibla.Normalized();
        var yaw = Mathf.Atan2(-qibla.X, -qibla.Z); // local -Z is the prayer face
        var prayer = new Node3D { Name = "MosqueQiblaWall", Position = new(-.7f, 0, -4.14f), Rotation = new(0, yaw, 0) };
        room.AddChild(prayer);
        prayer.SetMeta("qiblaBearingDegrees", MosqueQiblaBearingDegrees);
        // Mihrab recess with shaped arch; the whole assembly shares the qibla normal.
        FacilitySolid(prayer, "MihrabBacking", new(1.40f, 2.75f, .16f), new(0, 1.375f, -.06f), "779482", "wood_painted_green");
        foreach (var x in new[] { -.58f, .58f })
            FacilitySolid(prayer, "MihrabPilaster" + x, new(.10f, 1.94f, .16f), new(x, .97f, .09f), "d8c9a0", "wood_furniture");
        for (var i = 0; i < 10; i++)
        {
            var a0 = Mathf.Pi * i / 10; var a1 = Mathf.Pi * (i + 1) / 10;
            var p0 = new Vector3(Mathf.Cos(a0) * .58f, 1.91f + Mathf.Sin(a0) * .68f, .1f);
            var p1 = new Vector3(Mathf.Cos(a1) * .58f, 1.91f + Mathf.Sin(a1) * .68f, .1f);
            FacilityRod(prayer, "MihrabArch" + i, p0, p1, .045f, "d8c9a0");
        }
        // Minbar to the imam's right, reached by actual short steps, without a throne or sacred inscription.
        var minbar = new Node3D { Name = "MosqueMinbar", Position = new(1.05f, 0, .10f) };
        prayer.AddChild(minbar);
        for (var i = 0; i < 3; i++)
            FacilitySolid(minbar, "MinbarTread" + i, new(.70f, .16f * (i + 1), .29f), new(0, .08f * (i + 1), .88f - i * .29f), "946f45", "wood_furniture");
        FacilitySolid(minbar, "MinbarPlatform", new(.78f, .48f, .56f), new(0, .24f, -.1f), "946f45", "wood_furniture");
        foreach (var x in new[] { -.42f, .42f })
        {
            FacilitySolid(minbar, "MinbarPost" + x, new(.055f, 1.10f, .055f), new(x, .97f, -.28f), "cfb97d", "wood_furniture");
            FacilityRod(minbar, "MinbarRail" + x, new(x, 1.50f, -.30f), new(x, .94f, 1.00f), .026f, "cfb97d");
        }
        FacilityLabel(room, "MosqueQiblaNotice", "Кыйбла", new(1.30f, 2.43f, -4.57f), 0, .0015f);

        // A separate west-side library. Its 1.1 m doorway leaves the prayer carpet and entrance route open.
        FacilitySolid(room, "MosqueLibraryEastWall", new(.10f, 3.75f, 2.395f), new(-3.40f, 1.875f, 3.3775f), "dedbc6", "wood_painted_trim");
        FacilitySolid(room, "MosqueLibraryEastJamb", new(.10f, 3.75f, .135f), new(-3.40f, 1.875f, .9375f), "dedbc6", "wood_painted_trim");
        FacilitySolid(room, "MosqueLibraryLintel", new(.10f, 1.53f, 1.15f), new(-3.40f, 2.985f, 1.60f), "dedbc6", "wood_painted_trim");
        FacilitySolid(room, "MosqueLibraryNorthWall", new(3.19f, 3.75f, .10f), new(-4.995f, 1.875f, .82f), "dedbc6", "wood_painted_trim");
        FacilityManualDoor(room, "MosqueLibrary", "mosque/library", new(-3.40f, 0, 1.05f), 1.10f, 2.17f, 0, 90);
        FacilityLabel(room, "MosqueLibrarySign", "Китапханә\nБиблиотека", new(-3.33f, 2.52f, 1.61f), 90, .0014f);
        FacilitySolid(room, "MosqueBookcaseLeft", new(.08f, 1.65f, .39f), new(-6.15f, .825f, 4.23f), "80674c", "wood_furniture");
        FacilitySolid(room, "MosqueBookcaseRight", new(.08f, 1.65f, .39f), new(-4.65f, .825f, 4.23f), "80674c", "wood_furniture");
        for (var shelf = 0; shelf < 4; shelf++)
        {
            FacilitySolid(room, "MosqueBookShelf" + shelf, new(1.60f, .055f, .40f), new(-5.40f, .10f + shelf * .49f, 4.23f), "80674c", "wood_furniture");
            if (shelf == 3) continue;
            for (var book = 0; book < 8; book++)
                AddMosqueShelfBook(room, $"MosqueBook{book}_{shelf}", new(.065f, .31f + book % 3 * .025f, .22f), new(-5.94f + book * .15f, .1275f + shelf * .49f, 4.23f), book % 2 == 0 ? "405d57" : "8f7960");
        }
        FacilityBench(room, "MosqueReadingBench", new(-5.40f, 0, 3.58f), 1.35f, 0);
        FacilityTable(room, "MosqueImamDesk", new(-6.05f, 0, 1.55f), new(.74f, .74f, .80f));
        AddVisualBox(room, "MosqueImamNotebook", new(.29f, .018f, .21f), new(-6.02f, .767f, 1.55f), "bbb297", "paper");
        FacilityLamp(room, "MosqueLibraryLamp", new(-5.1f, 3.35f, 2.5f), "ffdfad", .45f, 3.0f);
        BuildMosqueDonationBox();
        room.SetMeta("libraryClearSizeMetres", new Vector2(3.14f, 3.70f));
        room.SetMeta("hallClearHeightMetres", 3.74f);
        return carpet;
    }

    private void ApplyMosquePrayerCarpet(MeshInstance3D carpet)
    {
        const string path = "res://assets/textures/civic/mosque_prayer_carpet_v2_albedo.png";
        // Explicit consumer UVs keep floral/arch heads pointing toward Mecca, independent of the building rotation.
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var box = (BoxMesh)carpet.Mesh;
        var left = -box.Size.X * .5f; var right = -left;
        var near = -box.Size.Z * .5f; var far = -near;
        var top = box.Size.Y * .5f + .0004f;
        var q = MosqueLocalQibla.Normalized(); var u = new Vector3(-q.Z, 0, q.X);
        var a = new Vector3(left, top, near); var b = new Vector3(right, top, near);
        var c = new Vector3(right, top, far); var d = new Vector3(left, top, far);
        foreach (var p in new[] { a, b, c, a, c, d })
        {
            surface.SetNormal(Vector3.Up);
            // Image upward means decreasing V in texture coordinates.
            surface.SetUV(new Vector2(p.Dot(u) / 1.8f, -p.Dot(q) / 2.2f));
            surface.AddVertex(p);
        }
        var textile = new StandardMaterial3D { AlbedoColor = Colors.White, Roughness = .97f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        if (ResourceLoader.Exists(path)) textile.AlbedoTexture = GD.Load<Texture2D>(path);
        else { textile.AlbedoColor = new Color("38655f"); GD.PushWarning("Mosque prayer carpet texture awaits asset import: " + path); }
        var topMesh = new MeshInstance3D { Name = "MosquePatternedCarpetSurface", Mesh = surface.Commit(), MaterialOverride = textile };
        carpet.AddChild(topMesh);
        surface.Dispose();
        carpet.SetMeta("textureConsumer", path);
        carpet.SetMeta("textureTileMetres", new Vector2(1.8f, 2.2f));
        carpet.SetMeta("textureUpDirection", q);
    }

    private void BuildMosqueDonationBox()
    {
        var room = _mosqueRoom!;
        var box = new Node3D { Name = "MosqueDonationBox", Position = new(2.48f, 0, 1.56f) };
        room.AddChild(box);
        box.SetMeta("presentationRole", "locked donation box; visible unmarked contributions; no theft interaction");
        FacilitySolid(box, "DonationPedestal", new(.44f, .74f, .35f), new(0, .37f, 0), "80674c", "wood_furniture");
        FacilitySolid(box, "DonationBack", new(.46f, .42f, .04f), new(0, .95f, -.16f), "80674c", "wood_furniture");
        foreach (var x in new[] { -.22f, .22f })
            FacilitySolid(box, "DonationSide" + x, new(.04f, .42f, .32f), new(x, .95f, 0), "80674c", "wood_furniture");
        FacilitySolid(box, "DonationLid", new(.49f, .04f, .35f), new(0, 1.18f, 0), "80674c", "wood_furniture");
        AddVisualBox(box, "DonationSlot", new(.20f, .002f, .014f), new(0, 1.201f, 0), "26332d", "metal");
        var glass = FacilitySolid(box, "DonationGlass", new(.40f, .36f, .016f), new(0, .96f, .169f), "b5c8c2", "glass");
        glass.MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(.70f, .81f, .77f, .17f), Roughness = .12f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        for (var i = 0; i < 9; i++)
        {
            var note = AddVisualBox(box, "DonationNote" + i, new(.145f, .002f, .061f), new(-.085f + i % 3 * .075f, .746f + i * .005f, -.045f + i % 2 * .075f), i % 2 == 0 ? "b9c19f" : "b4b5c2", "paper");
            note.RotationDegrees = new(0, i * 23, 0);
        }
        for (var i = 0; i < 7; i++)
            DiscoveryCylinder(box, "DonationCoin" + i, .013f, .013f, .003f, new(-.14f + i % 4 * .08f, .746f + i / 4 * .004f, .07f), "baaf83");
        AddVisualBox(box, "DonationLock", new(.045f, .065f, .025f), new(.18f, 1.08f, .19f), "b2aa8f", "metal");
        FacilityLabel(box, "DonationLabel", "Сәдака", new(0, .50f, .179f), 0, .00135f);
    }

    // Later prayer-time owner can resolve this anchored place after any relocation.
    // No invented timetable, synthesized recording or ambient loop is presented as azan.
    internal bool TryGetMosqueAdhanPoint(out Transform3D point)
    {
        point = _mosqueAdhanAnchor?.GlobalTransform ?? Transform3D.Identity;
        return _mosqueAdhanAnchor is not null && IsInstanceValid(_mosqueAdhanAnchor);
    }
}
