using Godot;

namespace Urman.Godot;

/// <summary>Technical opacity masks for airborne ice and powder. These are
/// rendering resources, not generated artwork or a replacement for material maps.</summary>
public static class WinterParticleSurfaces
{
    private static Texture2D? _mask;
    public static QuadMesh Snow(float size, float opacity)
    {
        if (_mask is null)
        {
            var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var px = (x + .5f - 32f) / 32f;
                var py = (y + .5f - 32f) / 32f;
                var radius = Mathf.Sqrt(px * px + py * py);
                var edge = 1f - Mathf.SmoothStep(.18f, 1f, radius);
                var grain = .86f + .14f * Mathf.Sin(x * 1.7f + Mathf.Cos(y * 1.1f));
                image.SetPixel(x, y, new Color(1, 1, 1, edge * edge * grain));
            }
            _mask = ImageTexture.CreateFromImage(image);
        }
        return new QuadMesh
        {
            Size = new(size, size),
            Material = new StandardMaterial3D
            {
                AlbedoColor = new(.93f, .95f, .98f, opacity), AlbedoTexture = _mask,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = true
            }
        };
    }
    public static Gradient Fade() => new()
    {
        Colors = [new(1, 1, 1, 0), Colors.White, Colors.White, new(1, 1, 1, 0)],
        Offsets = [0, .12f, .65f, 1]
    };
}
