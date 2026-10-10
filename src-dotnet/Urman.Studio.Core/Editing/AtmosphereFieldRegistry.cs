using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>Whether the game actually reacts to a registered atmosphere field.</summary>
public enum AtmosphereFieldReadStatus
{
    /// <summary>A live runtime consumer reads the value and it changes the frame.</summary>
    ReadByGame,

    /// <summary>The value only reaches meta or the atmosphere dump; no frame depends on it.</summary>
    MetaOrDumpOnly,

    /// <summary>The parser accepts the value and nothing reads it at all.</summary>
    ParsedNotRead
}

/// <summary>When the runtime parser demands a field; the editor must not offer less.</summary>
public enum AtmosphereFieldPresence
{
    /// <summary>Always required: the parser throws when it is missing.</summary>
    Always,

    /// <summary>Optional at params level, with a documented fallback.</summary>
    Optional,

    /// <summary>Required as soon as its optional block exists (snow/plume).</summary>
    RequiredIfBlockPresent
}

/// <summary>
/// One registered leaf of an atmosphere profile. Positions are the editor's
/// projection of <c>game/scripts/AtmosphereProfiles.cs</c>, not a second format:
/// every entry names the runtime consumer that reads it, and
/// <see cref="AtmosphereFieldRegistry.Inspect"/> reports any drift between this
/// list and the authored file.
/// </summary>
public sealed record AtmosphereField(
    string Path,
    string Kind,
    string Unit,
    string DefaultText,
    AtmosphereFieldPresence Presence,
    string? Block,
    AtmosphereFieldReadStatus ReadStatus,
    string Consumer,
    double? HardMin = null,
    double? HardMax = null,
    string? Note = null);

/// <summary>One authored atmosphere profile as the editor sees it.</summary>
public sealed record AtmosphereProfileView(
    string EntityId,
    string ProfileId,
    IReadOnlyList<string> Zones,
    bool PhaseOnly,
    bool ReachableByZone,
    string ReachabilityNote);

/// <summary>Everything the projection found in one authored atmosphere file.</summary>
public sealed record AtmosphereRegistryReport(
    IReadOnlyList<AtmosphereProfileView> Profiles,
    IReadOnlyList<string> UnknownDataFields,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<string> OutOfRangeValues,
    IReadOnlyList<string> DeadFieldsInData,
    IReadOnlyList<string> Notes);

/// <summary>
/// The registered atmosphere fields and a drift check against the real file.
/// Spec AI-18: the author must see actual fields with their real effect, and the
/// UI must not contain unknown fields. Fields the game does not read are listed
/// with <see cref="AtmosphereFieldReadStatus"/> instead of being hidden.
/// This is the single metadata owner for atmosphere fields: AI-10 operations and
/// any later atmosphere UI are meant to reference it rather than keep their own
/// list or ranges.
/// </summary>
public static class AtmosphereFieldRegistry
{
    public const string FilePath = "game/content/world/atmosphere.v1.json";
    public const string EntityKind = "atmosphere-profile";

    /// <summary>
    /// Zones the Act I runtime actually asks for. <c>Act1ConnectedWorld</c> tunes
    /// the outdoor environment with <c>night = zone == "kara_urman_night"</c> and
    /// skips interior placements, so these three pairs are the exercised scope.
    /// </summary>
    public static IReadOnlyList<(string Zone, bool Night)> ExercisedZoneStates { get; } =
        [("village_day", false), ("zirat_road", false), ("kara_urman_night", true)];

