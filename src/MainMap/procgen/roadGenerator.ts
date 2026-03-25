import { GRID_W, GRID_H, VILLAGE_CORE_X, VILLAGE_CORE_Y } from './config';
import { MapCell, TileType } from './types';

export function generateRoads(map: MapCell[][]) {
    const mainX = VILLAGE_CORE_X;
    const mainCrossY = [VILLAGE_CORE_Y - 17, VILLAGE_CORE_Y + 3, VILLAGE_CORE_Y + 23];

    // 2. CURVILINEAR STREETS & BRANCHES (Spine Generation)
    // Main vertical spine
    for (let y = 5; y < GRID_H; y++) {
        const rx = mainX + Math.floor(Math.sin(y * 0.08) * 5);
        for (let dx = -1; dx <= 1; dx++) {
            if (map[rx + dx] && map[rx + dx][y] && map[rx + dx][y].type !== TileType.WATER) {
                map[rx + dx][y].type = TileType.ROAD;
            }
        }
    }

    // Horizontal organic branches
    for (let yy of mainCrossY) {
        for (let x = 5; x < GRID_W - 5; x++) {
            const offY = yy + Math.floor(Math.sin(x * 0.15) * 3);
            for (let dy = -1; dy <= 0; dy++) {
                if (map[x] && map[x][offY + dy] && map[x][offY + dy].type !== TileType.WATER) {
                    map[x][offY + dy].type = TileType.ROAD;
                }
            }
        }
    }

    return { mainX, mainCrossY };
}
