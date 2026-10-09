using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Snow relief (banks, drifts, aprons, trodden paths) is laid by many owners before the
/// buildings, bridges and porches around it settle. Once the world is complete, every
/// triangle of such relief that lies inside a solid box — a wall, foundation, porch,
/// bridge deck — is dropped, so no drift shows through a house or as a sheet across a deck.
/// Snow caps resting on props are not relief and stay untouched.
/// </summary>
public partial class Act1ConnectedWorld
{
    private bool _snowReliefClipped;
    // Real interior volumes registered by hollow civic buildings. The plinth box
    // below the floor cannot catch a drift that leans over the floor line and is
    // visible inside the room; the vertical window below has the same shape as
    // the solid-architecture boxes.
    private readonly List<(Transform3D Inverse, Vector3 Half, Aabb Bounds)> _interiorSnowClippers = new();

    /// <summary>Registers one real room volume: snow relief whose triangle centre
    /// lies inside it (walls included) is dropped from the visible mesh. Called by
    /// the square builders before the first frame, like every other solid owner.</summary>
    private void ClipSnowInside(Node3D owner, Vector3 centre, Vector3 half)
    {
        var transform = owner.GlobalTransform * new Transform3D(Basis.Identity, centre);
        _interiorSnowClippers.Add((transform.AffineInverse(), half, transform * new Aabb(-half, half * 2f)));
    }

