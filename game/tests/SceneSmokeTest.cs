using Godot;

namespace Urman.Godot.Tests;

public partial class SceneSmokeTest : Node
{
    private const string KaraUrmanScenePath = "res://scenes/zones/style_benchmark_kara_urman_night.tscn";

    private static readonly string[] ScenePaths =
    [
        "res://scenes/player/first_person_player.tscn",
        "res://scenes/zones/style_benchmark_day_street.tscn",
        "res://scenes/zones/style_benchmark_house_pc.tscn",
        "res://scenes/zones/chapter1_fap_clinic.tscn",
        "res://scenes/zones/chapter1_zirat_road.tscn",
        "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
        "res://scenes/ui/old_pc_ui.tscn",
        "res://scenes/ui/dialogue_ui.tscn",
        "res://scenes/ui/audio_cue_ui.tscn",
        "res://scenes/ui/journal_ui.tscn",
        "res://scenes/ui/document_ui.tscn",
        "res://scenes/ui/settings_ui.tscn",
        "res://assets/generated/urman_modular_kit.glb",
        "res://assets/generated/urman_character_kit.glb",
        "res://scenes/zones/fullgame_zone.tscn",
        "res://scenes/zones/fullgame/act2_house.tscn",
        "res://scenes/zones/fullgame/act2_river.tscn",
        "res://scenes/zones/fullgame/act2_mosque.tscn",
        "res://scenes/zones/fullgame/act2_council.tscn",
        "res://scenes/zones/fullgame/act3_archive.tscn",
        "res://scenes/zones/fullgame/act3_soviet.tscn",
        "res://scenes/zones/fullgame/act3_water.tscn",
        "res://scenes/zones/fullgame/act4_tukay.tscn",
        "res://scenes/zones/fullgame/act4_1552.tscn",
        "res://scenes/zones/fullgame/act4_pact.tscn",
        "res://scenes/zones/fullgame/act5_boundary.tscn",
        "res://scenes/zones/fullgame/act5_epilogue.tscn",
        "res://scenes/full_game.tscn",
        "res://scenes/main.tscn",
        "res://tests/full_game_dressing_capture.tscn",
        "res://tests/collision_qa_smoke_test.tscn",
        "res://tests/performance_benchmark.tscn"
    ];

    public override async void _Ready()
    {
        foreach (var path in ScenePaths)
        {
            var packed = ResourceLoader.Load<PackedScene>(path);
            if (packed is null)
            {
                GD.PushError($"Scene smoke test could not load {path}.");
                GetTree().Quit(1);
                return;
            }

            var instance = packed.Instantiate();
            AddChild(instance);
            // Let _Ready run before releasing the scene. This matters for scenes
            // that create presentation resources (audio players, materials, etc.)
            // during startup; an immediate Free leaves them alive at process exit.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (path == KaraUrmanScenePath)
            {
                var pinePhysicsError = ValidateKaraUrmanPinePhysics(instance);
                if (pinePhysicsError.Length > 0)
                {
                    await GodotSmokeCleanup.ReleaseAsync(instance);
                    GD.PushError($"Scene smoke PineA physics contract failed: {pinePhysicsError}");
                    GetTree().Quit(1);
                    return;
                }
            }
            else if (path == "res://scenes/zones/style_benchmark_day_street.tscn")
            {
                var villageModuleError = ValidateDayStreetPresentationModules(instance);
                if (villageModuleError.Length > 0)
                {
                    await GodotSmokeCleanup.ReleaseAsync(instance);
                    GD.PushError($"Scene smoke day-street module contract failed: {villageModuleError}");
                    GetTree().Quit(1);
                    return;
                }
                var npcError = ValidateAct1NpcPresentation(instance, "alsu", "Alsu", string.Empty);
                if (npcError.Length > 0)
                {
                    await GodotSmokeCleanup.ReleaseAsync(instance);
                    GD.PushError($"Scene smoke day-street NPC contract failed: {npcError}");
                    GetTree().Quit(1);
                    return;
                }
            }
            else if (path == "res://scenes/zones/style_benchmark_house_pc.tscn")
            {
                var oldPcError = ValidateHouseOldPcPresentation(instance);
                if (oldPcError.Length > 0)
                {
                    await GodotSmokeCleanup.ReleaseAsync(instance);
                    GD.PushError($"Scene smoke house OldPc module contract failed: {oldPcError}");
                    GetTree().Quit(1);
                    return;
                }
                var npcError = ValidateAct1NpcPresentation(instance, "gulsina", "Gulsina", "GulsinaNpc");
                if (npcError.Length > 0)
                {
                    await GodotSmokeCleanup.ReleaseAsync(instance);
                    GD.PushError($"Scene smoke house NPC contract failed: {npcError}");
                    GetTree().Quit(1);
                    return;
                }
            }
            else if (path == "res://scenes/zones/chapter1_fap_clinic.tscn")
            {
                var npcError = ValidateAct1NpcPresentation(instance, "naila", "Naila", string.Empty);
                if (npcError.Length > 0)
                {
                    await GodotSmokeCleanup.ReleaseAsync(instance);
                    GD.PushError($"Scene smoke FAP NPC contract failed: {npcError}");
                    GetTree().Quit(1);
                    return;
                }
            }

            await GodotSmokeCleanup.ReleaseAsync(instance);
            GD.Print($"scene-smoke: {path}");
        }

        GetTree().Quit(0);
    }

