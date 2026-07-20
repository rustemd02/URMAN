import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { auditSchemas, resolveSchemaReference, validateDocument } from '../../../scripts/validate-content-schemas.mjs';

async function withTemporarySchemas(schemas, callback) {
  const directory = await mkdtemp(path.join(tmpdir(), 'urman-schema-test-'));
  try {
    await Promise.all(Object.entries(schemas).map(([file, schema]) => (
      writeFile(path.join(directory, file), `${JSON.stringify(schema, null, 2)}\n`, 'utf8')
    )));
    return await callback(directory);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
}

const minimalModule = {
  schemaVersion: 1,
  moduleId: 'urman.chapter1',
  exactVersion: '1.0.0',
  sourceFiles: ['characters.json', 'scenes/arrival.json'],
  assets: ['urman.chapter1:asset/arrival_road'],
  dependencies: [],
  provides: ['urman.chapter1:scene/arrival'],
  requires: [],
};

const minimalCampaign = {
  schemaVersion: 1,
  id: 'urman.chapter1',
  exactVersion: '1.0.0',
  entrypoint: 'urman.chapter1:scene/arrival',
  modules: [{ moduleId: 'urman.chapter1', exactVersion: '1.0.0' }],
  roleBindings: { protagonist: 'urman.chapter1:character/aidar' },
  capabilityRequirements: [],
  narrativeOrder: ['urman.chapter1:scene/arrival'],
  invariants: [
    {
      id: 'urman.chapter1:invariant/arrival_first',
      description: 'The campaign begins at the arrival road.',
      severity: 'error',
      kind: 'required-reachable',
      targetId: 'urman.chapter1:scene/arrival',
    },
  ],
};

test('all schema documents parse and use resolvable local references', async () => {
  const result = await auditSchemas();
  assert.equal(result.files.length, 17);
  assert.deepEqual(result.errors, []);
});

test('relative refs resolve against the hierarchical schema ID base', async () => {
  assert.deepEqual(
    await resolveSchemaReference('campaign-manifest.schema.json', 'common.schema.json#/$defs/CampaignId'),
    {
      schemaFile: 'common.schema.json',
      schemaId: 'https://schemas.urman.game/content/v1/common.schema.json',
      fragment: '/$defs/CampaignId',
    },
  );
});

test('minimal module and campaign manifests are valid', async () => {
  assert.deepEqual(await validateDocument('module-manifest.schema.json', minimalModule), { valid: true, errors: [] });
  assert.deepEqual(await validateDocument('campaign-manifest.schema.json', minimalCampaign), { valid: true, errors: [] });
});

test('invalid namespaced content ID is rejected', async () => {
  const result = await validateDocument('campaign-manifest.schema.json', {
    ...minimalCampaign,
    entrypoint: 'arrival',
  });
  assert.equal(result.valid, false);
  assert.match(result.errors.join('\n'), /does not match/);
});

test('unknown condition opcode is rejected by the closed union', async () => {
  const result = await validateDocument('condition-effect.schema.json', {
    op: 'javascript.eval',
    expression: 'true',
  });
  assert.equal(result.valid, false);
  assert.match(result.errors.join('\n'), /\/(?:op|expression):.*\[oneOf: no branch matched\]/);
});

test('additional manifest property is rejected', async () => {
  const result = await validateDocument('module-manifest.schema.json', {
    ...minimalModule,
    rendererName: 'BrowserRenderer',
  });
  assert.equal(result.valid, false);
  assert.match(result.errors.join('\n'), /additional property/);
});

test('scene interactions permit one typed target and reject a scene/dialogue target collision', async () => {
  const scene = {
    schemaVersion: 1,
    id: 'urman.chapter1:scene/arrival',
    sceneType: 'static',
    assetRefs: [],
    textRefs: [],
    entryConditions: [],
    onEnter: [],
    onExit: [],
    interactions: [{
      id: 'urman.chapter1:interaction/ask-rinat',
      labelTextId: 'urman.chapter1:text/inspect',
      conditions: [],
      effects: [],
      targetDialogueId: 'urman.chapter1:dialogue/rinat',
    }],
  };
  assert.deepEqual(await validateDocument('scene.schema.json', scene), { valid: true, errors: [] });
  const collision = await validateDocument('scene.schema.json', {
    ...scene,
    interactions: [{
      ...scene.interactions[0],
      targetSceneId: 'urman.chapter1:scene/other',
    }],
  });
  assert.equal(collision.valid, false);
  assert.match(collision.errors.join('\n'), /\/interactions\/0: value matches forbidden schema/);
});

test('uniqueItems uses structural equality for reordered dependency objects', async () => {
  const result = await validateDocument('module-manifest.schema.json', {
    ...minimalModule,
    dependencies: [
      { moduleId: 'urman.core', exactVersion: '1.0.0' },
      { exactVersion: '1.0.0', moduleId: 'urman.core' },
    ],
  });
  assert.equal(result.valid, false);
  assert.match(result.errors.join('\n'), /\/dependencies: array items must be unique/);
});

test('const and enum use structural equality for reordered nested objects', async () => {
  const schema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://schemas.urman.game/content/v1/structural.schema.json',
    schemaVersion: 1,
    type: 'object',
    required: ['constValue', 'enumValue'],
    properties: {
      constValue: { const: { outer: { alpha: 1, beta: 2 }, list: [{ x: 1, y: 2 }] } },
      enumValue: { enum: [{ nested: { first: true, second: false }, list: [1, 2] }] },
    },
    additionalProperties: false,
  };
  await withTemporarySchemas({ 'structural.schema.json': schema }, async (directory) => {
    const result = await validateDocument('structural.schema.json', {
      constValue: { list: [{ y: 2, x: 1 }], outer: { beta: 2, alpha: 1 } },
      enumValue: { list: [1, 2], nested: { second: false, first: true } },
    }, directory);
    assert.equal(result.valid, true, result.errors.join('\n'));
  });
});

