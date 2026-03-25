import {
    GRID_W,
    GRID_H,
    VILLAGE_CORE_X,
    VILLAGE_CORE_Y,
    VILLAGE_CORE_RADIUS
} from './config';
import { MapCell } from './types';
import { RNG } from './rng';

export interface ForestDot {
    x: number;
    y: number;
    r: number;
    seed: number;
    variant: 'tree' | 'bush' | 'dead';
    fog: number;
}

function distToCore(x: number, y: number): number {
    const dx = x - VILLAGE_CORE_X;
    const dy = y - VILLAGE_CORE_Y;
    return Math.sqrt(dx * dx + dy * dy);
}

function hashNoise(x: number, y: number, seed: number): number {
    const n = Math.sin((x * 127.1 + y * 311.7 + seed * 74.7)) * 43758.5453123;
    return n - Math.floor(n);
}

function canPlace(
    dots: ForestDot[],
    x: number,
    y: number,
    minDist: number
): boolean {
    const minDistSq = minDist * minDist;
    for (let i = 0; i < dots.length; i++) {
        const dx = dots[i].x - x;
        const dy = dots[i].y - y;
        if (dx * dx + dy * dy < minDistSq) return false;
    }
    return true;
}

export function generateFoliage(map: MapCell[][], rng: RNG): ForestDot[] {
    const dots: ForestDot[] = [];
    const localSeed = rng.intRange(1, 999999);

    for (let x = 1; x < GRID_W - 1; x++) {
        for (let y = 1; y < GRID_H - 1; y++) {
            if (!map[x] || !map[x][y]) continue;
            if (map[x][y].type !== 0) continue; // only grass

            const dist = distToCore(x, y);
            const n = hashNoise(x, y, localSeed);
            const edgeBand = dist - VILLAGE_CORE_RADIUS;

            // Базовая плотность по зонам
            let prob = 0;
            let minDist = 0;
            let variant: ForestDot['variant'] = 'tree';
            let fog = 0;

            // Совсем центр — почти чистый
            if (edgeBand < -2) {
                prob = 0.0;
            }
            // Окраина деревни — редкие кусты / одиночные деревца
            else if (edgeBand < 4) {
                prob = 0.03 + n * 0.04;
                minDist = 3.2;
                variant = n > 0.72 ? 'bush' : 'tree';
                fog = 0.05;
            }
            // Переходная зона
            else if (edgeBand < 10) {
                prob = 0.10 + n * 0.10;
                minDist = 2.8;
                variant = n > 0.82 ? 'dead' : n > 0.65 ? 'bush' : 'tree';
                fog = 0.12;
            }
            // Лесная кромка
            else if (edgeBand < 16) {
                prob = 0.28 + n * 0.18;
                minDist = 2.3;
                variant = n > 0.9 ? 'dead' : 'tree';
                fog = 0.2;
            }
            // Густой внешний лес
            else {
                prob = 0.45 + n * 0.22;
                minDist = 1.9;
                variant = n > 0.93 ? 'dead' : 'tree';
                fog = 0.32;
            }

            // Небольшие "карманы" и просветы, чтобы лес не был идеальным кольцом
            const clearingNoise = hashNoise(x * 2, y * 2, localSeed + 77);
            if (clearingNoise > 0.86 && edgeBand > 8) {
                prob *= 0.45;
            }

            if (!rng.chance(prob)) continue;
            if (!canPlace(dots, x, y, minDist)) continue;

            let scale = 1.0;

            if (variant === 'bush') {
                scale = rng.range(0.65, 1.05);
            } else if (variant === 'dead') {
                scale = rng.range(1.0, 1.45);
            } else {
                // Внешний лес чуть крупнее
                scale =
                    edgeBand > 16
                        ? rng.range(1.2, 2.0)
                        : rng.range(0.9, 1.6);
            }

            dots.push({
                x,
                y,
                r: scale,
                seed: rng.intRange(0, 5),
                variant,
                fog
            });
        }
    }

    // Для простых наземных объектов обычно лучше сортировать по нижней опорной точке / y-глубине
    dots.sort((a, b) => {
        if (a.y !== b.y) return a.y - b.y;
        return a.x - b.x;
    });

    return dots;
}