import { BaseScene } from './BaseScene';

type ForestPhase = 'presence' | 'voice' | 'temptation' | 'interruption' | 'end';

const MVP_ASSET_BASE = '/assets/urman_mvp_remaining/';
const ROUTE_ASSET_BASE = '/assets/urman_route_map/';

const FOREST_BACKGROUNDS: Record<ForestPhase, string> = {
    presence: `${ROUTE_ASSET_BASE}route_forest_approach_pressure_silence.png`,
    voice: `${ROUTE_ASSET_BASE}route_forest_approach_voice_lock.png`,
    temptation: `${ROUTE_ASSET_BASE}route_kara_urman_edge_voice_moment.png`,
    interruption: `${ROUTE_ASSET_BASE}route_kara_urman_edge_rinat_interruption.png`,
    end: `${ROUTE_ASSET_BASE}route_kara_urman_edge_rinat_interruption.png`,
};

export class ForestScene extends BaseScene {
    private phase: ForestPhase = 'presence';
    private timeouts = new Set<number>();
    private audioContext: AudioContext | null = null;
    private presenceOscillator: OscillatorNode | null = null;
    private presenceGain: GainNode | null = null;

    private readonly clickHandler = (event: MouseEvent) => this.handleClick(event);

    init(container: HTMLElement): void {
        this.container = container;
        container.addEventListener('click', this.clickHandler);
        this.game.state.setFlag('forest_cliffhanger_started', true);
        this.game.audio?.startAmbience('forest_presence');
        this.render();
    }

    destroy(): void {
        this.container?.removeEventListener('click', this.clickHandler);
        for (const timeout of this.timeouts) {
            window.clearTimeout(timeout);
        }
        this.timeouts.clear();
        this.stopPresenceTone();
        this.game.audio?.stopAmbience('forest_presence');
        super.destroy();
    }

    private handleClick(event: MouseEvent): void {
        const target = event.target;
        if (!(target instanceof HTMLElement)) return;

        const action = target.closest<HTMLElement>('[data-forest-action]')?.dataset.forestAction;
        if (!action) return;

        if (action === 'route' && this.phase !== 'temptation' && this.phase !== 'interruption' && this.phase !== 'end') {
            this.game.scenes.switchScene('village');
            return;
        }

        if (action === 'listen') {
            this.startPresenceTone();
            this.phase = 'voice';
            this.game.state.setFlag('forest_presence_heard', true);
            this.render();
            return;
        }

        if (action === 'step') {
            this.phase = 'temptation';
            this.game.state.setFlag('marat_voice_heard_at_forest_edge', true);
            this.game.audio?.playCue('marat_voice');
            this.render();
            return;
        }

        if (action === 'answer') {
            this.phase = 'interruption';
            this.game.state.setFlag('rinat_stops_aidar_answer', true);
            this.game.audio?.playCue('rinat_ne_otvechai');
            this.render();
            this.scheduleHardCut();
        }
    }

    private scheduleHardCut(): void {
        const timeout = window.setTimeout(() => {
            this.timeouts.delete(timeout);
            this.phase = 'end';
            this.game.state.setFlag('mvp_cliffhanger_complete', true);
            this.stopPresenceTone();
            this.game.audio?.startAmbience('street_silence', 0.5);
            this.render();
        }, 1800);
        this.timeouts.add(timeout);
    }

    private startPresenceTone(): void {
        if (this.audioContext) return;
        const AudioContextClass = window.AudioContext || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
        if (!AudioContextClass) return;

        this.audioContext = new AudioContextClass();
        const oscillator = this.audioContext.createOscillator();
        const gain = this.audioContext.createGain();
        oscillator.type = 'sine';
        oscillator.frequency.value = 72;
        gain.gain.value = 0.0001;
        oscillator.connect(gain);
        gain.connect(this.audioContext.destination);
        oscillator.start();
        gain.gain.exponentialRampToValueAtTime(0.035, this.audioContext.currentTime + 1.2);
        this.presenceOscillator = oscillator;
        this.presenceGain = gain;
    }

    private stopPresenceTone(): void {
        const context = this.audioContext;
        const oscillator = this.presenceOscillator;
        if (this.presenceGain && this.audioContext) {
            this.presenceGain.gain.setTargetAtTime(0.0001, this.audioContext.currentTime, 0.08);
        }
        if (oscillator) {
            window.setTimeout(() => {
                try {
                    oscillator.stop();
                } catch {
                    // The oscillator may already be stopped if the scene is destroyed twice.
                }
                void context?.close();
            }, 160);
        } else {
            void context?.close();
        }
        this.presenceOscillator = null;
        this.presenceGain = null;
        this.audioContext = null;
    }

