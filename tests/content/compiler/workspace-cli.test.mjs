import assert from 'node:assert/strict';
import { cp, mkdir, mkdtemp, readFile, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { auditWorkspace } from '../../../scripts/content/compile-content.mjs';
import { runContentCli } from '../../../scripts/content/content-cli.mjs';

test('empty authoring workspace is an explicit 0/0 audit and never a pack', async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-empty-'));
  await mkdir(path.join(cwd, 'content'), { recursive: true });
  const result = await auditWorkspace({ cwd });

  assert.deepEqual(result, { ok: true, checked: { modules: 0, campaigns: 0 }, diagnostics: [] });
  assert.equal('pack' in result, false);
});

test('unknown module ID fails workspace selection exactly', async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-empty-'));
  const result = await auditWorkspace({ cwd, moduleSelection: 'urman.unknown' });

  assert.equal(result.ok, false);
  assert.equal(result.diagnostics[0].code, 'UnknownModuleSelection');
  assert.equal('pack' in result, false);
});

test('module-only workspace audit applies semantic provides checks', async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-workspace-'));
  const target = path.join(cwd, 'content', 'modules', 'fixture');
  await mkdir(path.dirname(target), { recursive: true });
  await cp('tests/content/compiler/fixtures/valid/module', target, { recursive: true });
  const manifestPath = path.join(target, 'module.json');
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  manifest.provides.push('urman.compiler-fixture:text/not-authored');
  await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);

  const result = await auditWorkspace({ cwd, moduleSelection: 'urman.compiler-fixture' });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'UnresolvedReference' && jsonPointer === '/provides/2'));
});

test('module-only audit rejects definitions owned by another module', async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-owner-'));
  const target = path.join(cwd, 'content', 'modules', 'fixture');
  await mkdir(path.dirname(target), { recursive: true });
  await cp('tests/content/compiler/fixtures/valid/module', target, { recursive: true });
  const definitionsPath = path.join(target, 'definitions.json');
  const definitions = JSON.parse(await readFile(definitionsPath, 'utf8'));
  definitions[0].id = 'foreign.module:text/arrival-title';
  await writeJson(definitionsPath, definitions);

  const result = await auditWorkspace({ cwd, moduleSelection: 'urman.compiler-fixture' });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code, jsonPointer }) => code === 'UndeclaredDependency' && jsonPointer === '/0/id'));
});

test('compile CLI never discovers modules when explicit selections are absent', async () => {
  const errors = [];
  const exitCode = await runContentCli([
    'compile',
    '--campaign',
    'tests/content/compiler/fixtures/valid/campaign/campaign.json',
  ], { log() {}, error(message) { errors.push(message); } });

  assert.equal(exitCode, 1);
  assert.ok(errors.some((message) => message.includes('MissingModuleSelection')));
});

async function writeJson(file, value) {
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}

test('selected module audit includes dependency closure when detecting cycles', async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-cycle-'));
  const moduleRoot = path.join(cwd, 'content', 'modules');
  for (const [id, dependency] of [['probe.a', 'probe.b'], ['probe.b', 'probe.a']]) {
    const directory = path.join(moduleRoot, id);
    await mkdir(directory, { recursive: true });
    await writeJson(path.join(directory, 'module.json'), {
      schemaVersion: 1,
      moduleId: id,
      exactVersion: '1.0.0',
      sourceFiles: [],
      assets: [],
      dependencies: [{ moduleId: dependency, exactVersion: '1.0.0' }],
      provides: id === 'probe.b' ? ['probe.b:text/missing'] : [],
      requires: [],
    });
  }
  const result = await auditWorkspace({ cwd, moduleSelection: 'probe.a' });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code }) => code === 'DependencyCycle'));
  assert.ok(result.diagnostics.some(({ code, moduleId }) => code === 'UnresolvedReference' && moduleId === 'probe.b'));
});

test('malformed discovered campaign returns schema diagnostics instead of throwing', async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-campaign-'));
  const directory = path.join(cwd, 'content', 'campaigns', 'bad');
  await mkdir(directory, { recursive: true });
  await writeJson(path.join(directory, 'campaign.json'), {
    schemaVersion: 1,
    id: 'urman.bad',
    exactVersion: '1.0.0',
    entrypoint: 'urman.bad:scene/start',
    modules: 'bad',
    roleBindings: {},
    capabilityRequirements: [],
    narrativeOrder: ['urman.bad:scene/start'],
    invariants: [],
  });
  const result = await auditWorkspace({ cwd });
  assert.equal(result.ok, false);
  assert.ok(result.diagnostics.some(({ code }) => code === 'InvalidSchema'));
});

test('unknown CLI module ID is rendered from a typed compiler diagnostic', async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-unknown-'));
  const errors = [];
  const exitCode = await runContentCli(['compile', '--campaign', 'campaign.json', '--module', 'urman.unknown'], {
    log() {},
    error(message) { errors.push(message); },
  }, { cwd });
  assert.equal(exitCode, 1);
  assert.deepEqual(errors, ['UnknownModuleSelection content/modules/module: Unknown module selection urman.unknown.']);
});

test('compile CLI resolves repeatable explicit module path and dev module ID', async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'urman-mm30-cli-'));
  const moduleA = path.join(cwd, 'content', 'modules', 'fixture');
  const moduleB = path.join(cwd, 'content', 'modules', 'helper');
  const campaignDirectory = path.join(cwd, 'content', 'campaigns', 'main');
  await mkdir(path.dirname(moduleA), { recursive: true });
  await mkdir(moduleB, { recursive: true });
  await mkdir(campaignDirectory, { recursive: true });
  await cp('tests/content/compiler/fixtures/valid/module', moduleA, { recursive: true });
  await writeJson(path.join(moduleB, 'module.json'), {
    schemaVersion: 1,
    moduleId: 'urman.compiler-helper',
    exactVersion: '1.0.0',
    sourceFiles: [],
    assets: [],
    dependencies: [],
    provides: [],
    requires: [],
  });
  const campaign = JSON.parse(await readFile('tests/content/compiler/fixtures/valid/campaign/campaign.json', 'utf8'));
  campaign.modules.push({ moduleId: 'urman.compiler-helper', exactVersion: '1.0.0' });
  await writeJson(path.join(campaignDirectory, 'campaign.json'), campaign);
  const output = [];
  const errors = [];
  const exitCode = await runContentCli([
    'compile',
    '--campaign', 'content/campaigns/main/campaign.json',
    '--module', 'content/modules/fixture/module.json',
    '--module', 'urman.compiler-helper',
  ], { log(message) { output.push(message); }, error(message) { errors.push(message); } }, { cwd });
  assert.equal(exitCode, 0, errors.join('\n'));
  assert.equal(JSON.parse(output[0]).campaign.orderedModules.length, 2);
});
