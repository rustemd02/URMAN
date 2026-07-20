import type { JsonValue, TransactionPlan } from '../contracts/runtime-contracts.mjs';
export interface EvidenceClaim { readonly evidenceId: string; readonly claimantId: string; readonly sourceId: string; readonly provenanceChain: readonly string[]; readonly metadata: JsonValue }
export function planEvidenceClaim(options: { readonly state: Record<string, JsonValue>; readonly evidenceId: string; readonly claimantId: string; readonly sourceId: string; readonly metadata?: JsonValue; readonly stateKey?: string; readonly eventType?: string }): TransactionPlan;
