/**
 * types.ts - Pure Data Contracts for the World Map.
 * No logic, only strict TypeScript definitions.
 */

// 1. Fundamental Shapes
export type Point = readonly [number, number];   // [gx, gy]
export type Rect = {
    readonly gx: number;
    readonly gy: number;
    readonly gw: number;
    readonly gh: number;
};

// 2. Tile Definitions
export enum TileType {
    EMPTY,        // Deep forest shadow
    GRASS,        // Main forest floor
    CLEARING,     // Shorter village grass
    DIRT,         // Paths and yards
    ROAD,         // Major muddy village street
    WATER,        // River cells
    ROCK          // Decorative / impassable rocks
}

export interface MapCell {
    readonly type: TileType;
    readonly variant: number;  // 0-7 for random sprite tiling
}

// 3. ProcGen Specific Contracts
export interface ReservedZone extends Rect {
    readonly purpose: 'mosque' | 'house' | 'path' | 'well' | 'cemetery';
    readonly priority: number;
}

export interface RiverData {
    readonly nodes: readonly Point[];
    readonly widthScale: number[];  // Variable width along the path
}

export interface RoadData {
    readonly spine: readonly Point[];
    readonly branches: readonly (readonly Point[])[];
}

export interface ForestDot {
    readonly x: number;
    readonly y: number;
    readonly scale: number;
    readonly seed: number;
    readonly variant: 'pine' | 'oak' | 'bush' | 'dead';
    readonly fogFactor: number; // 0..1 alpha for fog/mist layer
}

// 4. Integrated World State
export type EntityType = 
    | 'building'  // mosque, club, clinic, house
    | 'prop'      // fence, cart, well, bench
    | 'animal'    // chicken, goat, goose
    | 'special';  // zirat (cemetery), bridge

export interface EntityStyle {
    readonly wallColor?: string;
    readonly roofColor?: string;
    readonly height?: number;
}

/**
 * A dev-greybox action is intentionally opaque to the map.  The host receives
 * this request through the module boundary; the map never performs a scene
 * switch or interprets a campaign token itself.
 */
export interface MainMapAction {
    readonly id: string;
    readonly label: string;
    readonly request: Readonly<Record<string, unknown>>;
}

/**
 * Configurable household input.  `lotIndex` is a geometry slot only, not a
 * character or story identifier, so a different dev pack can bind any set of
 * residents without changing generator code.
 */
export interface MainMapResidentBinding {
    readonly lotIndex: number;
    readonly entityId: string;
    readonly label: string;
    readonly actions: readonly MainMapAction[];
    readonly isPrimaryResidence?: boolean;
}

export type MainMapLandmarkSlot = 'mosque' | 'council' | 'club' | 'clinic' | 'zirat';

/** Labels and actions for fixed greybox geometry are authored by the caller. */
export interface MainMapLandmarkBinding {
    readonly slot: MainMapLandmarkSlot;
    readonly entityId: string;
    readonly label: string;
    readonly description?: string;
    readonly actions: readonly MainMapAction[];
}

export interface MainMapPresentationLabels {
    readonly bathhouse: string;
    readonly shed: string;
    readonly unnamedResidencePrefix: string;
}

export interface MainMapWorldBindings {
    readonly residents: readonly MainMapResidentBinding[];
    readonly landmarks: readonly MainMapLandmarkBinding[];
    readonly labels: MainMapPresentationLabels;
}

export interface MapEntity extends Rect {
    readonly id: string;
    readonly type: EntityType;
    readonly subType?: string;    // specific house id or prop name
    readonly name?: string;
    readonly desc?: string;
    readonly zOrder?: number;     // manual depth override if needed
    readonly style?: EntityStyle;
    readonly actions?: readonly MainMapAction[];
}

export interface WorldData {
    readonly cells: readonly (readonly MapCell[])[];
    readonly entities: readonly MapEntity[];
    readonly forest: readonly ForestDot[];
    readonly zones: readonly ReservedZone[];
    readonly riv: RiverData;
    readonly rd: RoadData;
}
