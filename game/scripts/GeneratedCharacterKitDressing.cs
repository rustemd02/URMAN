using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only bridge from the Blender character kit with CC0 head derivatives.
/// One authored character prefix is selected per NPC placement; the adapter
/// never creates physics bodies or owns dialogue/progression state.
/// </summary>
public static class GeneratedCharacterKitDressing
{
    public const string ScenePath = "res://assets/generated/urman_character_kit.glb";

    /// <summary>
    /// Human bodies with a full humanoid skeleton, winter clothing and library
    /// motion (tools/blender/generate_character_kit_v2.py). Prefixes move here
    /// one by one; walkers and the player's own body stay on the first kit
    /// until their foot rigs are ported.
    /// </summary>
    public const string HumanScenePath = "res://assets/generated/urman_character_kit_v2.glb";
    private static readonly HashSet<string> HumanPrefixes = new(StringComparer.Ordinal) { "Mansur", "Gulsina", "Naila", "TimurHazrat", "Alsu", "Rinat", "Resident", "Tamara", "PhoneGuy" };
    private static PackedScene? _humanKit;

    public static bool UsesHumanKit(string prefix) => HumanPrefixes.Contains(prefix);

    /// <summary>
    /// VIS-044/VIS-103: the people a conversation camera actually holds at 1–2 m.
    /// Only these prefixes get the hero face pass (mid-forms in the source mesh,
    /// softened skin-scan detail at runtime); the rest of the cast keeps the
    /// ordinary soft response. The set is deliberate, not a template: every entry
    /// carries its own authored shape in tools/blender/generate_character_kit_v2.py.
    /// </summary>
    public static readonly IReadOnlyCollection<string> HeroConversationPrefixes =
        new HashSet<string>(StringComparer.Ordinal) { "Mansur", "Gulsina", "TimurHazrat" };

    /// <summary>VIS-048: the one distant NPC whose visual work is governed in the first patch.</summary>
    private const string GovernedDistantCharacterId = "background_resident";

    // Every NPC instantiates this one kit. Without a managed owner its C#
    // wrapper can be collected between two loads while Godot still caches the
    // native scene; the next Load then swaps a dead GC handle ("Handle is not
    // initialized") and the NPC is not attached. Keep the wrapper alive.
    private static PackedScene? _kit;

    private const float Lod0End = 18f;
    private const float Lod0EndMargin = 2f;
    private const float Lod1Begin = 14f;
    private const float Lod1BeginMargin = 2f;
    private const float Lod1End = 48f;
    private const float Lod1EndMargin = 4f;

    /// <summary>Test-only: release the retained kit so shutdown leak checks stay clean.</summary>
    public static void ClearCacheForHeadlessTests()
    {
        _kit = null;
        _humanKit = null;
        HumanMaterials.Clear();
    }

