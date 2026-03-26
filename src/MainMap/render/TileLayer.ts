import { WORLD_CONFIG } from '../config';
import { TileType, MapCell } from '../types';
import { IsoProjection } from './IsoProjection';

/**
 * TileLayer - Responsibility: Efficiently draw the base ground grid.
 * Optimized for static tile rendering (Grass, Water, Roads).
 */
export class TileLayer {
    /**
     * Draws the ground tiles to a CanvasRenderingContext2D.
     * Iterates by logical grid (gx, gy) scanline.
     */
    public render(
        ctx: CanvasRenderingContext2D,
        map: readonly (readonly MapCell[])[]
    ): void {
        const gridW = WORLD_CONFIG.GRID_W;
        const gridH = WORLD_CONFIG.GRID_H;

        for (let gy = 0; gy < gridH; gy++) {
            for (let gx = 0; gx < gridW; gx++) {
                const cell = map[gx]?.[gy];
                if (!cell) continue;

                // 1. Get projected diamond corners
                const corners = IsoProjection.getTileCorners(gx, gy);

                // 2. Map TileType to a fill color
                ctx.fillStyle = this.getTileColor(cell);

                // 3. Draw the diamond path (integer-rounded)
                ctx.beginPath();
                ctx.moveTo(Math.round(corners[0].x), Math.round(corners[0].y));
                ctx.lineTo(Math.round(corners[1].x), Math.round(corners[1].y));
                ctx.lineTo(Math.round(corners[2].x), Math.round(corners[2].y));
                ctx.lineTo(Math.round(corners[3].x), Math.round(corners[3].y));
                ctx.closePath();
                ctx.fill();

                // 4. Subtle tile outline (optional, helps with readability)
                // ctx.strokeStyle = 'rgba(0,0,0,0.03)';
                // ctx.stroke();

                // 5. Build-time Coordinate labels (like Chess)
                if (gx === 0 || gy === 0) {
                    ctx.fillStyle = 'rgba(255,255,255,0.2)';
                    ctx.font = '8px monospace';
                    ctx.textAlign = 'center';
                    if (gx === 0) ctx.fillText(String(gy), corners[3].x - 10, corners[3].y);
                    if (gy === 0) ctx.fillText(String.fromCharCode(65 + (gx % 26)) + (gx > 25 ? Math.floor(gx/26) : ''), corners[0].x, corners[0].y - 10);
                }
            }
        }
    }

    /**
     * Internal color mapping for terrain.
     */
    private getTileColor(cell: MapCell): string {
        switch (cell.type) {
            case TileType.WATER:
                return WORLD_CONFIG.COLORS.RIVER_WATER;
            case TileType.ROAD:
                return cell.variant === 10 ? WORLD_CONFIG.COLORS.ROAD_ASPHALT : WORLD_CONFIG.COLORS.ROAD_DIRT;
            case TileType.DIRT:
                return '#4a3a25'; // slightly darker yard dirt
            case TileType.CLEARING:
                return '#2a4a1a'; // lighter village grass
            case TileType.GRASS:
                return WORLD_CONFIG.COLORS.GRASS_CLEAR;
            case TileType.EMPTY:
                return WORLD_CONFIG.COLORS.FOREST_DEEP;
            default:
                return '#000000';
        }
    }
}
