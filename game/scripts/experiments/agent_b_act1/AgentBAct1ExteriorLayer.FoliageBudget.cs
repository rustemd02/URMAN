using Godot;

namespace Urman.Godot;

/// <summary>
/// VIS-027 (silhouette across the LOD switch), VIS-029 (one heavy view's budget)
/// and VIS-030 (culling bounds of one plant cell) for the Act I exterior foliage.
///
/// Ownership: this file measures, publishes, and — behind one reversible switch —
/// trims. It never writes lighting, never registers an atmosphere or material
/// owner, and never touches a graphics preset. Iteration 02 of the execution
/// journal established that the exterior layer no longer writes environment/sky/
/// sun; that stays true here.
///
/// Why the numbers are measured instead of asserted: the cards require "Дистанции
/// сначала измерить" and "bounds подтверждены, не только нарисованы вручную". The
/// tiers are pre-built authored meshes whose real extents are only known after the
/// rebase to the rooted pivot and the region material pass, so every gate below
/// reads the runtime ArrayMesh and the runtime material parameters.
/// </summary>
public partial class AgentBAct1ExteriorLayer
{
    /// <summary>
    /// VIS-027 gate: the projected crown may not change by more than this percent of
    /// its own width across the switch. The card itself warns that a percentage test
    /// does not replace watching the video, so this reports rather than throws — a
    /// throw would take the whole village down because one authored tier is 3 %
    /// narrower than its neighbour.
    /// </summary>
    internal const float LodSilhouetteTolerancePercent = 2f;

    /// <summary>Ground-cover batch cell edge, taken from the actual batch key.</summary>
    internal const float GroundCoverCellSize = 12f;

    /// <summary>
    /// VIS-030 anti-giant-AABB gate: a cell's culling volume may not exceed this
    /// multiple of the cell's own footprint volume. Above it the batch draws the
    /// neighbourhood instead of the cell.
    /// </summary>
    internal const float CellVolumeFootprintFactor = 2.2f;

    /// <summary>
    /// VIS-027/029 budget switch. <c>legacy</c> restores the previous behaviour
    /// exactly (uncapped full-LOD distance, far tier still a shadow caster, outer
    /// ring rows still carry understory), so one build can shoot both sides of the
    /// A/B. The default is <c>budget</c> because every reduction acts outside the
    /// region the author reads as silhouette: behind the last standable row or
    /// inside fog the frame cannot resolve.
    /// </summary>
    internal static string FoliageBudgetMode =>
        OS.GetEnvironment("URMAN_FOLIAGE_LOD_BUDGET") is "legacy" or "off" ? "legacy" : "budget";

    private static bool? _foliageBudgetApplied;

    /// <summary>True when the bounded budget is active for this process.</summary>
    internal static bool FoliageBudgetApplied => _foliageBudgetApplied ??= FoliageBudgetMode == "budget";

    /// <summary>
    /// VIS-027/029: the full-LOD tier is what costs real geometry, and the old
    /// projected-size rule let it reach 78 m for a tall crown (rangeScale 3 × 26 m),
    /// past the authored fog line of every profile. The cap keeps the cross-fade
    /// band proportionally wide — so the switch stays a fade and not a pop — and only
    /// moves it nearer.
    /// </summary>
    internal static float LodRangeScaleCeiling => FoliageBudgetApplied ? 1.6f : 3f;

    /// <summary>VIS-030 switch: measured sway reserve instead of an unmeasured one.</summary>
    internal static string FoliageBoundsMode =>
        OS.GetEnvironment("URMAN_FOLIAGE_BOUNDS") is "legacy" or "off" ? "legacy" : "measured";

    private static bool? _foliageBoundsMeasured;

    internal static bool FoliageBoundsMeasured => _foliageBoundsMeasured ??= FoliageBoundsMode == "measured";

    /// <summary>
    /// VIS-085 placement switch. <c>grid</c> reproduces the previous metronome
    /// exactly (fixed row step with only its ±1.2 m jitter), <c>broken</c> — the
    /// default — moves each ring stem along its own row. The audit reports both
    /// numbers from the same run, so the A/B does not need two builds to be judged.
    /// </summary>
    internal static string ForestRhythmMode =>
        OS.GetEnvironment("URMAN_FOREST_RHYTHM") is "grid" or "legacy" ? "grid" : "broken";

    private static bool? _forestRhythmBroken;

    internal static bool ForestRhythmBroken => _forestRhythmBroken ??= ForestRhythmMode == "broken";

