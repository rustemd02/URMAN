import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { compileContent } from '../../../../scripts/content/compile-content.mjs';
import { RuntimeKernel } from '../../../../src/runtime/kernel/runtime-kernel.mjs';
import { CapabilityRegistry, ContentRegistry, SceneRegistry } from '../../../../src/runtime/registries/runtime-registries.mjs';
import { AudioResolver } from '../../../../src/runtime/media/audio-resolver.mjs';
import { AssetResolver, TextResolver } from '../../../../src/runtime/resolvers/content-resolvers.mjs';
import { createActiveSceneDescriptors } from '../../../../src/runtime/modules/active/active-provider-descriptors.mjs';
import { createCapabilitySceneSession } from '../../../../src/runtime/modules/active/capability-scene-provider.mjs';
import { createDialogueReactionSession } from '../../../../src/runtime/modules/active/dialogue-reaction-provider.mjs';
import { createRouteNavigationSession } from '../../../../src/runtime/modules/active/route-navigation-provider.mjs';
import { JournalProjection } from '../../../../src/runtime/modules/active/route-journal-projection.mjs';
import { createScenePresentationSession } from '../../../../src/runtime/modules/active/scene-presentation-provider.mjs';
import { createJournalPresenter, createVocabularyPresenter } from '../../../../src/runtime/modules/active/ui-projection-providers.mjs';

const ROOT = 'tests/fixtures/content/canary';
const MODULES = [
  `${ROOT}/common/module.json`,
  `${ROOT}/route/module.json`,
];

const EMPTY_CAPABILITY_PACK = Object.freeze({
  campaign: Object.freeze({ capabilityRequirements: Object.freeze([]) }),
  registries: Object.freeze({ capabilities: Object.freeze([]) }),
});
const CANARY_CAPABILITY_PROVIDER = Object.freeze({
  protocolId: 'canary.core:capability/signal',
  exactVersion: '1.0.0',
  create: () => ({}),
});

function isDisposedError(error) { return error?.code === 'ActiveProviderDisposed'; }

async function canary(variant) {
  const compiled = await compileContent({
    campaignPath: `${ROOT}/campaigns/${variant}/campaign.json`,
    moduleManifestPaths: MODULES,
  });
  assert.equal(compiled.ok, true, JSON.stringify(compiled.diagnostics));
  return compiled.pack;
}

async function chapterPack() {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'urman-active-chapter-'));
  const campaignPath = path.join(directory, 'campaign.json');
  await writeFile(campaignPath, `${JSON.stringify({
    schemaVersion: 1,
    id: 'active.chapter.fixture',
    exactVersion: '1.0.0',
    entrypoint: 'urman.chapter1:scene/forest',
    modules: [{ moduleId: 'urman.chapter1', exactVersion: '1.0.0' }],
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
    narrativeOrder: ['urman.chapter1:scene/forest'],
    invariants: [],
  })}\n`);
  try {
    const compiled = await compileContent({
      campaignPath,
      moduleManifestPaths: ['content/modules/urman-chapter1/module.json'],
    });
    assert.equal(compiled.ok, true, JSON.stringify(compiled.diagnostics));
    return compiled.pack;
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
}

function createRuntime({
  onScene = () => undefined,
  onRoute = () => undefined,
  onDialogue = () => undefined,
  rejectSceneEntry = false,
  rejectSceneTargetEntry = false,
  rejectRouteTargetEntry = false,
  rejectDialogueTargetNode = false,
  capabilityDescriptors = [],
} = {}) {
  return new RuntimeKernel({
    initialState: { journal: ['first'], hud: { pressure: 1 }, inventory: ['note'], vocabulary: ['known'] },
    handlers: {
      'runtime.scene': (command) => {
        onScene(command.payload);
        if (rejectSceneEntry && command.payload.action === 'enter') {
          return { rejection: { code: 'EntryDenied', message: 'Entry conditions were not accepted.' } };
        }
        if (rejectSceneTargetEntry && command.payload.targetEntry) {
          return { rejection: { code: 'TargetEntryDenied', message: 'Target entry conditions were not accepted.' } };
        }
        return { events: [{ type: 'active.scene', payload: command.payload }] };
      },
      'runtime.route': (command) => {
        onRoute(command.payload);
        if (rejectRouteTargetEntry && command.payload.targetEntry) {
          return { rejection: { code: 'TargetEntryDenied', message: 'Target entry conditions were not accepted.' } };
        }
        return { events: [{ type: 'active.route', payload: command.payload }] };
      },
      'runtime.dialogue': (command) => {
        onDialogue(command.payload);
        if (rejectDialogueTargetNode && command.payload.targetNode) {
          return { rejection: { code: 'TargetNodeDenied', message: 'Target node conditions were not accepted.' } };
        }
        return { events: [{ type: 'active.dialogue', payload: command.payload }] };
      },
    },
    capabilityRegistry: new CapabilityRegistry(EMPTY_CAPABILITY_PACK, capabilityDescriptors),
  });
}

function resolvers(pack) {
  const assetResolver = new AssetResolver(pack, { resolveFileUrl: (file) => `memory://${file}` });
  const textResolver = new TextResolver(pack);
  return { assetResolver, textResolver, audioResolver: new AudioResolver(pack, { assetResolver, textResolver }) };
}

