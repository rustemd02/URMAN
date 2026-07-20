import assert from 'node:assert/strict';
import test from 'node:test';

import {
  ConflictError,
  DuplicateOccurrence,
  OccurrenceConflict,
  PreflightError,
  RuntimeFailure,
} from '../../../src/runtime/contracts/runtime-contracts.mjs';
import { RuntimeKernel } from '../../../src/runtime/kernel/runtime-kernel.mjs';
import { OrderedEventBus } from '../../../src/runtime/kernel/ordered-event-bus.mjs';
import { StateStore } from '../../../src/runtime/kernel/state-store.mjs';
import { CapabilityRegistry } from '../../../src/runtime/registries/runtime-registries.mjs';

const EMPTY_PACK = Object.freeze({
  campaign: Object.freeze({ capabilityRequirements: Object.freeze([]) }),
  registries: Object.freeze({ capabilities: Object.freeze([]) }),
});

function createKernel(options = {}) {
  return new RuntimeKernel({ ...options, capabilityRegistry: new CapabilityRegistry(EMPTY_PACK) });
}

function restoreKernel(snapshot, options = {}) {
  return RuntimeKernel.fromSnapshot(snapshot, {
    ...options,
    capabilityRegistry: new CapabilityRegistry(EMPTY_PACK),
  });
}

test('one command commits its complete effect batch and ordered events atomically', async () => {
  const observed = [];
  const kernel = createKernel({
    initialState: { count: 0 },
    handlers: {
      increment: (command) => ({
        effects: [{ op: 'state.increment', key: 'count', delta: command.payload.delta }],
        events: [
          { type: 'count.changed', payload: { order: 1 } },
          { type: 'count.changed', payload: { order: 2 } },
        ],
        value: { count: command.payload.delta },
      }),
    },
  });
  kernel.context.subscribe('count.changed', (event) => observed.push(event));

  const result = await kernel.context.dispatch({ type: 'increment', actionOccurrenceId: 'action-1', payload: { delta: 2 } });

  assert.equal(result.status, 'committed');
  assert.equal('deliveryErrors' in result, false);
  assert.equal(kernel.context.select((state) => state.count), 2);
  assert.deepEqual(observed.map(({ sequence, payload }) => [sequence, payload.order]), [[1, 1], [2, 2]]);
  assert.ok(observed.every(({ transactionId }) => transactionId === 'tx:action-1'));
});

test('invalid effect later in a batch leaves zero partial state and emits nothing', async () => {
  let events = 0;
  const kernel = createKernel({
    initialState: { label: 'safe' },
    handlers: {
      invalid: () => ({
        effects: [
          { op: 'state.set', key: 'changed', value: true },
          { op: 'state.increment', key: 'label', delta: 1 },
        ],
        events: [{ type: 'should.not.fire', payload: null }],
      }),
    },
  });
  kernel.context.subscribe('*', () => { events += 1; });
  const result = await kernel.context.dispatch({ type: 'invalid', actionOccurrenceId: 'action-invalid', payload: null });

  assert.equal(result.status, 'rejected');
  assert.equal(result.error.code, 'PreflightError');
  assert.equal(result.error instanceof PreflightError, true);
  assert.deepEqual(kernel.context.select((state) => state), { label: 'safe' });
  assert.equal(events, 0);
});

test('same-fingerprint duplicate returns saved result reference; changed fingerprint conflicts', async () => {
  let commits = 0;
  const kernel = createKernel({
    handlers: {
      once: (command) => {
        commits += 1;
        return { effects: [{ op: 'state.set', key: 'value', value: command.payload.value }], value: command.payload };
      },
    },
  });
  const command = { type: 'once', actionOccurrenceId: 'stable-occurrence', payload: { value: 7 } };
  const first = await kernel.context.dispatch(command);
  const duplicate = await kernel.context.dispatch(command);
  const conflict = await kernel.context.dispatch({ ...command, payload: { value: 8 } });

  assert.equal(first.status, 'committed');
  assert.equal(duplicate.error.code, 'DuplicateOccurrence');
  assert.equal(duplicate.error instanceof DuplicateOccurrence, true);
  assert.equal(duplicate.error.savedResult, first);
  assert.equal(conflict.error.code, 'OccurrenceConflict');
  assert.equal(conflict.error instanceof OccurrenceConflict, true);
  assert.equal(commits, 1);
  assert.equal(kernel.context.select((state) => state.value), 7);
});

