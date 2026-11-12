using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void DressMosqueWindowDepth(Node3D window, MeshInstance3D glass, Vector2 size)
    {
        // The 55 cm wall has a real aperture. Reveal boards connect its two
        // faces, so sunlight and the hall lamps cast actual frame shadows.
        // Keep the original glass contact and all existing mullion names.
        var timber = MosqueFinish("walnut", "eee1c5", .9f);
        foreach (var x in new[] { -size.X * .5f, size.X * .5f })
            AddVisualBox(window, "WindowJambReveal" + x, new(.035f, size.Y, .48f), new(x, 0, 0), "d4c7aa", "wood_furniture").MaterialOverride = timber;
        foreach (var y in new[] { -size.Y * .5f, size.Y * .5f })
            AddVisualBox(window, "WindowHeadReveal" + y, new(size.X, .035f, .48f), new(0, y, 0), "d4c7aa", "wood_furniture").MaterialOverride = timber;
        foreach (var z in new[] { -.195f, .195f })
        {
            foreach (var x in new[] { -size.X * .5f, 0, size.X * .5f })
                MosqueWood(window, "SashStile" + z + "_" + x, new(.045f, size.Y, .055f), new(x, 0, z), tint:"eee1c5");
            foreach (var y in new[] { -size.Y * .5f, size.Y * .20f, size.Y * .5f })
                MosqueWood(window, "SashRail" + z + "_" + y, new(size.X, .045f, .055f), new(0, y, z), tint:"eee1c5");
        }
        glass.MaterialOverride = new StandardMaterial3D {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(.87f, .93f, .92f, .11f), Roughness = .08f,
            MetallicSpecular = .65f, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        glass.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        window.SetMeta("windowDepth", "recessed glass; joined reveals; double timber sash; real light shadows");
    }
}
