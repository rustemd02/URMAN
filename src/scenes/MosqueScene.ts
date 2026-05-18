import { BaseScene } from './BaseScene';

type MosqueView = 'exterior' | 'office';

const ASSET_BASE = '/assets/urman_mvp_remaining/';

const MOSQUE_ASSETS: Record<MosqueView, string> = {
    exterior: `${ASSET_BASE}loc_mosque_exterior_day_safe_light.png`,
    office: `${ASSET_BASE}loc_mosque_tea_office_timur_calm.png`,
};

export class MosqueScene extends BaseScene {
    private view: MosqueView = 'exterior';
    private heardTimur = false;

    private readonly clickHandler = (event: MouseEvent) => this.handleClick(event);

    init(container: HTMLElement): void {
        this.container = container;
        this.heardTimur = Boolean(this.game.state.flags.timur_first_safe_talk);
        container.addEventListener('click', this.clickHandler);
        this.game.state.setFlag('mosque_safe_zone_seen', true);
        this.game.audio?.startAmbience('mosque_calm');
        this.render();
    }

    destroy(): void {
        this.container?.removeEventListener('click', this.clickHandler);
        this.game.audio?.stopAmbience('mosque_calm');
        super.destroy();
    }

    private handleClick(event: MouseEvent): void {
        const target = event.target;
        if (!(target instanceof HTMLElement)) return;

        const action = target.closest<HTMLElement>('[data-mosque-action]')?.dataset.mosqueAction;
        if (!action) return;

        if (action === 'exterior' || action === 'office') {
            this.view = action;
            this.render();
            return;
        }

        if (action === 'timur') {
            this.heardTimur = true;
            this.game.state.setFlag('timur_first_safe_talk', true);
            this.game.state.setFlag('timur_warns_do_not_answer_unknown_voice', true);
            this.render();
            return;
        }

        if (action === 'route') {
            if (this.game.state.routeCurrentNodeId === 'arrival_vehicle_dusk') {
                this.game.state.routeCurrentNodeId = 'mosque_sign_day';
            }
            this.game.scenes.switchScene('village');
        }
    }

    private render(): void {
        if (!this.container) return;

        const isExterior = this.view === 'exterior';
        this.container.innerHTML = `
            ${this.renderStyles()}
            <main class="mosque-scene ${this.view}">
                <img class="mosque-bg" src="${MOSQUE_ASSETS[this.view]}" alt="${isExterior ? 'Мечеть Кырлая в спокойном дневном свете' : 'Чайная комната при мечети'}">
                ${isExterior ? '' : `<img class="timur-portrait" src="${ASSET_BASE}char_timur_hazrat_portrait_calm.png" alt="Тимур хәзрәт">`}
                <section class="mosque-topbar">
                    <div>
                        <div class="mosque-title">Мечеть</div>
                        <div class="mosque-subtitle">${isExterior ? 'двор, тихий свет' : 'чайная комната Тимура хәзрәтә'}</div>
                    </div>
                    <button data-mosque-action="route">Выйти к дороге</button>
                </section>
                <section class="mosque-card">
                    ${isExterior ? this.renderExterior() : this.renderOffice()}
                </section>
                <nav class="mosque-actions" aria-label="Действия в мечети">
                    <button class="${isExterior ? 'active' : ''}" data-mosque-action="exterior">Двор</button>
                    <button class="${!isExterior ? 'active' : ''}" data-mosque-action="office">К Тимуру хәзрәту</button>
                    <button data-mosque-action="route">Назад на улицу</button>
                </nav>
            </main>
        `;
    }

    private renderExterior(): string {
        return `
            <h1>Здесь тишина не пустая</h1>
            <p>У мечети светлее, чем на соседней улице. Не потому что страшное не существует, а потому что здесь люди помнят, как держаться вместе.</p>
            <p>Из чайной комнаты слышно, как ставят пиалу на блюдце.</p>
            <div class="mosque-card-actions">
                <button data-mosque-action="office">Войти к Тимуру хәзрәту</button>
            </div>
        `;
    }

    private renderOffice(): string {
        return `
            <h1>Тимур хәзрәт</h1>
            <p><b>Тимур:</b> «Я не стану говорить тебе: не спрашивай. Вопросы иногда держат человека живым».</p>
            <p><b>Тимур:</b> «Но если услышишь знакомый голос там, где человека быть не может, не отвечай сразу. Сначала вспомни, кто рядом с тобой».</p>
            <p class="mosque-note">${this.heardTimur ? 'Айдар запомнил это как практическое правило, а не как суеверие.' : 'Он говорит спокойно, без угрозы и без театра.'}</p>
            <div class="mosque-card-actions">
                <button data-mosque-action="timur">${this.heardTimur ? 'Правило записано' : 'Запомнить предупреждение'}</button>
            </div>
        `;
    }

