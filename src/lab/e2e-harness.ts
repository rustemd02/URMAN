import pack from 'virtual:urman-content-catalog';

import { Game } from '../game/Game';
import { RuntimeBootstrap } from '../runtime/bootstrap/runtime-bootstrap.mjs';
import { createBrowserProductionPersistenceGateway } from '../runtime/persistence/production-persistence-gateway.mjs';

declare global {
  interface Window {
    __URMAN_E2E__?: {
      snapshot(): object;
      restore(snapshot: object): Promise<void>;
      reset(): void;
    };
  }
}

const appElement = document.querySelector<HTMLElement>('#app');
if (!appElement) throw new Error('E2E harness requires #app.');
const app: HTMLElement = appElement;

let game: Game;

function reset(): void {
  game?.dispose();
  const runtime = new RuntimeBootstrap({
    pack,
    locale: 'ru',
    resolveFileUrl: (file) => file,
  });
  const persistence = createBrowserProductionPersistenceGateway({
    browserWindow: window,
    runtimePersistence: runtime.persistence,
  });
  game = new Game(app, { runtime: runtime.presentation, persistence });
  game.start();
}

reset();

// This API exists only on the URMAN_E2E Vite route. Production never serves
// this module and does not receive a gameplay global.
window.__URMAN_E2E__ = Object.freeze({
  snapshot: () => structuredClone(game.snapshot()),
  restore: async (snapshot: object) => { await game.restore(snapshot); },
  reset,
});