    public static Node3D Attach(
        Node3D parent,
        string characterId,
        string prefix,
        Vector3 anchor,
        bool sheltered = false)
    {
        var human = UsesHumanKit(prefix);
        var source = human ? HumanScenePath : ScenePath;
        // VIS-048 step 1: startup is measured in its own phases so a station receipt
        // can separate "the kit was loaded" from "this person is being animated".
        var startupBegin = Time.GetTicksUsec();
        var packed = human
            ? _humanKit is not null && GodotObject.IsInstanceValid(_humanKit) ? _humanKit : _humanKit = ResourceLoader.Load<PackedScene>(source)
            : _kit is not null && GodotObject.IsInstanceValid(_kit) ? _kit : _kit = ResourceLoader.Load<PackedScene>(source);
        if (packed is null)
        {
            throw new InvalidOperationException($"Generated character kit could not be loaded: {source}");
        }

        var loadedAt = Time.GetTicksUsec();
        var instance = packed.Instantiate<Node3D>();
        // Each placement needs one character. Other kit rigs are hidden below,
        // but retaining them also retains their skeletons and render instances.
        // Imported clips also carry rest tracks for the other kit rigs.
        // Give this instance its own selected clips before it enters the tree.
        foreach (var child in instance.GetChildren().OfType<Node3D>().ToArray())
        {
            var name = NodeName(child);
            if (name == $"{prefix}_Rig" || name == $"{prefix}_Anchor"
                || !(name.EndsWith("_Rig", StringComparison.Ordinal) || name.EndsWith("_Anchor", StringComparison.Ordinal))) continue;
            instance.RemoveChild(child);
            child.Free();
        }
        foreach (var player in instance.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>())
        {
            player.Autoplay = "";
            foreach (var libraryName in player.GetAnimationLibraryList())
            {
                var sourceLibrary = player.GetAnimationLibrary(libraryName);
                var selectedLibrary = new AnimationLibrary();
                foreach (var clipName in sourceLibrary.GetAnimationList())
                {
                    var name = clipName.ToString();
                    if (name != "RESET" && !name.StartsWith($"{prefix}_", StringComparison.Ordinal)) continue;
                    var clip = (Animation)sourceLibrary.GetAnimation(clipName).Duplicate();
                    for (var track = clip.GetTrackCount() - 1; track >= 0; track--)
                    {
                        var path = clip.TrackGetPath(track);
                        if (path.GetNameCount() == 0) continue;
                        var rootName = path.GetName(0).ToString();
                        if (rootName != $"{prefix}_Rig" && rootName != $"{prefix}_Anchor"
                            && (rootName.EndsWith("_Rig", StringComparison.Ordinal) || rootName.EndsWith("_Anchor", StringComparison.Ordinal)))
                            clip.RemoveTrack(track);
                    }
                    selectedLibrary.AddAnimation(clipName, clip);
                }
                player.RemoveAnimationLibrary(libraryName);
                player.AddAnimationLibrary(libraryName, selectedLibrary);
            }
        }
        var clipsSelectedAt = Time.GetTicksUsec();
        instance.Name = $"GeneratedCharacterKit_{characterId}";
        instance.SetMeta("assetSource", source);
        instance.SetMeta("characterKit", human ? "human-v2" : "procedural-v1");
        instance.SetMeta("characterId", characterId);
        instance.SetMeta("characterPrefix", prefix);
        instance.SetMeta("sheltered", sheltered);
        instance.SetMeta("lodPolicy", "LOD0 0-18m; LOD1 14-48m; self-fade");
        instance.SetMeta("collisionPolicy", "no character collision meshes; interaction targets and zone colliders own physics");
        parent.AddChild(instance);

        var meshes = instance
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .ToArray();
        var selected = meshes
            .Where(mesh => NodeName(mesh).StartsWith($"{prefix}_", StringComparison.Ordinal))
            .Where(mesh => NodeName(mesh).Contains("_LOD0", StringComparison.Ordinal)
                           || NodeName(mesh).Contains("_LOD1", StringComparison.Ordinal))
            .ToArray();
        if (selected.Length == 0)
        {
            instance.QueueFree();
            throw new InvalidOperationException($"Generated character prefix '{prefix}' selected no meshes.");
        }

        foreach (var mesh in meshes)
        {
            var isSelected = selected.Contains(mesh);
            mesh.Visible = isSelected;
            mesh.SetMeta("generatedCharacterVisible", mesh.Visible);
            if (!mesh.Visible)
            {
                continue;
            }

            ConfigureVisibilityRange(mesh);
            if (human) ApplyHumanMaterials(mesh, prefix, sheltered);
            else ApplyPainterlyMaterial(mesh, prefix, sheltered);
        }
        var dressedAt = Time.GetTicksUsec();

        var lod0 = selected.Count(mesh => mesh.Visible && NodeName(mesh).Contains("_LOD0", StringComparison.Ordinal));
        var lod1 = selected.Count(mesh => mesh.Visible && NodeName(mesh).Contains("_LOD1", StringComparison.Ordinal));
        if (lod0 == 0 || lod1 == 0)
        {
            instance.QueueFree();
            throw new InvalidOperationException($"Generated character prefix '{prefix}' requires both LOD0 and LOD1 (got {lod0}/{lod1}).");
        }

        var anchorNode = instance
            .FindChildren("*", nameof(Node3D), recursive: true, owned: false)
            .OfType<Node3D>()
            .FirstOrDefault(node => NodeName(node) == $"{prefix}_Anchor");
        if (anchorNode is null)
        {
            instance.QueueFree();
            throw new InvalidOperationException($"Generated character prefix '{prefix}' has no ground anchor.");
        }

        var animationPlayer = FindAnimationPlayer(instance, prefix);
        if (animationPlayer is null)
        {
            instance.QueueFree();
            throw new InvalidOperationException(
                $"Generated character prefix '{prefix}' has no AnimationPlayer with '{prefix}_Idle' and '{prefix}_Tension'.");
        }

        var idleClip = $"{prefix}_Idle";
        if (human)
        {
            // The human kit's clips are loops from the animation library; the
            // glTF carries no loop flag, so an idle otherwise froze after one pass.
            foreach (var suffix in new[] { "Idle", "Tension", "Talk", "Walk" })
                if (animationPlayer.HasAnimation($"{prefix}_{suffix}"))
                    animationPlayer.GetAnimation($"{prefix}_{suffix}").LoopMode = Animation.LoopModeEnum.Linear;
        }
        animationPlayer.Play(idleClip);
        // Visible idle life is carried by the authored clip itself, which needs
        // the exported skin to reach the bones (see the skinning note in
        // tools/blender/generate_character_kit.py). Callers that drive their own
        // sway simply stop the player after Attach, as the woodpile resident does.
        // Attach can run while a zone is still being assembled off-tree (for
        // example in the scene smoke test), so persist a stable node name
        // rather than calling GetPath before the instance enters SceneTree.
        instance.SetMeta("animationPlayer", animationPlayer.Name);
        instance.SetMeta("animationClips", $"{idleClip},{prefix}_Tension");
        instance.SetMeta("animationClip", idleClip);
        instance.SetMeta("animationStatus", "godot-animationplayer-idle-playing");
        AlignAnchor(instance, anchorNode, anchor);
        // The human kit's anchor is measured from its standing soles, so every
        // person built from it stands on the ground, not only those whose
        // staging grounds them explicitly.
        if (human) GroundSolesOnAnchor(instance);
        instance.SetMeta("visibleMeshCount", selected.Length);
        instance.SetMeta("lod0Count", lod0);
        instance.SetMeta("lod1Count", lod1);
        instance.SetMeta("generatedCharacterStatus", "godot-visibility-ranges-integrated");
        instance.SetMeta("anchor", anchor);
        instance.SetMeta("anchorReference", anchorNode.Name);
        instance.SetMeta("characterFocus", HeroConversationPrefixes.Contains(prefix) ? "hero-dialogue" : "cast");
        // VIS-048: startup receipt, in its own phases, separate from the steady-state
        // counters the governor below reports every frame.
        instance.SetMeta("kitLoadMicroseconds", (long)(loadedAt - startupBegin));
        instance.SetMeta("kitClipSelectionMicroseconds", (long)(clipsSelectedAt - loadedAt));
        instance.SetMeta("kitMaterialMicroseconds", (long)(dressedAt - clipsSelectedAt));
        instance.SetMeta("kitStartupMicroseconds", (long)(Time.GetTicksUsec() - startupBegin));
        // VIS-048: one anonymous far NPC, one prototype governor. Everything the
        // governor touches is instance-local; the per-instance AnimationLibrary
        // built above stays private to this person (no library is shared).
        if (characterId == GovernedDistantCharacterId)
            AttachDistantWorkGovernor(instance, animationPlayer, idleClip);
        return instance;
    }

