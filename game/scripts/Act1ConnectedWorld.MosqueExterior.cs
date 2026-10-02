using Godot;

namespace Urman.Godot;

/// <summary>
/// Village mosque exterior (author 2026-10-02: «мечеть нормальной сделать, чтобы минарет и
/// архитектура были норм»). The hall, its interior and contacts stay as built; only the
/// outside changes to the ordinary Tatar village type: painted plank walls with white
/// trim and arched window heads, a gable roof whose ridge runs along the hall, and the
/// minaret standing on that roof over the entrance — square base, eight-sided shaft with
/// a gallery, tented spire and crescent — instead of a separate tower beside the door.
/// Presentation only; the cultural review note on the complex stays open.
/// </summary>
public partial class Act1ConnectedWorld
{
    private void RestyleVillageMosque()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var complex = core.GetNode<Node3D>("VillageMosqueComplex");
        var room = _mosqueRoom ?? throw new InvalidOperationException("The mosque room is built before its exterior.");
        const string plank = "6f8c7a", trim = "ece9df", roofGreen = "4f6b5c";
        var plankMaterial = PainterlyMaterialLibrary.ForColor(plank, "wood_painted_green");

        // Hall base: the room floor sits on the plinth, the authored walls are 5.2 m from the base.
        var hallBase = room.Position - new Vector3(0, .53f, 0);
        var wallTop = hallBase.Y + 5.2f;
        foreach (var name in new[] { "MosqueHallWest", "MosqueHallEastLeft", "MosqueHallEastRight", "MosqueHallEastLintel" })
            if (complex.GetNodeOrNull<MeshInstance3D>(name) is { } shell) shell.MaterialOverride = plankMaterial;

        // Plank cladding on the two window walls (outside face only), following the
        // interior's own window piers so every opening stays open.
        foreach (var side in new[] { -1f, 1f })
        {
            var z = side * (4.25f + .29f);
            void Clad(string name, Vector3 size, Vector3 at) =>
                AddVisualBox(room, name, size, at with { Z = z }, plank, "wood_painted_green");
            Clad($"MosqueCladBelow{side}", new(11.1f, 1.8f, .03f), new(0, .37f, 0));
            Clad($"MosqueCladAbove{side}", new(11.1f, 2.05f, .03f), new(0, 3.675f, 0));
            foreach (var (x, width) in new[] { (-4.4f, 2.2f), (0f, 3.8f), (4.4f, 2.2f) })
                Clad($"MosqueCladPier{side}_{x}", new(width, 1.38f, .03f), new(x, 1.96f, 0));
            // White window surrounds with a pointed-arch head of short boards.
            foreach (var x in new[] { -2.6f, 2.6f })
            {
                var face = z + side * .03f;
                foreach (var dx in new[] { -.76f, .76f })
                    AddVisualBox(room, $"MosqueWindowJamb{side}_{x}_{dx}", new(.14f, 1.5f, .05f), new(x + dx, 1.96f, face), trim, "wood_painted_trim");
                AddVisualBox(room, $"MosqueWindowSill{side}_{x}", new(1.7f, .1f, .12f), new(x, 1.22f, face + side * .03f), trim, "wood_painted_trim");
                for (var i = 0; i < 6; i++)
                {
                    var a0 = Mathf.Pi * i / 6f; var a1 = Mathf.Pi * (i + 1) / 6f;
                    var p0 = new Vector2(Mathf.Cos(a0) * .76f, Mathf.Sin(a0) * .62f);
                    var p1 = new Vector2(Mathf.Cos(a1) * .76f, Mathf.Sin(a1) * .62f);
                    var mid = (p0 + p1) * .5f;
                    var piece = AddVisualBox(room, $"MosqueWindowArch{side}_{x}_{i}", new((p1 - p0).Length() + .05f, .13f, .05f),
                        new(x + mid.X, 2.68f + mid.Y, face), trim, "wood_painted_trim");
                    piece.Rotation = new Vector3(0, 0, Mathf.Atan2(p1.Y - p0.Y, p1.X - p0.X));
                }
            }
        }
        // White corner boards and a frieze band under the eaves.
        foreach (var x in new[] { -5.78f, 5.78f })
        foreach (var z in new[] { -4.52f, 4.52f })
            AddVisualBox(complex, $"MosqueCornerBoard{x}_{z}", new(.22f, 5.0f, .22f), hallBase + new Vector3(x, 2.6f, z), trim, "wood_painted_trim");
        foreach (var z in new[] { -4.56f, 4.56f })
            AddVisualBox(complex, $"MosqueFrieze{z}", new(11.7f, .32f, .06f), hallBase + new Vector3(0, 5.0f, z), trim, "wood_painted_trim");

