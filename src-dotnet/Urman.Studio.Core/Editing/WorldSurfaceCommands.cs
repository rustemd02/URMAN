using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored scatter command, read the way the runtime reads it.</summary>
public sealed record ScatterSpec(
    string EntityId,
    double X,
    double Z,
    double Radius,
    double Density,
    ulong Seed,
    bool SeedDeclared,
    IReadOnlyList<string> CatalogIds,
    bool CatalogIdsDeclared,
    bool CatalogIdsWasArray,
    double ScaleMin,
    double ScaleMax,
    double RoadClearance,
    bool RoadClearanceDeclared,
    IReadOnlyList<(double X, double Z, double Radius)> Excludes,
    bool ExcludesDeclared);

/// <summary>One authored terrain stroke.</summary>
public sealed record TerrainStrokeSpec(
    string EntityId,
    string Mode,
    double X,
    double Z,
    double Radius,
    double Strength,
    double Target,
    bool PositionDeclared);

/// <summary>
/// The scatter brush as the runtime builds it (spec AI-17, WORLD11). The runtime
/// computes <c>round(pi * r^2 * density)</c> plants from one seed and skips the
/// points that fall on a passage or inside an exclusion circle. This projection
/// applies the same rules before Apply, so an author cannot bake a scatter that
/// would stall the game or stop being reproducible.
/// </summary>
public static class ScatterCommand
{
    public const string RuntimePath = "game/scripts/AuthoredWorldDirector.cs";

    /// <summary>Plants the runtime builds for one scatter; above this the editor refuses before Apply.</summary>
    public const int PointBudget = 20000;

    public const double MinRadius = 0.5;
    public const double MaxRadius = 60;
    public const double MinDensity = 0.0001;
    public const double MaxDensity = 1;

    private static readonly string[] Fields =
        ["radius", "density", "seed", "catalogIds", "scaleMin", "scaleMax", "roadClearance", "exclude", "position"];

    public static ScatterSpec Parse(string entityId, JsonObject? entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var parameters = entity?["params"] as JsonObject ?? entity ?? [];
        var position = parameters["position"] as JsonArray;
        var excludes = new List<(double X, double Z, double Radius)>();
        if (parameters["exclude"] is JsonArray circles)
        {
            foreach (var circle in circles.OfType<JsonArray>())
            {
                if (circle.Count >= 3)
                {
                    excludes.Add((Number(circle[0]), Number(circle[1]), Number(circle[2])));
                }
                else
                {
                    excludes.Add((double.NaN, double.NaN, double.NaN));
                }
            }
        }

        return new ScatterSpec(
            entityId,
            position is { Count: >= 3 } ? Number(position[0]) : 0d,
            position is { Count: >= 3 } ? Number(position[2]) : 0d,
            Number(parameters["radius"]),
            Number(parameters["density"]),
            (ulong)Math.Max(0d, Number(parameters["seed"])),
            parameters.ContainsKey("seed"),
            (parameters["catalogIds"] as JsonArray ?? []).Select(Text).Where(id => id.Length > 0).ToArray(),
            parameters.ContainsKey("catalogIds"),
            parameters["catalogIds"] is JsonArray,
            parameters.ContainsKey("scaleMin") ? Number(parameters["scaleMin"]) : 0.8d,
            parameters.ContainsKey("scaleMax") ? Number(parameters["scaleMax"]) : 1.2d,
            parameters.ContainsKey("roadClearance") ? Number(parameters["roadClearance"]) : 0.6d,
            parameters.ContainsKey("roadClearance"),
            excludes,
            parameters.ContainsKey("exclude"));
    }

