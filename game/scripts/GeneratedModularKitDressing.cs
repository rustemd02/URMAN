using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only bridge from the project-original Blender kit to an authored
/// full-game zone. It deliberately does not create physics bodies or mutate
/// narrative state: the greybox zone keeps ownership of walkability and the
/// runtime kernel keeps ownership of story state.
/// </summary>
public static class GeneratedModularKitDressing
{
    public const string ScenePath = "res://assets/generated/urman_modular_kit.glb";

    private const float Lod0End = 24f;
    private const float Lod0EndMargin = 3f;
    private const float Lod1Begin = 18f;
    private const float Lod1BeginMargin = 3f;
    private const float Lod1End = 72f;
    private const float Lod1EndMargin = 6f;

    public static Node3D Attach(Node3D parent, string variant, string[] visiblePrefixes, Vector3 anchor)
    {
        return AttachInternal(
            parent,
            variant,
            visiblePrefixes,
            anchor,
            uniformScale: 1f,
            yawDegrees: 0f,
            attachProvisionalCollider: true);
    }

    public static Node3D AttachPresentationOnly(
        Node3D parent,
        string variant,
        string[] visiblePrefixes,
        Vector3 anchor,
        float uniformScale,
        float yawDegrees)
    {
        if (uniformScale <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(uniformScale), uniformScale, "Presentation scale must be positive.");
        }

