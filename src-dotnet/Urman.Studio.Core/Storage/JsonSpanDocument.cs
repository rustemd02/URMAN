using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace Urman.Studio.Core.Storage;

/// <summary>
/// A JSON file edited without reformatting it. The file keeps its original
/// bytes; an edit replaces only the text span of the top-level element or
/// property it touches, written in that span's own style (one-line compact or
/// indented to its depth). Opening and saving without edits is byte-identical
/// and one changed entity is one changed hunk (spec §19.3.4, §20.3).
/// </summary>
public sealed class JsonSpanDocument
{
    private readonly List<Segment> _segments;
    private readonly bool _rootIsArray;
    private readonly string _indentUnit;
    private readonly bool _escapeNonAscii;
    private readonly string _newline;
    private string _prefix;
    private string _suffix;

    private sealed class Segment
    {
        public required string? Key;
        public required string Text;      // exact text of the value (element or property value)
        public required string Lead;      // whitespace + key text before the value, as in the file
        public required JsonNode? Value;
        public bool Changed;
    }

    private JsonSpanDocument(List<Segment> segments, bool rootIsArray, string prefix, string suffix, string indentUnit, bool escapeNonAscii, string newline)
    {
        _segments = segments;
        _rootIsArray = rootIsArray;
        _prefix = prefix;
        _suffix = suffix;
        _indentUnit = indentUnit;
        _escapeNonAscii = escapeNonAscii;
        _newline = newline;
    }

    public bool RootIsArray => _rootIsArray;
    public int Count => _segments.Count;

    public string IndentUnit => _indentUnit;

    public static JsonSpanDocument Parse(string text, string? indentUnit = null)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
        if (!reader.Read() || reader.TokenType is not (JsonTokenType.StartArray or JsonTokenType.StartObject))
        {
            throw new JsonException("Authored JSON files must have an array or object root.");
        }

        var rootIsArray = reader.TokenType == JsonTokenType.StartArray;
        var rootOpen = (int)reader.TokenStartIndex;
        var spans = new List<(string? Key, int LeadStart, int Start, int End)>();
        var cursor = rootOpen + 1;
        string? pendingKey = null;
        var pendingLead = cursor;
        while (reader.Read())
        {
            if (reader.CurrentDepth == 0 && reader.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject)
            {
                var closeIndex = (int)reader.TokenStartIndex;
                var prefix = Encoding.UTF8.GetString(bytes, 0, rootOpen + 1);
                var segments = new List<Segment>();
                var previousEnd = rootOpen + 1;
                foreach (var span in spans)
                {
                    var lead = Encoding.UTF8.GetString(bytes, previousEnd, span.Start - previousEnd);
                    var valueText = Encoding.UTF8.GetString(bytes, span.Start, span.End - span.Start);
                    segments.Add(new Segment
                    {
                        Key = span.Key,
                        Lead = lead,
                        Text = valueText,
                        Value = JsonNode.Parse(valueText)
                    });
                    previousEnd = span.End;
                }

                var suffix = Encoding.UTF8.GetString(bytes, previousEnd, bytes.Length - previousEnd);
                return new(segments, rootIsArray, prefix, suffix, indentUnit ?? DetectIndent(text), text.Contains("\\u04", StringComparison.Ordinal), text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
            }

            if (reader.CurrentDepth == 1 && reader.TokenType == JsonTokenType.PropertyName)
            {
                pendingKey = reader.GetString();
                pendingLead = cursor;
                continue;
            }

            if (reader.CurrentDepth == 1 && reader.TokenType is not (JsonTokenType.EndArray or JsonTokenType.EndObject))
            {
                var start = (int)reader.TokenStartIndex;
                if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                {
                    reader.Skip();
                }

                var end = (int)reader.BytesConsumed;
                spans.Add((pendingKey, pendingLead, start, end));
                pendingKey = null;
                cursor = end;
            }
        }

        throw new JsonException("Unterminated JSON root.");
    }

    public JsonNode? Get(int index) => _segments[index].Value;
    public string? KeyAt(int index) => _segments[index].Key;
    public int IndexOfKey(string key) => _segments.FindIndex(segment => segment.Key == key);

    public int IndexOfId(string id) =>
        _segments.FindIndex(segment => segment.Value is JsonObject entity && entity["id"] is JsonValue value && value.TryGetValue<string>(out var text) && text == id);

    public void Replace(int index, JsonNode? value)
    {
        var segment = _segments[index];
        if (JsonNode.DeepEquals(segment.Value, value))
        {
            return;
        }

        segment.Value = value?.DeepClone();
        segment.Text = Serialize(value, segment.Text, Depth(segment.Lead));
        segment.Changed = true;
    }

