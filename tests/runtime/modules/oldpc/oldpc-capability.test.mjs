import assert from 'node:assert/strict';
import test from 'node:test';

import { CapabilityHost } from '../../../../src/runtime/capabilities/capability-host.mjs';
import { RuntimeKernel } from '../../../../src/runtime/kernel/runtime-kernel.mjs';
import { CapabilityRegistry, ContentRegistry } from '../../../../src/runtime/registries/runtime-registries.mjs';
import { LogicalClock } from '../../../../src/runtime/world/logical-clock.mjs';
import { OwnerRngStreams } from '../../../../src/runtime/world/seeded-rng.mjs';
import { DeterministicScheduler } from '../../../../src/runtime/world/deterministic-scheduler.mjs';
import { OldPcCapabilitySession, createOldPcCapabilityDescriptor } from '../../../../src/runtime/modules/oldpc/oldpc-capability.mjs';
import { createOldPcContentCatalog } from '../../../../src/runtime/modules/oldpc/oldpc-content-catalog.mjs';
import { DedOsAppRegistry } from '../../../../src/runtime/modules/oldpc/dedos-app-registry.mjs';
import { OldPcError, OldPcErrorCode } from '../../../../src/runtime/modules/oldpc/oldpc-errors.mjs';

const MODULE_ID = 'test.oldpc';
const PROTOCOL_ID = 'test.oldpc:capability/archive-hub';
const DOCUMENT_A = 'test.oldpc:document/notice';
const DOCUMENT_B = 'test.oldpc:document/locked-record';
const PRESENTATION_ID = 'test.chapter:scene/evidence-notice';

function localized(value) { return { default: value, translations: { ru: value } }; }

function document(id, {
  section = 'archive_search',
  requires = [],
  presentationIds = [],
  knowledgeRefs = ['test.chapter:knowledge/notice'],
  searchTerms = ['Марат'],
  suggestedTerms = ['реестр'],
} = {}) {
  return {
    schemaVersion: 1,
    id,
    title: localized(id),
    format: 'markdown',
    sourceFile: `documents/${id.slice(id.lastIndexOf('/') + 1)}.md`,
    bodyMarkdown: `# ${id}\n\nМарат и реестр.`,
    assetRefs: [],
    knowledgeRefs,
    accessConditions: requires,
    openEffects: presentationIds.map((sceneId) => ({ op: 'scene.request', sceneId })),
    oldPc: {
      type: 'document',
      pcSection: section,
      canonStatus: 'canon',
      reliability: 'partial_truth',
      searchTerms,
      suggestedTerms,
    },
  };
}

function pack() {
  return Object.freeze({
    campaign: Object.freeze({ capabilityRequirements: Object.freeze([]) }),
    registries: Object.freeze({
      documents: Object.freeze([
        Object.freeze(document(DOCUMENT_A, { presentationIds: [PRESENTATION_ID] })),
        Object.freeze(document(DOCUMENT_B, {
          section: 'internal_accounting',
          requires: [{ op: 'knowledge.status', knowledgeId: 'test.chapter:knowledge/gate', status: 'confirmed' }],
          searchTerms: ['компенсация'],
        })),
      ]),
      capabilities: Object.freeze([]),
    }),
  });
}

function contentRegistry() { return ContentRegistry.fromPack(pack()); }

function runtime({ rejectSave = false, capabilityDescriptors = [] } = {}) {
  const commands = [];
  const kernel = new RuntimeKernel({
    initialState: { unlocked: false },
    capabilityRegistry: new CapabilityRegistry(pack(), capabilityDescriptors),
    handlers: {
      'oldpc.search': (command) => { commands.push(command); return { events: [{ type: 'oldpc.search', payload: command.payload }] }; },
      'oldpc.document.open': (command) => { commands.push(command); return { events: [{ type: 'oldpc.open', payload: command.payload }] }; },
      'oldpc.document.save': (command) => {
        commands.push(command);
        if (rejectSave) return { rejection: { code: 'SaveDenied', message: 'Evidence was rejected.' } };
        return { events: [{ type: 'oldpc.save', payload: command.payload }] };
      },
    },
  });
  return { kernel, commands };
}

async function nextTurn() {
  await new Promise((resolve) => setImmediate(resolve));
  await new Promise((resolve) => setImmediate(resolve));
}

test('catalog is ContentRegistry-only, keeps normalized search and does not reveal locked records without a query', () => {
  const catalog = createOldPcContentCatalog({ contentRegistry: contentRegistry(), moduleId: MODULE_ID });
  assert.deepEqual(catalog.ids(), [DOCUMENT_B, DOCUMENT_A]);
  assert.deepEqual(catalog.search('', { isAccessible: (entry) => entry.id === DOCUMENT_A }).map(({ id }) => id), [DOCUMENT_A]);
  assert.deepEqual(catalog.search('компенсация', { isAccessible: () => false }).map(({ id }) => id), [DOCUMENT_B]);
  assert.deepEqual(catalog.get(DOCUMENT_A).presentationIds, [PRESENTATION_ID]);
  assert.throws(() => catalog.get('legacy_doc_notice'), (error) => error instanceof OldPcError && error.code === OldPcErrorCode.MissingDocument);
});

