import { BaseScene } from './BaseScene';

/**
 * Forest Scene - The dark woods surrounding the village.
 * (Placeholder Skeleton)
 */
export class ForestScene extends BaseScene {
    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
            <div style="background:#050a05; width:100%; height:100%; display:flex; align-items:center; justify-content:center; color:#2d5a1e; font-family:serif;">
                <div style="text-align:center;">
                    <h1 style="letter-spacing:10px; opacity:0.8;">УРМАН</h1>
                    <p style="font-size:12px; opacity:0.5;">Шумиха леса... Здесь легко потеряться.</p>
                    <button id="back-btn" style="margin-top:20px; background:transparent; border:1px solid #2d5a1e; color:#2d5a1e; cursor:pointer; padding:5px 15px;">Вернуться в деревню</button>
                </div>
            </div>
        `;

        container.querySelector('#back-btn')?.addEventListener('click', () => {
            this.game.scenes.switchScene('village');
        });
    }

    destroy() {
        super.destroy();
    }
}
