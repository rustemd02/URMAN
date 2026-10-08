using Godot;
using System.Collections.Generic;

namespace Urman.Godot;

/// <summary>
/// VIS-105 — the one place that decides how a vehicle finish answers the light.
///
/// Every transport surface resolves through the village's own painterly response
/// library (VIS-092) with the metal rules of VIS-095: paint carries no metal at
/// all, exposed steel and bright trim differ by roughness and highlight shape,
/// the tyre is the dullest and darkest member of the set. Nothing here is a
/// colour decision — colours still come from the authored hex — and nothing is
/// an asset-pack PBR set: no ORM, no 4K obligation, no new render framework.
///
/// The old path gave the body a metallic enamel trick and left `metal`, `rubber`
/// and the fallback a flat untuned default, so a hero closeup could show
/// material-less grey and the car sat outside the village frame. An unmapped
/// finish name is now an error at build time instead of a silent grey.
/// </summary>
public static partial class VehicleVisualFactory
{
    private static readonly Dictionary<string, Material> FinishCache = new(StringComparer.Ordinal);

    /// <summary>Transport finishes that go through the painterly response library.</summary>
    internal static Material PainterlyFinish(string color, string family)
        => PainterlyMaterialLibrary.ForColor(color, family, sheltered: true);

    /// <summary>
    /// One vehicle surface token (the Blender `<hex>__<surface>` suffix, or the
    /// procedural <see cref="Batch"/> name) resolved to its material. The switch is
    /// exhaustive over the authored vocabulary on purpose: adding a finish to the
    /// generator without adding it here fails loudly at the first build instead of
    /// shipping a default-grey panel.
    /// </summary>
    internal static Material ForFinish(string color, string surface) => surface switch
    {
        "paint" => PainterlyFinish(color, "vehicle_paint"),
        "metal" => PainterlyFinish(color, "vehicle_bare_metal"),
        "chrome" => PainterlyFinish(color, "vehicle_trim_metal"),
        "rubber" => PainterlyFinish(color, "vehicle_rubber"),
        "plastic" => PainterlyFinish(color, "vehicle_plastic"),
        "glass" => GlassFinish(color),
        // Soft and grained cabin finishes keep their own local-space grain: they
        // are the interior register of the recipe, not an exterior colour mass.
        "vinyl" => Grain(color, 260f, .06f, .45f, .78f, .3f),
        "leather" => Grain(color, 380f, .08f, .45f, .6f, .4f),
        "cloth" => Grain(color, 700f, .1f, .6f, .96f, .15f),
        "carpet" => Grain(color, 650f, .14f, .9f, .97f, .15f, fuzz: .3f),
        "headliner" => Grain(color, 900f, .04f, .5f, .95f, .2f),
        "sheepskin" => Grain(color, 55f, .22f, 1.4f, 1f, .15f, fuzz: 1f),
        "wool" => Grain(color, 160f, .12f, .8f, .98f, .15f, fuzz: .6f),
        // Interior trim with a hard varnish or an enamel face: the same physical
        // promise as the village's painted stove, kept as its own material.
        "wood_polished" => new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml(color), Roughness = .35f, Metallic = 0f,
            ClearcoatEnabled = true, Clearcoat = .6f, ClearcoatRoughness = .18f
        },
        "enamel" => new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml(color), Roughness = .2f, Metallic = 0f,
            ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = .05f
        },
        "gold" => new StandardMaterial3D { AlbedoColor = Color.FromHtml(color), Metallic = 1f, Roughness = .3f },
        // Lamp lenses and the radio LCD glow faintly so they read as glass/light
        // under the lens, not as paint.
        "lamp" => new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml(color), Roughness = .2f, Metallic = 0f,
            EmissionEnabled = true, Emission = Color.FromHtml(color), EmissionEnergyMultiplier = .15f
        },
        "lcd" => new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml(color), Roughness = .3f, Metallic = 0f,
            EmissionEnabled = true, Emission = new Color(.18f, .26f, .12f), EmissionEnergyMultiplier = .6f
        },
        // Settled snow modelled into the body (sill shelves, the gutter line). It
        // is the vehicle's own snow and stays anchored to the car, but the cabin
        // node tints it with the current atmosphere state (VehicleNivaCabin), so
        // it is never the odd one out in a coloured night.
        "snow" => SnowFinish(color),
        _ => throw new InvalidDataException(
            $"Vehicle finish '{surface}' has no material contract (colour {color}). "
            + "Map it in VehicleVisualFactory.ForFinish; transport never falls back to a default grey.")
    };

    /// <summary>
    /// Car glass: dark enough to read as a pane against snow, a tight sky
    /// highlight at a grazing angle, no metal. The previous copy used a mid-grey
    /// at roughness .18 and specular .55, which reads as a sticker on a painterly
    /// body rather than as a few millimetres of glass in a rubber frame.
    ///
    /// VIS-105 answer: the pane takes the highlight shape from the knobs this
    /// Godot build actually exposes on StandardMaterial3D. Roughness .08 keeps the
    /// sky reflection tight without dissolving into a pinpoint firefly, and the
    /// specular strength is the village window family's own answer
    /// (PainterlyMaterialLibrary "glass" row: specular .52), so the windscreen and
    /// the DК glazing respond to the same lamp with the same brightness. There is
    /// no specular-anti-aliasing and no shadow-to-opacity member on this binding,
    /// so nothing here pretends otherwise: the pane's readability through its own
    /// shadow band is carried by the alpha the driver actually sees through.
    /// </summary>
    internal static Material GlassFinish(string color)
    {
        var key = "glass:" + color;
        if (FinishCache.TryGetValue(key, out var cached)) return cached;
        var tint = Color.FromHtml(color);
        // Keep the authored hue, take the glass's own density: window glass is
        // never the flat pale grey an imported preview tends to show.
        var glass = new Color(tint.R * .72f, tint.G * .80f, tint.B * .78f, .34f);
        var material = new StandardMaterial3D
        {
            AlbedoColor = glass,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = .08f,
            Metallic = 0f,
            MetallicSpecular = .52f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        material.SetMeta("surface", "vehicle_glass");
        material.SetMeta("vehicleFinish", "glass");
        FinishCache[key] = material;
        return material;
    }

    /// <summary>
    /// Settled snow on a vehicle body. Deliberately not the village's world-space
    /// snow family: the projection would slide across the car as it drives
    /// (material anchor contract, mode E), so this is the piece's own matte
    /// finish, tinted per atmosphere state by the cabin presentation node.
    /// </summary>
    internal static StandardMaterial3D SnowFinish(string color)
    {
        var key = "snow:" + color;
        if (FinishCache.TryGetValue(key, out var cached)) return (StandardMaterial3D)cached;
        var material = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml(color),
            Roughness = .90f,
            Metallic = 0f,
            MetallicSpecular = .18f
        };
        material.SetMeta("surface", "vehicle_snow");
        material.SetMeta("vehicleFinish", "snow");
        // The atmosphere tint multiplies this authored colour, so the base has to
        // survive every rebind: a vehicle built after a coloured night must not
        // take the tinted value as its own source.
        material.SetMeta("vehicleSnowBaseColor", color);
        FinishCache[key] = material;
        return material;
    }

    /// <summary>
    /// VIS-105 step 1 for the one licensed third-party vehicle in Act I: the
    /// VAZ-2106 prop arrives with its source PBR set (photo textures, metallic
    /// steel, a flat grey-green snow patch). Geometry, UV, scale and the CC BY
    /// attribution stay exactly as ingested; only the material slots are rebinded
    /// onto the URMAN families, so the parked Zhiguli belongs to the same frame
    /// as the Niva and the village instead of sitting next to them as an asset
    /// pack. The source photo maps are not carried into the new families: the
    /// recipe normalises the response, and a photographic albedo under a painterly
    /// fence is exactly the salad the reset removes. Mesh resources are shared by
    /// every instance of the imported scene, so the rebind is an instance-level
    /// surface override and never edits the licensed source asset.
    /// </summary>
    internal static IReadOnlyList<string> RebindThirdPartyVehicleMaterials(Node3D model, string paintColor,
        List<string> unmapped)
    {
        var rebound = 0;
        foreach (var node in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            // The slot names live on the shared Mesh resource; the rebind is written
            // as an instance surface override, so the licensed source asset is never
            // edited and the other instances keep their own original look.
            if (node.Mesh is not { } mesh) continue;
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                var name = mesh.SurfaceGetMaterial(surface)?.ResourceName ?? string.Empty;
                var replacement = ThirdPartyFinish(name, paintColor);
                if (replacement is null)
                {
                    if (name.Length > 0 && !unmapped.Contains(name, StringComparer.OrdinalIgnoreCase))
                        unmapped.Add(name);
                    continue;
                }
                node.SetSurfaceOverrideMaterial(surface, replacement);
                rebound++;
            }
        }
        model.SetMeta("materialNormalization", FormattableString.Invariant(
            $"${rebound} slots rebounded onto URMAN vehicle families; source maps dropped (VIS-105)"));
        return unmapped;
    }

    private static Material? ThirdPartyFinish(string slotName, string paintColor)
    {
        var key = slotName.ToLowerInvariant().Replace(" ", "_");
        return key switch
        {
            // Painted bodywork: the same lacquer-over-steel response as the Niva.
            "body" or "paint" or "carpaint" or "body_paint" => PainterlyFinish(paintColor, "vehicle_paint"),
            // Bright trim and the mirror stalk.
            "chrome" => PainterlyFinish("c9cdc6", "vehicle_trim_metal"),
            // Steel that is actually bare or stamped: bumpers, wheels, brackets.
            "steel" or "metal" or "wheel_steel" or "wheel" => PainterlyFinish("8f948c", "vehicle_bare_metal"),
            "rubber" or "tire" or "tyre" => PainterlyFinish("1f2320", "vehicle_rubber"),
            "glass" or "windshield" or "window" => GlassFinish("83988c"),
            "plastic" or "headlights" or "lights" or "lamp"
                => PainterlyFinish("262826", "vehicle_plastic"),
            "leather" or "seats" or "seat" => Grain("302f2b", 260f, .06f, .45f, .78f, .3f),
            "wood" => PainterlyMaterialLibrary.ForColor("6b3f22", "wood_prop", sheltered: true),
            "settled_winter_snow" or "settled_snow" or "settled_winter_snow_1" => SnowFinish("eef2f6"),
            _ => null
        };
    }
}
