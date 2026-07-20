import assert from 'node:assert/strict';
import test from 'node:test';

import { compileContent } from '../../../scripts/content/compile-content.mjs';
import {
  CapabilityRegistry,
  ConditionRegistry,
  ContentRegistry,
  EffectRegistry,
  SceneRegistry,
} from '../../../src/runtime/registries/runtime-registries.mjs';

async function fixturePack() {
  const result = await compileContent({
    campaignPath: 'tests/content/compiler/fixtures/valid/campaign/campaign.json',
    moduleManifestPaths: ['tests/content/compiler/fixtures/valid/module/module.json'],
  });
  assert.equal(result.ok, true, JSON.stringify(result.diagnostics));
  return result.pack;
}

test('ContentRegistry.fromPack exposes deterministic immutable content definitions', async () => {
  const registry = ContentRegistry.fromPack(await fixturePack());
  assert.deepEqual(registry.ids(), [
    'urman.compiler-fixture:scene/arrival',
    'urman.compiler-fixture:text/arrival-title',
  ]);
  assert.equal(registry.get('urman.compiler-fixture:scene/arrival').sceneType, 'static');
  assert.throws(() => registry.get('missing'), (error) => error.code === 'MissingRegistration');
});

test('scene, condition and effect registries reject duplicates and route through descriptors', async () => {
  const pack = await fixturePack();
  const sceneDescriptor = { sceneType: 'static', create: (definition) => ({ sceneId: definition.id }) };
  const scenes = new SceneRegistry(pack, [sceneDescriptor]);
  assert.deepEqual(scenes.create('urman.compiler-fixture:scene/arrival', {}), { sceneId: 'urman.compiler-fixture:scene/arrival' });
  assert.throws(() => scenes.register(sceneDescriptor), (error) => error.code === 'DuplicateRegistration');

  const conditions = new ConditionRegistry(pack, [{ op: 'knowledge.status', evaluate: () => true }]);
  assert.equal(conditions.evaluate({ op: 'knowledge.status' }, {}), true);
  const effects = new EffectRegistry(pack, [{ op: 'scene.request', plan: (effect) => ({ target: effect.sceneId }) }]);
  assert.deepEqual(effects.plan({ op: 'scene.request', sceneId: 'sample:scene/next' }, {}), { target: 'sample:scene/next' });
});

test('capability lookup enforces one provider and exact version', async () => {
  const pack = await fixturePack();
  const descriptor = { protocolId: 'sample:capability/radio', exactVersion: '1.0.0', create: () => ({}) };
  const registry = new CapabilityRegistry(pack, [descriptor]);
  const registered = registry.require(descriptor.protocolId, '1.0.0');
  assert.deepEqual(registered, descriptor);
  assert.notStrictEqual(registered, descriptor);
  assert.equal(Object.isFrozen(registered), true);
  assert.throws(() => registry.require(descriptor.protocolId, '2.0.0'), (error) => error.code === 'IncompatibleExactVersion');
  assert.throws(() => registry.register(descriptor), (error) => error.code === 'DuplicateRegistration');
});

test('all pack-used scene types and opcodes are required during registry construction', async () => {
  const pack = await fixturePack();
  assert.throws(() => new SceneRegistry(pack), (error) => error.code === 'MissingRegistration');

  const conditionPack = structuredClone(pack);
  conditionPack.registries.scenes[0].entryConditions.push({
    op: 'knowledge.status',
    knowledgeId: 'sample:knowledge/example',
    status: 'hidden',
  });
  assert.throws(() => new ConditionRegistry(conditionPack), (error) => error.code === 'MissingRegistration');
  assert.doesNotThrow(() => new ConditionRegistry(conditionPack, [{ op: 'knowledge.status', evaluate: () => true }]));

  const effectPack = structuredClone(pack);
  effectPack.registries.scenes[0].onEnter.push({ op: 'scene.request', sceneId: effectPack.campaign.entrypoint });
  assert.throws(() => new EffectRegistry(effectPack), (error) => error.code === 'MissingRegistration');
  assert.doesNotThrow(() => new EffectRegistry(effectPack, [{ op: 'scene.request', plan: () => ({}) }]));
});

test('runtime descriptor executable shape is validated at construction', async () => {
  const pack = await fixturePack();
  assert.throws(() => new SceneRegistry(pack, [{ sceneType: 'static' }]), (error) => error.code === 'InvalidRegistration');
  assert.throws(() => new ConditionRegistry(pack, [{ op: 'knowledge.status' }]), (error) => error.code === 'InvalidRegistration');
  assert.throws(() => new EffectRegistry(pack, [{ op: 'scene.request' }]), (error) => error.code === 'InvalidRegistration');
  assert.throws(
    () => new CapabilityRegistry(pack, [{ protocolId: 'sample:capability/radio', exactVersion: '1.0.0' }]),
    (error) => error.code === 'InvalidRegistration',
  );
  assert.throws(
    () => new CapabilityRegistry(pack, [{ protocolId: 'sample:capability/radio', exactVersion: '^1.0.0', create: () => ({}) }]),
    (error) => error.code === 'InvalidRegistration',
  );
});

test('runtime descriptors require own data fields before they are frozen for use', async () => {
  const pack = await fixturePack();
  const inheritedScene = Object.create({ sceneType: 'static', create: () => ({}) });
  const inheritedCondition = Object.create({ op: 'knowledge.status', evaluate: () => true });
  const inheritedEffect = Object.create({ op: 'scene.request', plan: () => ({}) });
  const inheritedCapability = Object.create({
    protocolId: 'sample:capability/radio',
    exactVersion: '1.0.0',
    create: () => ({}),
  });

  assert.throws(() => new SceneRegistry(pack, [inheritedScene]), (error) => error.code === 'InvalidRegistration');
  assert.throws(() => new ConditionRegistry(pack, [inheritedCondition]), (error) => error.code === 'InvalidRegistration');
  assert.throws(() => new EffectRegistry(pack, [inheritedEffect]), (error) => error.code === 'InvalidRegistration');
  assert.throws(() => new CapabilityRegistry(pack, [inheritedCapability]), (error) => error.code === 'InvalidRegistration');
});

test('capability requirements fail at startup for missing provider or exact-version mismatch', async () => {
  const pack = structuredClone(await fixturePack());
  const requirement = { protocolId: 'sample:capability/radio', exactVersion: '1.0.0' };
  pack.campaign.capabilityRequirements = [requirement];
  assert.throws(() => new CapabilityRegistry(pack), (error) => error.code === 'MissingRegistration');
  assert.throws(
    () => new CapabilityRegistry(pack, [{ ...requirement, exactVersion: '2.0.0', create: () => ({}) }]),
    (error) => error.code === 'IncompatibleExactVersion',
  );
  assert.doesNotThrow(() => new CapabilityRegistry(pack, [{ ...requirement, create: () => ({}) }]));
});
