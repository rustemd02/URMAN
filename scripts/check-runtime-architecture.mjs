#!/usr/bin/env node
import { access, readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const SOURCE_FILE = /\.(?:mjs|mts|ts)$/;
const DEV_ONLY_PREFIXES = Object.freeze([
  'src/lab/',
  'src/MainMap/',
  'src/runtime/modules/legacy-mainmap/',
]);
const GENERIC_OR_COMPOSITION_PREFIXES = Object.freeze([
  'src/game/',
  'src/main.ts',
  'src/content/web/',
  'src/runtime/bootstrap/',
  'src/runtime/capabilities/',
  'src/runtime/contracts/',
  'src/runtime/kernel/',
  'src/runtime/media/',
  'src/runtime/modules/active/',
  'src/runtime/persistence/',
  'src/runtime/quests/',
  'src/runtime/registries/',
  'src/runtime/resolvers/',
  'src/runtime/world/',
]);
const SCENE_IDENTIFIER_BASE = String.raw`(?:scene(?:Id|ID|Key|Name|Token|Ref)?|(?:current|active|target|next|requested|destination|source|origin|from|to|selected|pending|previous|initial)Scene(?:Id|ID|Key|Name|Token|Ref)?)`;
const SCENE_IDENTIFIER = String.raw`(?:(?:[A-Za-z_$][\w$]*\.)*)${SCENE_IDENTIFIER_BASE}`;
const NON_TYPE_LITERAL = String.raw`['"](?!(?:string|number|boolean|object|function|symbol|bigint|undefined)['"])[^'"]+['"]`;
const RETIRED_OWNER_PATHS = Object.freeze([
  'src/data/characters.ts',
  'src/data/dialogue_data.ts',
  'src/data/knowledge_keys.ts',
  'src/data/vocabulary_data.ts',
  'src/data/quests.ts',
  'src/game/GameState.ts',
  'src/game/SceneManager.ts',
  'src/game/EventEmitter.ts',
  'src/data/chat_data.ts',
  'src/os/apps/chat.ts',
  'src/os/data/ChatProvider.ts',
  'src/os/data/chats/master_db.json',
  'src/os/data/oldPcContent.ts',
  'src/os/data/web_content.json',
  'src/scenes/ChapterScene.ts',
  'src/scenes/ForestScene.ts',
  'src/scenes/HouseScene.ts',
  'src/scenes/IntroScene.ts',
  'src/scenes/MainMenuScene.ts',
  'src/scenes/MosqueScene.ts',
  'src/scenes/RouteNavigationScene.ts',
  'src/scenes/ZiratMiniGame.ts',
  'src/systems/DialogueSystem.ts',
  'src/systems/InventorySystem.ts',
  'src/systems/InvestigationSystem.ts',
  'src/systems/QuestSystem.ts',
  'src/systems/SaveSystem.ts',
  'src/systems/AudioSystem.ts',
  'src/systems/VocabularySystem.ts',
  'src/ui/DialogueUI.ts',
  'src/ui/AudioDebugPanel.ts',
  'src/ui/HUD.ts',
  'src/ui/InventoryUI.ts',
  'src/ui/NotebookUI.ts',
]);
const BROWSER_STORAGE_OWNER = 'src/runtime/persistence/production-persistence-gateway.mjs';
const BROWSER_PERSISTENCE_FACTORY_CALLER = 'src/main.ts';

function isDevelopmentOnly(relativePath) {
  return DEV_ONLY_PREFIXES.some((prefix) => relativePath.startsWith(prefix));
}

function isGenericOrComposition(relativePath) {
  return GENERIC_OR_COMPOSITION_PREFIXES.some((prefix) => relativePath === prefix || relativePath.startsWith(prefix));
}

function isBrowserStorageOwner(relativePath) {
  return relativePath === BROWSER_STORAGE_OWNER;
}

function browserPersistenceFactoryViolation(source) {
  const withoutOwnerDeclaration = source.replace(/\bexport\s+function\s+createBrowserProductionPersistenceGateway\b/g, '');
  return /\bcreateBrowserProductionPersistenceGateway\b/.test(withoutOwnerDeclaration);
}

function diagnostic(rule, sourcePath, message) {
  return Object.freeze({ rule, sourcePath, message });
}

async function sourceFiles(root, relative = 'src') {
  const directory = path.join(root, relative);
  let entries;
  try { entries = await readdir(directory, { withFileTypes: true }); } catch (error) {
    if (error.code === 'ENOENT') return [];
    throw error;
  }
  const files = [];
  for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name))) {
    const child = path.join(relative, entry.name);
    if (entry.isDirectory()) files.push(...await sourceFiles(root, child));
    else if (entry.isFile() && SOURCE_FILE.test(entry.name)) files.push(child.split(path.sep).join('/'));
  }
  return files;
}

