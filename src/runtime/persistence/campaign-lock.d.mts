import type { CompiledContentPack } from '../../content/generated/content-types.js';
export interface CampaignLock {
  readonly campaignId: string;
  readonly campaignExactVersion: string;
  readonly modules: readonly { readonly moduleId: string; readonly exactVersion: string; readonly sha256: string }[];
  readonly capabilities: readonly { readonly protocolId: string; readonly exactVersion: string }[];
  readonly campaignFingerprint: string;
}
export function createCampaignLock(pack: CompiledContentPack): CampaignLock;
export function normalizeCampaignLock(lock: CampaignLock): CampaignLock;
export function assertCampaignLockMatches(lock: CampaignLock, pack: CompiledContentPack): CampaignLock;