    private void ClipSnowReliefUnderStructures()
    {
        if (_snowReliefClipped) return;
        _snowReliefClipped = true;
        // VIS-077/079, in this order and only once: publish the one exclusion set
        // (RoadInfo + access lines + solids + footprints), lay the large-mass tier while the
        // clipper can still trim it against the buildings, then break and reprofile the
        // street banks so no required approach is buried. Only after that does the relief
        // clip run, so every new mesh obeys the same rule as the authored ones.
        PublishSnowExclusions();
        BuildWinterSnowMass();
        ReworkStreetSnowBanks();
        var boxes = new List<(Transform3D Inverse, Vector3 Half, Aabb Bounds)>();
        foreach (var shape in FindDescendants<CollisionShape3D>(this))
        {
            if (shape.Disabled || shape.Shape is not BoxShape3D box || !shape.IsInsideTree()) continue;
            if (shape.GetParent() is not StaticBody3D body || body.CollisionLayer == 0) continue;
            // Solid architecture only: at least a metre across both ways (walls, plinths,
            // porches, decks); rails, posts and fence boards may stand in a drift.
            if (Mathf.Min(box.Size.X, box.Size.Z) < 1f) continue;
            var transform = shape.GlobalTransform;
            boxes.Add((transform.AffineInverse(), box.Size * .5f, transform * new Aabb(-box.Size * .5f, box.Size)));
        }
        boxes.AddRange(_interiorSnowClippers);
        int meshes = 0, dropped = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (mesh.Mesh is not ArrayMesh source || !IsSnowRelief(mesh)) continue;
            var bounds = mesh.GlobalTransform * source.GetAabb();
            var near = boxes.Where(box => box.Bounds.Grow(.6f).Intersects(bounds)).ToArray();
            if (near.Length == 0) continue;
            // Up to 60 cm below a deck or slab still counts: snow does not lie under a
            // footbridge or porch, it would only show through the gaps.
            bool InWindow((Transform3D Inverse, Vector3 Half, Aabb Bounds) box, Vector3 point)
            {
                var local = box.Inverse * point;
                return local.Y < box.Half.Y + .05f && local.Y > -box.Half.Y - .6f;
            }
            bool Inside(Vector3 point) => near.Any(box =>
            {
                var local = box.Inverse * point;
                return Mathf.Abs(local.X) < box.Half.X && Mathf.Abs(local.Z) < box.Half.Z && InWindow(box, point);
            });
            var result = new ArrayMesh();
            var changed = false;
            for (var s = 0; s < source.GetSurfaceCount(); s++)
            {
                using var arrays = source.SurfaceGetArrays(s);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var indices = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                    ? Enumerable.Range(0, vertices.Length).ToArray()
                    : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                var splitter = SnowSurfaceSplitter.TryCreate(arrays, vertices);
                var kept = new List<int>(indices.Length);
                for (var t = 0; t + 2 < indices.Length; t += 3)
                {
                    int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
                    var w0 = mesh.ToGlobal(vertices[i0]); var w1 = mesh.ToGlobal(vertices[i1]); var w2 = mesh.ToGlobal(vertices[i2]);
                    var centre = (w0 + w1 + w2) / 3f;
                    if (splitter is null)
                    {
                        // Unknown vertex attributes: keep the conservative whole-triangle rule.
                        if (Inside(centre)) { dropped++; changed = true; continue; }
                        kept.AddRange([i0, i1, i2]);
                        continue;
                    }
                    // VIS-010: subtract each solid's footprint from the triangle instead of
                    // dropping or keeping it whole by its centroid, so the visible edge of
                    // the snow ends on the wall line rather than in a saw of triangles.
                    List<Vector3[]> pieces = [[Vector3.Right, Vector3.Up, Vector3.Back]];
                    var split = false;
                    foreach (var box in near)
                    {
                        if (!InWindow(box, centre)) continue;
                        var l0 = box.Inverse * w0; var l1 = box.Inverse * w1; var l2 = box.Inverse * w2;
                        var next = new List<Vector3[]>();
                        foreach (var piece in pieces)
                            split |= SnowSurfaceSplitter.SubtractFootprint(piece, l0, l1, l2, box.Half, next);
                        pieces = next;
                        if (pieces.Count == 0) break;
                    }
                    if (!split) { kept.AddRange([i0, i1, i2]); continue; }
                    dropped++;
                    changed = true;
                    foreach (var piece in pieces)
                        for (var k = 1; k + 1 < piece.Length; k++)
                        {
                            kept.Add(splitter.Add(i0, i1, i2, piece[0]));
                            kept.Add(splitter.Add(i0, i1, i2, piece[k]));
                            kept.Add(splitter.Add(i0, i1, i2, piece[k + 1]));
                        }
                }
                if (kept.Count == 0) continue;
                splitter?.WriteBack(arrays);
                arrays[(int)Mesh.ArrayType.Index] = kept.ToArray();
                result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, source.SurfaceGetMaterial(s));
            }
            if (!changed) continue;
            mesh.Mesh = result;
            mesh.SetMeta("snowClippedUnderStructures", true);
            meshes++;
        }
        GD.Print($"act1-snow-relief: clipped meshes={meshes} triangles={dropped} solids={boxes.Count} addedVertices={SnowSurfaceSplitter.AddedVertices}");
        // VIS-077: the static half of the acceptance — the relief that stands in the world
        // right now, counted in authored elevation bands, without a texture in sight.
        SummarizeSnowElevation();
    }

    /// <summary>
    /// VIS-077: samples the rise above the collision ground of every snow-relief mesh and
    /// reports how many 12 cm elevation levels actually exist. The card asks for at least
    /// two levels besides the shader's micro displacement; a flat white world scores one.
    /// </summary>
    private void SummarizeSnowElevation()
    {
        var rises = new List<float>();
        var meshes = 0;
        const int stride = 8;
        const int sampleBudget = 40_000;
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (mesh.Mesh is not ArrayMesh source || !IsSnowRelief(mesh)) continue;
            meshes++;
            for (var s = 0; s < source.GetSurfaceCount(); s++)
            {
                using var arrays = source.SurfaceGetArrays(s);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                // Every eighth vertex, under a hard sample budget: enough to bucket a rise,
                // bounded so a first-frame diagnostic can never turn into a stall.
                for (var i = 0; i < vertices.Length && rises.Count < sampleBudget; i += stride)
                {
                    var world = mesh.ToGlobal(vertices[i]);
                    rises.Add(world.Y - AgentBAct1HeightField.CollisionGround(world.X, world.Z));
                }
                if (rises.Count >= sampleBudget) break;
            }
            if (rises.Count >= sampleBudget) break;
        }
        var levels = SnowReliefStandard.ElevationLevelCount(rises);
        var minimum = rises.Count == 0 ? 0f : rises.Min();
        var maximum = rises.Count == 0 ? 0f : rises.Max();
        SetMeta("snowReliefMeshes", meshes);
        SetMeta("snowReliefElevationLevels", levels);
        SetMeta("snowReliefElevationBand", SnowReliefStandard.ElevationBand);
        SetMeta("snowReliefRiseRange", $"{minimum:0.###}-{maximum:0.###}");
        GD.Print($"act1-snow-elevation: meshes={meshes} levels={levels} band={SnowReliefStandard.ElevationBand:0.###} " +
                 $"riseMin={minimum:0.###} riseMax={maximum:0.###}");
    }

    /// <summary>
    /// Per-vertex normals of a row-major grid relief: both face normals of every quad are
    /// handed to their corners and normalised at the end. A feathered aperture has degenerate
    /// faces, so a zero-length accumulation falls back to the up direction instead of a NaN
    /// normal, and every normal is turned upward regardless of the authored winding.
    /// </summary>
    private static Vector3[] GridNormals(Vector3[] vertices, int stations, int columns)
    {
        var accumulated = new Vector3[vertices.Length];
        for (var s = 0; s + 1 < stations; s++)
        for (var c = 0; c + 1 < columns; c++)
        {
            var topLeft = s * columns + c;
            var topRight = topLeft + 1;
            var bottomLeft = (s + 1) * columns + c;
            var bottomRight = bottomLeft + 1;
            var first = (vertices[topRight] - vertices[topLeft]).Cross(vertices[bottomLeft] - vertices[topLeft]);
            var second = (vertices[bottomRight] - vertices[bottomLeft]).Cross(vertices[topRight] - vertices[bottomLeft]);
            accumulated[topLeft] += first;
            accumulated[topRight] += first + second;
            accumulated[bottomLeft] += first + second;
            accumulated[bottomRight] += second;
        }
        var result = new Vector3[vertices.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var n = accumulated[i];
            if (n.Y < 0f) n = -n;
            result[i] = n.LengthSquared() > 1e-8f ? n.Normalized() : Vector3.Up;
        }
        return result;
    }

    private static bool IsSnowRelief(MeshInstance3D mesh)
    {
        if (!mesh.IsVisibleInTree()) return false;
        var name = mesh.Name.ToString();
        if (name is "Terrain_Main" || name.StartsWith("Backdrop", StringComparison.Ordinal)) return false;
        var material = mesh.MaterialOverride ?? mesh.GetActiveMaterial(0);
        var surface = material?.GetMeta("surface", "").AsString() ?? "";
        if (surface is not ("snow_ground" or "snow_trampled")) return false;
        return mesh.HasMeta("snowBankHeight") || mesh.HasMeta("terrainRole")
            || mesh.GetParent()?.Name.ToString() == "URMAN_AgentB_TerrainRoadKit";
    }

    // VIS-079. The authored cross-section columns of a snow bank, copied from the landform
    // builder so the rework reads the same nine samples it was built with: the negative side
    // is the cut face toward the carriageway, the positive side faces the field.
    private static readonly float[] BankProfileColumns = [-.5f, -.38f, -.26f, -.12f, 0f, .12f, .26f, .38f, .5f];
    /// <summary>How close a required corridor must come to a bank's centre before the bank
    /// gives way. Half the authored bank footprint, so the relief body itself is what clears.</summary>
    private const float BankApertureClearance = .55f;
    /// <summary>Length over which a bank feathers down to the snowfield before an opening.</summary>
    private const float BankApertureTaper = 1.6f;
    /// <summary>Height of the snow thrown out of an opening, piled just beside the gap.</summary>
    private const float BankThrownHeight = .10f;
    /// <summary>Distance from the aperture edge at which the thrown heap sits.</summary>
    private const float BankThrownReach = 1.0f;

    /// <summary>
    /// VIS-079: rebuilds every authored street bank from the published exclusion set instead
    /// of from coordinates. A bank now (a) feathers to zero across <see cref="BankApertureTaper"/>
    /// metres wherever a required access corridor crosses it, so a gate, a lane, a footbridge
    /// or a door walk is never buried, (b) carries the snow thrown out of that opening as a
    /// readable heap just beside the gap, and (c) loses the last of the procedural-wall look:
    /// the field side slumps, the road side keeps a steeper cut face with a lip, and the crest
    /// swells from a position hash rather than a sine. Geometry only — no material, no collider.
    /// </summary>
    private void ReworkStreetSnowBanks()
    {
        var reworked = 0; var apertures = 0; var residualCrossings = 0; var unparsed = 0;
        var bankVerticesBefore = 0; var bankVerticesAfter = 0;
        SnowRefineSkips.Clear();
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (!mesh.Name.ToString().StartsWith("StreetBank", StringComparison.Ordinal)) continue;
            if (mesh.Mesh is not ArrayMesh source || source.GetSurfaceCount() == 0 || !IsSnowRelief(mesh)) continue;
            // One shaping per bank. The pass is re-entered when a route is verified after
            // the first frame; a bank that has already been read is left as it stands, so
            // the thrown heaps and the slump never compound into a wall of noise.
            if (mesh.HasMeta("streetBankReworked")) continue;
            using var arrays = source.SurfaceGetArrays(0);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var columns = BankProfileColumns.Length;
            // The authored bank is a row-major grid of nine cross-section samples per station
            // and it keeps its index buffer. A de-indexed or differently sectioned mesh is
            // counted and left alone rather than guessed at, because a wrong grouping here
            // would twist the shoulder instead of shaping it.
            if (arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil) { unparsed++; continue; }
            if (vertices.Length == 0 || vertices.Length % columns != 0) { unparsed++; continue; }
            var stations = vertices.Length / columns;
            if (stations < 3) continue;

            var centres = new Vector2[stations];
            var arc = new float[stations];
            for (var s = 0; s < stations; s++)
            {
                var world = mesh.ToGlobal(vertices[s * columns + columns / 2]);
                centres[s] = new Vector2(world.X, world.Z);
                if (s > 0) arc[s] = arc[s - 1] + centres[s].DistanceTo(centres[s - 1]);
            }
            var aperture = new bool[stations];
            for (var s = 0; s < stations; s++)
                aperture[s] = SnowReliefStandard.CorridorClearance(centres[s]) < BankApertureClearance;

            // Arc distance to the nearest aperture station, from both directions.
            var reach = BankApertureTaper + arc[^1] + 1f;
            var forward = new float[stations];
            var backward = new float[stations];
            for (var s = 0; s < stations; s++)
                forward[s] = aperture[s] ? 0f : (s == 0 ? reach : forward[s - 1] + arc[s] - arc[s - 1]);
            for (var s = stations - 1; s >= 0; s--)
                backward[s] = aperture[s] ? 0f : (s == stations - 1 ? reach : backward[s + 1] + arc[s + 1] - arc[s]);

            var weight = new float[stations];
            var thrown = new float[stations];
            for (var s = 0; s < stations; s++)
            {
                var nearest = Mathf.Min(forward[s], backward[s]);
                weight[s] = Mathf.SmoothStep(0f, BankApertureTaper, nearest);
                // One heap per aperture edge: a Gaussian ridge centred BankThrownReach metres
                // from the gap, so the opening reads as worked rather than as a boolean hole.
                thrown[s] = nearest < 3f
                    ? BankThrownHeight * Mathf.Exp(-Mathf.Pow((nearest - BankThrownReach) / .7f, 2f)) * weight[s]
                    : 0f;
            }
            var meshApertures = 0;
            for (var s = 1; s < stations; s++) if (aperture[s] && !aperture[s - 1]) meshApertures++;
            if (aperture[0]) meshApertures++;
            apertures += meshApertures;

            var maxRise = 0f;
            for (var s = 0; s < stations; s++)
            {
                var taper = weight[s];
                // Non-repeating crest swell from the position hash: metre-long rhythm,
                // never the same shape twice along a 50 m shoulder.
                var clump = 1f + .16f * (float)(AgentBAct1HeightField.ValueNoise(
                    centres[s].X * .53 + 3.1, centres[s].Y * .47 - 1.7) - .5) * 2f;
                for (var c = 0; c < columns; c++)
                {
                    var index = s * columns + c;
                    var world = mesh.ToGlobal(vertices[index]);
                    var ground = AgentBAct1HeightField.CollisionGround(world.X, world.Z);
                    var rise = world.Y - ground;
                    var profile = BankProfileColumns[c];
                    var weather = 1f - .26f * Mathf.SmoothStep(.10f, .50f, profile);
                    var lip = .05f * Mathf.Exp(-Mathf.Pow((profile + .30f) / .11f, 2f));
                    var crest = Mathf.Exp(-Mathf.Pow(profile / .22f, 2f));
                    var next = Mathf.Max(rise * taper * weather * clump + (lip + thrown[s] * crest) * taper, -.02f);
                    maxRise = Mathf.Max(maxRise, next);
                    vertices[index] = mesh.ToLocal(new Vector3(world.X, ground + next, world.Z));
                    if (aperture[s] && next > .06f) residualCrossings++;
                }
            }

            // Soft wind-packed look: round the shoulders (smooth the rise grid) and double the
            // columns across, so the 23 cm quads of the authored section no longer read as a
            // folded sheet. Aperture / taper stations may only lose snow, never gain it.
            var noRaise = new bool[stations];
            for (var s = 0; s < stations; s++) noRaise[s] = aperture[s] || weight[s] < .999f;
            global::Godot.Collections.Array? softArrays = null;
            Vector3[] softVertices = []; Vector2[]? softUvs = null; var softColumns = columns; var softRise = 0f;
            var blocker = SnowGridBlocker(arrays, vertices.Length, columns);
            if (blocker is null && !RefineSnowGrid(mesh, vertices,
                    SnowArrayLength(arrays[(int)Mesh.ArrayType.TexUV]) == 0 ? null : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array(),
                    stations, columns, noRaise, out softVertices, out softUvs, out softColumns, out softRise))
                blocker = "refine";
            if (blocker is not null) NoteSnowRefineSkip(blocker);
            if (blocker is null)
            {
                softArrays = BuildSnowGridArrays(softVertices, softUvs, stations, softColumns);
                bankVerticesBefore += vertices.Length;
                bankVerticesAfter += softVertices.Length;
                maxRise = softRise;
            }
            else
            {
                bankVerticesBefore += vertices.Length;
                bankVerticesAfter += vertices.Length;
                arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                // Normals are re-derived on the known grid (face normals accumulated per vertex)
                // instead of asking SurfaceTool for them: the authored relief arrives indexed and
                // with a normal array already set, where generate_normals() refuses to run. This
                // way the new slump and the thrown heaps are actually shaded, the index buffer
                // stays as it was and no vertex is duplicated.
                arrays[(int)Mesh.ArrayType.Normal] = GridNormals(vertices, stations, columns);
            }
            // The material is taken before the swap: the old ArrayMesh goes out of use here,
            // and the new one must be owned by the node, not by a disposed local.
            var reliefMaterial = source.SurfaceGetMaterial(0);
            var reprofiled = new ArrayMesh();
            reprofiled.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, softArrays ?? arrays);
            // A surface belongs to the mesh resource, not to the instance: the material is put
            // on the reprofiled ArrayMesh before that mesh is published to the node, exactly
            // as the clipped relief above does.
            if (reliefMaterial is not null) reprofiled.SurfaceSetMaterial(0, reliefMaterial);
            mesh.Mesh = reprofiled;
            mesh.SetMeta("streetBankReworked", true);
            mesh.SetMeta("snowTier", SnowReliefStandard.TierMass);
            mesh.SetMeta("snowBankApertures", meshApertures);
            mesh.SetMeta("snowBankReworkedHeight", maxRise);
            mesh.SetMeta("snowBankExclusionOwner", nameof(SnowReliefStandard));
            if (aperture[0] || aperture[^1]) mesh.SetMeta("snowBankEndsInCorridor", true);
            reworked++;
        }
        SetMeta("streetBanksReworked", reworked);
        SetMeta("streetBankApertures", apertures);
        SetMeta("streetBankCorridorCrossings", residualCrossings);
        GD.Print($"act1-street-banks: reworked={reworked} apertures={apertures} " +
                 $"corridorCrossings={residualCrossings} unparsed={unparsed} " +
                 $"corridors={SnowReliefStandard.CorridorCount} solids={SnowReliefStandard.SolidCount} " +
                 $"footprints={SnowReliefStandard.FootprintCount} " +
                 $"vertices={bankVerticesBefore}->{bankVerticesAfter} refineSkipped={SnowRefineSkipSummary()}");
    }

    /// <summary>Why a relief grid was not refined, counted per reason and printed by the
    /// street-bank and snow-mass lines (refineSkipped=reason:count,...).</summary>
    private static readonly Dictionary<string, int> SnowRefineSkips = new();

    private static string SnowRefineSkipSummary() => SnowRefineSkips.Count == 0
        ? "none" : string.Join(",", SnowRefineSkips.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));

    private static void NoteSnowRefineSkip(string reason) =>
        SnowRefineSkips[reason] = SnowRefineSkips.TryGetValue(reason, out var n) ? n + 1 : 1;

    /// <summary>Number of elements in a packed array variant; 0 for nil or an empty array.</summary>
    private static int SnowArrayLength(Variant v) => v.VariantType switch
    {
        Variant.Type.PackedVector3Array => v.AsVector3Array().Length,
        Variant.Type.PackedVector2Array => v.AsVector2Array().Length,
        Variant.Type.PackedFloat32Array => v.AsFloat32Array().Length,
        Variant.Type.PackedFloat64Array => v.AsFloat64Array().Length,
        Variant.Type.PackedInt32Array => v.AsInt32Array().Length,
        Variant.Type.PackedColorArray => v.AsColorArray().Length,
        Variant.Type.PackedByteArray => v.AsByteArray().Length,
        Variant.Type.Nil => 0,
        _ => 1
    };

    /// <summary>
    /// Null when the grid can be rebuilt from vertex, normal, uv and index alone; otherwise the
    /// name of the attribute that blocks it. Authored tangents are not blocking: the snow surfaces
    /// use no normal map, so the rebuilt mesh simply omits the (stale) tangent array.
    /// </summary>
    private static string? SnowGridBlocker(global::Godot.Collections.Array arrays, int vertexCount, int columns)
    {
        foreach (var (type, name) in new[] { (Mesh.ArrayType.Color, "color"), (Mesh.ArrayType.TexUV2, "uv2"),
                     (Mesh.ArrayType.Bones, "bones"), (Mesh.ArrayType.Weights, "weights"),
                     (Mesh.ArrayType.Custom0, "custom0"), (Mesh.ArrayType.Custom1, "custom1"),
                     (Mesh.ArrayType.Custom2, "custom2"), (Mesh.ArrayType.Custom3, "custom3") })
            if (SnowArrayLength(arrays[(int)type]) > 0) return name;
        if (SnowArrayLength(arrays[(int)Mesh.ArrayType.Index]) == 0) return "noindex";
        if (vertexCount == 0 || vertexCount % columns != 0 || vertexCount / columns < 3) return "grid";
        var uv = SnowArrayLength(arrays[(int)Mesh.ArrayType.TexUV]);
        if (uv != 0 && uv != vertexCount) return "uvcount";
        return null;
    }

    /// <summary>Cross-section passes of the [1 2 1] rise filter (edge columns pinned).</summary>
    private const int SnowGridCrossPasses = 3;
    /// <summary>Along-run passes of the same filter (end stations pinned): removes the
    /// station-to-station noise that reads as facets on the crest.</summary>
    private const int SnowGridAlongPasses = 2;

    /// <summary>
    /// Softens a row-major snow relief grid (stations x columns, mesh-local vertices): the rise
    /// above the collision ground is filtered a few passes so the plateau-to-slope creases become
    /// rounded shoulders, then every column gap gets a mid column (clamped cubic through the
    /// neighbours, never above the higher neighbour), giving 2*columns-1 columns. Edge columns and
    /// end stations keep their rise, a vertex that already lay flat on the ground (inside the road
    /// clearance) stays flat, and <paramref name="noRaise"/> stations can only lose snow, so a
    /// refined bank never reaches further into a corridor than the grid it came from.
    /// </summary>
    private static bool RefineSnowGrid(MeshInstance3D mesh, Vector3[] vertices, Vector2[]? uvs, int stations, int columns,
        bool[]? noRaise, out Vector3[] refined, out Vector2[]? refinedUvs, out int refinedColumns, out float maxRise)
    {
        refined = vertices; refinedUvs = uvs; refinedColumns = columns; maxRise = 0f;
        if (stations < 3 || columns < 3 || vertices.Length != stations * columns) return false;
        if (uvs is not null && uvs.Length != vertices.Length) return false;
        var count = vertices.Length;
        var world = new Vector3[count];
        var ground = new float[count];
        var original = new float[count];
        for (var i = 0; i < count; i++)
        {
            world[i] = mesh.ToGlobal(vertices[i]);
            ground[i] = AgentBAct1HeightField.CollisionGround(world[i].X, world[i].Z);
            original[i] = world[i].Y - ground[i];
        }
        var rise = (float[])original.Clone();
        var scratch = new float[count];

        bool Locked(int s, int c, int i) =>
            s == 0 || s == stations - 1 || c == 0 || c == columns - 1 || original[i] <= .005f;
        float Limit(float value, int s, int i) => noRaise is not null && noRaise[s] ? Mathf.Min(value, original[i]) : value;

        for (var pass = 0; pass < SnowGridCrossPasses; pass++)
        {
            for (var s = 0; s < stations; s++)
            for (var c = 0; c < columns; c++)
            {
                var i = s * columns + c;
                scratch[i] = Locked(s, c, i) ? rise[i] : Limit(.5f * rise[i] + .25f * (rise[i - 1] + rise[i + 1]), s, i);
            }
            (rise, scratch) = (scratch, rise);
        }
        for (var pass = 0; pass < SnowGridAlongPasses; pass++)
        {
            for (var s = 0; s < stations; s++)
            for (var c = 0; c < columns; c++)
            {
                var i = s * columns + c;
                scratch[i] = Locked(s, c, i) ? rise[i]
                    : Limit(.5f * rise[i] + .25f * (rise[i - columns] + rise[i + columns]), s, i);
            }
            (rise, scratch) = (scratch, rise);
        }

        var newColumns = columns * 2 - 1;
        var result = new Vector3[stations * newColumns];
        var newUvs = uvs is null ? null : new Vector2[result.Length];
        var inverse = mesh.GlobalTransform.AffineInverse();
        for (var s = 0; s < stations; s++)
        for (var c = 0; c < newColumns; c++)
        {
            var o = s * newColumns + c;
            Vector3 placed;
            float placedRise;
            if ((c & 1) == 0)
            {
                var i = s * columns + c / 2;
                placedRise = rise[i];
                placed = new Vector3(world[i].X, ground[i] + rise[i], world[i].Z);
                if (newUvs is not null) newUvs[o] = uvs![i];
            }
            else
            {
                var a = s * columns + c / 2;
                var b = a + 1;
                var x = (world[a].X + world[b].X) * .5f;
                var z = (world[a].Z + world[b].Z) * .5f;
                var p0 = c / 2 - 1 >= 0 ? rise[a - 1] : rise[a];
                var p3 = c / 2 + 2 < columns ? rise[b + 1] : rise[b];
                var r = (-p0 + 9f * rise[a] + 9f * rise[b] - p3) / 16f;
                r = Mathf.Clamp(r, Mathf.Min(rise[a], rise[b]), Mathf.Max(rise[a], rise[b]));
                placedRise = r;
                placed = new Vector3(x, AgentBAct1HeightField.CollisionGround(x, z) + r, z);
                if (newUvs is not null) newUvs[o] = (uvs![a] + uvs[b]) * .5f;
            }
            maxRise = Mathf.Max(maxRise, placedRise);
            result[o] = inverse * placed;
        }
        refined = result; refinedUvs = newUvs; refinedColumns = newColumns;
        return true;
    }

    /// <summary>Indexed triangle arrays for a row-major relief grid, wound like the authored
    /// landform surface, with grid normals.</summary>
    private static global::Godot.Collections.Array BuildSnowGridArrays(Vector3[] vertices, Vector2[]? uvs, int stations, int columns)
    {
        var indices = new int[(columns - 1) * (stations - 1) * 6];
        var k = 0;
        for (var s = 0; s + 1 < stations; s++)
        for (var c = 0; c + 1 < columns; c++)
        {
            var topLeft = s * columns + c;
            var topRight = topLeft + 1;
            var bottomLeft = (s + 1) * columns + c;
            var bottomRight = bottomLeft + 1;
            indices[k++] = topLeft; indices[k++] = topRight; indices[k++] = bottomLeft;
            indices[k++] = topRight; indices[k++] = bottomRight; indices[k++] = bottomLeft;
        }
        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = GridNormals(vertices, stations, columns);
        if (uvs is not null) arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        return arrays;
    }
}

