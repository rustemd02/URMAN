/**
 * config.ts - Static World Configuration and Authored Layout for the Tatar Village Map.
 * Used by all generators and renderers to ensure consistency.
 */

export const WORLD_CONFIG = {
    // Grid dimensions
    GRID_W: 64,
    GRID_H: 64,

    // Isometric tile size (2:1 ratio)
    TILE_W: 64,
    TILE_H: 32,

    // Village center (logical core)
    CORE_X: 32,
    CORE_Y: 32,

    // Radii for procedural zones
    CLEARING_RADIUS: 14, 
    FOREST_START: 22,    

    // River placement (logical left-to-right flow)
    RIVER_Y_BAND: 10,    
    RIVER_WIDTH: 5,      

    // Seed for deterministic generation
    PROCEDURAL_SEED: 713,

    // Visuals
    COLORS: {
        FOREST_DEEP: '#0a1408',
        GRASS_CLEAR: '#1a2a16',
        RIVER_WATER: '#1e3050',
        ROAD_DIRT:   '#5a4a35',
        ROAD_ASPHALT: '#333333',
    }
} as const;

/**
 * AUTHORED_LAYOUT - The "Level Design" source of truth.
 * Explicitly defines the bones of the village.
 */
export const AUTHORED_LAYOUT = {
    // 1. Roads (Main Asphalt, Secondary, Side Lanes)
    // Structured as [x, y] point arrays
    roads: {
        main: [
            [32, 0], [32, 63]
        ],
        secondary: [
            [22, 16], [21, 24], [22, 32], [21, 44], [20, 52]
        ],
        extra_west: [
            [10, 10], [10, 55]
        ],
        extra_east: [
            [54, 10], [54, 55]
        ],
        lanes: [
            [[22, 20], [32, 20]], // Connection 1
            [[22, 38], [32, 38]], // Connection 2
            [[32, 30], [42, 30]], // Lane A
            [[32, 50], [42, 50]], // Lane B
            [[21, 48], [12, 48]], // Lane C
            [[10, 20], [22, 20]], // West connect
            [[42, 30], [54, 30]]  // East connect
        ]
    },

    // 2. Key Landmarks
    civic: {
        mosque:   { x: 38, y: 15, w: 5, h: 6 },
        selsovet: { x: 26, y: 22, w: 4, h: 4 },
        club:     { x: 38, y: 32, w: 5, h: 5 },
        clinic:   { x: 27, y: 40, w: 3, h: 3 }
    },

    // 3. Sacred Ground (Southwest corner)
    zirat: { x: 5, y: 50, w: 6, h: 6 },

    // 4. Resident Lot Anchors (15 houses)
    lots: [
        [37, 18], [37, 26], [37, 38], [37, 52], [37, 58], // Along Main (Right, shifted from 36)
        [27, 16], [27, 28], [27, 35], [27, 48], [26, 56], // Along Main (Left, shifted from 28/27)
        [16, 22], [16, 32], [15, 42], [14, 52], [47, 30]  // Secondary / Lanes (further out)
    ]
} as const;

export type WorldConfig = typeof WORLD_CONFIG;
