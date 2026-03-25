import {
    GRID_W,
    GRID_H,
    PROCEDURAL_SEED,
    VILLAGERS,
    VILLAGE_CORE_X,
    VILLAGE_CORE_Y,
} from './config';
import { VillageData, MapCell, TileType, MapEntity, ForestDot } from './types';
import { RNG } from './rng';
import { generateRoads } from './roadGenerator';
import { generateRiver } from './riverGenerator';
import { generatePlotSockets } from './plotGenerator';
import { generateHouseOnPlot } from './houseBuilder';
import { generateFoliage } from './foliageGenerator';

type Rect = Readonly<{
    gx: number;
    gy: number;
    gw: number;
    gh: number;
}>;

type ReservedZone = Readonly<{
    id: string;
    rect: Rect;
    reason: 'mosque' | 'zirat' | 'bridge' | 'landmark' | 'river-buffer';
}>;

type WorldDebugInfo = Readonly<{
    seed: number;
    socketCount: number;
    placedHouseCount: number;
    rejectedSocketCount: number;
    reservedZoneCount: number;
}>;

const BASE_TILE_VARIANTS = [0, 0, 0, 0, 1] as const;

function makeEmptyMap(rng: RNG): MapCell[][] {
    const map: MapCell[][] = [];

    for (let x = 0; x < GRID_W; x++) {
        map[x] = [];
        for (let y = 0; y < GRID_H; y++) {
            map[x][y] = {
                type: TileType.GRASS,
                variant: BASE_TILE_VARIANTS[rng.intRange(0, BASE_TILE_VARIANTS.length - 1)],
            };
        }
    }

    return map;
}

function rectsOverlap(a: Rect, b: Rect): boolean {
    return (
        a.gx < b.gx + b.gw &&
        a.gx + a.gw > b.gx &&
        a.gy < b.gy + b.gh &&
        a.gy + a.gh > b.gy
    );
}

function pointInRect(x: number, y: number, rect: Rect): boolean {
    return x >= rect.gx && x < rect.gx + rect.gw && y >= rect.gy && y < rect.gy + rect.gh;
}

function reserveZones(): readonly ReservedZone[] {
    return [
        {
            id: 'reserve-mosque',
            reason: 'mosque',
            rect: { gx: VILLAGE_CORE_X - 7, gy: VILLAGE_CORE_Y - 5, gw: 8, gh: 7 },
        },
        {
            id: 'reserve-zirat',
            reason: 'zirat',
            rect: { gx: 8, gy: GRID_H - 14, gw: 14, gh: 12 },
        },
    ] as const satisfies readonly ReservedZone[];
}

function socketHitsReserved(sock: { gx: number; gy: number }, zones: readonly ReservedZone[]): boolean {
    const plotGuess: Rect = { gx: sock.gx, gy: sock.gy, gw: 11, gh: 11 };
    return zones.some((z) => rectsOverlap(plotGuess, z.rect));
}

function entityRect(e: MapEntity): Rect | null {
    if (
        typeof (e as any).gx === 'number' &&
        typeof (e as any).gy === 'number' &&
        typeof (e as any).gw === 'number' &&
        typeof (e as any).gh === 'number'
    ) {
        return {
            gx: (e as any).gx,
            gy: (e as any).gy,
            gw: (e as any).gw,
            gh: (e as any).gh,
        };
    }
    return null;
}

