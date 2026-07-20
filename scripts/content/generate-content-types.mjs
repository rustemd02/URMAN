#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { loadSchemaRegistry, SCHEMA_DIR } from '../validate-content-schemas.mjs';

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(SCRIPT_DIR, '..', '..');
const OUTPUT = path.join(ROOT, 'src', 'content', 'generated', 'content-types.ts');
const ROOT_NAMES = Object.freeze({
  'asset.schema.json': 'AssetDefinition',
  'campaign-manifest.schema.json': 'CampaignManifest',
  'capability.schema.json': 'CapabilityManifest',
  'character.schema.json': 'CharacterDefinition',
  'compiled-content-pack.schema.json': 'CompiledContentPack',
  'content-compilation-result.schema.json': 'ContentCompilationResult',
  'dialogue.schema.json': 'DialogueDefinition',
  'document.schema.json': 'DocumentDefinition',
  'fixture.schema.json': 'ContentFixture',
  'knowledge.schema.json': 'KnowledgeDefinition',
  'module-manifest.schema.json': 'ContentModuleManifest',
  'quest.schema.json': 'QuestDefinition',
  'scene.schema.json': 'SceneDefinition',
  'text.schema.json': 'TextDefinition',
  'vocabulary.schema.json': 'VocabularyDefinition',
});

function pascal(value) {
  return value.replace(/\.schema\.json$/, '').split(/[^A-Za-z0-9]+/).filter(Boolean)
    .map((part) => `${part[0].toUpperCase()}${part.slice(1)}`).join('');
}

function propertyName(value) {
  return /^[A-Za-z_$][A-Za-z0-9_$]*$/.test(value) ? value : JSON.stringify(value);
}

function literal(value) {
  if (value === null) return 'null';
  if (typeof value === 'string') return JSON.stringify(value);
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return 'JsonValue';
}

function targetForRef(currentFile, ref) {
  const [filePart, fragment = ''] = ref.split('#');
  const targetFile = filePart ? path.resolve(path.dirname(currentFile), filePart) : currentFile;
  const tokens = fragment.replace(/^\//, '').split('/').filter(Boolean).map((token) => token.replaceAll('~1', '/').replaceAll('~0', '~'));
  return { targetFile, definition: tokens[0] === '$defs' ? tokens[1] : null };
}

function createSymbols(registry) {
  const symbols = new Map();
  for (const [file, schema] of registry) {
    const base = path.basename(file);
    if (ROOT_NAMES[base]) symbols.set(`${file}#`, ROOT_NAMES[base]);
    for (const name of Object.keys(schema.$defs ?? {}).sort()) {
      symbols.set(`${file}#${name}`, `${pascal(base)}${name}`);
    }
  }
  return symbols;
}

function schemaToType(schema, currentFile, symbols) {
  if (schema === true) return 'unknown';
  if (schema === false) return 'never';
  if (schema.$ref) {
    const target = targetForRef(currentFile, schema.$ref);
    return symbols.get(`${target.targetFile}#${target.definition ?? ''}`) ?? 'unknown';
  }
  if (Object.hasOwn(schema, 'const')) return literal(schema.const);
  if (schema.enum) return schema.enum.map(literal).join(' | ');
  if (schema.oneOf || schema.anyOf) return (schema.oneOf ?? schema.anyOf).map((branch) => `(${schemaToType(branch, currentFile, symbols)})`).join(' | ');
  if (schema.allOf) {
    const { allOf, ...siblings } = schema;
    const ownType = schemaToType(siblings, currentFile, symbols);
    const types = [
      ...allOf.map((branch) => schemaToType(branch, currentFile, symbols)),
      ownType,
    ].filter((type) => type !== 'unknown');
    return types.length ? types.map((type) => `(${type})`).join(' & ') : 'unknown';
  }
  if (Array.isArray(schema.type)) return schema.type.map((type) => schemaToType({ ...schema, type }, currentFile, symbols)).join(' | ');
  if (schema.type === 'array') return `readonly (${schemaToType(schema.items ?? true, currentFile, symbols)})[]`;
  if (schema.type === 'object' || schema.properties || Object.hasOwn(schema, 'additionalProperties')) {
    const required = new Set(schema.required ?? []);
    const fields = Object.entries(schema.properties ?? {}).sort(([left], [right]) => left.localeCompare(right))
      .map(([name, child]) => `readonly ${propertyName(name)}${required.has(name) ? '' : '?'}: ${schemaToType(child, currentFile, symbols)};`);
    let shape = fields.length ? `{ ${fields.join(' ')} }` : '{}';
    if (schema.additionalProperties && schema.additionalProperties !== true) {
      const valueType = schemaToType(schema.additionalProperties, currentFile, symbols);
      shape = fields.length ? `${shape} & Readonly<Record<string, ${valueType}>>` : `{ readonly [key: string]: ${valueType} }`;
    } else if (schema.additionalProperties === true) {
      shape = `${shape} & Readonly<Record<string, unknown>>`;
    }
    return shape;
  }
  if (schema.type === 'null') return 'null';
  if (schema.type === 'string' || (!schema.type && (schema.pattern || schema.minLength !== undefined))) return 'string';
  if (schema.type === 'integer' || schema.type === 'number') return 'number';
  if (schema.type === 'boolean') return 'boolean';
  return 'unknown';
}

export async function generateContentTypes() {
  const registry = await loadSchemaRegistry(SCHEMA_DIR);
  const symbols = createSymbols(registry);
  const sourceHash = createHash('sha256');
  for (const [file, schema] of [...registry.entries()].sort(([left], [right]) => left.localeCompare(right))) {
    sourceHash.update(path.basename(file));
    sourceHash.update(JSON.stringify(schema));
  }
  const declarations = [];
  for (const [file, schema] of [...registry.entries()].sort(([left], [right]) => left.localeCompare(right))) {
    for (const [name, definition] of Object.entries(schema.$defs ?? {}).sort(([left], [right]) => left.localeCompare(right))) {
      declarations.push(`export type ${symbols.get(`${file}#${name}`)} = ${schemaToType(definition, file, symbols)};`);
    }
    const rootName = symbols.get(`${file}#`);
    if (rootName) declarations.push(`export type ${rootName} = ${schemaToType(schema, file, symbols)};`);
  }
  return [
    '// GENERATED FILE — DO NOT EDIT.',
    `// Portable schema SHA-256: ${sourceHash.digest('hex')}`,
    '// Source: content/schemas/*.schema.json (lexical order).',
    '',
    ...declarations,
    '',
  ].join('\n');
}

export async function runTypeGenerator({ check = false, outputPath = OUTPUT } = {}) {
  const generated = await generateContentTypes();
  if (check) {
    let current = '';
    try { current = await readFile(outputPath, 'utf8'); } catch { /* A missing generated file is drift. */ }
    if (current !== generated) throw new Error(`Generated content types are stale: ${path.relative(ROOT, outputPath)}. Run npm run content:types.`);
    return { changed: false, outputPath };
  }
  await writeFile(outputPath, generated, 'utf8');
  return { changed: true, outputPath };
}

if (path.resolve(process.argv[1] ?? '') === fileURLToPath(import.meta.url)) {
  try {
    const result = await runTypeGenerator({ check: process.argv.includes('--check') });
    console.log(`${process.argv.includes('--check') ? 'verified' : 'generated'} ${path.relative(ROOT, result.outputPath)}`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
