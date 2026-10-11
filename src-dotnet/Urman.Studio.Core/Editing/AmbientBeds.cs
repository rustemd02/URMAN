using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One ambient bed: the file it plays, its zones and its declared rights.</summary>
public sealed record AmbientBedFacts(
    string BedId,
    string File,
    bool FileExists,
    int Zones,
    IReadOnlyList<string> UnknownZones,
    bool RightsDeclared,
    string Status,
    IReadOnlyList<string> Problems);

/// <summary>
/// The ambient half of the spec AI-18 slice. The runtime's AmbientAudioDirector loads
/// res://assets/audio/ambient_manifest.json and chooses a bed by the current zone, so this
/// projection checks what that path really needs: the file has to exist (res:// maps to the
/// game folder, not to the repository root), the zone strings have to be ones the project
/// actually uses, and a recorded bed has to declare where it came from and under what licence,
/// because publishing a sound without rights is not a step this project may take.
///
/// The manifest carries its own status, and that status is quoted rather than replaced:
/// listening, mix, loop and cultural review are the author's and the listeners' gate.
/// </summary>
public static class AmbientBeds
{
    public const string ManifestPath = "game/assets/audio/ambient_manifest.json";
    public const string DirectorPath = "game/scripts/AmbientAudioDirector.cs";
    public const string SecondManifestPath = "game/assets/audio/act1/sound_mood/beds_manifest.json";

    /// <summary>True when the runtime's director really loads this manifest.</summary>
    public static bool DirectorLoadsManifest(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), DirectorPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path)
            && File.ReadAllText(path).Contains(ManifestPath.Replace("game/", "res://"), StringComparison.Ordinal);
    }

    public static IReadOnlyList<AmbientBedFacts> Inspect(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var manifest = Manifest(root);
        if (manifest?["stems"] is not JsonArray stems)
        {
            return [];
        }

        var known = KnownZoneStrings(full);
        var beds = new List<AmbientBedFacts>();
        foreach (var stem in stems.OfType<JsonObject>())
        {
            var id = (string?)stem["id"] ?? "";
            var file = (string?)stem["file"] ?? "";
            var relative = file.Replace("res://", "game/", StringComparison.Ordinal);
            var exists = file.Length > 0 && File.Exists(Path.Combine(full, relative.Replace('/', Path.DirectorySeparatorChar)));
            var zones = (stem["zones"] as JsonArray ?? []).OfType<JsonValue>()
                .Select(zone => zone.GetValue<string>())
                .Where(zone => zone.Length > 0)
                .ToArray();
            var unknown = zones.Where(zone => !known.Contains(zone)).ToArray();
            var rights = ((string?)stem["source"] is { Length: > 0 }) && ((string?)stem["license"] is { Length: > 0 });
            var problems = new List<string>();

            if (file.Length == 0)
            {
                problems.Add("У беда нет файла: директору нечего играть.");
            }
            else if (!exists)
            {
                problems.Add($"Файл «{file}» не найден по пути {relative}: res:// указывает на папку игры, и без файла бед молчит.");
            }

            if (!rights)
            {
                problems.Add("Не указаны source и/или license: публиковать звук без прав нельзя, а происхождение теряется.");
            }

            if (zones.Length == 0)
            {
                problems.Add("Ни одной зоны: бед не выберется по текущей зоне.");
            }

            beds.Add(new AmbientBedFacts(id, file, exists, zones.Length, unknown, rights, (string?)stem["status"] ?? "", problems));
        }

        return beds.OrderBy(bed => bed.BedId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(string root)
    {
        var beds = Inspect(root);
        var manifest = Manifest(root);
        var unknownZones = beds.SelectMany(bed => bed.UnknownZones).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var missingFiles = beds.Count(bed => !bed.FileExists);
        var noRights = beds.Count(bed => !bed.RightsDeclared);
        var second = File.Exists(Path.Combine(Path.GetFullPath(root), SecondManifestPath.Replace('/', Path.DirectorySeparatorChar)));
        return
        [
            $"Бедов в манифесте: {beds.Count}; файлов нет: {missingFiles}; без прав: {noRights}; зон всего: {beds.Sum(bed => bed.Zones)}.",
            unknownZones.Length == 0
                ? "Каждая зона беда встречается в контенте или в исходниках рантайма."
                : $"Зоны, которых нет ни в контенте, ни в исходниках рантайма: {string.Join(", ", unknownZones)}. Такой бед не выберется по текущей зоне, пока строка не появится в игре.",
            DirectorLoadsManifest(root)
                ? $"Директор рантайма ({DirectorPath}) действительно загружает этот манифест, поэтому проверка относится к реальному пути звука."
                : "Директор рантайма этот манифест не загружает: проверять было бы нечего.",
            second
                ? $"Рядом есть второй каталог ({SecondManifestPath}): это отдельный список бедов, и считать один файл всей звуковой картиной нельзя."
                : "Второго каталога бедов нет.",
            $"Собственный статус манифеста: «{(string?)manifest?["status"] ?? ""}». Слушание, микс, луп и культурная приёмка остаются за автором и слушателями — это внешний гейт, а не эта проверка."
        ];
    }

    /// <summary>
    /// Every zone string the project mentions. The basis is stated rather than implied: a zone
    /// counts as known when its literal appears in the authored content or in the runtime
    /// sources, which is how the runtime's zone ids reach the code in the first place.
    /// </summary>
    private static HashSet<string> KnownZoneStrings(string full)
    {
        var zones = new HashSet<string>(StringComparer.Ordinal);
        foreach (var folder in new[] { "content", Path.Combine("game", "content"), Path.Combine("game", "scripts") })
        {
            var path = Path.Combine(full, folder);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(file) is not (".json" or ".cs"))
                {
                    continue;
                }

                var text = File.ReadAllText(file);
                foreach (System.Text.RegularExpressions.Match match in ZoneLiteral.Matches(text))
                {
                    zones.Add(match.Groups[1].Value);
                }
            }
        }

        return zones;
    }

    private static readonly System.Text.RegularExpressions.Regex ZoneLiteral = new(
        "\\\"((?:village|fullgame|house|fap|zirat|kara)[a-z0-9_@]*)\\\"",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static JsonObject? Manifest(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
    }
}
