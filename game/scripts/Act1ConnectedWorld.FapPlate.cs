using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The FAP's state plate by the door (author 2026-09-25): a typical bilingual
/// white-and-blue Tatarstan institution plate, «КАРА-УРМАН ФЕЛЬДШЕР-АКУШЕРЛЫК
/// ПУНКТЫ / КАРА-УРМАНСКИЙ ФЕЛЬДШЕРСКО-АКУШЕРСКИЙ ПУНКТ» with the hours, and no
/// district or hospital named. The face is a flat ImageGen unwrap
/// (textures/act1/README.md) on a 600 × 400 mm composite panel screwed to the
/// wall left of the entrance. Presentation only.
/// </summary>
public partial class Act1ConnectedWorld
{
    private void BuildFapPlate()
    {
        var facade = GetNode<Node3D>("Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredFacade");
        Aabb Global(string suffix)
        {
            var mesh = FindDescendants<MeshInstance3D>(facade).First(m => m.Name.ToString().EndsWith(suffix, StringComparison.Ordinal));
            return mesh.GlobalTransform * mesh.GetAabb();
        }
        var panel = Global("_DoorPanel_LOD0");
        var frames = new[] { Global("_DoorFrameLeft_LOD0"), Global("_DoorFrameRight_LOD0") };
        var door = panel.GetCenter();
        // Outward: the door leaf's thin horizontal axis, pointing away from the
        // middle of the facade's whole mass.
        var mass = FindDescendants<MeshInstance3D>(facade).Select(m => m.GlobalTransform * m.GetAabb())
            .Aggregate((a, b) => a.Merge(b)).GetCenter();
        var outward = (panel.Size.X < panel.Size.Z ? Vector3.Right : Vector3.Back) is var thin
            && thin.Dot(door - mass) < 0 ? -thin : thin;
        var right = (-outward).Cross(Vector3.Up);
        float Along(Aabb box, Vector3 axis, bool max) => Enumerable.Range(0, 8)
            .Select(i => box.GetEndpoint(i).Dot(axis)).Aggregate(max ? float.MinValue : float.MaxValue, (a, b) => max ? Math.Max(a, b) : Math.Min(a, b));
        var leftEdge = frames.Min(f => Along(f, right, false));
        // The wall plane is the facade body's outer face. The door frames reach
        // 17 cm past it, so their own extreme would float the plate off the
        // wall, and their inner extreme buries it inside the plaster.
        var wall = Along(Global("_Body_LOD0"), outward, true);
        var centre = right * (leftEdge - .42f) + outward * (wall + .022f) + Vector3.Up * (panel.Position.Y + 1.62f);
        centre += door - right * door.Dot(right) - outward * door.Dot(outward) - Vector3.Up * door.Y;

        var plate = new Node3D { Name = "FapStatePlate" };
        facade.AddChild(plate);
        plate.GlobalTransform = new Transform3D(new Basis(right, Vector3.Up, outward), centre);
        plate.SetMeta("presentationOnly", true);
        plate.SetMeta("languageReview", "open; Tatar line and hours need a native speaker (act1_language_review_sheet)");
        var backing = AddVisualBox(plate, "Panel", new(.60f, .40f, .006f), new(0, 0, .003f), "e8e8e2", "metal");
        backing.MaterialOverride = PainterlyMaterialLibrary.ForColor("e8e8e2", "metal", sheltered: true);
        plate.AddChild(new MeshInstance3D
        {
            Name = "Face", Mesh = new QuadMesh { Size = new(.60f, .40f) }, Position = new(0, 0, .0065f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = GD.Load<Texture2D>("res://assets/textures/act1/fap_plate_kara_urman_v1.png"),
                Roughness = .55f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            }
        });
        GD.Print(System.FormattableString.Invariant($"act1-fap-plate: at={centre} outward={outward}"));
    }
}
