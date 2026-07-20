import { CapabilityHost } from '../capabilities/capability-host.mjs';
import { canonicalJson } from '../contracts/json-value.mjs';
import { createOldPcCapabilityDescriptor } from '../modules/oldpc/oldpc-capability.mjs';
import { createActiveSceneDescriptors } from '../modules/active/active-provider-descriptors.mjs';
import { createDialogueReactionSession } from '../modules/active/dialogue-reaction-provider.mjs';
import { RuntimeKernel } from '../kernel/runtime-kernel.mjs';
import { createQuestInstance, reduceQuestLifecycle } from '../quests/quest-reducer.mjs';
import {
  CapabilityRegistry,
  ConditionRegistry,
  ContentRegistry,
  EffectRegistry,
  SceneRegistry,
} from '../registries/runtime-registries.mjs';
import { AudioResolver } from '../media/audio-resolver.mjs';
import { AssetResolver, TextResolver } from '../resolvers/content-resolvers.mjs';
import { decodeGameSnapshotV2, encodeGameSnapshotV2, restoreGameSnapshotV2 } from '../persistence/game-snapshot-v2.mjs';
import { DeterministicScheduler } from '../world/deterministic-scheduler.mjs';
import { LogicalClock } from '../world/logical-clock.mjs';
import { OwnerRngStreams } from '../world/seeded-rng.mjs';

const OLD_PC_INSTANCE_ID = 'urman.oldpc:instance/archive-hub';
const OLD_PC_MODULE_ID = 'urman.oldpc';

function required(value, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new TypeError(`${label} must be an object.`);
  return value;
}

function array(value, label) {
  if (!Array.isArray(value)) throw new TypeError(`${label} must be an array.`);
  return value;
}

function contentId(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty content ID.`);
  return value;
}

function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty string.`);
  return value;
}

function immutable(value) {
  return Object.freeze(structuredClone(value));
}

/**
 * A presentation session has no mutable owner state. Its occurrence namespace
 * is instead an exact read-only identity of the run state at creation, so a
 * revisited source after a committed handoff cannot collide with its former
 * input, while recreating the same snapshot preserves duplicate filtering.
 */
function runtimeSessionScope(kind, contentIdValue, runtimeContext, campaignFingerprint) {
  return `runtime-session:${canonicalJson({
    campaignFingerprint,
    kind: nonEmptyString(kind, 'Runtime session kind'),
    contentId: contentId(contentIdValue, 'Runtime session content ID'),
    state: runtimeContext.select((state) => state),
  }, 'Runtime session scope')}`;
}

function replace(state, key, value) {
  return Object.freeze({ op: 'state.set', key, value: immutable(value) });
}

function initialState(services) {
  const quests = {};
  for (const definition of services.contentRegistry.category('quests')) {
    quests[definition.id] = createQuestInstance(definition, {
      questInstanceId: `campaign:${definition.id}`,
      evaluateCondition: (condition, state) => evaluateConditions([condition], state, services.conditionRegistry),
      evaluationContext: Object.freeze({}),
    });
  }
  return Object.freeze({
    knowledge: Object.freeze({}),
    vocabulary: Object.freeze({}),
    npc: Object.freeze({}),
    beats: Object.freeze({}),
    pressure: 0,
    routes: Object.freeze([]),
    presentation: Object.freeze({ requestedSceneIds: Object.freeze([]), openedDocumentIds: Object.freeze([]) }),
    quests: Object.freeze(quests),
  });
}

function conditionDescriptors() {
  return Object.freeze([
    {
      op: 'all',
      evaluate: (condition, context) => array(condition.conditions, 'all.conditions').every((entry) => context.evaluate(entry)),
    },
    {
      op: 'not',
      evaluate: (condition, context) => !context.evaluate(required(condition.condition, 'not.condition')),
    },
    {
      op: 'knowledge.status',
      evaluate: (condition, { state }) => state.knowledge?.[contentId(condition.knowledgeId, 'knowledge.status.knowledgeId')] === condition.status,
    },
    {
      op: 'vocabulary.status',
      evaluate: (condition, { state }) => state.vocabulary?.[contentId(condition.vocabularyId, 'vocabulary.status.vocabularyId')] === condition.status,
    },
    {
      op: 'npc.state',
      evaluate: (condition, { state }) => state.npc?.[contentId(condition.characterId, 'npc.state.characterId')]?.[nonEmptyString(condition.stateKey, 'npc.state.stateKey')] === condition.value,
    },
    {
      op: 'beat.state',
      evaluate: (condition, { state }) => state.beats?.[contentId(condition.beatId, 'beat.state.beatId')] === condition.state,
    },
  ]);
}

