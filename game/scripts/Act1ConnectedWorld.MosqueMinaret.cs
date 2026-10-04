using System;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal const float MinaretGalleryHeight = 9.60f;
    private static readonly Vector3 MinaretCentre = new(3.83f, 0, -2.00f);

    private void BuildMosqueAccessibleMinaret()
    {
        var room = _mosqueRoom!;
        var stair = new Node3D { Name = "MosqueMinaretAccess", Position = MinaretCentre };
        room.AddChild(stair);
        stair.SetMeta("presentationRole", "timber octagonal roof minaret; narrow three-turn spiral; sheltered azanchi lantern");
        stair.SetMeta("riseMetres", .16f);
        stair.SetMeta("goingMetres", .236f);
        stair.SetMeta("clearFlightWidth", 1.04f);
        stair.SetMeta("sameLaneVerticalClearance", 3.12f);
        stair.SetMeta("shaftDiameterMetres", 2.60f);
        const int count = 60, perTurn = 20;
        const float rise = .16f, inner = .16f, outer = 1.20f, walkingRadius = .82f;
        var angle = Mathf.Tau / perTurn;
        var anchors = new global::Godot.Collections.Array<Vector3>();
        anchors.Add(stair.ToGlobal(new(-.70f, .035f, 1.70f)));
        // A broad first tread allows a normal turn from the vestibule into the
        // fanned flight; it is timber and contact at the same 16cm rise.
        MinaretSectorSolid(stair, "MinaretEntryTread", inner, outer, Mathf.DegToRad(126), Mathf.Pi * .5f, rise, rise, "ac8e65", "wood_furniture");
        anchors.Add(stair.ToGlobal(new(-.212f, rise + .035f, .792f)));
        for (var i = 0; i < count; i++)
        {
            var start = Mathf.Pi * .5f - i * angle;
            var end = start - angle;
            var top = (i + 1) * rise;
            MinaretSectorSolid(stair, "MinaretTread" + i, inner, outer, start, end, top, .08f, "ac8e65", "wood_furniture");
            anchors.Add(stair.ToGlobal(new(Mathf.Cos((start + end) * .5f) * walkingRadius, top + .035f,
                Mathf.Sin((start + end) * .5f) * walkingRadius)));
            // The continuous outer handrail follows the actual helix, with posts between treads.
            var a = new Vector3(Mathf.Cos(start) * 1.22f, top + .95f - rise * .5f, Mathf.Sin(start) * 1.22f);
            var b = new Vector3(Mathf.Cos(end) * 1.22f, top + .95f + rise * .5f, Mathf.Sin(end) * 1.22f);
            // The first two treads open to the vestibule: a rail across their
            // outside edge would fence off the bottom entrance at chest height.
            if (i >= 2) MinaretContactRail(stair, "SpiralHandrail" + i, a, b);
            if (i >= 2 && i % 2 == 0)
                FacilityRod(stair, "SpiralBaluster" + i, new(a.X, top, a.Z), a, .018f, "c4b18b");
        }
        // Structural centre mast matches the treads' inner edge; no invisible ramp or teleporter.
        var mast = new MeshInstance3D { Name = "SpiralTimberMast", Position = new(0, 4.80f, 0),
            Mesh = new CylinderMesh { TopRadius = inner, BottomRadius = inner, Height = MinaretGalleryHeight, RadialSegments = 12 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("80674c", "wood_furniture") };
        stair.AddChild(mast);
        var mastBody = new StaticBody3D { Name = "SpiralTimberMastBody", Position = mast.Position, CollisionLayer = 2, CollisionMask = 0 };
        mastBody.SetMeta("collisionOwner", "act1-exterior-architecture");
        mastBody.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = inner, Height = MinaretGalleryHeight } });
        stair.AddChild(mastBody); _facilityBodies.Add(mastBody);

        // Eight slim timber facets. The two south facets start above the real bottom doorway.
        for (var side = 0; side < 8; side++)
        {
            var mid = side * Mathf.Tau / 8 + Mathf.Pi / 8;
            var bottom = side is 1 or 2 ? 2.25f : 0f;
            var height = MinaretGalleryHeight - bottom;
            FacilitySolid(stair, "TimberShaftFacet" + side, new(1.08f, height, .10f),
                new(Mathf.Cos(mid) * 1.30f, bottom + height * .5f, Mathf.Sin(mid) * 1.30f), "729080", "wood_painted_green",
                new(0, Mathf.Pi * .5f - mid, 0));
            AddVisualBox(stair, "ShaftCornerTrim" + side, new(.065f, height, .065f),
                new(Mathf.Cos(side * Mathf.Tau / 8) * 1.39f, bottom + height * .5f, Mathf.Sin(side * Mathf.Tau / 8) * 1.39f), "ece9df", "wood_painted_trim");
        }
        FacilityLabel(room, "MosqueMinaretAccessSign", "Минарет", new(3.83f, 2.55f, -.67f), 0, .0014f);
        var gallery = new Node3D { Name = "MosqueMinaretGallery", Position = MinaretCentre + Vector3.Up * MinaretGalleryHeight };
        room.AddChild(gallery);
        // Quarter-circle arrival platform follows the last tread. Its underside is >2.3m
        // above the preceding turn; the final rising half remains a true open stairwell.
        for (var i = 0; i < 5; i++)
            MinaretSectorSolid(gallery, "LanternLanding" + i, inner, outer, Mathf.Pi * .5f - i * angle,
                Mathf.Pi * .5f - (i + 1) * angle, 0, .08f, "ac8e65", "wood_furniture");
        gallery.SetMeta("openStairwell", "final rising half has no floor above the standing capsule");
        for (var side = 0; side < 8; side++)
        {
            var mid = side * Mathf.Tau / 8 + Mathf.Pi / 8;
            var face = new Vector3(Mathf.Cos(mid) * 1.30f, 0, Mathf.Sin(mid) * 1.30f);
            var yaw = Mathf.Pi * .5f - mid;
            FacilitySolid(gallery, "LanternTimberApron" + side, new(1.08f, .82f, .10f), face + Vector3.Up * .41f,
                "729080", "wood_painted_green", new(0, yaw, 0));
            var frame = new Node3D { Name = "LanternWindowFrame" + side, Position = face, Rotation = new(0, yaw, 0) };
            gallery.AddChild(frame);
            FacilityWindow(frame, "LanternWindow", new(0, 1.46f, 0), new(.80f, 1.18f));
            foreach (var x in new[] { -.48f, .48f })
                FacilitySolid(frame, "LanternJamb" + x, new(.13f, 2.30f, .12f), new(x, 1.15f, 0), "ece9df", "wood_painted_trim");
            FacilitySolid(frame, "LanternHeader", new(1.08f, .25f, .12f), new(0, 2.175f, 0), "ece9df", "wood_painted_trim");
        }
        _mosqueAdhanAnchor = new Node3D { Name = "MosqueAdhanAnchor", Position = new(.53f, 0, .53f),
            Rotation = new(0, Mathf.Atan2(MosqueLocalQibla.X, MosqueLocalQibla.Z), 0) };
        gallery.AddChild(_mosqueAdhanAnchor);
        _mosqueAdhanAnchor.SetMeta("scheduleStatus", "licensed CC0 adhan recording installed 2026-10-04; validated local prayer calendar still open");
        _mosqueAdhanAnchor.SetMeta("audioPolicy", "no synthetic fallback; no unlicensed remote audio");
        var mic = new Node3D { Name = "AzanMicrophone", Position = new(.88f, 0, .56f) };
        gallery.AddChild(mic);
        FacilityRod(mic, "Stand", new(0, .04f, 0), new(0, 1.35f, 0), .013f, "626967");
        FacilityRod(mic, "Boom", new(0, 1.35f, 0), new(-.20f, 1.46f, 0), .011f, "626967");
        DiscoveryCylinder(mic, "MicrophoneHead", .027f, .025f, .11f, new(-.20f, 1.50f, 0), "303936");
        foreach (var a in new[] { 0f, Mathf.Tau / 3, Mathf.Tau * 2 / 3 })
            FacilityRod(mic, "StandFoot" + a, new(0, .04f, 0), new(Mathf.Cos(a) * .18f, .02f, Mathf.Sin(a) * .18f), .009f, "626967");
        FacilityRod(mic, "Cable", new(-.20f, 1.45f, .01f), new(0, .025f, .05f), .003f, "303936");
        mic.SetMeta("purpose", "author-requested modern microphone at sheltered azanchi point; visual equipment only");
        stair.SetMeta("walkAnchors", anchors);
        stair.SetMeta("adhanPoint", _mosqueAdhanAnchor.GlobalPosition);
        var cap = new Node3D { Name = "MosqueMinaretCrown", Position = MinaretCentre + Vector3.Up * (MinaretGalleryHeight + 2.30f) };
        room.AddChild(cap);
        MinaretOctagonalPiece(cap, "OctagonalCornice", 1.48f, 1.48f, .18f, Vector3.Zero, "ece9df", "wood_painted_trim");
        MinaretOctagonalPiece(cap, "TentedRoof", .035f, 1.46f, 3.0f, new(0, 1.55f, 0), "496e5b", "roof_metal");
        FacilityRod(cap, "FinialRod", new(0, 3.04f, 0), new(0, 3.65f, 0), .017f, "c9b26a");
        for (var i = 0; i < 16; i++)
        {
            var a0 = Mathf.DegToRad(-115 + i * 230f / 16); var a1 = Mathf.DegToRad(-115 + (i + 1) * 230f / 16);
            FacilityRod(cap, "Crescent" + i, new(Mathf.Cos(a0) * .25f, 3.83f + Mathf.Sin(a0) * .25f, 0),
                new(Mathf.Cos(a1) * .25f, 3.83f + Mathf.Sin(a1) * .25f, 0), .027f, "c9b26a");
        }
        for (var i = 0; i < 3; i++) FacilityLamp(stair, "MinaretStairLamp" + i, new(0, 1.85f + i * 3.20f, 0), "ffe5bd", .22f, 2.20f);
    }

    private void MinaretSectorSolid(Node3D parent, string name, float inner, float outer,
        float from, float to, float top, float thickness, string colour, string surface)
    {
        Vector3 P(float r, float a, float y) => new(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        var points = new[] { P(inner, from, top), P(outer, from, top), P(outer, to, top), P(inner, to, top),
            P(inner, from, top-thickness), P(outer, from, top-thickness), P(outer, to, top-thickness), P(inner, to, top-thickness) };
        using var mesh = new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var (a,b,c,d) in new[] { (3,2,1,0), (4,5,6,7), (0,1,5,4), (1,2,6,5), (2,3,7,6), (3,0,4,7) })
        {
            var normal = (points[c]-points[a]).Cross(points[b]-points[a]).Normalized();
            foreach (var n in new[] { a,b,c,a,c,d }) { mesh.SetNormal(normal); mesh.SetUV(new(points[n].X, points[n].Z)); mesh.AddVertex(points[n]); }
        }
        parent.AddChild(new MeshInstance3D { Name = name, Mesh = mesh.Commit(), MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface) });
        var body = new StaticBody3D { Name = name + "Body", CollisionLayer = 2, CollisionMask = 0 };
        body.SetMeta("collisionOwner", "act1-exterior-architecture");
        body.AddChild(new CollisionShape3D { Name = "Contact", Shape = new ConvexPolygonShape3D { Points = points, Margin = .001f } });
        parent.AddChild(body); _facilityBodies.Add(body);
    }

    private static void MinaretOctagonalPiece(Node3D parent, string name, float top, float bottom,
        float height, Vector3 at, string colour, string surface) => parent.AddChild(new MeshInstance3D { Name = name, Position = at,
            Mesh = new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = 8 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface) });

    private void MinaretContactRail(Node3D parent, string name, Vector3 from, Vector3 to)
    {
        var delta = to - from;
        FacilitySolid(parent, name, new(.04f, .04f, delta.Length()), (from + to) * .5f, "d5d0b7", "wood_furniture",
            new(-Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length()), Mathf.Atan2(delta.X, delta.Z), 0));
    }

    private void EnlargeMosqueShell(Node3D complex)
    {
        void Resize(string name, Vector3 sizeDelta, Vector3 move)
        {
            var mesh = complex.GetNode<MeshInstance3D>(name);
            mesh.Mesh = new BoxMesh { Size = ((BoxMesh)mesh.Mesh).Size + sizeDelta }; mesh.Position += move;
            var shape = complex.GetNode<CollisionShape3D>("MosqueCollisionProxy/" + name + "_Blocker");
            shape.Shape = new BoxShape3D { Size = ((BoxShape3D)shape.Shape).Size + sizeDelta }; shape.Position += move;
        }
        Resize("MosqueHallWest", new(0, 0, 1.20f), new(-1.40f, 0, 0));
        Resize("MosqueHallEastLeft", new(0, 0, .60f), new(0, 0, -.30f));
        Resize("MosqueHallEastRight", new(0, 0, .60f), new(0, 0, .30f));
    }

    private void BuildMosqueRoofWithStairOpening()
    {
        var room = _mosqueRoom!;
        const float eave = 4.67f, rise = 2.15f, half = 5.32f;
        using var roof = new SurfaceTool(); roof.Begin(Mesh.PrimitiveType.Triangles);
        void Patch(float x0, float x1, float z0, float z1)
        {
            float Y(float z) => eave + rise * (1 - Math.Abs(z) / half);
            var a = new Vector3(x0,Y(z0),z0); var b = new Vector3(x1,Y(z0),z0);
            var c = new Vector3(x1,Y(z1),z1); var d = new Vector3(x0,Y(z1),z1);
            var normal = (c-a).Cross(b-a).Normalized();
            foreach (var p in new[] {a,b,c,a,c,d}) { roof.SetNormal(normal); roof.SetUV(new(p.X*.3f,p.Z*.3f)); roof.AddVertex(p); }
        }
        Patch(-7.4f, 2.43f, -half, 0); Patch(-7.4f,2.43f,0,half);
        Patch(2.43f,5.23f,-half,-3.40f); Patch(2.43f,5.23f,-.60f,0); Patch(2.43f,5.23f,0,half);
        Patch(5.23f,6,-half,0); Patch(5.23f,6,0,half);
        var mesh = new MeshInstance3D { Name = "MosqueGableRoofV4", Mesh = roof.Commit(), MaterialOverride = PainterlyMaterialLibrary.ForColor("e6ebef","snow_roof") };
        room.AddChild(mesh);
        mesh.SetMeta("stairOpeningMin",new Vector2(2.43f,-3.40f)); mesh.SetMeta("stairOpeningMax",new Vector2(5.23f,-.60f));
        foreach (var z in new[] {-half,half}) AddVisualBox(room,"MosqueRoofFascia"+z,new(13.45f,.20f,.15f),new(-.7f,eave-.03f,z),"ece9df","wood_painted_trim");
        AddVisualBox(room,"MosqueRidgeCap",new(13.4f,.13f,.16f),new(-.7f,eave+rise+.015f,0),"496e5b","roof_metal");
        foreach (var x in new[] {-7.22f,5.82f})
        {
            using var gable = new SurfaceTool(); gable.Begin(Mesh.PrimitiveType.Triangles);
            var a = new Vector3(x,eave,-half+.1f); var b = new Vector3(x,eave+rise,0); var c = new Vector3(x,eave,half-.1f);
            foreach (var p in x < 0 ? new[] {a,b,c} : new[] {a,c,b}) { gable.SetNormal(x<0?Vector3.Left:Vector3.Right); gable.SetUV(new(p.Z*.25f,p.Y*.25f)); gable.AddVertex(p); }
            room.AddChild(new MeshInstance3D { Name = "MosqueTriangleGable"+x, Mesh = gable.Commit(), MaterialOverride = PainterlyMaterialLibrary.ForColor("729080","wood_painted_green") });
        }
    }
}
