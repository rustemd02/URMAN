import { SYSTEM_REGISTRY, SystemItem } from './registry';
import { renderChat } from '../apps/chat';
import { renderBrowser } from '../apps/browser';
import { renderMinesweeper } from '../apps/minesweeper';
import { renderNotepad } from '../apps/notepad';
import { renderPlayer } from '../apps/player';
import { renderTerminal } from '../apps/terminal';
import { renderWord } from '../apps/word';

export class DedOS {
    private screen: HTMLElement;
    private windowsContainer!: HTMLElement;
    private zIndex: number = 100;

    private activeWindow: HTMLElement | null = null;
    private resizingWindow: HTMLElement | null = null;
    private dragOffset = { x: 0, y: 0 };
    private resizeStart = { w: 0, h: 0, x: 0, y: 0 };
    private screenRect: DOMRect | null = null;

    constructor() {
        const el = document.getElementById('pc-screen');
        if (!el) throw new Error("PC screen element not found!");
        this.screen = el;
        this.initOS();
    }

    private initOS() {
        this.applyMonitorStyles();
        this.createLayers();
        this.renderDesktop();
        this.renderTaskbar();
        this.initGlobalEvents();
        this.updateClock();
        this.handleResize(); 
        window.addEventListener('resize', () => this.handleResize());
        this.showBootScreen();
    }

    private showBootScreen() {
        const bootOverlay = document.createElement('div');
        bootOverlay.style.cssText = `
            position: absolute; inset: 0; background: #000; z-index: 100000;
            display: flex; flex-direction: column; align-items: center; justify-content: center;
            color: #c0c0c0; font-family: 'Tahoma', sans-serif;
        `;
        bootOverlay.innerHTML = `<div style="font-size: 32px; font-weight: bold; font-style: italic;"><span style="color:#008080;">Тәрәзәләр</span> 98</div>`;
        this.screen.appendChild(bootOverlay);
        setTimeout(() => bootOverlay.remove(), 1000);
    }

    private handleResize() {
        const wrapper = this.screen.querySelector('.screen-content-wrapper') as HTMLElement;
        if (!wrapper) return;
        const screenW = this.screen.clientWidth;
        const screenH = this.screen.clientHeight;
        const scale = Math.min(screenW / 640, screenH / 480);
        wrapper.style.transform = `translate(-50%, -50%) scale(${scale})`;
    }

    private applyMonitorStyles() {
        const wrapper = document.getElementById('monitor-wrapper');
        if (wrapper && (window as any).monitorFrameUrl) {
            wrapper.style.backgroundImage = `url(${(window as any).monitorFrameUrl})`;
        }
    }

    private createLayers() {
        this.screen.innerHTML = '';
        const wrapper = document.createElement('div');
        wrapper.className = 'screen-content-wrapper';
        this.screen.appendChild(wrapper);

        const grid = document.createElement('div');
        grid.className = 'desktop-grid';
        wrapper.appendChild(grid);

        this.windowsContainer = document.createElement('div');
        this.windowsContainer.id = 'windows-container';
        wrapper.appendChild(this.windowsContainer);
    }

    private renderDesktop() {
        const grid = this.screen.querySelector('.desktop-grid');
        if (!grid) return;
        const desktopItems = SYSTEM_REGISTRY.filter(item => item.parentId === null);
        grid.innerHTML = desktopItems.map(item => `
            <div class="desktop-icon" data-id="${item.id}" style="width:50px; text-align:center; font-family:'MS Sans Serif'; cursor:pointer; color:#fff;">
                <div class="icon-img-container">${item.icon}</div>
                <div style="font-size:11px; text-shadow:1px 1px #000; margin-top:2px;">${item.name}</div>
            </div>
        `).join('');

        grid.querySelectorAll('.desktop-icon').forEach(el => {
            el.addEventListener('dblclick', () => {
                const id = el.getAttribute('data-id');
                const item = SYSTEM_REGISTRY.find(i => i.id === id);
                if (item) this.openWindow(item);
            });
        });
    }

