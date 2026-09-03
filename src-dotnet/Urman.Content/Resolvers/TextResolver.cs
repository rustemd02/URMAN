using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urman.Content.Resolvers;

public sealed class TextResolver
{
    private static readonly Regex TokenPattern = new("\\{\\{([A-Za-z_][A-Za-z0-9_.-]*)\\}\\}", RegexOptions.Compiled);
    private readonly ResolverCatalog _catalog;

    public TextResolver(JsonObject pack, bool validate = true)
    {
        _catalog = ResolverCatalog.Create(pack);
        if (validate)
        {
            _ = ManifestClosureValidator.Validate(pack);
        }
    }

    public IReadOnlyList<string> Ids() => _catalog.Ids("texts");

    public ResolvedText Resolve(
        string textRef,
        string? locale = null,
        IReadOnlyDictionary<string, object?>? variables = null)
    {
        var entry = _catalog.Require("texts", textRef, ResolverErrors.AbsentOrigin("texts", textRef));
        var localized = ContentResolverSupport.LocalizedValue(entry.Value["value"], locale, entry.Origin);
        var normalized = NormalizeVariables(variables, entry.Origin);
        var segments = new List<ResolvedTextSegment>();
        var position = 0;
        foreach (Match match in TokenPattern.Matches(localized.Text))
        {
            if (match.Index > position)
            {
                segments.Add(new PlainTextSegment(localized.Text[position..match.Index]));
            }

            var name = match.Groups[1].Value;
            if (!normalized.TryGetValue(name, out var value))
            {
                throw ResolverErrors.Create(
                    ResolverErrorCode.MissingVariable,
                    $"Missing text variable {name}.",
                    entry.Origin,
                    new Dictionary<string, object?> { ["variable"] = name });
            }

            if (value is string text)
            {
                segments.Add(new PlainTextSegment(text));
            }
            else
            {
                var token = (VocabularyToken)value;
                var vocabulary = _catalog.Require("vocabulary", token.VocabularyId, entry.Origin);
                var meaning = ContentResolverSupport.LocalizedValue(vocabulary.Value["meaning"], locale, vocabulary.Origin);
                segments.Add(new VocabularyTextSegment(
                    token.VocabularyId,
                    ResolverJson.RequiredString(vocabulary.Value, "language", vocabulary.Origin),
                    ResolverJson.RequiredString(vocabulary.Value, "term", vocabulary.Origin),
                    meaning.Text,
                    meaning.ResolvedLocale));
            }

            position = match.Index + match.Length;
        }

        if (position < localized.Text.Length || segments.Count == 0)
        {
            segments.Add(new PlainTextSegment(localized.Text[position..]));
        }

        var readOnlySegments = segments.AsReadOnly();
        return new ResolvedText(
            textRef,
            ResolverJson.RequiredString(entry.Value, "purpose", entry.Origin),
            locale ?? "default",
            localized.ResolvedLocale,
            string.Concat(readOnlySegments.Select(segment => segment switch
            {
                PlainTextSegment plain => plain.Value,
                VocabularyTextSegment vocabulary => vocabulary.Term,
                _ => string.Empty
            })),
            readOnlySegments);
    }

    private static IReadOnlyDictionary<string, object> NormalizeVariables(
        IReadOnlyDictionary<string, object?>? variables,
        ResolverOrigin origin)
    {
        var normalized = new Dictionary<string, object>(StringComparer.Ordinal);
        if (variables is null)
        {
            return new ReadOnlyDictionary<string, object>(normalized);
        }

        foreach (var (key, value) in variables)
        {
            normalized[key] = value switch
            {
                string text => text,
                bool boolean => boolean ? "true" : "false",
                byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                    Convert.ToString(value, CultureInfo.InvariantCulture)!,
                VocabularyToken token when !string.IsNullOrEmpty(token.VocabularyId) => token,
                _ => throw ResolverErrors.Create(
                    ResolverErrorCode.InvalidVariable,
                    $"Text variable {key} must be a primitive or vocabulary token.",
                    origin,
                    new Dictionary<string, object?> { ["variable"] = key })
            };
        }

        return new ReadOnlyDictionary<string, object>(normalized);
    }
}
