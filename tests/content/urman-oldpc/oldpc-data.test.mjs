import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { compileContent } from '../../../scripts/content/compile-content.mjs';
import { validateManifestClosure } from '../../../src/runtime/resolvers/content-resolvers.mjs';

const CHAPTER_MODULE = 'content/modules/urman-chapter1/module.json';
const OLD_PC_MODULE = 'content/modules/urman-oldpc/module.json';

async function oldPcPack() {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'urman-oldpc-content-'));
  const campaignPath = path.join(directory, 'campaign.json');
  await writeFile(campaignPath, `${JSON.stringify({
    schemaVersion: 1,
    id: 'urman.oldpc.fixture',
    exactVersion: '1.0.0',
    entrypoint: 'urman.chapter1:scene/arrival_vehicle_dusk',
    modules: [
      { moduleId: 'urman.chapter1', exactVersion: '1.0.0' },
      { moduleId: 'urman.oldpc', exactVersion: '1.0.0' },
    ],
    roleBindings: {
      aidar: 'urman.chapter1:character/aidar',
      alsu: 'urman.chapter1:character/alsu',
      gulsina: 'urman.chapter1:character/gulsina',
      mansur: 'urman.chapter1:character/mansur',
      marat: 'urman.chapter1:character/marat',
      naila: 'urman.chapter1:character/naila',
      rinat: 'urman.chapter1:character/rinat',
      'timur-hazrat': 'urman.chapter1:character/timur_hazrat',
    },
    capabilityRequirements: [],
    narrativeOrder: ['urman.chapter1:scene/arrival_vehicle_dusk'],
    invariants: [],
  })}\n`);
  try {
    const compiled = await compileContent({ campaignPath, moduleManifestPaths: [CHAPTER_MODULE, OLD_PC_MODULE] });
    assert.equal(compiled.ok, true, JSON.stringify(compiled.diagnostics));
    return compiled.pack;
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
}

function byId(records, id) {
  const record = records.find((entry) => entry.id === id);
  assert.ok(record, `missing ${id}`);
  return record;
}

test('urman.oldpc compiles ten full-body records with normalized metadata and canonical module IDs', async () => {
  const pack = await oldPcPack();
  const documents = pack.registries.documents.filter(({ id }) => id.startsWith('urman.oldpc:document/'));
  assert.equal(documents.length, 10);
  assert.deepEqual(documents.map(({ id }) => id).sort(), [
    'urman.oldpc:document/doc_household_misc_niva_receipt',
    'urman.oldpc:document/doc_kara_urman_edge_sketch',
    'urman.oldpc:document/doc_marat_official_death_notice',
    'urman.oldpc:document/msg_mansur_unsent_note',
    'urman.oldpc:document/msg_marat_saved_last_normal',
    'urman.oldpc:document/rec_household_line_mansur',
    'urman.oldpc:document/rec_internal_accounting_damaged',
    'urman.oldpc:document/rec_marat_case_register_conflict',
    'urman.oldpc:document/rec_violations_compensation_summary',
    'urman.oldpc:document/tw_shurale_urman_boundary',
  ]);
  for (const document of documents) {
    assert.match(document.bodyMarkdown, /\S/);
    assert.equal(document.oldPc?.canonStatus.length > 0, true);
    assert.equal(document.oldPc?.reliability.length > 0, true);
    assert.equal(document.oldPc?.searchTerms.length > 0, true);
    assert.equal(document.oldPc?.suggestedTerms.length > 0, true);
    assert.match(document.id, /^urman\.oldpc:document\//);
  }
  assert.match(byId(documents, 'urman.oldpc:document/doc_marat_official_death_notice').bodyMarkdown, /внезапная сердечная недостаточность/u);
  assert.match(byId(documents, 'urman.oldpc:document/rec_marat_case_register_conflict').bodyMarkdown, /граница \/ ответил/u);
  assert.match(byId(documents, 'urman.oldpc:document/msg_marat_saved_last_normal').bodyMarkdown, /ждёт, когда ты ответишь/u);
  assert.match(byId(documents, 'urman.oldpc:document/tw_shurale_urman_boundary').bodyMarkdown, /место, где действуют старые правила/u);
});

test('old-PC records have one-way canonical presentation links and typed unlock conditions', async () => {
  const pack = await oldPcPack();
  const closure = validateManifestClosure(pack);
  const documents = pack.registries.documents;
  const assets = pack.registries.assets.filter(({ id }) => id.startsWith('urman.oldpc:asset/'));
  assert.equal(assets.length, 14);
  assert.equal(new Set(assets.map(({ id }) => id)).size, 14, 'logical asset IDs are exact and unique');
  for (const asset of assets) {
    assert.match(asset.file, /^logical\/[a-z0-9._-]+\.ref$/);
    assert.equal(asset.file.includes('/assets/'), false);
  }
  assert.equal(closure.assetIds.includes('urman.oldpc:asset/ui_old_pc_desktop_base'), true);
  assert.equal(closure.assetIds.includes('urman.oldpc:asset/ui-old-pc-desktop-base'), false, 'legacy hyphenated asset IDs are forbidden');
  const expectedPresentation = new Map([
    ['urman.oldpc:document/doc_marat_official_death_notice', 'urman.chapter1:scene/evidence-official-death'],
    ['urman.oldpc:document/rec_marat_case_register_conflict', 'urman.chapter1:scene/evidence-internal-register'],
    ['urman.oldpc:document/msg_marat_saved_last_normal', 'urman.chapter1:scene/evidence-saved-message'],
    ['urman.oldpc:document/tw_shurale_urman_boundary', 'urman.chapter1:scene/evidence-tatarwiki-boundary'],
    ['urman.oldpc:document/doc_kara_urman_edge_sketch', 'urman.chapter1:scene/evidence-edge-sketch'],
  ]);
  for (const [documentId, sceneId] of expectedPresentation) {
    const document = byId(documents, documentId);
    assert.deepEqual(document.openEffects, [{ op: 'scene.request', sceneId }]);
  }
  const mapped = new Set(expectedPresentation.keys());
  for (const document of documents.filter(({ id }) => id.startsWith('urman.oldpc:document/'))) {
    if (!mapped.has(document.id)) assert.deepEqual(document.openEffects, []);
  }
  const register = byId(documents, 'urman.oldpc:document/rec_marat_case_register_conflict');
  assert.deepEqual(register.accessConditions, [{
    op: 'knowledge.status',
    knowledgeId: 'urman.chapter1:knowledge/clue_marat_official_death_version',
    status: 'confirmed',
  }, {
    op: 'npc.state',
    characterId: 'urman.chapter1:character/naila',
    stateKey: 'record_access_granted',
    value: true,
  }]);
  const edge = byId(documents, 'urman.oldpc:document/doc_kara_urman_edge_sketch');
  assert.equal(edge.accessConditions[0].op, 'all');
  assert.equal(documents.filter(({ id }) => id.startsWith('urman.oldpc:document/')).every(({ assetRefs }) => assetRefs.length > 0), true);
  assert.equal(JSON.stringify(pack.registries.scenes), JSON.stringify(pack.registries.scenes).replaceAll('внезапная сердечная недостаточность', ''));
});
