import assert from 'node:assert/strict';
import { cp, mkdtemp, readFile, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { compileContent } from '../../../scripts/content/compile-content.mjs';

const CAMPAIGN = 'tests/content/compiler/fixtures/valid/campaign/campaign.json';
const MODULE = 'tests/content/compiler/fixtures/valid/module/module.json';

test('explicit manifests compile to a deterministic immutable pack', async () => {
  const first = await compileContent({ campaignPath: CAMPAIGN, moduleManifestPaths: [MODULE] });
  const second = await compileContent({ campaignPath: CAMPAIGN, moduleManifestPaths: [MODULE] });

  assert.equal(first.ok, true, JSON.stringify(first.diagnostics));
  assert.deepEqual(first, second);
  assert.equal(Object.isFrozen(first.pack), true);
  assert.equal('diagnostics' in first.pack, false);
  assert.match(first.pack.campaignFingerprint, /^[a-f0-9]{64}$/);
  assert.match(first.pack.moduleFingerprints[0].sha256, /^[a-f0-9]{64}$/);
  assert.deepEqual(first.pack.dependencyGraph, { nodes: ['urman.compiler-fixture'], edges: [] });
  assert.deepEqual(first.pack.registries.scenes.map(({ id }) => id), ['urman.compiler-fixture:scene/arrival']);
  assert.deepEqual(first.pack.registries.texts.map(({ id }) => id), ['urman.compiler-fixture:text/arrival-title']);
});

async function mutableFixture() {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-'));
  await cp('tests/content/compiler/fixtures/valid', directory, { recursive: true });
  return directory;
}

async function readMutable(directory) {
  const definitionsPath = path.join(directory, 'module', 'definitions.json');
  const modulePath = path.join(directory, 'module', 'module.json');
  const campaignPath = path.join(directory, 'campaign', 'campaign.json');
  return {
    definitionsPath,
    modulePath,
    campaignPath,
    definitions: JSON.parse(await readFile(definitionsPath, 'utf8')),
    module: JSON.parse(await readFile(modulePath, 'utf8')),
    campaign: JSON.parse(await readFile(campaignPath, 'utf8')),
  };
}

async function writeJson(file, value) {
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}

function capability(id, protocolId, resourceId = 'urman.compiler-fixture:resource/shared-input') {
  return {
    schemaVersion: 1,
    id,
    protocolId,
    exactVersion: '1.0.0',
    stateSchemaVersion: 1,
    configSchemaRef: 'common.schema.json',
    stateSchemaRef: 'common.schema.json',
    commandSchemaRef: 'common.schema.json',
    eventSchemaRef: 'common.schema.json',
    outcomeSchemaRef: 'common.schema.json',
    requiredAssets: [],
    requiredAudio: [],
    resourceClaims: [{ resourceId, mode: 'exclusive' }],
    accessibility: {
      alternativeInput: true,
      reducedMotion: true,
      captions: true,
      equivalentOutcomeId: `${id.replace(':capability/', ':outcome/')}-complete`,
    },
    lifecycle: {
      supportsRestore: true,
      supportsSnapshot: true,
      supportsReset: true,
      idempotentStop: true,
      idempotentDispose: true,
    },
  };
}

test('duplicate IDs are fatal and never produce a partial pack', async () => {
  const directory = await mutableFixture();
  const definitionsPath = path.join(directory, 'module', 'definitions.json');
  const definitions = JSON.parse(await readFile(definitionsPath, 'utf8'));
  definitions.push({ ...definitions[0] });
  await writeFile(definitionsPath, `${JSON.stringify(definitions, null, 2)}\n`);

  const result = await compileContent({
    cwd: directory,
    campaignPath: 'campaign/campaign.json',
    moduleManifestPaths: ['module/module.json'],
  });

  assert.equal(result.ok, false);
  assert.equal('pack' in result, false);
  assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'DuplicateId' && jsonPointer === '/2/id'));
});

