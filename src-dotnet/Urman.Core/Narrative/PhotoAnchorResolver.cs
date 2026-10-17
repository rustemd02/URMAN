using System;
using System.Collections.Generic;
using System.Linq;

namespace Urman.Core.Narrative;

/// <summary>
/// Resolves a PhotoWorlds semantic anchor to a named safe node. An anchor is
/// never resolved to an empty string, a null or a zero spawn: an unknown or
/// missing anchor yields the named fallback anchor together with a diagnostic,
/// so the caller can log it and keep the player at a verified place instead of
/// spawning at the origin. Duplicate anchors are rejected by
/// <see cref="PhotoWorldCatalog"/> validation; this resolver only turns a single
/// anchor id into a safe, named node.
/// </summary>
public sealed record PhotoAnchorResolution(
    string AnchorId,
    string SafeNodeId,
    string? ExitNodeId,
    bool IsFallback,
    string Diagnostic);

public static class PhotoAnchorResolver
{
    /// <summary>
    /// The single named fallback used for unknown or missing anchors. It is a
    /// real, authored anchor (the home table), never an empty/zero spawn.
    /// </summary>
    public const string FallbackAnchorId = "PW_HOME_TABLE";

    // World-bound anchors: each maps to the authored spawn/exit safe nodes of
    // its PhotoWorld. These node names are the verified safe spawns, never zero.
    private static readonly IReadOnlyDictionary<string, (string Spawn, string Exit)> WorldAnchors =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["PW_FOREST_ENTRY"] = ("F10", "F16"),
            ["PW_RIVER_ENTRY"] = ("R00", "R08"),
            ["PW_HOME_PHOTO_ENTRY"] = ("H00", "H08"),
            ["PW_COMMON_ENTRY"] = ("C00", "C08"),
            ["PW_CELLAR_ENTRY"] = ("B00", "B07"),
        };

    // Support anchors have no world of their own; their safe node is the anchor
    // itself, which the game layer maps to a verified physical node.
    private static readonly IReadOnlySet<string> SupportAnchors =
        new HashSet<string>(StringComparer.Ordinal) { "PW_HOME_TABLE", "PW_PROLOGUE_ROAD" };

    public static IReadOnlyList<string> KnownAnchors { get; } =
        WorldAnchors.Keys.Concat(SupportAnchors).OrderBy(value => value, StringComparer.Ordinal).ToArray().AsReadOnly();

    public static bool IsKnownAnchor(string? anchorId) =>
        !string.IsNullOrWhiteSpace(anchorId)
        && (WorldAnchors.ContainsKey(anchorId!) || SupportAnchors.Contains(anchorId!));

    /// <summary>
    /// Resolves one anchor id to a named safe node. Never returns an empty or
    /// null safe node id; unknown/missing anchors fall back to
    /// <see cref="FallbackAnchorId"/> with <see cref="PhotoAnchorResolution.IsFallback"/>
    /// set and a diagnostic explaining why.
    /// </summary>
    public static PhotoAnchorResolution Resolve(string? anchorId)
    {
        if (string.IsNullOrWhiteSpace(anchorId))
        {
            return new PhotoAnchorResolution(
                FallbackAnchorId, FallbackAnchorId, null, true,
                "Empty anchor id; using named fallback instead of an unnamed or zero spawn.");
        }

        var id = anchorId!;

        if (WorldAnchors.TryGetValue(id, out var world))
        {
            return new PhotoAnchorResolution(id, world.Spawn, world.Exit, false,
                "Resolved from authored world safe nodes.");
        }

        if (SupportAnchors.Contains(id))
        {
            return new PhotoAnchorResolution(id, id, null, false,
                "Resolved support anchor (named, no world spawn).");
        }

        return new PhotoAnchorResolution(
            id, FallbackAnchorId, null, true,
            $"Unknown or missing anchor {id}; using named fallback {FallbackAnchorId} instead of a zero spawn.");
    }

    /// <summary>
    /// Validates a set of anchors the same way the catalog does: no duplicates,
    /// and every entry must be a known anchor. Throws on the first violation so
    /// a duplicated or unknown anchor is never silently accepted.
    /// </summary>
    public static void ValidateAnchors(IEnumerable<string> anchors)
    {
        if (anchors is null)
            throw new ArgumentNullException(nameof(anchors));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var anchor in anchors)
        {
            if (string.IsNullOrWhiteSpace(anchor))
                throw new InvalidDataException("PhotoWorlds semantic anchor must be a non-empty named id.");
            if (!seen.Add(anchor))
                throw new InvalidDataException($"Duplicate PhotoWorlds semantic anchor {anchor}.");
            if (!IsKnownAnchor(anchor))
                throw new InvalidDataException($"Unknown PhotoWorlds semantic anchor {anchor}.");
        }
    }
}
