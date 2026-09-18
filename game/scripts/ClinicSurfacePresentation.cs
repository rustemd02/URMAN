using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>Optical finishes for the clinic's existing panes and wash-station mirror.</summary>
public static class ClinicSurfacePresentation
{
    // These additional render layers do not replace the meshes' normal camera
    // layers. This room's large static surfaces and existing practical lights
    // enter the cached reflection; only the mirror receives it.
    private const uint ReflectionSourceLayer = 1u << 18;
    private const uint MirrorReceiverLayer = 1u << 19;
    private const string ProbeName = "ClinicStaticRoomReflection";
    private const string ExteriorVisibilityKey = "clinicExteriorGlassOriginalVisibility";

    private static readonly string[] InteriorGlassNames =
    [
        "FapInteriorShell_WindowFrontLeftGlass_LOD0",
        "FapInteriorShell_WindowFrontRightGlass_LOD0",
        "FapInteriorShell_WindowLeftGlass_LOD0",
        "FapInteriorShell_WindowRightGlass_LOD0"
    ];

    private static readonly string[] ExteriorGlassNames =
    [
        "FapFacade_WindowLeft_Glass_LOD0",
        "FapFacade_WindowRight_Glass_LOD0",
        "FapFacade_SideWindowLeft_Glass_LOD0",
        "FapFacade_SideWindowRight_Glass_LOD0",
        "FapFacade_WindowRight_WarmInset_LOD0"
    ];

    private static readonly Shader WinterGlassShader = new()
    {
        Code = """
            shader_type spatial;
            render_mode blend_mix, cull_back, diffuse_burley, specular_schlick_ggx;
            uniform vec3 pane_origin;
            uniform vec3 pane_right;
            uniform vec2 pane_size;
            varying vec2 pane_uv;
            void vertex() {
                vec3 point = VERTEX - pane_origin;
                pane_uv = vec2(dot(point, pane_right), point.y) / pane_size + vec2(0.5);
            }
            void fragment() {
                // Same winter-glass treatment as HeroRoom windows. The source
                // clinic meshes have no UV channel, so vertex projection above
                // supplies stable, correctly scaled coordinates on either wall.
                vec2 edge_distance = min(pane_uv, vec2(1.0) - pane_uv);
                float edge = min(edge_distance.x, edge_distance.y);
                float scallop = 0.007 * sin(pane_uv.x * 49.0 + sin(pane_uv.y * 23.0));
                float frost = 1.0 - smoothstep(0.015, 0.075 + scallop, edge);
                ALBEDO = mix(vec3(0.54, 0.66, 0.69), vec3(0.86, 0.91, 0.90), frost);
                ROUGHNESS = mix(0.20, 0.79, frost);
                SPECULAR = mix(0.46, 0.12, frost);
                ALPHA = mix(0.095, 0.64, frost);
            }
            """
    };