function trackedHost() {
  const models = [];
  let inputHandler = null;
  let subscriptions = 0;
  let media = 0;
  let mediaStops = 0;
  const mediaResources = [];
  const failures = [];
  const navigations = [];
  const handoffs = [];
  return {
    host: {
      present: (model) => models.push(model),
      subscribeInput: (handler) => {
        inputHandler = handler;
        subscriptions += 1;
        return { dispose: () => { subscriptions -= 1; inputHandler = null; } };
      },
      startMedia: (resource) => {
        media += 1;
        mediaResources.push(resource);
        return { dispose: () => { media -= 1; mediaStops += 1; } };
      },
      navigate: (request) => navigations.push(request),
      handoff: (request) => handoffs.push(request),
      reportFailure: (error) => failures.push(error),
    },
    models,
    navigations,
    handoffs,
    mediaResources,
    failures,
    send: (input) => inputHandler?.(input),
    counts: () => ({ subscriptions, media, mediaStops, hasInput: Boolean(inputHandler) }),
  };
}

function deferredRuntime({ capabilityDescriptor = CANARY_CAPABILITY_PROVIDER } = {}) {
  let release;
  const gate = new Promise((resolve) => { release = resolve; });
  const commands = [];
  return {
    context: {
      select: (pick) => pick({}),
      dispatch: async (command) => {
        commands.push(command);
        await gate;
        return { status: 'committed', transactionId: `deferred:${commands.length}`, value: null };
      },
      subscribe: () => ({ disposed: false, dispose() {} }),
      capabilities: {
        require: (protocolId, exactVersion) => {
          if (capabilityDescriptor.protocolId === protocolId && capabilityDescriptor.exactVersion === exactVersion) return capabilityDescriptor;
          throw new Error(`Missing capability ${protocolId}@${exactVersion}`);
        },
      },
    },
    release: () => release(),
    commands,
  };
}

test('generic scene and dialogue providers swap canary role asset through campaign bindings only', async () => {
  const [amber, indigo] = await Promise.all([canary('amber'), canary('indigo')]);
  const runtime = createRuntime();

  for (const pack of [amber, indigo]) {
    const contentRegistry = ContentRegistry.fromPack(pack);
    const host = trackedHost();
    const session = createDialogueReactionSession('canary.core:dialogue/greeting', {
      runtimeContext: runtime.context,
      contentRegistry,
      roleBindings: pack.campaign.roleBindings,
      resolveVariables: () => ({ term: { vocabularyId: 'canary.core:vocabulary/lantern' } }),
      presentationHost: host.host,
      sessionScope: `dialogue-${pack.campaign.id}`,
      ...resolvers(pack),
    });
    const [model] = await Promise.all([session.init(), session.init()]);
    assert.equal(model.speaker.characterId, pack.campaign.roleBindings.guide);
    assert.match(model.speaker.portrait.url, new RegExp(pack.campaign.id.endsWith('amber') ? 'guide-amber' : 'guide-indigo'));
    assert.equal(host.counts().subscriptions, 1);
    await session.dispose();
    assert.deepEqual(host.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });
  }
});

test('scene registry creates generic static presentation with resolver-only models and zero retained host handles', async () => {
  const pack = await canary('amber');
  const runtime = createRuntime();
  const host = trackedHost();
  const registry = new SceneRegistry(pack, createActiveSceneDescriptors());
  const session = registry.create('canary.core:scene/landing', {
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    sessionScope: 'landing',
    ...resolvers(pack),
  });
  const model = await session.init();
  assert.equal(model.kind, 'scene');
  assert.equal(model.assets[0].url, 'memory://assets/lantern.svg');
  assert.equal(model.assets[0].file, undefined);
  assert.equal(host.models.length, 1);
  await Promise.all([session.dispose(), session.dispose()]);
  assert.deepEqual(host.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });
});

test('projection presenters read immutable kernel state through selectors and never own it', async () => {
  const pack = await canary('amber');
  const runtime = createRuntime();
  const host = trackedHost();
  const journal = createJournalPresenter({
    runtimeContext: runtime.context,
    presentationHost: host.host,
    selectProjection: (state) => state.journal,
  });
  const vocabulary = createVocabularyPresenter({
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    selectProjection: (state) => state.vocabulary,
  });
  journal.init();
  journal.init();
  assert.equal(host.counts().subscriptions, 1);
  const rejected = await host.send({ type: 'ui.invalid' });
  assert.equal(rejected.status, 'rejected');
  assert.equal(host.failures.length, 1);
  assert.deepEqual(journal.render().projection, ['first']);
  assert.deepEqual(vocabulary.vocabularyIds(), ['canary.core:vocabulary/lantern']);
  assert.throws(() => { journal.render().projection.push('mutate'); }, TypeError);
  journal.dispose();
  vocabulary.dispose();
});

