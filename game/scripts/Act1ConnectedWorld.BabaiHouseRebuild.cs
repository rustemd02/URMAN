using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _babaiNewExterior;

    private void BuildBabaiRebuiltHouse(Node3D facade)
    {
        var room = _zoneInstances["house_old_pc"];
        var oldMeshes = FindDescendants<MeshInstance3D>(facade).ToArray();
        var kit = facade.GetParent();
        var donor = FindDescendants<MeshInstance3D>(kit).Where(m =>
            m.Name.ToString().StartsWith("VariantA_TimberGable_Dwelling_", StringComparison.Ordinal)).ToArray();
        Material MaterialFrom(string token, string fallback, string surface) => HeroHouseMaterial(
            donor.FirstOrDefault(m => m.Name.ToString().Contains(token, StringComparison.Ordinal)), fallback, surface);
        var timber = MaterialFrom("_Log", "806344", "wood_facade");
        var roof = MaterialFrom("_Roof_LOD0", "704a40", "roof_metal");
        var snow = MaterialFrom("_RoofSnow", "dce4ea", "snow_roof");

        // Keep the original closed door and stationary photo references. Only
        // their retired geometry is hidden; IDs and the entry datum stay put.
        var retiredPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var old in oldMeshes)
        {
            if (old.Name == "HeroHouse_StreetDoorClosed_LOD0") continue;
            retiredPaths.Add(old.GetPath().ToString());
            old.Visible = false;
            old.SetMeta("heroWindowExteriorOriginalVisibility", false);
            old.SetMeta("babaiShellRetired", true);
        }
        foreach (var shape in FindDescendants<CollisionShape3D>(this))
        {
            var oldSource = shape.HasMeta("authoredSourceMesh")
                && retiredPaths.Contains(shape.GetMeta("authoredSourceMesh").AsString());
            var rear = shape.HasMeta(HeroRearBoundaryOwnerMeta)
                && shape.GetMeta(HeroRearBoundaryOwnerMeta).AsString() == HeroHouseRecipe.EntityId;
            if (!oldSource && !rear) continue;
            shape.Disabled = true;
            shape.SetMeta("babaiShellRetired", true);
        }
        var shell = new Node3D { Name = "BabaiWide_Dwelling" };
        facade.AddChild(shell);
        shell.GlobalTransform = room.GlobalTransform;
        shell.SetMeta("babaiRelocated", true);
        shell.SetMeta("assetSource", "VariantA_TimberGable materials and TimberHomeStyle joinery; expanded metric architecture");
        shell.SetMeta("occupiedInterior", room.GetPath().ToString());
        _babaiNewExterior = shell;
        facade.SetMeta("rebuiltBabaiHouse", true);
        BuildBabaiNewExteriorGeometry(shell, timber, roof, snow);
        BuildBabaiHouseFoundation(shell);
        TimberHomeStyle.DressParcel(facade, "babai-wide-house", allowAddedStorey: false);
        RestoreBabaiActiveWindowGlass();

        var weather = GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        // The roof's upper triangular volume is sheltered without declaring
        // the air outside its slopes an indoor room.
        weather.RegisterWeatherShelter(shell, new(0, 8.1f, -2f), new(3.0f, 1.2f, 5.7f));
        weather.ReconcileBuildingFoliage(shell);
        ExcludeBabaiRebuiltTerrain();
        PublishBabaiHouseFootprint();
    }

    private static void BuildBabaiHouseFoundation(Node3D shell)
    {
        var bottom = -.28f;
        foreach (var x in new[] { -6.65f, 0f, 6.65f })
        foreach (var z in new[] { -7.65f, -2f, 3.65f })
        {
            var at = shell.ToGlobal(new(x, 0, z));
            bottom = Math.Min(bottom, AgentBAct1HeightField.CollisionGround(at.X, at.Z) - shell.GlobalPosition.Y - .08f);
        }
        var material = PainterlyMaterialLibrary.ForColor("847d70", "stone");
        var body = new StaticBody3D { Name = "ContinuousHouseFooting", CollisionLayer = 1, CollisionMask = 0 };
        shell.AddChild(body);
        foreach (var (size, at) in new[] {
            (new Vector3(13.5f,-bottom,.30f),new Vector3(0,bottom*.5f,-7.6f)),
            (new Vector3(13.5f,-bottom,.30f),new Vector3(0,bottom*.5f,3.6f)),
            (new Vector3(.30f,-bottom,11.2f),new Vector3(-6.6f,bottom*.5f,-2)),
            (new Vector3(.30f,-bottom,11.2f),new Vector3(6.6f,bottom*.5f,-2)) })
        {
            body.AddChild(new MeshInstance3D { Mesh = RuralPropGeometry.BevelBox(size,.012f), Position = at, MaterialOverride = material });
            body.AddChild(new CollisionShape3D { Position = at, Shape = new BoxShape3D { Size = size } });
        }
    }

    private void ExcludeBabaiRebuiltTerrain()
    {
        if (_babaiNewExterior is not { } shell) return;
        var weather = GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        var footprint = new Node3D { Name = "BabaiHouseTerrainFootprint", Position = new(0, 0, -2f) };
        shell.AddChild(footprint);
        weather.ExcludeOccupiedRoomTerrain(footprint, new(6.5f, 5.5f), fitBabaiYard: false);
        var volume = new Aabb(new(-6.5f, -40, -7.5f), new(13, 80, 11));
        foreach (var overlay in FindDescendants<MeshInstance3D>(weather.GetNode<Node3D>("AgentB_TerrainRoadKit"))
                     .Where(m => m.Name != "Terrain_Main" && m.Mesh is ArrayMesh).ToArray())
        {
            var localBounds = shell.GlobalTransform.AffineInverse() * overlay.GlobalTransform * overlay.Mesh.GetAabb();
            if (!localBounds.Intersects(volume)) continue;
            CutBabaiMeshOpening(overlay, shell, volume);
            var path = overlay.GetPath().ToString();
            foreach (var contact in FindDescendants<CollisionShape3D>(this).Where(c =>
                         c.HasMeta("authoredSourceMesh") && c.GetMeta("authoredSourceMesh").AsString() == path))
            {
                if (overlay.Mesh.GetSurfaceCount() == 0) contact.Disabled = true;
                else contact.Shape = overlay.Mesh.CreateTrimeshShape();
            }
        }
    }
}
