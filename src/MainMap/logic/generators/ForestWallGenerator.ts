import { WORLD_CONFIG } from '../../config';
import { TileType, MapCell, ForestDot, ReservedZone } from '../../types';

/**
 * generateForestWall - Surrounds the village with a dense wall of trees.
 * Focuses on the northern "behind-the-river" woods and map edges.
 */
export function generateForestWall(
    map: MapCell[][],
    activeZones: readonly ReservedZone[]
): ForestDot[] {
    const dots: ForestDot[] = [];

    // Sample the grid in steps to control density
    for (let gy = 0; gy < WORLD_CONFIG.GRID_H; gy += 1.5) {
        for (let gx = 0; gx < WORLD_CONFIG.GRID_W; gx += 1.5) {
            
            const cellX = Math.floor(gx);
            const cellY = Math.floor(gy);
            if (!map[cellX]?.[cellY]) continue;

            // 1. Skip if on road or in water
            if (map[cellX][cellY].type === TileType.ROAD || map[cellX][cellY].type === TileType.WATER) continue;

            // 2. Reserved zone check (Landmarks, Houses, Cemetery)
            const isReserved = activeZones.some(z => 
                gx >= z.gx && gx < z.gx + z.gw &&
                gy >= z.gy && gy < z.gy + z.gh
            );
            if (isReserved) continue;

            // 3. Density calculation based on location (with Sinus Noise Clumping)
            const noise = Math.sin(gx * 0.45) * Math.cos(gy * 0.45);
            const distToCore = Math.hypot(gx - WORLD_CONFIG.CORE_X, gy - WORLD_CONFIG.CORE_Y);
            const isFarEdge = distToCore > WORLD_CONFIG.FOREST_START;
            const isNearClearing = distToCore < WORLD_CONFIG.CLEARING_RADIUS;
            const isNorthForest = gy < WORLD_CONFIG.RIVER_Y_BAND - 3;

            let treeProbability = 0;
            if (isNorthForest) {
                treeProbability = 0.9 + noise * 0.1; // Maximum density across the river
            } else if (distToCore > 30) {
                treeProbability = 0.95; // Absolute wall at map edges
            } else if (isFarEdge) {
                treeProbability = 0.75 + noise * 0.25; // Dense perimeter wall
            } else if (!isNearClearing && noise > 0) {
                treeProbability = 0.3; // More scattered trees in buffer
            }

            // Deterministic check based on coordinate seed
            const seedVal = Math.sin(gx * 12.5 + gy * 31.7) * 10000;
            const rand = seedVal - Math.floor(seedVal);

            if (rand < treeProbability) {
                // Minimal spacing check (simple grid skip + jitter)
                const jx = gx + (rand * 0.8 - 0.4);
                const jy = gy + (Math.cos(seedVal) * 0.8 - 0.4);

                dots.push({
                    x: jx,
                    y: jy,
                    scale: 0.9 + rand * 1.3,
                    seed: Math.floor(rand * 1000),
                    variant: rand > 0.7 ? 'oak' : rand > 0.1 ? 'pine' : 'dead',
                    fogFactor: isNorthForest ? 0.4 : isFarEdge ? 0.25 : 0
                });
            }
        }
    }

    // Painter's algorithm sort for top-down depth rendering later
    return dots.sort((a, b) => a.y - b.y);
}
