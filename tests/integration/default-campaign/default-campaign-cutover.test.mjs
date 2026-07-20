import assert from 'node:assert/strict';
import test from 'node:test';

import { compileContent } from '../../../scripts/content/compile-content.mjs';
import { RuntimeBootstrap } from '../../../src/runtime/bootstrap/runtime-bootstrap.mjs';
import { OwnerRngStreams } from '../../../src/runtime/world/seeded-rng.mjs';

const PRODUCTION_CAMPAIGN = 'content/campaigns/urman.chapter1/campaign.json';
const MODULES = [
  'content/modules/urman-core/module.json',
  'content/modules/urman-chapter1/module.json',
  'content/modules/urman-oldpc/module.json',
];

async function productionPack() {
  const result = await compileContent({ campaignPath: PRODUCTION_CAMPAIGN, moduleManifestPaths: MODULES });
  assert.equal(result.ok, true, JSON.stringify(result.diagnostics));
  return result.pack;
}

async function productionRuntime() {
  return new RuntimeBootstrap({ pack: await productionPack(), seed: 'default-campaign-integration' });
}

function host() {
  const navigation = [];
  let input = null;
  return {
    host: {
      present: () => undefined,
      subscribeInput: (handler) => {
        input = handler;
        return { dispose: () => { if (input === handler) input = null; } };
      },
      navigate: (request) => navigation.push(request),
      handoff: (request) => navigation.push(request),
      startMedia: () => ({ dispose: () => undefined }),
      reportFailure: (error) => { throw error; },
    },
    navigation,
    send: (value) => input?.(value),
  };
}

async function advanceRequest(runtime, sceneId, interactionId, inputType, entryAlreadyCommitted = false) {
  const tracked = host();
  const session = runtime.createScene(sceneId, tracked.host, { entryAlreadyCommitted });
  await session.init();
  const result = await tracked.send({ type: inputType, interactionId });
  assert.equal(result.status, 'committed', `${sceneId} -> ${interactionId}`);
  assert.equal(tracked.navigation.length, 1, `${sceneId} emits one navigation request`);
  const request = tracked.navigation[0];
  assert.equal(request.entryAlreadyCommitted, true, `${sceneId} commits target entry atomically`);
  await session.dispose();
  return request;
}

async function advanceScene(runtime, sceneId, interactionId, inputType, entryAlreadyCommitted = false) {
  const request = await advanceRequest(runtime, sceneId, interactionId, inputType, entryAlreadyCommitted);
  assert.equal(typeof request.targetSceneId, 'string', `${sceneId} -> ${interactionId} targets a scene`);
  return request.targetSceneId;
}

