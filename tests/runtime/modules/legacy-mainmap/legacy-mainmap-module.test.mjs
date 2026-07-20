import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createServer } from 'vite';

import {
  LEGACY_MAINMAP_MODULE_ID,
  LEGACY_MAINMAP_SCENE_ID,
  createLegacyMainMapModuleDescriptor,
} from '../../../../src/runtime/modules/legacy-mainmap/legacy-mainmap-module.mjs';

const MAIN_MAP_SOURCE = new URL('../../../../src/MainMap/MainMapScene.ts', import.meta.url);
const HOMESTEAD_SOURCE = new URL('../../../../src/MainMap/logic/generators/HomesteadGenerator.ts', import.meta.url);

class FakeEventTarget {
  #listeners = new Map();

  addEventListener(type, listener) {
    const listeners = this.#listeners.get(type) ?? new Set();
    listeners.add(listener);
    this.#listeners.set(type, listeners);
  }

  removeEventListener(type, listener) {
    const listeners = this.#listeners.get(type);
    listeners?.delete(listener);
    if (listeners?.size === 0) this.#listeners.delete(type);
  }

  listenerCount() {
    return [...this.#listeners.values()].reduce((sum, listeners) => sum + listeners.size, 0);
  }
}

class FakeElement extends FakeEventTarget {
  constructor(ownerDocument) {
    super();
    this.ownerDocument = ownerDocument;
    this.style = {};
    this.textContent = '';
    this.children = [];
  }

  appendChild(child) { this.children.push(child); }
}

class FakeCanvas extends FakeElement {
  constructor(ownerDocument) {
    super(ownerDocument);
    this.width = 0;
    this.height = 0;
  }

