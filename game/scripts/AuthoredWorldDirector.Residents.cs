using System.Text.Json;
using Godot;

namespace Urman.Godot;

public partial class AuthoredWorldDirector
{
    // Presentation data belongs to the placed resident. The shared character
    // kit, named NPCs, greeting IDs and routine ownership remain unchanged.
    private static void ConfigureResidentPresentation(Node3D character, JsonElement parameters)
    {
        var height = parameters.TryGetProperty("scale", out var scale) ? scale.GetSingle() : 1f;
        character.Scale = Vector3.One * height;
        if (!parameters.TryGetProperty("residentAppearance", out var appearance)) return;

        var width = appearance.TryGetProperty("width", out var widthValue) ? Mathf.Clamp(widthValue.GetSingle(), .94f, 1.06f) : 1f;
        var depth = appearance.TryGetProperty("depth", out var depthValue) ? Mathf.Clamp(depthValue.GetSingle(), .95f, 1.05f) : 1f;
        character.Scale = new Vector3(height * width, height, height * depth);
        var prefix = character.GetMeta("characterPrefix").AsString();
        foreach (var mesh in character.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = mesh.Name.ToString();
            if (!mesh.Visible || !name.StartsWith(prefix + "_", StringComparison.Ordinal) || mesh.Mesh is null) continue;
            var colorKey = name.Contains("_Coat_", StringComparison.Ordinal) || name.Contains("_CoatSkirt_", StringComparison.Ordinal)
                ? "coatColor" : name.Contains("_Hat_", StringComparison.Ordinal) || name.Contains("_Scarf_", StringComparison.Ordinal)
                ? "headwearColor" : name.Contains("_Skirt_", StringComparison.Ordinal) || name.Contains("_Sash_", StringComparison.Ordinal)
                ? "accentColor" : "";
            if (colorKey.Length == 0 || !appearance.TryGetProperty(colorKey, out var color)) continue;
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                mesh.SetSurfaceOverrideMaterial(surface, PainterlyMaterialLibrary.ForColor(color.GetString()!, "cloth", sheltered: true));
        }

        character.SetMeta("residentAppearance", Text(appearance, "profile", "authored winter resident"));
        character.SetMeta("residentPresentationScale", character.Scale);
        character.SetMeta("residentPresentationPolicy", "existing winter kit; modest silhouette and garment palette; unchanged face and bones");
    }
}
