import { BaseScene } from '../scenes/BaseScene';
import { WorldEngine } from './logic/WorldEngine';
import { MapRenderer } from './render/MapRenderer';
import { IsoCamera } from './render/IsoCamera';
import { IsoProjection } from './render/IsoProjection';
import { MainMapAction, MainMapWorldBindings, MapEntity, WorldData } from './types';
import { WORLD_CONFIG } from './config';

export interface MainMapScenePresentation {
    readonly title: string;
    readonly instructions: string;
    readonly closeLabel: string;
    readonly fallbackEntityLabel: string;
}

export interface MainMapAmbientConfig {
    readonly intervalMs: number;
    readonly shouldEmit: () => boolean;
    readonly emit: () => void;
}

/**
 * The legacy map accepts all story-facing data and effects from an explicit
 * dev registration.  It has no authority over residents, dialogue IDs or
 * scene navigation.
 */
export interface MainMapSceneOptions extends MainMapWorldBindings {
    readonly presentation: MainMapScenePresentation;
    readonly onAction: (action: MainMapAction, entity: Readonly<Pick<MapEntity, 'id' | 'subType'>>) => void;
    readonly ambient?: MainMapAmbientConfig;
}

function isNonEmptyText(value: unknown): value is string {
    return typeof value === 'string' && value.trim().length > 0;
}

/** Validate host-owned config before this dev surface acquires any resource. */
function validateSceneOptions(options: MainMapSceneOptions): void {
    const presentation = options.presentation;
    if (!presentation || !isNonEmptyText(presentation.title) || !isNonEmptyText(presentation.instructions)
        || !isNonEmptyText(presentation.closeLabel) || !isNonEmptyText(presentation.fallbackEntityLabel)) {
        throw new Error('MainMap dev config requires complete presentation labels.');
    }
    if (typeof options.onAction !== 'function') {
        throw new Error('MainMap dev config requires an action callback.');
    }
    const ambient = options.ambient;
    if (ambient && (!Number.isFinite(ambient.intervalMs) || ambient.intervalMs <= 0
        || typeof ambient.shouldEmit !== 'function' || typeof ambient.emit !== 'function')) {
        throw new Error('MainMap ambient config requires a positive interval and callbacks.');
    }
}

/**
 * Compatibility shell kept only until MM-54 removes the legacy SceneManager
 * registration.  Calling it without the dev-module config is deliberately
 * rejected rather than silently restoring hardcoded production behaviour.
 */
export class MainMapScene extends BaseScene {
    private canvas: HTMLCanvasElement | null = null;
    private ctx: CanvasRenderingContext2D | null = null;
    private modal: HTMLElement | null = null;
    private actionButtons: HTMLElement | null = null;
    private view: Window | null = null;

    private world: WorldData | null = null;
    private readonly camera = new IsoCamera();
    private readonly renderer = new MapRenderer();

    private px: number = WORLD_CONFIG.CORE_X;
    private py: number = WORLD_CONFIG.CORE_Y;
    private tx: number = WORLD_CONFIG.CORE_X;
    private ty: number = WORLD_CONFIG.CORE_Y;
    private isWalking = false;
    private walkPhase = 0;
    private isFacingRight = true;
    private isDragging = false;
    private dragMoved = false;
    private lastDragX = 0;
    private lastDragY = 0;
    private lastTs = 0;
    private rafId: number | null = null;
    private ambientTimerId: number | null = null;
    private readonly keys = new Set<string>();
    private readonly disposers: Array<() => void> = [];
    private readonly actionDisposers: Array<() => void> = [];
    private mounted = false;

    public constructor(game: any, private readonly options?: MainMapSceneOptions) {
        super(game);
    }

