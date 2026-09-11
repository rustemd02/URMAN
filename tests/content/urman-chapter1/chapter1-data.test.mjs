import assert from 'node:assert/strict';
import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import { auditWorkspace } from '../../../scripts/content/compile-content.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const CHAPTER_DIR = path.join(ROOT, 'content/modules/urman-chapter1');
const CORE_DIR = path.join(ROOT, 'content/modules/urman-core');

async function readJson(relativePath) {
  return JSON.parse(await readFile(path.join(CHAPTER_DIR, relativePath), 'utf8'));
}

async function chapterDefinitions() {
  return readJson('definitions.json');
}

function byId(definitions, id) {
  const entry = definitions.find((definition) => definition.id === id);
  assert.ok(entry, `missing ${id}`);
  return entry;
}

function effectsIn(value) {
  const found = [];
  const visit = (candidate) => {
    if (Array.isArray(candidate)) {
      candidate.forEach(visit);
      return;
    }
    if (!candidate || typeof candidate !== 'object') return;
    if (typeof candidate.op === 'string') found.push(candidate);
    Object.values(candidate).forEach(visit);
  };
  visit(value);
  return found;
}

function targetSceneIds(scene) {
  return scene.interactions.flatMap(({ targetSceneId }) => targetSceneId ? [targetSceneId] : []);
}

function targetDialogueIds(scene) {
  return scene.interactions.flatMap(({ targetDialogueId }) => targetDialogueId ? [targetDialogueId] : []);
}

test('Chapter 1 core and portable module audit without a campaign manifest', async () => {
  const audit = await auditWorkspace({ cwd: ROOT, moduleSelection: 'urman.chapter1' });
  assert.equal(audit.ok, true, audit.diagnostics?.map(({ message }) => message).join('\n'));
  assert.deepEqual(audit.checked, { modules: 1, campaigns: 0 });

  const [chapterManifest, coreManifest] = await Promise.all([
    readJson('module.json'),
    JSON.parse(await readFile(path.join(CORE_DIR, 'module.json'), 'utf8')),
  ]);
  assert.deepEqual(chapterManifest.dependencies, []);
  assert.deepEqual(chapterManifest.sourceFiles, ['definitions.json', 'documents/arrival-photo-evidence.md']);
  assert.deepEqual(coreManifest.sourceFiles, []);
  assert.deepEqual(coreManifest.provides, []);
  assert.equal(JSON.stringify(coreManifest).match(/marat|kyrlay|kara-urman/i), null);
  assert.equal(JSON.stringify(chapterManifest).includes('urman.oldpc:'), false);
});

test('Chapter 1 keeps the accepted cast and does not turn folklore entities into ordinary characters', async () => {
  const definitions = await chapterDefinitions();
  const characterIds = definitions.filter(({ id = '' }) => id.includes(':character/')).map(({ id }) => id).sort();
  assert.deepEqual(characterIds, [
    'urman.chapter1:character/aidar',
    'urman.chapter1:character/alsu',
    'urman.chapter1:character/gulsina',
    'urman.chapter1:character/mansur',
    'urman.chapter1:character/marat',
    'urman.chapter1:character/naila',
    'urman.chapter1:character/rinat',
    'urman.chapter1:character/timur_hazrat',
  ]);
  assert.equal(characterIds.some((id) => /character\/(shurale|su_anasy|ubyr)/.test(id)), false);
  assert.deepEqual(byId(definitions, 'urman.chapter1:character/timur_hazrat').roleTags, ['timur-hazrat', 'moral-support']);
  assert.equal(byId(definitions, 'urman.chapter1:character/alsu').accessibilityDescription.translations.ru.includes('человеческий'), true);
  assert.equal(byId(definitions, 'urman.chapter1:character/rinat').accessibilityDescription.translations.ru.includes('не допустить'), true);
});

