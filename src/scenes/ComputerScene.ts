import { BaseScene } from './BaseScene';
import { DedOS } from '../os/core/OS';

export class ComputerScene extends BaseScene {
    private os: DedOS | null = null;

    init(container: HTMLElement) {
        this.container = container;
        
        // Создаем структуру монитора, которая раньше была в index.html
        container.innerHTML = `
            <div id="monitor-wrapper">
                <div id="pc-screen"></div>
            </div>
        `;

        // Инициализируем DedOS
        this.os = new DedOS();
        console.log("Computer Scene Initialized");
    }

    destroy() {
        super.destroy();
        this.os = null;
    }
}
