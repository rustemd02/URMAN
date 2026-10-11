using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One addressed building record and whether a coordinated move is possible.</summary>
public sealed record HouseRecord(
    string EntityId,
    string AddressId,
    string Street,
    string Number,
    string Cadastral,
    IReadOnlyList<string> Problems);

/// <summary>
/// The first slice of the coordinated-house-move half of the spec AI-45 slice. What a
/// move can actually address is the AUTHORED record: a building is a prop or a kit
/// placement that carries an address (id, street, number, cadastral) plus its form
/// reference and transform. Its doors, windows, interior and entrance points are NOT
/// separate authored records: they belong to the kit model and are found at runtime
/// through collision shape ownership, so they follow the parent by construction. This
/// projection therefore checks what a move must preserve and says plainly that the editor
/// moves the parent record, instead of implying it addresses doors and entrances one by one.
/// </summary>
public static class HouseRecords
{
    /// <summary>Every building record with an address, checked for the move preconditions.</summary>
    public static IReadOnlyList<HouseRecord> Inspect(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var addressIds = new HashSet<string>(StringComparer.Ordinal);
        var records = new List<HouseRecord>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || entity["params"]?["address"] is not JsonObject address)
            {
                continue;
            }

            var problems = new List<string>();
            var addressId = AuthoredJson.TextOrNull(address, "id") ?? "";
            if (addressId.Length == 0)
            {
                problems.Add("У записи нет address.id: адрес неотличим от других, и массовое перемещение не сможет её назвать.");
            }
            else if (!addressIds.Add(addressId))
            {
                problems.Add($"address.id «{addressId}» повторяется: две постройки претендуют на один адрес.");
            }

            foreach (var (name, label) in new[] { ("street", "улица"), ("number", "номер дома") })
            {
                if ((AuthoredJson.Text(address, name)).Length == 0)
                {
                    problems.Add($"У записи нет address.{name} ({label}): адрес неполный.");
                }
            }

            var known = AuthoredJson.TextOrNull(entity, "kind") is "prop" or "kit-placement";
            if (!known)
            {
                problems.Add($"Вид записи «{AuthoredJson.Text(entity, "kind") ?? "нет"}» не prop и не kit-placement: перемещение не знает, что именно двигать.");
            }

            var parameters = entity["params"] as JsonObject;
            if (parameters?["position"] is not JsonArray { Count: 3 } position || ReadVector(position) is null)
            {
                problems.Add("Позиция не из трёх читаемых чисел: постройку некуда поставить.");
            }

            if (parameters is null || !parameters.ContainsKey("yawDegrees")
                || TriggerScopeFacts.NumberProblem(parameters["yawDegrees"], out _) is not null)
            {
                problems.Add("Поле «yawDegrees» отсутствует или не читается: при перемещении потеряется разворот постройки.");
            }

            // The pad is an OBJECT (halfSize, streetAnchor, feather) and the runtime reads
            // terrainPad.halfSize[0..1] with GetProperty, so the half size is required and
            // the other two are optional.
            if (parameters?["terrainPad"] is { } pad)
            {
                if (pad is not JsonObject block)
                {
                    problems.Add("Поле «terrainPad» не объект: рантайм читает terrainPad.halfSize и упадёт.");
                }
                else
                {
                    if (block["halfSize"] is not JsonArray { Count: 2 } half || ReadVector(half) is null)
                    {
                        problems.Add("Поле «terrainPad.halfSize» не из двух читаемых чисел: площадка под домом не переедет вместе с ним.");
                    }

                    if (block["streetAnchor"] is { } anchor && (anchor is not JsonArray { Count: 2 } || ReadVector((JsonArray)anchor) is null))
                    {
                        problems.Add("Поле «terrainPad.streetAnchor» не из двух читаемых чисел: привязка площадки к улице потеряется при переносе.");
                    }

                    if (block["feather"] is { } feather && TriggerScopeFacts.NumberProblem(feather, out _) is not null)
                    {
                        problems.Add("Поле «terrainPad.feather» не читается: сглаживание площадки сломается.");
                    }
                }
            }

            records.Add(new HouseRecord(id, addressId, AuthoredJson.Text(address, "street"), AuthoredJson.Text(address, "number"), AuthoredJson.Text(address, "cadastral"), problems));
        }

        return records.OrderBy(record => record.AddressId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace)
    {
        var records = Inspect(workspace);
        var streets = records.Select(record => record.Street).Where(street => street.Length > 0).Distinct(StringComparer.Ordinal).Count();
        var cadastral = records.Count(record => record.Cadastral.Length > 0);
        return
        [
            $"Построек с адресом: {records.Count}; улиц: {streets}; с кадастровым номером: {cadastral}.",
            "Согласованное перемещение адресует АВТОРСКУЮ ЗАПИСЬ: форму (запись каталога или часть набора), позицию, разворот и площадку под домом. Двери, окна, интерьер и входные точки отдельными авторскими записями не являются: они принадлежат модели набора и находятся в рантайме через владение формами столкновений, поэтому едут вместе с родителем по построению.",
            "Поэтому обещать «редактор двигает двери и входы по отдельности» нельзя: он двигает родителя, а части следуют за моделью — это и есть согласованность, которую требует единица.",
            "Совпадение проёмов и входов на кадре проверяется только в игре."
        ];
    }

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
