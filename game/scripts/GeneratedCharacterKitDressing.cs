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
    private static readonly HashSet<string> HumanPrefixes = new(StringComparer.Ordinal) { "Mansur", "Gulsina", "Naila", "TimurHazrat", "Alsu", "Rinat", "Resident" };
    private static PackedScene? _humanKit;

    public static bool UsesHumanKit(string prefix) => HumanPrefixes.Contains(prefix);

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
        var packed = human
            ? _humanKit is not null && GodotObject.IsInstanceValid(_humanKit) ? _humanKit : _humanKit = ResourceLoader.Load<PackedScene>(source)
            : _kit is not null && GodotObject.IsInstanceValid(_kit) ? _kit : _kit = ResourceLoader.Load<PackedScene>(source);
        if (packed is null)
        {
            throw new InvalidOperationException($"Generated character kit could not be loaded: {source}");
        }

        var instance = packed.Instantiate<Node3D>();
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
            if (human) ApplyHumanMaterials(mesh, sheltered);
            else ApplyPainterlyMaterial(mesh, prefix, sheltered);
        }

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
        return instance;
    }

    /// <summary>
    /// Select one of the authored presentation clips without touching runtime
    /// narrative state. The caller supplies the suffix used by the Blender
    /// source (for example, <c>Idle</c> or <c>Tension</c>).
    /// </summary>
    public static bool PlayClip(Node3D instance, string clipSuffix)
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

        player.Play(clip);
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
            var face = (StandardMaterial3D)sourceFace.Duplicate();
            face.DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon;
            face.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
            face.Roughness = 1f;
            face.MetallicSpecular = 0f;
            face.Metallic = 0f;
            if (prefix == "Mansur" && isHead)
            {
                const string ageAlbedoPath = "res://assets/textures/characters/mansur_age_v1_albedo.png";
                face.AlbedoTexture = ResourceLoader.Load<Texture2D>(ageAlbedoPath)
                    ?? throw new InvalidOperationException($"Mansur age albedo is missing: {ageAlbedoPath}.");
            }
            mesh.MaterialOverride = face;
            mesh.SetMeta("painterlyMaterial", "CC0 textured face; native toon diffuse");
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
        mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface, sheltered: true);
        mesh.SetMeta("painterlyMaterial", surface.Length == 0 ? "shader" : surface);
    }

    private static readonly Dictionary<string, Material> HumanMaterials = new(StringComparer.Ordinal);

    /// <summary>
    /// Human kit materials are named "&lt;hex&gt;__&lt;surface&gt;": clothing and hair
    /// take the world's painterly shader; the textured skin, eyes and brows keep
    /// their CC0 albedo under the same toon diffuse the first kit's faces use.
    /// </summary>
    private static void ApplyHumanMaterials(MeshInstance3D mesh, bool sheltered)
    {
        for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            var authored = mesh.Mesh.SurfaceGetMaterial(surface);
            var name = authored?.ResourceName ?? string.Empty;
            var key = $"{name}|{sheltered}";
            if (!HumanMaterials.TryGetValue(key, out var material))
            {
                var parts = name.Split("__", 2);
                if (parts.Length == 2 && parts[0].Length == 6 && parts[1] != "skin_textured")
                {
                    var surfaceKind = parts[1] is "hair" ? string.Empty : "cloth";
                    material = PainterlyMaterialLibrary.ForColor(parts[0], surfaceKind, sheltered: true);
                }
                else if (authored is StandardMaterial3D textured)
                {
                    var toon = (StandardMaterial3D)textured.Duplicate();
                    toon.DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon;
                    toon.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
                    toon.Roughness = 1f;
                    toon.Metallic = 0f;
                    toon.MetallicSpecular = 0f;
                    material = toon;
                }
                else
                {
                    material = authored ?? PainterlyMaterialLibrary.ForColor("808080", "cloth", sheltered: true);
                }
                HumanMaterials[key] = material;
            }
            mesh.SetSurfaceOverrideMaterial(surface, material);
        }
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
}