test('active sessions and projections reject render, init and handle calls after disposal', async () => {
  const pack = await canary('amber');
  const contentRegistry = ContentRegistry.fromPack(pack);
  const scene = createScenePresentationSession({
    id: 'canary.route:scene/post-dispose-scene', sceneType: 'static', assetRefs: [], textRefs: ['canary.route:text/marker-title'], entryConditions: [], onEnter: [], onExit: [], interactions: [],
  }, { runtimeContext: createRuntime().context, contentRegistry, sessionScope: 'post-dispose-scene', ...resolvers(pack) });
  await scene.init();
  await scene.dispose();
  assert.throws(() => scene.render(), isDisposedError);
  await assert.rejects(scene.init(), isDisposedError);
  await assert.rejects(scene.handle({ type: 'scene.interact', interactionId: 'none' }), isDisposedError);

  const routeDefinition = {
    id: 'canary.route:scene/post-dispose-route', sceneType: 'route', assetRefs: [], textRefs: ['canary.route:text/marker-title'], entryConditions: [], onEnter: [], onExit: [], interactions: [],
  };
  const routeRegistry = { get: (id) => id === routeDefinition.id ? routeDefinition : contentRegistry.get(id), category: () => [] };
  const route = createRouteNavigationSession(routeDefinition, {
    runtimeContext: createRuntime().context, contentRegistry: routeRegistry, sessionScope: 'post-dispose-route', ...resolvers(pack),
  });
  await route.init();
  await route.dispose();
  assert.throws(() => route.render(), isDisposedError);
  await assert.rejects(route.init(), isDisposedError);
  await assert.rejects(route.handle({ type: 'route.choose', interactionId: 'none' }), isDisposedError);

  const dialogue = createDialogueReactionSession('canary.core:dialogue/greeting', {
    runtimeContext: createRuntime().context,
    contentRegistry,
    roleBindings: pack.campaign.roleBindings,
    resolveVariables: () => ({ term: { vocabularyId: 'canary.core:vocabulary/lantern' } }),
    sessionScope: 'post-dispose-dialogue',
    ...resolvers(pack),
  });
  await dialogue.init();
  await dialogue.dispose();
  assert.throws(() => dialogue.render(), isDisposedError);
  await assert.rejects(dialogue.init(), isDisposedError);
  await assert.rejects(dialogue.handle({ type: 'dialogue.choose', choiceId: 'none' }), isDisposedError);

  const capability = createCapabilitySceneSession({
    id: 'canary.core:scene/post-dispose-capability', sceneType: 'capability', assetRefs: [], textRefs: ['canary.core:text/landing-title'], entryConditions: [], onEnter: [], onExit: [], interactions: [], capabilityConfigRef: 'canary.core:capability/signal-provider',
  }, {
    runtimeContext: createRuntime({ capabilityDescriptors: [CANARY_CAPABILITY_PROVIDER] }).context,
    contentRegistry,
    sessionScope: 'post-dispose-capability',
    capabilitySceneAdapter: { create: async () => ({ start: async () => undefined, handle: async () => undefined, dispose: async () => undefined }) },
    ...resolvers(pack),
  });
  await capability.init();
  await capability.dispose();
  assert.throws(() => capability.render(), isDisposedError);
  await assert.rejects(capability.init(), isDisposedError);
  await assert.rejects(capability.handle({ type: 'capability.handle' }), isDisposedError);

  const journalProjection = new JournalProjection(createRuntime().context, (state) => state.journal);
  journalProjection.dispose();
  assert.throws(() => journalProjection.render(), isDisposedError);

  const uiProjection = createJournalPresenter({
    runtimeContext: createRuntime().context,
    selectProjection: (state) => state.journal,
    buildCommand: () => ({ type: 'ui.action' }),
  });
  uiProjection.dispose();
  assert.throws(() => uiProjection.render(), isDisposedError);
  assert.throws(() => uiProjection.init(), isDisposedError);
  await assert.rejects(uiProjection.handle({ type: 'ui.action' }), isDisposedError);
});

test('scene and route concurrent disposal issue exactly one exit command each', async () => {
  const pack = await canary('amber');
  const sceneActions = [];
  const sceneRuntime = createRuntime({ onScene: (payload) => sceneActions.push(payload.action) });
  const sceneHost = trackedHost();
  const scene = createScenePresentationSession({
    id: 'canary.route:scene/dispose-once',
    sceneType: 'static',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [],
    interactions: [],
  }, {
    runtimeContext: sceneRuntime.context,
    presentationHost: sceneHost.host,
    sessionScope: 'scene-dispose-once',
    ...resolvers(pack),
  });
  await scene.init();
  await Promise.all([scene.dispose(), scene.dispose()]);
  assert.deepEqual(sceneActions, ['enter', 'exit']);

  const routeActions = [];
  const routeRuntime = createRuntime({ onRoute: (payload) => routeActions.push(payload.action) });
  const routeHost = trackedHost();
  const definition = {
    id: 'canary.route:scene/route-dispose-once',
    sceneType: 'route',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [],
    interactions: [],
  };
  const registry = { get: (id) => id === definition.id ? definition : ContentRegistry.fromPack(pack).get(id), category: () => [] };
  const route = createRouteNavigationSession(definition, {
    runtimeContext: routeRuntime.context,
    contentRegistry: registry,
    presentationHost: routeHost.host,
    sessionScope: 'route-dispose-once',
    ...resolvers(pack),
  });
  await route.init();
  await Promise.all([route.dispose(), route.dispose()]);
  assert.deepEqual(routeActions, ['enter', 'exit']);
});

