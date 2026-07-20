import assert from 'node:assert/strict';
import test from 'node:test';

import { CapabilityHost } from '../../../src/runtime/capabilities/capability-host.mjs';
import { RuntimeKernel } from '../../../src/runtime/kernel/runtime-kernel.mjs';
import { CapabilityRegistry } from '../../../src/runtime/registries/runtime-registries.mjs';
import { LogicalClock } from '../../../src/runtime/world/logical-clock.mjs';
import { OwnerRngStreams } from '../../../src/runtime/world/seeded-rng.mjs';
import { DeterministicScheduler } from '../../../src/runtime/world/deterministic-scheduler.mjs';

const REQUIREMENT = Object.freeze({ protocolId: 'sample:capability/puzzle', exactVersion: '1.0.0' });
const PACK = Object.freeze({
  campaign: Object.freeze({ capabilityRequirements: Object.freeze([REQUIREMENT]) }),
  registries: Object.freeze({ capabilities: Object.freeze([]) }),
});

function runtimeWithHostDescriptor() {
  const registry = new CapabilityRegistry(PACK, [{ ...REQUIREMENT, create: () => ({}) }]);
  return new RuntimeKernel({
    capabilityRegistry: registry,
    handlers: {
      claim: (command) => command.payload,
      cleanup: (command) => command.payload,
      emit: (command) => ({ events: [{ type: 'host.ping', payload: command.payload }] }),
    },
  });
}

test('capability lifecycle has namespaced JSON snapshots and owner cleanup plans', async () => {
  const runtime = runtimeWithHostDescriptor();
  const clock = new LogicalClock();
  const scheduler = new DeterministicScheduler();
  const observed = [];
  let restoredState;
  let disposed = 0;
  const host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock,
    rngStreams: new OwnerRngStreams({ seed: 'host' }),
    scheduler,
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: (config) => config.mode === 'accessible',
      create: ({ context }) => ({
        restore: (state) => { restoredState = state; },
        start: () => {
          context.subscribe('host.ping', (event) => observed.push(event.payload));
          context.scheduler.schedule({ jobId: 'job/puzzle', dueTick: 1, payload: { timer: true } });
        },
        handle: () => ({ outcomeId: 'sample:outcome/resolved' }),
        snapshot: () => ({ progress: 2 }),
        stop: () => {},
        dispose: () => { disposed += 1; },
      }),
    }],
  });

  host.createSession({
    capabilityInstanceId: 'instance/puzzle',
    protocolId: REQUIREMENT.protocolId,
    exactVersion: REQUIREMENT.exactVersion,
    config: { mode: 'accessible' },
    claims: [{ resourceId: 'resource/terminal', mode: 'exclusive', lifecycleScope: 'capability' }],
    snapshot: {
      capabilityInstanceId: 'instance/puzzle', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1, state: { progress: 1 },
    },
  });
  assert.throws(() => host.claimPlan('instance/puzzle'), /not active/);
  host.startSession('instance/puzzle');
  const claim = await runtime.context.dispatch({ type: 'claim', actionOccurrenceId: 'claim/puzzle', payload: host.claimPlan('instance/puzzle') });
  await runtime.context.dispatch({ type: 'emit', actionOccurrenceId: 'emit/before-dispose', payload: { step: 1 } });

  assert.equal(claim.status, 'committed');
  assert.deepEqual(restoredState, { progress: 1 });
  assert.deepEqual(host.handle('instance/puzzle', { alternative: true }), host.handle('instance/puzzle', { alternative: false }));
  assert.deepEqual(host.snapshotSession('instance/puzzle'), {
    capabilityInstanceId: 'instance/puzzle', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion,
    stateSchemaVersion: 1, state: { progress: 2 },
  });
  assert.deepEqual(scheduler.takeDueForOwner('instance/puzzle', 0), []);
  clock.advance(1);
  assert.deepEqual(scheduler.takeDueForOwner('instance/puzzle', clock.now()).map((job) => job.jobId), ['job/puzzle']);
  assert.deepEqual(observed, [{ step: 1 }]);

  assert.throws(() => host.disposeSession('instance/puzzle'), /claim release did not commit/);
  await assert.rejects(
    host.disposeWithClaimRelease('instance/puzzle', () => runtime.context.dispatch({
      type: 'cleanup', actionOccurrenceId: 'cleanup/puzzle-noop', payload: {},
    })),
    /claim release did not commit/,
  );
  await runtime.context.dispatch({ type: 'emit', actionOccurrenceId: 'emit/after-noop-cleanup', payload: { step: 'still-active' } });
  assert.deepEqual(observed, [{ step: 1 }, { step: 'still-active' }]);
  const lifecycle = host.questLifecycle();
  const cleanupRecord = { capabilityInstanceIds: ['instance/puzzle'], reason: 'quest-cancel' };
  const release = await runtime.context.dispatch({
    type: 'cleanup',
    actionOccurrenceId: 'cleanup/puzzle',
    payload: lifecycle.prepareCleanup(cleanupRecord),
  });
  assert.deepEqual(lifecycle.finalizeCommitted(cleanupRecord, release), {
    capabilityInstanceIds: ['instance/puzzle'], finalized: true,
  });
  await runtime.context.dispatch({ type: 'emit', actionOccurrenceId: 'emit/after-dispose', payload: { step: 2 } });
  assert.equal(release.status, 'committed');
  assert.equal(disposed, 1);
  assert.throws(() => host.claimPlan('instance/puzzle'), /not active/);
  assert.deepEqual(observed, [{ step: 1 }, { step: 'still-active' }]);
  assert.deepEqual(scheduler.takeDue(100), []);

  const otherClaim = await runtime.context.dispatch({
    type: 'claim', actionOccurrenceId: 'claim/other', payload: {
      claims: [{ resourceId: 'resource/terminal', ownerId: 'instance/other', mode: 'exclusive', lifecycleScope: 'capability' }],
    },
  });
  assert.equal(otherClaim.status, 'committed');
});

