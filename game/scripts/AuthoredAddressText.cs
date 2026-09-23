using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Urman.Godot;

/// <summary>
/// Address text for scenes without the connected Act I world (the one-zone
/// loader, the full-game entrypoint). The world's SettlementRegistry stays the
/// owner whenever it exists; this reads the same authored manifest so a line
/// such as "Адрес бабая — {address:ADR-BABAI}" never reaches the player raw.
/// </summary>
public static class AuthoredAddressText
{
    private const string ManifestPath = "res://content/urman.settlement.addresses.v1.json";
    private static Dictionary<string, string>? _formatted;

    public static string Resolve(string text) =>
        text.Contains("{address:", System.StringComparison.Ordinal)
            ? Regex.Replace(text, @"\{address:([^}]+)\}", match => Format(match.Groups[1].Value))
            : text;

    private static string Format(string addressId)
    {
        var formatted = _formatted ??= Load();
        return formatted.TryGetValue(addressId, out var address) ? address : "адрес пока не установлен";
    }

    private static Dictionary<string, string> Load()
    {
        using var manifest = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(ManifestPath));
        var root = manifest.RootElement;
        var streets = new Dictionary<string, (string Tatar, string Russian)>();
        foreach (var row in root.GetProperty("streets").EnumerateArray())
            streets[row.GetProperty("id").GetString()!] = (row.GetProperty("tatar").GetString()!, row.GetProperty("russian").GetString()!);

        // Same shape as SettlementRegistry.FormatAddress: "Tatar / Russian, number".
        var formatted = new Dictionary<string, string>();
        foreach (var row in root.GetProperty("buildings").EnumerateArray())
        {
            var street = streets[row.GetProperty("streetId").GetString()!];
            formatted[row.GetProperty("addressId").GetString()!] =
                $"{street.Tatar} / {street.Russian}, {row.GetProperty("number").GetString()}";
        }
        if (root.TryGetProperty("addressAliases", out var aliases))
            foreach (var alias in aliases.EnumerateObject())
                if (formatted.TryGetValue(alias.Value.GetString()!, out var canonical))
                    formatted[alias.Name] = canonical;
        return formatted;
    }
}
