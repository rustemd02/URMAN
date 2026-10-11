using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One material family table of the painterly library, and how many families it holds.</summary>
public sealed record MaterialModeTable(string Table, int Families, string Role);

/// <summary>
/// The warning half of the spec AI-36 slice. The painterly library decides a family's mode
/// from four tables (SurfaceTextures, WinterTextures, TunedWithoutMap, FlatByDesign) and warns
/// when a surface has "no declared mode". One of those tables is missing from the condition,
/// so families the library DOES handle are reported as undeclared - log noise that hides real
/// unknowns. This projection reads the tables from the library source and reports the exact
/// gap and the exact families it affects, so the fix is a one-line change in the owner's file
/// rather than a guess.
///
/// The real check runs alongside it: every family the code asks for has to appear in one of the
/// tables, and that number is zero today.
/// </summary>
public static class MaterialModes
{
    public const string LibraryPath = "game/scripts/PainterlyMaterialLibrary.cs";

    public static IReadOnlyList<MaterialModeTable> Tables(string root)
    {
        var source = Source(root);
        return
        [
            new("SurfaceTextures", Dict(source, "SurfaceTextures = new").Count, "семейства с текстурой"),
            new("WinterTextures", Dict(source, "WinterTextures = new").Count, "зимние семейства с текстурой"),
            new("TunedWithoutMap", Strings(source, "TunedWithoutMap = new").Count, "настроены без текстуры"),
            new("FlatByDesign", Strings(source, "FlatByDesign = new").Count, "плоские по замыслу")
        ];
    }

    /// <summary>Families the library handles through WinterTextures but the warning still reports.</summary>
    public static IReadOnlyList<string> SpuriousWarningFamilies(string root)
    {
        var source = Source(root);
        var surface = Dict(source, "SurfaceTextures = new");
        var winter = Dict(source, "WinterTextures = new");
        var tuned = Strings(source, "TunedWithoutMap = new");
        var flat = Strings(source, "FlatByDesign = new");
        // The condition the library really uses, read from its own source.
        var conditionNames = Regex.Matches(
            Condition(source),
            @"!([A-Za-z]+)\.Contains")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        if (!conditionNames.Contains("WinterTextures"))
        {
            return winter.Where(family => !surface.Contains(family) && !tuned.Contains(family) && !flat.Contains(family))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return [];
    }

    /// <summary>The exact text of the warning condition, so the gap is quoted rather than described.</summary>
    public static string Condition(string root)
    {
        var source = Source(root);
        var start = source.IndexOf("if (surface.Length > 0", StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var end = source.IndexOf("PushWarning", start, StringComparison.Ordinal);
        return end < 0 ? source[start..] : source[start..end];
    }

    /// <summary>Families the project asks for that no table declares.</summary>
    public static IReadOnlyList<string> UndeclaredUsed(string root)
    {
        var full = Path.GetFullPath(root);
        var tables = Tables(root);
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var source = Source(root);
        declared.UnionWith(Dict(source, "SurfaceTextures = new"));
        declared.UnionWith(Dict(source, "WinterTextures = new"));
        declared.UnionWith(Strings(source, "TunedWithoutMap = new"));
        declared.UnionWith(Strings(source, "FlatByDesign = new"));
        if (tables.Count == 0 || declared.Count == 0)
        {
            return ["Таблицы семейств не прочитаны: сверять нечем."];
        }

        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var folder in new[] { Path.Combine("game", "scripts"), "src-dotnet" })
        {
            var path = Path.Combine(full, folder);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (Match match in UsedFamily.Matches(text))
                {
                    used.Add(match.Groups[1].Value);
                }
            }
        }

        return used.Where(family => !declared.Contains(family)).ToArray();
    }

    public static IReadOnlyList<string> Notes(string root)
    {
        var tables = Tables(root);
        var spurious = SpuriousWarningFamilies(root);
        var undeclared = UndeclaredUsed(root);
        return
        [
            "Таблицы семейств: " + string.Join("; ", tables.Select(table => $"{table.Table} — {table.Families} ({table.Role})")) + ".",
            spurious.Count == 0
                ? "Условие предупреждения учитывает все таблицы."
                : $"Условие предупреждения НЕ проверяет WinterTextures, поэтому {spurious.Count} семейств, которые библиотека обслуживает, попадают в лог как «нет объявленного режима»: {string.Join(", ", spurious)}. Починка — одна строка в файле владельца: добавить && !WinterTextures.ContainsKey(surface) в условие.",
            undeclared.Count == 0
                ? "Ни одно семейство, которое просит код, не осталось без объявления."
                : $"Код просит необъявленные семейства: {string.Join(", ", undeclared)} — это уже настоящий промах, а не шум.",
            "Здесь проверяются имена режимов и условие предупреждения; вид материала остаётся художественной приёмкой."
        ];
    }

    private static readonly Regex UsedFamily = new(
        "ForColor\\([^,]+,\\s*\"([a-zA-Z0-9_]+)\"",
        RegexOptions.CultureInvariant);

    private static string Source(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), LibraryPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static string Block(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var end = source.IndexOf("\n    };", start, StringComparison.Ordinal);
        return source[start..(end < 0 ? source.Length : end)];
    }

    private static HashSet<string> Dict(string source, string marker) =>
        Block(source, marker).Length == 0
            ? []
            : Regex.Matches(Block(source, marker), "\\[\"([a-zA-Z0-9_]+)\"\\]\\s*=")
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> Strings(string source, string marker) =>
        Block(source, marker).Length == 0
            ? []
            : Regex.Matches(Block(source, marker), "\"([a-zA-Z0-9_]+)\"")
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);
}