    /// <summary>Call after the existing room and source contacts are assembled.</summary>
    public static void Attach(Node3D room, Node3D authoredSet)
    {
        if (room.HasMeta("clinicOpticalSurfacesAttached")) return;
        var meshes = Descendants(authoredSet).OfType<MeshInstance3D>().ToArray();
        foreach (var name in InteriorGlassNames)
        {
            var pane = meshes.Single(mesh => mesh.Name == name);
            var bounds = pane.Mesh?.GetAabb()
                ?? throw new InvalidOperationException($"Clinic pane has no source mesh: {name}.");
            var side = bounds.Size.X < bounds.Size.Z;
            var size = new Vector2(side ? bounds.Size.Z : bounds.Size.X, bounds.Size.Y);
            if (size.X < 1f || size.Y < 1f)
                throw new InvalidOperationException($"Clinic pane has unexpected physical dimensions: {name} {size}.");
            var material = new ShaderMaterial { Shader = WinterGlassShader };
            material.SetShaderParameter("pane_origin", bounds.GetCenter());
            material.SetShaderParameter("pane_right", side ? Vector3.Back : Vector3.Right);
            material.SetShaderParameter("pane_size", size);
            pane.MaterialOverride = material;
            pane.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            pane.SetMeta("opticalSurface", "closed source pane; edge frost; actual exterior through clear center");
            pane.SetMeta("panePhysicalSize", size);
        }

        // Both distances of the folded towel share FapNoticeBlank with paper
        // in the source kit; only these consumers need the woven cloth finish.
        var towelMaterial = PainterlyMaterialLibrary.ForColor("c9c6b7", "fabric_pattern", sheltered: true);
        foreach (var name in new[] { "FapInteriorWashUnit_Towel_LOD0", "FapInteriorWashUnit_Towel_LOD1" })
        {
            var towel = meshes.Single(mesh => mesh.Name == name);
            towel.MaterialOverride = towelMaterial;
            towel.SetMeta("materialRole", "wash-station woven towel; source paper family retained on documents");
        }

        // The original kit shares its painted-metal material across fixtures.
        // Only the actual bowl needs a pale enamel surface; the faucet and
        // waste strainer retain a separate metal finish at both detail levels.
        var enamel = (ShaderMaterial)PainterlyMaterialLibrary
            .ForColor("c3c8bf", "iron", sheltered: true).Duplicate();
        enamel.SetShaderParameter("roughness_value", .32f);
        enamel.SetShaderParameter("metallic_value", 0f);
        enamel.SetShaderParameter("specular_value", .45f);
        enamel.SetShaderParameter("variation", .018f);
        enamel.SetShaderParameter("finish_grain", .025f);
        var faucetFinish = (ShaderMaterial)PainterlyMaterialLibrary
            .ForColor("8b9794", "iron", sheltered: true).Duplicate();
        faucetFinish.SetShaderParameter("roughness_value", .27f);
        faucetFinish.SetShaderParameter("metallic_value", .8f);
        faucetFinish.SetShaderParameter("variation", .015f);
        faucetFinish.SetShaderParameter("finish_grain", .025f);
        foreach (var mesh in meshes)
        {
            var name = mesh.Name.ToString();
            if (name.StartsWith("FapInteriorWashUnit_Basin_", StringComparison.Ordinal))
            {
                mesh.MaterialOverride = enamel;
                mesh.SetMeta("materialRole", "pale enamel on the actual concave wash bowl");
            }
            else if (name.StartsWith("FapInteriorWashUnit_Faucet", StringComparison.Ordinal)
                || name.StartsWith("FapInteriorWashUnit_BasinInset_", StringComparison.Ordinal))
            {
                mesh.MaterialOverride = faucetFinish;
                mesh.SetMeta("materialRole", "compact mixer or bowl-floor waste strainer");
            }
        }

        var mirror = Descendants(room.GetNode<Node3D>("ClinicPlainMirror"))
            .OfType<MeshInstance3D>().Single();
        var silver = (ShaderMaterial)PainterlyMaterialLibrary
            .ForColor("a7afaa", "iron", sheltered: true).Duplicate();
        silver.SetShaderParameter("roughness_value", 0.05f);
        silver.SetShaderParameter("metallic_value", 0.93f);
        silver.SetShaderParameter("specular_value", 0.5f);
        silver.SetShaderParameter("variation", 0.025f);
        silver.SetShaderParameter("finish_grain", 0.02f);
        mirror.MaterialOverride = silver;
        mirror.Layers |= MirrorReceiverLayer;
        mirror.SetMeta("opticalSurface", "worn silver; cached reflection of actual static clinic geometry");

        var reflectionSources = 0;
        foreach (var mesh in meshes.Where(IsReflectionSource))
        {
            mesh.Layers |= ReflectionSourceLayer;
            reflectionSources++;
        }
        // A probe's cull mask also filters Light3D instances. Leaving these
        // existing lamps on layer 1 made the isolated room capture unlit.
        // Keep their normal layer, energy and geometry cull mask intact.
        var reflectionLights = Descendants(room).OfType<Light3D>().ToArray();
        foreach (var light in reflectionLights)
            light.Layers |= ReflectionSourceLayer;
        room.AddChild(new ReflectionProbe
        {
            Name = ProbeName,
            Position = new(0, 1.65f, 0),
            Size = new(11.62f, 3.38f, 11.62f),
            MaxDistance = 9f,
            // Godot 4.7.1 clears the entire Environment for interior probe
            // captures, losing this room's actual ambient illumination. Use
            // that same environment; AmbientMode below adds no diffuse override.
            Interior = false,
            BoxProjection = true,
            BlendDistance = 0.05f,
            AmbientMode = ReflectionProbe.AmbientModeEnum.Disabled,
            UpdateMode = ReflectionProbe.UpdateModeEnum.Once,
            EnableShadows = false,
            CullMask = ReflectionSourceLayer,
            ReflectionMask = MirrorReceiverLayer,
            Intensity = 0.85f,
            Visible = false
        });
        room.SetMeta("clinicOpticalSurfacesAttached", true);
        room.SetMeta("clinicWindowPaneCount", InteriorGlassNames.Length);
        room.SetMeta("clinicReflectionSourceCount", reflectionSources);
        room.SetMeta("clinicReflectionLightCount", reflectionLights.Length);
        room.SetMeta("clinicReflectionPolicy", "one static room capture; no actors, outside world, ambient override or per-frame scene render");
    }

    /// <summary>Call after the active room's environment, lights and visibility are set.</summary>
    public static void SetClinicActive(Node3D room, bool active)
    {
        if (room.GetNodeOrNull<ReflectionProbe>(ProbeName) is { } probe)
            probe.Visible = active;
    }

    /// <summary>Only hide the matched exterior cards while the real clinic room is active.</summary>
    public static void SetExteriorGlassVisible(Node3D facade, bool visible)
    {
        foreach (var mesh in Descendants(facade).OfType<MeshInstance3D>()
            .Where(mesh => ExteriorGlassNames.Contains(mesh.Name.ToString(), StringComparer.Ordinal)))
        {
            if (!mesh.HasMeta(ExteriorVisibilityKey))
                mesh.SetMeta(ExteriorVisibilityKey, mesh.Visible);
            mesh.Visible = visible && mesh.GetMeta(ExteriorVisibilityKey).AsBool();
        }
    }

    private static bool IsReflectionSource(MeshInstance3D mesh)
    {
        var name = mesh.Name.ToString();
        if (mesh.Mesh is null || !name.EndsWith("_LOD0", StringComparison.Ordinal)
            || InteriorGlassNames.Contains(name, StringComparer.Ordinal)) return false;
        return name.StartsWith("FapInteriorShell_", StringComparison.Ordinal)
            || name.StartsWith("FapInteriorBench_", StringComparison.Ordinal)
            || name.StartsWith("FapInteriorCot_", StringComparison.Ordinal)
            || name.StartsWith("FapInteriorCabinet_", StringComparison.Ordinal)
            || name.StartsWith("FapInteriorReceptionCounter_", StringComparison.Ordinal)
            || name.StartsWith("FapInteriorRecordsDesk_", StringComparison.Ordinal)
            || name.StartsWith("FapInteriorPartition_", StringComparison.Ordinal);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
