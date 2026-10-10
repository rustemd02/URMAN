using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>Which kind of authored audio source a field belongs to.</summary>
public enum AudioSourceScope
{
    /// <summary>A local one-shot emitter: an event of the village household catalogue.</summary>
    LocalEmitter,

    /// <summary>A continuous bed stem of the shared ambience bus.</summary>
    SharedBusStem
}

/// <summary>Whether the game reacts to an authored audio field.</summary>
public enum AudioFieldReadStatus
{
    /// <summary>A live runtime consumer reads the value.</summary>
    ReadByGame,

    /// <summary>Kept for authors and tools; no runtime consumer reads it.</summary>
    AuthoredOnly,

    /// <summary>Provenance of a prepared asset, read by the tooling that rebuilds it.</summary>
    ProvenanceOnly
}

/// <summary>One registered field of an authored audio source.</summary>
public sealed record AudioSourceField(
    AudioSourceScope Scope,
    string Path,
    string Kind,
    string Unit,
    string DefaultText,
    AudioFieldReadStatus ReadStatus,
    string Consumer,
    bool Always,
    double? HardMin = null,
    double? HardMax = null,
    string? Note = null);

/// <summary>One authored household emitter as the editor sees it, with the scope facts an author needs.</summary>
public sealed record AudioEmitterView(
    string Id,
    string Label,
    string File,
    bool Outdoor,
    bool DayOnly,
    bool NightOnly,
    double Radius,
    double VolumeDb,
    double MinWait,
    double MaxWait,
    bool FileExists,
    long FileBytes,
    IReadOnlyList<string> AudibleZones,
    string ScopeNote);

/// <summary>One authored ambience stem as the editor sees it.</summary>
public sealed record AudioStemView(
    string Id,
    string File,
    IReadOnlyList<string> Zones,
    double VolumeDb,
    bool VolumeDeclared,
    bool Loop,
    IReadOnlyList<string> SharedWith,
    bool FileExists);

/// <summary>
/// The contract for a Studio audition: one existing source, the shared bus the
/// runtime already uses, a preview ceiling that cannot exceed the authored
/// level, no loop and a mandatory stop. It writes no user audio setting and adds
/// no second bus.
/// </summary>
public sealed record AudioAuditionPlan(
    AudioSourceScope Scope,
    string File,
    string Bus,
    double VolumeDb,
    bool Loop,
    bool RequiresStop,
    string Note);

/// <summary>Everything the projection found in the authored audio sources.</summary>
public sealed record AudioRegistryReport(
    IReadOnlyList<AudioEmitterView> Emitters,
    IReadOnlyList<AudioStemView> Stems,
    IReadOnlyList<string> UnknownFields,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<string> OutOfRangeValues,
    IReadOnlyList<string> FieldsWithoutFrameEffect,
    IReadOnlyList<string> EmittersNotAuditionable,
    IReadOnlyList<string> AuditionTargetsOutsideCatalog,
    IReadOnlyList<string> Notes);

/// <summary>
/// The registered fields of both authored audio sources - the village household
/// emitters and the shared ambience stems - with the runtime consumer of each and
/// a drift check against the real files. Spec AI-18: the author must see actual
/// sources and their real scope, and nothing the game does not read may look like
/// a working control.
/// This is the single metadata owner for audio fields; a later Studio panel and
/// any AI operation are meant to reference it instead of keeping their own list.
/// </summary>
public static class AudioSourceRegistry
{
    public const string EmitterPath = "game/content/world/act1_village_life.v1.json";
    public const string ManifestPath = "game/assets/audio/ambient_manifest.json";
    public const string DebugPanelPath = "game/scripts/DebugSoundPanel.cs";

    /// <summary>The runtime refuses to start unless the catalogue holds exactly this many events.</summary>
    public const int ExpectedEmitterCount = 47;

    /// <summary>The runtime refuses a clip larger than this (VillageHouseholdDirector.GetClip).</summary>
    public const long ClipByteBudget = 600_000;

    /// <summary>Preview never exceeds this; the authored value only lowers it.</summary>
    public const double PreviewCeilingDb = -12d;

    /// <summary>The bus the existing household voices and the bed already use; never a second bus.</summary>
    public const string AuditionBus = "Ambience";

    private static readonly string[] ProvenanceFields =
    [
        "source", "license", "status", "design", "conversion", "preparation", "preparedSha256", "preparedMetrics",
        "gainNote", "sourcePreviewUrl", "sourcePreviewSha256", "sourceLicenseUrl", "sourcePreviewOnly", "sourceProof", "seed"
    ];

