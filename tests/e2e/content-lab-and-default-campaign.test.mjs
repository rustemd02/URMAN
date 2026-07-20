import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { after, before, test } from 'node:test';

import { DEFAULT_MVP_JOURNEY, FINAL_RULE_ID, JOURNEY_DIALOGUE_CONTINUE_AFTER } from '../../scripts/playtest-mvp-smoke.mjs';
import { launch } from './support/chrome-cdp-driver.mjs';
import { startViteTestServer } from './support/vite-test-server.mjs';

let devServer;

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function withPage(run) {
  const page = await launch();
  try {
    return await run(page);
  } finally {
    await page.close();
  }
}

async function waitForValue(page, expression, { timeoutMs = 15_000 } = {}) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() <= deadline) {
    if (await page.evaluate(expression)) return;
    await delay(50);
  }
  throw new Error(`Timed out waiting for page expression: ${expression}`);
}

function setSelectExpression(selector, value) {
  return `(() => { const element = document.querySelector(${JSON.stringify(selector)}); if (!element) throw new Error('Missing select: ' + ${JSON.stringify(selector)}); element.value = ${JSON.stringify(value)}; element.dispatchEvent(new Event('change', { bubbles: true })); return element.value; })()`;
}

async function runViteBuild() {
  const child = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'build'], { stdio: ['ignore', 'pipe', 'pipe'] });
  let output = '';
  child.stdout.on('data', (chunk) => { output += chunk.toString(); });
  child.stderr.on('data', (chunk) => { output += chunk.toString(); });
  const code = await new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('exit', resolve);
  });
  if (code !== 0) throw new Error(`Vite production build for preview failed:\n${output}`);
}

before(async () => {
  devServer = await startViteTestServer({ e2e: true });
});

after(async () => {
  await devServer?.close();
});

test('default campaign saves/restores and confirms the final rule only after the forest cut', async () => {
  await withPage(async (page) => {
    await page.goto(`${devServer.url}/?e2e=1`);
    await page.waitFor('[data-launch-campaign]');
    await page.click('[data-launch-campaign]');
    await page.waitFor('[data-runtime-interaction="urman.chapter1:interaction/arrival-enter-house"]');

    const beforeJourney = await page.evaluate('window.__URMAN_E2E__.snapshot()');
    assert.equal(beforeJourney.runtime.state.knowledge[FINAL_RULE_ID], undefined);
    const restored = await page.evaluate(`(async () => { await window.__URMAN_E2E__.restore(${JSON.stringify(beforeJourney)}); return window.__URMAN_E2E__.snapshot(); })()`);
    assert.deepEqual(restored.runtime.state, beforeJourney.runtime.state, 'test-only save/load preserves the locked run state');
    await page.waitFor('[data-runtime-interaction="urman.chapter1:interaction/arrival-enter-house"]');

    for (const [index, interactionId] of DEFAULT_MVP_JOURNEY.entries()) {
      const nextInteractionId = DEFAULT_MVP_JOURNEY[index + 1];
      await page.click(`[data-runtime-interaction="${interactionId}"]`);
      if (JOURNEY_DIALOGUE_CONTINUE_AFTER.includes(interactionId)) {
        await page.waitFor('[data-runtime-dialogue-continue]');
        await page.click('[data-runtime-dialogue-continue]');
      }
      if (nextInteractionId) await page.waitFor(`[data-runtime-interaction="${nextInteractionId}"]`);
    }
    await waitForValue(page, `window.__URMAN_E2E__.snapshot().runtime.state.knowledge[${JSON.stringify(FINAL_RULE_ID)}] === 'confirmed'`);
    const finalSnapshot = await page.evaluate('window.__URMAN_E2E__.snapshot()');
    assert.equal(finalSnapshot.runtime.state.beats['urman.chapter1:beat/cliffhanger-hard-cut'], 'completed');
    assert.match(await page.text('.urman-runtime-scene'), /Не отвечай/);
  });
});

