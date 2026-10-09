using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// VIS-077: the large-mass tier of the new snow standard, and VIS-079: the single
/// exclusion set every snow owner consults.
///
/// Before this pass the winter ground had two levels of snow only where somebody had
/// already built them: the ploughed street shoulder and the trodden door paths. Every
/// other open metre — the field between the street and the forest, the green in front of
/// a yard, the arrival verge — was the flat terrain mesh with a snow material on it,
/// which is exactly the W3 anti-example. <see cref="BuildWinterSnowMass"/> adds the
/// missing mass tier: wind and shovel drifts 18–40 cm above the real ground, seeded from
/// the collision height field and kept off every road, approach, solid and footprint by
/// <see cref="PublishSnowExclusions"/>. One mesh per drift, the same authored bank
/// profile the street already uses, so nothing here invents a second snow vocabulary.
///
/// Both passes are presentation-only. The terrain collider, the navigation and the
/// address graph are not touched; the drifts are trimmed by the existing relief clipper
/// when they would otherwise show through a wall, porch or deck.
/// </summary>
public partial class Act1ConnectedWorld
{
    /// <summary>Grid step of the drift survey, in metres. Six metres is coarse enough that
    /// neighbouring drifts read as separate masses instead of one continuous ridge.</summary>
    private const float SnowMassGridStep = 6f;
    private const int SnowMassDriftBudget = 64;
    /// <summary>Corridor half-widths published for the exclusion set: a door walk is a
    /// 0.9 m pedestrian lane, a yard gate is driven through and needs more room.</summary>
    private const float DoorWalkCorridorHalf = .45f;
    private const float YardGateCorridorHalf = .60f;
    /// <summary>A verified trodden route keeps this half-width clear of snow relief.</summary>
    private const float VerifiedRouteCorridorHalf = .62f;

    private bool _snowExclusionsPublished;
    private bool _snowMassBuilt;
    private int _snowMassDrifts;
    private int _snowMassVertices;

    /// <summary>
    /// VIS-079: publishes once, additively, everything that counts as an obstacle or a
    /// required corridor for snow relief — addressed door walks, addressed yard gates, the
    /// building footprints of the address registry and the world-space AABB of every solid
    /// collision box. Later calls only add owners that were not published before, so a
    /// route verified after the first frame still gets its corridor.
    /// </summary>
    private void PublishSnowExclusions()
    {
        if (_snowExclusionsPublished) return;
        _snowExclusionsPublished = true;
        var corridors = 0; var solids = 0; var footprints = 0;
        foreach (var shape in FindDescendants<CollisionShape3D>(this))
        {
            if (shape.Disabled || shape.Shape is not BoxShape3D box || !shape.IsInsideTree()) continue;
            if (shape.GetParent() is not StaticBody3D body || body.CollisionLayer == 0) continue;
            var transform = shape.GlobalTransform;
            var world = transform * new Aabb(-box.Size * .5f, box.Size);
            if (SnowReliefStandard.PublishSolid(shape.GetPath().ToString(), world)) solids++;
        }
        if (AddressRegistry is not { } registry)
        {
            SetMeta("snowExclusionCorridors", corridors);
            GD.Print($"act1-snow-exclusions: corridors={corridors} solids={solids} footprints={footprints} registry=absent");
            return;
        }
        foreach (var access in registry.AccessPoints.Values)
        {
            if (registry.Graph.Nearest(access.Position) is not { } link) continue;
            if (SnowReliefStandard.PublishCorridor("access:" + access.AccessId,
                    new Vector2((float)access.Position.X, (float)access.Position.Z),
                    new Vector2((float)link.Point.X, (float)link.Point.Z), DoorWalkCorridorHalf)) corridors++;
        }
        foreach (var gate in FindDescendants<Node3D>(this).ToArray())
        {
            if (!gate.IsVisibleInTree() || !gate.Name.ToString().Contains("Gate", StringComparison.Ordinal)) continue;
            if (gate.GetParent().Name.ToString().Contains("Gate", StringComparison.Ordinal)) continue;
            if (!IsAddressedParcelGate(gate)) continue;
            var at = gate.GlobalPosition;
            if (registry.Graph.Nearest(AddressPoint(AddressGround(new Vector3(at.X, 0f, at.Z)))) is not { } link) continue;
            if (SnowReliefStandard.PublishCorridor($"gate:{gate.GetPath()}", new Vector2(at.X, at.Z),
                    new Vector2((float)link.Point.X, (float)link.Point.Z), YardGateCorridorHalf)) corridors++;
        }
        foreach (var building in registry.Buildings.Values)
        {
            if (building.Footprint.Count < 3) continue;
            var polygon = new Vector2[building.Footprint.Count];
            for (var i = 0; i < polygon.Length; i++)
                polygon[i] = new Vector2((float)building.Footprint[i].X, (float)building.Footprint[i].Z);
            if (SnowReliefStandard.PublishFootprint("footprint:" + building.BuildingId, polygon)) footprints++;
        }
        SetMeta("snowExclusionCorridors", corridors);
        SetMeta("snowExclusionSolids", solids);
        SetMeta("snowExclusionFootprints", footprints);
        GD.Print($"act1-snow-exclusions: corridors={corridors} solids={solids} footprints={footprints} " +
                 $"totalCorridors={SnowReliefStandard.CorridorCount}");
    }