    /// <summary>
    /// Select one of the authored presentation clips without touching runtime
    /// narrative state. The caller supplies the suffix used by the Blender
    /// source (for example, <c>Idle</c> or <c>Tension</c>).
    /// </summary>
    public static bool PlayClip(Node3D instance, string clipSuffix, double blendSeconds = -1)
    {
        var prefix = instance.GetMeta("characterPrefix").AsString();
        if (prefix.Length == 0 || clipSuffix.Length == 0)
        {
            return false;
        }

        var clip = $"{prefix}_{clipSuffix}";
        var player = FindAnimationPlayer(instance, prefix);
        if (player is null || !player.HasAnimation(clip))
        {
            return false;
        }

        player.Play(clip, blendSeconds);
        instance.SetMeta("animationClip", clip);
        instance.SetMeta("animationStatus", $"godot-animationplayer-{clipSuffix.ToLowerInvariant()}-playing");
        return true;
    }

    private static void ConfigureVisibilityRange(MeshInstance3D mesh)
    {
        mesh.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        if (NodeName(mesh).Contains("_LOD1", StringComparison.Ordinal))
        {
            mesh.VisibilityRangeBegin = Lod1Begin;
            mesh.VisibilityRangeBeginMargin = Lod1BeginMargin;
            mesh.VisibilityRangeEnd = Lod1End;
            mesh.VisibilityRangeEndMargin = Lod1EndMargin;
            mesh.SetMeta("visibilityRange", "14-48m");
        }
        else
        {
            mesh.VisibilityRangeBegin = 0f;
            mesh.VisibilityRangeBeginMargin = 0f;
            mesh.VisibilityRangeEnd = Lod0End;
            mesh.VisibilityRangeEndMargin = Lod0EndMargin;
            mesh.SetMeta("visibilityRange", "0-18m");
        }
    }

