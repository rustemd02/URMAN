using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildBoardCrossing(Node3D core, CarryCoordinator carry)
    {
        // This shallow household drain follows the existing EastFenceSwale.
        // It sits on the authoritative terrain; neither a cut nor an invisible
        // bridge changes that terrain. The two ends support the ordinary board.
        var crossing = new Node3D { Name = "BabaiDrainBoardCrossing",
            Position = YardGround(-15.5f, 8.8f), RotationDegrees = new(0, -45, 0) };
        core.AddChild(crossing);
        ConformExistingDrainBank(core, crossing);
        var highestGround = float.NegativeInfinity;
        foreach (var x in new[] { -.78f, 0, .78f })
        foreach (var z in new[] { -2.35f, -.50f, 0, .50f, 2.35f })
            highestGround = Math.Max(highestGround, CrossingGround(crossing, x, z));
        var seatY = highestGround + .34f;
        var walkY = seatY + .081f; // .006 placement clearance + the .075 board.
        foreach (var side in new[] { -1, 1 })
        {
            AddCrossingBearingStone(crossing, side, seatY);
            AddCrossingSnowBank(crossing, side, seatY, walkY);
        }

        // The ice follows the local ground at every vertex. Its wandering,
        // narrowing rim has no upright walls or raised, level slab underneath.
        var iceFaces = new List<Vector3>();
        using (var bed = new SurfaceTool())
        {
            bed.Begin(Mesh.PrimitiveType.Triangles);
            var xs = new[] { -2.25f, -1.65f, -1.0f, -.45f, 0, .55f, 1.1f, 1.75f, 2.15f };
            var centres = new[] { -.03f, .04f, .09f, .05f, 0, -.04f, -.08f, -.04f, .025f };
            var widths = new[] { .025f, .23f, .38f, .44f, .46f, .40f, .29f, .18f, .025f };
            var vertices = new Vector3[xs.Length, 3];
            for (var x = 0; x < xs.Length; x++)
            for (var row = 0; row < 3; row++)
            {
                var z = centres[x] + (row - 1) * widths[x];
                var skin = row == 1 && x > 0 && x < xs.Length - 1 ? .018f : .006f;
                vertices[x, row] = new(xs[x], CrossingGround(crossing, xs[x], z) + skin, z);
            }
            for (var x = 0; x < xs.Length - 1; x++)
            for (var row = 0; row < 2; row++)
            {
                AddCrossingTopTriangle(bed, vertices[x, row], vertices[x + 1, row + 1], vertices[x, row + 1], iceFaces);
                AddCrossingTopTriangle(bed, vertices[x, row], vertices[x + 1, row], vertices[x + 1, row + 1], iceFaces);
            }
            AddCrossingMesh(crossing, "ShallowFrozenDrainBed", bed, "8e9da8", "ice", "ice", DrainIceMaterial());
        }

        // Move the existing single board before the coordinator enters the
        // tree. Old placed/held snapshots still override this authored rest.
        var board = carry.Items.Single(item => item.ItemId == "carry-board");
        const float storageX = 2.15f;
        const float storageZ = -.78f;
        var storageY = float.NegativeInfinity;
        foreach (var x in new[] { storageX - .23f, storageX + .23f })
        foreach (var z in new[] { storageZ - .90f, storageZ + .90f })
            storageY = Math.Max(storageY, CrossingGround(crossing, x, z) + .22f);
        foreach (var end in new[] { -1, 1 })
        {
            using var support = new SurfaceTool();
            support.Begin(Mesh.PrimitiveType.Triangles);
            var z = storageZ + end * .74f;
            AddCrossingPrism(support, crossing, storageX - .23f, storageX + .23f, z - .11f, z + .11f,
                (_, _) => storageY);
            AddCrossingMesh(crossing, $"BoardDryingBatten{end}", support, "847050", "wood", "wood");
        }
        board.Position = crossing.ToGlobal(new(storageX, storageY + .006f, storageZ));
        board.RotationDegrees = new(0, crossing.GlobalRotationDegrees.Y, 0);

        crossing.SetMeta("boardItemId", board.ItemId);
        crossing.SetMeta("clearSpanMetres", 1.0f);
        crossing.SetMeta("bearingPlane", crossing.ToGlobal(new(0, seatY, 0)));
        crossing.SetMeta("placementAim", crossing.ToGlobal(new(0, CrossingSurfaceHeight(crossing, iceFaces, 0, 0), 0)));
        crossing.SetMeta("unsupportedAim", crossing.ToGlobal(new(0, CrossingSurfaceHeight(crossing, iceFaces, 0, .32f), .32f)));
        crossing.SetMeta("pickupApproach", CrossingGroundPoint(crossing, storageX, -2.70f));
        crossing.SetMeta("nearApproach", CrossingGroundPoint(crossing, 0, -2.70f));
        crossing.SetMeta("farApproach", CrossingGroundPoint(crossing, 0, 2.70f));
        // Continue north of the existing lateral dwelling. The former diagonal
        // to (-14.86,13.25) led into its physical seni wall at X=-14.666.
        crossing.SetMeta("destinationApproach", GroundedYardPoint(new(-17.4f, 0, 11.3f)) + Vector3.Up * .025f);
        crossing.SetMeta("sourceBasis", "existing BabaiYard_EastFenceSwale; shallow stone-lined household runoff; winter 2026");
        crossing.SetMeta("runtimeStateOwner", "existing carry-board world.props transform and world.custody; no new progress flag");
    }

    private static float CrossingGround(Node3D crossing, float x, float z)
    {
        var p = crossing.ToGlobal(new(x, 0, z));
        return crossing.ToLocal(new(p.X, AgentBAct1HeightField.CollisionGround(p.X, p.Z), p.Z)).Y;
    }

    private static void ConformExistingDrainBank(Node3D core, Node3D crossing)
    {
        // The old shallow bank continues beyond this crossing. Its raised
        // surface used to pass through the later ice and snow meshes. Keep
        // its identity and distant shape, and blend only this occupied part
        // back below the shared ground that supports the winter crossing.
        var kit = core.GetNode<Node3D>("AgentBExteriorWorld/AgentB_TerrainRoadKit");
        var bank = FindDescendants<MeshInstance3D>(kit).Single(mesh => mesh.Name == "BabaiYard_EastFenceSwale");
        if (bank.Mesh is not ArrayMesh original || original.GetSurfaceCount() != 1)
            throw new InvalidOperationException("The existing drain bank must retain its one authored surface.");
        var arrays = original.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var uvValue = arrays[(int)Mesh.ArrayType.TexUV];
        var uv = uvValue.VariantType == Variant.Type.Nil ? Array.Empty<Vector2>() : uvValue.AsVector2Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var modifiedTriangles = 0;
        var emittedTriangles = 0;
        var affected = new bool[indices.Length / 3];
        var divisions = 1;
        for (var triangle = 0; triangle < indices.Length; triangle += 3)
        {
            var v = new[] { vertices[indices[triangle]], vertices[indices[triangle + 1]], vertices[indices[triangle + 2]] };
            var local = v.Select(point => crossing.ToLocal(bank.ToGlobal(point))).ToArray();
            if (local.Min(point => point.X) > 3.20f || local.Max(point => point.X) < -3.20f
                || local.Min(point => point.Z) > 3.20f || local.Max(point => point.Z) < -3.20f) continue;
            affected[triangle / 3] = true;
            var longest = Math.Max(v[0].DistanceTo(v[1]), Math.Max(v[1].DistanceTo(v[2]), v[2].DistanceTo(v[0])));
            divisions = Math.Max(divisions, Mathf.CeilToInt(longest / .35f));
        }
        // Neighbours must sample their shared edge at identical fractions:
        // per-triangle subdivisions would split the curved blend into seams.
        Vector3 Project(Vector3 point)
        {
            var world = bank.ToGlobal(point);
            var local = crossing.ToLocal(world);
            var weight = 1f - Mathf.SmoothStep(2.45f, 3.20f, Math.Max(Math.Abs(local.X), Math.Abs(local.Z)));
            var ground = AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .025f;
            world.Y = Mathf.Lerp(world.Y, Math.Min(world.Y, ground), weight);
            return bank.ToLocal(world);
        }
        void Emit(Vector3 a, Vector3 b, Vector3 c, Vector2 ta, Vector2 tb, Vector2 tc)
        {
            a = Project(a); b = Project(b); c = Project(c);
            var normal = (c - a).Cross(b - a).Normalized();
            surface.SetNormal(normal); surface.SetUV(ta); surface.AddVertex(a);
            surface.SetNormal(normal); surface.SetUV(tb); surface.AddVertex(b);
            surface.SetNormal(normal); surface.SetUV(tc); surface.AddVertex(c);
            emittedTriangles++;
        }
        for (var triangle = 0; triangle < indices.Length; triangle += 3)
        {
            var ids = new[] { indices[triangle], indices[triangle + 1], indices[triangle + 2] };
            var v = ids.Select(index => vertices[index]).ToArray();
            var t = ids.Select(index => uv.Length == vertices.Length ? uv[index] : Vector2.Zero).ToArray();
            if (!affected[triangle / 3])
            {
                foreach (var index in ids)
                {
                    surface.SetNormal(normals[index]);
                    surface.SetUV(uv.Length == vertices.Length ? uv[index] : Vector2.Zero);
                    surface.AddVertex(vertices[index]);
                }
                emittedTriangles++;
                continue;
            }
            modifiedTriangles++;
            Vector3 V(int i, int j) => v[0] + (v[1] - v[0]) * ((float)i / divisions) + (v[2] - v[0]) * ((float)j / divisions);
            Vector2 T(int i, int j) => t[0] + (t[1] - t[0]) * ((float)i / divisions) + (t[2] - t[0]) * ((float)j / divisions);
            for (var i = 0; i < divisions; i++)
            for (var j = 0; j < divisions - i; j++)
            {
                Emit(V(i,j), V(i+1,j), V(i,j+1), T(i,j), T(i+1,j), T(i,j+1));
                if (i+j < divisions-1)
                    Emit(V(i+1,j), V(i+1,j+1), V(i,j+1), T(i+1,j), T(i+1,j+1), T(i,j+1));
            }
        }
        var mesh = surface.Commit();
        mesh.SurfaceSetMaterial(0, original.SurfaceGetMaterial(0));
        bank.Mesh = mesh;
        bank.SetMeta("drainOriginalMesh", original);
        bank.SetMeta("drainReprofileOwner", crossing.GetPath().ToString());
        bank.SetMeta("drainModifiedTriangles", modifiedTriangles);
        bank.SetMeta("drainOutputTriangles", emittedTriangles);
        bank.SetMeta("supportOwner", "AgentB_TerrainCollision; existing crossing ice and snow surfaces");
        GD.Print($"act1-drain-bank-conform: mesh={bank.GetPath()} originalTriangles={indices.Length/3} affectedTriangles={modifiedTriangles} outputTriangles={emittedTriangles} groundOwner=unchanged");
    }

    private static Vector3 CrossingGroundPoint(Node3D crossing, float x, float z)
        => crossing.ToGlobal(new(x, CrossingGround(crossing, x, z) + .025f, z));

    private static void AddCrossingBearingStone(Node3D crossing, int side, float seatY)
    {
        // The worn crowns retain all four original board contacts: local
        // X +/- .1376, Z +/- .792. Only the surrounding bank was oversized.
        var rim = side < 0
            ? new Vector2[] { new(-.22f, .61f), new(.17f, .61f), new(.27f, .70f), new(.23f, .92f),
                new(.09f, .98f), new(-.19f, .96f), new(-.29f, .83f), new(-.28f, .68f) }
            : new Vector2[] { new(-.19f, .62f), new(.23f, .62f), new(.31f, .76f), new(.26f, .91f),
                new(.13f, .96f), new(-.24f, .94f), new(-.30f, .79f), new(-.26f, .68f) };
        var crown = new Vector3[rim.Length];
        var bevel = new Vector3[rim.Length];
        var foot = new Vector3[rim.Length];
        for (var i = 0; i < rim.Length; i++)
        {
            crown[i] = new(rim[i].X, seatY, side * rim[i].Y);
            bevel[i] = new(rim[i].X * 1.10f, seatY - .055f - (i % 3) * .009f,
                side * (.79f + (rim[i].Y - .79f) * 1.10f));
            var x = rim[i].X * 1.28f;
            var z = side * (.79f + (rim[i].Y - .79f) * 1.28f);
            foot[i] = new(x, CrossingGround(crossing, x, z) - .025f, z);
        }
        using var stone = new SurfaceTool();
        stone.Begin(Mesh.PrimitiveType.Triangles);
        var centre = new Vector3(0, seatY, side * .79f);
        for (var i = 0; i < rim.Length; i++)
        {
            var next = (i + 1) % rim.Length;
            AddCrossingTopTriangle(stone, centre, crown[i], crown[next]);
            AddCrossingTopTriangle(stone, crown[i], bevel[i], bevel[next]);
            AddCrossingTopTriangle(stone, crown[i], bevel[next], crown[next]);
            AddCrossingTopTriangle(stone, bevel[i], foot[i], foot[next]);
            AddCrossingTopTriangle(stone, bevel[i], foot[next], bevel[next]);
        }
        // Snow belongs to the real sloping ground around each stone. A light
        // powder on the worn crown leaves its bearing face legible in winter.
        var material = (ShaderMaterial)PainterlyMaterialLibrary.ForColor("76796d", "stone", sheltered: true);
        material.SetShaderParameter("snow_coverage", .16f);
        AddCrossingMesh(crossing, side < 0 ? "NearStoneBank" : "FarStoneBank", stone,
            "76796d", "stone", "stone", material);
        var wear = MechanismVisual(crossing, $"WornBearingSeat{side}", new(.31f, .0015f, .19f),
            new(0, seatY + .00075f, side * .79f), "a49b89", "stone");
        wear.MaterialOverride = PainterlyMaterialLibrary.ForColor("a49b89", "stone", sheltered: true);
    }

    private static void AddCrossingSnowBank(Node3D crossing, int side, float seatY, float walkY)
    {
        // Unequal shoulders merge into the same terrain on every outer edge.
        // This is one open ground skin, with the same triangles for walking;
        // there are no separate closed prisms or vertical pedestal walls.
        var leftWidth = side < 0 ? 1.44f : 1.86f;
        var rightWidth = side < 0 ? 1.96f : 1.36f;
        float Bend(float x) => side < 0 ? -.05f * x + .016f * x * x : .055f * x - .011f * x * x;
        float Height(float x, float z)
        {
            var ground = CrossingGround(crossing, x, z) + .004f;
            var q = Math.Abs(z) - Bend(x);
            var level = q switch
            {
                <= .40f => ground,
                < .66f => Mathf.Lerp(ground, seatY - .15f, Mathf.SmoothStep(.40f, .66f, q)),
                <= .80f => seatY - .15f,
                < .91f => Mathf.Lerp(seatY - .15f, seatY - .010f, (q - .80f) / .11f),
                < 1.08f => Mathf.Lerp(seatY - .010f, walkY, (q - .91f) / .17f),
                <= 1.26f => walkY,
                < 2.40f => Mathf.Lerp(walkY, ground, Mathf.SmoothStep(1.26f, 2.40f, q)),
                _ => ground
            };
            var width = x < 0 ? leftWidth : rightWidth;
            var shoulder = 1f - Mathf.SmoothStep(.32f, width, Math.Abs(x));
            return Mathf.Lerp(ground, Math.Max(ground, level), shoulder);
        }
        var us = new[] { -1f, -.76f, -.50f, -.30f, -.15f, 0, .15f, .30f, .50f, .76f, 1f };
        var qs = new[] { .40f, .52f, .66f, .80f, .91f, 1.08f, 1.26f, 1.52f, 1.90f, 2.40f };
        var vertices = new Vector3[us.Length, qs.Length];
        for (var i = 0; i < us.Length; i++)
        for (var j = 0; j < qs.Length; j++)
        {
            var x = us[i] * (us[i] < 0 ? leftWidth : rightWidth);
            var z = side * (qs[j] + Bend(x));
            vertices[i, j] = new(x, Height(x, z), z);
        }
        using var snow = new SurfaceTool();
        snow.Begin(Mesh.PrimitiveType.Triangles);
        var faces = new List<Vector3>();
        for (var i = 0; i < us.Length - 1; i++)
        for (var j = 0; j < qs.Length - 1; j++)
        {
            AddCrossingTopTriangle(snow, vertices[i, j], vertices[i + 1, j + 1], vertices[i, j + 1], faces);
            AddCrossingTopTriangle(snow, vertices[i, j], vertices[i + 1, j], vertices[i + 1, j + 1], faces);
        }
        AddCrossingMesh(crossing, side < 0 ? "NearSnowSlope" : "FarSnowSlope", snow,
            "eef2f6", "snow_ground", "snow");
        // Project every sole onto these actual triangles, including the slope.
        // The marks suggest a used household path without granting knowledge.
        for (var step = 0; step < 4; step++)
            AddCrossingBootPrint(crossing, new(step % 2 == 0 ? -.09f : .09f, 0, side * (1.32f + step * .33f)), side * 6,
                (x, z) => CrossingSurfaceHeight(crossing, faces, x, z));
    }

    private static void AddCrossingTopTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c,
        List<Vector3>? faces = null)
    {
        if ((c - a).Cross(b - a).Y < 0) (b, c) = (c, b);
        var normal = (c - a).Cross(b - a).Normalized();
        foreach (var vertex in new[] { a, b, c })
        {
            surface.SetNormal(normal);
            surface.SetUV(new(vertex.X, vertex.Z));
            surface.AddVertex(vertex);
            faces?.Add(vertex);
        }
    }

    private static float CrossingSurfaceHeight(Node3D crossing, IReadOnlyList<Vector3> faces, float x, float z)
    {
        for (var i = 0; i < faces.Count; i += 3)
        {
            var a = faces[i]; var b = faces[i + 1]; var c = faces[i + 2];
            var determinant = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
            if (Math.Abs(determinant) < .0000001f) continue;
            var u = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / determinant;
            var v = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / determinant;
            if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f)
                return u * a.Y + v * b.Y + (1f - u - v) * c.Y;
        }
        return CrossingGround(crossing, x, z);
    }

    private static void AddCrossingPrism(SurfaceTool surface, Node3D crossing,
        float x0, float x1, float z0, float z1, Func<float, float, float> top)
    {
        var v = new[] { new Vector3(x0, top(x0, z0), z0), new Vector3(x1, top(x1, z0), z0),
            new Vector3(x0, top(x0, z1), z1), new Vector3(x1, top(x1, z1), z1),
            new Vector3(x0, CrossingGround(crossing, x0, z0) - .02f, z0),
            new Vector3(x1, CrossingGround(crossing, x1, z0) - .02f, z0),
            new Vector3(x0, CrossingGround(crossing, x0, z1) - .02f, z1),
            new Vector3(x1, CrossingGround(crossing, x1, z1) - .02f, z1) };
        // Clockwise faces agree with the actual trimesh collision surface.
        var triangles = new[] { 0,3,2,0,1,3, 4,7,5,4,6,7, 0,5,1,0,4,5,
            2,7,6,2,3,7, 0,6,4,0,2,6, 1,7,3,1,5,7 };
        for (var triangle = 0; triangle < triangles.Length; triangle += 3)
        {
            var a = v[triangles[triangle]];
            var b = v[triangles[triangle + 1]];
            var c = v[triangles[triangle + 2]];
            // These are flat stone/ice faces. Smoothing their normals across
            // vertical rims made the real shallow surfaces look inflated.
            var normal = (c - a).Cross(b - a).Normalized();
            for (var corner = 0; corner < 3; corner++)
            {
                var vertex = v[triangles[triangle + corner]];
                surface.SetNormal(normal);
                surface.SetUV(new(vertex.X, vertex.Z));
                surface.AddVertex(vertex);
            }
        }
    }

    private static void AddCrossingMesh(Node3D parent, string name, SurfaceTool surface,
        string colour, string material, string footstep, Material? surfaceMaterial = null)
    {
        var mesh = surface.Commit();
        var visible = new MeshInstance3D { Name = name, Mesh = mesh,
            MaterialOverride = surfaceMaterial ?? PainterlyMaterialLibrary.ForColor(colour, material) };
        parent.AddChild(visible);
        var body = new StaticBody3D { Name = "ActualSurfaceContact", CollisionLayer = 1u, CollisionMask = 0u };
        body.SetMeta("collisionOwner", "board-crossing-visible-surface");
        body.SetMeta("footstepSurface", footstep);
        visible.AddChild(body);
        var contact = new CollisionShape3D { Shape = mesh.CreateTrimeshShape() };
        contact.SetMeta("authoredSourceMesh", visible.GetPath().ToString());
        body.AddChild(contact);
    }

    private static ShaderMaterial DrainIceMaterial()
    {
        // This colour/shelter variant is local to the drain, but remains in the
        // material owner's cache so graphics and reduced-motion settings apply.
        // Reuse the existing ice texture and its scale; frost changes the finish.
        var material = (ShaderMaterial)PainterlyMaterialLibrary.ForColor("8e9da8", "ice", sheltered: true);
        material.SetShaderParameter("roughness_value", .82f);
        material.SetShaderParameter("specular_value", .12f);
        material.SetShaderParameter("wet_grade", 0f);
        material.SetShaderParameter("snow_roughness_range", new Vector2(.77f, .90f));
        return material;
    }

    private static void AddCrossingBootPrint(Node3D parent, Vector3 at, float yaw, Func<float, float, float> height)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var rim = new[] { new Vector3(-.046f, 0, -.11f), new Vector3(.046f, 0, -.11f),
            new Vector3(.065f, 0, .035f), new Vector3(.040f, 0, .125f),
            new Vector3(-.040f, 0, .125f), new Vector3(-.065f, 0, .035f) };
        var basis = Basis.FromEuler(new(0, Mathf.DegToRad(yaw), 0));
        Vector3 Project(Vector3 point)
        {
            var local = at + basis * point;
            return local with { Y = height(local.X, local.Z) + .003f };
        }
        for (var edge = 0; edge < rim.Length; edge++)
        {
            surface.SetUV(new(.5f, .5f)); surface.AddVertex(Project(Vector3.Zero));
            surface.SetUV(new(rim[edge].X + .5f, rim[edge].Z + .5f)); surface.AddVertex(Project(rim[edge]));
            var next = rim[(edge + 1) % rim.Length];
            surface.SetUV(new(next.X + .5f, next.Z + .5f)); surface.AddVertex(Project(next));
        }
        surface.GenerateNormals();
        var print = new MeshInstance3D { Name = "WornBootSole", Mesh = surface.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("9caaa5", "snow_trampled"),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        print.SetMeta("visualOnly", true);
        parent.AddChild(print);
    }
}
