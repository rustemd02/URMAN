import pack from 'virtual:urman-content-catalog';

import { Game } from './game/Game';
import { RuntimeBootstrap } from './runtime/bootstrap/runtime-bootstrap.mjs';
import { createBrowserProductionPersistenceGateway } from './runtime/persistence/production-persistence-gateway.mjs';

document.addEventListener('DOMContentLoaded', () => {
    const container = document.getElementById('app');
    if (!container) throw new Error('URMAN composition root requires #app.');
    const runtime = new RuntimeBootstrap({
        pack,
        locale: 'ru',
        resolveFileUrl: (file) => file,
    });
    const persistence = createBrowserProductionPersistenceGateway({
        browserWindow: window,
        runtimePersistence: runtime.persistence,
    });
    const game = new Game(container, { runtime: runtime.presentation, persistence });
    game.start();
});
