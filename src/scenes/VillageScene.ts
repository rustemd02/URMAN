import { BaseScene } from './BaseScene';
import { AssetLoader } from '../utils/AssetLoader';

// =========================================================
// Isometric 2.5D Village Scene — Polished Daylight Edition
// =========================================================

const TW = 96;
const TH = 48;
const GRID_W = 64;
const GRID_H = 64;
const VILLAGE_CX = 32;
const VILLAGE_CY = 32;

function toScreen(gx: number, gy: number): [number, number] {
    return [(gx - gy) * (TW / 2), (gx + gy) * (TH / 2)];
}
function toGrid(sx: number, sy: number): [number, number] {
    return [(sx / (TW/2) + sy / (TH/2)) / 2, (sy / (TH/2) - sx / (TW/2)) / 2];
}

const T_GRASS = 0, T_ROAD = 1, T_WATER = 3, T_FOREST = 4;
interface Point { x: number; y: number; }
interface RoadDef { points: Point[]; width: number; name: string; }

const CX = VILLAGE_CX, CY = VILLAGE_CY;
const MAIN_ROAD: RoadDef = {
    name: 'Трасса', width: 6.5,
    // Curves gently south-to-north with organic bends
    points: [{x:CX+2,y:-90},{x:CX+1,y:CY-18},{x:CX-1,y:CY-5},{x:CX+2,y:CY+8},{x:CX-2,y:CY+22},{x:CX+1,y:140}]
};
const SIDE_ROADS: RoadDef[] = [
    // ул. Ленина: gently curves, wide end, connects to main road at CX
    {name:'ул. Ленина',  width:2.8, points:[{x:7,y:CY-11},{x:CX-10,y:CY-12},{x:CX-4,y:CY-10},{x:CX+0.5,y:CY-10}]},
    {name:'ул. Ленина E',width:2.8, points:[{x:CX+0.5,y:CY-10},{x:CX+9,y:CY-11},{x:CX+18,y:CY-9},{x:57,y:CY-10}]},
    // ул. Мира: offset slightly so it curves into main road
    {name:'ул. Мира',    width:2.8, points:[{x:7,y:CY+5},{x:CX-9,y:CY+4},{x:CX-2,y:CY+5},{x:CX+1,y:CY+5}]},
    {name:'ул. Мира E',  width:2.8, points:[{x:CX+1,y:CY+5},{x:CX+10,y:CY+6},{x:CX+20,y:CY+4},{x:57,y:CY+5}]},
    // ул. Гагарина: lower, with curve
    {name:'ул. Гагарина',width:2.4, points:[{x:9,y:CY+17},{x:CX-7,y:CY+18},{x:CX-1,y:CY+17},{x:CX+1,y:CY+17}]},
    {name:'ул. Гагарина E',width:2.4, points:[{x:CX+1,y:CY+17},{x:CX+11,y:CY+18},{x:CX+22,y:CY+16},{x:55,y:CY+17}]},
    // пер. Кривой: winding alley
    {name:'пер. Кривой', width:1.6, points:[{x:CX-2,y:CY-5},{x:CX+6,y:CY-2},{x:CX+10,y:CY-9},{x:CX+14,y:CY-13}]},
];
const RIVER_PATH: Point[] = [{x:-80,y:9},{x:15,y:8},{x:CX,y:9},{x:50,y:10},{x:140,y:9}];

const TEXTURE_ASSETS = [
    {id:'grass',url:'/assets/grass_tex.jpg'},{id:'road',url:'/assets/road_tex.jpg'},
    {id:'wall_log',url:'/assets/wall_log.jpg'},{id:'roof_metal',url:'/assets/roof_metal.jpg'},
    {id:'hero_idle',url:'/assets/hero_idle.png'},{id:'hero_walk',url:'/assets/hero_walk.png'},
];

