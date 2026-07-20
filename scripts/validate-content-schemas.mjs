import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const SCHEMA_DIR = path.join(ROOT, 'content', 'schemas');
const DRAFT = 'https://json-schema.org/draft/2020-12/schema';
const SCHEMA_ID_BASE = 'https://schemas.urman.game/content/v1/';
const FORBIDDEN_CONTENT_KEYS = new Set([
  'dom',
  'css',
  'cssclass',
  'cssclassname',
  'viteurl',
  'vitepath',
  'typescriptmodulepath',
  'tsmodulepath',
  'classname',
  'renderer',
  'renderername',
  'executableexpression',
  'expression',
  'eval',
  'script',
  'function',
]);
const SUPPORTED_SCHEMA_KEYWORDS = new Set([
  '$schema', '$id', '$defs', '$ref', '$comment',
  'schemaVersion', 'title', 'description', 'default', 'examples',
  'type', 'const', 'enum', 'pattern', 'minLength', 'maxLength',
  'minimum', 'maximum', 'minItems', 'maxItems', 'uniqueItems', 'items',
  'contains', 'minContains', 'maxContains', 'minProperties', 'required',
  'properties', 'additionalProperties', 'propertyNames',
  'oneOf', 'anyOf', 'allOf', 'not',
]);
const JSON_TYPES = new Set(['null', 'boolean', 'object', 'array', 'number', 'integer', 'string']);
const DISCRIMINATOR_KEYS = ['op', 'ok', 'type', 'sceneType'];

function pointerToken(value) {
  return value.replaceAll('~', '~0').replaceAll('/', '~1');
}

function hasOwn(object, key) {
  return Object.hasOwn(object, key);
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isSchemaValue(value) {
  return typeof value === 'boolean' || isPlainObject(value);
}

function nonNegativeIntegerMessage(value) {
  return Number.isInteger(value) && value >= 0 ? null : 'must be a non-negative integer';
}

function finiteNumberMessage(value) {
  return typeof value === 'number' && Number.isFinite(value) ? null : 'must be a finite number';
}

function schemaArrayMessage(value) {
  return Array.isArray(value) && value.length > 0 && value.every(isSchemaValue)
    ? null
    : 'must be a non-empty array of schemas';
}

const KEYWORD_SHAPE_RULES = new Map([
  ['$schema', (value) => typeof value === 'string' && value.length > 0 ? null : 'must be a non-empty string'],
  ['$id', (value) => typeof value === 'string' && value.length > 0 ? null : 'must be a non-empty string'],
  ['$ref', (value) => typeof value === 'string' && value.length > 0 ? null : 'must be a non-empty string'],
  ['$comment', (value) => typeof value === 'string' ? null : 'must be a string'],
  ['schemaVersion', (value) => Number.isInteger(value) && value >= 1 ? null : 'must be a positive integer'],
  ['title', (value) => typeof value === 'string' ? null : 'must be a string'],
  ['description', (value) => typeof value === 'string' ? null : 'must be a string'],
  ['examples', (value) => Array.isArray(value) ? null : 'must be an array'],
  ['$defs', (value) => isPlainObject(value) ? null : 'must be an object of schemas'],
  ['properties', (value) => isPlainObject(value) ? null : 'must be an object of schemas'],
  ['type', (value) => {
    const types = Array.isArray(value) ? value : [value];
    return types.length > 0 && types.every((type) => JSON_TYPES.has(type)) && new Set(types).size === types.length
      ? null
      : 'must contain unique supported JSON type names';
  }],
  ['enum', (value) => Array.isArray(value) && value.length > 0 && new Set(value.map(canonicalJson)).size === value.length
    ? null
    : 'must be a non-empty array of structurally unique values'],
  ['pattern', (value) => {
    if (typeof value !== 'string') return 'must be a string';
    try {
      new RegExp(value, 'u');
      return null;
    } catch {
      return 'must be a valid regular expression';
    }
  }],
  ['minLength', nonNegativeIntegerMessage],
  ['maxLength', nonNegativeIntegerMessage],
  ['minimum', finiteNumberMessage],
  ['maximum', finiteNumberMessage],
  ['minItems', nonNegativeIntegerMessage],
  ['maxItems', nonNegativeIntegerMessage],
  ['uniqueItems', (value) => typeof value === 'boolean' ? null : 'must be a boolean'],
  ['items', (value) => isSchemaValue(value) ? null : 'must be a schema object or boolean'],
  ['contains', (value) => isSchemaValue(value) ? null : 'must be a schema object or boolean'],
  ['minContains', nonNegativeIntegerMessage],
  ['maxContains', nonNegativeIntegerMessage],
  ['minProperties', nonNegativeIntegerMessage],
  ['required', (value) => Array.isArray(value) && value.every((key) => typeof key === 'string') && new Set(value).size === value.length
    ? null
    : 'must be an array of unique strings'],
  ['additionalProperties', (value) => isSchemaValue(value) ? null : 'must be a schema object or boolean'],
  ['propertyNames', (value) => isSchemaValue(value) ? null : 'must be a schema object or boolean'],
  ['oneOf', schemaArrayMessage],
  ['anyOf', schemaArrayMessage],
  ['allOf', schemaArrayMessage],
  ['not', (value) => isSchemaValue(value) ? null : 'must be a schema object or boolean'],
]);

async function schemaFiles(directory = SCHEMA_DIR) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(entries.map((entry) => {
    const target = path.join(directory, entry.name);
    return entry.isDirectory() ? schemaFiles(target) : [target];
  }));
  return nested.flat().filter((file) => file.endsWith('.schema.json')).sort();
}

