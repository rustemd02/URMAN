import assert from 'node:assert/strict';
import test from 'node:test';

import { RuntimeKernel } from '../../../src/runtime/kernel/runtime-kernel.mjs';
import { CapabilityRegistry } from '../../../src/runtime/registries/runtime-registries.mjs';
import {
  createQuestRun,
  QuestStatus,
  questObjectiveOccurrenceId,
  reduceQuestLifecycle,
} from '../../../src/runtime/quests/quest-reducer.mjs';

const EMPTY_PACK = Object.freeze({
  campaign: Object.freeze({ capabilityRequirements: Object.freeze([]) }),
  registries: Object.freeze({ capabilities: Object.freeze([]) }),
});

const DEFINITION = Object.freeze({
  id: 'sample:quest/modular-route',
  stages: [
    {
      id: 'arrival',
      objectives: [
        {
          id: 'inspect', optional: false, startConditions: [], completionConditions: [],
          completionEffects: [{ op: 'knowledge.set' }], failureEffects: [],
          capability: { protocolId: 'sample:capability/inspect', exactVersion: '1.0.0', configRef: 'sample:config/inspect', outcomeSchemaRef: 'sample:outcome/inspect' },
        },
        { id: 'optional-note', optional: true, startConditions: [], completionConditions: [], completionEffects: [], failureEffects: [] },
      ],
      composition: { mode: 'all', objectiveIds: ['inspect', 'optional-note'] },
    },
    {
      id: 'archive',
      objectives: [
        { id: 'read', optional: false, startConditions: [], completionConditions: [], completionEffects: [], failureEffects: [] },
        { id: 'ask', optional: false, startConditions: [], completionConditions: [], completionEffects: [], failureEffects: [] },
      ],
      composition: { mode: 'threshold', objectiveIds: ['read', 'ask'], threshold: 1 },
    },
  ],
  outcomes: { success: [{ op: 'quest.set' }], optional: [{ op: 'quest.optional' }], failure: [{ op: 'quest.failed' }] },
  retryPolicy: { mode: 'stage', maximumAttempts: 2 },
  checkpointPolicy: { mode: 'stage' },
  cancelPolicy: { allowed: true, effects: [{ op: 'quest.cancelled' }] },
});

function createRuntimeWithQuest(definition = DEFINITION, { capabilityLifecycle = undefined } = {}) {
  const initial = createQuestRun(definition, {
    questInstanceId: 'instance/modular-route',
    configResolver: (configRef) => ({ configRef }),
  });
  let instance = initial.instance;
  let lastReduction;
  const runtime = new RuntimeKernel({
    capabilityRegistry: new CapabilityRegistry(EMPTY_PACK),
    handlers: {
      quest: (command, context) => {
        const reduced = reduceQuestLifecycle({
          definition,
          instance,
          command: command.payload,
          stateKey: 'questState',
          evaluationContext: context.state,
          configResolver: (configRef) => ({ configRef }),
          planEffects: (effects) => ({ events: effects.map((effect) => ({ type: 'authored.effect', payload: { op: effect.op } })) }),
          capabilityLifecycle,
        });
        lastReduction = reduced;
        if (!reduced.plan.rejection) instance = reduced.nextState;
        return reduced.plan;
      },
    },
  });
  return { runtime, initial, instance: () => instance, lastReduction: () => lastReduction };
}

test('data-driven quest stages support parallel objectives, threshold completion and capability requests', async () => {
  const { runtime, initial, instance } = createRuntimeWithQuest();
  assert.deepEqual(initial.capabilityRequests, [{
    capabilityInstanceId: 'capability:instance/modular-route:arrival:inspect',
    protocolId: 'sample:capability/inspect', exactVersion: '1.0.0', configRef: 'sample:config/inspect',
    config: { configRef: 'sample:config/inspect' }, outcomeSchemaRef: 'sample:outcome/inspect',
  }]);

  const inspected = await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest/inspect', payload: { type: 'objective.complete', objectiveId: 'inspect' },
  });
  assert.equal(inspected.status, 'committed');
  assert.equal(instance().stageIndex, 1);
  assert.equal(instance().status, QuestStatus.Active);

  const completed = await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest/read', payload: { type: 'objective.complete', objectiveId: 'read' },
  });
  assert.equal(completed.status, 'committed');
  assert.equal(instance().status, QuestStatus.Completed);
  assert.equal(runtime.context.select((state) => state.questState.status), QuestStatus.Completed);
});