test('duplicate explicit module selections are fatal', async () => {
  const result = await compileContent({ campaignPath: CAMPAIGN, moduleManifestPaths: [MODULE, MODULE] });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code }) => code === 'DuplicateModule'));
});

test('unknown opcodes are rejected by schema validation with an actionable pointer', async () => {
  const directory = await mutableFixture();
  const definitionsPath = path.join(directory, 'module', 'definitions.json');
  const definitions = JSON.parse(await readFile(definitionsPath, 'utf8'));
  definitions[1].onEnter.push({ op: 'runArbitraryScript', script: 'nope' });
  await writeFile(definitionsPath, `${JSON.stringify(definitions, null, 2)}\n`);

  const result = await compileContent({
    cwd: directory,
    campaignPath: 'campaign/campaign.json',
    moduleManifestPaths: ['module/module.json'],
  });

  assert.equal(result.ok, false);
  assert.equal('pack' in result, false);
  assert.ok(result.diagnostics.some(({ code, sourcePath }) => code === 'UnknownOpcode' && sourcePath === 'module/definitions.json'));
});

test('exact-version mismatch and missing entrypoint are fatal preflight errors', async () => {
  const directory = await mutableFixture();
  const campaignPath = path.join(directory, 'campaign', 'campaign.json');
  const campaign = JSON.parse(await readFile(campaignPath, 'utf8'));
  campaign.modules[0].exactVersion = '2.0.0';
  campaign.entrypoint = 'urman.compiler-fixture:scene/missing';
  await writeFile(campaignPath, `${JSON.stringify(campaign, null, 2)}\n`);

  const versionResult = await compileContent({
    cwd: directory,
    campaignPath: 'campaign/campaign.json',
    moduleManifestPaths: ['module/module.json'],
  });
  assert.equal(versionResult.ok, false);
  assert.ok(versionResult.diagnostics.some(({ code }) => code === 'IncompatibleExactVersion'));

  campaign.modules[0].exactVersion = '1.0.0';
  await writeFile(campaignPath, `${JSON.stringify(campaign, null, 2)}\n`);
  const entrypointResult = await compileContent({
    cwd: directory,
    campaignPath: 'campaign/campaign.json',
    moduleManifestPaths: ['module/module.json'],
  });
  assert.equal(entrypointResult.ok, false);
  assert.ok(entrypointResult.diagnostics.some(({ code, jsonPointer }) => code === 'MissingEntrypoint' && jsonPointer === '/entrypoint'));
});

test('schema-derived references reject undeclared modules and missing assets', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.definitions[1].entryConditions.push({
    op: 'npc.state',
    characterId: 'foreign.module:character/ghost',
    stateKey: 'met',
    value: true,
  });
  fixture.definitions[1].assetRefs.push('urman.compiler-fixture:asset/missing');
  await writeJson(fixture.definitionsPath, fixture.definitions);

  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'UndeclaredDependency' && jsonPointer.endsWith('/characterId')));
  assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'MissingAsset' && jsonPointer.endsWith('/assetRefs/0')));
});

test('scene-to-dialogue targets reject a missing dialogue and a mixed scene/dialogue target with exact diagnostics', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.definitions[1].interactions.push({
    id: 'urman.compiler-fixture:interaction/missing-dialogue',
    labelTextId: 'urman.compiler-fixture:text/arrival-title',
    conditions: [],
    effects: [],
    targetDialogueId: 'urman.compiler-fixture:dialogue/missing',
  });
  await writeJson(fixture.definitionsPath, fixture.definitions);

  let result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code, sourcePath, jsonPointer }) => code === 'UnknownDialogueTarget'
    && sourcePath === 'module/definitions.json'
    && jsonPointer === '/1/interactions/0/targetDialogueId'));

  fixture.definitions[1].interactions = [{
    id: 'urman.compiler-fixture:interaction/conflicting-targets',
    labelTextId: 'urman.compiler-fixture:text/arrival-title',
    conditions: [],
    effects: [],
    targetSceneId: 'urman.compiler-fixture:scene/arrival',
    targetDialogueId: 'urman.compiler-fixture:dialogue/missing',
  }];
  await writeJson(fixture.definitionsPath, fixture.definitions);

  result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code, sourcePath, jsonPointer }) => code === 'ConflictingInteractionTarget'
    && sourcePath === 'module/definitions.json'
    && jsonPointer === '/1/interactions/0'));
});

