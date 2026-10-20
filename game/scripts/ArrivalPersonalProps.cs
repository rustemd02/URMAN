using Godot;
using System.Threading.Tasks;

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
        var cloth = (StandardMaterial3D)RuralPropMaterials.Surface("cloth", "674b45").Duplicate();
        cloth.ResourceName = "FamilyBook_ClothMetric80mm";
        cloth.Uv1Scale = new(12.5f, 12.5f, 1f);
        cloth.SetMeta("surfaceTileMetres", .08f);
        var paper = (ShaderMaterial)PainterlyMaterialLibrary.ForColor("d6ccb3", "paper", sheltered: true).Duplicate();
        const string paperTexturePath = "res://assets/textures/painterly/book_paper_fibers_v1_albedo.png";
        if (ResourceLoader.Exists(paperTexturePath)
            && ResourceLoader.Load<Texture2D>(paperTexturePath) is { } paperTexture)
        {
            paper.SetShaderParameter("albedo_texture", paperTexture);
            paper.SetShaderParameter("has_albedo_texture", true);
            paper.SetShaderParameter("authored_uv_texture", true);
            paper.SetShaderParameter("texture_scale", new Vector2(12.5f, 12.5f));
            // The shared shader amplifies pale albedo; keep the book's quiet ivory tint.
            paper.SetShaderParameter("texture_strength", .10f);
            paper.SetMeta("sourceTexture", paperTexturePath);
            paper.SetMeta("surfaceTileMetres", .08f);
        }
        AddBookPart(book, "LowerClothBoard", new(FamilyBookWidth, .004f, FamilyBookDepth),
            new(0f, -.017f, 0f), cloth, .0015f);
        var pageBlock = RuralPropGeometry.Part(book, "RecessedPageBlock",
            RuralPropGeometry.BevelBox(new(FamilyBookWidth - .010f, .028f, FamilyBookDepth - .010f), .001f),
            Vector3.Zero, paper);
        pageBlock.SetMeta("visualOnly", true);
        var upperClothBoard = new MeshInstance3D
        {
            Name = "UpperClothBoard",
            Position = new(0f, .0162f, 0f),
            RotationDegrees = new(-90f, 0f, 0f),
            Mesh = RuralPropGeometry.BowedPanel(FamilyBookWidth, FamilyBookDepth, .004f, .0015f, .0008f),
            MaterialOverride = cloth
        };
        upperClothBoard.SetMeta("visualOnly", true);
        book.AddChild(upperClothBoard);
        AddBookPart(book, "ClothSpine", new(.020f, .030f, FamilyBookDepth),
            new(-FamilyBookWidth * .5f + .010f, 0f, 0f), cloth, .004f);
        ReplaceWithAuthoredFamilyBook(book, cloth, paper);

        if (!photoWorlds) return;

        book.AddChild(new FamilyBookTablePresence { Name = "PhotoWorldFamilyBookTablePresence" });

        var target = house.MakeInteractionBox("PhotoWorldFamilyBookTarget", new(.30f, .08f, .24f),
            book.Position, "674b45", "urman.fullgame:interaction/pw-book-handoff",
            "Принять семейную книгу от Гөлсинә", rayOnly: true);
        target.AfterDispatch = async () =>
        {
            var tree = house.GetTree();
            if (tree.GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge
                && tree.GetFirstNodeInGroup("journal_ui") is JournalUi journal)
                journal.OpenPhotoBook(bridge);
            return await Task.FromResult(true);
        };

        house.MakeInteractionBox("PhotoWorldForestEntryTarget", new(.30f, .08f, .24f),
            book.Position, "4f5148",
            "urman.fullgame:interaction/pw-enter-forest", "Вернуться к лесному снимку", rayOnly: true);

        var waterChain = BuildWaterChain(house, things, paper);
        BuildHomePrinter(house, things, paper);
        BuildHousePhotoChain(house, things, paper, waterChain);
        BuildCellarPhotoChain(house, things, paper, waterChain);
    }

    // The family envelope with the second print of the old footbridges lies one
    // spot behind the book patch: the grandmother names the place, the player
    // finds the print, she allows the copy, the page is mounted and the open
    // print leads into W02. Windows between those affordances are presentation
    // only; the compiled reactions stay owned by content.
    private static WaterChainHomePresence BuildWaterChain(StyleBenchmarkZone house, ArrivalPersonalProps things,
        Material paper)
    {
        const float envelopeWidth = .165f;
        const float envelopeDepth = .115f;
        // The raised table tier carries the kettle and the bowl on its east
        // stretch; its west stretch is open, so the envelope lies there turned
        // just behind the book patch, clear of every cloth prop and in view.
        const float envelopeYawDegrees = FamilyBookPlacementYawDegrees + 104f;
        var tray = house.GetNode<Node3D>("GeneratedHouseInteriorAct1")
            .FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_TableTray_LOD0");
        var trayTop = PublicBuildingShell.Bounds(house, tray).End.Y;
        var envelopePosition = new Vector3(.740f, trayTop + .0035f, -2.620f);

        var yaw = Mathf.DegToRad(envelopeYawDegrees);
        var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        var forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
        var halfWidth = envelopeWidth * .5f + .006f;
        var halfDepth = envelopeDepth * .5f + .006f;
        var footprint = new List<Vector2> { new(envelopePosition.X, envelopePosition.Z) };
        foreach (var signX in new[] { -1f, 1f })
        foreach (var signZ in new[] { -1f, 1f })
            footprint.Add(new Vector2(envelopePosition.X, envelopePosition.Z)
                + right * (signX * halfWidth) + forward * (signZ * halfDepth));

        var trayFaceTransform = house.GlobalTransform.AffineInverse() * tray.GlobalTransform;
        var trayFaces = tray.Mesh!.GetFaces().Select(face => trayFaceTransform * face).ToArray();
        var heights = footprint.Select(point => TableTopHeightAt(
            point, trayFaces, trayTop - .01f, trayTop + .01f)).ToArray();
        if (heights.Any(float.IsNaN) || heights.Max() - heights.Min() > .002f)
            throw new InvalidOperationException("The family envelope footprint is not fully supported by the measured tray surface.");

        var envelopeBounds = new Aabb(
            new(footprint.Min(point => point.X), envelopePosition.Y - .004f, footprint.Min(point => point.Y)),
            new(footprint.Max(point => point.X) - footprint.Min(point => point.X), .012f,
                footprint.Max(point => point.Y) - footprint.Min(point => point.Y)));
        foreach (var mesh in house.FindChildren("*", nameof(MeshInstance3D), true, false)
                     .OfType<MeshInstance3D>()
                     .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()
                         && mesh.Name != "HouseInterior_TableTop_LOD0" && mesh.Name != "TeaTablecloth"
                         && mesh.Name != "HouseInterior_TableTray_LOD0"
                         && mesh.Name != "HouseInterior_TableTray_LOD1"))
        {
            var occupied = PublicBuildingShell.Bounds(house, mesh);
            if (Overlaps(envelopeBounds, occupied))
                throw new InvalidOperationException($"The family envelope footprint overlaps existing home prop {mesh.GetPath()}.");
        }

        var envelope = new Node3D
        {
            Name = "FamilyWaterEnvelope",
            Position = envelopePosition,
            RotationDegrees = new(0f, envelopeYawDegrees, 0f)
        };
        envelope.SetMeta("geometryOwner", nameof(ArrivalPersonalProps));
        envelope.SetMeta("presentationRole", "family envelope holding the second print of the old footbridges");
        envelope.SetMeta("assetStatus", "envelope and print are project-original; the W02 front art remains specified, not produced");
        things.AddChild(envelope);
        // The washed cloth is near-white, so plain cream stock loses the
        // envelope completely; kraft keeps it and the print caption legible.
        var envelopePaper = paper.Duplicate() as ShaderMaterial;
        envelopePaper?.SetShaderParameter("base_color", Color.FromHtml("c8b184"));
        var body = new MeshInstance3D
        {
            Name = "EnvelopeBody",
            Mesh = RuralPropGeometry.BevelBox(new(envelopeWidth, .006f, envelopeDepth), .001f),
            MaterialOverride = envelopePaper ?? paper
        };
        body.SetMeta("visualOnly", true);
        envelope.AddChild(body);
        var flap = new MeshInstance3D
        {
            Name = "EnvelopeFlap",
            Position = new(0f, .0043f, -.034f),
            RotationDegrees = new(9f, 0f, 0f),
            Mesh = RuralPropGeometry.BevelBox(new(envelopeWidth, .0028f, .058f), .0008f),
            MaterialOverride = envelopePaper ?? paper
        };
        flap.SetMeta("visualOnly", true);
        envelope.AddChild(flap);

        var print = new Node3D { Name = "FamilyWaterPrint", Position = new(0f, .0046f, .024f) };
        print.SetMeta("visualOnly", true);
        envelope.AddChild(print);
        var printPaper = new MeshInstance3D
        {
            Name = "PrintPaper",
            Mesh = RuralPropGeometry.BevelBox(new(.126f, .0014f, .090f), .0004f),
            MaterialOverride = paper
        };
        printPaper.SetMeta("visualOnly", true);
        print.AddChild(printPaper);
        const string printBackPath = "res://assets/textures/photobook/backs-v1/p-water-back.svg";
        if (ResourceLoader.Exists(printBackPath)
            && ResourceLoader.Load<Texture2D>(printBackPath) is { } backTexture)
        {
            var backMaterial = new StandardMaterial3D
            {
                AlbedoTexture = backTexture,
                Roughness = 1f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
            };
            var back = new MeshInstance3D
            {
                Name = "PrintBack",
                Position = new(0f, .0012f, 0f),
                // The authored back text reads from the room side of the table.
                RotationDegrees = new(-90f, 0f, 180f),
                Mesh = new QuadMesh { Size = new(.116f, .080f) },
                MaterialOverride = backMaterial
            };
            back.SetMeta("visualOnly", true);
            back.SetMeta("assetSource", printBackPath);
            back.SetMeta("assetStatus", "authored back face; the W02 front art remains specified, not produced");
            print.AddChild(back);
        }

        var envelopeCenter = envelopePosition + Vector3.Up * .06f;
        var source = house.MakeInteractionBox("WaterPhotoSourceTarget", new(.26f, .14f, .24f),
            envelopeCenter, "cfc4a8", "urman.fullgame:interaction/pw-water-photo-source",
            "Найти в конверте снимок у мостков", rayOnly: true);
        var entry = house.MakeInteractionBox("WaterRiverEntryTarget", new(.30f, .16f, .26f),
            envelopeCenter, "8fa694", "urman.fullgame:interaction/pw-enter-river",
            "Открыть снимок у мостков", rayOnly: true);
        var npcAnchor = StyleBenchmarkInteriorFactory.GulsinaAnchor + Vector3.Up * .84f;
        var talk = house.MakeInteractionBox("WaterPlaceTalkTarget", new(.62f, 1.68f, .46f),
            npcAnchor, "6f6257", "urman.fullgame:interaction/pw-water-place-talk",
            "Спросить әби, как она называет старые мостки", rayOnly: true);
        var permission = house.MakeInteractionBox("WaterPhotoPermissionTarget", new(.62f, 1.68f, .46f),
            npcAnchor, "6f6257", "urman.fullgame:interaction/pw-water-photo-permission",
            "Получить разрешение на копию снимка",
            dialogueId: "urman.fullgame:dialogue/pw-water-photo-permission", rayOnly: true);
        // The same әби keeps the later family-file question. Only one of the
        // three boxes at this anchor is projected at a time, so the ray never
        // has to choose between the water chain and the common file.
        var filePermission = house.MakeInteractionBox("CommonFilePermissionTarget", new(.62f, 1.68f, .46f),
            npcAnchor, "6f6257", "urman.fullgame:interaction/pw-common-file-permission",
            "Попросить разрешённую копию файла Марата",
            dialogueId: "urman.fullgame:dialogue/pw-common-file-permission", rayOnly: true);

        var presence = new WaterChainHomePresence
        {
            Name = "PhotoWorldWaterChainPresence",
            SourceTarget = source,
            RiverEntryTarget = entry,
            TalkTarget = talk,
            PermissionTarget = permission,
            CommonFilePermissionTarget = filePermission,
            TeaTarget = house.GetNodeOrNull<InteractionTarget>("GulsinaNpc"),
            Print = print
        };
        things.AddChild(presence);
        return presence;
    }

    // The first return uses Gulsina's existing anchor. After that authored
    // conversation, its already-produced back print appears in a family envelope.
    // The front is deliberately not fabricated while its passport remains pending.
    private static void BuildHousePhotoChain(StyleBenchmarkZone house, ArrivalPersonalProps things,
        Material paper, WaterChainHomePresence presence)
    {
        const float envelopeWidth = .165f;
        const float envelopeDepth = .115f;
        const string backPath = "res://assets/textures/photobook/backs-v1/p-house-back.svg";
        const float envelopeYawDegrees = FamilyBookPlacementYawDegrees + 104f;
        var envelopePosition = FindHousePhotoEnvelopePosition(
            house, envelopeWidth, envelopeDepth, envelopeYawDegrees);
        var envelope = new Node3D
        {
            Name = "FamilyHousePhotoEnvelope",
            Position = envelopePosition,
            RotationDegrees = new(0f, envelopeYawDegrees, 0f),
            Visible = false
        };
        envelope.SetMeta("geometryOwner", nameof(ArrivalPersonalProps));
        envelope.SetMeta("presentationRole", "family envelope with the authored P-HOUSE back print");
        envelope.SetMeta("assetStatus", "project-original envelope; P-HOUSE front remains specified, not produced");
        things.AddChild(envelope);

        var envelopePaper = paper.Duplicate() as ShaderMaterial ?? throw new InvalidOperationException(
            "The family envelope requires the existing paper shader material.");
        envelopePaper.SetShaderParameter("base_color", Color.FromHtml("c8b184"));
        var body = new MeshInstance3D
        {
            Name = "EnvelopeBody",
            Mesh = RuralPropGeometry.BevelBox(new(envelopeWidth, .006f, envelopeDepth), .001f),
            MaterialOverride = envelopePaper
        };
        body.SetMeta("visualOnly", true);
        envelope.AddChild(body);
        var flap = new MeshInstance3D
        {
            Name = "EnvelopeFlap",
            Position = new(0f, .0043f, -.034f),
            RotationDegrees = new(9f, 0f, 0f),
            Mesh = RuralPropGeometry.BevelBox(new(envelopeWidth, .0028f, .058f), .0008f),
            MaterialOverride = envelopePaper
        };
        flap.SetMeta("visualOnly", true);
        envelope.AddChild(flap);
        var print = new Node3D { Name = "HousePhotoPrint", Position = new(0f, .0046f, .024f) };
        print.SetMeta("visualOnly", true);
        envelope.AddChild(print);
        var printPaper = new MeshInstance3D
        {
            Name = "PrintPaper",
            Mesh = RuralPropGeometry.BevelBox(new(.150f, .0014f, .100f), .0004f),
            MaterialOverride = paper
        };
        printPaper.SetMeta("visualOnly", true);
        print.AddChild(printPaper);

        var backTexture = ResourceLoader.Load<Texture2D>(backPath)
            ?? throw new InvalidOperationException("The authored P-HOUSE back has not been packaged.");
        var back = new MeshInstance3D
        {
            Name = "PrintBack",
            Position = new(0f, .0012f, 0f),
            RotationDegrees = new(-90f, 0f, 180f),
            Mesh = new QuadMesh { Size = new(.150f, .100f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = backTexture,
                Roughness = 1f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
            }
        };
        back.SetMeta("visualOnly", true);
        back.SetMeta("assetSource", backPath);
        back.SetMeta("assetStatus", "authored back face only; P-HOUSE front remains specified, not produced");
        print.AddChild(back);

        var entryTargetPosition = things.Transform * envelope.Transform
            * new Vector3(0f, .06f, presence.PrintOutLocalZ);
        house.MakeInteractionBox("HousePhotoEntryTarget", new(.30f, .16f, .26f),
            entryTargetPosition, "8fa694", "urman.fullgame:interaction/pw-enter-house",
            "Войти в знакомый двор со снимка", rayOnly: true);

        var source = house.MakeInteractionBox("HousePhotoSourceTarget", new(.26f, .14f, .24f),
            envelopePosition + Vector3.Up * .06f, "cfc4a8",
            "urman.fullgame:interaction/pw-house-photo-source", "Узнать старый снимок собственного двора",
            rayOnly: true);
        source.SetPresentationEnabled(false);
        var npcAnchor = StyleBenchmarkInteriorFactory.GulsinaAnchor + Vector3.Up * .84f;
        var homeTalk = house.MakeInteractionBox("FirstWorldHomeTalkTarget", new(.62f, 1.68f, .46f),
            npcAnchor, "6f6257", "urman.fullgame:interaction/pw-first-world-home-talk",
            "Поговорить о первом возвращении из снимка",
            dialogueId: "urman.fullgame:dialogue/pw-first-world-home", rayOnly: true);
        homeTalk.SetPresentationEnabled(false);
        var permission = house.MakeInteractionBox("HousePhotoPermissionTarget", new(.62f, 1.68f, .46f),
            npcAnchor, "6f6257", "urman.fullgame:interaction/pw-house-photo-permission",
            "Попросить семейную копию снимка двора", rayOnly: true);
        permission.SetPresentationEnabled(false);

        presence.HomeReturnTarget = homeTalk;
        presence.HousePhotoSourceTarget = source;
        presence.HousePhotoPermissionTarget = permission;
        presence.HousePhotoEnvelope = envelope;
        presence.HousePhotoPrint = print;
    }

    private static Vector3 FindHousePhotoEnvelopePosition(StyleBenchmarkZone house,
        float envelopeWidth, float envelopeDepth, float envelopeYawDegrees)
    {
        var table = house.GetNode<Node3D>("GeneratedHouseInteriorAct1")
            .FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_TableTop_LOD0");
        var support = PublicBuildingShell.Bounds(house, table);
        var faceTransform = house.GlobalTransform.AffineInverse() * table.GlobalTransform;
        var faces = table.Mesh!.GetFaces().Select(face => faceTransform * face).ToArray();
        var chosen = Vector3.Zero;
        var placed = false;
        var bestDistance = float.PositiveInfinity;
        const float gridStep = .06f;
        var marginX = envelopeWidth * .5f + .01f;
        var marginZ = envelopeDepth * .5f + .01f;
        var yaw = Mathf.DegToRad(envelopeYawDegrees);
        var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        var forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
        var extentX = Math.Abs(right.X) * marginX + Math.Abs(forward.X) * marginZ;
        var extentZ = Math.Abs(right.Y) * marginX + Math.Abs(forward.Y) * marginZ;

        for (var x = support.Position.X + extentX; x <= support.End.X - extentX; x += gridStep)
        for (var z = support.Position.Z + extentZ; z <= support.End.Z - extentZ; z += gridStep)
        {
            var center = new Vector2(x, z);
            var points = new[]
            {
                center,
                center - right * marginX - forward * marginZ,
                center - right * marginX + forward * marginZ,
                center + right * marginX - forward * marginZ,
                center + right * marginX + forward * marginZ
            };
            var heights = points.Select(point => TableTopHeightAt(
                point, faces, support.Position.Y - .01f, support.End.Y + .01f)).ToArray();
            if (heights.Any(float.IsNaN) || heights.Max() - heights.Min() > .002f)
                continue;

            var left = points.Min(point => point.X);
            var rightEdge = points.Max(point => point.X);
            var back = points.Min(point => point.Y);
            var front = points.Max(point => point.Y);
            var bounds = new Aabb(
                new(left, heights.Max() + .003f, back),
                new(rightEdge - left, .020f, front - back));
            var blocker = house.FindChildren("*", nameof(MeshInstance3D), true, false)
                .OfType<MeshInstance3D>()
                .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()
                    && mesh.Name != "HouseInterior_TableTop_LOD0" && mesh.Name != "TeaTablecloth")
                .FirstOrDefault(mesh => Overlaps(bounds, PublicBuildingShell.Bounds(house, mesh)));
            if (blocker is not null) continue;

            var distance = new Vector2(x, z).DistanceSquaredTo(
                new Vector2(FamilyBookTablePosition.X, FamilyBookTablePosition.Y));
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            chosen = new Vector3(x, heights.Max() + .004f, z);
            placed = true;
        }

        if (!placed)
            throw new InvalidOperationException("No supported free table patch remains for the P-HOUSE print.");
        return chosen;
    }

    // The desk printer beside the old PC. Its sheet is the same authored
    // P-COMMON page the book mounts, so the paper never becomes a second
    // source. The patch is picked from measured free table area: every
    // candidate must sit flat on the real TableTop faces and stay clear of
    // every already visible home prop, exactly like the book and envelope.
    private static void BuildHomePrinter(StyleBenchmarkZone house, ArrivalPersonalProps things,
        Material paper)
    {
        const float bodyWidth = .36f;
        const float bodyDepth = .30f;
        const float bodyHeight = .15f;
        const float placementMargin = .01f;
        const float footprintHalfWidth = bodyWidth * .5f + placementMargin;
        const float footprintBack = -bodyDepth * .5f - placementMargin;
        const float footprintFront = bodyDepth * .5f + .12f + placementMargin;
        var table = house.GetNode<Node3D>("GeneratedHouseInteriorAct1")
            .FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_TableTop_LOD0");
        var support = PublicBuildingShell.Bounds(house, table);
        var faceTransform = house.GlobalTransform.AffineInverse() * table.GlobalTransform;
        var faces = table.Mesh!.GetFaces().Select(face => faceTransform * face).ToArray();

        // Keep the full tray supported and prefer the front-left patch, where
        // the CRT is less likely to hide the sheet. Other clear patches remain valid.
        var preferredPosition = new Vector2(-.55f, -2.08f);
        var chosen = Vector3.Zero;
        var placed = false;
        var bestDistance = float.PositiveInfinity;
        var gridStep = .06f;
        var xs = new List<float>();
        var zs = new List<float>();
        for (var x = support.Position.X + footprintHalfWidth; x <= support.End.X - footprintHalfWidth; x += gridStep)
            xs.Add(x);
        for (var z = support.Position.Z - footprintBack; z <= support.End.Z - footprintFront; z += gridStep)
            zs.Add(z);

        foreach (var x in xs)
        foreach (var z in zs)
        {
            var candidate = new Vector2(x, z);
            var points = new List<Vector2> { candidate };
            foreach (var signX in new[] { -1f, 1f })
            foreach (var offsetZ in new[] { footprintBack, footprintFront })
                points.Add(candidate + new Vector2(signX * footprintHalfWidth, offsetZ));
            var heights = points.Select(point => TableTopHeightAt(
                point, faces, support.Position.Y - .01f, support.End.Y + .01f)).ToArray();
            if (heights.Any(float.IsNaN) || heights.Max() - heights.Min() > .002f)
                continue;

            var bounds = new Aabb(
                new(points.Min(point => point.X), heights.Max(), points.Min(point => point.Y)),
                new(points.Max(point => point.X) - points.Min(point => point.X), bodyHeight + .01f,
                    points.Max(point => point.Y) - points.Min(point => point.Y)));
            var blocker = house.FindChildren("*", nameof(MeshInstance3D), true, false)
                .OfType<MeshInstance3D>()
                .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()
                    && mesh.Name != "HouseInterior_TableTop_LOD0" && mesh.Name != "TeaTablecloth")
                .FirstOrDefault(mesh => Overlaps(bounds, PublicBuildingShell.Bounds(house, mesh)));
            if (blocker is not null)
            {
                // Overlap is an expected grid-search rejection, not a warning condition.
                continue;
            }

            var distance = (candidate - preferredPosition).LengthSquared();
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            chosen = new Vector3(x, heights.Max() + .004f, z);
            placed = true;
        }

        if (!placed)
            throw new InvalidOperationException("No supported free table patch remains for the desk printer.");

        var printer = new Node3D
        {
            Name = "HomeDeskPrinter",
            Position = chosen
        };
        printer.SetMeta("geometryOwner", nameof(ArrivalPersonalProps));
        printer.SetMeta("presentationRole", "desk printer that turns the authored P-COMMON print into paper");
        printer.SetMeta("assetStatus", "project-original procedural printer; the sheet reuses the authored p-common back artwork");
        things.AddChild(printer);

        var body = new MeshInstance3D
        {
            Name = "PrinterBody",
            Position = new(0f, bodyHeight * .5f, 0f),
            Mesh = RuralPropGeometry.BevelBox(new(bodyWidth, bodyHeight, bodyDepth), .004f),
            MaterialOverride = RuralPropMaterials.Surface("plastic", "c9bfa6")
        };
        body.SetMeta("visualOnly", true);
        printer.AddChild(body);

        var lid = new MeshInstance3D
        {
            Name = "PrinterLidSeam",
            Position = new(0f, bodyHeight + .001f, -.008f),
            Mesh = RuralPropGeometry.BevelBox(new(bodyWidth - .04f, .006f, bodyDepth - .06f), .0015f),
            MaterialOverride = RuralPropMaterials.Surface("plastic", "b3a98f")
        };
        lid.SetMeta("visualOnly", true);
        printer.AddChild(lid);

        var slot = new MeshInstance3D
        {
            Name = "PrinterOutputSlot",
            Position = new(0f, .052f, bodyDepth * .5f),
            Mesh = RuralPropGeometry.BevelBox(new(bodyWidth - .10f, .022f, .012f), .001f),
            MaterialOverride = RuralPropMaterials.Surface("rubber", "3f3a33")
        };
        slot.SetMeta("visualOnly", true);
        printer.AddChild(slot);

        var outputTray = new MeshInstance3D
        {
            Name = "PrinterOutputTray",
            Position = new(0f, .044f, bodyDepth * .5f + .06f),
            Mesh = RuralPropGeometry.BevelBox(new(bodyWidth - .08f, .006f, .12f), .002f),
            MaterialOverride = RuralPropMaterials.Surface("plastic", "bdb397")
        };
        outputTray.SetMeta("visualOnly", true);
        printer.AddChild(outputTray);

        var led = new MeshInstance3D
        {
            Name = "PrinterPowerLed",
            Position = new(.12f, .052f, bodyDepth * .5f + .001f),
            Mesh = new BoxMesh { Size = new(.008f, .004f, .002f) },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("86a86b")
        };
        led.SetMeta("visualOnly", true);
        printer.AddChild(led);

        var sheet = new Node3D { Name = "PrinterSheet", Position = new(0f, .049f, .075f), Visible = false };
        printer.AddChild(sheet);
        var sheetPaper = new MeshInstance3D
        {
            Name = "PrinterSheetPaper",
            Mesh = RuralPropGeometry.BevelBox(new(.148f, .0014f, .100f), .0004f),
            MaterialOverride = paper
        };
        sheetPaper.SetMeta("visualOnly", true);
        sheet.AddChild(sheetPaper);
        const string sheetBackPath = "res://assets/textures/photobook/backs-v1/p-common-back.svg";
        if (ResourceLoader.Exists(sheetBackPath)
            && ResourceLoader.Load<Texture2D>(sheetBackPath) is { } backTexture)
        {
            var backMaterial = new StandardMaterial3D
            {
                AlbedoTexture = backTexture,
                Roughness = 1f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
            };
            var back = new MeshInstance3D
            {
                Name = "PrinterSheetBack",
                Position = new(0f, .0011f, 0f),
                // The authored page reads from the room side, like the water print.
                RotationDegrees = new(-90f, 0f, 180f),
                Mesh = new QuadMesh { Size = new(.140f, .094f) },
                MaterialOverride = backMaterial
            };
            back.SetMeta("visualOnly", true);
            back.SetMeta("assetSource", sheetBackPath);
            back.SetMeta("assetStatus", "authored back face; the P-COMMON front art remains specified, not produced");
            sheet.AddChild(back);
        }

        var entryPosition = things.Transform * printer.Transform * new Vector3(0f, .08f, .215f);
        var entry = house.MakeInteractionBox("CommonPhotoEntryTarget", new(.22f, .14f, .18f),
            entryPosition, "8fa694", "urman.fullgame:interaction/pw-enter-common",
            "Войти в общий кадр с распечатанного снимка", rayOnly: true);
        entry.SetPresentationEnabled(false);

        printer.AddChild(new HomePrinterPresence
        {
            Name = "HomePrinterPresence",
            Sheet = sheet,
            EntryTarget = entry,
            SheetInLocalZ = .075f,
            SheetOutLocalZ = .215f
        });
    }

    private static void ReplaceWithAuthoredFamilyBook(Node3D book, Material cloth, Material paper)
    {
        const string modelPath = "res://assets/models/photoworlds/family-book-v1.glb";
        if (!ResourceLoader.Exists(modelPath)
            || ResourceLoader.Load<PackedScene>(modelPath) is not { } model) return;
        var authored = model.Instantiate<Node3D>();
        foreach (var mesh in authored.FindChildren("*", nameof(MeshInstance3D), true, false)
                     .OfType<MeshInstance3D>())
        {
            var materialName = mesh.Mesh?.GetSurfaceCount() > 0
                ? mesh.Mesh.SurfaceGetMaterial(0)?.ResourceName : null;
            if (materialName == "cloth") mesh.MaterialOverride = cloth;
            else if (materialName == "paper") mesh.MaterialOverride = paper;
            mesh.SetMeta("visualOnly", true);
            mesh.SetMeta("surfaceUVContract", "metres; texture repeat set by material");
        }
        // The table owns only the measured closed footprint. Pose clips stay idle
        // until a presentation owner supplies room for opening or inspection.
        foreach (var player in authored.FindChildren("*", nameof(AnimationPlayer), true, false)
                     .OfType<AnimationPlayer>())
        {
            player.Autoplay = string.Empty;
            player.Stop();
        }
        foreach (var old in book.GetChildren())
        {
            book.RemoveChild(old);
            old.Free();
        }
        book.AddChild(authored);
        book.SetMeta("assetSource", modelPath);
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
