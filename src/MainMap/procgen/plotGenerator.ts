import { GRID_W, GRID_H } from './config';
import { BuildingSocket } from './types';
import { RNG } from './rng';

export function generatePlotSockets(rng: RNG, mainX: number, mainCrossY: number[]): BuildingSocket[] {
    const sockets: BuildingSocket[] = [];

    // Avoid the river area (x + y < diagonal threshold)
    const RIVER_DIAG_LIMIT = 32;

    // Distribute plots along the main vertical spine
    for (let ay = 10; ay < GRID_H - 10; ay += 8) {
        if (ay + mainX < RIVER_DIAG_LIMIT) continue;

        const cx = mainX + Math.floor(Math.sin(ay * 0.1) * 3);
        // Left side plots
        if (rng.chance(0.85)) sockets.push({ gx: cx - 10, gy: ay });
        // Right side plots
        if (rng.chance(0.85)) sockets.push({ gx: cx + 4, gy: ay });
    }

    // Horizontal road plots
    for (let yy of mainCrossY) {
        for (let xx = 6; xx < GRID_W - 6; xx += 12) {
            if (xx + yy < RIVER_DIAG_LIMIT) continue;
            if (Math.abs(xx - mainX) < 8) continue; // Keep spine clear
            if (rng.chance(0.65)) sockets.push({ gx: xx, gy: yy - 6 });
        }
    }

    return sockets;
}