test('instance payload keywords remain opaque to schema ref and forbidden-key audits', async () => {
  const literal = {
    $ref: 'missing-literal.schema.json',
    properties: { rendererName: 'literal property, not a schema' },
    renderer: 'LiteralRenderer',
    expression: 'literal && harmless',
  };
  const schema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://schemas.urman.game/content/v1/literal-payload.schema.json',
    schemaVersion: 1,
    type: 'object',
    required: ['payload'],
    properties: {
      payload: {
        const: literal,
        enum: [literal],
        default: literal,
        examples: [literal],
      },
    },
    additionalProperties: false,
  };
  await withTemporarySchemas({ 'literal-payload.schema.json': schema }, async (directory) => {
    const audit = await auditSchemas(directory);
    assert.deepEqual(audit.errors, []);
    const result = await validateDocument('literal-payload.schema.json', { payload: literal }, directory);
    assert.equal(result.valid, true, result.errors.join('\n'));
  });
});

test('real schema-node forbidden properties and unresolved refs still fail bootstrap', async () => {
  const forbiddenSchema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://schemas.urman.game/content/v1/forbidden.schema.json',
    schemaVersion: 1,
    type: 'object',
    properties: { renderer: { type: 'string' } },
    additionalProperties: false,
  };
  const brokenRefSchema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://schemas.urman.game/content/v1/broken-ref.schema.json',
    schemaVersion: 1,
    $ref: 'missing.schema.json',
  };
  await withTemporarySchemas({
    'forbidden.schema.json': forbiddenSchema,
    'broken-ref.schema.json': brokenRefSchema,
  }, async (directory) => {
    const audit = await auditSchemas(directory);
    const messages = audit.errors.join('\n');
    assert.match(messages, /forbidden\.schema\.json\/properties: forbidden web\/executable content key renderer/);
    assert.match(messages, /broken-ref\.schema\.json\/: .*missing local \$ref target missing\.schema\.json/);
  });
});

