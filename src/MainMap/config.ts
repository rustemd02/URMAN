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
    CLEARING_RADIUS: 18, 
    FOREST_START: 28,    

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
            [32, 12], [32, 18], [33, 25], [32, 35], [31, 45], [32, 55], [32, 63]
        ],
        secondary: [
            [22, 16], [21, 24], [22, 32], [21, 44], [20, 52]
        ],
        lanes: [
            [[22, 20], [32, 20]], // Connection 1
            [[22, 38], [32, 38]], // Connection 2
            [[32, 30], [42, 30]], // Lane A
            [[32, 50], [42, 50]], // Lane B
            [[21, 48], [12, 48]]  // Lane C
        ]
    },

    // 2. Key Landmarks
    civic: {
        mosque:   { x: 38, y: 15, w: 5, h: 6 },
        selsovet: { x: 26, y: 22, w: 4, h: 4 },
        club:     { x: 38, y: 32, w: 5, h: 5 },
        clinic:   { x: 27, y: 40, w: 3, h: 3 }
    },

    // 3. Sacred Ground
    zirat: { x: 8, y: 20, w: 7, h: 7 },

    // 4. Resident Lot Anchors (15 houses)
    lots: [
        [36, 18], [36, 26], [36, 38], [36, 52], [36, 58], // Along Main (Right)
        [28, 16], [28, 28], [28, 35], [28, 48], [27, 56], // Along Main (Left)
        [16, 22], [16, 32], [16, 42], [15, 52], [46, 30]  // Secondary / Lanes
    ]
} as const;

export type WorldConfig = typeof WORLD_CONFIG;
