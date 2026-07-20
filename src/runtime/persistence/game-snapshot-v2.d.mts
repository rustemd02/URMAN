import type { CompiledContentPack } from '../../content/generated/content-types.js';
import type { CapabilityDescriptor, CapabilityHost, CapabilitySnapshot } from '../capabilities/capability-host.mjs';
import type { JsonValue, CommandHandler } from '../contracts/runtime-contracts.mjs';
import type { RuntimeKernel, RuntimeKernelSnapshot } from '../kernel/runtime-kernel.mjs';
import type { CapabilityRegistry } from '../registries/runtime-registries.mjs';
import type { DeterministicScheduler, SchedulerSnapshot } from '../world/deterministic-scheduler.mjs';
import type { LogicalClock, LogicalClockSnapshot } from '../world/logical-clock.mjs';
import type { OwnerRngStreams, OwnerRngStreamsSnapshot } from '../world/seeded-rng.mjs';
import type { CampaignLock } from './campaign-lock.mjs';
import type { SnapshotCodecError } from './snapshot-errors.mjs';

export const GAME_SNAPSHOT_V2_SCHEMA_VERSION: 2;
export interface CapabilityBinding {
  readonly capabilityInstanceId: string;
  readonly protocolId: string;
  readonly exactVersion: string;
  readonly config: JsonValue;
  readonly claims: readonly { readonly resourceId: string; readonly mode: 'exclusive' | 'shared'; readonly lifecycleScope: string }[];
}
export interface GameSnapshotV2 {
  readonly snapshotSchemaVersion: 2;
  readonly savedSequence: number;
  readonly campaignLock: CampaignLock;
  readonly runtime: RuntimeKernelSnapshot;
  readonly world: { readonly clock: LogicalClockSnapshot; readonly rngStreams: OwnerRngStreamsSnapshot; readonly scheduler: SchedulerSnapshot };
  readonly capabilityBindings: readonly CapabilityBinding[];
  readonly capabilitySnapshots: readonly CapabilitySnapshot[];
}
export interface SnapshotCodecOptions { readonly pack: CompiledContentPack; readonly capabilityDescriptors?: readonly CapabilityDescriptor[] }
export function decodeGameSnapshotV2(snapshot: JsonValue, options: SnapshotCodecOptions): GameSnapshotV2;
export function tryDecodeGameSnapshotV2(snapshot: JsonValue, options: SnapshotCodecOptions):
  | { readonly ok: true; readonly snapshot: GameSnapshotV2 }
  | { readonly ok: false; readonly error: SnapshotCodecError };
export function encodeGameSnapshotV2(options: SnapshotCodecOptions & {
  readonly kernel: RuntimeKernel;
  readonly clock: LogicalClock;
  readonly rngStreams: OwnerRngStreams;
  readonly scheduler: DeterministicScheduler;
  readonly capabilityBindings?: readonly CapabilityBinding[];
  readonly capabilitySnapshots?: readonly CapabilitySnapshot[];
}): GameSnapshotV2;
export function restoreGameSnapshotV2(snapshot: JsonValue, options: SnapshotCodecOptions & {
  readonly handlers?: Readonly<Record<string, CommandHandler>> | ReadonlyMap<string, CommandHandler>;
  readonly capabilityRegistry: CapabilityRegistry;
  readonly startCapabilities?: boolean;
}): Promise<{
  readonly campaignLock: CampaignLock;
  readonly kernel: RuntimeKernel;
  readonly clock: LogicalClock;
  readonly rngStreams: OwnerRngStreams;
  readonly scheduler: DeterministicScheduler;
  readonly capabilityHost: CapabilityHost;
  readonly capabilityInstanceIds: readonly string[];
}>;
