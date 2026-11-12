using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private static readonly Vector2 CentralMosqueCentre = new(13.5f, 52f);

    // Exchange the existing buildings, not their identities. The office is
    // constructed here from the start so its foundation, steps and registered
    // address all use its new parcel rather than stale world-space coordinates.
    private Vector2 RelocatedSovkhozOfficeCentre
    {
        get
        {
            var anchor = AgentBAct1Layout.MosqueAnchor;
            var end = AgentBAct1Layout.MosqueWalkAxis[^1];
            var core = GetNode<Node3D>("Act1CoreWorldGreybox");
            var pivot = core.ToGlobal(new Vector3(end.X, 0, end.Y));
            var originalCentre = core.ToGlobal(new Vector3(anchor.X - 7.7f, 0, anchor.Y - 1f));
            var yaw = new Basis(Vector3.Up, Mathf.DegToRad(MosqueBuildingQiblaYawDegrees) - core.GlobalRotation.Y);
            var moved = core.ToLocal(pivot + yaw * (originalCentre - pivot));
            return new(moved.X, moved.Z);
        }
    }

    private static readonly string[] RetiredMosqueFrontOwners =
        ["MosqueYardWest", "MosqueYardNorth", "MosqueYardSouth", "MosqueYardGateLeft", "MosqueYardGateRight",
            "MosqueYardGatePostLeft", "MosqueYardGatePostRight"];

    private void PlaceMosqueOnCentralParcel(Node3D complex, Vector3 floorLocal)
    {
        if (complex.HasMeta("centralParcelPlacement"))
            throw new InvalidOperationException("Mosque placement must precede interior construction exactly once.");
        var former = complex.GlobalTransform;
        var hallCentre = complex.ToGlobal(floorLocal + new Vector3(-.7f, 0, 0));
        var targetCentre = complex.GetParent<Node3D>().ToGlobal(new Vector3(CentralMosqueCentre.X, 0, CentralMosqueCentre.Y));
        complex.GlobalPosition += new Vector3(targetCentre.X - hallCentre.X, 0,
            targetCentre.Z - hallCentre.Z);
        var groundContact = GetNode<CollisionShape3D>(
            "Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_TerrainCollision/AgentB_TerrainFaces");
        var terrain = (ConcavePolygonShape3D)groundContact.Shape;
        var worldFaces = terrain.GetFaces().Select(p => groundContact.GlobalTransform * p).ToArray();
        var approach = complex.ToGlobal(floorLocal + new Vector3(8f, 0, 0));
        var floorY = MosqueTerrainHeight(worldFaces, approach.X, approach.Z) + .04f;
        complex.GlobalPosition += Vector3.Up * (floorY - complex.ToGlobal(floorLocal).Y);
        complex.SetMeta("centralParcelPlacement", true);
        complex.SetMeta("formerParcelTransform", former);
        complex.SetMeta("placementPolicy", "exchange with existing sovkhoz office; same qibla basis and child identities; level garden entrance");

        // The legacy discovery resolves this non-child locator even though its
        // old mesh is hidden by MosqueExterior. Keep the locator with the mosque.
        if (complex.GetParent().GetNodeOrNull<Node3D>("DistantMinaretSilhouette") is { } silhouette)
            silhouette.GlobalPosition = complex.ToGlobal(floorLocal + MinaretCentre);
        RetireCentralMosqueSnowBank(complex);
        RetireMosqueFrontPanels(complex);
        var frame = new Node3D { Name = "CentralMosqueGradeFrame", Position = floorLocal };
        complex.AddChild(frame);
        GradeCentralMosqueApproach(frame);
        frame.Free();
    }

    private static void RetireCentralMosqueSnowBank(Node3D complex)
    {
        var bank = complex.GetNode<MeshInstance3D>("MosqueYardSnowBank");
        bank.Visible = false;
        bank.SetMeta("replacedByLevelGarden", true);
    }

    private static void RetireMosqueFrontPanels(Node3D complex)
    {
        foreach (var name in RetiredMosqueFrontOwners)
        {
            var mesh = complex.GetNode<MeshInstance3D>(name);
            mesh.Visible = false;
            mesh.SetMeta("replacedByLevelGarden", true);
        }
        var proxy = complex.GetNode<StaticBody3D>("MosqueCollisionProxy");
        foreach (var shape in proxy.GetChildren().OfType<CollisionShape3D>().Where(shape =>
            RetiredMosqueFrontOwners.Any(name => shape.Name == name + "_Blocker"
                || shape.Name == name + "_SurfaceContact")).ToArray())
        {
            proxy.RemoveChild(shape);
            shape.Free();
        }
    }

    // Called after the Babai terrain reset and before the mosque's paired
    // render/physics cuts. The first pass above lets foundations and courtyard
    // spans sample the same entrance grade they will finally stand upon.
    private void PrepareCentralMosqueGround()
    {
        var room = _mosqueRoom!;
        GradeCentralMosqueApproach(room);
        BuildCentralMosqueAccessPath();
        RetireMosqueFrontPanels(room.GetParent<Node3D>());
        // Cover both the occupied shell/annex and the new level garden, without
        // suppressing the surrounding square or modifying narrative objects.
        var foliageFrame = new Node3D { Name = "MosqueParcelFoliageFrame", Position = new(1f, 0, 0) };
        room.AddChild(foliageFrame);
        SuppressSquareFoliage(foliageFrame, 11.5f, 8f, 8f);
        ClipSnowInside(room, new(1f, 1.5f, 0), new(11.5f, 3f, 8f));
        var office = GetNode<Node3D>("Act1CoreWorldGreybox/SovkhozSquare/SovkhozOffice");
        SuppressSquareFoliage(office, 7.2f, 4.7f, 8f);
        ClipSnowInside(office, new(0, 3f, 0), new(7.2f, 4f, 5f));
    }

    private static readonly Vector2[] CentralMosqueAccessCorners =
        [new(-10f, -6.1f), new(11.3f, -6.1f), new(11.3f, 0), new(10.42f, 0)];

    private Vector3[] CentralMosqueTerrainFaces()
    {
        var contact = GetNode<CollisionShape3D>(
            "Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_TerrainCollision/AgentB_TerrainFaces");
        var toRoom = _mosqueRoom!.GlobalTransform.AffineInverse() * contact.GlobalTransform;
        return ((ConcavePolygonShape3D)contact.Shape).GetFaces().Select(p => toRoom * p).ToArray();
    }

    private Vector3[] ExtendCentralMosqueApproach(Vector3[] gardenApproach)
    {
        var terrain = CentralMosqueTerrainFaces();
        var outside = CentralMosqueAccessCorners.Take(CentralMosqueAccessCorners.Length - 1)
            .Select(p => _mosqueRoom!.ToGlobal(new Vector3(p.X, MosqueTerrainHeight(terrain, p.X, p.Y) + .035f, p.Y)));
        var approach = outside.Concat(gardenApproach).ToArray();
        _mosqueRoom!.SetMeta("entryApproach", approach[0]);
        _mosqueRoom.SetMeta("entryApproachPath", approach);
        return approach;
    }

    private void BuildCentralMosqueAccessPath()
    {
        // The east-facing door keeps the true qibla orientation. A continuous
        // visible walk joins Tukay via the clear southern edge of the building,
        // then turns outside the garden edging onto the existing porch contact.
        var terrain = CentralMosqueTerrainFaces();
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 P(Vector2 p) => new(p.X, MosqueTerrainHeight(terrain, p.X, p.Y) + .018f, p.Y);
        void Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            foreach (var p in new[] { a, b, c })
            {
                surface.SetUV(p * .5f);
                surface.AddVertex(P(p));
            }
        }
        const float halfWidth = .65f;
        for (var leg = 1; leg < CentralMosqueAccessCorners.Length; leg++)
        {
            var from = CentralMosqueAccessCorners[leg - 1]; var to = CentralMosqueAccessCorners[leg];
            var along = to - from;
            var side = new Vector2(-along.Y, along.X).Normalized() * halfWidth;
            var count = Mathf.CeilToInt(along.Length() / .5f);
            for (var i = 0; i < count; i++)
            {
                var a = from.Lerp(to, (float)i / count); var b = from.Lerp(to, (float)(i + 1) / count);
                Triangle(a - side, b - side, b + side);
                Triangle(a - side, b + side, a + side);
            }
        }
        foreach (var corner in CentralMosqueAccessCorners.Skip(1).Take(CentralMosqueAccessCorners.Length - 2))
        for (var i = 0; i < 12; i++)
        {
            var a = Mathf.Tau * i / 12; var b = Mathf.Tau * (i + 1) / 12;
            Triangle(corner, corner + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * halfWidth,
                corner + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * halfWidth);
        }
        surface.Index(); surface.GenerateNormals();
        var path = new MeshInstance3D { Name = "CentralMosqueTrampledApproach", Mesh = surface.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForPath("a19c89"), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        path.SetMeta("presentationRole", "Tukay to mosque garden; conforms to the same physical terrain; no extra ramp collider");
        _mosqueRoom!.AddChild(path);
    }

    private void GradeCentralMosqueApproach(Node3D roomFrame)
    {
        var exterior = GetNode<Node3D>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        var mesh = FindDescendants<MeshInstance3D>(exterior.GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Single(node => node.Name == "Terrain_Main");
        var contact = exterior.GetNode<CollisionShape3D>("AgentB_TerrainCollision/AgentB_TerrainFaces");
        if (mesh.Mesh is not ArrayMesh source || source.GetSurfaceCount() != 1
            || contact.Shape is not ConcavePolygonShape3D terrain)
            throw new InvalidOperationException("Central mosque requires the paired terrain owners.");
        if (source.HasMeta("centralMosqueApproachGrade")) return;
        using var arrays = source.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var toRoom = roomFrame.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        var toMesh = toRoom.AffineInverse();
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var toCore = core.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        for (var i = 0; i < vertices.Length; i++)
        {
            var local = toRoom * vertices[i];
            // A flat earthen entrance at the floor, with a four-metre smooth
            // shoulder. There are no discrete treads or invisible stair ramps.
            var distance = Mathf.Max(Mathf.Max(4f - local.X, local.X - 12f), Mathf.Abs(local.Z) - 5.8f);
            if (distance >= 4f) continue;
            var t = Mathf.Clamp(distance / 4f, 0f, 1f);
            var weight = 1f - t * t * (3f - 2f * t);
            // Neighbouring named parcels H020 (south) and H016 (north)
            // retain their ground; only the exchanged civic site is graded.
            var parcelZ = (toCore * vertices[i]).Z;
            var north = Mathf.Clamp((59.5f - parcelZ) / 2f, 0f, 1f);
            var south = Mathf.Clamp((parcelZ - 44f) / 2f, 0f, 1f);
            weight *= north * north * (3f - 2f * north) * south * south * (3f - 2f * south);
            local.Y = Mathf.Lerp(local.Y, -.04f, weight);
            vertices[i] = toMesh * local;
        }
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        using var geometry = new ArrayMesh();
        geometry.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        using var surface = new SurfaceTool();
        surface.CreateFrom(geometry, 0);
        surface.GenerateNormals();
        var graded = surface.Commit();
        graded.SurfaceSetMaterial(0, source.SurfaceGetMaterial(0));
        graded.SetMeta("centralMosqueApproachGrade", true);
        mesh.Mesh = graded;
        var toContact = contact.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        terrain.SetFaces(graded.GetFaces().Select(p => toContact * p).ToArray());
    }
}
