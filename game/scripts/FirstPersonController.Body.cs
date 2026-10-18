using Godot;

namespace Urman.Godot;

public partial class FirstPersonController
{
    private const string BodyPrefix = "CouncilWitness";
    private Node3D _visibleBody = null!;
    private Skeleton3D _bodySkeleton = null!;
    private readonly List<BodyLeg> _bodyLegs = new();
    private int _bodySpine;
    private Vector3 _bodySpineRest;
    private int _bodyUpperCoat;
    private Transform3D _bodyUpperCoatRest;
    private MeshInstance3D? _bodyCoatMesh;
    private Vector3 _bodyPreviousFeet;
    private float _bodyGait;
    private float _bodyCrouch;
    private float _bodyLocomotionDip;
    private float _bodyAirBlend;
    private float _bodyLandingCompression;
    private float _bodyVerticalSpeedBeforeMove;
    private double _bodyGroundTravelMeters;
    private double _bodyLastFootContactTravelMeters;
    private double _bodyFootContactIntervalMeters;
    private ulong _bodyFootContactCount;
    private int _bodyNextSwingLegIndex;
    private bool _bodyWasGrounded;
    private bool _bodyTraveling;
    private global::Godot.Collections.Array<Rid> _bodyFloorExclude = null!;

    private sealed record BodyLeg(int Thigh, int Knee, int Ankle, Transform3D ThighRest,
        Transform3D KneeRest, Transform3D AnkleRest, Vector3 Sole, float UpperLength, float LowerLength)
    {
        public Transform3D? TurnPlant;
        public Transform3D TurnFrom, TurnTarget;
        public float TurnProgress = 1f;
        public Transform3D? GaitPlant;
        public Transform3D SwingFrom, SwingTarget;
        public Vector2 SwingDirection;
        public float SwingStartRock, SwingStartLift;
        public bool PreserveSwingStartPose;
        public Transform3D? LastFootPose;
        public float FootRockRadians, FootLiftMeters;
        public bool IsFootSettling;
        public float SettleProgress = 1f;
        public Transform3D SettleTargetFoot;
        public Vector3 SettleFromSole, SettleTargetSole;
        public float SettleStartRock, SettleStartLift;
        public Transform3D AirPoseFrom;
        public float AirPoseProgress = 1f;
        public bool AirPoseBlendActive, AirPoseJustStarted;
        public double SwingStartTravel;
        public float SwingTravelMeters;
        public float StanceTravelMeters;
        public double PlantStartTravel;
        public bool IsSwinging;
    }

    internal int VisibleBodyMeshCount { get; private set; }
    internal double BodyGroundTravelMeters => _bodyGroundTravelMeters;
    internal ulong BodyFootContactCount => _bodyFootContactCount;
    internal double BodyFootContactIntervalMeters => _bodyFootContactIntervalMeters;
    internal Vector3[] VisibleBodySoles => _bodyLegs.Select(leg =>
        _bodySkeleton.GlobalTransform * _bodySkeleton.GetBoneGlobalPose(leg.Ankle)
        * (leg.AnkleRest.AffineInverse() * leg.Sole)).ToArray();

    // A free capture camera looks at the street from beside the parked player; the
    // first-person body exists only for the player's own camera and would otherwise
    // stand in the middle of the frame as a headless figure.
    internal void SetCaptureBodyHidden(bool hidden)
    {
        if (_visibleBody is not null && IsInstanceValid(_visibleBody) && !VehicleControlled)
            _visibleBody.Visible = !hidden;
    }

    private void InitializeVisibleBody()
    {
        // Reuse the project's shaped winter trousers, boots and coat surfaces.
        // The source kit and NPC skins are immutable; knees/ankles and tailoring
        // belong only to this first-person instance, with no new collision owner.
        _visibleBody = GeneratedCharacterKitDressing.Attach(this, "aidar-first-person", BodyPrefix, Vector3.Zero, sheltered: true);
        _visibleBody.Name = "AidarLowerBody";
        _visibleBody.RotationDegrees = new(0, 180, 0);
        // The hips are slightly behind the capsule/neck axis. This is a fixed
        // anatomical offset, not a camera-facing overlay; the real boot soles
        // still resolve their own support points inside the same player body.
        _visibleBody.Position += new Vector3(0, 0, .06f);
        GeneratedCharacterKitDressing.GroundSolesOnAnchor(_visibleBody);
        foreach (var animation in _visibleBody.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>())
        {
            animation.Stop();
            animation.QueueFree();
        }
        var all = _visibleBody.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
        var selected = all.Where(mesh => mesh.Name.ToString() is
            "CouncilWitness_TrouserLeft_LOD0" or "CouncilWitness_TrouserRight_LOD0"
            or "CouncilWitness_BootLeft_LOD0" or "CouncilWitness_BootRight_LOD0"
            or "CouncilWitness_Body_LOD0" or "CouncilWitness_CoatHem_LOD0").ToArray();
        var firstBoot = selected.Single(mesh => mesh.Name == "CouncilWitness_BootLeft_LOD0");
        _bodySkeleton = firstBoot.GetNode<Skeleton3D>(firstBoot.Skeleton);
        _bodySkeleton.ResetBonePoses();
        foreach (var mesh in all)
        {
            if (!selected.Contains(mesh)) { mesh.Visible = false; mesh.QueueFree(); continue; }
            mesh.Visible = true;
            mesh.VisibilityRangeBegin = mesh.VisibilityRangeEnd = 0;
            mesh.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            mesh.ExtraCullMargin = .8f;
        }
        // Other characters' rigs/anchors are not kept hidden and ticking inside
        // the player. Shared imported mesh resources remain with their NPCs.
        foreach (var child in _visibleBody.GetChildren().OfType<Node3D>().ToArray())
            if (child != _bodySkeleton && !child.IsAncestorOf(_bodySkeleton)) child.QueueFree();

        _bodySpine = _bodySkeleton.FindBone("Spine");
        _bodySpineRest = _bodySkeleton.GetBonePosePosition(_bodySpine);
        PrepareBodyCoat(selected.Single(mesh => mesh.Name == "CouncilWitness_Body_LOD0"));
        foreach (var side in new[] { "Left", "Right" })
            PrepareBodyLeg(selected.Single(mesh => mesh.Name == $"{BodyPrefix}_Trouser{side}_LOD0"),
                selected.Single(mesh => mesh.Name == $"{BodyPrefix}_Boot{side}_LOD0"), side);
        InitializeFootwearPresentation(selected);
        _bodyFloorExclude = new() { GetRid() };
        VisibleBodyMeshCount = selected.Length;
        _visibleBody.SetMeta("sourcePolicy", "project character kit; continuous trouser pelvis, articulated coat, knee and ankle skinning");
        _visibleBody.SetMeta("collisionPolicy", "presentation only; existing player capsule owns all contact");
        ResetVisibleBodyMotion();
    }

