import { Game } from '../game/Game';
import { InventoryItem } from '../game/GameState';
import { NotebookUI } from './NotebookUI';

export class InventoryUI {
    private game: Game;
    private element: HTMLElement;
    private itemsList: HTMLElement;
    private notebook: NotebookUI;
    private isOpen = false;

    constructor(game: Game) {
        this.game = game;
        this.notebook = new NotebookUI(game);

        // Backpack button (Inventory trigger)
        this.element = document.createElement('div');
        this.element.id = 'inventory-toggle';
        this.element.style.cssText = `
            position: fixed;
            bottom: 30px;
            right: 30px;
            z-index: 10003;
            width: 60px;
            height: 60px;
            background: rgba(255,255,255,0.1);
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255,255,255,0.3);
            border-radius: 50%;
            cursor: pointer;
            box-shadow: 0 10px 25px rgba(0,0,0,0.4);
            transition: all 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 30px;
            user-select: none;
            pointer-events: auto;
        `;
        this.element.innerHTML = '🎒';

        // Items list (Inventory content)
        this.itemsList = document.createElement('div');
        this.itemsList.id = 'inventory-list';
        this.itemsList.style.cssText = `
            position: fixed;
            bottom: 110px;
            right: 30px;
            z-index: 10003;
            display: flex;
            flex-direction: column;
            gap: 15px;
            pointer-events: none;
            opacity: 0;
            transform: translateY(20px);
            transition: all 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
        `;

        document.body.appendChild(this.element);
        document.body.appendChild(this.itemsList);

        this.initEvents();
        this.update();
    }

    private initEvents() {
        this.element.addEventListener('click', () => this.toggle());
        this.game.events.on('inventory_updated', () => this.update());
        
        // Listen for new word discovery to show a little dot or wiggle
        this.game.events.on('word_discovered', () => {
            this.element.style.transform = 'scale(1.2) rotate(15deg)';
            setTimeout(() => {
                this.element.style.transform = '';
            }, 500);
        });
    }

    public toggle() {
        this.setVisible(!this.isOpen);
    }

    public setVisible(show: boolean) {
        this.isOpen = show;
        // Button always stays visible; only the items list toggles
        if (show) {
            this.itemsList.style.display = 'flex';
            this.itemsList.style.pointerEvents = 'auto';
            requestAnimationFrame(() => {
                this.itemsList.style.opacity = '1';
                this.itemsList.style.transform = 'translateY(0)';
            });
        } else {
            this.itemsList.style.opacity = '0';
            this.itemsList.style.transform = 'translateY(20px)';
            this.itemsList.style.pointerEvents = 'none';
            setTimeout(() => { this.itemsList.style.display = 'none'; }, 300);
            this.notebook.close();
        }
    }

    private update() {
        const items = this.game.state.inventory;
        this.itemsList.innerHTML = '';

        items.forEach(item => {
            const el = document.createElement('div');
            el.className = 'inventory-item';
            el.style.cssText = `
                width: 50px;
                height: 50px;
                background: rgba(255,255,255,0.2);
                backdrop-filter: blur(10px);
                border: 1px solid rgba(255,255,255,0.4);
                border-radius: 12px;
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 24px;
                cursor: pointer;
                transition: all 0.2s;
                position: relative;
            `;
            el.innerHTML = item.icon;
            el.title = item.name;

            el.addEventListener('mouseover', () => {
                el.style.background = 'rgba(255,255,255,0.4)';
                el.style.transform = 'scale(1.1)';
            });
            el.addEventListener('mouseout', () => {
                el.style.background = 'rgba(255,255,255,0.2)';
                el.style.transform = 'scale(1)';
            });

            el.addEventListener('click', () => {
                this.handleItemClick(item);
                this.toggle();
            });

            this.itemsList.appendChild(el);
        });
    }

    private handleItemClick(item: InventoryItem) {
        if (item.id === 'notepad') {
            this.notebook.open();
        }
    }
}
