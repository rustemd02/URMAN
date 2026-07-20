import assert from 'node:assert/strict';
import { test } from 'node:test';

import { findChromeExecutable } from './support/chrome-cdp-driver.mjs';

test('Chrome candidate diagnostics reject a non-executable candidate with actionable guidance', async () => {
  await assert.rejects(
    () => findChromeExecutable({ platform: 'unsupported-test-platform', candidates: ['/dev/null'] }),
    (error) => {
      assert.match(error.message, /Chrome\/CDP is unavailable on unsupported-test-platform/);
      assert.match(error.message, /Checked executable paths:\n- \/dev\/null/);
      assert.match(error.message, /CHROME_PATH/);
      return true;
    },
  );
});
