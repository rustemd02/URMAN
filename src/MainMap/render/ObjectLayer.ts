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
     */
    public renderForest(
        ctx: CanvasRenderingContext2D,
        trees: readonly ForestDot[]
    ): void {
        for (const t of trees) {
            this.drawTree(ctx, t);
        }
    }

    private drawEntity(ctx: CanvasRenderingContext2D, e: MapEntity): void {
        if (e.subType === 'fence') {
            this.drawFence(ctx, e);
            return;
        }

        const { sx, sy } = IsoProjection.toScreen(e.gx, e.gy);
        const HW = 32;
        const HH = 16;
        const bHeight = e.style?.height || 22;
        const wallColor = e.style?.wallColor || '#c9a050';
        const roofColor = e.style?.roofColor || '#3a6b3a';

        const centerX = sx + (e.gw * HW) - (e.gh * HW);
        const bottomY = sy + (e.gw * HH) + (e.gh * HH);

        // 1. Shadow
        ctx.fillStyle = 'rgba(0,0,0,0.15)';
        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(sx + e.gw * HW, sy + e.gw * HH);
        ctx.lineTo(centerX, bottomY);
        ctx.lineTo(sx - e.gh * HW, sy + e.gh * HH);
        ctx.closePath();
        ctx.fill();

        // 2. Left Wall
        ctx.fillStyle = this.adjustColor(wallColor, -20);
        ctx.beginPath();
        ctx.moveTo(sx - e.gh * HW, sy + e.gh * HH);
        ctx.lineTo(centerX, bottomY);
        ctx.lineTo(centerX, bottomY - bHeight);
        ctx.lineTo(sx - e.gh * HW, sy + e.gh * HH - bHeight);
        ctx.closePath();
        ctx.fill();

        // 3. Right Wall
        ctx.fillStyle = wallColor;
        ctx.beginPath();
        ctx.moveTo(sx + e.gw * HW, sy + e.gw * HH);
        ctx.lineTo(centerX, bottomY);
        ctx.lineTo(centerX, bottomY - bHeight);
        ctx.lineTo(sx + e.gw * HW, sy + e.gw * HH - bHeight);
        ctx.closePath();
        ctx.fill();

        // 4. Roof
        ctx.fillStyle = roofColor;
        ctx.beginPath();
        ctx.moveTo(sx, sy - bHeight);
        ctx.lineTo(sx + e.gw * HW, sy + e.gw * HH - bHeight);
        ctx.lineTo(centerX, bottomY - bHeight);
        ctx.lineTo(sx - e.gh * HW, sy + e.gh * HH - bHeight);
        ctx.closePath();
        ctx.fill();

        // 5. Label
        if (e.name && e.subType !== 'fence') {
            ctx.fillStyle = 'white';
            ctx.font = '12px Philosopher, serif';
            ctx.textAlign = 'center';
            ctx.fillText(e.name, centerX, sy - 15);
        }
    }

    private drawFence(ctx: CanvasRenderingContext2D, e: MapEntity): void {
        const { sx, sy } = IsoProjection.toScreen(e.gx, e.gy);
        const HW = 32;
        const HH = 16;
        
        ctx.strokeStyle = 'rgba(255,255,255,0.1)';
        ctx.setLineDash([4, 4]);
        ctx.lineWidth = 1;
        
        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(sx + e.gw * HW, sy + e.gw * HH);
        ctx.lineTo(sx + e.gw * HW - e.gh * HW, sy + e.gw * HH + e.gh * HH);
        ctx.lineTo(sx - e.gh * HW, sy + e.gh * HH);
        ctx.closePath();
        ctx.stroke();
        ctx.setLineDash([]);
    }

    private adjustColor(color: string, amount: number): string {
        const clamp = (val: number) => Math.min(Math.max(val, 0), 255);
        if (!color.startsWith('#')) return color;
        const num = parseInt(color.slice(1), 16);
        let r = (num >> 16) + amount;
        let g = ((num >> 8) & 0x00FF) + amount;
        let b = (num & 0x0000FF) + amount;
        return "#" + (0x1000000 + clamp(r) * 0x10000 + clamp(g) * 0x100 + clamp(b)).toString(16).slice(1);
    }

    public drawTree(ctx: CanvasRenderingContext2D, t: ForestDot): void {
        const { sx, sy } = IsoProjection.toScreen(t.x, t.y);
        ctx.save();
        ctx.translate(Math.round(sx), Math.round(sy));
        ctx.scale(t.scale, t.scale);

        ctx.fillStyle = 'rgba(0,0,0,0.15)';
        ctx.beginPath();
        ctx.ellipse(0, 0, 8, 3, 0, 0, Math.PI * 2);
        ctx.fill();

        if (t.variant === 'pine') {
            ctx.fillStyle = '#114411';
            ctx.beginPath();
            ctx.moveTo(0, -25); ctx.lineTo(-8, -5); ctx.lineTo(8, -5);
            ctx.fill();
        } else {
            ctx.fillStyle = '#1a4a14';
            ctx.beginPath(); ctx.arc(0, -20, 10, 0, Math.PI * 2); ctx.fill();
        }
        ctx.restore();
    }
}
