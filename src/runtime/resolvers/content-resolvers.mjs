import { canonicalJson, cloneJsonValue, deepFreeze } from '../contracts/json-value.mjs';
import {
  absentOrigin,
  createResolverCatalog,
  ownArrayField,
  ownStringField,
  referenceOrigin,
  requireCatalogEntry,
} from './resolver-catalog.mjs';
import { ResolverError, ResolverErrorCode, resolverError } from './resolver-errors.mjs';

export { ResolverError, ResolverErrorCode };

function hashText(text) {
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash >>> 0;
}

function normalizeVariant(variant) {
  if (variant === undefined) return Object.freeze({});
  if (typeof variant === 'string') return Object.freeze({ id: variant });
  if (!variant || typeof variant !== 'object' || Array.isArray(variant)) {
    throw resolverError(ResolverErrorCode.InvalidPack, 'Asset variant selector must be a string or object.');
  }
  const descriptors = Object.getOwnPropertyDescriptors(variant);
  const result = {};
  for (const [key, descriptor] of Object.entries(descriptors)) {
    if (!descriptor.enumerable || !Object.hasOwn(descriptor, 'value') || !['id', 'seed', 'state'].includes(key)) {
      throw resolverError(ResolverErrorCode.InvalidPack, `Asset variant selector field ${key} is invalid.`);
    }
    result[key] = descriptor.value;
  }
  if (result.id !== undefined && (typeof result.id !== 'string' || !result.id)) {
    throw resolverError(ResolverErrorCode.InvalidPack, 'Asset variant selector ID must be a non-empty string.');
  }
  if (result.seed !== undefined) {
    try { cloneJsonValue(result.seed, 'Asset variant seed'); } catch (error) { throw resolverError(ResolverErrorCode.InvalidPack, error.message); }
  }
  if (result.state !== undefined) {
    try { cloneJsonValue(result.state, 'Asset variant state'); } catch (error) { throw resolverError(ResolverErrorCode.InvalidPack, error.message); }
  }
  return Object.freeze(result);
}

function accessibilityFor(asset, origin) {
  const descriptor = Object.getOwnPropertyDescriptor(asset, 'accessibility');
  if (!descriptor || !Object.hasOwn(descriptor, 'value') || !descriptor.value || typeof descriptor.value !== 'object' || Array.isArray(descriptor.value)) {
    throw resolverError(ResolverErrorCode.InvalidPack, `${asset.id}.accessibility must be an object.`, origin, { sourcePointer: `${origin.sourcePointer}/accessibility` });
  }
  const accessibility = descriptor.value;
  if (typeof accessibility.decorative !== 'boolean') {
    throw resolverError(ResolverErrorCode.InvalidPack, `${asset.id}.accessibility.decorative must be a boolean.`, origin, { sourcePointer: `${origin.sourcePointer}/accessibility/decorative` });
  }
  for (const key of ['altTextId', 'captionTextId', 'audioDescriptionTextId']) {
    if (accessibility[key] !== undefined && (typeof accessibility[key] !== 'string' || !accessibility[key])) {
      throw resolverError(ResolverErrorCode.InvalidPack, `${asset.id}.accessibility.${key} must be a non-empty string.`, origin, { sourcePointer: `${origin.sourcePointer}/accessibility/${key}` });
    }
  }
  return accessibility;
}

function localizedValue(value, locale, origin) {
  if (!value || typeof value !== 'object' || Array.isArray(value)
    || typeof value.default !== 'string' || !value.default
    || !value.translations || typeof value.translations !== 'object' || Array.isArray(value.translations)) {
    throw resolverError(ResolverErrorCode.InvalidPack, `${origin.id} has an invalid localized value.`, origin);
  }
  if (locale === undefined) return Object.freeze({ text: value.default, resolvedLocale: 'default' });
  if (typeof locale !== 'string' || !locale) {
    throw resolverError(ResolverErrorCode.InvalidLocale, 'Locale must be a non-empty string.', origin, { requestedLocale: locale });
  }
  const candidates = [];
  let candidate = locale;
  while (candidate) {
    candidates.push(candidate);
    const separator = candidate.lastIndexOf('-');
    candidate = separator > 0 ? candidate.slice(0, separator) : '';
  }
  for (const candidateLocale of candidates) {
    const translated = value.translations[candidateLocale];
    if (typeof translated === 'string' && translated) {
      return Object.freeze({ text: translated, resolvedLocale: candidateLocale });
    }
  }
  return Object.freeze({ text: value.default, resolvedLocale: 'default' });
}