test('schema bootstrap rejects a nested misspelled keyword with an actionable path', async () => {
  const schema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://schemas.urman.game/content/v1/keyword.schema.json',
    schemaVersion: 1,
    type: 'object',
    properties: {
      items: { type: 'array', minItemz: 1, items: { type: 'string' } },
    },
    additionalProperties: false,
  };
  await withTemporarySchemas({ 'keyword.schema.json': schema }, async (directory) => {
    const audit = await auditSchemas(directory);
    assert.match(audit.errors.join('\n'), /keyword\.schema\.json\/properties\/items: unsupported JSON Schema keyword "minItemz"/);
    await assert.rejects(
      validateDocument('keyword.schema.json', { items: [] }, directory),
      /Schema bootstrap failed:.*keyword\.schema\.json\/properties\/items: unsupported JSON Schema keyword "minItemz"/s,
    );
  });
});

test('schema bootstrap mutation-probes every supported keyword shape family', async () => {
  const probes = [
    ['$schema', 42, /\$schema must be a non-empty string/],
    ['$id', 42, /\$id must be a non-empty string/],
    ['$ref', 42, /\$ref must be a non-empty string/],
    ['$comment', 42, /\$comment must be a string/],
    ['schemaVersion', 0, /schemaVersion must be a positive integer/],
    ['title', 42, /title must be a string/],
    ['description', 42, /description must be a string/],
    ['examples', {}, /examples must be an array/],
    ['$defs', [], /\$defs must be an object of schemas/],
    ['properties', [], /properties must be an object of schemas/],
    ['type', ['string', 'string'], /type must contain unique supported JSON type names/],
    ['enum', [{ a: 1, b: 2 }, { b: 2, a: 1 }], /enum must be a non-empty array of structurally unique values/],
    ['pattern', '[', /pattern must be a valid regular expression/],
    ['minLength', -1, /minLength must be a non-negative integer/],
    ['maxLength', '4', /maxLength must be a non-negative integer/],
    ['minimum', '0', /minimum must be a finite number/],
    ['maximum', null, /maximum must be a finite number/],
    ['minItems', 1.5, /minItems must be a non-negative integer/],
    ['maxItems', -1, /maxItems must be a non-negative integer/],
    ['uniqueItems', 'true', /uniqueItems must be a boolean/],
    ['items', [], /items must be a schema object or boolean/],
    ['contains', 'schema', /contains must be a schema object or boolean/],
    ['minContains', -1, /minContains must be a non-negative integer/],
    ['maxContains', 2.5, /maxContains must be a non-negative integer/],
    ['minProperties', -1, /minProperties must be a non-negative integer/],
    ['required', ['name', 'name'], /required must be an array of unique strings/],
    ['additionalProperties', 'false', /additionalProperties must be a schema object or boolean/],
    ['propertyNames', [], /propertyNames must be a schema object or boolean/],
    ['oneOf', [], /oneOf must be a non-empty array of schemas/],
    ['anyOf', {}, /anyOf must be a non-empty array of schemas/],
    ['allOf', [42], /allOf must be a non-empty array of schemas/],
    ['not', [], /not must be a schema object or boolean/],
  ];

  for (const [keyword, malformed, expected] of probes) {
    const schema = {
      $schema: 'https://json-schema.org/draft/2020-12/schema',
      $id: 'https://schemas.urman.game/content/v1/mutation.schema.json',
      schemaVersion: 1,
      type: 'object',
      properties: { probe: { [keyword]: malformed } },
      additionalProperties: false,
    };
    await withTemporarySchemas({ 'mutation.schema.json': schema }, async (directory) => {
      const audit = await auditSchemas(directory);
      const messages = audit.errors.join('\n');
      assert.match(messages, /mutation\.schema\.json\/properties\/probe\//, `${keyword} must include its schema pointer`);
      assert.match(messages, expected, `${keyword} must report its required shape`);
    });
  }

  for (const [minimumKey, minimum, maximumKey, maximum] of [
    ['minLength', 3, 'maxLength', 2],
    ['minimum', 2, 'maximum', 1],
    ['minItems', 3, 'maxItems', 2],
    ['minContains', 2, 'maxContains', 1],
  ]) {
    const schema = {
      $schema: 'https://json-schema.org/draft/2020-12/schema',
      $id: 'https://schemas.urman.game/content/v1/bounds.schema.json',
      schemaVersion: 1,
      [minimumKey]: minimum,
      [maximumKey]: maximum,
    };
    await withTemporarySchemas({ 'bounds.schema.json': schema }, async (directory) => {
      assert.match((await auditSchemas(directory)).errors.join('\n'), new RegExp(`${minimumKey} must be less than or equal to ${maximumKey}`));
    });
  }
});

test('oneOf and anyOf failures retain the deepest likely branch pointer', async () => {
  const sceneResult = await validateDocument('scene.schema.json', {
    schemaVersion: 1,
    id: 'urman.chapter1:scene/test',
    sceneType: 'static',
    assetRefs: [],
    textRefs: [],
    entryConditions: [{ op: 'knowledge.status', knowledgeId: 'bad-id', status: 'confirmed' }],
    onEnter: [],
    onExit: [],
    interactions: [],
  });
  assert.equal(sceneResult.valid, false);
  assert.match(sceneResult.errors.join('\n'), /\/entryConditions\/0\/knowledgeId:.*\[oneOf: no branch matched\]/);

  const anyOfSchema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://schemas.urman.game/content/v1/union.schema.json',
    schemaVersion: 1,
    anyOf: [
      {
        type: 'object',
        required: ['alpha'],
        properties: { alpha: { type: 'string', pattern: '^ok$' } },
        additionalProperties: false,
      },
      {
        type: 'object',
        required: ['beta'],
        properties: { beta: { type: 'integer', minimum: 1 } },
        additionalProperties: false,
      },
    ],
  };
  await withTemporarySchemas({ 'union.schema.json': anyOfSchema }, async (directory) => {
    const result = await validateDocument('union.schema.json', { alpha: 'bad' }, directory);
    assert.equal(result.valid, false);
    assert.match(result.errors.join('\n'), /\/alpha:.*\[anyOf: no branch matched\]/);
  });
});

