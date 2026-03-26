import { WORLD_CONFIG } from '../config';

const HW = WORLD_CONFIG.TILE_W / 2;
const HH = WORLD_CONFIG.TILE_H / 2;

/**
 * IsoProjection - Pure math for 2:1 isometric (Diamond grid) transformations.
 */
export const IsoProjection = {
    /** Projects grid coordinates to local world screen coordinates. */
    toScreen(gx: number, gy: number): { sx: number; sy: number } {
        return {
            sx: (gx - gy) * HW,
            sy: (gx + gy) * HH
        };
    },

    /** Unprojects local world screen coordinates back to grid coordinates. */
    toGrid(sx: number, sy: number): { gx: number; gy: number } {
        return {
            gx: (sx / HW + sy / HH) / 2,
            gy: (sy / HH - sx / HW) / 2
        };
    },

    /** Returns the 4 local screen corners of a tile at (gx, gy). */
    getTileCorners(gx: number, gy: number): { x: number; y: number }[] {
        const { sx, sy } = this.toScreen(gx, gy);
        return [
            { x: sx,      y: sy - HH }, // Top
            { x: sx + HW, y: sy      }, // Right
            { x: sx,      y: sy + HH }, // Bottom
            { x: sx - HW, y: sy      }  // Left
        ];
    },

    /** Calculates a depth weight for Z-sorting (Painter's algorithm). */
    getDepth(gx: number, gy: number, gh = 1): number {
        return (gy + gh) + (gx * 0.0001);
    }
} as const;
