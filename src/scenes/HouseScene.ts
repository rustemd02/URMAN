import { BaseScene } from './BaseScene';

export class HouseScene extends BaseScene {
    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `<div style="color: white; padding: 20px;">Сцена: Дом (в разработке)</div>`;
    }
}