test('successful oneOf and anyOf applicators still evaluate sibling assertions', async () => {
  const schemas = {
    'one-sibling.schema.json': {
      $schema: 'https://json-schema.org/draft/2020-12/schema',
      $id: 'https://schemas.urman.game/content/v1/one-sibling.schema.json',
      schemaVersion: 1,
      oneOf: [{ type: 'string' }, { type: 'number' }],
      minLength: 3,
    },
    'any-sibling.schema.json': {
      $schema: 'https://json-schema.org/draft/2020-12/schema',
      $id: 'https://schemas.urman.game/content/v1/any-sibling.schema.json',
      schemaVersion: 1,
      anyOf: [{ type: 'string' }, { type: 'number' }],
      minLength: 3,
    },
  };
  await withTemporarySchemas(schemas, async (directory) => {
    for (const schemaFile of Object.keys(schemas)) {
      const result = await validateDocument(schemaFile, 'x', directory);
      assert.equal(result.valid, false);
      assert.match(result.errors.join('\n'), /string is too short/);
    }
  });
});

test('boolean schemas execute consistently in every supported schema-bearing position', async () => {
  const cases = [
    {
      name: 'defs-reference',
      build: (gate) => ({ $defs: { Gate: gate }, $ref: '#/$defs/Gate' }),
      value: { present: true },
      expected: { true: true, false: false },
    },
    {
      name: 'properties',
      build: (gate) => ({ type: 'object', properties: { value: gate }, additionalProperties: false }),
      value: { value: 1 },
      expected: { true: true, false: false },
      boundary: { value: {}, expected: { true: true, false: true } },
    },
    {
      name: 'items',
      build: (gate) => ({ type: 'array', items: gate }),
      value: [1],
      expected: { true: true, false: false },
      boundary: { value: [], expected: { true: true, false: true } },
    },
    {
      name: 'contains',
      build: (gate) => ({ type: 'array', contains: gate }),
      value: [1],
      expected: { true: true, false: false },
      boundary: { value: [], expected: { true: false, false: false } },
    },
    {
      name: 'property-names',
      build: (gate) => ({ type: 'object', propertyNames: gate }),
      value: { name: 1 },
      expected: { true: true, false: false },
      boundary: { value: {}, expected: { true: true, false: true } },
    },
    {
      name: 'additional-properties',
      build: (gate) => ({ type: 'object', properties: {}, additionalProperties: gate }),
      value: { extra: 1 },
      expected: { true: true, false: false },
      boundary: { value: {}, expected: { true: true, false: true } },
    },
    {
      name: 'not',
      build: (gate) => ({ not: gate }),
      value: 'anything',
      expected: { true: false, false: true },
    },
    {
      name: 'one-of',
      build: (gate) => ({ oneOf: [gate] }),
      value: 'anything',
      expected: { true: true, false: false },
    },
    {
      name: 'any-of',
      build: (gate) => ({ anyOf: [gate] }),
      value: 'anything',
      expected: { true: true, false: false },
    },
    {
      name: 'all-of',
      build: (gate) => ({ allOf: [gate] }),
      value: 'anything',
      expected: { true: true, false: false },
    },
  ];

  const schemas = {};
  for (const testCase of cases) {
    for (const gate of [true, false]) {
      const file = `${testCase.name}-${gate}.schema.json`;
      schemas[file] = {
        $schema: 'https://json-schema.org/draft/2020-12/schema',
        $id: `https://schemas.urman.game/content/v1/${file}`,
        schemaVersion: 1,
        ...testCase.build(gate),
      };
    }
  }

  await withTemporarySchemas(schemas, async (directory) => {
    assert.deepEqual((await auditSchemas(directory)).errors, []);
    for (const testCase of cases) {
      for (const gate of [true, false]) {
        const file = `${testCase.name}-${gate}.schema.json`;
        const result = await validateDocument(file, testCase.value, directory);
        assert.equal(result.valid, testCase.expected[String(gate)], `${testCase.name}:${gate} non-empty behavior`);
        if (testCase.boundary) {
          const boundary = await validateDocument(file, testCase.boundary.value, directory);
          assert.equal(boundary.valid, testCase.boundary.expected[String(gate)], `${testCase.name}:${gate} empty boundary`);
        }
      }
    }

    assert.match((await validateDocument('items-false.schema.json', [1], directory)).errors.join('\n'), /\/0: rejected by false schema/);
    assert.match((await validateDocument('contains-false.schema.json', [1], directory)).errors.join('\n'), /contains matched 0/);
    assert.match((await validateDocument('property-names-false.schema.json', { name: 1 }, directory)).errors.join('\n'), /\/name: rejected by false schema/);
  });
});