function effectDescriptors() {
  return Object.freeze([
    {
      op: 'knowledge.set-status',
      plan: (effect, { state }) => {
        const knowledgeId = contentId(effect.knowledgeId, 'knowledge.set-status.knowledgeId');
        return Object.freeze({ effects: [replace(state, 'knowledge', { ...(state.knowledge ?? {}), [knowledgeId]: effect.status })] });
      },
    },
    {
      op: 'vocabulary.set-status',
      plan: (effect, { state }) => {
        const vocabularyId = contentId(effect.vocabularyId, 'vocabulary.set-status.vocabularyId');
        return Object.freeze({ effects: [replace(state, 'vocabulary', { ...(state.vocabulary ?? {}), [vocabularyId]: effect.status })] });
      },
    },
    {
      op: 'npc.set-state',
      plan: (effect, { state }) => {
        const characterId = contentId(effect.characterId, 'npc.set-state.characterId');
        const stateKey = nonEmptyString(effect.stateKey, 'npc.set-state.stateKey');
        return Object.freeze({ effects: [replace(state, 'npc', {
          ...(state.npc ?? {}),
          [characterId]: { ...(state.npc?.[characterId] ?? {}), [stateKey]: effect.value },
        })] });
      },
    },
    {
      op: 'beat.set-state',
      plan: (effect, { state }) => {
        const beatId = contentId(effect.beatId, 'beat.set-state.beatId');
        return Object.freeze({ effects: [replace(state, 'beats', { ...(state.beats ?? {}), [beatId]: effect.state })] });
      },
    },
    {
      op: 'pressure.change',
      plan: (effect, { state }) => {
        if (!Number.isSafeInteger(effect.delta)) throw new TypeError('pressure.change.delta must be a safe integer.');
        return Object.freeze({ effects: [replace(state, 'pressure', Math.max(0, Math.min(3, (state.pressure ?? 0) + effect.delta)))] });
      },
    },
    {
      op: 'route.unlock',
      plan: (effect, { state }) => {
        const routeNodeId = contentId(effect.routeNodeId, 'route.unlock.routeNodeId');
        const routes = new Set(array(state.routes ?? [], 'runtime.routes'));
        routes.add(routeNodeId);
        return Object.freeze({ effects: [replace(state, 'routes', [...routes].sort())] });
      },
    },
    {
      op: 'audio.request',
      plan: (effect) => Object.freeze({ events: [{ type: 'runtime.audio.requested', payload: { assetId: contentId(effect.assetId, 'audio.request.assetId') } }] }),
    },
    {
      op: 'scene.request',
      plan: (effect, { state }) => {
        const sceneId = contentId(effect.sceneId, 'scene.request.sceneId');
        const requested = new Set(array(state.presentation?.requestedSceneIds ?? [], 'runtime.presentation.requestedSceneIds'));
        requested.add(sceneId);
        return Object.freeze({ effects: [replace(state, 'presentation', {
          ...(state.presentation ?? {}),
          requestedSceneIds: [...requested].sort(),
          openedDocumentIds: state.presentation?.openedDocumentIds ?? [],
        })] });
      },
    },
  ]);
}

function evaluateConditions(entries, state, registry) {
  const evaluate = (condition) => registry.evaluate(condition, { state, evaluate });
  for (const condition of array(entries, 'runtime conditions')) {
    const accepted = evaluate(condition);
    if (!accepted) return false;
  }
  return true;
}

function effectPlan(entries, state, registry) {
  let staged = structuredClone(state);
  const effects = [];
  const events = [];
  for (const effect of array(entries, 'runtime effects')) {
    const plan = registry.plan(effect, { state: immutable(staged) }) ?? {};
    for (const stateEffect of array(plan.effects ?? [], 'effect plan.effects')) {
      if (stateEffect.op !== 'state.set') throw new TypeError(`Runtime bootstrap does not accept ${stateEffect.op}.`);
      staged[stateEffect.key] = structuredClone(stateEffect.value);
      effects.push(stateEffect);
    }
    for (const event of array(plan.events ?? [], 'effect plan.events')) events.push(event);
  }
  return Object.freeze({ effects, events });
}

function applyStateEffects(state, effects) {
  const next = structuredClone(state);
  for (const effect of effects) {
    if (effect.op !== 'state.set') throw new TypeError(`Runtime bootstrap does not accept ${effect.op}.`);
    next[effect.key] = structuredClone(effect.value);
  }
  return next;
}

