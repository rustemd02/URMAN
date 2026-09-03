namespace Urman.Core.Determinism;

public sealed record LogicalClockSnapshot(long Tick);

public sealed class LogicalClock
{
    public LogicalClock(long tick = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        Tick = tick;
    }

    public long Tick { get; private set; }

    public long Advance(long delta = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delta);
        Tick = checked(Tick + delta);
        return Tick;
    }

    public LogicalClockSnapshot CaptureSnapshot() => new(Tick);

    public static LogicalClock Restore(LogicalClockSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(snapshot.Tick);
    }
}