    /// <summary>
    /// VIS-085: a ring row emitted on a fixed step reads as a counted colonnade —
    /// the "стена повторов" the card names — even with jitter, because the interval
    /// keeps one dominant period. This displaces a stem <em>along its own row</em>
    /// only, so the row's normal position, the closure of the skyline and every
    /// keep-out already evaluated on the point are unchanged. Pure function of the
    /// point and the row: no RNG value is consumed or skipped, which the plan
    /// requires so later roots and fixtures keep their authored places.
    /// </summary>
    /// <param name="point">The already jittered grid position of the stem.</param>
    /// <param name="row">Ring row index, outermost first.</param>
    /// <param name="axis">0 when the row runs along X, 1 when it runs along Z.</param>
    internal static Vector2 BreakRowPeriod(Vector2 point, int row, int axis)
    {
        if (!ForestRhythmBroken) return point;
        // Two independent phases: one sets the direction and size of the move, the
        // other occasionally opens a wider bay so the density itself is uneven — the
        // wall is broken by irregular spacing, not by removing trees.
        var swing = DeterministicPhase(point, 91.7f + row * 3.1f) - .5f;
        var bay = DeterministicPhase(point, 97.3f + row * 1.7f);
        var offset = swing * (bay < .22f ? 3.6f : 2.1f);
        return axis == 0 ? point + new Vector2(offset, 0f) : point + new Vector2(0f, offset);
    }

    /// <summary>
    /// VIS-085 rhythm and layering audit: how regular the trunk interval really is
    /// before and after the break, how close the nearest trunks stand inside the
    /// first 20 m from the settlement envelope, which species repeat in runs, and how
    /// many distinct ring rows lie inside the fog-limited horizon. All of it is
    /// computed from the placements this build actually emitted.
    /// </summary>
    private void AuditForestRingRhythm()
    {
        var trunks = _forestRingTrees;
        (float Regular, float Modal, float Minimum, float Maximum, int Count) Rhythm(bool before)
        {
            var gaps = new List<float>();
            foreach (var group in trunks.GroupBy(tree => (tree.Row, tree.Edge)))
            {
                var stems = group.ToArray();
                if (stems.Length < 3) continue;
                // 0/1/4 are the rows that run along X, 2/3 along Z. Gaps are measured
                // along the row, because that is the period a walking player counts.
                var alongX = group.Key.Edge is not (2 or 3);
                var along = stems
                    .Select(tree => alongX
                        ? (before ? tree.Jittered : tree.Planted).X
                        : (before ? tree.Jittered : tree.Planted).Y)
                    .Order()
                    .ToArray();
                for (var index = 1; index < along.Length; index++)
                    if (along[index] - along[index - 1] > .01f) gaps.Add(along[index] - along[index - 1]);
            }

            if (gaps.Count == 0) return (0f, 0f, 0f, 0f, 0);
            const float bin = .4f;
            var modalBucket = gaps.GroupBy(gap => (int)(gap / bin))
                .OrderBy(bucket => bucket.Count())
                .Last().Key;
            var modal = modalBucket * bin + bin * .5f;
            var within = gaps.Count(gap => Mathf.Abs(gap - modal) <= modal * .15f);
            return (100f * within / gaps.Count, modal, gaps.Min(), gaps.Max(), gaps.Count);
        }

        var previous = Rhythm(true);
        var current = Rhythm(false);

        // The band the player can stand next to: 0-20 m inward from the envelope —
        // the card's "no repeating trunk interval in first 20m".
        var near = trunks.Where(tree =>
        {
            var depth = RingInwardDepth(tree.Planted);
            return depth >= 0f && depth <= 20f;
        }).ToList();
        var nearNeighbour = new List<float>();
        var buckets = new Dictionary<(int, int), List<Vector2>>();
        const float bucketSize = 10f;
        // The cell keys are named: the neighbour lookup below adds a margin to
        // each axis, and an unnamed (int, int) has no X/Y to address.
        (int X, int Y) BucketOf(Vector2 point) =>
            ((int)Mathf.Floor(point.X / bucketSize), (int)Mathf.Floor(point.Y / bucketSize));
        foreach (var tree in near)
        {
            var key = BucketOf(tree.Planted);
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Vector2>();
            list.Add(tree.Planted);
        }

        foreach (var tree in near)
        {
            var best = float.MaxValue;
            var centre = BucketOf(tree.Planted);
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
            {
                if (!buckets.TryGetValue((centre.X + dx, centre.Y + dz), out var list)) continue;
                foreach (var other in list)
                {
                    if (other == tree.Planted) continue;
                    best = Mathf.Min(best, other.DistanceTo(tree.Planted));
                }
            }

            if (best < float.MaxValue) nearNeighbour.Add(best);
        }

        // Species runs along a row: a wall is still a wall when the same trunk repeats
        // on a perfect period, so the run length is reported beside the spacing.
        var longestRun = 0;
        var runTrees = 0;
        foreach (var group in trunks.GroupBy(tree => (tree.Row, tree.Edge)))
        {
            var alongX = group.Key.Edge is not (2 or 3);
            var ordered = group
                .Select(tree => (Coordinate: alongX ? tree.Planted.X : tree.Planted.Y, tree.Variant))
                .OrderBy(pair => pair.Coordinate)
                .ToArray();
            var run = 1;
            for (var index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].Variant != ordered[index - 1].Variant)
                {
                    if (run >= 3) runTrees += run;
                    longestRun = Mathf.Max(longestRun, run);
                    run = 1;
                    continue;
                }

                run++;
            }

