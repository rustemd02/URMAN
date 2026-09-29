using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Urman.Studio.Core.Storage;

/// <summary>
/// A Markdown content source (documents, old-PC chats and hints): flat
/// front-matter keys whose values are scalars or one-line JSON, then the body.
/// Parsing follows the content compiler's rules exactly, so the entity Studio
/// edits is the entity the compiler reads, with the body as
/// <c>bodyMarkdown</c>. Writing keeps every untouched front-matter line
/// byte for byte and rewrites only the keys whose value changed.
/// </summary>
public static partial class MarkdownSource
{
    public const string BodyKey = "bodyMarkdown";

    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false
    };

    public static bool IsSource(string text) => Normalize(text).StartsWith("---\n", StringComparison.Ordinal);

    public static JsonObject Parse(string text)
    {
        var (lines, body) = Split(text);
        var entity = new JsonObject();
        foreach (var line in lines)
        {
            if (Key(line) is { } key) entity[key.Name] = ParseScalar(key.Raw);
        }

        entity[BodyKey] = body;
        return entity;
    }

    /// <summary>The source text for <paramref name="entity"/>, keeping the untouched lines of <paramref name="original"/>.</summary>
    public static string Write(JsonObject entity, string original)
    {
        var (lines, _) = Split(original);
        var output = new List<string> { "---" };
        var written = new HashSet<string>(StringComparer.Ordinal) { BodyKey };
        foreach (var line in lines)
        {
            if (Key(line) is not { } key)
            {
                output.Add(line); // blank lines and comments stay
                continue;
            }

            if (!entity.ContainsKey(key.Name) || !written.Add(key.Name)) continue; // removed (or a duplicate key)
            var value = entity[key.Name];
            output.Add(JsonNode.DeepEquals(ParseScalar(key.Raw), value) ? line : $"{key.Name}: {Format(value)}");
        }

        foreach (var (name, value) in entity)
        {
            if (written.Add(name)) output.Add($"{name}: {Format(value)}");
        }

        output.Add("---");
        return string.Join('\n', output) + "\n" + ((string?)entity[BodyKey] ?? "");
    }

    private static (string[] Lines, string Body) Split(string text)
    {
        text = Normalize(text);
        if (!text.StartsWith("---\n", StringComparison.Ordinal)) throw new FormatException("Markdown source must start with front matter.");
        var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0) throw new FormatException("Markdown front matter closing delimiter is missing.");
        return (text[4..end].Split('\n'), text[(end + 5)..]);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static (string Name, string Raw)? Key(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) return null;
        var match = FrontMatterLine().Match(line);
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : throw new FormatException($"Unsupported front matter line: {line}");
    }

    // Same rules as the compiler: null/true/false, quoted strings, JSON arrays and objects, numbers; anything else is text.
    private static JsonNode? ParseScalar(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0) return JsonValue.Create(string.Empty);
        if (value is "null" or "true" or "false" ||
            (value.StartsWith('"') && value.EndsWith('"')) ||
            (value.StartsWith('[') && value.EndsWith(']')) ||
            (value.StartsWith('{') && value.EndsWith('}')))
        {
            return JsonNode.Parse(value);
        }

        return ScalarNumber().IsMatch(value) ? JsonNode.Parse(value) : JsonValue.Create(value);
    }

    private static string Format(JsonNode? value)
    {
        if (value is JsonValue text && text.TryGetValue<string>(out var s) && s.Length > 0 && s == s.Trim() && !s.Contains('\n')
            && JsonNode.DeepEquals(ParseScalar(s), value))
        {
            return s; // plain text reads back as itself
        }

        return value?.ToJsonString(Compact) ?? "null";
    }

    [GeneratedRegex("^([A-Za-z][A-Za-z0-9_-]*):\\s*(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex FrontMatterLine();

    [GeneratedRegex("^-?(?:0|[1-9]\\d*)(?:\\.\\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ScalarNumber();
}
