export const GRID_W = 48;
export const GRID_H = 48;

// Не делаем деревню идеально по центру
export const VILLAGE_CORE_X = 24;
export const VILLAGE_CORE_Y = 24;

// Главная дорога — это не просто X, а стартовая ось,
// вокруг которой потом можно строить органичные изгибы
export const MAIN_ROAD_START_X = 24;
export const MAIN_ROAD_START_Y = 2;
export const MAIN_ROAD_END_X = 24;
export const MAIN_ROAD_END_Y = 46;

// Радиусы и зоны (уменьшаем для компактности)
export const VILLAGE_CORE_RADIUS = 8;
export const VILLAGE_EDGE_RADIUS = 15;
export const FOREST_MARGIN = 4;
export const RIVER_BAND_NORTH = 8;

// Constants for generation
export const PROCEDURAL_SEED = 9182;

// Render
export const TILE_W = 64;
export const TILE_H = 32;

// Population / house assignment
export const VILLAGERS = [
    'fanis',
    'zarifa',
    'ildar',
    'gulnara',
    'mansur',
    'rashid',
    'nail',
    'rushania',
    'razilya'
];

// План деревни
export const TARGET_HOUSE_COUNT = 15;
export const HOUSE_PLOT_MIN_DIST = 4;
export const HOUSE_PLOT_MAX_DIST = 8;

// Декорации и хоз. зоны
export const MAX_LIVESTOCK_PENS = 5;
export const MAX_SHEDS = 7;
export const MAX_WELLS = 2;

// Северная река и мост
export const RIVER_MIN_X = 6;
export const RIVER_MAX_X = 56;
export const RIVER_ROW_MIN = 6;
export const RIVER_ROW_MAX = 13;
export const BRIDGE_TARGET_X = 20;