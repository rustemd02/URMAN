namespace Urman.Studio.Core.Collaboration;

public enum BinaryMergeOutcome
{
    Unchanged,
    TakeMine,
    TakeTheirs,
    /// <summary>Both sides changed the file: models, textures and audio are never blended (spec COLLAB06).</summary>
    Conflict
}

/// <summary>Content-hash decision for a binary resource; the author resolves a conflict by keeping one side or saving the other as a new resource.</summary>
public static class BinaryMerge
{
    public static BinaryMergeOutcome Decide(string? baseSha256, string? mineSha256, string? theirsSha256)
    {
        if (string.Equals(mineSha256, theirsSha256, StringComparison.Ordinal))
        {
            return string.Equals(baseSha256, mineSha256, StringComparison.Ordinal) ? BinaryMergeOutcome.Unchanged : BinaryMergeOutcome.TakeMine;
        }

        if (string.Equals(baseSha256, mineSha256, StringComparison.Ordinal))
        {
            return BinaryMergeOutcome.TakeTheirs;
        }

        return string.Equals(baseSha256, theirsSha256, StringComparison.Ordinal) ? BinaryMergeOutcome.TakeMine : BinaryMergeOutcome.Conflict;
    }
}
