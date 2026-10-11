using System.Globalization;
using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One placed NPC and whether its schedule can really be staged.</summary>
public sealed record NpcScheduleFacts(
    string NpcId,
    string CharacterId,
    string KitPrefix,
    int BlockCount,
    IReadOnlyList<string> Motions,
    IReadOnlyList<string> Problems);

/// <summary>
/// The routes, occupations and animations half of the spec AI-22 slice. An NPC owns a
/// schedule of blocks; the runtime picks the first block whose <c>when</c> matches (a
/// block without <c>when</c> is unconditional), reads its <c>id</c> with GetProperty,
/// and plays its motions through AnimationCatalog. The motions are not free text: an id
/// missing from the animation catalogue is REFUSED at play time with a reason, so this
/// projection checks every declared motion against the real catalogue instead of
/// trusting the authored name.
/// </summary>
public static class NpcSchedules
{
    public const string AnimationCatalogPath = "game/content/animations/catalog.v1.json";

    /// <summary>The defaults the reader applies when a block does not name a motion.</summary>
    public const string DefaultWalkMotion = "urman.anim:walk";

    public const string DefaultIdleMotion = "urman.anim:idle";

    /// <summary>The motion ids the animation catalogue declares.</summary>
    public static IReadOnlySet<string> CatalogMotions(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var file = workspace.Files.FirstOrDefault(candidate => candidate.RelativePath == AnimationCatalogPath);
        var motions = new HashSet<string>(StringComparer.Ordinal);
        // The workspace can hand back either the document object or its entity array,
        // depending on how the file is rooted, so both shapes are accepted.
        var entities = file?.Snapshot() switch
        {
            JsonArray array => array,
            JsonObject catalog when catalog["entities"] is JsonArray declared => declared,
            _ => null
        };
        if (entities is null)
        {
            return motions;
        }

        foreach (var entity in entities.OfType<JsonObject>())
        {
            if ((string?)entity["kind"] == "animation" && (string?)entity["id"] is { Length: > 0 } id)
            {
                motions.Add(id);
            }
        }

        return motions;
    }

    public static IReadOnlyList<NpcScheduleFacts> Inspect(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var catalogue = CatalogMotions(workspace);
        var npcs = new List<NpcScheduleFacts>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || (string?)entity["kind"] != "npc")
            {
                continue;
            }

            var problems = new List<string>();
            var parameters = entity["params"] as JsonObject;
            var characterId = (string?)parameters?["characterId"] ?? "";
            if (characterId.Length == 0)
            {
                problems.Add("У NPC нет characterId: рантайм ищет расписание именно по нему, и персонаж не получит маршрута.");
            }

            if (parameters?["position"] is not JsonArray { Count: 3 } position || !AllFinite(position))
            {
                problems.Add("Позиция не из трёх конечных чисел: NPC встанет не туда или не встанет вовсе.");
            }

            if (parameters?["scale"] is { } scale)
            {
                if (!TryNumber(scale, out var scaleValue))
                {
                    problems.Add("Поле «scale» не число.");
                }
                else if (scaleValue <= 0)
                {
                    problems.Add($"scale = {scaleValue.ToString(CultureInfo.InvariantCulture)}: неположительный масштаб ломает рост персонажа.");
                }
            }

            var motions = new List<string>();
            var blocks = parameters?["schedule"] as JsonArray;
            if (blocks is null || blocks.Count == 0)
            {
                problems.Add("Расписание отсутствует или пусто: у NPC не будет ни маршрута, ни занятия.");
            }
            else
            {
                for (var index = 0; index < blocks.Count; index++)
                {
                    if (blocks[index] is not JsonObject block)
                    {
                        problems.Add($"Блок расписания #{index + 1} не объект.");
                        continue;
                    }

                    // The reader calls GetProperty("id") on the chosen block, so a missing
                    // id is not a warning: it throws while the schedule is staged.
                    if (((string?)block["id"] ?? "").Length == 0)
                    {
                        problems.Add($"У блока расписания #{index + 1} нет id: читатель берёт его через GetProperty и упадёт при выборе этого блока.");
                    }

                    if (block["when"] is { } when && when is not JsonArray)
                    {
                        problems.Add($"У блока «{(string?)block["id"] ?? index.ToString(CultureInfo.InvariantCulture)}» условие when не массив правил.");
                    }

                    if (block["place"] is { } place && (place is not JsonArray { Count: 3 } || !AllFinite((JsonArray)place)))
                    {
                        problems.Add($"У блока «{(string?)block["id"] ?? index.ToString(CultureInfo.InvariantCulture)}» точка place не из трёх конечных чисел.");
                    }

                    if (block["speed"] is { } speed && TryNumber(speed, out var speedValue) && speedValue <= 0)
                    {
                        problems.Add($"У блока «{(string?)block["id"] ?? index.ToString(CultureInfo.InvariantCulture)}» скорость {speedValue.ToString(CultureInfo.InvariantCulture)}: персонаж не сдвинется.");
                    }

                    foreach (var (name, fallback) in new[] { ("motion", DefaultIdleMotion), ("walkMotion", DefaultWalkMotion) })
                    {
                        var motion = (string?)block[name] ?? fallback;
                        motions.Add(motion);
                        if (motion.Length == 0)
                        {
                            problems.Add($"У блока «{(string?)block["id"] ?? index.ToString(CultureInfo.InvariantCulture)}» поле «{name}» пустое: движение не выбрано.");
                        }
                        else if (catalogue.Count > 0 && !catalogue.Contains(motion))
                        {
                            problems.Add($"Движение «{motion}» отсутствует в каталоге анимаций: AnimationCatalog.Play откажет в нём с причиной, и NPC останется в текущей позе.");
                        }
                    }
                }
            }

            npcs.Add(new NpcScheduleFacts(id, characterId, (string?)parameters?["kitPrefix"] ?? "", blocks?.Count ?? 0, motions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), problems));
        }

        return npcs.OrderBy(npc => npc.NpcId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>What an author should know before touching routes and animations.</summary>
    public static IReadOnlyList<string> Notes(StudioWorkspace workspace)
    {
        var npcs = Inspect(workspace);
        var catalogue = CatalogMotions(workspace);
        var blocks = npcs.Sum(npc => npc.BlockCount);
        return
        [
            $"Размещённых NPC: {npcs.Count}; блоков расписания: {blocks}; движений в каталоге анимаций: {catalogue.Count}.",
            "Блок расписания выбирается как первый, чьё условие when истинно; блок без when безусловен, поэтому порядок блоков и есть приоритет. Читатель берёт id выбранного блока через GetProperty, а движения — из AnimationCatalog по идентификатору.",
            $"Если блок не называет движение, берутся умолчания {DefaultWalkMotion} и {DefaultIdleMotion}; неизвестный идентификатор каталог отвергает с текстом причины, а не молча."
        ];
    }

    private static bool AllFinite(JsonArray vector) =>
        vector.All(item => item is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number));

    private static bool TryNumber(JsonNode node, out double number)
    {
        number = 0;
        return node is JsonValue value && value.TryGetValue<double>(out number) && double.IsFinite(number);
    }
}