            if (run >= 3) runTrees += run;
            longestRun = Mathf.Max(longestRun, run);
        }

        // VIS-113 target: the first 15-35 m has to read as layered depth, not as one
        // plane. Counted as distinct ring rows inside fixed horizon distances, so the
        // measure never depends on atmosphere values this layer does not own.
        int RowsWithin(float limit) => trunks
            .Where(tree =>
            {
                var depth = RingInwardDepth(tree.Planted);
                return depth >= 0f && depth <= limit;
            })
            .Select(tree => tree.Row)
            .Distinct()
            .Count();
        var rowsNear = RowsWithin(15f);
        var rowsMid = RowsWithin(25f);
        var rowsFar = RowsWithin(40f);
        var nearestMinimum = nearNeighbour.Count == 0 ? 0f : nearNeighbour.Min();
        var nearestMedian = nearNeighbour.Count == 0 ? 0f : Percentile(nearNeighbour, .5f);

        SetMeta("forestRingTrunkCount", trunks.Count);
        SetMeta("forestRhythmMode", ForestRhythmMode);
        SetMeta("forestRhythmIntervalShareBeforePercent", previous.Regular);
        SetMeta("forestRhythmIntervalShareAfterPercent", current.Regular);
        SetMeta("forestRhythmModalIntervalBeforeMeters", previous.Modal);
        SetMeta("forestRhythmModalIntervalAfterMeters", current.Modal);
        SetMeta("forestRhythmGapSpreadAfterMeters", new[] { current.Minimum, current.Maximum });
        SetMeta("forestNearBandTrunkCount", near.Count);
        SetMeta("forestNearBandNearestMedianMeters", nearestMedian);
        SetMeta("forestNearBandNearestMinMeters", nearestMinimum);
        SetMeta("forestSpeciesRunTrees", runTrees);
        SetMeta("forestSpeciesLongestRun", longestRun);
        SetMeta("forestLayerRowsWithin15m", rowsNear);
        SetMeta("forestLayerRowsWithin25m", rowsMid);
        SetMeta("forestLayerRowsWithin40m", rowsFar);
        // One interpolated string inside Invariant(...): joining several of them
        // with `+` yields a plain string, which no longer binds to the invariant
        // overload (CS1503) and would fall back to the current culture.
        GD.Print(FormattableString.Invariant(
            $"act1-forest-rhythm: mode={ForestRhythmMode} trunks={trunks.Count} intervalShare=[{previous.Regular:F1}%->{current.Regular:F1}%] modal=[{previous.Modal:F2}m->{current.Modal:F2}m] gapSpreadAfter=[{current.Minimum:F2},{current.Maximum:F2}]m nearBandTrunks={near.Count} nearestMin={nearestMinimum:F2}m nearestMedian={nearestMedian:F2}m speciesRuns={runTrees} longestRun={longestRun} rows=[{rowsNear},{rowsMid},{rowsFar}]@15/25/40m"));
    }

    /// <summary>
    /// Depth of a ring trunk inward from the settlement envelope: 0 on the edge the
    /// player can stand next to, growing outward. Outside the band rectangle the value
    /// is the distance to it, so nothing silently counts as "near".
    /// </summary>
    private static float RingInwardDepth(Vector2 point)
    {
        if (_forestRingBand is not { } band) return float.MaxValue;
        var dx = Mathf.Max(Mathf.Max(band.InnerMin.X - point.X, point.X - band.InnerMax.X), 0f);
        var dz = Mathf.Max(Mathf.Max(band.InnerMin.Y - point.Y, point.Y - band.InnerMax.Y), 0f);
        // Inside the envelope (the village itself) the ring depth is not defined;
        // those plants are authored village trees and are excluded from the band rule.
        return dx == 0f && dz == 0f ? -1f : Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float Percentile(List<float> values, float fraction)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return 0f;
        var index = Mathf.Clamp((int)Mathf.Round((ordered.Length - 1) * fraction), 0, ordered.Length - 1);
        return ordered[index];
    }


    private int _outerRowUnderstorySkipped;

    /// <summary>
    /// VIS-027 audit of every authored LOD tier family plus VIS-030 verification of
    /// every ground-cover cell. Runs once at build time, writes metas for readback
    /// and one machine-readable line to the engine log, which the station already
    /// archives as <c>game.log</c>.
    /// </summary>
    private void AuditFoliageLodAndCullingBounds(Node3D plants)
    {
        // --- VIS-027: tier families, measured from the meshes the world instantiates.
        var families = new Dictionary<string, (string Region, ArrayMesh? Near, ArrayMesh? Light, ArrayMesh? Far)>(StringComparer.Ordinal);
        foreach (var pair in _foliageMeshes)
        {
            var variant = pair.Key.Variant;
            if (!variant.StartsWith("Winter", StringComparison.Ordinal)) continue;
            var rest = variant["Winter".Length..];
            var tier = 0;
            if (rest.StartsWith("Light", StringComparison.Ordinal)) { rest = rest["Light".Length..]; tier = 1; }
            else if (rest.StartsWith("Far", StringComparison.Ordinal)) { rest = rest["Far".Length..]; tier = 2; }
            var key = $"{pair.Key.Region}:{rest}";
            families.TryGetValue(key, out var entry);
            entry = (pair.Key.Region,
                tier == 0 ? pair.Value : entry.Near,
                tier == 1 ? pair.Value : entry.Light,
                tier == 2 ? pair.Value : entry.Far);
            families[key] = entry;
        }

        var audited = 0;
        var worstCrown = 0f;
        var worstHeight = 0f;
        var worstBase = 0f;
        var overTolerance = new List<string>();
        var farAlphaSurfaces = 0;
        var farAlphaFamilies = new List<string>();
        foreach (var pair in families.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (pair.Value.Near is not { } nearMesh || pair.Value.Light is not { } lightMesh || pair.Value.Far is not { } farMesh)
                continue;
            var near = AgentBFoliageSilhouette.Measure(nearMesh);
            var light = AgentBFoliageSilhouette.Measure(lightMesh);
            var far = AgentBFoliageSilhouette.Measure(farMesh);
            if (!near.IsUsable || !light.IsUsable || !far.IsUsable) continue;
            audited++;
            // Both adjacent switches matter: 0→1 is the one a walking player crosses,
            // 1→2 is the one that happens inside the fog.
            var crown = Mathf.Max(
                AgentBFoliageSilhouette.DeltaPercent(near.CrownWidth, light.CrownWidth),
                AgentBFoliageSilhouette.DeltaPercent(light.CrownWidth, far.CrownWidth));
            var height = Mathf.Max(
                AgentBFoliageSilhouette.DeltaPercent(near.Height, light.Height),
                AgentBFoliageSilhouette.DeltaPercent(light.Height, far.Height));
            var baseDelta = Mathf.Max(
                AgentBFoliageSilhouette.DeltaPercent(near.BaseWidth, light.BaseWidth),
                AgentBFoliageSilhouette.DeltaPercent(light.BaseWidth, far.BaseWidth));
            worstCrown = Mathf.Max(worstCrown, crown);
            worstHeight = Mathf.Max(worstHeight, height);
            worstBase = Mathf.Max(worstBase, baseDelta);
            if (crown > LodSilhouetteTolerancePercent || height > LodSilhouetteTolerancePercent
                || baseDelta > LodSilhouetteTolerancePercent)
                overTolerance.Add(FormattableString.Invariant($"{pair.Key}:{crown:F1}/{height:F1}/{baseDelta:F1}"));
            if (far.AlphaClippedSurfaces > 0)
            {
                farAlphaSurfaces += far.AlphaClippedSurfaces;
                farAlphaFamilies.Add(FormattableString.Invariant($"{pair.Key}:{far.AlphaClippedSurfaces}"));
            }

            if (OS.GetEnvironment("URMAN_FOLIAGE_LOD_AUDIT") == "1")
                GD.Print(FormattableString.Invariant(
                    $"act1-foliage-lod-row: {pair.Key} crown={crown:F2}% height={height:F2}% base={baseDelta:F2}% near={near.CrownWidth:F3}/{near.Height:F3} light={light.CrownWidth:F3}/{light.Height:F3} far={far.CrownWidth:F3}/{far.Height:F3} farAlphaSurfaces={far.AlphaClippedSurfaces}"));
        }

        // --- VIS-030: one cell at a time, from the transformed source bounds of
        // every instance it carries. This is the group culling Godot applies to a
        // MultiMesh; the measurement proves the group is a 12 m cell and not a
        // forest-sized box, and sizes the wind reserve the engine cannot know.
        var cells = 0;
        var cellInstances = 0;
        var maxCellDiagonal = 0f;
        var maxCellVolume = 0f;
        var cellsOverFootprint = 0;
        var maxSwayReserve = 0f;
        var uncoveredInstances = 0;
        var silhouetteCache = new Dictionary<ulong, AgentBFoliageSilhouette.Sample>();
        AgentBFoliageSilhouette.Sample SampleOf(ArrayMesh? mesh)
        {
            if (mesh is null) return default;
            var rid = mesh.GetRid().Id;
            if (silhouetteCache.TryGetValue(rid, out var cached)) return cached;
            var measured = AgentBFoliageSilhouette.Measure(mesh);
            silhouetteCache[rid] = measured;
            return measured;
        }
        foreach (var cell in EnumerateDescendants<MultiMeshInstance3D>(plants))
        {
            var multimesh = cell.Multimesh;
            if (multimesh is null) continue;
            if (multimesh.Mesh is not ArrayMesh source) continue;
            if (multimesh.InstanceCount == 0) continue;
            cells++;
            var sourceBounds = source.GetAabb();
            var sample = SampleOf(source);
            var mergedPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mergedEnd = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var plainPosition = mergedPosition;
            var plainEnd = mergedEnd;
            var cellSway = 0f;
            for (var index = 0; index < multimesh.InstanceCount; index++)
            {
                var transform = multimesh.GetInstanceTransform(index);
                var instanceBounds = transform * sourceBounds;
                var reserve = AgentBFoliageSilhouette.SwayExtent(sample, transform.Basis.Scale);
                cellSway = Mathf.Max(cellSway, Mathf.Max(reserve.X, reserve.Z));
                var paddedPosition = instanceBounds.Position - reserve;
                var paddedEnd = instanceBounds.End + reserve;
                mergedPosition = ComponentMin(mergedPosition, paddedPosition);
                mergedEnd = ComponentMax(mergedEnd, paddedEnd);
                plainPosition = ComponentMin(plainPosition, instanceBounds.Position);
                plainEnd = ComponentMax(plainEnd, instanceBounds.End);
                cellInstances++;
            }

            var merged = new Aabb(mergedPosition, mergedEnd - mergedPosition);
            var plain = new Aabb(plainPosition, plainEnd - plainPosition);
            var cellVolume = merged.Size.X * merged.Size.Y * merged.Size.Z;
            maxCellVolume = Mathf.Max(maxCellVolume, cellVolume);
            maxCellDiagonal = Mathf.Max(maxCellDiagonal, merged.Size.Length());
            // Built this way the padded box always contains the plain one; the guard
            // stays so a future change of the merge rule cannot silently drop an
            // instance out of the volume it is supposed to describe.
            if (merged.Size.Length() < plain.Size.Length() - .001f) uncoveredInstances++;
            var footprintSide = GroundCoverCellSize * 1.75f;
            if (cellVolume > footprintSide * footprintSide * footprintSide * CellVolumeFootprintFactor) cellsOverFootprint++;
            maxSwayReserve = Mathf.Max(maxSwayReserve, cellSway);
            if (FoliageBoundsMeasured && cellSway > 0f)
            {
                // Documented engine mechanism for exactly this case: the vertex stage
                // moves foliage outside the CPU bounding box. The reserve is this
                // cell's measured envelope, not a constant, and the automatic merged
                // MultiMesh AABB stays authoritative for the instances themselves.
                cell.ExtraCullMargin = cellSway;
            }

            cell.SetMeta("cullingCellInstances", multimesh.InstanceCount);
            cell.SetMeta("cullingCellAabbDiagonal", merged.Size.Length());
            cell.SetMeta("cullingCellAabbVolume", cellVolume);
            cell.SetMeta("cullingCellSwayReserve", cellSway);
        }

        // --- VIS-030 for the individually culled trees: the same wind reserve,
        // measured per instance mesh. Their bounds are per trunk, so no group box is
        // involved and nothing is merged into one large volume.
        // Trees only: the batched ground-cover groups carry plantVariant too, but they
        // are VisualInstance3D and were measured as cells above. Same discriminator
        // ReconcileBuildingFoliage uses.
        var treeInstances = 0;
        var maxTreeSway = 0f;
        var tierCounts = new int[3];
        var tieredTrees = 0;
        var rangeGapTrees = 0;
        var doubleFullBandTrees = 0;
        var sharedRootTrees = 0;
        foreach (var tree in plants.GetChildren().OfType<Node3D>()
                     .Where(node => node is not VisualInstance3D && node.HasMeta("plantPosition")))
        {
            var bands = new float[4, 3];
            var present = new bool[3];
            foreach (var instance in EnumerateSelfAndDescendants<MeshInstance3D>(tree))
            {
                treeInstances++;
                // VIS-027 "обе стороны границы имеют одинаковый корень": every tier is
                // a child of the one rooted plant node and carries no transform of its
                // own, so the trunk base cannot move across the switch. Asserted here
                // from the actual node state instead of from the code that built it.
                if (instance.Position == Vector3.Zero && instance.RotationDegrees == Vector3.Zero
                    && instance.Scale == Vector3.One) sharedRootTrees++;
                var name = instance.Name.ToString();
                var suffix = name[(name.LastIndexOf('_') + 1)..];
                var tier = instance.GetMeta("visibilityRangeTier", -1).AsInt32();
                if (tier < 0 && suffix.StartsWith("LOD", StringComparison.Ordinal) && suffix.Length == 4
                    && int.TryParse(suffix[3..], out var parsed)) tier = parsed;
                if (tier is >= 0 and <= 2)
                {
                    if (!present[tier])
                    {
                        present[tier] = true;
                        tierCounts[tier]++;
                    }

                    bands[0, tier] = instance.VisibilityRangeBegin;
                    bands[1, tier] = instance.VisibilityRangeBeginMargin;
                    bands[2, tier] = instance.VisibilityRangeEnd;
                    bands[3, tier] = instance.VisibilityRangeEndMargin;
                }

                if (instance.Mesh is not ArrayMesh mesh) continue;
                var sample = SampleOf(mesh);
                var reserve = Mathf.Max(sample.SwayMarginX, sample.SwayMarginZ);
                maxTreeSway = Mathf.Max(maxTreeSway, reserve);
                if (FoliageBoundsMeasured && reserve > 0f) instance.ExtraCullMargin = reserve;
            }

            if (!present[0] || !present[1] || !present[2]) continue;
            tieredTrees++;
            // Godot's own band semantics: a tier is partially visible over
            // [begin, end] and fully visible over [begin + begin_margin,
            // end - end_margin]. A gap is the distance band where no tier of this
            // tree is drawn at all; a double-full band is where two complete copies
            // of the same trunk are drawn together. Both are arithmetic on the bands,
            // so they are verified per tree rather than assumed from the formula.
            for (var tier = 0; tier < 2; tier++)
            {
                if (bands[0, tier + 1] > bands[2, tier] + .001f) rangeGapTrees++;
                if (bands[0, tier + 1] + bands[1, tier + 1] < bands[2, tier] - bands[3, tier] - .001f) doubleFullBandTrees++;
            }
        }

        plants.SetMeta("lodTieredTreeCount", tieredTrees);
        plants.SetMeta("lodSharedRootInstanceCount", sharedRootTrees);
        plants.SetMeta("lodRangeGapBandCount", rangeGapTrees);
        plants.SetMeta("lodDoubleFullBandCount", doubleFullBandTrees);

        // --- VIS-029 census: the structural counts a station run attributes the
        // heavy view to. Draw calls themselves belong to the existing renderer
        // diagnostics probe; see the HANDOFF section of ledger_EXTERIOR.md.
        var shadowCasterTrees = EnumerateDescendants<MeshInstance3D>(plants)
            .Count(instance => instance.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off);
        var shadowCasterCells = EnumerateDescendants<MultiMeshInstance3D>(plants)
            .Count(instance => instance.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off);
        var meshes = new HashSet<ulong>();
        var materials = new HashSet<ulong>();
        var alphaSurfaces = 0;
        var surfaceTotal = 0;
        foreach (var mesh in _foliageMeshes.Values)
        {
            meshes.Add(mesh.GetRid().Id);
            surfaceTotal += mesh.GetSurfaceCount();
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.SurfaceGetMaterial(surface) is { } material) materials.Add(material.GetRid().Id);
                // Variant.VariantType, not System.Type: a shader parameter is a
                // Godot Variant (the same read PainterlyMaterialLibrary uses).
                if (mesh.SurfaceGetMaterial(surface) is ShaderMaterial shader
                    && shader.GetShaderParameter("cutout_texture").VariantType != Variant.Type.Nil) alphaSurfaces++;
            }
        }

        plants.SetMeta("lodTierFamiliesAudited", audited);
        plants.SetMeta("lodSilhouetteTolerancePercent", LodSilhouetteTolerancePercent);
        plants.SetMeta("lodSilhouetteWorstCrownDeltaPercent", worstCrown);
        plants.SetMeta("lodSilhouetteWorstHeightDeltaPercent", worstHeight);
        plants.SetMeta("lodSilhouetteWorstBaseDeltaPercent", worstBase);
        plants.SetMeta("lodSilhouetteFamiliesOverTolerance", overTolerance.ToArray());
        plants.SetMeta("lodFarTierAlphaClippedSurfaces", farAlphaSurfaces);
        plants.SetMeta("lodFarTierAlphaClippedFamilies", farAlphaFamilies.ToArray());
        plants.SetMeta("lodRangeScaleCeiling", LodRangeScaleCeiling);
        plants.SetMeta("cullingCellsAudited", cells);
        plants.SetMeta("cullingCellInstanceCount", cellInstances);
        plants.SetMeta("cullingCellMaxDiagonalMeters", maxCellDiagonal);
        plants.SetMeta("cullingCellMaxVolumeCubicMeters", maxCellVolume);
        plants.SetMeta("cullingCellsOverFootprintLimit", cellsOverFootprint);
        plants.SetMeta("cullingCellFootprintMeters", GroundCoverCellSize);
        plants.SetMeta("cullingSwayReserveMaxMeters", maxSwayReserve);
        plants.SetMeta("cullingTreeSwayReserveMaxMeters", maxTreeSway);
        plants.SetMeta("cullingInstancesUncovered", uncoveredInstances);
        plants.SetMeta("cullingBoundsMode", FoliageBoundsMode);
        plants.SetMeta("cullingBoundsPolicy", FoliageBoundsMeasured
            ? "per cell: merged transformed source bounds of every instance + measured wind reserve through extra_cull_margin; MultiMesh still culls as one group"
            : "engine AABB only; the measurement is reported, not applied");
        plants.SetMeta("foliageBudgetMode", FoliageBudgetMode);
        plants.SetMeta("foliageLodTierTreeCounts", tierCounts);
        plants.SetMeta("foliageTreeInstanceCount", treeInstances);
        plants.SetMeta("foliageSceneNodeCount", plants.GetChildCount());
        plants.SetMeta("foliageDistinctMeshCount", meshes.Count);
        plants.SetMeta("foliageDistinctMaterialCount", materials.Count);
        plants.SetMeta("foliageSurfaceCount", surfaceTotal);
        plants.SetMeta("foliageAlphaSurfaceCount", alphaSurfaces);
        plants.SetMeta("foliageShadowCasterCount", shadowCasterTrees + shadowCasterCells);
        plants.SetMeta("foliageOuterRowUnderstorySkipped", _outerRowUnderstorySkipped);
        plants.SetMeta("occlusionCullingActive", GetViewport().UseOcclusionCulling);
        // VIS-030 asks for the occluder source to be checked separately when one is
        // used. Nothing in this layer creates an occlusion occluder, and that has to
        // be evidenced rather than assumed, so the whole exterior layer is walked.
        plants.SetMeta("occlusionOccluderSourceCount", EnumerateDescendants<Node>(this)
            .Count(node => node is OccluderInstance3D));
        SetMeta("foliageLodAuditFamilies", audited);
        SetMeta("foliageCullingCells", cells);

        // Receipt line, assembled from invariant fragments. `a + b` between two
        // interpolated strings yields a plain string, which does not bind to
        // FormattableString.Invariant (CS1503) and would print the metrics in the
        // current culture; each metric group is therefore formatted invariantly on
        // its own before concatenation. The text of the line is unchanged.
        GD.Print(string.Concat(
            FormattableString.Invariant($"act1-foliage-lod: families={audited} tolerance={LodSilhouetteTolerancePercent:F1}%"),
            FormattableString.Invariant($" worstCrown={worstCrown:F2}% worstHeight={worstHeight:F2}% worstBase={worstBase:F2}%"),
            FormattableString.Invariant($" overTolerance={overTolerance.Count} farAlphaSurfaces={farAlphaSurfaces}"),
            FormattableString.Invariant($" cells={cells} cellInstances={cellInstances} maxCellDiagonal={maxCellDiagonal:F1}m"),
            FormattableString.Invariant($" maxCellVolume={maxCellVolume:F0}m3 overFootprint={cellsOverFootprint}"),
            FormattableString.Invariant($" swayReserve={maxSwayReserve:F3}m treeSwayReserve={maxTreeSway:F3}m uncovered={uncoveredInstances}"),
            FormattableString.Invariant($" bounds={FoliageBoundsMode} budget={FoliageBudgetMode} lodRangeScaleCeiling={LodRangeScaleCeiling:F2}"),
            FormattableString.Invariant($" tieredTrees={tieredTrees} sharedRootInstances={sharedRootTrees} rangeGaps={rangeGapTrees}"),
            FormattableString.Invariant($" doubleFullBands={doubleFullBandTrees}"),
            FormattableString.Invariant($" tiers=[{tierCounts[0]},{tierCounts[1]},{tierCounts[2]}] meshes={meshes.Count} materials={materials.Count}"),
            FormattableString.Invariant($" surfaces={surfaceTotal} alphaSurfaces={alphaSurfaces}"),
            FormattableString.Invariant($" shadowCasters={shadowCasterTrees + shadowCasterCells} skippedOuterUnderstory={_outerRowUnderstorySkipped}"),
            FormattableString.Invariant($" occlusion={GetViewport().UseOcclusionCulling} graphics={GraphicsQuality.BudgetSnapshot()}")));

        // VIS-085: the ring's own rhythm and layering, measured from the placements
        // this build emitted, in the same pass and the same log channel.
        AuditForestRingRhythm();
    }

    private static Vector3 ComponentMin(Vector3 a, Vector3 b) =>
        new(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y), Mathf.Min(a.Z, b.Z));

    private static Vector3 ComponentMax(Vector3 a, Vector3 b) =>
        new(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y), Mathf.Max(a.Z, b.Z));

    /// <summary>
    /// VIS-029/085 budget rule for the forest ring. The two outermost rows are the
    /// skyline: from any standable position they are only ever seen as silhouettes
    /// against fog, and their 0-2 m understory is exactly the layer the tall trunks
    /// in front of them already hide. Dropping those understory entries keeps every
    /// trunk, every closure guarantee and every authored position, and removes only
    /// what cannot be read.
    /// </summary>
    private static bool SuppressOuterRowUnderstory(int row, int rowCount) =>
        FoliageBudgetApplied && row <= 1 && rowCount > 3;

    /// <summary>VIS-029: the far tier exists precisely because it is never near.</summary>
    private static bool SuppressFarTierShadow(int lod) => FoliageBudgetApplied && lod == 2;

    /// <summary>
    /// Readback for the capture station and the performance receipt: the foliage
    /// layer's own budget numbers on one line, so a before/after pair can prove it
    /// ran on the same preset with the same structural cost. A caller assembling a
    /// frame summary can include it — see the HANDOFF section of ledger_EXTERIOR.md.
    /// </summary>
    public string DescribeFoliageBudget()
    {
        var plants = GetNodeOrNull<Node3D>("AgentB_PlantedFoliage");
        float Number(string key) => plants?.GetMeta(key, 0f).AsSingle() ?? 0f;
        // Same invariant-fragment assembly as the audit line above: concatenating
        // interpolated strings is a plain string and does not bind to
        // FormattableString.Invariant (CS1503). Output text is unchanged.
        return string.Concat(
            FormattableString.Invariant($"budget={FoliageBudgetMode} bounds={FoliageBoundsMode}"),
            FormattableString.Invariant($" families={(plants?.GetMeta("lodTierFamiliesAudited", 0).AsInt32() ?? 0)}"),
            FormattableString.Invariant($" worstCrown={Number("lodSilhouetteWorstCrownDeltaPercent"):F2}%"),
            FormattableString.Invariant($" worstHeight={Number("lodSilhouetteWorstHeightDeltaPercent"):F2}%"),
            FormattableString.Invariant($" overTolerance={(plants?.GetMeta("lodSilhouetteFamiliesOverTolerance").AsStringArray().Length ?? 0)}"),
            FormattableString.Invariant($" cells={(int)Number("cullingCellsAudited")} cellInstances={(int)Number("cullingCellInstanceCount")}"),
            FormattableString.Invariant($" maxCellDiagonal={Number("cullingCellMaxDiagonalMeters"):F1}m"),
            FormattableString.Invariant($" overFootprint={(int)Number("cullingCellsOverFootprintLimit")}"),
            FormattableString.Invariant($" swayReserve={Number("cullingSwayReserveMaxMeters"):F3}m"),
            FormattableString.Invariant($" meshes={(int)Number("foliageDistinctMeshCount")} materials={(int)Number("foliageDistinctMaterialCount")}"),
            FormattableString.Invariant($" surfaces={(int)Number("foliageSurfaceCount")} alphaSurfaces={(int)Number("foliageAlphaSurfaceCount")}"),
            FormattableString.Invariant($" shadowCasters={(int)Number("foliageShadowCasterCount")} skippedOuterUnderstory={_outerRowUnderstorySkipped}"),
            FormattableString.Invariant($" graphics={GraphicsQuality.BudgetSnapshot()}"));
    }
}