function advanceQuests(state, services) {
  let staged = structuredClone(state);
  const effects = [];
  const events = [];
  const applyReduction = (definition, reduced) => {
    if (reduced.plan.rejection) return false;
    const questEffects = reduced.plan.effects.filter((effect) => effect.key !== '__quest_instance__');
    staged = applyStateEffects(staged, questEffects);
    staged.quests = { ...(staged.quests ?? {}), [definition.id]: structuredClone(reduced.nextState) };
    effects.push(...questEffects, replace(staged, 'quests', staged.quests));
    events.push(...reduced.plan.events);
    return true;
  };
  for (const definition of services.contentRegistry.category('quests')) {
    let instance = staged.quests?.[definition.id];
    if (!instance) continue;
    const reduce = (command) => reduceQuestLifecycle({
      definition,
      instance,
      command,
      stateKey: '__quest_instance__',
      evaluateCondition: (condition, evaluationState) => evaluateConditions([condition], evaluationState, services.conditionRegistry),
      evaluationContext: staged,
      planEffects: (entries) => effectPlan(entries, staged, services.effectRegistry),
    });
    const refreshed = reduce({ type: 'quest.refresh' });
    if (!applyReduction(definition, refreshed)) continue;
    instance = staged.quests[definition.id];
    const stage = definition.stages[instance.stageIndex];
    for (const objective of stage.objectives) {
      if (instance.objectives?.[stage.id]?.[objective.id]?.status !== 'active') continue;
      const completed = reduceQuestLifecycle({
        definition,
        instance,
        command: { type: 'objective.complete', objectiveId: objective.id },
        stateKey: '__quest_instance__',
        evaluateCondition: (condition, evaluationState) => evaluateConditions([condition], evaluationState, services.conditionRegistry),
        evaluationContext: staged,
        planEffects: (entries) => effectPlan(entries, staged, services.effectRegistry),
      });
      if (applyReduction(definition, completed)) instance = staged.quests[definition.id];
    }
  }
  return Object.freeze({ effects, events });
}

function rejected(message) {
  return Object.freeze({ rejection: Object.freeze({ code: 'ContentConditionRejected', message }) });
}

function commandPlan(payload, state, services) {
  required(payload, 'Runtime command payload');
  const ownConditions = payload.conditions ?? payload.entryConditions ?? [];
  if (!evaluateConditions(ownConditions, state, services.conditionRegistry)) return rejected('Content conditions rejected this action.');
  const target = payload.targetEntry ?? payload.targetNode ?? null;
  if (target && !evaluateConditions(target.entryConditions ?? target.conditions ?? [], state, services.conditionRegistry)) {
    return rejected('Target content conditions rejected this action.');
  }
  const entries = [
    ...(payload.effects ?? []),
    ...(payload.exitEffects ?? []),
    ...(target?.effects ?? []),
  ];
  const planned = effectPlan(entries, state, services.effectRegistry);
  const advanced = advanceQuests(applyStateEffects(state, planned.effects), services);
  return Object.freeze({
    effects: [...planned.effects, ...advanced.effects],
    events: [...planned.events, ...advanced.events],
    value: Object.freeze({ action: payload.action ?? 'enter', targetId: payload.targetSceneId ?? target?.sceneId ?? target?.nodeId ?? null }),
  });
}

function oldPcOpenPlan(command, state) {
  const payload = required(command.payload, 'Old-PC open payload');
  const knowledge = { ...(state.knowledge ?? {}) };
  for (const knowledgeId of array(payload.knowledgeRefs, 'Old-PC knowledgeRefs')) knowledge[contentId(knowledgeId, 'Old-PC knowledge ref')] = 'confirmed';
  const opened = new Set(array(state.presentation?.openedDocumentIds ?? [], 'runtime.presentation.openedDocumentIds'));
  opened.add(contentId(payload.documentId, 'Old-PC document ID'));
  return Object.freeze({
    effects: [
      replace(state, 'knowledge', knowledge),
      replace(state, 'presentation', {
        ...(state.presentation ?? {}),
        requestedSceneIds: state.presentation?.requestedSceneIds ?? [],
        openedDocumentIds: [...opened].sort(),
      }),
    ],
    events: [{ type: 'oldpc.document.opened', payload: immutable({ documentId: payload.documentId, knowledgeRefs: payload.knowledgeRefs, presentationIds: payload.presentationIds }) }],
  });
}

