import { SYSTEM_REGISTRY, SystemItem } from './registry';
// Проверь, чтобы эти импорты совпадали с названиями файлов
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
        if (!el) throw new Error("Элемент #pc-screen не найден! Проверь index.html");
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
        this.handleResize(); // Начальное масштабирование
        window.addEventListener('resize', () => this.handleResize());
    }

    private handleResize() {
        const wrapper = this.screen.querySelector('.screen-content-wrapper') as HTMLElement;
        if (!wrapper) return;

        const screenW = this.screen.clientWidth;
        const screenH = this.screen.clientHeight;
        
        // Масштабируем 640x480 под реальный размер #pc-screen
        const scale = Math.min(screenW / 640, screenH / 480);
        wrapper.style.transform = `translate(-50%, -50%) scale(${scale})`;
    }

    private applyMonitorStyles() {
        const wrapper = document.getElementById('monitor-wrapper');
        if (wrapper) {
            // Исправлено расширение на .png, так как файл в ассетах - monitor-frame.png
            wrapper.style.backgroundImage = "url('/assets/monitor-frame.png')";
            wrapper.style.backgroundSize = "contain";
            wrapper.style.backgroundRepeat = "no-repeat";
            wrapper.style.backgroundPosition = "center";
        }
    }

    private createLayers() {
        this.screen.innerHTML = '';
        
        // Обертка для контента с фильтрами
        const wrapper = document.createElement('div');
        wrapper.className = 'screen-content-wrapper';
        this.screen.appendChild(wrapper);

        // Слой иконок
        const grid = document.createElement('div');
        grid.className = 'desktop-grid';
        wrapper.appendChild(grid);

        // Слой окон
        this.windowsContainer = document.createElement('div');
        this.windowsContainer.id = 'windows-container';
        wrapper.appendChild(this.windowsContainer);
    }

    private renderDesktop() {
        const grid = this.screen.querySelector('.desktop-grid');
        if (!grid) return;

        const desktopItems = SYSTEM_REGISTRY.filter(item => item.parentId === null);
        
        grid.innerHTML = desktopItems.map(item => `
            <div class="desktop-icon" data-id="${item.id}" title="${item.id === 'minesweeper' ? 'Размер: 1552 КБ' : ''}">
                <div class="icon-img">${item.icon}</div>
                <div class="icon-text">${item.name}</div>
            </div>
        `).join('');

        // Добавляем события клика
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
            <button class="start-btn">
                <span style="font-size: 12px; margin-right: 2px;">💻</span>
                Җибәр
            </button>
            <div class="taskbar-apps"></div>
            <button class="win98-btn fs-btn" style="height:16px; padding:0 4px; margin-right:4px; font-size:8px;">[ ]</button>
            <div id="clock">00:00</div>
        `;
        if (wrapper) wrapper.appendChild(bar);
        else this.screen.appendChild(bar);

        bar.querySelector('.fs-btn')?.addEventListener('click', () => {
            if (!document.fullscreenElement) {
                document.documentElement.requestFullscreen();
            } else {
                if (document.exitFullscreen) {
                    document.exitFullscreen();
                }
            }
        });
    }

    public openWindow(item: SystemItem) {
        const existing = document.getElementById(`win-${item.id}`);
        if (existing) {
            this.bringToFront(existing);
            return;
        }

        const win = document.createElement('div');
        win.id = `win-${item.id}`;
        win.className = 'window active';
        
        win.style.zIndex = (++this.zIndex).toString();
        
        // Изначально удобные окна для разрешения 640x480
        win.style.width = '300px';
        win.style.height = '220px';
        
        if (item.id === 'chat') {
            win.style.width = '450px';
            win.style.height = '350px';
        } else if (item.id === 'browser') {
            win.style.width = '580px';
            win.style.height = '420px';
        } else if (item.id === 'minesweeper') {
            win.style.width = '240px';
            win.style.height = '380px';
        }

        // Позиционирование
        const startX = 30 + Math.random() * 30;
        const startY = 30 + Math.random() * 30;
        win.style.left = `${startX}px`;
        win.style.top = `${startY}px`;
        
        win.innerHTML = `
            <div class="window-title">
                <div class="title-info">
                    <span class="title-icon">${item.icon}</span>
                    <span class="title-text">${item.name}</span>
                </div>
                <div class="title-controls">
                    <button class="win-btn">_</button>
                    <button class="win-btn">□</button>
                    <button class="win-btn close-btn">X</button>
                </div>
            </div>
            <div class="window-content">
                ${this.getAppContent(item)}
            </div>
            <div class="resizer"></div>
        `;

        win.addEventListener('mousedown', () => this.bringToFront(win));
        
        win.querySelector('.close-btn')?.addEventListener('mousedown', (e) => {
            e.stopPropagation(); // Предотвращаем drag при клике на закрытие
        });
        
        win.querySelector('.close-btn')?.addEventListener('click', (e) => {
            e.stopPropagation();
            win.remove();
        });

        const titleBar = win.querySelector('.window-title') as HTMLElement;
        titleBar.addEventListener('mousedown', (e) => this.startDrag(e, win));

        const resizer = win.querySelector('.resizer') as HTMLElement;
        resizer.addEventListener('mousedown', (e) => this.startResize(e, win));

        this.windowsContainer.appendChild(win);

        // Инициализация специфичных для приложения взаимодействий
        if (item.id === 'chat') {
            import('../apps/chat').then(m => m.initChatInteractions(win));
        }
        if (item.id === 'minesweeper') {
            import('../apps/minesweeper').then(m => m.initMinesweeper(win));
        }
        if (item.id === 'browser') {
            import('../apps/browser').then(m => m.initBrowser(win));
        }
        if (item.id === 'notepad' || item.type === 'doc') {
            import('../apps/notepad').then(m => m.initNotepad(win, item.type === 'doc' ? (item.content as string) : undefined));
        }
    }

    private getAppContent(item: SystemItem): string {
        try {
            switch (item.id) {
                case 'chat': return renderChat();
                case 'browser': return renderBrowser();
                case 'minesweeper': return renderMinesweeper ? renderMinesweeper() : "Ошибка модуля";
                case 'notepad': return renderNotepad ? renderNotepad() : "Ошибка модуля";
                case 'player': return renderPlayer ? renderPlayer() : "Ошибка модуля";
                case 'terminal': return renderTerminal ? renderTerminal() : "Ошибка модуля";
                default: 
                    if (item.type === 'folder') return this.renderFolder(item);
                    if (item.type === 'doc') return renderWord(item.content as string);
                    if (item.type === 'image') return `<div style="background:#000; height:100%; display:flex; align-items:center; justify-content:center;"><img src="${item.content}" style="max-width:100%; max-height:100%; object-fit:contain;"></div>`;
                    return `<div style="padding: 20px;">Система хатасы: ${item.name}</div>`;
            }
        } catch (e) {
            console.error("Ошибка рендера приложения:", e);
            return `<div style="padding:20px; color:red;">Ошибка загрузки ${item.name}</div>`;
        }
    }

    private renderFolder(folder: SystemItem): string {
        const children = SYSTEM_REGISTRY.filter(i => (folder.content as string[]).includes(i.id));
        return `
            <div class="folder-view" style="display: grid; grid-template-columns: repeat(auto-fill, 70px); gap: 10px; padding: 10px; background: #fff; height: 100%;">
                ${children.map(child => `
                    <div class="folder-icon" data-id="${child.id}" style="display:flex; flex-direction:column; align-items:center; cursor:pointer;">
                        <div style="font-size: 24px;">${child.icon}</div>
                        <div style="font-size: 10px; color: #000; text-align:center;">${child.name}</div>
                    </div>
                `).join('')}
            </div>
        `;
    }

    private bringToFront(win: HTMLElement) {
        this.zIndex++;
        win.style.zIndex = this.zIndex.toString();
        document.querySelectorAll('.window').forEach(w => w.classList.remove('active'));
        win.classList.add('active');
    }

    private startDrag(e: MouseEvent, win: HTMLElement) {
        this.activeWindow = win;
        this.screenRect = this.screen.getBoundingClientRect();
        const winRect = win.getBoundingClientRect();
        
        this.dragOffset.x = e.clientX - winRect.left;
        this.dragOffset.y = e.clientY - winRect.top;
        
        this.bringToFront(win);
        e.preventDefault();
    }

    private startResize(e: MouseEvent, win: HTMLElement) {
        this.resizingWindow = win;
        this.resizeStart = {
            w: win.offsetWidth,
            h: win.offsetHeight,
            x: e.clientX,
            y: e.clientY
        };
        this.bringToFront(win);
        e.preventDefault();
        e.stopPropagation();
    }

    private initGlobalEvents() {
        document.addEventListener('mousemove', (e) => {
            if (this.activeWindow && this.screenRect) {
                let x = e.clientX - this.screenRect.left - this.dragOffset.x;
                let y = e.clientY - this.screenRect.top - this.dragOffset.y;

                const maxX = this.screen.clientWidth - this.activeWindow.offsetWidth;
                const maxY = this.screen.clientHeight - 28;

                x = Math.max(0, Math.min(x, maxX));
                y = Math.max(0, Math.min(y, maxY));

                this.activeWindow.style.left = `${x}px`;
                this.activeWindow.style.top = `${y}px`;
            }

            if (this.resizingWindow) {
                const dw = e.clientX - this.resizeStart.x;
                const dh = e.clientY - this.resizeStart.y;
                
                const newW = Math.max(200, this.resizeStart.w + dw);
                const newH = Math.max(150, this.resizeStart.h + dh);
                
                // Ограничение по размеру экрана
                const maxWidth = this.screen.clientWidth - this.resizingWindow.offsetLeft;
                const maxHeight = this.screen.clientHeight - this.resizingWindow.offsetTop - 28;

                this.resizingWindow.style.width = `${Math.min(newW, maxWidth)}px`;
                this.resizingWindow.style.height = `${Math.min(newH, maxHeight)}px`;
            }
        });

        document.addEventListener('mouseup', () => {
            this.activeWindow = null;
            this.resizingWindow = null;
            this.screenRect = null;
        });

        // Глобальный клик для папок
        this.screen.addEventListener('click', (e) => {
            const folderIcon = (e.target as HTMLElement).closest('.folder-icon');
            if (folderIcon) {
                const id = folderIcon.getAttribute('data-id');
                const item = SYSTEM_REGISTRY.find(i => i.id === id);
                if (item) this.openWindow(item);
            }
        });

        (window as any).game = {
            openWindowById: (id: string) => {
                const item = SYSTEM_REGISTRY.find(i => i.id === id);
                if (item) this.openWindow(item);
            }
        };
    }

    private updateClock() {
        const el = document.getElementById('clock');
        const tick = () => { 
            if(el) el.innerText = new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'}); 
        };
        tick(); 
        setInterval(tick, 1000);
    }
}