import { BaseScene } from './BaseScene';
import { DedOS, DedOSOptions } from '../os/core/OS';
import type { Game } from '../game/Game';
import type { OldPcHubController } from '../os/apps/oldPcHub';

export type DedOSFactory = (options: DedOSOptions) => DedOS;

/**
 * Injection seam for the composition root. MM-54 supplies the started
 * RuntimeContext-backed archive capability; this scene only owns the web shell.
 */
export interface ComputerSceneOptions {
    readonly oldPcHub?: OldPcHubController;
    readonly createDedOS?: DedOSFactory;
}

export class ComputerScene extends BaseScene {
    private os: DedOS | null = null;
    private readonly oldPcHub?: OldPcHubController;
    private readonly createDedOS: DedOSFactory;
    private powerButton: HTMLElement | null = null;
    private powerDownListener: ((event: MouseEvent) => void) | null = null;
    private powerUpListener: ((event: MouseEvent) => void) | null = null;
    private readonly powerTimers = new Set<number>();

    constructor(game: Game, options: ComputerSceneOptions = {}) {
        super(game);
        this.oldPcHub = options.oldPcHub;
        this.createDedOS = options.createDedOS ?? ((osOptions) => new DedOS(osOptions));
    }

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

        const powerBtn = container.querySelector('.monitor-power-btn') as HTMLElement | null;
        const screen = container.querySelector('#pc-screen') as HTMLElement;

        this.powerButton = powerBtn;
        this.powerDownListener = () => {
            powerBtn?.style.setProperty('transform', 'scale(0.95)');
        };
        this.powerUpListener = () => {
            powerBtn?.style.setProperty('transform', 'scale(1)');
            
            // Проверяем текущее состояние через opacity, так как display может быть пустым
            const isOff = screen.style.opacity === '0';
            
            if (isOff) {
                screen.style.display = 'flex';
                this.powerTimers.add(window.setTimeout(() => {
                    screen.style.transition = 'opacity 0.5s ease-in';
                    screen.style.opacity = '1';
                }, 10));
            } else {
                screen.style.transition = 'opacity 0.2s ease-out';
                screen.style.opacity = '0';
                this.powerTimers.add(window.setTimeout(() => {
                    if (screen.style.opacity === '0') screen.style.display = 'none'; 
                }, 200));
            }
        };
        powerBtn?.addEventListener('mousedown', this.powerDownListener);
        powerBtn?.addEventListener('mouseup', this.powerUpListener);

        this.os = this.createDedOS({ oldPcHub: this.oldPcHub });
        this.game.audio?.startAmbience('old_pc_hum');
        console.log("Computer Scene Initialized with SVG Mask and Power Button");
    }

    destroy() {
        this.game.audio?.stopAmbience('old_pc_hum');
        if (this.powerButton && this.powerDownListener) this.powerButton.removeEventListener('mousedown', this.powerDownListener);
        if (this.powerButton && this.powerUpListener) this.powerButton.removeEventListener('mouseup', this.powerUpListener);
        for (const timer of this.powerTimers) window.clearTimeout(timer);
        this.powerTimers.clear();
        this.powerButton = null;
        this.powerDownListener = null;
        this.powerUpListener = null;
        this.os?.destroy();
        super.destroy();
        this.os = null;
    }

    onOSMessage(title: string, message: string) {
        if (this.os) {
            this.os.showSystemMessage(title, message);
        }
    }
}
