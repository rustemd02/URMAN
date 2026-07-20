const SYSTEM_MODULE_ID = 'urman.compiler';

export const DiagnosticCode = Object.freeze({
  MissingCampaignSelection: 'MissingCampaignSelection',
  MissingModuleSelection: 'MissingModuleSelection',
  MissingFile: 'MissingFile',
  InvalidJson: 'InvalidJson',
  InvalidFrontmatter: 'InvalidFrontmatter',
  InvalidSchema: 'InvalidSchema',
  DuplicateId: 'DuplicateId',
  DuplicateModule: 'DuplicateModule',
  DuplicateCapabilityProvider: 'DuplicateCapabilityProvider',
  MissingModule: 'MissingModule',
  IncompatibleExactVersion: 'IncompatibleExactVersion',
  UndeclaredDependency: 'UndeclaredDependency',
  UnresolvedReference: 'UnresolvedReference',
  MissingAsset: 'MissingAsset',
  MissingRoleBinding: 'MissingRoleBinding',
  MissingEntrypoint: 'MissingEntrypoint',
  MissingCapability: 'MissingCapability',
  UnknownOpcode: 'UnknownOpcode',
  DependencyCycle: 'DependencyCycle',
  UnreachableRequiredEntry: 'UnreachableRequiredEntry',
  NarrativeInvariantViolation: 'NarrativeInvariantViolation',
  ResourceClaimConflict: 'ResourceClaimConflict',
  UnknownDialogueTarget: 'UnknownDialogueTarget',
  ConflictingInteractionTarget: 'ConflictingInteractionTarget',
  UnknownModuleSelection: 'UnknownModuleSelection',
});

export function errorDiagnostic(code, {
  moduleId = SYSTEM_MODULE_ID,
  sourcePath = 'content',
  jsonPointer = '',
  message,
} = {}) {
  return Object.freeze({
    code,
    severity: 'error',
    moduleId,
    sourcePath,
    jsonPointer,
    message: message ?? code,
  });
}

export function failure(...diagnostics) {
  if (diagnostics.length === 0 || !diagnostics.some(({ severity }) => severity === 'error')) {
    throw new TypeError('A failed ContentCompilationResult requires at least one error diagnostic.');
  }

  const ordered = [...diagnostics].sort((left, right) => [
    left.sourcePath,
    left.jsonPointer,
    left.code,
    left.moduleId,
    left.message,
  ].join('\0').localeCompare([
    right.sourcePath,
    right.jsonPointer,
    right.code,
    right.moduleId,
    right.message,
  ].join('\0')));
  return Object.freeze({ ok: false, diagnostics: Object.freeze(ordered) });
}

export function success(pack, diagnostics = []) {
  if (diagnostics.some(({ severity }) => severity === 'error')) {
    throw new TypeError('A successful ContentCompilationResult cannot contain error diagnostics.');
  }

  return Object.freeze({
    ok: true,
    pack,
    diagnostics: Object.freeze([...diagnostics]),
  });
}