function normalizedVariables(variables, origin) {
  if (variables === undefined) return Object.freeze({});
  if (!variables || typeof variables !== 'object' || Array.isArray(variables)
    || (Object.getPrototypeOf(variables) !== Object.prototype && Object.getPrototypeOf(variables) !== null)) {
    throw resolverError(ResolverErrorCode.InvalidVariable, 'Text variables must be a plain object.', origin);
  }
  const normalized = {};
  for (const [key, descriptor] of Object.entries(Object.getOwnPropertyDescriptors(variables))) {
    if (!descriptor.enumerable || !Object.hasOwn(descriptor, 'value')) {
      throw resolverError(ResolverErrorCode.InvalidVariable, `Text variable ${key} must be an own data property.`, origin, { variable: key });
    }
    const value = descriptor.value;
    if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') normalized[key] = String(value);
    else if (value && typeof value === 'object' && !Array.isArray(value)) {
      const tokenFields = Object.getOwnPropertyDescriptors(value);
      const token = tokenFields.vocabularyId;
      if (Object.keys(tokenFields).length !== 1 || !token || !token.enumerable || !Object.hasOwn(token, 'value')
        || typeof token.value !== 'string' || !token.value) {
        throw resolverError(ResolverErrorCode.InvalidVariable, `Text variable ${key} must be a primitive or vocabulary token.`, origin, { variable: key });
      }
      normalized[key] = Object.freeze({ vocabularyId: token.value });
    } else {
      throw resolverError(ResolverErrorCode.InvalidVariable, `Text variable ${key} must be a primitive or vocabulary token.`, origin, { variable: key });
    }
  }
  return Object.freeze(normalized);
}

function outputTextSegments(template, variables, catalog, locale, origin) {
  const segments = [];
  let position = 0;
  const tokenPattern = /\{\{([A-Za-z_][A-Za-z0-9_.-]*)\}\}/g;
  for (const match of template.matchAll(tokenPattern)) {
    if (match.index > position) segments.push(Object.freeze({ type: 'text', value: template.slice(position, match.index) }));
    const variableName = match[1];
    if (!Object.hasOwn(variables, variableName)) {
      throw resolverError(ResolverErrorCode.MissingVariable, `Missing text variable ${variableName}.`, origin, { variable: variableName });
    }
    const value = variables[variableName];
    if (typeof value === 'string') segments.push(Object.freeze({ type: 'text', value }));
    else {
      const vocabulary = requireCatalogEntry(catalog, 'vocabulary', value.vocabularyId, origin);
      const meaning = localizedValue(vocabulary.value.meaning, locale, vocabulary.origin);
      segments.push(Object.freeze({
        type: 'vocabulary',
        vocabularyId: vocabulary.value.id,
        language: vocabulary.value.language,
        term: vocabulary.value.term,
        meaning: meaning.text,
        meaningLocale: meaning.resolvedLocale,
      }));
    }
    position = match.index + match[0].length;
  }
  if (position < template.length || segments.length === 0) segments.push(Object.freeze({ type: 'text', value: template.slice(position) }));
  return Object.freeze(segments);
}

function resolvedText(segments) {
  return segments.map((segment) => segment.type === 'text' ? segment.value : segment.term).join('');
}

function fieldReference(asset, key, origin) {
  const id = asset.accessibility[key];
  if (typeof id !== 'string' || !id) {
    throw resolverError(ResolverErrorCode.InvalidAccessibility, `${asset.id} needs accessibility.${key}.`, origin, { sourcePointer: `${origin.sourcePointer}/accessibility/${key}` });
  }
  return id;
}

function requireTextReference(catalog, id, origin) {
  return requireCatalogEntry(catalog, 'texts', id, origin);
}