function importViolation(source) {
  const staticImport = /\b(?:import|export)\s+(?:type\s+)?(?:[\s\S]*?\s+from\s+)?['"][^'"]*(?:MainMap|legacy-mainmap|content-lab|ContentLab|\/lab(?:\/|$))[^'"]*['"]/;
  const dynamicImport = /\bimport\(\s*['"][^'"]*(?:MainMap|legacy-mainmap|content-lab|ContentLab|\/lab(?:\/|$))[^'"]*['"]\s*\)/;
  return staticImport.test(source) || dynamicImport.test(source);
}

function retiredCallerViolation(source) {
  return /\b(?:GameState|SceneManager|EventEmitter|RouteNavigationScene|HouseScene|ForestScene|MosqueScene|IntroScene|MainMenuScene|ChapterScene|ZiratMiniGame|SaveSystem|AudioSystem|DialogueSystem|InvestigationSystem|VocabularySystem|QuestSystem|InventorySystem|DialogueUI|AudioDebugPanel|HUD|InventoryUI|NotebookUI|oldPcContent|ChatProvider)\b/.test(source)
    || /(?:from\s*|import\s*)['"][^'"]*\/(?:data\/(?:characters|chat_data|dialogue_data|knowledge_keys|vocabulary_data|quests)|systems\/AudioSystem|os\/(?:apps\/chat|data\/(?:oldPcContent|ChatProvider|chats\/master_db)))['"]/.test(source);
}

function narrativeContentIdViolation(source) {
  const ids = source.matchAll(/\burman\.(?:chapter\d+|legacy\.mainmap):([a-z][a-z0-9-]*)\/[a-z0-9][a-z0-9._-]*/g);
  for (const match of ids) {
    // Capability protocol IDs are an explicit composition contract, not a story lookup.
    if (match[1] !== 'capability') return true;
  }
  return false;
}

function hardcodedSceneControlViolation(source) {
  const typeofSceneGuard = new RegExp(String.raw`\btypeof\s+(?:[A-Za-z_$][\w$]*\.)*${SCENE_IDENTIFIER_BASE}\s*(?:===|!==|==|!=)\s*['"](?:string|number|boolean|object|function|symbol|bigint|undefined)['"]`, 'g');
  const withoutTypeGuards = source.replace(typeofSceneGuard, '');
  const sceneSwitch = new RegExp(String.raw`\bswitch\s*\(\s*${SCENE_IDENTIFIER}\s*\)\s*\{[\s\S]*?\bcase\s+${NON_TYPE_LITERAL}\s*:`, 'm');
  const sceneComparison = new RegExp(String.raw`(?:\b${SCENE_IDENTIFIER}\s*(?:===|!==|==|!=)\s*${NON_TYPE_LITERAL}|${NON_TYPE_LITERAL}\s*(?:===|!==|==|!=)\s*\b${SCENE_IDENTIFIER})`, 'm');
  return sceneSwitch.test(withoutTypeGuards) || sceneComparison.test(withoutTypeGuards);
}

/**
 * Static guard for the production source closure. It deliberately does not
 * scan `scripts/**`: the legacy smoke replacement belongs to MM-60/MM-70.
 */
export async function checkRuntimeArchitecture({ cwd = process.cwd(), requireRetiredOwnersAbsent = true } = {}) {
  const root = path.resolve(cwd);
  const diagnostics = [];
  const files = (await sourceFiles(root)).filter((relativePath) => !isDevelopmentOnly(relativePath));

  for (const relativePath of files) {
    const sourcePath = path.join(root, relativePath);
    const source = await readFile(sourcePath, 'utf8');
    if (importViolation(source)) {
      diagnostics.push(diagnostic('ProductionDevImport', relativePath, 'Production source imports Content Lab or dev-only MainMap code.'));
    }
    if (/\blocalStorage\b/.test(source) && !isBrowserStorageOwner(relativePath)) {
      diagnostics.push(diagnostic('RawLocalStorage', relativePath, 'Production source accesses localStorage directly.'));
    }
    if (relativePath !== BROWSER_PERSISTENCE_FACTORY_CALLER
      && relativePath !== BROWSER_STORAGE_OWNER
      && browserPersistenceFactoryViolation(source)) {
      diagnostics.push(diagnostic('BrowserPersistenceFactoryCaller', relativePath, 'Only src/main.ts may import or call createBrowserProductionPersistenceGateway.'));
    }
    if (/\bwindow\.URMAN\b/.test(source)) {
      diagnostics.push(diagnostic('WindowUrman', relativePath, 'Production source accesses window.URMAN.'));
    }
    if (/\/assets(?:\/|['"`])/.test(source)) {
      diagnostics.push(diagnostic('DirectAssetPath', relativePath, 'Production source contains a direct /assets/ path.'));
    }
    if (retiredCallerViolation(source)) {
      diagnostics.push(diagnostic('RetiredOwnerCaller', relativePath, 'Production source references an MM-10 retired owner.'));
    }
    if (isGenericOrComposition(relativePath)) {
      if (narrativeContentIdViolation(source)) {
        diagnostics.push(diagnostic('NarrativeContentId', relativePath, 'Generic, renderer or composition source hardcodes a narrative content ID.'));
      }
      if (hardcodedSceneControlViolation(source)) {
        diagnostics.push(diagnostic('HardcodedSceneSwitch', relativePath, 'Generic, renderer or composition source contains a hardcoded scene switch.'));
      }
    }
  }

  if (requireRetiredOwnersAbsent) {
    for (const relativePath of RETIRED_OWNER_PATHS) {
      try {
        await access(path.join(root, relativePath));
        diagnostics.push(diagnostic('RetiredOwnerPresent', relativePath, 'An MM-10 owner marked for deletion remains in the production tree.'));
      } catch (error) {
        if (error.code !== 'ENOENT') throw error;
      }
    }
  }

  const ordered = diagnostics.sort((left, right) => `${left.sourcePath}\0${left.rule}\0${left.message}`
    .localeCompare(`${right.sourcePath}\0${right.rule}\0${right.message}`));
  return Object.freeze({
    ok: ordered.length === 0,
    checkedFiles: Object.freeze(files),
    diagnostics: Object.freeze(ordered),
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const result = await checkRuntimeArchitecture();
  if (!result.ok) {
    for (const entry of result.diagnostics) console.error(`${entry.rule} ${entry.sourcePath}: ${entry.message}`);
    process.exitCode = 1;
  } else {
    console.log(`checked ${result.checkedFiles.length} production source files`);
  }
}