export async function loadSchemaRegistry(directory = SCHEMA_DIR) {
  const registry = new Map();
  for (const file of await schemaFiles(directory)) {
    const schema = JSON.parse(await readFile(file, 'utf8'));
    registry.set(path.resolve(file), schema);
  }
  return registry;
}

function auditSchemaNode(node, file, errors, inspect, pointer = '') {
  if (typeof node === 'boolean') return;
  if (!node || typeof node !== 'object' || Array.isArray(node)) {
    errors.push(`${file}${pointer || '/'}: schema node must be an object or boolean`);
    return;
  }
  inspect(node, pointer);
  for (const key of Object.keys(node)) {
    if (!SUPPORTED_SCHEMA_KEYWORDS.has(key)) {
      errors.push(`${file}${pointer || '/'}: unsupported JSON Schema keyword "${key}"`);
    }
  }
  for (const [key, value] of Object.entries(node)) {
    const rule = KEYWORD_SHAPE_RULES.get(key);
    const message = rule?.(value);
    if (message) errors.push(`${file}${pointer}/${pointerToken(key)}: ${key} ${message}`);
  }
  for (const [minimumKey, maximumKey] of [
    ['minLength', 'maxLength'],
    ['minimum', 'maximum'],
    ['minItems', 'maxItems'],
    ['minContains', 'maxContains'],
  ]) {
    if (hasOwn(node, minimumKey) && hasOwn(node, maximumKey)
      && typeof node[minimumKey] === 'number' && typeof node[maximumKey] === 'number'
      && node[minimumKey] > node[maximumKey]) {
      errors.push(`${file}${pointer || '/'}: ${minimumKey} must be less than or equal to ${maximumKey}`);
    }
  }
  for (const key of ['$defs', 'properties']) {
    if (!hasOwn(node, key)) continue;
    if (!node[key] || typeof node[key] !== 'object' || Array.isArray(node[key])) {
      errors.push(`${file}${pointer}/${pointerToken(key)}: ${key} must be an object`);
      continue;
    }
    for (const [name, child] of Object.entries(node[key])) {
      auditSchemaNode(child, file, errors, inspect, `${pointer}/${pointerToken(key)}/${pointerToken(name)}`);
    }
  }
  for (const key of ['items', 'contains', 'additionalProperties', 'propertyNames', 'not']) {
    if (hasOwn(node, key)) auditSchemaNode(node[key], file, errors, inspect, `${pointer}/${pointerToken(key)}`);
  }
  for (const key of ['oneOf', 'anyOf', 'allOf']) {
    if (!hasOwn(node, key)) continue;
    if (!Array.isArray(node[key]) || node[key].length === 0) {
      errors.push(`${file}${pointer}/${pointerToken(key)}: ${key} must be a non-empty array`);
      continue;
    }
    node[key].forEach((child, index) => auditSchemaNode(child, file, errors, inspect, `${pointer}/${pointerToken(key)}/${index}`));
  }
}

function resolvePointer(document, fragment, label) {
  if (!fragment) return document;
  if (!fragment.startsWith('/')) throw new Error(`${label}: $ref fragment must be a JSON Pointer`);
  return fragment.slice(1).split('/').reduce((current, rawToken) => {
    const token = decodeURIComponent(rawToken).replaceAll('~1', '/').replaceAll('~0', '~');
    if (!current || typeof current !== 'object' || !hasOwn(current, token)) {
      throw new Error(`${label}: unresolved JSON Pointer #${fragment}`);
    }
    return current[token];
  }, document);
}

