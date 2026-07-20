export const ResolverErrorCode = Object.freeze({
  InvalidPack: 'InvalidPack',
  DuplicateLogicalId: 'DuplicateLogicalId',
  MissingAsset: 'MissingAsset',
  MissingText: 'MissingText',
  MissingVocabulary: 'MissingVocabulary',
  MissingVariant: 'MissingVariant',
  MissingVariable: 'MissingVariable',
  InvalidVariable: 'InvalidVariable',
  InvalidLocale: 'InvalidLocale',
  InvalidResolvedUrl: 'InvalidResolvedUrl',
  InvalidAccessibility: 'InvalidAccessibility',
  WrongAssetKind: 'WrongAssetKind',
});

export class ResolverError extends Error {
  constructor(code, message, details = {}) {
    super(message);
    this.name = 'ResolverError';
    this.code = code;
    this.details = Object.freeze({ ...details });
  }
}

export function moduleIdFromLogicalId(id) {
  if (typeof id !== 'string') return null;
  const separator = id.indexOf(':');
  if (separator <= 0 || id.indexOf('/', separator) < 0) return null;
  return id.slice(0, separator);
}

export function originFor(category, index, id, suffix = '') {
  return Object.freeze({
    id,
    moduleId: moduleIdFromLogicalId(id) ?? 'unknown-module',
    sourcePointer: `/registries/${category}/${index}${suffix}`,
  });
}

export function resolverError(code, message, origin = {}, extra = {}) {
  return new ResolverError(code, message, { ...origin, ...extra });
}
