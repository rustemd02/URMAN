using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The village shop uses its existing pitched roof and three real street
/// windows as a small blue-and-white rural pavilion. The kit shell, doors,
/// interior, collision and address remain the owners of the usable building.
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
        var wallTop = street.End.Y * scale;
        var front = street.End.Z * scale;
        var halfWidth = street.Size.X * .5f * scale;
        var centreX = street.GetCenter().X * scale;

        var plaster = PainterlyMaterialLibrary.ForColor("e0e5e4", "plaster");
        var trim = PainterlyMaterialLibrary.ForColor("426f99", "wood_painted_blue");
        var roof = PainterlyMaterialLibrary.ForColor("416a8b", "roof_metal");
        var reskinned = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(building).Where(m => m.Visible))
        {
            var name = mesh.Name.ToString();
            if (name.EndsWith("_Wall_LOD0", StringComparison.Ordinal) || name.Contains("_BoardedGable_", StringComparison.Ordinal))
            { mesh.MaterialOverride = plaster; reskinned++; }
            else if (name.EndsWith("_Roof_LOD0", StringComparison.Ordinal))
            { mesh.MaterialOverride = roof; reskinned++; }
            else if (name.Contains("_Corner_", StringComparison.Ordinal) || name.Contains("_Foundation_", StringComparison.Ordinal)
                     || name.Contains("_FootingCap_", StringComparison.Ordinal)
                     || name.Contains("_Verge", StringComparison.Ordinal) || name.Contains("_RidgeBeam_", StringComparison.Ordinal)
                     || (name.Contains("_Window", StringComparison.Ordinal)
                         && (name.Contains("_Jamb", StringComparison.Ordinal) || name.Contains("_Rail", StringComparison.Ordinal)
                             || name.Contains("_Sill", StringComparison.Ordinal) || name.Contains("_Mullion", StringComparison.Ordinal))))
            { mesh.MaterialOverride = trim; reskinned++; }
        }

        var storefront = new Node3D { Name = "ShopStorefront" };
        shop.Metric.AddChild(storefront);
        storefront.SetMeta("presentationOnly", true);
        storefront.SetMeta("designNote", "blue metal gable roof, white plaster and three existing glazed bays; kit shell, doors and collision unchanged");
        // These narrow bands tie the three real glazed bays together without
        // covering the wall or changing any window opening.
        AddVisualBox(storefront, "WindowHeadBand", new(halfWidth * 2, .10f, .035f),
            new(centreX, wallTop - .06f, front + .018f), "426f99", "wood_painted_blue");
        AddVisualBox(storefront, "WindowSillBand", new(halfWidth * 2, .11f, .04f),
            new(centreX, .72f * scale, front + .018f), "426f99", "wood_painted_blue");

        // The existing timber-backed sign hangs on the solid boarded gable,
        // below the roof slopes and above the street windows.
        var sign = FindDescendants<Node3D>(building).First(node => node.Name == "AshamlyklarSign");
        var hours = sign.GetNode<Node3D>("OpeningHoursPlate");
        sign.GlobalPosition = storefront.ToGlobal(new(centreX, wallTop + .52f * scale, front + .015f));
        // Keep the opening-hours plate readable at eye level on the street wall.
        var hoursBasis = hours.GlobalBasis;
        hours.Reparent(storefront);
        hours.GlobalBasis = hoursBasis;
        hours.Position = new(centreX + halfWidth - .55f, 1.55f, front + .012f);

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

        var bin = new MeshInstance3D
        {
            Name = "ShopBin", Mesh = new CylinderMesh { TopRadius = .22f, BottomRadius = .19f, Height = .62f, RadialSegments = 14 },
            Position = new(centreX + halfWidth - .25f, .31f, front + .45f),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("39433d", "metal")
        };
        storefront.AddChild(bin);
        AddVisualBox(storefront, "ShopBinSnow", new(.38f, .04f, .38f), new(centreX + halfWidth - .25f, .64f, front + .45f), "e4e7e3", "snow_ground");
        GD.Print(System.FormattableString.Invariant(
            $"act1-shop-storefront: reskinned={reskinned} gableSign={sign.GlobalPosition} entry={shop.Entrance}"));
    }
}
