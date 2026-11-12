using System;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _babaiWingExterior;

    private void BuildBabaiWingExterior(Node3D facade)
    {
        var room = _zoneInstances["house_old_pc"];
        var wing = room.GetNode<Node3D>("BabaiHouseWing");
        var root = new Node3D { Name = "BabaiWingExterior" };
        facade.AddChild(root);
        // Rotate the actual wing plan so its kitchen's south portal meets the
        // east portal of the zal. The two leaves are on opposite faces of the
        // .17 m shared wall: room (3.965,2.75) and (4.135,2.75).
        root.GlobalTransform = room.GlobalTransform * new Transform3D(new Basis(Vector3.Up, -Mathf.Pi * .5f), new(7.60f, 0, 5.95f));
        _babaiWingExterior = root;
        root.SetMeta("babaiRelocated", true);
        root.SetMeta("presentationRole", "physical exterior projection of the existing kitchen, hall, bedroom, toilet and attic plan; saved underground interior coordinates unchanged");
        root.SetMeta("planSource", wing.GetPath().ToString());
        root.SetMeta("planSize", new Vector3(10.4f, 5.72f, 7.4f));
        var body = new StaticBody3D { Name = "WingExteriorContacts", CollisionLayer = 2, CollisionMask = 0 };
        root.AddChild(body); _facilityBodies.Add(body);
        var sourceFrame = wing.GlobalTransform.AffineInverse();
        var glass = new StandardMaterial3D { AlbedoColor = new Color(.70f, .79f, .83f, .17f),
            Roughness = .18f, MetallicSpecular = .55f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var index = 0;
        var sharedWallContacts = new System.Collections.Generic.Dictionary<StaticBody3D, MeshInstance3D>();
        // Share only immutable mesh/material resources, never scripts, portals,
        // actors, audio or stateful nodes. Windows now look into the real plan
        // and its furniture, not a painted room picture or an unrelated box.
        foreach (var source in FindDescendants<MeshInstance3D>(wing))
        {
            var mesh = new MeshInstance3D { Name = "PlanMesh" + index++, Mesh = source.Mesh,
                Transform = sourceFrame * source.GlobalTransform, MaterialOverride = source.MaterialOverride,
                CastShadow = source.CastShadow };
            for (var surface = 0; surface < source.Mesh.GetSurfaceCount(); surface++)
                if (source.GetSurfaceOverrideMaterial(surface) is { } material) mesh.SetSurfaceOverrideMaterial(surface, material);
            root.AddChild(mesh);
            mesh.SetMeta("sourcePlanMesh", source.GetPath().ToString());
            if (source.GetParent() is StaticBody3D wall && wall.Name.ToString().StartsWith("WingWallSouth", StringComparison.Ordinal))
            {
                CutBabaiMeshOpening(mesh, root, new Aabb(new(-3.75f, -.02f, 3.30f), new(1.10f, 2.13f, .60f)));
                sharedWallContacts[wall] = mesh;
            }
            if (source.Name != "Pane" || !source.GetParent().Name.ToString().Contains("Window", StringComparison.Ordinal)) continue;
            mesh.MaterialOverride = glass; mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var pane = source.Mesh.GetAabb();
            body.AddChild(new CollisionShape3D { Name = "Glazing" + index,
                Transform = mesh.Transform * new Transform3D(Basis.Identity, pane.GetCenter()),
                Shape = new BoxShape3D { Size = new(Mathf.Max(pane.Size.X, .018f), Mathf.Max(pane.Size.Y, .018f), Mathf.Max(pane.Size.Z, .018f)) } });
        }
        foreach (var source in FindDescendants<CollisionShape3D>(wing))
        {
            // Portal Area3D shapes and visual-only owners are not architecture.
            if (source.GetParent() is not StaticBody3D solid || solid.CollisionLayer == 0 || source.Disabled) continue;
            if (sharedWallContacts.TryGetValue(solid, out var piercedWall))
                body.AddChild(new CollisionShape3D { Name = "PlanContact" + index++, Shape = piercedWall.Mesh.CreateTrimeshShape(), Transform = piercedWall.Transform });
            else body.AddChild(new CollisionShape3D { Name = "PlanContact" + index++, Shape = source.Shape,
                Transform = sourceFrame * source.GlobalTransform });
        }
        // Real room lights are copied as light sources only. Their unchanged
        // furniture and window frames produce the shadows through clear glass.
        foreach (var source in FindDescendants<OmniLight3D>(wing).Where(l => l.ShadowEnabled))
            root.AddChild(new OmniLight3D { Name = "PlanLight" + index++, Transform = sourceFrame * source.GlobalTransform,
                LightColor = source.LightColor, LightEnergy = source.LightEnergy * .65f, OmniRange = source.OmniRange,
                ShadowEnabled = true, ShadowBias = .015f, LightSize = .08f });

        var logMaterial = HeroHouseMaterial(FindHeroHouseMesh("HeroHouse_Left_Log"), "8a7458", "wood_facade");
        var roofMaterial = HeroHouseMaterial(FindHeroHouseMesh("HeroHouse_Roof_"), "6f3f35", "roof_metal");
        var snowMaterial = HeroHouseMaterial(FindHeroHouseMesh("HeroHouse_RoofSnow"), "eef2f6", "snow_roof");
        void Box(string name, Vector3 size, Vector3 at, Material material, Vector3 rotation = default)
        {
            root.AddChild(new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = at,
                Rotation = rotation, MaterialOverride = material });
        }
        // Horizontal boards form the added wing's outer coat. Split around the
        // same openings as the interior, including the two attic windows.
        void Boards(string name, bool alongX, float at, float from, float to, float top, (float Center,float Width,float Bottom,float Top)[] openings)
        {
            for (var row = 0; row * .18f < top; row++)
            {
                var bottom = row * .18f; var height = Mathf.Min(.177f, top-bottom);
                var cursor = from; var part = 0;
                foreach (var opening in openings.OrderBy(o => o.Center))
                {
                    if (bottom >= opening.Top || bottom + height <= opening.Bottom) continue;
                    Span(cursor, opening.Center-opening.Width*.5f); cursor = opening.Center+opening.Width*.5f;
                }
                Span(cursor,to);
                void Span(float a,float b)
                {
                    if (b-a < .001f) return;
                    Box(name+row+"_"+part++, alongX ? new(b-a,height,.035f) : new(.035f,height,b-a),
                        alongX ? new((a+b)*.5f,bottom+height*.5f,at) : new(at,bottom+height*.5f,(a+b)*.5f), logMaterial);
                }
            }
        }
        Boards("WingNorthBoard",true,-3.72f,-5.2f,5.2f,3.72f,[(-3.2f,1.06f,1f,2.4f),(3.6f,1.06f,.74f,2.14f)]);
        Boards("WingSouthBoard",true,3.72f,-5.2f,5.2f,3.72f,[(-3.2f,1.10f,0f,2.11f)]);
        Boards("WingWestBoard",false,-5.22f,-3.5f,3.5f,3.72f,[(-.6f,1.06f,.74f,2.14f)]);
        Boards("WingEastBoard",false,5.22f,-3.5f,3.5f,3.72f,[(-2.6f,1.06f,.74f,2.14f),(2.55f,.46f,1.47f,1.93f)]);
        foreach (var source in FindDescendants<MeshInstance3D>(wing).Where(m => m.GetParent().Name.ToString().StartsWith("AtticRoof", StringComparison.Ordinal)
                     || m.GetParent().Name == "AtticDormerCeiling"))
        {
            var local = sourceFrame * source.GlobalTransform;
            var sheet = new MeshInstance3D { Name = "WingRoofSheet" + index++, Mesh = source.Mesh,
                Transform = local with { Origin = local.Origin + Vector3.Up * .035f }, MaterialOverride = roofMaterial };
            root.AddChild(sheet);
            root.AddChild(new MeshInstance3D { Name = "WingRoofSnow" + index++, Mesh = source.Mesh,
                Transform = local with { Origin = local.Origin + Vector3.Up * .080f }, MaterialOverride = snowMaterial });
        }
        // Foundation encloses the projected plan down to the local terrain.
        var bottomY = -.20f;
        foreach (var x in new[] { -5.25f, 0f, 5.25f }) foreach (var z in new[] { -3.75f, 0f, 3.75f })
        {
            var at = root.ToGlobal(new(x, 0, z));
            bottomY = Mathf.Min(bottomY, AgentBAct1HeightField.CollisionGround(at.X, at.Z) - root.GlobalPosition.Y - .06f);
        }
        Box("WingFoundation", new(10.5f,-bottomY,7.5f),new(0,bottomY*.5f,0),PainterlyMaterialLibrary.ForColor("817c6f","stone"));
        body.AddChild(new CollisionShape3D { Name="Foundation", Position=new(0,bottomY*.5f,0),Shape=new BoxShape3D { Size=new(10.5f,-bottomY,7.5f) } });
        // Retire just the old seni family now inside the larger occupied wing.
        var replaced = FindDescendants<MeshInstance3D>(facade).Where(m => m.Name.ToString().StartsWith("HeroHouse_",StringComparison.Ordinal)
            && m.Name.ToString().Contains("Seni",StringComparison.Ordinal)).ToArray();
        var paths = replaced.Select(m => m.GetPath().ToString()).ToHashSet(StringComparer.Ordinal);
        foreach (var old in replaced) { old.Visible=false; old.SetMeta("suppressionReason","replaced by full-size connected household wing"); }
        foreach (var contact in FindDescendants<CollisionShape3D>(this).Where(c => c.HasMeta("authoredSourceMesh") && paths.Contains(c.GetMeta("authoredSourceMesh").AsString())))
            contact.Disabled=true;
        var layer=GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        layer.RegisterWeatherShelter(root,new(0,2.85f,0),new(5.3f,2.95f,3.8f));
        layer.ReconcileBuildingFoliage(root);
    }

    private void ExcludeBabaiWingTerrain()
    {
        if (_babaiWingExterior is null) return;
        var layer = GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        layer.ExcludeOccupiedRoomTerrain(_babaiWingExterior,new(5.2f,3.7f),fitBabaiYard:false);
        foreach (var overlay in FindDescendants<MeshInstance3D>(layer.GetNode<Node3D>("AgentB_TerrainRoadKit"))
                     .Where(m => m.Name != "Terrain_Main" && m.Mesh is ArrayMesh).ToArray())
        {
            CutBabaiMeshOpening(overlay, _babaiWingExterior, new Aabb(new(-5.2f,-40f,-3.7f),new(10.4f,80f,7.4f)));
            var path = overlay.GetPath().ToString();
            foreach (var contact in FindDescendants<CollisionShape3D>(this).Where(c => c.HasMeta("authoredSourceMesh") && c.GetMeta("authoredSourceMesh").AsString() == path))
            {
                if (overlay.Mesh.GetSurfaceCount() == 0) contact.Disabled=true;
                else contact.Shape=overlay.Mesh.CreateTrimeshShape();
            }
        }
    }
}
