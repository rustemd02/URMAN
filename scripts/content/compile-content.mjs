import { createHash } from 'node:crypto';
import { access, readFile, readdir } from 'node:fs/promises';
import path from 'node:path';

import { loadSchemaRegistry, SCHEMA_DIR, validateDocument } from '../validate-content-schemas.mjs';
import { DiagnosticCode, errorDiagnostic, failure, success } from './diagnostics.mjs';

const REGISTRY_BY_KIND = Object.freeze({
  asset: ['assets', 'asset.schema.json'],
  capability: ['capabilities', 'capability.schema.json'],
  character: ['characters', 'character.schema.json'],
  chat: ['chats', 'chat.schema.json'],
  dialogue: ['dialogues', 'dialogue.schema.json'],
  document: ['documents', 'document.schema.json'],
  hint: ['hints', 'hint.schema.json'],
  knowledge: ['knowledge', 'knowledge.schema.json'],
  quest: ['quests', 'quest.schema.json'],
  scene: ['scenes', 'scene.schema.json'],
  text: ['texts', 'text.schema.json'],
  vocabulary: ['vocabulary', 'vocabulary.schema.json'],
});
const REGISTRY_NAMES = [...new Set(Object.values(REGISTRY_BY_KIND).map(([name]) => name))].sort();
const OPCODE_KEYS = new Set(['op']);

function hasOwn(value, key) {
  return Object.hasOwn(value, key);
}

function pointerToken(value) {
  return String(value).replaceAll('~', '~0').replaceAll('/', '~1');
}

