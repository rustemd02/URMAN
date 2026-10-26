using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

internal partial class ArrivalPersonalProps
{
    private const float CellarPhotoStockThickness = .0014f;
    private const float CellarPhotoBackOffset = .0012f;
    private const float CellarPhotoStackGap = .001f;

    private static void BuildCellarPhotoChain(StyleBenchmarkZone house, ArrivalPersonalProps things,
        Material paper, WaterChainHomePresence presence)
    {
        const float boxWidth = .22f;
        const float boxDepth = .24f;
        const float boxSideHeight = .064f;
        const float boxBottomThickness = .008f;
        const float boxLidThickness = .010f;

        var shelfBody = house.GetNode<StaticBody3D>("FamilyPhotoShelf");
        var shelf = shelfBody.GetNode<MeshInstance3D>("Visible");
        var shelfBounds = PublicBuildingShell.Bounds(house, shelf);
        var shelfTransform = house.GlobalTransform.AffineInverse() * shelf.GlobalTransform;
        var shelfFaces = shelf.Mesh!.GetFaces().Select(face => shelfTransform * face).ToArray();
        var boxCenter = new Vector2(shelfBounds.Position.X + .18f, shelfBounds.Position.Z + shelfBounds.Size.Z * .5f);
        const float supportMargin = .003f;
        var supportPoints = new[]
        {
            boxCenter,
            boxCenter + new Vector2(-boxWidth * .5f - supportMargin, -boxDepth * .5f - supportMargin),
            boxCenter + new Vector2(-boxWidth * .5f - supportMargin, boxDepth * .5f + supportMargin),
            boxCenter + new Vector2(boxWidth * .5f + supportMargin, -boxDepth * .5f - supportMargin),
            boxCenter + new Vector2(boxWidth * .5f + supportMargin, boxDepth * .5f + supportMargin)
        };
        var supportHeights = supportPoints.Select(point => TableTopHeightAt(
            point, shelfFaces, shelfBounds.Position.Y - .01f, shelfBounds.End.Y + .01f)).ToArray();
        if (supportHeights.Any(float.IsNaN) || supportHeights.Max() - supportHeights.Min() > .002f)
            throw new InvalidOperationException("The family photo box footprint is not fully supported by the measured FamilyPhotoShelf surface.");

        var boxBaseY = supportHeights.Max();
        var boxBounds = new Aabb(
            new(boxCenter.X - boxWidth * .5f, boxBaseY, boxCenter.Y - boxDepth * .5f),
            new(boxWidth, boxSideHeight + boxLidThickness + .004f, boxDepth));
        foreach (var mesh in house.FindChildren("*", nameof(MeshInstance3D), true, false)
                     .OfType<MeshInstance3D>()
                     .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()
                         && !ReferenceEquals(mesh, shelf)
                         && !(mesh.GetParent() is StaticBody3D body
                             && (body.Name == "FamilyPhotoShelfBracketRear"
                                 || body.Name == "FamilyPhotoShelfBracketFront"))))
        {
            if (Overlaps(boxBounds, PublicBuildingShell.Bounds(house, mesh)))
                throw new InvalidOperationException($"The family photo box overlaps existing home prop {mesh.GetPath()}.");
        }

        var box = new Node3D
        {
            Name = "FamilyPhotoBoxPresentation",
            Position = new(boxCenter.X, boxBaseY, boxCenter.Y)
        };
        box.SetMeta("geometryOwner", nameof(ArrivalPersonalProps));
        box.SetMeta("presentationRole", "small family photo box seated on the authored shelf; compiled facts own its open state");
        box.SetMeta("storedSource", "Семейный альбом Гөлсинә; контактный отпечаток S-PHOTOGRAPHER остаётся в семье, разрешённая копия попадает в книгу");
        box.SetMeta("dimensionsMetres", new Vector3(boxWidth, boxSideHeight + boxLidThickness, boxDepth));
        box.SetMeta("supportMesh", shelf.GetPath().ToString());
        box.SetMeta("assetStatus", "project-original box; no unproduced photo front art is synthesized");
        things.AddChild(box);

        var wood = RuralPropMaterials.Surface("wood", "765842");
        RuralPropGeometry.Block(box, "BoxBottom", new(boxWidth, boxBottomThickness, boxDepth),
            new(0f, boxBottomThickness * .5f, 0f), wood, .0015f);
        RuralPropGeometry.Block(box, "BoxBack", new(boxWidth, boxSideHeight, .008f),
            new(0f, boxSideHeight * .5f, -boxDepth * .5f + .004f), wood, .001f);
        RuralPropGeometry.Block(box, "BoxLeft", new(.008f, boxSideHeight, boxDepth - .008f),
            new(-boxWidth * .5f + .004f, boxSideHeight * .5f, .004f), wood, .001f);
        RuralPropGeometry.Block(box, "BoxRight", new(.008f, boxSideHeight, boxDepth - .008f),
            new(boxWidth * .5f - .004f, boxSideHeight * .5f, .004f), wood, .001f);
        // A low front lip leaves a visible, reachable slot for the tucked photo.
        RuralPropGeometry.Block(box, "BoxFrontLip", new(boxWidth - .016f, .024f, .008f),
            new(0f, .012f, boxDepth * .5f - .004f), wood, .001f);

