import { WORLD_CONFIG, AUTHORED_LAYOUT } from '../../config';
import { TileType, MapCell, RoadData, Point } from '../../types';

/**
 * generateMainStreet - Implements the authored village road skeleton.
 * Directs the North-South asphalt road, secondary roads, and side lanes.
 */
export function generateMainStreet(map: MapCell[][]): { 
    rd: RoadData; 
    civicAnchors: {
        mosque: Point;
        club: Point;
        council: Point;
        clinic: Point;
    } 
} {
    const spine: Point[] = [];

    // Helper to draw a line between two points on the map
    const drawRoad = (p1: Point, p2: Point, isAsphalt: boolean) => {
        let x0 = p1[0];
        let y0 = p1[1];
        const x1 = p2[0];
        const y1 = p2[1];

        const dx = Math.abs(x1 - x0);
        const dy = Math.abs(y1 - y0);
        const sx = (x0 < x1) ? 1 : -1;
        const sy = (y0 < y1) ? 1 : -1;
        let err = dx - dy;

        while (true) {
            if (x0 >= 0 && x0 < WORLD_CONFIG.GRID_W && y0 >= 0 && y0 < WORLD_CONFIG.GRID_H) {
                map[x0][y0] = { 
                    type: TileType.ROAD, 
                    variant: isAsphalt ? 10 : 0 // 10 = asphalt visually, 0 = dirt
                };
                // Make it slightly wider
                if (y0 + 1 < WORLD_CONFIG.GRID_H) map[x0][y0+1] = map[x0][y0];
                if (x0 + 1 < WORLD_CONFIG.GRID_W) map[x0+1][y0] = map[x0][y0];
                
                spine.push([x0, y0]);
            }

            if ((x0 === x1) && (y0 === y1)) break;
            const e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    };

    // 1. Draw Authored Main Road (Asphalt)
    for (let i = 0; i < AUTHORED_LAYOUT.roads.main.length - 1; i++) {
        drawRoad(AUTHORED_LAYOUT.roads.main[i] as any, AUTHORED_LAYOUT.roads.main[i+1] as any, true);
    }

    // 2. Draw Secondary Road (Dirt/Gravel)
    for (let i = 0; i < AUTHORED_LAYOUT.roads.secondary.length - 1; i++) {
        drawRoad(AUTHORED_LAYOUT.roads.secondary[i] as any, AUTHORED_LAYOUT.roads.secondary[i+1] as any, false);
    }

    // 3. Draw Side Lanes
    for (const lane of AUTHORED_LAYOUT.roads.lanes) {
        drawRoad(lane[0] as any, lane[1] as any, false);
    }

    // 4. Return fixed anchors for civic buildings
    const c = AUTHORED_LAYOUT.civic;
    return {
        rd: { spine, branches: [] },
        civicAnchors: {
            mosque:  [c.mosque.x, c.mosque.y],
            club:    [c.club.x, c.club.y],
            council: [c.selsovet.x, c.selsovet.y],
            clinic:  [c.clinic.x, c.clinic.y]
        }
    };
}
