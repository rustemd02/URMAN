import assert from 'node:assert/strict';
import test from 'node:test';

import {
  createViteContentPlugin,
  RESOLVED_VIRTUAL_CONTENT_ID,
  VIRTUAL_CONTENT_ID,
} from '../../../scripts/content/vite-content-plugin.mjs';

const VALID_OPTIONS = {
  campaignPath: 'tests/content/compiler/fixtures/valid/campaign/campaign.json',
  moduleManifestPaths: ['tests/content/compiler/fixtures/valid/module/module.json'],
};

test('Vite adapter publishes only a compiled immutable virtual catalog', async () => {
  const plugin = createViteContentPlugin(VALID_OPTIONS);
  await plugin.buildStart();

  assert.equal(plugin.resolveId(VIRTUAL_CONTENT_ID), RESOLVED_VIRTUAL_CONTENT_ID);
  const source = plugin.load(RESOLVED_VIRTUAL_CONTENT_ID);
  assert.match(source, /const pack = deepFreeze/);
  assert.match(source, /campaignFingerprint/);
  assert.doesNotMatch(source, /node:fs|readFile|frontmatter|compileContent/);
});

test('Vite adapter fails before exposing a catalog when explicit selection is invalid', async () => {
  const plugin = createViteContentPlugin({ moduleManifestPaths: [] });
  await assert.rejects(() => plugin.buildStart(), /MissingCampaignSelection/);
  assert.throws(() => plugin.load(RESOLVED_VIRTUAL_CONTENT_ID), /before buildStart/);
});

test('unrelated Vite IDs are ignored', () => {
  const plugin = createViteContentPlugin(VALID_OPTIONS);
  assert.equal(plugin.resolveId('virtual:other'), null);
  assert.equal(plugin.load('\0virtual:other'), null);
});
