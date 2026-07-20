import assert from 'node:assert/strict';
import test from 'node:test';

import { replaceActiveLabRun } from '../../src/lab/run-replacement.mjs';

test('failed Content Lab cleanup retains the active run and blocks its replacement', () => {
  const activeRun = Object.freeze({ id: 'active-run' });
  let createCalls = 0;
  const result = replaceActiveLabRun(
    activeRun,
    () => { throw new Error('dispose failed'); },
    () => {
      createCalls += 1;
      return Object.freeze({ id: 'replacement-run' });
    },
  );

  assert.equal(result.replaced, false);
  assert.equal(result.activeRun, activeRun);
  assert.match(result.error.message, /dispose failed/);
  assert.equal(createCalls, 0);
});
