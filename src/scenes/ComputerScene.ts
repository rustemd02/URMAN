import { BaseScene } from './BaseScene';
import { DedOS } from '../os/core/OS';

export class ComputerScene extends BaseScene {
    private os: DedOS | null = null;

    init(container: HTMLElement) {
        this.container = container;
        
        container.innerHTML = `
            <div id="monitor-wrapper">
                <div id="pc-screen"></div>
                
                <!-- Кнопка включения монитора -->
                <div class="monitor-power-btn" style="
                    position: absolute;
                    bottom: 12%;
                    right: 32%;
                    width: 30px;
                    height: 30px;
                    cursor: pointer;
                    z-index: 10001;
                    border-radius: 50%;
                "></div>

                <!-- SVG Маска для экрана -->
                <svg width="0" height="0" style="position:absolute;">
                    <defs>
                        <clipPath id="monitor-mask" clipPathUnits="objectBoundingBox">
                            <!-- Создаем форму с легким "вздутием" (старый ЭЛТ) -->
                            <path d="M0.02,0.02 
                                     Q0.5,-0.01 0.98,0.02 
                                     Q1.01,0.5 0.98,0.98 
                                     Q0.5,1.01 0.02,0.98 
                                     Q-0.01,0.5 0.02,0.02 Z" />
                        </clipPath>
                    </defs>
                </svg>
            </div>
        `;

        const powerBtn = container.querySelector('.monitor-power-btn');
        const screen = container.querySelector('#pc-screen') as HTMLElement;
        
        powerBtn?.addEventListener('mousedown', () => {
            (powerBtn as HTMLElement).style.transform = 'scale(0.95)';
        });
        
        powerBtn?.addEventListener('mouseup', () => {
            (powerBtn as HTMLElement).style.transform = 'scale(1)';
            
            // Проверяем текущее состояние через opacity, так как display может быть пустым
            const isOff = screen.style.opacity === '0';
            
            if (isOff) {
                screen.style.display = 'flex';
                setTimeout(() => {
                    screen.style.transition = 'opacity 0.5s ease-in';
                    screen.style.opacity = '1';
                }, 10);
            } else {
                screen.style.transition = 'opacity 0.2s ease-out';
                screen.style.opacity = '0';
                setTimeout(() => { 
                    if (screen.style.opacity === '0') screen.style.display = 'none'; 
                }, 200);
            }
        });

        // Инициализируем DedOS
        this.os = new DedOS();
        console.log("Computer Scene Initialized with SVG Mask and Power Button");
    }

    destroy() {
        super.destroy();
        this.os = null;
    }

    onOSMessage(title: string, message: string) {
        if (this.os) {
            this.os.showSystemMessage(title, message);
        }
    }
}