test('kernel snapshots are JSON data and restore ledger, claims, state, and event sequence without replaying', async () => {
  let handlerCalls = 0;
  const handlers = {
    apply: (command) => {
      handlerCalls += 1;
      return {
        claims: [{
          resourceId: 'resource/archive-terminal',
          ownerId: 'owner/archive-session',
          mode: 'exclusive',
          lifecycleScope: 'scope/archive-session',
        }],
        effects: [{ op: 'state.set', key: 'restoredValue', value: command.payload.value }],
        events: [{ type: 'archive.changed', payload: { value: command.payload.value } }],
        value: { stored: command.payload.value },
      };
    },
  };
  const source = createKernel({ initialState: { preserved: true }, handlers });
  const command = { type: 'apply', actionOccurrenceId: 'restore-occurrence', payload: { value: 7 } };
  const first = await source.context.dispatch(command);
  const unknownCommand = { type: 'missing', actionOccurrenceId: 'restore-rejection', payload: null };
  const rejectedResult = await source.context.dispatch(unknownCommand);
  const snapshot = source.exportSnapshot();

  assert.doesNotThrow(() => JSON.stringify(snapshot));
  assert.deepEqual(snapshot.occurrences.find(({ actionOccurrenceId }) => actionOccurrenceId === 'restore-rejection').result, {
    status: 'rejected',
    error: {
      code: 'UnknownCommand',
      message: 'Unknown command missing.',
      details: { type: 'missing' },
    },
  });

  const restored = restoreKernel(JSON.parse(JSON.stringify(snapshot)), { handlers });
  assert.deepEqual(restored.context.select((state) => state), { preserved: true, restoredValue: 7 });
  assert.deepEqual(restored.exportSnapshot().claims, snapshot.claims);

  const delivered = [];
  restored.context.subscribe('archive.changed', (event) => delivered.push(event));
  const duplicate = await restored.context.dispatch(command);
  assert.equal(duplicate.error instanceof DuplicateOccurrence, true);
  assert.notEqual(duplicate.error.savedResult, first);
  assert.deepEqual(duplicate.error.savedResult, first);
  assert.equal(handlerCalls, 1);
  assert.deepEqual(restored.context.select((state) => state), { preserved: true, restoredValue: 7 });
  assert.deepEqual(delivered, []);

  const restoredRejectedDuplicate = await restored.context.dispatch(unknownCommand);
  assert.equal(restoredRejectedDuplicate.error instanceof DuplicateOccurrence, true);
  assert.notEqual(restoredRejectedDuplicate.error.savedResult, rejectedResult);
  assert.equal(restoredRejectedDuplicate.error.savedResult.error instanceof RuntimeFailure, true);
  assert.equal(restoredRejectedDuplicate.error.savedResult.error.code, 'UnknownCommand');

  const conflict = await restored.context.dispatch({ ...command, payload: { value: 8 } });
  assert.equal(conflict.error instanceof OccurrenceConflict, true);
  assert.equal(handlerCalls, 1);

  const next = await restored.context.dispatch({ type: 'apply', actionOccurrenceId: 'restore-next', payload: { value: 9 } });
  assert.equal(next.status, 'committed');
  assert.deepEqual(delivered.map((event) => event.sequence), [2]);
});

test('kernel restore rejects incomplete snapshots and duplicate ledger entries', async () => {
  const source = createKernel({
    handlers: {
      claim: () => ({
        claims: [{ resourceId: 'resource/restore', ownerId: 'owner/restore', mode: 'exclusive', lifecycleScope: 'scope/restore' }],
      }),
    },
  });
  await source.context.dispatch({ type: 'claim', actionOccurrenceId: 'restore-validation', payload: null });
  const snapshot = source.exportSnapshot();

  const incomplete = structuredClone(snapshot);
  delete incomplete.eventSequence;
  assert.throws(() => restoreKernel(incomplete), /invalid shape/);

  const duplicateOccurrence = structuredClone(snapshot);
  duplicateOccurrence.occurrences.push(structuredClone(duplicateOccurrence.occurrences[0]));
  assert.throws(() => restoreKernel(duplicateOccurrence), /duplicate occurrence/);

  const duplicateClaim = structuredClone(snapshot);
  duplicateClaim.claims.push(structuredClone(duplicateClaim.claims[0]));
  assert.throws(() => restoreKernel(duplicateClaim), /duplicate resource claim/);
});

