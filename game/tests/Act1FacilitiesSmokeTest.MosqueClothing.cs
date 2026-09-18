using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot.Tests;

public partial class Act1FacilitiesSmokeTest
{
    private async Task CheckMosqueTimurFootwear(Node3D timur)
    {
        var records = _world.MosqueFootwear;
        var failures = new List<string>();
        var surfaces = new List<object>();
        var views = new List<object>();
        var state = _bridge.SelectRuntimeState().GetRawText();
        var props = _bridge.SelectWorldProps().GetRawText();
        var arrivalActorPose = timur.GlobalTransform;
        var arrivalFeet = _player.GlobalPosition;
        var arrivalTurns = timur.GetMeta("conversationTurnCount", 0L).AsInt64();
        // The normal conversation owner turns Timur towards an approaching
        // player. Let that real tween finish before freezing the comparison
        // baseline; this test neither rotates the actor nor disables its owner.
        var previousPose = arrivalActorPose;
        var settleStarted = Time.GetTicksMsec();
        var settleFirstProcessFrame = Engine.GetProcessFrames();
        var settleFrames = 0;
        var stableFrames = 0;
        var facingError = float.PositiveInfinity;
        while (settleFrames < 120 && Time.GetTicksMsec() - settleStarted < 2500)
        {
            await Frames(1);
            // Conversation tweens advance on process frames. Several physics
            // catch-up ticks in one render iteration cannot establish stability.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            settleFrames++;
            var currentPose = timur.GlobalTransform;
            stableFrames = currentPose.IsEqualApprox(previousPose) ? stableFrames + 1 : 0;
            previousPose = currentPose;
            var direction = timur.GetParent<Node3D>().ToLocal(_player.GlobalPosition) - timur.Position;
            direction.Y = 0;
            facingError = Math.Abs(Mathf.AngleDifference(timur.Rotation.Y, Mathf.Atan2(direction.X, direction.Z)));
            if (stableFrames >= 3 && facingError <= Mathf.DegToRad(18f)) break;
        }
        var actorPose = timur.GlobalTransform;
        var feet = _player.GlobalPosition;
        var settleMilliseconds = Time.GetTicksMsec() - settleStarted;
        var settleLastProcessFrame = Engine.GetProcessFrames();
        var turns = timur.GetMeta("conversationTurnCount", 0L).AsInt64();
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var look = _player.CapturePortableTransform();
        var clip = timur.GetMeta("animationClip").AsString();
        var status = timur.GetMeta("animationStatus").AsString();
        var animator = timur.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>()
            .Single(player => player.HasAnimation("TimurHazrat_Idle"));
        var animationPosition = animator.CurrentAnimationPosition;
        var wasPlaying = animator.IsPlaying();
        var visibility = records.Select(item => (item.Node, item.Node.Visible, item.Node.VisibilityRangeBegin,
            item.Node.VisibilityRangeEnd, item.Node.VisibilityRangeBeginMargin, item.Node.VisibilityRangeEndMargin)).ToArray();
        void Require(bool ok, string message) { if (!ok) failures.Add(message); }
        object V(Vector3 point) => new { x = point.X, y = point.Y, z = point.Z };
        object? poseEvidence = null;
        try
        {
            Require(stableFrames >= 3 && facingError <= Mathf.DegToRad(18f)
                && actorPose.Origin.DistanceTo(arrivalActorPose.Origin) < .00001f
                && feet.DistanceTo(arrivalFeet) < .015f,
                "The existing conversation turn settles at the reached player without moving either ground anchor.");
            Require(records.Count == 4 && records.Select(item => item.Node.Name.ToString()).Distinct().Count() == 4,
                "Both existing Timur feet have both authored LOD derivatives.");
            foreach (var item in records)
            {
                Require(item.Node.Mesh == item.Published && item.Source != item.Published && item.Node.Skin == item.Skin
                    && item.Node.Skeleton == item.Skeleton, item.Node.Name + ": same mesh node, original skin and skeleton.");
                var groundedPose = item.LocalPose;
                if (item.Node.GetParent() == timur) groundedPose.Origin -= Vector3.Up * .010f;
                Require(item.Node.Transform.IsEqualApprox(groundedPose), item.Node.Name + ": only the existing GroundSoles projection changes its pose.");
                Require(item.Node.VisibilityRangeBegin == item.RangeBegin && item.Node.VisibilityRangeEnd == item.RangeEnd
                    && item.Node.VisibilityRangeBeginMargin == item.BeginMargin && item.Node.VisibilityRangeEndMargin == item.EndMargin
                    && item.Node.VisibilityRangeFadeMode == item.Fade, item.Node.Name + ": authored LOD ranges remain intact.");
                Require(item.Source.GetSurfaceCount() == item.Published.GetSurfaceCount(), item.Node.Name + ": surface count.");
                for (var surface = 0; surface < item.Source.GetSurfaceCount(); surface++)
                {
                    var source = item.Source.SurfaceGetArrays(surface);
                    var current = item.Published.SurfaceGetArrays(surface);
                    Require(GD.VarToBytes(source).SequenceEqual(item.SourceArrays[surface]), item.Node.Name + ": shared source arrays remain byte-identical.");
                    var attributes = true;
                    for (var channel = 0; channel < source.Count; channel++)
                        if (channel != (int)Mesh.ArrayType.Vertex && channel != (int)Mesh.ArrayType.Normal && channel != (int)Mesh.ArrayType.Tangent)
                            attributes &= GD.VarToBytes(source[channel]).SequenceEqual(GD.VarToBytes(current[channel]));
                    Require(attributes, item.Node.Name + ": every other attribute and the triangle indices survive.");
                    using var oldLods = Act1ConnectedWorld.MosqueSockSurfaceLods(item.Source, surface);
                    using var newLods = Act1ConnectedWorld.MosqueSockSurfaceLods(item.Published, surface);
                    var lodsSame = oldLods.Count == newLods.Count && oldLods.All(pair => newLods.TryGetValue(pair.Key, out var value)
                        && pair.Value.AsInt32Array().SequenceEqual(value.AsInt32Array()));
                    Require(lodsSame, item.Node.Name + ": internal surface LOD indices and thresholds survive.");
                    var before = source[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    var after = current[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    var normals = current[(int)Mesh.ArrayType.Normal].AsVector3Array();
                    var indices = current[(int)Mesh.ArrayType.Index].AsInt32Array();
                    if (indices.Length == 0) indices = Enumerable.Range(0, after.Length).ToArray();
                    Require(before.Length == after.Length, item.Node.Name + ": vertex identity.");
                    var cuffError = before.Select((point, index) => point.Y >= item.CuffY ? point.DistanceTo(after[index]) : 0).Max();
                    var oldSole = before.Min(point => point.Y); var newSole = after.Min(point => point.Y);
                    var edgeVertices = before.Select((point, index) => (point, index))
                        .Where(pair => pair.point.Y - oldSole > .017f && pair.point.Y - oldSole < .023f).ToArray();
                    var edgeHeight = edgeVertices.Length > 0 ? edgeVertices.Max(pair => after[pair.index].Y - newSole) : -1;
                    var lower = before.Select((point, index) => (point, index)).Where(pair => pair.point.Y < oldSole + .023f).ToArray();
                    var lowerWidth = lower.Max(pair => after[pair.index].X) - lower.Min(pair => after[pair.index].X);
                    Require(cuffError < .00001f && Math.Abs(oldSole - newSole) < .00001f,
                        item.Node.Name + ": exact original cuff and sole plane.");
                    // LOD1 decimation has slanted edge vertices; report its actual
                    // edge separately and require the un-decimated 2.5 mm profile.
                    if (item.Node.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal))
                        Require(edgeHeight is > .002f and < .0031f, item.Node.Name + ": textile edge replaces the 20 mm hard sole.");
                    Require(lowerWidth is > .10f and < .122f, item.Node.Name + ": a foot-width cloth silhouette replaces the boot outsole.");
                    Require(normals.Length == after.Length && normals.All(normal => normal.IsFinite() && Math.Abs(normal.Length() - 1) < .001f),
                        item.Node.Name + ": finite normalized surface normals.");
                    if (source[(int)Mesh.ArrayType.Tangent].VariantType != Variant.Type.Nil)
                    {
                        var oldTangents = source[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
                        var newTangents = current[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
                        var tangentValid = oldTangents.Length == newTangents.Length && newTangents.Length == after.Length * 4;
                        for (var vertex = 0; tangentValid && vertex < after.Length; vertex++)
                        {
                            var offset = vertex * 4;
                            var tangent = new Vector3(newTangents[offset], newTangents[offset + 1], newTangents[offset + 2]);
                            tangentValid &= tangent.IsFinite() && Math.Abs(tangent.Length() - 1) < .001f
                                && Math.Abs(tangent.Dot(normals[vertex])) < .001f && newTangents[offset + 3] == oldTangents[offset + 3];
                        }
                        Require(tangentValid, item.Node.Name + ": imported tangents retain handedness and match the new surface normals.");
                    }
                    var oldNormals = source[(int)Mesh.ArrayType.Normal].AsVector3Array();
                    var wrongFacing = 0;
                    for (var index = 0; index < indices.Length; index += 3)
                    {
                        var a = indices[index]; var b = indices[index + 1]; var c = indices[index + 2];
                        var oldDot = (before[b] - before[a]).Cross(before[c] - before[a]).Dot(oldNormals[a] + oldNormals[b] + oldNormals[c]);
                        var newDot = (after[b] - after[a]).Cross(after[c] - after[a]).Dot(normals[a] + normals[b] + normals[c]);
                        if (oldDot * newDot <= 0) wrongFacing++;
                    }
                    Require(wrongFacing == 0, item.Node.Name + ": original outside winding matches the new normals.");
                    surfaces.Add(new { mesh = item.Node.GetPath().ToString(), surface, vertices = after.Length, indices = indices.Length,
                        attributes, lodsSame, cuffError, oldSole, newSole, edgeHeight, lowerWidth, wrongFacing,
                        originalMaterial = item.Source.SurfaceGetMaterial(surface)?.ResourceName.ToString(),
                        presentationMaterial = item.Node.MaterialOverride?.GetClass().ToString() });
                }
                Require((item.Source.ShadowMesh is null) == (item.Published.ShadowMesh is null), item.Node.Name + ": imported shadow-mesh policy survives.");
                if (item.Published.ShadowMesh is { } shadow)
                {
                    var visiblePositions = Enumerable.Range(0, item.Published.GetSurfaceCount()).SelectMany(surface =>
                        item.Published.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();
                    var shadowPositions = Enumerable.Range(0, shadow.GetSurfaceCount()).SelectMany(surface =>
                        shadow.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();
                    Require(shadow != item.Source.ShadowMesh && shadowPositions.Length > 0
                        && shadowPositions.All(point => visiblePositions.Any(other => point.DistanceTo(other) < .00001f)),
                        item.Node.Name + ": derived shadow uses the same new surface positions, never the old boot outline.");
                }
            }
            foreach (var suffix in new[] { "Idle", "Tension" })
            {
                Require(GeneratedCharacterKitDressing.PlayClip(timur, suffix), "Timur's existing " + suffix + " clip plays.");
                await Frames(18);
                foreach (var lod in new[] { 0, 1 })
                {
                    foreach (var item in records)
                    {
                        item.Node.Visible = item.Node.Name.ToString().EndsWith("_LOD" + lod, StringComparison.Ordinal);
                        item.Node.VisibilityRangeBegin = 0; item.Node.VisibilityRangeEnd = 0;
                        item.Node.VisibilityRangeBeginMargin = 0; item.Node.VisibilityRangeEndMargin = 0;
                    }
                    var name = $"09e_timur_sock_{suffix.ToLowerInvariant()}_lod{lod}";
                    await Capture(name, timur.GlobalPosition + Vector3.Up * .12f);
                    await Frames(2);
                    foreach (var item in records.Where(item => item.Node.Visible))
                    {
                        using var posed = item.Node.BakeMeshFromCurrentSkeletonPose();
                        var points = Enumerable.Range(0, posed.GetSurfaceCount()).SelectMany(surface =>
                            posed.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                            .Select(point => item.Node.GlobalTransform * point).ToArray();
                        Require(points.Length > 0 && points.All(point => !_camera.IsPositionBehind(point)), name + ": feet are in front of the ordinary camera.");
                        var pixels = points.Select(_camera.UnprojectPosition).ToArray();
                        var screen = GetViewport().GetVisibleRect();
                        Require(pixels.All(screen.HasPoint), name + ": both complete feet fit the recorded frame.");
                        var min = points.Min(point => point.Y);
                        var center = points.Aggregate(Vector3.Zero, (sum, point) => sum + point) / points.Length;
                        using var ray = PhysicsRayQueryParameters3D.Create(center + Vector3.Up * .25f,
                            center - Vector3.Up * .40f, 3, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
                        var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                        float? support = hit.Count > 0 ? hit["position"].AsVector3().Y : null;
                        var carpet = _mosque.GetNode<MeshInstance3D>("MosquePrayerCarpet");
                        var carpetTop = carpet.ToGlobal(carpet.Mesh.GetAabb().GetCenter() + Vector3.Up * carpet.Mesh.GetAabb().Size.Y * .5f).Y;
                        Require(support is { } floor && float.IsFinite(floor) && Math.Abs(min - floor) < .035f,
                            name + ": original posed sole remains at the actual hall support.");
                        Require(hit.Count > 0 && hit["collider"].AsGodotObject() == _mosque.GetNode<StaticBody3D>("MosquePrayerCarpetBody")
                            && Math.Abs(min - carpetTop) < .0025f && support is { } carpetSupport
                            && Math.Abs(carpetSupport - carpetTop) < .0001f,
                            name + ": the cloth soles and exact visible carpet top share the same physical support.");
                        views.Add(new { name, mesh = item.Node.GetPath().ToString(), clip = suffix,
                            actualAnimation = animator.CurrentAnimation.ToString(), animationPosition = animator.CurrentAnimationPosition,
                            imageWritten = File.Exists(Path.Combine(Output, name + ".png")), posedMeasurementAfterCaptureFrames = 2,
                            forcedLodAtOrdinaryDistance = lod,
                            camera = _camera.GetCameraTransform().ToString(), feet = V(_player.GlobalPosition),
                            actorPose = timur.GlobalTransform.ToString(),
                            conversationTurnCount = timur.GetMeta("conversationTurnCount", 0L).AsInt64(),
                            posedSoleY = min, supportY = support, gap = min - support,
                            visualCarpetTopY = carpetTop, visualCarpetGap = min - carpetTop,
                            supportOwner = hit.Count > 0 ? ((Node)hit["collider"].AsGodotObject()).GetPath().ToString() : "missing",
                            pixelsMin = new { x = pixels.Min(pixel => pixel.X), y = pixels.Min(pixel => pixel.Y) },
                            pixelsMax = new { x = pixels.Max(pixel => pixel.X), y = pixels.Max(pixel => pixel.Y) },
                            artisticReadabilityRequiresImageReview = true });
                    }
                }
            }
        }
        catch (Exception error)
        {
            failures.Add(error.ToString());
            throw;
        }
        finally
        {
            foreach (var saved in visibility)
            {
                saved.Node.Visible = saved.Visible; saved.Node.VisibilityRangeBegin = saved.VisibilityRangeBegin;
                saved.Node.VisibilityRangeEnd = saved.VisibilityRangeEnd; saved.Node.VisibilityRangeBeginMargin = saved.VisibilityRangeBeginMargin;
                saved.Node.VisibilityRangeEndMargin = saved.VisibilityRangeEndMargin;
            }
            animator.Play(clip); animator.Seek(animationPosition, update: true);
            if (!wasPlaying) animator.Pause();
            timur.SetMeta("animationClip", clip); timur.SetMeta("animationStatus", status);
            _player.ApplySmokeLook((float)look.RotationDegrees.X, (float)look.RotationDegrees.Y);
            var actorUnchanged = timur.GlobalTransform.IsEqualApprox(actorPose);
            var playerDistance = _player.GlobalPosition.DistanceTo(feet);
            poseEvidence = new
            {
                arrivalActorPose = arrivalActorPose.ToString(), arrivalFeet = V(arrivalFeet), arrivalTurns,
                settleFrames, stableFrames, facingError, settleMilliseconds, settleFirstProcessFrame, settleLastProcessFrame,
                actorBefore = actorPose.ToString(), actorAfter = timur.GlobalTransform.ToString(), actorUnchanged,
                actorAnchorDistance = timur.GlobalPosition.DistanceTo(actorPose.Origin),
                actorYawDifference = Mathf.AngleDifference(actorPose.Basis.GetEuler().Y, timur.GlobalRotation.Y),
                turnsBefore = turns, turnsAfter = timur.GetMeta("conversationTurnCount", 0L).AsInt64(),
                turnReason = timur.GetMeta("conversationTurnReason", "").AsString(),
                facing = timur.GetMeta("conversationFacing", "").AsString(),
                playerBefore = V(feet), playerAfter = V(_player.GlobalPosition), playerDistance,
                velocity = V(_player.Velocity), onFloor = _player.IsOnFloor(),
                revisionBefore = revision, revisionAfter = _player.PresentationTransformRevision,
                recoveriesBefore = recoveries, recoveriesAfter = _player.FallRecoveries,
                clampsBefore = clamps, clampsAfter = _player.EdgeClamps,
                actorOwnerPaused = false, actorPoseWrittenByTest = false
            };
            Require(actorUnchanged && playerDistance < .015f,
                "Footwear inspection preserves actor anchor and the physically reached player position.");
            Require(_player.PresentationTransformRevision == revision && _player.FallRecoveries == recoveries
                && _player.EdgeClamps == clamps,
                "Footwear inspection uses no player pose reset, recovery or boundary correction.");
            Require(_bridge.SelectRuntimeState().GetRawText() == state && _bridge.SelectWorldProps().GetRawText() == props,
                "Clothing/animation/LOD inspection grants no knowledge and changes no world props.");
            Directory.CreateDirectory(Output);
            using var receipt = new FileStream(Path.Combine(Output, "mosque-footwear-geometry.json"), FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(receipt, new { passed = failures.Count == 0, surfaces, views, poseEvidence, failures,
                scope = "four local meshes, current rig, ordinary camera; forced LOD comparison is diagnostic", artAndCulturalAcceptance = "not-established-by-test" },
                new JsonSerializerOptions { WriteIndented = true });
        }
        Check(failures.Count == 0, "Timur's indoor cloth feet retain skinning, cuffs, ground support and both LODs: " + string.Join("; ", failures));
    }
}
