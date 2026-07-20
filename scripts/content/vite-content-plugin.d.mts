import type { Plugin } from 'vite';
import type { CompiledContentPack } from '../../src/content/generated/content-types.js';

export const VIRTUAL_CONTENT_ID: 'virtual:urman-content-catalog';
export const RESOLVED_VIRTUAL_CONTENT_ID: string;
export interface ViteContentOptions {
  readonly campaignPath: string;
  readonly moduleManifestPaths: readonly string[];
  readonly cwd?: string;
}
export function virtualCatalogSource(pack: CompiledContentPack): string;
export function createViteContentPlugin(options: ViteContentOptions): Plugin;
