import { BaseScene } from './BaseScene';

// =========================================================
// Isometric 2.5D Village Scene — Polished Daylight Edition
// =========================================================

const TW = 96;
const TH = 48;
const GRID_W = 64;
const GRID_H = 64;

// Village center (all roads converge here)
const VILLAGE_CX = 32;
const VILLAGE_CY = 32;

function toScreen(gx: number, gy: number): [number, number] {
    return [
        (gx - gy) * (TW / 2),
        (gx + gy) * (TH / 2),
    ];
}

function toGrid(sx: number, sy: number): [number, number] {
    return [
        (sx / (TW / 2) + sy / (TH / 2)) / 2,
        (sy / (TH / 2) - sx / (TW / 2)) / 2,
    ];
}

// Tile types (for collision logic)
const T_GRASS = 0;
const T_ROAD  = 1;
const T_WATER = 3;
const T_FOREST = 4;

// --- Spline Geography ---
interface Point { x: number; y: number; }
interface RoadDef { points: Point[]; width: number; name: string; }

// Main highway: straight with very gentle curves
const MAIN_ROAD: RoadDef = {
    name: 'Трасса (ул. Хәбибуллина)',
    width: 5,
    points: [
        { x: 32, y: 6 },   // bridge approach from north
        { x: 32, y: 12 },
        { x: 32, y: 20 },
        { x: 32, y: 30 },
        { x: 32, y: 40 },
        { x: 32, y: 50 },
        { x: 32, y: 60 },
    ]
};

// Side streets branch off the main road
const SIDE_ROADS: RoadDef[] = [
    { name: 'ул. Ленина',   width: 2.8, points: [{ x: 12, y: 20 }, { x: 22, y: 20 }, { x: 32, y: 20 }, { x: 42, y: 20 }, { x: 52, y: 20 }] },
    { name: 'ул. Мира',     width: 2.8, points: [{ x: 12, y: 34 }, { x: 22, y: 34 }, { x: 32, y: 34 }, { x: 42, y: 34 }, { x: 52, y: 34 }] },
    { name: 'ул. Гагарина', width: 2.5, points: [{ x: 14, y: 48 }, { x: 24, y: 48 }, { x: 32, y: 48 }, { x: 42, y: 48 }, { x: 50, y: 48 }] },
];

// River runs east–west, gently curved, north of village
const RIVER_PATH: Point[] = [
    { x: -5, y: 9 }, { x: 15, y: 8 }, { x: 25, y: 9 }, { x: 32, y: 9 },
    { x: 40, y: 10 }, { x: 55, y: 9 }, { x: 70, y: 10 }
];

// --- Buildings ---
interface Building {
    id: string; name: string;
    gx: number; gy: number; gw: number; gh: number;
    wallColor: string; roofColor: string; wallDark: string;
    desc: string; actions: { label: string; fn: () => void }[];
}
interface PlotDef { gx: number; gy: number; gw: number; gh: number; livestock?: string; }

