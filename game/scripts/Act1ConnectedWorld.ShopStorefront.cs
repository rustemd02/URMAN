using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The village shop stops reading as a dwelling (author 2026-09-25: "здание
/// магаза перестрой, это не жилой дом"). A rural Tatarstan magazin of the
/// 1980s–90s is a plastered single-storey block whose street front rises into
/// a flat false front hiding the roof; the sign lives on that front, the
/// windows carry welded bars, a lamp hangs over the sign and a bin stands by
/// the steps. The kit shell, its doors, interior, collision and address stay;
/// only its skin and presentation change.
/// </summary>
public partial class Act1ConnectedWorld
{
    private void BuildShopStorefront(PublicBuildingRoom shop)
    {
        var building = shop.Building;
        var scale = building.GlobalBasis.Scale.X;
        Aabb Source(string suffix) => PublicBuildingShell.Bounds(building, FindDescendants<MeshInstance3D>(building)
            .Single(mesh => mesh.Name.ToString().EndsWith(suffix, StringComparison.Ordinal)));
        var street = Source("_Street_Wall_LOD0");
        var ridgeTop = Source("_RidgeBeam_LOD0").End.Y * scale;
        var wallTop = street.End.Y * scale;
        var front = street.End.Z * scale;
        var roof = Source("_Roof_LOD0");
        var roofFront = roof.End.Z * scale;
        var halfWidth = street.Size.X * .5f * scale;
        var roofHalfWidth = roof.Size.X * .5f * scale;
        var centreX = street.GetCenter().X * scale;

        // Plastered and whitewashed skin instead of the dwelling's timber.
        var plaster = PainterlyMaterialLibrary.ForColor("d6cfbd", "plaster");
        var trim = PainterlyMaterialLibrary.ForColor("7d8a86", "plaster");
        var reskinned = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(building).Where(m => m.Visible))
        {
            var name = mesh.Name.ToString();
            if (name.EndsWith("_Wall_LOD0", StringComparison.Ordinal) || name.Contains("_BoardedGable_", StringComparison.Ordinal))
            { mesh.MaterialOverride = plaster; reskinned++; }
            else if (name.Contains("_Corner_", StringComparison.Ordinal) || name.Contains("_Foundation_", StringComparison.Ordinal)
                     || name.Contains("_FootingCap_", StringComparison.Ordinal))
            { mesh.MaterialOverride = trim; reskinned++; }
        }

        var storefront = new Node3D { Name = "ShopStorefront" };
        shop.Metric.AddChild(storefront);
        storefront.SetMeta("presentationOnly", true);
        storefront.SetMeta("designNote", "rural magazin false front, barred windows, lamp and bin; kit shell, doors and collision unchanged");

        // False front: a flat plastered parapet standing on the street wall up
        // past the ridge, with a raised centre and a painted cornice.
        var parapetBottom = wallTop - .25f;
        var parapetTop = ridgeTop + .12f;
        // It stands just in front of the roof's gable overhang, so the dwelling's
        // gable and verge boards disappear behind it.
        var width = roofHalfWidth * 2 + .12f;
        var z = roofFront + .09f;
        foreach (var verge in FindDescendants<MeshInstance3D>(building).Where(m => m.Name.ToString().Contains("_Front_Verge", StringComparison.Ordinal)).ToArray())
        {
            verge.SetMeta("suppressionReason", "shop false front replaces the dwelling gable trim");
            HidePresentationNode(verge);
        }
        AddVisualBox(storefront, "FalseFront", new(width, parapetTop - parapetBottom, .16f),
            new(centreX, (parapetTop + parapetBottom) * .5f, z), "d6cfbd", "plaster");
        AddVisualBox(storefront, "FalseFrontStep", new(width * .42f, .34f, .16f),
            new(centreX, parapetTop + .17f, z), "d6cfbd", "plaster");
        AddVisualBox(storefront, "Cornice", new(width + .10f, .09f, .24f), new(centreX, parapetTop - .02f, z + .02f), "7d8a86", "plaster");
        AddVisualBox(storefront, "CorniceStep", new(width * .42f + .08f, .08f, .24f), new(centreX, parapetTop + .35f, z + .02f), "7d8a86", "plaster");
        AddVisualBox(storefront, "SignBand", new(width - .20f, .06f, .20f), new(centreX, parapetBottom + .06f, z + .02f), "7d8a86", "plaster");
        // The parapet's underside over the wall reads as the shop's canopy:
        // a painted board soffit.
        AddVisualBox(storefront, "CanopySoffit", new(width - .02f, .03f, z - front + .06f),
            new(centreX, parapetBottom - .015f, (z + front) * .5f), "7d8a86", "wood");
        // Side returns close the gap between the parapet and the wall corners.
        foreach (var side in new[] { -1f, 1f })
            AddVisualBox(storefront, $"FalseFrontReturn{side}", new(.14f, parapetTop - parapetBottom, z - front),
                new(centreX + side * (width * .5f - .07f), (parapetTop + parapetBottom) * .5f, (z + front) * .5f), "c7bfab", "plaster");
        // Snow lies on the cornice and the step.
        AddVisualBox(storefront, "CorniceSnow", new(width + .06f, .05f, .22f), new(centreX, parapetTop + .045f, z + .01f), "e4e7e3", "snow_ground");
        AddVisualBox(storefront, "CorniceStepSnow", new(width * .42f + .04f, .05f, .22f), new(centreX, parapetTop + .415f, z + .01f), "e4e7e3", "snow_ground");

        // The sign moves onto the false front, centred on the parapet.
        var sign = FindDescendants<Node3D>(building).First(node => node.Name == "AshamlyklarSign");
        var hours = sign.GetNode<Node3D>("OpeningHoursPlate");
        // Village shop signs run most of the front's width.
        sign.Scale *= 1.45f;
        sign.GlobalPosition = storefront.ToGlobal(new(centreX, (parapetBottom + parapetTop) * .5f + .05f, z + .08f));
        // The hours plate stays at eye level beside the door, on the wall.
        var hoursBasis = hours.GlobalBasis;
        hours.Reparent(storefront);
        hours.GlobalBasis = hoursBasis;
        hours.Position = new(centreX + halfWidth - .55f, 1.55f, front + .012f);

        // Welded window bars: five uprights, two flats, on every street window.
        var bars = 0;
        foreach (var glass in FindDescendants<MeshInstance3D>(building)
                     .Where(m => m.Visible && m.Name.ToString().Contains("_Street_Window", StringComparison.Ordinal)
                                 && m.Name.ToString().EndsWith("_Glass_LOD0", StringComparison.Ordinal)))
        {
            var box = PublicBuildingShell.Bounds(building, glass);
            var c = box.GetCenter() * scale;
            var w = box.Size.X * scale + .10f;
            var h = box.Size.Y * scale + .10f;
            var zb = front + .06f;
            for (var i = 0; i < 5; i++)
                AddVisualBox(storefront, $"Bar{bars}_{i}", new(.014f, h, .014f), new(c.X - w * .5f + w * (i + .5f) / 5f, c.Y, zb), "2f3432", "metal");
            foreach (var y in new[] { -.3f, .3f })
                AddVisualBox(storefront, $"BarFlat{bars}_{y}", new(w, .03f, .008f), new(c.X, c.Y + y * h, zb + .01f), "2f3432", "metal");
            bars++;
        }

        // The dwelling parcel's front yard fence and gate stood across the shop's
        // approach from the street: a shop's front is open to the road.
        var shopCentre = building.GlobalPosition;
        foreach (var yard in FindDescendants<Node3D>(GetNode<Node3D>("Act1CoreWorldGreybox"))
                     .Where(n => n.Name == "VillageParcel_VariantB_PlasterAnnex_Yard" && n.IsVisibleInTree()
                                 && n.GlobalPosition.DistanceTo(shopCentre) < 14f).ToArray())
        {
            if (new[] { yard }.Concat(FindDescendants<Node>(yard)).Any(IsProtectedGameplayNode))
                throw new InvalidOperationException("Shop yard fence carries gameplay: " + yard.GetPath());
            yard.SetMeta("connectedWorldSuppressionReason", "shop front open to the street (author 2026-09-25)");
            HidePresentationNode(yard);
        }

        // A gooseneck lamp over the sign and a bin by the steps.
        var lampY = parapetTop + .05f;
        foreach (var side in new[] { -.55f, .55f })
        {
            AddVisualBox(storefront, $"LampArm{side}", new(.025f, .025f, .42f), new(centreX + side, lampY, z + .28f), "2f3432", "metal");
            var shade = new MeshInstance3D
            {
                Name = $"LampShade{side}", Mesh = new CylinderMesh { TopRadius = .05f, BottomRadius = .14f, Height = .10f, RadialSegments = 14 },
                Position = new(centreX + side, lampY - .06f, z + .48f),
                MaterialOverride = PainterlyMaterialLibrary.ForColor("3f5a4e", "metal")
            };
            storefront.AddChild(shade);
        }
        var bin = new MeshInstance3D
        {
            Name = "ShopBin", Mesh = new CylinderMesh { TopRadius = .22f, BottomRadius = .19f, Height = .62f, RadialSegments = 14 },
            Position = new(centreX + halfWidth - .25f, .31f, front + .45f),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("39433d", "metal")
        };
        storefront.AddChild(bin);
        AddVisualBox(storefront, "ShopBinSnow", new(.38f, .04f, .38f), new(centreX + halfWidth - .25f, .64f, front + .45f), "e4e7e3", "snow_ground");
        GD.Print(System.FormattableString.Invariant(
            $"act1-shop-storefront: reskinned={reskinned} barredWindows={bars} parapet={parapetBottom:0.00}..{parapetTop:0.00} ridge={ridgeTop:0.00}"));
    }
}
