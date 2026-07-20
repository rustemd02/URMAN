import assert from 'node:assert/strict';
import test from 'node:test';

import { presentationInputFor } from '../../../src/game/presentation-input.mjs';

test('a rendered dialogue choice maps to DialogueReactionSession choiceId input', () => {
  const model = Object.freeze({
    kind: 'dialogue',
    choices: Object.freeze([{ id: 'sample.dialogue:choice/continue' }]),
  });
  assert.deepEqual(
    presentationInputFor(model.kind, model.choices[0].id),
    { type: 'dialogue.choose', choiceId: 'sample.dialogue:choice/continue' },
  );
});