function oldPcDescriptor(pack, contentRegistry) {
  const definition = contentRegistry.category('capabilities')
    .find((entry) => entry.protocolId === 'urman.oldpc:capability/archive-hub');
  if (!definition) throw new TypeError('Production campaign requires the urman.oldpc archive capability definition.');
  return createOldPcCapabilityDescriptor({
    contentRegistry,
    moduleId: OLD_PC_MODULE_ID,
    protocolId: definition.protocolId,
    exactVersion: definition.exactVersion,
    stateSchemaVersion: definition.stateSchemaVersion,
    isAccessible: (document, state) => document.accessConditions.every((condition) => {
      if (condition.op !== 'knowledge.status') return false;
      return state.knowledge?.[condition.knowledgeId] === condition.status;
    }),
  });
}

function createServices(pack, { resolveFileUrl = (file) => file, locale = 'ru', seed = undefined } = {}) {
  required(pack, 'CompiledContentPack');
  const contentRegistry = ContentRegistry.fromPack(pack);
  const oldPc = oldPcDescriptor(pack, contentRegistry);
  const capabilityDescriptors = Object.freeze([oldPc]);
  const capabilityRegistry = new CapabilityRegistry(pack, capabilityDescriptors);
  const conditionRegistry = new ConditionRegistry(pack, conditionDescriptors());
  const effectRegistry = new EffectRegistry(pack, effectDescriptors());
  const sceneRegistry = new SceneRegistry(pack, createActiveSceneDescriptors());
  const assetResolver = new AssetResolver(pack, { resolveFileUrl });
  const textResolver = new TextResolver(pack);
  const audioResolver = new AudioResolver(pack, { assetResolver, textResolver });
  const services = {
    contentRegistry,
    capabilityDescriptors,
    capabilityRegistry,
    conditionRegistry,
    effectRegistry,
    sceneRegistry,
    assetResolver,
    textResolver,
    audioResolver,
    locale,
    seed: seed === undefined
      ? nonEmptyString(pack.campaignFingerprint, 'Compiled campaign fingerprint')
      : nonEmptyString(seed, 'Runtime seed'),
  };
  return Object.freeze(services);
}

function handlers(services) {
  return Object.freeze({
    'runtime.scene': (command, context) => commandPlan(command.payload, context.state, services),
    'runtime.route': (command, context) => commandPlan(command.payload, context.state, services),
    'runtime.dialogue': (command, context) => commandPlan(command.payload, context.state, services),
    'oldpc.search': (command) => Object.freeze({ events: [{ type: 'oldpc.search', payload: immutable(command.payload) }] }),
    'oldpc.document.open': (command, context) => oldPcOpenPlan(command, context.state),
    'oldpc.document.save': (command) => Object.freeze({ events: [{ type: 'oldpc.document.saved', payload: immutable(command.payload) }] }),
  });
}

function createNewRun(services) {
  const kernel = new RuntimeKernel({
    initialState: initialState(services),
    handlers: handlers(services),
    capabilityRegistry: services.capabilityRegistry,
  });
  const clock = new LogicalClock();
  const rngStreams = new OwnerRngStreams({ seed: services.seed });
  const scheduler = new DeterministicScheduler();
  const capabilityHost = new CapabilityHost({
    runtimeContext: kernel.context,
    clock,
    rngStreams,
    scheduler,
    descriptors: services.capabilityDescriptors,
  });
  capabilityHost.createSession({
    capabilityInstanceId: OLD_PC_INSTANCE_ID,
    protocolId: services.capabilityDescriptors[0].protocolId,
    exactVersion: services.capabilityDescriptors[0].exactVersion,
    config: { moduleId: OLD_PC_MODULE_ID },
  });
  capabilityHost.startSession(OLD_PC_INSTANCE_ID);
  return Object.freeze({ kernel, clock, rngStreams, scheduler, capabilityHost, capabilityInstanceIds: Object.freeze([OLD_PC_INSTANCE_ID]) });
}

/**
 * Composition-side runtime assembly. It receives an already compiled pack and
 * never discovers content or mutates it. Browser rendering is deliberately
 * kept outside this module so the generic runtime boundary remains host-free.
 */
export class RuntimeBootstrap {
  #pack;
  #services;
  #run;
  #persistencePort;
  #presentationPort;