// Buildings are placed ALONG roads realistically
const BUILDINGS: Building[] = [
    // === On main road ===
    { id: 'store', name: 'Авыл кибете', gx: 35, gy: 25, gw: 3, gh: 2,
      wallColor: '#4a7aaa', roofColor: '#2a5580', wallDark: '#305a7a',
      desc: 'Сельский магазин.',
      actions: [{ label: 'Зайти', fn: () => alert('Магазин закрыт на обед.') }] },
    { id: 'house-council', name: 'Сельсовет', gx: 26, gy: 26, gw: 4, gh: 3,
      wallColor: '#c06030', roofColor: '#8a2020', wallDark: '#904020',
      desc: 'Здание администрации.',
      actions: [{ label: 'Постучаться', fn: () => alert('Никого нет.') }] },
    { id: 'clinic', name: 'Медпункт (ФАП)', gx: 35, gy: 38, gw: 3, gh: 2,
      wallColor: '#e8e8e8', roofColor: '#cc2222', wallDark: '#c0c0c0',
      desc: 'Фельдшерско-акушерский пункт.',
      actions: [{ label: 'Войти', fn: () => alert('Закрыто.') }] },

    // === ул. Ленина (верхняя) ===
    { id: 'house-baba', name: 'Дом Бабая', gx: 20, gy: 17, gw: 3, gh: 3,
      wallColor: '#c99040', roofColor: '#3a8a25', wallDark: '#8a6020',
      desc: 'Родной дом бабушки и дедушки.',
      actions: [
          { label: 'Компьютер бабая', fn: () => {} },
          { label: 'Поговорить с бабаем', fn: () => alert('Бабай смотрит телевизор.') },
          { label: 'Поговорить с әби', fn: () => alert('Әби готовит эчпочмаки.') },
      ] },
    { id: 'house-1', name: 'Дом Зарифы апы', gx: 42, gy: 17, gw: 2, gh: 2,
      wallColor: '#b09040', roofColor: '#2a7020', wallDark: '#806020',
      desc: 'Дом соседки.',
      actions: [{ label: 'Постучать', fn: () => alert('Тихо.') }] },

    // === ул. Мира (средняя) ===
    { id: 'house-2', name: 'Дом Ильдара', gx: 20, gy: 31, gw: 2, gh: 2,
      wallColor: '#a08040', roofColor: '#1a6020', wallDark: '#704020',
      desc: 'Дом друга Ильдара.',
      actions: [{ label: 'Постучать', fn: () => alert('Ильдар на поле.') }] },
    { id: 'house-4', name: 'Дом Мансура', gx: 42, gy: 31, gw: 2, gh: 2,
      wallColor: '#b08040', roofColor: '#2a8020', wallDark: '#806020',
      desc: 'Дом Мансура.',
      actions: [{ label: 'Постучать', fn: () => alert('Слышен лай собаки.') }] },

    // === ул. Гагарина (нижняя) ===
    { id: 'house-3', name: 'Заброшенный дом', gx: 20, gy: 45, gw: 2, gh: 2,
      wallColor: '#706858', roofColor: '#3a3828', wallDark: '#4a4038',
      desc: 'Старый заброшенный дом.',
      actions: [{ label: 'Заглянуть', fn: () => alert('Жутковато...') }] },
    { id: 'house-5', name: 'Дом Рушании', gx: 42, gy: 45, gw: 2, gh: 2,
      wallColor: '#c09050', roofColor: '#3a7020', wallDark: '#906030',
      desc: 'Дом Рушании.',
      actions: [{ label: 'Постучать', fn: () => alert('Сейчас не может говорить.') }] },

    // === Near river ===
    { id: 'mosque', name: 'Мечеть', gx: 26, gy: 13, gw: 3, gh: 3,
      wallColor: '#f0f0e0', roofColor: '#4a8aba', wallDark: '#c0c0b0',
      desc: 'Деревенская мечеть. Слышен азан.',
      actions: [{ label: 'Войти', fn: () => alert('Мечеть открыта.') }] },
    { id: 'banya', name: 'Баня у реки', gx: 38, gy: 13, gw: 2, gh: 2,
      wallColor: '#6a3a1a', roofColor: '#1a2a0a', wallDark: '#4a2a0a',
      desc: 'Деревенская баня на берегу.',
      actions: [{ label: 'Заглянуть', fn: () => alert('Баня топится.') }] },
];

const PLOTS: PlotDef[] = BUILDINGS.map(b => ({
    gx: b.gx - 1, gy: b.gy - 1, gw: b.gw + 2, gh: b.gh + 3,
    livestock: Math.random() > 0.7 ? (Math.random() > 0.5 ? '🐄' : '🐓') : undefined
}));

const LABELS = [
    { gx: 36, gy: 14, text: 'трасса на КАЗАНЬ ↑' },
    { gx: 14, gy: 20, text: 'ул. Ленина' },
    { gx: 14, gy: 34, text: 'ул. Мира' },
    { gx: 16, gy: 48, text: 'ул. Гагарина' },
];

// =========================================================
export class VillageScene extends BaseScene {
    private canvas!: HTMLCanvasElement;
    private ctx!: CanvasRenderingContext2D;
    private modalEl!: HTMLElement;

    private camX = 0; private camY = 0; private scale = 1;
    // Start player in the center of the village
    private px = VILLAGE_CX; private py = VILLAGE_CY;
    private tx = VILLAGE_CX; private ty = VILLAGE_CY;
    private walking = false; private legPhase = 0;
    private dragging = false; private dragSX = 0; private dragSY = 0;
    private camSX = 0; private camSY = 0;
    private didDrag = false; private rafId = 0; private lastTs = 0;
    private forestDots: { x: number; y: number; r: number; c: string }[] = [];