/// <summary>
/// VIS-010: splits a snow-relief triangle against a solid's XZ footprint and appends
/// the new vertices with every attribute interpolated (position, normal, tangent,
/// colour, UV, UV2). Pieces are tracked in barycentric coordinates of the source
/// triangle, so the remainder is exact and keeps the source winding.
/// </summary>
internal sealed class SnowSurfaceSplitter
{
    private const float Epsilon = .001f;
    internal static int AddedVertices;
    private readonly List<Vector3> _vertices;
    private readonly List<Vector3>? _normals;
    private readonly List<float>? _tangents;
    private readonly List<Color>? _colors;
    private readonly List<Vector2>? _uv;
    private readonly List<Vector2>? _uv2;

    private SnowSurfaceSplitter(global::Godot.Collections.Array arrays, Vector3[] vertices)
    {
        _vertices = [.. vertices];
        Variant At(Mesh.ArrayType type) => arrays[(int)type];
        if (At(Mesh.ArrayType.Normal).VariantType != Variant.Type.Nil) _normals = [.. At(Mesh.ArrayType.Normal).AsVector3Array()];
        if (At(Mesh.ArrayType.Tangent).VariantType != Variant.Type.Nil) _tangents = [.. At(Mesh.ArrayType.Tangent).AsFloat32Array()];
        if (At(Mesh.ArrayType.Color).VariantType != Variant.Type.Nil) _colors = [.. At(Mesh.ArrayType.Color).AsColorArray()];
        if (At(Mesh.ArrayType.TexUV).VariantType != Variant.Type.Nil) _uv = [.. At(Mesh.ArrayType.TexUV).AsVector2Array()];
        if (At(Mesh.ArrayType.TexUV2).VariantType != Variant.Type.Nil) _uv2 = [.. At(Mesh.ArrayType.TexUV2).AsVector2Array()];
    }