test('command fingerprints distinguish negative zero from zero', async () => {
  const kernel = createKernel({
    handlers: {
      number: (command) => ({ value: { negativeZero: Object.is(command.payload, -0) } }),
    },
  });
  const negativeZero = { type: 'number', actionOccurrenceId: 'signed-zero', payload: -0 };
  const first = await kernel.context.dispatch(negativeZero);
  const duplicate = await kernel.context.dispatch(negativeZero);
  const conflict = await kernel.context.dispatch({ ...negativeZero, payload: 0 });
  assert.equal(first.value.negativeZero, true);
  assert.equal(duplicate.error.savedResult, first);
  assert.equal(conflict.error instanceof OccurrenceConflict, true);
});

test('persisted state, results, events and error details canonicalize negative zero', async () => {
  const observed = [];
  const handlers = {
    persist: (command) => ({
      effects: [{ op: 'state.set', key: 'signed', value: command.payload }],
      events: [{ type: 'signed', payload: command.payload }],
      value: command.payload,
    }),
    reject: () => ({ rejection: { code: 'Signed', message: 'Signed.', details: -0 } }),
  };
  const kernel = createKernel({ handlers });
  kernel.context.subscribe('signed', (event) => observed.push(event.payload));

  const committed = await kernel.context.dispatch({ type: 'persist', actionOccurrenceId: 'persist-signed-zero', payload: -0 });
  const rejected = await kernel.context.dispatch({ type: 'reject', actionOccurrenceId: 'reject-signed-zero', payload: null });
  const serializedSnapshot = JSON.parse(JSON.stringify(kernel.exportSnapshot()));
  const restored = restoreKernel(serializedSnapshot, { handlers });

  assert.equal(Object.is(committed.value, -0), false);
  assert.equal(Object.is(kernel.context.select((state) => state.signed), -0), false);
  assert.equal(Object.is(observed[0], -0), false);
  assert.equal(Object.is(rejected.error.details, -0), false);
  assert.equal(Object.is(serializedSnapshot.state.signed, -0), false);
  assert.equal(Object.is(restored.context.select((state) => state.signed), -0), false);
});

test('prepared JSON payload normalization matches canonical fingerprint equivalence', async () => {
  let handlerCalls = 0;
  const kernel = createKernel({
    handlers: {
      inspect: (command) => {
        handlerCalls += 1;
        return {
          value: {
            keys: Object.keys(command.payload),
            aliasesSharedObject: command.payload.left === command.payload.right,
          },
        };
      },
    },
  });
  const shared = { value: 1 };
  const firstPayload = { right: shared, b: 2, left: shared, a: 1 };
  const reorderedCopiedPayload = { a: 1, left: { value: 1 }, b: 2, right: { value: 1 } };
  const command = { type: 'inspect', actionOccurrenceId: 'normalized-json-equivalence', payload: firstPayload };
  const first = await kernel.context.dispatch(command);
  const duplicate = await kernel.context.dispatch({ ...command, payload: reorderedCopiedPayload });
  assert.deepEqual(first.value.keys, ['a', 'b', 'left', 'right']);
  assert.equal(first.value.aliasesSharedObject, false);
  assert.equal(duplicate.error.savedResult, first);
  assert.equal(handlerCalls, 1);
});

test('mutable proxy payloads are cloned from one validated descriptor snapshot', async () => {
  let descriptorReads = 0;
  const source = { value: 'safe' };
  const payload = new Proxy(source, {
    getOwnPropertyDescriptor(target, key) {
      if (key === 'value') {
        descriptorReads += 1;
        return {
          configurable: true,
          enumerable: true,
          writable: true,
          value: descriptorReads === 1 ? 'safe' : undefined,
        };
      }
      return Reflect.getOwnPropertyDescriptor(target, key);
    },
  });
  const kernel = createKernel({
    handlers: {
      proxy: (command) => ({
        effects: [{ op: 'state.set', key: 'observed', value: command.payload.value }],
        value: { observed: command.payload.value },
      }),
    },
  });

  const result = await kernel.context.dispatch({ type: 'proxy', actionOccurrenceId: 'proxy-snapshot-1', payload });

  assert.equal(result.status, 'committed');
  assert.equal(descriptorReads, 1);
  assert.deepEqual(result.value, { observed: 'safe' });
  assert.equal(kernel.context.select((state) => state.observed), 'safe');
});

