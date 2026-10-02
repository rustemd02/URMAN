using System;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildMosqueAccessibleMinaret()
    {
        var room = _mosqueRoom!;
        var stair = new Node3D { Name = "MosqueMinaretAccess" };
        room.AddChild(stair);
        stair.SetMeta("presentationRole", "real seven-flight minaret stair, guarded landings and azan gallery");
        stair.SetMeta("riseMetres", .18f);
        stair.SetMeta("goingMetres", .28f);
        stair.SetMeta("clearFlightWidth", 1.0f);
        stair.SetMeta("sameLaneVerticalClearance", 2.76f);
        const int flights = 7, treads = 8;
        const float rise = .18f, going = .28f;
        const float north = -3.22f, south = -.98f;
        var anchors = new global::Godot.Collections.Array<Vector3>();
        anchors.Add(room.ToGlobal(new(3.18f, .035f, -.42f)));
        for (var flight = 0; flight < flights; flight++)
        {
            var northwards = flight % 2 == 0;
            var x = northwards ? 3.16f : 4.48f;
            var floor = flight * treads * rise;
            for (var step = 0; step < treads; step++)
            {
                var z = northwards ? south - going * (step + .5f) : north + going * (step + .5f);
                var top = floor + (step + 1) * rise;
                FacilitySolid(stair, $"MinaretTread{flight}_{step}", new(1.0f, .12f, going + .002f), new(x, top - .06f, z), "ac8e65", "wood_furniture");
                // Open stringer construction avoids opaque massive slabs in the stairwell.
                foreach (var side in new[] { -.47f, .47f })
                {
                    AddVisualBox(stair, $"TreadNosing{flight}_{step}_{side}", new(.045f, .025f, going), new(x + side, top - .01f, z), "ddca91", "wood_furniture");
                    if (step % 2 != 0) continue;
                    FacilitySolid(stair, $"MinaretStairPost{flight}_{step}_{side}", new(.04f, .95f, .04f), new(x + side, top + .475f, z), "d5d0b7", "wood_furniture");
                }
            }
            var startZ = northwards ? south - going * .5f : north + going * .5f;
            var endZ = northwards ? north + going * .5f : south - going * .5f;
            foreach (var side in new[] { -.47f, .47f })
            {
                MinaretContactRail(stair, $"MinaretFlightHandrail{flight}_{side}", new(x + side, floor + rise + .95f, startZ), new(x + side, floor + treads * rise + .95f, endZ));
                MinaretContactRail(stair, $"MinaretStringer{flight}_{side}", new(x + side, floor + .04f, startZ), new(x + side, floor + treads * rise - .14f, endZ));
            }
            var landingZ = northwards ? -3.63f : -.57f;
            var landingY = floor + treads * rise;
            FacilitySolid(stair, "MinaretLanding" + flight, new(2.42f, .12f, .82f), new(3.82f, landingY - .06f, landingZ), "ac8e65", "wood_furniture");
            var outerZ = northwards ? -4.04f : -.16f;
            MinaretContactRail(stair, "MinaretLandingRail" + flight, new(2.63f, landingY + 1.02f, outerZ), new(5.02f, landingY + 1.02f, outerZ));
            foreach (var postX in new[] { 2.65f, 3.82f, 5.0f })
                FacilitySolid(stair, $"MinaretLandingPost{flight}_{postX}", new(.05f, 1.02f, .05f), new(postX, landingY + .51f, outerZ), "d5d0b7", "wood_furniture");
            anchors.Add(room.ToGlobal(new(x, landingY + .035f, landingZ)));
            if (flight + 1 < flights)
                anchors.Add(room.ToGlobal(new(northwards ? 4.48f : 3.16f, landingY + .035f, landingZ)));
        }
        // Side walls enclose the staircase but preserve a 1.45 m entrance at the south bottom.
        FacilitySolid(stair, "MinaretStairWestWall", new(.10f, 10.08f, 3.94f), new(2.50f, 5.04f, -2.10f), "729080", "wood_painted_green");
        // East bottom opening preserves the stable entrance door's complete inward sweep.
        FacilitySolid(stair, "MinaretStairEastNorthWall", new(.10f, 10.08f, 2.94f), new(5.14f, 5.04f, -2.60f), "729080", "wood_painted_green");
        FacilitySolid(stair, "MinaretStairEastDoorHeader", new(.10f, 7.68f, 1.00f), new(5.14f, 6.24f, -.63f), "729080", "wood_painted_green");
        // A genuine upper window pierces the north wall and lights the mid-stair landing.
        FacilitySolid(stair, "MinaretStairNorthLowerWall", new(2.74f, 5.55f, .10f), new(3.82f, 2.775f, -4.07f), "729080", "wood_painted_green");
        FacilitySolid(stair, "MinaretStairNorthWindowHeader", new(2.74f, 2.93f, .10f), new(3.82f, 8.615f, -4.07f), "729080", "wood_painted_green");
        foreach (var x in new[] { 2.965f, 4.675f })
            FacilitySolid(stair, "MinaretStairNorthWindowPier" + x, new(1.03f, 1.60f, .10f), new(x, 6.35f, -4.07f), "729080", "wood_painted_green");
        FacilityWindow(stair, "MinaretNorthWindow", new(3.82f, 6.35f, -4.07f), new(.66f, 1.60f));
        FacilitySolid(stair, "MinaretStairSouthUpperWall", new(2.74f, 5.73f, .10f), new(3.82f, 7.215f, -.13f), "729080", "wood_painted_green");
        FacilityLabel(room, "MosqueMinaretAccessSign", "Минарет", new(2.22f, 2.64f, -.85f), 90, .0014f);
        // Gallery wraps the tower above the last landing; its access opening stays unobstructed.
        const float galleryY = flights * treads * rise;
        var gallery = new Node3D { Name = "MosqueMinaretGallery", Position = new(3.82f, galleryY, -2.10f) };
        room.AddChild(gallery);
        FacilitySolid(gallery, "GalleryFloor", new(3.55f, .14f, 4.66f), new(0, -.07f, 0), "b09a70", "wood_furniture");
        // Floor only at the final level, with the last flight opening cut out.
        var floorMesh = gallery.GetNode<MeshInstance3D>("GalleryFloor");
        var floorBody = gallery.GetNode<StaticBody3D>("GalleryFloorBody");
        gallery.RemoveChild(floorMesh); floorMesh.Free();
        _facilityBodies.Remove(floorBody); gallery.RemoveChild(floorBody); floorBody.Free();
        FacilitySolid(gallery, "GalleryNorthFloor", new(3.55f, .14f, .90f), new(0, -.07f, -1.88f), "b09a70", "wood_furniture");
        FacilitySolid(gallery, "GallerySouthFloor", new(3.55f, .14f, .82f), new(0, -.07f, 1.92f), "b09a70", "wood_furniture");
        foreach (var x in new[] { -1.40f, 1.40f })
            FacilitySolid(gallery, "GallerySideFloor" + x, new(.75f, .14f, 3.0f), new(x, -.07f, -.05f), "b09a70", "wood_furniture");
        // Continue the north landing to the sheltered azan point.
        FacilitySolid(gallery, "GalleryAccessPlatform", new(2.42f, .14f, .82f), new(0, -.07f, -1.53f), "b09a70", "wood_furniture");
        foreach (var x in new[] { -1.75f, 1.75f })
        {
            MinaretContactRail(gallery, "GallerySideRail" + x, new(x, 1.05f, -2.30f), new(x, 1.05f, 2.30f));
            for (var i = 0; i < 10; i++)
                FacilitySolid(gallery, $"GallerySideBaluster{x}_{i}", new(.055f, 1.05f, .055f), new(x, .525f, -2.30f + i * .511f), "e9e4d1", "wood_painted_trim");
        }
        foreach (var z in new[] { -2.30f, 2.30f })
        {
            MinaretContactRail(gallery, "GalleryEndRail" + z, new(-1.75f, 1.05f, z), new(1.75f, 1.05f, z));
            for (var i = 0; i < 8; i++)
                FacilitySolid(gallery, $"GalleryEndBaluster{z}_{i}", new(.055f, 1.05f, .055f), new(-1.75f + i * .5f, .525f, z), "e9e4d1", "wood_painted_trim");
        }
        // Guard the inner stair opening; only the north landing's access is open.
        MinaretContactRail(gallery, "GalleryInnerEastRail", new(1.02f, 1.05f, -1.10f), new(1.02f, 1.05f, 1.51f));
        MinaretContactRail(gallery, "GalleryInnerWestRail", new(-1.02f, 1.05f, -1.10f), new(-1.02f, 1.05f, 1.51f));
        MinaretContactRail(gallery, "GalleryInnerSouthRail", new(-1.02f, 1.05f, 1.51f), new(1.02f, 1.05f, 1.51f));
        foreach (var x in new[] { -1.02f, 1.02f })
            for (var i = 0; i < 12; i++)
                FacilitySolid(gallery, $"GalleryInnerSideBaluster{x}_{i}", new(.04f, 1.05f, .04f), new(x, .525f, -1.10f + i * 2.61f / 11), "e9e4d1", "wood_painted_trim");
        for (var i = 0; i < 10; i++)
            FacilitySolid(gallery, "GalleryInnerSouthBaluster" + i, new(.04f, 1.05f, .04f), new(-1.02f + i * 2.04f / 9, .525f, 1.51f), "e9e4d1", "wood_painted_trim");
        _mosqueAdhanAnchor = new Node3D { Name = "MosqueAdhanAnchor", Position = new(.10f, 0, -2.0f), Rotation = new(0, Mathf.Atan2(MosqueLocalQibla.X, MosqueLocalQibla.Z), 0) };
        gallery.AddChild(_mosqueAdhanAnchor);
        _mosqueAdhanAnchor.SetMeta("scheduleStatus", "hook-ready; validated local prayer calendar and licensed human azan recording required");
        _mosqueAdhanAnchor.SetMeta("audioPolicy", "no synthetic fallback; no unlicensed remote audio");
        stair.SetMeta("walkAnchors", anchors);
        stair.SetMeta("adhanPoint", _mosqueAdhanAnchor.GlobalPosition);
        for (var i = 0; i < 8; i++)
        {
            var angle = Mathf.Tau * (i + .5f) / 8;
            FacilitySolid(gallery, "GalleryCanopyColumn" + i, new(.12f, 2.18f, .12f),
                new(Mathf.Cos(angle) * 1.48f, 1.09f, Mathf.Sin(angle) * 1.94f), "e9e4d1", "wood_painted_trim");
        }
        // Tented octagonal cap and gold crescent form the small rural skyline.
        var cap = new Node3D { Name = "MosqueMinaretCrown", Position = new(3.82f, galleryY + 2.18f, -2.10f) };
        room.AddChild(cap);
        MinaretOctagonalPiece(cap, "OctagonalCornice", 2.0f, 2.0f, .20f, Vector3.Zero, "e9e4d1", "wood_painted_trim");
        MinaretOctagonalPiece(cap, "TentedRoof", .07f, 1.9f, 2.75f, new(0, 1.4f, 0), "496e5b", "roof_metal");
        FacilityRod(cap, "FinialRod", new(0, 2.75f, 0), new(0, 3.72f, 0), .025f, "c9b26a");
        for (var i = 0; i < 12; i++)
        {
            var a0 = Mathf.DegToRad(-115 + i * 230f / 12); var a1 = Mathf.DegToRad(-115 + (i + 1) * 230f / 12);
            FacilityRod(cap, "Crescent" + i, new(Mathf.Cos(a0) * .32f, 3.92f + Mathf.Sin(a0) * .32f, 0), new(Mathf.Cos(a1) * .32f, 3.92f + Mathf.Sin(a1) * .32f, 0), .036f, "c9b26a");
        }
        FacilityLamp(stair, "MinaretStairLamp", new(3.82f, 3.0f, -2.1f), "ffe5bd", .30f, 3.5f);
        FacilityLamp(stair, "MinaretUpperStairLamp", new(3.82f, 7.0f, -2.1f), "ffe5bd", .30f, 3.5f);
    }

    private static void MinaretOctagonalPiece(Node3D parent, string name, float top, float bottom,
        float height, Vector3 at, string colour, string surface)
    {
        parent.AddChild(new MeshInstance3D { Name = name, Position = at, Scale = new(1, 1, 1.25f),
            Mesh = new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = 8 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface) });
    }

    private void MinaretContactRail(Node3D parent, string name, Vector3 from, Vector3 to)
    {
        var delta = to - from;
        var mesh = FacilitySolid(parent, name, new(.05f, .05f, delta.Length()), (from + to) * .5f, "d5d0b7", "wood_furniture",
            new(-Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length()), Mathf.Atan2(delta.X, delta.Z), 0));
        mesh.SetMeta("presentationRole", "continuous handrail with matching contact");
    }

    private void BuildMosqueRoofWithStairOpening()
    {
        var room = _mosqueRoom!;
        const float eave = 4.67f, rise = 2.15f, half = 4.72f;
        using var roof = new SurfaceTool();
        roof.Begin(Mesh.PrimitiveType.Triangles);
        // Each patch is a true pitched roof, split at the tower's rectangle.
        void Patch(float x0, float x1, float z0, float z1)
        {
            float Y(float z) => eave + rise * (1 - Math.Abs(z) / half);
            var a = new Vector3(x0, Y(z0), z0); var b = new Vector3(x1, Y(z0), z0);
            var c = new Vector3(x1, Y(z1), z1); var d = new Vector3(x0, Y(z1), z1);
            var normal = (c - a).Cross(b - a).Normalized();
            foreach (var p in new[] { a, b, c, a, c, d })
            {
                roof.SetNormal(normal); roof.SetUV(new(p.X * .3f, p.Z * .3f)); roof.AddVertex(p);
            }
        }
        Patch(-6.0f, 2.45f, -half, 0); Patch(-6.0f, 2.45f, 0, half);
        Patch(2.45f, 5.20f, -half, -4.12f); Patch(2.45f, 5.20f, -.08f, 0);
        Patch(2.45f, 5.20f, 0, half); Patch(5.20f, 6.0f, -half, 0); Patch(5.20f, 6.0f, 0, half);
        var mesh = new MeshInstance3D { Name = "MosqueGableRoofV4", Mesh = roof.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("e6ebef", "snow_roof") };
        room.AddChild(mesh);
        mesh.SetMeta("stairOpeningMin", new Vector2(2.45f, -4.12f));
        mesh.SetMeta("stairOpeningMax", new Vector2(5.20f, -.08f));
        foreach (var z in new[] { -half, half })
            AddVisualBox(room, "MosqueRoofFascia" + z, new(12.05f, .20f, .15f), new(0, eave - .03f, z), "e9e4d1", "wood_painted_trim");
        AddVisualBox(room, "MosqueRidgeCap", new(12.0f, .13f, .16f), new(0, eave + rise + .015f, 0), "496e5b", "roof_metal");
        // Triangular end gables close the attic silhouette instead of leaving open roof ends.
        foreach (var x in new[] { -5.82f, 5.82f })
        {
            using var gable = new SurfaceTool(); gable.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var p in new[] { new Vector3(x, eave, -half + .1f), new(x, eave + rise, 0), new(x, eave, half - .1f) })
            { gable.SetNormal(x < 0 ? Vector3.Left : Vector3.Right); gable.SetUV(new(p.Z * .25f, p.Y * .25f)); gable.AddVertex(p); }
            var material = PainterlyMaterialLibrary.ForColor("729080", "wood_painted_green");
            material = (Material)material.Duplicate();
            if (material is StandardMaterial3D standard) standard.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            room.AddChild(new MeshInstance3D { Name = "MosqueTriangleGable" + x, Mesh = gable.Commit(), MaterialOverride = material });
        }
    }
}
