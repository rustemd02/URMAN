import { GRID_W, GRID_H } from './config';
import { MapCell, TileType, MapEntity } from './types';

export function generateRiver(map: MapCell[][], entities: MapEntity[]) {
    // Left-to-right on screen roughly corresponds to gx + gy = constant in grid
    // For a 48x48 map, center diagonal is around 24-30
    const diagonalBase = 28; // Crosses the main road near the north entries
    
    for (let x = 0; x < GRID_W; x++) {
        for (let y = 0; y < GRID_H; y++) {
            // Diagonal band check
            const diag = x + y;
            const variance = Math.sin(x * 0.2) * 1.5 + Math.sin(y * 0.25) * 1.5;
            const currentDiag = diag + variance;
            
            const riverWidth = 4 + Math.cos((x - y) * 0.1) * 2;
            
            if (Math.abs(currentDiag - diagonalBase) < riverWidth) {
                if (map[x] && map[x][y]) {
                    map[x][y].type = TileType.WATER;
                }
            }
        }
    }

    // Pier Placement (near expected Alsu location, now at North area)
    entities.push({
        id: 'alsu-pier', type: 'prop', name: 'Причал', gx: 12, gy: 16, gw: 3, gh: 4,
        style: { propType: 'pier' },
        desc: 'Старый деревянный причал у дома Алсу.'
    });
}