    /// <summary>Add an element (array root) or property (object root) after the last one, in the style of its neighbours.</summary>
    public void Add(JsonNode? value, string? key = null)
    {
        if (_rootIsArray != (key is null))
        {
            throw new InvalidOperationException(_rootIsArray ? "Array root takes no key." : "Object root needs a key.");
        }

        var template = _segments.LastOrDefault();
        // The first element of an empty container is indented one level
        // deeper than its closing bracket.
        var closingIndent = _suffix.TrimStart('\r', '\n').TakeWhile(ch => ch == ' ').Count();
        var lead = template is null
            ? _newline + new string(' ', closingIndent) + _indentUnit + (key is null ? "" : $"{Quote(key)}: ")
            : "," + LeadWhitespace(template.Lead) + (key is null ? "" : KeyText(template.Lead, key));
        _segments.Add(new Segment
        {
            Key = key,
            Lead = lead,
            Text = Serialize(value, template?.Text ?? "", Depth(lead)),
            Value = value?.DeepClone(),
            Changed = true
        });
        if (template is null)
        {
            _suffix = _newline + new string(' ', closingIndent) + _suffix.TrimStart();
        }
    }

    /// <summary>Put already formatted text back into a segment (a nested span document that kept its own formatting).</summary>
    public void ReplaceRaw(int index, string text)
    {
        var segment = _segments[index];
        if (segment.Text == text)
        {
            return;
        }

        segment.Text = text;
        segment.Value = JsonNode.Parse(text);
        segment.Changed = true;
    }

    public string RawAt(int index) => _segments[index].Text;

    /// <summary>Put the elements in a new order (a permutation of the current indexes), keeping each element's text.</summary>
    public void Reorder(IReadOnlyList<int> order)
    {
        if (order.Count != _segments.Count || order.Distinct().Count() != order.Count) throw new ArgumentException("Not a permutation.", nameof(order));
        if (order.Select((value, index) => value == index).All(same => same)) return;
        var firstLead = _segments[0].Lead;
        var restLead = _segments.Count > 1 ? _segments[1].Lead : "," + firstLead;
        var reordered = order.Select(index => _segments[index]).ToList();
        for (var index = 0; index < reordered.Count; index++)
        {
            reordered[index].Lead = index == 0 ? firstLead : reordered[index].Lead.StartsWith(',') ? reordered[index].Lead : restLead;
            reordered[index].Changed = true;
        }

        _segments.Clear();
        _segments.AddRange(reordered);
    }

    public void RemoveAt(int index)
    {
        _segments.RemoveAt(index);
        if (index == 0 && _segments.Count > 0)
        {
            // The new first element loses the separator it had after the removed one.
            _segments[0].Lead = _segments[0].Lead.TrimStart(',');
        }
    }

    public string ToText()
    {
        var builder = new StringBuilder(_prefix);
        foreach (var segment in _segments)
        {
            builder.Append(segment.Lead).Append(segment.Text);
        }

        return builder.Append(_suffix).ToString();
    }

    public JsonNode ToNode()
    {
        if (_rootIsArray)
        {
            return new JsonArray(_segments.Select(segment => segment.Value?.DeepClone()).ToArray());
        }

        var result = new JsonObject();
        foreach (var segment in _segments)
        {
            result[segment.Key!] = segment.Value?.DeepClone();
        }

        return result;
    }

    // ---- formatting -----------------------------------------------------------

    private string Serialize(JsonNode? value, string styleSample, int depth)
    {
        var compact = !styleSample.Contains('\n');
        var options = new JsonSerializerOptions
        {
            WriteIndented = !compact,
            IndentCharacter = ' ',
            IndentSize = Math.Max(1, _indentUnit.Length),
            Encoder = _escapeNonAscii ? JavaScriptEncoder.Default : JavaScriptEncoder.Create(UnicodeRanges.All),
            NewLine = _newline
        };
        var text = value?.ToJsonString(options) ?? "null";
        if (compact || depth == 0)
        {
            return text;
        }

        var pad = string.Concat(Enumerable.Repeat(_indentUnit, depth));
        return text.Replace(_newline, _newline + pad, StringComparison.Ordinal);
    }

    private int Depth(string lead)
    {
        var line = lead.Split('\n').Last();
        var spaces = line.TakeWhile(ch => ch == ' ').Count();
        return _indentUnit.Length == 0 ? 0 : spaces / _indentUnit.Length;
    }

    private static string LeadWhitespace(string lead)
    {
        var trimmed = lead.TrimStart(',');
        var keyStart = trimmed.IndexOf('"');
        return keyStart < 0 ? trimmed : trimmed[..keyStart];
    }

    private static string KeyText(string templateLead, string key)
    {
        var colon = templateLead.LastIndexOf(':');
        var separator = colon >= 0 ? templateLead[colon..] : ": ";
        return Quote(key) + separator;
    }

    private static string Quote(string key) => JsonSerializer.Serialize(key, new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });

    private static string DetectIndent(string text)
    {
        foreach (var line in text.Split('\n').Skip(1))
        {
            var spaces = line.TakeWhile(ch => ch == ' ').Count();
            if (spaces > 0)
            {
                return new string(' ', spaces);
            }
        }

        return "  ";
    }
}
