import { EventEmitter } from './EventEmitter';

export interface InventoryItem {
    id: string;
    name: string;
    icon: string;
}

export class GameState {
    private events: EventEmitter;
    
    // Состояние игрока
    public inventory: InventoryItem[] = [];
    public currentScene: string = 'intro';
    public flags: { [key: string]: boolean } = {};
    
    // Сюжетные переменные
    public day: number = 1;
    public time: string = '20:00';

    constructor(events: EventEmitter) {
        this.events = events;
    }

    setFlag(flag: string, value: boolean) {
        this.flags[flag] = value;
        this.events.emit('flag_changed', { flag, value });
    }

    addItem(item: InventoryItem) {
        this.inventory.push(item);
        this.events.emit('inventory_updated', this.inventory);
    }
}
