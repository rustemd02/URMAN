using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>What playing a catalogue motion on a character did, or why it could not (spec ANIM02).</summary>
public readonly record struct AnimationPlayResult(bool Played, string Clip, string Problem);

/// <summary>
/// The animation catalogue (res://content/animations/catalog.v1.json, edited in
/// URMAN Studio, spec ANIM01–ANIM04). A motion is a character kit clip, a clip
/// of the bundled Quaternius Universal Animation Library (CC0) or a procedural
/// motion. Library clips share the kit's 65-bone skeleton: each track is
/// re-pointed at the character's own skeleton node, bone names unchanged; a
/// clip whose bones the character lacks is refused with a reason instead of
/// deforming the character.
/// </summary>
public static class AnimationCatalog
{
    public const string CatalogPath = "res://content/animations/catalog.v1.json";
    public const string LibraryPath = "res://assets/animations/ual1_standard.glb";
    private const string LibraryName = "ual";
    private static Dictionary<string, JsonElement>? _entries;
    private static Dictionary<string, Animation>? _library;
    // Keeps the library scene's managed wrapper alive with its animations.
    private static Node? _libraryScene;

    /// <summary>The character's kit AnimationPlayer and prefix, resolved once.</summary>
    private sealed class KitPlayer
    {
        public AnimationPlayer Player = null!;
        public string Prefix = "";
    }

    // A character's kit player and prefix never change, but resolving them means a
    // recursive FindChildren over the whole character subtree (hundreds of nodes in
    // the kit) plus a LINQ iterator, and Play/Compatibility are called on every
    // motion change. Weak keys keep this free of leaks as characters are rebuilt.
    private static readonly ConditionalWeakTable<Node3D, KitPlayer> _kitPlayers = new();

    private static KitPlayer ResolveKitPlayer(Node3D character)
    {
        if (_kitPlayers.TryGetValue(character, out var cached) && IsCacheUsable(character, cached)) return cached;
        var prefix = character.HasMeta("characterPrefix") ? character.GetMeta("characterPrefix").AsString() : "";
        AnimationPlayer? found = null;
        using var candidates = (global::Godot.Collections.Array)character.FindChildren("*", nameof(AnimationPlayer), true, false);
        foreach (var candidate in candidates)
        {
            if (candidate.AsGodotObject() is AnimationPlayer player && player.HasAnimation($"{prefix}_Idle"))
            {
                found = player;
                break;
            }
        }

        var resolved = new KitPlayer { Player = found!, Prefix = prefix };
        _kitPlayers.Remove(character);
        if (found is not null) _kitPlayers.Add(character, resolved);
        return resolved;
    }

    private static bool IsCacheUsable(Node3D character, KitPlayer cached) =>
        cached.Player is not null
        && GodotObject.IsInstanceValid(cached.Player)
        && character.IsAncestorOf(cached.Player)
        && cached.Player.HasAnimation($"{cached.Prefix}_Idle");

    public static IReadOnlyDictionary<string, JsonElement> Entries => _entries ??= Load(null);

    /// <summary>Studio preview: use edited catalogue data (null = reread the file).</summary>
    public static void Reload(string? json = null) => _entries = Load(json);