test('dispose waits for in-flight init and leaves no late input registration or capability alive', async () => {
  const pack = await canary('amber');
  const sceneHost = trackedHost();
  const sceneRuntime = deferredRuntime();
  const scene = createScenePresentationSession({
    id: 'canary.route:scene/late-init-scene', sceneType: 'static', assetRefs: [], textRefs: ['canary.route:text/marker-title'], entryConditions: [], onEnter: [], onExit: [], interactions: [],
  }, { runtimeContext: sceneRuntime.context, presentationHost: sceneHost.host, sessionScope: 'late-init-scene', ...resolvers(pack) });
  const sceneInit = scene.init();
  await Promise.resolve();
  const sceneDispose = scene.dispose();
  sceneRuntime.release();
  await Promise.all([sceneInit, sceneDispose]);
  assert.deepEqual(sceneHost.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });

  const routeHost = trackedHost();
  const routeRuntime = deferredRuntime();
  const routeDefinition = {
    id: 'canary.route:scene/late-init-route', sceneType: 'route', assetRefs: [], textRefs: ['canary.route:text/marker-title'], entryConditions: [], onEnter: [], onExit: [], interactions: [],
  };
  const routeRegistry = { get: (id) => id === routeDefinition.id ? routeDefinition : ContentRegistry.fromPack(pack).get(id), category: () => [] };
  const route = createRouteNavigationSession(routeDefinition, { runtimeContext: routeRuntime.context, contentRegistry: routeRegistry, presentationHost: routeHost.host, sessionScope: 'late-init-route', ...resolvers(pack) });
  const routeInit = route.init();
  await Promise.resolve();
  const routeDispose = route.dispose();
  routeRuntime.release();
  await Promise.all([routeInit, routeDispose]);
  assert.deepEqual(routeHost.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });

  const dialogueHost = trackedHost();
  const dialogueRuntime = deferredRuntime();
  const dialogue = createDialogueReactionSession('canary.core:dialogue/greeting', {
    runtimeContext: dialogueRuntime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    roleBindings: pack.campaign.roleBindings,
    resolveVariables: () => ({ term: { vocabularyId: 'canary.core:vocabulary/lantern' } }),
    presentationHost: dialogueHost.host,
    sessionScope: 'late-init-dialogue',
    ...resolvers(pack),
  });
  const dialogueInit = dialogue.init();
  await Promise.resolve();
  const dialogueDispose = dialogue.dispose();
  dialogueRuntime.release();
  await Promise.all([dialogueInit, dialogueDispose]);
  assert.deepEqual(dialogueHost.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });

  const capabilityHost = trackedHost();
  const capabilityRuntime = deferredRuntime();
  const trace = [];
  const capability = createCapabilitySceneSession({
    id: 'canary.core:scene/late-init-capability', sceneType: 'capability', assetRefs: [], textRefs: ['canary.core:text/landing-title'], entryConditions: [], onEnter: [], onExit: [], interactions: [], capabilityConfigRef: 'canary.core:capability/signal-provider',
  }, {
    runtimeContext: capabilityRuntime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: capabilityHost.host,
    sessionScope: 'late-init-capability',
    capabilitySceneAdapter: { create: async () => ({ start: async () => trace.push('start'), handle: async () => null, dispose: async () => trace.push('dispose') }) },
    ...resolvers(pack),
  });
  const capabilityInit = capability.init();
  await Promise.resolve();
  const capabilityDispose = capability.dispose();
  capabilityRuntime.release();
  await Promise.all([capabilityInit, capabilityDispose]);
  assert.deepEqual(trace, ['start', 'dispose']);
  assert.deepEqual(capabilityHost.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });
});

test('dialogue choice sends target node conditions and effects in one command before changing the rendered node', async () => {
  const pack = await canary('amber');
  const baseRegistry = ContentRegistry.fromPack(pack);
  const dialogue = {
    id: 'canary.route:dialogue/atomic-choice',
    startNodeId: 'first',
    nodes: [
      {
        id: 'first',
        speakerRole: 'guide',
        textId: 'canary.core:text/guide-line',
        conditions: [],
        effects: [],
        choices: [{
          id: 'continue',
          textId: 'canary.route:text/marker-title',
          conditions: [],
          effects: [{ op: 'pressure.change', delta: 1 }],
          nextNodeId: 'second',
        }],
      },
      {
        id: 'second',
        speakerRole: 'guide',
        textId: 'canary.core:text/guide-line',
        conditions: [{ op: 'pressure.at-least', minimum: 99 }],
        effects: [{ op: 'pressure.change', delta: 1 }],
        choices: [],
      },
    ],
  };
  const registry = { get: (id) => id === dialogue.id ? dialogue : baseRegistry.get(id), category: (name) => baseRegistry.category(name) };
  const payloads = [];
  const runtime = createRuntime({ rejectDialogueTargetNode: true, onDialogue: (payload) => payloads.push(payload) });
  const host = trackedHost();
  const session = createDialogueReactionSession(dialogue.id, {
    runtimeContext: runtime.context,
    contentRegistry: registry,
    roleBindings: pack.campaign.roleBindings,
    resolveVariables: () => ({ term: { vocabularyId: 'canary.core:vocabulary/lantern' } }),
    presentationHost: host.host,
    sessionScope: 'dialogue-atomic-choice',
    ...resolvers(pack),
  });
  await session.init();
  const result = await session.handle({ type: 'dialogue.choose', choiceId: 'continue' });
  assert.equal(result.status, 'rejected');
  assert.deepEqual(payloads[1].targetNode, {
    nodeId: 'second',
    conditions: dialogue.nodes[1].conditions,
    effects: dialogue.nodes[1].effects,
  });
  assert.equal(host.models.length, 1);
  session.dispose();
});

