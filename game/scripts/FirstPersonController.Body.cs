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
    private Vector3 _bodyPreviousFeet;
    private float _bodyGait;
    private float _bodyStride;
    private float _bodyCrouch;
    private global::Godot.Collections.Array<Rid> _bodyFloorExclude = null!;

    private sealed record BodyLeg(int Thigh, int Knee, int Ankle, Transform3D ThighRest,
        Transform3D KneeRest, Transform3D AnkleRest, Vector3 Sole, float UpperLength, float LowerLength, float Phase);

    internal int VisibleBodyMeshCount { get; private set; }
    internal Vector3[] VisibleBodySoles => _bodyLegs.Select(leg =>
        _bodySkeleton.GlobalTransform * _bodySkeleton.GetBoneGlobalPose(leg.Ankle)
        * (leg.AnkleRest.AffineInverse() * leg.Sole)).ToArray();

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
        var vertices = boot.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array()
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
            thighRest.Origin.DistanceTo(kneeRest.Origin), kneeRest.Origin.DistanceTo(ankleRest.Origin), side == "Left" ? 0 : .5f));
    }

    private void PrepareBodyCoat(MeshInstance3D coat)
    {
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
            var arrays = coat.Mesh.SurfaceGetArrays(surface);
            var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var bones = new int[points.Length * 4];
            var weights = new float[points.Length * 4];
            for (var point = 0; point < points.Length; point++)
            {
                var upper = Mathf.SmoothStep(.90f, 1.10f, (toSkeleton * points[point]).Y);
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
        var arrays = trouser.Mesh.SurfaceGetArrays(0);
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

        using var builder = new SurfaceTool();
        builder.Begin(Mesh.PrimitiveType.Triangles);
        builder.SetSmoothGroup(0);
        void Emit(int ring, int point)
        {
            var vertex = rings[ring][point % sides];
            var lower = 1f - Mathf.SmoothStep(.42f, .53f, vertex.Y);
            builder.SetUV(new(point / (float)sides, 1f - vertex.Y));
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

    private void ResetVisibleBodyMotion()
    {
        _bodyPreviousFeet = GlobalPosition;
        _bodyGait = _bodyStride = 0;
    }

    /// <summary>Metres of travel per full two-foot stride cycle. Half of it is
    /// the plant interval, so the visible feet and the footstep audio land on
    /// the same footsteps instead of sliding against each other.</summary>
    internal const float GaitCycleMeters = 1.1f;

    private void SetBodyGlobalPose(int bone, Transform3D pose)
    {
        var parent = _bodySkeleton.GetBoneParent(bone);
        var parentPose = parent >= 0 ? _bodySkeleton.GetBoneGlobalPose(parent) : Transform3D.Identity;
        _bodySkeleton.SetBonePose(bone, parentPose.AffineInverse() * pose);
    }

    private void UpdateVisibleBody(double delta, bool moving)
    {
        if (_visibleBody is null || VehicleControlled) return;
        var movement = new Vector2(GlobalPosition.X - _bodyPreviousFeet.X, GlobalPosition.Z - _bodyPreviousFeet.Z);
        _bodyPreviousFeet = GlobalPosition;
        var distance = movement.Length();
        if (distance > .6f) { movement = Vector2.Zero; distance = 0; }
        // One foot plants every half cycle. Only travel along the facing may
        // advance the cycle: strafing then shuffles instead of moonwalking
        // forward, and backpedalling walks the cycle backwards.
        var forward = new Vector2(-GlobalTransform.Basis.Z.X, -GlobalTransform.Basis.Z.Z);
        var forwardLength = forward.Length();
        var forwardShare = distance > .000001f && forwardLength > .000001f
            ? Mathf.Clamp(movement.Dot(forward) / (forwardLength * distance), -1f, 1f) : 0f;
        var cycleShare = forwardShare >= 0f
            ? Mathf.Lerp(.35f, 1f, forwardShare)
            : Mathf.Lerp(.35f, -1f, -forwardShare);
        var planarSpeed = (float)delta > 0f ? distance / (float)delta : 0f;
        var grounded = IsOnFloor() && !IsClimbingLadder;
        if (grounded) _bodyGait = ((_bodyGait + distance * cycleShare / GaitCycleMeters) % 1f + 1f) % 1f;
        var walking = moving && distance > .0005f && grounded;
        var modeSpeed = IsCrouching ? WalkSpeed * .58f : IsSprinting ? WalkSpeed * SprintMultiplier : WalkSpeed;
        var strideTarget = walking
            ? (IsSprinting ? .24f : .17f)
                * Mathf.Clamp(planarSpeed / modeSpeed, .4f, 1f)
                * Mathf.Clamp(.4f + .6f * Mathf.Abs(forwardShare), .4f, 1f)
            : 0f;
        _bodyStride = Mathf.MoveToward(_bodyStride, strideTarget, (float)delta * 1.2f);
        _bodyCrouch = Mathf.MoveToward(_bodyCrouch, IsCrouching ? 1 : 0, (float)delta * 9f);
        var hipShift = new Vector3(0, -.27f * _bodyCrouch, -.10f * _bodyCrouch);
        _bodySkeleton.SetBonePosePosition(_bodySpine, _bodySpineRest + hipShift);
        SetBodyGlobalPose(_bodyUpperCoat, new(Basis.Identity.Scaled(new Vector3(1, 1f - .60f * _bodyCrouch, 1)),
            _bodyUpperCoatRest.Origin + hipShift));
        foreach (var leg in _bodyLegs)
        {
            var phase = (_bodyGait + leg.Phase) % 1f;
            var wave = Mathf.Sin(phase * Mathf.Tau);
            var foot = leg.AnkleRest.Origin + new Vector3(0,
                Math.Max(0, wave) * _bodyStride * .40f, -Mathf.Cos(phase * Mathf.Tau) * _bodyStride);
            if (IsOnFloor() && !IsClimbingLadder)
            {
                var at = _bodySkeleton.ToGlobal(foot);
                using var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * .22f,
                    at - Vector3.Up * .38f, CollisionMask, _bodyFloorExclude);
                var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
                if (hit.Count > 0 && hit["normal"].AsVector3().Y > .70f)
                    foot.Y = _bodySkeleton.ToLocal(hit["position"].AsVector3()).Y + .095f
                        + Math.Max(0, wave) * _bodyStride * .40f;
            }
            else foot.Y += .06f;
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
            SetBodyGlobalPose(leg.Ankle, new(Basis.Identity, foot));
        }
    }
}
