using Godot;

namespace Urman.Godot;

public static partial class VehicleVisualFactory
{
    public const string NivaModelPath = "res://assets/generated/urman_niva.glb";

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
                part.SetSurfaceOverrideMaterial(surface, ModelMaterial(authored));
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

    private static Material ModelMaterial(string authoredName)
    {
        if (ModelMaterials.TryGetValue(authoredName, out var cached)) return cached;
        // The generator authors every finish as "<6 hex>__<surface>"
        // (tools/blender/generate_niva.py); anything else is a hand-renamed slot
        // that would silently take the old 808080 default grey. VIS-105 forbids a
        // default grey on transport, so it is a build-time error instead.
        var parts = authoredName.Split("__", 2);
        if (parts.Length != 2 || parts[1].Length == 0 || !IsHex6(parts[0]))
            throw new InvalidDataException(
                $"Niva finish '{authoredName}' is not authored as <hex6>__<surface>; "
                + "fix the generator material name rather than letting the model take a fallback.");
        var material = ForFinish(parts[0], parts[1]);
        ModelMaterials[authoredName] = material;
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
