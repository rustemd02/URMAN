import { cloneJsonValue } from '../contracts/json-value.mjs';
import {
  ResolverErrorCode,
  moduleIdFromLogicalId,
  originFor,
  resolverError,
} from './resolver-errors.mjs';

const CATEGORY_KIND = Object.freeze({
  assets: 'asset',
  capabilities: 'capability',
  characters: 'character',
  dialogues: 'dialogue',
  documents: ['doc', 'ument'].join(''),
  knowledge: 'knowledge',
  quests: 'quest',
  scenes: 'scene',
  texts: 'text',
  vocabulary: 'vocabulary',
});

function ownData(object, key, label) {
  if (!object || typeof object !== 'object' || Array.isArray(object)) {
    throw resolverError(ResolverErrorCode.InvalidPack, `${label} must be an object.`, {}, { sourcePointer: label });
  }
  const descriptor = Object.getOwnPropertyDescriptor(object, key);
  if (!descriptor || !Object.hasOwn(descriptor, 'value')) {
    throw resolverError(ResolverErrorCode.InvalidPack, `${label}.${key} must be an own data property.`, {}, { sourcePointer: `${label}/${key}` });
  }
  return descriptor.value;
}

function ownArray(object, key, label) {
  const value = ownData(object, key, label);
  if (!Array.isArray(value)) {
    throw resolverError(ResolverErrorCode.InvalidPack, `${label}.${key} must be an array.`, {}, { sourcePointer: `${label}/${key}` });
  }
  return value;
}

function parseLogicalId(id, category, index) {
  if (typeof id !== 'string') {
    throw resolverError(ResolverErrorCode.InvalidPack, `Registry entry ${category}/${index} has no logical ID.`, originFor(category, index, id));
  }
  const match = /^([a-z][a-z0-9]*(?:[.-][a-z0-9]+)*):([a-z][a-z0-9-]*)\/([a-z0-9][a-z0-9._-]*)$/.exec(id);
  if (!match || match[2] !== CATEGORY_KIND[category]) {
    throw resolverError(ResolverErrorCode.InvalidPack, `Registry entry ${id} does not match ${category}.`, originFor(category, index, id));
  }
  return Object.freeze({ moduleId: match[1], id });
}

function moduleOrder(pack) {
  const campaign = ownData(pack, 'campaign', 'CompiledContentPack');
  const modules = ownArray(campaign, 'orderedModules', 'CompiledContentPack.campaign');
  const order = new Map();
  for (const [index, dependency] of modules.entries()) {
    const moduleId = ownData(dependency, 'moduleId', `CompiledContentPack.campaign.orderedModules/${index}`);
    if (typeof moduleId !== 'string' || !moduleId) {
      throw resolverError(ResolverErrorCode.InvalidPack, `Campaign module ${index} has an invalid module ID.`, {}, { sourcePointer: `/campaign/orderedModules/${index}/moduleId` });
    }
    if (order.has(moduleId)) {
      throw resolverError(ResolverErrorCode.InvalidPack, `Campaign contains duplicate module ${moduleId}.`, {}, { moduleId, sourcePointer: `/campaign/orderedModules/${index}/moduleId` });
    }
    order.set(moduleId, index);
  }
  return order;
}

function normalizedEntries(pack, category, order) {
  const registries = ownData(pack, 'registries', 'CompiledContentPack');
  const entries = ownArray(registries, category, 'CompiledContentPack.registries');
  const normalized = entries.map((entry, index) => {
    let value;
    try {
      value = cloneJsonValue(entry, `CompiledContentPack.registries.${category}[${index}]`);
    } catch (error) {
      throw resolverError(ResolverErrorCode.InvalidPack, error.message, {}, { sourcePointer: `/registries/${category}/${index}` });
    }
    const parsed = parseLogicalId(value.id, category, index);
    if (!order.has(parsed.moduleId)) {
      throw resolverError(
        ResolverErrorCode.InvalidPack,
        `Registry entry ${parsed.id} belongs to a module outside the resolved campaign.`,
        originFor(category, index, parsed.id),
      );
    }
    return Object.freeze({
      value,
      origin: originFor(category, index, parsed.id),
      moduleIndex: order.get(parsed.moduleId),
      sourceIndex: index,
    });
  });
  return normalized.sort((left, right) =>
    left.moduleIndex - right.moduleIndex
    || left.sourceIndex - right.sourceIndex
    || left.value.id.localeCompare(right.value.id));
}

