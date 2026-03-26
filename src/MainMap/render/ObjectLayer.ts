import { MapEntity, ForestDot } from '../types';
import { IsoProjection } from './IsoProjection';

/**
 * ObjectLayer - Responsibility: Render world buildings and static landmarks.
 * Handles depth sorting and projection for fixed-position entities.
 */
export class ObjectLayer {
    /**
     * Renders static entities (buildings, mosque, etc.) to the context.
     * Uses painter's algorithm via pre-calculated depth keys.
     */
    public render(
        ctx: CanvasRenderingContext2D,
        entities: readonly MapEntity[]
    ): void {
        const sorted = [...entities].sort((a, b) => {
            const dA = IsoProjection.getDepth(a.gx, a.gy, a.gh);
            const dB = IsoProjection.getDepth(b.gx, b.gy, b.gh);
            return dA - dB || a.id.localeCompare(b.id);
        });

        for (const e of sorted) {
            this.drawEntity(ctx, e);
        }
    }

    /**
     * Renders a batch of trees. 
     * Expects trees to be pre-sorted by Y for efficiency.
     */
    public renderForest(
        ctx: CanvasRenderingContext2D,
        trees: readonly ForestDot[]
    ): void {
        for (const t of trees) {
            this.drawTree(ctx, t);
        }
    }

    /**
     * Internal: Basic isometric box / sprite rendering for buildings.
     */
    private drawEntity(ctx: CanvasRenderingContext2D, e: MapEntity): void {
        const { sx, sy } = IsoProjection.toScreen(e.gx + e.gw / 2, e.gy + e.gh / 2);
        
        const pxX = Math.round(sx);
        const pxY = Math.round(sy);

        // Visual properties
        const wallColor = e.style?.wallColor || '#c9a050';
        const roofColor = e.style?.roofColor || '#3a6b3a';
        const bHeight = e.style?.height || 22;

        const halfPW = Math.round((e.gw * 32) / 2); 
        const halfPH = Math.round((e.gh * 16) / 2); 

        // 1. Shadow
        ctx.fillStyle = 'rgba(0,0,0,0.1)';
        ctx.beginPath();
        ctx.ellipse(pxX, pxY, halfPW, halfPH, 0, 0, Math.PI * 2);
        ctx.fill();

        // 2. Walls
        ctx.fillStyle = wallColor;
        ctx.beginPath();
        ctx.moveTo(pxX - halfPW, pxY);
        ctx.lineTo(pxX, pxY + halfPH);
        ctx.lineTo(pxX + halfPW, pxY);
        ctx.lineTo(pxX + halfPW, pxY - bHeight);
        ctx.lineTo(pxX, pxY + halfPH - bHeight);
        ctx.lineTo(pxX - halfPW, pxY - bHeight);
        ctx.closePath();
        ctx.fill();

        // 3. Roof
        ctx.fillStyle = roofColor;
        ctx.beginPath();
        ctx.moveTo(pxX - halfPW, pxY - bHeight);
        ctx.lineTo(pxX, pxY + halfPH - bHeight);
        ctx.lineTo(pxX + halfPW, pxY - bHeight);
        ctx.lineTo(pxX, pxY - halfPH - bHeight - 6);
        ctx.closePath();
        ctx.fill();

        // 4. Label
        if (e.name) {
            ctx.fillStyle = 'rgba(255,255,255,0.8)';
            ctx.font = '10px Philosopher, serif';
            ctx.textAlign = 'center';
            ctx.fillText(e.name, pxX, pxY - bHeight - 12);
        }
    }

    /**
     * Internal: Basic tree drawing.
     */
    public drawTree(ctx: CanvasRenderingContext2D, t: ForestDot): void {
        const { sx, sy } = IsoProjection.toScreen(t.x, t.y);
        const pxX = Math.round(sx);
        const pxY = Math.round(sy);

        ctx.save();
        ctx.translate(pxX, pxY);
        ctx.scale(t.scale, t.scale);

        // 1. Shadow
        ctx.fillStyle = 'rgba(0,0,0,0.15)';
        ctx.beginPath();
        ctx.ellipse(0, 0, 8, 3, 0, 0, Math.PI * 2);
        ctx.fill();

        if (t.variant === 'pine') {
            ctx.fillStyle = '#114411';
            ctx.beginPath();
            ctx.moveTo(0, -25);
            ctx.lineTo(-8, -5);
            ctx.lineTo(8, -5);
            ctx.fill();
            ctx.beginPath();
            ctx.moveTo(0, -35);
            ctx.lineTo(-6, -15);
            ctx.lineTo(6, -15);
            ctx.fill();
        } else if (t.variant === 'oak') {
            ctx.fillStyle = '#1a4a14';
            ctx.beginPath();
            ctx.arc(0, -20, 10, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#3a2a1a'; // trunk
            ctx.fillRect(-1.5, -10, 3, 10);
        } else {
            ctx.fillStyle = '#2a2a2a';
            ctx.beginPath();
            ctx.moveTo(0, -10);
            ctx.lineTo(-4, 0);
            ctx.lineTo(4, 0);
            ctx.fill();
        }

        ctx.restore();
    }
}
