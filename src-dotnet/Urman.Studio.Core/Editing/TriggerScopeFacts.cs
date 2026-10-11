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
    string? Note = null)
{
    /// <summary>
    /// Who owns the rule about this field: "runtime" for what the game itself reads and
    /// enforces, "editor" for a Studio policy the game does not check, "runtime+editor"
    /// for both. The two must not be confused: a value the editor refuses can still be
    /// perfectly playable, and a value the runtime rejects always is not.
    /// </summary>
    public string Policy { get; init; } = "runtime";
}

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
            "Без поля ReadVector вернёт ноль и область получится нулевой: срабатывание практически невозможно. Меньше трёх чисел или значение вне float — исключение при чтении; лишние числа рантайм игнорирует. Минимум 0,2 м и требование ровно трёх чисел — политика редактора, игра их не проверяет.")
        {
            Policy = "runtime+editor"
        },
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
        else if (parameters["size"] is not JsonArray size || size.Count < 3)
        {
            // ToVector3 reads [0], [1] and [2]: fewer than three values really does throw.
            problems.Add("size: рантайм читает [0], [1] и [2] — при нехватке чисел чтение области упадёт.");
        }
        else
        {
            if (size.Count > 3)
            {
                // The runtime ignores anything past the third value, so "exactly three" is
                // an editor rule, not an engine rule, and is reported as such.
                lines.Add("size: рантайм читает только первые три числа и игнорирует остальные; требование ровно трёх — политика редактора.");
            }

            // The precise reason matters: "not three numbers", "not a number" and "does
            // not fit a float" send the author to three different fixes.
            string? unreadable = null;
            foreach (var axis in size)
            {
                unreadable ??= NumberProblem(axis, out _);
            }

            if (unreadable is not null)
            {
                problems.Add($"size: {unreadable} — область не прочитается.");
            }
            else if (size.Any(axis => TryNumber(axis, out var side) && side < EditorMinimumSize))
            {
                lines.Add($"Область меньше {EditorMinimumSize:0.#} м — это политика редактора, а не запрет игры: рантайм примет и меньший размер.");
            }
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

    /// <summary>The smallest area the editor offers; the game itself accepts less.</summary>
    public const double EditorMinimumSize = .2d;

    /// <summary>The authored string, null when the key is absent, and false when it is not a string.</summary>
    private static string? ReadString(JsonObject parameters, string name, out bool isText)
    {
        isText = true;
        if (!parameters.TryGetPropertyValue(name, out var node) || node is null)
        {
            // An explicit null behaves like an absent key in the runtime helper
            // (GetString() returns null and the fallback wins), so it is not a type error.
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
        if (!parameters.TryGetPropertyValue(name, out var node) || node is null)
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
    /// A string field read without a cast: a wrong type returns the fallback instead of
    /// throwing inside the inspector, and the facts report the type separately.
    /// </summary>
    public static string ReadText(JsonObject? parameters, string name, string fallback) =>
        parameters is not null ? ReadString(parameters, name, out _) ?? fallback : fallback;

    /// <summary>A bool field read without a cast; a wrong type returns the fallback.</summary>
    public static bool ReadFlag(JsonObject? parameters, string name, bool fallback) =>
        parameters is not null && ReadBool(parameters, name, out _) is { } flag ? flag : fallback;

    /// <summary>
    /// Reads any JSON number as a finite double. Public because a panel that shows
    /// a size or a level must not cast a JsonValue blindly: an integer value is not
    /// a double, and a string value would throw inside the inspector.
    /// </summary>
    public static bool TryNumber(JsonNode? node, out double value) =>
        NumberProblem(node, out value) is null;

    /// <summary>
    /// Why a value cannot be used as a number, or null when it can. The runtime reads
    /// the field through GetSingle, so a finite double beyond the float range is an
    /// overflow there, not a large number: it must be refused, not silently kept.
    /// </summary>
    public static string? NumberProblem(JsonNode? node, out double value)
    {
        value = 0d;
        if (node is not JsonValue json)
        {
            return node is null ? "значение пустое" : "значение не число";
        }

        if (json.TryGetValue<double>(out var number))
        {
            value = number;
            if (!double.IsFinite(value))
            {
                return "значение не конечное число";
            }

            return Math.Abs(value) > float.MaxValue ? "значение не влезает в float, а рантайм читает его как float" : null;
        }

        if (json.TryGetValue<float>(out var single))
        {
            value = single;
            return float.IsFinite(single) ? null : "значение не конечное число";
        }

        if (json.TryGetValue<int>(out var small))
        {
            value = small;
            return null;
        }

        if (json.TryGetValue<long>(out var integer))
        {
            value = integer;
            return Math.Abs(value) > float.MaxValue ? "значение не влезает в float, а рантайм читает его как float" : null;
        }

        if (json.TryGetValue<decimal>(out var precise))
        {
            value = (double)precise;
            return Math.Abs(value) > float.MaxValue ? "значение не влезает в float, а рантайм читает его как float" : null;
        }

        return "значение не число";
    }

    private static bool Finite(JsonNode? node) => TryNumber(node, out _);
    /// <summary>
    /// The authored trigger contract, mirrored from BuildTrigger and OnTriggerEntered:
    ///
    ///   * size is read with ReadVector, which falls back to Vector3.Zero when the field is
    ///     absent, so a trigger without a size builds a degenerate area that NEVER fires - the
    ///     quietest possible defect;
    ///   * interactionId is read with GetProperty inside OnTriggerEntered, so a missing one
    ///     throws when the player walks in rather than while the world loads;
    ///   * actor and repeat are optional with the defaults "player" and "once-per-run".
    ///
    /// The campaign authors no trigger today, so this is the contract waiting for the first one,
    /// and the notes say that out loud instead of implying triggers work.
    /// </summary>
    public static IReadOnlyList<string> ContractGaps(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var interactions = ItemSpawns.AuthoredInteractions(workspace);
        var gaps = new List<string>();
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not JsonObject entity || AuthoredJson.Text(entity, "kind") != "trigger")
            {
                continue;
            }

            var parameters = entity["params"] as JsonObject;
            if (parameters?["size"] is not JsonArray { Count: 3 } size)
            {
                gaps.Add($"{id}: поле «size» отсутствует или не из трёх чисел — BuildTrigger прочитает его через ReadVector и получит Vector3.Zero, то есть вырожденную область, которая НИКОГДА не сработает.");
            }
            else
            {
                for (var index = 0; index < size.Count; index++)
                {
                    if (TriggerScopeFacts.NumberProblem(size[index], out var problem) is { } sizeProblem)
                    {
                        gaps.Add($"{id}: размер[{index}]: {sizeProblem}");
                    }
                }

                if (size.All(value => TryNumber(value, out var number) && number <= 0))
                {
                    gaps.Add($"{id}: у области все три размера нулевые или отрицательные — такая область не сработает.");
                }
            }

            var interaction = AuthoredJson.Text(parameters, "interactionId");
            if (interaction.Length == 0)
            {
                gaps.Add($"{id}: у триггера нет interactionId — OnTriggerEntered читает его через GetProperty и упадёт, КОГДА игрок войдёт, а не при загрузке мира.");
            }
            else if (interactions.Count > 0 && !interactions.ContainsKey(interaction))
            {
                gaps.Add($"{id}: взаимодействия «{interaction}» нет среди авторских — триггеру нечего запустить.");
            }

            foreach (var field in new[] { "actor", "repeat" })
            {
                if (parameters?[field] is { } value && (value is not JsonValue text || !text.TryGetValue<string>(out _)))
                {
                    gaps.Add($"{id}: поле «{field}» не строка — рантайм читает его через Text/GetString.");
                }
            }
        }

        return gaps;
    }

    /// <summary>Whether the campaign authors any trigger at all.</summary>
    public static bool AnyAuthored(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.EntityIds.Any(id => AuthoredJson.Text(workspace.Get(id), "kind") == "trigger");
    }

}