test('unknown commands are ledgered and become duplicate/conflict on replay', async () => {
  const kernel = createKernel();
  const command = { type: 'missing', actionOccurrenceId: 'unknown-once', payload: { value: 1 } };
  const unknown = await kernel.context.dispatch(command);
  const duplicate = await kernel.context.dispatch(command);
  const conflict = await kernel.context.dispatch({ ...command, payload: { value: 2 } });
  assert.equal(unknown.error.code, 'UnknownCommand');
  assert.equal(duplicate.error instanceof DuplicateOccurrence, true);
  assert.equal(duplicate.error.savedResult, unknown);
  assert.equal(conflict.error instanceof OccurrenceConflict, true);
  assert.equal(kernel.exportSnapshot().occurrences.length, 1);
});

test('non-JSON command payloads are ledgered preflight rejections with deterministic replay', async () => {
  const cyclic = {};
  cyclic.self = cyclic;
  const cases = [
    ['cyclic', cyclic],
    ['function', () => {}],
    ['bigint', 1n],
  ];

  for (const [name, payload] of cases) {
    const kernel = createKernel({
      handlers: { valid: () => ({ effects: [{ op: 'state.set', key: 'changed', value: true }] }) },
    });
    const command = { type: 'valid', actionOccurrenceId: `invalid-command-${name}`, payload };
    const first = await kernel.context.dispatch(command);
    const duplicate = await kernel.context.dispatch(command);

    assert.equal(first.error instanceof PreflightError, true, name);
    assert.equal(duplicate.error instanceof DuplicateOccurrence, true, name);
    assert.equal(duplicate.error.savedResult, first, name);
    assert.deepEqual(kernel.exportSnapshot().state, {}, name);
    assert.equal(kernel.exportSnapshot().occurrences.length, 1, name);
  }

  const conflictKernel = createKernel();
  const first = await conflictKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'invalid-command-conflict', payload: () => {} });
  const conflict = await conflictKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'invalid-command-conflict', payload: 1n });
  assert.equal(first.error instanceof PreflightError, true);
  assert.equal(conflict.error instanceof OccurrenceConflict, true);

  const bigintKernel = createKernel();
  const bigintFirst = await bigintKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'invalid-bigint-conflict', payload: 1n });
  const bigintConflict = await bigintKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'invalid-bigint-conflict', payload: 2n });
  assert.equal(bigintFirst.error instanceof PreflightError, true);
  assert.equal(bigintConflict.error instanceof OccurrenceConflict, true);

  const sameFunction = () => {};
  const functionKernel = createKernel();
  const functionFirst = await functionKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'invalid-function-identity', payload: sameFunction });
  const functionDuplicate = await functionKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'invalid-function-identity', payload: sameFunction });
  const functionConflict = await functionKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'invalid-function-identity', payload: () => 'different function source' });
  assert.equal(functionDuplicate.error.savedResult, functionFirst);
  assert.equal(functionConflict.error instanceof OccurrenceConflict, true);
});

test('invalid command fingerprints stay stable across kernel restore', async () => {
  function originalInvalidFunction() {}
  function unrelatedInvalidFunction() { return 'different'; }
  const command = {
    type: 'missing',
    actionOccurrenceId: 'invalid-restore-occurrence',
    payload: originalInvalidFunction,
  };
  const source = createKernel();
  const first = await source.context.dispatch(command);
  const restored = restoreKernel(JSON.parse(JSON.stringify(source.exportSnapshot())));

  const unrelated = await restored.context.dispatch({ ...command, payload: unrelatedInvalidFunction });
  const replay = await restored.context.dispatch(command);
  const changed = await restored.context.dispatch({ ...command, payload: 1n });

  assert.equal(first.error instanceof PreflightError, true);
  assert.equal(unrelated.error instanceof OccurrenceConflict, true);
  assert.equal(replay.error instanceof DuplicateOccurrence, true);
  assert.equal(replay.error.savedResult.error instanceof RuntimeFailure, true);
  assert.equal(replay.error.savedResult.error.code, 'PreflightError');
  assert.equal(changed.error instanceof OccurrenceConflict, true);
});