    public static AnimationPlayResult Play(Node3D character, string motionId, double blend = .2)
    {
        if (!Entries.TryGetValue(motionId, out var entry))
        {
            return new(false, "", $"В каталоге нет движения {motionId}.");
        }

        var p = entry.GetProperty("params");
        var source = p.GetProperty("source");
        var speed = p.TryGetProperty("speed", out var speedValue) ? speedValue.GetSingle() : 1f;
        var loop = p.TryGetProperty("loop", out var loopValue) && loopValue.GetBoolean();
        var kit = ResolveKitPlayer(character);
        var prefix = kit.Prefix;
        var player = kit.Player;
        if (player is null)
        {
            return new(false, "", "У персонажа нет проигрывателя анимаций кита.");
        }

        if (source.TryGetProperty("procedural", out var procedural))
        {
            // Procedural motions keep the stance clip; the caller turns or moves the node.
            player.Play($"{prefix}_Idle", blend);
            return new(true, $"{prefix}_Idle", procedural.GetString() == "turn" ? "" : $"Процедурное движение {procedural.GetString()} исполняется вызывающим кодом.");
        }

        if (source.TryGetProperty("kit", out var kitClip))
        {
            var name = $"{prefix}_{kitClip.GetString()}";
            if (!player.HasAnimation(name)) return new(false, name, $"У этого персонажа нет клипа кита «{kitClip.GetString()}».");
            player.GetAnimation(name).LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
            player.Play(name, blend, speed);
            return new(true, name, "");
        }

        var clip = source.GetProperty("clip").GetString()!;
        if (!TryClip(clip, out var original))
        {
            return new(false, clip, $"В библиотеке нет клипа {clip}.");
        }

        var skeletonPath = SkeletonPath(player, prefix);
        var skeleton = player.GetNode(player.RootNode).GetNodeOrNull<Skeleton3D>(skeletonPath);
        if (skeleton is null)
        {
            return new(false, clip, "Не найден скелет персонажа.");
        }

        var key = $"{LibraryName}/{prefix}_{clip}";
        if (!player.HasAnimation(key))
        {
            var retargeted = (Animation)original.Duplicate();
            var missing = new List<string>();
            for (var track = retargeted.GetTrackCount() - 1; track >= 0; track--)
            {
                var path = retargeted.TrackGetPath(track).ToString();
                var bone = path.Contains(':') ? path[(path.LastIndexOf(':') + 1)..] : "";
                if (bone.Length == 0)
                {
                    retargeted.RemoveTrack(track);
                    continue;
                }

                if (skeleton.FindBone(bone) < 0)
                {
                    missing.Add(bone);
                    retargeted.RemoveTrack(track);
                    continue;
                }

                // Root translation would slide the body away from where the
                // movement owner puts it: one owner of position (ANIM05).
                if (bone == "root" && retargeted.TrackGetType(track) == Animation.TrackType.Position3D)
                {
                    retargeted.RemoveTrack(track);
                    continue;
                }

                retargeted.TrackSetPath(track, $"{skeletonPath}:{bone}");
            }

            if (missing.Count > 8)
            {
                return new(false, clip, $"Клип не подходит к скелету персонажа: нет костей {string.Join(", ", missing.Take(6))}…");
            }

            if (!player.HasAnimationLibrary(LibraryName)) player.AddAnimationLibrary(LibraryName, new AnimationLibrary());
            player.GetAnimationLibrary(LibraryName).AddAnimation($"{prefix}_{clip}", retargeted);
        }

        var animation = player.GetAnimation(key);
        animation.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        player.Play(key, blend, speed);
        character.SetMeta("animationClip", key);
        return new(true, key, "");
    }

    /// <summary>
    /// VIS-046: the owner that already moves the character keeps the ownership of
    /// position; this only retimes the motion that is currently playing, so the
    /// library clip and the real footfalls share one cadence instead of the body
    /// sliding under an unrelated tempo. Returns false when there is no motion to
    /// retime (nothing is playing, or the mixer belongs to a caller that stopped it).
    /// </summary>
    public static bool TrySetMotionScale(Node3D character, double scale,
        out float appliedScale, out float cycleSeconds)
    {
        appliedScale = 1f;
        cycleSeconds = 0f;
        var kit = ResolveKitPlayer(character);
        var player = kit.Player;
        if (player is null) return false;
        var clip = player.GetCurrentAnimation().ToString();
        if (clip.Length == 0 || !player.HasAnimation(clip)) return false;
        var animation = player.GetAnimation(clip);
        cycleSeconds = (float)animation.Length;
        if (cycleSeconds <= 0f) return false;
        appliedScale = (float)Mathf.Clamp((float)scale, MinMotionScale, MaxMotionScale);
        player.SpeedScale = appliedScale;
        character.SetMeta("animationMotionScale", appliedScale);
        character.SetMeta("animationMotionCycleSeconds", cycleSeconds);
        character.SetMeta("animationMotionClip", clip);
        return true;
    }