test('prototype-named JSON members require own properties in pointers and instance validation', async () => {
  const dangerousNames = ['constructor', 'toString', '__proto__'];
  const declaredProperties = JSON.parse(`{
    "constructor": { "$ref": "#/$defs/constructor" },
    "toString": { "$ref": "#/$defs/toString" },
    "__proto__": { "$ref": "#/$defs/__proto__" }
  }`);
  const definitions = JSON.parse(`{
    "constructor": { "const": "constructor" },
    "toString": { "const": "toString" },
    "__proto__": { "const": "__proto__" }
  }`);
  const ownedSchema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://schemas.urman.game/content/v1/owned-prototype.schema.json',
    schemaVersion: 1,
    type: 'object',
    $defs: definitions,
    required: dangerousNames,
    properties: declaredProperties,
    additionalProperties: false,
  };
  const strictSchema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://schemas.urman.game/content/v1/strict-prototype.schema.json',
    schemaVersion: 1,
    type: 'object',
    properties: {},
    additionalProperties: false,
  };
  await withTemporarySchemas({
    'owned-prototype.schema.json': ownedSchema,
    'strict-prototype.schema.json': strictSchema,
  }, async (directory) => {
    assert.deepEqual((await auditSchemas(directory)).errors, []);
    const ownedValue = JSON.parse('{"constructor":"constructor","toString":"toString","__proto__":"__proto__"}');
    assert.equal((await validateDocument('owned-prototype.schema.json', ownedValue, directory)).valid, true);

    const missing = await validateDocument('owned-prototype.schema.json', {}, directory);
    assert.equal(missing.valid, false);
    for (const name of dangerousNames) assert.match(missing.errors.join('\n'), new RegExp(`missing required property ${name}`));

    const extras = await validateDocument('strict-prototype.schema.json', ownedValue, directory);
    assert.equal(extras.valid, false);
    for (const name of dangerousNames) assert.match(extras.errors.join('\n'), new RegExp(`/${name}: additional property is not allowed`));
  });

  const brokenSchemas = Object.fromEntries(dangerousNames.map((name, index) => {
    const file = `inherited-pointer-${index}.schema.json`;
    return [file, {
      $schema: 'https://json-schema.org/draft/2020-12/schema',
      $id: `https://schemas.urman.game/content/v1/${file}`,
      schemaVersion: 1,
      $ref: `#/${name}`,
    }];
  }));
  await withTemporarySchemas(brokenSchemas, async (directory) => {
    const messages = (await auditSchemas(directory)).errors.join('\n');
    for (const name of dangerousNames) assert.match(messages, new RegExp(`unresolved JSON Pointer #/${name}`));
  });
});