    /// <summary>Null when the surface carries attributes this splitter cannot interpolate (bones, weights, custom).</summary>
    internal static SnowSurfaceSplitter? TryCreate(global::Godot.Collections.Array arrays, Vector3[] vertices)
    {
        foreach (var type in new[] { Mesh.ArrayType.Bones, Mesh.ArrayType.Weights, Mesh.ArrayType.Custom0,
                     Mesh.ArrayType.Custom1, Mesh.ArrayType.Custom2, Mesh.ArrayType.Custom3 })
            if (arrays[(int)type].VariantType != Variant.Type.Nil) return null;
        return new SnowSurfaceSplitter(arrays, vertices);
    }

    internal int Add(int i0, int i1, int i2, Vector3 b)
    {
        var index = _vertices.Count;
        _vertices.Add(_vertices[i0] * b.X + _vertices[i1] * b.Y + _vertices[i2] * b.Z);
        _normals?.Add((_normals[i0] * b.X + _normals[i1] * b.Y + _normals[i2] * b.Z).Normalized());
        if (_tangents is not null)
        {
            var t = new Vector3(_tangents[i0 * 4], _tangents[i0 * 4 + 1], _tangents[i0 * 4 + 2]) * b.X
                + new Vector3(_tangents[i1 * 4], _tangents[i1 * 4 + 1], _tangents[i1 * 4 + 2]) * b.Y
                + new Vector3(_tangents[i2 * 4], _tangents[i2 * 4 + 1], _tangents[i2 * 4 + 2]) * b.Z;
            t = t.Normalized();
            _tangents.AddRange([t.X, t.Y, t.Z, _tangents[i0 * 4 + 3]]);
        }
        _colors?.Add(_colors[i0] * b.X + _colors[i1] * b.Y + _colors[i2] * b.Z);
        _uv?.Add(_uv[i0] * b.X + _uv[i1] * b.Y + _uv[i2] * b.Z);
        _uv2?.Add(_uv2[i0] * b.X + _uv2[i1] * b.Y + _uv2[i2] * b.Z);
        AddedVertices++;
        return index;
    }