    /// <summary>VIS-046: restore the catalogue's authored tempo when the motion ends.</summary>
    public static void ResetMotionScale(Node3D character)
    {
        var kit = ResolveKitPlayer(character);
        if (kit.Player is null) return;
        kit.Player.SpeedScale = 1f;
        character.SetMeta("animationMotionScale", 1f);
    }

    // A walk clip retimed past these bounds stops reading as a person: below the
    // floor the legs run through molasses, above it the clip turns into a shuffle.
    private const float MinMotionScale = .70f;
    private const float MaxMotionScale = 1.35f;

    /// <summary>Whether a library clip fits the character's skeleton, without playing it.</summary>
    public static string Compatibility(Node3D character, string clip)
    {
        var kit = ResolveKitPlayer(character);
        var prefix = kit.Prefix;
        var player = kit.Player;
        if (player is null || !TryClip(clip, out var animation)) return "нет данных";
        var skeleton = player.GetNode(player.RootNode).GetNodeOrNull<Skeleton3D>(SkeletonPath(player, prefix));
        if (skeleton is null) return "нет скелета";
        var bones = Enumerable.Range(0, animation.GetTrackCount()).Select(track => animation.TrackGetPath(track).ToString())
            .Where(path => path.Contains(':')).Select(path => path[(path.LastIndexOf(':') + 1)..]).Distinct().ToArray();
        var missing = bones.Count(bone => skeleton.FindBone(bone) < 0);
        return missing == 0 ? "совместим" : $"не хватает костей: {missing} из {bones.Length}";
    }

    // The detached library scene owns native mesh/animation resources. Release
    // it only after a test scene has finished, alongside the other test caches.
    internal static void ClearCacheForTests()
    {
        _kitPlayers.Clear();
        _library?.Clear();
        _library = null;
        if (GodotObject.IsInstanceValid(_libraryScene)) _libraryScene!.Free();
        _libraryScene = null;
    }

    public static IReadOnlyCollection<string> LibraryClips => Library().Keys;

    // Godot's importer strips a "_Loop" suffix from clip names and marks the
    // clip looping, so the catalogue's source name may appear shortened.
    private static bool TryClip(string clip, out Animation animation) =>
        Library().TryGetValue(clip, out animation!)
        || (clip.EndsWith("_Loop", StringComparison.Ordinal) && Library().TryGetValue(clip[..^"_Loop".Length], out animation!));

    private static string SkeletonPath(AnimationPlayer player, string prefix)
    {
        var idle = player.GetAnimation($"{prefix}_Idle");
        for (var track = 0; track < idle.GetTrackCount(); track++)
        {
            var path = idle.TrackGetPath(track).ToString();
            if (path.Contains(':')) return path[..path.LastIndexOf(':')];
        }

        return $"{prefix}_Rig/Skeleton3D";
    }

    private static Dictionary<string, Animation> Library()
    {
        if (_library is not null) return _library;
        _library = new Dictionary<string, Animation>(StringComparer.Ordinal);
        var scene = ResourceLoader.Load<PackedScene>(LibraryPath)?.Instantiate();
        if (scene is null) return _library;
        _libraryScene = scene;
        foreach (var player in scene.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>())
        {
            foreach (var name in player.GetAnimationList())
            {
                _library[name.ToString().Contains('/') ? name.ToString()[(name.ToString().IndexOf('/') + 1)..] : name.ToString()] = player.GetAnimation(name);
            }
        }

        return _library;
    }

    private static Dictionary<string, JsonElement> Load(string? json)
    {
        json ??= global::Godot.FileAccess.FileExists(CatalogPath) ? global::Godot.FileAccess.GetFileAsString(CatalogPath) : "{\"entities\":[]}";
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("entities").EnumerateArray()
            .ToDictionary(entity => entity.GetProperty("id").GetString()!, entity => entity.Clone(), StringComparer.Ordinal);
    }
}
