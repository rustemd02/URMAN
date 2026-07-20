import assert from 'node:assert/strict';
import test from 'node:test';

import { compileContent } from '../../../scripts/content/compile-content.mjs';
import {
  AssetResolver,
  ResolverError,
  TextResolver,
  validateManifestClosure,
} from '../../../src/runtime/resolvers/content-resolvers.mjs';
import { AudioResolver } from '../../../src/runtime/media/audio-resolver.mjs';

const MODULES = Object.freeze([
  Object.freeze({ moduleId: 'urman.alpha', exactVersion: '1.0.0' }),
  Object.freeze({ moduleId: 'urman.beta', exactVersion: '1.0.0' }),
]);

function text(id, value, purpose = 'ui') {
  return { schemaVersion: 1, id, purpose, value: { default: value, translations: {} } };
}

function asset(id, {
  kind = 'image',
  file = 'media/base.bin',
  mediaType = 'application/octet-stream',
  variants = [],
  accessibility = { decorative: true },
} = {}) {
  return { schemaVersion: 1, id, kind, file, mediaType, variants, accessibility };
}

function vocabulary(id, { term = 'урман', language = 'tt', meaning = 'лес' } = {}) {
  return {
    schemaVersion: 1,
    id,
    term,
    language,
    meaning: { default: meaning, translations: { tt: 'урман' } },
    initialStatus: 'unknown',
    rereadTargets: [],
    applicationTargets: [],
    consultantReview: { status: 'pending', reviewer: null, notes: '' },
  };
}

function pack({ assets = [], texts = [], vocabulary: vocabularyEntries = [], registries = {} } = {}) {
  return {
    schemaVersion: 1,
    campaign: { orderedModules: MODULES },
    registries: {
      assets,
      capabilities: [],
      characters: [],
      dialogues: [],
      documents: [],
      knowledge: [],
      quests: [],
      scenes: [],
      texts,
      vocabulary: vocabularyEntries,
      ...registries,
    },
  };
}

test('resolvers consume compiler output and localize text without exposing markup', async () => {
  const compiled = await compileContent({
    campaignPath: 'tests/content/compiler/fixtures/valid/campaign/campaign.json',
    moduleManifestPaths: ['tests/content/compiler/fixtures/valid/module/module.json'],
  });
  assert.equal(compiled.ok, true, JSON.stringify(compiled.diagnostics));
  const model = new TextResolver(compiled.pack).resolve('urman.compiler-fixture:text/arrival-title', 'ru');
  assert.equal(model.text, 'Приезд');
  assert.equal(model.resolvedLocale, 'ru');
  assert.equal(Object.hasOwn(model, 'html'), false);
  assert.equal(Object.isFrozen(model), true);
});

test('asset IDs are ordered by resolved modules and variants use only explicit seed/state', () => {
  const content = pack({
    assets: [
      asset('urman.beta:asset/portrait', { file: 'beta/portrait.bin' }),
      asset('urman.alpha:asset/lantern', {
        file: 'alpha/lantern-base.bin',
        variants: [
          { id: 'quiet', file: 'alpha/lantern-quiet.bin' },
          { id: 'wind', file: 'alpha/lantern-wind.bin' },
        ],
      }),
    ],
  });
  const resolver = new AssetResolver(content, {
    resolveFileUrl: (file, metadata) => `https://content.example/${metadata.moduleId}/${file}`,
  });
  assert.deepEqual(resolver.ids(), ['urman.alpha:asset/lantern', 'urman.beta:asset/portrait']);
  assert.equal(resolver.resolve('urman.alpha:asset/lantern').variantId, null);
  assert.equal(resolver.resolve('urman.alpha:asset/lantern', 'wind').url, 'https://content.example/urman.alpha/alpha/lantern-wind.bin');
  const first = resolver.resolve('urman.alpha:asset/lantern', { seed: 'new-run', state: { phase: 2 } });
  const second = resolver.resolve('urman.alpha:asset/lantern', { seed: 'new-run', state: { phase: 2 } });
  assert.deepEqual(first, second);
  assert.equal(Object.isFrozen(first), true);
});