test('unknown or mismatched capability/config fails before a session starts', () => {
  const runtime = runtimeWithHostDescriptor();
  const host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 1 }),
    scheduler: new DeterministicScheduler(),
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => false,
      create: () => ({}),
    }],
  });
  assert.throws(() => host.createSession({
    capabilityInstanceId: 'instance/invalid', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {},
  }), /config is invalid/);
  assert.throws(() => host.createSession({
    capabilityInstanceId: 'instance/version', protocolId: REQUIREMENT.protocolId, exactVersion: '2.0.0', config: {},
  }), /requires 2.0.0/);
  assert.throws(() => host.createSession({
    capabilityInstanceId: 'instance/missing', protocolId: 'sample:capability/missing', exactVersion: '1.0.0', config: {},
  }), /no descriptor/);
});

test('presentation port exposes only immutable JSON, host-forwarded input and disposable updates', () => {
  const runtime = runtimeWithHostDescriptor();
  let emit;
  let providerDisposals = 0;
  const handledInputs = [];
  const host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 'presentation' }),
    scheduler: new DeterministicScheduler(),
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: () => Object.freeze({
        handle: (input) => {
          handledInputs.push(input);
          return { outcomeId: 'sample:outcome/presented' };
        },
        render: () => ({ rows: [{ id: 'first' }] }),
        subscribe: (listener) => {
          emit = listener;
          return Object.freeze({ dispose: () => { providerDisposals += 1; } });
        },
      }),
    }],
  });
  host.createSession({
    capabilityInstanceId: 'instance/presentation', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {},
  });
  assert.throws(() => host.presentation('instance/presentation'), /presentation is not active/);
  host.startSession('instance/presentation');

  const presentation = host.presentation('instance/presentation');
  assert.deepEqual(Object.keys(presentation).sort(), ['handle', 'render', 'subscribe']);
  assert.equal(Object.hasOwn(presentation, 'session'), false);
  assert.equal(Object.hasOwn(presentation, 'runtime'), false);
  const model = presentation.render();
  assert.equal(Object.isFrozen(model), true);
  assert.equal(Object.isFrozen(model.rows), true);
  assert.equal(Object.isFrozen(model.rows[0]), true);
  assert.throws(() => model.rows.push({ id: 'mutate' }), TypeError);
  assert.deepEqual(presentation.handle({ type: 'choose', target: 'first' }), { outcomeId: 'sample:outcome/presented' });
  assert.deepEqual(handledInputs, [{ type: 'choose', target: 'first' }]);
  assert.equal(Object.isFrozen(handledInputs[0]), true, 'the provider receives host-normalized JSON, not caller state');

  const updates = [];
  const subscription = presentation.subscribe((update) => {
    updates.push(update);
    assert.throws(() => update.rows[0].id = 'mutate', TypeError);
  });
  emit({ rows: [{ id: 'updated' }] });
  assert.deepEqual(updates, [{ rows: [{ id: 'updated' }] }]);
  assert.equal(subscription.disposed, false);

  host.stopSession('instance/presentation');
  assert.equal(subscription.disposed, true);
  assert.equal(providerDisposals, 1);
  emit({ rows: [{ id: 'late' }] });
  assert.deepEqual(updates, [{ rows: [{ id: 'updated' }] }]);
  assert.throws(() => presentation.render(), /presentation is not active/);
  assert.throws(() => presentation.handle({ type: 'late' }), /not active/);
  assert.throws(() => presentation.subscribe(() => {}), /presentation is not active/);
});

