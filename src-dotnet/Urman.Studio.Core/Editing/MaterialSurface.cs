using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>
/// An authored colour field in the content. These belong to the ATMOSPHERE records, not to
/// materials: there is no material colour in the content at all, and this projection must
/// not imply otherwise.
/// </summary>
public sealed record AtmosphereColourField(string Path, string Value, IReadOnlyList<string> Problems);

/// <summary>
/// The first real slice of the local-versus-global material half of the spec AI-36 slice.
/// The runtime does not read a material from the content: PainterlyMaterialLibrary builds
/// a material from a (surface family, colour, sheltered) triple and CACHES it, so two
/// objects that ask for the same triple literally share one material. That is what makes a
/// change local or global:
///
///   * local  - the colour an object asks for, which lives in the authored data;
///   * global - the family's library entry (its texture, response, finish), which every
///              object using that family and colour shares.
///
/// A family the library does not declare is not an error: ForColor warns once and falls
/// back to the flat world-space default, which is exactly the silent visual change an
/// author must be warned about. There is no authored per-object material field anywhere in
/// the content, so this projection says what exists instead of inventing one.
/// </summary>
public static class MaterialSurface
{
    public const string LibraryPath = "game/scripts/PainterlyMaterialLibrary.cs";

    /// <summary>Every set that gives a family a mode: the winter maps are their own set.</summary>
    private static readonly string[] DeclaredSets = ["SurfaceTextures", "TunedWithoutMap", "FlatByDesign", "WinterTextures"];

    /// <summary>
    /// The three sets ForColor itself consults before it warns. WinterTextures is NOT among
    /// them, so a winter family is handled by the builder and still warned about once.
    /// </summary>
    private static readonly string[] WarningSets = ["SurfaceTextures", "TunedWithoutMap", "FlatByDesign"];

    private static readonly Regex FamilyKey = new("\"([a-z][a-z0-9_]*)\"", RegexOptions.CultureInvariant);

    private static readonly Regex ForColorFamily = new(
        "ForColor\\s*\\([^,)]*,\\s*\"([a-z][a-z0-9_]*)\"",
        RegexOptions.CultureInvariant);

    private static readonly Regex HexColour = new("^[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);

    /// <summary>The surface families the library declares a mode for.</summary>
    public static IReadOnlySet<string> DeclaredFamilies(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var path = Path.Combine(Path.GetFullPath(root), LibraryPath.Replace('/', Path.DirectorySeparatorChar));
        var families = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return families;
        }

        var text = File.ReadAllText(path);
        foreach (var name in DeclaredSets)
        {
            var start = text.IndexOf(name + " = new", StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            var end = text.IndexOf("};", start, StringComparison.Ordinal);
            foreach (Match match in FamilyKey.Matches(text[start..(end < 0 ? text.Length : end)]))
            {
                families.Add(match.Groups[1].Value);
            }
        }

        return families;
    }

    /// <summary>The families ForColor treats as undeclared, i.e. the ones it warns about.</summary>
    public static IReadOnlySet<string> WarnedFamilies(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var path = Path.Combine(Path.GetFullPath(root), LibraryPath.Replace('/', Path.DirectorySeparatorChar));
        var families = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return families;
        }

        var text = File.ReadAllText(path);
        foreach (var name in WarningSets)
        {
            var start = text.IndexOf(name + " = new", StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            var end = text.IndexOf("};", start, StringComparison.Ordinal);
            foreach (Match match in FamilyKey.Matches(text[start..(end < 0 ? text.Length : end)]))
            {
                families.Add(match.Groups[1].Value);
            }
        }

        return families;
    }

    /// <summary>The families the runtime asks for when it builds a material.</summary>
    public static IReadOnlySet<string> UsedFamilies(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var folder = Path.Combine(Path.GetFullPath(root), "game", "scripts");
        var families = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(folder))
        {
            return families;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in ForColorFamily.Matches(File.ReadAllText(path)))
            {
                families.Add(match.Groups[1].Value);
            }
        }

