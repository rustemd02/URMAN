using System.Globalization;
using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One cutscene action type and the parameters the runtime demands.</summary>
public sealed record CutsceneActionType(
    string Action,
    IReadOnlyList<string> RequiredParams,
    string Executor,
    string? Note = null);

/// <summary>What one cutscene action really does, and what is wrong with it.</summary>
public sealed record CutsceneActionFacts(
    string EntityId,
    string Action,
    string Summary,
    IReadOnlyList<string> Problems);

/// <summary>
/// The cutscene actions as the single runtime executor reads them (spec AI-34,
/// TSC07). Everything here is copied from <c>TamaraFenceCutscene</c>: an unknown
/// action is pushed out as "needs a new mechanic", a missing parameter throws in
/// the executor, an undeclared actor throws in the actors dictionary, a text
/// suffix is resolved as <c>urman.chapter1:text/&lt;suffix&gt;</c>, and a clip is a
/// suffix over the actor's character prefix, so a clip the character does not have
/// is ignored silently and the actor simply stands still.
/// </summary>
public static class CutsceneActions
{
    public const string RuntimePath = "game/scripts/TamaraFenceCutscene.cs";
    public const string AnimationCataloguePath = "game/content/animations/catalog.v1.json";

    /// <summary>The namespace the runtime prefixes to every spoken line's text suffix.</summary>
    public const string TextPrefix = "urman.chapter1:text/";

    private static readonly CutsceneActionType[] Registered =
    [
        new("show", ["actor", "visible"], "Show", "Показывает или скрывает персонажа."),
        new("place", ["actor", "at"], "Place", "Ставит персонажа в точку [x, z]; для героя z ограничена снизу."),
        new("clip", ["actor", "clip"], "Clip", "Клип — суффикс к префиксу персонажа; чужого клипа у него нет, и тогда он молча не анимируется."),
        new("cut", ["tag", "position", "look", "fov", "seconds"], "Cut", "Монтажный кадр: точка съёмки, взгляд, FOV и длительность; handheld и pushIn необязательны."),
        new("wait", ["seconds"], "Pause", "Пауза."),
        new("say", ["speaker", "text", "seconds"], "Say/SayAs", "Текст — суффикс: рантайм сам подставляет urman.chapter1:text/. talk задаёт, кто говорит."),
        new("move", ["actor", "stops", "speed"], "MoveAlong", "Точки остановок [x, z]; finalFacing необязателен."),
        new("face", ["actor", "target"], "FaceTowards/TurnGuyTowards", "Поворот к цели."),
        new("filming", ["on"], "Filming", "Включает или выключает съёмку."),
        new("laugh", ["level"], "SetGuyLaugh", "Уровень смеха."),
    ];

    public static IReadOnlyList<CutsceneActionType> Types => Registered;