test('production main resets only v1 once, restores V2, autosaves commits and requires confirmation before discarding a mismatched V2 save', async () => {
  await withPage(async (page) => {
    await page.goto(devServer.url);
    await page.waitFor('[data-launch-campaign]');
    await page.evaluate(`(() => {
      localStorage.removeItem('urman.persistence.reset.v2');
      for (const key of [
        'urman.mvp.save.v1',
        'urman.oldPcHub.state.v1',
        'game.notepad.files.v1',
        'game.notepad.activeFile.v1',
      ]) localStorage.setItem(key, 'retired');
      localStorage.setItem('urman_username', 'Aidar');
      localStorage.setItem('urman.content-lab.v1', 'isolated');
    })()`);
    await page.goto(devServer.url);
    await page.waitFor('[data-launch-campaign]');
    assert.match(await page.text('.urman-runtime-announcement'), /Прогресс ранней pre-MVP версии сброшен/);
    const resetState = await page.evaluate(`({
      legacy: [
        localStorage.getItem('urman.mvp.save.v1'),
        localStorage.getItem('urman.oldPcHub.state.v1'),
        localStorage.getItem('game.notepad.files.v1'),
        localStorage.getItem('game.notepad.activeFile.v1'),
      ],
      marker: localStorage.getItem('urman.persistence.reset.v2'),
      username: localStorage.getItem('urman_username'),
      lab: localStorage.getItem('urman.content-lab.v1'),
    })`);
    assert.deepEqual(resetState.legacy, [null, null, null, null]);
    assert.equal(resetState.marker, 'completed');
    assert.equal(resetState.username, 'Aidar');
    assert.equal(resetState.lab, 'isolated', 'the production reset never touches Content Lab storage');

    await page.click('[data-launch-campaign]');
    await page.waitFor('[data-runtime-interaction="urman.chapter1:interaction/arrival-enter-house"]');
    const entrySave = await page.evaluate("JSON.parse(localStorage.getItem('urman.mvp.save.v2'))");
    assert.equal(entrySave.snapshotSchemaVersion, 2, 'the initial committed entry is autosaved');
    const beforeCommit = entrySave.runtime.occurrences.length;
    await page.click('[data-runtime-interaction="urman.chapter1:interaction/arrival-enter-house"]');
    await page.waitFor('[data-runtime-interaction="urman.chapter1:interaction/house-to-route"]');
    await waitForValue(page, "JSON.parse(localStorage.getItem('urman.mvp.save.v2')).runtime.occurrences.length > " + beforeCommit);
    const beforeRestore = await page.evaluate("JSON.parse(localStorage.getItem('urman.mvp.save.v2'))");

    await page.goto(devServer.url);
    await page.waitFor('[data-runtime-interaction="urman.chapter1:interaction/arrival-enter-house"]');
    const afterRestore = await page.evaluate("JSON.parse(localStorage.getItem('urman.mvp.save.v2'))");
    assert.deepEqual(afterRestore.runtime, beforeRestore.runtime, 'valid V2 storage replaces the run only through the strict main-entry restore path');
    assert.doesNotMatch(await page.text('.urman-runtime-announcement'), /pre-MVP версии сброшен/, 'the v1 reset notice is one-time');

    await page.evaluate(`(() => {
      const saved = JSON.parse(localStorage.getItem('urman.mvp.save.v2'));
      saved.campaignLock.campaignFingerprint = ${JSON.stringify('c'.repeat(64))};
      localStorage.setItem('urman.mvp.save.v2', JSON.stringify(saved));
    })()`);
    await page.goto(devServer.url);
    await page.waitFor('[data-persistence-reset-current-save]');
    assert.match(await page.text('.urman-persistence-reset'), /только текущего сохранения версии 2/i);
    await page.click('[data-persistence-reset-current-save]');
    await page.waitFor('[data-launch-campaign]');
    const afterDiscard = await page.evaluate(`({
      v2: localStorage.getItem('urman.mvp.save.v2'),
      marker: localStorage.getItem('urman.persistence.reset.v2'),
      username: localStorage.getItem('urman_username'),
      lab: localStorage.getItem('urman.content-lab.v1'),
    })`);
    assert.equal(afterDiscard.v2, null);
    assert.equal(afterDiscard.marker, 'completed');
    assert.equal(afterDiscard.username, 'Aidar');
    assert.equal(afterDiscard.lab, 'isolated');
  });
});

test('an autosave failure reports after, rather than rolls back, the committed player transition', async () => {
  await withPage(async (page) => {
    await page.goto(`${devServer.url}/?e2e=1`);
    await page.waitFor('[data-launch-campaign]');
    await page.click('[data-launch-campaign]');
    await page.waitFor('[data-runtime-interaction="urman.chapter1:interaction/arrival-enter-house"]');
    const before = await page.evaluate('window.__URMAN_E2E__.snapshot().runtime.occurrences.length');
    await page.evaluate(`(() => {
      window.__URMAN_PERSISTENCE_TEST_ORIGINAL_SET_ITEM__ = Storage.prototype.setItem;
      Storage.prototype.setItem = () => { throw new Error('simulated quota'); };
    })()`);
    try {
      await page.click('[data-runtime-interaction="urman.chapter1:interaction/arrival-enter-house"]');
      await page.waitFor('[data-runtime-interaction="urman.chapter1:interaction/house-to-route"]');
      await waitForValue(page, "window.__URMAN_E2E__.snapshot().runtime.occurrences.length > " + before);
      await waitForValue(page, "document.querySelector('.urman-runtime-announcement')?.textContent.includes('Не удалось сохранить прогресс') === true");
    } finally {
      await page.evaluate(`(() => {
        Storage.prototype.setItem = window.__URMAN_PERSISTENCE_TEST_ORIGINAL_SET_ITEM__;
        delete window.__URMAN_PERSISTENCE_TEST_ORIGINAL_SET_ITEM__;
      })()`);
    }
  });
});

