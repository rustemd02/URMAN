using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Presentation-only redressing of the mosque imam for the village's
    // January 2026 interior. The actor keeps its authored rig, skin, LOD
    // policy, ground anchor, interaction target and RuntimeBridge ownership;
    // only the visible garment palette, the cap silhouette and one shirt
    // collar built from primitives change. The existing indoor footwear
    // derivative in Act1ConnectedWorld.MosqueClothing.cs remains the only
    // owner of the cloth soles.
    //
    // Lore anchors: a 35-year-old, tidy, Kazan-educated imam and the moral
    // anchor of the village (docs/urman_knowledge_base/00_codex_context.md:1744,
    // docs/urman_knowledge_base/characters.md:136); a skullcap is acceptable
    // only as an ordinary garment, never a carnival marker
    // (docs/urman_knowledge_base/design_style.md:139).
    private void ConfigureMosqueImamDress(Node3D timur)
    {
        if (timur.GetMeta("characterId").AsString() != "timur_hazrat")
            throw new InvalidOperationException("The mosque imam dress requires the existing timur_hazrat actor.");
        if (timur.HasMeta("mosqueImamDressVersion"))
            throw new InvalidOperationException("The mosque imam dress is applied once per built actor.");

        var meshes = timur.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().ToArray();
        MeshInstance3D Piece(string name) => meshes.SingleOrDefault(item => item.Name == name)
            ?? throw new InvalidOperationException($"The mosque imam dress needs the authored kit piece {name}.");

        // The authored hair is rigidly bound to the head and shares the cap's
        // bind frame, so its measured envelope is the real skull measure that
        // the oversized karakul (0.338 m across, sunk 22 mm below the crown)
        // failed to use. The head is deeper than wide, so the kalyapush is an
        // ellipse around the measured skull centre, not the authored forward
        // cap axis.
        var hair = Piece("TimurHazrat_Hair_LOD0").Mesh?.GetAabb()
            ?? throw new InvalidOperationException("Timur's authored hair bounds are missing.");
        var skullCentre = hair.GetCenter();
        var skullHalfWidth = hair.Size.X * .5f;
        var skullHalfDepth = hair.Size.Z * .5f;
        if (skullHalfWidth is < .06f or > .11f || skullHalfDepth is < .07f or > .14f)
            throw new InvalidOperationException("Timur's authored head measure changed; the kalyapush fit needs review.");
        var rimX = skullHalfWidth + .018f;
        var rimZ = skullHalfDepth + .022f;
        var rimY = hair.End.Y - .09f;
        var topY = hair.End.Y + .016f;

        var coatCloth = PainterlyMaterialLibrary.ForColor("363d47", "cloth", sheltered: true);
        var trouserCloth = PainterlyMaterialLibrary.ForColor("2d3136", "cloth", sheltered: true);
        var capSourceLod0 = Piece("TimurHazrat_Hat_LOD0").Mesh as ArrayMesh
            ?? throw new InvalidOperationException("Timur's authored cap LOD0 is not the expected static ArrayMesh.");
        if (!GeneratedCharacterKitDressing.HasMetricClothUv(Piece("TimurHazrat_Hat_LOD0")))
            throw new InvalidOperationException("Timur's cap requires metric rest UVs before its moving-cloth shader.");
        var capBaseGeometry = MosqueImamCapGeometry(capSourceLod0.GetAabb(), skullCentre.X, skullCentre.Z,
            rimX, rimZ, rimY, topY, uvScale: 1f);
        // Fitting shrinks the authored karakul's surface. Derive one area-based
        // repeat correction from LOD0 and share it with LOD1 to avoid a mip/LOD jump.
        var capUvScale = MosqueImamCapUvScale(capSourceLod0, capBaseGeometry);
        var shirtCloth = PainterlyMaterialLibrary.ForLocalWoodPiece("ded6c2", Vector3.Zero, "cloth",
            sheltered: true, preserveSurfaceDefaults: true);

        var derived = new List<ArrayMesh>();
        try
        {
            foreach (var lod in new[] { 0, 1 })
            {
                // Sober city-imam suit tones replace the near-military green
                // wool overcoat and the black trousers of the generic kit.
                // VIS-104: skinned garment pieces take UV-bound cloth once the kit
                // carries metric UVs; the derived cap still needs its own rest UVs.
                foreach (var garment in new[] { "Coat", "CoatSkirt" })
                {
                    var piece = Piece($"TimurHazrat_{garment}_LOD{lod}");
                    piece.MaterialOverride = GeneratedCharacterKitDressing.HasMetricClothUv(piece)
                        ? GeneratedCharacterKitDressing.ClothFor(piece, "363d47") : coatCloth;
                }
                var trousers = Piece($"TimurHazrat_Trousers_LOD{lod}");
                trousers.MaterialOverride = GeneratedCharacterKitDressing.HasMetricClothUv(trousers)
                    ? GeneratedCharacterKitDressing.ClothFor(trousers, "2d3136") : trouserCloth;
                // The wrapped sash reads as a generic fantasy robe belt and
                // disappears under the buttoned garment.
                var sash = Piece($"TimurHazrat_Sash_LOD{lod}");
                sash.Visible = false;
                sash.SetMeta("mosqueDressHidden", "sash removed for the sober imam garment");

                // The karakul cylinder is re-derived in place: it stays rigidly
                // bound to the head bone with both LODs and its imported
                // surface LODs, but becomes a fitted, sober kalyapush.
                var hat = Piece($"TimurHazrat_Hat_LOD{lod}");
                if (hat.Mesh is not ArrayMesh source || source.GetBlendShapeCount() != 0)
                    throw new InvalidOperationException($"Timur's authored cap LOD{lod} is not the expected static ArrayMesh.");
                if (!GeneratedCharacterKitDressing.HasMetricClothUv(hat))
                    throw new InvalidOperationException($"Timur's cap LOD{lod} is missing metric rest UVs.");
                var geometry = MosqueImamCapGeometry(source.GetAabb(), skullCentre.X, skullCentre.Z,
                    rimX, rimZ, rimY, topY, capUvScale);
                var shaped = DeriveMosqueImamCap(source, geometry);
                derived.Add(shaped);
                hat.Mesh = shaped;
                hat.MaterialOverride = GeneratedCharacterKitDressing.ClothFor(hat, "3d4c41");
                hat.SetMeta("mosqueDress", "kalyapush: authored karakul re-derived to a fitted tatar skullcap");
            }

            var skeleton = timur.FindChildren("*", nameof(Skeleton3D), true, false)
                .OfType<Skeleton3D>().SingleOrDefault()
                ?? throw new InvalidOperationException("Timur's authored skeleton is missing.");
            var headBone = skeleton.FindBone("Head");
            var chestBone = skeleton.FindBone("spine_03");
            if (headBone < 0 || chestBone < 0)
                throw new InvalidOperationException("Timur's authored head/chest bones changed; the collar anchor needs review.");
            var headRest = skeleton.GetBoneGlobalRest(headBone);
            var chestRest = skeleton.GetBoneGlobalRest(chestBone);
            // The measured kit neck column runs from the head rest origin
            // upward; +3.5 cm places a low band inside the coat's neckline
            // scoop (rim radius ~0.09-0.11 m) and outside the neck skin
            // (~0.061-0.071 m). The collar follows the chest bone, not the head.
            var collarAt = headRest.Origin + Vector3.Up * .035f;
            var attachment = new BoneAttachment3D { Name = "MosqueImamCollarAttachment", BoneName = "spine_03" };
            skeleton.AddChild(attachment);
            var collar = new Node3D
            {
                Name = "MosqueImamShirtCollar",
                Transform = chestRest.AffineInverse() * new Transform3D(Basis.Identity, collarAt)
            };
            attachment.AddChild(collar);
            var neckRadius = skullHalfWidth * .72f;
            collar.AddChild(new MeshInstance3D
            {
                Name = "MosqueImamShirtCollarBand",
                Mesh = new CylinderMesh
                {
                    TopRadius = neckRadius + .021f, BottomRadius = neckRadius + .007f,
                    Height = .034f, RadialSegments = 16, Rings = 1, CapTop = false, CapBottom = false
                },
                MaterialOverride = shirtCloth
            });
            collar.SetMeta("garmentRole", "off-white shirt collar; the garment's single light accent");
            collar.SetMeta("clearanceOwner", "chest bone; coat neckline and neck skin remain the authored kit geometry");
        }
        catch
        {
            foreach (var mesh in derived) mesh.Dispose();
            throw;
        }

        timur.SetMeta("mosqueImamDressVersion", 1);
        timur.SetMeta("mosqueImamDress", "sober charcoal джилян; dark trousers; deep-green kalyapush; off-white shirt collar; indoor cloth socks from the existing footwear lane");
        timur.SetMeta("mosqueImamDressPieces", "Coat=363d47 cloth, CoatSkirt=363d47, Trousers=2d3136, Sash=hidden, Hat=kalyapush 3d4c41, Collar=ded6c2 primitive, Boots=existing mosque socks");
        timur.SetMeta("mosqueImamDressLore", "00_codex_context.md:1744; characters.md:136; design_style.md:139");
        timur.SetMeta("mosqueImamDressReview", "human eye review required at the ordinary camera; no runtime dress test exists");
    }

    private readonly record struct ImamCapGeometry(Aabb Source, float AxisX, float AxisZ, float CentreX, float CentreZ,
        float RimX, float RimZ, float SourceRim, float RimY, float TopY, float UvScale);

    private static ImamCapGeometry MosqueImamCapGeometry(Aabb source, float centreX, float centreZ,
        float rimX, float rimZ, float rimY, float topY, float uvScale)
    {
        if (source.Size.Y is < .08f or > .15f || source.Size.X is < .20f or > .40f || source.Size.Z is < .20f or > .40f)
            throw new InvalidOperationException("Timur's authored karakul profile changed; the kalyapush derivative needs review.");
        var axis = source.GetCenter();
        var sourceRim = Mathf.Max(source.Size.X, source.Size.Z) * .5f;
        if (sourceRim is < .12f or > .22f)
            throw new InvalidOperationException("Timur's authored karakul rim changed; the kalyapush derivative needs review.");
        if (!(rimX > 0f && rimZ > 0f && rimX < sourceRim && rimZ < sourceRim && topY - rimY >= .07f)
            || !float.IsFinite(uvScale) || uvScale <= 0f)
            throw new InvalidOperationException("The kalyapush target profile or metric UV scale is invalid.");
        return new(source, axis.X, axis.Z, centreX, centreZ, rimX, rimZ, sourceRim, rimY, topY, uvScale);
    }

    private static float MosqueImamCapUvScale(ArrayMesh source, ImamCapGeometry geometry)
    {
        if (source.GetSurfaceCount() == 0)
            throw new InvalidOperationException("Timur's fitted cap has no visible surfaces to map.");
        double fittedArea = 0d, uvArea = 0d;
        var triangleCount = 0;
        for (var surface = 0; surface < source.GetSurfaceCount(); surface++)
        {
            if (source.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
                throw new InvalidOperationException("The fitted cap UV scale requires triangular visible surfaces.");
            using var arrays = source.SurfaceGetArrays(surface);
            var positions = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var uvValue = arrays[(int)Mesh.ArrayType.TexUV];
            if (uvValue.VariantType != Variant.Type.PackedVector2Array)
                throw new InvalidOperationException("Timur's visible cap surface has no metric UV channel.");
            var uv = uvValue.AsVector2Array();
            if (positions.Length == 0 || uv.Length != positions.Length
                || positions.Any(point => !point.IsFinite()) || uv.Any(point => !point.IsFinite()))
                throw new InvalidOperationException("Timur's visible cap positions or UVs are empty, mismatched or non-finite.");
            var indexValue = arrays[(int)Mesh.ArrayType.Index];
            var indices = indexValue.VariantType == Variant.Type.PackedInt32Array
                ? indexValue.AsInt32Array() : Array.Empty<int>();
            var triangles = indices.Length == 0 ? Enumerable.Range(0, positions.Length).ToArray() : indices;
            if (triangles.Length == 0 || triangles.Length % 3 != 0
                || triangles.Any(index => index < 0 || index >= positions.Length))
                throw new InvalidOperationException("Timur's visible cap has invalid triangle indices for UV scaling.");
            for (var index = 0; index < triangles.Length; index += 3)
            {
                var a = triangles[index]; var b = triangles[index + 1]; var c = triangles[index + 2];
                var ua = uv[a]; var ub = uv[b]; var uc = uv[c];
                var uvCross = ((double)ub.X - ua.X) * ((double)uc.Y - ua.Y)
                    - ((double)ub.Y - ua.Y) * ((double)uc.X - ua.X);
                var triangleUvArea = Math.Abs(uvCross) * .5d;
                if (!double.IsFinite(triangleUvArea) || triangleUvArea <= 1e-12d)
                    throw new InvalidOperationException("Timur's visible cap contains a zero-area or non-finite UV triangle.");
                var pa = MapMosqueImamCapVertex(positions[a], geometry);
                var pb = MapMosqueImamCapVertex(positions[b], geometry);
                var pc = MapMosqueImamCapVertex(positions[c], geometry);
                var mappedCross = (pb - pa).Cross(pc - pa);
                var mappedAreaSquared = mappedCross.LengthSquared();
                if (!float.IsFinite(mappedAreaSquared) || mappedAreaSquared <= 1e-16f)
                    throw new InvalidOperationException("Timur's fitted cap contains a collapsed or non-finite triangle.");
                uvArea += triangleUvArea;
                fittedArea += Math.Sqrt(mappedAreaSquared) * .5d;
                triangleCount++;
            }
        }
        if (triangleCount == 0 || !double.IsFinite(uvArea) || !double.IsFinite(fittedArea)
            || uvArea <= 1e-12d || fittedArea <= 1e-12d)
            throw new InvalidOperationException("Timur's cap cannot derive a finite metric UV correction from LOD0.");
        var scale = Math.Sqrt(fittedArea / uvArea);
        if (!double.IsFinite(scale) || scale <= 0d || scale > float.MaxValue)
            throw new InvalidOperationException("Timur's fitted-cap metric UV correction is invalid.");
        return (float)scale;
    }

    // Radial shrink toward the measured skull centre plus a vertical lift: the
    // authored straight fur cylinder becomes a fitted, slightly tapered
    // skullcap whose ellipse follows the deeper-than-wide head.
    private static Vector3 MapMosqueImamCapVertex(Vector3 point, in ImamCapGeometry geometry)
    {
        var t = Mathf.Clamp((point.Y - geometry.Source.Position.Y) / geometry.Source.Size.Y, 0f, 1f);
        var dx = point.X - geometry.AxisX;
        var dz = point.Z - geometry.AxisZ;
        if (dx * dx + dz * dz < .000016f)
            return new(geometry.CentreX + dx, t >= .5f ? geometry.TopY : geometry.RimY, geometry.CentreZ + dz);
        var taper = Mathf.Lerp(1f, .80f, t);
        var scaleX = geometry.RimX * taper / geometry.SourceRim;
        var scaleZ = geometry.RimZ * taper / geometry.SourceRim;
        return new(geometry.CentreX + dx * scaleX, Mathf.Lerp(geometry.RimY, geometry.TopY, t),
            geometry.CentreZ + dz * scaleZ);
    }

    private static Vector3 MapMosqueImamCapDirection(Vector3 point, Vector3 direction, in ImamCapGeometry geometry)
    {
        var t = Mathf.Clamp((point.Y - geometry.Source.Position.Y) / geometry.Source.Size.Y, 0f, 1f);
        var taper = Mathf.Lerp(1f, .80f, t);
        var vertical = (geometry.TopY - geometry.RimY) / geometry.Source.Size.Y;
        return new(direction.X * geometry.RimX * taper / geometry.SourceRim, direction.Y * vertical,
            direction.Z * geometry.RimZ * taper / geometry.SourceRim);
    }

    private static ArrayMesh DeriveMosqueImamCap(ArrayMesh source, ImamCapGeometry geometry, bool shadow = false)
    {
        var result = new ArrayMesh { ResourceName = source.ResourceName + "_MosqueKalyapush", CustomAabb = source.CustomAabb };
        try
        {
            for (var surface = 0; surface < source.GetSurfaceCount(); surface++)
            {
                using var arrays = source.SurfaceGetArrays(surface);
                var before = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var hasNormals = arrays[(int)Mesh.ArrayType.Normal].VariantType != Variant.Type.Nil;
                var oldNormals = hasNormals ? arrays[(int)Mesh.ArrayType.Normal].AsVector3Array() : Array.Empty<Vector3>();
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                if (source.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles || before.Length == 0
                    || (hasNormals && oldNormals.Length != before.Length) || (!hasNormals && !shadow))
                    throw new InvalidOperationException("The authored kalyapush derivative requires triangular surfaces and the visible mesh's normals.");
                var after = before.Select(point => MapMosqueImamCapVertex(point, geometry)).ToArray();
                var triangles = indices.Length == 0 ? Enumerable.Range(0, before.Length).ToArray() : indices;
                if (triangles.Length % 3 != 0 || triangles.Any(index => index < 0 || index >= before.Length))
                    throw new InvalidOperationException("Invalid authored kalyapush indices.");
                var normals = new Vector3[before.Length];
                // glTF splits vertices at UV seams. Sum face normals by the original
                // position+normal so those duplicate seam vertices keep their shared
                // smooth shading; distinct authored hard normals stay separate.
                var normalSums = new Dictionary<(Vector3 Position, Vector3 Normal), Vector3>();
                for (var index = 0; hasNormals && index < triangles.Length; index += 3)
                {
                    var a = triangles[index]; var b = triangles[index + 1]; var c = triangles[index + 2];
                    var oldCross = (before[b] - before[a]).Cross(before[c] - before[a]);
                    var reference = oldNormals[a] + oldNormals[b] + oldNormals[c];
                    var orientation = oldCross.Dot(reference);
                    if (Math.Abs(orientation) < 1e-12f) throw new InvalidOperationException("Unoriented authored kalyapush face.");
                    var cross = (after[b] - after[a]).Cross(after[c] - after[a]) * Math.Sign(orientation);
                    if (cross.LengthSquared() < 1e-16f) throw new InvalidOperationException("Collapsed kalyapush face.");
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var vertex = triangles[index + corner];
                        var key = (before[vertex], oldNormals[vertex]);
                        normalSums[key] = normalSums.GetValueOrDefault(key) + cross;
                    }
                }
                for (var vertex = 0; hasNormals && vertex < normals.Length; vertex++)
                {
                    if (!normalSums.TryGetValue((before[vertex], oldNormals[vertex]), out var summed)
                        || summed.LengthSquared() < 1e-16f)
                        throw new InvalidOperationException("Unused kalyapush vertex or invalid seam normal group.");
                    normals[vertex] = summed.Normalized();
                }
                arrays[(int)Mesh.ArrayType.Vertex] = after;
                if (hasNormals) arrays[(int)Mesh.ArrayType.Normal] = normals;
                if (!shadow)
                {
                    var uvValue = arrays[(int)Mesh.ArrayType.TexUV];
                    if (uvValue.VariantType != Variant.Type.PackedVector2Array)
                        throw new InvalidOperationException("Timur's visible fitted cap lost its metric UV channel.");
                    var uv = uvValue.AsVector2Array();
                    if (uv.Length != before.Length || uv.Any(value => !value.IsFinite()))
                        throw new InvalidOperationException("Timur's visible fitted-cap UV channel is mismatched or non-finite.");
                    for (var index = 0; index < triangles.Length; index += 3)
                    {
                        var a = uv[triangles[index]]; var b = uv[triangles[index + 1]]; var c = uv[triangles[index + 2]];
                        var uvCross = ((double)b.X - a.X) * ((double)c.Y - a.Y)
                            - ((double)b.Y - a.Y) * ((double)c.X - a.X);
                        if (!double.IsFinite(uvCross) || Math.Abs(uvCross) <= 2e-12d)
                            throw new InvalidOperationException("Timur's visible fitted cap contains a zero-area or non-finite UV triangle.");
                    }
                    for (var vertex = 0; vertex < uv.Length; vertex++)
                    {
                        uv[vertex] *= geometry.UvScale;
                        if (!uv[vertex].IsFinite()) throw new InvalidOperationException("Timur's fitted-cap UV is non-finite after metric scaling.");
                    }
                    arrays[(int)Mesh.ArrayType.TexUV] = uv;
                }
                if (arrays[(int)Mesh.ArrayType.Tangent].VariantType != Variant.Type.Nil)
                {
                    var tangents = arrays[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
                    if (!hasNormals || tangents.Length != before.Length * 4)
                        throw new InvalidOperationException("Invalid imported kalyapush tangent channel.");
                    for (var vertex = 0; vertex < before.Length; vertex++)
                    {
                        var offset = vertex * 4;
                        var tangent = MapMosqueImamCapDirection(before[vertex],
                            new(tangents[offset], tangents[offset + 1], tangents[offset + 2]), geometry);
                        tangent -= normals[vertex] * tangent.Dot(normals[vertex]);
                        if (tangent.LengthSquared() < 1e-12f)
                            tangent = normals[vertex].Cross(Math.Abs(normals[vertex].Y) < .9f ? Vector3.Up : Vector3.Right);
                        tangent = tangent.Normalized();
                        tangents[offset] = tangent.X; tangents[offset + 1] = tangent.Y; tangents[offset + 2] = tangent.Z;
                    }
                    arrays[(int)Mesh.ArrayType.Tangent] = tangents;
                }
                // Preserve skin bindings, colors, materials and imported internal
                // LOD indices. Positions and derived normal/tangent frames follow
                // the fitted cap; visible metric UVs receive one shared LOD0 scale.
                using var lods = MosqueSockSurfaceLods(source, surface);
                var flags = source.SurfaceGetFormat(surface) &
                    (Mesh.ArrayFormat.FlagUse8BoneWeights | Mesh.ArrayFormat.FlagUseDynamicUpdate);
                result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, lods: lods, flags: flags);
                result.SurfaceSetMaterial(surface, source.SurfaceGetMaterial(surface));
                result.SurfaceSetName(surface, source.SurfaceGetName(surface));
            }
            if (!shadow && source.ShadowMesh is { } originalShadow)
                result.ShadowMesh = DeriveMosqueImamCap(originalShadow, geometry, shadow: true);
            return result;
        }
        catch { result.Dispose(); throw; }
    }
}
