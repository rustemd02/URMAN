import { GRID_W, GRID_H } from './config';
import { MapCell, MapEntity, BuildingSocket, TileType } from './types';
import { RNG } from './rng';
import { CHARACTERS } from '../../data/characters';

/**
 * v2 goals:
 * - real rectangle overlap instead of rough distance checks
 * - separate validation/planning/apply steps
 * - layout archetypes for variety
 * - safer NPC assignment
 * - less hardcoded symmetry
 * - modern TypeScript style
 */

type EntityKind =
    | 'plot_base'
    | 'house'
    | 'building'
    | 'mosque'
    | 'banya'
    | 'fence'
    | 'prop'
    | 'animal';

type OccupyingEntity = MapEntity & {
    gx: number;
    gy: number;
    gw: number;
    gh: number;
    type: EntityKind | string;
};

type PlotRect = Readonly<{
    gx: number;
    gy: number;
    gw: number;
    gh: number;
}>;

type HouseStyle = Readonly<{
    wallColor: string;
    roofColor: string;
    h?: number;
    propType?: string;
}>;

type PlannedEntity = Readonly<{
    id: string;
    type: EntityKind;
    gx: number;
    gy: number;
    gw: number;
    gh: number;
    gd?: number;
    name?: string;
    desc?: string;
    style?: HouseStyle;
}>;

type PathTile = Readonly<{
    x: number;
    y: number;
    type: TileType;
}>;

type HomesteadLayoutKind = 'compact' | 'wide' | 'garden_heavy' | 'edge_farm';

type HomesteadPlan = Readonly<{
    layout: HomesteadLayoutKind;
    plot: PlotRect;
    entities: readonly PlannedEntity[];
    pathTiles: readonly PathTile[];
    assignedVillagerId: string | null;
}>;

const HOUSE_WALLS = [
    '#c99040',
    '#6eb8b0',
    '#5aa060',
    '#d8a0b0',
    '#c0b090',
    '#3b9c8b',
] as const;

const HOUSE_ROOFS = [
    '#3a8a25',
    '#8a4060',
    '#25508a',
    '#8a3a3a',
    '#b54e2a',
] as const;

const HOMESTEAD_TYPES = [
    'compact',
    'wide',
    'garden_heavy',
    'edge_farm',
] as const satisfies readonly HomesteadLayoutKind[];

const BLOCKING_ENTITY_TYPES = new Set<string>([
    'house',
    'building',
    'mosque',
    'banya',
    'fence',
]);

function isBlockingEntity(entity: MapEntity): entity is OccupyingEntity {
    return (
        typeof entity?.gx === 'number' &&
        typeof entity?.gy === 'number' &&
        typeof entity?.gw === 'number' &&
        typeof entity?.gh === 'number' &&
        BLOCKING_ENTITY_TYPES.has(entity.type)
    );
}

function rectsOverlap(a: PlotRect, b: PlotRect): boolean {
    return (
        a.gx < b.gx + b.gw &&
        a.gx + a.gw > b.gx &&
        a.gy < b.gy + b.gh &&
        a.gy + a.gh > b.gy
    );
}

function inBounds(rect: PlotRect): boolean {
    return (
        rect.gx >= 2 &&
        rect.gy >= 2 &&
        rect.gx + rect.gw < GRID_W &&
        rect.gy + rect.gh < GRID_H
    );
}

function plotTouchesWater(rect: PlotRect, map: MapCell[][]): boolean {
    for (let x = rect.gx; x < rect.gx + rect.gw; x++) {
        for (let y = rect.gy; y < rect.gy + rect.gh; y++) {
            if (map[x]?.[y]?.type === TileType.WATER) return true;
        }
    }
    return false;
}

function plotBlockedByEntities(rect: PlotRect, entities: readonly MapEntity[]): boolean {
    return entities.some((entity) => {
        if (!isBlockingEntity(entity)) return false;
        return rectsOverlap(rect, {
            gx: entity.gx,
            gy: entity.gy,
            gw: entity.gw,
            gh: entity.gh,
        });
    });
}

function canPaintDirt(map: MapCell[][], x: number, y: number): boolean {
    const cell = map[x]?.[y];
    return !!cell && cell.type !== TileType.WATER;
}

function pickOne<T>(items: readonly T[], rng: RNG): T {
    return items[rng.intRange(0, items.length - 1)];
}

function clampToPlot(
    plot: PlotRect,
    gx: number,
    gy: number,
    gw: number,
    gh: number,
): PlotRect {
    const maxX = plot.gx + plot.gw - gw;
    const maxY = plot.gy + plot.gh - gh;
    return {
        gx: Math.max(plot.gx, Math.min(gx, maxX)),
        gy: Math.max(plot.gy, Math.min(gy, maxY)),
        gw,
        gh,
    };
}

