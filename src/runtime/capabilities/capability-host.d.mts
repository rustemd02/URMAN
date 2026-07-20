import type { JsonValue, RuntimeContext, TransactionPlan } from '../contracts/runtime-contracts.mjs';
import type { LogicalClock } from '../world/logical-clock.mjs';
import type { OwnerRngStreams, SeededRngStream } from '../world/seeded-rng.mjs';
import type { ClaimedScheduledJob, DeterministicScheduler, ScheduledJob } from '../world/deterministic-scheduler.mjs';
export interface CapabilitySnapshot { readonly capabilityInstanceId: string; readonly protocolId: string; readonly exactVersion: string; readonly stateSchemaVersion: number; readonly state: JsonValue }
export interface CapabilityPresentationSubscription { readonly disposed: boolean; dispose(): void }
export interface CapabilityPresentationPort { render(): JsonValue; handle(input: JsonValue): JsonValue; subscribe(listener: (model: JsonValue) => void): CapabilityPresentationSubscription }
export interface CapabilitySession {
  restore?(state: JsonValue): void;
  start?(): void;
  handle?(input: JsonValue): JsonValue;
  render?(): JsonValue;
  subscribe?(listener: (model: JsonValue) => void): CapabilityPresentationSubscription;
  snapshot?(): JsonValue;
  stop?(): void;
  dispose?(): void;
}
export interface CapabilityDescriptor {
  readonly protocolId: string;
  readonly exactVersion: string;
  readonly stateSchemaVersion: number;
  validateConfig(config: JsonValue): true | void;
  create(options: { readonly capabilityInstanceId: string; readonly config: JsonValue; readonly context: {
    readonly runtime: RuntimeContext;
    readonly clock: { now(): number };
    readonly rng: SeededRngStream;
    readonly scheduler: { schedule(job: Omit<ScheduledJob, 'ownerId'>): ScheduledJob; claimDue(): readonly ClaimedScheduledJob[]; acknowledge(jobId: string, occurrenceId: string): ClaimedScheduledJob; release(jobId: string, occurrenceId: string): void; takeDue(): readonly ClaimedScheduledJob[] };
    readonly claims: { requested(): readonly unknown[] };
    subscribe: RuntimeContext['subscribe'];
    readonly cleanup: { add(disposer: () => void): { dispose(): void } };
  } }): CapabilitySession;
}
export class CapabilityHost {
  constructor(options: { readonly runtimeContext: RuntimeContext; readonly clock: LogicalClock; readonly rngStreams: OwnerRngStreams; readonly scheduler: DeterministicScheduler; readonly descriptors?: readonly CapabilityDescriptor[] });
  register(descriptor: CapabilityDescriptor): this;
  createSession(options: { readonly capabilityInstanceId: string; readonly protocolId: string; readonly exactVersion: string; readonly config: JsonValue; readonly claims?: readonly { readonly resourceId: string; readonly mode: 'exclusive' | 'shared'; readonly lifecycleScope: string }[]; readonly snapshot?: CapabilitySnapshot }): { readonly capabilityInstanceId: string; readonly protocolId: string; readonly exactVersion: string };
  restoreSession(capabilityInstanceId: string, snapshot: CapabilitySnapshot): void;
  startSession(capabilityInstanceId: string): void;
  handle(capabilityInstanceId: string, input: JsonValue): JsonValue;
  presentation(capabilityInstanceId: string): CapabilityPresentationPort;
  snapshotSession(capabilityInstanceId: string): CapabilitySnapshot;
  claimPlan(capabilityInstanceId: string): Pick<TransactionPlan, 'claims'>;
  stopSession(capabilityInstanceId: string): Pick<TransactionPlan, 'releaseClaimOwnerIds'>;
  disposeSession(capabilityInstanceId: string): Pick<TransactionPlan, 'releaseClaimOwnerIds'>;
  stopWithClaimRelease(capabilityInstanceId: string, commitCleanup: (plan: Pick<TransactionPlan, 'releaseClaimOwnerIds'>) => Promise<{ readonly status: string }> | { readonly status: string }): Promise<{ readonly status: string }>;
  disposeWithClaimRelease(capabilityInstanceId: string, commitCleanup: (plan: Pick<TransactionPlan, 'releaseClaimOwnerIds'>) => Promise<{ readonly status: string }> | { readonly status: string }): Promise<{ readonly status: string }>;
  questLifecycle(): { prepareCleanup(input: { readonly capabilityInstanceIds: readonly string[]; readonly reason: string }): Pick<TransactionPlan, 'releaseClaimOwnerIds'>; finalizeCommitted(cleanup: { readonly capabilityInstanceIds: readonly string[] }, result: { readonly status: string }, options?: { readonly dispose?: boolean }): { readonly capabilityInstanceIds: readonly string[]; readonly finalized: true } };
  finalizeCommittedCleanup(cleanup: { readonly capabilityInstanceIds: readonly string[] }, result: { readonly status: string }, options?: { readonly dispose?: boolean }): { readonly capabilityInstanceIds: readonly string[]; readonly finalized: true };
  cleanupPlan(capabilityInstanceId: string): Pick<TransactionPlan, 'releaseClaimOwnerIds'>;
}
