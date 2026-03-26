import { WORLD_CONFIG } from '../config';
import { TileType, MapCell, WorldData, ReservedZone, MapEntity } from '../types';

// Pure Functional Generators
import { generateRiver } from './generators/RiverGenerator';
import { generateMainStreet } from './generators/MainStreetGenerator';
import { generateCivicCore } from './generators/CivicCoreGenerator';
import { generateCemetery } from './generators/CemeteryGenerator';
import { generateHomesteads } from './generators/HomesteadGenerator';
import { generateForestWall } from './generators/ForestWallGenerator';

/**
 * WorldEngine - The main orchestrator of the v3 Tatar Village generation.
 * Coordinates independent generator functions in a strict order of priority.
 */
export class WorldEngine {
    /**
     * Runs the full generation pipeline and returns the complete world state.
     */
    public generate(): WorldData {
        // 0. Initialize empty grid (base layer)
        const cells: MapCell[][] = [];
        for (let x = 0; x < WORLD_CONFIG.GRID_W; x++) {
            cells[x] = [];
            for (let y = 0; y < WORLD_CONFIG.GRID_H; y++) {
                cells[x][y] = { type: TileType.GRASS, variant: (x + y * 13) % 8 };
            }
        }

        const allEntities: MapEntity[] = [];
        const activeZones: ReservedZone[] = [];

        // 1. River
        const riverRes = generateRiver(cells);

        // 2. Main Street
        const streetRes = generateMainStreet(cells);

        // 3. Civic Core
        const civicRes = generateCivicCore(streetRes.civicAnchors);
        allEntities.push(...civicRes.entities);
        activeZones.push(...civicRes.zones);

        // 4. Cemetery
        const cemeteryRes = generateCemetery();
        allEntities.push(cemeteryRes.entity);
        activeZones.push(cemeteryRes.zone);

        // 5. Homesteads (15 houses)
        const homeRes = generateHomesteads(cells, streetRes.rd.spine, [...activeZones]);
        allEntities.push(...homeRes.entities);
        activeZones.push(...homeRes.houseZones);

        // 6. Forest Wall
        const forestDots = generateForestWall(cells, activeZones);

        // Final consolidation into WorldData
        return {
            cells,
            entities: allEntities,
            forest: forestDots,
            zones: activeZones,
            riv: riverRes,
            rd: streetRes.rd
        };
    }
}
