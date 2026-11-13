using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void DressBabaiLandmarkHouse(Node3D shell)
    {
        // One household palette and the existing measured carving language.
        // The entrance, floor belts and gable give the street-facing house its
        // hierarchy; there are no invented inscriptions or symbolic emblems.
        var timber = PainterlyMaterialLibrary.ForColor("62503f", "wood_facade");
        var petrol = PainterlyMaterialLibrary.ForColor("365b61", "wood_painted_trim");
        var cream = PainterlyMaterialLibrary.ForColor("e1d8bd", "wood_painted_trim");
        var roof = PainterlyMaterialLibrary.ForColor("454e50", "roof_metal");
        var iron = PainterlyMaterialLibrary.ForColor("373d3b", "iron");
        foreach (var mesh in FindDescendants<MeshInstance3D>(shell))
        {
            var name = mesh.Name.ToString();
            if (name.StartsWith("Babai_Wall_", StringComparison.Ordinal))
                mesh.MaterialOverride = name.EndsWith("Gable", StringComparison.Ordinal) ? petrol : timber;
            else if ((name.StartsWith("CarvedTimber_", StringComparison.Ordinal)
                && name.EndsWith("_wood_painted_trim", StringComparison.Ordinal))
                || name.Contains("_Verge", StringComparison.Ordinal) || name.Contains("_Eave_", StringComparison.Ordinal)
                || name.EndsWith("_StructuralFrame", StringComparison.Ordinal) || name == "SashJoinery")
                mesh.MaterialOverride = cream;
            else if (name.StartsWith("BabaiRoof", StringComparison.Ordinal) && !name.Contains("Snow", StringComparison.Ordinal))
                mesh.MaterialOverride = roof;
        }

        var joinery = new Node3D { Name = "BabaiSignatureJoinery" };
        shell.AddChild(joinery);
        var contacts = new StaticBody3D { Name = "PorchStructureContacts", CollisionLayer = 1, CollisionMask = 0 };
        joinery.AddChild(contacts);
        var batches = new Dictionary<Material, SurfaceTool>();
        void Piece(Material material, Mesh mesh, Vector3 at, Vector3 rotation = default)
        {
            if (!batches.TryGetValue(material, out var surface))
            {
                surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
                batches.Add(material, surface);
            }
            surface.AppendFrom(mesh, 0, new Transform3D(Basis.FromEuler(rotation), at));
        }
        void Box(Material material, Vector3 size, Vector3 at, bool solid = false, Vector3 rotation = default)
        {
            Piece(material, RuralPropGeometry.BevelBox(size, Math.Min(.006f, Math.Min(size.X, Math.Min(size.Y, size.Z)) * .18f)), at, rotation);
            if (solid) contacts.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = at, Rotation = rotation });
        }
        var west = BabaiHousePlan.WallWest; var east = BabaiHousePlan.WallEast;
        var back = BabaiHousePlan.WallBack; var front = BabaiHousePlan.WallFront;
        var width = east - west;
        // Corner boards cover the log ends as a coherent carpentry joint.
        foreach (var x in new[] { west, east }) foreach (var z in new[] { back, front })
        {
            var sx = x < BabaiHousePlan.CentreX ? -1f : 1f;
            var sz = z < BabaiHousePlan.CentreZ ? -1f : 1f;
            Box(petrol, new(.30f, 7.22f, .065f), new(x, 3.65f, z + sz * .16f));
            Box(petrol, new(.065f, 7.22f, .30f), new(x + sx * .16f, 3.65f, z));
            Box(cream, new(.025f, 7.22f, .025f), new(x - sx * .12f, 3.65f, z + sz * .201f));
        }
        // Belts lie between storeys, clear of every opening and inward sash.
        foreach (var level in new[] { 2.8f, 5.6f })
        {
            foreach (var z in new[] { back, front })
            {
                var sign = z > BabaiHousePlan.CentreZ ? 1f : -1f;
                Box(petrol, new(width, .24f, .06f), new(BabaiHousePlan.CentreX, level, z + sign * .158f));
                foreach (var dy in new[] { -.13f, .13f })
                    Box(cream, new(width + .08f, .035f, .095f), new(BabaiHousePlan.CentreX, level + dy, z + sign * .17f));
            }
            foreach (var x in new[] { west, east })
            {
                var sign = x > BabaiHousePlan.CentreX ? 1f : -1f;
                Box(petrol, new(.06f, .24f, front - back - .30f), new(x + sign * .158f, level, BabaiHousePlan.CentreZ));
                foreach (var dy in new[] { -.13f, .13f })
                    Box(cream, new(.095f, .035f, front - back - .25f), new(x + sign * .17f, level + dy, BabaiHousePlan.CentreZ));
            }
        }

        var door = StyleBenchmarkInteriorFactory.DoorX;
        foreach (var side in new[] { -1f, 1f })
        {
            Box(cream, new(.11f, 2.3f, .10f), new(door + side * .705f, 1.15f, front + .17f));
            // Turned posts carry the roof; the short brace really meets the
            // post and header, leaving the entire door-width approach clear.
            var column = RuralPropGeometry.Lathe("BabaiEntranceTurnedPost",
                [new(.082f,0), new(.082f,.13f), new(.062f,.16f), new(.062f,.62f),
                 new(.084f,.66f), new(.084f,.72f), new(.058f,.77f), new(.058f,1.86f),
                 new(.084f,1.91f), new(.084f,1.98f), new(.068f,2.03f), new(.068f,2.44f)], 12);
            var at = new Vector3(door + side * 1.04f, 0, 4.82f);
            Piece(petrol, column, at);
            contacts.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = .085f, Height = 2.44f }, Position = at + Vector3.Up * 1.22f });
            foreach (var y in new[] { .12f, .70f, 1.95f, 2.38f })
                Box(cream, new(.185f,.045f,.185f),at + Vector3.Up * y);
            Box(cream,new(.105f,.53f,.105f),new(door + side * .86f,2.20f,4.82f),rotation:new(0,0,side * -.72f));
            Box(petrol,new(.11f,.14f,1.35f),new(door + side * 1.04f,2.43f,4.21f),true);
            Box(petrol,new(.10f,.205f,.12f),new(door + side * 1.04f,2.60f,4.82f));
        }
        Box(cream,new(1.52f,.10f,.10f),new(door,2.30f,front + .17f));
        Box(petrol,new(2.27f,.15f,.15f),new(door,2.44f,4.82f),true);
        Box(cream,new(2.36f,.055f,.19f),new(door,2.53f,4.82f));
        Box(petrol,new(.09f,.65f,.12f),new(door,2.855f,4.82f));

        const float run = 1.45f, rise = .72f;
        var angle = Mathf.Atan2(rise, run); var length = Mathf.Sqrt(run * run + rise * rise);
        var snow = PainterlyMaterialLibrary.ForColor("dce4ea", "snow_roof");
        foreach (var side in new[] { -1f, 1f })
        {
            var rotation = new Vector3(0,0,-side * angle);
            var normal = new Vector3(side * Mathf.Sin(angle),Mathf.Cos(angle),0);
            var mid = new Vector3(door + side * run * .5f,3.25f-rise*.5f,4.30f);
            Box(petrol,new(length,.065f,1.90f),mid-normal*.035f,true,rotation);
            Box(roof,new(length,.055f,1.90f),mid+normal*.028f,false,rotation);
            Box(snow,new(length,.065f,1.90f),mid+normal*.096f,false,rotation);
            Box(cream,new(length,.13f,.08f),mid with { Z = 5.26f },rotation:rotation);
            // The familiar short cut teeth follow the rake as actual open
            // gaps, never a texture or a solid triangular panel over the entry.
            for (var i=0;i<10;i++)
            {
                var along = .10f + i*.135f;
                var x = door + side*along;
                var y = 3.25f-along*(rise/run)-.125f;
                Box(cream,new(.055f,.10f+(i%2)*.025f,.065f),new(x,y,5.27f),rotation:rotation);
            }
        }
        Box(cream,new(.075f,.25f,.075f),new(door,3.35f,5.27f));
        // A narrow boot bench sits against the side, not in the entrance lane.
        Box(timber,new(.30f,.055f,.68f),new(door-.91f,.46f,4.18f),true);
        foreach(var z in new[]{3.92f,4.44f})
            Box(petrol,new(.23f,.43f,.065f),new(door-.91f,.215f,z),true);

        // One warm, shielded everyday porch lamp. No new writing or street prop.
        var lampAt = new Vector3(door+.86f,1.97f,front+.24f);
        Box(iron,new(.09f,.24f,.09f),lampAt + new Vector3(0,0,-.035f));
        var glass = new StandardMaterial3D { AlbedoColor=new("edcf91"), EmissionEnabled=true,
            Emission=new Color("e9ba6f"), EmissionEnergyMultiplier=.7f, Roughness=.45f };
        Box(glass,new(.065f,.15f,.025f),lampAt + new Vector3(0,0,.028f));
        joinery.AddChild(new OmniLight3D { Name="PorchWelcomeLight", Position=lampAt+Vector3.Back*.12f,
            LightColor=new("f1d5aa"), LightEnergy=.40f, OmniRange=3.2f, ShadowEnabled=false });
        foreach (var (material,surface) in batches)
        {
            surface.Index();
            joinery.AddChild(new MeshInstance3D { Name="JoineryBatch"+joinery.GetChildCount(), Mesh=surface.Commit(), MaterialOverride=material });
            surface.Dispose();
        }
        shell.SetMeta("householdPalette", "dark timber / petrol / warm ivory / graphite");
        shell.SetMeta("streetIdentity", "carved covered entrance; continuous floor belts; painted gable");
    }
}