test('dialogue participant and speaker roles must be bound by the campaign', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.definitions.push({
    schemaVersion: 1,
    id: 'urman.compiler-fixture:dialogue/unbound',
    participantRoles: ['witness'],
    startNodeId: 'start',
    nodes: [{ id: 'start', speakerRole: 'witness', textId: 'urman.compiler-fixture:text/arrival-title', conditions: [], effects: [], choices: [] }],
  });
  await writeJson(fixture.definitionsPath, fixture.definitions);

  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, false);
  assert.equal(result.diagnostics.filter(({ code }) => code === 'MissingRoleBinding').length, 2);
});

test('missing, duplicate and conflicting selected capability providers are fatal', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.campaign.capabilityRequirements.push({ protocolId: 'urman.compiler-fixture:capability/protocol-a', exactVersion: '1.0.0' });
  await writeJson(fixture.campaignPath, fixture.campaign);
  let result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.ok(result.diagnostics.some(({ code }) => code === 'MissingCapability'));

  fixture.definitions.push(
    capability('urman.compiler-fixture:capability/provider-a', 'urman.compiler-fixture:capability/protocol-a'),
    capability('urman.compiler-fixture:capability/provider-a-duplicate', 'urman.compiler-fixture:capability/protocol-a'),
  );
  await writeJson(fixture.definitionsPath, fixture.definitions);
  result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'DuplicateCapabilityProvider' && jsonPointer === '/3/protocolId'));

  fixture.definitions.pop();
  fixture.definitions.push(capability('urman.compiler-fixture:capability/provider-b', 'urman.compiler-fixture:capability/protocol-b'));
  fixture.campaign.capabilityRequirements.push({ protocolId: 'urman.compiler-fixture:capability/protocol-b', exactVersion: '1.0.0' });
  await writeJson(fixture.definitionsPath, fixture.definitions);
  await writeJson(fixture.campaignPath, fixture.campaign);
  result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.ok(result.diagnostics.some(({ code }) => code === 'ResourceClaimConflict'));
});

test('quest capability requirements participate in provider preflight', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.definitions.push({
    schemaVersion: 1,
    id: 'urman.compiler-fixture:quest/radio',
    titleTextId: 'urman.compiler-fixture:text/arrival-title',
    stages: [{
      id: 'start',
      objectives: [{
        id: 'tune',
        titleTextId: 'urman.compiler-fixture:text/arrival-title',
        optional: false,
        startConditions: [],
        completionConditions: [],
        completionEffects: [],
        failureEffects: [],
        capability: {
          protocolId: 'foreign.module:capability/missing-radio',
          exactVersion: '1.0.0',
          configRef: 'urman.compiler-fixture:text/arrival-title',
          outcomeSchemaRef: 'common.schema.json',
        },
      }],
      composition: { mode: 'all', objectiveIds: ['tune'] },
    }],
    outcomes: { success: [], optional: [], failure: [] },
    retryPolicy: { mode: 'none', maximumAttempts: 1 },
    checkpointPolicy: { mode: 'objective' },
    cancelPolicy: { allowed: true, effects: [] },
  });
  await writeJson(fixture.definitionsPath, fixture.definitions);
  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'MissingCapability' && jsonPointer.includes('/objectives/0/capability')));
  assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'UndeclaredDependency' && jsonPointer.endsWith('/capability/protocolId')));
});

