import assert from 'node:assert/strict';
import test from 'node:test';

import { compileContent } from '../../../scripts/content/compile-content.mjs';

test('missing campaign selection is fatal and never creates an empty pack', async () => {
  const result = await compileContent({});

  assert.equal(result.ok, false);
  assert.equal('pack' in result, false);
  assert.deepEqual(result.diagnostics.map(({ code }) => code), ['MissingCampaignSelection']);
  assert.equal(result.diagnostics[0].jsonPointer, '/campaignPath');
});

test('an explicit nonexistent campaign path returns an actionable source diagnostic', async () => {
  const result = await compileContent({
    campaignPath: 'tests/content/compiler/fixtures/missing.json',
    moduleManifestPaths: ['tests/content/compiler/fixtures/valid/module/module.json'],
  });

  assert.equal(result.ok, false);
  assert.equal('pack' in result, false);
  assert.equal(result.diagnostics[0].code, 'MissingFile');
  assert.equal(result.diagnostics[0].sourcePath, 'tests/content/compiler/fixtures/missing.json');
  assert.match(result.diagnostics[0].message, /Cannot read selected content file/);
});