    private static readonly Regex DebugPanelClip = new("ClipRoot\\s*\\+\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant);

    private static readonly AudioSourceField[] RegisteredFields =
    [
        // ---- local emitter ----
        new(AudioSourceScope.LocalEmitter, "id", "string", "идентификатор события", "нет — обязательное", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector: ключ словаря и проверка уникальности", true,
            Note: "Ровно 47 уникальных id, иначе деревня не поднимется."),
        new(AudioSourceScope.LocalEmitter, "label", "string", "человеческое имя", "нет — обязательное", AudioFieldReadStatus.AuthoredOnly, "нет: Motif.Label не читает ни один потребитель", true,
            Note: "Подпись для людей и будущего UI; на кадр и звук не влияет."),
        new(AudioSourceScope.LocalEmitter, "file", "string", "res:// путь к клипу", "нет — обязательное", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector: ResourceLoader.Exists и GetClip", true,
            Note: "Отсутствующий файл роняет старт деревни."),
        new(AudioSourceScope.LocalEmitter, "outdoor", "bool", "улица или дом", "false", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector: house.Outside/Inside, −3 дБ и фильтры в доме", false,
            Note: "Меняет и позицию эмиттера, и микс."),
        new(AudioSourceScope.LocalEmitter, "dayOnly", "bool", "только днём", "false", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector.Tick: гейт по времени суток", false),
        new(AudioSourceScope.LocalEmitter, "nightOnly", "bool", "только ночью", "false", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector.Tick: гейт по времени суток", false,
            Note: "Одновременные dayOnly и nightOnly делают событие неслышимым всегда."),
        new(AudioSourceScope.LocalEmitter, "radius", "number", "метры", "нет — обязательное", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector: MaxDistance и отбор по расстоянию", true, 6d, 25d,
            Note: "Рантайм бросает исключение вне 6..25."),
        new(AudioSourceScope.LocalEmitter, "volumeDb", "number", "дБ", "0 (если поля нет)", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector.StartEvent: базовый уровень события", false, -60d, 0d,
            Note: "Рантайм НЕ проверяет это поле; диапазон задаёт реестр."),
        new(AudioSourceScope.LocalEmitter, "minWait", "number", "секунды", "нет — обязательное", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector: пауза до следующего события", true, 20d,
            Note: "Рантайм бросает исключение при значении меньше 20."),
        new(AudioSourceScope.LocalEmitter, "maxWait", "number", "секунды", "нет — обязательное", AudioFieldReadStatus.ReadByGame, "VillageHouseholdDirector: верхняя граница паузы", true,
            Note: "Рантайм бросает исключение, если maxWait меньше minWait."),

        // ---- shared bus stem ----
        new(AudioSourceScope.SharedBusStem, "id", "string", "идентификатор stem", "нет — обязательное", AudioFieldReadStatus.ReadByGame, "AmbientAudioDirector.LoadManifest: ключ и проверка", true),
        new(AudioSourceScope.SharedBusStem, "file", "string", "res:// путь к клипу", "нет — обязательное", AudioFieldReadStatus.ReadByGame, "AmbientAudioDirector: источник непрерывной подложки", true),
        new(AudioSourceScope.SharedBusStem, "zones", "string[]", "зоны маршрутизации", "нет — обязательное, минимум одна", AudioFieldReadStatus.ReadByGame, "AmbientAudioDirector.SetZone: ключ выбора подложки", true,
            Note: "Две подложки на одну зону — рантайм бросает исключение."),
        new(AudioSourceScope.SharedBusStem, "volumeDb", "number", "дБ", "−12 (если поля нет)", AudioFieldReadStatus.ReadByGame, "AmbientAudioDirector: целевая громкость подложки", false, -60d, 0d,
            Note: "Рантайм проверяет конечность и диапазон −60..0."),
        new(AudioSourceScope.SharedBusStem, "loop", "bool", "зацикливание", "false", AudioFieldReadStatus.ReadByGame, "AmbientAudioDirector.ConfigureNativeLoop", false)
    ];

    public static IReadOnlyList<AudioSourceField> Fields => RegisteredFields;

    public static IEnumerable<AudioSourceField> EmitterFields => RegisteredFields.Where(candidate => candidate.Scope == AudioSourceScope.LocalEmitter);

    public static IEnumerable<AudioSourceField> StemFields => RegisteredFields.Where(candidate => candidate.Scope == AudioSourceScope.SharedBusStem);

    /// <summary>Fields the game does not read: the UI must name them instead of pretending they work.</summary>
    public static IEnumerable<AudioSourceField> FieldsWithoutFrameEffect =>
        RegisteredFields.Where(candidate => candidate.ReadStatus != AudioFieldReadStatus.ReadByGame);

    /// <summary>Provenance keys of a prepared stem; read by the rebuilding tools, not by the game.</summary>
    public static IReadOnlyList<string> ProvenanceKeys => ProvenanceFields;

    public static AudioSourceField? Find(AudioSourceScope scope, string path) =>
        RegisteredFields.FirstOrDefault(candidate => candidate.Scope == scope && string.Equals(candidate.Path, path, StringComparison.Ordinal));

    /// <summary>
    /// Hard rules only: what makes the runtime throw or clamp. Advisory limits an
    /// author should respect, but the game does not enforce, are reported with the
    /// field and get a warning from <see cref="Inspect"/>.
    /// </summary>
    public static IReadOnlyList<string> Validate(AudioSourceScope scope, string path, JsonNode? value)
    {
        var problems = new List<string>();
        var field = Find(scope, path);
        if (field is null)
        {
            problems.Add($"Неизвестное поле аудио ({scope}): «{path}».");
            return problems;
        }

        if (value is null)
        {
            if (field.Always)
            {
                problems.Add($"Поле «{path}» обязательно, а значение пустое.");
            }

            return problems;
        }

        switch (field.Kind)
        {
            case "string":
                if (value is not JsonValue text || !text.TryGetValue<string>(out _))
                {
                    problems.Add($"Поле «{path}» должно быть строкой.");
                }

                break;
            case "string[]":
                if (value is not JsonArray items)
                {
                    problems.Add($"Поле «{path}» должно быть массивом строк.");
                    break;
                }

                for (var index = 0; index < items.Count; index++)
                {
                    if (items[index] is not JsonValue item || !item.TryGetValue<string>(out _))
                    {
                        problems.Add($"Элемент «{path}[{index}]» должен быть строкой.");
                    }
                }

                break;
            case "bool":
                if (value is not JsonValue flag || !flag.TryGetValue<bool>(out _))
                {
                    problems.Add($"Поле «{path}» должно быть true или false.");
                }

                break;
            case "number":
                if (!TryNumber(value, out var number) || !double.IsFinite(number) || Math.Abs(number) > float.MaxValue)
                {
                    problems.Add($"Поле «{path}» должно быть конечным числом в диапазоне float.");
                    break;
                }

                if (field.HardMin is { } min && number < min)
                {
                    problems.Add($"Поле «{path}» = {number.ToString(CultureInfo.InvariantCulture)} меньше допустимого {min.ToString(CultureInfo.InvariantCulture)}: игра откажется работать.");
                }

                if (field.HardMax is { } max && number > max)
                {
                    problems.Add($"Поле «{path}» = {number.ToString(CultureInfo.InvariantCulture)} больше допустимого {max.ToString(CultureInfo.InvariantCulture)}: игра откажется работать.");
                }

                break;
        }

        return problems;
    }

    /// <summary>
    /// The audition contract for one existing source: shared bus, preview level
    /// never above <see cref="PreviewCeilingDb"/> and never above the authored
    /// level, no loop, stop required.
    /// </summary>
    public static AudioAuditionPlan PlanAudition(AudioSourceScope scope, string file, double authoredVolumeDb, string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        var level = Math.Min(authoredVolumeDb, PreviewCeilingDb);
        return new AudioAuditionPlan(scope, file, AuditionBus, level, false, true, note);
    }

    public static IReadOnlyList<string> DistinctZones(IEnumerable<AudioStemView> stems) =>
        stems.SelectMany(stem => stem.Zones).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reads both authored files and reports the facts an author needs.</summary>
    public static AudioRegistryReport Inspect(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var notes = new List<string>();
        var unknown = new List<string>();
        var missing = new List<string>();
        var outOfRange = new List<string>();
        var dead = new List<string>();

        var emitters = ReadEmitters(full, unknown, missing, outOfRange, dead, notes);
        var stems = ReadStems(full, unknown, missing, outOfRange, notes);
        var (notAuditionable, outsideCatalog) = AuditionCoverage(full, emitters, stems, notes);

        return new AudioRegistryReport(emitters, stems, unknown, missing, outOfRange, dead, notAuditionable, outsideCatalog, notes);
    }

    private static List<AudioEmitterView> ReadEmitters(
        string root,
        List<string> unknown,
        List<string> missing,
        List<string> outOfRange,
        List<string> dead,
        List<string> notes)
    {
        var emitters = new List<AudioEmitterView>();
        var file = Resolve(root, EmitterPath);
        if (!File.Exists(file))
        {
            notes.Add($"Нет файла {EmitterPath}.");
            return emitters;
        }

        if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject document)
        {
            notes.Add($"{EmitterPath}: корень не объект.");
            return emitters;
        }

        if (document["events"] is not JsonArray events)
        {
            notes.Add($"{EmitterPath}: нет массива events (StudioWorkspace индексирует только «entities», поэтому файл для редактора невидим).");
            return emitters;
        }

        if (events.Count != ExpectedEmitterCount)
        {
            outOfRange.Add($"{EmitterPath}: событий {events.Count}, а рантайм требует ровно {ExpectedEmitterCount}.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in events.OfType<JsonObject>())
        {
            var id = Text(item["id"]);
            if (!seen.Add(id))
            {
                outOfRange.Add($"{EmitterPath}: повтор id «{id}» — рантайм бросает исключение.");
            }

            foreach (var (path, value) in item)
            {
                if (Find(AudioSourceScope.LocalEmitter, path) is null)
                {
                    unknown.Add($"{id}: {path}");
                    continue;
                }

                foreach (var problem in Validate(AudioSourceScope.LocalEmitter, path, value))
                {
                    outOfRange.Add($"{id}: {problem}");
                }
            }

            foreach (var field in EmitterFields.Where(candidate => candidate.Always))
            {
                if (!item.ContainsKey(field.Path))
                {
                    missing.Add($"{id}: {field.Path}");
                }
            }

            var file64 = Text(item["file"]);
            var clipRelative = file64.StartsWith("res://", StringComparison.Ordinal) ? "game/" + file64["res://".Length..] : file64;
            var clipPath = Resolve(root, clipRelative);
            var clipsExists = clipPath.Length > 0 && File.Exists(clipPath);
            var bytes = clipsExists ? new FileInfo(clipPath).Length : 0L;
            if (!clipsExists)
            {
                outOfRange.Add($"{id}: клип {file64} не найден — деревня не поднимется.");
            }
            else if (bytes > ClipByteBudget)
            {
                outOfRange.Add($"{id}: клип {bytes} байт больше рантайм-бюджета {ClipByteBudget} — GetClip бросит исключение.");
            }

            var minWait = Number(item["minWait"]);
            var maxWait = Number(item["maxWait"]);
            if (minWait > 0 && maxWait > 0 && maxWait < minWait)
            {
                outOfRange.Add($"{id}: maxWait {maxWait} меньше minWait {minWait} — рантайм бросает исключение.");
            }

            var dayOnly = Bool(item["dayOnly"]);
            var nightOnly = Bool(item["nightOnly"]);
            if (dayOnly && nightOnly)
            {
                outOfRange.Add($"{id}: dayOnly и nightOnly одновременно — событие не сработает никогда.");
            }

            var outdoor = Bool(item["outdoor"]);
            var zones = new List<string> { "village_day" };
            if (!outdoor)
            {
                zones.Add("house_old_pc");
            }

            if (!dayOnly)
            {
                zones.Add("kara_urman_night");
            }

            emitters.Add(new AudioEmitterView(
                id,
                Text(item["label"]),
                file64,
                outdoor,
                dayOnly,
                nightOnly,
                Number(item["radius"]),
                Number(item["volumeDb"]),
                minWait,
                maxWait,
                clipsExists,
                bytes,
                zones,
                $"Библиотечное событие, а не размещённая точка: дом выбирается по порядку массива (i % {events.Count}) после сортировки владельцев, поэтому запись слышна в 1–2 реальных домах, а перестановка массива меняет всю привязку."));
        }

        foreach (var field in FieldsWithoutFrameEffect.Where(candidate => candidate.Scope == AudioSourceScope.LocalEmitter))
        {
            if (emitters.Count > 0 && events.OfType<JsonObject>().All(item => item.ContainsKey(field.Path)))
            {
                dead.Add($"{EmitterPath}: {field.Path} — {field.Note ?? "игра значение не читает"}");
            }
        }

        return emitters;
    }

    private static List<AudioStemView> ReadStems(
        string root,
        List<string> unknown,
        List<string> missing,
        List<string> outOfRange,
        List<string> notes)
    {
        var stems = new List<AudioStemView>();
        var file = Resolve(root, ManifestPath);
        if (!File.Exists(file))
        {
            notes.Add($"Нет файла {ManifestPath} (путь вне SourceGlobs Studio).");
            return stems;
        }

        if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject document || document["stems"] is not JsonArray entries)
        {
            notes.Add($"{ManifestPath}: нет массива stems.");
            return stems;
        }

        var zoneOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var sameFile = entries.OfType<JsonObject>().GroupBy(item => Text(item["file"]), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Select(item => Text(item["id"])).ToArray(), StringComparer.Ordinal);
        foreach (var item in entries.OfType<JsonObject>())
        {
            var id = Text(item["id"]);
            foreach (var (path, value) in item)
            {
                if (Find(AudioSourceScope.SharedBusStem, path) is not null)
                {
                    foreach (var problem in Validate(AudioSourceScope.SharedBusStem, path, value))
                    {
                        outOfRange.Add($"{id}: {problem}");
                    }

                    continue;
                }

                if (ProvenanceFields.Contains(path, StringComparer.Ordinal))
                {
                    continue;
                }

                unknown.Add($"{id}: {path}");
            }

            foreach (var field in StemFields.Where(candidate => candidate.Always))
            {
                if (!item.ContainsKey(field.Path))
                {
                    missing.Add($"{id}: {field.Path}");
                }
            }

            foreach (var zone in (item["zones"] as JsonArray ?? []).Select(Text).Where(zone => zone.Length > 0))
            {
                if (!zoneOwners.TryAdd(zone, id))
                {
                    outOfRange.Add($"{ManifestPath}: зона «{zone}» назначена двум подложкам ({zoneOwners[zone]} и {id}) — рантайм бросает исключение.");
                }
            }

            var stemFile = Text(item["file"]);
            var resolved = stemFile.StartsWith("res://", StringComparison.Ordinal) ? Resolve(root, "game/" + stemFile["res://".Length..]) : "";
            var shared = sameFile.TryGetValue(stemFile, out var users) ? users.Where(user => !string.Equals(user, id, StringComparison.Ordinal)).ToArray() : [];
            stems.Add(new AudioStemView(
                id,
                stemFile,
                (item["zones"] as JsonArray ?? []).Select(Text).Where(zone => zone.Length > 0).ToArray(),
                Number(item["volumeDb"]) is var declared && item.ContainsKey("volumeDb") ? declared : -12d,
                item.ContainsKey("volumeDb"),
                Bool(item["loop"]),
                shared,
                resolved.Length > 0 && File.Exists(resolved)));
        }

        foreach (var id in stems.Where(stem => !stem.FileExists).Select(stem => stem.Id))
        {
            outOfRange.Add($"{ManifestPath}: {id}: файл подложки не найден.");
        }

        return stems;
    }

    /// <summary>
    /// Compares the files the game really plays with the handwritten audition list
    /// of the in-game debug panel, so a missing or stale entry is visible instead
    /// of being mistaken for coverage.
    /// </summary>
    private static (IReadOnlyList<string> NotAuditionable, IReadOnlyList<string> OutsideCatalog) AuditionCoverage(
        string root,
        IReadOnlyList<AudioEmitterView> emitters,
        IReadOnlyList<AudioStemView> stems,
        List<string> notes)
    {
        var panel = Resolve(root, DebugPanelPath);
        if (!File.Exists(panel))
        {
            notes.Add($"Нет файла {DebugPanelPath}: покрытие прослушивания не проверено.");
            return ([], []);
        }

        const string clipRoot = "res://assets/audio/act1/";
        var audition = DebugPanelClip.Matches(File.ReadAllText(panel))
            .Select(match => clipRoot + match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        notes.Add($"{DebugPanelPath}: найдено {audition.Count} литеральных клипов прослушивания; символические ссылки (например AdhanPath) в сравнение не входят.");

        var played = emitters.Select(emitter => emitter.File)
            .Concat(stems.Select(stem => stem.File))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var notAuditionable = played.Where(file => !audition.Contains(file)).Order(StringComparer.Ordinal).ToArray();
        var outsideCatalog = audition.Where(file => !played.Contains(file, StringComparer.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        return (notAuditionable, outsideCatalog);
    }

    private static string Resolve(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static double Number(JsonNode? node) => TryNumber(node, out var number) ? number : 0d;

    /// <summary>Reads any JSON number, not only one that was created as a double.</summary>
    private static bool TryNumber(JsonNode? node, out double number)
    {
        number = 0d;
        if (node is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<double>(out number))
        {
            return true;
        }

        if (value.TryGetValue<float>(out var single))
        {
            number = single;
            return true;
        }

        if (value.TryGetValue<int>(out var small))
        {
            number = small;
            return true;
        }

        if (value.TryGetValue<long>(out var integer))
        {
            number = integer;
            return true;
        }

        if (value.TryGetValue<decimal>(out var precise))
        {
            number = (double)precise;
            return true;
        }

        return false;
    }

    private static bool Bool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}
