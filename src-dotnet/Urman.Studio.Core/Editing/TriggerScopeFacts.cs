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
/// the runtime demands. Everything here is copied from
/// <c>AuthoredWorldDirector</c>: an absent key takes the default, while an
/// explicitly empty string is kept (so <c>actor: ""</c> removes the filter), a
/// non-string value throws at the first qualifying entry, and a missing size
/// builds a zero-sized area rather than refusing.
/// </summary>
public static class TriggerScopeFacts
{
    public const string RuntimePath = "game/scripts/AuthoredWorldDirector.cs";

    private static readonly TriggerParam[] Registered =
    [
        new("size", "number[3]", "нет — рантайм возьмёт нулевой размер", true,
            "AuthoredWorldDirector.BuildTrigger: BoxShape3D, смещение по половине высоты",
            "Без поля ReadVector вернёт ноль и область получится нулевой: срабатывание практически невозможно. Не число или не три элемента — исключение при чтении."),
        new("interactionId", "string", "нет — обязательное для срабатывания", true,
            "AuthoredWorldDirector.OnTriggerEntered: DispatchInteractionAsync",
            "Это цель попытки запуска, а не гарантия: сработает ли она, решают физические и скомпилированные условия взаимодействия. Поле читается через GetProperty, поэтому без него рантайм падает на первом подходящем входе, а не при построении мира."),
        new("actor", "string", "player", false,
            "AuthoredWorldDirector.OnTriggerEntered: фильтр по группе player_controller",
            "player — только игрок; любое другое значение, включая пустую строку, снимает фильтр целиком и область сработает и на NPC. Не строка — исключение при входе."),
        new("repeat", "string", "once-per-run", false,
            "AuthoredWorldDirector.OnTriggerEntered: FiredThisRun",
            "once-per-run запрещает повторный вход только ПОСЛЕ успешного запуска: если взаимодействие не сработало, вход можно повторить. Любое другое значение оставляет повторные срабатывания, но их всё равно решают условия самого взаимодействия."),
        new("countIfInside", "bool", "false", false,
            "AuthoredWorldDirector.RecheckInside: перепроверка уже стоящих внутри",
            "Учитывает игрока, который уже стоит в области к моменту, когда шаг стал важен. Не bool — исключение при перепроверке."),
        new("states", "array", "нет — необязательное", false,
            "AuthoredWorldDirector.ApplyStates: выбор состояния по ContentRuleEngine",
            "Показывает и скрывает область по состояниям истории. Не массив — исключение при применении состояний.")
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

        // actor: absent keeps the default, an explicit empty string removes the filter.
        var actor = ReadString(parameters, "actor", out var actorIsText);
        if (!actorIsText)
        {
            problems.Add("actor не строка: рантайм читает его через GetString и упадёт на первом входе.");
        }
        else if (actor is null)
        {
            lines.Add("actor не задан: рантайм считает player, срабатывает только на игрока.");
        }
        else if (actor == "player")
        {
            lines.Add("Срабатывает только на игрока: рантайм пропускает тела вне группы player_controller.");
        }
        else
        {
            lines.Add(actor.Length == 0
                ? "Пустое значение actor снимает фильтр: срабатывает на любое тело, включая NPC."
                : $"Срабатывает на любое тело, а не только на игрока: фильтр снят значением actor = «{actor}».");
            problems.Add("Панель утверждала «область срабатывает только на игрока», а рантайм с таким actor сработает и на NPC.");
        }

        // repeat: the guard applies after a successful dispatch only.
        var repeat = ReadString(parameters, "repeat", out var repeatIsText);
        if (!repeatIsText)
        {
            problems.Add("repeat не строка: рантайм читает его через GetString и упадёт на первом входе.");
        }
        else if (repeat is null || repeat == "once-per-run")
        {
            lines.Add(repeat is null
                ? "repeat не задан: рантайм считает once-per-run — повторный вход после успешного запуска игнорируется."
                : "Один раз за прогон, но только после УСПЕШНОГО запуска: если взаимодействие не сработало, вход можно повторить.");
        }
        else
        {
            lines.Add($"Повторные срабатывания разрешены (repeat = «{repeat}»), но каждое всё равно проходит условия взаимодействия.");
        }

        if (!parameters.ContainsKey("interactionId"))
        {
            problems.Add("Нет interactionId: рантайм читает это поле через GetProperty и падает, когда игрок впервые войдёт и фильтр пропустит его.");
        }
        else if (ReadString(parameters, "interactionId", out var targetIsText) is { Length: > 0 } target)
        {
            lines.Add($"Цель попытки запуска: «{target}»; сработает ли она, решают физические и скомпилированные условия взаимодействия.");
        }
        else
        {
            problems.Add(targetIsText
                ? "interactionId пустой: попытка запуска уйдёт с пустой целью."
                : "interactionId не строка: рантайм упадёт при чтении.");
        }

        // size: a missing field is a zero-sized area, not a refusal.
        if (!parameters.ContainsKey("size"))
        {
            problems.Add("Нет size: ReadVector вернёт ноль, область получится нулевого размера и срабатывание станет практически невозможным.");
        }
        else if (parameters["size"] is not JsonArray size || size.Count != 3 || !size.All(Finite))
        {
            problems.Add("size не из трёх конечных чисел: чтение области упадёт или даст вырожденный размер.");
        }

        var inside = ReadBool(parameters, "countIfInside", out var insideIsBool);
        if (!insideIsBool)
        {
            problems.Add("countIfInside не true/false: рантайм упадёт при перепроверке стоящих внутри.");
        }
        else if (inside == true)
        {
            lines.Add("Учитывает игрока, который уже стоит внутри, когда шаг становится важен.");
        }
        else
        {
            lines.Add("Уже стоящего внутри игрока не учитывает: срабатывание только на вход.");
        }

        if (parameters.ContainsKey("states") && parameters["states"] is not JsonArray)
        {
            problems.Add("states не массив: рантайм упадёт при применении состояний.");
        }
        else if (parameters["states"] is JsonArray states && states.Count > 0)
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
    /// Source-presence check only: it confirms that the runtime file still mentions
    /// the parameters this projection describes. It is not a runtime proof of the
    /// behaviour, and it is worded that way on purpose.
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
            var mentioned = Regex.IsMatch(text, "Params[^\\n]{0,48}\"" + Regex.Escape(param.Path) + "\"");
            if (!mentioned)
            {
                problems.Add($"Поле «{param.Path}» больше не упоминается рядом с Params в {RuntimePath}: это проверка присутствия в исходнике, и она просит перечитать проекцию.");
            }
        }

