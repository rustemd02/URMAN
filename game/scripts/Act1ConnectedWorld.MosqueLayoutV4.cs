using System;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Settlement north is +Z (NorthContinuousWinterRoads), east is +X.
    // Fictional Kara-Urman uses Kazan as a regional proxy, not a claimed real village location.
    // Initial great-circle bearing: atan2(sin Δλ cos φ2,
    // cos φ1 sin φ2 − sin φ1 cos φ2 cos Δλ), Kazan 55.79/49.12 → Kaaba 21.4225/39.8262.
    // MosqueOrientation.cs yaws the whole complex onto this bearing before the
    // interior is built, so this world vector resolves to room-local (0, 0, -1):
    // the mihrab, the minbar, the imam and the carpet rows all share one clean axis.
    internal const float MosqueQiblaBearingDegrees = 195.1725f;
    private Vector3 MosqueLocalQibla => _mosqueRoom!.GlobalBasis.Inverse() *
        new Vector3(Mathf.Sin(Mathf.DegToRad(MosqueQiblaBearingDegrees)), 0,
            Mathf.Cos(Mathf.DegToRad(MosqueQiblaBearingDegrees)));
    private Node3D? _mosqueAdhanAnchor;

    private void BuildMosqueDecoratedCeiling()
    {
        var room = _mosqueRoom!;
        // Three continuous fields leave the existing minaret opening free.
        AddVisualBox(room, "MosqueCeiling", new(9.1f, .12f, 9.15f), new(-2.075f, 3.80f, 0), "eee9dc", "wood_painted_trim");
        AddVisualBox(room, "MosqueVestibuleSouthCeiling", new(2.75f, .12f, 4.20f), new(3.85f, 3.80f, 2.47f), "eee9dc", "wood_painted_trim");
        AddVisualBox(room, "MosqueVestibuleNorthCeiling", new(2.75f, .12f, 1.45f), new(3.85f, 3.80f, -3.85f), "eee9dc", "wood_painted_trim");
        var battens = new List<Transform3D>();
        foreach (var x in new[] { -5.50f, -3.35f, -1.20f, .95f })
            battens.Add(MosqueBoxTransform(new(x, 3.704f, 0), new(.075f, .068f, 9.10f), 0));
        foreach (var z in new[] { -4.48f, 4.48f })
        {
            battens.Add(MosqueBoxTransform(new(-.7f, 3.64f, z), new(11.8f, .13f, .10f), 0));
            battens.Add(MosqueBoxTransform(new(-.7f, 3.71f, z), new(11.8f, .035f, .18f), 0));
        }
        MosqueBatch(room, "MosqueCeilingJoinery", battens, MosqueFinish("walnut", "e2d4b8", 1.2f));
        foreach (var x in new[] { -3.35f, .35f })
        {
            var pendant = new Node3D { Name = x < -1 ? "MosqueChandelier" : "MosqueSecondPendant", Position = new(x, 3.20f, -.35f) };
            room.AddChild(pendant);
            FacilityRod(pendant, "Suspension", Vector3.Zero, new(0, .52f, 0), .012f, "857751");
            var shade = DiscoveryCylinder(pendant, "LinenDrum", .32f, .36f, .24f, Vector3.Zero, "e8dfc7");
            shade.MaterialOverride = MosqueFinish("paper", "ffffff", .55f);
            foreach (var y in new[] { -.125f, .125f })
            {
                var rim = new MeshInstance3D { Name = y < 0 ? "LowerRim" : "UpperRim", Position = new(0, y, 0),
                    Mesh = new TorusMesh { InnerRadius = .321f, OuterRadius = .337f, Rings = 24, RingSegments = 8 },
                    MaterialOverride = PainterlyMaterialLibrary.ForColor("8c7a53", "metal") };
                pendant.AddChild(rim);
            }
        }
    }

    private void BuildMosqueWashCorner()
    {
        BuildMosqueWashroom();
    }

    private MeshInstance3D BuildMosquePrayerAndLibrary()
    {
        var room = _mosqueRoom!;
        var qiblaLocal = MosqueLocalQibla.Normalized();
        room.SetMeta("worldTrueNorth", new Vector3(0, 0, 1));
        room.SetMeta("worldEast", Vector3.Right);
        room.SetMeta("qiblaRegionalProxy", "Kazan 55.79N 49.12E; fictional village has no surveyed coordinate");
        room.SetMeta("qiblaBearingDegrees", MosqueQiblaBearingDegrees);
        room.SetMeta("qiblaWorldDirection", (room.GlobalBasis * qiblaLocal).Normalized());
        room.SetMeta("qiblaLocalDirection", qiblaLocal);
        room.SetMeta("carpetRowAxisLocal", qiblaLocal);
        var carpet = BuildMosquePrayerMatField(room);
        var prayer = new Node3D { Name = "MosqueQiblaWall", Position = new(-.7f, 0, -4.14f),
            Rotation = new(0, Mathf.Atan2(-qiblaLocal.X, -qiblaLocal.Z), 0) };
        room.AddChild(prayer);
        prayer.SetMeta("qiblaBearingDegrees", MosqueQiblaBearingDegrees);
        // A shallow timber-lined niche preserves the established prayer axis.
        // No added calligraphy or religious symbol is inferred from references.
        FacilitySolid(prayer, "MihrabBacking", new(1.50f, 2.82f, .12f), new(0, 1.41f, -.24f), "a9b4a0", "wood_painted_trim");
        AddVisualBox(prayer, "MihrabInset", new(1.16f, 2.34f, .018f), new(0, 1.20f, -.169f), "e5deca", "plaster");
        foreach (var x in new[] { -.66f, .66f })
        {
            MosqueWood(prayer, "MihrabPilaster" + x, new(.11f, 2.02f, .18f), new(x, 1.01f, -.12f), true);
            MosqueWood(prayer, "MihrabPlinth" + x, new(.16f, .13f, .23f), new(x, .065f, -.10f));
            MosqueWood(prayer, "MihrabCapital" + x, new(.16f, .09f, .23f), new(x, 2.01f, -.10f));
        }
        // Joined curved moulding, a single smooth arch rather than isolated rods.
        var arch = new MosqueBookSurface();
        for (var i = 0; i < 32; i++)
        {
            var a = Mathf.Pi * i / 32; var b = Mathf.Pi * (i + 1) / 32;
            Vector3 P(float angle, float r, float depth) => new(Mathf.Cos(angle) * r, 1.98f + Mathf.Sin(angle) * r, depth);
            arch.Quad(P(a,.60f,-.035f), P(b,.60f,-.035f), P(b,.72f,-.035f), P(a,.72f,-.035f), Vector3.Back);
            arch.Quad(P(a,.60f,-.20f), P(b,.60f,-.20f), P(b,.60f,-.035f), P(a,.60f,-.035f), new(-Mathf.Cos(a),-Mathf.Sin(a),0));
            arch.Quad(P(a,.72f,-.035f), P(b,.72f,-.035f), P(b,.72f,-.20f), P(a,.72f,-.20f), new(Mathf.Cos(a),Mathf.Sin(a),0));
        }
        var archMesh = new ArrayMesh(); arch.Commit(archMesh, "MouldedArch", MosqueFinish("walnut"));
        prayer.AddChild(new MeshInstance3D { Name = "MihrabArch", Mesh = archMesh });
        BuildMosqueMinbar(prayer);
        MosquePrintedSign(room, "MosqueQiblaNotice", "Кыйбла", new(1.30f, 2.43f, -4.55f), 0, .48f, "signs-main", 2);
        MosquePrintedSign(room, "MosqueLibrarySign", "Китапханә\nБиблиотека", new(-5.40f, 2.08f, 4.54f), 180, .75f, "signs-main", 3);
        BuildMosqueLibraryCase(room, "Mosque", new(-5.40f, 0, 4.23f), 1.60f);
        FacilityBench(room, "MosqueReadingBench", new(-5.40f, 0, 3.58f), 1.35f, 0);
        FacilityLamp(room, "MosqueLibraryLamp", new(-5.1f, 3.35f, 2.5f), "ffe5c2", .38f, 2.6f);
        BuildMosqueDonationBox();
        room.SetMeta("libraryClearSizeMetres", new Vector2(3.14f, 3.70f));
        room.SetMeta("hallClearHeightMetres", 3.74f);
        return carpet;
    }

    private void BuildMosqueMinbar(Node3D prayer)
    {
        var minbar = new Node3D { Name = "MosqueMinbar", Position = new(1.05f, 0, .10f) };
        prayer.AddChild(minbar);
        for (var i = 0; i < 3; i++)
        {
            var top = .16f * (i + 1);
            MosqueWood(minbar, "MinbarTread" + i, new(.74f, top, .31f), new(0, top * .5f, .88f - i * .29f), true);
            MosqueWood(minbar, "MinbarNosing" + i, new(.77f, .028f, .045f), new(0, top - .014f, 1.02f - i * .29f));
        }
        MosqueWood(minbar, "MinbarPlatform", new(.80f, .48f, .56f), new(0, .24f, -.1f), true);
        foreach (var x in new[] { -.43f, .43f })
        {
            MosqueWood(minbar, "MinbarPost" + x, new(.07f, 1.12f, .07f), new(x, 1.02f, -.28f), true);
            DiscoveryCylinder(minbar, "PostCap" + x, .055f, .045f, .055f, new(x, 1.61f, -.28f), "a58a61");
            FacilityRod(minbar, "MinbarRail" + x, new(x, 1.54f, -.28f), new(x, .98f, 1.00f), .027f, "90704a");
            for (var i = 0; i < 5; i++)
            {
                var z = .05f + i * .19f;
                var top = Mathf.Lerp(1.54f, .98f, (z + .28f) / 1.28f);
                var bottom = .48f - Math.Max(0, z - .20f) * .48f;
                var post = DiscoveryCylinder(minbar, $"Baluster{x}_{i}", .017f, .024f, top - bottom, new(x, (top + bottom) * .5f, z), "90704a");
                post.MaterialOverride = MosqueFinish("walnut");
            }
            MosqueWood(minbar, "MinbarSidePanel" + x, new(.035f, .29f, .46f), new(x, .27f, -.08f));
        }
        // A lectern rail, not a chair or throne.
        MosqueWood(minbar, "MinbarReadingRail", new(.79f, .07f, .10f), new(0, 1.53f, -.28f));
    }

    private void BuildMosqueLibraryCase(Node3D parent, string prefix, Vector3 at, float width)
    {
        MosqueWood(parent, prefix + "BookcaseLeft", new(.07f, 1.72f, .39f), at + new Vector3(-width*.5f+.035f,.86f,0), true);
        MosqueWood(parent, prefix + "BookcaseRight", new(.07f, 1.72f, .39f), at + new Vector3(width*.5f-.035f,.86f,0), true);
        MosqueWood(parent, prefix + "BookcaseBack", new(width-.14f,1.60f,.025f), at + new Vector3(0,.85f,.18f));
        for (var shelf = 0; shelf < 4; shelf++)
        {
            var y = .10f + shelf * .49f;
            MosqueWood(parent, prefix + "BookShelf" + shelf, new(width-.12f,.045f,.39f), at + new Vector3(0,y,0), true);
            if (shelf == 3) continue;
            var count = Mathf.FloorToInt((width - .25f) / .14f);
            for (var book = 0; book < count; book++)
            {
                var binding = AddMosqueShelfBook(parent, $"{prefix}Book{book}_{shelf}", new(.058f + (book%2)*.012f,.28f + book%3*.025f,.22f),
                    at + new Vector3(-width*.5f+.17f+book*.14f,y+.0225f,-.025f), "405d57");
                MosqueBookFinish(binding, book % 3 == 0 ? "d1bb9d" : "ffffff");
            }
        }
        MosqueWood(parent, prefix + "BookcaseCornice", new(width+.06f,.075f,.43f), at+new Vector3(0,1.72f,0));
    }

    private void BuildMosqueDonationBox()
    {
        var box = new Node3D { Name = "MosqueDonationBox", Position = new(.9f,0,4.38f), RotationDegrees = new(0,180,0) };
        _mosqueRoom!.AddChild(box);
        box.SetMeta("presentationRole", "locked donation box; no theft interaction");
        MosqueWood(box, "DonationPedestal", new(.44f,.74f,.35f), new(0,.37f,0), true);
        MosqueWood(box, "DonationFoot", new(.48f,.06f,.39f), new(0,.03f,0));
        MosqueWood(box, "DonationInsetPanel", new(.32f,.48f,.018f), new(0,.42f,.178f), tint:"dfd0b8");
        MosqueWood(box, "DonationBack", new(.46f,.42f,.04f), new(0,.95f,-.16f), true);
        foreach(var x in new[]{-.22f,.22f})
            MosqueWood(box,"DonationSide"+x,new(.04f,.42f,.32f),new(x,.95f,0),true);
        // Two lid leaves leave a real slot, rather than a painted black stripe.
        foreach(var z in new[]{-.095f,.095f})
            MosqueWood(box,"DonationLid"+z,new(.49f,.04f,.16f),new(0,1.18f,z),true);
        foreach(var x in new[]{-.175f,.175f})
            MosqueWood(box,"DonationSlotEnd"+x,new(.14f,.04f,.03f),new(x,1.18f,0));
        var glass=FacilitySolid(box,"DonationGlass",new(.40f,.36f,.016f),new(0,.96f,.169f),"b5c8c2","glass");
        glass.MaterialOverride=new StandardMaterial3D {Transparency=BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor=new Color(.70f,.81f,.77f,.13f),Roughness=.18f,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
        for(var i=0;i<5;i++) MosquePaper(box,"DonationNote"+i,new(-.075f+i%3*.065f,.746f+i*.004f,-.025f+i%2*.06f),new(.13f,.06f),new(0,i*23,0));
        AddVisualBox(box,"DonationLock",new(.035f,.048f,.025f),new(.18f,1.08f,.19f),"a89970","metal");
        MosquePrintedSign(box,"DonationLabel","Сәдака",new(0,.52f,.199f),0,.28f,"signs-details",0);
    }

    // Later prayer-time owner can resolve this anchored place after any relocation.
    // No invented timetable, synthesized recording or ambient loop is presented as azan.
    internal bool TryGetMosqueAdhanPoint(out Transform3D point)
    {
        point = _mosqueAdhanAnchor?.GlobalTransform ?? Transform3D.Identity;
        return _mosqueAdhanAnchor is not null && IsInstanceValid(_mosqueAdhanAnchor);
    }
}