function entityFromRect(
    base: Omit<PlannedEntity, 'gx' | 'gy' | 'gw' | 'gh'>,
    rect: PlotRect,
): PlannedEntity {
    return {
        ...base,
        gx: rect.gx,
        gy: rect.gy,
        gw: rect.gw,
        gh: rect.gh,
        gd: rect.gh,
    };
}

function getCharacter(charId: string | null) {
    return charId ? CHARACTERS[charId] ?? null : null;
}

function getHouseMeta(charId: string | null, fallbackIndex: number) {
    const character = getCharacter(charId);
    if (!character) {
        return {
            houseName: `Участок ${fallbackIndex + 1}`,
            houseDesc: 'Обычный деревенский дом.',
        };
    }

    const firstName = character.fullName.split(' ')[0] ?? character.fullName;
    return {
        houseName: `Дом: ${firstName}`,
        houseDesc: `${character.role}.`,
    };
}

function chooseVillager(villagers: readonly string[], successfulHouseIndex: number): string | null {
    return villagers[successfulHouseIndex] ?? null;
}

function makeBasePalette(seedIndex: number, rng: RNG): HouseStyle {
    const wallColor = HOUSE_WALLS[seedIndex % HOUSE_WALLS.length];
    const roofColor = HOUSE_ROOFS[(seedIndex + rng.intRange(0, HOUSE_ROOFS.length - 1)) % HOUSE_ROOFS.length];
    return { wallColor, roofColor };
}

function makePathTiles(plot: PlotRect, doorX: number, map: MapCell[][]): PathTile[] {
    const pathTiles: PathTile[] = [];

    for (let y = plot.gy - 2; y <= plot.gy; y++) {
        if (canPaintDirt(map, doorX, y)) {
            pathTiles.push({ x: doorX, y, type: TileType.DIRT });
        }
    }

    return pathTiles;
}