function findNorthRiverBand(map: MapCell[][]): Rect | null {
    let minX = Number.POSITIVE_INFINITY;
    let maxX = Number.NEGATIVE_INFINITY;
    let minY = Number.POSITIVE_INFINITY;
    let maxY = Number.NEGATIVE_INFINITY;

    for (let x = 0; x < GRID_W; x++) {
        for (let y = 0; y < GRID_H; y++) {
            if (map[x]?.[y]?.type === TileType.WATER) {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
    }

    if (!Number.isFinite(minX)) return null;

    return {
        gx: minX,
        gy: minY,
        gw: maxX - minX + 1,
        gh: maxY - minY + 1,
    };
}

function buildBridgeEntity(map: MapCell[][], mainX: number): { bridge: MapEntity | null; zone: ReservedZone | null } {
    const riverBand = findNorthRiverBand(map);
    if (!riverBand) return { bridge: null, zone: null };

    const desiredCenterX = Math.max(riverBand.gx + 3, Math.min(riverBand.gx + riverBand.gw - 4, mainX + 6));
    const bridgeW = Math.min(12, riverBand.gw);
    const bridgeX = Math.max(riverBand.gx, desiredCenterX - Math.floor(bridgeW / 2));
    const bridgeY = riverBand.gy + Math.max(0, Math.floor(riverBand.gh / 2) - 1);

    const bridgeRect: Rect = {
        gx: bridgeX,
        gy: bridgeY,
        gw: bridgeW,
        gh: 3,
    };

    const bridge: MapEntity = {
        id: 'main-bridge',
        type: 'prop',
        name: 'Старый мост',
        gx: bridgeRect.gx,
        gy: bridgeRect.gy,
        gw: bridgeRect.gw,
        gh: bridgeRect.gh,
        gd: bridgeRect.gh,
        desc: 'Скрипучий северный мост через реку.',
        actions: [{ label: 'Перейти в лес', fn: (g: any) => g.scenes.switchScene('zirat') }],
    };

    return {
        bridge,
        zone: {
            id: 'reserve-bridge',
            reason: 'bridge',
            rect: {
                gx: bridgeRect.gx - 2,
                gy: Math.max(0, bridgeRect.gy - 2),
                gw: bridgeRect.gw + 4,
                gh: bridgeRect.gh + 4,
            },
        },
    };
}

function placeLandmarks(entities: MapEntity[]): void {
    entities.push(
        {
            id: 'mosque',
            type: 'mosque',
            name: 'Деревенская мечеть',
            gx: VILLAGE_CORE_X - 6,
            gy: VILLAGE_CORE_Y - 4,
            gw: 5,
            gh: 5,
            gd: 5,
            desc: 'Сердце деревни. Утренний азан слышен далеко в лесу.',
            actions: [{ label: 'Войти', fn: (g: any) => g.scenes.switchScene('mosque') }],
        },
        {
            id: 'zirat-comp',
            type: 'cemetery',
            name: 'Зират',
            gx: 10,
            gy: GRID_H - 10,
            gw: 10,
            gh: 8,
            gd: 8,
            desc: 'Тихое место. Здесь спят предки.',
        },
    );
}

function pruneSocketsByReservedZones<T extends { gx: number; gy: number }>(
    sockets: readonly T[],
    zones: readonly ReservedZone[],
): T[] {
    return sockets.filter((sock) => !socketHitsReserved(sock, zones));
}

export function generateVillageWorld(
    fixedBuildings: readonly MapEntity[] = [],
    seed: number = PROCEDURAL_SEED,
): VillageData & { debug: WorldDebugInfo; forestDots: ForestDot[] } {
    const rng = new RNG(seed);
    const map = makeEmptyMap(rng);
    const entities: MapEntity[] = [...fixedBuildings];

    const baseReserved = reserveZones();

    // Phase 1: terrain
    generateRiver(map, entities);
    const { mainX, mainCrossY } = generateRoads(map);

    // Phase 2: bridge inferred from river band instead of blind hardcode
    const { bridge, zone: bridgeZone } = buildBridgeEntity(map, mainX);
    const reservedZones = bridgeZone ? [...baseReserved, bridgeZone] : [...baseReserved];

    if (bridge) {
        entities.push(bridge);
    }

    // Phase 3: plot sockets near roads
    const rawSockets = generatePlotSockets(rng, mainX, mainCrossY);
    const sockets = pruneSocketsByReservedZones(rawSockets, reservedZones);

    // Phase 4: houses
    let placedHouseCount = 0;

    for (const sock of sockets) {
        const ok = generateHouseOnPlot(
            sock,
            placedHouseCount,
            VILLAGERS,
            rng,
            map,
            entities,
            placedHouseCount,
        );

        if (ok) {
            placedHouseCount++;
        }
    }

    // Phase 5: landmarks after reserving their zones from housing
    placeLandmarks(entities);

    // Optional cleanup: remove any accidentally overlapping fixed building with reserved zones
    // if your fixedBuildings are noisy; kept off by default.

    // Phase 6: atmosphere
    const forestDots = generateFoliage(map, rng);

    const debug: WorldDebugInfo = {
        seed,
        socketCount: sockets.length,
        placedHouseCount,
        rejectedSocketCount: sockets.length - placedHouseCount,
        reservedZoneCount: reservedZones.length,
    };

    return {
        map,
        entities,
        forestDots,
        debug,
    };
}