export function canonicalJson(value) {
  if (value === null || typeof value !== 'object') return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(',')}]`;
  return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonicalJson(value[key])}`).join(',')}}`;
}

function sha256(value) {
  return createHash('sha256').update(typeof value === 'string' ? value : canonicalJson(value)).digest('hex');
}

function deepFreeze(value) {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value;
  for (const child of Object.values(value)) deepFreeze(child);
  return Object.freeze(value);
}

function portableSourcePath(filePath, cwd) {
  const relative = path.relative(cwd, filePath).split(path.sep).join('/');
  if (relative && !relative.startsWith('../')) return relative;
  return path.basename(filePath) || 'content';
}

function diagnostic(code, sourcePath, jsonPointer, message, moduleId = 'urman.compiler') {
  return errorDiagnostic(code, { sourcePath, jsonPointer, message, moduleId });
}

async function readText(filePath, cwd, moduleId = 'urman.compiler') {
  const sourcePath = portableSourcePath(filePath, cwd);
  try {
    return { ok: true, sourcePath, text: await readFile(filePath, 'utf8') };
  } catch (error) {
    return failure(diagnostic(
      DiagnosticCode.MissingFile,
      sourcePath,
      '',
      `Cannot read selected content file: ${sourcePath} (${error.code ?? 'read error'}).`,
      moduleId,
    ));
  }
}

async function readJson(filePath, cwd, moduleId = 'urman.compiler') {
  const loaded = await readText(filePath, cwd, moduleId);
  if (!loaded.ok) return loaded;
  try {
    return { ok: true, value: JSON.parse(loaded.text), sourcePath: loaded.sourcePath, normalizedSource: canonicalJson(JSON.parse(loaded.text)) };
  } catch (error) {
    return failure(diagnostic(
      DiagnosticCode.InvalidJson,
      loaded.sourcePath,
      '',
      `Invalid JSON in selected content file: ${loaded.sourcePath} (${error.message}).`,
      moduleId,
    ));
  }
}

function parseScalar(raw) {
  const value = raw.trim();
  if (value === '') return '';
  if (value === 'null') return null;
  if (value === 'true') return true;
  if (value === 'false') return false;
  if (/^-?(?:0|[1-9]\d*)(?:\.\d+)?$/.test(value)) return Number(value);
  if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith('[') && value.endsWith(']')) || (value.startsWith('{') && value.endsWith('}'))) {
    return JSON.parse(value);
  }
  return value.replace(/^'(.*)'$/, '$1');
}

function parseFrontmatter(text) {
  const normalized = text.replaceAll('\r\n', '\n');
  if (!normalized.startsWith('---\n')) throw new Error('Markdown source must start with YAML frontmatter.');
  const end = normalized.indexOf('\n---\n', 4);
  if (end < 0) throw new Error('Markdown frontmatter closing delimiter is missing.');
  const header = normalized.slice(4, end);
  const data = {};
  for (const [index, line] of header.split('\n').entries()) {
    if (!line.trim() || line.trimStart().startsWith('#')) continue;
    const match = /^([A-Za-z][A-Za-z0-9_-]*):\s*(.*)$/.exec(line);
    if (!match) throw new Error(`Unsupported frontmatter syntax on line ${index + 1}. Use flat keys and JSON arrays/objects.`);
    data[match[1]] = parseScalar(match[2]);
  }
  return { data, body: normalized.slice(end + 5) };
}

async function readDefinitionFile(filePath, cwd, moduleId) {
  if (!filePath.endsWith('.md')) return readJson(filePath, cwd, moduleId);
  const loaded = await readText(filePath, cwd, moduleId);
  if (!loaded.ok) return loaded;
  try {
    const parsed = parseFrontmatter(loaded.text);
    const bodyMarkdown = parsed.body;
    if (!bodyMarkdown.trim()) throw new Error('Markdown body must be non-empty.');
    const definition = { ...parsed.data, bodyMarkdown };
    return {
      ok: true,
      value: definition,
      sourcePath: loaded.sourcePath,
      normalizedSource: canonicalJson(definition),
    };
  } catch (error) {
    return failure(diagnostic(DiagnosticCode.InvalidFrontmatter, loaded.sourcePath, '', error.message, moduleId));
  }
}

async function schemaErrors(schemaFile, value) {
  const result = await validateDocument(schemaFile, value);
  return result.valid ? [] : result.errors;
}

function resolveSchemaPointer(document, fragment) {
  if (!fragment) return document;
  return fragment.replace(/^\//, '').split('/').reduce((current, rawToken) => {
    const token = decodeURIComponent(rawToken).replaceAll('~1', '/').replaceAll('~0', '~');
    return current?.[token];
  }, document);
}

function resolveSchemaNode(schema, currentFile, registry) {
  if (!schema || typeof schema !== 'object' || !schema.$ref) return { schema, file: currentFile, isContentId: false };
  const [filePart, fragment = ''] = schema.$ref.split('#');
  const targetFile = filePart ? path.resolve(path.dirname(currentFile), filePart) : currentFile;
  const targetDocument = registry.get(targetFile);
  const target = resolveSchemaPointer(targetDocument, fragment);
  const isContentId = targetFile === path.join(SCHEMA_DIR, 'common.schema.json') && fragment === '/$defs/ContentId';
  return isContentId ? { schema: target, file: targetFile, isContentId: true } : resolveSchemaNode(target, targetFile, registry);
}

function isContentDeclaration(originSchemaFile, key, pointer) {
  if (key === 'id' && pointer === '/id') return true;
  if (path.basename(originSchemaFile) !== 'capability.schema.json') return false;
  return pointer === '/protocolId' || /^\/resourceClaims\/\d+\/resourceId$/.test(pointer);
}

function collectSchemaContentIds(value, schema, currentFile, registry, pointer = '', key = '', originSchemaFile = currentFile) {
  const resolved = resolveSchemaNode(schema, currentFile, registry);
  if (resolved.isContentId) {
    return typeof value === 'string' ? [{ id: value, pointer: pointer || '/', key, declaration: isContentDeclaration(originSchemaFile, key, pointer) }] : [];
  }
  const node = resolved.schema;
  if (!node || typeof node !== 'object') return [];
  const found = [];
  for (const branches of [node.oneOf, node.anyOf, node.allOf]) {
    if (branches) for (const branch of branches) found.push(...collectSchemaContentIds(value, branch, resolved.file, registry, pointer, key, originSchemaFile));
  }
  if (Array.isArray(value) && node.items) {
    value.forEach((item, index) => found.push(...collectSchemaContentIds(item, node.items, resolved.file, registry, `${pointer}/${index}`, key, originSchemaFile)));
  } else if (value && typeof value === 'object' && !Array.isArray(value)) {
    for (const [childKey, child] of Object.entries(value)) {
      if (node.properties?.[childKey]) {
        found.push(...collectSchemaContentIds(child, node.properties[childKey], resolved.file, registry, `${pointer}/${pointerToken(childKey)}`, childKey, originSchemaFile));
      } else if (node.additionalProperties && typeof node.additionalProperties === 'object') {
        found.push(...collectSchemaContentIds(child, node.additionalProperties, resolved.file, registry, `${pointer}/${pointerToken(childKey)}`, childKey, originSchemaFile));
      }
    }
  }
  return [...new Map(found.map((entry) => [`${entry.pointer}\0${entry.id}\0${entry.declaration}`, entry])).values()];
}

function kindFromId(id) {
  const separator = typeof id === 'string' ? id.indexOf(':') : -1;
  const slash = typeof id === 'string' ? id.indexOf('/', separator + 1) : -1;
  return separator >= 0 && slash > separator ? id.slice(separator + 1, slash) : null;
}

function moduleFromId(id) {
  return typeof id === 'string' && id.includes(':') ? id.slice(0, id.indexOf(':')) : null;
}

function interactionTargetCollisionPointers(definition) {
  if (kindFromId(definition?.id) !== 'scene' || !Array.isArray(definition.interactions)) return [];
  return definition.interactions.flatMap((interaction, index) => (
    interaction && typeof interaction === 'object'
      && hasOwn(interaction, 'targetSceneId')
      && hasOwn(interaction, 'targetDialogueId')
      ? [`/interactions/${index}`]
      : []
  ));
}

function collisionDiagnostics(definition, sourcePath, pointer, moduleId) {
  return interactionTargetCollisionPointers(definition).map((interactionPointer) => diagnostic(
    DiagnosticCode.ConflictingInteractionTarget,
    sourcePath,
    `${pointer}${interactionPointer}`,
    'A scene interaction may target either one scene or one dialogue, never both.',
    moduleId,
  ));
}

function collectOpcodes(value, pointer = '') {
  const opcodes = [];
  if (Array.isArray(value)) {
    value.forEach((child, index) => opcodes.push(...collectOpcodes(child, `${pointer}/${index}`)));
  } else if (value && typeof value === 'object') {
    for (const [key, child] of Object.entries(value)) {
      const childPointer = `${pointer}/${pointerToken(key)}`;
      if (OPCODE_KEYS.has(key) && typeof child === 'string') opcodes.push({ op: child, pointer: childPointer });
      opcodes.push(...collectOpcodes(child, childPointer));
    }
  }
  return opcodes;
}

function dependencyCycle(modules) {
  const graph = new Map(modules.map((module) => [module.manifest.moduleId, module.manifest.dependencies.map(({ moduleId }) => moduleId)]));
  const visiting = new Set();
  const visited = new Set();
  const stack = [];
  function visit(id) {
    if (visiting.has(id)) return [...stack.slice(stack.indexOf(id)), id];
    if (visited.has(id)) return null;
    visiting.add(id);
    stack.push(id);
    for (const dependency of graph.get(id) ?? []) {
      const cycle = visit(dependency);
      if (cycle) return cycle;
    }
    stack.pop();
    visiting.delete(id);
    visited.add(id);
    return null;
  }
  for (const id of graph.keys()) {
    const cycle = visit(id);
    if (cycle) return cycle;
  }
  return null;
}

function sceneReachability(entrypoint, scenes) {
  const byId = new Map(scenes.map((scene) => [scene.id, scene]));
  const reached = new Set();
  const queue = [entrypoint];
  while (queue.length) {
    const id = queue.shift();
    if (reached.has(id) || !byId.has(id)) continue;
    reached.add(id);
    for (const interaction of byId.get(id).interactions ?? []) {
      if (interaction.targetSceneId) queue.push(interaction.targetSceneId);
      if (interaction.targetDialogueId) reached.add(interaction.targetDialogueId);
    }
    const visitEffects = (value) => {
      if (Array.isArray(value)) value.forEach(visitEffects);
      else if (value && typeof value === 'object') {
        if (value.op === 'scene.request' && typeof value.sceneId === 'string') queue.push(value.sceneId);
        Object.values(value).forEach(visitEffects);
      }
    };
    visitEffects(byId.get(id).onEnter ?? []);
    visitEffects(byId.get(id).onExit ?? []);
    for (const interaction of byId.get(id).interactions ?? []) visitEffects(interaction.effects ?? []);
  }
  return reached;
}

function applyTransitionOverrides(campaign, registries, diagnostics, sourcePath) {
  const syntheticInteractionPointers = new Set();
  if (!Array.isArray(campaign.transitionOverrides) || campaign.transitionOverrides.length === 0) return syntheticInteractionPointers;

  const scenes = registries.get('scenes');
  const texts = registries.get('texts');
  const dialogues = registries.get('dialogues');
  const documents = registries.get('documents');
  const interactionIds = new Set(
    [...scenes.values()].flatMap((scene) => (scene.interactions ?? []).map((interaction) => interaction.id)),
  );

  for (const [index, override] of campaign.transitionOverrides.entries()) {
    const pointer = `/transitionOverrides/${index}`;
    if (!override || typeof override !== 'object') {
      diagnostics.push(diagnostic(DiagnosticCode.InvalidSchema, sourcePath, pointer, 'Transition override must be an object.'));
      continue;
    }

    const sourceScene = scenes.get(override.sourceSceneId);
    if (!sourceScene) {
      diagnostics.push(diagnostic(DiagnosticCode.UnresolvedReference, sourcePath, `${pointer}/sourceSceneId`, `Transition source scene ${override.sourceSceneId} is missing.`));
      continue;
    }
    if (kindFromId(override.id) !== 'interaction' || interactionIds.has(override.id)) {
      diagnostics.push(diagnostic(DiagnosticCode.DuplicateId, sourcePath, `${pointer}/id`, `Transition interaction ${override.id} is missing, invalid or already declared.`));
      continue;
    }
    if (!texts.has(override.labelTextId)) {
      diagnostics.push(diagnostic(DiagnosticCode.UnresolvedReference, sourcePath, `${pointer}/labelTextId`, `Transition label ${override.labelTextId} is missing.`));
      continue;
    }

    const targetFields = ['targetSceneId', 'targetDialogueId', 'targetDocumentId']
      .filter((field) => override[field] !== undefined);
    if (targetFields.length !== 1) {
      diagnostics.push(diagnostic(DiagnosticCode.InvalidSchema, sourcePath, pointer, 'Transition override must declare exactly one target field.'));
      continue;
    }
    const targetRegistry = {
      targetSceneId: scenes,
      targetDialogueId: dialogues,
      targetDocumentId: documents,
    }[targetFields[0]];
    const targetId = override[targetFields[0]];
    if (!targetRegistry.has(targetId)) {
      diagnostics.push(diagnostic(DiagnosticCode.UnresolvedReference, sourcePath, `${pointer}/${targetFields[0]}`, `Transition target ${targetId} is missing.`));
      continue;
    }

    const interactionIndex = sourceScene.interactions?.length ?? 0;
    syntheticInteractionPointers.add(`${sourceScene.id}\0/interactions/${interactionIndex}`);
    sourceScene.interactions = [...(sourceScene.interactions ?? []), {
      id: override.id,
      labelTextId: override.labelTextId,
      conditions: [...(override.conditions ?? [])],
      effects: [...(override.effects ?? [])],
      [targetFields[0]]: targetId,
    }];
    interactionIds.add(override.id);
  }
  return syntheticInteractionPointers;
}

function orderedRegistry(registry) {
  return [...registry.values()].sort((left, right) => left.id.localeCompare(right.id));
}

export async function compileContent(options = {}) {
  const cwd = path.resolve(options.cwd ?? process.cwd());
  if (typeof options.campaignPath !== 'string' || options.campaignPath.length === 0) {
    return failure(diagnostic(
      DiagnosticCode.MissingCampaignSelection,
      'content',
      '/campaignPath',
      'An explicit campaignPath is required to compile a content pack.',
    ));
  }
  const manifestPaths = Array.isArray(options.moduleManifestPaths) ? options.moduleManifestPaths : [];
  if (manifestPaths.length === 0) {
    return failure(diagnostic(
      DiagnosticCode.MissingModuleSelection,
      'content',
      '/moduleManifestPaths',
      'At least one explicit moduleManifestPath is required to compile a content pack.',
    ));
  }
  const schemaRegistry = await loadSchemaRegistry(SCHEMA_DIR);
  const campaignPath = path.resolve(cwd, options.campaignPath);
  const campaignResult = await readJson(campaignPath, cwd);
  if (!campaignResult.ok) return campaignResult;
  const diagnostics = [];
  for (const message of await schemaErrors('campaign-manifest.schema.json', campaignResult.value)) {
    diagnostics.push(diagnostic(DiagnosticCode.InvalidSchema, campaignResult.sourcePath, message.split(':')[0], message));
  }
  const modules = [];
  const seenModules = new Map();
  for (const selectedPath of manifestPaths) {
    const absolute = path.resolve(cwd, selectedPath);
    const loaded = await readJson(absolute, cwd);
    if (!loaded.ok) diagnostics.push(...loaded.diagnostics);
    else {
      for (const message of await schemaErrors('module-manifest.schema.json', loaded.value)) {
        diagnostics.push(diagnostic(DiagnosticCode.InvalidSchema, loaded.sourcePath, message.split(':')[0], message, loaded.value.moduleId ?? 'urman.compiler'));
      }
      if (seenModules.has(loaded.value.moduleId)) {
        diagnostics.push(diagnostic(DiagnosticCode.DuplicateModule, loaded.sourcePath, '/moduleId', `Duplicate module ${loaded.value.moduleId}.`, loaded.value.moduleId));
      } else {
        seenModules.set(loaded.value.moduleId, loaded.sourcePath);
        modules.push({ manifest: loaded.value, manifestPath: absolute, sourcePath: loaded.sourcePath, normalizedManifest: loaded.normalizedSource });
      }
    }
  }
  if (diagnostics.length) return failure(...diagnostics);

  const campaign = campaignResult.value;
  const moduleById = new Map(modules.map((module) => [module.manifest.moduleId, module]));
  const campaignModuleIds = new Set(campaign.modules.map(({ moduleId }) => moduleId));
  for (const module of modules) {
    if (!campaignModuleIds.has(module.manifest.moduleId)) {
      diagnostics.push(diagnostic(DiagnosticCode.UndeclaredDependency, module.sourcePath, '/moduleId', `Module ${module.manifest.moduleId} is not part of campaign ${campaign.id}.`, module.manifest.moduleId));
    }
  }
  for (const [index, dependency] of campaign.modules.entries()) {
    const selected = moduleById.get(dependency.moduleId);
    if (!selected) diagnostics.push(diagnostic(DiagnosticCode.MissingModule, campaignResult.sourcePath, `/modules/${index}`, `Campaign module ${dependency.moduleId} was not supplied.`));
    else if (selected.manifest.exactVersion !== dependency.exactVersion) diagnostics.push(diagnostic(DiagnosticCode.IncompatibleExactVersion, campaignResult.sourcePath, `/modules/${index}/exactVersion`, `Campaign requires ${dependency.moduleId}@${dependency.exactVersion}, supplied ${selected.manifest.exactVersion}.`, dependency.moduleId));
  }
  for (const module of modules) {
    for (const [index, dependency] of module.manifest.dependencies.entries()) {
      const selected = moduleById.get(dependency.moduleId);
      if (!selected) diagnostics.push(diagnostic(DiagnosticCode.MissingModule, module.sourcePath, `/dependencies/${index}`, `Dependency ${dependency.moduleId} was not supplied.`, module.manifest.moduleId));
      else if (selected.manifest.exactVersion !== dependency.exactVersion) diagnostics.push(diagnostic(DiagnosticCode.IncompatibleExactVersion, module.sourcePath, `/dependencies/${index}/exactVersion`, `Dependency requires ${dependency.moduleId}@${dependency.exactVersion}, supplied ${selected.manifest.exactVersion}.`, module.manifest.moduleId));
    }
  }
  const cycle = dependencyCycle(modules);
  if (cycle) diagnostics.push(diagnostic(DiagnosticCode.DependencyCycle, campaignResult.sourcePath, '/modules', `Module dependency cycle: ${cycle.join(' -> ')}.`));
  if (diagnostics.length) return failure(...diagnostics);

  const registries = new Map(REGISTRY_NAMES.map((name) => [name, new Map()]));
  const definitionMeta = new Map();
  const moduleFingerprintInputs = [];
  for (const module of modules) {
    const sourceInputs = [];
    for (const [index, sourceFile] of module.manifest.sourceFiles.entries()) {
      const absolute = path.resolve(path.dirname(module.manifestPath), sourceFile);
      const loaded = await readDefinitionFile(absolute, cwd, module.manifest.moduleId);
      if (!loaded.ok) {
        diagnostics.push(...loaded.diagnostics);
        continue;
      }
      sourceInputs.push({ sourceFile, normalized: loaded.normalizedSource });
      const definitions = Array.isArray(loaded.value) ? loaded.value : [loaded.value];
      for (const [definitionIndex, definition] of definitions.entries()) {
        const pointer = Array.isArray(loaded.value) ? `/${definitionIndex}` : '';
        const kind = kindFromId(definition?.id);
        const registryContract = REGISTRY_BY_KIND[kind];
        if (!registryContract) {
          diagnostics.push(diagnostic(DiagnosticCode.InvalidSchema, loaded.sourcePath, `${pointer}/id`, `Unsupported content kind in ID ${definition?.id ?? '<missing>'}.`, module.manifest.moduleId));
          continue;
        }
        const [registryName, schemaFile] = registryContract;
        const collisionPointers = new Set(interactionTargetCollisionPointers(definition));
        for (const message of await schemaErrors(schemaFile, definition)) {
          const schemaPointer = message.split(':')[0];
          if (collisionPointers.has(schemaPointer) && message.endsWith('value matches forbidden schema')) continue;
          diagnostics.push(diagnostic(message.includes('/op') ? DiagnosticCode.UnknownOpcode : DiagnosticCode.InvalidSchema, loaded.sourcePath, `${pointer}${message.split(':')[0] === '/' ? '' : message.split(':')[0]}`, message, module.manifest.moduleId));
        }
        diagnostics.push(...collisionDiagnostics(definition, loaded.sourcePath, pointer, module.manifest.moduleId));
        if (moduleFromId(definition.id) !== module.manifest.moduleId) {
          diagnostics.push(diagnostic(DiagnosticCode.UndeclaredDependency, loaded.sourcePath, `${pointer}/id`, `Definition ${definition.id} is owned by another module.`, module.manifest.moduleId));
        }
        if (definitionMeta.has(definition.id)) {
          diagnostics.push(diagnostic(DiagnosticCode.DuplicateId, loaded.sourcePath, `${pointer}/id`, `Duplicate content ID ${definition.id}.`, module.manifest.moduleId));
        } else {
          definitionMeta.set(definition.id, {
            moduleId: module.manifest.moduleId,
            sourcePath: loaded.sourcePath,
            pointer,
            definition,
            schemaFile: path.join(SCHEMA_DIR, schemaFile),
          });
          registries.get(registryName).set(definition.id, definition);
        }
      }
    }
    moduleFingerprintInputs.push({ moduleId: module.manifest.moduleId, exactVersion: module.manifest.exactVersion, manifest: module.manifest, sources: sourceInputs.sort((a, b) => a.sourceFile.localeCompare(b.sourceFile)) });
  }
  if (diagnostics.length) return failure(...diagnostics);

  const syntheticInteractionPointers = applyTransitionOverrides(campaignResult.value, registries, diagnostics, campaignResult.sourcePath);
  if (diagnostics.length) return failure(...diagnostics);

  const allDefinitions = new Set(definitionMeta.keys());
  const declaredIds = new Set(allDefinitions);
  const schemaContentIds = new Map();
  for (const [id, meta] of definitionMeta) {
    const entries = collectSchemaContentIds(meta.definition, schemaRegistry.get(meta.schemaFile), meta.schemaFile, schemaRegistry);
    schemaContentIds.set(id, entries);
    for (const entry of entries) if (entry.declaration) declaredIds.add(entry.id);
  }
  for (const [id, meta] of definitionMeta) {
    const owner = moduleById.get(meta.moduleId).manifest;
    const allowedModules = new Set([meta.moduleId, ...owner.dependencies.map(({ moduleId }) => moduleId)]);
    for (const reference of schemaContentIds.get(id).filter((entry) => !entry.declaration)) {
      const syntheticInteractionPrefix = `${id}\0${reference.pointer.slice(0, reference.pointer.lastIndexOf('/'))}`;
      if (syntheticInteractionPointers.has(syntheticInteractionPrefix)) continue;
      const targetModule = moduleFromId(reference.id);
      if (!allowedModules.has(targetModule)) diagnostics.push(diagnostic(DiagnosticCode.UndeclaredDependency, meta.sourcePath, `${meta.pointer}${reference.pointer}`, `${id} references ${reference.id} without a declared module dependency.`, meta.moduleId));
      else if (reference.key === 'targetDialogueId' && !registries.get('dialogues').has(reference.id)) diagnostics.push(diagnostic(DiagnosticCode.UnknownDialogueTarget, meta.sourcePath, `${meta.pointer}${reference.pointer}`, `Scene interaction targets unknown dialogue ${reference.id}.`, meta.moduleId));
      else if (REGISTRY_BY_KIND[kindFromId(reference.id)] && !declaredIds.has(reference.id)) diagnostics.push(diagnostic(kindFromId(reference.id) === 'asset' ? DiagnosticCode.MissingAsset : DiagnosticCode.UnresolvedReference, meta.sourcePath, `${meta.pointer}${reference.pointer}`, `Unresolved content reference ${reference.id}.`, meta.moduleId));
    }
    for (const opcode of collectOpcodes(meta.definition)) {
      if (typeof opcode.op !== 'string') diagnostics.push(diagnostic(DiagnosticCode.UnknownOpcode, meta.sourcePath, `${meta.pointer}${opcode.pointer}`, `Unknown opcode ${opcode.op}.`, meta.moduleId));
    }
  }
  for (const module of modules) {
    for (const [index, id] of module.manifest.provides.entries()) {
      if (!allDefinitions.has(id)) diagnostics.push(diagnostic(DiagnosticCode.UnresolvedReference, module.sourcePath, `/provides/${index}`, `Provided ID ${id} has no definition.`, module.manifest.moduleId));
    }
    for (const [index, assetId] of module.manifest.assets.entries()) {
      if (!registries.get('assets').has(assetId)) diagnostics.push(diagnostic(DiagnosticCode.MissingAsset, module.sourcePath, `/assets/${index}`, `Asset definition ${assetId} is missing.`, module.manifest.moduleId));
    }
  }
  for (const [role, id] of Object.entries(campaign.roleBindings)) {
    if (!registries.get('characters').has(id)) diagnostics.push(diagnostic(DiagnosticCode.MissingRoleBinding, campaignResult.sourcePath, `/roleBindings/${pointerToken(role)}`, `Role ${role} references missing character ${id}.`));
  }
  for (const dialogue of orderedRegistry(registries.get('dialogues'))) {
    const meta = definitionMeta.get(dialogue.id);
    for (const [index, role] of dialogue.participantRoles.entries()) {
      if (!hasOwn(campaign.roleBindings, role)) diagnostics.push(diagnostic(DiagnosticCode.MissingRoleBinding, meta.sourcePath, `${meta.pointer}/participantRoles/${index}`, `Dialogue ${dialogue.id} requires unbound role ${role}.`, meta.moduleId));
    }
    for (const [index, node] of dialogue.nodes.entries()) {
      if (!hasOwn(campaign.roleBindings, node.speakerRole)) diagnostics.push(diagnostic(DiagnosticCode.MissingRoleBinding, meta.sourcePath, `${meta.pointer}/nodes/${index}/speakerRole`, `Dialogue ${dialogue.id} uses unbound speaker role ${node.speakerRole}.`, meta.moduleId));
    }
  }
  if (!registries.get('scenes').has(campaign.entrypoint)) diagnostics.push(diagnostic(DiagnosticCode.MissingEntrypoint, campaignResult.sourcePath, '/entrypoint', `Campaign entrypoint ${campaign.entrypoint} is missing.`));
  const providersByProtocol = new Map();
  for (const provider of orderedRegistry(registries.get('capabilities'))) {
    const providers = providersByProtocol.get(provider.protocolId) ?? [];
    providers.push(provider);
    providersByProtocol.set(provider.protocolId, providers);
  }
  for (const [protocolId, providers] of providersByProtocol) {
    if (providers.length > 1) {
      const meta = definitionMeta.get(providers[1].id);
      diagnostics.push(diagnostic(DiagnosticCode.DuplicateCapabilityProvider, meta.sourcePath, `${meta.pointer}/protocolId`, `Multiple providers declare protocol ${protocolId}.`, moduleFromId(providers[1].id)));
    }
  }
  const requirements = [
    ...campaign.capabilityRequirements.map((requirement, index) => ({ requirement, sourcePath: campaignResult.sourcePath, pointer: `/capabilityRequirements/${index}`, moduleId: 'urman.compiler' })),
    ...modules.flatMap(({ manifest, sourcePath }) => manifest.requires.map((requirement, index) => ({ requirement, sourcePath, pointer: `/requires/${index}`, moduleId: manifest.moduleId }))),
    ...orderedRegistry(registries.get('quests')).flatMap((quest) => {
      const meta = definitionMeta.get(quest.id);
      return quest.stages.flatMap((stage, stageIndex) => stage.objectives.flatMap((objective, objectiveIndex) => objective.capability ? [{
        requirement: { protocolId: objective.capability.protocolId, exactVersion: objective.capability.exactVersion },
        sourcePath: meta.sourcePath,
        pointer: `${meta.pointer}/stages/${stageIndex}/objectives/${objectiveIndex}/capability`,
        moduleId: meta.moduleId,
      }] : []));
    }),
  ];
  const resolvedCapabilityRequirements = [...new Map(requirements.map(({ requirement }) => [
    `${requirement.protocolId}@${requirement.exactVersion}`,
    { protocolId: requirement.protocolId, exactVersion: requirement.exactVersion },
  ])).values()].sort((left, right) =>
    left.protocolId.localeCompare(right.protocolId) || left.exactVersion.localeCompare(right.exactVersion));
  const selectedProviders = new Map();
  for (const { requirement, sourcePath, pointer, moduleId } of requirements) {
    const providers = providersByProtocol.get(requirement.protocolId) ?? [];
    if (providers.length === 0) diagnostics.push(diagnostic(DiagnosticCode.MissingCapability, sourcePath, pointer, `Capability ${requirement.protocolId} is missing.`, moduleId));
    else if (providers.length === 1 && providers[0].exactVersion !== requirement.exactVersion) diagnostics.push(diagnostic(DiagnosticCode.IncompatibleExactVersion, sourcePath, `${pointer}/exactVersion`, `Capability ${requirement.protocolId} requires ${requirement.exactVersion}, found ${providers[0].exactVersion}.`, moduleId));
    else if (providers.length === 1) selectedProviders.set(requirement.protocolId, providers[0]);
  }
  const claimsByResource = new Map();
  for (const provider of selectedProviders.values()) {
    for (const claim of provider.resourceClaims) {
      const claims = claimsByResource.get(claim.resourceId) ?? [];
      claims.push({ provider, claim });
      claimsByResource.set(claim.resourceId, claims);
    }
  }
  for (const [resourceId, claims] of claimsByResource) {
    if (claims.length > 1 && claims.some(({ claim }) => claim.mode === 'exclusive')) {
      const meta = definitionMeta.get(claims[1].provider.id);
      diagnostics.push(diagnostic(DiagnosticCode.ResourceClaimConflict, meta.sourcePath, `${meta.pointer}/resourceClaims`, `Selected capability providers conflict on ${resourceId}.`, meta.moduleId));
    }
  }
  const reached = sceneReachability(campaign.entrypoint, orderedRegistry(registries.get('scenes')));
  const narrativeIndex = new Map(campaign.narrativeOrder.map((id, index) => [id, index]));
  for (const [index, orderedId] of campaign.narrativeOrder.entries()) {
    if (REGISTRY_BY_KIND[kindFromId(orderedId)] && !declaredIds.has(orderedId)) diagnostics.push(diagnostic(DiagnosticCode.NarrativeInvariantViolation, campaignResult.sourcePath, `/narrativeOrder/${index}`, `Narrative order references missing content ${orderedId}.`));
  }
  for (const [index, invariant] of campaign.invariants.entries()) {
    const pointer = `/invariants/${index}`;
    if (invariant.kind === 'required-reachable') {
      if (!declaredIds.has(invariant.targetId)) diagnostics.push(diagnostic(DiagnosticCode.NarrativeInvariantViolation, campaignResult.sourcePath, `${pointer}/targetId`, `Required target ${invariant.targetId} is missing.`));
      else if (['scene', 'dialogue'].includes(kindFromId(invariant.targetId)) && !reached.has(invariant.targetId)) diagnostics.push(diagnostic(DiagnosticCode.UnreachableRequiredEntry, campaignResult.sourcePath, `${pointer}/targetId`, `Required content ${invariant.targetId} is unreachable from the entrypoint.`));
    } else if (invariant.kind === 'reveal-not-before') {
      const subjectIndex = narrativeIndex.get(invariant.subjectId);
      const afterIndex = narrativeIndex.get(invariant.afterId);
      if (subjectIndex === undefined || afterIndex === undefined || subjectIndex <= afterIndex) diagnostics.push(diagnostic(DiagnosticCode.NarrativeInvariantViolation, campaignResult.sourcePath, pointer, `${invariant.subjectId} must occur strictly after ${invariant.afterId} in narrativeOrder.`));
    }
  }
  for (const asset of orderedRegistry(registries.get('assets'))) {
    const meta = definitionMeta.get(asset.id);
    for (const file of [asset.file, ...(asset.variants ?? []).map((variant) => variant.file)]) {
      const absolute = path.resolve(path.dirname(moduleById.get(meta.moduleId).manifestPath), file);
      try { await access(absolute); } catch { diagnostics.push(diagnostic(DiagnosticCode.MissingAsset, meta.sourcePath, `${meta.pointer}/file`, `Asset file ${file} is missing.`, meta.moduleId)); }
    }
  }
  if (diagnostics.length) return failure(...diagnostics);

  const orderedModules = campaign.modules.map((dependency) => ({ ...dependency }));
  const moduleFingerprints = moduleFingerprintInputs
    .map((input) => ({ moduleId: input.moduleId, exactVersion: input.exactVersion, sha256: sha256(input) }))
    .sort((left, right) => left.moduleId.localeCompare(right.moduleId));
  const pack = {
    schemaVersion: 1,
    packVersion: '1.0.0',
    campaign: {
      id: campaign.id,
      exactVersion: campaign.exactVersion,
      entrypoint: campaign.entrypoint,
      orderedModules,
      roleBindings: Object.fromEntries(Object.entries(campaign.roleBindings).sort(([left], [right]) => left.localeCompare(right))),
      capabilityRequirements: resolvedCapabilityRequirements,
      narrativeOrder: [...campaign.narrativeOrder],
      invariants: [...campaign.invariants].sort((left, right) => left.id.localeCompare(right.id)),
      ...(Array.isArray(campaign.transitionOverrides) && campaign.transitionOverrides.length > 0
        ? { transitionOverrides: [...campaign.transitionOverrides].sort((left, right) => `${left.sourceSceneId}:${left.id}`.localeCompare(`${right.sourceSceneId}:${right.id}`)) }
        : {}),
    },
    registries: Object.fromEntries(REGISTRY_NAMES.map((name) => [name, orderedRegistry(registries.get(name))])),
    dependencyGraph: {
      nodes: modules.map(({ manifest }) => manifest.moduleId).sort(),
      edges: modules.flatMap(({ manifest }) => manifest.dependencies.map(({ moduleId }) => ({ from: manifest.moduleId, to: moduleId })))
        .sort((left, right) => `${left.from}:${left.to}`.localeCompare(`${right.from}:${right.to}`)),
    },
    moduleFingerprints,
    campaignFingerprint: '',
  };
  pack.campaignFingerprint = sha256({ campaign: pack.campaign, moduleFingerprints });
  const packSchemaErrors = await schemaErrors('compiled-content-pack.schema.json', pack);
  if (packSchemaErrors.length) {
    return failure(...packSchemaErrors.map((message) => diagnostic(DiagnosticCode.InvalidSchema, campaignResult.sourcePath, message.split(':')[0], `Compiled pack violates its wire schema: ${message}`)));
  }
  return success(deepFreeze(pack));
}

async function findNamedManifests(root, filename) {
  const found = [];
  async function walk(directory) {
    let entries;
    try { entries = await readdir(directory, { withFileTypes: true }); } catch (error) {
      if (error.code === 'ENOENT') return;
      throw error;
    }
    for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name))) {
      const target = path.join(directory, entry.name);
      if (entry.isDirectory()) await walk(target);
      else if (entry.isFile() && entry.name === filename) found.push(target);
    }
  }
  await walk(root);
  return found;
}

export async function discoverWorkspaceManifests(cwd = process.cwd()) {
  const root = path.resolve(cwd);
  const [modulePaths, campaignPaths] = await Promise.all([
    findNamedManifests(path.join(root, 'content', 'modules'), 'module.json'),
    findNamedManifests(path.join(root, 'content', 'campaigns'), 'campaign.json'),
  ]);
  return { modulePaths: modulePaths.sort(), campaignPaths: campaignPaths.sort() };
}

export async function resolveWorkspaceModuleSelection(selection, cwd = process.cwd()) {
  const root = path.resolve(cwd);
  if (typeof selection !== 'string' || selection.length === 0) {
    return failure(diagnostic(DiagnosticCode.MissingModuleSelection, 'content/modules', '/module', 'An explicit module ID or path is required.'));
  }
  if (selection.includes('/') || selection.endsWith('.json')) return Object.freeze({ ok: true, modulePath: path.resolve(root, selection), diagnostics: Object.freeze([]) });
  const discovered = await discoverWorkspaceManifests(root);
  for (const modulePath of discovered.modulePaths) {
    const loaded = await readJson(modulePath, root);
    if (loaded.ok && loaded.value.moduleId === selection) return Object.freeze({ ok: true, modulePath, diagnostics: Object.freeze([]) });
  }
  return failure(diagnostic(DiagnosticCode.UnknownModuleSelection, 'content/modules', '/module', `Unknown module selection ${selection}.`));
}

async function auditModule(modulePath, cwd, schemaRegistry) {
  const loaded = await readJson(modulePath, cwd);
  if (!loaded.ok) return { manifest: null, diagnostics: [...loaded.diagnostics], definitions: [] };
  const manifest = loaded.value;
  const diagnostics = [];
  for (const message of await schemaErrors('module-manifest.schema.json', manifest)) {
    diagnostics.push(diagnostic(DiagnosticCode.InvalidSchema, loaded.sourcePath, message.split(':')[0], message, manifest.moduleId ?? 'urman.compiler'));
  }
  if (diagnostics.length) return { manifest, diagnostics, definitions: [] };
  const ids = new Set();
  const definitionsFound = [];
  for (const [index, sourceFile] of manifest.sourceFiles.entries()) {
    const source = await readDefinitionFile(path.resolve(path.dirname(modulePath), sourceFile), cwd, manifest.moduleId);
    if (!source.ok) {
      diagnostics.push(...source.diagnostics);
      continue;
    }
    const definitions = Array.isArray(source.value) ? source.value : [source.value];
    for (const [definitionIndex, definition] of definitions.entries()) {
      const pointer = Array.isArray(source.value) ? `/${definitionIndex}` : '';
      const contract = REGISTRY_BY_KIND[kindFromId(definition?.id)];
      if (!contract) {
        diagnostics.push(diagnostic(DiagnosticCode.InvalidSchema, source.sourcePath, `${pointer}/id`, `Unsupported content kind in ID ${definition?.id ?? '<missing>'}.`, manifest.moduleId));
        continue;
      }
      const collisionPointers = new Set(interactionTargetCollisionPointers(definition));
      for (const message of await schemaErrors(contract[1], definition)) {
        const schemaPointer = message.split(':')[0];
        if (collisionPointers.has(schemaPointer) && message.endsWith('value matches forbidden schema')) continue;
        diagnostics.push(diagnostic(message.includes('/op') ? DiagnosticCode.UnknownOpcode : DiagnosticCode.InvalidSchema, source.sourcePath, `${pointer}${message.split(':')[0]}`, message, manifest.moduleId));
      }
      diagnostics.push(...collisionDiagnostics(definition, source.sourcePath, pointer, manifest.moduleId));
      if (ids.has(definition.id)) diagnostics.push(diagnostic(DiagnosticCode.DuplicateId, source.sourcePath, `${pointer}/id`, `Duplicate content ID ${definition.id}.`, manifest.moduleId));
      if (moduleFromId(definition.id) !== manifest.moduleId) diagnostics.push(diagnostic(DiagnosticCode.UndeclaredDependency, source.sourcePath, `${pointer}/id`, `Definition ${definition.id} is owned by another module.`, manifest.moduleId));
      ids.add(definition.id);
      definitionsFound.push({ definition, pointer, sourcePath: source.sourcePath, schemaFile: path.join(SCHEMA_DIR, contract[1]) });
    }
  }
  const declaredIds = new Set(ids);
  const referencesByDefinition = new Map();
  for (const record of definitionsFound) {
    const entries = collectSchemaContentIds(record.definition, schemaRegistry.get(record.schemaFile), record.schemaFile, schemaRegistry);
    referencesByDefinition.set(record, entries);
    for (const entry of entries) if (entry.declaration) declaredIds.add(entry.id);
  }
  const allowedModules = new Set([manifest.moduleId, ...manifest.dependencies.map(({ moduleId }) => moduleId)]);
  for (const record of definitionsFound) {
    for (const reference of referencesByDefinition.get(record).filter((entry) => !entry.declaration)) {
      const targetModule = moduleFromId(reference.id);
      if (!allowedModules.has(targetModule)) diagnostics.push(diagnostic(DiagnosticCode.UndeclaredDependency, record.sourcePath, `${record.pointer}${reference.pointer}`, `${record.definition.id} references ${reference.id} without a declared module dependency.`, manifest.moduleId));
      else if (reference.key !== 'targetDialogueId' && targetModule === manifest.moduleId && REGISTRY_BY_KIND[kindFromId(reference.id)] && !declaredIds.has(reference.id)) diagnostics.push(diagnostic(kindFromId(reference.id) === 'asset' ? DiagnosticCode.MissingAsset : DiagnosticCode.UnresolvedReference, record.sourcePath, `${record.pointer}${reference.pointer}`, `Unresolved content reference ${reference.id}.`, manifest.moduleId));
    }
  }
  for (const [index, id] of manifest.provides.entries()) {
    if (!ids.has(id)) diagnostics.push(diagnostic(DiagnosticCode.UnresolvedReference, loaded.sourcePath, `/provides/${index}`, `Provided ID ${id} has no definition.`, manifest.moduleId));
  }
  for (const [index, id] of manifest.assets.entries()) {
    if (!ids.has(id) || kindFromId(id) !== 'asset') diagnostics.push(diagnostic(DiagnosticCode.MissingAsset, loaded.sourcePath, `/assets/${index}`, `Asset definition ${id} is missing.`, manifest.moduleId));
  }
  for (const record of definitionsFound.filter(({ definition }) => kindFromId(definition.id) === 'asset')) {
    for (const file of [record.definition.file, ...(record.definition.variants ?? []).map((variant) => variant.file)]) {
      try { await access(path.resolve(path.dirname(modulePath), file)); } catch { diagnostics.push(diagnostic(DiagnosticCode.MissingAsset, record.sourcePath, `${record.pointer}/file`, `Asset file ${file} is missing.`, manifest.moduleId)); }
    }
  }
  return { manifest, diagnostics, definitions: definitionsFound };
}

export async function auditWorkspace({ cwd = process.cwd(), moduleSelection } = {}) {
  const root = path.resolve(cwd);
  const schemaRegistry = await loadSchemaRegistry(SCHEMA_DIR);
  const discovered = await discoverWorkspaceManifests(root);
  const moduleAudits = [];
  for (const modulePath of discovered.modulePaths) moduleAudits.push({ modulePath, ...(await auditModule(modulePath, root, schemaRegistry)) });
  let selectedAudits = moduleAudits;
  if (moduleSelection) {
    const selectionAsPath = path.resolve(root, moduleSelection);
    const byId = moduleAudits.filter(({ manifest }) => manifest?.moduleId === moduleSelection);
    const byPath = moduleAudits.filter(({ modulePath }) => path.resolve(modulePath) === selectionAsPath);
    selectedAudits = byId.length ? byId : byPath;
    if (selectedAudits.length === 0 && (moduleSelection.includes('/') || moduleSelection.endsWith('.json'))) {
      try {
        await access(selectionAsPath);
        selectedAudits = [{ modulePath: selectionAsPath, ...(await auditModule(selectionAsPath, root, schemaRegistry)) }];
      } catch {
        // The stable UnknownModuleSelection below owns this failure.
      }
    }
    if (selectedAudits.length === 0) {
      return failure(diagnostic(DiagnosticCode.UnknownModuleSelection, 'content/modules', '/module', `Unknown module selection ${moduleSelection}.`));
    }
  }
  const diagnostics = [];
  const closure = new Map();
  const includeDependencies = (audit) => {
    if (!audit?.manifest || closure.has(audit.manifest.moduleId)) return;
    closure.set(audit.manifest.moduleId, audit);
    for (const dependency of audit.manifest.dependencies) {
      includeDependencies(moduleAudits.find(({ manifest }) => manifest?.moduleId === dependency.moduleId));
    }
  };
  selectedAudits.forEach(includeDependencies);
  const auditTargets = [...new Set([...selectedAudits, ...closure.values()])];
  diagnostics.push(...auditTargets.flatMap(({ diagnostics: entries }) => entries));
  const seenModules = new Set();
  for (const audit of auditTargets) {
    if (!audit.manifest) continue;
    if (seenModules.has(audit.manifest.moduleId)) diagnostics.push(diagnostic(DiagnosticCode.DuplicateModule, portableSourcePath(audit.modulePath, root), '/moduleId', `Duplicate module ${audit.manifest.moduleId}.`, audit.manifest.moduleId));
    seenModules.add(audit.manifest.moduleId);
  }
  const cycle = dependencyCycle([...closure.values()].map(({ manifest }) => ({ manifest })));
  if (cycle) diagnostics.push(diagnostic(DiagnosticCode.DependencyCycle, 'content/modules', '/dependencies', `Module dependency cycle: ${cycle.join(' -> ')}.`));
  const availableModules = new Map();
  for (const audit of moduleAudits) {
    if (audit.manifest && !availableModules.has(audit.manifest.moduleId)) availableModules.set(audit.manifest.moduleId, audit);
  }
  const globalDeclarations = new Set();
  const globalDefinitionKinds = new Map();
  const topLevelOwners = new Map();
  for (const audit of moduleAudits) {
    for (const record of audit.definitions ?? []) {
      if (topLevelOwners.has(record.definition.id) && auditTargets.includes(audit)) diagnostics.push(diagnostic(DiagnosticCode.DuplicateId, record.sourcePath, `${record.pointer}/id`, `Duplicate content ID ${record.definition.id}.`, audit.manifest.moduleId));
      topLevelOwners.set(record.definition.id, audit.manifest?.moduleId);
      const entries = collectSchemaContentIds(record.definition, schemaRegistry.get(record.schemaFile), record.schemaFile, schemaRegistry);
      globalDeclarations.add(record.definition.id);
      globalDefinitionKinds.set(record.definition.id, kindFromId(record.definition.id));
      for (const entry of entries) if (entry.declaration) globalDeclarations.add(entry.id);
    }
  }
  for (const audit of auditTargets) {
    if (!audit.manifest) continue;
    for (const [index, dependency] of audit.manifest.dependencies.entries()) {
      const target = availableModules.get(dependency.moduleId);
      if (!target) diagnostics.push(diagnostic(DiagnosticCode.MissingModule, portableSourcePath(audit.modulePath, root), `/dependencies/${index}`, `Dependency ${dependency.moduleId} is not present in the workspace.`, audit.manifest.moduleId));
      else if (target.manifest.exactVersion !== dependency.exactVersion) diagnostics.push(diagnostic(DiagnosticCode.IncompatibleExactVersion, portableSourcePath(audit.modulePath, root), `/dependencies/${index}/exactVersion`, `Dependency ${dependency.moduleId} requires ${dependency.exactVersion}, found ${target.manifest.exactVersion}.`, audit.manifest.moduleId));
    }
    for (const record of audit.definitions ?? []) {
      const references = collectSchemaContentIds(record.definition, schemaRegistry.get(record.schemaFile), record.schemaFile, schemaRegistry)
        .filter((entry) => !entry.declaration);
      for (const reference of references) {
        if (reference.key === 'targetDialogueId' && globalDefinitionKinds.get(reference.id) !== 'dialogue') diagnostics.push(diagnostic(DiagnosticCode.UnknownDialogueTarget, record.sourcePath, `${record.pointer}${reference.pointer}`, `Scene interaction targets unknown dialogue ${reference.id}.`, audit.manifest.moduleId));
        else if (REGISTRY_BY_KIND[kindFromId(reference.id)] && !globalDeclarations.has(reference.id)) diagnostics.push(diagnostic(kindFromId(reference.id) === 'asset' ? DiagnosticCode.MissingAsset : DiagnosticCode.UnresolvedReference, record.sourcePath, `${record.pointer}${reference.pointer}`, `Unresolved content reference ${reference.id}.`, audit.manifest.moduleId));
      }
    }
  }
  if (!moduleSelection) {
    for (const campaignPath of discovered.campaignPaths) {
      const campaignLoaded = await readJson(campaignPath, root);
      if (!campaignLoaded.ok) {
        diagnostics.push(...campaignLoaded.diagnostics);
        continue;
      }
      const campaignSchemaErrors = await schemaErrors('campaign-manifest.schema.json', campaignLoaded.value);
      if (campaignSchemaErrors.length) {
        diagnostics.push(...campaignSchemaErrors.map((message) => diagnostic(DiagnosticCode.InvalidSchema, campaignLoaded.sourcePath, message.split(':')[0], message)));
        continue;
      }
      const ids = new Set((campaignLoaded.value.modules ?? []).map(({ moduleId }) => moduleId));
      const selectedPaths = moduleAudits.filter(({ manifest }) => ids.has(manifest?.moduleId)).map(({ modulePath }) => modulePath);
      const compiled = await compileContent({ cwd: root, campaignPath, moduleManifestPaths: selectedPaths });
      if (!compiled.ok) diagnostics.push(...compiled.diagnostics);
    }
  }
  if (diagnostics.length) return failure(...diagnostics);
  return Object.freeze({
    ok: true,
    checked: Object.freeze({
      modules: moduleSelection ? selectedAudits.length : discovered.modulePaths.length,
      campaigns: moduleSelection ? 0 : discovered.campaignPaths.length,
    }),
    diagnostics: Object.freeze([]),
  });
}