    // Pre-computed screen bounds for the world
    private worldMinSX = 0; private worldMinSY = 0;
    private worldMaxSX = 0; private worldMaxSY = 0;

    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
        <style>
            .vr { position:relative; width:100vw; height:100vh; overflow:hidden; background:#4a8a38; }
            .vc { display:block; cursor:crosshair; }
            .v-hud { position:absolute; top:14px; left:14px; color:#fff; font-family:'Tahoma',sans-serif;
                     pointer-events:none; text-shadow:1px 1px 3px rgba(0,0,0,0.6); }
            .v-hud h3 { margin:0 0 3px; font-size:15px; }
            .v-hud p  { margin:0; font-size:11px; opacity:.7; }
            .v-ov { display:none; position:absolute; inset:0; background:rgba(0,0,0,.55);
                    align-items:center; justify-content:center; z-index:100; }
            .v-ov.open { display:flex; }
            .v-mod { background:#1a1a24; border:2px solid #5a4a2a; border-radius:12px;
                     padding:26px 30px; min-width:300px; color:white;
                     box-shadow:0 12px 48px rgba(0,0,0,.8); animation:mIn .15s ease; }
            @keyframes mIn { from { transform:scale(.92) translateY(8px); opacity:0; } to { transform:scale(1); opacity:1; } }
            .v-mod h2 { margin:0 0 6px; color:#e6b34b; font-size:17px; }
            .v-mod p  { color:#ccc; font-size:12px; margin:0 0 14px; }
            .v-btn { display:block; width:100%; padding:10px 14px; margin:6px 0;
                     background:#252530; color:#eee; border:1px solid #4a4a5a;
                     border-radius:7px; font-size:13px; cursor:pointer; transition:background .14s; }
            .v-btn:hover { background:#353545; }
            .v-cls { background:transparent; border:none; color:#888; font-size:11px;
                     cursor:pointer; text-decoration:underline; margin-top:6px; }
        </style>
        <div class="vr" id="vr">
            <canvas class="vc" id="vc"></canvas>
            <div class="v-hud">
                <h3>Кара-Урман</h3>
                <p>Скролл — зум · Тянуть — камера · Клик — идти / войти</p>
            </div>
            <div class="v-ov" id="vov"><div class="v-mod">
                <h2 id="vt"></h2><p id="vd"></p><div id="vb"></div>
                <button class="v-cls" id="vcl">Закрыть</button>
            </div></div>
        </div>`;

        this.canvas  = container.querySelector('#vc')!;
        this.ctx     = this.canvas.getContext('2d')!;
        this.modalEl = container.querySelector('#vov')!;
        container.querySelector('#vcl')!.addEventListener('click', () => this.modalEl.classList.remove('open'));

        // Wire dynamic action
        BUILDINGS.find(b => b.id === 'house-baba')!.actions[0].fn = () => this.game.scenes.switchScene('computer');

        // Compute world bounds (iso screen coords farthest corners)
        const corners = [toScreen(0, 0), toScreen(GRID_W, 0), toScreen(0, GRID_H), toScreen(GRID_W, GRID_H)];
        this.worldMinSX = Math.min(...corners.map(c => c[0])) - 200;
        this.worldMinSY = Math.min(...corners.map(c => c[1])) - 200;
        this.worldMaxSX = Math.max(...corners.map(c => c[0])) + 200;
        this.worldMaxSY = Math.max(...corners.map(c => c[1])) + 200;

        this.buildForestDots();

        const resize = () => {
            this.canvas.width  = window.innerWidth;
            this.canvas.height = window.innerHeight;
            this.centerOnPlayer();
        };
        window.addEventListener('resize', resize);
        resize();
        this.initInput();
        this.rafId = requestAnimationFrame(ts => this.loop(ts));
    }

    // ------ Camera ------
    private centerOnPlayer() {
        const [sx, sy] = toScreen(this.px, this.py);
        this.camX = this.canvas.width  / 2 - sx * this.scale;
        this.camY = this.canvas.height / 2 - sy * this.scale;
    }

    private clampCamera() {
        const cw = this.canvas.width, ch = this.canvas.height;
        const maxCamX = -this.worldMinSX * this.scale + 100;
        const minCamX = cw - this.worldMaxSX * this.scale - 100;
        const maxCamY = -this.worldMinSY * this.scale + 100;
        const minCamY = ch - this.worldMaxSY * this.scale - 100;
        this.camX = Math.max(Math.min(minCamX, maxCamX), Math.min(this.camX, Math.max(minCamX, maxCamX)));
        this.camY = Math.max(Math.min(minCamY, maxCamY), Math.min(this.camY, Math.max(minCamY, maxCamY)));
    }

    // ------ Input ------
    private initInput() {
        this.canvas.addEventListener('wheel', e => {
            e.preventDefault();
            const f = e.deltaY < 0 ? 1.12 : 0.89;
            const ns = Math.max(0.35, Math.min(2.5, this.scale * f));
            const mx = e.clientX, my = e.clientY;
            this.camX = mx - (mx - this.camX) * (ns / this.scale);
            this.camY = my - (my - this.camY) * (ns / this.scale);
            this.scale = ns;
            this.clampCamera();
        }, { passive: false });

        this.canvas.addEventListener('mousedown', e => {
            this.dragging = true; this.didDrag = false;
            this.dragSX = e.clientX; this.dragSY = e.clientY;
            this.camSX = this.camX;  this.camSY = this.camY;
        });
        window.addEventListener('mousemove', e => {
            if (!this.dragging) return;
            const dx = e.clientX - this.dragSX, dy = e.clientY - this.dragSY;
            if (Math.abs(dx) + Math.abs(dy) > 5) this.didDrag = true;
            this.camX = this.camSX + dx;
            this.camY = this.camSY + dy;
            this.clampCamera();
        });
        window.addEventListener('mouseup', e => {
            if (!this.dragging) return;
            this.dragging = false;
            if (!this.didDrag) this.onClick(e.clientX, e.clientY);
        });
    }

    private onClick(cx: number, cy: number) {
        const sx = (cx - this.camX) / this.scale;
        const sy = (cy - this.camY) / this.scale;
        const [gx, gy] = toGrid(sx, sy);

        // Check building clicks
        for (const b of BUILDINGS) {
            if (gx >= b.gx && gx < b.gx + b.gw && gy >= b.gy && gy < b.gy + b.gh) {
                this.openModal(b); return;
            }
        }

        // Block water
        if (this.getGroundTypeAt(gx, gy) === T_WATER) {
            this.onOSMessage?.('Су', 'Тирән. Монда идти нельзя.');
            return;
        }
        // Block forest
        if (this.getGroundTypeAt(gx, gy) === T_FOREST) return;

        this.tx = gx; this.ty = gy; this.walking = true;
    }

    private openModal(b: Building) {
        (this.container!.querySelector('#vt') as HTMLElement).textContent = b.name;
        (this.container!.querySelector('#vd') as HTMLElement).textContent = b.desc;
        const btns = this.container!.querySelector('#vb') as HTMLElement;
        btns.innerHTML = '';
        for (const a of b.actions) {
            const btn = document.createElement('button');
            btn.className = 'v-btn'; btn.textContent = a.label;
            btn.addEventListener('click', a.fn); btns.appendChild(btn);
        }
        this.modalEl.classList.add('open');
    }

    // ------ Ground type via distance fields ------
    private getGroundTypeAt(gx: number, gy: number): number {
        // River
        if (this.distToPath(gx, gy, RIVER_PATH) < 2.0) return T_WATER;
        // Bridge area — passable
        if (Math.abs(gx - 32) < 3 && Math.abs(gy - 9) < 2.5) return T_ROAD;
        // Main road
        if (this.distToPath(gx, gy, MAIN_ROAD.points) < MAIN_ROAD.width / 2) return T_ROAD;
        // Side roads
        for (const r of SIDE_ROADS) {
            if (this.distToPath(gx, gy, r.points) < r.width / 2) return T_ROAD;
        }
        // Forest boundary
        if (gy < 7 || gy > 55 || gx < 8 || gx > 56) return T_FOREST;
        return T_GRASS;
    }

    private distToPath(x: number, y: number, path: Point[]): number {
        let min = 999;
        for (let i = 0; i < path.length - 1; i++) {
            const d = this.distSeg(x, y, path[i], path[i + 1]);
            if (d < min) min = d;
        }
        return min;
    }

    private distSeg(px: number, py: number, a: Point, b: Point): number {
        const dx = b.x - a.x, dy = b.y - a.y;
        const l2 = dx * dx + dy * dy;
        if (l2 === 0) return Math.hypot(px - a.x, py - a.y);
        const t = Math.max(0, Math.min(1, ((px - a.x) * dx + (py - a.y) * dy) / l2));
        return Math.hypot(px - (a.x + t * dx), py - (a.y + t * dy));
    }

    // ------ Game Loop ------
    private loop(ts: number) {
        const dt = Math.min((ts - this.lastTs) / 1000, 0.05);
        this.lastTs = ts;
        this.update(dt);
        this.draw();
        this.rafId = requestAnimationFrame(t => this.loop(t));
    }

    private update(dt: number) {
        if (!this.walking) return;
        const dx = this.tx - this.px, dy = this.ty - this.py;
        const d = Math.sqrt(dx * dx + dy * dy);
        if (d < 0.08) { this.walking = false; this.legPhase = 0; return; }
        const spd = 5.5;
        const step = Math.min(spd * dt, d);
        this.px += (dx / d) * step;
        this.py += (dy / d) * step;
        this.legPhase += dt * 12;
    }

    // ------ Draw ------
    private draw() {
        const { ctx, canvas } = this;
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.save();
        ctx.translate(this.camX, this.camY);
        ctx.scale(this.scale, this.scale);

        // 1. Background grass
        ctx.fillStyle = '#4a8a38';
        const pad = 600;
        ctx.fillRect(this.worldMinSX - pad, this.worldMinSY - pad,
                     this.worldMaxSX - this.worldMinSX + pad * 2,
                     this.worldMaxSY - this.worldMinSY + pad * 2);

        // 2. Geography
        this.drawRiver();
        this.drawRoads();

        // 3. Forest boundary ring
        this.drawForest();

        // 4. Sorted world objects
        const drawables: { depth: number; fn: () => void }[] = [];
        for (const p of PLOTS) drawables.push({ depth: p.gx + p.gy, fn: () => this.drawPlot(p) });
        for (const b of BUILDINGS) drawables.push({ depth: b.gx + b.gy + b.gw + b.gh, fn: () => this.drawBuilding(b) });
        drawables.push({ depth: this.px + this.py + 0.5, fn: () => this.drawPlayer() });
        drawables.sort((a, b) => a.depth - b.depth);
        for (const d of drawables) d.fn();

        // 5. Labels
        for (const l of LABELS) {
            const [sx, sy] = toScreen(l.gx, l.gy);
            ctx.fillStyle = 'rgba(60,40,10,0.7)';
            ctx.font = 'bold 11px Tahoma, sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(l.text, sx, sy);
        }

        // 6. Bridge sign
        this.drawBridgeSign();

        // 7. Destination ping
        if (this.walking) {
            const [sx, sy] = toScreen(this.tx, this.ty);
            const a = 0.4 + 0.4 * Math.sin(Date.now() / 200);
            ctx.strokeStyle = `rgba(255,255,255,${a})`;
            ctx.lineWidth = 2 / this.scale;
            ctx.beginPath(); ctx.arc(sx, sy - 4, 8, 0, Math.PI * 2); ctx.stroke();
        }

        ctx.restore();

        // 8. Soft edge vignette (subtle, light)
        this.drawEdgeFade();
    }

    // ------ River ------
    private drawRiver() {
        const { ctx } = this;
        const time = Date.now() / 1000;

        // River bed (brown shore)
        this.drawSmoothPath(RIVER_PATH, 6.5, '#5a7040');
        // River water
        this.drawSmoothPath(RIVER_PATH, 4.5, '#2a6898');

        // Animated flow
        ctx.save();
        const pts = RIVER_PATH.map(p => toScreen(p.x, p.y));
        ctx.beginPath();
        ctx.moveTo(pts[0][0], pts[0][1]);
        for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);

        ctx.setLineDash([15, 80]);
        ctx.lineDashOffset = -time * 40;
        ctx.lineWidth = 6;
        ctx.strokeStyle = 'rgba(180,220,255,0.12)';
        ctx.lineCap = 'round';
        ctx.stroke();

        ctx.lineDashOffset = -time * 25 - 40;
        ctx.lineWidth = 4;
        ctx.strokeStyle = 'rgba(255,255,255,0.06)';
        ctx.stroke();
        ctx.restore();

        // Bridge planks
        this.drawBridge();
    }

    private drawBridge() {
        const { ctx } = this;
        const [bx, by] = toScreen(32, 9);
        // Wooden planks
        ctx.fillStyle = '#6a4a2a';
        ctx.fillRect(bx - 30, by - 8, 60, 16);
        ctx.strokeStyle = '#4a3018';
        ctx.lineWidth = 1;
        for (let i = -28; i < 28; i += 8) {
            ctx.beginPath(); ctx.moveTo(bx + i, by - 8); ctx.lineTo(bx + i, by + 8); ctx.stroke();
        }
        // Railings
        ctx.strokeStyle = '#5a3a1a';
        ctx.lineWidth = 2;
        ctx.beginPath(); ctx.moveTo(bx - 30, by - 8); ctx.lineTo(bx + 30, by - 8); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(bx - 30, by + 8); ctx.lineTo(bx + 30, by + 8); ctx.stroke();
    }

    // ------ Roads ------
    private drawRoads() {
        // Main highway — wider, with shoulder
        this.drawSmoothPath(MAIN_ROAD.points, MAIN_ROAD.width + 1, '#3a3a30'); // shoulder
        this.drawSmoothPath(MAIN_ROAD.points, MAIN_ROAD.width, '#383838');
        // Center line dashes
        this.drawSmoothPath(MAIN_ROAD.points, 0.15, 'rgba(255,255,200,0.2)', [12, 24]);

        // Side streets
        for (const r of SIDE_ROADS) {
            this.drawSmoothPath(r.points, r.width + 0.5, '#3a3a30');
            this.drawSmoothPath(r.points, r.width, '#404040');
        }
    }

    private drawSmoothPath(points: Point[], width: number, color: string, dash: number[] = []) {
        const { ctx } = this;
        if (points.length < 2) return;
        ctx.save();
        ctx.beginPath();

        const screenPts = points.map(p => toScreen(p.x, p.y));
        ctx.moveTo(screenPts[0][0], screenPts[0][1]);

        // Use quadratic curves for smooth bends
        if (screenPts.length === 2) {
            ctx.lineTo(screenPts[1][0], screenPts[1][1]);
        } else {
            for (let i = 1; i < screenPts.length - 1; i++) {
                const mx = (screenPts[i][0] + screenPts[i + 1][0]) / 2;
                const my = (screenPts[i][1] + screenPts[i + 1][1]) / 2;
                ctx.quadraticCurveTo(screenPts[i][0], screenPts[i][1], mx, my);
            }
            const last = screenPts[screenPts.length - 1];
            ctx.lineTo(last[0], last[1]);
        }

        ctx.setLineDash(dash);
        ctx.lineWidth = width * 14;
        ctx.lineJoin = 'round';
        ctx.lineCap = 'round';
        ctx.strokeStyle = color;
        ctx.stroke();
        ctx.restore();
    }

    // ------ Forest ring ------
    private drawForest() {
        const { ctx } = this;
        for (const dot of this.forestDots) {
            ctx.fillStyle = dot.c;
            ctx.beginPath();
            ctx.arc(dot.x, dot.y, dot.r, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    // ------ Plots & Buildings ------
    private drawPlot(p: PlotDef) {
        const { ctx } = this;
        const corners = [
            toScreen(p.gx, p.gy), toScreen(p.gx + p.gw, p.gy),
            toScreen(p.gx + p.gw, p.gy + p.gh), toScreen(p.gx, p.gy + p.gh)
        ];
        ctx.beginPath();
        ctx.moveTo(corners[0][0], corners[0][1]);
        for (let i = 1; i < 4; i++) ctx.lineTo(corners[i][0], corners[i][1]);
        ctx.closePath();
        ctx.fillStyle = 'rgba(90,130,55,0.35)';
        ctx.fill();
        ctx.strokeStyle = '#3a5a20';
        ctx.lineWidth = 1;
        ctx.stroke();

        // Garden rows
        ctx.strokeStyle = 'rgba(60,40,20,0.15)';
        for (let ry = p.gy + 0.5; ry < p.gy + p.gh; ry += 0.7) {
            const [ax, ay] = toScreen(p.gx + 0.3, ry);
            const [bx, by] = toScreen(p.gx + p.gw - 0.3, ry);
            ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke();
        }

        if (p.livestock) {
            const [ex, ey] = toScreen(p.gx + p.gw - 1, p.gy + p.gh - 0.5);
            ctx.font = `${TH * 0.6}px serif`;
            ctx.textAlign = 'center';
            ctx.fillText(p.livestock, ex, ey);
        }
    }

    private drawBuilding(b: Building) {
        const { ctx } = this;
        const H = TH * 1.5;
        const tl = toScreen(b.gx, b.gy);
        const tr = toScreen(b.gx + b.gw, b.gy);
        const br = toScreen(b.gx + b.gw, b.gy + b.gh);
        const bl = toScreen(b.gx, b.gy + b.gh);

        // Shadow
        ctx.fillStyle = 'rgba(0,0,0,0.15)';
        ctx.beginPath();
        ctx.moveTo(tl[0] + 6, tl[1] + H + 6);
        ctx.lineTo(tr[0] + 6, tr[1] + H + 6);
        ctx.lineTo(br[0] + 6, br[1] + H + 6);
        ctx.lineTo(bl[0] + 6, bl[1] + H + 6);
        ctx.closePath(); ctx.fill();

        // Left wall
        ctx.beginPath();
        ctx.moveTo(tl[0], tl[1]); ctx.lineTo(bl[0], bl[1]);
        ctx.lineTo(bl[0], bl[1] + H); ctx.lineTo(tl[0], tl[1] + H);
        ctx.closePath();
        ctx.fillStyle = b.wallColor; ctx.fill();
        ctx.strokeStyle = 'rgba(0,0,0,0.3)'; ctx.lineWidth = 1; ctx.stroke();

        // Right wall
        ctx.beginPath();
        ctx.moveTo(bl[0], bl[1]); ctx.lineTo(br[0], br[1]);
        ctx.lineTo(br[0], br[1] + H); ctx.lineTo(bl[0], bl[1] + H);
        ctx.closePath();
        ctx.fillStyle = b.wallDark; ctx.fill();
        ctx.strokeStyle = 'rgba(0,0,0,0.4)'; ctx.lineWidth = 1; ctx.stroke();

        // Window on left wall
        const lwx = (tl[0] + bl[0]) / 2, lwy = (tl[1] + bl[1]) / 2 + H * 0.25;
        ctx.fillStyle = '#f0d040';
        ctx.fillRect(lwx - 6, lwy, 10, 8);
        ctx.strokeStyle = '#7a5a10'; ctx.lineWidth = 0.7;
        ctx.strokeRect(lwx - 6, lwy, 10, 8);

        // Window on right wall
        const rwx = (br[0] + bl[0]) / 2, rwy = (br[1] + bl[1]) / 2 + H * 0.25;
        ctx.fillStyle = '#d4b020';
        ctx.fillRect(rwx - 5, rwy, 9, 7);
        ctx.strokeRect(rwx - 5, rwy, 9, 7);

        // Roof (iso top face)
        const roofGrad = ctx.createLinearGradient(
            (tl[0] + tr[0]) / 2, tl[1], (bl[0] + br[0]) / 2, bl[1]
        );
        roofGrad.addColorStop(0, b.roofColor);
        roofGrad.addColorStop(1, this.darken(b.roofColor, 0.25));
        ctx.beginPath();
        ctx.moveTo(tl[0], tl[1]); ctx.lineTo(tr[0], tr[1]);
        ctx.lineTo(br[0], br[1]); ctx.lineTo(bl[0], bl[1]);
        ctx.closePath();
        ctx.fillStyle = roofGrad; ctx.fill();
        ctx.strokeStyle = 'rgba(0,0,0,0.35)'; ctx.lineWidth = 1.5; ctx.stroke();

        // Name
        const midX = (tl[0] + tr[0] + bl[0] + br[0]) / 4;
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 10px Tahoma, sans-serif';
        ctx.textAlign = 'center';
        ctx.shadowColor = '#000'; ctx.shadowBlur = 3;
        ctx.fillText(b.name, midX, tl[1] - 8);
        ctx.shadowBlur = 0;
    }

    // ------ Player character (animated) ------
    private drawPlayer() {
        const { ctx } = this;
        const [sx, sy] = toScreen(this.px, this.py);
        const leg = this.walking ? Math.sin(this.legPhase) * 5 : 0;

        // Shadow
        ctx.fillStyle = 'rgba(0,0,0,0.2)';
        ctx.beginPath(); ctx.ellipse(sx, sy + 2, 11, 4, 0, 0, Math.PI * 2); ctx.fill();

        // Legs (animated!)
        ctx.fillStyle = '#1a1a3a';
        ctx.fillRect(sx - 6, sy - 12, 5, 14 + leg);    // left leg
        ctx.fillRect(sx + 1, sy - 12, 5, 14 - leg);    // right leg

        // Shoes
        ctx.fillStyle = '#2a1a0a';
        ctx.fillRect(sx - 7, sy + 1 + leg, 6, 3);
        ctx.fillRect(sx + 1, sy + 1 - leg, 6, 3);

        // Body (torso)
        ctx.fillStyle = '#c03030';
        const bodyTop = sy - 30;
        ctx.fillRect(sx - 7, bodyTop, 14, 20);
        // Side highlight
        ctx.fillStyle = '#a02020';
        ctx.fillRect(sx + 5, bodyTop + 2, 3, 16);

        // Arms
        const armSwing = this.walking ? Math.sin(this.legPhase + Math.PI) * 3 : 0;
        ctx.fillStyle = '#c03030';
        ctx.fillRect(sx - 10, bodyTop + 3 + armSwing, 4, 12);  // left arm
        ctx.fillRect(sx + 7, bodyTop + 3 - armSwing, 4, 12);   // right arm

        // Head
        ctx.fillStyle = '#f5cba7';
        ctx.beginPath(); ctx.arc(sx, bodyTop - 8, 9, 0, Math.PI * 2); ctx.fill();
        ctx.strokeStyle = '#c8a078'; ctx.lineWidth = 0.8; ctx.stroke();

        // Hair
        ctx.fillStyle = '#2a1a0a';
        ctx.beginPath(); ctx.arc(sx, bodyTop - 12, 9, Math.PI, 2 * Math.PI); ctx.fill();

        // Eyes
        ctx.fillStyle = '#222';
        ctx.fillRect(sx - 4, bodyTop - 10, 2.5, 2.5);
        ctx.fillRect(sx + 2, bodyTop - 10, 2.5, 2.5);
    }

    // ------ Bridge sign / Kazan ------
    private drawBridgeSign() {
        const { ctx } = this;
        // Bridge warning
        const [bx, by] = toScreen(34, 7);
        ctx.fillStyle = 'rgba(200,160,80,0.85)';
        ctx.fillRect(bx - 38, by - 28, 76, 20);
        ctx.strokeStyle = '#7a5520'; ctx.lineWidth = 1.5;
        ctx.strokeRect(bx - 38, by - 28, 76, 20);
        ctx.fillStyle = '#2a1a00';
        ctx.font = 'bold 9px Tahoma'; ctx.textAlign = 'center';
        ctx.fillText('⚠ Старый мост', bx, by - 14);

        // Kazan road sign
        const [sx2, sy2] = toScreen(32, 56);
        ctx.fillStyle = '#888'; ctx.fillRect(sx2 - 2, sy2 - 45, 4, 45);
        ctx.fillStyle = '#0055a4'; ctx.fillRect(sx2 - 45, sy2 - 72, 90, 28);
        ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5;
        ctx.strokeRect(sx2 - 43, sy2 - 70, 86, 24);
        ctx.fillStyle = '#fff'; ctx.font = 'bold 10px Tahoma'; ctx.textAlign = 'center';
        ctx.fillText('➔ КАЗАНЬ  120 км', sx2, sy2 - 54);
    }

    // ------ Edge vignette (light, soft) ------
    private drawEdgeFade() {
        const { ctx, canvas } = this;
        const cw = canvas.width, ch = canvas.height;

        // Top edge
        let grad = ctx.createLinearGradient(0, 0, 0, 60);
        grad.addColorStop(0, 'rgba(20,35,15,0.5)');
        grad.addColorStop(1, 'rgba(20,35,15,0)');
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, cw, 60);

        // Bottom edge
        grad = ctx.createLinearGradient(0, ch - 60, 0, ch);
        grad.addColorStop(0, 'rgba(20,35,15,0)');
        grad.addColorStop(1, 'rgba(20,35,15,0.5)');
        ctx.fillStyle = grad;
        ctx.fillRect(0, ch - 60, cw, 60);

        // Left edge
        grad = ctx.createLinearGradient(0, 0, 60, 0);
        grad.addColorStop(0, 'rgba(20,35,15,0.5)');
        grad.addColorStop(1, 'rgba(20,35,15,0)');
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, 60, ch);

        // Right edge
        grad = ctx.createLinearGradient(cw - 60, 0, cw, 0);
        grad.addColorStop(0, 'rgba(20,35,15,0)');
        grad.addColorStop(1, 'rgba(20,35,15,0.5)');
        ctx.fillStyle = grad;
        ctx.fillRect(cw - 60, 0, 60, ch);
    }

    // ------ Forest dot generation ------
    private buildForestDots() {
        this.forestDots = [];
        const seed = 0xDEADBEEF;
        let s = seed;
        const rng = () => { s = (s * 1664525 + 1013904223) & 0xffffffff; return (s >>> 0) / 0xffffffff; };

        const colors = ['#13240e', '#1a3010', '#1e3814', '#0e1a08', '#223a16'];

        // North forest (above river)
        for (let i = 0; i < 500; i++) {
            const gx = rng() * GRID_W;
            const gy = rng() * 8 - 2;
            const [sx, sy] = toScreen(gx, gy);
            this.forestDots.push({ x: sx, y: sy, r: 14 + rng() * 30, c: colors[Math.floor(rng() * colors.length)] });
        }
        // South forest
        for (let i = 0; i < 400; i++) {
            const gx = rng() * GRID_W;
            const gy = 54 + rng() * 14;
            const [sx, sy] = toScreen(gx, gy);
            this.forestDots.push({ x: sx, y: sy, r: 12 + rng() * 25, c: colors[Math.floor(rng() * colors.length)] });
        }
        // West forest
        for (let i = 0; i < 200; i++) {
            const gx = -4 + rng() * 14;
            const gy = 8 + rng() * 48;
            const [sx, sy] = toScreen(gx, gy);
            this.forestDots.push({ x: sx, y: sy, r: 10 + rng() * 22, c: colors[Math.floor(rng() * colors.length)] });
        }
        // East forest
        for (let i = 0; i < 200; i++) {
            const gx = 52 + rng() * 16;
            const gy = 8 + rng() * 48;
            const [sx, sy] = toScreen(gx, gy);
            this.forestDots.push({ x: sx, y: sy, r: 10 + rng() * 22, c: colors[Math.floor(rng() * colors.length)] });
        }
    }

    private darken(hex: string, amount: number): string {
        const n = parseInt(hex.slice(1), 16);
        const r = Math.max(0, ((n >> 16) & 0xff) * (1 - amount)) | 0;
        const g = Math.max(0, ((n >> 8) & 0xff) * (1 - amount)) | 0;
        const b = Math.max(0, (n & 0xff) * (1 - amount)) | 0;
        return `rgb(${r},${g},${b})`;
    }

    destroy() {
        super.destroy();
        cancelAnimationFrame(this.rafId);
    }
}
