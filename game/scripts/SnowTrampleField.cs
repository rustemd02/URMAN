using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only snow trample field for the winter Act I exterior.
///
/// The player compresses the snow: every step stamps a soft footprint into a
/// rolling world-space mask that the painterly ground shader samples to darken,
/// flatten and de-gloss the packed trail. No collision, navigation, interaction
/// or runtime-state ownership; the mask is session presentation only and is
/// never saved.
/// </summary>
public partial class SnowTrampleField : Node3D
{
    private const float WindowExtent = 48f;
    private const float StampSpacing = 0.55f;
    private const float FootLateralOffset = 0.18f;
    private const int HighResolution = 1024;
    private const int LowResolution = 512;

    private Image _mask = null!;
    private ImageTexture _texture = null!;
    private Image _footprint = null!;
    private readonly List<(Vector2 Position, float Rotation, float Pack)> _stamps = new();
    private Vector2 _windowCentre = new(float.MaxValue, float.MaxValue);
    private Vector2 _lastStampPosition = new(float.MaxValue, float.MaxValue);
    private float _footSide;
    private bool _dirty;
    private bool _enabled = true;
    private CpuParticles3D? _puffs;
    private FirstPersonController? _player;

    public override void _Ready()
    {
        SetMeta("presentationOnly", true);
        SetMeta("visualOnly", true);
        SetMeta("collisionOwner", "none");
        SetMeta("navigationOwner", "none");
        SetMeta("interactionOwner", "none");
        SetMeta("runtimeStateOwnership", "RuntimeBridge");
        SetMeta(
            "snowTramplePolicy",
            "session-only presentation mask; packed footprints and trails follow the player; never saved");

        var resolution = PainterlyMaterialLibrary.LowQualityMaterials ? LowResolution : HighResolution;
        _mask = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);
        _mask.Fill(new Color(0f, 0f, 0f, 0f));
        _texture = ImageTexture.CreateFromImage(_mask);
        _footprint = BuildFootprintStamp(resolution);
        _puffs = BuildPuffs();
        AddChild(_puffs);
        SetMeta("snowTrampleResolution", resolution);
    }

    /// <summary>Exterior presentation follows the same door as weather.</summary>
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (_puffs is not null)
        {
            _puffs.Emitting = false;
        }
    }

    public override void _Process(double delta)
    {
        if (!_enabled)
        {
            return;
        }

        _player ??= GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (_player is null || !IsInstanceValid(_player))
        {
            return;
        }

        var position = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
        if (_lastStampPosition.X > float.MaxValue * 0.5f)
        {
            _lastStampPosition = position;
            _windowCentre = position;
        }

        if (_lastStampPosition.DistanceTo(position) >= StampSpacing)
        {
            AddStamp(position);
            _lastStampPosition = position;
        }

        var half = WindowExtent * 0.5f;
        if (Mathf.Abs(position.X - _windowCentre.X) > half * 0.45f
            || Mathf.Abs(position.Y - _windowCentre.Y) > half * 0.45f)
        {
            _windowCentre = position;
            _dirty = true;
        }

        if (_dirty)
        {
            Redraw();
            _dirty = false;
        }
    }

    private void AddStamp(Vector2 position)
    {
        // Alternate feet with a small lateral offset and rotation jitter so
        // the trail reads as a walk, not as a dotted line.
        _footSide = -_footSide;
        var direction = position - _lastStampPosition;
        var lateral = direction.LengthSquared() > 1e-4f
            ? new Vector2(-direction.Y, direction.X).Normalized() * (_footSide * FootLateralOffset)
            : Vector2.Zero;
        var rotation = Mathf.Atan2(direction.Y, direction.X);
        _stamps.Add((position + lateral, rotation, 0.65f + (float)GD.RandRange(-0.12, 0.18)));
        _lastStampPosition = position;
        _dirty = true;

        if (_puffs is not null && _player is not null && !_player.ReducedMotion)
        {
            _puffs.GlobalPosition = new Vector3(position.X, (float)Experiments.AgentBAct1.AgentBAct1HeightField.Ground(position.X, position.Y) + 0.06f, position.Y);
            _puffs.Restart();
            _puffs.Emitting = true;
        }
    }

    private void Redraw()
    {
        _mask.Fill(new Color(0f, 0f, 0f, 0f));
        var half = WindowExtent * 0.5f;
        var resolution = _mask.GetWidth();
        var pixelsPerMetre = resolution / WindowExtent;
        var stampSize = _footprint.GetWidth();
        foreach (var (position, rotation, pack) in _stamps)
        {
            var local = position - (_windowCentre - new Vector2(half, half));
            if (local.X < -1f || local.Y < -1f
                || local.X > WindowExtent + 1f || local.Y > WindowExtent + 1f)
            {
                continue;
            }

            var centre = new Vector2(local.X * pixelsPerMetre, local.Y * pixelsPerMetre);
            var destination = new Vector2I(
                Mathf.RoundToInt(centre.X - stampSize * 0.5f),
                Mathf.RoundToInt(centre.Y - stampSize * 0.5f));
            // _mask.BlendRect ignores rotation; a small fixed footprint still
            // reads correctly and keeps the redraw native and cheap. Packing
            // value is carried by the stamp alpha.
            _mask.BlendRect(_footprint, new Rect2I(0, 0, stampSize, stampSize), destination);
        }

        _texture.Update(_mask);
        PainterlyMaterialLibrary.SetSnowTrample(
            _texture,
            _windowCentre - new Vector2(half, half),
            WindowExtent);
        SetMeta("snowTrampleStampCount", _stamps.Count);
        SetMeta("snowTrampleWindowCentre", $"{_windowCentre.X:F1},{_windowCentre.Y:F1}");
    }

    private static Image BuildFootprintStamp(int maskResolution)
    {
        // Soft pressed ellipse; size follows the mask resolution so a footprint
        // keeps its real-world ~0.3 x 0.42 m size at 1024 or 512.
        var pixelsPerMetre = maskResolution / WindowExtent;
        var width = Mathf.Max(4, Mathf.RoundToInt(0.30f * pixelsPerMetre));
        var height = Mathf.Max(5, Mathf.RoundToInt(0.42f * pixelsPerMetre));
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var u = (x + 0.5f) / width * 2f - 1f;
                var v = (y + 0.5f) / height * 2f - 1f;
                var radius = Mathf.Sqrt(u * u + v * v);
                var alpha = Mathf.Clamp(1f - radius, 0f, 1f);
                alpha = alpha * alpha * 0.85f;
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        return image;
    }

    private static CpuParticles3D BuildPuffs()
    {
        var quad = new QuadMesh { Size = new Vector2(0.12f, 0.12f) };
        quad.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.96f, 0.99f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        return new CpuParticles3D
        {
            Name = "SnowStepPuffs",
            Amount = 10,
            OneShot = true,
            Explosiveness = 0.96f,
            Lifetime = 0.55,
            LocalCoords = false,
            Mesh = quad,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.16f,
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 55f,
            Gravity = new Vector3(0f, -1.6f, 0f),
            InitialVelocityMin = 0.35f,
            InitialVelocityMax = 0.9f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.2f,
            Emitting = false
        };
    }
}
