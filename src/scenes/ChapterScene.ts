import { BaseScene } from './BaseScene';

export class ChapterScene extends BaseScene {
    init(container: HTMLElement) {
        this.container = container;
        
        container.innerHTML = `
            <style>
                @keyframes fade-in-out {
                    0% { opacity: 0; }
                    20% { opacity: 1; }
                    80% { opacity: 1; }
                    100% { opacity: 0; }
                }
                .chapter-bg {
                    width: 100vw;
                    height: 100vh;
                    background: #000;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    font-family: 'Verdana', sans-serif;
                    color: white;
                    text-align: center;
                }
                .chapter-content {
                    animation: fade-in-out 5s forwards;
                }
                .chapter-number {
                    font-size: 18px;
                    letter-spacing: 5px;
                    color: #888;
                    margin-bottom: 20px;
                    text-transform: uppercase;
                }
                .chapter-title {
                    font-size: 40px;
                    letter-spacing: 2px;
                    font-weight: bold;
                }
            </style>
            
            <div class="chapter-bg">
                <div class="chapter-content">
                    <div class="chapter-number">Глава 1</div>
                    <div class="chapter-title">Пакт с Шурале</div>
                </div>
            </div>
        `;

        // Wait 5 seconds to finish the animation, then switch to the actual game
        setTimeout(() => {
            this.game.scenes.switchScene('intro');
        }, 5000);
    }
}