test('Content Lab creates new isolated runs for campaign and data-only swaps with focus and ARIA observability', async () => {
  await withPage(async (page) => {
    await page.goto(`${devServer.url}/?lab=1`);
    await page.waitFor('[data-content-lab="true"]');
    assert.equal(await page.evaluate("document.querySelector('[data-content-lab]').getAttribute('aria-labelledby')"), 'lab-title');
    assert.equal(await page.evaluate("document.querySelector('[data-lab-status]').getAttribute('aria-live')"), 'polite');
    await page.focus('[name="campaign"]');
    assert.equal(await page.evaluate("document.activeElement === document.querySelector('[name=campaign]')"), true);

    assert.equal(await page.evaluate(setSelectExpression('[name="campaign"]', 'urman.dev-oldpc')), 'urman.dev-oldpc');
    await page.waitFor('[data-lab-action="new-run"]');
    await page.click('[data-lab-action="new-run"]');
    await waitForValue(page, "document.querySelector('#lab-run-id').textContent === 'lab-run-1'");
    const oldPcFingerprint = await page.text('#lab-run-fingerprint');
    assert.match(oldPcFingerprint, /^[a-f0-9]{64}$/, 'the run uses a real SHA-256 compiler fingerprint');

    assert.equal(await page.evaluate(setSelectExpression('[name="campaign"]', 'urman.chapter1')), 'urman.chapter1');
    assert.equal(await page.evaluate(setSelectExpression('[name="swap"]', 'asset')), 'asset');
    assert.equal(await page.evaluate(setSelectExpression('[name="module"]', 'urman.chapter1')), 'urman.chapter1');
    assert.equal(await page.evaluate(setSelectExpression('[name="quest"]', 'urman.chapter1:quest/quest_language_reread')), 'urman.chapter1:quest/quest_language_reread');
    assert.equal(await page.evaluate(setSelectExpression('[name="scene"]', 'urman.chapter1:scene/house')), 'urman.chapter1:scene/house');
    assert.equal(await page.evaluate(setSelectExpression('[name="preset"]', 'standard')), 'standard');
    assert.match(await page.text('#lab-inspection-selection'), /module=urman\.chapter1; quest=urman\.chapter1:quest\/quest_language_reread; scene=urman\.chapter1:scene\/house/);
    await page.click('[data-lab-action="new-run"]');
    await waitForValue(page, "document.querySelector('#lab-run-id').textContent === 'lab-run-2'");
    assert.notEqual(await page.text('#lab-run-fingerprint'), oldPcFingerprint, 'a campaign/content swap creates a fresh lock and run');
    assert.match(await page.text('#lab-diagnostics-heading').then(() => page.evaluate("document.querySelector('#lab-diagnostics-heading').parentElement.textContent")), /separately compiled swap variant/);
    assert.match(await page.text('#lab-storage-namespace'), /urman\.content-lab\.v1/);

    await page.focus('[data-lab-action="sample-capability"]');
    assert.equal(await page.evaluate("document.activeElement === document.querySelector('[data-lab-action=sample-capability]')"), true);
    await page.click('[data-lab-action="sample-capability"]');
    assert.match(await page.text('#lab-cleanup'), /Active provider received standard input .*outcome contract/);
    assert.match(await page.text('#lab-event-log'), /provider handle preset=standard query=content-lab-seed/);
    assert.match(await page.text('#lab-event-log'), /inspection module=urman\.chapter1, quest=urman\.chapter1:quest\/quest_language_reread, scene=urman\.chapter1:scene\/house/);
    const png = await page.screenshot();
    assert.deepEqual([...png.subarray(1, 4)], [80, 78, 71], 'driver returns PNG bytes');
  });
});

test('CDP focus fails loudly when the browser cannot activate the target', async () => {
  await withPage(async (page) => {
    await page.goto(`data:text/html,${encodeURIComponent('<!doctype html><button id="disabled" disabled>disabled</button>')}`);
    await assert.rejects(() => page.focus('#disabled'), /Browser could not focus selector: #disabled/);
  });
});

test('production preview does not serve Content Lab', async () => {
  await runViteBuild();
  const preview = await startViteTestServer({ preview: true });
  try {
    await withPage(async (page) => {
      await page.goto(`${preview.url}/?lab=1`);
      assert.equal(await page.evaluate("Boolean(document.querySelector('[data-content-lab]'))"), false);
      assert.equal(await page.evaluate("document.documentElement.innerHTML.includes('virtual:urman-content-lab-catalog')"), false);
    });
  } finally {
    await preview.close();
  }
});
