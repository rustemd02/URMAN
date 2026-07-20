import type { JsonValue, ResourceClaimRequest, TransactionPlan } from '../contracts/runtime-contracts.mjs';
export interface ItemInstance { readonly itemId: string; readonly custodyOwnerId: string; readonly condition: JsonValue }
export type CustodyOperation =
  | { readonly op: 'claim' | 'transfer'; readonly itemId: string; readonly fromOwnerId: string; readonly toOwnerId: string }
  | { readonly op: 'consume'; readonly itemId: string; readonly ownerId: string };
export function planCustodyBatch(options: { readonly state: Record<string, JsonValue>; readonly operations: readonly CustodyOperation[]; readonly claimOwnerId: string; readonly claimLifecycleScope: string; readonly stateKey?: string; readonly eventType?: string }): TransactionPlan;
export function itemResourceClaims(operations: readonly CustodyOperation[], owner: { readonly ownerId: string; readonly lifecycleScope: string }): readonly ResourceClaimRequest[];