function resolveRef(currentFile, ref, registry, directory) {
  if (typeof ref !== 'string' || ref.length === 0) throw new Error(`${currentFile}: invalid empty $ref`);
  const current = registry.get(currentFile);
  if (!current) throw new Error(`${currentFile}: schema is not registered`);
  if (!hasOwn(current, '$id')) throw new Error(`${currentFile}: schema has no own $id`);
  const baseUri = new URL(current.$id, pathToFileURL(currentFile));
  const resolvedUri = new URL(ref, baseUri);
  const fragment = resolvedUri.hash.slice(1);
  resolvedUri.hash = '';
  const targetEntry = [...registry.entries()].find(([file, schema]) => {
    if (!hasOwn(schema, '$id')) return false;
    const schemaUri = new URL(schema.$id, pathToFileURL(file));
    schemaUri.hash = '';
    return schemaUri.href === resolvedUri.href;
  });
  if (!targetEntry) throw new Error(`${currentFile}: missing local $ref target ${ref} resolved as ${resolvedUri.href}`);
  const [targetFile, target] = targetEntry;
  return {
    file: targetFile,
    id: resolvedUri.href,
    fragment,
    schema: resolvePointer(target, fragment, `${currentFile}: ${ref}`),
  };
}

export async function resolveSchemaReference(schemaFile, ref, directory = SCHEMA_DIR) {
  const registry = await loadSchemaRegistry(directory);
  const absolute = path.resolve(directory, schemaFile);
  const resolved = resolveRef(absolute, ref, registry, directory);
  return { schemaFile: path.relative(directory, resolved.file), schemaId: resolved.id, fragment: resolved.fragment };
}

export async function auditSchemas(directory = SCHEMA_DIR) {
  const registry = await loadSchemaRegistry(directory);
  const errors = [];
  const ids = new Map();
  for (const [file, schema] of registry) {
    if (!hasOwn(schema, '$schema') || schema.$schema !== DRAFT) errors.push(`${file}: expected draft 2020-12`);
    if (!hasOwn(schema, 'schemaVersion') || schema.schemaVersion !== 1) errors.push(`${file}: expected schemaVersion 1`);
    const relativeSchemaPath = path.relative(directory, file).split(path.sep).join('/');
    const expectedId = `${SCHEMA_ID_BASE}${relativeSchemaPath}`;
    if (!hasOwn(schema, '$id') || typeof schema.$id !== 'string') errors.push(`${file}: missing stable $id`);
    else if (schema.$id !== expectedId) errors.push(`${file}: expected hierarchical $id ${expectedId}`);
    else if (ids.has(schema.$id)) errors.push(`${file}: duplicate $id ${schema.$id}`);
    else ids.set(schema.$id, file);
    auditSchemaNode(schema, file, errors, (node, pointer) => {
      if (hasOwn(node, '$ref') && typeof node.$ref === 'string') {
        try {
          resolveRef(file, node.$ref, registry, directory);
        } catch (error) {
          errors.push(`${file}${pointer || '/'}: ${error.message}`);
        }
      }
      if (hasOwn(node, 'properties') && node.properties && typeof node.properties === 'object' && !Array.isArray(node.properties)) {
        for (const key of Object.keys(node.properties)) {
          if (FORBIDDEN_CONTENT_KEYS.has(key.toLowerCase())) {
            errors.push(`${file}${pointer}/properties: forbidden web/executable content key ${key}`);
          }
        }
      }
    });
  }
  return { files: [...registry.keys()], registry, errors };
}