test('invalid custom-prototype commands do not coerce constructor values and replay after restore', async () => {
  let coercionCalls = 0;
  const constructorValue = {};
  Object.defineProperty(constructorValue, 'toString', {
    get() {
      coercionCalls += 1;
      throw new Error('must not coerce invalid command prototypes');
    },
  });
  const prototype = {};
  Object.defineProperty(prototype, 'constructor', { value: constructorValue });
  const payload = Object.create(prototype);
  payload.value = 1;
  const command = { type: 'missing', actionOccurrenceId: 'custom-prototype-replay', payload };

  const source = createKernel();
  const first = await source.context.dispatch(command);
  const restored = restoreKernel(JSON.parse(JSON.stringify(source.exportSnapshot())));
  const replay = await restored.context.dispatch(command);

  assert.equal(first.error instanceof PreflightError, true);
  assert.equal(replay.error instanceof DuplicateOccurrence, true);
  assert.equal(coercionCalls, 0);
});

test('event sequence exhaustion rejects before a state commit and remains restorable', async () => {
  const handlers = {
    emit: () => ({
      effects: [{ op: 'state.set', key: 'changed', value: true }],
      events: [{ type: 'changed', payload: null }],
    }),
  };
  const initial = createKernel({ initialState: { changed: false }, handlers });
  const snapshot = structuredClone(initial.exportSnapshot());
  snapshot.eventSequence = Number.MAX_SAFE_INTEGER;
  const kernel = restoreKernel(snapshot, { handlers });
  let events = 0;
  kernel.context.subscribe('*', () => { events += 1; });

  const result = await kernel.context.dispatch({ type: 'emit', actionOccurrenceId: 'sequence-exhausted', payload: null });

  assert.equal(result.error instanceof PreflightError, true);
  assert.deepEqual(kernel.context.select((state) => state), { changed: false });
  assert.equal(events, 0);
  assert.equal(kernel.exportSnapshot().eventSequence, Number.MAX_SAFE_INTEGER);
  assert.doesNotThrow(() => restoreKernel(JSON.parse(JSON.stringify(kernel.exportSnapshot())), { handlers }));
});

test('command and array accessors are never invoked and invalid array extras fingerprint distinctly', async () => {
  let getterCalls = 0;
  const accessorCommand = { type: 'valid', actionOccurrenceId: 'accessor-command' };
  Object.defineProperty(accessorCommand, 'payload', {
    enumerable: true,
    get() { getterCalls += 1; throw new Error('must not execute'); },
  });
  const kernel = createKernel({ handlers: { valid: () => ({ effects: [{ op: 'state.set', key: 'changed', value: true }] }) } });
  const accessorFirst = await kernel.context.dispatch(accessorCommand);
  const accessorDuplicate = await kernel.context.dispatch(accessorCommand);
  assert.equal(accessorFirst.error instanceof PreflightError, true);
  assert.equal(accessorDuplicate.error instanceof DuplicateOccurrence, true);
  assert.equal(getterCalls, 0);
  assert.deepEqual(kernel.exportSnapshot().state, {});

  const arrayWithAccessor = [];
  Object.defineProperty(arrayWithAccessor, '0', { enumerable: true, get() { getterCalls += 1; return 1; } });
  arrayWithAccessor.length = 1;
  const arrayKernel = createKernel();
  const arrayAccessor = await arrayKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'array-accessor', payload: arrayWithAccessor });
  assert.equal(arrayAccessor.error instanceof PreflightError, true);
  assert.equal(getterCalls, 0);

  const firstArray = [1];
  firstArray.extra = 1;
  const secondArray = [1];
  secondArray.extra = 2;
  const extraKernel = createKernel();
  const extraFirst = await extraKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'array-extra', payload: firstArray });
  const extraConflict = await extraKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'array-extra', payload: secondArray });
  assert.equal(extraFirst.error instanceof PreflightError, true);
  assert.equal(extraConflict.error instanceof OccurrenceConflict, true);

  const validArray = [1, { nested: true }];
  const validKernel = createKernel();
  const validFirst = await validKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'valid-array', payload: validArray });
  const validDuplicate = await validKernel.context.dispatch({ type: 'missing', actionOccurrenceId: 'valid-array', payload: [1, { nested: true }] });
  assert.equal(validFirst.error.code, 'UnknownCommand');
  assert.equal(validDuplicate.error instanceof DuplicateOccurrence, true);
});

