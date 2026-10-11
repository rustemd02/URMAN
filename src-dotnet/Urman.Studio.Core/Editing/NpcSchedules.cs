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

    /// <summary>
    /// A route or routine capability the runtime implements, the field it reads, and how many
    /// authored schedule blocks actually use it.
    /// </summary>
    public sealed record NpcRoutineCapability(string Capability, string RuntimeField, int AuthoredBlocks, string Note);

    public const string RuntimePath = "game/scripts/AuthoredWorldDirector.Schedules.cs";

    /// <summary>The runtime fields this projection expects AuthoredWorldDirector.Schedules to read.</summary>
    public static readonly IReadOnlyList<string> RuntimeFields =
        ["place", "route", "speed", "motion", "walkMotion", "when", "yawDegrees", "waitSeconds", "look", "follow"];

    /// <summary>
    /// The routine capabilities the runtime implements against the ones the campaign really
    /// uses. The runtime can walk a route, wait at a point, look at a target, follow the
    /// player and switch blocks by condition; a campaign that never writes those fields leaves
    /// the capability unexercised, which is a content fact worth stating instead of assuming
    /// the routes work because the code exists.
    /// </summary>
    public static IReadOnlyList<NpcRoutineCapability> Capabilities(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var blocks = 0;
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || (string?)entity["kind"] != "npc")
            {
                continue;
            }

            foreach (var block in ((entity["params"] as JsonObject)?["schedule"] as JsonArray ?? []).OfType<JsonObject>())
            {
                blocks++;
                foreach (var field in RuntimeFields)
                {
                    if (field == "waitSeconds" || field == "look")
                    {
                        // Read inside a route point, so they count when a point carries them.
                        if ((block["route"] as JsonArray ?? []).OfType<JsonObject>().Any(point => point[field] is not null))
                        {
                            counts[field] = counts.GetValueOrDefault(field) + 1;
                        }

                        continue;
                    }

                    if (block[field] is not null)
                    {
                        counts[field] = counts.GetValueOrDefault(field) + 1;
                    }
                }
            }
        }

        return
        [
            new("Точка стоянки", "place", counts.GetValueOrDefault("place"), $"Блоков с place: {counts.GetValueOrDefault("place")} из {blocks}."),
            new("Маршрут", "route", counts.GetValueOrDefault("route"), counts.GetValueOrDefault("route") == 0
                ? "Ни один блок не задаёт route: рантайм умеет обходить маршрут, но в кампании маршрутов нет — способность не упражняется контентом."
                : $"Блоков с route: {counts.GetValueOrDefault("route")} из {blocks}."),
            new("Ожидание в точке", "waitSeconds", counts.GetValueOrDefault("waitSeconds"), "Читается внутри точки маршрута."),
            new("Взгляд в точке", "look", counts.GetValueOrDefault("look"), "Значение «player» или точка."),
            new("Смена блока по условию", "when", counts.GetValueOrDefault("when"), "Первое подходящее условие побеждает."),
            new("Разворот", "yawDegrees", counts.GetValueOrDefault("yawDegrees"), "Ставится и на блоке, и в точке."),
            new("Скорость", "speed", counts.GetValueOrDefault("speed"), "Иначе берётся скорость по умолчанию."),
            new("Движение стоянки", "motion", counts.GetValueOrDefault("motion"), "Иначе берётся idle по умолчанию."),
            new("Движение ходьбы", "walkMotion", counts.GetValueOrDefault("walkMotion"), "Иначе берётся walk по умолчанию."),
            new("Следование", "follow", counts.GetValueOrDefault("follow"), "distance и loseMetres.")
        ];
    }

    /// <summary>Fields this projection names that the runtime reader does not mention at all.</summary>
    public static IReadOnlyList<string> MissingFromRuntime(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), RuntimePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return [];
        }

        var text = File.ReadAllText(path);
        return RuntimeFields.Where(field => !text.Contains($"\"{field}\"", StringComparison.Ordinal)).ToArray();
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
