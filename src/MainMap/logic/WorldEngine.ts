import { AUTHORED_LAYOUT, WORLD_CONFIG } from '../config';
import { TileType, MapCell, WorldData, ReservedZone, MapEntity, MainMapLandmarkSlot, MainMapWorldBindings } from '../types';

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
const REQUIRED_LANDMARKS: readonly MainMapLandmarkSlot[] = ['mosque', 'council', 'club', 'clinic', 'zirat'];

function nonEmpty(value: unknown): value is string {
    return typeof value === 'string' && value.trim().length > 0;
}

function validateActions(actions: readonly { readonly id: string; readonly label: string; readonly request: Readonly<Record<string, unknown>> }[], owner: string): void {
    const ids = new Set<string>();
    for (const action of actions) {
        if (!nonEmpty(action.id) || !nonEmpty(action.label) || !action.request || typeof action.request !== 'object' || Array.isArray(action.request)) {
            throw new Error(`MainMap dev config has an invalid action at ${owner}.`);
        }
        if (ids.has(action.id)) throw new Error(`MainMap dev config duplicates action ${action.id} at ${owner}.`);
        ids.add(action.id);
    }
}

function validateBindings(bindings: MainMapWorldBindings): void {
    if (!bindings || !Array.isArray(bindings.residents) || !Array.isArray(bindings.landmarks)) {
        throw new Error('MainMap dev config requires resident and landmark bindings.');
    }
    if (!nonEmpty(bindings.labels?.bathhouse) || !nonEmpty(bindings.labels?.shed) || !nonEmpty(bindings.labels?.unnamedResidencePrefix)) {
        throw new Error('MainMap dev config requires all presentation labels.');
    }
    const lotIndexes = new Set<number>();
    const entityIds = new Set<string>();
    for (const resident of bindings.residents) {
        if (!Number.isSafeInteger(resident.lotIndex) || resident.lotIndex < 0 || resident.lotIndex >= AUTHORED_LAYOUT.lots.length
            || !nonEmpty(resident.entityId) || !nonEmpty(resident.label) || !Array.isArray(resident.actions)) {
            throw new Error('MainMap dev config has an invalid resident binding.');
        }
        if (lotIndexes.has(resident.lotIndex)) throw new Error(`MainMap dev config duplicates resident lot ${resident.lotIndex}.`);
        if (entityIds.has(resident.entityId)) throw new Error(`MainMap dev config duplicates entity ${resident.entityId}.`);
        lotIndexes.add(resident.lotIndex);
        entityIds.add(resident.entityId);
        validateActions(resident.actions, `resident lot ${resident.lotIndex}`);
    }
    const slots = new Set<MainMapLandmarkSlot>();
    for (const landmark of bindings.landmarks) {
        if (!REQUIRED_LANDMARKS.includes(landmark.slot) || !nonEmpty(landmark.entityId) || !nonEmpty(landmark.label) || !Array.isArray(landmark.actions)) {
            throw new Error('MainMap dev config has an invalid landmark binding.');
        }
        if (slots.has(landmark.slot)) throw new Error(`MainMap dev config duplicates landmark ${landmark.slot}.`);
        if (entityIds.has(landmark.entityId)) throw new Error(`MainMap dev config duplicates entity ${landmark.entityId}.`);
        slots.add(landmark.slot);
        entityIds.add(landmark.entityId);
        validateActions(landmark.actions, `landmark ${landmark.slot}`);
    }
    for (const slot of REQUIRED_LANDMARKS) {
        if (!slots.has(slot)) throw new Error(`MainMap dev config is missing ${slot} landmark binding.`);
    }
}

export class WorldEngine {
    public constructor(private readonly bindings: MainMapWorldBindings) {
        validateBindings(bindings);
    }

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
        const civicRes = generateCivicCore(streetRes.civicAnchors, this.bindings.landmarks);
        allEntities.push(...civicRes.entities);
        activeZones.push(...civicRes.zones);

        // 4. Cemetery
        const cemeteryRes = generateCemetery(this.bindings.landmarks);
        allEntities.push(cemeteryRes.entity);
        activeZones.push(cemeteryRes.zone);

        // 5. Homesteads (15 houses)
        const homeRes = generateHomesteads(cells, streetRes.rd.spine, [...activeZones], this.bindings.residents, this.bindings.labels);
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
