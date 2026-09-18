import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const CHAPTER_DIR = path.join(ROOT, 'content/modules/urman-chapter1');
const CORE_DIR = path.join(ROOT, 'content/modules/urman-core');
const runFile = promisify(execFile);

async function readJson(relativePath) {
  return JSON.parse(await readFile(path.join(CHAPTER_DIR, relativePath), 'utf8'));
}

async function chapterDefinitions() {
  const manifest = await readJson('module.json');
  const sources = await Promise.all(manifest.sourceFiles.filter((file) => file.endsWith('.json')).map(readJson));
  return sources.flatMap((source) => Array.isArray(source) ? source : [source]);
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

test('Chapter 1 source references validate with the production C# content catalog', async () => {
  // Chapter and archive documents share campaign references. The retired JS
  // module-only audit treats these as undeclared imports and cannot validate
  // the production catalog. Use the same validator as the native content CLI;
  // this command only reads sources and never builds or replaces a game pack.
  const dotnet = path.join(ROOT, '.tools/dotnet/dotnet');
  const cli = path.join(ROOT, 'tools-dotnet/Urman.ContentCli/bin/Debug/net10.0/Urman.ContentCli.dll');
  await access(cli);
  const { stdout } = await runFile(dotnet, [cli, 'validate', '--root', ROOT], { cwd: ROOT });
  assert.match(stdout, /checked \d+ modules, \d+ campaigns/);

  const [chapterManifest, coreManifest] = await Promise.all([
    readJson('module.json'),
    JSON.parse(await readFile(path.join(CORE_DIR, 'module.json'), 'utf8')),
  ]);
  assert.deepEqual(chapterManifest.dependencies, []);
  assert.equal(chapterManifest.sourceFiles.includes('documents/arrival-photo-evidence.md'), true);
  assert.equal(chapterManifest.sourceFiles.includes('documents/arrival-mother-message.md'), true);
  await Promise.all(chapterManifest.sourceFiles.map((file) => access(path.join(CHAPTER_DIR, file))));
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
    'urman.chapter1:character/razilya',
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
  const approach = byId(definitions, 'urman.chapter1:scene/forest-approach');
  const finale = byId(definitions, 'urman.chapter1:scene/forest');
  const routeHint = byId(definitions, 'urman.chapter1:knowledge/route_kara_urman_edge_hint');
  const rinatWarning = byId(definitions, 'urman.chapter1:dialogue/rinat_internal_register');

  assert.equal(arrival.sceneType, 'route');
  assert.equal(arrival.title.translations.ru, 'Дорога в Кара-Урман');
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
  assert.equal(targetSceneIds(ziratTurn).includes(approach.id), true);
  assert.deepEqual(targetSceneIds(approach), [finale.id]);
  for (const evidenceScene of [official, internal, savedMessage, boundary, reread, edgeSketch]) {
    assert.equal(evidenceScene.sceneType, 'presentation');
    assert.deepEqual(evidenceScene.assetRefs, []);
    assert.deepEqual(evidenceScene.textRefs, ['urman.chapter1:text/journal-source']);
  }
  for (const evidenceScene of [official, internal, savedMessage, boundary, reread, edgeSketch]) {
    assert.deepEqual(evidenceScene.onEnter, [], `${evidenceScene.id} must not replace reading or comparing its source`);
  }
  assert.equal(internal.interactions.some((interaction) => interaction.targetDialogueId === rinatWarning.id
    && effectsIn(interaction.conditions).some((entry) => entry.op === 'not'
      && entry.condition?.op === 'npc.state'
      && entry.condition.characterId === 'urman.chapter1:character/rinat'
      && entry.condition.stateKey === 'alerted'
      && entry.condition.value === true)), true);
  assert.equal(internal.interactions.some((interaction) => interaction.targetSceneId === savedMessage.id
    && interaction.conditions.some((entry) => entry.op === 'npc.state'
      && entry.characterId === 'urman.chapter1:character/rinat'
      && entry.stateKey === 'alerted'
      && entry.value === true)), true);
  assert.equal(reread.entryConditions.some((entry) => entry.op === 'npc.state' && entry.characterId === 'urman.chapter1:character/rinat' && entry.stateKey === 'alerted' && entry.value === true), true);
  const comparisons = byId(definitions, 'urman.chapter1:scene/investigation-journal');
  const voiceLink = comparisons.interactions.find(({ id }) => id === 'urman.chapter1:interaction/compare-voice-link');
  assert.deepEqual(voiceLink.journalAction.sourceIds, [
    'urman.oldpc:document/msg_marat_saved_last_normal', 'urman.oldpc:document/tw_shurale_urman_boundary',
  ]);
  for (const word of ['tt_javap', 'tt_tavysh']) {
    assert.equal(voiceLink.effects.some((entry) => entry.op === 'vocabulary.set-status'
      && entry.vocabularyId === `urman.chapter1:vocabulary/${word}` && entry.status === 'confirmed'), true);
  }
  const rereadChoice = byId(definitions, 'urman.chapter1:scene/investigation-reread').interactions
    .find(({ id }) => id === 'urman.chapter1:interaction/compare-reread-response');
  assert.equal(rereadChoice.conditions.some((entry) => entry.op === 'beat.state'
    && entry.beatId === 'urman.chapter1:beat/boundary-source-reopened' && entry.state === 'completed'), true);
  assert.equal(edgeSketch.entryConditions.some((entry) => entry.op === 'knowledge.status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_folklore_as_survival_rule' && entry.status === 'hypothesis'), true);
  assert.equal(edgeSketch.entryConditions.some((entry) => entry.op === 'knowledge.status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_voice_answer_is_dangerous_hint' && entry.status === 'hypothesis'), true);
  assert.equal(edgeSketch.entryConditions.some((entry) => entry.op === 'beat.state' && entry.beatId === 'urman.chapter1:beat/language-reread' && entry.state === 'completed'), true);
  assert.equal(rinatWarning.nodes[0].conditions.some((entry) => entry.op === 'knowledge.status' && entry.knowledgeId === 'urman.chapter1:knowledge/contradiction_marat_official_vs_internal' && entry.status === 'confirmed'), true);
  assert.equal(effectsIn(rinatWarning.nodes[0].effects).some((entry) => entry.op === 'npc.set-state' && entry.stateKey === 'alerted'), false);
  const presentCategory = rinatWarning.nodes[0].choices.find(({ id }) => id === 'present-category');
  assert.equal(presentCategory.effects.some((entry) => entry.op === 'npc.set-state' && entry.characterId === 'urman.chapter1:character/rinat' && entry.stateKey === 'alerted' && entry.value === true), true);
  assert.equal(ziratTurn.entryConditions.some((entry) => entry.op === 'knowledge.status' && entry.knowledgeId === 'urman.chapter1:knowledge/clue_kara_urman_edge_is_rule_boundary'), true);
  assert.equal(ziratTurn.entryConditions.some((entry) => entry.op === 'npc.state' && entry.characterId === 'urman.chapter1:character/rinat' && entry.stateKey === 'alerted'), true);
  assert.equal(effectsIn(ziratTurn.onEnter).some((entry) => entry.op === 'beat.set-state' && entry.beatId === 'urman.chapter1:beat/rinat-visible-before-edge'), false);
  const roadside = byId(definitions, 'urman.chapter1:scene/rinat-roadside-presence');
  assert.equal(roadside.interactions.some((interaction) => interaction.id === 'urman.chapter1:interaction/observe-rinat-roadside'
    && interaction.effects.some((entry) => entry.op === 'beat.set-state'
      && entry.beatId === 'urman.chapter1:beat/rinat-visible-before-edge' && entry.state === 'completed')), true);
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

test('the main route requires an observed field layout in addition to the read roadside tag', async () => {
  const definitions = await chapterDefinitions();
  const interactions = definitions.flatMap((definition) => definition.interactions ?? []);
  const fieldId = 'urman.chapter1:knowledge/clue_sketch_field_landmarks';
  const routeId = 'urman.chapter1:knowledge/clue_marat_last_route_near_zirat';
  const find = (id) => byId(interactions, `urman.chapter1:interaction/${id}`);
  const compare = find('compare-route-match');
  const tag = find('zirat-roadside-clue');
  const field = find('observe-sketch-landmarks');
  assert.equal(byId(definitions, fieldId).initialStatus, 'hidden');
  assert.equal(compare.conditions.some((condition) => condition.op === 'knowledge.status'
    && condition.knowledgeId === fieldId && condition.status === 'confirmed'), true);
  assert.deepEqual(byId(definitions, routeId).sourceIds, [
    'urman.oldpc:document/doc_kara_urman_edge_sketch',
    'urman.chapter1:knowledge/clue_zirat_roadside_marks', fieldId,
  ]);
  assert.equal(effectsIn(tag.effects).some((effect) => effect.op === 'knowledge.set-status'
    && [fieldId, routeId].includes(effect.knowledgeId)), false);
  assert.equal(effectsIn(field.effects).some((effect) => effect.op === 'knowledge.set-status'
    && effect.knowledgeId === fieldId && effect.status === 'confirmed'), true);
  assert.equal(effectsIn(field.effects).some((effect) => effect.op === 'knowledge.set-status'
    && effect.knowledgeId === routeId), false);
  const mandatoryConditions = JSON.stringify([compare.conditions, find('zirat-road-to-forest').conditions]);
  assert.equal(/clue_sketch_tag_base_checked|clue_sketch_place_compared/.test(mandatoryConditions), false,
    'the lower tag fitting, snow removal and optional image comparison must stay optional');
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

  const directRuleTexts = definitions.filter(({ id, value }) => id.includes(':text/')
    && value.translations?.ru?.includes('Не отвечай')).map(({ id }) => id).sort();
  assert.deepEqual(directRuleTexts, [
    'urman.chapter1:text/audio-rinat-caption', 'urman.chapter1:text/audio-rinat-transcript',
    'urman.chapter1:text/finale-rinat-warning',
  ]);
  assert.deepEqual(definitions.filter(({ id = '' }) => id.includes(':document/')), []);
  const photo = await readFile(path.join(CHAPTER_DIR, 'documents/arrival-photo-evidence.md'), 'utf8');
  assert.equal(photo.includes('id: urman.chapter1:document/arrival-photo-evidence'), true);
});

test('source excerpts replace supplied answers and preserve an explicit category recheck', async () => {
  const definitions = await chapterDefinitions();
  const prefix = 'urman.chapter1:';
  const excerpts = byId(definitions, `${prefix}scene/investigation-source-excerpts`);
  const cases = [
    ['excerpt-notice-cause', 'clue_notice_cause_excerpt', 'doc_marat_official_death_notice'],
    ['excerpt-register-wording', 'clue_register_wording_excerpt', 'rec_marat_case_register_conflict'],
    ['excerpt-register-category', 'clue_register_category_excerpt', 'rec_marat_case_register_conflict'],
    ['excerpt-message-voice', 'clue_message_voice_excerpt', 'msg_marat_saved_last_normal'],
  ];
  assert.deepEqual(excerpts.onEnter, []);
  assert.deepEqual(excerpts.onExit, []);
  for (const [actionId, knowledgeId, documentId] of cases) {
    const knowledge = byId(definitions, `${prefix}knowledge/${knowledgeId}`);
    const action = byId(excerpts.interactions, `${prefix}interaction/${actionId}`);
    const source = await readFile(path.join(ROOT, 'content/modules/urman-oldpc/documents', `${documentId}.md`), 'utf8');
    assert.equal(knowledge.initialStatus, 'hidden');
    assert.deepEqual(knowledge.sourceIds, [`urman.oldpc:document/${documentId}`]);
    assert.ok(source.includes(knowledge.summary.translations.ru), `${knowledgeId} must retain an actual complete source fragment`);
    assert.equal(action.journalAction, undefined, 'a ready-made journal answer must not replace the reader selection');
    assert.equal(action.targetSceneId, undefined);
    assert.equal(action.targetDialogueId, undefined);
    const expectedWrites = [`${prefix}knowledge/${knowledgeId}`];
    if (actionId === 'excerpt-message-voice') expectedWrites.push(`${prefix}knowledge/clue_message_question_prepared`);
    assert.deepEqual(effectsIn(action.effects).filter(({ op }) => op === 'knowledge.set-status').map(({ knowledgeId: id }) => id), expectedWrites);
  }
  const allInteractions = definitions.flatMap((entry) => entry.interactions ?? []);
  assert.equal(allInteractions.some(({ id }) => id === `${prefix}interaction/compare-message-heard`), false);
  assert.equal(definitions.some(({ id }) => id === `${prefix}text/compare-message-heard`), false);
  const compare = byId(allInteractions, `${prefix}interaction/compare-records-contradiction`);
  for (const raw of ['clue_notice_cause_excerpt', 'clue_register_wording_excerpt']) {
    assert.ok(compare.conditions.some(({ op, knowledgeId, status }) => op === 'knowledge.status'
      && knowledgeId === `${prefix}knowledge/${raw}` && status === 'confirmed'));
  }
  const naila = byId(definitions, `${prefix}dialogue/naila_medical_record`);
  const correction = byId(naila.nodes, 'category-correction').choices.find(({ id }) => id === 'review-record-fields');
  assert.ok(correction.conditions.some(({ op, stateKey, value }) => op === 'npc.state'
    && stateKey === 'category_excerpt_review_requested' && value === false));
  const mistake = byId(naila.nodes, 'record-question').choices.find(({ id }) => id === 'category-as-diagnosis');
  assert.ok(mistake.effects.some(({ op, stateKey, value }) => op === 'npc.set-state'
    && stateKey === 'category_excerpt_review_requested' && value === true));
  assert.equal(effectsIn(mistake.effects).some(({ knowledgeId }) => knowledgeId === `${prefix}knowledge/clue_register_category_excerpt`), false,
    'a wrong interpretation must not erase the genuinely read source');
  const category = byId(excerpts.interactions, `${prefix}interaction/excerpt-register-category`);
  assert.ok(effectsIn(category.conditions).some(({ op, stateKey, value }) => op === 'npc.state'
    && stateKey === 'category_excerpt_review_requested' && value === true));
  assert.ok(category.effects.some(({ op, stateKey, value }) => op === 'npc.set-state'
    && stateKey === 'category_excerpt_review_requested' && value === false));
  const answer = byId(naila.nodes, 'separate-record');
  assert.ok(answer.conditions.some(({ knowledgeId, status }) => knowledgeId === `${prefix}knowledge/clue_register_category_excerpt` && status === 'confirmed'));
});

test('vocabulary retains its core words and assets resolve as declared logical or real image sources', async () => {
  const definitions = await chapterDefinitions();
  const vocabulary = definitions.filter(({ id = '' }) => id.includes(':vocabulary/'));
  for (const word of ['tt_urman', 'tt_tavysh', 'tt_javap', 'tt_yaramyy', 'tt_zirat', 'tt_shurale']) {
    assert.equal(vocabulary.some(({ id }) => id === `urman.chapter1:vocabulary/${word}`), true);
  }
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
    assert.equal(path.isAbsolute(asset.file), false, `${asset.id} must use a portable module path`);
    if (asset.mimeType === 'application/vnd.urman.logical-asset-ref') {
      assert.equal(asset.file.startsWith('logical/'), true, `${asset.id} declares a logical reference token`);
    }
    assert.equal(asset.file.includes('/assets/'), false);
    await access(path.join(CHAPTER_DIR, asset.file));
  }
});
