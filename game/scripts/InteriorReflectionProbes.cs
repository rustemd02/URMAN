using Godot;

namespace Urman.Godot;

/// <summary>
/// P2 / VIS-098/099/101: one static reflection capture per hero room, so varnished
/// floors, painted trim, glass and enamel pick up the room's own light instead of a
/// flat sky. Same contract as the clinic probe (ClinicSurfacePresentation): Godot
/// 4.7.1 clears the Environment for "interior" captures, so the probe keeps the
/// room's environment (Interior=false), adds no ambient (AmbientMode Disabled),
/// box-projects onto the room and renders once.
/// </summary>
public static class InteriorReflectionProbes
{
    public static ReflectionProbe Add(Node3D room, string name, Vector3 centre, Vector3 size, float intensity = .8f)
    {
        var probe = new ReflectionProbe
        {
            Name = name,
            Position = centre,
            Size = size,
            MaxDistance = Mathf.Max(size.X, size.Z),
            Interior = false,
            BoxProjection = true,
            BlendDistance = .25f,
            AmbientMode = ReflectionProbe.AmbientModeEnum.Disabled,
            UpdateMode = ReflectionProbe.UpdateModeEnum.Once,
            EnableShadows = false,
            Intensity = intensity
        };
        room.AddChild(probe);
        room.SetMeta("roomReflectionProbe", probe.Name);
        return probe;
    }
}
