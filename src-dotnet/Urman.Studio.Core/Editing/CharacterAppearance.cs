using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One placed character, with what its appearance really controls.</summary>
public sealed record CharacterAppearanceFacts(
    string EntityId,
    string KitPrefix,
    string Profile,
    string Presentation,
    IReadOnlyList<string> Problems);

/// <summary>
/// The first slice of the character-track units (AI-28 prepared-base changes, AI-42 a new
/// garment, AI-43 a new character with skeleton, weights and sockets). Nothing here is a
/// modeller: the slice establishes what a placed character's appearance is made of today
/// and where its real limits are, because that is what a Studio step must respect.
///
/// The runtime contract (AuthoredWorldDirector.Residents.ConfigureResidentPresentation):
/// the kit prefix names the meshes of the shared character kit, skin width and depth are
/// CLAMPED to a narrow range, the three colour fields are looked up by MESH NAME SUBSTRING,
/// and the character's own policy string says it plainly - "existing winter kit; modest
/// silhouette and garment palette; unchanged face and bones".
/// </summary>
public static class CharacterAppearance
{
    public const string DirectoryPath = "game/scripts/AuthoredWorldDirector.Residents.cs";

    public const string DressingPath = "game/scripts/GeneratedCharacterKitDressing.cs";

    public const string Policy = "existing winter kit; modest silhouette and garment palette; unchanged face and bones";

    /// <summary>Skin width and depth are clamped by the runtime, so a wider value loses effect.</summary>
    public static readonly (string Name, double Min, double Max)[] Clamped =
    [
        ("width", .94d, 1.06d), ("depth", .95d, 1.05d)
    ];

    private static readonly Regex PrefixSet = new("\"([A-Z][A-Za-z]+)\"", RegexOptions.CultureInvariant);

    private static readonly Regex HexColor = new("^[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);

    /// <summary>The kit prefixes the dressing code really serves.</summary>
    public static IReadOnlySet<string> KnownPrefixes(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var path = Path.Combine(Path.GetFullPath(root), DressingPath.Replace('/', Path.DirectorySeparatorChar));
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return prefixes;
        }

        // The prefix set is declared as a string collection in the dressing code.
        var text = File.ReadAllText(path);
        var start = text.IndexOf("HumanPrefixes", StringComparison.Ordinal);
        if (start < 0)
        {
            return prefixes;
        }

        var end = text.IndexOf('}', start);
        foreach (Match match in PrefixSet.Matches(text[start..(end < 0 ? text.Length : end)]))
        {
            prefixes.Add(match.Groups[1].Value);
        }

        return prefixes;
    }

    public static IReadOnlyList<CharacterAppearanceFacts> Inspect(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var known = KnownPrefixes(root);
        var placements = new List<CharacterAppearanceFacts>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || (string?)entity["kind"] != "npc")
            {
                continue;
            }

            var parameters = entity["params"] as JsonObject;
            var problems = new List<string>();
            var prefix = (string?)parameters?["kitPrefix"] ?? "";
            if (known.Count == 0 && placements.Count == 0)
            {
                // No prefix set could be read at all: say so instead of passing silently.
                problems.Add("Не удалось прочитать набор префиксов из GeneratedCharacterKitDressing: сверить внешность нечем.");
            }

            if (prefix.Length == 0)
            {
                problems.Add("У NPC нет kitPrefix: меши тела не сопоставятся, персонаж останется неодетым или пустым.");
            }
            else if (known.Count > 0 && !known.Contains(prefix))
            {
                problems.Add($"Префикс «{prefix}» не обслуживает ни один набор в GeneratedCharacterKitDressing: тело для него не найдётся.");
            }

            var appearance = parameters?["residentAppearance"] as JsonObject;
            var profile = "";
            if (appearance is not null)
            {
                profile = (string?)appearance["profile"] ?? "";
                if (profile.Length == 0)
                {
                    problems.Add("Поле «residentAppearance.profile» пустое: в метаданные уйдёт запасное описание, и автор не увидит, чей это образ.");
                }

                foreach (var (name, min, max) in Clamped)
                {
                    if (appearance[name] is not { } node)
                    {
                        continue;
                    }

                    if (TriggerScopeFacts.NumberProblem(node, out var value) is { } why)
                    {
                        problems.Add($"Поле «{name}»: {why} — силуэт не применится.");
                        continue;
                    }

                    if (value < min || value > max)
                    {
                        // The runtime clamps instead of refusing, so this is editor policy:
                        // the authored number silently loses its effect.
                        problems.Add($"Поле «{name}» = {value.ToString(CultureInfo.InvariantCulture)} вне диапазона {min.ToString(CultureInfo.InvariantCulture)}–{max.ToString(CultureInfo.InvariantCulture)}: рантайм ЗАЖМЁТ значение, и авторская величина потеряет силу.");
                    }
                }

                foreach (var color in new[] { "coatColor", "headwearColor", "accentColor" })
                {
                    if (appearance[color] is not { } node)
                    {
                        continue;
                    }

                    if ((string?)node is not { } hex || !HexColor.IsMatch(hex))
                    {
                        problems.Add($"Поле «{color}» не цвет из 6 hex-цифр: материал получит неверную строку.");
                    }
                }
            }

            placements.Add(new CharacterAppearanceFacts(id, prefix, profile, appearance is null ? "нет residentAppearance" : Policy, problems));
        }

        return placements.OrderBy(placement => placement.EntityId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var placements = Inspect(workspace, root);
        var known = KnownPrefixes(root);
        var prefixes = placements.Select(placement => placement.KitPrefix).Where(prefix => prefix.Length > 0).Distinct(StringComparer.Ordinal).Count();
        var dressed = placements.Count(placement => placement.Profile.Length > 0);
        return
        [
            $"Размещённых персонажей: {placements.Count}; различных kitPrefix: {prefixes}; с образом (profile): {dressed}; префиксов знает набор: {known.Count} ({string.Join(", ", known.Order(StringComparer.Ordinal))}).",
            "Внешность персонажа сегодня — это: префикс набора (имена мешей тела), три цвета, которые рантайм ищет ПО ПОДСТРОКЕ ИМЕНИ МЕША (_Coat_/_CoatSkirt_, _Hat_/_Scarf_, _Skirt_/_Sash_), и ширина/глубина силуэта, которые рантайм ЗАЖИМАЕТ в узкий диапазон, а не отвергает.",
            "Собственная строка политики персонажа говорит прямо: «" + Policy + "». Значит скульптинг лица, новая одежда и новый скелет с весами и сокетами — это работа в Blender (tools/blender/generate_character_kit_v2.py) с последующим импортом, а не поле Studio; обещать их здесь нельзя.",
            "Проекция не может заглянуть внутрь GLB: если у набора нет меша с нужной подстрокой, цвет молча ничего не изменит. Это ограничение честно названо, а не спрятано."
        ];
    }
}
