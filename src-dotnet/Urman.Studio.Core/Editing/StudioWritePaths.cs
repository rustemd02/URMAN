using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One place in the Studio that writes a file, and how it is classified.</summary>
public sealed record StudioWriteSite(
    string RelativePath,
    int Line,
    string Kind,
    string Classification,
    string Scope,
    string Note);

/// <summary>
/// The first slice of the quest-editor half of the spec AI-19 slice: the "one model for
/// manual and AI editing" boundary, checked as an inventory of every file write in the
/// Studio sources. The requirement is that authored content is written through the one
/// EditSession journal, so this projection finds every direct write and names what it
/// really touches: editor UI state, AI staging, logs and self-check fixtures are not
/// content, while a write that lands on an authored file outside the session is exactly
/// the second save contour the brief forbids.
///
/// This is an inventory, not a proof of absence: it reads the sources by pattern, so it
/// can miss a write hidden behind an indirection. The guarantee it supports is the one
/// the count of session-mediated writes gives.
/// </summary>
public static class StudioWritePaths
{
    public const string StudioGlob = "game/studio";

    private static readonly Regex WriteCall = new(
        "\\b(File\\.WriteAllText|File\\.WriteAllBytes|AtomicFile\\.WriteAllText|AtomicFile\\.WriteAllBytes|new StreamWriter\\()",
        RegexOptions.CultureInvariant);

    private static readonly Regex SessionCall = new(
        "\\bSession\\.(Set|SetField|ApplyFilesAsync)\\b",
        RegexOptions.CultureInvariant);

    /// <summary>Every direct write in the Studio sources, classified.</summary>
    public static IReadOnlyList<StudioWriteSite> Inspect(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var folder = Path.Combine(Path.GetFullPath(root), StudioGlob.Replace('/', Path.DirectorySeparatorChar));
        var sites = new List<StudioWriteSite>();
        if (!Directory.Exists(folder))
        {
            return sites;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), path).Replace('\\', '/');
            var lines = File.ReadAllLines(path);
            // A file may spell its path constant far from the write that uses it, so the
            // whole file is considered as context, not only the line with the call.
            var declaresEditorState = lines.Any(candidate => candidate.Contains(".urman-studio", StringComparison.Ordinal));
            var line = 0;
            foreach (var text in lines)
            {
                line++;
                if (!WriteCall.IsMatch(text))
                {
                    continue;
                }

                var (kind, classification, scope, note) = Classify(relative, text, declaresEditorState);
                sites.Add(new StudioWriteSite(relative, line, kind, classification, scope, note));
            }
        }

        return sites;
    }

    /// <summary>How many writes go through the one session journal.</summary>
    public static int SessionMediatedWrites(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var folder = Path.Combine(Path.GetFullPath(root), StudioGlob.Replace('/', Path.DirectorySeparatorChar));
        var count = 0;
        if (!Directory.Exists(folder))
        {
            return count;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            count += File.ReadLines(path).Count(line => SessionCall.IsMatch(line));
        }

        return count;
    }

    /// <summary>The writes that are not content and therefore not a second save contour.</summary>
    public static IReadOnlyList<StudioWriteSite> OutsideSession(string root) =>
        Inspect(root).Where(site => site.Classification != "content-through-session").ToArray();

    public static IReadOnlyList<string> Notes(string root)
    {
        var sites = Inspect(root);
        var groups = sites.GroupBy(site => site.Classification, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {group.Count()}");
        return
        [
            $"Прямых записей файлов в Studio: {sites.Count} ({string.Join(", ", groups)}); записей через сессию (Set/SetField/ApplyFilesAsync): {SessionMediatedWrites(root)}.",
            "Авторский контент пишется через один EditSession-журнал: и ручная правка поля, и принятое AI-предложение проходят через него, поэтому Undo/Redo и транзакция общие. Прямые записи вне сессии относятся к состоянию интерфейса (раскладка графа, настройки), к staging AI-запроса, к журналу и к фикстурам самопроверки — это не контент.",
            "Отдельно стоят генератор каталога и импорт ассетов: это явные действия инструмента, а не правка авторской записи, но их границу подтверждает владелец фундамента.",
            "Это инвентарь по исходникам, а не доказательство отсутствия: запись, спрятанная за косвенным вызовом, в него не попадёт."
        ];
    }

    private static (string Kind, string Classification, string Scope, string Note) Classify(string relative, string text, bool declaresEditorState)
    {
        var kind = WriteCall.Match(text).Groups[1].Value;
        if (text.Contains("_stage", StringComparison.Ordinal)
            || text.Contains("request.json", StringComparison.Ordinal)
            || text.Contains("proposal.json", StringComparison.Ordinal)
            || text.Contains("recipe.json", StringComparison.Ordinal))
        {
            return (kind, "ai-staging", "staging запроса", "Артефакты запроса/предложения для ревью, контент не меняют.");
        }

        if (relative.EndsWith("StudioSelfCheck.cs", StringComparison.Ordinal))
        {
            return (kind, "self-check", "фикстуры проверки", "Временные файлы самопроверки.");
        }

        if (text.Contains("LogPath", StringComparison.Ordinal))
        {
            return (kind, "log", "журнал", "Поток журнала запуска.");
        }

        if (relative.EndsWith("StudioCatalogBuilder.cs", StringComparison.Ordinal))
        {
            return (kind, "generator", "генератор каталога", "Явное действие инструмента; границу подтверждает владелец.");
        }

        if (relative.EndsWith("StudioImporter.cs", StringComparison.Ordinal))
        {
            return (kind, "import", "импорт ассета", "Явный импорт байтов в целевой файл.");
        }

        // The generic case comes last: a file that declares a .urman-studio path writes
        // editor state, unless a more specific category above already claimed the call.
        if (declaresEditorState
            || text.Contains("layout.json", StringComparison.Ordinal)
            || text.Contains("preferences", StringComparison.Ordinal)
            || text.Contains("allowGlobalInstructions", StringComparison.Ordinal))
        {
            return (kind, "editor-state", "состояние редактора", "Раскладка/настройки Studio, не авторский контент.");
        }

        return (kind, "needs-review", "не классифицировано", "Запись вне сессии, которую инвентарь не смог отнести к известным категориям.");
    }
}
