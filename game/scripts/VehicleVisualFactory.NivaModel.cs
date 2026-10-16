using Godot;

namespace Urman.Godot;

public static partial class VehicleVisualFactory
{
    public const string NivaModelPath = "res://assets/generated/urman_niva.glb";
    private const string NivaTyreRubberTexturePath = "res://assets/textures/realism_20260929/moulded_rubber_black_v1_basecolor.png";
    private const float NivaTyreRubberRepeatMetres = 0.35f;
    private const string NivaInteriorVinylTexturePath = "res://assets/textures/painterly/urman_m09_v01_basecolor.png";
    private const float NivaInteriorVinylRepeatMetres = 0.5f;
    private static readonly Vector3 NivaInteriorVinylReferenceLinear = new(0.07446924f, 0.04984906f, 0.03598655f);

    // Retained like the character kit: every Niva build instantiates this one
    // scene, and a collected wrapper between loads can swap a dead GC handle.
    private static PackedScene? _nivaModel;
    private static readonly Dictionary<string, Material> ModelMaterials = new(StringComparer.Ordinal);

    /// <summary>Test-only: release the retained Niva model for shutdown leak checks.</summary>
    public static void ClearCacheForHeadlessTests()
    {
        _nivaModel = null;
        ModelMaterials.Clear();
        // VIS-105 glass/snow finishes live in the same retained-material cache.
        FinishCache.Clear();
    }

    /// <summary>
    /// One named presentation mesh from the Blender-authored Niva (NivaBody,
    /// NivaWheel or NivaRadio), re-materialled by the cabin's own rules. The
    /// mesh has no collision; the vehicle's hull and wheel shapes stay authoritative.
    /// </summary>
    private static MeshInstance3D NivaModelPart(Node3D parent, string partName, string instanceName)
    {
        var model = _nivaModel is not null && GodotObject.IsInstanceValid(_nivaModel)
            ? _nivaModel
            : _nivaModel = ResourceLoader.Load<PackedScene>(NivaModelPath)
                ?? throw new InvalidOperationException("Niva model is missing: " + NivaModelPath);
        var scene = model.Instantiate<Node3D>();
        try
        {
            var source = scene.FindChild(partName, true, false) as MeshInstance3D
                ?? throw new InvalidOperationException($"Niva model has no {partName} mesh.");
            var part = new MeshInstance3D { Name = instanceName, Mesh = source.Mesh, Transform = source.Transform };
            // Same batching contract as the procedural parts: the authored count
            // (Blender extras) must equal what the import actually carries.
            var authoredCorners = AuthoredCorners(source);
            var importedCorners = 0;
            for (var surface = 0; surface < source.Mesh.GetSurfaceCount(); surface++)
            {
                using var arrays = source.Mesh.SurfaceGetArrays(surface);
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                importedCorners += indices.Length > 0 ? indices.Length : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
            }
            if (authoredCorners != importedCorners)
                throw new InvalidOperationException($"{partName} lost triangles on import: authored {authoredCorners}, imported {importedCorners}.");
            source.Mesh.SetMeta("expectedTriangleCorners", authoredCorners);
            source.Mesh.SetMeta("manualTriangleCorners", authoredCorners);
            for (var surface = 0; surface < source.Mesh.GetSurfaceCount(); surface++)
            {
                var authored = source.Mesh.SurfaceGetMaterial(surface)?.ResourceName ?? string.Empty;
                part.SetSurfaceOverrideMaterial(surface, ModelMaterial(
                    authored, partName == "NivaWheel", partName == "NivaInterior"));
            }
            part.SetMeta("assetSource", NivaModelPath);
            part.SetMeta("collisionAuthority", "none; VehicleController hull and wheel shapes");
            parent.AddChild(part);
            return part;
        }
        finally
        {
            scene.Free();
        }
    }

    private static int AuthoredCorners(Node source)
    {
        var extras = source.HasMeta("extras") ? source.GetMeta("extras").AsGodotDictionary() : null;
        if (extras is null || !extras.ContainsKey("urman_triangle_corners"))
            throw new InvalidOperationException($"{source.Name} has no authored triangle count (glTF extras).");
        return extras["urman_triangle_corners"].AsInt32();
    }

