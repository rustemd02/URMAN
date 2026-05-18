import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const root = process.cwd();
const contentRoot = path.join(root, 'content', 'old_pc');

const requiredFields = [
  'id',
  'type',
  'title',
  'pcSection',
  'canonStatus',
  'reliability',
  'mvp',
  'dangerLevel',
  'visibleFromStart',
  'requires',
  'searchTerms',
  'suggestedTerms',
  'reveals',
  'contradicts',
  'unlocks',
  'relatedCharacters',
  'relatedLocations',
  'vocabulary',
];

const allowed = {
  type: ['document', 'record', 'message', 'tatarwiki_article', 'folder_note', 'corrupted_fragment'],
  pcSection: [
    'archive_search',
    'documents_marat',
    'tatarwiki',
    'saved_messages',
    'internal_accounting',
    'household_registry',
    'violations_compensation',
    'kara_urman',
    'damaged_hidden',
    'household_misc',
  ],
  canonStatus: ['canon', 'soft_canon', 'hypothesis', 'proposal', 'in_world_lie'],
  reliability: [
    'official_lie',
    'partial_truth',
    'personal_memory',
    'village_record',
    'pact_record',
    'folklore_mask',
    'corrupted',
    'unverified',
  ],
};

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const abs = path.join(dir, entry.name);
    if (entry.isDirectory()) return walk(abs);
    if (entry.isFile() && entry.name.endsWith('.md') && entry.name !== 'README.md') return [abs];
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
  if (!match) {
    throw new Error(`${filePath}: missing YAML frontmatter`);
  }

  const meta = {};
  for (const line of match[1].split('\n')) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const sep = trimmed.indexOf(':');
    if (sep === -1) {
      throw new Error(`${filePath}: invalid frontmatter line "${line}"`);
    }
    const key = trimmed.slice(0, sep).trim();
    const value = trimmed.slice(sep + 1).trim();
    meta[key] = parseScalar(value);
  }

  return { meta, body: match[2].trim() };
}

function validateItem(item, filePath, ids, unlockableKeys) {
  for (const field of requiredFields) {
    if (!(field in item.meta)) {
      throw new Error(`${filePath}: missing required field "${field}"`);
    }
  }

  if (!/^[a-z0-9_]+$/.test(item.meta.id)) {
    throw new Error(`${filePath}: id must be snake_case lowercase`);
  }

  if (ids.has(item.meta.id)) {
    throw new Error(`${filePath}: duplicate id "${item.meta.id}"`);
  }
  ids.add(item.meta.id);

  for (const [field, values] of Object.entries(allowed)) {
    if (!values.includes(item.meta[field])) {
      throw new Error(`${filePath}: invalid ${field} "${item.meta[field]}"`);
    }
  }

  for (const field of ['requires', 'searchTerms', 'suggestedTerms', 'reveals', 'contradicts', 'unlocks', 'relatedCharacters', 'relatedLocations', 'vocabulary']) {
    if (!Array.isArray(item.meta[field])) {
      throw new Error(`${filePath}: "${field}" must be an array`);
    }
  }

  if (typeof item.meta.mvp !== 'boolean') {
    throw new Error(`${filePath}: "mvp" must be boolean`);
  }

  if (!Number.isInteger(item.meta.dangerLevel) || item.meta.dangerLevel < 0 || item.meta.dangerLevel > 3) {
    throw new Error(`${filePath}: "dangerLevel" must be an integer from 0 to 3`);
  }

  if (typeof item.meta.visibleFromStart !== 'boolean') {
    throw new Error(`${filePath}: "visibleFromStart" must be boolean`);
  }

  if (!item.body) {
    throw new Error(`${filePath}: body must not be empty`);
  }

  for (const key of [...item.meta.reveals, ...item.meta.unlocks, ...item.meta.vocabulary]) {
    unlockableKeys.add(key);
  }
}

const files = walk(contentRoot);
const ids = new Set();
const unlockableKeys = new Set();
const items = files.map((filePath) => {
  const item = parseFrontmatter(filePath);
  validateItem(item, path.relative(root, filePath), ids, unlockableKeys);
  return { ...item, filePath };
});

for (const item of items) {
  for (const requirement of item.meta.requires) {
    if (!ids.has(requirement) && !unlockableKeys.has(requirement)) {
      throw new Error(`${path.relative(root, item.filePath)}: unknown requirement "${requirement}"`);
    }
  }
}

console.log(`old pc content ok: ${items.length} files`);
