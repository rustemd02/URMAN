using System.Text.Json.Nodes;
using Urman.Studio.Core.Scenes;
using Xunit;

namespace Urman.Studio.Tests;

// CINE01/CINE05: the Tamara scene, now data, as list and timeline of the same actions.
public sealed class CutsceneTimelineTests
{
    private static (JsonArray Actions, Func<string, string> Text) Load()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "Urman.slnx"))) root = root.Parent!;
        var actions = JsonNode.Parse(File.ReadAllText(Path.Combine(root.FullName, "game/content/cutscenes/tamara_fence.cutscene.v1.json")))!["entities"]!.AsArray();
        var texts = JsonNode.Parse(File.ReadAllText(Path.Combine(root.FullName, "content/modules/urman-chapter1/tamara-fence.json")))!.AsArray()
            .OfType<JsonObject>().Where(item => item["value"] is not null)
            .ToDictionary(item => ((string)item["id"]!).Replace("urman.chapter1:text/", ""), item => (string)item["value"]!["default"]!);
        return (actions, key => texts.GetValueOrDefault(key, key));
    }

    [Fact]
    public void TheTamaraSceneLastsAboutWhatTheGamePlays()
    {
        var (actions, text) = Load();
        var clips = CutsceneTimeline.Build(actions, text);
        Assert.Equal(actions.Count, clips.Count);
        var length = CutsceneTimeline.Length(clips);
        Assert.InRange(length, 50, 70); // measured in game 2026-09-27: 61.3 s
        Assert.Equal(9, clips.Count(clip => clip.Action == "cut"));
        Assert.All(clips.Where(clip => clip.Action == "move"), clip => Assert.True(clip.Estimated));
        // Shot starts measured in the game (tamara_fence_capture, trace 2026-09-27); the estimate stays within 25 % (walk lengths depend on the real ground).
        var measured = new[] { 0.0, 2.88, 11.14, 13.57, 21.93, 28.32, 37.61, 45.52, 58.72 };
        var estimated = clips.Where(clip => clip.Action == "cut").Select(clip => clip.Start).ToArray();
        for (var index = 1; index < measured.Length && index < estimated.Length; index++)
        {
            Assert.InRange(estimated[index], measured[index] * .75 - .5, measured[index] * 1.1 + .5); // walks are estimates
        }
    }

    [Fact]
    public void ScrubbingShowsTheShotAndLineOfThatMomentWithoutState()
    {
        var (actions, text) = Load();
        var clips = CutsceneTimeline.Build(actions, text);
        var punchline = clips.First(clip => clip.Action == "say" && clip.Label.Contains("забор Тамаре", StringComparison.Ordinal));
        var frame = CutsceneTimeline.At(actions, clips, punchline.Start + .1, text);
        Assert.Equal("punchline", (string)actions[frame.Shot!.Index]!["params"]!["tag"]!);
        Assert.Contains("Тамаре Геннадьевне", frame.LineText, StringComparison.Ordinal);
        Assert.True(frame.Actors.ContainsKey("guy") && frame.Actors.ContainsKey("tamara"));
        var tamara = frame.Actors["tamara"];
        Assert.InRange(tamara.X, 4.6, 4.8);
    }
}
