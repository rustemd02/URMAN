import { BaseScene } from '../scenes/BaseScene';
import { WorldEngine } from './logic/WorldEngine';
import { MapRenderer } from './render/MapRenderer';
import { IsoCamera } from './render/IsoCamera';
import { IsoProjection } from './render/IsoProjection';
import { WorldData, MapEntity } from './types';
import { WORLD_CONFIG } from './config';

/**
 * MainMapScene - Responsibility: Orchestrates the Tatar Village Map.
 */
export class MainMapScene extends BaseScene {
    private canvas!: HTMLCanvasElement;
    private ctx!: CanvasRenderingContext2D;
    private modal!: HTMLElement;

    private world!: WorldData;
    private camera = new IsoCamera();
    private renderer = new MapRenderer();

    // Player State
    private px: number = WORLD_CONFIG.CORE_X;
    private py: number = WORLD_CONFIG.CORE_Y;
    private tx: number = WORLD_CONFIG.CORE_X;
    private ty: number = WORLD_CONFIG.CORE_Y;
    private isWalking = false;
    private walkPhase = 0;
    private isFacingRight = true;

    // Interaction / Input
    private isDragging = false;
    private dragMoved = false;
    private lastDragX = 0;
    private lastDragY = 0;
    private lastTs = 0;
    private rafId = 0;
    private keys = new Set<string>();

    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
        <div id="urman-scene" style="position:relative; width:100vw; height:100vh; overflow:hidden; background:#0a1408;">
            <canvas id="mc" style="display:block; cursor:crosshair; width:100%; height:100%; touch-action:none;"></canvas>
            <div id="hud" style="position:absolute; top:12px; left:12px; color:white; font-family:Philosopher,serif; pointer-events:none; text-shadow:1px 1px 4px black;">
                <h3 style="margin:0">Кара-Урман</h3>
                <p style="margin:0; opacity:0.75; font-size:12px;">Тяните — камера · WASD — движение · Колесо — зум</p>
            </div>
            <div id="m-ov" style="display:none; position:absolute; inset:0; background:rgba(0,0,0,0.7); align-items:center; justify-content:center; z-index:100; font-family:Philosopher,serif;">
               <div style="background:#1a1a24; border:2px solid #5a4a2a; border-radius:12px; padding:24px; color:white; min-width:280px; box-shadow:0 12px 48px rgba(0,0,0,0.8);">
                    <h2 id="m-title" style="margin:0 0 8px; color:#e6b34b;"></h2>
                    <p id="m-desc" style="color:#ccc; font-size:14px; margin-bottom:16px;"></p>
                    <div id="m-btns"></div>
                    <button id="m-cls" style="background:transparent; border:none; color:#999; text-decoration:underline; font-size:12px; cursor:pointer; margin-top:10px;">Закрыть</button>
               </div>
            </div>
        </div>`;

        this.canvas = container.querySelector('#mc')!;
        this.ctx = this.canvas.getContext('2d')!;
        this.modal = container.querySelector('#m-ov')!;
        container.querySelector('#m-cls')!.addEventListener('click', () => this.modal.style.display = 'none');

        const engine = new WorldEngine();
        this.world = engine.generate();

        this.camera.scale = 1.0;
        this.centerOn(this.px, this.py);

        this.bindEvents();
        this.setupAmbientAudio();
        this.rafId = requestAnimationFrame(t => this.loop(t));
    }

    private setupAmbientAudio() {
        setInterval(() => {
            const hourStr = (this.game.state as any).timeOfDay || "12:00";
            const hour = parseInt(hourStr.split(':')[0]);
            
            if (hour >= 18 || hour <= 6) { // Evening or night
                if (Math.random() > 0.4) {
                    this.game.audio?.play('wolf_howl', 0.15);
                }
            }
        }, 15000); // Check every 15s
    }

    private bindEvents() {
        this.canvas.addEventListener('wheel', (e) => {
            e.preventDefault();
            const factor = e.deltaY < 0 ? 1.15 : 0.85;

            const worldX = (e.clientX - this.camera.x) / this.camera.scale;
            const worldY = (e.clientY - this.camera.y) / this.camera.scale;

            const nextScale = Math.max(0.4, Math.min(4.0, this.camera.scale * factor));

            this.camera.x = e.clientX - worldX * nextScale;
            this.camera.y = e.clientY - worldY * nextScale;
            this.camera.scale = nextScale;
        }, { passive: false });

        this.canvas.addEventListener('pointerdown', (e) => {
            this.isDragging = true;
            this.dragMoved = false;
            this.lastDragX = e.clientX;
            this.lastDragY = e.clientY;
        });

        window.addEventListener('pointermove', (e) => {
            if (!this.isDragging) return;
            const dx = e.clientX - this.lastDragX;
            const dy = e.clientY - this.lastDragY;
            if (Math.hypot(dx, dy) > 4) this.dragMoved = true;
            this.camera.pan(dx, dy);
            this.lastDragX = e.clientX;
            this.lastDragY = e.clientY;
        });

        window.addEventListener('pointerup', (e) => {
            if (!this.isDragging) return;
            this.isDragging = false;
            if (!this.dragMoved) this.handleWorldClick(e.clientX, e.clientY);
        });

        window.addEventListener('keydown', (e) => this.keys.add(e.code));
        window.addEventListener('keyup', (e) => this.keys.delete(e.code));

        window.addEventListener('resize', () => {
            this.canvas.width = window.innerWidth;
            this.canvas.height = window.innerHeight;
        });
        window.dispatchEvent(new Event('resize'));
    }

    private handleWorldClick(cx: number, cy: number) {
        const worldPos = this.camera.unproject(cx, cy);
        const { gx, gy } = IsoProjection.toGrid(worldPos.wx, worldPos.wy);

        const targetX = Math.floor(gx);
        const targetY = Math.floor(gy);

        const entity = this.world.entities.find(e =>
            targetX >= e.gx && targetX < e.gx + e.gw &&
            targetY >= e.gy && targetY < e.gy + e.gh
        );

        if (entity && entity.actions?.length) {
            this.showInteraction(entity);
            return;
        }

        this.tx = gx;
        this.ty = gy;
        this.isWalking = true;
        this.isFacingRight = gx >= this.px;
    }

    private showInteraction(e: MapEntity) {
        (this.container?.querySelector('#m-title') as HTMLElement).innerText = e.name || 'Объект';
        (this.container?.querySelector('#m-desc') as HTMLElement).innerText = e.desc || '';
        const btns = this.container?.querySelector('#m-btns') as HTMLElement;
        btns.innerHTML = '';

        e.actions?.forEach(act => {
            const btn = document.createElement('button');
            btn.innerText = act.label;
            btn.style.cssText = `display:block; width:100%; padding:10px; margin:8px 0; background:#252530; border:1px solid #4a4a5a; color:white; border-radius:6px; cursor:pointer;`;
            btn.onclick = () => {
                this.modal.style.display = 'none';
                if (act.sceneTarget) this.game.scenes.switchScene(act.sceneTarget);
            };
            btns.appendChild(btn);
        });

        this.modal.style.display = 'flex';
    }

    private loop(ts: number) {
        const dt = Math.min((ts - this.lastTs) / 1000, 0.05);
        this.lastTs = ts;
        this.update(dt);
        this.renderer.draw(this.ctx, this.camera, this.world, { gx: this.px, gy: this.py }, this.walkPhase, this.isFacingRight);
        this.rafId = requestAnimationFrame(t => this.loop(t));
    }

    private update(dt: number) {
        const camSpeed = 15 / this.camera.scale;
        if (this.keys.has('KeyW')) this.camera.y += camSpeed;
        if (this.keys.has('KeyS')) this.camera.y -= camSpeed;
        if (this.keys.has('KeyA')) this.camera.x += camSpeed;
        if (this.keys.has('KeyD')) this.camera.x -= camSpeed;

        this.clampCamera();

        if (!this.isWalking) {
            this.walkPhase = 0;
            return;
        }

        const dx = this.tx - this.px;
        const dy = this.ty - this.py;
        const d = Math.hypot(dx, dy);

        if (d < 0.1) {
            this.isWalking = false;
            return;
        }

        const move = Math.min(5.0 * dt, d);
        this.px += (dx / d) * move;
        this.py += (dy / d) * move;
        this.walkPhase = (this.walkPhase + dt * 2) % 1;
    }

    private centerOn(gx: number, gy: number) {
        const { sx, sy } = IsoProjection.toScreen(gx, gy);
        this.camera.x = window.innerWidth / 2 - sx * this.camera.scale;
        this.camera.y = window.innerHeight / 2 - sy * this.camera.scale;
    }

    private clampCamera() {
        const HW = WORLD_CONFIG.TILE_W / 2;
        const HH = WORLD_CONFIG.TILE_H / 2;
        const scale = this.camera.scale;
        const w = window.innerWidth;
        const h = window.innerHeight;

        const leftBound = -WORLD_CONFIG.GRID_H * HW * scale;
        const rightBound = WORLD_CONFIG.GRID_W * HW * scale;
        const topBound = 0;
        const bottomBound = (WORLD_CONFIG.GRID_W + WORLD_CONFIG.GRID_H) * HH * scale;

        const padX = w * 0.78;
        const padY = h * 0.78;

        this.camera.x = Math.max(w - padX - rightBound, Math.min(this.camera.x, padX - leftBound));
        this.camera.y = Math.max(h - padY - bottomBound, Math.min(this.camera.y, padY - topBound));
    }

    destroy() {
        cancelAnimationFrame(this.rafId);
        super.destroy();
    }
}