    private renderStyles(): string {
        return `
            <style>
                .mosque-scene,
                .mosque-scene * {
                    box-sizing: border-box;
                }

                .mosque-scene {
                    position: relative;
                    width: 100vw;
                    height: 100vh;
                    overflow: hidden;
                    color: #f4ecda;
                    background: #15130f;
                    font-family: "Philosopher", "Cormorant Garamond", serif;
                }

                .mosque-bg {
                    position: absolute;
                    inset: 0;
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                    filter: saturate(0.9) contrast(1.02);
                }

                .mosque-scene::after {
                    content: "";
                    position: absolute;
                    inset: 0;
                    pointer-events: none;
                    background:
                        radial-gradient(circle at 56% 43%, transparent 38%, rgba(11, 10, 8, 0.34) 100%),
                        linear-gradient(180deg, rgba(26, 22, 16, 0.16), rgba(8, 7, 6, 0.34));
                }

                .office::after {
                    background:
                        radial-gradient(circle at 58% 48%, transparent 34%, rgba(12, 9, 6, 0.42) 100%),
                        linear-gradient(180deg, rgba(20, 15, 11, 0.15), rgba(8, 7, 6, 0.42));
                }

                .timur-portrait {
                    position: absolute;
                    right: 7%;
                    bottom: 4%;
                    z-index: 5;
                    width: min(28vw, 360px);
                    max-height: 76vh;
                    object-fit: contain;
                    filter: drop-shadow(0 20px 34px rgba(0, 0, 0, 0.38));
                }

                .mosque-topbar {
                    position: absolute;
                    z-index: 20;
                    top: 18px;
                    left: 22px;
                    right: 22px;
                    display: flex;
                    justify-content: space-between;
                    gap: 16px;
                    align-items: flex-start;
                }

                .mosque-title {
                    font-size: clamp(30px, 4.2vw, 54px);
                    line-height: 0.95;
                    text-shadow: 0 2px 18px rgba(0, 0, 0, 0.62);
                }

                .mosque-subtitle {
                    margin-top: 8px;
                    font-size: 13px;
                    letter-spacing: 0.08em;
                    text-transform: uppercase;
                    color: rgba(244, 236, 218, 0.7);
                }

                .mosque-card {
                    position: absolute;
                    z-index: 22;
                    left: 22px;
                    bottom: 104px;
                    width: min(560px, calc(100vw - 44px));
                    padding: 22px;
                    border: 1px solid rgba(239, 217, 166, 0.24);
                    border-radius: 6px;
                    background: rgba(24, 21, 16, 0.72);
                    backdrop-filter: blur(9px);
                    box-shadow: 0 20px 54px rgba(0, 0, 0, 0.34);
                }

                .mosque-card h1 {
                    margin: 0 0 12px;
                    font-size: 31px;
                    line-height: 1.05;
                    font-weight: 600;
                }

                .mosque-card p {
                    margin: 0 0 11px;
                    color: rgba(244, 236, 218, 0.83);
                    line-height: 1.48;
                }

                .mosque-note {
                    color: rgba(217, 195, 140, 0.9) !important;
                }

                .mosque-card-actions,
                .mosque-actions {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 9px;
                    margin-top: 14px;
                }

                .mosque-actions {
                    position: absolute;
                    z-index: 24;
                    left: 50%;
                    bottom: 24px;
                    transform: translateX(-50%);
                    width: min(620px, calc(100vw - 40px));
                    justify-content: center;
                    margin-top: 0;
                }

                .mosque-scene button {
                    min-height: 38px;
                    padding: 0 14px;
                    border: 1px solid rgba(241, 234, 216, 0.28);
                    border-radius: 4px;
                    color: #f4ecda;
                    background: rgba(20, 17, 13, 0.72);
                    font: inherit;
                    cursor: pointer;
                    backdrop-filter: blur(8px);
                    transition: background 160ms ease, border-color 160ms ease, transform 160ms ease;
                }

                .mosque-scene button:hover,
                .mosque-scene button.active {
                    border-color: rgba(235, 207, 137, 0.62);
                    background: rgba(62, 52, 36, 0.82);
                    transform: translateY(-1px);
                }

                @media (max-width: 760px) {
                    .timur-portrait {
                        right: -4%;
                        width: 52vw;
                        opacity: 0.42;
                    }

                    .mosque-card {
                        left: 12px;
                        bottom: 104px;
                        width: calc(100vw - 24px);
                        padding: 17px;
                    }

                    .mosque-topbar {
                        top: 12px;
                        left: 12px;
                        right: 12px;
                    }

                    .mosque-title {
                        font-size: 30px;
                    }
                }
            </style>
        `;
    }
}
