using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored shop product and how its declared uses stand against the code.</summary>
public sealed record ShopProductFacts(
    string Sku,
    string Title,
    IReadOnlyList<string> Uses,
    IReadOnlyList<string> Problems);

/// <summary>
/// The shop half of the spec AI-25 slice. The catalogue is authored in
/// <c>game/content/urman.shop.v1.json</c> while the handlers that decide whether a
/// product may be applied live in code, so this projection reads both: the authored
/// rows against the contract ShopCatalog.Load enforces, and every declared use id
/// against the use ids the runtime really compares. A product whose use id no handler
/// matches can be bought and never applied, which is the trap worth reporting.
/// </summary>
public static class ShopCatalogue
{
    public const string ShopPath = "game/content/urman.shop.v1.json";

    /// <summary>The two files that compare a use id when a supply is applied.</summary>
    public static readonly string[] HandlerPaths =
    [
        "game/scripts/RuntimeBridge.Shop.cs",
        "game/scripts/Act1ConnectedWorld.ShopUses.cs"
    ];

    private static readonly Regex UseLiteral = new("\"([a-z][a-z0-9-]*/[a-z0-9-]+)\"", RegexOptions.CultureInvariant);

    /// <summary>
    /// The use ids the runtime compares, read out of the handler sources. Only lines
    /// that mention a use id are considered, so an unrelated path literal is not
    /// mistaken for a handler.
    /// </summary>
    public static IReadOnlySet<string> HandledUses(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var handled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relative in HandlerPaths)
        {
            var full = Path.Combine(Path.GetFullPath(root), relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                continue;
            }

            foreach (var line in File.ReadLines(full))
            {
                if (!line.Contains("useId", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in UseLiteral.Matches(line))
                {
                    handled.Add(match.Groups[1].Value);
                }
            }
        }

        return handled;
    }

    public static IReadOnlyList<ShopProductFacts> Inspect(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var handled = HandledUses(root);
        var full = Path.Combine(Path.GetFullPath(root), ShopPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full) || JsonNode.Parse(File.ReadAllText(full)) is not JsonObject file || file["items"] is not JsonArray rows)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var products = new List<ShopProductFacts>();
        if (rows.Count == 0)
        {
            products.Add(new ShopProductFacts("", "", [], ["Каталог пуст: рантайм отвергает пустой список товаров, магазин не откроется."]));
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            var problems = new List<string>();
            var sku = (string?)row["sku"] ?? "";
            if (sku.Length == 0)
            {
                problems.Add("У товара нет sku: рантайм прочитает пустой ключ, и покупка будет невозможна.");
            }
            else if (!seen.Add(sku))
            {
                problems.Add($"sku «{sku}» повторяется: рантайм отвергает каталог с дубликатом.");
            }

            foreach (var name in new[] { "title", "unit", "description" })
            {
                if (((string?)row[name] ?? "").Length == 0)
                {
                    problems.Add($"Поле «{name}» пустое: в магазине это увидит игрок.");
                }
            }

            var uses = new List<string>();
            if (row["uses"] is not JsonArray declared || declared.Count == 0)
            {
                problems.Add("Поле «uses» отсутствует или пусто: товар можно купить, но нечем применить.");
            }
            else
            {
                for (var index = 0; index < declared.Count; index++)
                {
                    if (declared[index] is not JsonValue value || !value.TryGetValue<string>(out var use) || use.Length == 0)
                    {
                        problems.Add($"Элемент uses[{index}] не строка: рантайм бросит исключение при разборе каталога.");
                        continue;
                    }

                    uses.Add(use);
                    if (!handled.Contains(use))
                    {
                        problems.Add($"Применение «{use}» не обрабатывает никто в рантайме: товар купят, но применить не смогут.");
                    }
                }
            }

            products.Add(new ShopProductFacts(sku, (string?)row["title"] ?? "", uses, problems));
        }

        return products.OrderBy(product => product.Sku, StringComparer.Ordinal).ToArray();
    }

    /// <summary>What an author should know before touching the shop parameters.</summary>
    public static IReadOnlyList<string> Notes(string root)
    {
        var products = Inspect(root);
        var handled = HandledUses(root);
        return
        [
            $"Товаров в каталоге: {products.Count}; применений, которые знает код: {handled.Count} ({string.Join(", ", handled.Order(StringComparer.Ordinal))}).",
            "Каталог авторский (game/content/urman.shop.v1.json: sku, title, unit, description, uses), а вот обработчики применения живут в коде: покупка и леджер — состояние игрока, поэтому новый use id без обработчика не заработает сам по себе.",
            "Рантайм отвергает пустой каталог и повторяющийся sku; остальные поля он читает через GetString и GetProperty, то есть отсутствие или неверный тип — исключение."
        ];
    }
}