    init(container: HTMLElement): void {
        if (!this.options) {
            throw new Error('MainMapScene requires explicit dev-only module configuration.');
        }
        if (this.mounted) throw new Error('MainMapScene cannot be mounted twice without dispose.');
        validateSceneOptions(this.options);
        const view = container.ownerDocument.defaultView;
        if (!view) throw new Error('MainMapScene requires a window-backed container.');
        // Validate every role/landmark binding before this scene acquires DOM,
        // timers or animation resources.
        const world = new WorldEngine(this.options).generate();

        this.container = container;
        this.view = view;
        this.mounted = true;
        container.innerHTML = `
        <div data-mainmap-root style="position:relative; width:100vw; height:100vh; overflow:hidden; background:#0a1408;">
            <canvas data-mainmap-canvas style="display:block; cursor:crosshair; width:100%; height:100%; touch-action:none;"></canvas>
            <div data-mainmap-hud style="position:absolute; top:12px; left:12px; color:white; font-family:Philosopher,serif; pointer-events:none; text-shadow:1px 1px 4px black;">
                <h3 data-mainmap-title style="margin:0"></h3>
                <p data-mainmap-instructions style="margin:0; opacity:0.75; font-size:12px;"></p>
            </div>
            <div data-mainmap-modal style="display:none; position:absolute; inset:0; background:rgba(0,0,0,0.7); align-items:center; justify-content:center; z-index:100; font-family:Philosopher,serif;">
               <div style="background:#1a1a24; border:2px solid #5a4a2a; border-radius:12px; padding:24px; color:white; min-width:280px; box-shadow:0 12px 48px rgba(0,0,0,0.8);">
                    <h2 data-mainmap-modal-title style="margin:0 0 8px; color:#e6b34b;"></h2>
                    <p data-mainmap-modal-description style="color:#ccc; font-size:14px; margin-bottom:16px;"></p>
                    <div data-mainmap-actions></div>
                    <button data-mainmap-close style="background:transparent; border:none; color:#999; text-decoration:underline; font-size:12px; cursor:pointer; margin-top:10px;"></button>
               </div>
            </div>
        </div>`;

        const canvas = container.querySelector<HTMLCanvasElement>('[data-mainmap-canvas]');
        const modal = container.querySelector<HTMLElement>('[data-mainmap-modal]');
        const buttons = container.querySelector<HTMLElement>('[data-mainmap-actions]');
        const title = container.querySelector<HTMLElement>('[data-mainmap-title]');
        const instructions = container.querySelector<HTMLElement>('[data-mainmap-instructions]');
        const close = container.querySelector<HTMLButtonElement>('[data-mainmap-close]');
        if (!canvas || !modal || !buttons || !title || !instructions || !close) {
            this.destroy();
            throw new Error('MainMapScene failed to create its dev-only DOM surface.');
        }
        const ctx = canvas.getContext('2d');
        if (!ctx) {
            this.destroy();
            throw new Error('MainMapScene requires a 2D canvas context.');
        }

        title.textContent = this.options.presentation.title;
        instructions.textContent = this.options.presentation.instructions;
        close.textContent = this.options.presentation.closeLabel;
        this.canvas = canvas;
        this.ctx = ctx;
        this.modal = modal;
        this.actionButtons = buttons;
        this.listen(close, 'click', () => { if (this.modal) this.modal.style.display = 'none'; });

        this.world = world;
        this.camera.scale = 1.0;
        this.centerOn(this.px, this.py);
        this.bindEvents();
        this.setupAmbientCue();
        this.rafId = view.requestAnimationFrame((timestamp) => this.loop(timestamp));
    }

    private setupAmbientCue(): void {
        const ambient = this.options?.ambient;
        if (!ambient || !this.view) return;
        this.ambientTimerId = this.view.setInterval(() => {
            if (ambient.shouldEmit()) ambient.emit();
        }, ambient.intervalMs);
    }

    private listen(target: EventTarget, type: string, listener: EventListenerOrEventListenerObject, options?: AddEventListenerOptions | boolean): void {
        target.addEventListener(type, listener, options);
        this.disposers.push(() => target.removeEventListener(type, listener, options));
    }

