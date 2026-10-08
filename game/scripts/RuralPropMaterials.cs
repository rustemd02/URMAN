using Godot;

namespace Urman.Godot;

/// <summary>Surface-specific physical response, using inspected ImageGen maps.
/// Unavailable dedicated maps stay explicit in the production passports.</summary>
public static class RuralPropMaterials
{
    private static readonly Dictionary<string, StandardMaterial3D> Cache = new();

    /// <summary>One authored finish. TileMetres is the physical width of one tile, so
    /// a family can never inherit an accidental repeat; Roughness/Metallic/MetallicSpecular
    /// are the response, and the drawn slots stay optional: a normal or ORM file that has
    /// not been produced yet is simply not bound (VIS-092/095).</summary>
    private sealed record Finish(
        string File, float TileMetres, float Roughness, float Metallic, float MetallicSpecular,
        float NormalScale, string? NormalFile, string? OrmFile)
    {
        public Finish(string file, float tileMetres, float roughness, float metallic, float metallicSpecular)
            : this(file, tileMetres, roughness, metallic, metallicSpecular, 0f, null, null) { }

        /// <summary>True when the finish is a coating over metal. A coating must never
        /// carry metallic &gt; 0 as a shine trick (VIS-095), so the contract gate reads
        /// this instead of guessing from the family name.</summary>
        public bool IsCoating => Metallic == 0f;
    }

    private static readonly Finish Unknown = new("urman_w08_v01_basecolor.png", .75f, .65f, 0f, .20f);

    private static readonly Dictionary<string, Finish> Finishes = new(StringComparer.Ordinal)
    {
        // Oiled interior wood: a satin lacquer, not a wet plastic highlight.
        ["wood"] = new("urman_w08_v01_basecolor.png", .75f, .52f, 0f, .24f),
        ["plywood"] = new("realism_20260929/birch_plywood_varnished_v1_basecolor.png", .5f, .50f, 0f, .26f),
        // Painted sheet steel: a coating. Metallic 0 by contract; age lives in the
        // roughness distribution, and the drawn normal is the rolled-steel hide.
        ["steel"] = new("realism_20260929/painted_steel_enamel_v1_basecolor.png", .5f, .55f, 0f, .28f,
            .28f, "realism_20260929/painted_steel_v1_normal.png", "realism_20260929/painted_steel_v1_orm.png"),
        // Vitreous enamel over steel: the brightest dielectric in the village. Chips are
        // a roughness/metallic event inside the ORM map, not a second material.
        ["enamel"] = new("realism_20260929/painted_steel_enamel_v1_basecolor.png", .5f, .30f, 0f, .35f,
            .10f, "realism_20260929/enamel_v1_normal.png", "realism_20260929/enamel_chips_v1_orm.png"),
        // Exposed galvanised sheet: physically metal, deliberately not chrome.
        ["zinc"] = new("realism_20260929/galvanized_zinc_source_v1_basecolor.png", .5f, .50f, 1f, .58f,
            .34f, "realism_20260929/metal_galvanized_v1_normal.png", "realism_20260929/metal_galvanized_v1_orm.png"),
        ["laminate"] = new("realism_20260929/school_laminate_beige_source_v1_basecolor.png", 1f, .40f, 0f, .30f),
        ["cloth"] = new("urman_t01_v02_basecolor.png", .5f, .97f, 0f, .05f),
        ["upholstery"] = new("realism_20260929/burgundy_stage_velvet_v1_basecolor.png", .5f, .97f, 0f, .05f),
        ["velvet"] = new("realism_20260929/burgundy_stage_velvet_v1_basecolor.png", .5f, .97f, 0f, .05f),
        ["curtain"] = new("urman_t04_v01_basecolor.png", .5f, .97f, 0f, .05f),
        ["plastic"] = new("urman_m05_v01_basecolor.png", .5f, .46f, 0f, .26f),
        ["rubber"] = new("realism_20260929/moulded_rubber_black_v1_basecolor.png", .35f, .91f, 0f, .10f),
        ["ceramic"] = new("realism_20260929/glazed_ceramic_cream_source_v1_basecolor.png", .5f, .26f, 0f, .34f),
        ["earthenware"] = new("realism_20260929/unglazed_earthenware_source_v1_basecolor.png", .5f, .90f, 0f, .08f),
        // The legacy `metal` name is what this project uses for galvanised roofs,
        // buckets and stove parts: exposed sheet metal, so metallic is allowed, with a
        // rough winter-aged highlight instead of a polished one.
        ["metal"] = new("realism_20260929/galvanized_zinc_source_v1_basecolor.png", .5f, .52f, 1f, .55f,
            .30f, "realism_20260929/metal_galvanized_v1_normal.png", "realism_20260929/metal_galvanized_v1_orm.png"),
        ["brass"] = new("realism_20260929/samovar_brass_v1_basecolor.png", .5f, .30f, 1f, .62f,
            .14f, "realism_20260929/metal_brass_v1_normal.png", null),
        ["concrete"] = new("realism_20260929/public_concrete_source_v1_basecolor.png", 1f, .90f, 0f, .08f,
            .26f, "realism_20260929/concrete_v1_normal.png", "realism_20260929/concrete_v1_orm.png")
    };

