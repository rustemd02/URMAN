import type { CommonJsonValue as JsonValue } from '../../content/generated/content-types.js';

export type { JsonValue };

export interface Command<TPayload extends JsonValue = JsonValue> {
  readonly type: string;
  readonly actionOccurrenceId: string;
  readonly payload: TPayload;
}

export interface DomainEvent<TPayload extends JsonValue = JsonValue> {
  readonly type: string;
  readonly payload: TPayload;
  readonly sequence: number;
  readonly transactionId: string;
}

export interface RuntimeState {
  readonly [key: string]: JsonValue;
}

export interface StateEffect {
  readonly op: 'state.set' | 'state.delete' | 'state.increment';
  readonly key: string;
  readonly value?: JsonValue;
  readonly delta?: number;
}

export interface ResourceClaimRequest {
  readonly resourceId: string;
  readonly ownerId: string;
  readonly mode: 'exclusive' | 'shared';
  readonly lifecycleScope: string;
}

export interface TransactionPlan {
  readonly effects?: readonly StateEffect[];
  readonly events?: readonly { readonly type: string; readonly payload: JsonValue }[];
  readonly claims?: readonly ResourceClaimRequest[];
  readonly releaseClaimOwnerIds?: readonly string[];
  readonly releaseClaimLifecycleScopes?: readonly string[];
  readonly value?: JsonValue;
  readonly rejection?: { readonly code: string; readonly message: string; readonly details?: JsonValue };
}

export interface CommandHandlerContext {
  readonly state: RuntimeState;
  select<T>(selector: (state: RuntimeState) => T): T;
}

export type CommandHandler = (command: Command, context: CommandHandlerContext) => TransactionPlan;

export type CommandResult =
  | { readonly status: 'committed'; readonly transactionId: string; readonly value: JsonValue }
  | { readonly status: 'rejected'; readonly error: RuntimeFailure };

export interface RuntimeFailureSnapshot {
  readonly code: string;
  readonly message: string;
  readonly details?: JsonValue;
}

export type CommandResultSnapshot =
  | { readonly status: 'committed'; readonly transactionId: string; readonly value: JsonValue }
  | { readonly status: 'rejected'; readonly error: RuntimeFailureSnapshot };

export interface DisposableSubscription { readonly disposed: boolean; dispose(): void }

export interface CapabilityLookup {
  require(protocolId: string, exactVersion: string): unknown;
}

export interface RuntimeResourceClaim {
  readonly resourceId: string;
  readonly entries: readonly ResourceClaimRequest[];
}

export interface RuntimeReadModel {
  readonly state: RuntimeState;
  readonly claims: readonly RuntimeResourceClaim[];
}

export interface RuntimeContext {
  select<T>(selector: (state: RuntimeState) => T): T;
  query<T>(selector: (model: RuntimeReadModel) => T): T;
  dispatch(command: Command): Promise<CommandResult>;
  subscribe(eventType: string, handler: (event: DomainEvent) => void): DisposableSubscription;
  readonly capabilities: CapabilityLookup;
}

export const CommandStatus: Readonly<{ Committed: 'committed'; Rejected: 'rejected' }>;
export const RuntimeErrorCode: Readonly<Record<'Preflight' | 'CommandRejected' | 'DuplicateOccurrence' | 'OccurrenceConflict' | 'ResourceConflict' | 'UnknownCommand', string>>;
export const RegistryErrorCode: Readonly<Record<'DuplicateRegistration' | 'InvalidRegistration' | 'IncompatibleExactVersion' | 'MissingRegistration', string>>;

export class RuntimeFailure extends Error {
  readonly code: string;
  readonly details?: JsonValue;
  constructor(code: string, message: string, details?: JsonValue);
}
export class PreflightError extends RuntimeFailure { constructor(message: string, details?: JsonValue) }
export class ConflictError extends RuntimeFailure { constructor(message: string, details?: JsonValue) }
export class UnknownCommandError extends RuntimeFailure { constructor(type: string) }
export class DuplicateOccurrence extends RuntimeFailure {
  readonly savedResult: CommandResult;
  constructor(actionOccurrenceId: string, savedResult: CommandResult);
}
export class OccurrenceConflict extends RuntimeFailure { constructor(actionOccurrenceId: string) }