    private bindEvents(): void {
        const canvas = this.canvas;
        const view = this.view;
        if (!canvas || !view) throw new Error('MainMapScene is not mounted.');

        this.listen(canvas, 'wheel', (event) => {
            const e = event as WheelEvent;
            e.preventDefault();
            const factor = e.deltaY < 0 ? 1.15 : 0.85;
            const worldX = (e.clientX - this.camera.x) / this.camera.scale;
            const worldY = (e.clientY - this.camera.y) / this.camera.scale;
            const nextScale = Math.max(0.4, Math.min(4.0, this.camera.scale * factor));
            this.camera.x = e.clientX - worldX * nextScale;
            this.camera.y = e.clientY - worldY * nextScale;
            this.camera.scale = nextScale;
        }, { passive: false });
        this.listen(canvas, 'pointerdown', (event) => {
            const e = event as PointerEvent;
            this.isDragging = true;
            this.dragMoved = false;
            this.lastDragX = e.clientX;
            this.lastDragY = e.clientY;
        });
        this.listen(view, 'pointermove', (event) => {
            if (!this.isDragging) return;
            const e = event as PointerEvent;
            const dx = e.clientX - this.lastDragX;
            const dy = e.clientY - this.lastDragY;
            if (Math.hypot(dx, dy) > 4) this.dragMoved = true;
            this.camera.pan(dx, dy);
            this.lastDragX = e.clientX;
            this.lastDragY = e.clientY;
        });
        this.listen(view, 'pointerup', (event) => {
            if (!this.isDragging) return;
            const e = event as PointerEvent;
            this.isDragging = false;
            if (!this.dragMoved) this.handleWorldClick(e.clientX, e.clientY);
        });
        this.listen(view, 'keydown', (event) => this.keys.add((event as KeyboardEvent).code));
        this.listen(view, 'keyup', (event) => this.keys.delete((event as KeyboardEvent).code));
        this.listen(view, 'resize', () => this.resize());
        this.resize();
    }

    private resize(): void {
        if (!this.canvas || !this.view) return;
        this.canvas.width = this.view.innerWidth;
        this.canvas.height = this.view.innerHeight;
    }

    private handleWorldClick(cx: number, cy: number): void {
        if (!this.world) return;
        const worldPos = this.camera.unproject(cx, cy);
        const { gx, gy } = IsoProjection.toGrid(worldPos.wx, worldPos.wy);
        const targetX = Math.floor(gx);
        const targetY = Math.floor(gy);
        const entity = this.world.entities.find((candidate) =>
            targetX >= candidate.gx && targetX < candidate.gx + candidate.gw
            && targetY >= candidate.gy && targetY < candidate.gy + candidate.gh,
        );
        if (entity?.actions?.length) {
            this.showInteraction(entity);
            return;
        }
        this.tx = gx;
        this.ty = gy;
        this.isWalking = true;
        this.isFacingRight = gx >= this.px;
    }

    private showInteraction(entity: MapEntity): void {
        const container = this.container;
        const modal = this.modal;
        const buttons = this.actionButtons;
        if (!container || !modal || !buttons || !this.options) return;
        const title = container.querySelector<HTMLElement>('[data-mainmap-modal-title]');
        const description = container.querySelector<HTMLElement>('[data-mainmap-modal-description]');
        if (!title || !description) return;
        title.textContent = entity.name || this.options.presentation.fallbackEntityLabel;
        description.textContent = entity.desc || '';
        for (const dispose of this.actionDisposers.splice(0)) dispose();
        buttons.textContent = '';
        for (const action of entity.actions ?? []) {
            const button = container.ownerDocument.createElement('button');
            button.textContent = action.label;
            button.style.cssText = 'display:block; width:100%; padding:10px; margin:8px 0; background:#252530; border:1px solid #4a4a5a; color:white; border-radius:6px; cursor:pointer;';
            const onClick = () => {
                modal.style.display = 'none';
                this.options?.onAction(action, { id: entity.id, subType: entity.subType });
            };
            button.addEventListener('click', onClick);
            this.actionDisposers.push(() => button.removeEventListener('click', onClick));
            buttons.appendChild(button);
        }
        modal.style.display = 'flex';
    }

