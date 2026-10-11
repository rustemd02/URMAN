using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One address the settlement registry binds to a building, a parcel and a door.</summary>
public sealed record AddressBindingFacts(
    string AddressId,
    string Street,
    string Number,
    string Role,
    string? PrimaryDoor,
    IReadOnlyList<string> Problems);

/// <summary>
/// The address half of the spec AI-45 slice. Moving a house means moving everything bound to
/// it, and the settlement registry (game/content/urman.settlement.addresses.v1.json) is where
/// those bindings live: each building carries a buildingId, a parcelId, an addressId, a
/// street and number, and the primaryDoor node of the authored scene. The registry's own
/// source line says these bindings "do not move geometry", so this projection checks the
/// BINDINGS rather than pretending they are the house:
///
///   * a declared primaryDoor has to exist as a node of the authored scene, or a relocation
///     would move the house and leave the door behind;
///   * an alias has to point at a declared address, or a quest telling the player the shop
///     address resolves to nothing;
///   * and how many world parcels the registry does NOT cover is reported as its declared
///     scope, not as a defect, because the registry binds the authored scene rather than
///     every address in the world.
/// </summary>
public static class AddressBindings
{
    public const string RegistryPath = "game/content/urman.settlement.addresses.v1.json";
    public const string ScenePath = "game/scripts/Act1ConnectedWorld.cs";

    /// <summary>The registry's own statement of what these bindings are.</summary>
    public static string Scope(string root)
    {
        var registry = Registry(root);
        return AuthoredJson.Text(registry, "source");
    }

    public static IReadOnlyList<AddressBindingFacts> Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var registry = Registry(root);
        if (registry?["buildings"] is not JsonArray buildings)
        {
            return [];
        }

        var scene = SceneText(full);
        var declared = buildings.OfType<JsonObject>()
            .Select(building => AuthoredJson.TextOrNull(building, "addressId"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var aliases = (registry["addressAliases"] as JsonObject ?? [])
            .ToDictionary(pair => pair.Key, pair => AuthoredJson.TextValue(pair.Value), StringComparer.Ordinal);

        var bindings = new List<AddressBindingFacts>();
        foreach (var building in buildings.OfType<JsonObject>())
        {
            var addressId = AuthoredJson.TextOrNull(building, "addressId") ?? "";
            var door = AuthoredJson.TextOrNull(building, "primaryDoor");
            var problems = new List<string>();

            if (door is { Length: > 0 } && scene.Length > 0 && !scene.Contains(door, StringComparison.Ordinal))
            {
                problems.Add($"Дверь «{door}» не найдена в авторской сцене ({ScenePath}): перенос дома оставит дверь на месте.");
            }

            var aliased = aliases.Where(pair => pair.Value == addressId).Select(pair => pair.Key).ToArray();
            foreach (var alias in aliased)
            {
                if (!declared.Contains(addressId))
                {
                    problems.Add($"Псевдоним «{alias}» ведёт на «{addressId}», которого нет среди адресов реестра.");
                }
            }

            bindings.Add(new AddressBindingFacts(
                addressId,
                AuthoredJson.Text(building, "streetId"),
                AuthoredJson.Text(building, "number"),
                AuthoredJson.Text(building, "role"),
                door,
                problems));
        }

        foreach (var pair in aliases.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var alias = pair.Key;
            var target = pair.Value;
            if (target.Length == 0 || !declared.Contains(target))
            {
                bindings.Add(new AddressBindingFacts(alias, "", "", "alias", null,
                    [$"Псевдоним «{alias}» ведёт на «{target}», которого нет среди адресов реестра: квест с этим адресом не разрешится."]));
            }
        }

        return bindings.OrderBy(binding => binding.AddressId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>World parcels the registry does not bind, and the registry's own scope line.</summary>
    public static (int Bound, int Unbound, string Scope) Coverage(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var registry = Registry(root);
        var bound = (registry?["buildings"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(building => (AuthoredJson.TextOrNull(building, "streetId"), AuthoredJson.TextOrNull(building, "number")))
            .ToHashSet();
        // A parcel id such as ADR-H033 does not have the namespace:kind/name shape the
        // workspace indexes, so parcels are read from the world files themselves instead of
        // being looked up as entities (verified: the id is not indexed).
        var world = new HashSet<(string?, string?)>();
        var worldFolder = Path.Combine(Path.GetFullPath(root), "game", "content", "world");
        if (Directory.Exists(worldFolder))
        {
            foreach (var file in Directory.EnumerateFiles(worldFolder, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                CollectParcels(JsonNode.Parse(File.ReadAllText(file)), world);
            }
        }

        return (world.Count(pair => bound.Contains(pair)), world.Count(pair => !bound.Contains(pair)), Scope(root));
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var bindings = Inspect(workspace, root);
        var (bound, unbound, scope) = Coverage(workspace, root);
        var doors = bindings.Count(binding => binding.PrimaryDoor is { Length: > 0 });
        return
        [
            $"Адресов в реестре: {bindings.Count}; с объявленной дверью: {doors}; проблем: {bindings.Sum(binding => binding.Problems.Count)}.",
            $"Мировых участков, покрытых реестром по (улица, номер): {bound}; не покрытых: {unbound}.",
            unbound > 0
                ? $"Непокрытые адреса — это НЕ дефект: реестр сам говорит «{scope}», то есть связывает авторскую сцену, а не каждый адрес мира. Он ложится на записи kind=kit этой сцены, а массовая сетка участков (kind=parcel) в его предмет не входит. Так и надо читать это число."
                : "Все мировые участки покрыты реестром.",
            "Проверяется связка «адрес → дом → участок → дверь», а не геометрия: перенос дома обязан унести и дверь, и адрес, а эта проекция показывает, где связка уже не сходится."
        ];
    }

    private static void CollectParcels(JsonNode? node, HashSet<(string?, string?)> world)
    {
        switch (node)
        {
            case JsonObject obj:
                // Both kinds carry an address: the bulk parcels of the grid and the kit
                // records the registry actually binds. Collecting one of them hid the match.
                if (AuthoredJson.Text(obj, "kind") is "parcel" or "kit" && obj["street"] is not null && obj["number"] is not null)
                {
                    world.Add((AuthoredJson.Text(obj, "street"), AuthoredJson.TextOrNull(obj, "number")));
                }

                foreach (var pair in obj)
                {
                    CollectParcels(pair.Value, world);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    CollectParcels(item, world);
                }

                break;
        }
    }

    private static JsonObject? Registry(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), RegistryPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
    }

    private static string SceneText(string full)
    {
        var folder = Path.GetDirectoryName(Path.Combine(full, ScenePath.Replace('/', Path.DirectorySeparatorChar))) ?? "";
        if (!Directory.Exists(folder))
        {
            return "";
        }

        var text = new System.Text.StringBuilder();
        foreach (var file in Directory.EnumerateFiles(folder, "Act1ConnectedWorld*.cs", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            text.AppendLine(File.ReadAllText(file));
        }

        return text.ToString();
    }
}
