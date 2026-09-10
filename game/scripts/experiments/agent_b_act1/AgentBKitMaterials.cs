using Godot;
using System.Collections.Generic;

namespace Urman.Experiments.AgentBAct1;

/// <summary>
/// Shared material rebinding for every authored Agent B kit. Blender exports
/// textureless AB_* materials; Godot presentation swaps them for painterly
/// surfaces, wet/reflective standard materials and warm emissive windows.
/// One map, one cache, every kit consumer delegates here.
/// </summary>
public static class AgentBKitMaterials
{
    private static readonly Dictionary<string, (string Color, string Surface, float Emission)> Map = new()
    {
        ["AB_earth"] = ("4a4136", "earth", 0f),
        ["AB_earth_wet"] = ("39473f", "earth", 0f),
        ["AB_earth_path"] = ("5c5040", "earth", 0f),
        ["AB_earth_zirat_path"] = ("39433b", "earth", 0f),
        ["AB_road_crown"] = ("4e564f", "earth", 0f),
        ["AB_road_rut"] = ("303b35", "earth", 0f),
        ["AB_road_kara"] = ("453c33", "earth", 0f),
        ["AB_water_dark"] = ("202a29", "", 0f),
        ["AB_grass"] = ("5a6248", "grass", 0f),
        ["AB_grass_dry"] = ("75714c", "grass", 0f),
        ["AB_sedge"] = ("55603f", "grass", 0f),
        ["AB_fern"] = ("4c5c3c", "foliage", 0f),
        ["AB_moss"] = ("5c6647", "foliage", 0f),
        ["AB_bark"] = ("5f4f3e", "bark_pine", 0f),
        ["AB_bark_dark"] = ("493c30", "bark_pine", 0f),
        ["AB_bark_birch"] = ("c8c2b2", "bark_birch", 0f),
        ["AB_foliage_birch"] = ("75834e", "leaf_birch", 0f),
        ["AB_foliage_pine"] = ("3c4f3c", "foliage", 0f),
        ["AB_foliage_spruce"] = ("425043", "foliage", 0f),
        ["AB_foliage_shrub"] = ("556440", "foliage", 0f),
        ["AB_foliage_kara"] = ("2a3430", "foliage", 0f),
        ["AB_foliage_kara_deep"] = ("222b28", "foliage", 0f),
        ["AB_plaster"] = ("707065", "plaster", 0f),
        ["AB_plaster_faded"] = ("62675f", "plaster", 0f),
        ["AB_timber"] = ("6b5a45", "wood", 0f),
        ["AB_timber_dark"] = ("4e4133", "wood", 0f),
        ["AB_log_wall"] = ("7a674e", "log_wall", 0f),
        ["AB_fade_paint"] = ("8e7f66", "wood_fence", 0f),
        ["AB_roof_iron"] = ("6f7268", "roof_metal", 0f),
        ["AB_roof_iron_dark"] = ("575a52", "roof_metal", 0f),
        ["AB_roof_shingle"] = ("5d5044", "roof", 0f),
        ["AB_window_warm"] = ("e8b04a", "", 3.0f),
        ["AB_window_cold"] = ("5d6a72", "", 0f),
        ["AB_snow"] = ("eef2f6", "snow_ground", 0f),
        ["AB_foliage_rowan"] = ("8a5334", "rowan_berries", 0f),
        ["AB_foliage_broadleaf"] = ("6d7350", "foliage", 0f),
        ["AB_stone"] = ("87837a", "stone", 0f),
        ["AB_stone_dark"] = ("63605a", "stone", 0f),
        ["AB_sign_blank"] = ("8a7a5f", "wood", 0f)
    };

    private static readonly Dictionary<string, Material> Cache = new();

    public static int ReboundMaterialCount { get; private set; }

    public static Material MaterialFor(string name)
    {
        if (Cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        Material material;
        if (!Map.TryGetValue(name, out var descriptor))
        {
            material = Urman.Godot.PainterlyMaterialLibrary.ForColor("808080");
        }
        else if (descriptor.Emission > 0f || name == "AB_water_dark")
        {
            var albedo = Color.FromHtml(descriptor.Color);
            var standard = new StandardMaterial3D
            {
                AlbedoColor = albedo,
                Roughness = name == "AB_water_dark" ? 0.58f : 0.55f,
                Metallic = 0f,
                MetallicSpecular = name == "AB_water_dark" ? 0.12f : 0.5f,
                EmissionEnabled = descriptor.Emission > 0f
            };
            if (descriptor.Emission > 0f)
            {
                standard.Emission = albedo;
                standard.EmissionEnergyMultiplier = descriptor.Emission;
            }

            material = standard;
        }
        else
        {
            material = Urman.Godot.PainterlyMaterialLibrary.ForColor(descriptor.Color, descriptor.Surface);
        }

        Cache[name] = material;
        return material;
    }

    public static Node3D InstantiateKit(Node host, string kitPath, string nodeName)
    {
        var packed = ResourceLoader.Load<PackedScene>(kitPath)
            ?? throw new System.InvalidOperationException($"Missing Agent B kit: {kitPath}");
        var instance = packed.Instantiate<Node3D>()
            ?? throw new System.InvalidOperationException($"Agent B kit did not instantiate: {kitPath}");
        host.AddChild(instance);
        instance.Name = nodeName;
        RebindMaterials(instance);
        foreach (var collision in EnumerateDescendants<CollisionObject3D>(instance))
        {
            throw new System.InvalidOperationException(
                $"Agent B kit {kitPath} must be presentation-only but contains collision: {collision.Name}");
        }

        return instance;
    }

    public static void RebindMaterials(Node3D root)
    {
        foreach (var meshInstance in EnumerateDescendants<MeshInstance3D>(root))
        {
            var mesh = meshInstance.Mesh;
            if (mesh is null)
            {
                continue;
            }

            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                var material = mesh.SurfaceGetMaterial(surface);
                var name = material?.ResourceName ?? string.Empty;
                if (name.Length == 0 || !name.StartsWith("AB_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                meshInstance.SetSurfaceOverrideMaterial(surface, MaterialFor(name));
                ReboundMaterialCount++;
            }
        }
    }

    private static IEnumerable<T> EnumerateDescendants<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var nested in EnumerateDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
