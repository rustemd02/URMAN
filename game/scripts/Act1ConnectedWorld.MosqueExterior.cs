using Godot;

namespace Urman.Godot;

/// <summary>Small painted timber mosque, with a roof minaret reached by a real
/// guarded staircase through the vestibule. Religious presentation remains subject to local review.</summary>
public partial class Act1ConnectedWorld
{
    private void RestyleVillageMosque()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var complex = core.GetNode<Node3D>("VillageMosqueComplex");
        var room = _mosqueRoom ?? throw new InvalidOperationException("The mosque room is built before its exterior.");
        const string plank = "6f8c7a", trim = "ece9df";
        var plankMaterial = PainterlyMaterialLibrary.ForColor(plank, "wood_painted_green");

        // Hall base: the room floor sits on the plinth, the authored walls are 5.2 m from the base.
        var hallBase = room.Position - new Vector3(0, .53f, 0);
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

        // Gabled hall roof is meshed around the stair/minaret footprint, never across its stairwell.
        foreach (var name in new[] { "MosqueRoofWest", "MosqueRoofEast", "MosqueRoofGableSouth", "MosqueRoofGableNorth", "MosqueDome", "MosqueDomeSnow" })
            if (complex.GetNodeOrNull<MeshInstance3D>(name) is { } old) old.Visible = false;
        if (core.GetNodeOrNull<Node3D>("DistantMinaretSilhouette") is { } oldMinaret)
        {
            oldMinaret.Visible = false;
            oldMinaret.SetMeta("presentationRole", "superseded by accessible roof minaret; retained legacy node");
        }
        BuildMosqueAccessibleMinaret();
        BuildMosqueRoofWithStairOpening();
        complex.SetMeta("exteriorStyle", "painted timber, ivory window trim, shaped gables, accessible roof minaret; layout v4 2026-10-02");
        complex.SetMeta("presentationOnly", false);
    }
}