function ensureReferences(catalog, entries, field, category, expectedAssetKind = undefined) {
  for (const entry of entries) {
    const values = ownArrayField(entry.value, field, entry.origin);
    for (const [index, id] of values.entries()) {
      const reference = referenceOrigin(entry.origin, `/${field}/${index}`);
      const asset = requireCatalogEntry(catalog, category, id, reference);
      if (expectedAssetKind && asset.value.kind !== expectedAssetKind) {
        throw resolverError(
          ResolverErrorCode.WrongAssetKind,
          `${id} must be an ${expectedAssetKind} asset.`,
          reference,
          { id, expectedKind: expectedAssetKind, actualKind: asset.value.kind },
        );
      }
    }
  }
}

/** Validates all generic asset/text links exposed by a compiled pack. */
export function validateManifestClosure(pack) {
  const catalog = createResolverCatalog(pack);
  const assets = [...catalog.category('assets').values()];
  for (const entry of assets) {
    const asset = entry.value;
    ownStringField(asset, 'kind', entry.origin);
    ownStringField(asset, 'file', entry.origin);
    ownStringField(asset, 'mediaType', entry.origin);
    const accessibility = accessibilityFor(asset, entry.origin);
    for (const key of ['altTextId', 'captionTextId', 'audioDescriptionTextId']) {
      if (accessibility[key] !== undefined) {
        requireTextReference(catalog, accessibility[key], referenceOrigin(entry.origin, `/accessibility/${key}`));
      }
    }
    if (!accessibility.decorative && asset.kind === 'image') {
      fieldReference(asset, 'altTextId', entry.origin);
    }
    if (!accessibility.decorative && asset.kind === 'audio') {
      fieldReference(asset, 'captionTextId', entry.origin);
      fieldReference(asset, 'audioDescriptionTextId', entry.origin);
    }
    const variantIds = new Set();
    for (const [index, variant] of ownArrayField(asset, 'variants', entry.origin).entries()) {
      if (!variant || typeof variant !== 'object' || Array.isArray(variant)
        || typeof variant.id !== 'string' || !variant.id || typeof variant.file !== 'string' || !variant.file) {
        throw resolverError(ResolverErrorCode.InvalidPack, `${asset.id}.variants/${index} is invalid.`, entry.origin, { sourcePointer: `${entry.origin.sourcePointer}/variants/${index}` });
      }
      if (variantIds.has(variant.id)) {
        throw resolverError(ResolverErrorCode.InvalidPack, `${asset.id} has duplicate variant ${variant.id}.`, entry.origin, { sourcePointer: `${entry.origin.sourcePointer}/variants/${index}` });
      }
      variantIds.add(variant.id);
    }
  }
  ensureReferences(catalog, catalog.category('characters').values(), 'assetIds', 'assets');
  ensureReferences(catalog, catalog.category('scenes').values(), 'assetRefs', 'assets');
  ensureReferences(catalog, catalog.category('documents').values(), 'assetRefs', 'assets');
  ensureReferences(catalog, catalog.category('capabilities').values(), 'requiredAssets', 'assets');
  ensureReferences(catalog, catalog.category('capabilities').values(), 'requiredAudio', 'assets', 'audio');
  for (const entry of catalog.category('scenes').values()) {
    ensureReferences(catalog, [entry], 'textRefs', 'texts');
  }
  for (const entry of catalog.category('dialogues').values()) {
    const nodes = ownArrayField(entry.value, 'nodes', entry.origin);
    for (const [nodeIndex, node] of nodes.entries()) {
      const nodeOrigin = referenceOrigin(entry.origin, `/nodes/${nodeIndex}`);
      requireTextReference(catalog, ownStringField(node, 'textId', nodeOrigin), referenceOrigin(nodeOrigin, '/textId'));
      for (const [choiceIndex, choice] of ownArrayField(node, 'choices', nodeOrigin).entries()) {
        const choiceOrigin = referenceOrigin(nodeOrigin, `/choices/${choiceIndex}`);
        requireTextReference(catalog, ownStringField(choice, 'textId', choiceOrigin), referenceOrigin(choiceOrigin, '/textId'));
      }
    }
  }
  for (const entry of catalog.category('quests').values()) {
    requireTextReference(catalog, ownStringField(entry.value, 'titleTextId', entry.origin), referenceOrigin(entry.origin, '/titleTextId'));
    for (const [stageIndex, stage] of ownArrayField(entry.value, 'stages', entry.origin).entries()) {
      const stageOrigin = referenceOrigin(entry.origin, `/stages/${stageIndex}`);
      for (const [objectiveIndex, objective] of ownArrayField(stage, 'objectives', stageOrigin).entries()) {
        const objectiveOrigin = referenceOrigin(stageOrigin, `/objectives/${objectiveIndex}`);
        requireTextReference(catalog, ownStringField(objective, 'titleTextId', objectiveOrigin), referenceOrigin(objectiveOrigin, '/titleTextId'));
      }
    }
  }
  return Object.freeze({
    assetIds: catalog.ids('assets'),
    textIds: catalog.ids('texts'),
    vocabularyIds: catalog.ids('vocabulary'),
  });
}