test('scripted audio-first presentation releases its host media and input subscription on disposal', async () => {
  const pack = await canary('amber');
  const runtime = createRuntime();
  const host = trackedHost();
  const session = createScenePresentationSession({
    id: 'canary.core:scene/audio-moment',
    sceneType: 'scripted',
    assetRefs: [],
    textRefs: ['canary.core:text/signal-caption'],
    entryConditions: [],
    onEnter: [{ op: 'audio.request', assetId: 'canary.core:asset/signal-tone' }],
    onExit: [],
    interactions: [],
  }, {
    runtimeContext: runtime.context,
    presentationHost: host.host,
    sessionScope: 'audio-moment',
    ...resolvers(pack),
  });
  await session.init();
  assert.deepEqual(host.counts(), { subscriptions: 1, media: 1, mediaStops: 0, hasInput: true });
  await Promise.all([session.dispose(), session.dispose()]);
  assert.deepEqual(host.counts(), { subscriptions: 0, media: 0, mediaStops: 1, hasInput: false });
});

test('Chapter 1 forest reaches the host only after its entry conditions commit and exposes both requested audio accessibility models', async () => {
  const pack = await chapterPack();
  const forest = pack.registries.scenes.find((scene) => scene.id === 'urman.chapter1:scene/forest');
  assert.ok(forest);
  const entries = [];
  const runtime = createRuntime({ onScene: (payload) => entries.push(payload) });
  const host = trackedHost();
  const session = createScenePresentationSession(forest, {
    runtimeContext: runtime.context,
    presentationHost: host.host,
    sessionScope: 'forest-accessibility',
    ...resolvers(pack),
  });
  await session.init();
  assert.equal(session.entryResult.status, 'committed');
  assert.equal(entries[0].entryConditions.length, 3);
  assert.equal(host.models.length, 1);
  assert.equal(host.mediaResources.length, 2);
  assert.ok(host.mediaResources.every((resource) => resource.captions && resource.transcript && resource.nonAudioCue));
  await session.dispose();
  assert.deepEqual(host.counts(), { subscriptions: 0, media: 0, mediaStops: 2, hasInput: false });
});

test('entry rejection keeps a content-gated scene unpresented and starts no audio or listeners', async () => {
  const pack = await chapterPack();
  const forest = pack.registries.scenes.find((scene) => scene.id === 'urman.chapter1:scene/forest');
  const entries = [];
  const runtime = createRuntime({ rejectSceneEntry: true, onScene: (payload) => entries.push(payload) });
  const host = trackedHost();
  const session = createScenePresentationSession(forest, {
    runtimeContext: runtime.context,
    presentationHost: host.host,
    sessionScope: 'forest-rejected',
    ...resolvers(pack),
  });
  await session.init();
  assert.equal(session.entryResult.status, 'rejected');
  assert.equal(host.models.length, 0);
  assert.deepEqual(host.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });
  await session.dispose();
  assert.deepEqual(entries.map((entry) => entry.action), ['enter']);
});

test('capability scene resolves its capability definition and exact runtime provider before adapter lifecycle', async () => {
  const pack = await canary('amber');
  const runtime = createRuntime({ capabilityDescriptors: [CANARY_CAPABILITY_PROVIDER] });
  const host = trackedHost();
  const lifecycle = [];
  const session = createCapabilitySceneSession({
    id: 'canary.core:scene/capability-view',
    sceneType: 'capability',
    assetRefs: [],
    textRefs: ['canary.core:text/landing-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [],
    interactions: [],
    capabilityConfigRef: 'canary.core:capability/signal-provider',
  }, {
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    sessionScope: 'capability-view',
    capabilitySceneAdapter: {
      create: ({ sceneId, capabilityConfigRef, capabilityConfig, providerDescriptor }) => {
        assert.equal(Object.isFrozen(capabilityConfig), true);
        assert.equal(Object.isFrozen(providerDescriptor), true);
        lifecycle.push(['create', sceneId, capabilityConfigRef, capabilityConfig, providerDescriptor]);
        return {
          start: async () => lifecycle.push(['start']),
          handle: async (payload) => {
            lifecycle.push(['handle', payload]);
            return { outcome: payload };
          },
          dispose: async () => lifecycle.push(['dispose']),
        };
      },
    },
    ...resolvers(pack),
  });
  const model = await session.init();
  assert.equal(model.capabilityConfigRef, 'canary.core:capability/signal-provider');
  assert.deepEqual(await host.send({ type: 'capability.handle', payload: { mode: 'accessible' } }), { outcome: { mode: 'accessible' } });
  await Promise.all([session.dispose(), session.dispose()]);
  await assert.rejects(
    session.handle({ type: 'capability.handle', payload: { mode: 'accessible' } }),
    /after capability scene disposal/,
  );
  assert.deepEqual(lifecycle, [
    ['create', 'canary.core:scene/capability-view', 'canary.core:capability/signal-provider', {
      ...ContentRegistry.fromPack(pack).get('canary.core:capability/signal-provider'),
    }, CANARY_CAPABILITY_PROVIDER],
    ['start'],
    ['handle', { mode: 'accessible' }],
    ['dispose'],
  ]);
  assert.deepEqual(host.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });
});

