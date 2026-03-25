export enum TileType {
    GRASS = 0,
    DIRT = 1,
    ROAD = 2,
    WATER = 3
}

export interface MapCell {
    type: TileType;
    variant: number;
}

export interface MapEntity {
    id: string;
    type: 'building' | 'house' | 'mosque' | 'banya' | 'prop' | 'fence' | 'animal' | 'plot_base' | 'cemetery';
    name?: string;
    desc?: string;
    gx: number;
    gy: number;
    gw: number;
    gh: number;
    gd?: number; // Depth in grid units
    gz?: number; // Vertical offset
    style?: any;
    actions?: { label: string; fn: (game: any, scene: any) => void }[];
    invisible?: boolean;
}

export interface ForestDot {
    x: number;
    y: number;
    r: number;
    seed: number;
    variant: 'tree' | 'bush' | 'dead';
    fog: number;
    c?: string;
}

export interface VillageData {
    map: MapCell[][];
    entities: MapEntity[];
    forestDots?: ForestDot[];
}

export interface BuildingSocket {
    gx: number;
    gy: number;
}
