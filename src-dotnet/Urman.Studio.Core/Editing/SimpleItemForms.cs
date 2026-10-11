using System.Globalization;
using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One placed thing: where its form comes from and whether that link holds.</summary>
public sealed record PlacedForm(
    string EntityId,
    string PlacementKind,
    string FormReference,
    string FormSource,
    string State,
    IReadOnlyList<string> Problems);

/// <summary>One catalogued form: a node of a model kit with a declared size.</summary>
public sealed record CataloguedForm(
    string CatalogId,
    string Name,
    string Kit,
    string Scene,
    string Node,
    string Collision,
    string License,
    IReadOnlyList<string> Problems);

/// <summary>
/// The first slice of the simple-item-form half of the spec AI-27 slice. A simple thing
/// in this project is not a free-form mesh: its FORM is a catalogued node of a model kit
/// (with a declared size, collision and licence) and its PLACEMENT is a prop or a
/// kit-placement that puts that form into the world. So this projection checks exactly
/// that chain on the real sources: the catalogue entry is complete, the placement names
/// a form that exists, the asset path is really there, and the transform is readable.
///
/// Changing the form ITSELF is a different matter: it means changing the kit model or the
/// generator that produces it, which belongs to the model track (AI-09/AI-11/AI-36). This
/// slice guarantees the link and the placement, and says so instead of implying that a
/// Studio field could reshape a mesh.
/// </summary>
public static class SimpleItemForms
{
    public const string CatalogPath = "game/content/world/catalog.v1.json";