test('compiled campaign retains the deterministic union of campaign, module and quest capability requirements', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  const protocols = ['campaign', 'module', 'quest'].map((name) => `urman.compiler-fixture:capability/${name}-protocol`);
  fixture.campaign.capabilityRequirements.push({ protocolId: protocols[0], exactVersion: '1.0.0' });
  fixture.module.requires.push({ protocolId: protocols[1], exactVersion: '1.0.0' });
  fixture.definitions.push(
    capability('urman.compiler-fixture:capability/campaign-provider', protocols[0], 'urman.compiler-fixture:resource/campaign'),
    capability('urman.compiler-fixture:capability/module-provider', protocols[1], 'urman.compiler-fixture:resource/module'),
    capability('urman.compiler-fixture:capability/quest-provider', protocols[2], 'urman.compiler-fixture:resource/quest'),
    {
      schemaVersion: 1,
      id: 'urman.compiler-fixture:quest/capability-union',
      titleTextId: 'urman.compiler-fixture:text/arrival-title',
      stages: [{
        id: 'start',
        objectives: [{
          id: 'use-capability',
          titleTextId: 'urman.compiler-fixture:text/arrival-title',
          optional: false,
          startConditions: [],
          completionConditions: [],
          completionEffects: [],
          failureEffects: [],
          capability: {
            protocolId: protocols[2],
            exactVersion: '1.0.0',
            configRef: 'urman.compiler-fixture:text/arrival-title',
            outcomeSchemaRef: 'common.schema.json',
          },
        }],
        composition: { mode: 'all', objectiveIds: ['use-capability'] },
      }],
      outcomes: { success: [], optional: [], failure: [] },
      retryPolicy: { mode: 'none', maximumAttempts: 1 },
      checkpointPolicy: { mode: 'objective' },
      cancelPolicy: { allowed: true, effects: [] },
    },
  );
  await writeJson(fixture.campaignPath, fixture.campaign);
  await writeJson(fixture.modulePath, fixture.module);
  await writeJson(fixture.definitionsPath, fixture.definitions);

  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, true, JSON.stringify(result.diagnostics));
  assert.deepEqual(result.pack.campaign.capabilityRequirements, protocols.sort().map((protocolId) => ({ protocolId, exactVersion: '1.0.0' })));
});

test('scene.request effects contribute typed reachability edges', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.definitions.push({ ...fixture.definitions[1], id: 'urman.compiler-fixture:scene/effect-target' });
  fixture.definitions[1].onEnter.push({ op: 'scene.request', sceneId: 'urman.compiler-fixture:scene/effect-target' });
  fixture.campaign.invariants = [{
    id: 'urman.compiler-fixture:invariant/effect-reachable',
    description: 'Effect target is reachable.',
    severity: 'error',
    kind: 'required-reachable',
    targetId: 'urman.compiler-fixture:scene/effect-target',
  }];
  fixture.campaign.narrativeOrder.push('urman.compiler-fixture:scene/effect-target');
  await writeJson(fixture.definitionsPath, fixture.definitions);
  await writeJson(fixture.campaignPath, fixture.campaign);

  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, true, JSON.stringify(result.diagnostics));
});

test('schema context treats route, custody and required-audio IDs as references', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.definitions[1].onEnter.push(
    { op: 'route.unlock', routeNodeId: 'foreign.module:route/forest' },
    { op: 'item.transaction', transactions: [{ itemId: 'urman.compiler-fixture:item/key', quantityDelta: 1, fromCustodianId: 'foreign.module:character/keeper' }] },
  );
  const provider = capability('urman.compiler-fixture:capability/audio-provider', 'urman.compiler-fixture:capability/audio-protocol');
  provider.requiredAudio.push('foreign.module:asset/voice');
  fixture.definitions.push(provider);
  await writeJson(fixture.definitionsPath, fixture.definitions);

  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, false);
  for (const suffix of ['/routeNodeId', '/fromCustodianId', '/requiredAudio/0']) {
    assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'UndeclaredDependency' && jsonPointer.endsWith(suffix)), suffix);
  }
});

test('module capability requirements keep their manifest pointer', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.module.requires.push({ protocolId: 'urman.compiler-fixture:capability/missing', exactVersion: '1.0.0' });
  await writeJson(fixture.modulePath, fixture.module);
  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code, sourcePath, jsonPointer }) => code === 'MissingCapability' && sourcePath === 'module/module.json' && jsonPointer === '/requires/0'));
});

