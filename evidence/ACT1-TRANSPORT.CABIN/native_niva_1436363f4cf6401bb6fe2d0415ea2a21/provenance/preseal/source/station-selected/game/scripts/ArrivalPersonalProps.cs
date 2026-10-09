using Godot;

namespace Urman.Godot;

// The same home sources in connected Act I and the compact entrypoint.
// Knowledge, replies and save state remain owned by RuntimeBridge.
internal partial class ArrivalPersonalProps : Node3D
{
    private const float FamilyBookWidth = .220f;
    private const float FamilyBookThickness = .038f;
    private const float FamilyBookDepth = .285f;
    // The portrait book is turned across the table to fit the measured clear
    // patch between the paper/phone and the front edge. Runtime still verifies
    // triangle support and visible-prop clearance before creating the volume.
    private const float FamilyBookPlacementYawDegrees = 90f;
    private static readonly Vector2 FamilyBookTablePosition = new(.712f, -2.1025f);

    internal static ArrivalPersonalProps Build(StyleBenchmarkZone house, bool photoWorlds)
    {
        var table = house.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_TableTop_LOD0");
        var support = PublicBuildingShell.Bounds(house, table);
        // The right-hand end is clear of the CRT, papers and tea invitation.
        var photoAt = new Vector3(support.End.X - .57f, support.End.Y + .006f, support.End.Z - .24f);
        var phoneAt = photoAt + new Vector3(.31f, .006f, 0);
        var things = new ArrivalPersonalProps { Name = "ArrivalPersonalThings" };
        house.AddChild(things);
        var arrivalPhoto = new Node3D
        {
            Name = "ArrivalPhotoAtHome",
            Position = photoAt,
            RotationDegrees = new(-90f, 0f, -8f)
        };
        things.AddChild(arrivalPhoto);
        Box(arrivalPhoto, "PhotoPaper", new(.194f, .230f, .004f), Vector3.Zero, "d8cdb2", "paper");
        var photograph = ResourceLoader.Load<Texture2D>("res://assets/images/arrival_marat_childhood_existing_v1.png")
            ?? throw new InvalidOperationException("The authored arrival photograph has not been packaged.");
        // Keep the untouched archival scan in the reader. The world print maps
        // its paper rectangle, excluding the scanner's black surround.
        arrivalPhoto.AddChild(new MeshInstance3D
        {
            Name = "MaratChildhoodPhotograph", Position = new(0, 0, .0025f),
            Mesh = new QuadMesh { Size = new(.180f, .215f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = photograph, Roughness = 1f,
                Uv1Offset = new(142f / 1024f, 42f / 1024f, 0f),
                Uv1Scale = new(781f / 1024f, 933f / 1024f, 1f),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            }
        });
        var photoTargetSize = photoWorlds ? new Vector3(.24f, .14f, .28f) : new Vector3(.38f, .24f, .32f);
        house.MakeInteractionBox("ArrivalPhotoTarget", photoTargetSize,
            photoAt + Vector3.Up * .06f, "665b49", "urman.chapter1:interaction/view-arrival-photo",
            "Посмотреть фотографию Марата",
            documentId: "urman.chapter1:document/arrival-photo-evidence",
            rayOnly: true);
        var phone = new Node3D { Name = "ArrivalPhone", Position = phoneAt };
        things.AddChild(phone);
        Box(phone, "Case", new(.080f, .016f, .158f), Vector3.Zero, "252b2b", "metal");
        Box(phone, "Screen", new(.070f, .002f, .139f), new(0f, .009f, 0f), "bac8c4");
        Box(phone, "Speaker", new(.018f, .002f, .003f), new(0f, .0105f, -.062f), "222928");
        Box(phone, "Message", new(.052f, .002f, .034f), new(-.004f, .011f, -.019f), "738f81");
        Box(phone, "MessageLine", new(.037f, .002f, .004f), new(-.003f, .0122f, -.019f), "d7e2d7");
        foreach (var reply in new[] { false, true })
            house.MakeInteractionBox(reply ? "ArrivalMotherReplyTarget" : "ArrivalPhoneTarget", new(.34f, .24f, .32f),
                phoneAt + Vector3.Up * .06f, "252b2b",
                reply ? "urman.chapter1:interaction/arrival-answer-mother" : "urman.chapter1:interaction/view-arrival-message",
                reply ? "Ответить маме" : "Сообщение от мамы",
                dialogueId: reply ? "urman.chapter1:dialogue/arrival_mother_reply" : "",
                documentId: reply ? "" : "urman.chapter1:document/arrival-mother-message", rayOnly: true);
        return things;
    }

    internal static void AddFamilyBook(StyleBenchmarkZone house, bool photoWorlds)
    {
        // The old-PC presentation imports a second copy of the modular kit after
        // this room has been built. Resolve the actual household table by its
        // owning furniture group, not by a recursive name match across both kits.
        var table = house.GetNode<Node3D>("GeneratedHouseInteriorAct1")
            .FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_TableTop_LOD0");
        var support = PublicBuildingShell.Bounds(house, table);
        var faceTransform = house.GlobalTransform.AffineInverse() * table.GlobalTransform;
        var faces = table.Mesh!.GetFaces().Select(face => faceTransform * face).ToArray();
        // The local .220 × .285 portrait footprint is yawed 90° in the room,
        // so its room-space X/Z extents are .285 × .220.
        var footprint = new[]
        {
            new Vector2(FamilyBookTablePosition.X - FamilyBookDepth * .5f, FamilyBookTablePosition.Y - FamilyBookWidth * .5f),
            new Vector2(FamilyBookTablePosition.X + FamilyBookDepth * .5f, FamilyBookTablePosition.Y - FamilyBookWidth * .5f),
            new Vector2(FamilyBookTablePosition.X - FamilyBookDepth * .5f, FamilyBookTablePosition.Y + FamilyBookWidth * .5f),
            new Vector2(FamilyBookTablePosition.X + FamilyBookDepth * .5f, FamilyBookTablePosition.Y + FamilyBookWidth * .5f),
            FamilyBookTablePosition
        };
        var supportHeights = footprint.Select(point => TableTopHeightAt(
            point, faces, support.Position.Y - .01f, support.End.Y + .01f)).ToArray();
        if (supportHeights.Any(float.IsNaN)
            || supportHeights.Max() - supportHeights.Min() > .002f)
            throw new InvalidOperationException("The family book footprint is not fully supported by the measured TableTop surface.");

        const float clothSurfaceOffset = .004f;
        const float clearance = .001f;
        var clothTop = supportHeights.Max() + clothSurfaceOffset;
        var bookBounds = new Aabb(
            new(FamilyBookTablePosition.X - FamilyBookDepth * .5f,
                clothTop + clearance, FamilyBookTablePosition.Y - FamilyBookWidth * .5f),
            new(FamilyBookDepth, FamilyBookThickness, FamilyBookWidth));
        foreach (var mesh in house.FindChildren("*", nameof(MeshInstance3D), true, false)
                     .OfType<MeshInstance3D>()
                     .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()
                         && mesh.Name != "HouseInterior_TableTop_LOD0" && mesh.Name != "TeaTablecloth"))
        {
            var occupied = PublicBuildingShell.Bounds(house, mesh);
            if (Overlaps(bookBounds, occupied))
                throw new InvalidOperationException($"The family book footprint overlaps existing home prop {mesh.GetPath()}.");
        }

        var book = new Node3D
        {
            Name = "FamilyTukayBookPresentation",
            Position = new(FamilyBookTablePosition.X,
                clothTop + clearance + FamilyBookThickness * .5f, FamilyBookTablePosition.Y),
            RotationDegrees = new(0f, FamilyBookPlacementYawDegrees, 0f)
        };
        book.SetMeta("geometryOwner", nameof(ArrivalPersonalProps));
        book.SetMeta("presentationRole", "closed family literary volume; muted cloth cover and visible page block; no invented cover text");
        book.SetMeta("dimensionsMetres", new Vector3(FamilyBookWidth, FamilyBookThickness, FamilyBookDepth));
        book.SetMeta("supportMesh", table.GetPath().ToString());

        var things = house.GetNodeOrNull<ArrivalPersonalProps>("ArrivalPersonalThings")
            ?? throw new InvalidOperationException("Home props must be built before the family book is placed.");
        things.AddChild(book);
        var cloth = RuralPropMaterials.Surface("cloth", "674b45");
        var paper = PainterlyMaterialLibrary.ForColor("d6ccb3", "paper", sheltered: true);
        AddBookPart(book, "LowerClothBoard", new(FamilyBookWidth, .004f, FamilyBookDepth),
            new(0f, -.017f, 0f), cloth, .0015f);
        AddBookPart(book, "RecessedPageBlock", new(FamilyBookWidth - .010f, .028f, FamilyBookDepth - .010f),
            Vector3.Zero, paper, .001f);
        AddBookPart(book, "UpperClothBoard", new(FamilyBookWidth, .004f, FamilyBookDepth),
            new(0f, .017f, 0f), cloth, .0015f);
        AddBookPart(book, "ClothSpine", new(.020f, .030f, FamilyBookDepth),
            new(-FamilyBookWidth * .5f + .010f, 0f, 0f), cloth, .004f);

        if (!photoWorlds) return;

        house.MakeInteractionBox("PhotoWorldFamilyBookTarget", new(.30f, .08f, .24f),
            book.Position, "674b45", "urman.fullgame:interaction/pw-book-handoff",
            "Принять семейную книгу от Гөлсинә", rayOnly: true);
    }