    private renderTaskbar() {
        const wrapper = this.screen.querySelector('.screen-content-wrapper');
        const bar = document.createElement('div');
        bar.id = 'taskbar';
        bar.innerHTML = `
            <button class="start-btn"><b>Тәрәзәләр</b></button>
            <div class="taskbar-apps"></div>
            <div id="clock">00:00</div>
        `;
        if (wrapper) wrapper.appendChild(bar);
    }

    public openWindow(item: SystemItem) {
        if (item.id === 'village') {
            (window as any).URMAN.scenes.switchScene('village');
            return;
        }

        const existing = document.getElementById(`win-${item.id}`);
        if (existing) {
            this.bringToFront(existing);
            return;
        }

        const win = document.createElement('div');
        win.id = `win-${item.id}`;
        win.className = 'window active';
        win.style.zIndex = (++this.zIndex).toString();
        win.style.width = '300px';
        win.style.height = '200px';

        if (item.id === 'chat') { win.style.width='450px'; win.style.height='350px'; }
        if (item.id === 'browser') { win.style.width='580px'; win.style.height='420px'; }

        win.style.left = `${30 + Math.random()*30}px`;
        win.style.top = `${30 + Math.random()*30}px`;

        win.innerHTML = `
            <div class="window-title">
                <div class="title-info"><span>${item.name}</span></div>
                <div class="title-controls"><button class="win-btn close-btn">X</button></div>
            </div>
            <div class="window-content">${this.getAppContent(item)}</div>
        `;

        win.addEventListener('mousedown', () => this.bringToFront(win));
        win.querySelector('.close-btn')?.addEventListener('click', () => win.remove());
        
        const titleBar = win.querySelector('.window-title') as HTMLElement;
        titleBar.addEventListener('mousedown', (e) => this.startDrag(e, win));
        this.windowsContainer.appendChild(win);

        if (item.id === 'calendar') {
           import('./kernel32').then(m => m.renderSystemTimer(win.querySelector('.calendar-container') as HTMLElement));
        }
        if (item.id === 'task_manager') {
           this.renderTaskManager(win.querySelector('.task-manager-container') as HTMLElement);
        }
        if (item.id === 'chat') {
            import('../apps/chat').then(m => m.initChatInteractions(win));
        }
    }

    private getAppContent(item: SystemItem): string {
        switch (item.id) {
            case 'chat': return renderChat();
            case 'browser': return renderBrowser();
            case 'calendar': return '<div class="calendar-container"></div>';
            case 'task_manager': return '<div class="task-manager-container"></div>';
            case 'terminal': return renderTerminal();
            default: return `<div>App: ${item.name}</div>`;
        }
    }

    private renderTaskManager(container: HTMLElement) {
        container.innerHTML = `<div style="padding:10px; color:#000;"><h3>Task Manager</h3><p>Progress: 10%</p></div>`;
    }

    private bringToFront(win: HTMLElement) {
        win.style.zIndex = (++this.zIndex).toString();
    }

    private startDrag(e: MouseEvent, win: HTMLElement) {
        this.activeWindow = win;
        const rect = win.getBoundingClientRect();
        const screenRect = this.screen.getBoundingClientRect();
        this.dragOffset.x = e.clientX - rect.left;
        this.dragOffset.y = e.clientY - rect.top;
        this.screenRect = screenRect;
        this.bringToFront(win);
        e.preventDefault();
    }

    private initGlobalEvents() {
        document.addEventListener('mousemove', (e) => {
            if (this.activeWindow && this.screenRect) {
                const x = e.clientX - this.screenRect.left - this.dragOffset.x;
                const y = e.clientY - this.screenRect.top - this.dragOffset.y;
                this.activeWindow.style.left = `${x}px`;
                this.activeWindow.style.top = `${y}px`;
            }
        });
        document.addEventListener('mouseup', () => { this.activeWindow = null; });
    }

    private updateClock() {
        const el = document.getElementById('clock');
        const tick = () => { if (el) el.innerText = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }); };
        tick(); setInterval(tick, 1000);
    }
}