  constructor({ pack, resolveFileUrl = undefined, locale = 'ru', seed = undefined } = {}) {
    this.#pack = required(pack, 'CompiledContentPack');
    this.#services = createServices(pack, { resolveFileUrl, locale, seed });
    this.#run = createNewRun(this.#services);
    // Persistence receives this closed adapter rather than a pack, provider
    // catalog or live run. Only `restore` below may replace `#run`.
    this.#persistencePort = Object.freeze({
      capture: () => this.#captureSnapshot(),
      decode: (snapshot) => decodeGameSnapshotV2(snapshot, {
        pack: this.#pack,
        capabilityDescriptors: this.#services.capabilityDescriptors,
      }),
      restore: async (snapshot) => this.#restoreSnapshot(snapshot),
    });
    // Browser hosts receive this presentation-only facade, never the full
    // bootstrap with its kernel context, registry or capability-host access.
    this.#presentationPort = Object.freeze({
      campaign: this.#pack.campaign,
      createScene: (sceneId, presentationHost, options = undefined) => this.createScene(sceneId, presentationHost, options),
      createDialogue: (dialogueId, presentationHost, options = undefined) => this.createDialogue(dialogueId, presentationHost, options),
      oldPcPresentation: () => this.oldPcPresentation(),
      snapshot: () => this.snapshot(),
      restore: async (snapshot) => this.restore(snapshot),
    });
  }

  get pack() { return this.#pack; }
  get campaign() { return this.#pack.campaign; }
  get campaignFingerprint() { return this.#pack.campaignFingerprint; }
  get runtimeContext() { return this.#run.kernel.context; }
  get contentRegistry() { return this.#services.contentRegistry; }
  get capabilityHost() { return this.#run.capabilityHost; }
  get persistence() { return this.#persistencePort; }
  get presentation() { return this.#presentationPort; }

  /** Narrow web adapter port; callers never receive a raw capability session. */
  oldPcPresentation() {
    return this.#run.capabilityHost.presentation(OLD_PC_INSTANCE_ID);
  }

  createScene(sceneId, presentationHost, { entryAlreadyCommitted = false } = {}) {
    const resolvedSceneId = contentId(sceneId, 'Scene ID');
    const runtimeContext = this.#run.kernel.context;
    return this.#services.sceneRegistry.create(resolvedSceneId, {
      runtimeContext,
      contentRegistry: this.#services.contentRegistry,
      assetResolver: this.#services.assetResolver,
      textResolver: this.#services.textResolver,
      audioResolver: this.#services.audioResolver,
      locale: this.#services.locale,
      presentationHost,
      entryAlreadyCommitted,
      sessionScope: runtimeSessionScope('scene', resolvedSceneId, runtimeContext, this.#pack.campaignFingerprint),
    });
  }

  createDialogue(dialogueId, presentationHost, { entryAlreadyCommitted = false } = {}) {
    const resolvedDialogueId = contentId(dialogueId, 'Dialogue ID');
    const runtimeContext = this.#run.kernel.context;
    return createDialogueReactionSession(resolvedDialogueId, {
      runtimeContext,
      contentRegistry: this.#services.contentRegistry,
      assetResolver: this.#services.assetResolver,
      textResolver: this.#services.textResolver,
      roleBindings: this.#pack.campaign.roleBindings,
      locale: this.#services.locale,
      presentationHost,
      entryAlreadyCommitted,
      sessionScope: runtimeSessionScope('dialogue', resolvedDialogueId, runtimeContext, this.#pack.campaignFingerprint),
    });
  }

  snapshot() {
    return this.#persistencePort.capture();
  }

  async restore(snapshot) {
    return this.#persistencePort.restore(snapshot);
  }

  #captureSnapshot() {
    const bindings = this.#run.capabilityInstanceIds.map((capabilityInstanceId) => Object.freeze({
      capabilityInstanceId,
      protocolId: this.#services.capabilityDescriptors[0].protocolId,
      exactVersion: this.#services.capabilityDescriptors[0].exactVersion,
      config: Object.freeze({ moduleId: OLD_PC_MODULE_ID }),
      claims: Object.freeze([]),
    }));
    const capabilitySnapshots = this.#run.capabilityInstanceIds.map((capabilityInstanceId) => this.#run.capabilityHost.snapshotSession(capabilityInstanceId));
    return encodeGameSnapshotV2({
      pack: this.#pack,
      kernel: this.#run.kernel,
      clock: this.#run.clock,
      rngStreams: this.#run.rngStreams,
      scheduler: this.#run.scheduler,
      capabilityBindings: bindings,
      capabilitySnapshots,
      capabilityDescriptors: this.#services.capabilityDescriptors,
    });
  }

  async #restoreSnapshot(snapshot) {
    const restored = await restoreGameSnapshotV2(snapshot, {
      pack: this.#pack,
      handlers: handlers(this.#services),
      capabilityRegistry: this.#services.capabilityRegistry,
      capabilityDescriptors: this.#services.capabilityDescriptors,
    });
    this.#run = restored;
    return immutable({ campaignFingerprint: this.#pack.campaignFingerprint, capabilityInstanceIds: restored.capabilityInstanceIds });
  }
}

export const PRODUCTION_OLD_PC_INSTANCE_ID = OLD_PC_INSTANCE_ID;