    /// <summary>The corridor half a verified winter footpath publishes for itself, read by
    /// the relief passes so a trodden route is never buried by a drift or a bank.</summary>
    internal static float WinterRouteCorridorHalf => VerifiedRouteCorridorHalf;

    /// <summary>
    /// VIS-077 large mass: open-ground drifts. A candidate cell is accepted only on gentle
    /// ground, outside every cut, at least <see cref="MassRoadClearance"/> beyond the
    /// carriageway edge, clear of the published exclusion set and off the paved square; the
    /// hash makes the pattern deterministic, so two runs of the same build agree.
    /// </summary>
    private void BuildWinterSnowMass()
    {
        if (_snowMassBuilt) return;
        _snowMassBuilt = true;
        var root = new Node3D { Name = "WinterSnowMass" };
        root.SetMeta("presentationOnly", true);
        root.SetMeta("visualOnly", true);
        root.SetMeta("collisionOwner", "none");
        root.SetMeta("navigationOwner", "none");
        root.SetMeta("interactionOwner", "none");
        root.SetMeta("supportOwner", nameof(AgentBAct1HeightField));
        root.SetMeta("presentationRole", "VIS-077 large snow mass: wind and shovel drifts on open winter ground");
        AddChild(root);
        for (var z = -84f; z <= 126f && _snowMassDrifts < SnowMassDriftBudget; z += SnowMassGridStep)
        for (var x = -44f; x <= 46f && _snowMassDrifts < SnowMassDriftBudget; x += SnowMassGridStep)
        {
            if (!TrySnowMassSite(x, z, out var noise)) continue;
            AddSnowMassDrift(root, _snowMassDrifts, x, z, noise);
        }
        root.SetMeta("snowMassDrifts", _snowMassDrifts);
        root.SetMeta("snowMassVertices", _snowMassVertices);
        root.SetMeta("snowMassHeightRange",
            $"{SnowReliefStandard.MassHeightMin:0.###}-{SnowReliefStandard.MassHeightMax:0.###}");
        GD.Print($"act1-snow-mass: drifts={_snowMassDrifts} vertices={_snowMassVertices} " +
                 $"band={SnowReliefStandard.ElevationBand:0.###} heightMin={SnowReliefStandard.MassHeightMin:0.###} " +
                 $"heightMax={SnowReliefStandard.MassHeightMax:0.###}");
    }

    private static bool TrySnowMassSite(float x, float z, out double noise)
    {
        // The threshold is what spreads the budget over the whole surveyed band instead of
        // filling it from the southern end: at .78 about one cell in five is a candidate, and
        // the road, corridor, solid and footprint exclusions take that to the order of the cap.
        noise = AgentBAct1HeightField.ValueNoise(x * .041 + 11.0, z * .037 - 5.0);
        if (noise <= .78) return false;
        var at = new Vector2(x, z);
        if (AgentBAct1Layout.InsidePlaza(at, 2.5f)) return false;
        if (AgentBAct1HeightField.RoadVergeClearance(x, z) < SnowReliefStandard.MassRoadClearance) return false;
        if (AgentBAct1HeightField.GroundSlope(x, z) > SnowReliefStandard.MassMaximumSlope) return false;
        // A drift must not stand on the lip of either cut, and never inside one: the gorge
        // and the ravine keep their depth (VIS-015 acceptance: no new white block in the cut).
        // The probe reach covers a drift's own half length plus its meander, so no part of
        // the mass can overhang a rim that the centre test alone would have missed.
        if (NearSnowMassCut(x, z, 5.5f)) return false;
        return !SnowReliefStandard.Blocked(at, SnowReliefStandard.MassSolidMargin);
    }