test('open/save/search send stable commands and snapshot only records UI projection after commits', async () => {
  const { kernel, commands } = runtime({ rejectSave: true });
  const session = new OldPcCapabilitySession({
    runtimeContext: kernel.context,
    contentRegistry: contentRegistry(),
    moduleId: MODULE_ID,
    capabilityInstanceId: 'oldpc/session-a',
    isAccessible: (entry, state) => entry.id === DOCUMENT_A || state.unlocked === true,
  });
  session.start();
  const search = session.handle({ type: 'search', query: 'Марат' });
  const open = session.handle({ type: 'open', documentId: DOCUMENT_A });
  const save = session.handle({ type: 'save', documentId: DOCUMENT_A });
  assert.deepEqual([search.status, open.status, save.status], ['pending', 'pending', 'pending']);
  assert.deepEqual([search.actionOccurrenceId, open.actionOccurrenceId, save.actionOccurrenceId], [
    'oldpc/session-a:oldpc.search:1',
    'oldpc/session-a:oldpc.open:2',
    'oldpc/session-a:oldpc.save:3',
  ]);
  await nextTurn();
  assert.deepEqual(commands.map(({ type }) => type), ['oldpc.search', 'oldpc.document.open', 'oldpc.document.save']);
  assert.deepEqual(commands[1].payload.presentationIds, [PRESENTATION_ID]);
  assert.deepEqual(commands[1].payload.knowledgeRefs, ['test.chapter:knowledge/notice']);
  assert.equal(Object.hasOwn(commands[2].payload, 'knowledgeRefs'), false, 'savedDocumentIds is a non-gating UI bookmark, not progression');
  assert.equal(session.snapshot().query, 'Марат');
  assert.equal(session.snapshot().activeDocumentId, DOCUMENT_A);
  assert.deepEqual(session.snapshot().savedDocumentIds, []);
  assert.throws(() => session.handle({ type: 'open', documentId: DOCUMENT_B }), (error) => error instanceof OldPcError && error.code === OldPcErrorCode.LockedDocument);
});

test('CapabilityHost snapshot restores active document, query and a committed saved clue without a second progression store', async () => {
  const descriptor = createOldPcCapabilityDescriptor({
    contentRegistry: contentRegistry(),
    moduleId: MODULE_ID,
    protocolId: PROTOCOL_ID,
  });
  const { kernel } = runtime({ capabilityDescriptors: [descriptor] });
  const host = new CapabilityHost({
    runtimeContext: kernel.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 'oldpc-test' }),
    scheduler: new DeterministicScheduler(),
    descriptors: [descriptor],
  });
  host.createSession({
    capabilityInstanceId: 'oldpc/host-a',
    protocolId: PROTOCOL_ID,
    exactVersion: '1.0.0',
    config: { moduleId: MODULE_ID },
  });
  host.startSession('oldpc/host-a');
  host.handle('oldpc/host-a', { type: 'search', query: 'Марат' });
  host.handle('oldpc/host-a', { type: 'open', documentId: DOCUMENT_A });
  host.handle('oldpc/host-a', { type: 'save', documentId: DOCUMENT_A });
  await nextTurn();
  const snapshot = host.snapshotSession('oldpc/host-a');
  assert.deepEqual(snapshot.state, {
    activeDocumentId: DOCUMENT_A,
    activeSection: 'archive_search',
    nextActionSequence: 4,
    query: 'Марат',
    savedDocumentIds: [DOCUMENT_A],
  });
  host.createSession({
    capabilityInstanceId: 'oldpc/host-b',
    protocolId: PROTOCOL_ID,
    exactVersion: '1.0.0',
    config: { moduleId: MODULE_ID },
    snapshot: { ...snapshot, capabilityInstanceId: 'oldpc/host-b' },
  });
  host.startSession('oldpc/host-b');
  assert.deepEqual(host.snapshotSession('oldpc/host-b').state, snapshot.state);
});

