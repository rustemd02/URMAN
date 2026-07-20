import type { CapabilityRegistry } from '../registries/runtime-registries.mjs';
import type {
  Command,
  CommandHandler,
  CommandResult,
  CommandResultSnapshot,
  DisposableSubscription,
  DomainEvent,
  RuntimeReadModel,
  RuntimeContext,
  RuntimeState,
} from '../contracts/runtime-contracts.mjs';

export interface RuntimeKernelOptions {
  readonly initialState?: RuntimeState;
  readonly handlers?: Readonly<Record<string, CommandHandler>> | ReadonlyMap<string, CommandHandler>;
  readonly capabilityRegistry: CapabilityRegistry;
}

export interface RuntimeKernelSnapshotClaim {
  readonly resourceId: string;
  readonly ownerId: string;
  readonly mode: 'exclusive' | 'shared';
  readonly lifecycleScope: string;
}

export interface RuntimeKernelSnapshot {
  readonly state: RuntimeState;
  readonly claims: readonly {
    readonly resourceId: string;
    readonly entries: readonly RuntimeKernelSnapshotClaim[];
  }[];
  readonly occurrences: readonly {
    readonly actionOccurrenceId: string;
    readonly fingerprint: string;
    readonly result: CommandResultSnapshot;
  }[];
  readonly eventSequence: number;
}

export interface RuntimeKernelRestoreOptions {
  readonly handlers?: Readonly<Record<string, CommandHandler>> | ReadonlyMap<string, CommandHandler>;
  readonly capabilityRegistry: CapabilityRegistry;
}

export class RuntimeKernel {
  constructor(options: RuntimeKernelOptions);
  static fromSnapshot(snapshot: RuntimeKernelSnapshot, options: RuntimeKernelRestoreOptions): RuntimeKernel;
  readonly context: RuntimeContext;
  select<T>(selector: (state: RuntimeState) => T): T;
  query<T>(selector: (model: RuntimeReadModel) => T): T;
  subscribe(eventType: string, handler: (event: DomainEvent) => void): DisposableSubscription;
  dispatch(command: Command): Promise<CommandResult>;
  exportSnapshot(): RuntimeKernelSnapshot;
}
