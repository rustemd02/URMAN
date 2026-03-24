import { BaseScene } from './BaseScene';

export class ForestScene extends BaseScene {
    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `<div style="color: white; padding: 20px;">Сцена: Лес (в разработке)</div>`;
    }
}