        var lidPivot = new Node3D
        {
            Name = "FamilyPhotoBoxLid",
            Position = new(0f, boxSideHeight, -boxDepth * .5f + .004f)
        };
        box.AddChild(lidPivot);
        RuralPropGeometry.Block(lidPivot, "BoxLid", new(boxWidth, boxLidThickness, boxDepth),
            new(0f, boxLidThickness * .5f, boxDepth * .5f - .004f), wood, .002f);

        var photographerCardY = boxBottomThickness + .001f;
        var handCardY = photographerCardY + CellarPhotoStockThickness + CellarPhotoBackOffset + CellarPhotoStackGap;
        var cellarCardY = handCardY + CellarPhotoStockThickness + CellarPhotoBackOffset + CellarPhotoStackGap;
        var handCard = BuildCellarBackCard(box, "HandPhotoCard",
            new(0f, handCardY, -.054f),
            "res://assets/textures/photobook/backs-v1/s-hand-back.svg",
            "S-HAND", paper, "authored back face; front art is not produced");
        handCard.Visible = false;
        var cellarCard = BuildCellarBackCard(box, "CellarPhotoCard",
            new(0f, cellarCardY, .055f),
            "res://assets/textures/photobook/backs-v1/p-cellar-back.svg",
            "P-CELLAR", paper, "authored back face; front art is not produced");
        cellarCard.Visible = false;
        var photographerCard = BuildCellarBackCard(box, "PhotographerPhotoCard",
            new(0f, photographerCardY, -.010f),
            "res://assets/textures/photobook/backs-v1/s-photographer-back.svg",
            "S-PHOTOGRAPHER", paper, "authored back face; front art is not produced");
        photographerCard.Visible = false;
        photographerCard.GetNode<MeshInstance3D>("PhotoBack").Visible = false;

        var familySource = house.MakeInteractionBox("FamilyBoxSourceTarget", new(.27f, .20f, .29f),
            box.Position + new Vector3(0f, .060f, 0f), "765842",
            "urman.fullgame:interaction/pw-family-box-source", "Открыть семейный ящик со снимками", rayOnly: true);
        familySource.SetPresentationEnabled(false);
        var cellarSource = house.MakeInteractionBox("CellarPhotoSourceTarget", new(.20f, .12f, .15f),
            box.Position + new Vector3(0f, .030f, .055f), "cfc4a8",
            "urman.fullgame:interaction/pw-cellar-photo-source", "Найти снимок перед уборкой", rayOnly: true);
        cellarSource.SetPresentationEnabled(false);

        var npcAnchor = StyleBenchmarkInteriorFactory.GulsinaAnchor + Vector3.Up * .95f;
        var familyPermission = house.MakeInteractionBox("FamilyBoxPermissionTarget", new(.62f, 1.90f, .46f),
            npcAnchor, "6f6257", "urman.fullgame:interaction/pw-family-box-permission",
            "Попросить разрешение сохранить семейный снимок", rayOnly: true);
        familyPermission.SetPresentationEnabled(false);
        var cellarPermission = house.MakeInteractionBox("CellarPhotoPermissionTarget", new(.62f, 1.90f, .46f),
            npcAnchor, "6f6257", "urman.fullgame:interaction/pw-cellar-photo-permission",
            "Попросить разрешение на снимок перед уборкой", rayOnly: true);
        cellarPermission.SetPresentationEnabled(false);

        presence.FamilyBoxSourceTarget = familySource;
        presence.FamilyBoxPermissionTarget = familyPermission;
        presence.CellarPhotoSourceTarget = cellarSource;
        presence.CellarPhotoPermissionTarget = cellarPermission;
        presence.FamilyBoxLid = lidPivot;
        presence.HandPhotoCard = handCard;
        presence.CellarPhotoCard = cellarCard;
        presence.PhotographerPhotoCard = photographerCard;
    }

    private static Node3D BuildCellarBackCard(Node3D parent, string name, Vector3 position,
        string backPath, string photoId, Material paper, string assetStatus)
    {
        var texture = ResourceLoader.Load<Texture2D>(backPath)
            ?? throw new InvalidOperationException($"The authored {photoId} back has not been packaged.");
        var card = new Node3D { Name = name, Position = position };
        card.SetMeta("presentationRole", $"physical {photoId} photo card; authored back only");
        card.SetMeta("assetStatus", assetStatus);
        card.SetMeta("photoId", photoId);
        parent.AddChild(card);
        var stock = RuralPropGeometry.Part(card, "PhotoStock",
            RuralPropGeometry.BevelBox(new(.150f, CellarPhotoStockThickness, .100f), .0004f), Vector3.Zero, paper);
        stock.SetMeta("visualOnly", true);
        var back = new MeshInstance3D
        {
            Name = "PhotoBack",
            Position = Vector3.Up * CellarPhotoBackOffset,
            RotationDegrees = new(-90f, 0f, 180f),
            Mesh = new QuadMesh { Size = new(.150f, .100f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = texture,
                Roughness = 1f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
            }
        };
        back.SetMeta("visualOnly", true);
        back.SetMeta("assetSource", backPath);
        back.SetMeta("assetStatus", assetStatus);
        card.AddChild(back);
        return card;
    }
}
