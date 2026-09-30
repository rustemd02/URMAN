using Godot;

namespace Urman.Godot;

/// <summary>Surface-specific physical response, using inspected ImageGen maps.
/// Unavailable dedicated maps stay explicit in the production passports.</summary>
public static class RuralPropMaterials
{
    private static readonly Dictionary<string,StandardMaterial3D> Cache=new();
    public static StandardMaterial3D Surface(string kind,string tint="ffffff")
    {
        var key=kind+":"+tint;if(Cache.TryGetValue(key,out var material))return material;
        var (file,tile,rough,metal)=kind switch
        {
            "wood" => ("urman_w08_v01_basecolor.png",.75f,.45f,0f),
            "plywood" => ("realism_20260929/birch_plywood_varnished_v1_basecolor.png",.5f,.48f,0f),
            "steel" => ("realism_20260929/painted_steel_enamel_v1_basecolor.png",.5f,.45f,0f),
            "laminate" => ("realism_20260929/school_laminate_beige_source_v1_basecolor.png",1f,.4f,0f),
            "cloth" => ("urman_t01_v02_basecolor.png",.5f,.97f,0f),
            "upholstery" => ("realism_20260929/burgundy_stage_velvet_v1_basecolor.png",.5f,.97f,0f),
            "velvet" => ("realism_20260929/burgundy_stage_velvet_v1_basecolor.png",.5f,.97f,0f),
            "curtain" => ("urman_t04_v01_basecolor.png",.5f,.97f,0f),
            "plastic" => ("urman_m05_v01_basecolor.png",.5f,.43f,0f),
            "rubber" => ("",.35f,.91f,0f),
            "ceramic" => ("realism_20260929/glazed_ceramic_cream_source_v1_basecolor.png",.5f,.28f,0f),
            "earthenware" => ("realism_20260929/unglazed_earthenware_source_v1_basecolor.png",.5f,.9f,0f),
            "metal" => ("realism_20260929/galvanized_zinc_source_v1_basecolor.png",.5f,.58f,1f),
            "brass" => ("",.5f,.32f,1f),
            "concrete" => ("realism_20260929/public_concrete_source_v1_basecolor.png",1f,.9f,0f),
            _ => ("urman_w08_v01_basecolor.png",.75f,.65f,0f)
        };
        var path="res://assets/textures/"+(file.Contains('/') ? file : "painterly/"+file);
        material=new StandardMaterial3D{ResourceName="Rural_"+kind,AlbedoColor=Color.FromHtml(tint),
            Roughness=rough,Metallic=metal,MetallicSpecular=.5f,Uv1Scale=new(1f/tile,1f/tile,1f),
            CullMode=kind is "cloth" or "curtain" or "velvet" ? BaseMaterial3D.CullModeEnum.Disabled:BaseMaterial3D.CullModeEnum.Back,
            RimEnabled=kind is "velvet" or "upholstery", Rim=.18f, RimTint=.45f};
        if(file.Length>0 && ResourceLoader.Exists(path))material.AlbedoTexture=ResourceLoader.Load<Texture2D>(path);
        material.SetMeta("sourceTexture",file.Length>0 ? path : "missing-dedicated-map");material.SetMeta("surfaceTileMetres",tile);
        material.SetMeta("dedicatedTexturePending",material.AlbedoTexture is null);
        Cache[key]=material;return material;
    }
}