    private render(): void {
        if (!this.container) return;

        const isEnd = this.phase === 'end';
        this.container.innerHTML = `
            ${this.renderStyles()}
            <main class="forest-scene ${this.phase}">
                <img class="forest-bg" src="${FOREST_BACKGROUNDS[this.phase]}" alt="Кромка Кара-Урмана">
                ${this.phase === 'interruption' ? `<img class="rinat-cutout" src="${MVP_ASSET_BASE}char_rinat_forest_edge_cutout_interruption.png" alt="Ринат у кромки леса">` : ''}
                <section class="forest-sound" aria-hidden="true">
                    <span></span><span></span><span></span><span></span>
                </section>
                ${isEnd ? this.renderEndState() : this.renderActiveState()}
            </main>
        `;
    }

    private renderActiveState(): string {
        return `
            <section class="forest-card">
                ${this.renderPhaseText()}
                <div class="forest-actions">
                    ${this.renderPhaseActions()}
                </div>
            </section>
        `;
    }

    private renderPhaseText(): string {
        if (this.phase === 'presence') {
            return `
                <span class="forest-kicker">Кара-Урман</span>
                <h1>Лес сначала звучит</h1>
                <p>Не шорохом и не голосом. Скорее паузой, которая становится слишком плотной, чтобы быть просто тишиной.</p>
            `;
        }

        if (this.phase === 'voice') {
            return `
                <span class="forest-kicker">голос из деревьев</span>
                <h1>«Айдар…»</h1>
                <p>Голос похож на Марата не полностью. Этого хватает, чтобы тело поверило раньше головы.</p>
            `;
        }

        if (this.phase === 'temptation') {
            return `
                <span class="forest-kicker">пауза</span>
                <h1>Ответ почти готов</h1>
                <p><b>Марат:</b> «Син ишетәсеңме?»</p>
                <p>Вопрос висит ровно там, где начинается лес. Кажется, достаточно сказать одно слово.</p>
            `;
        }

        return `
            <span class="forest-kicker">Ринат</span>
            <h1>«Не отвечай»</h1>
            <p>Ринат не объясняет. Он просто успевает раньше твоего голоса.</p>
        `;
    }

    private renderPhaseActions(): string {
        if (this.phase === 'presence') {
            return `
                <button data-forest-action="listen">Прислушаться</button>
                <button data-forest-action="route">Отступить к дороге</button>
            `;
        }

        if (this.phase === 'voice') {
            return `
                <button data-forest-action="step">Сделать шаг к голосу</button>
                <button data-forest-action="route">Вернуться, пока можно</button>
            `;
        }

        if (this.phase === 'temptation') {
            return `<button class="danger" data-forest-action="answer">Ответить</button>`;
        }

        return `<span class="forest-hard-cut-note">Экран обрывается.</span>`;
    }

    private renderEndState(): string {
        return `
            <section class="forest-end">
                <div class="forest-end-title">Конец MVP-среза</div>
                <p>Айдар понял главное: дело Марата не заканчивается деревенской ложью. У леса есть правила, и Ринат их знает.</p>
            </section>
        `;
    }