    private static void AddBookPart(Node3D book, string name, Vector3 size, Vector3 at,
        Material material, float bevel)
    {
        var part = new MeshInstance3D
        {
            Name = name,
            Position = at,
            Mesh = RuralPropGeometry.BevelBox(size, bevel),
            MaterialOverride = material
        };
        part.SetMeta("visualOnly", true);
        book.AddChild(part);
    }

    private static float TableTopHeightAt(Vector2 point, Vector3[] faces, float bottom, float top)
    {
        var from = new Vector3(point.X, top, point.Y);
        var to = new Vector3(point.X, bottom, point.Y);
        var highest = float.NegativeInfinity;
        for (var index = 0; index + 2 < faces.Length; index += 3)
        {
            var intersection = Geometry3D.SegmentIntersectsTriangle(from, to,
                faces[index], faces[index + 1], faces[index + 2]);
            if (intersection.VariantType != Variant.Type.Nil)
                highest = Math.Max(highest, intersection.AsVector3().Y);
        }
        return float.IsNegativeInfinity(highest) ? float.NaN : highest;
    }

    private static bool Overlaps(Aabb first, Aabb second) =>
        first.Position.X < second.End.X && first.End.X > second.Position.X
        && first.Position.Y < second.End.Y && first.End.Y > second.Position.Y
        && first.Position.Z < second.End.Z && first.End.Z > second.Position.Z;

    private static void Box(Node3D parent, string name, Vector3 size, Vector3 at, string color, string surface = "")
    {
        var mesh = new MeshInstance3D
        {
            Name = name, Position = at, Mesh = new BoxMesh { Size = size },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface)
        };
        mesh.SetMeta("visualOnly", true);
        parent.AddChild(mesh);
    }
}