test('an inherited non-enumerable $ref is ignored while owned refs still resolve', { concurrency: false }, async () => {
  const previous = Object.getOwnPropertyDescriptor(Object.prototype, '$ref');
  Object.defineProperty(Object.prototype, '$ref', {
    value: '#/$defs/inherited_ref_must_not_run',
    enumerable: false,
    configurable: true,
  });
  try {
    const result = await validateDocument('module-manifest.schema.json', minimalModule);
    assert.equal(result.valid, true, result.errors.join('\n'));
  } finally {
    if (previous) Object.defineProperty(Object.prototype, '$ref', previous);
    else delete Object.prototype.$ref;
  }
});

test('minLength and maxLength count Unicode code points rather than UTF-16 units or graphemes', async () => {
  const schemas = {
    'min-two.schema.json': {
      $schema: 'https://json-schema.org/draft/2020-12/schema',
      $id: 'https://schemas.urman.game/content/v1/min-two.schema.json',
      schemaVersion: 1,
      type: 'string',
      minLength: 2,
    },
    'max-one.schema.json': {
      $schema: 'https://json-schema.org/draft/2020-12/schema',
      $id: 'https://schemas.urman.game/content/v1/max-one.schema.json',
      schemaVersion: 1,
      type: 'string',
      maxLength: 1,
    },
  };
  await withTemporarySchemas(schemas, async (directory) => {
    const emojiMin = await validateDocument('min-two.schema.json', '😀', directory);
    const emojiMax = await validateDocument('max-one.schema.json', '😀', directory);
    assert.equal(emojiMin.valid, false, 'one astral emoji is one code point');
    assert.equal(emojiMax.valid, true, emojiMax.errors.join('\n'));

    const combiningSequence = 'e\u0301';
    const combiningMin = await validateDocument('min-two.schema.json', combiningSequence, directory);
    const combiningMax = await validateDocument('max-one.schema.json', combiningSequence, directory);
    assert.equal(combiningMin.valid, true, combiningMin.errors.join('\n'));
    assert.equal(combiningMax.valid, false, 'base plus combining mark is two code points');
  });
});

test('exact versions accept valid SemVer prerelease and reject ranges or malformed numeric identifiers', async () => {
  const validPrerelease = await validateDocument('module-manifest.schema.json', {
    ...minimalModule,
    exactVersion: '1.0.0-rc.1',
  });
  assert.equal(validPrerelease.valid, true, validPrerelease.errors.join('\n'));

  for (const exactVersion of ['^1.0.0', '>=1.0.0', '1.0', '01.0.0', '1.0.0-01', '1.0.0-']) {
    const result = await validateDocument('module-manifest.schema.json', { ...minimalModule, exactVersion });
    assert.equal(result.valid, false, `${exactVersion} must be rejected`);
  }
});

const diagnosticBase = {
  code: 'UnresolvedReference',
  moduleId: 'urman.chapter1',
  sourcePath: 'scenes/arrival.json',
  jsonPointer: '/entrypoint',
  message: 'Reference is unresolved.',
};

