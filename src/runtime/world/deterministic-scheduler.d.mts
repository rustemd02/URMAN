import type { JsonValue } from '../contracts/runtime-contracts.mjs';
export interface ScheduledJob { readonly jobId: string; readonly ownerId: string; readonly dueTick: number; readonly payload: JsonValue }
export interface ClaimedScheduledJob extends ScheduledJob { readonly occurrenceId: string }
export interface SchedulerSnapshot { readonly jobs: readonly ScheduledJob[]; readonly fired: readonly { readonly jobId: string; readonly firedAtTick: number; readonly occurrenceId: string }[] }
export class DeterministicScheduler {
  schedule(job: ScheduledJob): ScheduledJob;
  cancelOwner(ownerId: string): number;
  claimDue(nowTick: number): readonly ClaimedScheduledJob[];
  claimDueForOwner(ownerId: string, nowTick: number): readonly ClaimedScheduledJob[];
  acknowledge(jobId: string, occurrenceId: string): ClaimedScheduledJob;
  release(jobId: string, occurrenceId: string): void;
  takeDue(nowTick: number): readonly ClaimedScheduledJob[];
  takeDueForOwner(ownerId: string, nowTick: number): readonly ClaimedScheduledJob[];
  exportSnapshot(): SchedulerSnapshot;
  static fromSnapshot(snapshot: SchedulerSnapshot): DeterministicScheduler;
}
