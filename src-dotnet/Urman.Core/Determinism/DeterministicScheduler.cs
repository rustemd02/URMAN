using System.Text.Json;

namespace Urman.Core.Determinism;

public sealed record ScheduledJob(string JobId, string OwnerId, long DueTick, JsonElement Payload);

public sealed record ScheduledJobClaim(
    string JobId,
    string OwnerId,
    long DueTick,
    JsonElement Payload,
    string OccurrenceId);

public sealed record FiredScheduledJob(string JobId, long FiredAtTick, string OccurrenceId);

public sealed record DeterministicSchedulerSnapshot(
    IReadOnlyList<ScheduledJob> Jobs,
    IReadOnlyList<FiredScheduledJob> Fired);

public sealed class DeterministicScheduler
{
    private readonly Dictionary<string, ScheduledJob> _jobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FiredScheduledJob> _fired = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _leases = new(StringComparer.Ordinal);

    public ScheduledJob Schedule(ScheduledJob job)
    {
        var normalized = ValidateJob(job);
        if (_jobs.ContainsKey(normalized.JobId) || _fired.ContainsKey(normalized.JobId))
        {
            throw new ArgumentException($"Scheduled job ID {normalized.JobId} has already been used.", nameof(job));
        }

        _jobs.Add(normalized.JobId, normalized);
        return normalized;
    }

    public int CancelOwner(string ownerId)
    {
        ownerId = Required(ownerId, "Scheduled job owner ID");
        var ids = _jobs.Values
            .Where(job => job.OwnerId == ownerId)
            .Select(job => job.JobId)
            .ToArray();
        foreach (var jobId in ids)
        {
            _jobs.Remove(jobId);
            _leases.Remove(jobId);
        }

        return ids.Length;
    }

    public IReadOnlyList<ScheduledJobClaim> ClaimDue(long nowTick) => ClaimDueInternal(nowTick, null);

    public IReadOnlyList<ScheduledJobClaim> ClaimDueForOwner(string ownerId, long nowTick) =>
        ClaimDueInternal(nowTick, Required(ownerId, "Scheduled job owner ID"));

    public ScheduledJobClaim Acknowledge(string jobId, string occurrenceId)
    {
        jobId = Required(jobId, "Scheduled job ID");
        occurrenceId = Required(occurrenceId, "Scheduled job occurrence ID");
        if (!_jobs.TryGetValue(jobId, out var job) || _fired.ContainsKey(jobId))
        {
            throw new ArgumentException($"Scheduled job {jobId} is not pending.");
        }

        if (!_leases.TryGetValue(jobId, out var lease) || lease != occurrenceId)
        {
            throw new ArgumentException($"Scheduled job {jobId} is not leased by {occurrenceId}.");
        }

        _jobs.Remove(jobId);
        _leases.Remove(jobId);
        _fired.Add(jobId, new(jobId, job.DueTick, occurrenceId));
        return Claim(job);
    }

    public void Release(string jobId, string occurrenceId)
    {
        jobId = Required(jobId, "Scheduled job ID");
        occurrenceId = Required(occurrenceId, "Scheduled job occurrence ID");
        if (!_leases.TryGetValue(jobId, out var lease) || lease != occurrenceId)
        {
            throw new ArgumentException($"Scheduled job {jobId} is not leased by {occurrenceId}.");
        }

        _leases.Remove(jobId);
    }

    public DeterministicSchedulerSnapshot CaptureSnapshot() => new(
        _jobs.Values
            .OrderBy(job => job.DueTick)
            .ThenBy(job => job.JobId, StringComparer.Ordinal)
            .Select(Clone)
            .ToArray(),
        _fired.Values
            .OrderBy(entry => entry.JobId, StringComparer.Ordinal)
            .ToArray());

    public static DeterministicScheduler Restore(DeterministicSchedulerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Jobs is null || snapshot.Fired is null)
        {
            throw new ArgumentException("Scheduler snapshot is invalid.", nameof(snapshot));
        }

        var scheduler = new DeterministicScheduler();
        foreach (var job in snapshot.Jobs)
        {
            scheduler.Schedule(job);
        }

        foreach (var entry in snapshot.Fired)
        {
            ArgumentNullException.ThrowIfNull(entry);
            var jobId = Required(entry.JobId, "Scheduler fire ledger job ID");
            var occurrenceId = Required(entry.OccurrenceId, "Scheduler fire occurrence ID");
            ArgumentOutOfRangeException.ThrowIfNegative(entry.FiredAtTick);
            if (scheduler._jobs.ContainsKey(jobId) || !scheduler._fired.TryAdd(jobId, new(jobId, entry.FiredAtTick, occurrenceId)))
            {
                throw new ArgumentException($"Duplicate scheduler job ID {jobId}.", nameof(snapshot));
            }
        }

        return scheduler;
    }

    private IReadOnlyList<ScheduledJobClaim> ClaimDueInternal(long nowTick, string? ownerId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nowTick);
        var due = _jobs.Values
            .Where(job => job.DueTick <= nowTick && (ownerId is null || job.OwnerId == ownerId) && !_leases.ContainsKey(job.JobId))
            .OrderBy(job => job.DueTick)
            .ThenBy(job => job.JobId, StringComparer.Ordinal)
            .ToArray();
        var claims = new List<ScheduledJobClaim>(due.Length);
        foreach (var job in due)
        {
            var claim = Claim(job);
            _leases.Add(job.JobId, claim.OccurrenceId);
            claims.Add(claim);
        }

        return claims;
    }

    private static ScheduledJobClaim Claim(ScheduledJob job) => new(
        job.JobId,
        job.OwnerId,
        job.DueTick,
        job.Payload.Clone(),
        $"scheduled:{job.JobId}:{job.DueTick}");

    private static ScheduledJob ValidateJob(ScheduledJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var jobId = Required(job.JobId, "Scheduled job ID");
        var ownerId = Required(job.OwnerId, "Scheduled job owner ID");
        ArgumentOutOfRangeException.ThrowIfNegative(job.DueTick);
        if (job.Payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException($"Scheduled job {jobId} payload is undefined.", nameof(job));
        }

        return new(jobId, ownerId, job.DueTick, job.Payload.Clone());
    }

    private static ScheduledJob Clone(ScheduledJob job) => new(job.JobId, job.OwnerId, job.DueTick, job.Payload.Clone());

    private static string Required(string? value, string label) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"{label} must be a non-empty string.");
}