    /// <summary>
    /// The models of a scatter that the catalogue does not know. The runtime calls
    /// BuildVisual for each chosen model and writes an error for an unknown catalogue
    /// entry, so a missing model silently thins the scatter it was supposed to fill.
    /// </summary>
    public static IReadOnlyList<string> MissingModels(ScatterSpec spec, IReadOnlyCollection<string> catalogueIds)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(catalogueIds);
        return catalogueIds.Count == 0
            ? []
            : spec.CatalogIds.Where(model => !catalogueIds.Contains(model)).ToArray();
    }

    /// <summary>The exact plant count the runtime will try to build.</summary>
    public static int ExpectedPointCount(ScatterSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var raw = Math.PI * spec.Radius * spec.Radius * spec.Density;
        return double.IsFinite(raw) && raw > 0 ? (int)Math.Round(raw) : 0;
    }

    /// <summary>Everything that would make the runtime fail, stall or lose reproducibility.</summary>
    public static IReadOnlyList<string> Validate(ScatterSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var problems = new List<string>();
        // The four cases are told apart: an absent field, a wrongly typed field, an
        // empty list and a list with no usable string are different mistakes, and the
        // runtime fails the same way in all of them.
        if (!spec.CatalogIdsDeclared)
        {
            problems.Add("Нет поля catalogIds: рантайм возьмёт models[RandiRange(0, -1)] и упадёт.");
        }
        else if (!spec.CatalogIdsWasArray)
        {
            problems.Add("Поле catalogIds есть, но это не массив: породы не прочитаются, и рантайм упадёт на пустом списке.");
        }
        else if (spec.CatalogIds.Count == 0)
        {
            problems.Add("catalogIds пуст: рантайм возьмёт models[RandiRange(0, -1)] и упадёт.");
        }

        if (!spec.SeedDeclared)
        {
            problems.Add("Нет seed: рисунок посадок не воспроизводится, и Redo даст другой результат.");
        }

        if (spec.Radius < MinRadius || spec.Radius > MaxRadius)
        {
            problems.Add($"Радиус {spec.Radius.ToString(CultureInfo.InvariantCulture)} м вне допустимого {MinRadius}..{MaxRadius}.");
        }

        if (spec.Density < MinDensity || spec.Density > MaxDensity)
        {
            problems.Add($"Густота {spec.Density.ToString(CultureInfo.InvariantCulture)} вне допустимого {MinDensity}..{MaxDensity}.");
        }

        if (spec.ScaleMin > spec.ScaleMax)
        {
            problems.Add($"Размер от {spec.ScaleMin} больше размера до {spec.ScaleMax}: RandfRange получит обратный диапазон.");
        }

        if (spec.RoadClearance < 0)
        {
            problems.Add("Отступ от дорог отрицательный: проходы перестанут быть защищены.");
        }

        if (spec.Excludes.Any(circle => double.IsNaN(circle.X) || double.IsNaN(circle.Z) || double.IsNaN(circle.Radius)))
        {
            problems.Add("В исключениях есть запись не из трёх чисел: рантайм читает [0], [1], [2] и упадёт.");
        }

        var count = ExpectedPointCount(spec);
        if (count > PointBudget)
        {
            problems.Add($"Это {count} растений при бюджете {PointBudget}: слишком много точек и памяти, Apply блокируется до записи.");
        }

        return problems;
    }

    /// <summary>Why the brush must not be applied, or null when it may be.</summary>
    public static string? BlockReason(ScatterSpec spec)
    {
        var problems = Validate(spec);
        return problems.Count == 0 ? null : string.Join(" ", problems);
    }

    /// <summary>Honest notes that are not errors: what protects the passages and what does not.</summary>
    public static IReadOnlyList<string> Notes(ScatterSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var notes = new List<string>
        {
            $"Рантайм построит примерно {ExpectedPointCount(spec)} растений из seed {spec.Seed} и пропустит точки на дорогах и в исключениях."
        };
        notes.Add(spec.ExcludesDeclared && spec.Excludes.Count > 0
            ? $"Исключений задано: {spec.Excludes.Count}."
            : "Исключений нет: проходы держит только отступ от дорог, дома и защищённые точки не исключены.");
        if (!spec.RoadClearanceDeclared)
        {
            notes.Add("Отступ от дорог не задан: рантайм возьмёт 0.6 м.");
        }

        return notes;
    }

    /// <summary>
    /// Checks that every field this projection relies on is really read in the
    /// runtime scatter builder, so the numbers cannot drift away from the game.
    /// </summary>
    public static IReadOnlyList<string> VerifyAgainstRuntime(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var problems = new List<string>();
        var path = Path.Combine(Path.GetFullPath(root), RuntimePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            problems.Add($"Нет файла {RuntimePath}: связь с потребителем не проверена.");
            return problems;
        }

        var text = File.ReadAllText(path);
        foreach (var field in Fields)
        {
            // radius/density/... are read inside BuildScatter; position is read by the
            // generic placement path, so both spellings are accepted here.
            var readsField = Regex.IsMatch(text, "(?:Params|parameters)[^\\n]{0,40}\"" + Regex.Escape(field) + "\"")
                || Regex.IsMatch(text, "p\\.[A-Za-z]+\\(\"" + Regex.Escape(field) + "\"");
            if (!readsField)
            {
                problems.Add($"Поле «{field}» больше не читается в {RuntimePath}: проекция устарела.");
            }
        }

        return problems;
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    /// <summary>Reads any JSON number: values created by the editor are often int or float, not double.</summary>
    private static double Number(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return 0d;
        }

        if (value.TryGetValue<double>(out var number))
        {
            return number;
        }

        if (value.TryGetValue<float>(out var single))
        {
            return single;
        }

        if (value.TryGetValue<int>(out var small))
        {
            return small;
        }

        if (value.TryGetValue<long>(out var integer))
        {
            return integer;
        }

        if (value.TryGetValue<decimal>(out var precise))
        {
            return (double)precise;
        }

        return 0d;
    }
}

