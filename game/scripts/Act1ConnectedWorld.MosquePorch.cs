using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // An artistic proposal in the existing timber language, not a reconstruction
    // of an unviewed historical building. Decoration is ordinary leaf/woodwork.
    private Vector3[] BuildMosqueGardenEntrance(Node3D complex)
    {
        var room = _mosqueRoom!;
        HidePresentationNode(complex.GetNode<Node3D>("MosqueEntranceAwning"));
        var porch = new Node3D { Name = "MosqueCarvedGardenEntrance" };
        room.AddChild(porch);
        porch.SetMeta("presentationRole", "level garden approach and carved timber entrance; no exterior stair or raised railed platform");
        const string timber = "92714f", pale = "e7dfca", leaf = "627f6c";
        // The relocated courtyard terrain meets this continuous contact at Y=0.
        // It bridges the .03 m plinth/floor joint, without a sill or any step.
        FacilitySolid(porch, "MosqueGardenWalkContact", new(5.25f, .12f, 2.40f),
            new(7.875f, -.06f, 0), "a49b85", "stone");
        var pavers = new List<Transform3D>();
        for (var row = 0; row < 12; row++)
        for (var col = 0; col < 4; col++)
            pavers.Add(MosqueBoxTransform(new(5.47f + row * .435f, -.003f, -.9f + col * .6f),
                new(.42f, .012f, .58f), 0));
        MosqueBatch(porch, "MosqueGardenWalkPavers", pavers, PainterlyMaterialLibrary.ForColor("b8ad97", "stone"));

        var details = new List<Transform3D>();
        var carving = new List<Transform3D>();
        void WoodSegment(List<Transform3D> batch, Vector3 from, Vector3 to, float width, float depth)
        {
            var delta = to - from;
            var basis = new Basis(new Quaternion(Vector3.Up, delta.Normalized()));
            batch.Add(new Transform3D(basis * Basis.FromScale(new Vector3(depth, delta.Length(), width)), (from + to) * .5f));
        }
        foreach (var z in new[] { -1.55f, 1.55f })
        {
            FacilitySolid(porch, "PorchPostStoneFoot" + z, new(.27f, .10f, .27f), new(7.66f, .01f, z), "aaa18c", "stone");
            FacilitySolid(porch, "PorchTimberPost" + z, new(.18f, 2.68f, .18f), new(7.66f, 1.34f, z), timber, "wood_furniture");
            foreach (var y in new[] { .18f, .40f, 2.30f, 2.52f })
                details.Add(MosqueBoxTransform(new(7.66f, y, z), new(.23f, .09f, .23f), 0));
            // Braces terminate above 2.3 m; the entire central walk stays open.
            WoodSegment(details, new(7.66f, 2.31f, z), new(7.66f, 2.66f, z - Mathf.Sign(z) * .42f), .09f, .09f);
            WoodSegment(details, new(7.66f, 2.31f, z), new(7.18f, 2.66f, z), .09f, .09f);
            FacilitySolid(porch, "PorchSideBeam" + z, new(2.50f, .16f, .15f), new(6.70f, 2.67f, z), timber, "wood_furniture");
            AddVisualBox(porch, "PorchDoorJamb" + z, new(.10f, 2.31f, .15f), new(5.83f, 1.155f, z * .57f), pale, "wood_painted_trim");
        }
        AddVisualBox(porch, "PorchDoorHead", new(.12f, .18f, 1.94f), new(5.84f, 2.34f, 0), pale, "wood_painted_trim");
        FacilitySolid(porch, "PorchFrontHeader", new(.17f, .18f, 3.45f), new(7.83f, 2.60f, 0), timber, "wood_furniture");
        AddVisualBox(porch, "PorchCarvedHeaderGround", new(.035f, .19f, 3.48f), new(7.93f, 2.60f, 0), pale, "wood_painted_trim");

        // A real small gable, with sheet iron matching the hall, and exposed
        // boarded underside. The central front panel is reserved for the sign.
        const float halfSpan = 1.84f, eaveY = 2.72f, ridgeY = 3.60f;
        var pitch = Mathf.Atan((ridgeY - eaveY) / halfSpan);
        var slopeLength = Mathf.Sqrt(halfSpan * halfSpan + (ridgeY - eaveY) * (ridgeY - eaveY));
        var roofRibs = new List<Transform3D>();
        foreach (var side in new[] { -1f, 1f })
        {
            var rotation = new Vector3(side * pitch, 0, 0);
            FacilitySolid(porch, "PorchBoardedRoof" + side, new(2.75f, .10f, slopeLength),
                new(6.655f, (eaveY + ridgeY) * .5f, side * halfSpan * .5f), timber, "wood_furniture", rotation);
            AddVisualBox(porch, "PorchIronRoof" + side, new(2.87f, .045f, slopeLength + .10f),
                new(6.685f, (eaveY + ridgeY) * .5f + .074f, side * halfSpan * .5f), MosqueIronColour, "roof_metal").Rotation = rotation;
            for (var i = 0; i < 8; i++)
                roofRibs.Add(new Transform3D(Basis.FromEuler(rotation) * Basis.FromScale(new(.027f, .025f, slopeLength + .10f)),
                    new(5.40f + i * .36f, (eaveY + ridgeY) * .5f + .103f, side * halfSpan * .5f)));
            WoodSegment(details, new(8.00f, eaveY, side * halfSpan), new(8.00f, ridgeY, 0), .12f, .12f);
            // Short sawn pendants sit outside the posts, never across a head.
            for (var i = 0; i < 9; i++)
                details.Add(MosqueBoxTransform(new(5.55f + i * .28f, 2.61f - (i % 2) * .025f, side * 1.83f), new(.11f, .20f, .045f), 0));
        }
        AddVisualBox(porch, "PorchIronRidge", new(2.93f, .07f, .17f), new(6.69f, 3.70f, 0), MosqueIronColour, "roof_metal");
        using (var front = new SurfaceTool())
        {
            front.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var p in new[] { new Vector3(7.96f, eaveY, -halfSpan), new Vector3(7.96f, eaveY, halfSpan), new Vector3(7.96f, ridgeY, 0) })
            { front.SetNormal(Vector3.Right); front.SetUV(new(p.Z, p.Y)); front.AddVertex(p); }
            porch.AddChild(new MeshInstance3D { Name = "PorchTimberGable", Mesh = front.Commit(), MaterialOverride = PainterlyMaterialLibrary.ForColor(timber, "wood_furniture") });
        }
        // Paired pointed leaves in relief, no scripts, crests or sacred signs.
        using (var leaves = new SurfaceTool())
        {
            leaves.Begin(Mesh.PrimitiveType.Triangles);
            for (var i = 0; i < 17; i++)
            {
                var z = -1.60f + i * .20f;
                WoodSegment(carving, new(7.958f, 2.57f, z - .085f), new(7.958f, 2.57f, z + .085f), .018f, .018f);
                foreach (var side in new[] { -1f, 1f })
                {
                    var a = new Vector3(7.965f, 2.57f, z);
                    var b = new Vector3(7.965f, 2.59f + side * .025f, z + .075f);
                    var c = new Vector3(7.965f, 2.57f + side * .078f, z + .095f);
                    var d = new Vector3(7.965f, 2.57f + side * .040f, z + .025f);
                    var reverse = (c-a).Cross(b-a).Dot(Vector3.Right) < 0;
                    foreach (var p in reverse ? new[] {a,c,b,a,d,c} : new[] {a,b,c,a,c,d})
                    { leaves.SetNormal(Vector3.Right); leaves.SetUV(new(p.Z, p.Y)); leaves.AddVertex(p); }
                }
            }
            porch.AddChild(new MeshInstance3D { Name = "PorchCarvedLeafRelief", Mesh = leaves.Commit(), MaterialOverride = PainterlyMaterialLibrary.ForColor(leaf, "wood_painted_trim") });
        }
        MosqueBatch(porch, "PorchCarvedTimberTrim", details, PainterlyMaterialLibrary.ForColor(pale, "wood_painted_trim"));
        MosqueBatch(porch, "PorchCarvedLeafStems", carving, PainterlyMaterialLibrary.ForColor(leaf, "wood_painted_trim"));
        MosqueBatch(porch, "PorchRoofProfiles", roofRibs, PainterlyMaterialLibrary.ForColor(MosqueIronColour, "roof_metal"));
        BuildMosqueWinterFrontGarden(porch);

        var approach = new[] { room.ToGlobal(new Vector3(10.42f, .035f, 0)), room.ToGlobal(new Vector3(8.35f, .035f, 0)),
            room.ToGlobal(new Vector3(6.40f, .035f, 0)), room.ToGlobal(new Vector3(5.90f, .035f, 0)) };
        room.SetMeta("entryApproach", approach[0]);
        room.SetMeta("entryApproachPath", approach);
        room.SetMeta("entryAccessPoint", approach[^1]);
        room.SetMeta("entryRepair", "level garden walk into timber porch; no exterior stairs or railings");
        return approach;
    }

    private void BuildMosqueWinterFrontGarden(Node3D porch)
    {
        var twigs = new List<Transform3D>();
        var edging = new List<Transform3D>();
        foreach (var side in new[] { -1f, 1f })
        {
            var z = side * 3.27f;
            // Low planted beds are lateral to the 2.4 m path. No fence crosses
            // the entrance or conceals its door; winter planting stays low.
            FacilitySolid(porch, "GardenBed" + side, new(3.90f, .14f, 2.35f), new(8.15f, -.005f, z), "625b48", "soil");
            foreach (var edgeZ in new[] { z - 1.20f, z + 1.20f })
                FacilitySolid(porch, "GardenStoneEdge" + side + "_" + edgeZ, new(4.00f, .17f, .10f), new(8.15f, .025f, edgeZ), "aaa18c", "stone");
            foreach (var x in new[] { 6.15f, 10.15f })
                FacilitySolid(porch, "GardenStoneEnd" + side + "_" + x, new(.10f, .17f, 2.35f), new(x, .025f, z), "aaa18c", "stone");
            for (var i = 0; i < 5; i++)
            {
                var x = 6.57f + i * .77f;
                var center = new Vector3(x, .29f, z + (i % 2 == 0 ? -.38f : .35f));
                porch.AddChild(new MeshInstance3D { Name = "GardenLowEvergreen" + side + "_" + i,
                    Position = center, Scale = new(.95f, .63f, .85f),
                    Mesh = new SphereMesh { Radius = .44f, Height = .88f, RadialSegments = 12, Rings = 6 },
                    MaterialOverride = PainterlyMaterialLibrary.ForColor(i % 2 == 0 ? "657568" : "71806a", "foliage") });
                porch.AddChild(new MeshInstance3D { Name = "GardenShrubSnow" + side + "_" + i,
                    Position = center + new Vector3(-.03f, .20f, .015f), Scale = new(1, .20f, .80f),
                    Mesh = new SphereMesh { Radius = .33f, Height = .66f, RadialSegments = 12, Rings = 6 },
                    MaterialOverride = PainterlyMaterialLibrary.ForColor("e4e9e7", "snow_ground") });
                for (var stem = 0; stem < 3; stem++)
                {
                    var from = new Vector3(x + .16f * stem - .16f, .065f, z + side * .79f);
                    var tip = from + new Vector3(.07f * (stem - 1), .34f + .08f * (stem % 2), .08f);
                    var delta = tip - from;
                    twigs.Add(new Transform3D(new Basis(new Quaternion(Vector3.Up, delta.Normalized())) * Basis.FromScale(new(.013f, delta.Length(), .013f)), (from + tip) * .5f));
                }
            }
            for (var i = 0; i < 12; i++)
                edging.Add(MosqueBoxTransform(new(6.35f + i * .325f, .10f, z + side * 1.12f), new(.25f, .06f, .14f), 0));
        }
        MosqueBatch(porch, "GardenDormantStems", twigs, PainterlyMaterialLibrary.ForColor("93836a", "wood_furniture"));
        MosqueBatch(porch, "GardenSettledSnowEdges", edging, PainterlyMaterialLibrary.ForColor("e4e9e7", "snow_ground"));
    }
}
