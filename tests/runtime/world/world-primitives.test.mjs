import assert from 'node:assert/strict';
import test from 'node:test';

import { RuntimeKernel } from '../../../src/runtime/kernel/runtime-kernel.mjs';
import { CapabilityRegistry } from '../../../src/runtime/registries/runtime-registries.mjs';
import { planCustodyBatch, itemResourceClaims } from '../../../src/runtime/world/custody-store.mjs';
import { DeterministicScheduler } from '../../../src/runtime/world/deterministic-scheduler.mjs';
import { planEvidenceClaim } from '../../../src/runtime/world/evidence-provenance.mjs';
import { LogicalClock } from '../../../src/runtime/world/logical-clock.mjs';
import { OwnerRngStreams } from '../../../src/runtime/world/seeded-rng.mjs';

const EMPTY_PACK = Object.freeze({
  campaign: Object.freeze({ capabilityRequirements: Object.freeze([]) }),
  registries: Object.freeze({ capabilities: Object.freeze([]) }),
});

function kernel(options = {}) {
  return new RuntimeKernel({ ...options, capabilityRegistry: new CapabilityRegistry(EMPTY_PACK) });
}

test('logical clock and owner streams are serializable and deterministic without wall time', () => {
  const firstClock = new LogicalClock({ tick: 5 });
  assert.equal(firstClock.advance(4), 9);
  assert.deepEqual(LogicalClock.fromSnapshot(firstClock.exportSnapshot()).exportSnapshot(), { tick: 9 });

  const first = new OwnerRngStreams({ seed: 'same-run' });
  const alpha = [first.stream('owner/a').nextUint32(), first.stream('owner/a').nextUint32()];
  first.stream('owner/b').nextUint32();
  const beta = first.stream('owner/a').nextUint32();
  const restored = OwnerRngStreams.fromSnapshot(JSON.parse(JSON.stringify(first.exportSnapshot())));
  const afterRestore = restored.stream('owner/a').nextUint32();

  const second = new OwnerRngStreams({ seed: 'same-run' });
  const repeated = [second.stream('owner/a').nextUint32(), second.stream('owner/a').nextUint32()];
  second.stream('owner/b').nextUint32();
  assert.deepEqual(alpha, repeated);
  assert.equal(beta, second.stream('owner/a').nextUint32());
  assert.equal(afterRestore, first.stream('owner/a').nextUint32());
});

test('scheduler reoffers an unacknowledged job after restore and fires it once after committed acknowledgement', () => {
  const scheduler = new DeterministicScheduler();
  scheduler.schedule({ jobId: 'job/reveal', ownerId: 'capability/reveal', dueTick: 4, payload: { kind: 'reveal' } });
  const extracted = scheduler.claimDue(4);
  assert.deepEqual(extracted.map(({ jobId, occurrenceId }) => [jobId, occurrenceId]), [['job/reveal', 'scheduled:job/reveal:4']]);

  // A live action that fails before kernel commit releases its lease, so it
  // cannot stall the logical timer until a save/load boundary.
  scheduler.release(extracted[0].jobId, extracted[0].occurrenceId);
  const retriedLiveAction = scheduler.claimDue(4);
  assert.deepEqual(retriedLiveAction.map(({ jobId, occurrenceId }) => [jobId, occurrenceId]), [['job/reveal', 'scheduled:job/reveal:4']]);

  // A save before the action transaction commits does not persist the lease.
  const restoredBeforeAck = DeterministicScheduler.fromSnapshot(JSON.parse(JSON.stringify(scheduler.exportSnapshot())));
  const reoffered = restoredBeforeAck.claimDue(4);
  assert.deepEqual(reoffered.map(({ jobId, occurrenceId }) => [jobId, occurrenceId]), [['job/reveal', 'scheduled:job/reveal:4']]);
  restoredBeforeAck.acknowledge(reoffered[0].jobId, reoffered[0].occurrenceId);

  const afterFire = DeterministicScheduler.fromSnapshot(JSON.parse(JSON.stringify(restoredBeforeAck.exportSnapshot())));
  assert.deepEqual(afterFire.claimDue(100), []);
  assert.throws(() => afterFire.schedule({ jobId: 'job/reveal', ownerId: 'capability/reveal', dueTick: 5, payload: null }), /already been used/);
});

test('custody batch claims the item, so competing consume receives ResourceConflict with zero partial effects', async () => {
  const initialState = {
    custody: [{ itemId: 'item/key', custodyOwnerId: 'npc/keeper', condition: { durability: 1 } }],
  };
  const runtime = kernel({
    initialState,
    handlers: {
      consume: (command, context) => planCustodyBatch({
        state: context.state,
        stateKey: 'custody',
        operations: [{ op: 'consume', itemId: command.payload.itemId, ownerId: 'npc/keeper' }],
        claimOwnerId: command.payload.claimOwnerId,
        claimLifecycleScope: 'action',
      }),
    },
  });
  const first = await runtime.context.dispatch({ type: 'consume', actionOccurrenceId: 'consume/one', payload: { itemId: 'item/key', claimOwnerId: 'action/one' } });
  const second = await runtime.context.dispatch({ type: 'consume', actionOccurrenceId: 'consume/two', payload: { itemId: 'item/key', claimOwnerId: 'action/two' } });

  assert.equal(first.status, 'committed');
  assert.equal(second.status, 'rejected');
  assert.equal(second.error.code, 'ResourceConflict');
  assert.deepEqual(runtime.context.select((state) => state.custody), []);
  assert.deepEqual(itemResourceClaims([{ op: 'consume', itemId: 'item/key', ownerId: 'npc/keeper' }], {
    ownerId: 'capability/item-use', lifecycleScope: 'capability',
  }), [{ resourceId: 'item/item/key', ownerId: 'capability/item-use', mode: 'exclusive', lifecycleScope: 'capability' }]);
});

test('evidence claims retain a deterministic provenance chain', () => {
  const first = planEvidenceClaim({
    state: {}, evidenceId: 'evidence/letter', claimantId: 'actor/a', sourceId: 'document/letter', metadata: { page: 1 }, stateKey: 'evidence',
  });
  const state = { evidence: first.effects[0].value };
  const derived = planEvidenceClaim({
    state, evidenceId: 'evidence/inference', claimantId: 'actor/a', sourceId: 'evidence/letter', stateKey: 'evidence',
  });
  assert.deepEqual(derived.value.provenanceChain, ['document/letter', 'evidence/letter']);
  assert.throws(() => planEvidenceClaim({ state, evidenceId: 'evidence/letter', claimantId: 'actor/a', sourceId: 'document/letter', stateKey: 'evidence' }), /already exists/);
});
