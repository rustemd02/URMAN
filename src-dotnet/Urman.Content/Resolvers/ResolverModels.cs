using System.Text.Json.Nodes;

namespace Urman.Content.Resolvers;

public sealed record AssetVariantSelector(string? Id = null, JsonNode? Seed = null, JsonNode? State = null);

public sealed record AssetResolutionMetadata(string AssetId, string? VariantId, string ModuleId);

public sealed record ResolvedAccessibility(
    bool Decorative,
    string? AltTextId,
    string? CaptionTextId,
    string? AudioDescriptionTextId);

public sealed record ResolvedAsset(
    string AssetId,
    string Kind,
    string MediaType,
    string Url,
    string? VariantId,
    string? Sha256,
    ResolvedAccessibility Accessibility);

public sealed record VocabularyToken(string VocabularyId);

public abstract record ResolvedTextSegment(string Type);

public sealed record PlainTextSegment(string Value) : ResolvedTextSegment("text");

public sealed record VocabularyTextSegment(
    string VocabularyId,
    string Language,
    string Term,
    string Meaning,
    string MeaningLocale) : ResolvedTextSegment("vocabulary");

public sealed record ResolvedText(
    string TextId,
    string Purpose,
    string RequestedLocale,
    string ResolvedLocale,
    string Text,
    IReadOnlyList<ResolvedTextSegment> Segments);

public sealed record ManifestClosure(
    IReadOnlyList<string> AssetIds,
    IReadOnlyList<string> TextIds,
    IReadOnlyList<string> VocabularyIds);
