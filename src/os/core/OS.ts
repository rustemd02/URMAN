import { DedOsAppRegistry } from '../../runtime/modules/oldpc/dedos-app-registry.mjs';
import { initBrowser, renderBrowser } from '../apps/browser';
import { initMinesweeper, renderMinesweeper } from '../apps/minesweeper';
import { initNotepad, renderNotepad } from '../apps/notepad';
import { renderPlayer } from '../apps/player';
import { renderTerminal } from '../apps/terminal';
import { renderWord } from '../apps/word';
import { OldPcHubController } from '../apps/oldPcHub';
import { SystemTimer } from './kernel32';
import { SYSTEM_REGISTRY, SystemItem } from './registry';

type AppInstance = { render(): string; init?(root: HTMLElement): void | (() => void) };

export interface DedOSOptions {
    readonly appRegistry?: DedOsAppRegistry;
    readonly oldPcHub?: OldPcHubController;
    readonly onExit?: () => void;
    readonly monitorFrameUrl?: string;
}

/** A development guard only. MM-54 must inject the capability before production cutover. */
function missingOldPcCapabilityGuard(): AppInstance {
    return { render: () => '<div data-oldpc-development-guard="missing-capability" style="padding:12px">Архивный capability-модуль ожидает composition root.</div>' };
}

function createDefaultAppRegistry(options: DedOSOptions): DedOsAppRegistry {
    const archive = options.oldPcHub
        ? { render: () => renderBrowser(), init: (root: HTMLElement) => initBrowser(root, options.oldPcHub!) }
        : missingOldPcCapabilityGuard();
    return new DedOsAppRegistry([
        { appId: 'urman.oldpc:app/archive_search', create: () => archive },
        { appId: 'urman.oldpc:app/notepad', create: () => ({ render: () => renderNotepad('Айдар, если найдёшь странную отметку — сохраняй как улику.'), init: (root: HTMLElement) => initNotepad(root, 'Айдар, если найдёшь странную отметку — сохраняй как улику.') }) },
        { appId: 'urman.oldpc:app/player', create: () => ({ render: () => renderPlayer() }) },
        { appId: 'urman.oldpc:app/terminal', create: () => ({ render: () => renderTerminal() }) },
        { appId: 'urman.oldpc:app/calendar', create: () => ({ render: () => '<div class="calendar-container"></div>', init: (root: HTMLElement) => new SystemTimer().render(root.querySelector('.calendar-container') as HTMLElement) }) },
        { appId: 'urman.oldpc:app/task-manager', create: () => ({ render: () => `<div class="task-manager-container" style="padding:10px;color:#000;font-family:Tahoma,sans-serif"><h3>Состояние системы</h3><p>Архив: подключается через registry</p><p>Контент-разделов: ${SYSTEM_REGISTRY.filter((item) => item.launchSection).length}</p></div>` }) },
        { appId: 'urman.oldpc:app/exit', create: () => ({ render: () => '', init: () => options.onExit?.() }) },
        { appId: 'urman.oldpc:app/trash', create: () => ({ render: () => renderWord('Корзина пуста.\n\nКроме одного ярлыка с подписью: «Не удалять старые реестры».') }) },
        { appId: 'urman.oldpc:app/minesweeper', create: () => ({ render: () => renderMinesweeper(), init: (root: HTMLElement) => initMinesweeper(root) }) },
    ]);
}

/** Web shell only: every app is an exact descriptor, while content/progression stays in runtime modules. */
export class DedOS {
    private readonly screen: HTMLElement;
    private readonly appRegistry: DedOsAppRegistry;
    private readonly monitorFrameUrl?: string;
    private windowsContainer!: HTMLElement;
    private zIndex = 100;
    private activeWindow: HTMLElement | null = null;
    private dragOffset = { x: 0, y: 0 };
    private screenRect: DOMRect | null = null;
    private readonly appCleanups = new Map<HTMLElement, () => void>();
    private clockTimer: number | null = null;
    private bootTimer: number | null = null;
    private destroyed = false;

    private readonly resizeListener = () => this.handleResize();
    private readonly moveListener = (event: MouseEvent) => this.handleMouseMove(event);
    private readonly upListener = () => { this.activeWindow = null; };

