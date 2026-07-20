import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';

async function sourceFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await sourceFiles(target));
    else if (/\.(?:mjs|mts|ts)$/.test(entry.name)) files.push(target);
  }
  return files.sort();
}

test('generic runtime has no campaign, browser, persistence or legacy imports', async () => {
  const files = await sourceFiles('src/runtime');
  assert.ok(files.length > 0);
  for (const file of files) {
    const source = await readFile(file, 'utf8');
    const isInjectedBrowserStorageBoundary = file.endsWith('runtime/persistence/production-persistence-gateway.mjs');
    assert.doesNotMatch(source, /(?:src\/)?(?:data|scenes|os)\//, file);
    // `document` is now a portable content kind (DocumentDefinition), so the
    // boundary must reject DOM access rather than a harmless identifier.
    if (isInjectedBrowserStorageBoundary) {
      assert.match(source, /browserWindow\.localStorage/, 'only the injected production gateway may adapt browser storage');
      assert.doesNotMatch(source, /\b(?:window|sessionStorage)\b|\b(?:globalThis|window)\.document\b|\bdocument\s*\.\s*(?:body|documentElement|createElement|querySelector(?:All)?|getElementById|addEventListener|removeEventListener)\b/, file);
    } else {
      assert.doesNotMatch(source, /\b(?:window|localStorage|sessionStorage)\b|\b(?:globalThis|window)\.document\b|\bdocument\s*\.\s*(?:body|documentElement|createElement|querySelector(?:All)?|getElementById|addEventListener|removeEventListener)\b/, file);
    }
    assert.doesNotMatch(source, /\/assets\//, file);
    assert.doesNotMatch(source, /\b(?:char_|clue_|quest_)[a-z0-9_]+/i, file);
    assert.doesNotMatch(source, /window\.URMAN/, file);
  }
});

test('runtime contracts expose query/dispatch/select/subscription but no mutable store API', async () => {
  const contracts = await readFile('src/runtime/contracts/runtime-contracts.d.mts', 'utf8');
  assert.match(contracts, /select<T>/);
  assert.match(contracts, /query<T>/);
  assert.match(contracts, /dispatch\(command/);
  assert.match(contracts, /subscribe\(eventType/);
  assert.match(contracts, /readonly lifecycleScope: string/);
  assert.match(contracts, /releaseClaimLifecycleScopes/);
  assert.doesNotMatch(contracts, /setState|mutableState|getMutable|store:/);
});
