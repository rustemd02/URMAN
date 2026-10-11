using System.Globalization;
using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One authored kit placement, as the takeover will apply it.</summary>
public sealed record AuthoredKitPlacement(
    string EntityId,
    string Anchor,
    string Component,
    string State,
    IReadOnlyList<string> Problems);

/// <summary>
/// The first slice of the generated-to-authored half of the spec AI-44 slice. The
/// takeover (game/scripts/KitPlacementTakeover.cs) hands ownership of a generated kit
/// component to the authored plot as soon as the plot lists its logicalAnchor: from then
/// on the plot owns position, yaw, scale and visibility, and the generator's own numbers
/// are only a proposal. That makes every precondition below a real one, because the
/// runtime reads them with GetProperty (a missing field throws) and the anchor is the
/// lookup key in a dictionary (a duplicate silently drops one component).
///
/// What this projection cannot know is which components the builder actually placed: the
/// unmatched-anchor list ("the generator changed, the author must decide") is computed at
/// runtime from the builder side, and the notes say so instead of guessing.
/// </summary>
public static class KitTakeover
{
    public const string PlotPath = "game/content/world/act1_village_kit.world.v1.json";

    /// <summary>Every authored kit placement, with the takeover preconditions checked.</summary>
    public static IReadOnlyList<AuthoredKitPlacement> Inspect(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var placements = new List<AuthoredKitPlacement>();
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var components = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || AuthoredJson.Text(entity, "kind") != "kit-placement")
            {
                continue;
            }

            var parameters = entity["params"] as JsonObject;
            var problems = new List<string>();
            var anchor = AuthoredJson.Text(parameters, "logicalAnchor");
            if (anchor.Length == 0)
            {
                problems.Add("У размещения нет logicalAnchor: взятие под авторское владение ищет компонент именно по нему, и запись останется неуправляемой.");
            }
            else if (!anchors.Add(anchor))
            {
                problems.Add($"logicalAnchor «{anchor}» повторяется: словарь рантайма оставит только одну запись, и второй компонент молча потеряет авторские значения.");
            }

            var component = AuthoredJson.Text(parameters, "component");
            if (component.Length == 0)
            {
                problems.Add("У размещения нет component: сборщик не сопоставит его с частью набора.");
            }
            else
            {
                // The component name is the kit PART TYPE, not an identity: many
                // placements legitimately share one component, so repeats are counted
                // as facts rather than reported as a defect. The anchor is the key.
                components.Add(component);
            }

            if ((AuthoredJson.Text(parameters, "assetSource")).Length == 0)
            {
                problems.Add("У размещения нет assetSource: источник модели не указан.");
            }

            RequireVector(parameters, "position", 3, "позиция", problems);
            RequireVector(parameters, "scale", 3, "масштаб", problems);

            if (parameters is null || !parameters.ContainsKey("yawDegrees"))
            {
                problems.Add("Поле «yawDegrees» отсутствует: взятие под авторское владение читает поворот через GetProperty и упадёт.");
            }
            else if (TriggerScopeFacts.NumberProblem(parameters["yawDegrees"], out _) is { } yawProblem)
            {
                problems.Add($"Поле «yawDegrees»: {yawProblem} — поворот не читается, объект встанет неверно.");
            }

            if (parameters?["groundingReferenceXZ"] is { } grounding)
            {
                var readable = grounding is JsonArray { Count: 2 };
                if (grounding is JsonArray reference)
                {
                    for (var index = 0; index < reference.Count; index++)
                    {
                        if (TriggerScopeFacts.NumberProblem(reference[index], out _) is not null)
                        {
                            readable = false;
                        }
                    }
                }

                if (!readable)
                {
                    problems.Add("Поле «groundingReferenceXZ» не из двух читаемых чисел: высота объекта будет посчитана неверно.");
                }
            }

            foreach (var flag in new[] { "hidden", "retired" })
            {
                if (parameters?[flag] is { } node && (node is not JsonValue value || !value.TryGetValue<bool>(out _)))
                {
                    problems.Add($"Поле «{flag}» не булево: рантайм читает его через GetBoolean и упадёт.");
                }
            }

            var state = new List<string>();
            if (IsTrue(parameters?["retired"]))
            {
                state.Add("выведено из игры");
            }

            if (IsTrue(parameters?["hidden"]))
            {
                state.Add("скрыто");
            }

            if (parameters?["groundingReferenceXZ"] is not null)
            {
                state.Add("высота привязана к рельефу");
            }

