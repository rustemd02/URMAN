import { BaseScene } from './BaseScene';

type HouseView = 'kitchen' | 'pcRoom';

const ASSET_BASE = '/assets/urman_mvp_remaining/';

const HOUSE_ASSETS: Record<HouseView, string> = {
    kitchen: `${ASSET_BASE}loc_kitchen_first_dinner_warm.png`,
    pcRoom: `${ASSET_BASE}loc_mansur_pc_room_on.png`,
};

export class HouseScene extends BaseScene {
    private view: HouseView = 'kitchen';
    private talkedToMansur = false;
    private talkedToGulsina = false;

    private readonly clickHandler = (event: MouseEvent) => this.handleClick(event);

    init(container: HTMLElement): void {
        this.container = container;
        this.talkedToMansur = Boolean(this.game.state.flags.house_mansur_first_warning);
        this.talkedToGulsina = Boolean(this.game.state.flags.house_gulsina_first_warning);
        container.addEventListener('click', this.clickHandler);
        this.game.state.setFlag('house_first_objective_started', true);
        this.game.audio?.startAmbience('home_ambience');
        this.render();
    }

    destroy(): void {
        this.container?.removeEventListener('click', this.clickHandler);
        this.game.audio?.stopAmbience('home_ambience');
        super.destroy();
    }

    private handleClick(event: MouseEvent): void {
        const target = event.target;
        if (!(target instanceof HTMLElement)) return;

        const action = target.closest<HTMLElement>('[data-house-action]')?.dataset.houseAction;
        if (!action) return;

        if (action === 'kitchen' || action === 'pcRoom') {
            this.view = action;
            this.render();
            return;
        }

        if (action === 'mansur') {
            this.talkedToMansur = true;
            this.game.state.setFlag('house_mansur_first_warning', true);
            this.game.state.setFlag('mansur_pc_access_allowed', true);
            this.render();
            return;
        }

        if (action === 'gulsina') {
            this.talkedToGulsina = true;
            this.game.state.setFlag('house_gulsina_first_warning', true);
            this.game.state.setFlag('first_home_objective_ready', true);
            this.render();
            return;
        }

        if (action === 'computer') {
            this.game.state.setFlag('old_pc_access_from_house', true);
            this.game.scenes.switchScene('computer');
            return;
        }

        if (action === 'route') {
            if (this.game.state.routeCurrentNodeId === 'arrival_vehicle_dusk') {
                this.game.state.routeCurrentNodeId = 'mansur_turn_evening';
            }
            this.game.scenes.switchScene('village');
        }
    }

    private render(): void {
        if (!this.container) return;

        const isKitchen = this.view === 'kitchen';
        const objectiveDone = this.talkedToMansur && this.talkedToGulsina;
        const dialogue = isKitchen ? this.renderKitchenDialogue() : this.renderPcRoomDialogue();

        this.container.innerHTML = `
            ${this.renderStyles()}
            <main class="house-scene ${this.view}">
                <img class="house-bg" src="${HOUSE_ASSETS[this.view]}" alt="${isKitchen ? 'Кухня дома Мансура и Гөлсинә' : 'Комната со старым ПК Мансура'}">
                ${isKitchen ? this.renderKitchenCharacters() : this.renderPcRoomCharacters()}
                <section class="house-topbar">
                    <div>
                        <div class="house-title">Дом Мансура и Гөлсинә</div>
                        <div class="house-subtitle">${isKitchen ? 'кухня, первый вечер' : 'комната с компьютером'}</div>
                    </div>
                    <button data-house-action="route">Выйти к дороге</button>
                </section>
                <section class="house-card house-objective">
                    <span>${objectiveDone ? 'Цель обновлена' : 'Первая цель'}</span>
                    <strong>${objectiveDone ? 'Сесть за старый ПК и найти первые бумаги о Марате.' : 'Поговорить с бабаем и әби, не превращая приезд в допрос.'}</strong>
                </section>
                <section class="house-card house-dialogue">
                    ${dialogue}
                </section>
                <nav class="house-actions" aria-label="Действия в доме">
                    <button class="${isKitchen ? 'active' : ''}" data-house-action="kitchen">Кухня</button>
                    <button class="${!isKitchen ? 'active' : ''}" data-house-action="pcRoom">Комната ПК</button>
                    <button data-house-action="computer">Сесть за компьютер</button>
                </nav>
            </main>
        `;
    }

    private renderKitchenCharacters(): string {
        return `
            <img class="house-character mansur-table" src="${ASSET_BASE}char_mansur_cutout_seated_table.png" alt="Мансур бабай за столом">
            <img class="house-character gulsina-tea" src="${ASSET_BASE}char_gulsina_cutout_cooking_tea.png" alt="Гөлсинә готовит чай">
        `;
    }

    private renderPcRoomCharacters(): string {
        return `<img class="house-character mansur-pc" src="${ASSET_BASE}char_mansur_cutout_near_pc.png" alt="Мансур рядом со старым компьютером">`;
    }

    private renderKitchenDialogue(): string {
        return `
            <h2>Чай не остывает, разговор тоже</h2>
            <p><b>Гөлсинә:</b> «Ашап ал, улым. С дороги сначала едят, потом спрашивают».</p>
            <p><b>Мансур:</b> «Компьютер в маленькой комнате. Только старые папки не двигай. Там порядок такой, что чужому глазу кажется беспорядком».</p>
            <div class="dialogue-actions">
                <button data-house-action="mansur">${this.talkedToMansur ? 'Мансур уже разрешил ПК' : 'Спросить Мансура про Марата'}</button>
                <button data-house-action="gulsina">${this.talkedToGulsina ? 'Гөлсинә уже предупредила' : 'Поговорить с Гөлсинә'}</button>
            </div>
        `;
    }