        // Gable roof along the hall (east-west ridge); the old slabs and squat dome go.
        foreach (var name in new[] { "MosqueRoofWest", "MosqueRoofEast", "MosqueRoofGableSouth", "MosqueRoofGableNorth", "MosqueDome", "MosqueDomeSnow" })
            if (complex.GetNodeOrNull<MeshInstance3D>(name) is { } old) old.Visible = false;
        const float ridgeRise = 2.3f;
        var roofFrame = new Node3D { Name = "MosqueGableRoof", Position = hallBase with { Y = 0 }, RotationDegrees = new(0, 90, 0) };
        complex.AddChild(roofFrame);
        var roof = AddVisualPitchedRoof(roofFrame, "MosqueRoofMetal", 9.1f, 11.6f, wallTop, ridgeRise, .45f, roofGreen);
        roof.MaterialOverride = PainterlyMaterialLibrary.ForColor(roofGreen, "roof_metal");
        var snow = AddVisualPitchedRoof(roofFrame, "MosqueRoofSnow", 9.1f, 11.6f, wallTop + .04f, ridgeRise, .45f, "e6ebef");
        snow.MaterialOverride = PainterlyMaterialLibrary.ForColor("e6ebef", "snow_roof");
        var ridgeY = wallTop + .18f + ridgeRise;

        // The minaret stands on the ridge over the entrance (east end): a square plank base
        // straddling the roof, then the authored eight-sided shaft, gallery and tented spire.
        var minaretAt = hallBase with { X = hallBase.X + 5.5f - 2.0f };
        var baseBox = new Vector3(2.1f, 2.4f, 2.1f);
        AddVisualBox(complex, "MinaretRoofBase", baseBox, minaretAt with { Y = ridgeY - .9f + baseBox.Y * .5f - .6f }, plank, "wood_painted_green");
        AddVisualBox(complex, "MinaretRoofBaseCornice", new(2.4f, .18f, 2.4f), minaretAt with { Y = ridgeY + .9f }, trim, "wood_painted_trim");
        AddVisualBox(complex, "MinaretRoofBaseSnow", new(2.42f, .06f, 2.42f), minaretAt with { Y = ridgeY + 1.0f }, "eef2f6", "snow_ground");
        if (core.GetNodeOrNull<Node3D>("DistantMinaretSilhouette") is { } minaret)
        {
            // A village minaret is stout: wider than the tall distant silhouette, about 11 m above the roof.
            const float scale = .55f, girth = 1.15f;
            minaret.GlobalPosition = complex.ToGlobal(minaretAt with { Y = ridgeY + 1.0f - 1.925f * scale });
            minaret.Scale = new Vector3(girth, scale, girth);
            // The tower's own ground plinth would now float on the roof base.
            foreach (var name in new[] { "MinaretBase", "MinaretBaseSnow" })
                if (minaret.GetNodeOrNull<MeshInstance3D>(name) is { } groundPiece) groundPiece.Visible = false;
            foreach (var shaft in new[] { "MinaretShaft", "MinaretUpperShaft" })
                if (minaret.GetNodeOrNull<MeshInstance3D>(shaft) is { } piece)
                    piece.MaterialOverride = plankMaterial;
            if (minaret.GetNodeOrNull<MeshInstance3D>("MinaretSpire") is { } spire)
                spire.MaterialOverride = PainterlyMaterialLibrary.ForColor(roofGreen, "roof_metal");
            // Gallery rail on the balcony.
            for (var i = 0; i < 16; i++)
            {
                var a = Mathf.Tau * i / 16f;
                AddVisualBox(minaret, $"MinaretGalleryPost{i}", new(.06f, .7f, .06f),
                    new(Mathf.Cos(a) * .95f, 17.12f, Mathf.Sin(a) * .95f), trim, "wood_painted_trim");
            }
            var rail = new MeshInstance3D
            {
                Name = "MinaretGalleryRail", Position = new(0, 17.45f, 0),
                Mesh = new TorusMesh { InnerRadius = .91f, OuterRadius = .99f, Rings = 16, RingSegments = 4 },
                MaterialOverride = PainterlyMaterialLibrary.ForColor(trim, "wood_painted_trim")
            };
            minaret.AddChild(rail);
            // Crescent on a short rod above the spire tip, in an undistorted frame.
            var finial = new Node3D { Name = "MinaretFinial", Position = new(0, 21.4f, 0), Scale = new Vector3(1f / girth, 1f / scale, 1f / girth) * .62f };
            minaret.AddChild(finial);
            AddVisualBox(finial, "MinaretFinialRod", new(.05f, 1.1f, .05f), new(0, .55f, 0), "c9b26a", "metal");
            for (var i = 0; i < 9; i++)
            {
                var a0 = Mathf.DegToRad(-115 + i * 25f); var a1 = Mathf.DegToRad(-115 + (i + 1) * 25f);
                var p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * .42f; var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * .42f;
                var mid = (p0 + p1) * .5f;
                var thick = .09f * Mathf.Sin(Mathf.Pi * (i + .5f) / 9f) + .03f;
                var piece = AddVisualBox(finial, $"MinaretCrescent{i}", new((p1 - p0).Length() + .02f, thick, .05f),
                    new(mid.X - .1f, 1.35f + mid.Y, 0), "c9b26a", "metal");
                piece.Rotation = new Vector3(0, 0, Mathf.Atan2(p1.Y - p0.Y, p1.X - p0.X));
            }
            minaret.SetMeta("presentationRole", "minaret on the mosque roof over the entrance (Tatar village type)");
        }
        complex.SetMeta("exteriorStyle", "painted planks, white trim, arched windows, gable roof with roof-mounted minaret (relayout v3, 2026-10-02)");
    }
}
