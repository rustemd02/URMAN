import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import { checkRuntimeArchitecture } from '../../../scripts/check-runtime-architecture.mjs';
import { compileContent } from '../../../scripts/content/compile-content.mjs';
import { AssetResolver, TextResolver, validateManifestClosure } from '../../../src/runtime/resolvers/content-resolvers.mjs';
import { ContentRegistry } from '../../../src/runtime/registries/runtime-registries.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');

function relative(filePath) {
  return path.relative(ROOT, filePath).split(path.sep).join('/');
}

async function findNamedFiles(directory, filename) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name))) {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await findNamedFiles(target, filename));
    else if (entry.isFile() && entry.name === filename) files.push(target);
  }
  return files.sort();
}

async function moduleManifestPaths() {
  const paths = await findNamedFiles(path.join(ROOT, 'content/modules'), 'module.json');
  const records = await Promise.all(paths.map(async (manifestPath) => ({
    manifestPath,
    manifest: JSON.parse(await readFile(manifestPath, 'utf8')),
  })));
  return new Map(records.map(({ manifestPath, manifest }) => [manifest.moduleId, manifestPath]));
}

async function campaigns() {
  const paths = await findNamedFiles(path.join(ROOT, 'content/campaigns'), 'campaign.json');
  return Promise.all(paths.map(async (campaignPath) => ({
    campaignPath,
    campaign: JSON.parse(await readFile(campaignPath, 'utf8')),
  })));
}

