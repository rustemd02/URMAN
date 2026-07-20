import type { JsonValue, RuntimeContext } from '../../contracts/runtime-contracts.mjs';
import type { CapabilityDescriptor, CapabilityPresentationSubscription } from '../../capabilities/capability-host.mjs';
import type { OldPcContentCatalog, OldPcDocument } from './oldpc-content-catalog.mjs';

export interface OldPcSnapshot {
  readonly activeDocumentId: string | null;
  readonly activeSection: string;
  readonly nextActionSequence: number;
  readonly query: string;
  readonly savedDocumentIds: readonly string[];
}
export interface OldPcPendingAction {
  readonly status: 'pending';
  readonly actionOccurrenceId: string;
  readonly command: { readonly type: string; readonly actionOccurrenceId: string; readonly payload: JsonValue };
}
export class OldPcCapabilitySession {
  constructor(options: {
    readonly runtimeContext: RuntimeContext;
    readonly contentRegistry: ConstructorParameters<typeof OldPcContentCatalog>[0]['contentRegistry'];
    readonly moduleId: string;
    readonly capabilityInstanceId: string;
    readonly isAccessible?: (document: OldPcDocument, state: Readonly<Record<string, JsonValue>>) => boolean;
    readonly commandTypes?: Readonly<Record<'search' | 'open' | 'save', string>>;
    readonly snapshot?: OldPcSnapshot;
  });
  readonly disposed: boolean;
  start(): void;
  restore(snapshot: OldPcSnapshot): void;
  snapshot(): OldPcSnapshot;
  subscribe(listener: (model: Readonly<Record<string, unknown>>) => void): CapabilityPresentationSubscription;
  render(): Readonly<Record<string, unknown>>;
  handle(input: JsonValue): OldPcPendingAction | Readonly<Record<string, unknown>>;
  stop(): void;
  dispose(): void;
}
export function createOldPcCapabilityDescriptor(options: {
  readonly contentRegistry: ConstructorParameters<typeof OldPcContentCatalog>[0]['contentRegistry'];
  readonly moduleId: string;
  readonly protocolId: string;
  readonly exactVersion?: string;
  readonly stateSchemaVersion?: number;
  readonly isAccessible?: (document: OldPcDocument, state: Readonly<Record<string, JsonValue>>) => boolean;
  readonly commandTypes?: Readonly<Record<'search' | 'open' | 'save', string>>;
}): CapabilityDescriptor;