    constructor(options: DedOSOptions = {}) {
        const screen = document.getElementById('pc-screen');
        if (!screen) throw new Error('PC screen element not found.');
        this.screen = screen;
        this.appRegistry = options.appRegistry ?? createDefaultAppRegistry(options);
        this.monitorFrameUrl = options.monitorFrameUrl;
        this.initOS();
    }

    private initOS() {
        this.applyMonitorStyles();
        this.createLayers();
        this.renderDesktop();
        this.renderTaskbar();
        window.addEventListener('resize', this.resizeListener);
        document.addEventListener('mousemove', this.moveListener);
        document.addEventListener('mouseup', this.upListener);
        this.updateClock();
        this.handleResize();
        this.showBootScreen();
    }

    private showBootScreen() {
        const bootOverlay = document.createElement('div');
        bootOverlay.style.cssText = 'position:absolute;inset:0;background:#000;z-index:100000;display:flex;flex-direction:column;align-items:center;justify-content:center;color:#c0c0c0;font-family:Tahoma,sans-serif;';
        bootOverlay.innerHTML = '<div style="font-size:28px;font-weight:bold">Старый компьютер</div><div style="font-size:11px;margin-top:10px;color:#7fd3d0">Локальный архив Кырлая загружается...</div>';
        this.screen.appendChild(bootOverlay);
        this.bootTimer = window.setTimeout(() => bootOverlay.remove(), 1000);
    }

    private applyMonitorStyles() {
        const wrapper = document.getElementById('monitor-wrapper');
        if (wrapper && this.monitorFrameUrl) wrapper.style.backgroundImage = `url(${this.monitorFrameUrl})`;
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
        const desktopItems = SYSTEM_REGISTRY.filter((item) => item.parentId === null);
        grid.innerHTML = desktopItems.map((item) => `<div class="desktop-icon" data-id="${item.id}"><div class="icon-img-container">${item.icon}</div><div class="icon-text">${item.name}</div></div>`).join('');
        grid.querySelectorAll<HTMLElement>('.desktop-icon').forEach((element) => {
            element.addEventListener('dblclick', () => {
                const item = SYSTEM_REGISTRY.find((candidate) => candidate.id === element.dataset.id);
                if (item) this.openWindow(item);
            });
        });
    }

    private renderTaskbar() {
        const wrapper = this.screen.querySelector('.screen-content-wrapper');
        if (!wrapper) return;
        const bar = document.createElement('div');
        bar.id = 'taskbar';
        bar.innerHTML = '<button class="start-btn"><b>Пуск</b></button><div class="taskbar-apps"></div><div id="clock">00:00</div>';
        wrapper.appendChild(bar);
    }

    public openWindow(item: SystemItem, appOverride?: AppInstance) {
        if (this.destroyed) return;
        const existing = this.windowsContainer.querySelector<HTMLElement>(`.window[data-launcher-id="${CSS.escape(item.id)}"]`);
        if (existing) {
            this.bringToFront(existing);
            return;
        }
        const app = appOverride ?? this.appRegistry.create(item.appId, { item });
        const windowElement = document.createElement('div');
        windowElement.className = 'window active';
        windowElement.dataset.launcherId = item.id;
        windowElement.style.zIndex = String(++this.zIndex);
        windowElement.style.width = item.launchSection ? '600px' : '360px';
        windowElement.style.height = item.launchSection ? '430px' : '260px';
        windowElement.style.left = `${30 + (this.zIndex % 6) * 10}px`;
        windowElement.style.top = `${30 + (this.zIndex % 5) * 10}px`;
        windowElement.innerHTML = `<div class="window-title"><div class="title-info"><span>${item.name}</span></div><div class="title-controls"><button class="win-btn min-btn">_</button><button class="win-btn close-btn">X</button></div></div><div class="window-content">${app.render()}</div><div class="resizer" aria-hidden="true"></div>`;
        const close = () => this.closeWindow(windowElement);
        windowElement.addEventListener('mousedown', () => this.bringToFront(windowElement));
        windowElement.querySelector('.close-btn')?.addEventListener('click', close);
        windowElement.querySelector('.min-btn')?.addEventListener('click', () => {
            windowElement.classList.toggle('minimized');
            this.updateTaskbar();
        });
        const titleBar = windowElement.querySelector('.window-title') as HTMLElement;
        titleBar.addEventListener('mousedown', (event) => this.startDrag(event, windowElement));
        this.windowsContainer.appendChild(windowElement);
        const cleanup = app.init?.(windowElement);
        if (typeof cleanup === 'function') this.appCleanups.set(windowElement, cleanup);
        this.updateTaskbar();
    }