            placements.Add(new AuthoredKitPlacement(id, anchor, component, string.Join(", ", state), problems));
        }

        return placements.OrderBy(placement => placement.Anchor, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace)
    {
        var placements = Inspect(workspace);
        var retired = placements.Count(placement => placement.State.Contains("выведено из игры", StringComparison.Ordinal));
        var hidden = placements.Count(placement => placement.State.Contains("скрыто", StringComparison.Ordinal));
        var grounded = placements.Count(placement => placement.State.Contains("рельеф", StringComparison.Ordinal));
        return
        [
            $"Авторских размещений набора: {placements.Count}; уникальных якорей: {placements.Select(p => p.Anchor).Where(a => a.Length > 0).Distinct(StringComparer.Ordinal).Count()}; различных частей набора: {placements.Select(p => p.Component).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).Count()}. Состояния: выведено из игры {retired}, скрыто {hidden}, с привязкой высоты к рельефу {grounded}.",
            "Взятие под авторское владение идёт по logicalAnchor: как только якорь есть в авторском мире, позиция, поворот, масштаб и видимость берутся из него, а значения сборщика становятся только предложением. Поэтому повтор якоря или компонента — не мелочь: словарь рантайма оставит одну запись, а вторая потеряет авторские значения.",
            "Список якорей, которые автор объявил, а сборщик не поставил («генератор изменился — решай автор»), считается на стороне сборщика во время запуска; эта проекция его не заменяет и не угадывает.",
            "Наличие записи не доказывает вид: совпадение кадра до и после взятия под авторское владение проверяется только в игре."
        ];
    }

    private static void RequireVector(JsonObject? parameters, string name, int count, string label, List<string> problems)
    {
        if (parameters?[name] is not JsonArray { Count: var actual } vector || actual != count)
        {
            problems.Add($"Поле «{name}» отсутствует или не из {count} чисел: {label} не читается, а взятие под авторское владение берёт его через GetProperty/ToVector3.");
            return;
        }

        for (var index = 0; index < vector.Count; index++)
        {
            if (TriggerScopeFacts.NumberProblem(vector[index], out _) is not null)
            {
                problems.Add($"Поле «{name}» содержит нечитаемое число: {label} не применится.");
                return;
            }
        }
    }

    private static bool IsTrue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
    /// <summary>
    /// The two state flags the runtime reads, and what each of them really does: "hidden"
    /// only clears visibility, while "retired" runs Retire, which also marks the node as
    /// retiredByPlot so it neither draws nor collides. A placement that is hidden without
    /// being retired is therefore invisible and still there, which may be intended (an
    /// invisible barrier) or may not.
    /// </summary>
    public static IReadOnlyList<string> StateNotes(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        // The buckets are disjoint, so a placement that is both retired and hidden is
        // counted once (as retired, which is the stronger state) and the total is the number
        // of placements rather than a sum that could count one twice.
        var retired = 0;
        var hiddenOnly = 0;
        var visible = 0;
        var total = 0;
        var hiddenNotRetired = new List<string>();
        var both = 0;
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || AuthoredJson.Text(entity, "kind") != "kit-placement")
            {
                continue;
            }

            var parameters = entity["params"] as JsonObject;
            var isRetired = (bool?)parameters?["retired"] ?? false;
            var isHidden = (bool?)parameters?["hidden"] ?? false;
            if (isRetired)
            {
                retired++;
            }

            total++;
            if (isRetired)
            {
                if (isHidden)
                {
                    both++;
                }
            }
            else if (isHidden)
            {
                hiddenOnly++;
                hiddenNotRetired.Add(AuthoredJson.Text(parameters, "logicalAnchor") is { Length: > 0 } anchor ? anchor : id);
            }
            else
            {
                visible++;
            }
        }

        return
        [
            $"Размещений: {total}; из них retired: {retired} (в том числе скрытых одновременно: {both}); скрытых без retired: {hiddenOnly}; видимых: {visible}. Группы не пересекаются, поэтому сумма равна числу размещений.",
            "Рантайм: «hidden» только снимает видимость, а «retired» вызывает Retire и помечает узел retiredByPlot — такая часть не рисуется и не сталкивается. Разница существенная: скрытая, но не retired часть остаётся в столкновениях.",
            hiddenNotRetired.Count == 0
                ? "Нет частей, которые скрыты, но не retired."
                : $"Скрыты, но не retired: {hiddenNotRetired.Count} ({string.Join(", ", hiddenNotRetired.Take(4))}). Проверьте, задумано ли это: рантайм такую часть не рисует, но она продолжает участвовать в столкновениях.",
            "Сверка с предложением сборщика (URMAN_KIT_HARVEST и Unmatched в рантайме) требует запуска сборщика и здесь не выполняется — это NOT_RUN."
        ];
    }

    /// <summary>Placements whose state flags are not the booleans the runtime reads.</summary>
    public static IReadOnlyList<string> StateGaps(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var gaps = new List<string>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || AuthoredJson.Text(entity, "kind") != "kit-placement")
            {
                continue;
            }

            var parameters = entity["params"] as JsonObject;
            foreach (var flag in new[] { "hidden", "retired" })
            {
                var value = parameters?[flag];
                if (value is null)
                {
                    continue;
                }

                if (value is not JsonValue json)
                {
                    gaps.Add($"{id}: поле «{flag}» не значение, а {value.GetType().Name}: рантайм читает его через GetBoolean и упадёт на этом размещении.");
                }
                else if (!json.TryGetValue<bool>(out _))
                {
                    gaps.Add($"{id}: поле «{flag}» не булево: рантайм читает его через GetBoolean и упадёт на этом размещении.");
                }
            }
        }

        return gaps;
    }

}