    /// <summary>
    /// VIS-102: one soft material response for every face the game shows, so no
    /// character reads as an asset-pack plastic mannequin and none reads as a skin
    /// scan. Soft diffuse with skin scattering and only a broad faint specular, and — the part that actually removes
    /// the photoreal smell — the authored normal map is held down to a third of its
    /// strength. Pores are not the target; the skull's form and the light are
    /// (VIS-044, VIS-103), so the face must survive a grayscale close-up.
    /// </summary>
    private const float SkinRoughness = .74f;
    private const float SkinScanDetailCeiling = .35f;

    internal static StandardMaterial3D SoftSkinResponse(StandardMaterial3D source, string prefix)
    {
        var face = (StandardMaterial3D)source.Duplicate();
        // P2 / VIS-102/103: a soft, living face rather than a banded toon mask. Burley
        // diffuse turns the light smoothly over brow, cheek and jaw; screen-space
        // skin scattering warms the terminator and softens the shadow side; a faint
        // broad specular and rim keep the head readable against any wall.
        face.DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Burley;
        face.SpecularMode = BaseMaterial3D.SpecularModeEnum.SchlickGgx;
        face.Roughness = SkinRoughness;
        face.MetallicSpecular = .16f;
        face.Metallic = 0f;
        face.SubsurfScatterEnabled = true;
        face.SubsurfScatterSkinMode = true;
        face.SubsurfScatterStrength = .32f;
        face.RimEnabled = true;
        face.Rim = .12f;
        face.RimTint = .6f;
        // A full-strength scan normal makes the CC0 head answer light with pores
        // instead of with brow, nose, cheek and jaw. Cap it, keep the map.
        // Godot 4 calls the normal map's strength `normal_scale` (BaseMaterial3D.NormalScale).
        if (face.NormalEnabled && face.NormalScale > SkinScanDetailCeiling)
            face.NormalScale = SkinScanDetailCeiling;
        face.SetMeta("softSkinResponse", HeroConversationPrefixes.Contains(prefix) ? "hero" : "cast");
        return face;
    }

    /// <summary>
    /// VIS-031/VIS-104/VIS-102: which anchor a cloth piece actually uses. Written on the
    /// mesh so a capture receipt can prove the contract instead of trusting the call site.
    /// </summary>
    private static void SetClothAnchorMeta(MeshInstance3D mesh, bool uvLocal) =>
        mesh.SetMeta("clothAnchor", uvLocal ? "uv-local" : "world-pending-kit");

    private static void ApplyPainterlyMaterial(MeshInstance3D mesh, string prefix, bool sheltered)
    {
        var (coat, accent) = prefix switch
        {
            "Mansur" => ("5e4638", "80654a"),
            "Gulsina" => ("6b5960", "b6a389"),
            "Alsu" => ("7b594c", "c0a884"),
            "TimurHazrat" => ("4f5b55", "8c795a"),
            "CouncilElder" => ("5f5145", "806c50"),
            "CouncilWitness" => ("4d5960", "9a775d"),
            "Naila" => ("a0aaa3", "728887"),
            "ArchiveClerk" => ("50575c", "9a7b62"),
            "PactKeeper" => ("4e4542", "a27b58"),
            _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, "Unknown generated character prefix.")
        };