    private static Material ModelMaterial(string authoredName, bool isNivaWheel = false, bool isNivaInterior = false)
    {
        // The generator authors every finish as "<6 hex>__<surface>"
        // (tools/blender/generate_niva.py); anything else is a hand-renamed slot
        // that would silently take the old 808080 default grey. VIS-105 forbids a
        // default grey on transport, so it is a build-time error instead.
        var parts = authoredName.Split("__", 2);
        if (parts.Length != 2 || parts[1].Length == 0 || !IsHex6(parts[0]))
            throw new InvalidDataException(
                $"Niva finish '{authoredName}' is not authored as <hex6>__<surface>; "
                + "fix the generator material name rather than letting the model take a fallback.");
        var isNivaTyreRubber = isNivaWheel && StringComparer.Ordinal.Equals(parts[1], "rubber");
        var isNivaInteriorVinyl = isNivaInterior
            && StringComparer.Ordinal.Equals(parts[0], "302f2b")
            && StringComparer.Ordinal.Equals(parts[1], "vinyl");
        // Only the four imported tyre surfaces get this variant. The source
        // vehicle_rubber material stays cached for body, cabin and pedal parts.
        var cacheKey = isNivaTyreRubber
            ? authoredName + "|NivaWheelTyre"
            : isNivaInteriorVinyl ? authoredName + "|NivaInteriorVinyl" : authoredName;
        if (ModelMaterials.TryGetValue(cacheKey, out var cached)) return cached;
        var material = ForFinish(parts[0], parts[1]);
        if (isNivaTyreRubber) material = NivaTyreRubberMaterial(material);
        else if (isNivaInteriorVinyl) material = NivaInteriorVinylMaterial(material);
        ModelMaterials[cacheKey] = material;
        return material;
    }

    private static Material NivaInteriorVinylMaterial(Material vinyl)
    {
        // Optional M09 stays out of headless paths and falls back to the existing
        // procedural seat finish if either the map or its import is unavailable.
        if (PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests
            || vinyl is not ShaderMaterial source
            || !ResourceLoader.Exists(NivaInteriorVinylTexturePath)
            || ResourceLoader.Load<Texture2D>(NivaInteriorVinylTexturePath) is not { } texture)
            return vinyl;

        // Keep the half-metre projection mesh-local, independent of imported UVs.
        var material = (ShaderMaterial)source.Duplicate();
        material.SetShaderParameter("albedo_texture", texture);
        material.SetShaderParameter("use_albedo_texture", true);
        material.SetShaderParameter("albedo_reference_linear", NivaInteriorVinylReferenceLinear);
        material.SetShaderParameter("albedo_texture_scale", new Vector2(
            1f / NivaInteriorVinylRepeatMetres, 1f / NivaInteriorVinylRepeatMetres));
        return material;
    }

    private static Material NivaTyreRubberMaterial(Material vehicleRubber)
    {
        // Missing/failed optional texture loading leaves the authored finish in
        // place, so an absent M07 asset never prevents the vehicle from loading.
        if (PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests
            || vehicleRubber is not ShaderMaterial source
            || !ResourceLoader.Exists(NivaTyreRubberTexturePath)
            || ResourceLoader.Load<Texture2D>(NivaTyreRubberTexturePath) is not { } texture)
            return vehicleRubber;

        // The imported tyre primitive has no UVs. Keep the base vehicle finish
        // shared and give only this wheel-scoped duplicate a mesh-local triplanar
        // projection, one repeat per authored 0.35 m tile.
        var material = (ShaderMaterial)source.Duplicate();
        material.SetShaderParameter("albedo_texture", texture);
        material.SetShaderParameter("has_albedo_texture", true);
        material.SetShaderParameter("authored_uv_texture", false);
        material.SetShaderParameter("local_floor_texture", true);
        var repeatsPerMetre = 1f / NivaTyreRubberRepeatMetres;
        material.SetShaderParameter("texture_scale", new Vector2(repeatsPerMetre, repeatsPerMetre));
        return material;
    }

    /// <summary>
    /// Six hex digits, the exact form tools/blender/generate_niva.py writes into a
    /// material name. Spelled out instead of a framework helper because this target
    /// exposes no Uri.IsHexDigits, and a silently accepted name is the default-grey
    /// panel VIS-105 forbids on transport.
    /// </summary>
    private static bool IsHex6(string value)
        => value.Length == 6 && value.All(c => "0123456789abcdefABCDEF".IndexOf(c) >= 0);
}