test('presentation port fails closed when a session does not explicitly provide render and subscribe', () => {
  const runtime = runtimeWithHostDescriptor();
  const host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 'presentation-failure' }),
    scheduler: new DeterministicScheduler(),
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: () => ({ render: () => ({}) }),
    }],
  });
  host.createSession({
    capabilityInstanceId: 'instance/no-presentation-subscribe', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {},
  });
  host.startSession('instance/no-presentation-subscribe');
  assert.throws(
    () => host.presentation('instance/no-presentation-subscribe'),
    /does not implement the presentation port/,
  );
});

test('failed provider restore disposes subscriptions and scheduled jobs before gameplay', async () => {
  const runtime = runtimeWithHostDescriptor();
  const scheduler = new DeterministicScheduler();
  let deliveries = 0;
  const host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 2 }),
    scheduler,
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: ({ context }) => {
        context.subscribe('host.ping', () => { deliveries += 1; });
        context.scheduler.schedule({ jobId: 'job/failed-restore', dueTick: 0, payload: null });
        return { restore: () => { throw new Error('restore rejected'); }, dispose: () => {} };
      },
    }],
  });
  assert.throws(() => host.createSession({
    capabilityInstanceId: 'instance/restore-failure', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {},
    snapshot: { capabilityInstanceId: 'instance/restore-failure', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, stateSchemaVersion: 1, state: {} },
  }), /restore rejected/);
  await runtime.context.dispatch({ type: 'emit', actionOccurrenceId: 'emit/restore-failure', payload: null });
  assert.equal(deliveries, 0);
  assert.deepEqual(scheduler.takeDue(0), []);
});

test('failed start and retained owner callbacks cannot leak subscriptions, jobs or timers', async () => {
  const runtime = runtimeWithHostDescriptor();
  const scheduler = new DeterministicScheduler();
  let deliveries = 0;
  let retainedContext;
  let queriedReadModel;
  let disposed = 0;
  const host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 3 }),
    scheduler,
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: ({ context }) => {
        retainedContext = context;
        return {
          start: () => {
            queriedReadModel = context.runtime.query(({ state, claims }) => ({ state, claims }));
            context.subscribe('host.ping', () => { deliveries += 1; });
            context.scheduler.schedule({ jobId: 'job/failed-start', dueTick: 0, payload: null });
            throw new Error('start rejected');
          },
          stop: () => { throw new Error('stop rejected'); },
          dispose: () => { disposed += 1; },
        };
      },
    }],
  });
  host.createSession({ capabilityInstanceId: 'instance/start-failure', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {} });
  assert.throws(() => host.startSession('instance/start-failure'), /start rejected/);
  assert.throws(() => host.claimPlan('instance/start-failure'), /not active/);
  await runtime.context.dispatch({ type: 'emit', actionOccurrenceId: 'emit/start-failure', payload: null });
  assert.equal(deliveries, 0);
  assert.equal(disposed, 1);
  assert.deepEqual(queriedReadModel, { state: {}, claims: [] });
  assert.equal(Object.isFrozen(queriedReadModel), true, 'provider query remains an immutable kernel read model');
  assert.deepEqual(scheduler.claimDue(0), []);
  assert.throws(() => retainedContext.scheduler.schedule({ jobId: 'job/late', dueTick: 1, payload: null }), /owner context is closed/);
  assert.throws(() => retainedContext.subscribe('host.ping', () => {}), /owner context is closed/);
  assert.throws(() => retainedContext.runtime.subscribe('host.ping', () => {}), /owner context is closed/);
  assert.throws(() => retainedContext.runtime.query(() => null), /owner context is closed/);
  assert.throws(() => retainedContext.cleanup.add(() => {}), /owner context is closed/);
});

test('host lifecycle calls provider stop once when dispose follows stop', () => {
  const runtime = runtimeWithHostDescriptor();
  let stops = 0;
  let disposes = 0;
  const host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 4 }),
    scheduler: new DeterministicScheduler(),
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: () => ({ stop: () => { stops += 1; }, dispose: () => { disposes += 1; } }),
    }],
  });
  host.createSession({ capabilityInstanceId: 'instance/idempotent', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {} });
  host.startSession('instance/idempotent');
  host.stopSession('instance/idempotent');
  host.disposeSession('instance/idempotent');
  assert.equal(stops, 1);
  assert.equal(disposes, 1);
});

