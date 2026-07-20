import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const sourcePath = new URL('../../../../src/scenes/ComputerScene.ts', import.meta.url);
const osPath = new URL('../../../../src/os/core/OS.ts', import.meta.url);
const registryPath = new URL('../../../../src/os/core/registry.ts', import.meta.url);

test('ComputerScene exposes a composition-root injection seam and delegates old-PC UI lifecycle to DedOS', async () => {
  const [scene, os, registry] = await Promise.all([readFile(sourcePath, 'utf8'), readFile(osPath, 'utf8'), readFile(registryPath, 'utf8')]);
  assert.match(scene, /interface ComputerSceneOptions[\s\S]*oldPcHub\?: OldPcHubController/);
  assert.match(scene, /this\.os = this\.createDedOS\(\{ oldPcHub: this\.oldPcHub \}\)/);
  assert.match(scene, /this\.os\?\.destroy\(\)/);
  assert.doesNotMatch(scene, /game\.state|GameState|window\.URMAN|localStorage/);
  assert.match(os, /data-oldpc-development-guard="missing-capability"/);
  assert.match(os, /initBrowser\(root, options\.oldPcHub!\)/);
  assert.match(os, /urman\.oldpc:app\/archive_search/);
  assert.match(registry, /urman\.oldpc:app\/archive_search/);
  assert.doesNotMatch(`${os}\n${registry}`, /urman\.oldpc:app\/archive-hub/);
});
