using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One prepared bed of the sound-mood catalogue and what its provenance record claims.</summary>
public sealed record SoundMoodBedFacts(
    string BedId,
    string File,
    bool FileExists,
    bool FileHashMatches,
    bool PcmBytesMatch,
    int Sources,
    int StaleSources,
    IReadOnlyList<string> Problems);

/// <summary>
/// The second half of the spec AI-18 slice. The sound-mood catalogue does not only name files:
/// each bed declares its own sha256, its PCM byte count and the recipe that built it, and each
/// SOURCE it was mixed from declares a path, a licence and that source's sha256. A hash that no
/// longer matches means the documented provenance stopped describing the file, so a rebuild
/// would use different audio than the record says - which is exactly the reproducibility the
/// brief asks to keep.
///
/// The catalogue also carries its own acceptance state, quoted rather than replaced.
/// </summary>
public static class SoundMoodBeds
{
    public const string ManifestPath = "game/assets/audio/act1/sound_mood/beds_manifest.json";

    public static IReadOnlyList<SoundMoodBedFacts> Inspect(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(full, ManifestPath.Replace('/', Path.DirectorySeparatorChar)))) as JsonObject;
        var beds = new List<SoundMoodBedFacts>();
        foreach (var bed in (manifest?["beds"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var id = (string?)bed["id"] ?? "";
            var file = (string?)bed["file"] ?? "";
            var path = Path.Combine(full, file.Replace("res://", "game/", StringComparison.Ordinal).Replace('/', Path.DirectorySeparatorChar));
            var exists = file.Length > 0 && File.Exists(path);
            var hashMatches = false;
            var pcmMatches = false;
            var problems = new List<string>();
            if (!exists)
            {
                problems.Add($"{id}: файла «{file}» нет: бед не зазвучит, а проверить нечего.");
            }
            else
            {
                var bytes = File.ReadAllBytes(path);
                var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                hashMatches = string.Equals(actual, (string?)bed["sha256"], StringComparison.OrdinalIgnoreCase);
                if (!hashMatches)
                {
                    var declaredBed = (string?)bed["sha256"] ?? "";
                    var declaredBedPrefix = declaredBed.Length > 12 ? declaredBed[..12] : declaredBed;
                    problems.Add($"{id}: собственный sha256 не совпадает с файлом (объявлен {declaredBedPrefix}…, фактически {actual[..12]}…): запись перестала описывать то, что лежит на диске.");
                }

                if ((int?)bed["pcmBytes"] is { } declaredPcm && bytes.Length > 44)
                {
                    var dataChunk = BitConverter.ToUInt32(bytes, 40);
                    pcmMatches = dataChunk == (uint)declaredPcm;
                    if (!pcmMatches)
                    {
                        problems.Add($"{id}: pcmBytes {declaredPcm} не совпадает с data-чанком {dataChunk}.");
                    }
                }
            }

            var sources = (bed["sources"] as JsonArray ?? []).OfType<JsonObject>().ToArray();
            var stale = 0;
            foreach (var source in sources)
            {
                var sourcePath = (string?)source["path"] ?? "";
                if (sourcePath.Length == 0)
                {
                    continue;
                }

                var sourceFull = Path.Combine(full, sourcePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(sourceFull))
                {
                    stale++;
                    problems.Add($"{id}: источник «{sourcePath}» не найден: рецепт ссылается на отсутствующий файл.");
                    continue;
                }

                if (((string?)source["license"] ?? "").Length == 0)
                {
                    problems.Add($"{id}: у источника «{sourcePath}» нет лицензии.");
                }

                if ((string?)source["sourceSha256"] is { Length: > 0 } declaredSource)
                {
                    var actualSource = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceFull))).ToLowerInvariant();
                    if (!string.Equals(actualSource, declaredSource, StringComparison.OrdinalIgnoreCase))
                    {
                        stale++;
                        problems.Add($"{id}: sha256 источника «{sourcePath}» не совпадает (объявлен {declaredSource[..12]}…, фактически {actualSource[..12]}…): запись указывает на другое содержимое, чем описывает.");
                    }
                }
            }

            beds.Add(new SoundMoodBedFacts(id, file, exists, hashMatches, pcmMatches, sources.Length, stale, problems));
        }

        return beds;
    }

    public static IReadOnlyList<string> Notes(string root)
    {
        var beds = Inspect(root);
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetFullPath(root), ManifestPath.Replace('/', Path.DirectorySeparatorChar)))) as JsonObject;
        var stale = beds.Sum(bed => bed.StaleSources);
        var sources = beds.Sum(bed => bed.Sources);
        return
        [
            $"Бедов во втором каталоге: {beds.Count}; источников: {sources}; устаревших хешей источников: {stale}.",
            beds.All(bed => bed.FileExists && bed.FileHashMatches)
                ? "Собственные sha256 бедов совпадают с файлами, а pcmBytes — с data-чанком."
                : "Есть беды, чей собственный sha256 или pcmBytes не совпадает с файлом.",
            stale == 0
                ? "Все хеши источников совпадают: запись воспроизводима из того, что лежит на диске."
                : $"Хеши источников не совпадают у {stale} из {sources}: рецепт описывает другое содержимое, чем лежит на диске, и пересборка дала бы не тот звук, что записан. Это находка для владельца звука.",
            $"Собственная приёмка каталога: «{(string?)manifest?["listening"] ?? ""}» — слушание остаётся внешним гейтом, а не этой проверкой."
        ];
    }
}