/**
 * Rebuilds the content indexes in explicit campaign-module order.  The compiler
 * already rejects duplicates; this second boundary protects hosts that receive a
 * malformed or hand-built pack and never allows a last-write-wins override.
 */
export function createResolverCatalog(pack) {
  if (!pack || typeof pack !== 'object' || Array.isArray(pack)) {
    throw resolverError(ResolverErrorCode.InvalidPack, 'CompiledContentPack is required.');
  }
  const order = moduleOrder(pack);
  const categories = new Map();
  for (const category of Object.keys(CATEGORY_KIND)) {
    const byId = new Map();
    for (const entry of normalizedEntries(pack, category, order)) {
      const existing = byId.get(entry.value.id);
      if (existing) {
        throw resolverError(
          ResolverErrorCode.DuplicateLogicalId,
          `Duplicate logical ID ${entry.value.id}.`,
          entry.origin,
          { firstSourcePointer: existing.origin.sourcePointer, firstModuleId: existing.origin.moduleId },
        );
      }
      byId.set(entry.value.id, entry);
    }
    categories.set(category, byId);
  }
  return Object.freeze({
    category(category) {
      const entries = categories.get(category);
      if (!entries) throw resolverError(ResolverErrorCode.InvalidPack, `Unknown resolver category ${category}.`);
      return entries;
    },
    ids(category) {
      return Object.freeze([...this.category(category).keys()]);
    },
  });
}

export function absentOrigin(category, id) {
  const moduleId = moduleIdFromLogicalId(id) ?? 'unknown-module';
  return Object.freeze({ id, moduleId, sourcePointer: `/registries/${category}` });
}

export function requireCatalogEntry(catalog, category, id, from = undefined) {
  const entry = catalog.category(category).get(id);
  if (entry) return entry;
  const missingCode = category === 'assets'
    ? ResolverErrorCode.MissingAsset
    : category === 'texts'
      ? ResolverErrorCode.MissingText
      : ResolverErrorCode.MissingVocabulary;
  const origin = from ?? absentOrigin(category, id);
  throw resolverError(missingCode, `Missing ${category.slice(0, -1)} ${id}.`, origin, { id });
}

export function ownArrayField(value, key, origin) {
  const descriptor = Object.getOwnPropertyDescriptor(value, key);
  if (!descriptor || !Object.hasOwn(descriptor, 'value') || !Array.isArray(descriptor.value)) {
    throw resolverError(ResolverErrorCode.InvalidPack, `${origin.id}.${key} must be an array.`, origin, { sourcePointer: `${origin.sourcePointer}/${key}` });
  }
  return descriptor.value;
}

export function ownStringField(value, key, origin, { optional = false } = {}) {
  const descriptor = Object.getOwnPropertyDescriptor(value, key);
  if (!descriptor) {
    if (optional) return undefined;
    throw resolverError(ResolverErrorCode.InvalidPack, `${origin.id}.${key} is required.`, origin, { sourcePointer: `${origin.sourcePointer}/${key}` });
  }
  if (!Object.hasOwn(descriptor, 'value') || typeof descriptor.value !== 'string' || !descriptor.value) {
    throw resolverError(ResolverErrorCode.InvalidPack, `${origin.id}.${key} must be a non-empty string.`, origin, { sourcePointer: `${origin.sourcePointer}/${key}` });
  }
  return descriptor.value;
}

export function referenceOrigin(origin, suffix) {
  return Object.freeze({ ...origin, sourcePointer: `${origin.sourcePointer}${suffix}` });
}
