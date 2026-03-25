import { BaseScene } from '../scenes/BaseScene';
import { AssetLoader } from '../utils/AssetLoader';
import { generateVillageWorld } from './procgen/index';
import { MapEntity, MapCell, TileType, ForestDot } from './procgen/types';
import { GRID_W, GRID_H, VILLAGE_CORE_X, VILLAGE_CORE_Y } from './procgen/config';
import { sortIsometricEntities } from './procgen/isoSort';

const TW = 96;
const TH = 48;

type ScreenPoint = readonly [number, number];

type CameraState = {
    x: number;
    y: number;
    scale: number;
};

type ViewBounds = {
    minGX: number;
    maxGX: number;
    minGY: number;
    maxGY: number;
};

type LayerCanvases = {
    ground: HTMLCanvasElement | OffscreenCanvas;
    statics: HTMLCanvasElement | OffscreenCanvas;
};

type CachedPatterns = Partial<Record<'grass' | 'road' | 'water', CanvasPattern>>;

const TEXTURE_ASSETS = [
    { id: 'grass', url: '/assets/grass_tex.png' },
    { id: 'road', url: '/assets/road_tex.jpg' },
    { id: 'water', url: '/assets/water_tex.png' },
    { id: 'wall_log', url: '/assets/wood_tex.png' },
    { id: 'roof_metal', url: '/assets/roof_metal.jpg' },
    { id: 'hero_idle', url: '/assets/hero_idle.png' },
    { id: 'hero_walk', url: '/assets/hero_walk.png' },
    { id: 'tatar_house', url: '/assets/tatar_house_sprite.jpg' },
    { id: 'zirat', url: '/assets/zirat_sprite.png' },
    { id: 'light_grass', url: '/assets/light_grass_tex.png' }
] as const;

function toScreen(gx: number, gy: number): ScreenPoint {
    return [(gx - gy) * (TW / 2), (gx + gy) * (TH / 2)];
}

function toGrid(sx: number, sy: number): ScreenPoint {
    return [
        (sx / (TW / 2) + sy / (TH / 2)) / 2,
        (sy / (TH / 2) - sx / (TW / 2)) / 2
    ];
}

function createCanvas(width: number, height: number): HTMLCanvasElement | OffscreenCanvas {
    if (typeof OffscreenCanvas !== 'undefined') {
        return new OffscreenCanvas(width, height);
    }
    const c = document.createElement('canvas');
    c.width = width;
    c.height = height;
    return c;
}

function get2DContext(canvas: HTMLCanvasElement | OffscreenCanvas): CanvasRenderingContext2D {
    const ctx = canvas.getContext('2d', { alpha: false }) as CanvasRenderingContext2D | null;
    if (!ctx) throw new Error('2D context not available');
    return ctx;
}

function roundScreen([x, y]: ScreenPoint): ScreenPoint {
    return [Math.round(x), Math.round(y)];
}

function entityDepth(e: MapEntity): number {
    return e.gy + (e.gh ?? 1) + (e.gx * 0.0001);
}

export class MainMapScene extends BaseScene {
    private canvas!: HTMLCanvasElement;
    private ctx!: CanvasRenderingContext2D;
    private modalEl!: HTMLElement;

    private map: MapCell[][] = [];
    private entities: MapEntity[] = [];
    private forestDots: ForestDot[] = [];

    private camera: CameraState = { x: 0, y: 0, scale: 1 };
    private px = VILLAGE_CORE_X;
    private py = VILLAGE_CORE_Y;
    private tx = VILLAGE_CORE_X;
    private ty = VILLAGE_CORE_Y;
    private walking = false;
    private legPhase = 0;

    private dragging = false;
    private didDrag = false;
    private dragSX = 0;
    private dragSY = 0;
    private camStartX = 0;
    private camStartY = 0;

    private rafId = 0;
    private lastTs = 0;

    private worldMinSX = 0;
    private worldMinSY = 0;
    private worldMaxSX = 0;
    private worldMaxSY = 0;

    private layers!: LayerCanvases;
    private patterns: CachedPatterns = {};
    private staticDirty = true;
    private groundDirty = true;

