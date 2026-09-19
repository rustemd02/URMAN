using System.Text.Json.Nodes;
using Urman.Content.Resolvers;
using Xunit;

namespace Urman.Content.Tests;

public sealed class ContentResolversTests
{
    [Fact]
    public async Task CurrentCompiledPackHasClosedManifestAndLocalizedText()
    {
        var root = FindWorkspaceRoot();
        var fixture = Path.Combine(root, "tests-dotnet", "fixtures", "content", "urman.chapter1.compiled.v1.json");
        var pack = JsonNode.Parse(await File.ReadAllTextAsync(fixture, TestContext.Current.CancellationToken))!.AsObject();

        var closure = ManifestClosureValidator.Validate(pack);
        var textId = closure.TextIds.Single(id => id.EndsWith(":text/scene-arrival-title", StringComparison.Ordinal));
        var model = new TextResolver(pack, validate: false).Resolve(textId, "ru");

        // The village is КАРА-УРМАН since the author decision of 2026-09-16
        // (decision_log 2026-09-18); Кырлай remains a historical alias only.
        Assert.Equal("Дорога в Кара-Урман", model.Text);
        Assert.Equal("ru", model.ResolvedLocale);
        Assert.NotEmpty(closure.AssetIds);
    }

    [Fact]
    public void AssetIdsFollowModuleOrderAndSeededVariantIsDeterministic()
    {
        var lantern = Asset(
            "urman.alpha:asset/lantern",
            "alpha/lantern-base.bin",
            variants:
            [
                Variant("quiet", "alpha/lantern-quiet.bin"),
                Variant("wind", "alpha/lantern-wind.bin")
            ]);
        var portrait = Asset("urman.beta:asset/portrait", "beta/portrait.bin");
        var resolver = new AssetResolver(
            Pack(assets: [portrait, lantern]),
            (file, metadata) => $"https://content.example/{metadata.ModuleId}/{file}");

        Assert.Equal(
            ["urman.alpha:asset/lantern", "urman.beta:asset/portrait"],
            resolver.Ids());
        Assert.Null(resolver.Resolve("urman.alpha:asset/lantern").VariantId);
        Assert.Equal(
            "https://content.example/urman.alpha/alpha/lantern-wind.bin",
            resolver.Resolve("urman.alpha:asset/lantern", "wind").Url);

        var selector = new AssetVariantSelector(
            Seed: JsonValue.Create("new-run"),
            State: new JsonObject { ["phase"] = 2 });
        Assert.Equal(
            resolver.Resolve("urman.alpha:asset/lantern", selector),
            resolver.Resolve("urman.alpha:asset/lantern", selector));
    }

    [Fact]
    public void TextResolverUsesLocaleFallbackAndSemanticVocabularyToken()
    {
        const string textId = "urman.alpha:text/greeting";
        const string wordId = "urman.alpha:vocabulary/urman";
        var greeting = Text(
            textId,
            "Привет, {{word}}! {{name}}",
            new JsonObject { ["tt"] = "Сәлам, {{word}}! {{name}}" });
        var pack = Pack(texts: [greeting], vocabulary: [Vocabulary(wordId)]);
        var resolver = new TextResolver(pack);

        var model = resolver.Resolve(textId, "tt-RU", new Dictionary<string, object?>
        {
            ["word"] = new VocabularyToken(wordId),
            ["name"] = "<Айдар>"
        });

        Assert.Equal("tt", model.ResolvedLocale);
        Assert.Equal("Сәлам, урман! <Айдар>", model.Text);
        var segment = Assert.IsType<VocabularyTextSegment>(model.Segments[1]);
        Assert.Equal(wordId, segment.VocabularyId);
        Assert.Equal("урман", segment.Meaning);
        Assert.Equal("tt", segment.MeaningLocale);

        var error = Assert.Throws<ResolverException>(() =>
            resolver.Resolve(textId, "tt", new Dictionary<string, object?> { ["name"] = "Айдар" }));
        Assert.Equal(ResolverErrorCode.MissingVariable, error.Code);
        Assert.Equal("urman.alpha", error.Details["moduleId"]);
    }

    [Fact]
    public void AudioResolverSuppliesEquivalentCaptionTranscriptAndNonAudioCue()
    {
        const string captionId = "urman.alpha:text/knock-caption";
        const string transcriptId = "urman.alpha:text/knock-transcript";
        const string audioId = "urman.alpha:asset/knock";
        var audio = Asset(
            audioId,
            "audio/knock.ogg",
            kind: "audio",
            mediaType: "audio/ogg",
            accessibility: new JsonObject
            {
                ["decorative"] = false,
                ["captionTextId"] = captionId,
                ["audioDescriptionTextId"] = transcriptId
            });
        var pack = Pack(
            assets: [audio],
            texts:
            [
                Text(captionId, "Стук в дверь", purpose: "caption"),
                Text(transcriptId, "Стук снаружи; кто-то ждёт у двери", purpose: "accessibility")
            ]);

        var resolved = new AudioResolver(pack, resolveFileUrl: (file, _) => $"host:{file}").Resolve(
            audioId,
            new AudioResolveOptions(OutcomeKey: "urman.alpha:outcome/visitor-heard"));

        Assert.Equal("host:audio/knock.ogg", resolved.Asset.Url);
        Assert.Equal("Стук в дверь", resolved.Captions!.Text);
        Assert.Equal("Стук снаружи; кто-то ждёт у двери", resolved.Transcript!.Text);
        Assert.Equal("urman.alpha:outcome/visitor-heard", resolved.NonAudioCue!.OutcomeKey);
        Assert.Equal(resolved.Transcript.Text, resolved.NonAudioCue.Text);
    }