test('text resolver selects a Tatar variant and emits vocabulary as a semantic token', () => {
  const textId = 'urman.alpha:text/greeting';
  const wordId = 'urman.alpha:vocabulary/urman';
  const content = pack({
    texts: [{ ...text(textId, 'Привет, {{word}}! {{name}}'), value: { default: 'Привет, {{word}}! {{name}}', translations: { tt: 'Сәлам, {{word}}! {{name}}' } } }],
    vocabulary: [vocabulary(wordId)],
  });
  const model = new TextResolver(content).resolve(textId, 'tt-RU', {
    word: { vocabularyId: wordId },
    name: '<Айдар>',
  });
  assert.equal(model.resolvedLocale, 'tt');
  assert.equal(model.text, 'Сәлам, урман! <Айдар>');
  assert.deepEqual(model.segments[1], {
    type: 'vocabulary', vocabularyId: wordId, language: 'tt', term: 'урман', meaning: 'урман', meaningLocale: 'tt',
  });
  assert.equal(Object.hasOwn(model.segments[2], 'html'), false);
  assert.throws(
    () => new TextResolver(content).resolve(textId, 'tt', { name: 'Айдар' }),
    (error) => error instanceof ResolverError && error.code === 'MissingVariable' && error.details.moduleId === 'urman.alpha',
  );
});

test('audio supplies captions, transcript and a non-audio cue with the identical outcome key', () => {
  const captionId = 'urman.alpha:text/knock-caption';
  const transcriptId = 'urman.alpha:text/knock-transcript';
  const audioId = 'urman.alpha:asset/knock';
  const content = pack({
    texts: [text(captionId, 'Стук в дверь', 'caption'), text(transcriptId, 'Стук снаружи; кто-то ждёт у двери', 'accessibility')],
    assets: [asset(audioId, {
      kind: 'audio', file: 'audio/knock.ogg', mediaType: 'audio/ogg',
      accessibility: { decorative: false, captionTextId: captionId, audioDescriptionTextId: transcriptId },
    })],
  });
  const resolved = new AudioResolver(content, { resolveFileUrl: (file) => `host:${file}` }).resolve(audioId, { outcomeKey: 'urman.alpha:outcome/visitor-heard' });
  assert.equal(resolved.asset.url, 'host:audio/knock.ogg');
  assert.equal(resolved.captions.text, 'Стук в дверь');
  assert.equal(resolved.transcript.text, 'Стук снаружи; кто-то ждёт у двери');
  assert.equal(resolved.nonAudioCue.outcomeKey, 'urman.alpha:outcome/visitor-heard');
  assert.equal(resolved.nonAudioCue.text, resolved.transcript.text);
});

test('closure rejects duplicate IDs and missing accessibility references with stable origins', () => {
  const duplicateId = 'urman.alpha:asset/duplicate';
  assert.throws(
    () => new AssetResolver(pack({ assets: [asset(duplicateId), asset(duplicateId)] })),
    (error) => error instanceof ResolverError
      && error.code === 'DuplicateLogicalId'
      && error.details.moduleId === 'urman.alpha'
      && error.details.sourcePointer === '/registries/assets/1/id'.replace('/id', ''),
  );

  const imageId = 'urman.alpha:asset/sign';
  assert.throws(
    () => validateManifestClosure(pack({ assets: [asset(imageId, { accessibility: { decorative: false } })] })),
    (error) => error instanceof ResolverError
      && error.code === 'InvalidAccessibility'
      && error.details.moduleId === 'urman.alpha'
      && error.details.sourcePointer === '/registries/assets/0/accessibility/altTextId',
  );

  const missingId = 'urman.beta:asset/absent';
  const resolver = new AssetResolver(pack());
  assert.throws(
    () => resolver.resolve(missingId),
    (error) => error instanceof ResolverError
      && error.code === 'MissingAsset'
      && error.details.moduleId === 'urman.beta'
      && error.details.sourcePointer === '/registries/assets',
  );
});
