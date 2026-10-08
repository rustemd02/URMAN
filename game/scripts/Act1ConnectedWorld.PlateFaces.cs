using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The school's gable board carries a painted enamel plate instead of the
/// plain painted board and its localised label: one textured quad from a flat
/// ChatGPT unwrap (game/assets/textures/act1/README.md), with the board still
/// acting as its backing, exactly as on the shop's АШАМЛЫКЛАР sign and the
/// FAP's state plate. The wording follows the 1992 Tatarstan rule that a sign
/// carries a Tatar line over a Russian line; both lines are open in the Act I
/// language review sheet. Presentation only: the door, the address, the
/// interaction and the collision are untouched.
/// </summary>
public partial class Act1ConnectedWorld
{
    private void BuildPlateFaces()
    {
        // face, board size, UV window that crops the generator's margins.
        var plates = new (string Id, string Face, float Width, float Height, Vector2 UvScale, Vector2 UvOffset)[]
        {
            ("school", "school_plate_maktap_v1.png", 1.70f, .225f, new(.9816f, .4503f), new(.0092f, .2680f)),
            ("council", "council_plate_avyl_sovety_v1.png", 1.48f, .225f, new(.9894f, .4140f), new(.0053f, .3254f))
        };
        foreach (var (id, face, width, height, uvScale, uvOffset) in plates)
        {
            var building = _publicBuildings.Single(entry => entry.Id == id).Building;
            var fascia = FindDescendants<Node3D>(building).Single(node => node.Name == id + "BuildingSign");
            foreach (var child in fascia.GetChildren().OfType<Node3D>().ToArray())
            {
                // The painted board stays as the backing; the localised content
                // label gives way to the painted face.
                if (child.Name == "PaintedBoard") continue;
                child.SetMeta("suppressionReason", "the plain board reads as the assembled enamel plate");
                child.Visible = false;
            }
            AddPlateFace(fascia, "PaintedPlateFace", face, new(width, height), new(0, 0, .0175f), uvScale, uvOffset);
            fascia.SetMeta("presentationOnly", true);
            fascia.SetMeta("plateWording", "МӘКТӘП / ШКОЛА; the content label `urman.chapter1:text/school-building-sign` still names the building");
            fascia.SetMeta("languageReview", "open; the Tatar line needs a native speaker (act1_language_review_sheet)");
        }
    }

    private static void AddPlateFace(Node3D parent, string name, string face, Vector2 size, Vector3 position,
        Vector2 uvScale, Vector2 uvOffset)
    {
        var path = "res://assets/textures/act1/" + face;
        var texture = GD.Load<Texture2D>(path)
            ?? throw new InvalidOperationException("The painted plate face is missing: " + path);
        parent.AddChild(new MeshInstance3D
        {
            Name = name, Mesh = new QuadMesh { Size = size }, Position = position,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = texture, Roughness = .72f, Metallic = 0f, // VIS-095: painted plate
                Uv1Scale = new(uvScale.X, uvScale.Y, 1f), Uv1Offset = new(uvOffset.X, uvOffset.Y, 0f),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            }
        });
    }
}