    public static StandardMaterial3D Surface(string kind, string tint = "ffffff")
    {
        var key = kind + ":" + tint;
        if (Cache.TryGetValue(key, out var saved)) return saved;
        var finish = Finishes.TryGetValue(kind, out var authored) ? authored : Unknown;
        var path = "res://assets/textures/" + (finish.File.Contains('/') ? finish.File : "painterly/" + finish.File);
        var material = new StandardMaterial3D
        {
            ResourceName = "Rural_" + kind,
            AlbedoColor = Color.FromHtml(tint),
            Roughness = finish.Roughness,
            Metallic = finish.Metallic,
            MetallicSpecular = finish.MetallicSpecular,
            // The tile stays metric so a repeated finish cannot read at 10 m as one
            // shared noise field; 1/TileMetres is Godot's repeat convention.
            Uv1Scale = new(1f / finish.TileMetres, 1f / finish.TileMetres, 1f),
            CullMode = kind is "cloth" or "curtain" or "velvet"
                ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Back,
            RimEnabled = kind is "velvet" or "upholstery",
            Rim = .18f,
            RimTint = .45f
        };
        if (ResourceLoader.Exists(path)) material.AlbedoTexture = ResourceLoader.Load<Texture2D>(path);
        // Optional response slots, bound only when the delivered file exists. Until then
        // the finish keeps its authored flat roughness — the honest state, never a black
        // channel. Requests: docs/urman_knowledge_base/art/asset_requests/MAT.md M-02..M-04.
        if (finish.NormalFile is { Length: > 0 } normalFile)
        {
            var normalPath = "res://assets/textures/" + normalFile;
            if (ResourceLoader.Exists(normalPath)
                && ResourceLoader.Load<Texture2D>(normalPath) is { } normal)
            {
                material.NormalTexture = normal;
                material.NormalScale = finish.NormalScale;
            }
        }
        if (finish.OrmFile is { Length: > 0 } ormFile)
        {
            var ormPath = "res://assets/textures/" + ormFile;
            if (ResourceLoader.Exists(ormPath)
                && ResourceLoader.Load<Texture2D>(ormPath) is { } orm)
            {
                // ORM convention: G = roughness, B = metallic. A chip, a scratch band or
                // a rare rust streak is therefore one map, not a copied material. The engine
                // has no single combined ORM slot: the same texture is bound twice and every
                // response reads its own channel of it. Each channel stays multiplied by the
                // authored scalar above, so the map modulates the finish instead of replacing
                // it — and a coating keeps Metallic 0 by contract (VIS-095), which means its
                // age reads through the roughness channel only.
                material.RoughnessTexture = orm;
                material.RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Green;
                material.MetallicTexture = orm;
                material.MetallicTextureChannel = BaseMaterial3D.TextureChannel.Blue;
            }
        }
        material.SetMeta("sourceTexture", material.AlbedoTexture is null ? "missing-dedicated-map" : path);
        material.SetMeta("surfaceTileMetres", finish.TileMetres);
        material.SetMeta("dedicatedTexturePending", material.AlbedoTexture is null);
        material.SetMeta("responseNormalPending", material.NormalTexture is null);
        material.SetMeta("responseOrmPending", material.RoughnessTexture is null);
        material.SetMeta("metallicPolicy", finish.IsCoating ? "coating-metallic-0" : "exposed-metal");
        Cache[key] = material;
        return material;
    }
}
