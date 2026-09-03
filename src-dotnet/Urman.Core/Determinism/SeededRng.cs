namespace Urman.Core.Determinism;

public sealed record SeededRngSnapshot(uint Seed, uint State, long Position);

public sealed record OwnerRngSnapshot(string OwnerId, uint Seed, uint State, long Position);

public sealed record OwnerRngStreamsSnapshot(uint MasterSeed, IReadOnlyList<OwnerRngSnapshot> Streams);

public sealed class SeededRng
{
    private const uint NonZeroFallback = 0x6d2b79f5;
    private uint _state;

    public SeededRng(uint seed, uint? state = null, long position = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        Seed = seed == 0 ? NonZeroFallback : seed;
        _state = state ?? Seed;
        Position = position;
    }

    public uint Seed { get; }

    public long Position { get; private set; }

    public uint NextUInt32()
    {
        if (Position == long.MaxValue)
        {
            throw new OverflowException("RNG position would overflow.");
        }

        var next = _state;
        next ^= next << 13;
        next ^= next >> 17;
        next ^= next << 5;
        _state = next;
        Position++;
        return next;
    }

    public double NextDouble() => NextUInt32() / 4294967296d;

    public SeededRngSnapshot CaptureSnapshot() => new(Seed, _state, Position);

    public static SeededRng Restore(SeededRngSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(snapshot.Seed, snapshot.State, snapshot.Position);
    }

    public static uint HashText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var hash = 0x811c9dc5u;
        foreach (var character in text)
        {
            hash ^= character;
            hash = unchecked(hash * 0x01000193u);
        }

        return hash == 0 ? NonZeroFallback : hash;
    }
}

public sealed class OwnerRngStreams
{
    private readonly Dictionary<string, SeededRng> _streams = new(StringComparer.Ordinal);

    public OwnerRngStreams(uint masterSeed)
    {
        MasterSeed = masterSeed == 0 ? 0x6d2b79f5 : masterSeed;
    }

    public uint MasterSeed { get; }

    public SeededRng ForOwner(string ownerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        if (!_streams.TryGetValue(ownerId, out var stream))
        {
            stream = new SeededRng(SeededRng.HashText($"{MasterSeed}:{ownerId}"));
            _streams.Add(ownerId, stream);
        }

        return stream;
    }

    public OwnerRngStreamsSnapshot CaptureSnapshot() => new(
        MasterSeed,
        _streams.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry =>
            {
                var snapshot = entry.Value.CaptureSnapshot();
                return new OwnerRngSnapshot(entry.Key, snapshot.Seed, snapshot.State, snapshot.Position);
            })
            .ToArray());

    public static OwnerRngStreams Restore(OwnerRngStreamsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var result = new OwnerRngStreams(snapshot.MasterSeed);
        foreach (var entry in snapshot.Streams)
        {
            ArgumentException.ThrowIfNullOrEmpty(entry.OwnerId);
            if (!result._streams.TryAdd(entry.OwnerId, new SeededRng(entry.Seed, entry.State, entry.Position)))
            {
                throw new ArgumentException($"Duplicate RNG stream owner {entry.OwnerId}.", nameof(snapshot));
            }
        }

        return result;
    }
}