    private renderStyles(): string {
        return `
            <style>
                @keyframes forestPulse {
                    0%, 100% { transform: scaleY(0.35); opacity: 0.22; }
                    50% { transform: scaleY(1); opacity: 0.62; }
                }

                @keyframes forestCut {
                    from { opacity: 0; }
                    to { opacity: 1; }
                }

                .forest-scene,
                .forest-scene * {
                    box-sizing: border-box;
                }

                .forest-scene {
                    position: relative;
                    width: 100vw;
                    height: 100vh;
                    overflow: hidden;
                    color: #eee7d3;
                    background: #020302;
                    font-family: "Philosopher", "Cormorant Garamond", serif;
                }

                .forest-bg {
                    position: absolute;
                    inset: 0;
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                    filter: saturate(0.74) contrast(1.08) brightness(0.74);
                }

                .forest-scene::after {
                    content: "";
                    position: absolute;
                    inset: 0;
                    pointer-events: none;
                    background:
                        radial-gradient(circle at 50% 44%, transparent 25%, rgba(2, 3, 2, 0.66) 100%),
                        linear-gradient(180deg, rgba(2, 3, 2, 0.22), rgba(2, 3, 2, 0.72));
                }

                .temptation .forest-bg,
                .interruption .forest-bg {
                    filter: saturate(0.62) contrast(1.14) brightness(0.62);
                }

                .rinat-cutout {
                    position: absolute;
                    z-index: 8;
                    right: 10%;
                    bottom: 0;
                    width: min(30vw, 380px);
                    max-height: 78vh;
                    object-fit: contain;
                    filter: drop-shadow(0 22px 36px rgba(0, 0, 0, 0.56));
                }

                .forest-sound {
                    position: absolute;
                    z-index: 18;
                    left: 22px;
                    top: 22px;
                    display: flex;
                    align-items: center;
                    gap: 5px;
                    width: 76px;
                    height: 36px;
                    padding: 7px 9px;
                    border: 1px solid rgba(238, 231, 211, 0.18);
                    border-radius: 4px;
                    background: rgba(5, 7, 5, 0.46);
                    backdrop-filter: blur(8px);
                }

                .forest-sound span {
                    display: block;
                    width: 8px;
                    height: 100%;
                    transform-origin: center bottom;
                    background: rgba(217, 195, 140, 0.7);
                    animation: forestPulse 1.9s ease-in-out infinite;
                }

                .forest-sound span:nth-child(2) { animation-delay: 0.28s; }
                .forest-sound span:nth-child(3) { animation-delay: 0.56s; }
                .forest-sound span:nth-child(4) { animation-delay: 0.18s; }

                .presence .forest-sound {
                    opacity: 0.45;
                }

                .forest-card {
                    position: absolute;
                    z-index: 24;
                    left: 50%;
                    bottom: 40px;
                    width: min(640px, calc(100vw - 40px));
                    padding: 24px;
                    transform: translateX(-50%);
                    border: 1px solid rgba(238, 231, 211, 0.18);
                    border-radius: 6px;
                    background: rgba(9, 11, 8, 0.78);
                    backdrop-filter: blur(10px);
                    box-shadow: 0 24px 80px rgba(0, 0, 0, 0.58);
                }

                .forest-kicker {
                    display: block;
                    margin-bottom: 8px;
                    color: #d9c38c;
                    font-size: 12px;
                    letter-spacing: 0.12em;
                    text-transform: uppercase;
                }

                .forest-card h1 {
                    margin: 0 0 12px;
                    font-size: clamp(30px, 4vw, 48px);
                    line-height: 0.98;
                    font-weight: 600;
                }

                .forest-card p {
                    margin: 0 0 11px;
                    color: rgba(238, 231, 211, 0.82);
                    line-height: 1.5;
                }

                .forest-actions {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 10px;
                    margin-top: 17px;
                }

                .forest-actions button {
                    min-height: 40px;
                    padding: 0 15px;
                    border: 1px solid rgba(238, 231, 211, 0.28);
                    border-radius: 4px;
                    color: #eee7d3;
                    background: rgba(19, 22, 16, 0.78);
                    font: inherit;
                    cursor: pointer;
                    transition: background 160ms ease, border-color 160ms ease, transform 160ms ease;
                }

                .forest-actions button:hover {
                    border-color: rgba(217, 195, 140, 0.66);
                    background: rgba(51, 57, 35, 0.82);
                    transform: translateY(-1px);
                }

                .forest-actions button.danger {
                    border-color: rgba(200, 177, 126, 0.58);
                    background: rgba(47, 35, 22, 0.86);
                }

                .forest-hard-cut-note {
                    color: rgba(238, 231, 211, 0.58);
                    letter-spacing: 0.08em;
                    text-transform: uppercase;
                }

                .forest-end {
                    position: absolute;
                    inset: 0;
                    z-index: 40;
                    display: grid;
                    place-items: center;
                    padding: 24px;
                    text-align: center;
                    background: #000;
                    animation: forestCut 120ms linear forwards;
                }

                .forest-end-title {
                    font-size: clamp(30px, 5vw, 58px);
                    line-height: 1;
                    margin-bottom: 16px;
                }

                .forest-end p {
                    max-width: 620px;
                    margin: 0;
                    color: rgba(238, 231, 211, 0.7);
                    line-height: 1.55;
                }

                @media (max-width: 720px) {
                    .rinat-cutout {
                        right: -9%;
                        width: 58vw;
                        opacity: 0.62;
                    }

                    .forest-card {
                        bottom: 18px;
                        width: calc(100vw - 24px);
                        padding: 18px;
                    }

                    .forest-sound {
                        left: 12px;
                        top: 12px;
                    }
                }
            </style>
        `;
    }
}
