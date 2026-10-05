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
            var z = side * (4.85f + .29f);
            void Clad(string name, Vector3 size, Vector3 at) =>
                AddVisualBox(room, name, size, at with { Z = z }, plank, "wood_painted_green");
            Clad($"MosqueCladBelow{side}", new(12.5f, 1.8f, .03f), new(-.7f, .37f, 0));
            Clad($"MosqueCladAbove{side}", new(12.5f, 2.05f, .03f), new(-.7f, 3.675f, 0));
            foreach (var (x, width) in new[] { (-5.1f, 3.6f), (0f, 3.8f), (4.4f, 2.2f) })
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
        foreach (var x in new[] { -7.18f, 5.78f })
        foreach (var z in new[] { -5.12f, 5.12f })
            AddVisualBox(complex, $"MosqueCornerBoard{x}_{z}", new(.22f, 5.0f, .22f), hallBase + new Vector3(x, 2.6f, z), trim, "wood_painted_trim");
        foreach (var z in new[] { -5.16f, 5.16f })
            AddVisualBox(complex, $"MosqueFrieze{z}", new(13.1f, .32f, .06f), hallBase + new Vector3(-.7f, 5.0f, z), trim, "wood_painted_trim");

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
        BuildMosqueTimberJoints(complex);
        RepairMosqueEntranceTraversal(complex);
        complex.SetMeta("exteriorStyle", "painted timber, ivory window trim, shaped gables, accessible roof minaret; slim spiral lantern and spacious rooms 2026-10-04");
        complex.SetMeta("presentationOnly", false);
    }

    /// <summary>
    /// Traversal audit for the exterior entrance stair built by
    /// Act1ConnectedWorld.MosqueInterior.BuildMosqueEntrySteps. Every riser inside
    /// the flight is generated at <= 18 cm and any tread shorter than 26 cm throws
    /// at build time, but the yard ground -> lower landing step is outside that
    /// count: the landing top is the maximum terrain over its own footprint + 12 mm
    /// while the approach point 0.4 m outside the footprint is sampled separately.
    /// On the 2 m terrain grid with up to 22 cm of vertex jitter that first riser
    /// can pass FirstPersonController.MaximumStepHeight (.22 m). This pass measures
    /// the real built boxes; it adds one deep intermediate step only when the first
    /// riser exceeds a safe .20 m, and it closes the 25 mm doorway slot between the
    /// interior timber floor and the exterior landing with a flush sill. Interior
    /// risers and treads are verified from the builder's own meta, never moved.
    /// </summary>
    private void RepairMosqueEntranceTraversal(Node3D complex)
    {
        var room = _mosqueRoom;
        if (room is null
            || room.GetNodeOrNull<MeshInstance3D>("MosqueEntryLowerLanding") is not { Mesh: BoxMesh lower } lowerMesh
            || room.GetNodeOrNull<MeshInstance3D>("MosqueEntryUpperLanding") is not { Mesh: BoxMesh upper } upperMesh
            || room.GetNodeOrNull<MeshInstance3D>("MosqueTimberFloor") is not { Mesh: BoxMesh floor } floorMesh
            || !room.HasMeta("entryApproachPath"))
            return;
        var approach = room.GetMeta("entryApproachPath").AsVector3Array();
        if (approach.Length == 0) return;
        var lowerTop = lowerMesh.Position.Y + lower.Size.Y * .5f;
        var upperTop = upperMesh.Position.Y + upper.Size.Y * .5f;
        var floorTop = floorMesh.Position.Y + floor.Size.Y * .5f;
        // The authored approach anchor sits 35 mm above its terrain, so measure
        // the actual support under it rather than trusting the anchor's Y.
        float approachFeet;
        using (var supportRay = PhysicsRayQueryParameters3D.Create(
            approach[0] + Vector3.Up * .60f, approach[0] - Vector3.Up * .80f, 3u))
        using (var supportHit = room.GetWorld3D().DirectSpaceState.IntersectRay(supportRay))
            approachFeet = supportHit.Count > 0
                ? room.ToLocal(supportHit["position"].AsVector3()).Y
                : room.ToLocal(approach[0]).Y;
        // The controller allows a .22 m riser; keep a 2 cm reserve under it.
        const float safeRise = .20f;
        var firstRise = lowerTop - approachFeet;
        var firstStepAdded = firstRise > safeRise;
        if (firstStepAdded)
        {
            var direction = room.ToLocal(approach[0]) - lowerMesh.Position;
            direction.Y = 0;
            if (direction.LengthSquared() < .0001f) direction = Vector3.Back;
            direction = direction.Normalized();
            const float depth = .40f;
            var boxReach = Mathf.Abs(direction.X) * lower.Size.X * .5f + Mathf.Abs(direction.Z) * lower.Size.Z * .5f;
            var width = Mathf.Abs(direction.Z) * lower.Size.X + Mathf.Abs(direction.X) * lower.Size.Z;
            var top = approachFeet + firstRise * .5f;
            var bottom = Mathf.Min(top - .10f, approachFeet - .12f);
            var center = lowerMesh.Position + direction * (boxReach + depth * .5f);
            FacilitySolid(room, "MosqueEntryApproachStep", new(width, top - bottom, depth),
                new(center.X, (top + bottom) * .5f, center.Z), "827d6a", "stone")
                .SetMeta("traversalRepair",
                    $"first riser was {firstRise:0.000} m; split into two steps of {firstRise * .5f:0.000} m with a {depth:0.00} m tread");
        }
        // The interior timber floor ends at x 5.225 while the exterior landings
        // start at 5.25: a 25 mm open slot, 30 mm deep, sits exactly in the
        // doorway. The capsule bridges it, but a flush sill removes the gap
        // entirely; two 1 mm seams stay far below any query and cannot z-fight.
        var roomFloorEast = floorMesh.Position.X + floor.Size.X * .5f;
        var landingWest = upperMesh.Position.X - upper.Size.X * .5f;
        var thresholdSlot = landingWest - roomFloorEast;
        var sillAdded = thresholdSlot > .002f && Mathf.Abs(floorTop - upperTop) < .002f;
        if (sillAdded)
        {
            var zMin = upperMesh.Position.Z - upper.Size.Z * .5f;
            var zMax = upperMesh.Position.Z + upper.Size.Z * .5f;
            if (room.GetNodeOrNull<MeshInstance3D>("MosqueEntryDoorLanding") is { Mesh: BoxMesh door } doorMesh)
            {
                zMin = Mathf.Min(zMin, doorMesh.Position.Z - door.Size.Z * .5f);
                zMax = Mathf.Max(zMax, doorMesh.Position.Z + door.Size.Z * .5f);
            }
            var sillWidth = Mathf.Max(.010f, thresholdSlot - .002f);
            FacilitySolid(room, "MosqueEntryThresholdSill", new(sillWidth, .12f, zMax - zMin),
                new(roomFloorEast + .001f + sillWidth * .5f, floorTop - .06f, (zMin + zMax) * .5f),
                "827d6a", "stone").SetMeta("traversalRepair",
                $"closed a {thresholdSlot:0.000} m x 0.030 m doorway slot between the interior floor and the exterior landing");
        }
        var innerRise = (float)room.GetMeta("entryRiseHeight", 0).AsDouble();
        var treadDepth = (float)room.GetMeta("entryTreadDepth", 0).AsDouble();
        var audit = $"firstRise={firstRise:0.000} approachFeet={approachFeet:0.000} lowerTop={lowerTop:0.000} "
            + $"upperTop={upperTop:0.000} innerRise={innerRise:0.000} treadDepth={treadDepth:0.000} "
            + $"thresholdSlotBefore={thresholdSlot:0.000} firstStepAdded={firstStepAdded} sillAdded={sillAdded}";
        room.SetMeta("entranceTraversalAudit", audit);
        complex.SetMeta("entranceTraversalAudit", audit);
        GD.Print("act1-mosque-entrance: " + audit);
        if (innerRise > safeRise)
            GD.PushWarning($"Mosque entrance flight has an interior riser of {innerRise:0.000} m above the safe {safeRise:0.000} m margin.");
    }
}