test('non-JSON transaction outputs reject before state, claims or events and replay deterministically', async () => {
  const cyclic = {};
  cyclic.self = cyclic;
  const invalidValues = [cyclic, () => {}, 1n];

  for (const [index, value] of invalidValues.entries()) {
    let events = 0;
    const kernel = createKernel({
      initialState: { safe: true },
      handlers: {
        invalidOutput: () => ({
          effects: [{ op: 'state.set', key: 'changed', value: true }],
          claims: [{ resourceId: 'resource/test', ownerId: 'owner/test', mode: 'exclusive', lifecycleScope: 'scope/test' }],
          events: [{ type: 'should.not.fire', payload: null }],
          value,
        }),
      },
    });
    kernel.context.subscribe('*', () => { events += 1; });
    const command = { type: 'invalidOutput', actionOccurrenceId: `invalid-output-${index}`, payload: null };
    const first = await kernel.context.dispatch(command);
    const duplicate = await kernel.context.dispatch(command);
    const snapshot = kernel.exportSnapshot();

    assert.equal(first.error instanceof PreflightError, true, String(index));
    assert.equal(duplicate.error instanceof DuplicateOccurrence, true, String(index));
    assert.equal(duplicate.error.savedResult, first, String(index));
    assert.deepEqual(snapshot.state, { safe: true }, String(index));
    assert.deepEqual(snapshot.claims, [], String(index));
    assert.equal(snapshot.eventSequence, 0, String(index));
    assert.equal(snapshot.occurrences.length, 1, String(index));
    assert.equal(events, 0, String(index));
  }
});

test('non-JSON rejection details become a ledgered preflight rejection', async () => {
  const cyclic = {};
  cyclic.self = cyclic;
  const kernel = createKernel({
    handlers: {
      reject: () => ({ rejection: { code: 'Rejected', message: 'No.', details: cyclic } }),
    },
  });
  const command = { type: 'reject', actionOccurrenceId: 'invalid-rejection-details', payload: null };
  const first = await kernel.context.dispatch(command);
  const duplicate = await kernel.context.dispatch(command);

  assert.equal(first.error instanceof PreflightError, true);
  assert.equal(duplicate.error instanceof DuplicateOccurrence, true);
  assert.equal(duplicate.error.savedResult, first);
  assert.equal(kernel.exportSnapshot().occurrences.length, 1);
});

test('competing exclusive resource use yields one commit and one typed conflict without effects', async () => {
  const kernel = createKernel({
    handlers: {
      acquire: (command) => ({
        claims: [{
          resourceId: command.payload.resourceId,
          ownerId: command.payload.ownerId,
          mode: 'exclusive',
          lifecycleScope: `scope/${command.payload.ownerId}`,
        }],
        effects: [{ op: 'state.set', key: command.payload.ownerId, value: 'committed' }],
      }),
    },
  });
  const [first, second] = await Promise.all([
    kernel.context.dispatch({ type: 'acquire', actionOccurrenceId: 'claim-a', payload: { resourceId: 'resource/radio', ownerId: 'owner-a' } }),
    kernel.context.dispatch({ type: 'acquire', actionOccurrenceId: 'claim-b', payload: { resourceId: 'resource/radio', ownerId: 'owner-b' } }),
  ]);

  assert.equal(first.status, 'committed');
  assert.equal(second.status, 'rejected');
  assert.equal(second.error.code, 'ResourceConflict');
  assert.equal(second.error instanceof ConflictError, true);
  assert.equal(kernel.context.select((state) => state['owner-a']), 'committed');
  assert.equal(kernel.context.select((state) => state['owner-b']), undefined);
  assert.equal(kernel.exportSnapshot().claims.length, 1);
  assert.equal(kernel.exportSnapshot().claims[0].entries[0].lifecycleScope, 'scope/owner-a');
});

test('conflicting claims reject before an effects accessor is evaluated', async () => {
  let effectsGetterCalls = 0;
  const kernel = createKernel({
    handlers: {
      acquire: (command) => {
        const plan = {
          claims: [{
            resourceId: 'resource/radio',
            ownerId: command.payload.ownerId,
            mode: 'exclusive',
            lifecycleScope: `scope/${command.payload.ownerId}`,
          }],
        };
        if (command.payload.ownerId === 'owner-b') {
          Object.defineProperty(plan, 'effects', {
            enumerable: true,
            get() {
              effectsGetterCalls += 1;
              throw new Error('effects must not be read after a claim conflict');
            },
          });
        }
        return plan;
      },
    },
  });

  await kernel.context.dispatch({ type: 'acquire', actionOccurrenceId: 'claim-owner-a', payload: { ownerId: 'owner-a' } });
  const conflict = await kernel.context.dispatch({ type: 'acquire', actionOccurrenceId: 'claim-owner-b', payload: { ownerId: 'owner-b' } });

  assert.equal(conflict.status, 'rejected');
  assert.equal(conflict.error.code, 'ResourceConflict');
  assert.equal(conflict.error instanceof ConflictError, true);
  assert.equal(effectsGetterCalls, 0);
  assert.deepEqual(kernel.exportSnapshot().claims, [{
    resourceId: 'resource/radio',
    entries: [{ resourceId: 'resource/radio', ownerId: 'owner-a', mode: 'exclusive', lifecycleScope: 'scope/owner-a' }],
  }]);
});