        var name = NodeName(mesh);
        var isHead = name.StartsWith(prefix + "_Head_", StringComparison.Ordinal);
        var isFaceEyes = name.StartsWith(prefix + "_FaceEyes_", StringComparison.Ordinal);
        if ((isHead || isFaceEyes)
            && mesh.Mesh?.SurfaceGetMaterial(0) is StandardMaterial3D sourceFace)
        {
            var face = SoftSkinResponse(sourceFace, prefix);
            if (prefix == "Mansur" && isHead)
            {
                const string ageAlbedoPath = "res://assets/textures/characters/mansur_age_v1_albedo.png";
                face.AlbedoTexture = ResourceLoader.Load<Texture2D>(ageAlbedoPath)
                    ?? throw new InvalidOperationException($"Mansur age albedo is missing: {ageAlbedoPath}.");
            }
            mesh.MaterialOverride = face;
            mesh.SetMeta("painterlyMaterial", "CC0 textured face; soft matte response");
            mesh.SetMeta("softSkinResponse", HeroConversationPrefixes.Contains(prefix) ? "hero" : "cast");
            return;
        }
        var skin = prefix switch
        {
            "Mansur" => "a77b67",
            "Gulsina" => "b18470",
            "Alsu" => "ae7967",
            "TimurHazrat" => "a27a67",
            "CouncilWitness" => "a87d69",
            "Naila" => "b27f6d",
            _ => "a47c68"
        };
        var hair = prefix switch
        {
            "Mansur" => "8f887a",
            "Gulsina" => "817970",
            "Alsu" or "Naila" => "3b302e",
            "TimurHazrat" => "40342f",
            "CouncilWitness" => "4b3b35",
            _ => "332e2b"
        };
        var isFaceInk = name.Contains("FaceEyeIris", StringComparison.Ordinal)
            || name.Contains("FaceBrow", StringComparison.Ordinal)
            || name.Contains("FaceMouth", StringComparison.Ordinal);
        var faceInk = name.Contains("FaceMouth", StringComparison.Ordinal)
            ? "684039"
            : name.Contains("FaceBrow", StringComparison.Ordinal)
                ? hair
                : "3f302c";
        var isEyeWhite = name.Contains("FaceEye", StringComparison.Ordinal)
            && !name.Contains("Eyelid", StringComparison.Ordinal)
            && !isFaceInk;
        var isHand = name.Contains("Hand", StringComparison.Ordinal);
        var color = isHand || name.Contains("Head", StringComparison.Ordinal) || name.Contains("FaceNose", StringComparison.Ordinal)
            || name.Contains("FaceEyelid", StringComparison.Ordinal)
            || name.Contains("FaceMouthLowerLip", StringComparison.Ordinal)
            || name.Contains("FaceChin", StringComparison.Ordinal)
            || name.Contains("Ear", StringComparison.Ordinal) || name.Contains("Neck", StringComparison.Ordinal)
            ? skin
            : name.Contains("Hair", StringComparison.Ordinal) || name.Contains("FaceBeard", StringComparison.Ordinal)
                ? hair
                : name.Contains("Hat", StringComparison.Ordinal)
                    ? prefix == "Gulsina" ? "bbb09a" : hair
                    : name.Contains("Boot", StringComparison.Ordinal)
                        ? "494640"
                        : name.Contains("Trouser", StringComparison.Ordinal)
                    ? "494640"
                    : isEyeWhite
                    ? "d8cbb4"
                    : isFaceInk
                    ? faceInk
                    : name.Contains("ShoulderCuff", StringComparison.Ordinal) || name.Contains("Scarf", StringComparison.Ordinal)
                      || name.Contains("Apron", StringComparison.Ordinal) || name.Contains("TeaTowel", StringComparison.Ordinal)
                      || name.Contains("CardiganPlacket", StringComparison.Ordinal)
                      || name.Contains("CoatFrontPlacket", StringComparison.Ordinal)
                    ? accent
                    : coat;
        // Rinat's folded winter scarf must read as cloth beside his skin.
        // His existing cuffs/placket and the other eight palettes stay authored.
        if (prefix == "CouncilWitness" && name.StartsWith("CouncilWitness_ScarfBand_", StringComparison.Ordinal))
            color = "667874";
        var surface = isHand || name.Contains("Head", StringComparison.Ordinal)
            || name.Contains("Hair", StringComparison.Ordinal)
            || name.Contains("Face", StringComparison.Ordinal)
            || name.Contains("Ear", StringComparison.Ordinal)
            || name.Contains("Neck", StringComparison.Ordinal)
            ? string.Empty
            : "cloth";
        // Living characters do not carry the roof/furniture snow blanket on
        // shoulders and boots. Reuse the existing no-deposit material variant.
        var uvCloth = surface == "cloth" && (prefix == "CouncilWitness" || HasMetricClothUv(mesh));
        mesh.MaterialOverride = uvCloth
            ? PainterlyMaterialLibrary.ForMovingCloth(color)
            : PainterlyMaterialLibrary.ForColor(color, surface, sheltered: true);
        SetClothAnchorMeta(mesh, uvCloth);
        mesh.SetMeta("painterlyMaterial", surface.Length == 0 ? "shader" : surface);
    }

    private static readonly Dictionary<string, Material> HumanMaterials = new(StringComparer.Ordinal);

    /// <summary>
    /// VIS-104: the generators mark cloth that has metric rest UVs with the
    /// "cloth_uv_units" property (glTF node extras). Only such meshes may take the
    /// UV-bound cloth material; without the mark the UVs are not metric and the
    /// world-space projection stays, so an unregenerated kit looks as before.
    /// </summary>
    internal static bool HasMetricClothUv(MeshInstance3D mesh) =>
        mesh.HasMeta("extras") && mesh.GetMeta("extras").VariantType == Variant.Type.Dictionary
        && mesh.GetMeta("extras").AsGodotDictionary().ContainsKey("cloth_uv_units");

    /// <summary>Cloth for an address-specific recolour of a skinned NPC piece (mosque imam, shop seller).</summary>
    internal static Material ClothFor(MeshInstance3D mesh, string htmlColor)
    {
        var uvCloth = HasMetricClothUv(mesh);
        SetClothAnchorMeta(mesh, uvCloth);
        return uvCloth
            ? PainterlyMaterialLibrary.ForMovingCloth(htmlColor)
            : PainterlyMaterialLibrary.ForColor(htmlColor, "cloth", sheltered: true);
    }

    /// <summary>
    /// Human kit materials are named "&lt;hex&gt;__&lt;surface&gt;": clothing and hair
    /// take the world's painterly shader; the textured skin, eyes and brows keep their
    /// CC0 albedo under <see cref="SoftSkinResponse"/> — the same soft matte answer the
    /// first kit's faces get, with the scan normal held down (VIS-102).
    /// </summary>
    private static void ApplyHumanMaterials(MeshInstance3D mesh, string prefix, bool sheltered)
    {
        var sawCloth = false;
        var sawSoftSkin = false;
        var uvClothApplied = false;
        for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            var authored = mesh.Mesh.SurfaceGetMaterial(surface);
            var name = authored?.ResourceName ?? string.Empty;
            var uvCloth = prefix == "Alsu" || HasMetricClothUv(mesh);
            var parts = name.Split("__", 2);
            var namedFamily = parts.Length == 2 && parts[0].Length == 6;
            var isCloth = namedFamily && parts[1] is not ("skin_textured" or "hair");
            // Exactly the pieces the second branch below turns into the soft response:
            // the authored skin (and the CC0 eyes and brows that carry no family name).
            var isTexturedSkin = authored is StandardMaterial3D
                && (!namedFamily || parts[1] == "skin_textured");
            var key = $"{name}|{sheltered}|{uvCloth}";
            if (!HumanMaterials.TryGetValue(key, out var material))
            {
                if (parts.Length == 2 && parts[0].Length == 6 && parts[1] != "skin_textured")
                {
                    var surfaceKind = parts[1] is "hair" ? string.Empty : "cloth";
                    material = uvCloth && surfaceKind == "cloth"
                        ? PainterlyMaterialLibrary.ForMovingCloth(parts[0])
                        : PainterlyMaterialLibrary.ForColor(parts[0], surfaceKind, sheltered: true);
                }
                else if (authored is StandardMaterial3D textured)
                {
                    // VIS-102: skin, eyes and brows all answer light the same soft way,
                    // so the head is one material family beside the painterly cloth.
                    material = SoftSkinResponse(textured, prefix);
                }
                else
                {
                    material = authored ?? PainterlyMaterialLibrary.ForColor("808080", "cloth", sheltered: true);
                }
                HumanMaterials[key] = material;
            }
            mesh.SetSurfaceOverrideMaterial(surface, material);
            if (isTexturedSkin) sawSoftSkin = true;
            if (!isCloth) continue;
            // VIS-031/VIS-104: the anchor is a property of this mesh's own cloth surfaces,
            // so it is recorded per mesh even when the material itself came from the cache.
            sawCloth = true;
            uvClothApplied |= uvCloth;
        }
        if (sawCloth) SetClothAnchorMeta(mesh, uvClothApplied);
        if (sawSoftSkin)
            mesh.SetMeta("softSkinResponse", HeroConversationPrefixes.Contains(prefix) ? "hero" : "cast");
        mesh.SetMeta("painterlyMaterial", "human-kit named materials");
    }

    private static AnimationPlayer? FindAnimationPlayer(Node3D instance, string prefix)
    {
        var idle = $"{prefix}_Idle";
        var tension = $"{prefix}_Tension";
        return instance
            .FindChildren("*", nameof(AnimationPlayer), recursive: true, owned: false)
            .OfType<AnimationPlayer>()
            .FirstOrDefault(player => player.HasAnimation(idle) && player.HasAnimation(tension));
    }

    internal static void GroundSolesOnAnchor(Node3D instance)
    {
        if (instance.HasMeta("solesGroundedOnAnchor")) return;
        // The source foot_shape starts at z=0.010 above its ground Anchor.
        // On Alsu's sloping snow this display-board clearance adds to the
        // terrain difference as she turns. Move the whole visual rig together
        // onto its existing anchor, preserving bone/mesh frames and the actor's
        // world origin, scale, interaction targets and collision ownership.
        const float exportedSoleClearance = .010f;
        foreach (var child in instance.GetChildren().OfType<Node3D>())
            child.Position -= Vector3.Up * exportedSoleClearance;
        instance.SetMeta("solesGroundedOnAnchor", true);
        instance.SetMeta("removedSoleClearance", exportedSoleClearance);
    }

    private static void AlignAnchor(Node3D instance, Node3D reference, Vector3 anchor)
    {
        // Exported characters share a display board. Rebase its root children
        // so every later yaw rotates around this character's feet, not x=0 of
        // the board. Animation tracks target bones, not these scene roots.
        var boardOffset = reference.Position;
        foreach (var child in instance.GetChildren().OfType<Node3D>()) child.Position -= boardOffset;
        instance.Position = anchor;
    }

    private static string NodeName(Node node) => node.Name.ToString();

    /// <summary>
    /// VIS-048: the prototype that removes the unnecessary animation work of one
    /// far NPC without touching his logic.
    ///
    /// He is the woodpile neighbour: no interaction target, no narrative state,
    /// no route, only a looping idle that exists so the village looks lived in.
    /// Beyond the end of his own LOD1 range (48 m + 4 m margin) the engine already
    /// draws nothing, so the mixer keeps advancing bones for nobody.
    ///
    /// Rules the card sets, and how they hold here:
    /// • only invisible *visual* work stops; nothing in this file moves a
    ///   interaction, a runtime state or a logical clock;
    /// • logical time is kept: the loop phase the idle would have reached is
    ///   recomputed from the wall-clock time spent gated, so he returns in the
    ///   pose his own clock says, not restarted at frame zero;
    /// • no shared mutable state: the counters, the mixer reference and the timer
    ///   all belong to this one instance, and the AnimationLibrary built in Attach
    ///   stays private to him (it is never handed to another person);
    /// • a caller that drives the mixer itself wins: if the clip stops advancing
    ///   while the governor is not the one that stopped it, the governor disarms
    ///   for good and never resumes on that character's behalf.
    /// </summary>
    private static void AttachDistantWorkGovernor(Node3D instance, AnimationPlayer player, string clip)
    {
        var state = new DistantWorkState(instance, player, clip);
        // Godot's Timer node, not System.Threading.Timer: this tick has to run on
        // the scene thread inside the tree so it can read and stop the mixer.
        var timer = new global::Godot.Timer
        {
            Name = "KitDistantWorkGovernor",
            WaitTime = DistantWorkState.TickSeconds,
            OneShot = false,
            // The governor reads positions, so it must run in real time and stop
            // with the tree: a paused game must not accumulate gated seconds.
            // Godot 4 expresses "off while the tree is paused" as the node's
            // process mode, the successor of the old process_always flag.
            ProcessMode = Node.ProcessModeEnum.Pausable,
        };
        instance.AddChild(timer);
        timer.Timeout += state.Tick;
        timer.Start();
        state.Report();
    }

    /// <summary>Instance-local state of the VIS-048 prototype. Plain C#, not a
    /// Godot script class: nothing here is reachable from another character.</summary>
    private sealed class DistantWorkState
    {
        internal const float TickSeconds = .25f;

        private readonly Node3D _instance;
        private readonly AnimationPlayer _player;
        private readonly float _cycleSeconds;
        private readonly float _beyondMetres;
        private string _clip;
        private double _lastPosition;
        private bool _gated;
        private bool _disarmed;
        private int _stalledReads;
        private int _noOwnerReads;
        private double _gatedSeconds;
        private long _decisionMicrosecondsMax;
        private long _stopMicroseconds;
        private long _resumeMicroseconds;

        internal DistantWorkState(Node3D instance, AnimationPlayer player, string clip)
        {
            _instance = instance;
            _player = player;
            _clip = clip;
            // The character's own far limit, read from the LOD constants this file
            // already owns, plus a margin so the return never races the fade.
            _beyondMetres = Lod1End + Lod1EndMargin;
            _cycleSeconds = player.HasAnimation(clip) ? (float)player.GetAnimation(clip).Length : 0f;
            // Godot 4's mixer exposes the playing clip's cursor as
            // AnimationPlayer.CurrentAnimationPosition (there is no playback_position
            // member on the C# AnimationPlayer).
            _lastPosition = player.CurrentAnimationPosition;
        }

        internal void Tick()
        {
            if (_disarmed || !GodotObject.IsInstanceValid(_instance) || !GodotObject.IsInstanceValid(_player))
                return;
            var began = Time.GetTicksUsec();
            var camera = ViewerCamera();
            if (camera is null)
            {
                // No viewer means no evidence about visibility. Do not invent work
                // stopping in a headless test or before the player exists.
                if (++_noOwnerReads >= 40) _disarmed = true;
                Report();
                return;
            }

            var position = _player.CurrentAnimationPosition;
            var advanced = position - _lastPosition;
            _lastPosition = position;
            if (!_gated)
            {
                // Someone else owns this mixer now (the caller stopped it after
                // Attach, as the woodpile sway driver does). Their stop wins.
                if (System.Math.Abs(advanced) < .000001) _stalledReads++;
                else _stalledReads = 0;
                if (_stalledReads >= 2) { _disarmed = true; Report(); return; }
                var current = _player.GetCurrentAnimation().ToString();
                if (current.Length > 0) _clip = current;
            }

            var distance = camera.GlobalPosition.DistanceTo(_instance.GlobalPosition);
            if (distance > _beyondMetres && !_gated)
            {
                var stopped = Time.GetTicksUsec();
                // AnimationMixer.stop() applies no transition and no reset: the
                // skeleton keeps the pose it held, so nothing pops on the way out.
                _player.Stop();
                _stopMicroseconds = (long)(Time.GetTicksUsec() - stopped);
                _gated = true;
                _gatedSeconds = 0;
                _lastPosition = 0;
            }
            else if (_gated)
            {
                if (distance <= _beyondMetres) Resume();
                else _gatedSeconds += TickSeconds;
            }

            _decisionMicrosecondsMax = System.Math.Max(_decisionMicrosecondsMax,
                (long)(Time.GetTicksUsec() - began));
            Report();
        }

        private void Resume()
        {
            var began = Time.GetTicksUsec();
            // Logical continuation: the loop phase the idle would have reached while
            // the mixer was stopped, then one evaluation so the pose is in place
            // before the first frame that actually draws him.
            var phase = _cycleSeconds > 0f
                ? (float)(_gatedSeconds % _cycleSeconds)
                : 0f;
            _player.Play(_clip);
            if (_cycleSeconds > 0f) _player.Seek(phase);
            _player.Advance(0);
            _resumeMicroseconds = (long)(Time.GetTicksUsec() - began);
            _gated = false;
            _gatedSeconds = 0;
            _stalledReads = 0;
            _instance.SetMeta("kitWorkResumePhaseSeconds", phase);
            _instance.SetMeta("kitWorkResumeClip", _clip);
        }

        private Camera3D? ViewerCamera() =>
            (_instance.GetTree()?.GetFirstNodeInGroup("player_controller") as FirstPersonController)
                ?.GetNodeOrNull<Camera3D>("Head/Camera3D");

        internal void Report()
        {
            // Startup numbers live on the instance from Attach; these are the
            // steady-state ones, and the card demands the two stay separate.
            _instance.SetMeta("kitWorkPolicy", "beyond-lod1-end-mixer-stop");
            _instance.SetMeta("kitWorkBeyondMetres", _beyondMetres);
            _instance.SetMeta("kitWorkTickSeconds", TickSeconds);
            _instance.SetMeta("kitWorkGated", _gated);
            _instance.SetMeta("kitWorkDisarmed", _disarmed);
            _instance.SetMeta("kitWorkGatedSeconds", (float)_gatedSeconds);
            _instance.SetMeta("kitWorkStopMicroseconds", _stopMicroseconds);
            _instance.SetMeta("kitWorkResumeMicroseconds", _resumeMicroseconds);
            _instance.SetMeta("kitWorkDecisionMicrosecondsMax", _decisionMicrosecondsMax);
            _instance.SetMeta("kitWorkCycleSeconds", _cycleSeconds);
        }
    }
}