test('capability scene rejects missing, wrong-kind and incompatible providers before adapter create', async () => {
  const pack = await canary('amber');
  const contentRegistry = ContentRegistry.fromPack(pack);
  const definition = {
    id: 'canary.core:scene/capability-validation', sceneType: 'capability', assetRefs: [], textRefs: ['canary.core:text/landing-title'], entryConditions: [], onEnter: [], onExit: [], interactions: [], capabilityConfigRef: 'canary.core:capability/missing',
  };
  let creates = 0;
  const context = {
    runtimeContext: createRuntime({ capabilityDescriptors: [CANARY_CAPABILITY_PROVIDER] }).context,
    contentRegistry,
    capabilitySceneAdapter: { create: () => { creates += 1; return {}; } },
    ...resolvers(pack),
  };
  assert.throws(() => createCapabilitySceneSession(definition, context), (error) => error?.code === 'MissingActiveProviderDefinition');
  assert.equal(creates, 0);

  assert.throws(() => createCapabilitySceneSession({ ...definition, capabilityConfigRef: 'canary.core:text/landing-title' }, context), (error) => error?.code === 'MissingActiveProviderDefinition');
  assert.equal(creates, 0);

  assert.throws(() => createCapabilitySceneSession({ ...definition, capabilityConfigRef: 'canary.core:capability/signal-provider' }, {
    ...context,
    runtimeContext: createRuntime({ capabilityDescriptors: [{ ...CANARY_CAPABILITY_PROVIDER, exactVersion: '2.0.0' }] }).context,
  }), (error) => error?.code === 'IncompatibleExactVersion');
  assert.equal(creates, 0);
});

test('a failed capability start cleans up its provider exactly once before a later dispose', async () => {
  const pack = await canary('amber');
  const runtime = createRuntime({ capabilityDescriptors: [CANARY_CAPABILITY_PROVIDER] });
  const host = trackedHost();
  let providerDisposals = 0;
  const session = createCapabilitySceneSession({
    id: 'canary.core:scene/capability-failure',
    sceneType: 'capability',
    assetRefs: [],
    textRefs: ['canary.core:text/landing-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [],
    interactions: [],
    capabilityConfigRef: 'canary.core:capability/signal-provider',
  }, {
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    sessionScope: 'capability-failure',
    capabilitySceneAdapter: {
      create: () => ({
        start: async () => { throw new Error('capability start failed'); },
        handle: async () => null,
        dispose: async () => { providerDisposals += 1; },
      }),
    },
    ...resolvers(pack),
  });
  await assert.rejects(session.init(), /capability start failed/);
  await session.dispose();
  assert.equal(providerDisposals, 1);
  assert.deepEqual(host.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });
});

test('route provider delegates a content-declared external handoff only after a committed command', async () => {
  const pack = await canary('amber');
  const runtime = createRuntime();
  const host = trackedHost();
  const session = createRouteNavigationSession({
    id: 'canary.route:scene/marker-route',
    sceneType: 'route',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [],
    interactions: [{
      id: 'canary.route:interaction/leave-marker',
      labelTextId: 'canary.route:text/marker-title',
      conditions: [],
      effects: [],
      targetSceneId: 'canary.core:scene/landing',
    }],
  }, {
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    sessionScope: 'marker-route',
    ...resolvers(pack),
  });
  await session.init();
  const result = await session.handle({ type: 'route.choose', interactionId: 'canary.route:interaction/leave-marker' });
  assert.equal(result.status, 'committed');
  assert.deepEqual(host.handoffs, [{
    fromSceneId: 'canary.route:scene/marker-route',
    targetSceneId: 'canary.core:scene/landing',
    entryAlreadyCommitted: true,
  }]);
  await session.dispose();
  assert.deepEqual(host.counts(), { subscriptions: 0, media: 0, mediaStops: 0, hasInput: false });
});

test('static scene target entry is included in its one interaction command before navigation', async () => {
  const pack = await canary('amber');
  const target = ContentRegistry.fromPack(pack).get('canary.core:scene/landing');
  const payloads = [];
  const runtime = createRuntime({ rejectSceneTargetEntry: true, onScene: (payload) => payloads.push(payload) });
  const host = trackedHost();
  const session = createScenePresentationSession({
    id: 'canary.route:scene/static-source',
    sceneType: 'static',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [{ op: 'pressure.change', delta: 1 }],
    interactions: [{
      id: 'canary.route:interaction/static-target',
      labelTextId: 'canary.route:text/marker-title',
      conditions: [],
      effects: [{ op: 'pressure.change', delta: 1 }],
      targetSceneId: target.id,
    }],
  }, {
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    sessionScope: 'static-atomic-target',
    ...resolvers(pack),
  });
  await session.init();
  const result = await session.handle({ type: 'scene.interact', interactionId: 'canary.route:interaction/static-target' });
  assert.equal(result.status, 'rejected');
  assert.deepEqual(payloads[1].targetEntry, {
    sceneId: target.id,
    entryConditions: target.entryConditions,
    effects: target.onEnter,
  });
  assert.equal(host.navigations.length, 0);
  await session.dispose();
});