test('lifecycle-scoped cleanup releases matching claims in one kernel transaction', async () => {
  const kernel = createKernel({
    handlers: {
      acquire: () => ({ claims: [{ resourceId: 'resource/door', ownerId: 'owner/quest-a', mode: 'exclusive', lifecycleScope: 'scope/quest-a' }] }),
      cleanup: () => ({
        releaseClaimLifecycleScopes: ['scope/quest-a'],
        effects: [{ op: 'state.set', key: 'cleaned', value: true }],
      }),
    },
  });
  await kernel.context.dispatch({ type: 'acquire', actionOccurrenceId: 'scope-acquire', payload: null });
  assert.equal(kernel.exportSnapshot().claims[0].entries[0].lifecycleScope, 'scope/quest-a');
  const cleanup = await kernel.context.dispatch({ type: 'cleanup', actionOccurrenceId: 'scope-cleanup', payload: null });
  assert.equal(cleanup.status, 'committed');
  assert.deepEqual(kernel.exportSnapshot().claims, []);
  assert.equal(kernel.context.select((state) => state.cleaned), true);
});

test('selectors and events expose immutable copies and subscriptions dispose cleanly', async () => {
  const kernel = createKernel({
    initialState: { nested: { value: 1 } },
    handlers: { emit: () => ({ events: [{ type: 'sample', payload: { value: 1 } }] }) },
  });
  const selected = kernel.context.select((state) => state.nested);
  assert.equal(Object.isFrozen(selected), true);
  assert.throws(() => { selected.value = 2; }, TypeError);
  const subscription = kernel.context.subscribe('sample', () => {});
  subscription.dispose();
  assert.equal(subscription.disposed, true);
  await kernel.context.dispatch({ type: 'emit', actionOccurrenceId: 'emit-1', payload: null });
  assert.equal('state' in kernel.context, false);
});

test('runtime query exposes immutable state and resource-claim snapshots without a store writer', async () => {
  const kernel = createKernel({
    initialState: { safe: true },
    handlers: {
      claim: () => ({
        claims: [{ resourceId: 'resource/query', ownerId: 'owner/query', mode: 'exclusive', lifecycleScope: 'scope/query' }],
      }),
    },
  });
  await kernel.context.dispatch({ type: 'claim', actionOccurrenceId: 'query-claim', payload: null });

  const snapshot = kernel.context.query((model) => model);
  assert.deepEqual(snapshot.claims, [{
    resourceId: 'resource/query',
    entries: [{ resourceId: 'resource/query', ownerId: 'owner/query', mode: 'exclusive', lifecycleScope: 'scope/query' }],
  }]);
  assert.throws(() => { snapshot.claims[0].entries.push({}); }, TypeError);
  assert.deepEqual(kernel.context.query(({ state }) => state), { safe: true });
});

test('event delivery snapshots subscriptions and isolates async subscribers', async () => {
  const bus = new OrderedEventBus();
  let deliveries = 0;
  bus.subscribe('event', () => {
    deliveries += 1;
    bus.subscribe('event', () => { deliveries += 1; });
  });
  const firstErrors = bus.publish({ type: 'event', payload: null, sequence: 1, transactionId: 'tx:event-1' });
  assert.deepEqual(firstErrors, []);
  assert.equal(deliveries, 1);
  bus.publish({ type: 'event', payload: null, sequence: 2, transactionId: 'tx:event-2' });
  assert.equal(deliveries, 3);

  const asyncBus = new OrderedEventBus();
  const asyncOrder = [];
  asyncBus.subscribe('event', async () => {
    asyncOrder.push('async');
    throw new Error('async failure');
  });
  asyncBus.subscribe('event', () => { asyncOrder.push('sync'); });
  const asyncErrors = asyncBus.publish({ type: 'event', payload: null, sequence: 1, transactionId: 'tx:async' });
  assert.deepEqual(asyncErrors, ['1:async subscribers are not supported']);
  assert.deepEqual(asyncOrder, ['async', 'sync']);
  await Promise.resolve();
});

