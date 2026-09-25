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
                var arrays = source.Mesh.SurfaceGetArrays(surface);
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
        var parts = authoredName.Split("__", 2);
        var color = parts.Length == 2 && parts[0].Length == 6 ? parts[0] : "808080";
        var surface = parts.Length == 2 ? parts[1] : "metal";
        Material material = surface switch
        {
            "glass" => new StandardMaterial3D
            {
                AlbedoColor = new(.48f, .58f, .55f, .22f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = .18f, MetallicSpecular = .55f, CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            // Chrome reads as bright, slightly reflective trim against the painterly body.
            "chrome" => new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml(color), Metallic = .85f, Roughness = .28f, MetallicSpecular = .7f
            },
            // Lamp lenses and the radio's LCD glow faintly so they read as glass/light, not paint.
            "lamp" => new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml(color), Roughness = .2f, EmissionEnabled = true,
                Emission = Color.FromHtml(color), EmissionEnergyMultiplier = .15f
            },
            "lcd" => new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml(color), Roughness = .3f, EmissionEnabled = true,
                Emission = new Color(.18f, .26f, .12f), EmissionEnergyMultiplier = .6f
            },
            // Factory metallic enamel under a lacquer coat: the flake gives the
            // panels a soft sheen, the clear coat the sharp sky highlight.
            // The vertex colour is the road grime baked by the generator.
            "paint" => new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml(color), Metallic = .62f, Roughness = .34f, MetallicSpecular = .6f,
                ClearcoatEnabled = true, Clearcoat = .9f, ClearcoatRoughness = .12f, VertexColorUseAsAlbedo = true
            },
            "plastic" => Grain(color, 420f, .05f, .25f, .74f, .35f, vertexColor: true),
            "snow" => new StandardMaterial3D { AlbedoColor = Color.FromHtml(color), Roughness = .88f, MetallicSpecular = .25f },
            "sheepskin" => Grain(color, 55f, .22f, 1.4f, 1f, .15f, fuzz: 1f),
            "wool" => Grain(color, 160f, .12f, .8f, .98f, .15f, fuzz: .6f),
            "gold" => new StandardMaterial3D { AlbedoColor = Color.FromHtml(color), Metallic = 1f, Roughness = .3f },
            "enamel" => new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml(color), Roughness = .2f, ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = .05f
            },
            "wood_polished" => new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml(color), Roughness = .35f, ClearcoatEnabled = true, Clearcoat = .6f
            },
            "leather" => Grain(color, 380f, .08f, .45f, .6f, .4f),
            "carpet" => Grain(color, 650f, .14f, .9f, .97f, .15f, fuzz: .3f),
            "headliner" => Grain(color, 900f, .04f, .5f, .95f, .2f),
            "cloth" => Grain(color, 700f, .1f, .6f, .96f, .15f),
            "vinyl" => Grain(color, 260f, .06f, .45f, .78f, .3f),
            "rubber" => Grain(color, 500f, .06f, .3f, .9f, .25f),
            _ => PainterlyMaterialLibrary.ForColor(color, surface, sheltered: true)
        };
        ModelMaterials[authoredName] = material;
        return material;
    }
}