    /// <summary>Every catalogued form, checked for the fields a placement relies on.</summary>
    public static IReadOnlyList<CataloguedForm> Catalog(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var file = workspace.Files.FirstOrDefault(candidate => candidate.RelativePath == CatalogPath);
        var entities = file?.Snapshot() switch
        {
            JsonArray array => array,
            JsonObject document when document["entities"] is JsonArray declared => declared,
            _ => null
        };
        var forms = new List<CataloguedForm>();
        if (entities is null)
        {
            return forms;
        }

        foreach (var entity in entities.OfType<JsonObject>())
        {
            var problems = new List<string>();
            var id = (string?)entity["id"] ?? "";
            var source = entity["source"] as JsonObject;
            var scene = (string?)source?["scene"] ?? "";
            // A kit names the node either directly (source.node) or by prefix
            // (source.prefix, the modular kits), so both shapes select the form.
            var node = (string?)source?["node"] ?? (string?)source?["prefix"] ?? "";
            if (scene.Length == 0 || node.Length == 0)
            {
                problems.Add("В записи каталога нет source.scene и source.node/source.prefix: размещению нечего взять из набора моделей.");
            }

            if (entity["size"] is not JsonArray { Count: 3 } size || ReadVector(size) is not { } dimensions)
            {
                problems.Add("Размер формы не из трёх читаемых чисел: редактор покажет неверные габариты и не сможет проверить посадку.");
            }
            else if (dimensions.Any(number => number <= 0))
            {
                problems.Add("Размер формы содержит ноль или минус: габариты вырождены.");
            }

            if (((string?)entity["collision"] ?? "").Length == 0)
            {
                problems.Add("У формы не указан тип столкновения: размещение не сможет включить физику.");
            }

            if (((string?)entity["license"] ?? "").Length == 0)
            {
                problems.Add("У формы не указана лицензия: публиковать ресурс без прав нельзя.");
            }

            forms.Add(new CataloguedForm(
                id,
                (string?)entity["name"] ?? "",
                (string?)entity["kit"] ?? "",
                scene,
                node,
                (string?)entity["collision"] ?? "",
                (string?)entity["license"] ?? "",
                problems));
        }

        return forms.OrderBy(form => form.CatalogId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Every placement, with the form it resolves to and its authoring state.</summary>
    public static IReadOnlyList<PlacedForm> Placements(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var catalog = Catalog(workspace).Select(form => form.CatalogId).ToHashSet(StringComparer.Ordinal);
        var placements = new List<PlacedForm>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity)
            {
                continue;
            }

            var kind = (string?)entity["kind"] ?? "";
            if (kind is not ("prop" or "kit-placement"))
            {
                continue;
            }

            var parameters = entity["params"] as JsonObject;
            var problems = new List<string>();
            var reference = "";
            var source = "";
            if (kind == "prop")
            {
                reference = (string?)parameters?["catalogId"] ?? "";
                if (reference.Length == 0)
                {
                    problems.Add("У размещения нет catalogId: форма не выбрана, объект не появится.");
                }
                else if (catalog.Count > 0 && !catalog.Contains(reference))
                {
                    problems.Add($"Форма «{reference}» отсутствует в каталоге: размещение останется без модели.");
                }

                source = CatalogPath;
            }
            else
            {
                reference = (string?)parameters?["component"] ?? "";
                var anchor = (string?)parameters?["logicalAnchor"] ?? "";
                var asset = (string?)parameters?["assetSource"] ?? "";
                source = asset;
                if (reference.Length == 0)
                {
                    problems.Add("У размещения набора нет params.component: неясно, какая часть набора ставится.");
                }

                if (anchor.Length == 0)
                {
                    problems.Add("У размещения набора нет params.logicalAnchor: автор не увидит, к какому месту деревни оно привязано.");
                }

                if (asset.Length == 0)
                {
                    problems.Add("У размещения набора нет params.assetSource: источник модели не указан.");
                }
                else if (asset.StartsWith("res://", StringComparison.Ordinal))
                {
                    var full = Path.Combine(Path.GetFullPath(root), "game", asset["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(full))
                    {
                        problems.Add($"Файл модели «{asset}» не найден: размещение не загрузится.");
                    }
                }
                else if (asset.Contains("://", StringComparison.Ordinal))
                {
                    problems.Add($"Схема источника «{asset}» не поддерживается: читается только res://.");
                }
            }

            CheckVector(parameters, "position", "позиция", problems);
            CheckNumber(parameters, "yawDegrees", "поворот", problems, mustBePositive: false);

            // The two families store the scale differently, and the readers agree:
            // AuthoredWorldDirector reads a scalar for a prop (default 1) while
            // KitPlacementTakeover reads a three-number vector for a kit placement.
            if (kind == "prop")
            {
                CheckNumber(parameters, "scale", "масштаб", problems, mustBePositive: true);
            }
            else
            {
                CheckVector(parameters, "scale", "масштаб (вектор из трёх чисел)", problems, required: true);
                if (parameters?["scale"] is JsonArray { Count: 3 } vector && ReadVector(vector) is { } factors && factors.Any(factor => factor <= 0))
                {
                    problems.Add("Масштаб набора содержит ноль или минус: часть набора выродится.");
                }
            }
            if (parameters?["originalPosition"] is not null)
            {
                CheckVector(parameters, "originalPosition", "исходная позиция", problems);
            }

            var states = new List<string>();
            if (IsTrue(parameters?["retired"]))
            {
                states.Add("выведено из игры");
            }

            if (IsTrue(parameters?["hidden"]))
            {
                states.Add("скрыто");
            }

            if (parameters?["originalPosition"] is not null)
            {
                states.Add("перенесено");
            }

            placements.Add(new PlacedForm(id, kind, reference, source, string.Join(", ", states), problems));
        }

        return placements.OrderBy(placement => placement.EntityId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var forms = Catalog(workspace);
        var placements = Placements(workspace, root);
        var scenes = forms.Select(form => form.Scene).Where(scene => scene.Length > 0).Distinct(StringComparer.Ordinal).Count();
        var retired = placements.Count(placement => placement.State.Contains("выведено из игры", StringComparison.Ordinal));
        var hidden = placements.Count(placement => placement.State.Contains("скрыто", StringComparison.Ordinal));
        return
        [
            $"Форм в каталоге: {forms.Count} (наборов: {forms.Select(form => form.Kit).Where(kit => kit.Length > 0).Distinct(StringComparer.Ordinal).Count()}, сцен моделей: {scenes}); размещений: {placements.Count} (prop {placements.Count(p => p.PlacementKind == "prop")}, kit-placement {placements.Count(p => p.PlacementKind == "kit-placement")}).",
            $"Авторские состояния размещений: выведено из игры {retired}, скрыто {hidden}; это состояния, а не ошибки — редактор обязан их показывать и сохранять.",
            "Форма простого предмета — это запись каталога: узел набора моделей со размером, столкновением и лицензией. Размещение лишь ставит форму в мир (позиция, поворот, масштаб), поэтому редактирование формы означает правку модели или её генератора — это дорожка моделей (AI-09/AI-11/AI-36), а не поле Studio.",
            "Проекция проверяет связь и посадку, но не доказывает внешний вид: как форма выглядит в кадре, решает просмотр в игре."
        ];
    }

    private static void CheckVector(JsonObject? parameters, string name, string label, List<string> problems, bool required = true)
    {
        if (parameters is null || !parameters.ContainsKey(name))
        {
            if (required)
            {
                problems.Add($"Поле «{name}» отсутствует: {label} не читается, а рантайм берёт его через GetProperty.");
            }

            return;
        }

        if (parameters[name] is not JsonArray { Count: 3 } vector)
        {
            problems.Add($"{label} не из трёх чисел: объект встанет не туда или не встанет вовсе.");
            return;
        }

        if (ReadVector(vector) is null)
        {
            problems.Add($"{label} содержит нечитаемое число: объект встанет не туда.");
        }
    }

    /// <summary>
    /// scale and yawDegrees are OPTIONAL: the runtime falls back to 1 and 0, so an absent
    /// field is not a defect. A present field still has to be a readable number.
    /// </summary>
    private static void CheckNumber(JsonObject? parameters, string name, string label, List<string> problems, bool mustBePositive)
    {
        if (parameters is null || !parameters.ContainsKey(name))
        {
            return;
        }

        if (TriggerScopeFacts.NumberProblem(parameters[name], out var number) is { } why)
        {
            problems.Add($"Поле «{name}»: {why} — {label} не читается, а рантайм подставит своё значение.");
            return;
        }

        if (mustBePositive && number <= 0)
        {
            problems.Add($"{label} = {number.ToString(CultureInfo.InvariantCulture)}: неположительный масштаб ломает объект.");
        }
    }

    private static bool IsTrue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    /// <summary>Three readable numbers, or null when any component cannot be read.</summary>
    private static double[]? ReadVector(JsonArray vector)
    {
        var numbers = new double[vector.Count];
        for (var index = 0; index < vector.Count; index++)
        {
            if (TriggerScopeFacts.NumberProblem(vector[index], out var number) is not null)
            {
                return null;
            }

            numbers[index] = number;
        }

        return numbers;
    }
}
