using System.Text.Json;
using Godot;

namespace Urman.Godot;

public sealed record ShopProduct(string Sku, string Title, string Unit, string Description, IReadOnlyList<string> Uses)
{
    public string ItemId => "shop/" + Sku;
}
public sealed record ShopLedgerEntry(string Sku, string ItemId, string Title, string Unit, bool InPocket);

public static class ShopCatalog
{
    private static IReadOnlyList<ShopProduct>? _products;
    public static IReadOnlyList<ShopProduct> All => _products ??= Load();
    public static ShopProduct? Find(string sku) => All.FirstOrDefault(product => product.Sku == sku);
    private static IReadOnlyList<ShopProduct> Load()
    {
        using var json = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString("res://content/urman.shop.v1.json"));
        var products = json.RootElement.GetProperty("items").EnumerateArray().Select(row => new ShopProduct(
            row.GetProperty("sku").GetString()!, row.GetProperty("title").GetString()!,
            row.GetProperty("unit").GetString()!, row.GetProperty("description").GetString()!,
            row.GetProperty("uses").EnumerateArray().Select(value => value.GetString()!).ToArray())).ToArray();
        if (products.Length == 0 || products.Select(p => p.Sku).Distinct(StringComparer.Ordinal).Count() != products.Length)
            throw new InvalidDataException("Shop stock must have unique persistent product IDs.");
        return products;
    }
}