function createHomesteadPlan(
    sock: BuildingSocket,
    id: number,
    villagers: readonly string[],
    successfulHouseIndex: number,
    rng: RNG,
    map: MapCell[][],
): HomesteadPlan {
    const plot: PlotRect = {
        gx: sock.gx,
        gy: sock.gy,
        gw: rng.chance(0.5) ? 10 : 11,
        gh: rng.chance(0.5) ? 10 : 11,
    };

    const layout = pickOne(HOMESTEAD_TYPES, rng);
    const villagerId = chooseVillager(villagers, successfulHouseIndex);
    const { houseName, houseDesc } = getHouseMeta(villagerId, id);
    const palette = makeBasePalette(id, rng);

    const entities: PlannedEntity[] = [
        {
            id: `plot-base-${id}`,
            type: 'plot_base',
            gx: plot.gx,
            gy: plot.gy,
            gw: plot.gw - 1,
            gh: plot.gh - 1,
            gd: plot.gh - 1,
        },
        {
            id: `fence-${id}`,
            type: 'fence',
            gx: plot.gx,
            gy: plot.gy,
            gw: plot.gw - 1,
            gh: plot.gh - 1,
            gd: plot.gh - 1,
        },
    ];

    let houseRect: PlotRect;
    let doorX: number;

    switch (layout) {
        case 'compact': {
            houseRect = clampToPlot(
                plot,
                plot.gx + rng.intRange(1, 2),
                plot.gy + rng.intRange(1, 2),
                4,
                3,
            );
            doorX = houseRect.gx + 2;

            entities.push(
                entityFromRect(
                    {
                        id: villagerId ? `house-${villagerId}` : `house-proc-${id}`,
                        type: 'house',
                        name: houseName,
                        desc: houseDesc,
                        style: palette,
                    },
                    houseRect,
                ),
            );

            if (rng.chance(0.55)) {
                entities.push({
                    id: `banya-${id}`,
                    type: 'banya',
                    name: 'Баня',
                    gx: plot.gx + 6,
                    gy: plot.gy + 1,
                    gw: 2,
                    gh: 2,
                    style: { wallColor: '#5a3a1a', roofColor: '#1a2a0a', h: 15 },
                    desc: 'Маленькая банька. Пахнет березовыми вениками.',
                });
            }

            for (let i = 0; i < 3; i++) {
                entities.push({
                    id: `crop-${id}-${i}`,
                    type: 'prop',
                    name: 'Огород',
                    gx: plot.gx + 6 + (i % 2) * 2,
                    gy: plot.gy + 5 + Math.floor(i / 2) * 2,
                    gw: 1,
                    gh: 1,
                    style: { propType: 'vegetables' },
                });
            }
            break;
        }

        case 'wide': {
            houseRect = clampToPlot(
                plot,
                plot.gx + rng.intRange(2, 3),
                plot.gy + rng.intRange(1, 2),
                5,
                3,
            );
            doorX = houseRect.gx + 2;

            entities.push(
                entityFromRect(
                    {
                        id: villagerId ? `house-${villagerId}` : `house-proc-${id}`,
                        type: 'house',
                        name: houseName,
                        desc: houseDesc,
                        style: palette,
                    },
                    houseRect,
                ),
            );

            for (let i = 0; i < 4; i++) {
                entities.push({
                    id: `crop-${id}-${i}`,
                    type: 'prop',
                    name: 'Грядка',
                    gx: plot.gx + 1 + i * 2,
                    gy: plot.gy + 6,
                    gw: 1,
                    gh: 1,
                    style: { propType: 'vegetables' },
                });
            }
            break;
        }

        case 'garden_heavy': {
            houseRect = clampToPlot(
                plot,
                plot.gx + rng.intRange(1, 2),
                plot.gy + rng.intRange(1, 2),
                4,
                3,
            );
            doorX = houseRect.gx + 2;

            entities.push(
                entityFromRect(
                    {
                        id: villagerId ? `house-${villagerId}` : `house-proc-${id}`,
                        type: 'house',
                        name: houseName,
                        desc: houseDesc,
                        style: palette,
                    },
                    houseRect,
                ),
            );

            for (let row = 0; row < 2; row++) {
                for (let col = 0; col < 3; col++) {
                    entities.push({
                        id: `crop-${id}-${row}-${col}`,
                        type: 'prop',
                        name: row === 0 ? 'Огород' : 'Травы',
                        gx: plot.gx + 5 + col,
                        gy: plot.gy + 4 + row * 2,
                        gw: 1,
                        gh: 1,
                        style: { propType: row === 0 ? 'vegetables' : 'herbs' },
                    });
                }
            }

            if (rng.chance(0.4)) {
                entities.push({
                    id: `animal-${id}`,
                    type: 'animal',
                    name: rng.chance(0.5) ? '🐄' : '🐑',
                    gx: plot.gx + 2,
                    gy: plot.gy + 7,
                    gw: 1,
                    gh: 1,
                });
            }
            break;
        }

        case 'edge_farm': {
            houseRect = clampToPlot(
                plot,
                plot.gx + rng.intRange(1, 2),
                plot.gy + rng.intRange(2, 3),
                4,
                3,
            );
            doorX = houseRect.gx + 2;

            entities.push(
                entityFromRect(
                    {
                        id: villagerId ? `house-${villagerId}` : `house-proc-${id}`,
                        type: 'house',
                        name: houseName,
                        desc: houseDesc,
                        style: palette,
                    },
                    houseRect,
                ),
            );

            entities.push({
                id: `banya-${id}`,
                type: 'banya',
                name: 'Сарай',
                gx: plot.gx + 6,
                gy: plot.gy + 2,
                gw: 2,
                gh: 2,
                style: { wallColor: '#6f4c2b', roofColor: '#3a2b1a', h: 13 },
                desc: 'Небольшой хозблок у края участка.',
            });

            if (rng.chance(0.65)) {
                entities.push({
                    id: `animal-${id}`,
                    type: 'animal',
                    name: rng.chance(0.5) ? '🐄' : '🐑',
                    gx: plot.gx + 7,
                    gy: plot.gy + 6,
                    gw: 1,
                    gh: 1,
                });
            }

            for (let i = 0; i < 2; i++) {
                entities.push({
                    id: `crop-${id}-${i}`,
                    type: 'prop',
                    name: 'Огород',
                    gx: plot.gx + 1 + i * 2,
                    gy: plot.gy + 7,
                    gw: 1,
                    gh: 1,
                    style: { propType: 'vegetables' },
                });
            }
            break;
        }
    }

    return {
        layout,
        plot,
        entities,
        pathTiles: makePathTiles(plot, doorX, map),
        assignedVillagerId: villagerId,
    };
}

function validateHomesteadPlan(
    plan: HomesteadPlan,
    map: MapCell[][],
    entities: readonly MapEntity[],
): boolean {
    if (!inBounds(plan.plot)) return false;
    if (plotTouchesWater(plan.plot, map)) return false;
    if (plotBlockedByEntities(plan.plot, entities)) return false;

    for (const entity of plan.entities) {
        const rect: PlotRect = {
            gx: entity.gx,
            gy: entity.gy,
            gw: entity.gw,
            gh: entity.gh,
        };

        if (!inBounds(rect)) return false;
        if (plotTouchesWater(rect, map)) return false;
    }

    return true;
}

function applyHomesteadPlan(
    plan: HomesteadPlan,
    map: MapCell[][],
    entities: MapEntity[],
): void {
    entities.push(...plan.entities);

    for (const tile of plan.pathTiles) {
        if (canPaintDirt(map, tile.x, tile.y)) {
            map[tile.x]![tile.y]!.type = tile.type;
        }
    }
}

export function generateHouseOnPlot(
    sock: BuildingSocket,
    id: number,
    villagers: readonly string[],
    rng: RNG,
    map: MapCell[][],
    entities: MapEntity[],
    successfulHouseIndex = id,
): boolean {
    const plan = createHomesteadPlan(sock, id, villagers, successfulHouseIndex, rng, map);

    if (!validateHomesteadPlan(plan, map, entities)) {
        return false;
    }

    applyHomesteadPlan(plan, map, entities);
    return true;
}