        return families;
    }

    /// <summary>
    /// The atmosphere colour fields of the content. They are NOT material parameters: they
    /// feed the atmosphere records, and the material of an object is decided by the runtime
    /// from a (family, colour, sheltered) triple whose colour comes from code or from these
    /// atmosphere values, never from a material field in the content.
    /// </summary>
    public static IReadOnlyList<AtmosphereColourField> AtmosphereColours(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var colours = new List<AtmosphereColourField>();
        foreach (var file in workspace.Files)
        {
            var node = file.Snapshot();
            Collect(node, file.RelativePath, "", colours);
        }

        return colours.OrderBy(colour => colour.Path, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Notes(StudioWorkspace workspace, string root)
    {
        var declared = DeclaredFamilies(root);
        var used = UsedFamilies(root);
        var warned = WarnedFamilies(root);
        var undeclared = used.Where(family => !declared.Contains(family)).Order(StringComparer.Ordinal).ToArray();
        var spurious = used.Where(family => !warned.Contains(family) && declared.Contains(family)).Order(StringComparer.Ordinal).ToArray();
        var colours = AtmosphereColours(workspace);
        return
        [
            $"Семейств поверхностей объявлено: {declared.Count}; запрошено в коде: {used.Count}; цветовых полей в контенте: {colours.Count} — и все они принадлежат ЗАПИСЯМ АТМОСФЕРЫ, а не материалам.",
            undeclared.Length == 0
                ? "Все запрошенные семейства объявлены в библиотеке: каждый запрос получит свой режим, а не плоскую заглушку."
                : $"Запрошены, но не объявлены: {string.Join(", ", undeclared)} — такие запросы получают плоский мировой материал и однократное предупреждение, то есть молчаливую смену вида.",
            spurious.Length == 0
                ? "Предупреждение о «необъявленном режиме» ни для одного семейства не срабатывает зря."
                : $"Зимние семейства {string.Join(", ", spurious)} объявлены в WinterTextures и обрабатываются сборщиком, но условие предупреждения в ForColor смотрит только SurfaceTextures/TunedWithoutMap/FlatByDesign — то есть по этим семействам предупреждение выдаётся зря.",
            "Материал строится из тройки (семейство, цвет, укрытие) и КЭШИРУЕТСЯ: два объекта с одной тройкой делят один материал. Поэтому локальное изменение — это цвет, который просит объект (он в данных), а общее — запись семейства в библиотеке (её текстура, отклик, отделка), которую делят все объекты этого семейства и цвета.",
            "Отдельного авторского поля материала в контенте НЕТ, и цветовых полей МАТЕРИАЛА тоже нет: тридцать пять цветов выше — это поля атмосферы. Материал объекта рантайм собирает из тройки (семейство, цвет, укрытие), где цвет приходит из кода или из атмосферных значений, поэтому правка вида — это библиотека/модель, то есть дорожка художника (AI-09/AI-11/AI-36). Проекция не выдумывает поле, которого игра не читает, и не называет атмосферные цвета материалом."
        ];
    }

    private static void Collect(System.Text.Json.Nodes.JsonNode? node, string path, string prefix, List<AtmosphereColourField> colours)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                foreach (var pair in obj)
                {
                    if (pair.Key == "color" && pair.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        var problems = new List<string>();
                        if (!HexColour.IsMatch(text))
                        {
                            problems.Add($"Цвет «{text}» не из 6 hex-цифр: библиотека разберёт его как непрозрачный чёрный.");
                        }

                        colours.Add(new AtmosphereColourField($"{path}:{prefix}{pair.Key}", text, problems));
                        continue;
                    }

                    Collect(pair.Value, path, $"{prefix}{pair.Key}.", colours);
                }

                break;
            case System.Text.Json.Nodes.JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    Collect(array[index], path, $"{prefix}[{index}].", colours);
                }

                break;
        }
    }
}