test('static scene dialogue handoff commits the dialogue start entry exactly once before the generic dialogue session renders', async () => {
  const pack = await canary('amber');
  const baseRegistry = ContentRegistry.fromPack(pack);
  const source = {
    id: 'canary.route:scene/dialogue-source',
    sceneType: 'static',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [{ op: 'pressure.change', delta: 1 }],
    interactions: [{
      id: 'canary.route:interaction/dialogue-target',
      labelTextId: 'canary.route:text/marker-title',
      conditions: [],
      effects: [{ op: 'pressure.change', delta: 1 }],
      targetDialogueId: 'canary.core:dialogue/greeting',
    }],
  };
  const greeting = {
    ...baseRegistry.get('canary.core:dialogue/greeting'),
    nodes: [{
      ...baseRegistry.get('canary.core:dialogue/greeting').nodes[0],
      conditions: [{ op: 'pressure.at-least', minimum: 1 }],
      effects: [{ op: 'pressure.change', delta: 2 }],
    }],
  };
  const registry = {
    get: (id) => id === greeting.id ? greeting : baseRegistry.get(id),
    category: (name) => baseRegistry.category(name),
  };
  const scenePayloads = [];
  const dialoguePayloads = [];
  const runtime = createRuntime({
    onScene: (payload) => scenePayloads.push(payload),
    onDialogue: (payload) => dialoguePayloads.push(payload),
  });
  const host = trackedHost();
  const scene = createScenePresentationSession(source, {
    runtimeContext: runtime.context,
    contentRegistry: registry,
    presentationHost: host.host,
    sessionScope: 'scene-dialogue-handoff',
    ...resolvers(pack),
  });

  await scene.init();
  const result = await scene.handle({ type: 'scene.interact', interactionId: source.interactions[0].id });
  assert.equal(result.status, 'committed');
  assert.deepEqual(scenePayloads[1].targetEntry, {
    dialogueId: greeting.id,
    nodeId: 'welcome',
    entryConditions: greeting.nodes[0].conditions,
    effects: greeting.nodes[0].effects,
  });
  assert.deepEqual(host.handoffs, [{
    fromSceneId: source.id,
    targetDialogueId: greeting.id,
    entryAlreadyCommitted: true,
  }]);

  const dialogue = createDialogueReactionSession(greeting.id, {
    runtimeContext: runtime.context,
    contentRegistry: registry,
    roleBindings: pack.campaign.roleBindings,
    resolveVariables: () => ({ term: { vocabularyId: 'canary.core:vocabulary/lantern' } }),
    presentationHost: host.host,
    sessionScope: 'scene-dialogue-target',
    entryAlreadyCommitted: true,
    ...resolvers(pack),
  });
  await dialogue.init();
  assert.equal(dialoguePayloads.length, 0, 'the handoff must not replay dialogue start effects');
  await Promise.all([dialogue.dispose(), scene.dispose()]);
});

test('rejected static scene dialogue target sends no handoff and leaves the dialogue entry unstarted', async () => {
  const pack = await canary('amber');
  const target = ContentRegistry.fromPack(pack).get('canary.core:dialogue/greeting');
  const scenePayloads = [];
  const dialoguePayloads = [];
  const runtime = createRuntime({
    rejectSceneTargetEntry: true,
    onScene: (payload) => scenePayloads.push(payload),
    onDialogue: (payload) => dialoguePayloads.push(payload),
  });
  const host = trackedHost();
  const session = createScenePresentationSession({
    id: 'canary.route:scene/dialogue-rejected-source',
    sceneType: 'static',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [{ op: 'pressure.change', delta: 1 }],
    interactions: [{
      id: 'canary.route:interaction/dialogue-rejected-target',
      labelTextId: 'canary.route:text/marker-title',
      conditions: [],
      effects: [{ op: 'pressure.change', delta: 1 }],
      targetDialogueId: target.id,
    }],
  }, {
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    sessionScope: 'scene-dialogue-rejected',
    ...resolvers(pack),
  });
  await session.init();
  const result = await session.handle({ type: 'scene.interact', interactionId: 'canary.route:interaction/dialogue-rejected-target' });
  assert.equal(result.status, 'rejected');
  assert.deepEqual(scenePayloads[1].targetEntry, {
    dialogueId: target.id,
    nodeId: target.startNodeId,
    entryConditions: target.nodes[0].conditions,
    effects: target.nodes[0].effects,
  });
  assert.equal(host.handoffs.length, 0);
  assert.equal(dialoguePayloads.length, 0);
  await session.dispose();
});

