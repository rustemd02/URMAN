import { clonePersistedJsonValue } from '../contracts/json-value.mjs';

function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty string.`);
  return value;
}

function tick(value, label) {
  if (!Number.isSafeInteger(value) || value < 0) throw new TypeError(`${label} must be a safe non-negative integer.`);
  return value;
}

function normalizedJob(job) {
  if (!job || typeof job !== 'object' || Array.isArray(job)) throw new TypeError('Scheduled job must be an object.');
  const allowed = ['jobId', 'ownerId', 'dueTick', 'payload'];
  if (Object.keys(job).some((key) => !allowed.includes(key)) || allowed.some((key) => !Object.hasOwn(job, key))) {
    throw new TypeError('Scheduled job has an invalid shape.');
  }
  return Object.freeze({
    jobId: nonEmptyString(job.jobId, 'Scheduled job ID'),
    ownerId: nonEmptyString(job.ownerId, 'Scheduled job owner ID'),
    dueTick: tick(job.dueTick, 'Scheduled job due tick'),
    payload: clonePersistedJsonValue(job.payload, `Scheduled job ${job.jobId} payload`),
  });
}

/**
 * A callback-free scheduler. The consumer dispatches returned jobs through the
 * kernel, so save/restore never captures executable closures or wall time.
 */
export class DeterministicScheduler {
  #jobs = new Map();
  #fired = new Map();
  // Leases are intentionally not persisted. A save taken after extraction but
  // before the kernel commits its action must make the job eligible again.
  #leases = new Map();

  schedule(job) {
    const normalized = normalizedJob(job);
    if (this.#jobs.has(normalized.jobId) || this.#fired.has(normalized.jobId)) {
      throw new TypeError(`Scheduled job ID ${normalized.jobId} has already been used.`);
    }
    this.#jobs.set(normalized.jobId, normalized);
    return normalized;
  }

  cancelOwner(ownerId) {
    ownerId = nonEmptyString(ownerId, 'Scheduled job owner ID');
    let cancelled = 0;
    for (const [jobId, job] of this.#jobs) {
      if (job.ownerId === ownerId) {
        this.#jobs.delete(jobId);
        this.#leases.delete(jobId);
        cancelled += 1;
      }
    }
    return cancelled;
  }

  #claimDue(nowTick, ownerId = undefined) {
    nowTick = tick(nowTick, 'Scheduler current tick');
    const due = [...this.#jobs.values()]
      .filter((job) => job.dueTick <= nowTick && (ownerId === undefined || job.ownerId === ownerId) && !this.#leases.has(job.jobId))
      .sort((left, right) => left.dueTick - right.dueTick || left.jobId.localeCompare(right.jobId));
    return Object.freeze(due.map((job) => {
      const occurrenceId = `scheduled:${job.jobId}:${job.dueTick}`;
      this.#leases.set(job.jobId, occurrenceId);
      return Object.freeze({ ...job, occurrenceId });
    }));
  }

  claimDue(nowTick) {
    return this.#claimDue(nowTick);
  }

  claimDueForOwner(ownerId, nowTick) {
    ownerId = nonEmptyString(ownerId, 'Scheduled job owner ID');
    return this.#claimDue(nowTick, ownerId);
  }

  acknowledge(jobId, occurrenceId) {
    jobId = nonEmptyString(jobId, 'Scheduled job ID');
    occurrenceId = nonEmptyString(occurrenceId, 'Scheduled job occurrence ID');
    const job = this.#jobs.get(jobId);
    if (!job || this.#fired.has(jobId)) throw new TypeError(`Scheduled job ${jobId} is not pending.`);
    if (this.#leases.get(jobId) !== occurrenceId) throw new TypeError(`Scheduled job ${jobId} is not leased by ${occurrenceId}.`);
    this.#jobs.delete(jobId);
    this.#leases.delete(jobId);
    this.#fired.set(jobId, Object.freeze({ firedAtTick: job.dueTick, occurrenceId }));
    return Object.freeze({ ...job, occurrenceId });
  }

  release(jobId, occurrenceId) {
    jobId = nonEmptyString(jobId, 'Scheduled job ID');
    occurrenceId = nonEmptyString(occurrenceId, 'Scheduled job occurrence ID');
    if (this.#leases.get(jobId) !== occurrenceId) throw new TypeError(`Scheduled job ${jobId} is not leased by ${occurrenceId}.`);
    this.#leases.delete(jobId);
  }

  // Compatibility aliases retain the old read shape but no longer mark jobs
  // fired. Consumers must acknowledge only after their kernel action commits.
  takeDue(nowTick) { return this.claimDue(nowTick); }
  takeDueForOwner(ownerId, nowTick) { return this.claimDueForOwner(ownerId, nowTick); }

  exportSnapshot() {
    return Object.freeze({
      jobs: Object.freeze([...this.#jobs.values()].sort((left, right) => left.dueTick - right.dueTick || left.jobId.localeCompare(right.jobId))),
      fired: Object.freeze([...this.#fired.entries()]
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([jobId, entry]) => Object.freeze({ jobId, ...entry }))),
    });
  }

  static fromSnapshot(snapshot) {
    if (!snapshot || typeof snapshot !== 'object' || Array.isArray(snapshot)
      || Object.keys(snapshot).sort().join(',') !== 'fired,jobs'
      || !Array.isArray(snapshot.jobs) || !Array.isArray(snapshot.fired)) {
      throw new TypeError('Scheduler snapshot is invalid.');
    }
    const scheduler = new DeterministicScheduler();
    for (const job of snapshot.jobs) scheduler.schedule(job);
    for (const entry of snapshot.fired) {
      if (!entry || typeof entry !== 'object' || Array.isArray(entry)
        || Object.keys(entry).sort().join(',') !== 'firedAtTick,jobId,occurrenceId') {
        throw new TypeError('Scheduler fire ledger entry is invalid.');
      }
      const jobId = nonEmptyString(entry.jobId, 'Scheduler fire ledger job ID');
      if (scheduler.#jobs.has(jobId) || scheduler.#fired.has(jobId)) throw new TypeError(`Duplicate scheduler job ID ${jobId}.`);
      scheduler.#fired.set(jobId, Object.freeze({
        firedAtTick: tick(entry.firedAtTick, 'Scheduler fire tick'),
        occurrenceId: nonEmptyString(entry.occurrenceId, 'Scheduler fire occurrence ID'),
      }));
    }
    return scheduler;
  }
}
