import { defineConfig } from 'vite';
import { viteContentPlugin } from './src/content/web/viteContentPlugin';
import { viteContentLabPlugin } from './src/lab/viteContentLabPlugin';

/**
 * Production starts from one explicit authored campaign. Discovery is useful
 * to authoring tools only and must never decide what the browser runs.
 */
export default defineConfig({
  plugins: [viteContentPlugin({
    campaignPath: 'content/campaigns/urman.chapter1/campaign.json',
    moduleManifestPaths: [
      'content/modules/urman-core/module.json',
      'content/modules/urman-chapter1/module.json',
      'content/modules/urman-oldpc/module.json',
    ],
  }), viteContentLabPlugin()],
});