    private loop(timestamp: number): void {
        if (!this.mounted || !this.ctx || !this.world || !this.view) return;
        const delta = Math.min((timestamp - this.lastTs) / 1000, 0.05);
        this.lastTs = timestamp;
        this.update(delta);
        this.renderer.draw(this.ctx, this.camera, this.world, { gx: this.px, gy: this.py }, this.walkPhase, this.isFacingRight);
        this.rafId = this.view.requestAnimationFrame((nextTimestamp) => this.loop(nextTimestamp));
    }

    private update(delta: number): void {
        const cameraSpeed = 15 / this.camera.scale;
        if (this.keys.has('KeyW')) this.camera.y += cameraSpeed;
        if (this.keys.has('KeyS')) this.camera.y -= cameraSpeed;
        if (this.keys.has('KeyA')) this.camera.x += cameraSpeed;
        if (this.keys.has('KeyD')) this.camera.x -= cameraSpeed;
        this.clampCamera();
        if (!this.isWalking) {
            this.walkPhase = 0;
            return;
        }
        const dx = this.tx - this.px;
        const dy = this.ty - this.py;
        const distance = Math.hypot(dx, dy);
        if (distance < 0.1) {
            this.isWalking = false;
            return;
        }
        const move = Math.min(5.0 * delta, distance);
        this.px += (dx / distance) * move;
        this.py += (dy / distance) * move;
        this.walkPhase = (this.walkPhase + delta * 2) % 1;
    }

    private centerOn(gx: number, gy: number): void {
        if (!this.view) return;
        const { sx, sy } = IsoProjection.toScreen(gx, gy);
        this.camera.x = this.view.innerWidth / 2 - sx * this.camera.scale;
        this.camera.y = this.view.innerHeight / 2 - sy * this.camera.scale;
    }

    private clampCamera(): void {
        if (!this.view) return;
        const halfTileWidth = WORLD_CONFIG.TILE_W / 2;
        const halfTileHeight = WORLD_CONFIG.TILE_H / 2;
        const scale = this.camera.scale;
        const leftBound = -WORLD_CONFIG.GRID_H * halfTileWidth * scale;
        const rightBound = WORLD_CONFIG.GRID_W * halfTileWidth * scale;
        const bottomBound = (WORLD_CONFIG.GRID_W + WORLD_CONFIG.GRID_H) * halfTileHeight * scale;
        const padX = this.view.innerWidth * 0.78;
        const padY = this.view.innerHeight * 0.78;
        this.camera.x = Math.max(this.view.innerWidth - padX - rightBound, Math.min(this.camera.x, padX - leftBound));
        this.camera.y = Math.max(this.view.innerHeight - padY - bottomBound, Math.min(this.camera.y, padY));
    }

    destroy(): void {
        if (!this.mounted) return;
        this.mounted = false;
        if (this.view && this.rafId !== null) this.view.cancelAnimationFrame(this.rafId);
        if (this.view && this.ambientTimerId !== null) this.view.clearInterval(this.ambientTimerId);
        this.rafId = null;
        this.ambientTimerId = null;
        for (const dispose of this.disposers.splice(0)) dispose();
        for (const dispose of this.actionDisposers.splice(0)) dispose();
        this.keys.clear();
        if (this.canvas) {
            // A 2D canvas owns no Three.js objects here; zeroing its backing
            // store releases the remaining renderer allocation before the DOM
            // surface is detached by BaseScene.
            this.canvas.width = 0;
            this.canvas.height = 0;
        }
        this.canvas = null;
        this.ctx = null;
        this.modal = null;
        this.actionButtons = null;
        this.world = null;
        this.view = null;
        super.destroy();
    }
}