test('every authored campaign compiles from exact module selections and resolves its portable closure', async () => {
  const [manifestById, campaignRecords] = await Promise.all([moduleManifestPaths(), campaigns()]);
  assert.equal(campaignRecords.length, 4, 'closure gate must enumerate every authored campaign, including dev-only and full-game campaigns');

  for (const { campaignPath, campaign } of campaignRecords) {
    const modulePaths = campaign.modules.map(({ moduleId }) => {
      const modulePath = manifestById.get(moduleId);
      assert.ok(modulePath, `${campaign.id} selects existing module ${moduleId}`);
      return relative(modulePath);
    });
    const result = await compileContent({
      cwd: ROOT,
      campaignPath: relative(campaignPath),
      moduleManifestPaths: modulePaths,
    });
    assert.equal(result.ok, true, `${campaign.id}: ${JSON.stringify(result.diagnostics)}`);
    assert.deepEqual(result.pack.campaign.orderedModules, campaign.modules, `${campaign.id} preserves exact ordered module selection`);
    assert.deepEqual(result.pack.moduleFingerprints.map(({ moduleId, exactVersion }) => ({ moduleId, exactVersion })),
      [...campaign.modules].sort((left, right) => left.moduleId.localeCompare(right.moduleId)), `${campaign.id} locks exact module versions`);

    const content = ContentRegistry.fromPack(result.pack);
    assert.equal(content.get(result.pack.campaign.entrypoint).id, result.pack.campaign.entrypoint, `${campaign.id} resolves its entrypoint`);
    for (const [role, characterId] of Object.entries(result.pack.campaign.roleBindings)) {
      assert.match(characterId, /:character\//, `${campaign.id} role ${role} resolves to a character ID`);
      assert.equal(content.get(characterId).id, characterId, `${campaign.id} resolves role ${role}`);
    }
    for (const requirement of result.pack.campaign.capabilityRequirements) {
      assert.ok(result.pack.registries.capabilities.some((provider) => provider.protocolId === requirement.protocolId
        && provider.exactVersion === requirement.exactVersion), `${campaign.id} resolves ${requirement.protocolId}@${requirement.exactVersion}`);
    }

    const closure = validateManifestClosure(result.pack);
    const assets = new AssetResolver(result.pack, {
      resolveFileUrl: (file, context) => `runtime://${context.moduleId}/${file}`,
    });
    const texts = new TextResolver(result.pack);
    for (const assetId of closure.assetIds) {
      const resolved = assets.resolve(assetId);
      assert.match(resolved.url, /^runtime:\/\//, `${campaign.id} resolves logical asset ${assetId}`);
      assert.ok(resolved.accessibility, `${campaign.id} retains accessibility metadata for ${assetId}`);
    }
    for (const textId of closure.textIds) {
      assert.notEqual(texts.resolve(textId).text, '', `${campaign.id} resolves default text ${textId}`);
      assert.notEqual(texts.resolve(textId, 'ru').text, '', `${campaign.id} resolves localized text ${textId}`);
    }
  }
});

test('production source closure has no legacy, lab, browser-global or narrative-owner escape hatch', async () => {
  const result = await checkRuntimeArchitecture({ cwd: ROOT });
  assert.equal(result.ok, true, result.diagnostics.map(({ rule, sourcePath, message }) => `${rule} ${sourcePath}: ${message}`).join('\n'));
  assert.ok(result.checkedFiles.includes('src/game/Game.ts'));
  assert.equal(result.checkedFiles.some((file) => file.startsWith('src/lab/')), false);
  assert.equal(result.checkedFiles.some((file) => file.startsWith('src/MainMap/')), false);
  assert.equal(result.checkedFiles.some((file) => file.startsWith('src/runtime/modules/legacy-mainmap/')), false);
});

test('dev-only Lab implementation is excluded, while production imports of Lab and MainMap remain forbidden', async () => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'urman-mm55-lab-'));
  try {
    await mkdir(path.join(directory, 'src/lab'), { recursive: true });
    await mkdir(path.join(directory, 'src/game'), { recursive: true });
    await writeFile(path.join(directory, 'src/lab/main.mjs'), [
      "import '../MainMap/MainMapScene';",
      'localStorage.getItem(\'lab-only\');',
      "const narrative = 'urman.chapter1:scene/lab-preview';",
    ].join('\n'));

    const labOnly = await checkRuntimeArchitecture({ cwd: directory, requireRetiredOwnersAbsent: false });
    assert.equal(labOnly.ok, true, JSON.stringify(labOnly.diagnostics));
    assert.equal(labOnly.checkedFiles.includes('src/lab/main.mjs'), false);

    await writeFile(path.join(directory, 'src/game/lab-import.mjs'), "import '../lab/main';\n");
    await writeFile(path.join(directory, 'src/game/mainmap-import.mjs'), "import '../MainMap/MainMapScene';\n");
    const productionImports = await checkRuntimeArchitecture({ cwd: directory, requireRetiredOwnersAbsent: false });
    assert.equal(productionImports.ok, false);
    assert.ok(productionImports.diagnostics.some(({ rule, sourcePath }) => rule === 'ProductionDevImport' && sourcePath === 'src/game/lab-import.mjs'));
    assert.ok(productionImports.diagnostics.some(({ rule, sourcePath }) => rule === 'ProductionDevImport' && sourcePath === 'src/game/mainmap-import.mjs'));
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

test('static closure checker reports each forbidden source boundary with its source file', async () => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'urman-mm55-'));
  try {
    await mkdir(path.join(directory, 'src/runtime/kernel'), { recursive: true });
    await mkdir(path.join(directory, 'src/runtime/bootstrap'), { recursive: true });
    await mkdir(path.join(directory, 'src/game'), { recursive: true });
    await mkdir(path.join(directory, 'src/systems'), { recursive: true });
    await mkdir(path.join(directory, 'src/os/data'), { recursive: true });
    await writeFile(path.join(directory, 'src/runtime/kernel/violation.mjs'), [
      "import '../../MainMap/MainMapScene';",
      "const narrative = 'urman.chapter1:scene/forest';",
      "const asset = '/assets/forest.png';",
      'localStorage.getItem(\'legacy\');',
      'window.URMAN;',
      'switch (sceneId) { case \'forest\': break; }',
    ].join('\n'));
    await writeFile(path.join(directory, 'src/game/Game.ts'), "import '../data/dialogue_data';\n");
    await writeFile(path.join(directory, 'src/game/lab-import.mjs'), "import '../lab/ContentLab';\n");
    await writeFile(path.join(directory, 'src/game/audio-import.mjs'), "import '../systems/AudioSystem';\n");
    await writeFile(path.join(directory, 'src/systems/AudioSystem.ts'), 'export class AudioSystem {}\n');
    await writeFile(path.join(directory, 'src/os/data/web_content.json'), '{"legacy":true}\n');
    for (const kind of ['location', 'route-node', 'item', 'beat', 'document']) {
      await writeFile(path.join(directory, 'src/runtime/kernel', `narrative-${kind}.mjs`), `const id = 'urman.chapter12:${kind}/fixture';\n`);
    }
    await writeFile(path.join(directory, 'src/runtime/bootstrap/capability-composition.mjs'), "const protocolId = 'urman.chapter1:capability/explicit-module-contract';\n");
    await writeFile(path.join(directory, 'src/runtime/kernel/scene-comparison.mjs'), "if (currentSceneId === 'arbitrary-scene') return;\nif ('other-scene' !== destinationSceneToken) return;\n");
    await writeFile(path.join(directory, 'src/runtime/kernel/scene-switch.mjs'), "switch (requestedSceneRef) { case 'arbitrary-scene': break; }\n");
    await writeFile(path.join(directory, 'src/runtime/kernel/dotted-scene-comparison.mjs'), "if (runtime.currentSceneId !== 'forest') return;\n");
    await writeFile(path.join(directory, 'src/runtime/kernel/dotted-scene-switch.mjs'), "switch (flow.targetSceneRef) { case 'other-scene': break; }\n");
    await writeFile(path.join(directory, 'src/runtime/kernel/content-flow.mjs'), "if (typeof targetSceneId === 'string') return targetSceneId;\nswitch (typeof targetSceneId) { case 'string': break; }\n");

    const result = await checkRuntimeArchitecture({ cwd: directory });
    assert.equal(result.ok, false);
    assert.deepEqual([...new Set(result.diagnostics.map(({ rule }) => rule))].sort(), [
      'DirectAssetPath',
      'HardcodedSceneSwitch',
      'NarrativeContentId',
      'ProductionDevImport',
      'RawLocalStorage',
      'RetiredOwnerCaller',
      'RetiredOwnerPresent',
      'WindowUrman',
    ]);
    assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'ProductionDevImport' && sourcePath === 'src/game/lab-import.mjs'));
    assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'RetiredOwnerCaller' && sourcePath === 'src/game/audio-import.mjs'));
    assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'RetiredOwnerPresent' && sourcePath === 'src/systems/AudioSystem.ts'));
    assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'RetiredOwnerPresent' && sourcePath === 'src/os/data/web_content.json'));
    for (const kind of ['location', 'route-node', 'item', 'beat', 'document']) {
      assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'NarrativeContentId' && sourcePath === `src/runtime/kernel/narrative-${kind}.mjs`));
    }
    assert.equal(result.diagnostics.some(({ rule, sourcePath }) => rule === 'NarrativeContentId' && sourcePath === 'src/runtime/bootstrap/capability-composition.mjs'), false);
    assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'HardcodedSceneSwitch' && sourcePath === 'src/runtime/kernel/scene-comparison.mjs'));
    assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'HardcodedSceneSwitch' && sourcePath === 'src/runtime/kernel/scene-switch.mjs'));
    assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'HardcodedSceneSwitch' && sourcePath === 'src/runtime/kernel/dotted-scene-comparison.mjs'));
    assert.ok(result.diagnostics.some(({ rule, sourcePath }) => rule === 'HardcodedSceneSwitch' && sourcePath === 'src/runtime/kernel/dotted-scene-switch.mjs'));
    assert.equal(result.diagnostics.some(({ rule, sourcePath }) => rule === 'HardcodedSceneSwitch' && sourcePath === 'src/runtime/kernel/content-flow.mjs'), false);
    assert.ok(result.diagnostics.every(({ sourcePath }) => sourcePath.startsWith('src/')));
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