test('required reachability and reveal ordering are enforced from typed invariants', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.definitions.push({ ...fixture.definitions[1], id: 'urman.compiler-fixture:scene/unreachable' });
  fixture.campaign.narrativeOrder = [
    'urman.compiler-fixture:text/arrival-title',
    'urman.compiler-fixture:beat/rinat-do-not-answer',
  ];
  fixture.campaign.invariants = [
    {
      id: 'urman.compiler-fixture:invariant/reachable',
      description: 'Required scene is reachable.',
      severity: 'error',
      kind: 'required-reachable',
      targetId: 'urman.compiler-fixture:scene/unreachable',
    },
    {
      id: 'urman.compiler-fixture:invariant/reveal',
      description: 'Text is revealed after the beat.',
      severity: 'error',
      kind: 'reveal-not-before',
      subjectId: 'urman.compiler-fixture:text/arrival-title',
      afterId: 'urman.compiler-fixture:beat/rinat-do-not-answer',
    },
  ];
  await writeJson(fixture.definitionsPath, fixture.definitions);
  await writeJson(fixture.campaignPath, fixture.campaign);

  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code }) => code === 'UnreachableRequiredEntry'));
  assert.ok(result.diagnostics.some(({ code }) => code === 'NarrativeInvariantViolation'));
});

test('module dependency cycles are fatal', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.module.dependencies = [{ moduleId: 'urman.compiler-helper', exactVersion: '1.0.0' }];
  fixture.campaign.modules.push({ moduleId: 'urman.compiler-helper', exactVersion: '1.0.0' });
  const helperDirectory = path.join(directory, 'helper');
  await cp(path.join(directory, 'module'), helperDirectory, { recursive: true });
  const helperManifestPath = path.join(helperDirectory, 'module.json');
  await writeJson(helperManifestPath, {
    schemaVersion: 1,
    moduleId: 'urman.compiler-helper',
    exactVersion: '1.0.0',
    sourceFiles: [],
    assets: [],
    dependencies: [{ moduleId: 'urman.compiler-fixture', exactVersion: '1.0.0' }],
    provides: [],
    requires: [],
  });
  await writeJson(fixture.modulePath, fixture.module);
  await writeJson(fixture.campaignPath, fixture.campaign);
  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json', 'helper/module.json'] });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code }) => code === 'DependencyCycle'));
});

test('Markdown source body is normalized into the document registry and unlisted files are ignored', async () => {
  const directory = await mutableFixture();
  const fixture = await readMutable(directory);
  fixture.module.sourceFiles.push('note.md');
  fixture.module.provides.push('urman.compiler-fixture:document/note');
  await writeJson(fixture.modulePath, fixture.module);
  await writeFile(path.join(directory, 'module', 'note.md'), `---\nschemaVersion: 1\nid: urman.compiler-fixture:document/note\ntitle: {"default":"Note","translations":{}}\nformat: markdown\nsourceFile: note.md\noldPc: {"type":"record","pcSection":"internal_accounting","canonStatus":"canon","reliability":"partial_truth","searchTerms":["реестр"],"suggestedTerms":["граница"]}\nassetRefs: []\nknowledgeRefs: []\naccessConditions: []\nopenEffects: []\n---\n    Line one.   \n    Line two.\n`);
  await writeFile(path.join(directory, 'module', 'unlisted.json'), '{ invalid json');

  const result = await compileContent({ cwd: directory, campaignPath: 'campaign/campaign.json', moduleManifestPaths: ['module/module.json'] });
  assert.equal(result.ok, true, JSON.stringify(result.diagnostics));
  assert.equal(result.pack.registries.documents[0].bodyMarkdown, '    Line one.   \n    Line two.\n');
  assert.deepEqual(result.pack.registries.documents[0].oldPc, {
    type: 'record',
    pcSection: 'internal_accounting',
    canonStatus: 'canon',
    reliability: 'partial_truth',
    searchTerms: ['реестр'],
    suggestedTerms: ['граница'],
  });
});