test('failed quest restores a deterministic checkpoint on retry and lifecycle rejections stay atomic', async () => {
  const { runtime, instance } = createRuntimeWithQuest();
  await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest/advance-before-fail', payload: { type: 'objective.complete', objectiveId: 'inspect' },
  });
  const failed = await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest/fail', payload: { type: 'objective.fail', objectiveId: 'read' },
  });
  assert.equal(failed.status, 'committed');
  assert.equal(instance().status, QuestStatus.Failed);
  const retried = await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest/retry', payload: { type: 'quest.retry' },
  });
  assert.equal(retried.status, 'committed');
  assert.equal(instance().status, QuestStatus.Active);
  assert.equal(instance().attempt, 2);
  assert.equal(instance().stageIndex, 1);
  assert.equal(instance().objectives.archive.read.status, 'active');

  const before = runtime.context.select((state) => state.questState);
  const invalid = await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest/missing', payload: { type: 'objective.complete', objectiveId: 'missing' },
  });
  assert.equal(invalid.status, 'rejected');
  assert.equal(invalid.error.code, 'UnknownQuestObjective');
  assert.deepEqual(runtime.context.select((state) => state.questState), before);
});

test('quest retry policy restarts from the original deterministic checkpoint', async () => {
  const definition = structuredClone(DEFINITION);
  definition.retryPolicy = { mode: 'quest', maximumAttempts: 2 };
  const { runtime, instance } = createRuntimeWithQuest(definition);
  await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest-policy/advance', payload: { type: 'objective.complete', objectiveId: 'inspect' },
  });
  await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest-policy/fail', payload: { type: 'objective.fail', objectiveId: 'read' },
  });
  const retried = await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest-policy/retry', payload: { type: 'quest.retry' },
  });
  assert.equal(retried.status, 'committed');
  assert.equal(instance().stageIndex, 0);
  assert.equal(instance().objectives.arrival.inspect.status, 'active');
});

test('cancel and retry release every child capability owner in the quest transaction and retry re-requests active providers', async () => {
  const cleanupCalls = [];
  const capabilityLifecycle = {
    prepareCleanup: (input) => {
      cleanupCalls.push(input);
      return { releaseClaimLifecycleScopes: ['capability-session'] };
    },
  };
  const { runtime, instance, lastReduction } = createRuntimeWithQuest(DEFINITION, { capabilityLifecycle });
  const capabilityId = 'capability:instance/modular-route:arrival:inspect';
  assert.ok(Object.hasOwn(instance().activeCapabilities, capabilityId));

  await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest-capability/fail', payload: { type: 'objective.fail', objectiveId: 'inspect' },
  });
  assert.deepEqual(cleanupCalls, [{ capabilityInstanceIds: [capabilityId], reason: 'objective-fail' }]);
  assert.deepEqual(lastReduction().plan.releaseClaimOwnerIds, [capabilityId]);
  assert.deepEqual(lastReduction().capabilityCleanups, [{
    capabilityInstanceIds: [capabilityId], reason: 'objective-fail', releaseClaimOwnerIds: [capabilityId], releaseClaimLifecycleScopes: ['capability-session'],
  }]);
  assert.deepEqual(instance().activeCapabilities, {});

  await runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest-capability/retry', payload: { type: 'quest.retry' },
  });
  assert.deepEqual(lastReduction().capabilityRequests.map((request) => request.capabilityInstanceId), [capabilityId]);
  assert.ok(Object.hasOwn(instance().activeCapabilities, capabilityId));

  const cancelled = createRuntimeWithQuest(DEFINITION, { capabilityLifecycle });
  await cancelled.runtime.context.dispatch({
    type: 'quest', actionOccurrenceId: 'quest-capability/cancel', payload: { type: 'quest.cancel' },
  });
  assert.deepEqual(cancelled.lastReduction().plan.releaseClaimOwnerIds, [capabilityId]);
  assert.deepEqual(cancelled.lastReduction().capabilityCleanups.map((cleanup) => cleanup.capabilityInstanceIds), [[capabilityId]]);
  assert.deepEqual(cancelled.instance().activeCapabilities, {});
});

test('stable objective occurrence ID does not depend on campaign quest order', () => {
  assert.equal(questObjectiveOccurrenceId({
    questInstanceId: 'instance/modular-route', stageId: 'arrival', objectiveId: 'inspect', triggerId: 'completed',
  }), 'quest-trigger:instance/modular-route:arrival:inspect:completed');
});