    private static readonly System.Text.RegularExpressions.Regex CaseLabel = new(
        "case\\s+\"([a-z]+)\"\\s*:", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex ActorKey = new(
        "\\[\"([a-z][a-z0-9_]*)\"\\]\\s*=", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>The kinds the executor really switches over, read from its source.</summary>
    public static IReadOnlySet<string> HandledKinds(string root)
    {
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        var text = ExecutorSource(root);
        var start = text.IndexOf("switch (p.GetProperty(\"action\")", StringComparison.Ordinal);
        foreach (System.Text.RegularExpressions.Match match in CaseLabel.Matches(start < 0 ? text : text[start..]))
        {
            kinds.Add(match.Groups[1].Value);
        }

        return kinds;
    }

    /// <summary>The actor keys the executor's own dictionary is built with.</summary>
    public static IReadOnlySet<string> RuntimeActorKeys(string root)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var text = ExecutorSource(root);
        var start = text.IndexOf("var actors = new Dictionary<string, Node3D>", StringComparison.Ordinal);
        if (start < 0)
        {
            return keys;
        }

        var end = text.IndexOf("};", start, StringComparison.Ordinal);
        foreach (System.Text.RegularExpressions.Match match in ActorKey.Matches(text[start..(end < 0 ? text.Length : end)]))
        {
            keys.Add(match.Groups[1].Value);
        }

        return keys;
    }

    /// <summary>
    /// Compares the registered table with the executor source. A table that promises an
    /// executor the code no longer has, or misses a kind the code gained, would make the
    /// editor lie about what the game can play.
    /// </summary>
    public static IReadOnlyList<string> VerifyAgainstExecutor(string root)
    {
        var handled = HandledKinds(root);
        if (handled.Count == 0)
        {
            return ["Не удалось прочитать виды действий из исходника исполнителя: сверить таблицу нечем."];
        }

        var problems = new List<string>();
        foreach (var type in Registered.Where(type => !handled.Contains(type.Action)))
        {
            problems.Add($"Таблица обещает исполнителя {type.Executor} для действия «{type.Action}», но в исходнике такого вида больше нет.");
        }

        foreach (var kind in handled.Where(kind => Find(kind) is null))
        {
            problems.Add($"Исполнитель умеет действие «{kind}», а таблица его не знает: автор не увидит его в редакторе.");
        }

        return problems;
    }

    private static string ExecutorSource(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var path = Path.Combine(Path.GetFullPath(root), RuntimePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public static CutsceneActionType? Find(string action) =>
        Registered.FirstOrDefault(type => string.Equals(type.Action, action, StringComparison.Ordinal));

    /// <summary>The text id the runtime builds from a spoken suffix.</summary>
    public static string TextIdFor(string suffix) => TextPrefix + suffix;

    /// <summary>
    /// Checks one action against the executor and the real project: what the game
    /// will do, and what it will silently skip.
    /// </summary>
    public static CutsceneActionFacts Describe(StudioWorkspace workspace, JsonObject? action, IReadOnlyCollection<string> declaredActors,
        IReadOnlyCollection<string>? runtimeActors = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(declaredActors);
        var parameters = action?["params"] as JsonObject;
        var id = (string?)action?["id"] ?? "";
        if (parameters is null)
        {
            return new CutsceneActionFacts(id, "", "У действия нет блока params.", ["Нет блока params."]);
        }

        var name = Text(parameters["action"]);
        var type = Find(name);
        var problems = new List<string>();
        if (type is null)
        {
            return new CutsceneActionFacts(id, name, $"Действие «{name}» игра не исполняет.", [$"Действие «{name}» не имеет исполнителя: рантайм пишет «needs a new mechanic» и пропускает его."]);
        }

        foreach (var required in type.RequiredParams.Where(required => !parameters.ContainsKey(required)))
        {
            problems.Add($"Нет параметра «{required}»: исполнитель {type.Executor} читает его через GetProperty и упадёт.");
        }

        var summary = new List<string> { $"Действие «{name}» исполняет {type.Executor}." };
        if (type.Note is { Length: > 0 } note)
        {
            summary.Add(note);
        }

        foreach (var actorField in new[] { "actor", "talk" })
        {
            if (Text(parameters[actorField]) is not { Length: > 0 } actor)
            {
                continue;
            }

            if (!declaredActors.Contains(actor, StringComparer.Ordinal))
            {
                problems.Add($"В поле «{actorField}» указан «{actor}», которого нет среди объявленных персонажей сцены: словарь actors бросит исключение.");
            }
            else if (runtimeActors is { Count: > 0 } && !runtimeActors.Contains(actor, StringComparer.Ordinal))
            {
                problems.Add($"В поле «{actorField}» указан «{actor}»: сценарий его объявляет, но словарь самого исполнителя такого ключа не знает, и поиск упадёт.");
            }
        }

        if (name == "say")
        {
            var suffix = Text(parameters["text"]);
            if (suffix.Length == 0)
            {
                problems.Add("У реплики нет text: игрок увидит пустую строку.");
            }
            else if (workspace.Locate(TextIdFor(suffix)) is null)
            {
                problems.Add($"Реплика «{TextIdFor(suffix)}» не найдена в проекте: рантайм подставит этот id, а игрок увидит пустую строку.");
            }
            else
            {
                summary.Add($"Реплика берётся из {TextIdFor(suffix)}.");
            }
        }

        if (name == "clip")
        {
            var clip = Text(parameters["clip"]);
            if (clip.Length == 0)
            {
                problems.Add("У клипа нет имени: персонаж не будет анимирован.");
            }
            else
            {
                var (known, complete) = KnownAnimation(workspace, clip);
                if (!known && complete)
                {
                    problems.Add($"Клипа «{clip}» нет в каталоге анимаций: имя собирается как префикс персонажа плюс суффикс, и чужого клипа у него нет — персонаж молча останется без анимации.");
                }
                else if (!known)
                {
                    summary.Add($"Проверить клип «{clip}» по каталогу анимаций не удалось: в каталоге есть записи не объектами.");
                }
                else
                {
                    summary.Add($"Клип «{clip}» есть в каталоге анимаций.");
                }
            }
        }

        if (parameters["position"] is JsonArray position && position.Count != 3)
        {
            problems.Add("position должен быть точкой из трёх чисел.");
        }

        if (parameters["stops"] is JsonArray stops && stops.OfType<JsonArray>().Any(stop => stop.Count != 2))
        {
            problems.Add("Каждая остановка в stops должна быть парой [x, z].");
        }

        return new CutsceneActionFacts(id, name, string.Join(" ", summary), problems);
    }

    /// <summary>
    /// Checks one cutscene file from disk. The root of a cutscene is not an
    /// addressable entity in the workspace (only its actions are), so the panel and
    /// this projection read the same file directly.
    /// </summary>
    public static (IReadOnlyList<CutsceneActionFacts> Actions, IReadOnlyList<string> Problems, IReadOnlyList<string> Notes) Inspect(StudioWorkspace workspace, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var path = Path.Combine(workspace.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return ([], [$"Нет файла {relativePath}."], []);
        }

        return JsonNode.Parse(File.ReadAllText(path)) is JsonObject root
            ? Inspect(workspace, root)
            : ([], [$"{relativePath}: корень не объект."], []);
    }

    /// <summary>Checks the whole cutscene: every action, and the honest limits of the fixed namespace.</summary>
    public static (IReadOnlyList<CutsceneActionFacts> Actions, IReadOnlyList<string> Problems, IReadOnlyList<string> Notes) Inspect(StudioWorkspace workspace, JsonObject? cutscene)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var actors = (cutscene?["actors"] as JsonObject)?.Select(pair => pair.Key).ToArray() ?? [];
        var runtimeActors = RuntimeActorKeys(workspace.Root);
        var actions = new List<CutsceneActionFacts>();
        // The registered table is compared with the executor source before any action is
        // judged: a table that drifted would mislabel every action after it.
        var problems = new List<string>(VerifyAgainstExecutor(workspace.Root));
        var notes = new List<string>
        {
            "Сцена и её исполнитель зашиты на один тайминговый файл и namespace urman.chapter1: выбор другой кампании или катсцены не поддержан (это граница AI-34, а не готовность)."
        };
        if (cutscene?["entities"] is not JsonArray entities)
        {
            return (actions, ["У катсцены нет массива entities."], notes);
        }

        for (var index = 0; index < entities.Count; index++)
        {
            if (entities[index] is not JsonObject entity)
            {
                // A malformed action must be named: the executor would never see it, and
                // silently skipping it would make the cutscene look complete.
                problems.Add($"Действие #{index + 1}: не объект — исполнитель его не увидит.");
                continue;
            }

            var facts = Describe(workspace, entity, actors, runtimeActors);
            actions.Add(facts);
            problems.AddRange(facts.Problems.Select(problem => $"{facts.EntityId}: {problem}"));
        }

        notes.Add($"Персонажей объявлено: {actors.Length}; действий: {actions.Count}.");
        if (actors.Length == 0)
        {
            problems.Add("В сцене не объявлено ни одного персонажа: любое действие с actor упадёт на словаре actors.");
        }

        var unknown = actions.Where(facts => facts.Action.Length > 0 && Find(facts.Action) is null).Select(facts => facts.Action).Distinct(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            notes.Add($"Действия без исполнителя: {string.Join(", ", unknown)}.");
        }

        return (actions, problems, notes);
    }

    /// <summary>
    /// Source-presence check: the runtime file still names the actions this
    /// projection registers and the text prefix it builds. It is not runtime proof.
    /// </summary>
    public static IReadOnlyList<string> VerifyAgainstRuntime(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var problems = new List<string>();
        var path = Path.Combine(Path.GetFullPath(root), RuntimePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            problems.Add($"Нет файла {RuntimePath}: связь с исполнителем не проверена.");
            return problems;
        }

        var text = File.ReadAllText(path);
        foreach (var type in Registered)
        {
            if (!text.Contains($"case \"{type.Action}\"", StringComparison.Ordinal))
            {
                problems.Add($"Действие «{type.Action}» больше не упоминается в {RuntimePath}: это проверка присутствия в исходнике, и она просит перечитать проекцию.");
            }
        }

        if (!text.Contains(TextPrefix, StringComparison.Ordinal))
        {
            problems.Add($"Рантайм больше не строит текст как {TextPrefix}<суффикс>: проекция устарела.");
        }

        return problems;
    }

    /// <summary>
    /// Whether the catalog holds the clip, and whether the catalog could be read at
    /// all. A malformed entry must not turn into a claim that the clip is missing.
    /// </summary>
    private static (bool Known, bool Complete) KnownAnimation(StudioWorkspace workspace, string clip)
    {
        var catalogue = Path.Combine(workspace.Root, AnimationCataloguePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(catalogue) || JsonNode.Parse(File.ReadAllText(catalogue)) is not JsonObject document || document["entities"] is not JsonArray entities)
        {
            return (true, false); // no catalogue to check against: do not claim a problem
        }

        var complete = true;
        foreach (var item in entities)
        {
            if (item is not JsonObject entity)
            {
                complete = false;
                continue;
            }

            var id = Text(entity["id"]);
            var colon = id.IndexOf(':');
            var name = colon >= 0 ? id[(colon + 1)..] : id;
            if (string.Equals(name, clip, StringComparison.OrdinalIgnoreCase))
            {
                return (true, complete);
            }
        }

        return (false, complete);
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
    /// <summary>
    /// The parameters each executor case reads. The required ones are read with GetProperty,
    /// which THROWS when the field is absent, so an authored action missing one would break the
    /// cutscene mid-play. The optional ones are read with Flag(...) or TryGetProperty(...), and a
    /// field that appears in neither is read by nobody: that is the same silent-ignore trap the
    /// appearance fields had.
    /// </summary>
    public static (IReadOnlyDictionary<string, IReadOnlyList<string>> Required,
                   IReadOnlyDictionary<string, IReadOnlyList<string>> Optional) Parameters(string root)
    {
        var text = Runtime(root);
        var required = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var optional = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (System.Text.RegularExpressions.Match match in CaseBlock.Matches(text))
        {
            var kind = match.Groups[1].Value;
            var body = match.Groups[2].Value;
            required[kind] = Names(RequiredField, body);
            optional[kind] = Names(OptionalField, body);
        }

        return (required, optional);
    }

    /// <summary>Authored actions whose parameters do not match what their executor reads.</summary>
    public static IReadOnlyList<string> ParameterGaps(StudioWorkspace workspace, string root)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (required, optional) = Parameters(root);
        if (required.Count == 0)
        {
            return ["Ни один case исполнителя не прочитан: сверять параметры нечем."];
        }

        var gaps = new List<string>();
        foreach (var id in workspace.EntityIds)
        {
            foreach (var action in Actions(workspace.Get(id)))
            {
                var kind = (string?)action["action"] ?? "";
                if (kind.Length == 0)
                {
                    gaps.Add($"{id}: действие без action.");
                    continue;
                }

                if (!required.TryGetValue(kind, out var requiredNames))
                {
                    // The kind itself is reported by VerifyAgainstExecutor; parameters are moot.
                    continue;
                }

                foreach (var name in requiredNames.Where(name => action[name] is null))
                {
                    gaps.Add($"{id}: действие «{kind}» без поля «{name}» — исполнитель читает его через GetProperty и упадёт.");
                }

                var known = requiredNames.Concat(optional.GetValueOrDefault(kind) ?? []).ToHashSet(StringComparer.Ordinal);
                foreach (var pair in action.Where(pair => pair.Key is not ("action" or "actor")))
                {
                    if (!known.Contains(pair.Key))
                    {
                        gaps.Add($"{id}: поле «{pair.Key}» действия «{kind}» исполнитель не читает ни через GetProperty, ни через Flag/TryGetProperty — оно молча ничего не изменит.");
                    }
                }
            }
        }

        return gaps;
    }

    /// <summary>Every action object inside an entity's action list.</summary>
    private static IEnumerable<JsonObject> Actions(JsonNode? entity)
    {
        if (entity is not JsonObject root)
        {
            yield break;
        }

        var pending = new Stack<JsonNode?>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case JsonObject obj:
                    if (obj["action"] is JsonValue value && value.TryGetValue<string>(out var kind) && kind.Length > 0)
                    {
                        yield return obj;
                        continue;
                    }

                    foreach (var pair in obj)
                    {
                        pending.Push(pair.Value);
                    }

                    break;
                case JsonArray array:
                    foreach (var item in array)
                    {
                        pending.Push(item);
                    }

                    break;
            }
        }
    }

    private static IReadOnlyList<string> Names(System.Text.RegularExpressions.Regex pattern, string body) =>
        pattern.Matches(body).Select(match => match.Groups[1].Value)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static readonly System.Text.RegularExpressions.Regex CaseBlock = new(
        @"case\s+""([a-z]+)""\s*:\s*\n?\s*\{?(.*?)(?=\n\s*case\s+""|\n\s*default:)",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex RequiredField = new(
        @"GetProperty\(""([A-Za-z]+)""\)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex OptionalField = new(
        @"(?:Flag\(|TryGetProperty\()""([A-Za-z]+)""", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string Runtime(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), RuntimePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

}
