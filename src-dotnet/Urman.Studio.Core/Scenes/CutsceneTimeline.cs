using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Scenes;

/// <summary>One action placed on the timeline. <see cref="Estimated"/> marks durations that depend on the world (walking).</summary>
public sealed record TimelineClip(string Id, int Index, string Action, string Track, string Label, double Start, double Duration, bool Estimated);

/// <summary>Where the presentation stands at a moment: the shot, the caption and each actor (no game state is touched).</summary>
public sealed record TimelineFrame(TimelineClip? Shot, TimelineClip? Line, string? LineText, IReadOnlyDictionary<string, (double X, double Z)> Actors);

/// <summary>
/// The list and the timeline of one cutscene are two views of the same ordered
/// actions (spec CINE01). Timing mirrors the game's player: a line lasts at
/// least as long as it takes to read (plus a short beat unless deadpan), a
/// walk takes its path length over its speed, and camera cuts and staging are
/// instant. Walks are shown as estimates because the real ground can differ.
/// </summary>
public static class CutsceneTimeline
{
    public static readonly IReadOnlyDictionary<string, string> Tracks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["cut"] = "Камеры", ["say"] = "Реплики и субтитры", ["move"] = "Персонажи: перемещение", ["place"] = "Персонажи: перемещение",
        ["show"] = "Персонажи: перемещение", ["face"] = "Персонажи: перемещение", ["clip"] = "Анимации", ["laugh"] = "Анимации",
        ["filming"] = "Анимации", ["wait"] = "Ожидания"
    };

    public static IReadOnlyList<TimelineClip> Build(JsonArray actions, Func<string, string> resolveText)
    {
        var clips = new List<TimelineClip>();
        var time = 0.0;
        var positions = new Dictionary<string, (double X, double Z)>(StringComparer.Ordinal);
        for (var index = 0; index < actions.Count; index++)
        {
            if (actions[index] is not JsonObject entity || entity["params"] is not JsonObject p) continue;
            var action = (string?)p["action"] ?? "";
            var duration = 0.0;
            var estimated = false;
            switch (action)
            {
                case "wait":
                    duration = (double?)p["seconds"] ?? 0;
                    break;
                case "say":
                    var text = resolveText((string?)p["text"] ?? "");
                    duration = Math.Max((double?)p["seconds"] ?? 0, text.Length * .055 + .9) + (((bool?)p["deadpan"] ?? false) ? 0 : .18);
                    break;
                case "place" when p["at"] is JsonArray at && (string?)p["actor"] is { } placed:
                    positions[placed] = ((double)at[0]!, (double)at[1]!);
                    break;
                case "move" when p["stops"] is JsonArray stops && (string?)p["actor"] is { } walker:
                    var from = positions.GetValueOrDefault(walker);
                    var length = 0.0;
                    foreach (var stop in stops.OfType<JsonArray>())
                    {
                        var to = ((double)stop[0]!, (double)stop[1]!);
                        length += Math.Sqrt(Math.Pow(to.Item1 - from.X, 2) + Math.Pow(to.Item2 - from.Z, 2));
                        from = to;
                    }

                    positions[walker] = from;
                    duration = length / Math.Max((double?)p["speed"] ?? 1, .05);
                    estimated = true;
                    break;
            }

            clips.Add(new((string)entity["id"]!, index, action, Tracks.GetValueOrDefault(action, "События мира"), (string?)entity["name"] ?? action, time, duration, estimated));
            time += duration;
        }

        return clips;
    }

    public static double Length(IReadOnlyList<TimelineClip> clips) => clips.Count == 0 ? 0 : clips.Max(clip => clip.Start + clip.Duration);

    /// <summary>The presentation at time <paramref name="t"/>: active shot, caption and interpolated actor positions.</summary>
    public static TimelineFrame At(JsonArray actions, IReadOnlyList<TimelineClip> clips, double t, Func<string, string> resolveText)
    {
        var shot = clips.LastOrDefault(clip => clip.Action == "cut" && clip.Start <= t);
        var line = clips.LastOrDefault(clip => clip.Action == "say" && clip.Start <= t && t < clip.Start + clip.Duration);
        var actors = new Dictionary<string, (double X, double Z)>(StringComparer.Ordinal);
        foreach (var clip in clips.Where(clip => clip.Start <= t))
        {
            var p = actions[clip.Index]!["params"]!.AsObject();
            var actor = (string?)p["actor"];
            if (actor is null) continue;
            if (clip.Action == "place" && p["at"] is JsonArray at) actors[actor] = ((double)at[0]!, (double)at[1]!);
            if (clip.Action == "move" && p["stops"] is JsonArray stops)
            {
                var progress = clip.Duration <= 0 ? 1 : Math.Clamp((t - clip.Start) / clip.Duration, 0, 1);
                var points = new List<(double X, double Z)> { actors.GetValueOrDefault(actor) };
                points.AddRange(stops.OfType<JsonArray>().Select(stop => ((double)stop[0]!, (double)stop[1]!)));
                var total = 0.0;
                for (var i = 1; i < points.Count; i++) total += Distance(points[i - 1], points[i]);
                var travelled = total * progress;
                var position = points[^1];
                for (var i = 1; i < points.Count; i++)
                {
                    var segment = Distance(points[i - 1], points[i]);
                    if (travelled <= segment)
                    {
                        var f = segment <= 0 ? 1 : travelled / segment;
                        position = (points[i - 1].X + (points[i].X - points[i - 1].X) * f, points[i - 1].Z + (points[i].Z - points[i - 1].Z) * f);
                        break;
                    }

                    travelled -= segment;
                }

                actors[actor] = position;
            }
        }

        return new(shot, line, line is null ? null : resolveText((string?)actions[line.Index]!["params"]!["text"] ?? ""), actors);
    }

    private static double Distance((double X, double Z) a, (double X, double Z) b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
}
