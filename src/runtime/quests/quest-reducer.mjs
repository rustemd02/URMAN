import { clonePersistedJsonValue } from '../contracts/json-value.mjs';

export const QuestStatus = Object.freeze({ Active: 'active', Completed: 'completed', Failed: 'failed', Cancelled: 'cancelled' });
export const ObjectiveStatus = Object.freeze({ Pending: 'pending', Active: 'active', Completed: 'completed', Failed: 'failed' });

function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty string.`);
  return value;
}

function clone(value, label) { return clonePersistedJsonValue(value, label); }

function requiredDefinition(definition) {
  if (!definition || typeof definition !== 'object' || Array.isArray(definition)
    || typeof definition.id !== 'string' || !definition.id || !Array.isArray(definition.stages) || definition.stages.length === 0) {
    throw new TypeError('Quest definition is invalid.');
  }
  return definition;
}

function stageByIndex(definition, stageIndex) {
  if (!Number.isSafeInteger(stageIndex) || stageIndex < 0 || stageIndex >= definition.stages.length) {
    throw new TypeError('Quest instance has an invalid active stage.');
  }
  return definition.stages[stageIndex];
}

function objectiveMap(stage) {
  const result = new Map();
  for (const objective of stage.objectives ?? []) {
    if (!objective || typeof objective.id !== 'string' || !objective.id || result.has(objective.id)) {
      throw new TypeError('Quest stage has invalid or duplicate objective IDs.');
    }
    result.set(objective.id, objective);
  }
  if (!result.size) throw new TypeError('Quest stage needs objectives.');
  const ids = stage.composition?.objectiveIds;
  if (!Array.isArray(ids) || ids.length === 0 || ids.some((id) => !result.has(id))) {
    throw new TypeError('Quest stage composition references an invalid objective.');
  }
  if (!['all', 'any', 'threshold'].includes(stage.composition.mode)) throw new TypeError('Quest stage composition is invalid.');
  if (stage.composition.mode === 'threshold'
    && (!Number.isSafeInteger(stage.composition.threshold) || stage.composition.threshold < 1 || stage.composition.threshold > ids.length)) {
    throw new TypeError('Quest stage threshold is invalid.');
  }
  return result;
}

function conditionsPass(conditions, evaluateCondition, evaluationContext) {
  if (!Array.isArray(conditions)) throw new TypeError('Quest conditions must be an array.');
  return conditions.every((condition) => evaluateCondition(condition, evaluationContext) === true);
}

function activeCapabilityMap(state) {
  if (!state.activeCapabilities || typeof state.activeCapabilities !== 'object' || Array.isArray(state.activeCapabilities)) {
    state.activeCapabilities = {};
  }
  return state.activeCapabilities;
}

function requestCapability(state, stage, objective, objectiveId, capabilityRequests, configResolver) {
  if (!objective.capability) return;
  const capabilityInstance = capabilityInstanceId(state.questInstanceId, stage.id, objectiveId);
  const activeCapabilities = activeCapabilityMap(state);
  if (Object.hasOwn(activeCapabilities, capabilityInstance)) return;
  const config = clone(configResolver(objective.capability.configRef), `Quest capability config ${objective.capability.configRef}`);
  const request = Object.freeze({
    capabilityInstanceId: capabilityInstance,
    protocolId: objective.capability.protocolId,
    exactVersion: objective.capability.exactVersion,
    configRef: objective.capability.configRef,
    config,
    outcomeSchemaRef: objective.capability.outcomeSchemaRef,
  });
  activeCapabilities[capabilityInstance] = { ...request };
  capabilityRequests.push(request);
}

function activateStage(definition, state, evaluateCondition, evaluationContext, capabilityRequests, configResolver) {
  const stage = stageByIndex(definition, state.stageIndex);
  const objectives = objectiveMap(stage);
  const statuses = state.objectives[stage.id] ?? {};
  for (const [objectiveId, objective] of objectives) {
    if (!Object.hasOwn(statuses, objectiveId) || statuses[objectiveId].status === ObjectiveStatus.Pending) {
      const active = conditionsPass(objective.startConditions, evaluateCondition, evaluationContext);
      statuses[objectiveId] = { status: active ? ObjectiveStatus.Active : ObjectiveStatus.Pending };
    }
    if (statuses[objectiveId].status === ObjectiveStatus.Active) {
      requestCapability(state, stage, objective, objectiveId, capabilityRequests, configResolver);
    }
  }
  state.objectives[stage.id] = statuses;
}

function isStageComplete(stage, objectives) {
  const configured = stage.composition.objectiveIds;
  const completed = configured.filter((objectiveId) => objectives[objectiveId]?.status === ObjectiveStatus.Completed).length;
  if (stage.composition.mode === 'all') {
    // Optional objectives may enrich a route but cannot block its main path.
    const required = configured.filter((objectiveId) => !stage.objectives.find((objective) => objective.id === objectiveId).optional);
    return required.every((objectiveId) => objectives[objectiveId]?.status === ObjectiveStatus.Completed);
  }
  if (stage.composition.mode === 'any') return completed > 0;
  return completed >= stage.composition.threshold;
}

function snapshotCheckpoint(state) {
  return clone({
    stageIndex: state.stageIndex,
    objectives: state.objectives,
    capabilitySnapshots: state.capabilitySnapshots,
    generatedBindings: state.generatedBindings,
  }, 'Quest checkpoint');
}

function restoreCheckpoint(state, checkpoint = state.checkpoint) {
  if (!checkpoint || typeof checkpoint !== 'object' || Array.isArray(checkpoint)) throw new TypeError('Quest retry requires a checkpoint.');
  state.stageIndex = checkpoint.stageIndex;
  state.objectives = structuredClone(clone(checkpoint.objectives, 'Quest checkpoint objectives'));
  state.capabilitySnapshots = structuredClone(clone(checkpoint.capabilitySnapshots, 'Quest checkpoint capability snapshots'));
  state.generatedBindings = structuredClone(clone(checkpoint.generatedBindings, 'Quest checkpoint generated bindings'));
  state.status = QuestStatus.Active;
}

function lifecycleEvent(state, name, payload = {}) {
  return Object.freeze({
    type: 'quest.lifecycle',
    payload: clone({
      questId: state.questId,
      questInstanceId: state.questInstanceId,
      name,
      stageIndex: state.stageIndex,
      ...payload,
    }, 'Quest lifecycle event'),
  });
}

function normalizeFragment(fragment, label) {
  if (!fragment || typeof fragment !== 'object' || Array.isArray(fragment)) throw new TypeError(`${label} must return a transaction fragment.`);
  const read = (key) => {
    const value = fragment[key] ?? [];
    if (!Array.isArray(value)) throw new TypeError(`${label}.${key} must be an array.`);
    return value;
  };
  return { effects: read('effects'), events: read('events'), claims: read('claims') };
}

function effectsFragment(effects, planEffects, evaluationContext) {
  if (!Array.isArray(effects)) throw new TypeError('Quest effects must be an array.');
  return normalizeFragment(planEffects(effects, evaluationContext), 'Quest effect planner');
}

function appendFragment(target, fragment) {
  target.effects.push(...fragment.effects);
  target.events.push(...fragment.events);
  target.claims.push(...fragment.claims);
}

function capabilityInstanceId(questInstanceId, stageId, objectiveId) {
  return `capability:${questInstanceId}:${stageId}:${objectiveId}`;
}

function normalizedCleanupFragment(fragment, label) {
  if (fragment === undefined) return { releaseClaimOwnerIds: [], releaseClaimLifecycleScopes: [] };
  if (!fragment || typeof fragment !== 'object' || Array.isArray(fragment)) throw new TypeError(`${label} must return a cleanup fragment.`);
  const ids = (field) => {
    const value = fragment[field] ?? [];
    if (!Array.isArray(value) || value.some((id) => typeof id !== 'string' || !id)) throw new TypeError(`${label}.${field} must be an array of non-empty strings.`);
    return value;
  };
  return { releaseClaimOwnerIds: ids('releaseClaimOwnerIds'), releaseClaimLifecycleScopes: ids('releaseClaimLifecycleScopes') };
}

function prepareCapabilityCleanup(state, capabilityLifecycle, reason, instanceIds = Object.keys(activeCapabilityMap(state)).sort()) {
  const capabilityInstanceIds = [...new Set(instanceIds)].sort();
  if (capabilityInstanceIds.length === 0) return null;
  const supplied = capabilityLifecycle?.prepareCleanup?.(Object.freeze({ capabilityInstanceIds: Object.freeze(capabilityInstanceIds), reason }));
  const fragment = normalizedCleanupFragment(supplied, 'Capability lifecycle cleanup');
  // Provider claims are owner-scoped to capability instance IDs. The default
  // plan is correct even without a host-specific adapter; the adapter may add
  // lifecycle scopes but cannot replace these owners.
  for (const capabilityInstanceId of capabilityInstanceIds) delete state.activeCapabilities[capabilityInstanceId];
  return Object.freeze({
    capabilityInstanceIds: Object.freeze(capabilityInstanceIds),
    reason,
    releaseClaimOwnerIds: Object.freeze([...new Set([...capabilityInstanceIds, ...fragment.releaseClaimOwnerIds])].sort()),
    releaseClaimLifecycleScopes: Object.freeze([...new Set(fragment.releaseClaimLifecycleScopes)].sort()),
  });
}

/** Stable occurrence identity independent of campaign/module ordering. */
export function questObjectiveOccurrenceId({ questInstanceId, stageId, objectiveId, triggerId } = {}) {
  return `quest-trigger:${nonEmptyString(questInstanceId, 'Quest instance ID')}:${nonEmptyString(stageId, 'Quest stage ID')}:${nonEmptyString(objectiveId, 'Quest objective ID')}:${nonEmptyString(triggerId, 'Quest trigger ID')}`;
}

/**
 * Builds portable instance state. Definitions remain untouched and can be
 * swapped or reordered by a campaign without invalidating instance identity.
 */
function initializeQuestRun(definition, { questInstanceId, generatedBindings = {}, evaluateCondition = () => true, evaluationContext = {}, configResolver = () => ({}) } = {}) {
  requiredDefinition(definition);
  const state = {
    questId: definition.id,
    questInstanceId: nonEmptyString(questInstanceId, 'Quest instance ID'),
    status: QuestStatus.Active,
    attempt: 1,
    stageIndex: 0,
    objectives: {},
    generatedBindings: clone(generatedBindings, 'Quest generated bindings'),
    capabilitySnapshots: {},
    activeCapabilities: {},
    checkpoint: null,
    originCheckpoint: null,
  };
  const capabilityRequests = [];
  activateStage(definition, state, evaluateCondition, evaluationContext, capabilityRequests, configResolver);
  state.originCheckpoint = snapshotCheckpoint(state);
  state.checkpoint = state.originCheckpoint;
  return Object.freeze({
    instance: clone(state, 'Quest instance'),
    capabilityRequests: Object.freeze(capabilityRequests),
  });
}

export function createQuestInstance(definition, options = {}) {
  return initializeQuestRun(definition, options).instance;
}

/** Use this variant when the initial active objectives need provider sessions. */
export function createQuestRun(definition, options = {}) {
  return initializeQuestRun(definition, options);
}

/**
 * Reduce an authored lifecycle command to a single kernel transaction plan.
 * Condition/effect meanings are supplied by the registries; this reducer never
 * branches on narrative IDs or objective mechanic kinds.
 */
export function reduceQuestLifecycle({ definition, instance, command, stateKey, evaluateCondition = () => true, planEffects = () => ({}), configResolver = () => ({}), evaluationContext = {}, cleanupPlan = {}, capabilityLifecycle = undefined } = {}) {
  requiredDefinition(definition);
  if (!instance || typeof instance !== 'object' || Array.isArray(instance)) throw new TypeError('Quest instance is required.');
  if (instance.questId !== definition.id) throw new TypeError('Quest instance definition ID mismatch.');
  if (!command || typeof command !== 'object' || Array.isArray(command) || typeof command.type !== 'string' || !command.type) {
    throw new TypeError('Quest lifecycle command is invalid.');
  }
  stateKey = nonEmptyString(stateKey, 'Quest state key');
  // Boundary cloning validates persisted JSON; structuredClone then gives this
  // reducer a private mutable draft which is frozen again at the return seam.
  const state = structuredClone(clone(instance, 'Quest instance'));
  const fragments = { effects: [], events: [], claims: [] };
  const capabilityRequests = [];
  const capabilityCleanups = [];
  const releaseClaimOwnerIds = [...(cleanupPlan.releaseClaimOwnerIds ?? [])];
  const releaseClaimLifecycleScopes = [...(cleanupPlan.releaseClaimLifecycleScopes ?? [])];
  const appendCapabilityCleanup = (cleanup) => {
    if (!cleanup) return;
    capabilityCleanups.push(cleanup);
    releaseClaimOwnerIds.push(...cleanup.releaseClaimOwnerIds);
    releaseClaimLifecycleScopes.push(...cleanup.releaseClaimLifecycleScopes);
  };
  const reject = (code, message, details = {}) => Object.freeze({
    nextState: clone(instance, 'Unchanged quest instance'),
    capabilityRequests: Object.freeze([]),
    capabilityCleanups: Object.freeze([]),
    plan: Object.freeze({ rejection: Object.freeze({ code, message, details: clone(details, 'Quest rejection details') }) }),
  });
  if (state.status !== QuestStatus.Active && !['quest.retry'].includes(command.type)) {
    return reject('QuestNotActive', `Quest ${state.questInstanceId} is not active.`, { status: state.status });
  }

  if (command.type === 'quest.refresh') {
    activateStage(definition, state, evaluateCondition, evaluationContext, capabilityRequests, configResolver);
    fragments.events.push(lifecycleEvent(state, 'refreshed'));
  } else if (command.type === 'objective.complete' || command.type === 'objective.fail') {
    const stage = stageByIndex(definition, state.stageIndex);
    const objectives = objectiveMap(stage);
    const objectiveId = nonEmptyString(command.objectiveId, 'Quest objective ID');
    const objective = objectives.get(objectiveId);
    if (!objective) return reject('UnknownQuestObjective', `Quest objective ${objectiveId} is not active in this stage.`);
    const record = state.objectives[stage.id]?.[objectiveId];
    if (!record || record.status !== ObjectiveStatus.Active) return reject('QuestObjectiveNotActive', `Quest objective ${objectiveId} is not active.`);
    if (command.type === 'objective.complete' && !conditionsPass(objective.completionConditions, evaluateCondition, evaluationContext)) {
      return reject('QuestCompletionConditionsFailed', `Quest objective ${objectiveId} completion conditions failed.`);
    }
    record.status = command.type === 'objective.complete' ? ObjectiveStatus.Completed : ObjectiveStatus.Failed;
    appendCapabilityCleanup(prepareCapabilityCleanup(
      state,
      capabilityLifecycle,
      command.type === 'objective.complete' ? 'objective-complete' : 'objective-fail',
      objective.capability ? [capabilityInstanceId(state.questInstanceId, stage.id, objectiveId)] : [],
    ));
    appendFragment(fragments, effectsFragment(command.type === 'objective.complete' ? objective.completionEffects : objective.failureEffects, planEffects, evaluationContext));
    if (command.type === 'objective.complete' && objective.optional) {
      appendFragment(fragments, effectsFragment(definition.outcomes.optional, planEffects, evaluationContext));
    }
    fragments.events.push(lifecycleEvent(state, command.type === 'objective.complete' ? 'objective-completed' : 'objective-failed', { objectiveId }));
    if (command.type === 'objective.complete' && definition.checkpointPolicy.mode === 'objective') state.checkpoint = snapshotCheckpoint(state);

    if (command.type === 'objective.fail' && !objective.optional) {
      state.status = QuestStatus.Failed;
      appendCapabilityCleanup(prepareCapabilityCleanup(state, capabilityLifecycle, 'quest-failed'));
      appendFragment(fragments, effectsFragment(definition.outcomes.failure, planEffects, evaluationContext));
      fragments.events.push(lifecycleEvent(state, 'failed', { objectiveId }));
    } else if (isStageComplete(stage, state.objectives[stage.id])) {
      if (state.stageIndex === definition.stages.length - 1) {
        state.status = QuestStatus.Completed;
        appendCapabilityCleanup(prepareCapabilityCleanup(state, capabilityLifecycle, 'quest-completed'));
        appendFragment(fragments, effectsFragment(definition.outcomes.success, planEffects, evaluationContext));
        fragments.events.push(lifecycleEvent(state, 'completed'));
      } else {
        const outgoingCapabilityIds = stage.objectives
          .filter((entry) => entry.capability)
          .map((entry) => capabilityInstanceId(state.questInstanceId, stage.id, entry.id))
          .filter((instanceId) => Object.hasOwn(activeCapabilityMap(state), instanceId));
        appendCapabilityCleanup(prepareCapabilityCleanup(state, capabilityLifecycle, 'stage-advance', outgoingCapabilityIds));
        state.stageIndex += 1;
        activateStage(definition, state, evaluateCondition, evaluationContext, capabilityRequests, configResolver);
        if (definition.checkpointPolicy.mode !== 'none') state.checkpoint = snapshotCheckpoint(state);
        fragments.events.push(lifecycleEvent(state, 'stage-advanced'));
      }
    }
  } else if (command.type === 'quest.cancel') {
    if (!definition.cancelPolicy?.allowed) return reject('QuestCancelForbidden', `Quest ${state.questInstanceId} cannot be cancelled.`);
    state.status = QuestStatus.Cancelled;
    appendCapabilityCleanup(prepareCapabilityCleanup(state, capabilityLifecycle, 'quest-cancel'));
    appendFragment(fragments, effectsFragment(definition.cancelPolicy.effects, planEffects, evaluationContext));
    fragments.events.push(lifecycleEvent(state, 'cancelled'));
  } else if (command.type === 'quest.retry') {
    if (state.status !== QuestStatus.Failed) return reject('QuestRetryUnavailable', `Quest ${state.questInstanceId} is not failed.`);
    const policy = definition.retryPolicy;
    if (!policy || policy.mode === 'none' || state.attempt >= policy.maximumAttempts) {
      return reject('QuestRetryExhausted', `Quest ${state.questInstanceId} has no remaining retries.`);
    }
    appendCapabilityCleanup(prepareCapabilityCleanup(state, capabilityLifecycle, 'quest-retry'));
    restoreCheckpoint(state, policy.mode === 'quest' ? state.originCheckpoint : state.checkpoint);
    // Snapshots preserve provider state separately; retry always requests a
    // fresh active objective session after the old owner cleanup transaction.
    state.activeCapabilities = {};
    state.attempt += 1;
    activateStage(definition, state, evaluateCondition, evaluationContext, capabilityRequests, configResolver);
    fragments.events.push(lifecycleEvent(state, 'retried'));
  } else {
    return reject('UnknownQuestLifecycleCommand', `Unknown quest lifecycle command ${command.type}.`);
  }

  fragments.effects.push({ op: 'state.set', key: stateKey, value: clone(state, 'Quest transaction state') });
  return Object.freeze({
    nextState: clone(state, 'Quest next state'),
    capabilityRequests: Object.freeze(capabilityRequests),
    capabilityCleanups: Object.freeze(capabilityCleanups),
    plan: Object.freeze({
      effects: Object.freeze(fragments.effects),
      events: Object.freeze(fragments.events),
      claims: Object.freeze(fragments.claims),
      releaseClaimOwnerIds: Object.freeze([...new Set(releaseClaimOwnerIds)].sort()),
      releaseClaimLifecycleScopes: Object.freeze([...new Set(releaseClaimLifecycleScopes)].sort()),
      value: Object.freeze({ questInstanceId: state.questInstanceId, status: state.status }),
    }),
  });
}