    private static string ValidateKaraUrmanPinePhysics(Node scene)
    {
        if (scene.GetMeta("styleImportedModules").AsString() != "PineA_project_original"
            || scene.GetMeta("styleImportedPineInstances").AsInt32() != 3)
        {
            return "scene metadata must preserve PineA_project_original with exactly three instances";
        }

        var original = scene.GetNodeOrNull<Node3D>("GeneratedModularKit");
        if (original is null)
        {
            return "original GeneratedModularKit instance is missing";
        }

        var originalObjects = original
            .FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false)
            .OfType<CollisionObject3D>()
            .ToArray();
        var originalShapes = original
            .FindChildren("*", nameof(CollisionShape3D), recursive: true, owned: false)
            .OfType<CollisionShape3D>()
            .ToArray();
        var proxy = original.GetNodeOrNull<StaticBody3D>("KitCollisionProxy");
        if (proxy is null
            || originalObjects.Length != 1
            || originalObjects[0] != proxy
            || originalShapes.Length != 1
            || originalShapes[0].GetParent() != proxy
            || proxy.CollisionLayer != 2
            || proxy.CollisionMask != 0
            || original.GetMeta("collisionShapeCount").AsInt32() != 1)
        {
            return "original PineA must contain only one controlled layer-2 KitCollisionProxy and its single shape";
        }