    private static bool NearSnowMassCut(float x, float z, float reach)
    {
        foreach (var offset in new[] { Vector2.Zero, new Vector2(reach, 0f), new Vector2(-reach, 0f),
                     new Vector2(0f, reach), new Vector2(0f, -reach) })
            if (AgentBAct1HeightField.InsideGorgeOrRavine(x + offset.X, z + offset.Y)) return true;
        return false;
    }

    /// <summary>One drift: a meandering 4.5–8 m ridge 2.6–3.6 m across, built with the
    /// authored bank profile so the mass tier and the street shoulder are the same snow.
    /// The crest wanders with the same hash, which is what keeps a 6 m drift from reading
    /// as an engineered berm.</summary>
    private void AddSnowMassDrift(Node3D root, int index, float x, float z, double noise)
    {
        var bearing = (float)(noise * Mathf.Tau * 3.7 + index * .9f);
        var along = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
        var across = new Vector2(-along.Y, along.X);
        var length = 4.5f + (float)(noise * 7.0 % 3.5);
        var width = 2.6f + (float)(noise * 13.0 % 1.0);
        var height = SnowReliefStandard.MassHeightMin
            + (float)(noise * 29.0 % 1.0) * (SnowReliefStandard.MassHeightMax - SnowReliefStandard.MassHeightMin);
        using var curve = new Curve3D { BakeInterval = .4f };
        for (var k = 0; k <= 3; k++)
        {
            var t = k / 3f - .5f;
            var meander = (float)((AgentBAct1HeightField.ValueNoise(x + k * 3.7, z - k * 2.1) - .5) * 1.4);
            var point = new Vector2(x, z) + along * (t * length) + across * meander;
            curve.AddPoint(new Vector3(point.X, 0f, point.Y));
        }
        var mesh = AddVisualLandformSurface(root, $"SnowDrift_{index:00}", width, height, curve.GetBakedLength(),
            Vector3.Zero, "e8edf0", "snow_ground", 0f, true, curve);
        mesh.SetMeta("snowTier", SnowReliefStandard.TierMass);
        mesh.SetMeta("snowBankHeight", height);
        mesh.SetMeta("presentationOnly", true);
        mesh.SetMeta("collisionOwner", "none");
        mesh.SetMeta("presentationRole", "VIS-077 large snow mass drift; visual only, no traversal ownership");
        _snowMassDrifts++;
        SoftenSnowDrift(mesh);
        if (mesh.Mesh is ArrayMesh array)
        {
            using var arrays = array.SurfaceGetArrays(0);
            _snowMassVertices += arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
        }
    }

    /// <summary>The authored drift section has nine columns and a plateau with creased
    /// shoulders. The same softening as the street banks (rounded rise grid, doubled columns
    /// across) makes it read as wind-packed snow; footprint, height range, metas and material
    /// are untouched, and nothing rises above what the authored drift already did.</summary>
    private static void SoftenSnowDrift(MeshInstance3D mesh)
    {
        const int authoredColumns = 9;
        if (mesh.Mesh is not ArrayMesh source || source.GetSurfaceCount() == 0) return;
        using var arrays = source.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        if (arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil || !CanRefineSnowGrid(arrays)) return;
        if (vertices.Length == 0 || vertices.Length % authoredColumns != 0) return;
        var stations = vertices.Length / authoredColumns;
        var uvs = arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil
            ? null : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        if (!RefineSnowGrid(mesh, vertices, uvs, stations, authoredColumns, null,
                out var softVertices, out var softUvs, out var softColumns, out _)) return;
        var material = source.SurfaceGetMaterial(0);
        var soft = new ArrayMesh();
        soft.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,
            BuildSnowGridArrays(softVertices, softUvs, stations, softColumns));
        if (material is not null) soft.SurfaceSetMaterial(0, material);
        mesh.Mesh = soft;
    }
}
