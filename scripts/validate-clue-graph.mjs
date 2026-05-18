import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const root = process.cwd();
const oldPcRoot = path.join(root, 'content', 'old_pc');
const srcRoot = path.join(root, 'src');

const sharedKeyPattern = /^(clue|contradiction|tt|route|pressure)_[a-z0-9_]+$/;
const queryKeyPattern = /^(query|term|saved)_.+/;

const definitionFiles = [
  'src/data/knowledge_keys.ts',
  'src/data/vocabulary_data.ts',
  'src/data/dialogue_data.ts',
  'src/data/quests.ts',
].map((file) => path.join(root, file));

const errors = [];
const warnings = [];

function rel(filePath) {
  return path.relative(root, filePath);
}

function addError(message) {
  errors.push(message);
}

function walk(dir, predicate) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const abs = path.join(dir, entry.name);
    if (entry.isDirectory()) return walk(abs, predicate);
    if (entry.isFile() && predicate(abs)) return [abs];
    return [];
  });
}

function parseScalar(raw) {
  const value = raw.trim();
  if (value === 'true') return true;
  if (value === 'false') return false;
  if (/^-?\d+$/.test(value)) return Number(value);
  if (value.startsWith('[') && value.endsWith(']')) {
    const inner = value.slice(1, -1).trim();
    if (!inner) return [];
    return inner
      .split(',')
      .map((part) => part.trim().replace(/^["']|["']$/g, ''))
      .filter(Boolean);
  }
  return value.replace(/^["']|["']$/g, '');
}

function parseFrontmatter(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  const match = raw.match(/^---\n([\s\S]*?)\n---\n([\s\S]*)$/);
  if (!match) return null;

  const meta = {};
  for (const line of match[1].split('\n')) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const sep = trimmed.indexOf(':');
    if (sep === -1) continue;
    meta[trimmed.slice(0, sep).trim()] = parseScalar(trimmed.slice(sep + 1));
  }

  return { meta, body: match[2].trim(), filePath };
}

function arrayValue(value) {
  return Array.isArray(value) ? value.filter((item) => typeof item === 'string') : [];
}

function extractStringKeys(text) {
  const keys = new Set();
  const re = /['"`]((?:clue|contradiction|tt|route|pressure)_[a-z0-9_]+)['"`]/g;
  let match;
  while ((match = re.exec(text))) keys.add(match[1]);
  return keys;
}

function extractIdDefinitions(text) {
  const keys = new Set();
  const re = /\bid\s*:\s*['"`]((?:clue|contradiction|tt|route|pressure)_[a-z0-9_]+)['"`]/g;
  let match;
  while ((match = re.exec(text))) keys.add(match[1]);
  return keys;
}

function extractArrayValues(text, fieldName) {
  const values = [];
  const re = new RegExp(`\\b${fieldName}\\s*:\\s*\\[([^\\]]*)\\]`, 'g');
  let match;
  while ((match = re.exec(text))) {
    const stringRe = /['"`]([^'"`]+)['"`]/g;
    let stringMatch;
    while ((stringMatch = stringRe.exec(match[1]))) values.push(stringMatch[1]);
  }
  return values;
}

function extractSharedDefinitions() {
  const ids = new Set();
  const vocabularyIds = new Set();
  const references = new Set();
  const definitionLocations = new Map();

  for (const filePath of definitionFiles) {
    if (!fs.existsSync(filePath)) continue;
    const text = fs.readFileSync(filePath, 'utf8');
    for (const key of extractIdDefinitions(text)) {
      ids.add(key);
      if (key.startsWith('tt_')) vocabularyIds.add(key);
      if (!definitionLocations.has(key)) definitionLocations.set(key, rel(filePath));
    }
    for (const key of extractStringKeys(text)) references.add(key);
  }

  return { ids, vocabularyIds, references, definitionLocations };
}

function extractGameplayKeyReferences() {
  const files = walk(srcRoot, (filePath) => filePath.endsWith('.ts') && /dialogue|dialog|chat|quest|vocab|knowledge/i.test(filePath));
  const references = [];
  const fields = ['requires', 'requiredKeys', 'requiresAll', 'requiresAny', 'unlocks', 'unlockKeys', 'reveals', 'contradicts'];

  for (const filePath of files) {
    const text = fs.readFileSync(filePath, 'utf8');
    for (const field of fields) {
      for (const key of extractArrayValues(text, field)) {
        references.push({ key, filePath, field });
      }
    }
  }

  return references;
}

const files = walk(oldPcRoot, (filePath) => filePath.endsWith('.md') && !filePath.endsWith('README.md'));
const oldPcItems = files.map(parseFrontmatter).filter(Boolean);
const oldPcIds = new Set(oldPcItems.map((item) => item.meta.id).filter(Boolean));
const oldPcProducedKeys = new Set();
const oldPcConsumedKeys = new Set();
const oldPcKeyReferences = new Map();

function rememberReference(key, filePath, role) {
  if (!oldPcKeyReferences.has(key)) oldPcKeyReferences.set(key, []);
  oldPcKeyReferences.get(key).push(`${rel(filePath)}:${role}`);
}

for (const item of oldPcItems) {
  const itemId = String(item.meta.id ?? '');
  if (!itemId) continue;

  for (const field of ['reveals', 'contradicts', 'unlocks', 'vocabulary']) {
    for (const key of arrayValue(item.meta[field])) {
      oldPcProducedKeys.add(key);
      rememberReference(key, item.filePath, field);
    }
  }

  for (const key of arrayValue(item.meta.requires)) {
    oldPcConsumedKeys.add(key);
    rememberReference(key, item.filePath, 'requires');
  }

  for (const term of [...arrayValue(item.meta.searchTerms), ...arrayValue(item.meta.suggestedTerms)]) {
    oldPcProducedKeys.add(`query_${term.trim().toLocaleLowerCase('ru')}`);
  }
}

const shared = extractSharedDefinitions();
const gameplayReferences = extractGameplayKeyReferences();
const knownRuntimeKeys = new Set([...oldPcIds, ...oldPcProducedKeys, ...shared.ids]);

for (const item of oldPcItems) {
  const fileLabel = rel(item.filePath);
  const meaningfulFields = ['reveals', 'contradicts', 'unlocks', 'vocabulary'];
  if (!meaningfulFields.some((field) => arrayValue(item.meta[field]).length > 0)) {
    addError(`${fileLabel}: old PC item is orphaned from gameplay; add reveals/contradicts/unlocks/vocabulary`);
  }

  for (const requirement of arrayValue(item.meta.requires)) {
    if (oldPcIds.has(requirement) || queryKeyPattern.test(requirement) || knownRuntimeKeys.has(requirement)) continue;
    addError(`${fileLabel}: missing required key "${requirement}"`);
  }

  for (const field of ['reveals', 'contradicts', 'unlocks', 'vocabulary']) {
    for (const key of arrayValue(item.meta[field])) {
      if (!sharedKeyPattern.test(key)) continue;
      if (!shared.ids.has(key)) {
        addError(`${fileLabel}: ${field} key "${key}" has no shared definition`);
      }
    }
  }
}

for (const reference of gameplayReferences) {
  if (!sharedKeyPattern.test(reference.key) && !oldPcIds.has(reference.key)) continue;
  if (reference.field.toLowerCase().includes('require') && !knownRuntimeKeys.has(reference.key)) {
    addError(`${rel(reference.filePath)}: ${reference.field} references unknown key "${reference.key}"`);
  }
  if (sharedKeyPattern.test(reference.key) && !shared.ids.has(reference.key)) {
    addError(`${rel(reference.filePath)}: ${reference.field} key "${reference.key}" has no shared definition`);
  }
}

for (const key of shared.ids) {
  if (!key.startsWith('clue_') && !key.startsWith('contradiction_')) continue;
  const used = oldPcProducedKeys.has(key)
    || oldPcConsumedKeys.has(key)
    || gameplayReferences.some((reference) => reference.key === key);
  if (!used) {
    const where = shared.definitionLocations.get(key) ?? 'shared data';
    addError(`${where}: orphan shared clue "${key}" is not produced or consumed by old PC/dialogue data`);
  }
}

for (const ttKey of shared.vocabularyIds) {
  const usedInOldPc = oldPcProducedKeys.has(ttKey) || oldPcConsumedKeys.has(ttKey);
  const usedInDefinitionConfig = definitionFiles.some((filePath) => {
    if (!fs.existsSync(filePath)) return false;
    const text = fs.readFileSync(filePath, 'utf8');
    const idIndex = text.indexOf(ttKey);
    if (idIndex === -1) return false;
    const window = text.slice(idIndex, idIndex + 900);
    return /reReadTargets\s*:\s*\[[^\]]*['"`][^'"`]+['"`]/.test(window)
      || /dialogueKeyId\s*:\s*['"`][^'"`]+['"`]/.test(window)
      || /searchTerms\s*:\s*\[[^\]]*['"`][^'"`]+['"`]/.test(window);
  });

  if (!usedInOldPc && !usedInDefinitionConfig) {
    const where = shared.definitionLocations.get(ttKey) ?? 'shared vocabulary data';
    addError(`${where}: vocabulary "${ttKey}" has no gameplay use (old PC use, reReadTargets, searchTerms or dialogueKeyId)`);
  }
}

if (shared.ids.size === 0) {
  warnings.push('No shared key definition file found yet; expected src/data/knowledge_keys.ts and/or src/data/vocabulary_data.ts.');
}

if (errors.length) {
  const uniqueErrors = [...new Set(errors)].sort();
  console.error(`clue graph validation failed: ${uniqueErrors.length} error(s)`);
  for (const error of uniqueErrors) console.error(`- ${error}`);
  if (warnings.length) {
    console.error('\nwarnings:');
    for (const warning of warnings) console.error(`- ${warning}`);
  }
  process.exit(1);
}

if (warnings.length) {
  for (const warning of warnings) console.warn(`warning: ${warning}`);
}

console.log(`clue graph ok: ${oldPcItems.length} old PC items, ${shared.ids.size} shared keys`);