        foreach (var name in new[] { "GeneratedPineA_MidLeft", "GeneratedPineA_NearRight" })
        {
            var presentation = scene.GetNodeOrNull<Node3D>(name);
            if (presentation is null)
            {
                return $"presentation PineA '{name}' is missing";
            }

            var collisionObjectCount = presentation
                .FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false)
                .Count;
            var collisionShapeCount = presentation
                .FindChildren("*", nameof(CollisionShape3D), recursive: true, owned: false)
                .Count;
            if (!presentation.GetMeta("presentationOnlyInstance").AsBool()
                || presentation.GetMeta("collisionShapeCount").AsInt32() != 0
                || collisionObjectCount != 0
                || collisionShapeCount != 0)
            {
                return $"presentation PineA '{name}' must have presentationOnlyInstance=true and zero physics descendants";
            }
        }

        return string.Empty;
    }

    private static string ValidateDayStreetPresentationModules(Node scene)
    {
        if (scene.GetMeta("styleImportedModules").AsString() != "HouseA_project_original"
            || scene.GetMeta("stylePresentationModules").AsString()
                != "WellA_project_original|WoodpileA_project_original")
        {
            return "day street metadata must preserve HouseA and the WellA/WoodpileA presentation modules";
        }

        var sign = scene.GetNodeOrNull<Label3D>("VillageSignText");
        if (sign is null || sign.Text != "ФАП" || sign.GetMeta("wayfindingLandmark").AsString() != "fap")
        {
            return "day street wayfinding sign is missing its diegetic ФАП landmark contract";
        }

        foreach (var (nodeName, moduleName) in new[]
                 {
                     ("GeneratedWellA", "WellA_project_original"),
                     ("GeneratedWoodpileA", "WoodpileA_project_original")
                 })
        {
            var module = scene.GetNodeOrNull<Node3D>(nodeName);
            if (module is null)
            {
                return $"presentation module '{nodeName}' is missing";
            }

            var collisionObjects = module
                .FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false)
                .Count;
            var collisionShapes = module
                .FindChildren("*", nameof(CollisionShape3D), recursive: true, owned: false)
                .Count;
            if (!module.GetMeta("presentationOnlyInstance").AsBool()
                || module.GetMeta("stylePresentationModule").AsString() != moduleName
                || module.GetMeta("collisionShapeCount").AsInt32() != 0
                || module.GetMeta("importedCollisionObjectsRemoved").AsInt32() <= 0
                || module.GetMeta("importedCollisionShapesRemoved").AsInt32() <= 0
                || collisionObjects != 0
                || collisionShapes != 0)
            {
                return $"presentation module '{nodeName}' must have zero physics descendants and positive imported-collision sanitation metadata";
            }
        }

        return string.Empty;
    }

    private static string ValidateHouseOldPcPresentation(Node scene)
    {
        if (scene.GetMeta("styleImportedModules").AsString() != "OldPc_project_original")
        {
            return "house metadata must preserve OldPc_project_original";
        }

        var module = scene.GetNodeOrNull<Node3D>("GeneratedOldPcAct1");
        if (module is null)
        {
            return "presentation OldPc module is missing";
        }

        var collisionObjects = module
            .FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false)
            .Count;
        var collisionShapes = module
            .FindChildren("*", nameof(CollisionShape3D), recursive: true, owned: false)
            .Count;
        if (!module.GetMeta("presentationOnlyInstance").AsBool()
            || module.GetMeta("stylePresentationModule").AsString() != "OldPc_project_original"
            || module.GetMeta("visibleMeshCount").AsInt32() != 16
            || module.GetMeta("lod0Count").AsInt32() != 8
            || module.GetMeta("lod1Count").AsInt32() != 8
            || module.GetMeta("collisionShapeCount").AsInt32() != 0
            || module.GetMeta("importedCollisionObjectsRemoved").AsInt32() <= 0
            || module.GetMeta("importedCollisionShapesRemoved").AsInt32() <= 0
            || collisionObjects != 0
            || collisionShapes != 0)
        {
            return "presentation OldPc must be the 8/8 GLB pair with zero physics descendants and positive imported-collision sanitation metadata";
        }

        var interaction = scene.GetNodeOrNull<InteractionTarget>("OldPc");
        var interactionShape = interaction?.GetNodeOrNull<CollisionShape3D>("InteractionProxyCollisionShape");
        if (interaction is null
            || interaction.InteractionId != "urman.chapter1:interaction/oldpc-power"
            || interactionShape is null)
        {
            return "the OldPc gameplay interaction target or its layer-1 ray shape is missing";
        }

        return string.Empty;
    }

    private static string ValidateAct1NpcPresentation(
        Node scene,
        string expectedCharacterId,
        string expectedPrefix,
        string interactionName)
    {
        var host = scene.GetNodeOrNull<Node3D>("Act1NpcPresentation");
        if (host is null
            || host.GetMeta("status").AsString() != "generated-character-kit-v1"
            || host.GetMeta("assetSource").AsString() != GeneratedCharacterKitDressing.ScenePath
            || host.GetMeta("ownership").AsString() != "presentation-only"
            || host.GetMeta("defaultAnimationClip").AsString() != "Idle"
            || host.GetMeta("npcCount").AsInt32() != 1)
        {
            return "Act 1 NPC host metadata is missing or not presentation-only";
        }

        var npcs = host.GetChildren()
            .OfType<Node3D>()
            .Where(node => node.Name.ToString().StartsWith("Npc_", StringComparison.Ordinal))
            .ToArray();
        if (npcs.Length != 1)
        {
            return $"expected one authored NPC, found {npcs.Length}";
        }

        var npc = npcs[0];
        var contractFailures = new List<string>();
        if (npc.GetMeta("characterId").AsString() != expectedCharacterId) contractFailures.Add($"characterId={npc.GetMeta("characterId").AsString()}");
        if (npc.GetMeta("characterPrefix").AsString() != expectedPrefix) contractFailures.Add($"characterPrefix={npc.GetMeta("characterPrefix").AsString()}");
        if (npc.GetMeta("presentationStatus").AsString() != "generated-character-kit-v1") contractFailures.Add($"presentationStatus={npc.GetMeta("presentationStatus").AsString()}");
        if (npc.GetMeta("interactionOwnership").AsString() != "none") contractFailures.Add($"interactionOwnership={npc.GetMeta("interactionOwnership").AsString()}");
        if (npc.GetMeta("collisionLayer").AsInt32() != 0) contractFailures.Add($"collisionLayer={npc.GetMeta("collisionLayer").AsInt32()}");
        if (npc.GetMeta("animationClip").AsString() != $"{expectedPrefix}_Idle") contractFailures.Add($"animationClip={npc.GetMeta("animationClip").AsString()}");
        if (!npc.GetMeta("animationClips").AsString().Contains($"{expectedPrefix}_Tension", StringComparison.Ordinal)) contractFailures.Add($"animationClips={npc.GetMeta("animationClips").AsString()}");
        // A live NPC (Alsu's street walk, Rinat's landing) owns exactly one
        // contact capsule so the player cannot walk through her; its layer and
        // lifecycle are proven by the walk proofs. Anything else is a leak.
        var strayColliders = npc.FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false)
            .Where(node => !(node is AnimatableBody3D body
                             && body.Name.ToString() == $"{expectedPrefix}PhysicalContact"
                             && body.HasMeta("collisionOwner")))
            .ToArray();
        if (strayColliders.Length != 0) contractFailures.Add($"colliders={string.Join(",", strayColliders.Select(node => node.Name.ToString()))}");
        if (contractFailures.Count != 0)
        {
            return $"NPC metadata, animation clips, or single-contact-body contract failed: {string.Join("; ", contractFailures)}";
        }

        var player = npc.FindChildren("*", nameof(AnimationPlayer), recursive: true, owned: false)
            .OfType<AnimationPlayer>()
            .FirstOrDefault(candidate => candidate.HasAnimation($"{expectedPrefix}_Idle")
                                         && candidate.HasAnimation($"{expectedPrefix}_Tension"));
        if (player is null || !player.IsPlaying() || player.CurrentAnimation != $"{expectedPrefix}_Idle")
        {
            return "NPC must be playing authored Idle while exposing both Idle and Tension clips";
        }

        // Playing is metadata; motion is geometry. The review pack could only
        // show that cast frames differ over time, and part of that difference
        // could come from snow or branch sway, so sample the skeleton itself:
        // if the authored Idle clip moves no bone between two positions inside
        // it, the NPC is a statue that merely reports a running player.
        var skeleton = npc.FindChildren("*", nameof(Skeleton3D), recursive: true, owned: false)
            .OfType<Skeleton3D>().FirstOrDefault();
        if (skeleton is null || skeleton.GetBoneCount() == 0)
        {
            return "NPC has no skeleton, so its animation cannot be observed";
        }

        var idleAnimation = player.GetAnimation($"{expectedPrefix}_Idle");
        if (idleAnimation is null || idleAnimation.Length <= 0.05)
        {
            return "NPC Idle clip has no usable length to sample";
        }

        // The skinning fix (2026-08-14 .. 2026-09-14) exists so that this is
        // true: the authored clip must move the skeleton. Before the skin, the
        // same check measured poseMoved=0, globalMoved=0 and no mesh movement,
        // because bone-parented meshes export no skin and Godot then imports
        // animation as inert tracks named after the bone.
        // The asset contract that makes animation possible, and that the earlier
        // bone-parented kit failed: every character mesh must be skinned to the
        // skeleton, and the Idle clip must actually carry moving bone channels.
        // Bone-parented meshes export no skin, Godot then imports the animation
        // as inert tracks named after the bones, and the cast stands frozen -
        // measured here (poseMoved=0, globalMoved=0, no mesh movement) before the
        // skinning fix in tools/blender/generate_character_kit.py.
        var characterMeshes = npc.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>().Where(mesh => mesh.Mesh is not null).ToArray();
        var skinned = characterMeshes.Count(mesh => mesh.Skeleton is not null && mesh.Skeleton.ToString().Length > 0);
        if (characterMeshes.Length == 0 || skinned != characterMeshes.Length)
        {
            return $"NPC meshes are not fully skinned ({skinned}/{characterMeshes.Length}), "
                + "so the imported animation cannot reach the character";
        }

        var idleBones = new[] { "Spine", "Head", "Arm.L", "Arm.R" }
            .Select(name => skeleton.FindBone(name))
            .Where(bone => bone >= 0)
            .ToArray();
        if (idleBones.Length == 0)
        {
            return "NPC skeleton exposes none of the idle bones (Spine/Head/Arm.L/Arm.R)";
        }

        // Seek/Advance cannot be used to observe the pose here: this smoke builds
        // the zone off-tree, so an AnimationPlayer never ticks. The clip's own
        // channels are therefore inspected instead; end-to-end in-game movement
        // is verified by the capture harness (character region differs 16.7
        // percent between phases against 1.5 percent of background).
        var boneNames = idleBones.Select(bone => skeleton.GetBoneName(bone)).ToHashSet();
        var movingChannels = 0;
        for (var track = 0; track < idleAnimation.GetTrackCount(); track++)
        {
            var trackPath = idleAnimation.TrackGetPath(track).ToString();
            var separator = trackPath.LastIndexOf(':');
            if (separator < 0 || !boneNames.Contains(trackPath[(separator + 1)..])) continue;
            var keys = idleAnimation.TrackGetKeyCount(track);
            if (keys < 2) continue;
            var first = idleAnimation.TrackGetKeyValue(track, 0).AsQuaternion();
            for (var key = 1; key < keys; key++)
            {
                if (first.AngleTo(idleAnimation.TrackGetKeyValue(track, key).AsQuaternion()) > 0.004f)
                {
                    movingChannels++;
                    break;
                }
            }
        }

        GD.Print($"npc-idle-clip: {expectedPrefix} skinned={skinned}/{characterMeshes.Length} "
            + $"idleBones={idleBones.Length} movingChannels={movingChannels}");
        if (movingChannels == 0)
        {
            return "NPC Idle clip carries no moving bone channels, so the character would stand frozen";
        }

        if (interactionName.Length == 0)
        {
            return string.Empty;
        }

        var interaction = scene.GetNodeOrNull<InteractionTarget>(interactionName);
        var hiddenMesh = interaction?.GetNodeOrNull<Node3D>("HiddenInteractionProxyVisual/HiddenInteractionProxyMesh")
            as MeshInstance3D;
        var collisionShape = interaction?.GetNodeOrNull<CollisionShape3D>("InteractionProxyCollisionShape");
        if (interaction is null
            || hiddenMesh is null
            || hiddenMesh.Visible
            || collisionShape is null
            || interaction.GetMeta("proxyVisualHidden").AsBool() != true)
        {
            return "NPC interaction must retain its collision shape while hiding only the proxy mesh";
        }

        if (interactionName == "GulsinaNpc")
        {
            // The live Rinat is attached by Act1ConnectedWorld, not by this
            // standalone house scene. Keep this smoke focused on the authored
            // interaction contract.
            var rinatTarget = scene.GetNodeOrNull<InteractionTarget>("InternalRegisterToRinat");
            var targetMesh = rinatTarget?.GetNodeOrNull<MeshInstance3D>(
                "HiddenInteractionProxyVisual/HiddenInteractionProxyMesh");
            var targetShape = rinatTarget?.GetNodeOrNull<CollisionShape3D>(
                "InteractionProxyCollisionShape");
            if (rinatTarget is null
                || rinatTarget.InteractionId != "urman.chapter1:interaction/internal-register-to-rinat"
                || targetMesh is null
                || targetMesh.Visible
                || targetShape is null)
            {
                return "House must retain the hidden internal-register target for the live Rinat conversation";
            }
        }

        return string.Empty;
    }
}