    private renderPcRoomDialogue(): string {
        return `
            <h2>Старый ПК Мансура</h2>
            <p><b>Мансур:</b> «Если ищешь Марата, начни не с людей. Люди будут помнить то, что им разрешили помнить».</p>
            <p>Экран чуть гудит. На рабочем столе видны архив, сохранённые сообщения и папка с внутренним учётом Кырлая.</p>
            <div class="dialogue-actions">
                <button data-house-action="computer">Открыть архивный ПК</button>
                <button data-house-action="kitchen">Вернуться к столу</button>
            </div>
        `;
    }

    private renderStyles(): string {
        return `
            <style>
                .house-scene,
                .house-scene * {
                    box-sizing: border-box;
                }

                .house-scene {
                    position: relative;
                    width: 100vw;
                    height: 100vh;
                    overflow: hidden;
                    color: #f2ead8;
                    background: #17130f;
                    font-family: "Philosopher", "Cormorant Garamond", serif;
                }

                .house-bg {
                    position: absolute;
                    inset: 0;
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                    filter: saturate(0.93) contrast(1.03);
                }

                .house-scene::after {
                    content: "";
                    position: absolute;
                    inset: 0;
                    pointer-events: none;
                    background:
                        radial-gradient(circle at 52% 44%, transparent 34%, rgba(13, 10, 7, 0.38) 100%),
                        linear-gradient(180deg, rgba(25, 19, 13, 0.15), rgba(7, 6, 5, 0.42));
                }

                .house-character {
                    position: absolute;
                    z-index: 5;
                    pointer-events: none;
                    object-fit: contain;
                    filter: drop-shadow(0 18px 26px rgba(0, 0, 0, 0.36));
                }

                .mansur-table {
                    left: 8%;
                    bottom: 4%;
                    width: min(28vw, 360px);
                    max-height: 72vh;
                }

                .gulsina-tea {
                    right: 8%;
                    bottom: 7%;
                    width: min(25vw, 330px);
                    max-height: 70vh;
                }

                .mansur-pc {
                    right: 10%;
                    bottom: 5%;
                    width: min(29vw, 380px);
                    max-height: 75vh;
                }

                .house-topbar {
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

                .house-title {
                    font-size: clamp(28px, 4vw, 50px);
                    line-height: 0.95;
                    text-shadow: 0 2px 18px rgba(0, 0, 0, 0.64);
                }

                .house-subtitle {
                    margin-top: 8px;
                    font-size: 13px;
                    letter-spacing: 0.08em;
                    text-transform: uppercase;
                    color: rgba(242, 234, 216, 0.68);
                }

                .house-card {
                    position: absolute;
                    z-index: 22;
                    border: 1px solid rgba(239, 217, 166, 0.22);
                    border-radius: 6px;
                    background: rgba(24, 20, 15, 0.76);
                    backdrop-filter: blur(9px);
                    box-shadow: 0 18px 48px rgba(0, 0, 0, 0.34);
                }

                .house-objective {
                    left: 22px;
                    bottom: 112px;
                    width: min(410px, calc(100vw - 44px));
                    padding: 15px 16px;
                }

                .house-objective span {
                    display: block;
                    margin-bottom: 6px;
                    color: #d9c38c;
                    font-size: 12px;
                    letter-spacing: 0.09em;
                    text-transform: uppercase;
                }

                .house-objective strong {
                    font-size: 18px;
                    line-height: 1.28;
                    font-weight: 600;
                }

                .house-dialogue {
                    right: 22px;
                    bottom: 112px;
                    width: min(520px, calc(100vw - 44px));
                    padding: 20px;
                }

                .house-dialogue h2 {
                    margin: 0 0 10px;
                    font-size: 26px;
                    line-height: 1.05;
                }

                .house-dialogue p {
                    margin: 0 0 10px;
                    color: rgba(242, 234, 216, 0.82);
                    line-height: 1.46;
                }

                .dialogue-actions,
                .house-actions {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 9px;
                    margin-top: 14px;
                }

                .house-actions {
                    position: absolute;
                    z-index: 24;
                    left: 50%;
                    bottom: 24px;
                    transform: translateX(-50%);
                    width: min(640px, calc(100vw - 40px));
                    justify-content: center;
                    margin-top: 0;
                }

                .house-scene button {
                    min-height: 38px;
                    padding: 0 14px;
                    border: 1px solid rgba(241, 234, 216, 0.27);
                    border-radius: 4px;
                    color: #f2ead8;
                    background: rgba(20, 17, 13, 0.72);
                    font: inherit;
                    cursor: pointer;
                    backdrop-filter: blur(8px);
                    transition: background 160ms ease, border-color 160ms ease, transform 160ms ease;
                }

                .house-scene button:hover,
                .house-scene button.active {
                    border-color: rgba(235, 207, 137, 0.62);
                    background: rgba(68, 53, 34, 0.82);
                    transform: translateY(-1px);
                }

                @media (max-width: 860px) {
                    .house-character {
                        opacity: 0.52;
                    }

                    .mansur-table,
                    .gulsina-tea,
                    .mansur-pc {
                        width: 45vw;
                    }

                    .house-objective {
                        display: none;
                    }

                    .house-dialogue {
                        left: 12px;
                        right: 12px;
                        bottom: 108px;
                        width: auto;
                        padding: 16px;
                    }

                    .house-topbar {
                        top: 12px;
                        left: 12px;
                        right: 12px;
                    }

                    .house-title {
                        font-size: 27px;
                    }
                }
            </style>
        `;
    }
}
