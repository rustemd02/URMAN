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

    public static CutsceneActionType? Find(string action) =>
        Registered.FirstOrDefault(type => string.Equals(type.Action, action, StringComparison.Ordinal));

    /// <summary>The text id the runtime builds from a spoken suffix.</summary>
    public static string TextIdFor(string suffix) => TextPrefix + suffix;

    /// <summary>
    /// Checks one action against the executor and the real project: what the game
    /// will do, and what it will silently skip.
    /// </summary>
    public static CutsceneActionFacts Describe(StudioWorkspace workspace, JsonObject? action, IReadOnlyCollection<string> declaredActors)
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
            if (Text(parameters[actorField]) is { Length: > 0 } actor && !declaredActors.Contains(actor, StringComparer.Ordinal))
            {
                problems.Add($"В поле «{actorField}» указан «{actor}», которого нет среди объявленных персонажей сцены: словарь actors бросит исключение.");
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
            else if (!KnownAnimation(workspace, clip))
            {
                problems.Add($"Клипа «{clip}» нет в каталоге анимаций: имя собирается как префикс персонажа плюс суффикс, и чужого клипа у него нет — персонаж молча останется без анимации.");
            }
            else
            {
                summary.Add($"Клип «{clip}» есть в каталоге анимаций.");
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
        var actions = new List<CutsceneActionFacts>();
        var problems = new List<string>();
        var notes = new List<string>
        {
            "Сцена и её исполнитель зашиты на один тайминговый файл и namespace urman.chapter1: выбор другой кампании или катсцены не поддержан (это граница AI-34, а не готовность)."
        };
        if (cutscene?["entities"] is not JsonArray entities)
        {
            return (actions, ["У катсцены нет массива entities."], notes);
        }

        foreach (var entity in entities.OfType<JsonObject>())
        {
            var facts = Describe(workspace, entity, actors);
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

    private static bool KnownAnimation(StudioWorkspace workspace, string clip)
    {
        var catalogue = Path.Combine(workspace.Root, AnimationCataloguePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(catalogue) || JsonNode.Parse(File.ReadAllText(catalogue)) is not JsonObject document || document["entities"] is not JsonArray entities)
        {
            return true; // no catalogue to check against: do not claim a problem
        }

        foreach (var entity in entities.OfType<JsonObject>())
        {
            var id = Text(entity["id"]);
            var colon = id.IndexOf(':');
            var name = colon >= 0 ? id[(colon + 1)..] : id;
            if (string.Equals(name, clip, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
}