export class AssetResolver {
  #catalog;
  #resolveFileUrl;

  constructor(pack, { resolveFileUrl = (file) => file, validate = true } = {}) {
    this.#catalog = createResolverCatalog(pack);
    if (typeof resolveFileUrl !== 'function') throw resolverError(ResolverErrorCode.InvalidPack, 'resolveFileUrl must be a function.');
    this.#resolveFileUrl = resolveFileUrl;
    if (validate) validateManifestClosure(pack);
  }

  ids() { return this.#catalog.ids('assets'); }

  resolve(assetRef, variant = undefined) {
    const entry = requireCatalogEntry(this.#catalog, 'assets', assetRef, absentOrigin('assets', assetRef));
    const asset = entry.value;
    const selector = normalizeVariant(variant);
    const options = [{ id: null, file: ownStringField(asset, 'file', entry.origin) }, ...ownArrayField(asset, 'variants', entry.origin)];
    let selected;
    if (selector.id !== undefined) selected = options.find((option) => option.id === selector.id);
    else if (selector.seed !== undefined || selector.state !== undefined) {
      const index = hashText(canonicalJson({ assetId: asset.id, seed: selector.seed ?? null, state: selector.state ?? null }, 'Asset variant selector')) % options.length;
      selected = options[index];
    } else selected = options[0];
    if (!selected) {
      throw resolverError(ResolverErrorCode.MissingVariant, `Asset ${assetRef} has no variant ${selector.id}.`, entry.origin, { assetId: assetRef, variantId: selector.id });
    }
    let url;
    try { url = this.#resolveFileUrl(selected.file, Object.freeze({ assetId: asset.id, variantId: selected.id, moduleId: entry.origin.moduleId })); } catch (error) {
      throw resolverError(ResolverErrorCode.InvalidResolvedUrl, error.message, entry.origin, { assetId: asset.id, variantId: selected.id });
    }
    if (typeof url !== 'string' || !url) {
      throw resolverError(ResolverErrorCode.InvalidResolvedUrl, `Asset URL for ${asset.id} must be a non-empty string.`, entry.origin, { assetId: asset.id, variantId: selected.id });
    }
    return deepFreeze({
      assetId: asset.id,
      kind: ownStringField(asset, 'kind', entry.origin),
      mediaType: ownStringField(asset, 'mediaType', entry.origin),
      url,
      variantId: selected.id,
      sha256: asset.sha256,
      accessibility: accessibilityFor(asset, entry.origin),
    });
  }
}

export class TextResolver {
  #catalog;

  constructor(pack, { validate = true } = {}) {
    this.#catalog = createResolverCatalog(pack);
    if (validate) validateManifestClosure(pack);
  }

  ids() { return this.#catalog.ids('texts'); }

  resolve(textRef, locale = undefined, variables = undefined) {
    const entry = requireCatalogEntry(this.#catalog, 'texts', textRef, absentOrigin('texts', textRef));
    const localized = localizedValue(entry.value.value, locale, entry.origin);
    const segments = outputTextSegments(localized.text, normalizedVariables(variables, entry.origin), this.#catalog, locale, entry.origin);
    return deepFreeze({
      textId: entry.value.id,
      purpose: ownStringField(entry.value, 'purpose', entry.origin),
      requestedLocale: locale ?? 'default',
      resolvedLocale: localized.resolvedLocale,
      text: resolvedText(segments),
      segments,
    });
  }
}