test('arrival, evidence route, Rinat causality, and hard cut are authored as one portable progression', async () => {
  const definitions = await chapterDefinitions();
  const arrival = byId(definitions, 'urman.chapter1:scene/arrival_vehicle_dusk');
  const house = byId(definitions, 'urman.chapter1:scene/house');
  const crossroad = byId(definitions, 'urman.chapter1:scene/crossroad_signs_inspect');
  const fap = byId(definitions, 'urman.chapter1:scene/fap_waiting_room_day');
  const fapDesk = byId(definitions, 'urman.chapter1:scene/fap_pressure_document_desk');
  const official = byId(definitions, 'urman.chapter1:scene/evidence-official-death');
  const internal = byId(definitions, 'urman.chapter1:scene/evidence-internal-register');
  const savedMessage = byId(definitions, 'urman.chapter1:scene/evidence-saved-message');
  const boundary = byId(definitions, 'urman.chapter1:scene/evidence-tatarwiki-boundary');
  const reread = byId(definitions, 'urman.chapter1:scene/evidence-tatarwiki-reread');
  const edgeSketch = byId(definitions, 'urman.chapter1:scene/evidence-edge-sketch');
  const ziratTurn = byId(definitions, 'urman.chapter1:scene/zirat-road');
  const finale = byId(definitions, 'urman.chapter1:scene/forest');
  const routeHint = byId(definitions, 'urman.chapter1:knowledge/route_kara_urman_edge_hint');
  const rinatWarning = byId(definitions, 'urman.chapter1:dialogue/rinat_internal_register');

  assert.equal(arrival.sceneType, 'route');
  assert.equal(arrival.title.translations.ru, 'Дорога к Кырлаю');
  assert.equal(definitions.some(({ id = '' }) => /scene\/(kfu|kazan|chapter1)$/.test(id)), false);
  assert.deepEqual(targetSceneIds(arrival), ['urman.chapter1:scene/house']);
  assert.deepEqual(targetSceneIds(house), ['urman.chapter1:scene/crossroad_signs_inspect']);
  assert.equal(targetSceneIds(crossroad).includes('urman.chapter1:scene/fap_waiting_room_day'), true);
  assert.deepEqual(targetSceneIds(fap), ['urman.chapter1:scene/fap_pressure_document_desk']);
  assert.deepEqual(targetSceneIds(fapDesk), ['urman.chapter1:scene/evidence-official-death']);
  assert.deepEqual(fapDesk.assetRefs, ['urman.chapter1:asset/loc_fap_pressure_document_desk']);
  assert.deepEqual(targetSceneIds(official), ['urman.chapter1:scene/evidence-internal-register']);
  assert.deepEqual(targetSceneIds(internal), ['urman.chapter1:scene/evidence-saved-message']);
  assert.deepEqual(targetDialogueIds(internal), ['urman.chapter1:dialogue/rinat_internal_register']);
  assert.deepEqual(targetSceneIds(savedMessage), ['urman.chapter1:scene/evidence-tatarwiki-boundary']);
  assert.deepEqual(targetSceneIds(boundary), ['urman.chapter1:scene/evidence-tatarwiki-reread']);
  assert.deepEqual(targetSceneIds(reread), ['urman.chapter1:scene/evidence-edge-sketch']);
  assert.equal(targetSceneIds(edgeSketch).includes('urman.chapter1:scene/zirat-road'), true);
  assert.equal(targetSceneIds(ziratTurn).includes('urman.chapter1:scene/forest'), true);
  for (const evidenceScene of [official, internal, savedMessage, boundary, reread, edgeSketch]) {
    assert.equal(evidenceScene.sceneType, 'presentation');
    assert.deepEqual(evidenceScene.assetRefs, []);
    assert.deepEqual(evidenceScene.textRefs, ['urman.chapter1:text/journal-source']);
  }
  assert.equal(official.onEnter.some((entry) => entry.op === 'knowledge.set-status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_marat_official_death_version' && entry.status === 'confirmed'), true);
  assert.equal(internal.onEnter.some((entry) => entry.op === 'knowledge.set-status' && entry.knowledgeId === 'urman.chapter1:knowledge/contradiction_marat_official_vs_internal' && entry.status === 'confirmed'), true);
  assert.equal(internal.interactions.some((interaction) => interaction.targetDialogueId === rinatWarning.id
    && interaction.conditions.some((entry) => entry.op === 'not'
      && entry.condition?.op === 'npc.state'
      && entry.condition.characterId === 'urman.chapter1:character/rinat'
      && entry.condition.stateKey === 'alerted'
      && entry.condition.value === true)), true);
  assert.equal(internal.interactions.some((interaction) => interaction.targetSceneId === savedMessage.id
    && interaction.conditions.some((entry) => entry.op === 'npc.state'
      && entry.characterId === 'urman.chapter1:character/rinat'
      && entry.stateKey === 'alerted'
      && entry.value === true)), true);
  assert.equal(savedMessage.onEnter.some((entry) => entry.op === 'knowledge.set-status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_marat_was_afraid_before_death' && entry.status === 'confirmed'), true);
  assert.equal(boundary.onEnter.some((entry) => entry.op === 'knowledge.set-status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_voice_answer_is_dangerous_hint' && entry.status === 'hypothesis'), true);
  assert.equal(reread.entryConditions.some((entry) => entry.op === 'npc.state' && entry.characterId === 'urman.chapter1:character/rinat' && entry.stateKey === 'alerted' && entry.value === true), true);
  assert.equal(reread.onEnter.some((entry) => entry.op === 'vocabulary.set-status' && entry.vocabularyId === 'urman.chapter1:vocabulary/tt_javap' && entry.status === 'confirmed'), true);
  assert.equal(reread.onEnter.some((entry) => entry.op === 'vocabulary.set-status' && entry.vocabularyId === 'urman.chapter1:vocabulary/tt_tavysh' && entry.status === 'confirmed'), true);
  assert.equal(edgeSketch.entryConditions.some((entry) => entry.op === 'knowledge.status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_folklore_as_survival_rule' && entry.status === 'hypothesis'), true);
  assert.equal(edgeSketch.entryConditions.some((entry) => entry.op === 'knowledge.status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_voice_answer_is_dangerous_hint' && entry.status === 'hypothesis'), true);
  assert.equal(edgeSketch.entryConditions.some((entry) => entry.op === 'beat.state' && entry.beatId === 'urman.chapter1:beat/language-reread' && entry.state === 'completed'), true);
  assert.equal(rinatWarning.nodes[0].conditions.some((entry) => entry.op === 'knowledge.status' && entry.knowledgeId === 'urman.chapter1:knowledge/contradiction_marat_official_vs_internal' && entry.status === 'confirmed'), true);
  assert.equal(rinatWarning.nodes[0].effects.some((entry) => entry.op === 'npc.set-state' && entry.characterId === 'urman.chapter1:character/rinat' && entry.stateKey === 'alerted' && entry.value === true), true);
  assert.equal(ziratTurn.entryConditions.some((entry) => entry.op === 'knowledge.status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_kara_urman_edge_is_rule_boundary'), true);
  assert.equal(ziratTurn.entryConditions.some((entry) => entry.op === 'npc.state' && entry.characterId === 'urman.chapter1:character/rinat' && entry.stateKey === 'alerted'), true);
  assert.equal(effectsIn(ziratTurn.onEnter).some((entry) => entry.op === 'beat.set-state' && entry.beatId === 'urman.chapter1:beat/rinat-visible-before-edge'), true);
  assert.deepEqual(routeHint.sourceIds, [
    'urman.chapter1:scene/evidence-edge-sketch',
    'urman.chapter1:dialogue/rinat_internal_register',
  ]);
  assert.deepEqual(finale.interactions.map(({ id }) => id), ['urman.chapter1:interaction/forest-rinat-intervention']);
  assert.equal(finale.onEnter.some((effect) => effect.op === 'knowledge.set-status'), false);
  assert.equal(finale.entryConditions.some((entry) => entry.op === 'beat.state' && entry.beatId === 'urman.chapter1:beat/rinat-visible-before-edge' && entry.state === 'completed'), true);
  assert.equal(ziratTurn.entryConditions.some((entry) => entry.op === 'beat.state' && entry.beatId === 'urman.chapter1:beat/language-reread' && entry.state === 'completed'), true);
  assert.equal(finale.onEnter.some((entry) => entry.op === 'audio.request' && entry.assetId === 'urman.chapter1:asset/audio-marat-voice'), true);
  assert.equal(finale.onEnter.some((entry) => entry.op === 'audio.request' && entry.assetId === 'urman.chapter1:asset/audio-rinat-interruption'), true);
});

