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
        var capCloth = PainterlyMaterialLibrary.ForColor("3d4c41", "cloth", sheltered: true);
        var shirtCloth = PainterlyMaterialLibrary.ForColor("ded6c2", "cloth", sheltered: true);

        var derived = new List<ArrayMesh>();
        try
        {
            foreach (var lod in new[] { 0, 1 })
            {
                // Sober city-imam suit tones replace the near-military green
                // wool overcoat and the black trousers of the generic kit.
                Piece($"TimurHazrat_Coat_LOD{lod}").MaterialOverride = coatCloth;
                Piece($"TimurHazrat_CoatSkirt_LOD{lod}").MaterialOverride = coatCloth;
                Piece($"TimurHazrat_Trousers_LOD{lod}").MaterialOverride = trouserCloth;
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
                var geometry = MosqueImamCapGeometry(source.GetAabb(), skullCentre.X, skullCentre.Z,
                    rimX, rimZ, rimY, topY);
                var shaped = DeriveMosqueImamCap(source, geometry);
                derived.Add(shaped);
                hat.Mesh = shaped;
                hat.MaterialOverride = capCloth;
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
        float RimX, float RimZ, float SourceRim, float RimY, float TopY);

    private static ImamCapGeometry MosqueImamCapGeometry(Aabb source, float centreX, float centreZ,
        float rimX, float rimZ, float rimY, float topY)
    {
        if (source.Size.Y is < .08f or > .15f || source.Size.X is < .20f or > .40f || source.Size.Z is < .20f or > .40f)
            throw new InvalidOperationException("Timur's authored karakul profile changed; the kalyapush derivative needs review.");
        var axis = source.GetCenter();
        var sourceRim = Mathf.Max(source.Size.X, source.Size.Z) * .5f;
        if (sourceRim is < .12f or > .22f)
            throw new InvalidOperationException("Timur's authored karakul rim changed; the kalyapush derivative needs review.");
        if (!(rimX > 0f && rimZ > 0f && rimX < sourceRim && rimZ < sourceRim && topY - rimY >= .07f))
            throw new InvalidOperationException("The kalyapush target profile is invalid.");
        return new(source, axis.X, axis.Z, centreX, centreZ, rimX, rimZ, sourceRim, rimY, topY);
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
                for (var index = 0; hasNormals && index < triangles.Length; index += 3)
                {
                    var a = triangles[index]; var b = triangles[index + 1]; var c = triangles[index + 2];
                    var oldCross = (before[b] - before[a]).Cross(before[c] - before[a]);
                    var reference = oldNormals[a] + oldNormals[b] + oldNormals[c];
                    var orientation = oldCross.Dot(reference);
                    if (Math.Abs(orientation) < 1e-12f) throw new InvalidOperationException("Unoriented authored kalyapush face.");
                    var cross = (after[b] - after[a]).Cross(after[c] - after[a]) * Math.Sign(orientation);
                    if (cross.LengthSquared() < 1e-16f) throw new InvalidOperationException("Collapsed kalyapush face.");
                    normals[a] += cross; normals[b] += cross; normals[c] += cross;
                }
                for (var vertex = 0; hasNormals && vertex < normals.Length; vertex++)
                {
                    if (normals[vertex].LengthSquared() < 1e-16f) throw new InvalidOperationException("Unused kalyapush vertex.");
                    normals[vertex] = normals[vertex].Normalized();
                }
                arrays[(int)Mesh.ArrayType.Vertex] = after;
                if (hasNormals) arrays[(int)Mesh.ArrayType.Normal] = normals;
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
                // Skin bindings, colors, UVs and the imported internal surface
                // LODs stay exactly as authored; only positions and the
                // dependent normal/tangent frames move.
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