  getContext() { return {}; }
}

function fakeSurface() {
  const view = new FakeEventTarget();
  view.innerWidth = 1280;
  view.innerHeight = 720;
  view.raf = [];
  view.cancelledFrames = [];
  view.timers = [];
  view.clearedTimers = [];
  view.requestAnimationFrame = (callback) => {
    const id = view.raf.length + 1;
    view.raf.push({ id, callback });
    return id;
  };
  view.cancelAnimationFrame = (id) => view.cancelledFrames.push(id);
  view.setInterval = (callback, interval) => {
    const id = view.timers.length + 1;
    view.timers.push({ id, callback, interval });
    return id;
  };
  view.clearInterval = (id) => view.clearedTimers.push(id);
  const document = {
    defaultView: view,
    createElement: () => new FakeElement(document),
  };
  const elements = new Map([
    ['[data-mainmap-canvas]', new FakeCanvas(document)],
    ['[data-mainmap-modal]', new FakeElement(document)],
    ['[data-mainmap-actions]', new FakeElement(document)],
    ['[data-mainmap-title]', new FakeElement(document)],
    ['[data-mainmap-instructions]', new FakeElement(document)],
    ['[data-mainmap-close]', new FakeElement(document)],
    ['[data-mainmap-modal-title]', new FakeElement(document)],
    ['[data-mainmap-modal-description]', new FakeElement(document)],
  ]);
  const container = new FakeElement(document);
  container.markup = '';
  Object.defineProperty(container, 'innerHTML', {
    get: () => container.markup,
    set: (value) => { container.markup = value; },
  });
  container.querySelector = (selector) => elements.get(selector) ?? null;
  return { container, elements, view };
}

function devOptions(suffix) {
  return {
    residents: [{
      lotIndex: 0,
      entityId: `dev.mainmap:resident/${suffix}`,
      label: `House ${suffix}`,
      isPrimaryResidence: true,
      actions: [{ id: `inspect-${suffix}`, label: `Inspect ${suffix}`, request: { intent: 'inspect' } }],
    }],
    landmarks: ['mosque', 'council', 'club', 'clinic', 'zirat'].map((slot) => ({
      slot,
      entityId: `dev.mainmap:landmark/${slot}-${suffix}`,
      label: `${slot} ${suffix}`,
      actions: [],
    })),
    labels: { bathhouse: 'Bath', shed: 'Shed', unnamedResidencePrefix: 'Residence' },
    presentation: { title: `Map ${suffix}`, instructions: 'Drag', closeLabel: 'Close', fallbackEntityLabel: 'Entity' },
    onAction() {},
    ambient: { intervalMs: 50, shouldEmit: () => false, emit() {} },
  };
}

test('legacy MainMap is createable only by an explicit development registration', () => {
  let created = 0;
  const descriptor = createLegacyMainMapModuleDescriptor({
    createScene: () => {
      created += 1;
      return { init() {}, destroy() {} };
    },
  });
  assert.equal(descriptor.moduleId, LEGACY_MAINMAP_MODULE_ID);
  assert.equal(descriptor.sceneId, LEGACY_MAINMAP_SCENE_ID);
  assert.equal(descriptor.devOnly, true);
  assert.equal(created, 0);
  assert.throws(() => descriptor.create({ environment: 'production' }), /development registration/);
  assert.throws(() => descriptor.register({ register() {} }, { environment: 'test' }), /development registration/);
  const registrations = [];
  descriptor.register({ register: (entry) => registrations.push(entry) }, { environment: 'development' });
  assert.equal(registrations.length, 1);
  assert.equal(registrations[0].id, LEGACY_MAINMAP_SCENE_ID);
  const session = registrations[0].create();
  assert.equal(created, 1);
  session.init({});
  session.dispose();
  session.dispose();
});

test('legacy MainMap delegates the whole surface lifecycle and leaves no reusable session after dispose', () => {
  const calls = [];
  const descriptor = createLegacyMainMapModuleDescriptor({
    createScene: (context) => {
      calls.push(['create', context.roleBindings]);
      return {
        init: (container) => calls.push(['init', container]),
        destroy: () => calls.push(['destroy']),
      };
    },
  });
  const firstContext = { environment: 'development', roleBindings: { household: 'test:character/a' } };
  const secondContext = { environment: 'development', roleBindings: { household: 'test:character/b' } };
  const session = descriptor.create(firstContext);
  const container = { id: 'map-root' };
  session.init(container);
  session.dispose();
  session.dispose();
  const swappedSession = descriptor.create(secondContext);
  swappedSession.init(container);
  swappedSession.dispose();
  assert.deepEqual(calls, [
    ['create', { household: 'test:character/a' }],
    ['init', container],
    ['destroy'],
    ['create', { household: 'test:character/b' }],
    ['init', container],
    ['destroy'],
  ]);
  assert.equal(session.disposed, true);
  assert.throws(() => session.init(container), /disposed/);
});

test('descriptor closes a scene exactly once when its init fails and preserves the original error', () => {
  const initFailure = new Error('surface init failed');
  let destroys = 0;
  const descriptor = createLegacyMainMapModuleDescriptor({
    createScene: () => ({
      init() { throw initFailure; },
      destroy() { destroys += 1; },
    }),
  });
  const session = descriptor.create({ environment: 'development' });
  assert.throws(() => session.init({}), (error) => error === initFailure);
  assert.equal(session.disposed, true);
  assert.equal(destroys, 1);
  session.dispose();
  assert.equal(destroys, 1);
  assert.throws(() => session.init({}), /disposed/);
});

test('MainMap accepts resident/action bindings and retains no narrative or global-scene owner', async () => {
  const [scene, homes] = await Promise.all([readFile(MAIN_MAP_SOURCE, 'utf8'), readFile(HOMESTEAD_SOURCE, 'utf8')]);
  assert.match(scene, /interface MainMapSceneOptions extends MainMapWorldBindings/);
  assert.match(scene, /onAction: \(action: MainMapAction/);
  assert.match(scene, /cancelAnimationFrame/);
  assert.match(scene, /clearInterval/);
  assert.match(scene, /removeEventListener/);
  assert.match(scene, /canvas\.width = 0/);
  assert.doesNotMatch(scene, /this\.game\.|window\.|switchScene\(|three/);
  assert.doesNotMatch(homes, /CHARACTERS|residentId|babay|dialogue_|sceneTarget/);
  assert.match(homes, /residentByLot/);
  assert.match(homes, /resident\?\.actions/);
  assert.doesNotMatch(homes, /from ['"]\.\.\/\.\.\/data\//);
});

test('the actual dev surface releases listeners, animation frame, interval and canvas backing store for either role binding set', async () => {
  const server = await createServer({ configFile: false, appType: 'custom', logLevel: 'error' });
  try {
    const { MainMapScene } = await server.ssrLoadModule('/src/MainMap/MainMapScene.ts');
    for (const suffix of ['alpha', 'beta']) {
      const { container, elements, view } = fakeSurface();
      const scene = new MainMapScene({}, devOptions(suffix));
      scene.init(container);
      assert.equal(view.listenerCount(), 5);
      assert.equal(elements.get('[data-mainmap-canvas]').listenerCount(), 2);
      assert.equal(elements.get('[data-mainmap-close]').listenerCount(), 1);
      scene.showInteraction({
        id: `dev.mainmap:entity/${suffix}`,
        type: 'building',
        gx: 0,
        gy: 0,
        gw: 1,
        gh: 1,
        actions: [{ id: `action-${suffix}`, label: 'Open', request: { intent: 'open' } }],
      });
      const actionButton = elements.get('[data-mainmap-actions]').children[0];
      assert.equal(actionButton.listenerCount(), 1);
      assert.equal(view.raf.length, 1);
      assert.equal(view.timers.length, 1);
      scene.destroy();
      assert.equal(view.listenerCount(), 0);
      assert.equal(elements.get('[data-mainmap-canvas]').listenerCount(), 0);
      assert.equal(elements.get('[data-mainmap-close]').listenerCount(), 0);
      assert.equal(actionButton.listenerCount(), 0);
      assert.deepEqual(view.cancelledFrames, [1]);
      assert.deepEqual(view.clearedTimers, [1]);
      assert.equal(elements.get('[data-mainmap-canvas]').width, 0);
      assert.equal(elements.get('[data-mainmap-canvas]').height, 0);
      assert.equal(container.innerHTML, '');
    }
  } finally {
    await server.close();
  }
});

test('malformed dev config fails before MainMap acquires a DOM listener, timer, frame or canvas surface', async () => {
  const server = await createServer({ configFile: false, appType: 'custom', logLevel: 'error' });
  try {
    const { MainMapScene } = await server.ssrLoadModule('/src/MainMap/MainMapScene.ts');
    const { container, elements, view } = fakeSurface();
    const options = devOptions('invalid');
    options.ambient = { ...options.ambient, intervalMs: 0 };
    const scene = new MainMapScene({}, options);
    assert.throws(() => scene.init(container), /ambient config/);
    assert.equal(view.listenerCount(), 0);
    assert.equal(elements.get('[data-mainmap-canvas]').listenerCount(), 0);
    assert.equal(elements.get('[data-mainmap-close]').listenerCount(), 0);
    assert.equal(view.raf.length, 0);
    assert.equal(view.timers.length, 0);
    assert.equal(container.innerHTML, '');
  } finally {
    await server.close();
  }
});