    internal void WriteBack(global::Godot.Collections.Array arrays)
    {
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
        if (_normals is not null) arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
        if (_tangents is not null) arrays[(int)Mesh.ArrayType.Tangent] = _tangents.ToArray();
        if (_colors is not null) arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
        if (_uv is not null) arrays[(int)Mesh.ArrayType.TexUV] = _uv.ToArray();
        if (_uv2 is not null) arrays[(int)Mesh.ArrayType.TexUV2] = _uv2.ToArray();
    }

    /// <summary>
    /// Appends the parts of <paramref name="piece"/> that lie outside the footprint
    /// |x| &lt; half.X, |z| &lt; half.Z (box-local) to <paramref name="output"/>.
    /// Returns false when the piece was left whole (entirely outside).
    /// </summary>
    internal static bool SubtractFootprint(Vector3[] piece, Vector3 l0, Vector3 l1, Vector3 l2, Vector3 half, List<Vector3[]> output)
    {
        Vector3 Local(Vector3 b) => l0 * b.X + l1 * b.Y + l2 * b.Z;
        // Inside half-spaces of the footprint, as f(p) >= 0.
        Func<Vector3, float>[] planes =
        [
            p => half.X - p.X, p => half.X + p.X, p => half.Z - p.Z, p => half.Z + p.Z
        ];
        foreach (var plane in planes)
            if (piece.All(b => plane(Local(b)) <= Epsilon))
            {
                output.Add(piece); // wholly outside one side: untouched
                return false;
            }
        var remaining = piece;
        foreach (var plane in planes)
        {
            var outside = Clip(remaining, b => -plane(Local(b)));
            if (outside.Length >= 3 && Area(outside, Local) > 1e-7f) output.Add(outside);
            remaining = Clip(remaining, b => plane(Local(b)));
            if (remaining.Length < 3) break;
        }
        return true;
    }

    private static Vector3[] Clip(Vector3[] polygon, Func<Vector3, float> keep)
    {
        var result = new List<Vector3>(polygon.Length + 2);
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
            var fa = keep(a); var fb = keep(b);
            if (fa >= 0f) result.Add(a);
            if ((fa >= 0f) != (fb >= 0f))
                result.Add(a.Lerp(b, fa / (fa - fb)));
        }
        return result.ToArray();
    }

    private static float Area(Vector3[] polygon, Func<Vector3, Vector3> local)
    {
        var area = 0f;
        var origin = local(polygon[0]);
        for (var i = 1; i + 1 < polygon.Length; i++)
            area += (local(polygon[i]) - origin).Cross(local(polygon[i + 1]) - origin).Length() * .5f;
        return area;
    }
}