const minimalPack = {
  schemaVersion: 1,
  packVersion: '1.0.0',
  campaign: {
    id: 'urman.chapter1',
    exactVersion: '1.0.0',
    entrypoint: 'urman.chapter1:scene/arrival',
    orderedModules: [{ moduleId: 'urman.chapter1', exactVersion: '1.0.0' }],
    roleBindings: {},
    capabilityRequirements: [],
    narrativeOrder: ['urman.chapter1:scene/arrival'],
    invariants: [],
  },
  registries: {
    characters: [],
    scenes: [],
    dialogues: [],
    quests: [],
    knowledge: [],
    vocabulary: [],
    documents: [],
    assets: [],
    texts: [],
    capabilities: [],
  },
  dependencyGraph: { nodes: ['urman.chapter1'], edges: [] },
  moduleFingerprints: [{ moduleId: 'urman.chapter1', exactVersion: '1.0.0', sha256: '0'.repeat(64) }],
  campaignFingerprint: '1'.repeat(64),
};

test('successful compilation result cannot contain an error diagnostic', async () => {
  const result = await validateDocument('content-compilation-result.schema.json', {
    ok: true,
    pack: minimalPack,
    diagnostics: [{ ...diagnosticBase, severity: 'error' }],
  });
  assert.equal(result.valid, false);
});

test('discriminated union diagnostics prefer the branch matching ok:true', async () => {
  const result = await validateDocument('content-compilation-result.schema.json', {
    ok: true,
    diagnostics: [{ ...diagnosticBase, severity: 'error' }],
  });
  assert.equal(result.valid, false);
  const messages = result.errors.join('\n');
  assert.match(messages, /\/(?:diagnostics\/0\/severity|): .*\[oneOf: no branch matched\]/);
  assert.doesNotMatch(messages, /\/ok: expected constant false/);
});

test('failed compilation result requires at least one error diagnostic', async () => {
  const warningsOnly = await validateDocument('content-compilation-result.schema.json', {
    ok: false,
    diagnostics: [{ ...diagnosticBase, severity: 'warning' }],
  });
  assert.equal(warningsOnly.valid, false);

  const withError = await validateDocument('content-compilation-result.schema.json', {
    ok: false,
    diagnostics: [{ ...diagnosticBase, severity: 'error' }],
  });
  assert.equal(withError.valid, true, withError.errors.join('\n'));
});

test('capability state schema version is a required positive integer', async () => {
  const capability = {
    schemaVersion: 1,
    id: 'urman.chapter1:capability/lockpick',
    protocolId: 'urman.chapter1:capability/lockpick-protocol',
    exactVersion: '1.0.0',
    stateSchemaVersion: 1,
    configSchemaRef: 'common.schema.json',
    stateSchemaRef: 'common.schema.json',
    commandSchemaRef: 'common.schema.json',
    eventSchemaRef: 'common.schema.json',
    outcomeSchemaRef: 'common.schema.json',
    requiredAssets: [],
    requiredAudio: [],
    resourceClaims: [],
    accessibility: {
      alternativeInput: true,
      reducedMotion: true,
      captions: true,
      equivalentOutcomeId: 'urman.chapter1:outcome/lock-opened',
    },
    lifecycle: {
      supportsRestore: true,
      supportsSnapshot: true,
      supportsReset: true,
      idempotentStop: true,
      idempotentDispose: true,
    },
  };
  assert.equal((await validateDocument('capability.schema.json', capability)).valid, true);
  assert.equal((await validateDocument('capability.schema.json', { ...capability, stateSchemaVersion: 0 })).valid, false);
  const { stateSchemaVersion, ...missingVersion } = capability;
  assert.equal((await validateDocument('capability.schema.json', missingVersion)).valid, false);
});

