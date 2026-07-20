import assert from 'node:assert/strict';
import { access, readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';

const retiredPaths = [
  'src/data/chat_data.ts',
  'src/os/data/ChatProvider.ts',
  'src/os/apps/chat.ts',
  'src/os/data/chats/master_db.json',
];

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

test('DedOS has no legacy chat narrative owner, app descriptor, launcher or source import', async () => {
  await Promise.all(retiredPaths.map((file) => assert.rejects(access(file), { code: 'ENOENT' }, file)));

  const [os, registry, sources] = await Promise.all([
    readFile('src/os/core/OS.ts', 'utf8'),
    readFile('src/os/core/registry.ts', 'utf8'),
    sourceFiles('src'),
  ]);
  const shell = `${os}\n${registry}`;
  assert.doesNotMatch(shell, /urman\.oldpc:app\/chat|urman\.oldpc:launcher\/chat|renderChat|initChatInteractions|Старый мессенджер/);

  const legacyReference = /ChatProvider|chat_data|urman\.oldpc:app\/chat|urman\.oldpc:launcher\/chat|renderChat|initChatInteractions/;
  for (const file of sources) assert.doesNotMatch(await readFile(file, 'utf8'), legacyReference, file);
});
