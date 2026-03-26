import { WORLD_CONFIG } from '../../config';
import { TileType, MapCell, RiverData } from '../../types';

/**
 * generateRiver - Creates a wide water band in the north.
 * Aligned with the authored bridge at x=32.
 */
export function generateRiver(map: MapCell[][]): RiverData {
    const yCenter = WORLD_CONFIG.RIVER_Y_BAND;
    const width = WORLD_CONFIG.RIVER_WIDTH;

    for (let gy = 0; gy < WORLD_CONFIG.GRID_H; gy++) {
        for (let gx = 0; gx < WORLD_CONFIG.GRID_W; gx++) {
            
            // Wavy river bank
            const wave = Math.sin(gx * 0.2) * 1.5;
            const dist = Math.abs(gy - (yCenter + wave));

            if (dist < width / 2) {
                // Bridge placeholder: Don't paint water where the main road crosses (x=32)
                const isBridgeZone = (gx >= 31 && gx <= 33);
                
                if (isBridgeZone) {
                    map[gx][gy] = { type: TileType.ROAD, variant: 10 }; // Asphalt Bridge
                } else {
                    map[gx][gy] = { type: TileType.WATER, variant: 0 };
                }
            }
        }
    }

    return {
        nodes: [[0, yCenter], [WORLD_CONFIG.GRID_W - 1, yCenter]],
        widthScale: [1, 1]
    };
}
