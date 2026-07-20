import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { generateContentTypes, runTypeGenerator } from '../../../scripts/content/generate-content-types.mjs';

test('schema-derived TypeScript generation is byte-for-byte deterministic', async () => {
  const first = await generateContentTypes();
  const second = await generateContentTypes();

  assert.equal(first, second);
  assert.match(first, /export type CompiledContentPack =/);
  assert.match(first, /export type ContentCompilationResult =/);
  assert.match(first, /export type QuestDefinition =/);
  assert.match(first, /readonly nextNodeId: string \| null/);
  assert.match(first, /export type SceneDefinition = .*readonly targetDialogueId\?: CommonContentId/s);
  assert.doesNotMatch(first, /export type SceneDefinition = .*readonly interactions: readonly \(\(unknown\)\)\[\]/s);
});

test('check mode detects drift without writing the target', async () => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-types-'));
  const outputPath = path.join(directory, 'content-types.ts');
  await writeFile(outputPath, 'stale\n');

  await assert.rejects(() => runTypeGenerator({ check: true, outputPath }), /stale/);
  assert.equal(await readFile(outputPath, 'utf8'), 'stale\n');
});