    [Fact]
    public void ClosureRejectsDuplicateIdsAndMissingAccessibilityReferencesWithStableOrigins()
    {
        const string duplicateId = "urman.alpha:asset/duplicate";
        var duplicateError = Assert.Throws<ResolverException>(() =>
            _ = new AssetResolver(Pack(assets: [Asset(duplicateId, "a.bin"), Asset(duplicateId, "b.bin")])));
        Assert.Equal(ResolverErrorCode.DuplicateLogicalId, duplicateError.Code);
        Assert.Equal("urman.alpha", duplicateError.Details["moduleId"]);
        Assert.Equal("/registries/assets/1", duplicateError.Details["sourcePointer"]);

        const string imageId = "urman.alpha:asset/sign";
        var accessibilityError = Assert.Throws<ResolverException>(() =>
            ManifestClosureValidator.Validate(Pack(assets:
            [
                Asset(imageId, "sign.png", accessibility: new JsonObject { ["decorative"] = false })
            ])));
        Assert.Equal(ResolverErrorCode.InvalidAccessibility, accessibilityError.Code);
        Assert.Equal("/registries/assets/0/accessibility/altTextId", accessibilityError.Details["sourcePointer"]);

        var resolver = new AssetResolver(Pack());
        var missingError = Assert.Throws<ResolverException>(() => resolver.Resolve("urman.beta:asset/absent"));
        Assert.Equal(ResolverErrorCode.MissingAsset, missingError.Code);
        Assert.Equal("urman.beta", missingError.Details["moduleId"]);
        Assert.Equal("/registries/assets", missingError.Details["sourcePointer"]);
    }

    private static JsonObject Pack(
        IReadOnlyList<JsonObject>? assets = null,
        IReadOnlyList<JsonObject>? texts = null,
        IReadOnlyList<JsonObject>? vocabulary = null)
    {
        static JsonArray Nodes(IReadOnlyList<JsonObject>? values) =>
            new(values?.Select(value => (JsonNode?)value.DeepClone()).ToArray() ?? []);

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["campaign"] = new JsonObject
            {
                ["orderedModules"] = new JsonArray
                {
                    new JsonObject { ["moduleId"] = "urman.alpha", ["exactVersion"] = "1.0.0" },
                    new JsonObject { ["moduleId"] = "urman.beta", ["exactVersion"] = "1.0.0" }
                }
            },
            ["registries"] = new JsonObject
            {
                ["assets"] = Nodes(assets),
                ["capabilities"] = new JsonArray(),
                ["characters"] = new JsonArray(),
                ["dialogues"] = new JsonArray(),
                ["documents"] = new JsonArray(),
                ["knowledge"] = new JsonArray(),
                ["quests"] = new JsonArray(),
                ["scenes"] = new JsonArray(),
                ["texts"] = Nodes(texts),
                ["vocabulary"] = Nodes(vocabulary)
            }
        };
    }

    private static JsonObject Asset(
        string id,
        string file,
        string kind = "image",
        string mediaType = "application/octet-stream",
        IReadOnlyList<JsonObject>? variants = null,
        JsonObject? accessibility = null) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["id"] = id,
            ["kind"] = kind,
            ["file"] = file,
            ["mediaType"] = mediaType,
            ["variants"] = new JsonArray(variants?.Select(value => (JsonNode?)value.DeepClone()).ToArray() ?? []),
            ["accessibility"] = accessibility?.DeepClone() ?? new JsonObject { ["decorative"] = true }
        };

    private static JsonObject Variant(string id, string file) => new() { ["id"] = id, ["file"] = file };

    private static JsonObject Text(
        string id,
        string value,
        JsonObject? translations = null,
        string purpose = "ui") =>
        new()
        {
            ["schemaVersion"] = 1,
            ["id"] = id,
            ["purpose"] = purpose,
            ["value"] = new JsonObject
            {
                ["default"] = value,
                ["translations"] = translations?.DeepClone() ?? new JsonObject()
            }
        };

    private static JsonObject Vocabulary(string id) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["id"] = id,
            ["term"] = "урман",
            ["language"] = "tt",
            ["meaning"] = new JsonObject
            {
                ["default"] = "лес",
                ["translations"] = new JsonObject { ["tt"] = "урман" }
            }
        };

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "content"))
                && File.Exists(Path.Combine(current.FullName, "Urman.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("URMAN workspace root was not found.");
    }
}