    private staticEntities: MapEntity[] = [];
    private dynamicEntities: MapEntity[] = [];

    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
        <style>
            .vr { position:relative; width:100vw; height:100vh; overflow:hidden; background:#1f2f18; }
            .vc { display:block; cursor:crosshair; width:100%; height:100%; }
            .v-hud { position:absolute; top:14px; left:14px; color:#fff; font-family:'Philosopher',sans-serif;
                     pointer-events:none; text-shadow:1px 1px 3px rgba(0,0,0,.8); }
            .v-hud h3 { margin:0 0 3px; font-size:18px; }
            .v-hud p  { margin:0; font-size:13px; opacity:.82; }
            .v-ov { display:none; position:absolute; inset:0; background:rgba(0,0,0,.65);
                    align-items:center; justify-content:center; z-index:100; font-family:'Philosopher',sans-serif; }
            .v-ov.open { display:flex; }
            .v-mod { background:#1a1a24; border:2px solid #5a4a2a; border-radius:12px;
                     padding:26px 30px; min-width:300px; color:white;
                     box-shadow:0 12px 48px rgba(0,0,0,.8); }
            .v-mod h2 { margin:0 0 6px; color:#e6b34b; font-size:22px; }
            .v-mod p  { color:#ccc; font-size:14px; margin:0 0 16px; min-height:40px; }
            .v-btn { display:block; width:100%; padding:12px 14px; margin:8px 0;
                     background:#252530; color:#eee; border:1px solid #4a4a5a;
                     border-radius:7px; font-size:15px; cursor:pointer; font-family:'Philosopher',sans-serif; }
            .v-cls { background:transparent; border:none; color:#999; font-size:13px;
                     cursor:pointer; text-decoration:underline; margin-top:10px; font-family:'Philosopher',sans-serif;}
        </style>
        <div class="vr">
            <canvas class="vc" id="vc"></canvas>
            <div class="v-hud">
                <h3>Кара-Урман</h3>
                <p>Скролл — зум · Тянуть — камера · Клик — идти / взаимодействовать</p>
            </div>
            <div class="v-ov" id="vov">
                <div class="v-mod">
                    <h2 id="vt"></h2>
                    <p id="vd"></p>
                    <div id="vb"></div>
                    <button class="v-cls" id="vcl">Закрыть</button>
                </div>
            </div>
        </div>`;

        this.canvas = container.querySelector('#vc')!;
        this.ctx = this.canvas.getContext('2d', { alpha: false })!;
        this.modalEl = container.querySelector('#vov')!;

        container.querySelector('#vcl')!.addEventListener('click', () => {
            this.modalEl.classList.remove('open');
            (this.container!.querySelector('#vb') as HTMLElement).innerHTML = '';
        });

        this.generateMap();

        AssetLoader.loadAll(TEXTURE_ASSETS)
            .then(() => {
                this.buildPatternCache();
                this.groundDirty = true;
                this.staticDirty = true;
            })
            .catch(() => {
                this.buildPatternCache();
            });

        this.recreateLayerCanvases();
        this.bindResize();
        this.initInput();
        this.centerOnPlayer();

        this.rafId = requestAnimationFrame((ts) => this.loop(ts));
    }

    private bindResize() {
        const resize = () => {
            this.canvas.width = window.innerWidth;
            this.canvas.height = window.innerHeight;
            this.recreateLayerCanvases();
            this.centerOnPlayer();
            this.groundDirty = true;
            this.staticDirty = true;
        };
        window.addEventListener('resize', resize);
        resize();
    }

    private recreateLayerCanvases() {
        this.layers = {
            ground: createCanvas(this.canvas.width, this.canvas.height),
            statics: createCanvas(this.canvas.width, this.canvas.height)
        };
    }

    private buildPatternCache() {
        const grass = AssetLoader.get('grass');
        const road = AssetLoader.get('road');
        const water = AssetLoader.get('water');

        if (grass) this.patterns.grass = this.ctx.createPattern(grass, 'repeat') ?? undefined;
        if (road) this.patterns.road = this.ctx.createPattern(road, 'repeat') ?? undefined;
        if (water) this.patterns.water = this.ctx.createPattern(water, 'repeat') ?? undefined;
    }

    private generateMap() {
        const worldData = generateVillageWorld();
        this.map = worldData.map;
        this.entities = worldData.entities;
        this.forestDots = worldData.forestDots ?? [];

        this.partitionEntities();

        const corners = [
            toScreen(0, 0),
            toScreen(GRID_W, 0),
            toScreen(0, GRID_H),
            toScreen(GRID_W, GRID_H)
        ];

        this.worldMinSX = Math.min(...corners.map((c) => c[0])) - 400;
        this.worldMinSY = Math.min(...corners.map((c) => c[1])) - 400;
        this.worldMaxSX = Math.max(...corners.map((c) => c[0])) + 400;
        this.worldMaxSY = Math.max(...corners.map((c) => c[1])) + 400;

        this.bindEntityActions();
    }

    private partitionEntities() {
        this.staticEntities = [];
        this.dynamicEntities = [];

        for (const e of this.entities) {
            if (e.type === 'animal') this.dynamicEntities.push(e);
            else this.staticEntities.push(e);
        }
    }

    private bindEntityActions() {
        const bHouseBaba = this.entities.find((b) => b.id === 'house-baba');
        if (bHouseBaba?.actions?.[0]) {
            bHouseBaba.actions[0].fn = () => this.game.scenes.switchScene('computer');
        }

        const bMosque = this.entities.find((b) => b.id === 'mosque');
        if (bMosque?.actions?.[0]) {
            bMosque.actions[0].fn = () => this.game.scenes.switchScene('mosque');
        }
    }

    private centerOnPlayer() {
        const [sx, sy] = toScreen(this.px, this.py);
        this.camera.x = this.canvas.width / 2 - sx * this.camera.scale;
        this.camera.y = this.canvas.height / 2 - sy * this.camera.scale;
        this.clampCamera();
    }

    private clampCamera() {
        const cw = this.canvas.width;
        const ch = this.canvas.height;

        const maxCamX = -this.worldMinSX * this.camera.scale + 120;
        const minCamX = cw - this.worldMaxSX * this.camera.scale - 120;
        const maxCamY = -this.worldMinSY * this.camera.scale + 120;
        const minCamY = ch - this.worldMaxSY * this.camera.scale - 120;

        this.camera.x = Math.max(Math.min(minCamX, maxCamX), Math.min(this.camera.x, Math.max(minCamX, maxCamX)));
        this.camera.y = Math.max(Math.min(minCamY, maxCamY), Math.min(this.camera.y, Math.max(minCamY, maxCamY)));
    }

    private initInput() {
        this.canvas.addEventListener('wheel', (e) => {
            e.preventDefault();
            const factor = e.deltaY < 0 ? 1.12 : 0.89;
            const nextScale = Math.max(0.45, Math.min(2.2, this.camera.scale * factor));

            const mx = e.clientX;
            const my = e.clientY;

            this.camera.x = mx - (mx - this.camera.x) * (nextScale / this.camera.scale);
            this.camera.y = my - (my - this.camera.y) * (nextScale / this.camera.scale);
            this.camera.scale = nextScale;

            this.clampCamera();
            this.groundDirty = true;
            this.staticDirty = true;
        }, { passive: false });

        this.canvas.addEventListener('mousedown', (e) => {
            this.dragging = true;
            this.didDrag = false;
            this.dragSX = e.clientX;
            this.dragSY = e.clientY;
            this.camStartX = this.camera.x;
            this.camStartY = this.camera.y;
        });

        window.addEventListener('mousemove', (e) => {
            if (!this.dragging) return;
            const dx = e.clientX - this.dragSX;
            const dy = e.clientY - this.dragSY;
            if (Math.abs(dx) + Math.abs(dy) > 5) this.didDrag = true;

            this.camera.x = this.camStartX + dx;
            this.camera.y = this.camStartY + dy;
            this.clampCamera();
            this.groundDirty = true;
            this.staticDirty = true;
        });

        window.addEventListener('mouseup', (e) => {
            if (!this.dragging) return;
            this.dragging = false;
            if (!this.didDrag) this.onClick(e.clientX, e.clientY);
        });
    }

    private getViewBounds(): ViewBounds {
        const topLeft = toGrid(
            (-this.camera.x) / this.camera.scale - TW,
            (-this.camera.y) / this.camera.scale - TH
        );
        const bottomRight = toGrid(
            (this.canvas.width - this.camera.x) / this.camera.scale + TW,
            (this.canvas.height - this.camera.y) / this.camera.scale + TH
        );

        return {
            minGX: Math.max(0, Math.floor(Math.min(topLeft[0], bottomRight[0])) - 2),
            maxGX: Math.min(GRID_W - 1, Math.ceil(Math.max(topLeft[0], bottomRight[0])) + 2),
            minGY: Math.max(0, Math.floor(Math.min(topLeft[1], bottomRight[1])) - 2),
            maxGY: Math.min(GRID_H - 1, Math.ceil(Math.max(topLeft[1], bottomRight[1])) + 2)
        };
    }

    private onClick(cx: number, cy: number) {
        if (this.modalEl.classList.contains('open')) return;

        const sx = (cx - this.camera.x) / this.camera.scale;
        const sy = (cy - this.camera.y) / this.camera.scale;
        const [gxRaw, gyRaw] = toGrid(sx, sy);
        const gx = Math.floor(gxRaw);
        const gy = Math.floor(gyRaw);

        if (gx < 0 || gx >= GRID_W || gy < 0 || gy >= GRID_H) return;

        for (const e of [...this.dynamicEntities, ...this.staticEntities]) {
            if (!('gx' in e) || !('gy' in e)) continue;
            if (gx >= e.gx && gx < e.gx + e.gw && gy >= e.gy && gy < e.gy + e.gh) {
                if (e.actions?.length) {
                    this.openModal(e);
                    return;
                }
            }
        }

        const tile = this.map[gx]?.[gy];
        if (tile?.type === TileType.WATER) {
            this.onOSMessage?.('Су', 'Слишком глубоко, туда не пройти.');
            return;
        }

        this.tx = gx + 0.5;
        this.ty = gy + 0.5;
        this.walking = true;
    }

    private openModal(e: MapEntity) {
        if (!e.actions?.length) return;

        (this.container!.querySelector('#vt') as HTMLElement).textContent = e.name || 'Место';
        (this.container!.querySelector('#vd') as HTMLElement).textContent = e.desc || '';

        const btns = this.container!.querySelector('#vb') as HTMLElement;
        btns.innerHTML = '';

        for (const a of e.actions) {
            const btn = document.createElement('button');
            btn.className = 'v-btn';
            btn.textContent = a.label;
            btn.addEventListener('click', () => {
                this.modalEl.classList.remove('open');
                a.fn(this.game, this);
            });
            btns.appendChild(btn);
        }

        this.modalEl.classList.add('open');
    }

    private loop(ts: number) {
        const dt = Math.min((ts - this.lastTs) / 1000, 0.05);
        this.lastTs = ts;

        this.update(dt);
        this.draw();

        this.rafId = requestAnimationFrame((t) => this.loop(t));
    }

    private update(dt: number) {
        if (!this.walking) return;

        const dx = this.tx - this.px;
        const dy = this.ty - this.py;
        const d = Math.hypot(dx, dy);

        if (d < 0.08) {
            this.walking = false;
            this.legPhase = 0;
            return;
        }

        const spd = 4.8;
        const step = Math.min(spd * dt, d);

        this.px += (dx / d) * step;
        this.py += (dy / d) * step;
        this.legPhase += dt * 12;
    }

    private draw() {
        if (this.groundDirty) this.redrawGroundLayer();
        if (this.staticDirty) this.redrawStaticLayer();

        const ctx = this.ctx;
        ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        ctx.drawImage(this.layers.ground as CanvasImageSource, 0, 0);
        ctx.drawImage(this.layers.statics as CanvasImageSource, 0, 0);

        ctx.save();
        ctx.translate(this.camera.x, this.camera.y);
        ctx.scale(this.camera.scale, this.camera.scale);

        this.drawDynamicLayer();
        this.drawDestinationPing();

        ctx.restore();

        this.drawEdgeFade();
    }

    private redrawGroundLayer() {
        const ctx = get2DContext(this.layers.ground);
        ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        ctx.save();
        ctx.translate(this.camera.x, this.camera.y);
        ctx.scale(this.camera.scale, this.camera.scale);

        this.drawBackgroundFill(ctx);
        this.drawTiles(ctx);
        this.drawForest(ctx);

        ctx.restore();
        this.groundDirty = false;
    }

    private redrawStaticLayer() {
        const ctx = get2DContext(this.layers.statics);
        ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        ctx.save();
        ctx.translate(this.camera.x, this.camera.y);
        ctx.scale(this.camera.scale, this.camera.scale);

        const visible = this.staticEntities
            .filter((e) => this.isEntityVisible(e))
            .sort((a, b) => entityDepth(a) - entityDepth(b));

        for (const e of visible) this.drawEntity(ctx, e);

        ctx.restore();
        this.staticDirty = false;
    }

    private drawBackgroundFill(ctx: CanvasRenderingContext2D) {
        const pattern = this.patterns.grass;
        if (pattern) {
            ctx.fillStyle = pattern;
        } else {
            ctx.fillStyle = '#3f6d2f';
        }

        ctx.fillRect(
            this.worldMinSX - 500,
            this.worldMinSY - 500,
            this.worldMaxSX - this.worldMinSX + 1000,
            this.worldMaxSY - this.worldMinSY + 1000
        );
    }

    private drawTiles(ctx: CanvasRenderingContext2D) {
        const bounds = this.getViewBounds();

        for (let y = bounds.minGY; y <= bounds.maxGY; y++) {
            for (let x = bounds.minGX; x <= bounds.maxGX; x++) {
                const cell = this.map[x]?.[y];
                if (!cell) continue;

                if (cell.type === TileType.ROAD || cell.type === TileType.DIRT) {
                    this.drawIsoTile(ctx, x, y, this.patterns.road, cell.type === TileType.DIRT ? 'rgba(90,70,35,0.28)' : undefined);
                } else if (cell.type === TileType.WATER) {
                    this.drawIsoTile(ctx, x, y, this.patterns.water, 'rgba(0,90,160,0.18)');
                }
            }
        }
    }

    private drawIsoTile(
        ctx: CanvasRenderingContext2D,
        gx: number,
        gy: number,
        pattern?: CanvasPattern,
        overlay?: string
    ) {
        const p1 = roundScreen(toScreen(gx, gy));
        const p2 = roundScreen(toScreen(gx + 1, gy));
        const p3 = roundScreen(toScreen(gx + 1, gy + 1));
        const p4 = roundScreen(toScreen(gx, gy + 1));

        ctx.beginPath();
        ctx.moveTo(p1[0], p1[1]);
        ctx.lineTo(p2[0], p2[1]);
        ctx.lineTo(p3[0], p3[1]);
        ctx.lineTo(p4[0], p4[1]);
        ctx.closePath();

        if (pattern) {
            ctx.save();
            ctx.clip();
            ctx.fillStyle = pattern;
            const minX = Math.min(p1[0], p2[0], p3[0], p4[0]);
            const minY = Math.min(p1[1], p2[1], p3[1], p4[1]);
            const maxX = Math.max(p1[0], p2[0], p3[0], p4[0]);
            const maxY = Math.max(p1[1], p2[1], p3[1], p4[1]);
            ctx.fillRect(minX, minY, maxX - minX, maxY - minY);
            if (overlay) {
                ctx.fillStyle = overlay;
                ctx.fillRect(minX, minY, maxX - minX, maxY - minY);
            }
            ctx.restore();
            return;
        }

        if (overlay) {
            ctx.fillStyle = overlay;
            ctx.fill();
        }
    }

    private drawDynamicLayer() {
        const drawables: { depth: number; fn: () => void }[] = [];

        for (const e of this.dynamicEntities) {
            if (!this.isEntityVisible(e)) continue;
            drawables.push({
                depth: entityDepth(e),
                fn: () => this.drawEntity(this.ctx, e)
            });
        }

        drawables.push({
            depth: this.px + this.py + 0.5,
            fn: () => this.drawPlayer(this.ctx)
        });

        drawables.sort((a, b) => a.depth - b.depth);

        for (const d of drawables) d.fn();
    }

    private isEntityVisible(e: MapEntity): boolean {
        const [sx, sy] = toScreen(e.gx, e.gy);
        const maxW = (e.gw + e.gh) * TW * 0.5 + 120;
        const maxH = (e.gw + e.gh) * TH * 0.5 + 160;

        const screenX = sx * this.camera.scale + this.camera.x;
        const screenY = sy * this.camera.scale + this.camera.y;

        return !(
            screenX + maxW < -200 ||
            screenX - maxW > this.canvas.width + 200 ||
            screenY + maxH < -200 ||
            screenY - maxH > this.canvas.height + 200
        );
    }

    private drawEntity(ctx: CanvasRenderingContext2D, e: MapEntity) {
        const [sx, sy] = roundScreen(toScreen(e.gx, e.gy));

        if (e.type === 'plot_base') {
            this.drawIsoArea(ctx, e.gx, e.gy, e.gw, e.gh, 'rgba(50,35,20,0.22)');
            return;
        }

        if (e.type === 'house' || e.type === 'mosque' || e.type === 'building' || e.type === 'banya') {
            const h = e.type === 'mosque' ? 72 : e.type === 'house' ? 48 : e.style?.h || 24;
            const wallColor = e.style?.wallColor || '#8c6a43';
            const roofColor = e.style?.roofColor || '#466b43';

            this.drawIsoBuilding(ctx, sx, sy, e.gw, e.gh, h, wallColor, roofColor);

            if (e.name) {
                ctx.fillStyle = 'rgba(0,0,0,0.55)';
                ctx.font = '10px Philosopher';
                ctx.textAlign = 'center';
                ctx.fillText(e.name, sx, sy - h - 8);
                ctx.fillStyle = '#fff';
                ctx.fillText(e.name, sx, sy - h - 9);
            }
            return;
        }

        if (e.type === 'cemetery') {
            this.drawIsoArea(ctx, e.gx, e.gy, e.gw, e.gh, 'rgba(70,95,70,0.28)');
            const zirat = AssetLoader.get('zirat');
            if (zirat) {
                const [cx, cy] = roundScreen(toScreen(e.gx + e.gw / 2, e.gy + e.gh / 2));
                ctx.drawImage(zirat, cx - 60, cy - 80, 120, 100);
            }
            return;
        }

        if (e.type === 'animal') {
            ctx.font = '18px Arial';
            ctx.fillText(e.name || '🐑', sx - 9, sy + 6);
            return;
        }

        if (e.type === 'prop') {
            ctx.fillStyle = 'rgba(0,0,0,0.55)';
            ctx.font = '12px Arial';
            ctx.fillText(e.name || '?', sx - 8, sy + 5);
        }
    }

    private drawIsoArea(ctx: CanvasRenderingContext2D, gx: number, gy: number, gw: number, gh: number, fill: string) {
        const p1 = roundScreen(toScreen(gx, gy));
        const p2 = roundScreen(toScreen(gx + gw, gy));
        const p3 = roundScreen(toScreen(gx + gw, gy + gh));
        const p4 = roundScreen(toScreen(gx, gy + gh));

        ctx.beginPath();
        ctx.moveTo(p1[0], p1[1]);
        ctx.lineTo(p2[0], p2[1]);
        ctx.lineTo(p3[0], p3[1]);
        ctx.lineTo(p4[0], p4[1]);
        ctx.closePath();
        ctx.fillStyle = fill;
        ctx.fill();
    }

    private drawIsoBuilding(
        ctx: CanvasRenderingContext2D,
        sx: number,
        sy: number,
        gw: number,
        gh: number,
        h: number,
        wallColor: string,
        roofColor: string
    ) {
        const left = roundScreen(toScreen(0, gh))[0] - roundScreen(toScreen(0, 0))[0];
        const right = roundScreen(toScreen(gw, 0))[0] - roundScreen(toScreen(0, 0))[0];
        const downL = roundScreen(toScreen(0, gh))[1] - roundScreen(toScreen(0, 0))[1];
        const downR = roundScreen(toScreen(gw, 0))[1] - roundScreen(toScreen(0, 0))[1];

        ctx.fillStyle = wallColor;

        ctx.beginPath();
        ctx.moveTo(sx, sy - h);
        ctx.lineTo(sx - left, sy + downL - h);
        ctx.lineTo(sx - left, sy + downL);
        ctx.lineTo(sx, sy);
        ctx.closePath();
        ctx.fill();

        ctx.fillStyle = 'rgba(0,0,0,0.18)';
        ctx.beginPath();
        ctx.moveTo(sx, sy - h);
        ctx.lineTo(sx + right, sy + downR - h);
        ctx.lineTo(sx + right, sy + downR);
        ctx.lineTo(sx, sy);
        ctx.closePath();
        ctx.fill();

        const roofPeakY = sy - h - 18;

        ctx.fillStyle = roofColor;
        ctx.beginPath();
        ctx.moveTo(sx - left, sy + downL - h);
        ctx.lineTo(sx, sy - h - 6);
        ctx.lineTo(sx + right, sy + downR - h);
        ctx.lineTo(sx, roofPeakY);
        ctx.closePath();
        ctx.fill();
    }

    private drawPlayer(ctx: CanvasRenderingContext2D) {
        const [sx, sy] = roundScreen(toScreen(this.px, this.py));
        const leg = this.walking ? Math.sin(this.legPhase) * 4 : 0;

        ctx.fillStyle = 'rgba(0,0,0,0.25)';
        ctx.beginPath();
        ctx.ellipse(sx, sy + 2, 11, 4, 0, 0, Math.PI * 2);
        ctx.fill();

        const heroTex = AssetLoader.get(this.walking ? 'hero_walk' : 'hero_idle');
        if (heroTex) {
            ctx.drawImage(heroTex, sx - 20, sy - 50, 40, 50);
            return;
        }

        ctx.fillStyle = '#1b1f46';
        ctx.fillRect(sx - 6, sy - 12, 5, 14 + leg);
        ctx.fillRect(sx + 1, sy - 12, 5, 14 - leg);

        ctx.fillStyle = '#2a1a0a';
        ctx.fillRect(sx - 7, sy + 1 + leg, 6, 3);
        ctx.fillRect(sx + 1, sy + 1 - leg, 6, 3);

        ctx.fillStyle = '#c03030';
        const bodyTop = sy - 30;
        ctx.fillRect(sx - 7, bodyTop, 14, 20);

        ctx.fillStyle = '#f5cba7';
        ctx.beginPath();
        ctx.arc(sx, bodyTop - 8, 9, 0, Math.PI * 2);
        ctx.fill();
    }

    private drawForest(ctx: CanvasRenderingContext2D) {
        for (const f of this.forestDots) {
            const [sx, sy] = roundScreen(toScreen(f.x, f.y));
            const r = f.r;
            ctx.fillStyle = '#173116';
            ctx.beginPath();
            ctx.arc(sx, sy - r * 2.2, r * 1.5, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    private drawDestinationPing() {
        if (!this.walking) return;
        const [sx, sy] = roundScreen(toScreen(this.tx, this.ty));
        const a = 0.45 + 0.35 * Math.sin(Date.now() / 180);
        this.ctx.strokeStyle = `rgba(255,255,255,${a})`;
        this.ctx.lineWidth = 2 / this.camera.scale;
        this.ctx.beginPath();
        this.ctx.arc(sx, sy - 4, 8, 0, Math.PI * 2);
        this.ctx.stroke();
    }

    private drawEdgeFade() {
        const ctx = this.ctx;
        const cw = this.canvas.width;
        const ch = this.canvas.height;
        const grad = ctx.createRadialGradient(
            cw / 2, ch / 2, Math.min(cw, ch) * 0.2,
            cw / 2, ch / 2, Math.max(cw, ch) * 0.78
        );
        grad.addColorStop(0, 'rgba(0,0,0,0)');
        grad.addColorStop(0.55, 'rgba(20,35,15,0.12)');
        grad.addColorStop(1, 'rgba(5,10,0,0.82)');

        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, cw, ch);
    }

    destroy() {
        super.destroy();
        cancelAnimationFrame(this.rafId);
    }
}