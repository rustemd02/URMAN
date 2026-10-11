using System.Globalization;
using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>The Tamara fence parameters and whether the static reader could load them.</summary>
public sealed record FenceFacts(
    string EntityId,
    double FaceX,
    double ZStart,
    double ZEnd,
    int PanelCount,
    double WicketZ,
    double CrashSpeed,
    double BreakRadius,
    IReadOnlyList<string> Problems)
{
    public double Span => ZEnd - ZStart;
}

/// <summary>
/// The puzzle half of the spec AI-25 slice: the authored fence-run parameters checked
/// against what TamaraFenceQuest really reads. Those reads happen in STATIC field
/// initialisers through AuthoredWorldPlot.Params/Number/GetInt32, so a missing entity,
/// a missing field, a wrong type or a value the float conversion cannot carry does not
/// degrade gently: the type fails to initialise and the quest never starts. That is why
/// every case is reported here before the game runs.
/// </summary>
public static class TamaraFence
{
    public const string EntityId = "urman.world:act1/tamara-plot/fence";

    /// <summary>The six numbers the reader takes with Plot.Number, plus the integer count.</summary>
    private static readonly string[] Numbers = ["faceX", "zStart", "zEnd", "wicketZ", "crashSpeed", "breakRadius"];

    /// <summary>Every authored fence-run of the campaign, checked against the reader.</summary>
    public static IReadOnlyList<FenceFacts> Inspect(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var fences = new List<FenceFacts>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject candidate || (string?)candidate["kind"] != "fence-run")
            {
                continue;
            }

            fences.Add(Check(id, candidate));
        }

        return fences.OrderBy(fence => fence.EntityId, StringComparer.Ordinal).ToArray();
    }

    private static FenceFacts Check(string id, JsonObject entity)
    {
        var problems = new List<string>();
        if ((string?)entity["kind"] != "fence-run")
        {
            problems.Add($"Вид записи «{(string?)entity["kind"] ?? "нет"}» вместо fence-run: читатель ищет именно fence-run.");
        }

        if (entity["params"] is not JsonObject parameters)
        {
            problems.Add("У записи нет params: AuthoredWorldPlot.Params бросит исключение, и забор не соберётся.");
            return new FenceFacts(id, 0, 0, 0, 0, 0, 0, 0, problems);
        }

        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var name in Numbers)
        {
            if (!TryNumber(parameters, name, out var number, out var why))
            {
                problems.Add($"Поле «{name}»: {why}");
                continue;
            }

            values[name] = number;
        }

        var panelCount = 0;
        if (parameters["panelCount"] is not JsonValue countValue || !countValue.TryGetValue<int>(out panelCount))
        {
            problems.Add("Поле «panelCount» отсутствует или не целое число: читатель берёт его через GetInt32 и упадёт на дробном или слишком большом значении.");
            panelCount = 0;
        }
        else if (panelCount < 1)
        {
            problems.Add($"panelCount = {panelCount}: при нуле или минусе цикл сборки панелей не создаст ни одной, и забор исчезнет.");
        }

        if (values.TryGetValue("zStart", out var zStart) && values.TryGetValue("zEnd", out var zEnd) && zEnd == zStart)
        {
            problems.Add("zStart и zEnd совпадают: все панели встанут в одну точку, забор будет вырожденным.");
        }

        foreach (var name in new[] { "crashSpeed", "breakRadius" })
        {
            if (values.TryGetValue(name, out var number) && number <= 0)
            {
                problems.Add($"Поле «{name}» = {number.ToString(CultureInfo.InvariantCulture)}: с нулём или минусом авария и разлом не сработают.");
            }
        }

        return new FenceFacts(
            id,
            values.GetValueOrDefault("faceX"),
            values.GetValueOrDefault("zStart"),
            values.GetValueOrDefault("zEnd"),
            panelCount,
            values.GetValueOrDefault("wicketZ"),
            values.GetValueOrDefault("crashSpeed"),
            values.GetValueOrDefault("breakRadius"),
            problems);
    }

    /// <summary>What an author should know before touching the puzzle parameters.</summary>
    public static IReadOnlyList<string> Notes(StudioWorkspace workspace)
    {
        var fences = Inspect(workspace);
        if (fences.Count == 0)
        {
            return ["Записи fence-run в авторских мирах кампании нет: головоломка с забором не размещена."];
        }

        var fence = fences[0];
        return
        [
            $"Заборов fence-run в кампании: {fences.Count}. Первый: {fence.PanelCount} панелей на участке z {fence.ZStart.ToString(CultureInfo.InvariantCulture)}…{fence.ZEnd.ToString(CultureInfo.InvariantCulture)} (длина {fence.Span.ToString(CultureInfo.InvariantCulture)}), калитка z {fence.WicketZ.ToString(CultureInfo.InvariantCulture)}, авария от {fence.CrashSpeed.ToString(CultureInfo.InvariantCulture)} м/с, разлом в радиусе {fence.BreakRadius.ToString(CultureInfo.InvariantCulture)} м.",
            "Читатель TamaraFenceQuest берёт эти поля в статических инициализаторах (Plot.Number и GetInt32) до первого кадра, поэтому пропуск поля, неверный тип или значение, не представимое как float, роняет инициализацию типа целиком — квест не начнётся даже без аварии.",
            "panelCount задаёт и число панелей, и шаг по z между ними; zStart/zEnd — границы участка, wicketZ — калитка, crashSpeed сравнивается со скоростью Нивы, breakRadius — радиус разлома."
        ];
    }

    private static bool TryNumber(JsonObject parameters, string name, out double number, out string why)
    {
        number = 0;
        if (parameters[name] is not JsonValue value || !value.TryGetValue<double>(out var parsed))
        {
            why = "поле отсутствует или не число — инициализация типа упадёт.";
            return false;
        }

        if (!double.IsFinite(parsed))
        {
            why = "значение не конечное число — геометрия забора станет неопределённой.";
            return false;
        }

        if (Math.Abs(parsed) > float.MaxValue)
        {
            why = $"значение {parsed.ToString(CultureInfo.InvariantCulture)} не представимо как float: после преобразования получится бесконечность.";
            return false;
        }

        number = parsed;
        why = "";
        return true;
    }
}