/// <summary>
/// The terrain stroke as the runtime applies it (spec AI-17, WORLD10): one stroke
/// is one entity and one undo step. The mode decides what the stroke does, so an
/// unknown mode is refused here instead of being written as a stroke that would
/// do nothing.
/// </summary>
public static class TerrainStrokeCommand
{
    public const string RuntimePath = "game/scripts/experiments/agent_b_act1/AgentBAct1HeightField.cs";

    public static IReadOnlyList<string> Modes { get; } = ["raise", "lower", "smooth", "flatten"];

    public const double MaxStrength = 5d;

    public static TerrainStrokeSpec Parse(string entityId, JsonObject? entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var parameters = entity?["params"] as JsonObject ?? entity ?? [];
        var position = parameters["position"] as JsonArray;
        return new TerrainStrokeSpec(
            entityId,
            Text(parameters["mode"]),
            position is { Count: >= 3 } ? Number(position[0]) : 0d,
            position is { Count: >= 3 } ? Number(position[2]) : 0d,
            Number(parameters["radius"]),
            Number(parameters["strength"]),
            Number(parameters["target"]),
            parameters.ContainsKey("position"));
    }

    public static IReadOnlyList<string> Validate(TerrainStrokeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var problems = new List<string>();
        if (!Modes.Contains(spec.Mode, StringComparer.Ordinal))
        {
            problems.Add($"Режим «{spec.Mode}» игра не знает: штрих не подействует. Допустимо: {string.Join(", ", Modes)}.");
        }

        if (!spec.PositionDeclared)
        {
            problems.Add("Нет position: штрих некуда приложить.");
        }

        if (spec.Radius <= 0)
        {
            problems.Add("Радиус штриха не положительный.");
        }

        if (spec.Strength <= 0 && spec.Mode is not ("smooth" or "flatten"))
        {
            problems.Add("Сила штриха не положительная: рельеф не изменится.");
        }

        return problems;
    }

    /// <summary>Notes that are not errors, including a strength outside the toolbar range.</summary>
    public static IReadOnlyList<string> Notes(TerrainStrokeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var notes = new List<string>
        {
            $"Один штрих — одна сущность и один шаг отмены; режим {spec.Mode}, радиус {spec.Radius:0.#} м, сила {spec.Strength:0.###}."
        };
        if (spec.Strength > MaxStrength)
        {
            notes.Add($"Сила {spec.Strength:0.###} больше обычного предела {MaxStrength}: проверьте, что такой уступ действительно нужен.");
        }

        return notes;
    }

    public static IReadOnlyList<string> VerifyAgainstRuntime(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var problems = new List<string>();
        var path = Path.Combine(Path.GetFullPath(root), RuntimePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            problems.Add($"Нет файла {RuntimePath}: связь с потребителем не проверена.");
            return problems;
        }

        var text = File.ReadAllText(path);
        foreach (var field in new[] { "mode", "position", "radius", "strength", "target" })
        {
            if (!Regex.IsMatch(text, "p\\.GetProperty\\(\"" + Regex.Escape(field) + "\"\\)") && !text.Contains($"\"{field}\"", StringComparison.Ordinal))
            {
                problems.Add($"Поле «{field}» больше не читается в {RuntimePath}: проекция устарела.");
            }
        }

        return problems;
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    /// <summary>Reads any JSON number: values created by the editor are often int or float, not double.</summary>
    private static double Number(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return 0d;
        }

        if (value.TryGetValue<double>(out var number))
        {
            return number;
        }

        if (value.TryGetValue<float>(out var single))
        {
            return single;
        }

        if (value.TryGetValue<int>(out var small))
        {
            return small;
        }

        if (value.TryGetValue<long>(out var integer))
        {
            return integer;
        }

        if (value.TryGetValue<decimal>(out var precise))
        {
            return (double)precise;
        }

        return 0d;
    }
}