    private closeWindow(windowElement: HTMLElement) {
        const cleanup = this.appCleanups.get(windowElement);
        this.appCleanups.delete(windowElement);
        try { cleanup?.(); } finally { windowElement.remove(); this.updateTaskbar(); }
    }

    private handleResize() {
        const wrapper = this.screen.querySelector('.screen-content-wrapper') as HTMLElement | null;
        if (!wrapper) return;
        const scale = Math.min(this.screen.clientWidth / 640, this.screen.clientHeight / 480);
        wrapper.style.transform = `translate(-50%, -50%) scale(${scale})`;
    }

    private startDrag(event: MouseEvent, windowElement: HTMLElement) {
        this.activeWindow = windowElement;
        const rect = windowElement.getBoundingClientRect();
        this.screenRect = this.screen.getBoundingClientRect();
        this.dragOffset = { x: event.clientX - rect.left, y: event.clientY - rect.top };
        this.bringToFront(windowElement);
        event.preventDefault();
    }

    private handleMouseMove(event: MouseEvent) {
        if (!this.activeWindow || !this.screenRect) return;
        const x = event.clientX - this.screenRect.left - this.dragOffset.x;
        const y = event.clientY - this.screenRect.top - this.dragOffset.y;
        this.activeWindow.style.left = `${Math.max(0, Math.min(620, x))}px`;
        this.activeWindow.style.top = `${Math.max(0, Math.min(450, y))}px`;
    }

    private updateClock() {
        const clock = this.screen.querySelector('#clock');
        const tick = () => { if (clock) clock.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }); };
        tick();
        this.clockTimer = window.setInterval(tick, 1000);
    }

    private bringToFront(windowElement: HTMLElement) {
        this.windowsContainer.querySelectorAll('.window').forEach((entry) => entry.classList.remove('active'));
        windowElement.classList.add('active');
        windowElement.style.zIndex = String(++this.zIndex);
        this.updateTaskbar();
    }

    private updateTaskbar() {
        const apps = this.screen.querySelector('.taskbar-apps');
        if (!apps) return;
        const windows = [...this.windowsContainer.querySelectorAll<HTMLElement>('.window')];
        apps.innerHTML = windows.map((windowElement) => `<button class="taskbar-item ${windowElement.classList.contains('active') ? 'active' : ''}" data-window-id="${windowElement.dataset.launcherId}">${windowElement.querySelector('.title-info span')?.textContent ?? ''}</button>`).join('');
        apps.querySelectorAll<HTMLButtonElement>('.taskbar-item').forEach((button) => {
            button.addEventListener('click', () => {
                const windowElement = windows.find((entry) => entry.dataset.launcherId === button.dataset.windowId);
                if (!windowElement) return;
                windowElement.classList.remove('minimized');
                this.bringToFront(windowElement);
            });
        });
    }

    public showSystemMessage(title: string, message: string) {
        const item: SystemItem = {
            id: `urman.oldpc:launcher/message-${this.zIndex + 1}`,
            appId: 'urman.oldpc:app/trash',
            name: title,
            type: 'app',
            icon: '!',
            parentId: null,
            content: message,
        };
        this.openWindow(item, { render: () => renderWord(message) });
    }

    public destroy() {
        if (this.destroyed) return;
        this.destroyed = true;
        if (this.clockTimer !== null) window.clearInterval(this.clockTimer);
        if (this.bootTimer !== null) window.clearTimeout(this.bootTimer);
        window.removeEventListener('resize', this.resizeListener);
        document.removeEventListener('mousemove', this.moveListener);
        document.removeEventListener('mouseup', this.upListener);
        for (const windowElement of [...this.appCleanups.keys()]) this.closeWindow(windowElement);
        this.screen.innerHTML = '';
    }
}