interface Building {
    id: string; name: string; gx: number; gy: number; gw: number; gh: number;
    wallColor: string; roofColor: string; wallDark: string;
    desc: string; actions: { label: string; fn: () => void }[];
    textureId?: string; invisible?: boolean;
}
interface PlotDef {
    gx: number; gy: number; gw: number; gh: number;
    livestock?: string; hasBanya?: boolean; hasGarage?: boolean; hasCar?: boolean;
    trees?: number; // palissadnik trees count
}

const B: Building[] = [
 // Bridge
 {id:'bridge',name:'Старый мост',gx:30,gy:7,gw:4,gh:3,wallColor:'',roofColor:'',wallDark:'',invisible:true,
  desc:'Старый мост через реку.',actions:[{label:'Осмотреть',fn:()=>alert('Тихий плеск воды.')}]},
 // Mosque
 {id:'mosque',name:'Мечеть',gx:25,gy:13,gw:4,gh:4,wallColor:'#f0f0e0',roofColor:'#4a8aba',wallDark:'#c0c0b0',
  desc:'Деревенская мечеть.',actions:[{label:'Войти',fn:()=>{}}]},
 // Public
 {id:'store',name:'Авыл кибете',gx:36,gy:24,gw:3,gh:2,wallColor:'#4a7aaa',roofColor:'#2a5580',wallDark:'#305a7a',
  desc:'Сельский магазин.',actions:[{label:'Зайти',fn:()=>alert('Магазин закрыт.')}]},
 {id:'council',name:'Сельсовет',gx:25,gy:27,gw:4,gh:3,wallColor:'#c06030',roofColor:'#8a2020',wallDark:'#904020',
  desc:'Здание администрации.',actions:[{label:'Постучаться',fn:()=>alert('Никого нет.')}]},
 {id:'clinic',name:'Медпункт',gx:36,gy:37,gw:3,gh:2,wallColor:'#e8e8e8',roofColor:'#cc2222',wallDark:'#c0c0c0',
  desc:'ФАП.',actions:[{label:'Войти',fn:()=>alert('Закрыто.')}]},
 // ул. Ленина left — offset from grid for organic feel
 {id:'house-baba',name:'Дом Бабая',gx:15,gy:16,gw:3,gh:3,wallColor:'#c99040',roofColor:'#3a8a25',wallDark:'#8a6020',
  desc:'Родной дом.',actions:[
   {label:'Компьютер бабая',fn:()=>{}},{label:'Бабай',fn:()=>alert('Бабай смотрит телевизор.')},
   {label:'Әби',fn:()=>alert('Әби готовит эчпочмаки.')}]},
 {id:'house-alsu',name:'Дом Алсу',gx:14,gy:23,gw:2,gh:2,wallColor:'#d8a0b0',roofColor:'#8a4060',wallDark:'#a07080',
  desc:'Дом Алсу. Из окна доносится музыка.',actions:[
   {label:'Постучать',fn:()=>alert('Алсу: «Кем анда? Ааа, син...»')},
   {label:'Подарить конфеты',fn:()=>alert('Алсу улыбается. ❤️')}]},
 // ул. Ленина right — slightly staggered
 {id:'house-zarifa',name:'Дом Зарифы апы',gx:44,gy:16,gw:2,gh:2,wallColor:'#b09040',roofColor:'#2a7020',wallDark:'#806020',
  desc:'Дом зарифы.',actions:[{label:'Постучать',fn:()=>alert('Тихо.')}]},
 {id:'house-fanis',name:'Дом Фаниса абый',gx:45,gy:23,gw:3,gh:2,wallColor:'#8a7a60',roofColor:'#4a4a30',wallDark:'#5a5040',
  desc:'Дом Фаниса. Во дворе ВАЗ-2106.',actions:[{label:'Постучать',fn:()=>alert('Фанис: «Кил, бала!»')}]},
 // ул. Мира left — offset back from street
 {id:'house-ildar',name:'Дом Ильдара',gx:14,gy:30,gw:2,gh:2,wallColor:'#a08040',roofColor:'#1a6020',wallDark:'#704020',
  desc:'Дом Ильдара.',actions:[{label:'Постучать',fn:()=>alert('Ильдар на поле.')}]},
 {id:'house-gulnara',name:'Дом Гульнары',gx:15,gy:37,gw:2,gh:2,wallColor:'#c0a070',roofColor:'#2a8040',wallDark:'#907050',
  desc:'Дом Гульнары. Палисадник.',actions:[{label:'Постучать',fn:()=>alert('Гульнара поливает цветы.')}]},
 // ул. Мира right
 {id:'house-mansur',name:'Дом Мансура',gx:44,gy:30,gw:2,gh:2,wallColor:'#b08040',roofColor:'#2a8020',wallDark:'#806020',
  desc:'Дом Мансура.',actions:[{label:'Постучать',fn:()=>alert('🐕 Собака лает.')}]},
 {id:'house-rashid',name:'Дом Рашида',gx:45,gy:37,gw:2,gh:2,wallColor:'#907050',roofColor:'#3a5030',wallDark:'#604030',
  desc:'У Рашида гараж.',actions:[{label:'Постучать',fn:()=>alert('Рашид в гараже.')}]},
 // ул. Гагарина left
 {id:'house-abandoned',name:'Заброшенный дом',gx:15,gy:43,gw:2,gh:2,wallColor:'#706858',roofColor:'#3a3828',wallDark:'#4a4038',
  desc:'Жутковато...',actions:[{label:'Заглянуть',fn:()=>alert('Жутковато...')}]},
 {id:'house-nail',name:'Дом Наиля',gx:14,gy:50,gw:2,gh:2,wallColor:'#a09060',roofColor:'#2a5020',wallDark:'#706040',
  desc:'Дом Наиля. Трактор.',actions:[{label:'Постучать',fn:()=>alert('Наиль: «Сәлам!»')}]},
 // ул. Гагарина right
 {id:'house-rushania',name:'Дом Рушании',gx:44,gy:43,gw:2,gh:2,wallColor:'#c09050',roofColor:'#3a7020',wallDark:'#906030',
  desc:'Дом Рушании.',actions:[{label:'Постучать',fn:()=>alert('Рушания: «Кем?»')}]},
 {id:'house-razilya',name:'Дом Разили',gx:45,gy:50,gw:2,gh:2,wallColor:'#b0a070',roofColor:'#4a7030',wallDark:'#808050',
  desc:'Дом Разили.',actions:[{label:'Постучать',fn:()=>alert('Разиля: «Исәнмесез!»')}]},
 // Near river
 {id:'banya-common',name:'Общая баня',gx:38,gy:13,gw:2,gh:2,wallColor:'#6a3a1a',roofColor:'#1a2a0a',wallDark:'#4a2a0a',
  desc:'Баня на берегу.',actions:[{label:'Заглянуть',fn:()=>alert('Баня топится.')}]},
];
const BUILDINGS = B;