test('dispatch resolves when a subscriber registers another subscriber during delivery', async () => {
  const kernel = createKernel({
    handlers: { emit: () => ({ events: [{ type: 'event', payload: null }] }) },
  });
  let originalDeliveries = 0;
  let deferredDeliveries = 0;
  kernel.context.subscribe('event', () => {
    originalDeliveries += 1;
    kernel.context.subscribe('event', () => { deferredDeliveries += 1; });
  });
  const first = await kernel.context.dispatch({ type: 'emit', actionOccurrenceId: 'self-subscribe-1', payload: null });
  assert.equal(first.status, 'committed');
  assert.equal(originalDeliveries, 1);
  assert.equal(deferredDeliveries, 0);
  const second = await kernel.context.dispatch({ type: 'emit', actionOccurrenceId: 'self-subscribe-2', payload: null });
  assert.equal(second.status, 'committed');
  assert.equal(originalDeliveries, 2);
  assert.equal(deferredDeliveries, 1);
});

test('occurrence is recorded before ordered event delivery', async () => {
  const kernel = createKernel({
    handlers: { emit: () => ({ events: [{ type: 'recorded', payload: null }] }) },
  });
  let occurrenceSeen = false;
  kernel.context.subscribe('recorded', () => {
    occurrenceSeen = kernel.exportSnapshot().occurrences.some(({ actionOccurrenceId }) => actionOccurrenceId === 'emit-recorded');
  });
  await kernel.context.dispatch({ type: 'emit', actionOccurrenceId: 'emit-recorded', payload: null });
  assert.equal(occurrenceSeen, true);
});

test('StateStore reducer transaction commits all effects or none', () => {
  const store = new StateStore({ count: 1, label: 'safe' });
  assert.throws(() => store.reduceTransaction([
    { op: 'state.increment', key: 'count', delta: 1 },
    { op: 'state.increment', key: 'label', delta: 1 },
  ]));
  assert.deepEqual(store.snapshot(), { count: 1, label: 'safe' });
  const committed = store.reduceTransaction([{ op: 'state.increment', key: 'count', delta: 2 }]);
  assert.deepEqual(committed, { count: 3, label: 'safe' });
  assert.equal(Object.isFrozen(committed), true);
});

test('async handlers are rejected during preflight without state or events', async () => {
  const kernel = createKernel({
    initialState: { safe: true },
    handlers: { async: async () => ({ effects: [{ op: 'state.set', key: 'safe', value: false }] }) },
  });
  const result = await kernel.context.dispatch({ type: 'async', actionOccurrenceId: 'async-1', payload: null });
  assert.equal(result.status, 'rejected');
  assert.equal(result.error instanceof PreflightError, true);
  assert.deepEqual(kernel.exportSnapshot().state, { safe: true });
});

test('rejected async handlers are drained and remain ledgered atomic preflight failures', async () => {
  const kernel = createKernel({
    initialState: { safe: true },
    handlers: { asyncReject: async () => { throw new Error('async handler failure'); } },
  });
  const command = { type: 'asyncReject', actionOccurrenceId: 'async-reject-1', payload: null };
  const first = await kernel.context.dispatch(command);
  const duplicate = await kernel.context.dispatch(command);
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(first.error instanceof PreflightError, true);
  assert.equal(duplicate.error instanceof DuplicateOccurrence, true);
  assert.equal(duplicate.error.savedResult, first);
  assert.deepEqual(kernel.exportSnapshot().state, { safe: true });
  assert.equal(kernel.exportSnapshot().occurrences.length, 1);
});

test('thenables with a rejection field are drained before rejection-plan inspection', async () => {
  const kernel = createKernel({
    handlers: {
      maliciousThenable: () => {
        const plan = Promise.reject(new Error('thenable rejection'));
        plan.rejection = { code: 'FakeRejection', message: 'must not be used' };
        return plan;
      },
    },
  });
  const command = { type: 'maliciousThenable', actionOccurrenceId: 'thenable-rejection-field', payload: null };
  const first = await kernel.context.dispatch(command);
  const duplicate = await kernel.context.dispatch(command);
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(first.error instanceof PreflightError, true);
  assert.notEqual(first.error.code, 'FakeRejection');
  assert.equal(duplicate.error.savedResult, first);
  assert.deepEqual(kernel.exportSnapshot().state, {});
});