    private static readonly Regex HexColor = new("^[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

    private static readonly AtmosphereField[] RegisteredFields =
    [
        new("profile", "string", "идентификатор профиля", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "AtmosphereProfiles.Parse (ключ состояния)",
            Note: "По нему игра выбирает профиль; entity id в игре роли не играет."),
        new("identity", "string", "имя состояния", "\"\"", AtmosphereFieldPresence.Optional, null,
            AtmosphereFieldReadStatus.MetaOrDumpOnly, "Act1ConnectedWorld: мета unifiedAtmosphereIdentity; AtmosphereDump",
            Note: "Читателей меты не найдено: на кадр не влияет."),
        new("zones", "string[]", "зона | зона@day | зона@night", "[] (профиль только для превью)", AtmosphereFieldPresence.Optional, null,
            AtmosphereFieldReadStatus.ReadByGame, "AtmosphereProfiles.ResolveZoneProfileId; Act1ConnectedWorld",
            Note: "Единственный селектор состояния: область действия атмосферы."),
        new("ambient.energy", "number", "множитель энергии", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.AmbientLightEnergy"),
        new("ambient.color", "color", "цвет sRGB", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.AmbientLightColor"),
        new("ambient.skyContribution", "number", "доля 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.AmbientLightSkyContribution",
            Note: "Рядом источник принудительно Color, поэтому вклад неба движок может не учитывать; нужен кадр, чтобы подтвердить."),
        new("fog.color", "color", "цвет", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.FogLightColor"),
        new("fog.density", "number", "1/м", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.FogDensity"),
        new("fog.height", "number", "метры", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.FogHeight"),
        new("fog.heightDensity", "number", "1/м", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.FogHeightDensity"),
        new("fog.aerialPerspective", "number", "доля 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.FogAerialPerspective"),
        new("fog.skyAffect", "number", "доля 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.FogSkyAffect"),
        new("fog.sunScatter", "number", "доля 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.FogSunScatter"),
        new("exposure", "number", "множитель экспозиции", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.TonemapExposure"),
        new("ssao.intensity", "number", "множитель", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.SsaoIntensity"),
        new("ssao.radius", "number", "мировые единицы", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → Environment.SsaoRadius"),
        new("sky.top", "color", "цвет", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → ProceduralSkyMaterial.SkyTopColor"),
        new("sky.horizon", "color", "цвет", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → ProceduralSkyMaterial.SkyHorizonColor"),
        new("sky.groundHorizon", "color", "цвет", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → ProceduralSkyMaterial.GroundHorizonColor"),
        new("sky.groundBottom", "color", "цвет", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → ProceduralSkyMaterial.GroundBottomColor"),
        new("sky.sunAngleMax", "number", "градусы", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → ProceduralSkyMaterial.SunAngleMax"),
        new("sky.sunCurve", "number", "кривая", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → ProceduralSkyMaterial.SunCurve"),
        new("sky.cover[0]", "number", "R 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "AtmosphereProfiles → ProceduralSkyMaterial.SkyCoverModulate"),
        new("sky.cover[1]", "number", "G 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "AtmosphereProfiles → ProceduralSkyMaterial.SkyCoverModulate"),
        new("sky.cover[2]", "number", "B 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "AtmosphereProfiles → ProceduralSkyMaterial.SkyCoverModulate"),
        new("sky.cover[3]", "number", "A 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "AtmosphereProfiles → ProceduralSkyMaterial.SkyCoverModulate"),
        new("sun.color", "color", "цвет", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → DirectionalLight3D.LightColor"),
        new("sun.energy", "number", "энергия", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → DirectionalLight3D.LightEnergy"),
        new("sun.shadowOpacity", "number", "доля 0..1", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → DirectionalLight3D.ShadowOpacity"),
        new("sun.rotation[0]", "number", "градусы (высота)", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → DirectionalLight3D.RotationDegrees.X"),
        new("sun.rotation[1]", "number", "градусы (направление)", "нет — обязательное", AtmosphereFieldPresence.Always, null,
            AtmosphereFieldReadStatus.ReadByGame, "Act1ConnectedWorld → DirectionalLight3D.RotationDegrees.Y"),
        new("sun.rotation[2]", "number", "—", "не читается", AtmosphereFieldPresence.Optional, null,
            AtmosphereFieldReadStatus.ParsedNotRead, "нет",
            Note: "Парсер жёстко строит Vector3(rotation[0], rotation[1], 0): третий элемент молча отбрасывается."),
        new("snow.color", "color", "цвет", "нейтральный снег", AtmosphereFieldPresence.RequiredIfBlockPresent, "snow",
            AtmosphereFieldReadStatus.ReadByGame, "PainterlyMaterialLibrary.SetSnowMood; WinterParticleSurfaces; VehicleVisualFactory"),
        new("snow.coverage", "number", "множитель покрытости", "1", AtmosphereFieldPresence.RequiredIfBlockPresent, "snow",
            AtmosphereFieldReadStatus.ReadByGame, "PainterlyMaterialLibrary.SetSnowMood", 0d, 1d),
        new("snow.sparkle", "number", "множитель блеска", "1", AtmosphereFieldPresence.RequiredIfBlockPresent, "snow",
            AtmosphereFieldReadStatus.ReadByGame, "PainterlyMaterialLibrary.SetSnowMood", 0d, 2d),
        new("snow.tintStrength", "number", "доля 0..1", "0", AtmosphereFieldPresence.RequiredIfBlockPresent, "snow",
            AtmosphereFieldReadStatus.ReadByGame, "PainterlyMaterialLibrary.EffectiveSnowColor", 0d, 1d),
        new("weather.direction[0]", "number", "X единичного вектора XZ", "-1", AtmosphereFieldPresence.Optional, "weather",
            AtmosphereFieldReadStatus.ReadByGame, "VillageChimneySmoke (только дым из труб)",
            Note: "Значение покомпонентное. Если блока weather нет, рантайм берёт нормализованный вектор (-1, 0.22) целиком, а не это число. Снегопад и полосы ветра читают собственный вектор эмиттера."),
        new("weather.direction[1]", "number", "Z единичного вектора XZ", "0.22", AtmosphereFieldPresence.Optional, "weather",
            AtmosphereFieldReadStatus.ReadByGame, "VillageChimneySmoke (только дым из труб)",
            Note: "Значение покомпонентное. Нулевой вектор подменяется авторской константой (-1, 0.22)."),
        new("weather.speed", "number", "м/с", "8", AtmosphereFieldPresence.Optional, "weather",
            AtmosphereFieldReadStatus.ReadByGame, "VillageChimneySmoke (изгиб дыма)",
            Note: "Влияет на дым, а не на скорость снегопада."),
        new("weather.blizzardSpeed", "number", "м/с", "13.5", AtmosphereFieldPresence.Optional, "weather",
            AtmosphereFieldReadStatus.MetaOrDumpOnly, "мета unifiedAtmosphereBlizzardSpeed; AtmosphereDump",
            Note: "Игра значение не читает: эмиттер держит 11–16 м/с сам."),
        new("weather.flakes", "number", "число частиц", "4000", AtmosphereFieldPresence.Optional, "weather",
            AtmosphereFieldReadStatus.MetaOrDumpOnly, "мета unifiedAtmosphereFlakeBudget; AtmosphereDump",
            Note: "Игра значение не читает: Amount задан константой 4000/5200."),
        new("weather.blizzardFlakes", "number", "число частиц", "5200", AtmosphereFieldPresence.Optional, "weather",
            AtmosphereFieldReadStatus.MetaOrDumpOnly, "мета unifiedAtmosphereBlizzardFlakeBudget; AtmosphereDump",
            Note: "Игра значение не читает."),
        new("weather.streak", "number", "коэффициент растяжения", "0.012", AtmosphereFieldPresence.Optional, "weather",
            AtmosphereFieldReadStatus.MetaOrDumpOnly, "AtmosphereDump",
            Note: "Игра значение не читает: растяжение берётся из эмиттера."),
        new("plume.color", "color", "цвет", "серый дым", AtmosphereFieldPresence.RequiredIfBlockPresent, "plume",
            AtmosphereFieldReadStatus.ReadByGame, "VillageChimneySmoke → альбедо дыма"),
        new("plume.opacity", "number", "доля 0..1", "0.36", AtmosphereFieldPresence.RequiredIfBlockPresent, "plume",
            AtmosphereFieldReadStatus.ReadByGame, "VillageChimneySmoke → альфа альбедо"),
        new("plume", "block", "—", "блок отсутствует", AtmosphereFieldPresence.Optional, "plume",
            AtmosphereFieldReadStatus.ParsedNotRead, "нет",
            Note: "Наличие блока (HasPlumeBlock) не читает никто; читаются только color и opacity.")
    ];

    public static IReadOnlyList<AtmosphereField> Fields => RegisteredFields;

    /// <summary>Fields the game does not read: the UI must label them instead of pretending they work.</summary>
    public static IEnumerable<AtmosphereField> FieldsWithoutFrameEffect =>
        RegisteredFields.Where(candidate => candidate.ReadStatus != AtmosphereFieldReadStatus.ReadByGame);

    public static AtmosphereField? Find(string path) =>
        RegisteredFields.FirstOrDefault(field => string.Equals(field.Path, path, StringComparison.Ordinal));

    public static bool IsKnown(string path) => Find(path) is not null;

    /// <summary>
    /// Hard rules only: what makes the runtime parser throw or clamp. Advisory
    /// editor ranges live on the field and are not enforced here.
    /// </summary>
    public static IReadOnlyList<string> Validate(string path, JsonNode? value)
    {
        var problems = new List<string>();
        var field = Find(path);
        if (field is null)
        {
            problems.Add($"Неизвестное поле атмосферы: «{path}».");
            return problems;
        }

        if (value is null)
        {
            if (field.Presence != AtmosphereFieldPresence.Optional)
            {
                problems.Add($"Поле «{path}» обязательно, а значение пустое.");
            }

            return problems;
        }

        switch (field.Kind)
        {
            case "color":
                if (value is not JsonValue colorValue || !colorValue.TryGetValue<string>(out var color) || !HexColor.IsMatch(color))
                {
                    problems.Add($"Поле «{path}» должно быть цветом из 6 hex-цифр без «#».");
                }

                break;
            case "number":
                if (!TryNumber(value, out var number))
                {
                    problems.Add($"Поле «{path}» должно быть числом.");
                    break;
                }

                if (!double.IsFinite(number) || Math.Abs(number) > float.MaxValue)
                {
                    problems.Add($"Поле «{path}» = {number.ToString(CultureInfo.InvariantCulture)} игра не прочитает как число с плавающей точкой: GetSingle бросит исключение.");
                    break;
                }

                if (field.HardMin is { } min && number < min)
                {
                    problems.Add($"Поле «{path}» = {number.ToString(CultureInfo.InvariantCulture)} меньше допустимого {min.ToString(CultureInfo.InvariantCulture)}: игра ограничит значение.");
                }

                if (field.HardMax is { } max && number > max)
                {
                    problems.Add($"Поле «{path}» = {number.ToString(CultureInfo.InvariantCulture)} больше допустимого {max.ToString(CultureInfo.InvariantCulture)}: игра ограничит значение.");
                }

                break;
            case "string":
                if (value is not JsonValue stringValue || !stringValue.TryGetValue<string>(out _))
                {
                    problems.Add($"Поле «{path}» должно быть строкой: игра читает его через GetString.");
                }

                break;
            case "string[]":
                if (value is not JsonArray items)
                {
                    problems.Add($"Поле «{path}» должно быть массивом строк: игра читает элементы через GetString.");
                    break;
                }

                for (var index = 0; index < items.Count; index++)
                {
                    if (items[index] is not JsonValue item || !item.TryGetValue<string>(out _))
                    {
                        problems.Add($"Элемент «{path}[{index}]» должен быть строкой: игра читает его через GetString.");
                    }
                }

                break;
        }

        return problems;
    }

    /// <summary>
    /// Reads the authored file and reports the facts an author needs: which
    /// profiles exist, which of them the game can actually select, and where the
    /// data and the registry disagree.
    /// </summary>
    public static AtmosphereRegistryReport Inspect(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var notes = new List<string>();
        var full = Path.Combine(Path.GetFullPath(root), FilePath.Replace('/', Path.DirectorySeparatorChar));
        var profiles = new List<AtmosphereProfileView>();
        var unknown = new List<string>();
        var missing = new List<string>();
        var outOfRange = new List<string>();
        var dead = new List<string>();

        if (!File.Exists(full))
        {
            notes.Add($"Нет файла {FilePath}.");
            return new AtmosphereRegistryReport(profiles, unknown, missing, outOfRange, dead, notes);
        }

        if (JsonNode.Parse(File.ReadAllText(full)) is not JsonObject document || document["entities"] is not JsonArray entities)
        {
            notes.Add($"{FilePath}: нет массива entities.");
            return new AtmosphereRegistryReport(profiles, unknown, missing, outOfRange, dead, notes);
        }

        var declarations = new List<(string Id, List<string> Zones)>();
        foreach (var entity in entities.OfType<JsonObject>())
        {
            var entityId = Text(entity["id"]);
            if (entity["params"] is not JsonObject parameters)
            {
                notes.Add($"{entityId}: нет блока params.");
                continue;
            }

            var profileId = Text(parameters["profile"]);
            var zones = (parameters["zones"] as JsonArray)?.Select(Text).Where(value => value.Length > 0).ToList() ?? [];
            declarations.Add((profileId, zones));
            profiles.Add(new AtmosphereProfileView(entityId, profileId, zones, zones.Count == 0, false, ""));

            foreach (var (path, value) in Leaves(parameters))
            {
                var field = Find(path);
                if (field is null)
                {
                    unknown.Add($"{profileId}: {path}");
                    continue;
                }

                foreach (var problem in Validate(path, value))
                {
                    outOfRange.Add($"{profileId}: {problem}");
                }

                if (field.ReadStatus != AtmosphereFieldReadStatus.ReadByGame)
                {
                    dead.Add($"{profileId}: {path} — {field.Note ?? "игра значение не читает"}");
                }
            }

            foreach (var field in RegisteredFields.Where(field => field.Presence != AtmosphereFieldPresence.Optional))
            {
                var blockPresent = field.Block is null || parameters.ContainsKey(field.Block);
                if (blockPresent && !HasLeaf(parameters, field.Path))
                {
                    missing.Add($"{profileId}: {field.Path}");
                }
            }
        }

        var selected = new Dictionary<string, (string Zone, bool Night)>(StringComparer.Ordinal);
        foreach (var (zone, night) in ExercisedZoneStates)
        {
            if (ResolveZoneProfileId(declarations, zone, night) is { } id)
            {
                selected[id] = (zone, night);
            }
        }

        var resolved = profiles
            .Select(profile =>
            {
                if (selected.TryGetValue(profile.ProfileId, out var state))
                {
                    return profile with
                    {
                        ReachableByZone = true,
                        ReachabilityNote = $"Выбирается в обычной игре: зона {state.Zone}{(state.Night ? " (ночь)" : "")}."
                    };
                }

                return profile with
                {
                    ReachabilityNote = profile.PhaseOnly
                        ? "Нет ни одной зоны: профиль выбирается только явно, а не по зоне."
                        : $"Ни одно рабочее состояние зоны не даёт эту запись: {string.Join(", ", profile.Zones)}."
                };
            })
            .ToArray();

        foreach (var profile in resolved.Where(profile => !profile.ReachableByZone))
        {
            notes.Add($"{profile.ProfileId}: не выбирается в обычной игре по зоне — {profile.ReachabilityNote}");
        }

        return new AtmosphereRegistryReport(resolved, unknown, missing, outOfRange, dead, notes);
    }

    /// <summary>Mirrors the documented runtime rule: a suffixed claim wins over a bare one, in file order.</summary>
    public static string? ResolveZoneProfileId(IReadOnlyList<(string Id, List<string> Zones)> declarations, string zoneId, bool night)
    {
        var suffix = night ? "@night" : "@day";
        string? bare = null;
        foreach (var (id, zones) in declarations)
        {
            foreach (var claim in zones)
            {
                if (string.Equals(claim, zoneId + suffix, StringComparison.Ordinal))
                {
                    return id;
                }

                if (string.Equals(claim, zoneId, StringComparison.Ordinal) && bare is null)
                {
                    bare = id;
                }
            }
        }

        return bare;
    }

    private static IEnumerable<(string Path, JsonNode? Value)> Leaves(JsonNode node, string prefix = "")
    {
        if (node is not JsonObject obj)
        {
            yield break;
        }

        foreach (var pair in obj)
        {
            var path = prefix.Length == 0 ? pair.Key : $"{prefix}.{pair.Key}";
            if (pair.Value is JsonObject nested)
            {
                var registeredBlock = Find(path) is not null;
                foreach (var leaf in Leaves(nested, path))
                {
                    yield return leaf;
                }

                // A registered block (plume) is reported as its own leaf, so a
                // block whose flag nothing reads stays visible; an unregistered
                // object is only a container and must not become a field.
                if (registeredBlock)
                {
                    yield return (path, null);
                }

                continue;
            }

            if (pair.Value is JsonArray array)
            {
                // An array registered as a whole field (zones) stays one leaf; a
                // numeric array (sky.cover, sun.rotation) is reported per index.
                if (Find(path) is not null)
                {
                    yield return (path, array);
                    continue;
                }

                var index = 0;
                foreach (var item in array)
                {
                    if (item is JsonObject itemObject)
                    {
                        foreach (var leaf in Leaves(itemObject, $"{path}[{index}]"))
                        {
                            yield return leaf;
                        }
                    }
                    else
                    {
                        yield return ($"{path}[{index}]", item);
                    }

                    index++;
                }

                if (array.Count == 0)
                {
                    yield return (path, array);
                }

                continue;
            }

            yield return (path, pair.Value);
        }
    }

    private static bool HasLeaf(JsonObject parameters, string path)
    {
        var segments = path.Split('.');
        JsonNode? current = parameters;
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var bracket = segment.IndexOf('[');
            if (bracket > 0)
            {
                var name = segment[..bracket];
                var itemIndex = int.Parse(segment[(bracket + 1)..^1], CultureInfo.InvariantCulture);
                if (current is not JsonObject owner || owner[name] is not JsonArray array || itemIndex >= array.Count)
                {
                    return false;
                }

                if (index == segments.Length - 1)
                {
                    return true;
                }

                current = array[itemIndex];
                continue;
            }

            if (current is not JsonObject node || !node.TryGetPropertyValue(segment, out var next))
            {
                return false;
            }

            if (index == segments.Length - 1)
            {
                return true;
            }

            current = next;
        }

        return false;
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static bool TryNumber(JsonNode value, out double number)
    {
        number = 0d;
        return value is JsonValue jsonValue && jsonValue.TryGetValue<double>(out number);
    }
}
