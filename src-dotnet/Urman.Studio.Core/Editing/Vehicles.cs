using System.Globalization;
using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored vehicle, with what the runtime would do with its fields.</summary>
public sealed record VehicleFacts(
    string Id,
    string Kind,
    string DisplayName,
    bool HasMechanics,
    IReadOnlyList<string> Problems);

/// <summary>
/// The transport half of the spec AI-25 slice: the authored vehicles of the campaign
/// checked against the contract VehicleDefinition.Load really enforces. The reader
/// throws on a blank or duplicate id, an unknown kind, a vector that is not exactly
/// three finite numbers, a non-finite number, and any non-positive dimension or
/// dynamic value, so those are the cases reported here - with the same wording an
/// author needs, and without inventing a second format.
/// </summary>
public static class Vehicles
{
    public const string VehiclesPath = "game/content/vehicles/act1_vehicles.v1.json";

    private static readonly string[] Kinds = ["Niva", "Motorcycle", "HorseCart"];

    /// <summary>Required scalar fields (the reader throws when one is missing).</summary>
    private static readonly string[] Scalars =
    [
        "yawDegrees", "maxForwardSpeed", "reverseSpeed", "acceleration", "brakeDeceleration",
        "wheelBase", "steeringDegrees", "wheelRadius"
    ];

    /// <summary>Required three-number vectors, plus the fields that must be positive.</summary>
    private static readonly string[] Vectors = ["spawn", "hullSize", "hullCenter", "seat"];

    private static readonly string[] Positive = ["maxForwardSpeed", "reverseSpeed", "acceleration", "brakeDeceleration", "wheelBase", "wheelRadius"];

    public static IReadOnlyList<VehicleFacts> Inspect(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        // The vehicles file is not one of the Studio workspace's source globs, so it is read
        // from the project root directly, the same way the audio registry reads a manifest
        // that lives outside them.
        var full = Path.Combine(Path.GetFullPath(root), VehiclesPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full) || JsonNode.Parse(File.ReadAllText(full)) is not JsonObject file || file["vehicles"] is not JsonArray rows)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var vehicles = new List<VehicleFacts>();
        foreach (var row in rows.OfType<JsonObject>())
        {
            var problems = new List<string>();
            var id = (string?)row["id"] ?? "";
            if (id.Length == 0)
            {
                problems.Add("У машины нет id: рантайм бросит исключение и не загрузит весь файл машин.");
            }
            else if (!seen.Add(id))
            {
                problems.Add($"id «{id}» повторяется: рантайм отвергает файл с дубликатом.");
            }

            var kind = (string?)row["kind"] ?? "";
            if (!Kinds.Contains(kind, StringComparer.Ordinal))
            {
                problems.Add($"kind «{kind}» не поддерживается: игра понимает только {string.Join(", ", Kinds)} (с учётом регистра).");
            }

            if (((string?)row["displayName"] ?? "").Length == 0)
            {
                problems.Add("У машины нет displayName: имя в интерфейсе будет пустым.");
            }

            foreach (var name in Scalars)
            {
                if (!TryNumber(row, name, out var number, out var why))
                {
                    problems.Add($"Поле «{name}»: {why}");
                    continue;
                }

                if (Positive.Contains(name, StringComparer.Ordinal) && number <= 0)
                {
                    problems.Add($"Поле «{name}» = {number.ToString(CultureInfo.InvariantCulture)}: рантайм требует положительное значение.");
                }
            }

            foreach (var name in Vectors)
            {
                if (row[name] is not JsonArray vector)
                {
                    problems.Add($"Поле «{name}» отсутствует: рантайм читает его как три числа.");
                    continue;
                }

                if (vector.Count != 3)
                {
                    problems.Add($"Поле «{name}» содержит {vector.Count} чисел вместо трёх: рантайм бросает исключение.");
                    continue;
                }

                if (vector.Any(item => item is not JsonValue value || !value.TryGetValue<float>(out var component) || !float.IsFinite(component)))
                {
                    problems.Add($"Поле «{name}» содержит не число или бесконечность: рантайм бросает исключение.");
                }

                if (name == "hullSize" && vector.All(item => item is JsonValue value && value.TryGetValue<float>(out var component) && component > 0) is false)
                {
                    problems.Add("Габариты кузова должны быть положительными по всем трём осям: иначе рантайм отвергает машину.");
                }
            }

            if (row["hasRadio"] is not JsonValue radio || !radio.TryGetValue<bool>(out _))
            {
                problems.Add("Поле «hasRadio» отсутствует или не булево: рантайм читает его как bool.");
            }

            if (row["mechanics"] is { } mechanics && mechanics is not JsonObject)
            {
                problems.Add("Блок «mechanics» не объект: рантайм отвергает машину.");
            }

            vehicles.Add(new VehicleFacts(id, kind, (string?)row["displayName"] ?? "", row["mechanics"] is JsonObject, problems));
        }

        return vehicles.OrderBy(vehicle => vehicle.Id, StringComparer.Ordinal).ToArray();
    }

    /// <summary>What an author should know before touching transport parameters.</summary>
    public static IReadOnlyList<string> Notes(string root)
    {
        var vehicles = Inspect(root);
        var withMechanics = vehicles.Count(vehicle => vehicle.HasMechanics);
        return
        [
            $"Машин в кампании: {vehicles.Count}; с блоком drivetrain «mechanics»: {withMechanics}.",
            "Рантайм (VehicleDefinition.Load) читает id, kind, displayName, spawn, yawDegrees, maxForwardSpeed, reverseSpeed, acceleration, brakeDeceleration, wheelBase, steeringDegrees, hullSize, hullCenter, seat, wheelRadius, hasRadio и необязательный mechanics; пустой id, дубликат, чужой kind, вектор не из трёх чисел, не-конечное число и неположительные габариты или динамика бросают исключение до старта.",
            "mechanics — необязательный блок: без него машина едет на авторской кинематике, поэтому его отсутствие не ошибка."
        ];
    }

    private static bool TryNumber(JsonObject row, string name, out double number, out string why)
    {
        number = 0;
        if (row[name] is not JsonValue value || !value.TryGetValue<double>(out var parsed))
        {
            why = "поле отсутствует или не число — рантайм бросит исключение.";
            return false;
        }

        if (!double.IsFinite(parsed))
        {
            why = "значение не конечное число — рантайм бросит исключение.";
            return false;
        }

        if (Math.Abs(parsed) > float.MaxValue)
        {
            why = $"значение {parsed.ToString(CultureInfo.InvariantCulture)} не представимо как float — рантайм получит бесконечность и бросит исключение.";
            return false;
        }

        number = parsed;
        why = "";
        return true;
    }
}