test('production campaign preflights once, preserves the Chapter 1 route and locks its resolved fingerprint', async () => {
  const runtime = await productionRuntime();
  assert.equal(runtime.campaign.id, 'urman.chapter1');
  assert.equal(runtime.campaign.entrypoint, 'urman.chapter1:scene/arrival_vehicle_dusk');

  let sceneId = await advanceScene(runtime, runtime.campaign.entrypoint, 'urman.chapter1:interaction/arrival-enter-house', 'route.choose');
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/house-to-route', 'scene.interact', true);
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/route-to-fap', 'route.choose', true);
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/fap-to-document-desk', 'scene.interact', true);
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/fap-document-desk-to-official-record', 'scene.interact', true);
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/official-to-internal-register', 'scene.interact', true);
  const dialogueHandoff = await advanceRequest(runtime, sceneId, 'urman.chapter1:interaction/internal-register-to-rinat', 'scene.interact', true);
  assert.equal(dialogueHandoff.fromSceneId, sceneId);
  assert.equal(dialogueHandoff.targetDialogueId, 'urman.chapter1:dialogue/rinat_internal_register');
  assert.equal(runtime.runtimeContext.select((state) => state.npc['urman.chapter1:character/rinat']?.alerted), true, 'the scene interaction commits the dialogue start atomically');

  const dialogueHost = host();
  const dialogue = runtime.createDialogue(dialogueHandoff.targetDialogueId, dialogueHost.host, { entryAlreadyCommitted: dialogueHandoff.entryAlreadyCommitted });
  await dialogue.init();
  assert.equal(dialogue.entryResult?.transactionId, null, 'the dialogue never replays its atomically committed start node');
  await dialogue.dispose();

  sceneId = dialogueHandoff.fromSceneId;
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/internal-register-to-saved-message', 'scene.interact', true);
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/saved-message-to-boundary-source', 'scene.interact', true);
  assert.equal(sceneId, 'urman.chapter1:scene/evidence-tatarwiki-boundary');

  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/boundary-source-to-reread', 'scene.interact', true);
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/reread-to-edge-sketch', 'scene.interact', true);
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/edge-sketch-to-zirat-road', 'scene.interact', true);
  sceneId = await advanceScene(runtime, sceneId, 'urman.chapter1:interaction/zirat-road-to-forest', 'route.choose', true);
  assert.equal(sceneId, 'urman.chapter1:scene/forest');

  const finalScene = runtime.createScene(sceneId, host().host, { entryAlreadyCommitted: true });
  await finalScene.init();
  await finalScene.dispose();

  const state = runtime.runtimeContext.select((value) => value);
  assert.equal(state.knowledge['urman.chapter1:knowledge/clue_voice_answer_is_dangerous_hint'], 'hypothesis');
  assert.equal(state.knowledge['urman.chapter1:knowledge/clue_do_not_answer_rule'], 'confirmed');
  assert.equal(state.beats['urman.chapter1:beat/cliffhanger-hard-cut'], 'completed');

  const snapshot = runtime.snapshot();
  assert.equal(snapshot.campaignLock.campaignId, 'urman.chapter1');
  assert.equal(snapshot.campaignLock.campaignFingerprint, runtime.campaignFingerprint);
  const restored = await productionRuntime();
  await restored.restore(snapshot);
  assert.deepEqual(restored.runtimeContext.select((value) => value), state);
});

test('bootstrap scopes revisited sessions by immutable run state and preserves duplicate filtering after restore', async () => {
  const pack = structuredClone(await productionPack());
  const sceneId = 'urman.chapter1:scene/fap_waiting_room_day';
  const interactionId = 'urman.chapter1:interaction/replay-safe-noop';
  const scene = pack.registries.scenes.find((entry) => entry.id === sceneId);
  assert.ok(scene, 'test pack contains the generic static source scene');
  scene.interactions = [{
    id: interactionId,
    labelTextId: 'urman.chapter1:text/inspect',
    conditions: [],
    effects: [],
  }];

  const runtime = new RuntimeBootstrap({ pack, seed: 'session-scope-regression' });
  const firstSession = runtime.createScene(sceneId, host().host, { entryAlreadyCommitted: true });
  await firstSession.init();
  const stateBefore = runtime.runtimeContext.select((state) => state);
  const firstResult = await firstSession.handle({ type: 'scene.interact', interactionId });
  assert.equal(firstResult.status, 'committed');
  assert.deepEqual(runtime.runtimeContext.select((state) => state), stateBefore, 'the no-op action keeps the session-creation state identical');
  const snapshot = runtime.snapshot();
  await firstSession.dispose();

  const restored = new RuntimeBootstrap({ pack, seed: 'session-scope-regression' });
  await restored.restore(snapshot);
  const replaySession = restored.createScene(sceneId, host().host, { entryAlreadyCommitted: true });
  await replaySession.init();
  const replayResult = await replaySession.handle({ type: 'scene.interact', interactionId });
  assert.equal(replayResult.status, 'rejected');
  assert.equal(replayResult.error.code, 'DuplicateOccurrence');
  assert.deepEqual(restored.runtimeContext.select((state) => state), stateBefore, 'the restored duplicate causes no second commit');
  await replaySession.dispose();
});

test('bootstrap defaults its deterministic seed to the selected compiled campaign fingerprint', async () => {
  const pack = await productionPack();
  const runtime = new RuntimeBootstrap({ pack });
  const expected = new OwnerRngStreams({ seed: pack.campaignFingerprint }).exportSnapshot();
  assert.equal(runtime.snapshot().world.rngStreams.masterSeed, expected.masterSeed);
});