const PLOTS: PlotDef[] = [
  // Each plot lines up with its house, no overlaps with roads
  {gx:12,gy:14,gw:9,gh:7,livestock:'🐔',hasBanya:true,trees:3},       // Бабай
  {gx:11,gy:21,gw:8,gh:7,livestock:'🐈',trees:4},                     // Алсу
  {gx:41,gy:14,gw:9,gh:7,livestock:'🐄',hasBanya:true,hasCar:true,trees:2}, // Зарифа
  {gx:42,gy:21,gw:9,gh:7,hasGarage:true,hasCar:true,trees:1},         // Фанис
  {gx:11,gy:28,gw:8,gh:7,livestock:'🐓',hasBanya:true,trees:2},       // Ильдар
  {gx:12,gy:35,gw:8,gh:7,livestock:'🌻',trees:5},                     // Гульнара
  {gx:41,gy:28,gw:9,gh:7,livestock:'🐕',hasBanya:true,trees:1},       // Мансур
  {gx:42,gy:35,gw:9,gh:7,hasGarage:true,trees:2},                     // Рашид
  {gx:12,gy:41,gw:8,gh:7,trees:0},                                     // Заброшенный
  {gx:11,gy:48,gw:8,gh:7,livestock:'🚜',hasBanya:true,trees:2},       // Наиль
  {gx:41,gy:41,gw:9,gh:7,livestock:'🐑',hasBanya:true,trees:3},       // Рушания
  {gx:42,gy:48,gw:9,gh:7,hasBanya:true,trees:2},                      // Разиля
];

