import { WorldData } from '../types';
import { IsoCamera } from './IsoCamera';
import { TileLayer } from './TileLayer';
import { ObjectLayer } from './ObjectLayer';
import { PlayerSprite } from './PlayerSprite';
import { IsoProjection } from './IsoProjection';

/**
 * MapRenderer - Responsibility: Orchestrates the layered render stack.
 * Manages static terrain (TileLayer), static buildings (ObjectLayer),
 * and dynamic player character (PlayerSprite).
 */
export class MapRenderer {
    private readonly tiles = new TileLayer();
    private readonly objects = new ObjectLayer();
    private readonly playerSprite = new PlayerSprite();

    /**
     * The main draw call. Coordinates all sub-layers.
     */
    public draw(
        ctx: CanvasRenderingContext2D,
        camera: IsoCamera,
        world: WorldData,
        playerPos: { gx: number; gy: number },
        walkPhase: number = 0,
        facingRight: boolean = true
    ): void {
        const { width, height } = ctx.canvas;

        // 1. Clear Frame (Deep Forest background color)
        ctx.fillStyle = '#0a1408';
        ctx.fillRect(0, 0, width, height);

        // 2. Begin Transformation
        ctx.save();
        
        ctx.translate(Math.round(camera.x), Math.round(camera.y));
        ctx.scale(camera.scale, camera.scale);

        // 3. Render Static Ground (Terrain)
        this.tiles.render(ctx, world.cells);

        // 4. Render Forest and Objects
        this.drawSortedWorld(ctx, world, playerPos, walkPhase, facingRight);

        ctx.restore();
    }

    /**
     * Coordinates the depth-sorted rendering of buildings, forest, and player.
     */
    private drawSortedWorld(
        ctx: CanvasRenderingContext2D,
        world: WorldData,
        playerPos: { gx: number; gy: number },
        walkPhase: number,
        facingRight: boolean
    ): void {
        const pDepth = IsoProjection.getDepth(playerPos.gx, playerPos.gy, 0.5);
        let pDrawn = false;
        
        // Render pre-sorted forest items
        for (const t of world.forest) {
            const tDepth = t.y + 0.5;
            if (!pDrawn && pDepth < tDepth) {
                this.renderPlayer(ctx, playerPos, walkPhase, facingRight);
                pDrawn = true;
            }
            this.objects.drawTree(ctx, t);
        }

        // Render sorted entities (Buildings)
        const sortedEntities = [...world.entities].sort((a,b) => 
            IsoProjection.getDepth(a.gx, a.gy, a.gh) - IsoProjection.getDepth(b.gx, b.gy, b.gh)
        );

        for (const e of sortedEntities) {
            this.objects.render(ctx, [e]);
        }

        if (!pDrawn) {
            this.renderPlayer(ctx, playerPos, walkPhase, facingRight);
        }
    }

    private renderPlayer(
        ctx: CanvasRenderingContext2D,
        pos: { gx: number; gy: number },
        phase: number,
        facing: boolean
    ): void {
        const { sx, sy } = IsoProjection.toScreen(pos.gx, pos.gy);
        this.playerSprite.draw(ctx, sx, sy, phase, facing);
    }
}