test('provider dispose re-entry cannot recursively dispose the same session', () => {
  const runtime = runtimeWithHostDescriptor();
  let host;
  let stops = 0;
  let disposes = 0;
  host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 44 }),
    scheduler: new DeterministicScheduler(),
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: () => ({
        stop: () => { stops += 1; },
        dispose: () => {
          disposes += 1;
          host.disposeSession('instance/reentrant');
        },
      }),
    }],
  });
  host.createSession({ capabilityInstanceId: 'instance/reentrant', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {} });
  host.startSession('instance/reentrant');
  assert.doesNotThrow(() => host.disposeSession('instance/reentrant'));
  assert.equal(stops, 1);
  assert.equal(disposes, 1);
});

test('provider stop re-entry cannot make outer disposal invoke provider dispose twice', () => {
  const runtime = runtimeWithHostDescriptor();
  let host;
  let stops = 0;
  let disposes = 0;
  host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 45 }),
    scheduler: new DeterministicScheduler(),
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: () => ({
        stop: () => {
          stops += 1;
          host.disposeSession('instance/stop-reentrant');
        },
        dispose: () => { disposes += 1; },
      }),
    }],
  });
  host.createSession({ capabilityInstanceId: 'instance/stop-reentrant', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {} });
  host.startSession('instance/stop-reentrant');
  assert.doesNotThrow(() => host.disposeSession('instance/stop-reentrant'));
  assert.equal(stops, 1);
  assert.equal(disposes, 1);
});

test('cleanup disposer re-entry cannot make outer disposal invoke provider dispose twice', () => {
  const runtime = runtimeWithHostDescriptor();
  let host;
  let disposes = 0;
  host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 46 }),
    scheduler: new DeterministicScheduler(),
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: ({ context }) => {
        context.cleanup.add(() => host.disposeSession('instance/disposer-reentrant'));
        return { stop: () => {}, dispose: () => { disposes += 1; } };
      },
    }],
  });
  host.createSession({ capabilityInstanceId: 'instance/disposer-reentrant', protocolId: REQUIREMENT.protocolId, exactVersion: REQUIREMENT.exactVersion, config: {} });
  host.startSession('instance/disposer-reentrant');
  assert.doesNotThrow(() => host.disposeSession('instance/disposer-reentrant'));
  assert.equal(disposes, 1);
});

test('finalized multi-capability cleanup continues after a provider teardown error', async () => {
  const runtime = runtimeWithHostDescriptor();
  const scheduler = new DeterministicScheduler();
  const deliveries = [];
  const stopped = [];
  const disposed = [];
  const host = new CapabilityHost({
    runtimeContext: runtime.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 5 }),
    scheduler,
    descriptors: [{
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      stateSchemaVersion: 1,
      validateConfig: () => true,
      create: ({ capabilityInstanceId, context }) => ({
        start: () => {
          context.subscribe('host.ping', () => deliveries.push(capabilityInstanceId));
          context.scheduler.schedule({ jobId: `job/${capabilityInstanceId}`, dueTick: 0, payload: null });
        },
        stop: () => {
          stopped.push(capabilityInstanceId);
          if (capabilityInstanceId === 'instance/first') throw new Error('first stop failed');
        },
        dispose: () => disposed.push(capabilityInstanceId),
      }),
    }],
  });
  for (const capabilityInstanceId of ['instance/first', 'instance/second']) {
    host.createSession({
      capabilityInstanceId,
      protocolId: REQUIREMENT.protocolId,
      exactVersion: REQUIREMENT.exactVersion,
      config: {},
      claims: [{ resourceId: `resource/${capabilityInstanceId}`, mode: 'exclusive', lifecycleScope: 'capability' }],
    });
    host.startSession(capabilityInstanceId);
  }
  const claimed = await runtime.context.dispatch({
    type: 'claim',
    actionOccurrenceId: 'claim/multi-cleanup',
    payload: { claims: ['instance/first', 'instance/second'].flatMap((id) => host.claimPlan(id).claims) },
  });
  const lifecycle = host.questLifecycle();
  const cleanupRecord = { capabilityInstanceIds: ['instance/first', 'instance/second'], reason: 'quest-cancel' };
  const released = await runtime.context.dispatch({
    type: 'cleanup', actionOccurrenceId: 'cleanup/multi-cleanup', payload: lifecycle.prepareCleanup(cleanupRecord),
  });
  assert.equal(claimed.status, 'committed');
  assert.equal(released.status, 'committed');
  assert.throws(() => lifecycle.finalizeCommitted(cleanupRecord, released), /first stop failed/);
  await runtime.context.dispatch({ type: 'emit', actionOccurrenceId: 'emit/multi-cleanup', payload: null });
  assert.deepEqual(stopped, ['instance/first', 'instance/second']);
  assert.deepEqual(disposed, ['instance/first', 'instance/second']);
  assert.deepEqual(deliveries, []);
  assert.deepEqual(scheduler.claimDue(0), []);
});