function canonicalJson(value) {
  if (value === null || typeof value !== 'object') return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(',')}]`;
  return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonicalJson(value[key])}`).join(',')}}`;
}

function equal(left, right) {
  return canonicalJson(left) === canonicalJson(right);
}

function pointerDepth(error) {
  const pointer = error.slice(0, error.indexOf(':'));
  return pointer.split('/').filter(Boolean).length;
}

function schemaForDiscriminatorInspection(schema, currentFile, registry, directory) {
  if (!isPlainObject(schema)) return schema;
  if (!hasOwn(schema, '$ref') || typeof schema.$ref !== 'string') return schema;
  const target = resolveRef(currentFile, schema.$ref, registry, directory);
  return schemaForDiscriminatorInspection(target.schema, target.file, registry, directory);
}

function matchesPresentDiscriminator(schema, value, currentFile, registry, directory) {
  if (!isPlainObject(value)) return false;
  const inspected = schemaForDiscriminatorInspection(schema, currentFile, registry, directory);
  if (!isPlainObject(inspected) || !hasOwn(inspected, 'properties') || !isPlainObject(inspected.properties)) return false;
  let matched = false;
  for (const key of DISCRIMINATOR_KEYS) {
    if (!hasOwn(value, key) || !hasOwn(inspected.properties, key)) continue;
    const propertySchema = schemaForDiscriminatorInspection(inspected.properties[key], currentFile, registry, directory);
    if (!isPlainObject(propertySchema) || !hasOwn(propertySchema, 'const')) continue;
    if (!equal(propertySchema.const, value[key])) return false;
    matched = true;
  }
  return matched;
}

function bestUnionCause(branchResults, branches, value, currentFile, registry, directory) {
  const discriminatorMatches = branches
    .map((branch, index) => matchesPresentDiscriminator(branch, value, currentFile, registry, directory) ? index : -1)
    .filter((index) => index >= 0);
  const candidates = (discriminatorMatches.length > 0 ? discriminatorMatches : branchResults.map((_, index) => index))
    .map((index) => ({ index, errors: branchResults[index] }))
    .filter((candidate) => candidate.errors.length > 0)
    .sort((left, right) => left.errors.length - right.errors.length || left.index - right.index);
  const likelyBranch = candidates[0]?.errors;
  if (!likelyBranch) return null;
  return [...likelyBranch].sort((left, right) => pointerDepth(right) - pointerDepth(left))[0];
}

function matchesType(type, value) {
  if (type === 'null') return value === null;
  if (type === 'array') return Array.isArray(value);
  if (type === 'object') return value !== null && typeof value === 'object' && !Array.isArray(value);
  if (type === 'integer') return Number.isInteger(value);
  if (type === 'number') return typeof value === 'number' && Number.isFinite(value);
  return typeof value === type;
}

function validateNode(schema, value, currentFile, registry, directory, pointer, errors) {
  if (typeof schema === 'boolean') {
    if (!schema) errors.push(`${pointer || '/'}: rejected by false schema`);
    return;
  }
  if (hasOwn(schema, '$ref')) {
    const target = resolveRef(currentFile, schema.$ref, registry, directory);
    validateNode(target.schema, value, target.file, registry, directory, pointer, errors);
  }
  if (hasOwn(schema, 'allOf')) {
    for (const branch of schema.allOf) validateNode(branch, value, currentFile, registry, directory, pointer, errors);
  }
  if (hasOwn(schema, 'anyOf')) {
    const branchResults = schema.anyOf.map((branch) => {
      const branchErrors = [];
      validateNode(branch, value, currentFile, registry, directory, pointer, branchErrors);
      return branchErrors;
    });
    if (!branchResults.some((branchErrors) => branchErrors.length === 0)) {
      const cause = bestUnionCause(branchResults, schema.anyOf, value, currentFile, registry, directory);
      errors.push(cause ? `${cause} [anyOf: no branch matched]` : `${pointer || '/'}: anyOf has no matching branch`);
    }
  }
  if (hasOwn(schema, 'oneOf')) {
    const branchResults = schema.oneOf.map((branch) => {
      const branchErrors = [];
      validateNode(branch, value, currentFile, registry, directory, pointer, branchErrors);
      return branchErrors;
    });
    const matches = branchResults.filter((branchErrors) => branchErrors.length === 0).length;
    if (matches === 0) {
      const cause = bestUnionCause(branchResults, schema.oneOf, value, currentFile, registry, directory);
      errors.push(cause ? `${cause} [oneOf: no branch matched]` : `${pointer || '/'}: oneOf has no matching branch`);
    } else if (matches > 1) {
      errors.push(`${pointer || '/'}: value must match exactly one allowed schema (matched ${matches})`);
    }
  }
  if (hasOwn(schema, 'not')) {
    const branchErrors = [];
    validateNode(schema.not, value, currentFile, registry, directory, pointer, branchErrors);
    if (branchErrors.length === 0) errors.push(`${pointer || '/'}: value matches forbidden schema`);
  }
  if (hasOwn(schema, 'type')) {
    const types = Array.isArray(schema.type) ? schema.type : [schema.type];
    if (!types.some((type) => matchesType(type, value))) {
      errors.push(`${pointer || '/'}: expected ${types.join(' or ')}`);
      return;
    }
  }
  if (hasOwn(schema, 'const') && !equal(schema.const, value)) errors.push(`${pointer || '/'}: expected constant ${JSON.stringify(schema.const)}`);
  if (hasOwn(schema, 'enum') && !schema.enum.some((candidate) => equal(candidate, value))) errors.push(`${pointer || '/'}: value is not in enum`);

  if (typeof value === 'string') {
    const codePointLength = [...value].length;
    if (hasOwn(schema, 'minLength') && codePointLength < schema.minLength) errors.push(`${pointer || '/'}: string is too short`);
    if (hasOwn(schema, 'maxLength') && codePointLength > schema.maxLength) errors.push(`${pointer || '/'}: string is too long`);
    if (hasOwn(schema, 'pattern') && !new RegExp(schema.pattern, 'u').test(value)) errors.push(`${pointer || '/'}: string does not match ${schema.pattern}`);
  }
  if (typeof value === 'number') {
    if (hasOwn(schema, 'minimum') && value < schema.minimum) errors.push(`${pointer || '/'}: number is below minimum`);
    if (hasOwn(schema, 'maximum') && value > schema.maximum) errors.push(`${pointer || '/'}: number is above maximum`);
  }
  if (Array.isArray(value)) {
    if (hasOwn(schema, 'minItems') && value.length < schema.minItems) errors.push(`${pointer || '/'}: array has too few items`);
    if (hasOwn(schema, 'maxItems') && value.length > schema.maxItems) errors.push(`${pointer || '/'}: array has too many items`);
    if (hasOwn(schema, 'uniqueItems') && schema.uniqueItems && new Set(value.map(canonicalJson)).size !== value.length) errors.push(`${pointer || '/'}: array items must be unique`);
    if (hasOwn(schema, 'items')) {
      value.forEach((item, index) => validateNode(schema.items, item, currentFile, registry, directory, `${pointer}/${index}`, errors));
    }
    if (hasOwn(schema, 'contains')) {
      const matches = value.filter((item, index) => {
        const branchErrors = [];
        validateNode(schema.contains, item, currentFile, registry, directory, `${pointer}/${index}`, branchErrors);
        return branchErrors.length === 0;
      }).length;
      const minimum = hasOwn(schema, 'minContains') ? schema.minContains : 1;
      const maximum = hasOwn(schema, 'maxContains') ? schema.maxContains : Number.POSITIVE_INFINITY;
      if (matches < minimum || matches > maximum) errors.push(`${pointer || '/'}: contains matched ${matches}, expected ${minimum}..${maximum}`);
    }
  }
  if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
    if (hasOwn(schema, 'minProperties') && Object.keys(value).length < schema.minProperties) errors.push(`${pointer || '/'}: object has too few properties`);
    for (const key of hasOwn(schema, 'required') ? schema.required : []) {
      if (!hasOwn(value, key)) errors.push(`${pointer || '/'}: missing required property ${key}`);
    }
    for (const [key, child] of Object.entries(value)) {
      const childPointer = `${pointer}/${pointerToken(key)}`;
      if (hasOwn(schema, 'properties') && hasOwn(schema.properties, key)) {
        validateNode(schema.properties[key], child, currentFile, registry, directory, childPointer, errors);
      } else if (hasOwn(schema, 'additionalProperties') && schema.additionalProperties === false) {
        errors.push(`${childPointer}: additional property is not allowed`);
      } else if (hasOwn(schema, 'additionalProperties')) {
        validateNode(schema.additionalProperties, child, currentFile, registry, directory, childPointer, errors);
      }
      if (hasOwn(schema, 'propertyNames')) validateNode(schema.propertyNames, key, currentFile, registry, directory, childPointer, errors);
    }
  }
}

export async function validateDocument(schemaFile, value, directory = SCHEMA_DIR) {
  const audit = await auditSchemas(directory);
  if (audit.errors.length) throw new Error(`Schema bootstrap failed:\n${audit.errors.join('\n')}`);
  const registry = audit.registry;
  const absolute = path.resolve(directory, schemaFile);
  if (!registry.has(absolute)) throw new Error(`Unknown schema: ${schemaFile}`);
  const errors = [];
  validateNode(registry.get(absolute), value, absolute, registry, directory, '', errors);
  return { valid: errors.length === 0, errors };
}

async function main() {
  const result = await auditSchemas();
  if (result.errors.length) {
    for (const error of result.errors) console.error(error);
    process.exitCode = 1;
    return;
  }
  console.log(`Validated ${result.files.length} portable content schemas.`);
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url) {
  await main();
}
