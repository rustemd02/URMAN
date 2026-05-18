import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const root = process.cwd();
const routeRoot = path.join(root, 'public', 'assets', 'urman_route_map');
const graphPath = path.join(routeRoot, 'route_graph.json');
const manifestPath = path.join(routeRoot, 'asset_manifest.json');
const animationPath = path.join(routeRoot, 'animation_layers.json');
const editablePath = path.join(routeRoot, 'editable_layers.json');
const routeScenePath = path.join(root, 'src', 'scenes', 'RouteNavigationScene.ts');
const sceneManagerPath = path.join(root, 'src', 'game', 'SceneManager.ts');

const errors = [];

function rel(filePath) {
  return path.relative(root, filePath);
}

function addError(message) {
  errors.push(message);
}

function readJson(filePath) {
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch (error) {
    addError(`${rel(filePath)}: invalid or missing JSON (${error.message})`);
    return {};
  }
}

function fileExistsFromPublicPath(publicPath) {
  const normalized = publicPath.replace(/^\/+/, '');
  return fs.existsSync(path.join(root, 'public', normalized.replace(/^assets\//, 'assets/')));
}

function findBalancedObject(text, marker) {
  const markerIndex = text.indexOf(marker);
  if (markerIndex === -1) return '';
  const assignmentIndex = text.indexOf('=', markerIndex);
  const start = text.indexOf('{', assignmentIndex === -1 ? markerIndex : assignmentIndex);
  if (start === -1) return '';

  let depth = 0;
  let quote = null;
  let escape = false;

  for (let index = start; index < text.length; index += 1) {
    const char = text[index];
    if (quote) {
      if (escape) {
        escape = false;
      } else if (char === '\\') {
        escape = true;
      } else if (char === quote) {
        quote = null;
      }
      continue;
    }

    if (char === '"' || char === "'" || char === '`') {
      quote = char;
      continue;
    }
    if (char === '{') depth += 1;
    if (char === '}') {
      depth -= 1;
      if (depth === 0) return text.slice(start, index + 1);
    }
  }

  return '';
}

function extractExternalHandoffs() {
  if (!fs.existsSync(routeScenePath)) return new Map();
  const text = fs.readFileSync(routeScenePath, 'utf8');
  const block = findBalancedObject(text, 'const EXTERNAL_HANDOFFS');
  const handoffs = new Map();
  const topLevelKeyRe = /^\s{4}([a-zA-Z0-9_]+):\s*\{/gm;
  const matches = [...block.matchAll(topLevelKeyRe)];

  for (let index = 0; index < matches.length; index += 1) {
    const match = matches[index];
    const key = match[1];
    const entryStart = match.index;
    const entryEnd = matches[index + 1]?.index ?? block.length;
    const entry = block.slice(entryStart, entryEnd);
    const sceneId = entry.match(/\bsceneId:\s*['"`]([^'"`]+)['"`]/)?.[1];
    const imagePath = entry.match(/\bimagePath:\s*['"`]([^'"`]+)['"`]/)?.[1];
    handoffs.set(key, { sceneId, imagePath });
  }

  return handoffs;
}

function extractSceneIds() {
  if (!fs.existsSync(sceneManagerPath)) return new Set();
  const text = fs.readFileSync(sceneManagerPath, 'utf8');
  const ids = new Set();
  const caseRe = /case\s+['"`]([^'"`]+)['"`]\s*:/g;
  let match;
  while ((match = caseRe.exec(text))) ids.add(match[1]);
  return ids;
}

function extractStateTransitionRequirementTriggers() {
  if (!fs.existsSync(routeScenePath)) return new Set();
  const text = fs.readFileSync(routeScenePath, 'utf8');
  const block = findBalancedObject(text, 'const STATE_TRANSITION_REQUIREMENTS');
  const triggers = new Set();
  const keyRe = /^\s{4}([a-zA-Z0-9_]+):/gm;
  let match;
  while ((match = keyRe.exec(block))) triggers.add(match[1]);
  return triggers;
}

function validateAssetId(assetId, manifestIds, context) {
  if (!assetId) return;
  if (!manifestIds.has(assetId)) addError(`${context}: unknown asset id "${assetId}"`);
}

const graph = readJson(graphPath);
const manifest = readJson(manifestPath);
const animation = readJson(animationPath);
const editable = readJson(editablePath);

const nodes = Array.isArray(graph.nodes) ? graph.nodes : [];
const nodeIds = new Set(nodes.map((node) => node.id).filter(Boolean));
const manifestAssets = Array.isArray(manifest.assets) ? manifest.assets : [];
const manifestIds = new Set(manifestAssets.map((asset) => asset.asset_id).filter(Boolean));
const handoffs = extractExternalHandoffs();
const handoffIds = new Set(handoffs.keys());
const sceneIds = extractSceneIds();
const gatedTransitionTriggers = extractStateTransitionRequirementTriggers();
const intentionallyUngatedTriggers = new Set(['time_phase_evening']);

for (const node of nodes) {
  if (!node.id) {
    addError(`${rel(graphPath)}: route node without id`);
    continue;
  }

  for (const field of ['locationId', 'timePhase', 'pressureVariant', 'facing', 'backgroundAssetId']) {
    if (!node[field]) addError(`${rel(graphPath)}:${node.id}: missing "${field}"`);
  }
  if (!Array.isArray(node.availableActions)) addError(`${rel(graphPath)}:${node.id}: availableActions must be an array`);
  if (!node.exits || typeof node.exits !== 'object') addError(`${rel(graphPath)}:${node.id}: exits must be an object`);
  validateAssetId(node.backgroundAssetId, manifestIds, `${rel(graphPath)}:${node.id}.backgroundAssetId`);

  for (const [action, target] of Object.entries(node.exits ?? {})) {
    if (!target || typeof target !== 'string') {
      addError(`${rel(graphPath)}:${node.id}.exits.${action}: target must be a string`);
      continue;
    }
    if (!nodeIds.has(target) && !handoffIds.has(target) && !sceneIds.has(target)) {
      addError(`${rel(graphPath)}:${node.id}.exits.${action}: target "${target}" is not a node, scene, or EXTERNAL_HANDOFFS entry`);
    }
  }

  for (const sign of node.diegeticSigns ?? []) {
    if (!sign.target) continue;
    if (!nodeIds.has(sign.target) && !handoffIds.has(sign.target) && !sceneIds.has(sign.target)) {
      addError(`${rel(graphPath)}:${node.id}.diegeticSigns: target "${sign.target}" is not a node, scene, or EXTERNAL_HANDOFFS entry`);
    }
  }
}

const seenNodeIds = new Set();
for (const node of nodes) {
  if (!node.id) continue;
  if (seenNodeIds.has(node.id)) addError(`${rel(graphPath)}: duplicate route node "${node.id}"`);
  seenNodeIds.add(node.id);
}

const startNodeId = graph.startNodeId ?? 'arrival_vehicle_dusk';
if (nodes.length && !nodeIds.has(startNodeId)) {
  addError(`${rel(graphPath)}: start node "${startNodeId}" does not exist`);
}

for (const [transitionId, transition] of Object.entries(graph.transitions ?? {})) {
  validateAssetId(transition.assetId, manifestIds, `${rel(graphPath)}:transitions.${transitionId}.assetId`);
  validateAssetId(transition.overlayAssetId, manifestIds, `${rel(graphPath)}:transitions.${transitionId}.overlayAssetId`);
}

for (const [action, defaults] of Object.entries(graph.navigation_contract?.action_transition_defaults ?? {})) {
  const transitionId = defaults?.transitionId;
  if (transitionId && !(transitionId in (graph.transitions ?? {}))) {
    addError(`${rel(graphPath)}: action "${action}" references missing transition "${transitionId}"`);
  }
}

for (const collectionName of ['stateTransitions', 'supportViewTransitions']) {
  for (const transition of graph[collectionName] ?? []) {
    for (const [field, value] of Object.entries({
      from: transition.from,
      fromNodeId: transition.fromNodeId,
      to: transition.to,
      toNodeId: transition.toNodeId,
    })) {
      if (!value) continue;
      if (!nodeIds.has(value) && !handoffIds.has(value) && !sceneIds.has(value) && !manifestIds.has(value)) {
        addError(`${rel(graphPath)}:${collectionName}.${field}: target "${value}" is not a node, scene, handoff, or manifest asset`);
      }
    }
    if (collectionName === 'stateTransitions' && transition.trigger) {
      const trigger = transition.trigger;
      if (!gatedTransitionTriggers.has(trigger) && !intentionallyUngatedTriggers.has(trigger)) {
        addError(`${rel(graphPath)}:${collectionName}.${trigger}: trigger must be listed in STATE_TRANSITION_REQUIREMENTS or intentionallyUngatedTriggers`);
      }
    }
  }
}

const seenManifestIds = new Set();
for (const asset of manifestAssets) {
  if (!asset.asset_id) {
    addError(`${rel(manifestPath)}: manifest asset without asset_id`);
    continue;
  }
  if (seenManifestIds.has(asset.asset_id)) addError(`${rel(manifestPath)}: duplicate asset_id "${asset.asset_id}"`);
  seenManifestIds.add(asset.asset_id);
  if (!asset.file_name) {
    addError(`${rel(manifestPath)}:${asset.asset_id}: missing file_name`);
    continue;
  }
  const assetPath = path.join(routeRoot, asset.file_name);
  if (!fs.existsSync(assetPath)) addError(`${rel(manifestPath)}:${asset.asset_id}: missing asset file ${rel(assetPath)}`);
}

for (const asset of animation.assets ?? []) {
  validateAssetId(asset.asset_id, manifestIds, `${rel(animationPath)}.assets`);
}

for (const layer of editable.layers ?? []) {
  validateAssetId(layer.asset_id, manifestIds, `${rel(editablePath)}.layers`);
}

for (const [handoffId, handoff] of handoffs) {
  if (handoff.sceneId && !sceneIds.has(handoff.sceneId)) {
    addError(`${rel(routeScenePath)}: EXTERNAL_HANDOFFS.${handoffId} references unknown scene "${handoff.sceneId}"`);
  }
  if (handoff.imagePath && !fileExistsFromPublicPath(handoff.imagePath)) {
    addError(`${rel(routeScenePath)}: EXTERNAL_HANDOFFS.${handoffId} references missing image "${handoff.imagePath}"`);
  }
}

if (errors.length) {
  console.error(`route graph validation failed: ${errors.length} error(s)`);
  for (const error of [...new Set(errors)].sort()) console.error(`- ${error}`);
  process.exit(1);
}

console.log(`route graph ok: ${nodes.length} nodes, ${manifestIds.size} assets, ${handoffIds.size} handoffs`);