        return problems;
    }

    /// <summary>The authored string, null when the key is absent, and false when it is not a string.</summary>
    private static string? ReadString(JsonObject parameters, string name, out bool isText)
    {
        isText = true;
        if (!parameters.TryGetPropertyValue(name, out var node))
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        isText = false;
        return null;
    }

    private static bool? ReadBool(JsonObject parameters, string name, out bool isBool)
    {
        isBool = true;
        if (!parameters.TryGetPropertyValue(name, out var node))
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        isBool = false;
        return null;
    }

    /// <summary>
    /// Reads any JSON number as a finite double. Public because a panel that shows
    /// a size or a level must not cast a JsonValue blindly: an integer value is not
    /// a double, and a string value would throw inside the inspector.
    /// </summary>
    public static bool TryNumber(JsonNode? node, out double value)
    {
        value = 0d;
        if (node is not JsonValue json)
        {
            return false;
        }

        if (json.TryGetValue<double>(out var number))
        {
            value = number;
            return double.IsFinite(value);
        }

        if (json.TryGetValue<float>(out var single))
        {
            value = single;
            return float.IsFinite(single);
        }

        if (json.TryGetValue<int>(out var small))
        {
            value = small;
            return true;
        }

        if (json.TryGetValue<long>(out var integer))
        {
            value = integer;
            return true;
        }

        if (json.TryGetValue<decimal>(out var precise))
        {
            value = (double)precise;
            return true;
        }

        return false;
    }

    private static bool Finite(JsonNode? node) => TryNumber(node, out _);
}
