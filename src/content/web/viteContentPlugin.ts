import type { Plugin } from 'vite';

import { createViteContentPlugin } from '../../../scripts/content/vite-content-plugin.mjs';

export interface ViteContentPluginOptions {
  readonly campaignPath: string;
  readonly moduleManifestPaths: readonly string[];
  readonly cwd?: string;
}

/**
 * Build-time adapter only. MM-54 owns production wiring and must provide the
 * explicit resolved campaign/module paths; this adapter performs no discovery.
 */
export function viteContentPlugin(options: ViteContentPluginOptions): Plugin {
  return createViteContentPlugin(options);
}
