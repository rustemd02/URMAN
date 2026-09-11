using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only bridge from the project-original Blender character kit.
/// One authored character prefix is selected per NPC placement; the adapter
/// never creates physics bodies or owns dialogue/progression state.
/// </summary>
public static class GeneratedCharacterKitDressing
{
    public const string ScenePath = "res://assets/generated/urman_character_kit.glb";

    private const float Lod0End = 18f;
    private const float Lod0EndMargin = 2f;
    private const float Lod1Begin = 14f;
    private const float Lod1BeginMargin = 2f;
    private const float Lod1End = 48f;
    private const float Lod1EndMargin = 4f;

    public static Node3D Attach(
        Node3D parent,
        string characterId,
        string prefix,
        Vector3 anchor,
        bool sheltered = false)
    {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            throw new InvalidOperationException($"Generated character kit could not be loaded: {ScenePath}");
        }

        var instance = packed.Instantiate<Node3D>();
        instance.Name = $"GeneratedCharacterKit_{characterId}";
        instance.SetMeta("assetSource", ScenePath);
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
            ApplyPainterlyMaterial(mesh, prefix, sheltered);
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
        animationPlayer.Play(idleClip);
        // Attach can run while a zone is still being assembled off-tree (for
        // example in the scene smoke test), so persist a stable node name
        // rather than calling GetPath before the instance enters SceneTree.
        instance.SetMeta("animationPlayer", animationPlayer.Name);
        instance.SetMeta("animationClips", $"{idleClip},{prefix}_Tension");
        instance.SetMeta("animationClip", idleClip);
        instance.SetMeta("animationStatus", "godot-animationplayer-idle-playing");
        AlignAnchor(instance, anchorNode, anchor);
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
            "Alsu" => ("43535a", "9b7656"),
            "TimurHazrat" => ("4f5b55", "8c795a"),
            "CouncilElder" => ("5f5145", "806c50"),
            "CouncilWitness" => ("4d5960", "9a775d"),
            "Naila" => ("a0aaa3", "728887"),
            "ArchiveClerk" => ("50575c", "9a7b62"),
            "PactKeeper" => ("4e4542", "a27b58"),
            _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, "Unknown generated character prefix.")
        };

        var name = NodeName(mesh);
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
            "Mansur" or "Gulsina" => "6f5448",
            "Alsu" or "Naila" => "3b302e",
            "TimurHazrat" => "40342f",
            "CouncilWitness" => "4b3b35",
            _ => "332e2b"
        };
        var isFaceInk = name.Contains("FaceEyeIris", StringComparison.Ordinal)
            || name.Contains("FaceBrow", StringComparison.Ordinal)
            || name.Contains("FaceMouth", StringComparison.Ordinal);
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
                    ? hair
                    : name.Contains("Boot", StringComparison.Ordinal)
                        ? "494640"
                        : name.Contains("Trouser", StringComparison.Ordinal)
                    ? "494640"
                    : isEyeWhite
                    ? "d8cbb4"
                    : isFaceInk
                    ? "2f2522"
                    : name.Contains("Shoulder", StringComparison.Ordinal) || name.Contains("Scarf", StringComparison.Ordinal)
                      || name.Contains("Apron", StringComparison.Ordinal) || name.Contains("CardiganPlacket", StringComparison.Ordinal)
                    ? accent
                    : coat;
        var surface = isHand || name.Contains("Head", StringComparison.Ordinal)
            || name.Contains("Hair", StringComparison.Ordinal)
            || name.Contains("Face", StringComparison.Ordinal)
            || name.Contains("Ear", StringComparison.Ordinal)
            || name.Contains("Neck", StringComparison.Ordinal)
            ? string.Empty
            : "cloth";
        mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface, sheltered);
        mesh.SetMeta("painterlyMaterial", surface.Length == 0 ? "shader" : surface);
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