        return AttachInternal(
            parent,
            variant,
            visiblePrefixes,
            anchor,
            uniformScale,
            yawDegrees,
            attachProvisionalCollider: false);
    }

    private static Node3D AttachInternal(
        Node3D parent,
        string variant,
        string[] visiblePrefixes,
        Vector3 anchor,
        float uniformScale,
        float yawDegrees,
        bool attachProvisionalCollider)
    {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            throw new InvalidOperationException($"Generated modular kit could not be loaded: {ScenePath}");
        }

        var instance = packed.Instantiate<Node3D>();
        RemoveImportedCollisionNodes(instance);
        instance.Name = "GeneratedModularKit";
        instance.SetMeta("assetSource", ScenePath);
        instance.SetMeta("variant", variant);
        instance.SetMeta("lodPolicy", "LOD0 0-24m; LOD1 18-72m; self-fade");
        instance.SetMeta("collisionPolicy", attachProvisionalCollider
            ? "imported -col render meshes hidden; provisional layer-2 kit colliders; interaction targets remain layer 1"
            : "imported -col render meshes hidden; presentation-only instance has no physics body; interaction targets remain layer 1");
        instance.Scale = Vector3.One * uniformScale;
        instance.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
        parent.AddChild(instance);

        var meshes = instance
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .ToArray();
        if (meshes.Length == 0)
        {
            instance.QueueFree();
            throw new InvalidOperationException($"Generated modular kit has no MeshInstance3D nodes: {ScenePath}");
        }

        var selected = meshes
            .Where(mesh => visiblePrefixes.Any(prefix => NodeName(mesh).StartsWith(prefix, StringComparison.Ordinal)))
            // The Blender/Godot import may retain a source collision render mesh
            // under a shortened name (for example HouseA rather than HouseA-col).
            // Published environment modules are the explicit LOD pairs only;
            // treating every non-LOD descendant as hidden keeps that imported
            // helper out of both the visible and material-owning contracts.
            .Where(IsPublishedLodMesh)
            .ToArray();
        if (selected.Length == 0)
        {
            instance.QueueFree();
            throw new InvalidOperationException($"Generated modular kit variant '{variant}' selected no meshes.");
        }

        foreach (var mesh in meshes)
        {
            var isSelected = selected.Contains(mesh);
            var isCollision = NodeName(mesh).EndsWith("-col", StringComparison.Ordinal)
                              || !IsPublishedLodMesh(mesh);
            mesh.Visible = isSelected && !isCollision;
            mesh.SetMeta("generatedKitVisible", mesh.Visible);
            mesh.SetMeta("generatedKitCollision", isCollision);
            if (!mesh.Visible)
            {
                continue;
            }

            ConfigureVisibilityRange(mesh);
            ApplyPainterlyMaterial(mesh);
        }

        var lod0 = selected.Count(mesh => mesh.Visible && NodeName(mesh).Contains("_LOD0", StringComparison.Ordinal));
        var lod1 = selected.Count(mesh => mesh.Visible && NodeName(mesh).Contains("_LOD1", StringComparison.Ordinal));
        if (lod0 == 0 || lod1 == 0)
        {
            instance.QueueFree();
            throw new InvalidOperationException($"Generated modular kit variant '{variant}' requires both LOD0 and LOD1 (got {lod0}/{lod1}).");
        }

        instance.SetMeta("visibleMeshCount", selected.Count(mesh => mesh.Visible));
        instance.SetMeta("lod0Count", lod0);
        instance.SetMeta("lod1Count", lod1);
        instance.SetMeta("generatedKitStatus", "godot-visibility-ranges-integrated");

        var anchorPrefix = visiblePrefixes[0];
        var reference = selected.FirstOrDefault(mesh =>
            NodeName(mesh).StartsWith(anchorPrefix, StringComparison.Ordinal)
            && NodeName(mesh).Contains("_LOD0", StringComparison.Ordinal))
            ?? selected.First(mesh => NodeName(mesh).Contains("_LOD0", StringComparison.Ordinal));
        AlignAnchor(instance, reference, anchor);
        if (attachProvisionalCollider)
        {
            AttachProvisionalColliders(instance, variant, reference);
        }
        else
        {
            instance.SetMeta("collisionShapeCount", 0);
            instance.SetMeta("presentationOnlyInstance", true);
        }

        return instance;
    }

    private static void RemoveImportedCollisionNodes(Node3D instance)
    {
        var collisionNodes = instance
            .FindChildren("*", string.Empty, recursive: true, owned: false)
            .Where(node => node is CollisionObject3D or CollisionShape3D)
            .OrderByDescending(NodeDepth)
            .ToArray();
        var collisionObjectCount = collisionNodes.Count(node => node is CollisionObject3D);
        var collisionShapeCount = collisionNodes.Count(node => node is CollisionShape3D);

        foreach (var node in collisionNodes)
        {
            if (node is CollisionShape3D collisionShape)
            {
                // Imported GLB shapes are disposable presentation helpers. A
                // native Shape3D resource can be shared by PackedScene
                // instances. Detach it before freeing the node and let
                // Godot's ref-counting release the shared RID; manually
                // disposing it here leaks the final shared allocation.
                collisionShape.Shape = null;
            }
            else if (node is CollisionObject3D collisionObject)
            {
                collisionObject.CollisionLayer = 0;
                collisionObject.CollisionMask = 0;
            }

            node.Free();
        }

        instance.SetMeta("importedCollisionObjectsRemoved", collisionObjectCount);
        instance.SetMeta("importedCollisionShapesRemoved", collisionShapeCount);
    }

    private static int NodeDepth(Node node)
    {
        var depth = 0;
        for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
        {
            depth++;
        }

        return depth;
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
        }
        else
        {
            mesh.VisibilityRangeBegin = 0f;
            mesh.VisibilityRangeBeginMargin = 0f;
            mesh.VisibilityRangeEnd = Lod0End;
            mesh.VisibilityRangeEndMargin = Lod0EndMargin;
        }

        mesh.SetMeta("visibilityRange", NodeName(mesh).Contains("_LOD1", StringComparison.Ordinal)
            ? "18-72m"
            : "0-24m");
    }

    private static void ApplyPainterlyMaterial(MeshInstance3D mesh)
    {
        var name = NodeName(mesh);
        mesh.SetMeta("presentationOwnership", "presentation-only");
        mesh.SetMeta("collisionPolicy", "no physics body; visual mesh only");
        var (color, surface) = name switch
        {
            _ when name.StartsWith("HouseA_Walls", StringComparison.Ordinal) => ("806d58", "plaster"),
            _ when name.StartsWith("HouseA_Roof", StringComparison.Ordinal) => ("4d3e34", "roof"),
            _ when name.StartsWith("HouseA_Foundation", StringComparison.Ordinal) => ("4b4037", "stone"),
            _ when name.StartsWith("HouseA_Door", StringComparison.Ordinal) => ("4d392f", "wood_facade"),
            _ when name.StartsWith("HouseA_Porch", StringComparison.Ordinal) => ("6a4d38", "wood_facade"),
            _ when name.StartsWith("HouseA_FrontEave", StringComparison.Ordinal) => ("45352d", "wood_facade"),
            _ when name.StartsWith("HouseA_WindowFrame", StringComparison.Ordinal) => ("725344", "wood_facade"),
            _ when name.StartsWith("HouseA_WindowGlow", StringComparison.Ordinal) => ("d89b58", string.Empty),
            _ when name.StartsWith("HouseA_WindowTrim", StringComparison.Ordinal) => ("8a6b50", "wood_facade"),
            _ when name.StartsWith("FenceA_", StringComparison.Ordinal) => ("5c4737", "wood_fence"),
            _ when name.StartsWith("WellA_Rim", StringComparison.Ordinal) => ("5a5c50", "stone"),
            _ when name.StartsWith("WellA_Water", StringComparison.Ordinal) => ("2f4f50", string.Empty),
            _ when name.StartsWith("WellA_", StringComparison.Ordinal) => ("6a4d38", "wood_prop"),
            _ when name.StartsWith("WoodpileA_", StringComparison.Ordinal) => ("4b3b2f", "wood_bark"),
            _ when name.StartsWith("GateA_Ribbon", StringComparison.Ordinal) => ("9a8d78", "cloth"),
            _ when name.StartsWith("GateA_", StringComparison.Ordinal) => ("5c4737", "wood_fence"),
            _ when name.StartsWith("RoadDirt_", StringComparison.Ordinal) => ("75604b", "earth"),
            _ when name.StartsWith("PineA_Trunk", StringComparison.Ordinal) => ("40352d", "bark_pine"),
            _ when name.StartsWith("PineA_Crown", StringComparison.Ordinal) => ("263a35", "foliage"),
            _ when name.StartsWith("TableA_", StringComparison.Ordinal) => ("57402e", "wood_furniture"),
            _ when name.StartsWith("HouseInterior_FloorBoard", StringComparison.Ordinal) => ("57483b", "wood"),
            _ when name.StartsWith("HouseInterior_BaseTrim", StringComparison.Ordinal) => ("493629", "wood"),
            _ when name.StartsWith("HouseInterior_CeilingField", StringComparison.Ordinal) => ("695746", "wood"),
            _ when name.StartsWith("HouseInterior_CeilingBeam", StringComparison.Ordinal) => ("493629", "wood"),
            _ when name.StartsWith("HouseInterior_BackWall", StringComparison.Ordinal) => ("827461", "log_wall"),
            _ when name.StartsWith("HouseInterior_LeftWallWainscotField", StringComparison.Ordinal) => ("6a4d38", "wood_furniture"),
            _ when name.StartsWith("HouseInterior_LeftWallWainscotRail", StringComparison.Ordinal) => ("493629", "wood_furniture"),
            _ when name.StartsWith("HouseInterior_LeftWallCupboard", StringComparison.Ordinal) => ("68503a", "wood"),
            _ when name.StartsWith("HouseInterior_LeftWall", StringComparison.Ordinal) => ("786b5a", "wallpaper"),
            _ when name.StartsWith("HouseInterior_RightWall", StringComparison.Ordinal) => ("786b5a", "wallpaper"),
            _ when name.StartsWith("HouseInterior_FrontWallLintel", StringComparison.Ordinal) => ("624936", "wood_furniture"),
            _ when name.StartsWith("HouseInterior_FrontWall", StringComparison.Ordinal) => ("827461", "log_wall"),
            _ when name.StartsWith("HouseInterior_EntryDoor", StringComparison.Ordinal) => ("4b382c", "wood"),
            _ when name.StartsWith("HouseInterior_EntryFrame", StringComparison.Ordinal) => ("5d4a38", "wood"),
            _ when name.StartsWith("HouseInterior_EntryThreshold", StringComparison.Ordinal) => ("574636", "wood"),
            _ when name.StartsWith("HouseInterior_WindowRecess", StringComparison.Ordinal) => ("786b5a", "plaster"),
            _ when name.StartsWith("HouseInterior_WindowGlass", StringComparison.Ordinal) => ("617e72", string.Empty),
            _ when name.StartsWith("HouseInterior_WindowFrame", StringComparison.Ordinal) => ("725344", "wood"),
            _ when name.StartsWith("HouseInterior_WindowMuntin", StringComparison.Ordinal) => ("725344", "wood"),
            _ when name.StartsWith("HouseInterior_WindowSill", StringComparison.Ordinal) => ("725344", "wood"),
            _ when name.StartsWith("HouseInterior_HearthBase", StringComparison.Ordinal) => ("65655b", "stone"),
            // This is the existing compact metal heater on a stone hearth,
            // not a masonry stove. Its shell, door and flue share iron.
            _ when name.StartsWith("HouseInterior_HearthBody", StringComparison.Ordinal) => ("555950", "iron"),
            _ when name.StartsWith("HouseInterior_HearthTop", StringComparison.Ordinal) => ("65655b", "iron"),
            _ when name.StartsWith("HouseInterior_HearthDoor", StringComparison.Ordinal) => ("343936", "iron"),
            _ when name.StartsWith("HouseInterior_HearthHandle", StringComparison.Ordinal) => ("493e35", "iron"),
            _ when name.StartsWith("HouseInterior_HearthFlue", StringComparison.Ordinal) => ("3f433f", "iron"),
            _ when name.StartsWith("HouseInterior_LeftWallCupboard", StringComparison.Ordinal) => ("68503a", "wood"),
            _ when name.StartsWith("HouseInterior_OldPcBackboard", StringComparison.Ordinal) => ("4b382f", "wood_furniture_interior"),
            _ when name.StartsWith("HouseInterior_OldPcHutchCleat", StringComparison.Ordinal) => ("6a4d38", "wood_furniture_interior"),
            _ when name.StartsWith("HouseInterior_OldPcHutchShelf", StringComparison.Ordinal) => ("76533a", "wood_furniture_interior"),
            _ when name.StartsWith("HouseInterior_OldPcHutchTop", StringComparison.Ordinal) => ("493629", "wood_furniture_interior"),
            _ when name.StartsWith("HouseInterior_OldPcHutchFolder", StringComparison.Ordinal) => ("586760", "fabric"),
            _ when name.StartsWith("HouseInterior_OldPcDocumentFolio", StringComparison.Ordinal) => ("c5b58f", "paper"),
            _ when name.StartsWith("HouseInterior_TableKettleLid", StringComparison.Ordinal) => ("6d7065", "enamel"),
            _ when name.StartsWith("HouseInterior_TableKettle", StringComparison.Ordinal) => ("817d6b", "enamel"),
            _ when name.StartsWith("HouseInterior_TableBowl", StringComparison.Ordinal) => ("9a907b", "plaster"),
            _ when name.StartsWith("HouseInterior_Table", StringComparison.Ordinal) => ("57402e", "wood_furniture_interior"),
            _ when name.StartsWith("HouseInterior_StorageBasket", StringComparison.Ordinal) => ("74593d", "wood_prop"),
            _ when name.StartsWith("HouseInterior_Chair", StringComparison.Ordinal) => ("684b37", "wood"),
            _ when name.StartsWith("HouseInterior_Cupboard", StringComparison.Ordinal) => ("70563e", "wood"),
            _ when name.StartsWith("HouseInterior_RugField", StringComparison.Ordinal) => ("714939", "carpet"),
            _ when name.StartsWith("HouseInterior_RugBandA", StringComparison.Ordinal) => ("8d765c", "carpet"),
            _ when name.StartsWith("HouseInterior_RugBandB", StringComparison.Ordinal) => ("45615a", "carpet"),
            _ when name.StartsWith("HouseInterior_DaybedFrame", StringComparison.Ordinal) => ("624936", "wood_furniture_interior"),
            _ when name.StartsWith("HouseInterior_DaybedCushion", StringComparison.Ordinal) => ("6e675d", "fabric_upholstery"),
            _ when name.StartsWith("HouseInterior_DaybedBack", StringComparison.Ordinal) => ("59605b", "fabric_upholstery"),
            _ when name.StartsWith("HouseInterior_StorageChest", StringComparison.Ordinal) => ("604533", "wood_furniture_interior"),
            _ when name.StartsWith("HouseInterior_RightShelf", StringComparison.Ordinal) => ("584434", "wood"),
            _ when name.StartsWith("HouseInterior_ShelfVessel", StringComparison.Ordinal) => ("817d6b", "plaster"),
            _ when name.StartsWith("HouseInterior_RightRunner", StringComparison.Ordinal) => ("586760", "fabric"),
            _ when name.StartsWith("OldPc_Crt", StringComparison.Ordinal) => ("858274", "plastic_abs"),
            _ when name.StartsWith("OldPc_Glass", StringComparison.Ordinal) => ("617e72", string.Empty),
            _ when name.StartsWith("OldPc_Keyboard", StringComparison.Ordinal) => ("817d6b", "plastic_abs"),
            _ when name.StartsWith("OldPc_TowerPanel", StringComparison.Ordinal) => ("29332e", string.Empty),
            _ when name.StartsWith("OldPc_Tower", StringComparison.Ordinal) => ("858274", "plastic_abs"),
            _ when name.StartsWith("OldPc_PowerButton", StringComparison.Ordinal) => ("b67a42", string.Empty),
            _ when name.StartsWith("OldPc_DriveSlot", StringComparison.Ordinal) => ("29332e", string.Empty),
            _ when name.StartsWith("OldPc_LabelPlate", StringComparison.Ordinal) => ("817d6b", string.Empty),
            _ => (string.Empty, string.Empty)
        };

        if (color.Length == 0)
        {
            return;
        }

        var sheltered = name.StartsWith("HouseInterior_", StringComparison.Ordinal)
                         || name.StartsWith("OldPc_", StringComparison.Ordinal);
        mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface, sheltered);
        mesh.SetMeta("painterlyMaterial", surface.Length == 0 ? "shader" : surface);
    }

    private static string NodeName(Node node) => node.Name.ToString();

    private static bool IsPublishedLodMesh(MeshInstance3D mesh)
    {
        var name = NodeName(mesh);
        return name.Contains("_LOD0", StringComparison.Ordinal)
               || name.Contains("_LOD1", StringComparison.Ordinal);
    }

    private static void AlignAnchor(Node3D instance, MeshInstance3D referenceMesh, Vector3 anchor)
    {
        // The Blender exporter performs the Y-up conversion. Aligning by the
        // imported node position is converted to the same parent-local space
        // as the caller's anchor; connected zones need not be at world zero.
        instance.Position += anchor - instance.GetParent<Node3D>().ToLocal(referenceMesh.GlobalPosition);
        instance.SetMeta("anchor", anchor);
        instance.SetMeta("anchorReference", referenceMesh.Name);
    }

    private static void AttachProvisionalColliders(Node3D instance, string variant, MeshInstance3D reference)
    {
        var proxy = new StaticBody3D
        {
            Name = "KitCollisionProxy",
            CollisionLayer = 2,
            CollisionMask = 0
        };
        proxy.SetMeta("collisionStatus", "provisional-layer-2");
        instance.AddChild(proxy);
        proxy.Position = instance.ToLocal(reference.GlobalPosition);

        switch (variant)
        {
            case "act2-family-house":
            case "act5-epilogue-house":
            case "style-day-house":
                AddBoxCollider(proxy, "HouseBackWall", new(6.2f, 2.8f, 0.2f), new(0, 0, -2.5f));
                AddBoxCollider(proxy, "HouseFrontLeft", new(1.05f, 2.8f, 0.2f), new(-2.575f, 0, 2.5f));
                AddBoxCollider(proxy, "HouseFrontRight", new(4.15f, 2.8f, 0.2f), new(1.025f, 0, 2.5f));
                AddBoxCollider(proxy, "HouseSideLeft", new(0.2f, 2.8f, 4.8f), new(-3.0f, 0, 0));
                AddBoxCollider(proxy, "HouseSideRight", new(0.2f, 2.8f, 4.8f), new(3.0f, 0, 0));
                break;
            case "act3-soviet-old-pc":
                AddBoxCollider(proxy, "TableTop", new(3.2f, 0.14f, 1.35f), Vector3.Zero);
                AddBoxCollider(proxy, "OldPcBody", new(1.4f, 1.15f, 0.72f), new(0, 0.64f, 0));
                break;
            case "act5-boundary-forest":
            case "style-forest-pine":
                AddCylinderCollider(proxy, "PineTrunk", 0.22f, 3.2f, Vector3.Zero);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown generated kit collider variant.");
        }

        instance.SetMeta("collisionProxy", proxy.GetPath());
        instance.SetMeta("collisionShapeCount", proxy.GetChildCount());
    }

    private static void AddBoxCollider(StaticBody3D parent, string name, Vector3 size, Vector3 position)
    {
        parent.AddChild(new CollisionShape3D
        {
            Name = name,
            Position = position,
            Shape = new BoxShape3D { Size = size }
        });
    }

    private static void AddCylinderCollider(StaticBody3D parent, string name, float radius, float height, Vector3 position)
    {
        parent.AddChild(new CollisionShape3D
        {
            Name = name,
            Position = position,
            Shape = new CylinderShape3D { Radius = radius, Height = height }
        });
    }
}