test('old-PC descriptor provides an immutable host presentation port with terminal subscriptions', () => {
  const descriptor = createOldPcCapabilityDescriptor({
    contentRegistry: contentRegistry(),
    moduleId: MODULE_ID,
    protocolId: PROTOCOL_ID,
  });
  const { kernel } = runtime({ capabilityDescriptors: [descriptor] });
  const host = new CapabilityHost({
    runtimeContext: kernel.context,
    clock: new LogicalClock(),
    rngStreams: new OwnerRngStreams({ seed: 'oldpc-presentation' }),
    scheduler: new DeterministicScheduler(),
    descriptors: [descriptor],
  });
  host.createSession({
    capabilityInstanceId: 'oldpc/presentation',
    protocolId: PROTOCOL_ID,
    exactVersion: '1.0.0',
    config: { moduleId: MODULE_ID },
  });
  host.startSession('oldpc/presentation');

  const presentation = host.presentation('oldpc/presentation');
  const initial = presentation.render();
  assert.equal(Object.isFrozen(initial), true);
  assert.equal(Object.isFrozen(initial.results), true);
  assert.throws(() => initial.savedDocumentIds.push(DOCUMENT_A), TypeError);
  const updates = [];
  const subscription = presentation.subscribe((model) => updates.push(model));
  assert.equal(presentation.handle({ type: 'section', section: 'internal_accounting' }).activeSection, 'internal_accounting');
  assert.equal(updates.length, 1);
  assert.equal(updates[0].activeSection, 'internal_accounting');
  assert.equal(Object.isFrozen(updates[0]), true);

  host.disposeSession('oldpc/presentation');
  assert.equal(subscription.disposed, true);
  assert.throws(() => presentation.render(), /presentation is not active/);
  assert.throws(() => presentation.handle({ type: 'section', section: 'archive_search' }), /not active/);
  assert.throws(() => presentation.subscribe(() => {}), /presentation is not active/);
});

test('opening the official notice commits canonical knowledge and unlocks the register without saving a bookmark', async () => {
  const officialId = 'test.oldpc:document/official-notice';
  const registerId = 'test.oldpc:document/internal-register';
  const officialKnowledgeId = 'test.chapter:knowledge/official-notice';
  const progressionPack = Object.freeze({
    campaign: Object.freeze({ capabilityRequirements: Object.freeze([]) }),
    registries: Object.freeze({
      documents: Object.freeze([
        Object.freeze(document(officialId, {
          knowledgeRefs: [officialKnowledgeId],
          presentationIds: [PRESENTATION_ID],
        })),
        Object.freeze(document(registerId, {
          section: 'internal_accounting',
          knowledgeRefs: ['test.chapter:knowledge/register'],
          requires: [{ op: 'knowledge.status', knowledgeId: officialKnowledgeId, status: 'confirmed' }],
          searchTerms: ['реестр'],
        })),
      ]),
      capabilities: Object.freeze([]),
    }),
  });
  const commands = [];
  const kernel = new RuntimeKernel({
    initialState: { knowledge: {} },
    capabilityRegistry: new CapabilityRegistry(progressionPack),
    handlers: {
      'oldpc.document.open': (command) => {
        commands.push(command);
        return {
          effects: [{
            op: 'state.set',
            key: 'knowledge',
            value: Object.fromEntries(command.payload.knowledgeRefs.map((knowledgeId) => [knowledgeId, 'confirmed'])),
          }],
          events: [{ type: 'oldpc.open', payload: command.payload }],
        };
      },
      'oldpc.document.save': (command) => {
        commands.push(command);
        return { events: [{ type: 'oldpc.save', payload: command.payload }] };
      },
    },
  });
  const session = new OldPcCapabilitySession({
    runtimeContext: kernel.context,
    contentRegistry: ContentRegistry.fromPack(progressionPack),
    moduleId: MODULE_ID,
    capabilityInstanceId: 'oldpc/progression',
    isAccessible: (entry, state) => entry.accessConditions.every((condition) => (
      condition.op !== 'knowledge.status' || state.knowledge[condition.knowledgeId] === condition.status
    )),
  });
  session.start();
  session.handle({ type: 'open', documentId: officialId });
  await nextTurn();
  assert.deepEqual(commands[0].payload, {
    capabilityInstanceId: 'oldpc/progression',
    documentId: officialId,
    knowledgeRefs: [officialKnowledgeId],
    presentationIds: [PRESENTATION_ID],
  });
  assert.equal(kernel.context.select((state) => state.knowledge[officialKnowledgeId]), 'confirmed');
  assert.doesNotThrow(() => session.handle({ type: 'open', documentId: registerId }));
  assert.deepEqual(session.snapshot().savedDocumentIds, [], 'access derives from RuntimeContext, not the bookmark snapshot');
});

test('DedOS app registry uses exact descriptors instead of app-ID switches or implicit globals', () => {
  const registry = new DedOsAppRegistry([
    { appId: 'test.oldpc:app/archive', create: ({ label }) => ({ render: () => label, init: () => undefined }) },
  ]);
  assert.deepEqual(registry.ids(), ['test.oldpc:app/archive']);
  assert.equal(registry.create('test.oldpc:app/archive', { label: 'registry-only' }).render(), 'registry-only');
  assert.throws(
    () => registry.register({ appId: 'test.oldpc:app/archive', create: () => ({ render: () => '' }) }),
    (error) => error instanceof OldPcError && error.code === OldPcErrorCode.InvalidContext,
  );
  assert.throws(
    () => registry.create('test.oldpc:app/missing', {}),
    (error) => error instanceof OldPcError && error.code === OldPcErrorCode.InvalidContext,
  );
});
