using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public static partial class StyleBenchmarkInteriorFactory
{
    // Runs on the saved interior plan, before its exterior projection is made.
    // Keep the named owners and their transforms: only their local surfaces change.
    private static void DressBabaiWingFurniture(Node3D wing)
    {
        void Fabric(string path, float radius)
        {
            var visible = wing.GetNode<MeshInstance3D>(path + "/Visible");
            var bounds = visible.Mesh.GetAabb();
            visible.Mesh = RuralPropGeometry.BevelBox(bounds.Size, radius);
        }
        Fabric("Bedroom/BedMattress", .09f);
        Fabric("Bedroom/BedQuilt", .045f);
        Fabric("Bedroom/BedQuiltBorder", .035f);
        for (var i = 0; i < 4; i++) Fabric("Bedroom/BedPillow" + i, .075f);
        Fabric("Attic/AtticMattress", .07f);
        Fabric("Attic/AtticPillow", .06f);
        Fabric("Attic/AtticQuilt", .045f);
        Fabric("Attic/AtticBedBlanketFold", .04f);

        var cradle = wing.GetNode<StaticBody3D>("Attic/AtticCradle");
        var pieces = new List<(Vector3 Size, Vector3 Centre)>();
        // All timber stays inside the original .90 x .35 x .50 envelope.
        // The floor is a real shallow bed, with open rails instead of a solid cube.
        pieces.Add((new(.82f, .035f, .42f), new(0, -.105f, 0)));
        foreach (var x in new[] { -.425f, .425f })
        foreach (var z in new[] { -.225f, .225f })
            pieces.Add((new(.05f, .35f, .05f), new(x, 0, z)));
        foreach (var z in new[] { -.225f, .225f })
        {
            pieces.Add((new(.80f, .04f, .05f), new(0, .155f, z)));
            pieces.Add((new(.80f, .04f, .05f), new(0, -.09f, z)));
            for (var i = 0; i < 7; i++)
                pieces.Add((new(.022f, .225f, .025f), new(-.33f + i * .11f, .0325f, z)));
        }
        foreach (var x in new[] { -.425f, .425f })
        {
            pieces.Add((new(.05f, .04f, .40f), new(x, .155f, 0)));
            for (var i = 0; i < 3; i++)
                pieces.Add((new(.025f, .225f, .022f), new(x, .0325f, -.11f + i * .11f)));
        }
        cradle.GetNode<MeshInstance3D>("Visible").Mesh = WingFurnitureTimber(pieces, .006f);
        // Retain the old contact node/name, repurpose it for the visible bed floor.
        var contact = cradle.GetNode<CollisionShape3D>("Contact");
        for (var i = 0; i < pieces.Count; i++)
        {
            var shape = i == 0 ? contact : new CollisionShape3D { Name = "RailContact" + i };
            shape.Shape = new BoxShape3D { Size = pieces[i].Size };
            shape.Position = pieces[i].Centre;
            if (i != 0) cradle.AddChild(shape);
        }
        RuralPropGeometry.Block(cradle, "CradleBedding", new(.77f, .045f, .37f), new(0, -.065f, 0),
            PainterlyMaterialLibrary.ForColor("d5cdb8", "fabric_upholstery", sheltered: true), .02f);

        var tv = wing.GetNode<StaticBody3D>("Attic/AtticOldTv");
        var casing = new List<(Vector3 Size, Vector3 Centre)>
        {
            (new(.50f, .42f, .32f), new(0, 0, .04f)),
            (new(.50f, .045f, .08f), new(0, .1875f, -.16f)),
            (new(.50f, .045f, .08f), new(0, -.1875f, -.16f)),
            (new(.045f, .33f, .08f), new(-.2275f, 0, -.16f)),
            (new(.065f, .33f, .08f), new(.2175f, 0, -.16f))
        };
        var tvMesh = WingFurnitureTimber(casing, .012f);
        tv.GetNode<MeshInstance3D>("Visible").Mesh = tvMesh;
        // The shell recess is shallow; use its actual surface for the fixed contact.
        tv.GetNode<CollisionShape3D>("Contact").Shape = tvMesh.CreateTrimeshShape();
        var screen = wing.GetNode<MeshInstance3D>("Attic/AtticOldTvGlass/Visible");
        screen.Mesh = RuralPropGeometry.BevelBox(new(.36f, .30f, .02f), .009f);
        screen.Position = new(-.01f, 0, .035f);
        var knob = RuralPropGeometry.Lathe("WingTvKnob", [new(0, 0), new(.018f, 0),
            new(.020f, .004f), new(.018f, .018f), new(0, .018f)], 20);
        var plastic = PainterlyMaterialLibrary.ForColor("625c50", "plastic_abs", sheltered: true);
        foreach (var y in new[] { -.09f, .035f })
            RuralPropGeometry.Part(tv, "TuningKnob" + y, knob, new(.215f, y, -.179f), plastic, new(-90, 0, 0));
    }

    private static ArrayMesh WingFurnitureTimber(List<(Vector3 Size, Vector3 Centre)> parts, float bevel)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var part in parts)
            surface.AppendFrom(RuralPropGeometry.BevelBox(part.Size, bevel), 0,
                new Transform3D(Basis.Identity, part.Centre));
        return surface.Commit();
    }
}