    private static int BodyBind(Skin skin, Skeleton3D skeleton, int bone)
    {
        for (var bind = 0; bind < skin.GetBindCount(); bind++)
        {
            var name = skin.GetBindName(bind).ToString();
            if ((name.Length > 0 ? skeleton.FindBone(name) : skin.GetBindBone(bind)) == bone) return bind;
        }
        throw new InvalidOperationException("The first-person garment has no matching source bone.");
    }

    private int AddBodyBone(string name, int parent, Transform3D globalRest)
    {
        _bodySkeleton.AddBone(name);
        var bone = _bodySkeleton.FindBone(name);
        _bodySkeleton.SetBoneParent(bone, parent);
        _bodySkeleton.SetBoneRest(bone, _bodySkeleton.GetBoneGlobalRest(parent).AffineInverse() * globalRest);
        _bodySkeleton.ResetBonePose(bone);
        return bone;
    }

    private void PrepareBodyLeg(MeshInstance3D trouser, MeshInstance3D boot, string side)
    {
        var thigh = _bodySkeleton.FindBone(side == "Left" ? "Leg.L" : "Leg.R");
        var thighRest = _bodySkeleton.GetBoneGlobalRest(thigh);
        var bootSourceSkin = boot.GetSkinReference()?.GetSkin() ?? boot.Skin;
        var bootBind = BodyBind(bootSourceSkin, _bodySkeleton, thigh);
        var bootToSkeleton = thighRest * bootSourceSkin.GetBindPose(bootBind);
        // SurfaceGetArrays() builds a new caller-owned Array; AsVector3Array()
        // already copied the Variant out of it, so the returned boot profile is
        // identical whether or not this wrapper is released after the read.
        using var bootArrays = boot.Mesh.SurfaceGetArrays(0);
        var vertices = bootArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array()
            .Select(point => bootToSkeleton * point).ToArray();
        var bottom = vertices.Min(point => point.Y);
        var soles = vertices.Where(point => point.Y <= bottom + .0002f).Distinct().ToArray();
        var sole = soles.Aggregate(Vector3.Zero, (sum, point) => sum + point) / soles.Length;
        var kneeRest = new Transform3D(Basis.Identity, new(thighRest.Origin.X, .47f, .025f));
        var ankleRest = new Transform3D(Basis.Identity, sole + Vector3.Up * .095f);
        var knee = AddBodyBone("AidarKnee" + side, thigh, kneeRest);
        var ankle = AddBodyBone("AidarAnkle" + side, knee, ankleRest);

        var bootSkin = (Skin)bootSourceSkin.Duplicate();
        bootSkin.SetBindPose(bootBind, ankleRest.AffineInverse() * bootToSkeleton);
        bootSkin.SetBindName(bootBind, "AidarAnkle" + side);
        bootSkin.SetBindBone(bootBind, ankle);
        boot.Skin = bootSkin;

        var sourceSkin = trouser.GetSkinReference()?.GetSkin() ?? trouser.Skin;
        var thighBind = BodyBind(sourceSkin, _bodySkeleton, thigh);
        var modelToSkeleton = thighRest * sourceSkin.GetBindPose(thighBind);
        TailorBodyTrousers(trouser, modelToSkeleton, thigh, knee, side);
        _bodyLegs.Add(new(thigh, knee, ankle, thighRest, kneeRest, ankleRest, sole,
            thighRest.Origin.DistanceTo(kneeRest.Origin), kneeRest.Origin.DistanceTo(ankleRest.Origin)));
    }