test('concurrent scene input is serialized, so a committed handoff cannot be duplicated', async () => {
  const pack = await canary('amber');
  const target = ContentRegistry.fromPack(pack).get('canary.core:scene/landing');
  const runtime = createRuntime();
  const host = trackedHost();
  const session = createScenePresentationSession({
    id: 'canary.route:scene/single-flight-source',
    sceneType: 'static',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [],
    interactions: [{
      id: 'canary.route:interaction/single-flight-target',
      labelTextId: 'canary.route:text/marker-title',
      conditions: [],
      effects: [],
      targetSceneId: target.id,
    }],
  }, {
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    sessionScope: 'single-flight-source',
    ...resolvers(pack),
  });
  await session.init();
  const first = session.handle({ type: 'scene.interact', interactionId: 'canary.route:interaction/single-flight-target' });
  const second = session.handle({ type: 'scene.interact', interactionId: 'canary.route:interaction/single-flight-target' });
  assert.equal((await first).status, 'committed');
  await assert.rejects(second, /already committed/);
  assert.equal(host.navigations.length, 1);
  await session.dispose();
});

test('internal route target entry is preflighted in the same command, so a rejected target commits no partial route effects', async () => {
  const pack = await canary('amber');
  const source = {
    id: 'canary.route:scene/source',
    sceneType: 'route',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [],
    onEnter: [],
    onExit: [{ op: 'pressure.change', delta: 1 }],
    interactions: [{
      id: 'canary.route:interaction/to-target',
      labelTextId: 'canary.route:text/marker-title',
      conditions: [],
      effects: [{ op: 'pressure.change', delta: 1 }],
      targetSceneId: 'canary.route:scene/target',
    }],
  };
  const target = {
    id: 'canary.route:scene/target',
    sceneType: 'route',
    assetRefs: [],
    textRefs: ['canary.route:text/marker-title'],
    entryConditions: [{ op: 'pressure.at-least', minimum: 99 }],
    onEnter: [{ op: 'pressure.change', delta: 1 }],
    onExit: [],
    interactions: [],
  };
  const byId = new Map([[source.id, source], [target.id, target]]);
  const registry = { get: (id) => byId.get(id) ?? (() => { throw new Error(`Missing ${id}`); })(), category: () => [] };
  const routePayloads = [];
  const runtime = createRuntime({ rejectRouteTargetEntry: true, onRoute: (payload) => routePayloads.push(payload) });
  const observed = [];
  runtime.context.subscribe('active.route', (event) => observed.push(event));
  const host = trackedHost();
  const session = createRouteNavigationSession(source, {
    runtimeContext: runtime.context,
    contentRegistry: registry,
    presentationHost: host.host,
    sessionScope: 'route-atomic-target',
    ...resolvers(pack),
  });
  await session.init();
  const result = await session.handle({ type: 'route.choose', interactionId: 'canary.route:interaction/to-target' });
  assert.equal(result.status, 'rejected');
  assert.equal(routePayloads[1].targetEntry.sceneId, target.id);
  assert.deepEqual(routePayloads[1].targetEntry.entryConditions, target.entryConditions);
  assert.equal(observed.length, 1);
  assert.equal(host.models.length, 1);
  await session.dispose();
});

test('real Chapter 1 route-to-forest handoff carries forest entry in one rejected command and never exposes the finale', async () => {
  const pack = await chapterPack();
  const route = pack.registries.scenes.find((scene) => scene.id === 'urman.chapter1:scene/zirat-road');
  const forest = pack.registries.scenes.find((scene) => scene.id === 'urman.chapter1:scene/forest');
  assert.ok(route);
  assert.ok(forest);
  const payloads = [];
  const runtime = createRuntime({ rejectRouteTargetEntry: true, onRoute: (payload) => payloads.push(payload) });
  const host = trackedHost();
  const session = createRouteNavigationSession(route, {
    runtimeContext: runtime.context,
    contentRegistry: ContentRegistry.fromPack(pack),
    presentationHost: host.host,
    sessionScope: 'chapter-route-forest',
    ...resolvers(pack),
  });
  await session.init();
  const result = await session.handle({ type: 'route.choose', interactionId: 'urman.chapter1:interaction/zirat-road-to-forest' });
  assert.equal(result.status, 'rejected');
  assert.deepEqual(payloads[1].targetEntry, {
    sceneId: forest.id,
    entryConditions: forest.entryConditions,
    effects: forest.onEnter,
  });
  assert.equal(host.handoffs.length, 0);
  assert.equal(host.models.length, 1);
  await session.dispose();
});

test('active providers contain no fixed campaign ownership, raw asset path or browser global escape hatch', async () => {
  const { readFile, readdir } = await import('node:fs/promises');
  const path = await import('node:path');
  const root = 'src/runtime/modules/active';
  const files = (await readdir(root)).filter((name) => name.endsWith('.mjs')).sort();
  for (const name of files) {
    const source = await readFile(path.join(root, name), 'utf8');
    assert.doesNotMatch(source, /(?:char_|clue_|quest_)[a-z0-9_]+/i, name);
    assert.doesNotMatch(source, /\/assets\//, name);
    assert.doesNotMatch(source, /window\.URMAN/, name);
    assert.doesNotMatch(source, /\b(?:localStorage|sessionStorage)\b/, name);
  }
});
