using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private readonly List<StaticBody3D> _babaiWindowBodies = new();
    private readonly List<MeshInstance3D> _babaiWindowPanes = new();
    private Material? _babaiClearWindowGlass;

    private bool BabaiWindowsActive => FacilityExteriorActive || ActiveZoneId == "house_old_pc";

    // Centre is the wall opening's centre; local +Z faces outdoors. The wall
    // builder owns the opening, and the parcel dresser owns carved surrounds.
    private void BuildBabaiActiveWindow(Node3D parent, string name, Vector3 center, float yaw,
        float width = 1.06f, float height = 1.4f)
    {
        if (string.IsNullOrWhiteSpace(name) || width < .44f || height < .44f)
            throw new ArgumentException("A house window needs a stable name and a usable metric opening.");
        var key = "babai-window-" + name;
        if (_facilityDoors.Any(door => door.Key == key))
            throw new InvalidOperationException("Duplicate active house window: " + name);
        var root = new Node3D { Name = "BabaiActiveWindow" + name, Position = center,
            RotationDegrees = new(0, yaw, 0) };
        parent.AddChild(root);
        root.SetMeta("worldPropId", key);
        root.SetMeta("clearOpening", new Vector2(width, height));
        root.SetMeta("runtimeStateOwner", "RuntimeBridge/world.props");
        root.SetMeta("openingOutward", Vector3.Back);

        const float frame = .06f;
        var frameParts = new (Vector3 Size, Vector3 At)[]
        {
            (new(frame, height, .12f), new((-width + frame) * .5f, 0, 0)),
            (new(frame, height, .12f), new((width - frame) * .5f, 0, 0)),
            (new(width - frame * 2, frame, .12f), new(0, (-height + frame) * .5f, 0)),
            (new(width - frame * 2, frame, .12f), new(0, (height - frame) * .5f, 0)),
            // The fixed mullion leaves two genuine views but no player-width
            // passage bypassing the house's existing entrance interaction.
            (new(.038f, height - frame * 2, .12f), Vector3.Zero),
            (new(width + .10f, .034f, .24f), new(0, -height * .5f - .017f, -.055f))
        };
        var wood = PainterlyMaterialLibrary.ForColor("d7d2bd", "wood_painted_trim");
        BabaiWindowPieces(root, "Babai_Window" + name + "_StructuralFrame", frameParts, wood);
        var frameBody = new StaticBody3D { Name = "FrameContacts", CollisionLayer = 2, CollisionMask = 0 };
        foreach (var part in frameParts)
            frameBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = part.Size }, Position = part.At });
        root.AddChild(frameBody);
        RegisterBabaiWindowBody(frameBody, key);

        var leafWidth = width - frame * 2 - .024f;
        var leafHeight = height - frame * 2 - .024f;
        // The hinge lies behind the inner jamb face. The full swept 65 mm
        // contact stays clear of the frame even at the hinge corner; the sash
        // opens into the room, never through the external casing or wall.
        var hinge = new Node3D { Name = "SashHinge", Position = new(-leafWidth * .5f, -leafHeight * .5f, -.10f),
            RotationDegrees = new(0, 90, 0) };
        root.AddChild(hinge);
        const float rail = .045f;
        BabaiWindowPieces(hinge, "SashJoinery", new (Vector3 Size, Vector3 At)[]
        {
            (new(.048f, leafHeight, rail), new(0, leafHeight * .5f, rail * .5f)),
            (new(.048f, leafHeight, rail), new(0, leafHeight * .5f, leafWidth - rail * .5f)),
            (new(.048f, rail, leafWidth - rail * 2), new(0, rail * .5f, leafWidth * .5f)),
            (new(.048f, rail, leafWidth - rail * 2), new(0, leafHeight - rail * .5f, leafWidth * .5f)),
            (new(.040f, .026f, leafWidth - rail * 2), new(0, leafHeight * .73f, leafWidth * .5f))
        }, wood);
        _babaiClearWindowGlass ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(.76f, .84f, .87f, .11f), Roughness = .19f, MetallicSpecular = .48f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var pane = new MeshInstance3D { Name = "Babai_Window" + name + "_Glass",
            Mesh = new QuadMesh { Size = new(leafWidth - rail * 2, leafHeight - rail * 2) },
            Position = new(0, leafHeight * .5f, leafWidth * .5f), RotationDegrees = new(0, -90, 0),
            MaterialOverride = _babaiClearWindowGlass, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        pane.SetMeta("babaiOperableGlass", true);
        pane.SetMeta("worldPropId", key);
        hinge.AddChild(pane);
        _babaiWindowPanes.Add(pane);

        var brass = PainterlyMaterialLibrary.ForColor("837655", "metal");
        foreach (var y in new[] { leafHeight * .22f, leafHeight * .78f })
        {
            var pin = new MeshInstance3D { Name = "HingePin",
                Mesh = new CylinderMesh { TopRadius = .008f, BottomRadius = .008f,
                    Height = .080f, RadialSegments = 8, Rings = 1 },
                Position = hinge.Position + new Vector3(0, y, 0), MaterialOverride = brass };
            root.AddChild(pin);
            BabaiWindowPieces(root, "FixedHingeStrap", new (Vector3 Size, Vector3 At)[]
            {
                (new(.07f, .030f, .036f), hinge.Position + new Vector3(-.026f, y, .018f))
            }, brass);
            BabaiWindowPieces(hinge, "SashHingeStrap", new (Vector3 Size, Vector3 At)[]
            {
                (new(.010f, .030f, .066f), new(-.028f, y, .028f))
            }, brass);
        }
        foreach (var side in new[] { -1f, 1f })
        {
            BabaiWindowPieces(hinge, side > 0 ? "InnerLatch" : "OuterPull", new (Vector3 Size, Vector3 At)[]
            {
                (new(.012f, .105f, .030f), new(side * .030f, leafHeight * .47f, leafWidth - .073f)),
                (new(.038f, .018f, .018f), new(side * .049f, leafHeight * .47f - .035f, leafWidth - .073f)),
                (new(.038f, .018f, .018f), new(side * .049f, leafHeight * .47f + .035f, leafWidth - .073f)),
                (new(.016f, .105f, .020f), new(side * .069f, leafHeight * .47f, leafWidth - .073f))
            }, brass);
        }
        var body = new StaticBody3D { Name = "SashContact", CollisionLayer = 2, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Name = "GlazingAndJoinery",
            Shape = new BoxShape3D { Size = new(.065f, leafHeight - .012f, leafWidth) },
            Position = new(0, (leafHeight + .012f) * .5f, leafWidth * .5f) });
        hinge.AddChild(body);
        RegisterBabaiWindowBody(body, key);

        var target = new InteractionTarget { Name = "WindowUse", InteractionId = "urman.chapter1:local/" + key,
            Prompt = "Открыть окно", CollisionLayer = 4, CollisionMask = 0 };
        target.AddChild(new CollisionShape3D { Name = "SurfaceRay",
            Shape = new BoxShape3D { Size = new(.115f, leafHeight, leafWidth + .035f) } });
        var window = new FacilityDoor { Key = key, Hinge = hinge, Body = body, Target = target,
            Width = leafWidth, Height = leafHeight, RestYaw = Mathf.Pi * .5f,
            OpenYaw = Mathf.DegToRad(168), IsWindow = true,
            Active = () => BabaiWindowsActive && IsInstanceValid(root) && root.IsVisibleInTree() };
        target.PresentationRepeatAvailable = () => FacilityDoorActive(window);
        target.PresentationGate = () => FacilityDoorActive(window);
        target.PresentationRepeat = () => _ = ToggleFacilityDoor(window);
        target.SetMeta("runtimeStateOwner", "RuntimeBridge");
        target.SetMeta("facilityTarget", true);
        root.AddChild(target);
        target.GlobalTransform = hinge.GlobalTransform * new Transform3D(Basis.Identity,
            new(0, leafHeight * .5f, leafWidth * .5f));
        _facilityDoors.Add(window);
        _facilityProjectionApplied = false;
        EnsureFacilityTick();
    }

    private void RegisterBabaiWindowBody(StaticBody3D body, string key)
    {
        body.SetMeta("collisionOwner", "babai-active-window");
        body.SetMeta("worldPropId", key);
        _babaiWindowBodies.Add(body);
    }

    private void ProjectBabaiWindowContacts()
    {
        foreach (var body in _babaiWindowBodies)
            if (IsInstanceValid(body)) body.CollisionLayer = BabaiWindowsActive ? 2u : 0u;
    }

    // DressParcel measures these panes for carved surrounds, then replaces all
    // residential glass with its occupied-room shader. Call after dressing so
    // the operable windows retain a real transparent view in both directions.
    private void RestoreBabaiActiveWindowGlass()
    {
        foreach (var pane in _babaiWindowPanes)
            if (IsInstanceValid(pane)) pane.MaterialOverride = _babaiClearWindowGlass;
    }

    private static void BabaiWindowPieces(Node3D owner, string name,
        IEnumerable<(Vector3 Size, Vector3 At)> pieces, Material material)
    {
        using var surface = new SurfaceTool();
        using var cube = new BoxMesh { Size = Vector3.One };
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var part in pieces)
            surface.AppendFrom(cube, 0, new Transform3D(Basis.Identity.Scaled(part.Size), part.At));
        owner.AddChild(new MeshInstance3D { Name = name, Mesh = surface.Commit(), MaterialOverride = material });
    }
}