const LABELS = [
  {gx:36,gy:3,text:'трасса на КАЗАНЬ ↑'},{gx:8,gy:21,text:'ул. Ленина'},
  {gx:8,gy:35,text:'ул. Мира'},{gx:8,gy:49,text:'ул. Гагарина'},
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
        BUILDINGS.find(b => b.id === 'mosque')!.actions[0].fn = () => this.game.scenes.switchScene('mosque');

        // Compute world bounds (iso screen coords farthest corners)
        const corners = [toScreen(0, 0), toScreen(GRID_W, 0), toScreen(0, GRID_H), toScreen(GRID_W, GRID_H)];
        this.worldMinSX = Math.min(...corners.map(c => c[0])) - 300;
        this.worldMinSY = Math.min(...corners.map(c => c[1])) - 300;
        this.worldMaxSX = Math.max(...corners.map(c => c[0])) + 300;
        this.worldMaxSY = Math.max(...corners.map(c => c[1])) + 300;

        this.buildForestDots();

        // Load textures (async)
        AssetLoader.loadAll(TEXTURE_ASSETS).catch(() => console.log('Asset loading skipped - using procedural fallback.'));
        
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
        if (Math.abs(gx - VILLAGE_CX) < 3 && Math.abs(gy - (VILLAGE_CY-23)) < 2.5) return T_ROAD;
        // Main road
        if (this.distToPath(gx, gy, MAIN_ROAD.points) < MAIN_ROAD.width / 2) return T_ROAD;
        // Side roads
        for (const r of SIDE_ROADS) {
            if (this.distToPath(gx, gy, r.points) < r.width / 2) return T_ROAD;
        }
        // Forest boundary (matched to buildForestDots)
        if (gy < 5 || gy > 58 || gx < 8 || gx > 56) return T_FOREST;
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

        // 0. No relief — flat village

        // 1. Background grass
        const grassTex = AssetLoader.get('grass');
        if (grassTex) {
            const pattern = ctx.createPattern(grassTex, 'repeat');
            if (pattern) {
                ctx.fillStyle = pattern;
                ctx.save();
                ctx.scale(1/this.scale, 1/this.scale); // Keep pattern size consistent
                ctx.fillRect((this.worldMinSX - 600) * this.scale, (this.worldMinSY - 600) * this.scale,
                             (this.worldMaxSX - this.worldMinSX + 1200) * this.scale,
                             (this.worldMaxSY - this.worldMinSY + 1200) * this.scale);
                ctx.restore();
            }
        } else {
            ctx.fillStyle = '#4a8a38';
            const pad = 600;
            ctx.fillRect(this.worldMinSX - pad, this.worldMinSY - pad,
                         this.worldMaxSX - this.worldMinSX + pad * 2,
                         this.worldMaxSY - this.worldMinSY + pad * 2);
        }

        // 2. Grid
        this.drawGrid();

        // 3. Geography
        this.drawRiver();
        this.drawRoads();
        this.drawPaths();

        // 3. Forest boundary ring and Cemetery
        this.drawCemetery();
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

    // ------ Isometric grid ------
    private drawGrid() {
        const { ctx } = this;
        ctx.strokeStyle = 'rgba(0,0,0,0.08)';
        ctx.lineWidth = 0.5;
        // Draw grid lines for visible area only
        for (let gx = 0; gx <= GRID_W; gx++) {
            const [ax, ay] = toScreen(gx, 0);
            const [bx, by] = toScreen(gx, GRID_H);
            ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke();
        }
        for (let gy = 0; gy <= GRID_H; gy++) {
            const [ax, ay] = toScreen(0, gy);
            const [bx, by] = toScreen(GRID_W, gy);
            ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke();
        }
    }

    // ------ Dirt paths from houses to nearest street ------
    private drawPaths() {
        const { ctx } = this;
        ctx.setLineDash([]);
        for (const b of BUILDINGS) {
            if (b.invisible) continue;
            // Find nearest side-road Y or main-road X
            const bCX = b.gx + b.gw / 2;
            const bCY = b.gy + b.gh / 2;
            // Determine nearest street connection point
            let nearX = CX, nearY = bCY;
            let minDist = 999;
            // Check each street's points for closest approach
            const allRoads = [MAIN_ROAD, ...SIDE_ROADS];
            for (const road of allRoads) {
                for (const pt of road.points) {
                    const d = Math.hypot(pt.x - bCX, pt.y - bCY);
                    if (d < minDist) { minDist = d; nearX = pt.x; nearY = pt.y; }
                }
            }
            // Draw from building edge toward road
            const [fromX, fromY] = toScreen(bCX, bCY);
            const [toX, toY2] = toScreen(nearX, nearY);
            // Only draw short paths (avoid drawing across map)
            if (minDist < 10) {
                ctx.beginPath();
                ctx.moveTo(fromX, fromY);
                ctx.lineTo(toX, toY2);
                ctx.lineWidth = 18;
                ctx.strokeStyle = 'rgba(120,88,50,0.5)';
                ctx.lineCap = 'round';
                ctx.stroke();
                ctx.lineWidth = 10;
                ctx.strokeStyle = 'rgba(150,110,65,0.4)';
                ctx.stroke();
            }
        }
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

        // Bridge planks (or 3D model)
        this.drawBridge();
    }

    private drawBridge() {
        const { ctx } = this;
        const [bx, by] = toScreen(32, 9);
        
        // Better procedural bridge with polygons
        // Main deck (shadow/base)
        ctx.fillStyle = '#4a3018';
        ctx.fillRect(bx - 32, by - 12, 64, 24);
        
        // Wooden planks across the deck
        for (let i = -28; i < 30; i += 6) {
            ctx.fillStyle = i % 12 === 0 ? '#6a4a2a' : '#5a3a1a';
            ctx.fillRect(bx + i, by - 10, 4, 20);
        }
        
        // Railing posts
        ctx.fillStyle = '#3a2010';
        for (let x of [-28, -10, 10, 28]) {
            ctx.fillRect(bx + x - 2, by - 14, 4, 3); // top post
            ctx.fillRect(bx + x - 2, by + 11, 4, 3); // bottom post
        }
        
        // Railing lines
        ctx.strokeStyle = '#4a2a10';
        ctx.lineWidth = 1.5;
        ctx.beginPath(); ctx.moveTo(bx - 32, by - 13); ctx.lineTo(bx + 32, by - 13); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(bx - 32, by + 13); ctx.lineTo(bx + 32, by + 13); ctx.stroke();
    }

    // ------ Roads ------
    private drawRoads() {
        // Pass 1: Draw all shoulders (merged background)
        this.drawSmoothPath(MAIN_ROAD.points, MAIN_ROAD.width + 1.5, '#3a3a30');
        for (const r of SIDE_ROADS) {
            this.drawSmoothPath(r.points, r.width + 0.8, '#3a3a30');
        }

        // Pass 2: Draw all asphalt (merged surface)
        this.drawSmoothPath(MAIN_ROAD.points, MAIN_ROAD.width, '#383838');
        for (const r of SIDE_ROADS) {
            this.drawSmoothPath(r.points, r.width, '#404040');
        }

        // Pass 3: Markings
        this.drawSmoothPath(MAIN_ROAD.points, 0.18, 'rgba(255,255,220,0.25)', [12, 24]);
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
    private drawCemetery() {
        const { ctx } = this;
        const gxOff = 54, gyOff = 2;
        const [fx, fy] = toScreen(gxOff, gyOff);
        // Dark ground patch
        ctx.fillStyle = 'rgba(40,50,30,0.5)';
        ctx.beginPath(); ctx.ellipse(fx, fy, 160, 60, 0, 0, Math.PI*2); ctx.fill();
        // Iron fence
        ctx.strokeStyle = '#3a3a3a'; ctx.lineWidth = 2;
        ctx.beginPath(); ctx.ellipse(fx, fy, 160, 60, 0, 0, Math.PI*2); ctx.stroke();
        // Tombstones
        let seed = 42;
        const rng = () => { seed = (seed * 9301 + 49297) % 233280; return seed / 233280; };
        for (let i = 0; i < 25; i++) {
            const rx = gxOff - 2 + rng() * 5, ry = gyOff - 1.5 + rng() * 4;
            const [sx, sy] = toScreen(rx, ry);
            // Tombstone shape (rounded top)
            ctx.fillStyle = '#8a908a';
            ctx.fillRect(sx - 2, sy - 8, 5, 10);
            ctx.beginPath(); ctx.arc(sx + 0.5, sy - 8, 2.5, Math.PI, 0); ctx.fill();
            ctx.fillStyle = '#666'; ctx.fillRect(sx - 2, sy - 8, 2, 10); // shadow
        }
        ctx.fillStyle = 'rgba(255,255,255,0.8)'; ctx.font = '11px Tahoma';
        ctx.textAlign = 'center'; ctx.fillText('Зират', fx, fy - 50);
    }

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
        // Garden ground
        ctx.beginPath();
        ctx.moveTo(corners[0][0], corners[0][1]);
        for (let i = 1; i < 4; i++) ctx.lineTo(corners[i][0], corners[i][1]);
        ctx.closePath();
        ctx.fillStyle = 'rgba(80,120,50,0.25)';
        ctx.fill();
        // Wooden fence (brown posts along perimeter)
        ctx.strokeStyle = '#5a3a1a'; ctx.lineWidth = 1.5;
        ctx.stroke();
        // Fence posts
        const allPts = [corners[0], corners[1], corners[2], corners[3], corners[0]];
        for (let i = 0; i < allPts.length - 1; i++) {
            const steps = 5;
            for (let s = 0; s <= steps; s++) {
                const t = s / steps;
                const fx = allPts[i][0] + (allPts[i+1][0] - allPts[i][0]) * t;
                const fy = allPts[i][1] + (allPts[i+1][1] - allPts[i][1]) * t;
                ctx.fillStyle = '#4a2a0a';
                ctx.fillRect(fx - 1, fy - 6, 2, 6);
            }
        }
        // Garden rows
        ctx.strokeStyle = 'rgba(60,40,20,0.12)';
        for (let ry = p.gy + 0.5; ry < p.gy + p.gh; ry += 0.7) {
            const [ax, ay] = toScreen(p.gx + 0.3, ry);
            const [bx, by] = toScreen(p.gx + p.gw - 0.3, ry);
            ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke();
        }
        // Banya (small shed in back)
        if (p.hasBanya) {
            const [bx, by] = toScreen(p.gx + p.gw - 2, p.gy + p.gh - 2);
            ctx.fillStyle = '#5a3010'; ctx.fillRect(bx, by - 18, 20, 18);
            ctx.fillStyle = '#3a1a08'; ctx.fillRect(bx, by - 22, 20, 5); // roof
        }
        // Garage
        if (p.hasGarage) {
            const [gx, gy] = toScreen(p.gx + p.gw - 2, p.gy + p.gh - 2);
            ctx.fillStyle = '#666'; ctx.fillRect(gx, gy - 22, 24, 22);
            ctx.fillStyle = '#444'; ctx.fillRect(gx, gy - 26, 24, 5);
        }
        // Car
        if (p.hasCar) {
            const [cx, cy] = toScreen(p.gx + p.gw - 3, p.gy + 1);
            ctx.fillStyle = '#345'; ctx.fillRect(cx, cy - 6, 16, 8);
            ctx.fillStyle = '#222'; ctx.fillRect(cx + 1, cy - 9, 10, 4); // roof
        }
        // Palissadnik trees (in front of house)
        if (p.trees && p.trees > 0) {
            for (let t = 0; t < p.trees; t++) {
                const tx = p.gx + 0.5 + t * 1.2;
                const ty = p.gy + 0.5;
                const [sx2, sy2] = toScreen(tx, ty);
                ctx.fillStyle = '#1a5010'; ctx.beginPath(); ctx.arc(sx2, sy2 - 12, 8, 0, Math.PI*2); ctx.fill();
                ctx.fillStyle = '#3a2010'; ctx.fillRect(sx2 - 1, sy2 - 5, 2, 5);
            }
        }
        // Livestock emoji
        if (p.livestock) {
            const [ex, ey] = toScreen(p.gx + p.gw / 2, p.gy + p.gh - 1);
            ctx.font = `${TH * 0.5}px serif`; ctx.textAlign = 'center';
            ctx.fillText(p.livestock, ex, ey);
        }
    }

    private drawBuilding(b: Building) {
        if (b.invisible) return;
        const { ctx } = this;

        // Tiny deterministic jitter based on building id
        let seed = 0;
        for (let i = 0; i < b.id.length; i++) seed = (seed * 31 + b.id.charCodeAt(i)) & 0xFFFF;
        const jx = ((seed % 100) - 50) / 100 * 0.6; // ±0.3 grid units
        const jy = (((seed >> 4) % 100) - 50) / 100 * 0.6;
        const gx = b.gx + jx, gy = b.gy + jy;

        const tl = toScreen(gx, gy);
        const tr = toScreen(gx + b.gw, gy);
        const br = toScreen(gx + b.gw, gy + b.gh);
        const bl = toScreen(gx, gy + b.gh);
        const H = TH * 1.2; // Height of block

        // Drop shadow
        ctx.save();
        ctx.shadowColor = 'rgba(0,0,0,0.3)';
        ctx.shadowBlur = 8;
        ctx.shadowOffsetX = 4;
        ctx.shadowOffsetY = 4;

        // Top face (the visible roof/top)
        ctx.beginPath();
        ctx.moveTo(tl[0], tl[1]);
        ctx.lineTo(tr[0], tr[1]);
        ctx.lineTo(br[0], br[1]);
        ctx.lineTo(bl[0], bl[1]);
        ctx.closePath();
        ctx.fillStyle = b.roofColor;
        ctx.fill();
        ctx.shadowBlur = 0; ctx.shadowOffsetX = 0; ctx.shadowOffsetY = 0;
        ctx.strokeStyle = 'rgba(0,0,0,0.25)'; ctx.lineWidth = 1.5;
        ctx.stroke();
        ctx.restore();

        // Left side face
        ctx.beginPath();
        ctx.moveTo(bl[0], bl[1]);
        ctx.lineTo(tl[0], tl[1]);
        ctx.lineTo(tl[0], tl[1] + H);
        ctx.lineTo(bl[0], bl[1] + H);
        ctx.closePath();
        ctx.fillStyle = this.darken(b.roofColor, 0.35);
        ctx.fill();
        ctx.strokeStyle = 'rgba(0,0,0,0.2)'; ctx.lineWidth = 1;
        ctx.stroke();

        // Right side face
        ctx.beginPath();
        ctx.moveTo(br[0], br[1]);
        ctx.lineTo(bl[0], bl[1]);
        ctx.lineTo(bl[0], bl[1] + H);
        ctx.lineTo(br[0], br[1] + H);
        ctx.closePath();
        ctx.fillStyle = this.darken(b.roofColor, 0.5);
        ctx.fill();
        ctx.strokeStyle = 'rgba(0,0,0,0.2)'; ctx.lineWidth = 1;
        ctx.stroke();

        // Label on top
        const midX = (tl[0] + tr[0] + bl[0] + br[0]) / 4;
        const midY = (tl[1] + tr[1] + bl[1] + br[1]) / 4;
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 10px Tahoma,sans-serif';
        ctx.textAlign = 'center';
        ctx.shadowColor = '#000'; ctx.shadowBlur = 3;
        ctx.fillText(b.name, midX, midY);
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

        const heroTex = AssetLoader.get(this.walking ? 'hero_walk' : 'hero_idle');
        if (heroTex) {
            // Draw sprite if loaded
            ctx.drawImage(heroTex, sx - 20, sy - 50, 40, 50);
        } else {
            // Procedural character (legs, body, head)
            // Legs (animated!)
            ctx.fillStyle = '#1a1a3a';
            ctx.fillRect(sx - 6, sy - 12, 5, 14 + leg);    // left leg
            ctx.fillRect(sx + 1, sy - 12, 5, 14 - leg);    // right leg
            // Shoes
            ctx.fillStyle = '#2a1a0a';
            ctx.fillRect(sx - 7, sy + 1 + leg, 6, 3);
            ctx.fillRect(sx + 1, sy + 1 - leg, 6, 3);
            // Body (torso)
            ctx.fillStyle = '#c03030'; const bodyTop = sy - 30; ctx.fillRect(sx - 7, bodyTop, 14, 20);
            // Head
            ctx.fillStyle = '#f5cba7'; ctx.beginPath(); ctx.arc(sx, bodyTop - 8, 9, 0, Math.PI * 2); ctx.fill();
            // Hair
            ctx.fillStyle = '#2a1a0a'; ctx.beginPath(); ctx.arc(sx, bodyTop - 12, 9, Math.PI, 2 * Math.PI); ctx.fill();
        }
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
        const cw = canvas.width;
        const ch = canvas.height;
        
        // Deep edge vignette to hide the "end of the world"
        const grad = ctx.createRadialGradient(
            cw / 2, ch / 2, Math.min(cw, ch) * 0.2, 
            cw / 2, ch / 2, Math.max(cw, ch) * 0.75
        );
        grad.addColorStop(0, 'rgba(0,0,0,0)');
        grad.addColorStop(0.5, 'rgba(20,35,15,0.1)'); // Hint of forest green-brown
        grad.addColorStop(1, 'rgba(5,10,0,0.8)');   // Deep dark boundary
        
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, cw, ch);
    }

    // ------ Forest dot generation ------
    private buildForestDots() {
        this.forestDots = [];
        const seed = 0xDEADBEEF;
        let s = seed;
        const rng = () => { s = (s * 1664525 + 1013904223) & 0xffffffff; return (s >>> 0) / 0xffffffff; };

        const colors = ['#13240e', '#1a3010', '#1e3814', '#0e1a08', '#223a16'];

        // Helper to add forest chunk
        const addChunk = (count: number, xMin: number, xMax: number, yMin: number, yMax: number, rMin: number, rMax: number) => {
            for (let i = 0; i < count; i++) {
                const gx = xMin + rng() * (xMax - xMin);
                const gy = yMin + rng() * (yMax - yMin);
                const [sx, sy] = toScreen(gx, gy);
                this.forestDots.push({ x: sx, y: sy, r: rMin + rng() * rMax, c: colors[Math.floor(rng() * colors.length)] });
            }
        };

        // Surround village with very wide forest — far beyond visible area
        addChunk(800, -20, GRID_W+20, -20, 5, 16, 35);     // North
        addChunk(800, -20, GRID_W+20, 58, GRID_H+20, 16, 35); // South
        addChunk(500, -20, 8, 5, 58, 14, 28);               // West
        addChunk(500, 56, GRID_W+20, 5, 58, 14, 28);        // East
        // Extra density in corners
        addChunk(300, -20, 14, -20, 14, 18, 40);  // NW corner
        addChunk(300, 50, GRID_W+20, -20, 14, 18, 40); // NE corner
        addChunk(300, -20, 14, 50, GRID_H+20, 18, 40); // SW corner
        addChunk(300, 50, GRID_W+20, 50, GRID_H+20, 18, 40); // SE corner
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
