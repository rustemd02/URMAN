using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One registered trigger parameter and the runtime code that reads it.</summary>
public sealed record TriggerParam(
    string Path,
    string Kind,
    string DefaultText,
    bool Required,
    string Consumer,
    string? Note = null);

/// <summary>What a trigger really does, in the words an author needs.</summary>
public sealed record TriggerFacts(
    string Id,
    IReadOnlyList<string> Lines,
    IReadOnlyList<string> Problems);

/// <summary>
/// The honest facts about a world trigger (spec AI-21, TRIG02/TRIG05). The Studio
/// panel used to state that an area only reacts to the player, while the runtime
/// reads an <c>actor</c> filter; it also never showed <c>interactionId</c>, which
/// the runtime demands. This projection reads the same parameters as
/// <c>AuthoredWorldDirector</c> and says what will actually happen.
/// </summary>
public static class TriggerScopeFacts
{
    public const string RuntimePath = "game/scripts/AuthoredWorldDirector.cs";

    private static readonly TriggerParam[] Registered =
    [
        new("size", "number[3]", "нет — обязательное", true,
            "AuthoredWorldDirector.BuildTrigger: BoxShape3D, смещение по половине высоты",
            "Без размера область не построится."),
        new("interactionId", "string", "нет — обязательное", true,
            "AuthoredWorldDirector.OnTriggerEntered: DispatchInteractionAsync",
            "Рантайм читает его через GetProperty: без поля директор падает на этом триггере, а не молча пропускает."),
        new("actor", "string", "player", false,
            "AuthoredWorldDirector.OnTriggerEntered: фильтр по группе player_controller",
            "Значение player срабатывает только на игрока; любое другое значение снимает фильтр целиком, поэтому область сработает и на NPC."),
        new("repeat", "string", "once-per-run", false,
            "AuthoredWorldDirector.OnTriggerEntered: FiredThisRun",
            "once-per-run срабатывает один раз за прогон; другое значение оставляет повторные срабатывания, но их всё равно решают условия самого взаимодействия."),
        new("countIfInside", "bool", "false", false,
            "AuthoredWorldDirector.RecheckInside: перепроверка уже стоящих внутри",
            "Учитывает игрока, который уже стоит в области к моменту, когда шаг стал важен."),
        new("states", "array", "нет — необязательное", false,
            "AuthoredWorldDirector.ApplyStates: выбор состояния по ContentRuleEngine",
            "Показывает и скрывает область по состояниям истории.")
    ];

    public static IReadOnlyList<TriggerParam> Params => Registered;

    public static TriggerParam? Find(string path) =>
        Registered.FirstOrDefault(param => string.Equals(param.Path, path, StringComparison.Ordinal));

    /// <summary>
    /// Describes one trigger as the runtime will treat it, and names what is
    /// missing or unsupported instead of repeating a comfortable claim.
    /// </summary>
    public static TriggerFacts Describe(string id, JsonObject? trigger)
    {
        var parameters = trigger?["params"] as JsonObject;
        var lines = new List<string>();
        var problems = new List<string>();
        if (parameters is null)
        {
            return new TriggerFacts(id, ["У триггера нет блока params: рантайм не построит область."], ["Нет блока params."]);
        }

        var actor = Text(parameters, "actor", "player");
        lines.Add(actor == "player"
            ? "Срабатывает только на игрока: рантайм пропускает тела вне группы player_controller."
            : $"Срабатывает на любое тело, а не только на игрока: фильтр снят значением actor = «{actor}».");
        if (actor != "player")
        {
            problems.Add("Панель утверждала «область срабатывает только на игрока», а рантайм с этим actor сработает и на NPC.");
        }

        var repeat = Text(parameters, "repeat", "once-per-run");
        lines.Add(repeat == "once-per-run"
            ? "Один раз за прогон: повторные входы игнорируются."
            : $"Повторные срабатывания разрешены (repeat = «{repeat}»), но каждое всё равно проходит условия взаимодействия.");

        if (parameters["interactionId"] is JsonValue interaction && interaction.TryGetValue<string>(out var interactionId) && interactionId.Length > 0)
        {
            lines.Add($"Запускает взаимодействие «{interactionId}».");
        }
        else
        {
            problems.Add("Нет interactionId: рантайм читает это поле через GetProperty и падает на этом триггере.");
        }

        var inside = parameters["countIfInside"] is JsonValue insideValue && insideValue.TryGetValue<bool>(out var counted) && counted;
        lines.Add(inside
            ? "Учитывает игрока, который уже стоит внутри, когда шаг становится важен."
            : "Уже стоящего внутри игрока не учитывает: срабатывание только на вход.");

        if (parameters["size"] is not JsonArray size || size.Count != 3)
        {
            problems.Add("Размер области не задан тремя числами: рантайм не построит форму триггера.");
        }

        if (parameters["states"] is JsonArray states && states.Count > 0)
        {
            lines.Add($"Область зависит от состояний истории: {states.Count} вариант(ов).");
        }

        foreach (var pair in parameters)
        {
            if (Find(pair.Key) is null)
            {
                problems.Add($"Поле «{pair.Key}» не читает ни один потребитель рантайма: оно не влияет на игру.");
            }
        }

        return new TriggerFacts(id, lines, problems);
    }

    /// <summary>
    /// Checks that every parameter this projection claims is registered is really
    /// read in the runtime source, so the honesty claims cannot drift away from
    /// the code that executes them.
    /// </summary>
    public static IReadOnlyList<string> VerifyAgainstRuntime(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var problems = new List<string>();
        var path = Path.Combine(Path.GetFullPath(root), RuntimePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            problems.Add($"Нет файла {RuntimePath}: связь с потребителем не проверена.");
            return problems;
        }

        var text = File.ReadAllText(path);
        foreach (var param in Registered)
        {
            var read = Regex.IsMatch(text, "Params[^\\n]{0,48}\"" + Regex.Escape(param.Path) + "\"");
            if (!read)
            {
                problems.Add($"Поле «{param.Path}» больше не читается в {RuntimePath}: проекция устарела.");
            }
        }

        return problems;
    }

    private static string Text(JsonObject parameters, string name, string fallback) =>
        parameters[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : fallback;
}