    private void PrepareBodyCoat(MeshInstance3D coat)
    {
        _bodyCoatMesh = coat;
        // Preserve the real chest/waist/shoulder surface. Cutting across its
        // waist exposes the technical tops of the legs; closing that cut makes
        // a disk. The upper garment instead folds with the crouched torso.
        var source = coat.GetSkinReference()?.GetSkin() ?? coat.Skin;
        var bind = BodyBind(source, _bodySkeleton, _bodySpine);
        var toSkeleton = _bodySkeleton.GetBoneGlobalRest(_bodySpine) * source.GetBindPose(bind);
        _bodyUpperCoatRest = new(Basis.Identity, new(0, .85f, 0));
        _bodyUpperCoat = AddBodyBone("AidarUpperCoat", _bodySpine, _bodyUpperCoatRest);
        var skin = (Skin)source.Duplicate();
        var upperBind = skin.GetBindCount();
        skin.AddNamedBind("AidarUpperCoat", _bodyUpperCoatRest.AffineInverse() * toSkeleton);
        skin.SetBindBone(upperBind, _bodyUpperCoat);
        var shaped = new ArrayMesh();
        for (var surface = 0; surface < coat.Mesh.GetSurfaceCount(); surface++)
        {
            // Same as above: the surface arrays are read into managed copies and
            // copied again by AddSurfaceFromArrays, so the wrapper is ours.
            using var arrays = coat.Mesh.SurfaceGetArrays(surface);
            var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var bones = new int[points.Length * 4];
            var weights = new float[points.Length * 4];
            for (var point = 0; point < points.Length; point++)
            {
                var upper = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.90f, 1.10f, (toSkeleton * points[point]).Y));
                bones[point * 4] = bind; bones[point * 4 + 1] = upperBind;
                weights[point * 4] = 1f - upper; weights[point * 4 + 1] = upper;
            }
            arrays[(int)Mesh.ArrayType.Bones] = bones;
            arrays[(int)Mesh.ArrayType.Weights] = weights;
            shaped.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        coat.Mesh = shaped;
        coat.Skin = skin;
    }

    private void TailorBodyTrousers(MeshInstance3D trouser, Transform3D toSkeleton, int thigh, int knee, string side)
    {
        // The NPC asset uses separately closed trouser tubes hidden beneath its
        // coat. In first person those caps are visible. Retain its knee, calf and
        // ankle profiles, then join the two upper halves at a common crotch seam
        // and waistband. Both seam halves use Spine, independent of either step.
        // As*Array() below copies each Variant out of the freshly built surface
        // Array before this scope ends, so the retargeted trouser mesh and its
        // skin are unchanged; only the temporary native Array is released.
        using var arrays = trouser.Mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(point => toSkeleton * point).ToArray();
        var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
        var winding = 0f;
        for (var index = 0; index < indices.Length && Math.Abs(winding) < .0000001f; index += 3)
            winding = (vertices[indices[index + 1]] - vertices[indices[index]])
                .Cross(vertices[indices[index + 2]] - vertices[indices[index]])
                .Dot(toSkeleton.Basis * normals[indices[index]]);
        if (Math.Abs(winding) < .0000001f) throw new InvalidOperationException("Source trousers have no oriented surface.");
        var source = vertices.Distinct().ToArray();
        var sourceRings = new[] { .65f, .47f, .30f, .095f }.Select(height =>
            source.Where(point => Math.Abs(point.Y - height) < .035f).ToArray()).ToArray();
        if (sourceRings.Any(ring => ring.Length != 7))
            throw new InvalidOperationException("Aidar's source trouser profile no longer matches the imported character kit.");
        var sign = side == "Left" ? -1f : 1f;
        const int sides = 16;
        var rings = new Vector3[5][];
        rings[0] = Enumerable.Range(0, sides).Select(index =>
        {
            var angle = index * Mathf.Tau / sides;
            var outside = Mathf.Cos(angle);
            return new Vector3(sign * .185f * Math.Max(0, outside),
                .99f - .25f * Math.Max(0, -outside), .13f * Mathf.Sin(angle));
        }).ToArray();
        for (var ring = 1; ring < rings.Length; ring++)
            rings[ring] = Enumerable.Range(0, sides).Select(index =>
                SampleBodyClothRing(sourceRings[ring - 1], Mathf.Atan2(Mathf.Sin(index * Mathf.Tau / sides),
                    sign * Mathf.Cos(index * Mathf.Tau / sides)))).ToArray();

        var arcLengths = rings.Select(ring =>
        {
            var lengths = new float[sides + 1];
            for (var point = 1; point <= sides; point++)
                lengths[point] = lengths[point - 1] + ring[point % sides].DistanceTo(ring[point - 1]);
            return lengths;
        }).ToArray();
        using var builder = new SurfaceTool();
        builder.Begin(Mesh.PrimitiveType.Triangles);
        builder.SetSmoothGroup(0);
        void Emit(int ring, int point)
        {
            var vertex = rings[ring][point % sides];
            var lower = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.42f, .53f, vertex.Y));
            builder.SetUV(new(arcLengths[ring][point], 1f - vertex.Y));
            builder.SetBones(new[] { 0, 1, 2, 0 });
            builder.SetWeights(ring == 0 ? new[] { 0f, 0f, 1f, 0f } : new[] { 1f - lower, lower, 0f, 0f });
            builder.AddVertex(vertex);
        }
        for (var ring = 0; ring + 1 < rings.Length; ring++)
        for (var point = 0; point < sides; point++)
        {
            // Mirroring one leg reverses its contour. Keep Godot's clockwise
            // front faces on the outside of both halves of the same garment.
            if ((sign > 0) == (winding > 0))
            {
                Emit(ring, point); Emit(ring + 1, point + 1); Emit(ring + 1, point);
                Emit(ring, point); Emit(ring, point + 1); Emit(ring + 1, point + 1);
            }
            else
            {
                Emit(ring, point); Emit(ring + 1, point); Emit(ring + 1, point + 1);
                Emit(ring, point); Emit(ring + 1, point + 1); Emit(ring, point + 1);
            }
        }
        builder.Index();
        builder.GenerateNormals();
        trouser.Mesh = builder.Commit();
        var skin = new Skin();
        foreach (var bone in new[] { thigh, knee, _bodySpine })
        {
            var bind = skin.GetBindCount();
            skin.AddNamedBind(_bodySkeleton.GetBoneName(bone), _bodySkeleton.GetBoneGlobalRest(bone).AffineInverse());
            skin.SetBindBone(bind, bone);
        }
        trouser.Skin = skin;
        trouser.SetMeta("waistPolicy", "shared spine-weighted crotch seam and waistband; source knee/calf/ankle profiles");
    }

    private static Vector3 SampleBodyClothRing(Vector3[] source, float angle)
    {
        var centre = source.Aggregate(Vector3.Zero, (sum, point) => sum + point) / source.Length;
        var ordered = source.OrderBy(point => Mathf.Atan2(point.Z - centre.Z, point.X - centre.X)).ToArray();
        var angles = ordered.Select(point => Mathf.Atan2(point.Z - centre.Z, point.X - centre.X)).ToArray();
        if (angle < angles[0]) angle += Mathf.Tau;
        for (var index = 0; index < ordered.Length; index++)
        {
            var end = index + 1 == ordered.Length ? angles[0] + Mathf.Tau : angles[index + 1];
            if (angle > end) continue;
            var weight = (angle - angles[index]) / (end - angles[index]);
            return ordered[index].CubicInterpolate(ordered[(index + 1) % ordered.Length],
                ordered[(index + ordered.Length - 1) % ordered.Length], ordered[(index + 2) % ordered.Length], weight);
        }
        throw new InvalidOperationException("Source trouser ring does not enclose its profile centre.");
    }

    /// <summary>
    /// VIS-047: the carried thing must be held by this body, not hover in front of
    /// the camera. The grip envelope is measured from the coat that is actually
    /// skinned and drawn here — its own surface extreme along the direction the
    /// player is looking — so a heavier coat moves the grip with it instead of
    /// leaving the item at a hard-coded distance. Presentation only: no collision,
    /// no custody and no interaction identity is created or moved by this query.
    /// </summary>
    internal readonly record struct CarryGrip(Vector3 Chest, Vector3 CoatFront);

    internal bool TryGetCarryGrip(Vector3 facingHorizontal, out CarryGrip grip)
    {
        grip = default;
        if (_bodyCoatMesh is not { } coat || !IsInstanceValid(coat) || coat.IsQueuedForDeletion()) return false;
        if (_visibleBody is null || !IsInstanceValid(_visibleBody) || VehicleControlled) return false;
        if (facingHorizontal.LengthSquared() < .000001f) return false;
        var facing = facingHorizontal.Normalized();
        var chest = _bodySkeleton.GlobalTransform
            * _bodySkeleton.GetBoneGlobalPose(_bodyUpperCoat).Origin;
        var bounds = coat.GetAabb();
        var front = float.NegativeInfinity;
        for (var index = 0; index < 8; index++)
        {
            var corner = coat.ToGlobal(bounds.GetEndpoint(index)) - chest;
            front = Math.Max(front, corner.Dot(facing));
        }
        if (!float.IsFinite(front)) return false;
        grip = new CarryGrip(chest, chest + facing * Math.Max(front, 0f));
        return true;
    }

    private void ResetVisibleBodyMotion()
    {
        _bodyPreviousFeet = GlobalPosition;
        _bodyGait = 0;
        _bodyLocomotionDip = 0;
        _bodyGroundTravelMeters = 0;
        _bodyLastFootContactTravelMeters = 0;
        _bodyFootContactIntervalMeters = 0;
        _bodyFootContactCount = 0;
        _bodyNextSwingLegIndex = 0;
        _bodyTraveling = false;
        _bodyWasGrounded = false;
        _bodyAirBlend = _bodyLandingCompression = 0;
        foreach (var leg in _bodyLegs)
        {
            leg.TurnPlant = leg.GaitPlant = leg.LastFootPose = null;
            leg.IsSwinging = false;
            leg.IsFootSettling = false;
            leg.FootRockRadians = leg.FootLiftMeters = 0;
            leg.SettleProgress = 1;
            leg.AirPoseProgress = 1;
            leg.AirPoseBlendActive = leg.AirPoseJustStarted = false;
            leg.SwingTravelMeters = leg.StanceTravelMeters = 0;
            leg.SwingDirection = Vector2.Zero;
            leg.SwingStartRock = leg.SwingStartLift = 0;
            leg.PreserveSwingStartPose = false;
            leg.PlantStartTravel = 0;
            leg.TurnProgress = 1;
        }
    }

    private void SetBodyGlobalPose(int bone, Transform3D pose)
    {
        var parent = _bodySkeleton.GetBoneParent(bone);
        var parentPose = parent >= 0 ? _bodySkeleton.GetBoneGlobalPose(parent) : Transform3D.Identity;
        _bodySkeleton.SetBonePose(bone, parentPose.AffineInverse() * pose);
    }

    private void UpdateTurnPlants(bool stationary)
    {
        foreach (var leg in _bodyLegs)
        {
            if (!stationary) { leg.TurnPlant = null; leg.TurnProgress = 1; continue; }
            if (leg.TurnPlant is null)
            {
                leg.TurnPlant = leg.LastFootPose ?? leg.GaitPlant
                    ?? _bodySkeleton.GlobalTransform * new Transform3D(Basis.Identity, ProjectBodyFoot(leg.AnkleRest.Origin));
                leg.TurnProgress = 1;
            }
        }
        if (!stationary || _bodyLegs.Any(leg => leg.TurnProgress < 1)) return;
        // Keep one foot planted in the world while the other catches up with
        // the torso. A small look adjustment needs no step; a real pivot does.
        BodyLeg? next = null;
        var largestTurn = Mathf.DegToRad(25f);
        foreach (var leg in _bodyLegs)
        {
            var turn = Mathf.Acos(Mathf.Clamp(leg.TurnPlant!.Value.Basis.Z.Normalized()
                .Dot(_bodySkeleton.GlobalBasis.Z.Normalized()), -1f, 1f));
            if (turn <= largestTurn) continue;
            next = leg; largestTurn = turn;
        }
        if (next is null) return;
        next.TurnFrom = next.TurnPlant!.Value;
        next.TurnTarget = _bodySkeleton.GlobalTransform * new Transform3D(Basis.Identity, ProjectBodyFoot(next.AnkleRest.Origin));
        next.TurnProgress = 0;
    }

    private Vector3 ProjectBodyFoot(Vector3 foot)
    {
        var at = _bodySkeleton.ToGlobal(foot);
        using var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * .22f,
            at - Vector3.Up * .38f, CollisionMask, _bodyFloorExclude);
        // IntersectRay() hands out a fresh caller-owned Dictionary; the query is
        // already disposed. Only the normal and position are read from it, so
        // releasing it here cannot change a projected foot height.
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit["normal"].AsVector3().Y > .70f)
            foot.Y = _bodySkeleton.ToLocal(hit["position"].AsVector3()).Y + .095f;
        return foot;
    }

    private Vector3 BodyDirectionInSkeleton(Vector2 movement)
    {
        if (movement.LengthSquared() < .000001f) return Vector3.Back;
        var direction = _bodySkeleton.GlobalBasis.Inverse() * new Vector3(movement.X, 0, movement.Y).Normalized();
        direction.Y = 0;
        return direction.LengthSquared() > .000001f ? direction.Normalized() : Vector3.Back;
    }

    private float BodyStanceTravel(BodyLeg leg, Vector3 hipShift, Vector3 direction)
    {
        var foot = leg.GaitPlant is { } plant
            ? (_bodySkeleton.GlobalTransform.AffineInverse() * plant).Origin
            : leg.AnkleRest.Origin;
        var hip = leg.ThighRest.Origin + hipShift;
        var reach = Math.Max(.05f, leg.UpperLength + leg.LowerLength - .035f);
        var vertical = foot.Y - hip.Y;
        var planarReach = Mathf.Sqrt(Math.Max(0, reach * reach - vertical * vertical));
        var horizontalDirection = new Vector2(direction.X, direction.Z).Normalized();
        if (horizontalDirection.LengthSquared() < .000001f) horizontalDirection = Vector2.Up;
        var offset = new Vector2(foot.X - hip.X, foot.Z - hip.Z);
        var along = offset.Dot(horizontalDirection);
        var across = offset - horizontalDirection * along;
        var available = along + Mathf.Sqrt(Math.Max(0, planarReach * planarReach - across.LengthSquared()));
        // Stance consumes only three quarters of the measured leg reach. The
        // remaining margin lets the existing two-bone IK absorb slopes/steps
        // without stretching the imported trousers or boot. This is a per-foot
        // reachable travel window, not a fixed cadence shared by every direction.
        return Mathf.Clamp(available * .75f, .02f, .65f);
    }

    private void BeginBodyFootSettle(BodyLeg leg)
    {
        if (leg.LastFootPose is not { } last)
        {
            leg.IsSwinging = false;
            return;
        }

        var soleOffset = leg.AnkleRest.AffineInverse() * leg.Sole;
        var fromSole = last * soleOffset;
        var neutralBasis = last.Basis * new Basis(Vector3.Right, -leg.FootRockRadians);
        var neutralOrigin = fromSole - neutralBasis * soleOffset;
        var neutralLocal = _bodySkeleton.GlobalTransform.AffineInverse()
            * new Transform3D(neutralBasis, neutralOrigin);
        var targetOrigin = ProjectBodyFoot(neutralLocal.Origin);
        leg.SettleTargetFoot = _bodySkeleton.GlobalTransform
            * new Transform3D(neutralLocal.Basis, targetOrigin);
        leg.SettleFromSole = fromSole;
        leg.SettleTargetSole = leg.SettleTargetFoot * soleOffset;
        leg.SettleStartRock = leg.FootRockRadians;
        leg.SettleStartLift = leg.FootLiftMeters;
        leg.SettleProgress = 0;
        leg.IsFootSettling = true;
        leg.IsSwinging = false;
        leg.GaitPlant = null;
    }

    private static float BodyForwardLead(BodyLeg leg, Vector3 hipShift, Vector3 direction)
    {
        var alongDirection = direction.Normalized();
        var safeReach = Math.Max(.05f, leg.UpperLength + leg.LowerLength - .04f);
        var offset = leg.AnkleRest.Origin - (leg.ThighRest.Origin + hipShift);
        var along = offset.Dot(alongDirection);
        var across = offset - alongDirection * along;
        var maximumAlong = Mathf.Sqrt(Math.Max(0, safeReach * safeReach - across.LengthSquared()));
        // Keep a small but visible lead at touchdown, bounded by the actual
        // leg envelope at the lowered moving hip. The final two-bone solve still
        // clamps to the exact imported thigh and shin lengths.
        return Mathf.Clamp(maximumAlong - along, 0f, .26f);
    }

    private void RecordBodyFootContact()
    {
        if (_bodyFootContactCount > 0)
            _bodyFootContactIntervalMeters = Math.Max(0, _bodyGroundTravelMeters - _bodyLastFootContactTravelMeters);
        _bodyLastFootContactTravelMeters = _bodyGroundTravelMeters;
        _bodyFootContactCount++;
    }

    private void UpdateGroundFootCycle(Vector3 hipShift, Vector3 travelDirection, Vector2 actualMovement)
    {
        if (_bodyLegs.Count != 2 || !_bodyTraveling || _bodyLegs.Any(leg => leg.IsFootSettling)) return;
        var activeSwingIndex = _bodyLegs.FindIndex(leg => leg.IsSwinging);
        if (activeSwingIndex >= 0)
        {
            var swinging = _bodyLegs[activeSwingIndex];
            var travel = (float)Math.Max(0, _bodyGroundTravelMeters - swinging.SwingStartTravel);
            var actualDirection = actualMovement.Normalized();
            if (swinging.SwingDirection.LengthSquared() > .000001f
                && actualDirection.LengthSquared() > .000001f
                && swinging.SwingDirection.Dot(actualDirection) < 0f)
            {
                var reversalSupportWindow = BodyStanceTravel(_bodyLegs[1 - activeSwingIndex], hipShift, travelDirection);
                var reversalDistance = Mathf.Max(.02f, Math.Min(.55f, reversalSupportWindow * .85f));

                // Start the reversed arc from the ankle pose the skeleton is
                // displaying now. Remove this swing's lift/rock from its base
                // pose, then carry those offsets into the new arc exactly once.
                var visibleFoot = _bodySkeleton.GlobalTransform
                    * _bodySkeleton.GetBoneGlobalPose(swinging.Ankle);
                var visibleLocal = _bodySkeleton.GlobalTransform.AffineInverse() * visibleFoot;
                var soleOffset = swinging.AnkleRest.AffineInverse() * swinging.Sole;
                var neutralBasis = visibleLocal.Basis
                    * new Basis(Vector3.Right, -swinging.FootRockRadians);
                var neutralOrigin = visibleLocal.Origin + visibleLocal.Basis * soleOffset
                    - neutralBasis * soleOffset - Vector3.Up * swinging.FootLiftMeters;
                swinging.SwingFrom = _bodySkeleton.GlobalTransform
                    * new Transform3D(neutralBasis, neutralOrigin);
                swinging.SwingStartLift = swinging.FootLiftMeters;
                swinging.SwingStartRock = swinging.FootRockRadians;
                swinging.PreserveSwingStartPose = true;
                swinging.SwingStartTravel = _bodyGroundTravelMeters;
                swinging.SwingTravelMeters = reversalDistance;
                swinging.SwingDirection = actualDirection;
                var reversalLead = BodyForwardLead(swinging, hipShift, travelDirection);
                var reversalTargetLocal = swinging.AnkleRest.Origin + travelDirection * (reversalDistance + reversalLead);
                swinging.SwingTarget = _bodySkeleton.GlobalTransform
                    * new Transform3D(Basis.Identity, ProjectBodyFoot(reversalTargetLocal));
                travel = 0;
            }
            var progress = Mathf.Clamp(travel / Math.Max(.02f, swinging.SwingTravelMeters), 0f, 1f);
            if (progress < 1f) return;

            swinging.IsSwinging = false;
            swinging.GaitPlant = swinging.SwingTarget;
            swinging.PlantStartTravel = _bodyGroundTravelMeters;
            swinging.StanceTravelMeters = BodyStanceTravel(swinging, hipShift, travelDirection);
            _bodyNextSwingLegIndex = 1 - activeSwingIndex;
            RecordBodyFootContact();
            return;
        }

        var step = _bodyLegs[_bodyNextSwingLegIndex];
        var support = _bodyLegs[1 - _bodyNextSwingLegIndex];
        if (step.GaitPlant is null || support.GaitPlant is null) return;
        var travelFromPlant = (float)Math.Max(0, _bodyGroundTravelMeters - step.PlantStartTravel);
        var stepFootLocal = _bodySkeleton.GlobalTransform.AffineInverse() * step.GaitPlant.Value;
        var stepHip = step.ThighRest.Origin + hipShift;
        var stepReach = step.UpperLength + step.LowerLength - .04f;
        var stepNearReach = (stepFootLocal.Origin - stepHip).Length() >= stepReach * .94f;
        var supportWindow = BodyStanceTravel(support, hipShift, travelDirection);
        // Start the first foot transfer as soon as actual movement begins; the
        // opposite sole stays planted while the torso moves through its reach.
        var firstTransfer = _bodyFootContactCount == 0;
        if (!firstTransfer && travelFromPlant < step.StanceTravelMeters && !stepNearReach) return;

        // Land before the opposite planted leg reaches its safe IK envelope.
        // The small margin also gives the next foot a grounded contact frame.
        var swingDistance = Mathf.Max(.02f, Math.Min(.55f, supportWindow * .85f));
        step.SwingFrom = step.GaitPlant.Value;
        step.SwingStartTravel = _bodyGroundTravelMeters;
        step.SwingTravelMeters = swingDistance;
        step.SwingDirection = actualMovement.Normalized();
        step.SwingStartLift = step.SwingStartRock = 0;
        step.PreserveSwingStartPose = false;
        var lead = BodyForwardLead(step, hipShift, travelDirection);
        var targetLocal = step.AnkleRest.Origin + travelDirection * (swingDistance + lead);
        step.SwingTarget = _bodySkeleton.GlobalTransform
            * new Transform3D(Basis.Identity, ProjectBodyFoot(targetLocal));
        step.GaitPlant = null;
        step.StanceTravelMeters = 0;
        step.IsSwinging = true;
    }

    private void UpdateVisibleBody(double delta, bool moving)
    {
        if (_visibleBody is null || VehicleControlled) return;
        var movement = new Vector2(GlobalPosition.X - _bodyPreviousFeet.X, GlobalPosition.Z - _bodyPreviousFeet.Z);
        _bodyPreviousFeet = GlobalPosition;
        var distance = movement.Length();
        if (distance > .6f) { movement = Vector2.Zero; distance = 0; }
        var grounded = IsOnFloor() && !IsClimbingLadder;
        _bodyTraveling = grounded && distance > .0005f;
        if (grounded && distance > .0001f)
        {
            _bodyGroundTravelMeters += distance;
        }
        if (_bodyFootContactIntervalMeters > .001f)
        {
            var intervalProgress = Mathf.Clamp((float)((_bodyGroundTravelMeters - _bodyLastFootContactTravelMeters)
                / _bodyFootContactIntervalMeters), 0f, 1f);
            _bodyGait = (_bodyFootContactCount & 1) == 0
                ? intervalProgress * .5f
                : .5f + intervalProgress * .5f;
        }
        var wasGrounded = _bodyWasGrounded;
        var tookOff = wasGrounded && !grounded;
        if (grounded && !wasGrounded)
            _bodyLandingCompression = Mathf.Clamp(-_bodyVerticalSpeedBeforeMove * .006f, 0, .025f);
        if (_modalOpen || grounded)
        {
            foreach (var leg in _bodyLegs)
            {
                leg.AirPoseBlendActive = leg.AirPoseJustStarted = false;
                leg.AirPoseProgress = 1;
            }
        }
        else if (tookOff)
        {
            foreach (var leg in _bodyLegs)
            {
                leg.AirPoseBlendActive = false;
                leg.AirPoseProgress = 0;
                if (leg.LastFootPose is not { } last) continue;
                var priorLocal = _bodySkeleton.GlobalTransform.AffineInverse() * last;
                var offset = priorLocal.Origin - leg.AnkleRest.Origin;
                if (offset.Length() > .35f) offset = offset.Normalized() * .35f;
                // A short, local transition preserves the launch pose without
                // pinning the boot to its takeoff world position during flight.
                var priorRotation = priorLocal.Basis.Orthonormalized().GetRotationQuaternion();
                var priorAngle = priorRotation.GetAngle();
                var priorAxis = priorRotation.GetAxis();
                var boundedBasis = priorAngle > .18f && priorAxis.LengthSquared() > .000001f
                    ? new Basis(new Quaternion(priorAxis.Normalized(), .18f))
                    : priorLocal.Basis.Orthonormalized();
                leg.AirPoseFrom = new Transform3D(boundedBasis, leg.AnkleRest.Origin + offset);
                leg.AirPoseBlendActive = true;
                leg.AirPoseJustStarted = true;
            }
        }
        _bodyWasGrounded = grounded;
        _bodyAirBlend = Mathf.MoveToward(_bodyAirBlend, grounded ? 0f : 1f,
            (float)delta * (grounded ? 8f : 5f));
        _bodyLandingCompression = Mathf.MoveToward(_bodyLandingCompression, 0f, (float)delta * .20f);
        _bodyCrouch = Mathf.MoveToward(_bodyCrouch, IsCrouching ? 1 : 0, (float)delta * 3.8f);
        // Carry the gait dip through a takeoff/stop instead of returning the
        // pelvis to neutral in only a few frames; the planted-foot IK still
        // owns the same reach envelope throughout the slower transition.
        _bodyLocomotionDip = Mathf.MoveToward(_bodyLocomotionDip,
            _bodyTraveling && !IsCrouching ? 1f : 0f, (float)delta * 7f);
        var hipShift = new Vector3(0, -.27f * _bodyCrouch - .15f * _bodyLocomotionDip
                - .018f * _bodyAirBlend - _bodyLandingCompression,
            -.10f * _bodyCrouch);
        var localTravelDirection = BodyDirectionInSkeleton(movement);

        if (grounded)
        {
            foreach (var leg in _bodyLegs)
            {
                if (!_bodyTraveling)
                {
                    // Bring a mid-swing sole to the floor while its rocker
                    // unwinds. Keep the other foot's existing world plant.
                    if (leg.IsSwinging && !leg.IsFootSettling)
                    {
                        _bodyNextSwingLegIndex = 1 - _bodyLegs.IndexOf(leg);
                        BeginBodyFootSettle(leg);
                    }
                    if (!leg.IsFootSettling)
                    {
                        if (leg.LastFootPose is { } last) leg.GaitPlant = last;
                        if (leg.GaitPlant is null)
                            leg.GaitPlant = _bodySkeleton.GlobalTransform
                                * new Transform3D(Basis.Identity, ProjectBodyFoot(leg.AnkleRest.Origin));
                        leg.IsSwinging = false;
                        leg.FootRockRadians = leg.FootLiftMeters = 0;
                        leg.StanceTravelMeters = 0;
                        leg.PlantStartTravel = _bodyGroundTravelMeters;
                    }
                }
                else if (leg.GaitPlant is null && !leg.IsSwinging && !leg.IsFootSettling)
                {
                    leg.GaitPlant = _bodySkeleton.GlobalTransform
                        * new Transform3D(Basis.Identity, ProjectBodyFoot(leg.AnkleRest.Origin));
                    leg.PlantStartTravel = _bodyGroundTravelMeters;
                    leg.StanceTravelMeters = BodyStanceTravel(leg, hipShift, localTravelDirection);
                }
                else if (_bodyTraveling && leg.GaitPlant is not null && leg.StanceTravelMeters <= 0)
                {
                    leg.PlantStartTravel = _bodyGroundTravelMeters;
                    leg.StanceTravelMeters = BodyStanceTravel(leg, hipShift, localTravelDirection);
                }
            }
        }
        else
        {
            foreach (var leg in _bodyLegs)
            {
                leg.GaitPlant = null;
                leg.TurnPlant = null;
                leg.IsSwinging = false;
                leg.IsFootSettling = false;
                leg.StanceTravelMeters = 0;
            }
        }

        UpdateTurnPlants(grounded && !_bodyTraveling && !_bodyLegs.Any(leg => leg.IsFootSettling)
            && _bodyCrouch < .001f && !_modalOpen);
        // Turning the hips over planted feet needs a little knee bend. Keep
        // both anchors within the real leg reach instead of dragging them
        // inward with the final IK clamp as the torso rotates away.
        foreach (var leg in _bodyLegs.Where(leg => leg.TurnPlant is not null))
        {
            var foot = _bodySkeleton.ToLocal(leg.TurnPlant!.Value.Origin);
            var flat = new Vector2(foot.X - leg.ThighRest.Origin.X, foot.Z - leg.ThighRest.Origin.Z);
            var reach = leg.UpperLength + leg.LowerLength - .01f;
            var height = Mathf.Sqrt(Math.Max(0, reach * reach - flat.LengthSquared()));
            hipShift.Y = Math.Min(hipShift.Y, foot.Y + height - leg.ThighRest.Origin.Y);
        }
        _bodySkeleton.SetBonePosePosition(_bodySpine, _bodySpineRest + hipShift);
        SetBodyGlobalPose(_bodyUpperCoat, new(Basis.Identity.Scaled(new Vector3(1, 1f - .60f * _bodyCrouch, 1)),
            _bodyUpperCoatRest.Origin + hipShift));
        if (grounded) UpdateGroundFootCycle(hipShift, localTravelDirection, movement);
        foreach (var leg in _bodyLegs)
        {
            var footBasis = Basis.Identity;
            var foot = leg.AnkleRest.Origin;
            var soleOffset = leg.AnkleRest.AffineInverse() * leg.Sole;
            var turnLift = 0f;
            var projectedByPose = false;
            if (leg.TurnPlant is { } plant)
            {
                if (leg.TurnProgress < 1)
                {
                    leg.TurnProgress = Math.Min(1, leg.TurnProgress + (float)delta / .20f);
                    plant = leg.TurnFrom.InterpolateWith(leg.TurnTarget, Mathf.SmoothStep(0, 1, leg.TurnProgress));
                    leg.TurnPlant = plant;
                    turnLift = Mathf.Sin(leg.TurnProgress * Mathf.Pi) * .045f / _bodySkeleton.GlobalBasis.Scale.Y;
                }
                var localPlant = _bodySkeleton.GlobalTransform.AffineInverse() * plant;
                foot = localPlant.Origin;
                footBasis = localPlant.Basis;
                leg.FootRockRadians = leg.FootLiftMeters = 0;
            }

            if (grounded && leg.TurnPlant is null)
            {
                if (leg.IsFootSettling)
                {
                    leg.SettleProgress = Math.Min(1, leg.SettleProgress + (float)delta / .12f);
                    var eased = Mathf.SmoothStep(0, 1, leg.SettleProgress);
                    var sole = leg.SettleFromSole.Lerp(leg.SettleTargetSole, eased);
                    leg.FootRockRadians = Mathf.Lerp(leg.SettleStartRock, 0, eased);
                    leg.FootLiftMeters = Mathf.Lerp(leg.SettleStartLift, 0, eased);
                    var worldBasis = leg.SettleTargetFoot.Basis
                        * new Basis(Vector3.Right, leg.FootRockRadians);
                    var worldOrigin = sole - worldBasis * soleOffset;
                    var localPose = _bodySkeleton.GlobalTransform.AffineInverse()
                        * new Transform3D(worldBasis, worldOrigin);
                    foot = localPose.Origin;
                    footBasis = localPose.Basis;
                    projectedByPose = true;
                    if (leg.SettleProgress >= 1)
                    {
                        leg.GaitPlant = leg.SettleTargetFoot;
                        leg.IsFootSettling = false;
                        leg.FootRockRadians = leg.FootLiftMeters = 0;
                        leg.PlantStartTravel = _bodyGroundTravelMeters;
                        leg.StanceTravelMeters = 0;
                        RecordBodyFootContact();
                    }
                }
                else if (leg.IsSwinging)
                {
                    var travel = (float)Math.Max(0, _bodyGroundTravelMeters - leg.SwingStartTravel);
                    var progress = Mathf.Clamp(travel / Math.Max(.02f, leg.SwingTravelMeters), 0f, 1f);
                    var eased = 1f - (1f - progress) * (1f - progress);
                    var swingWorld = leg.SwingFrom.InterpolateWith(leg.SwingTarget, eased);
                    var neutralLocal = _bodySkeleton.GlobalTransform.AffineInverse() * swingWorld;
                    if (leg.PreserveSwingStartPose)
                        leg.PreserveSwingStartPose = false;
                    else
                        neutralLocal.Origin = ProjectBodyFoot(neutralLocal.Origin);
                    var liftWave = Mathf.Sin(progress * Mathf.Pi);
                    var rockWave = Mathf.Sin(progress * Mathf.Tau);
                    leg.FootLiftMeters = liftWave * .065f + leg.SwingStartLift
                        * (1f - progress) * (1f - Mathf.Abs(liftWave));
                    leg.FootRockRadians = rockWave * .10f + leg.SwingStartRock
                        * (1f - progress) * (1f - Mathf.Abs(rockWave));
                    var sole = neutralLocal.Origin + neutralLocal.Basis * soleOffset
                        + Vector3.Up * leg.FootLiftMeters;
                    footBasis = neutralLocal.Basis * new Basis(Vector3.Right, leg.FootRockRadians);
                    foot = sole - footBasis * soleOffset;
                    projectedByPose = true;
                }
                else if (leg.GaitPlant is { } gaitPlant)
                {
                    var localPlant = _bodySkeleton.GlobalTransform.AffineInverse() * gaitPlant;
                    foot = localPlant.Origin;
                    footBasis = localPlant.Basis;
                    leg.FootRockRadians = leg.FootLiftMeters = 0;
                }
            }

            if (grounded && !projectedByPose)
                foot = ProjectBodyFoot(foot);
            else if (!grounded)
            {
                leg.FootRockRadians = Mathf.MoveToward(leg.FootRockRadians, 0, (float)delta * 1.5f);
                leg.FootLiftMeters = Mathf.MoveToward(leg.FootLiftMeters, 0, (float)delta * .55f);
                footBasis *= new Basis(Vector3.Right, leg.FootRockRadians);
                var sole = foot + footBasis * soleOffset
                    + Vector3.Up * (.035f * _bodyAirBlend + leg.FootLiftMeters);
                foot = sole - footBasis * soleOffset;
                if (leg.AirPoseBlendActive)
                {
                    if (leg.AirPoseJustStarted)
                        leg.AirPoseJustStarted = false;
                    else
                        leg.AirPoseProgress = Math.Min(1, leg.AirPoseProgress + (float)delta / .12f);
                    var eased = Mathf.SmoothStep(0, 1, leg.AirPoseProgress);
                    var airPose = leg.AirPoseFrom.InterpolateWith(new Transform3D(footBasis, foot), eased);
                    footBasis = airPose.Basis;
                    foot = airPose.Origin;
                    if (leg.AirPoseProgress >= 1) leg.AirPoseBlendActive = false;
                }
            }
            foot.Y += turnLift;
            var hip = leg.ThighRest.Origin + hipShift;
            var offset = foot - hip;
            var reach = Mathf.Clamp(offset.Length(), .05f, leg.UpperLength + leg.LowerLength - .001f);
            var direction = offset.Normalized();
            var along = (leg.UpperLength * leg.UpperLength - leg.LowerLength * leg.LowerLength + reach * reach) / (2 * reach);
            var bend = (Vector3.Back - direction * direction.Dot(Vector3.Back)).Normalized();
            var knee = hip + direction * along + bend * Mathf.Sqrt(Math.Max(0, leg.UpperLength * leg.UpperLength - along * along));
            // Clamp an unreachable foot to the leg length instead of stretching
            // its imported trouser mesh while stepping near a raised edge.
            foot = hip + direction * reach;
            var thighTurn = new Quaternion((leg.KneeRest.Origin - leg.ThighRest.Origin).Normalized(), (knee - hip).Normalized());
            var shinTurn = new Quaternion((leg.AnkleRest.Origin - leg.KneeRest.Origin).Normalized(), (foot - knee).Normalized());
            SetBodyGlobalPose(leg.Thigh, new(new Basis(thighTurn) * leg.ThighRest.Basis, hip));
            SetBodyGlobalPose(leg.Knee, new(new Basis(shinTurn), knee));
            SetBodyGlobalPose(leg.Ankle, new(footBasis, foot));
            leg.LastFootPose = _bodySkeleton.GlobalTransform * new Transform3D(footBasis, foot);
        }
    }
}