test('campaign narrative order and closed invariant variants are required', async () => {
  assert.equal((await validateDocument('campaign-manifest.schema.json', minimalCampaign)).valid, true);
  const { narrativeOrder, ...withoutOrder } = minimalCampaign;
  assert.equal((await validateDocument('campaign-manifest.schema.json', withoutOrder)).valid, false);

  const revealInvariant = {
    ...minimalCampaign,
    invariants: [{
      id: 'urman.chapter1:invariant/do_not_answer_timing',
      description: 'The clue appears only after Rinat acts.',
      severity: 'error',
      kind: 'reveal-not-before',
      subjectId: 'urman.chapter1:knowledge/do_not_answer',
      afterId: 'urman.chapter1:beat/rinat_do_not_answer',
    }],
  };
  assert.equal((await validateDocument('campaign-manifest.schema.json', revealInvariant)).valid, true);
  assert.equal((await validateDocument('campaign-manifest.schema.json', {
    ...revealInvariant,
    invariants: [{ ...revealInvariant.invariants[0], script: 'nope' }],
  })).valid, false);
});

test('capability resource claims are typed and required', async () => {
  const capability = {
    schemaVersion: 1,
    id: 'urman.chapter1:capability/radio',
    protocolId: 'urman.chapter1:capability/radio-protocol',
    exactVersion: '1.0.0',
    stateSchemaVersion: 1,
    configSchemaRef: 'common.schema.json',
    stateSchemaRef: 'common.schema.json',
    commandSchemaRef: 'common.schema.json',
    eventSchemaRef: 'common.schema.json',
    outcomeSchemaRef: 'common.schema.json',
    requiredAssets: [],
    requiredAudio: [],
    resourceClaims: [{ resourceId: 'urman.chapter1:resource/radio-frequency', mode: 'exclusive' }],
    accessibility: {
      alternativeInput: true,
      reducedMotion: true,
      captions: true,
      equivalentOutcomeId: 'urman.chapter1:outcome/radio-complete',
    },
    lifecycle: {
      supportsRestore: true,
      supportsSnapshot: true,
      supportsReset: true,
      idempotentStop: true,
      idempotentDispose: true,
    },
  };
  assert.equal((await validateDocument('capability.schema.json', capability)).valid, true);
  assert.equal((await validateDocument('capability.schema.json', { ...capability, resourceClaims: [{ resourceId: capability.resourceClaims[0].resourceId, mode: 'write' }] })).valid, false);
  const { resourceClaims, ...withoutClaims } = capability;
  assert.equal((await validateDocument('capability.schema.json', withoutClaims)).valid, false);
});

test('portable documents may retain a normalized non-empty Markdown body', async () => {
  const document = {
    schemaVersion: 1,
    id: 'urman.chapter1:document/archive-note',
    title: { default: 'Archive note', translations: {} },
    format: 'markdown',
    sourceFile: 'documents/archive-note.md',
    bodyMarkdown: 'Do not answer.',
    assetRefs: [],
    knowledgeRefs: [],
    accessConditions: [],
    openEffects: [],
  };
  assert.equal((await validateDocument('document.schema.json', document)).valid, true);
  assert.equal((await validateDocument('document.schema.json', { ...document, bodyMarkdown: '' })).valid, false);
});

test('portable documents support only closed metadata required by the old PC document catalog', async () => {
  const document = {
    schemaVersion: 1,
    id: 'urman.oldpc:document/marat-death-notice',
    title: { default: 'Death notice', translations: { ru: 'Справка о смерти' } },
    format: 'markdown',
    sourceFile: 'documents/marat-death-notice.md',
    bodyMarkdown: 'The record does not match the family memory.',
    oldPc: {
      type: 'document',
      pcSection: 'documents_marat',
      canonStatus: 'canon',
      reliability: 'official_lie',
      searchTerms: ['Марат', 'смерть'],
      suggestedTerms: ['реестр'],
    },
    assetRefs: [],
    knowledgeRefs: [],
    accessConditions: [],
    openEffects: [],
  };
  assert.equal((await validateDocument('document.schema.json', document)).valid, true);
  assert.equal((await validateDocument('document.schema.json', {
    ...document,
    oldPc: { ...document.oldPc, reliability: 'uncertain' },
  })).valid, false);
  assert.equal((await validateDocument('document.schema.json', {
    ...document,
    oldPc: { ...document.oldPc, sourceKind: 'official_document' },
  })).valid, false);
});