test('the voice clue remains a hypothesis and the final rule is confirmed only by the finale', async () => {
  const definitions = await chapterDefinitions();
  const hypothesis = byId(definitions, 'urman.chapter1:knowledge/clue_voice_answer_is_dangerous_hint');
  const finalRule = byId(definitions, 'urman.chapter1:knowledge/clue_do_not_answer_rule');
  const finalRuleWrites = effectsIn(definitions).filter((entry) => entry.op === 'knowledge.set-status' && entry.knowledgeId === finalRule.id);
  const finalRuleText = byId(definitions, 'urman.chapter1:text/finale-rinat-warning');

  assert.equal(hypothesis.kind, 'hypothesis');
  assert.equal(hypothesis.initialStatus, 'hidden');
  assert.equal(finalRule.initialStatus, 'hidden');
  assert.deepEqual(finalRuleWrites, [{ op: 'knowledge.set-status', knowledgeId: finalRule.id, status: 'confirmed' }]);
  assert.equal(finalRuleWrites.every((entry) => byId(definitions, 'urman.chapter1:scene/forest').interactions[0].effects.includes(entry)), true);
  assert.equal(finalRuleText.value.translations.ru, 'Не отвечай.');

  const chapterSources = await Promise.all([
    readFile(path.join(CHAPTER_DIR, 'definitions.json'), 'utf8'),
    ...[
      'arrival-photo-evidence.md',
    ].map((name) => readFile(path.join(CHAPTER_DIR, 'documents', name), 'utf8')),
  ]);
  assert.deepEqual([...chapterSources.join('\n').matchAll(/Не отвечай/g)].map((match) => match.index).length, 2);
  assert.deepEqual(definitions.filter(({ id = '' }) => id.includes(':document/')), []);
  assert.equal(chapterSources.at(-1).includes('id: urman.chapter1:document/arrival-photo-evidence'), true);
});

test('vocabulary has a mutable learning state, reread targets, and logical asset references without public asset paths', async () => {
  const definitions = await chapterDefinitions();
  const vocabulary = definitions.filter(({ id = '' }) => id.includes(':vocabulary/'));
  assert.equal(vocabulary.length, 6);
  for (const entry of vocabulary) {
    assert.equal(entry.initialStatus, 'unknown');
    assert.ok(entry.rereadTargets.length > 0, `${entry.id} needs a reread target`);
    assert.ok(entry.applicationTargets.length > 0, `${entry.id} needs an application target`);
    assert.equal(entry.consultantReview.status, 'pending');
  }

  const chapterManifest = await readJson('module.json');
  const assets = definitions.filter(({ id = '' }) => id.includes(':asset/'));
  assert.deepEqual(assets.map(({ id }) => id).sort(), [...chapterManifest.assets].sort());
  for (const asset of assets) {
    assert.equal(asset.file.startsWith('logical/'), true, `${asset.id} must use a logical reference token`);
    assert.equal(asset.file.includes('/assets/'), false);
    await access(path.join(CHAPTER_DIR, asset.file));
  }
});
