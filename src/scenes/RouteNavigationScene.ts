import { BaseScene } from './BaseScene';

type RouteGraph = Readonly<{
    startNodeId?: string;
    nodes: RouteNode[];
    transitions: Record<string, RouteTransitionConfig>;
    stateTransitions?: RouteStateTransition[];
    supportViewTransitions?: RouteStateTransition[];
    navigation_contract?: {
        action_transition_defaults?: Record<string, RouteActionDefault>;
    };
}>;

type RouteNode = Readonly<{
    id: string;
    locationId: string;
    timePhase: string;
    pressureVariant: string;
    facing: string;
    backgroundAssetId: string;
    availableActions: string[];
    exits: Record<string, string>;
    diegeticSigns?: DiegeticSign[];
    landmarks?: string[];
    ambienceId?: string;
    journalSketchUpdate?: string;
}>;

type DiegeticSign = Readonly<{
    label: string;
    target: string;
    language?: string;
    visibility?: string;
}>;

type RouteTransitionConfig = Readonly<{
    durationMs?: [number, number];
    assetId?: string;
    overlayAssetId?: string;
    behavior?: string;
}>;

type RouteActionDefault = Readonly<{
    turnDegrees: number;
    transitionId: string | null;
}>;

type RouteStateTransition = Readonly<{
    from?: string;
    fromNodeId?: string;
    to?: string;
    toNodeId?: string;
    trigger?: string;
    notes?: string;
}>;

type AssetManifest = Readonly<{
    assets: ManifestAsset[];
}>;

type ManifestAsset = Readonly<{
    asset_id: string;
    file_name: string;
    asset_type?: string;
}>;

type AnimationLayerFile = Readonly<{
    assets: AnimationLayerAsset[];
}>;

type AnimationLayerAsset = Readonly<{
    asset_id: string;
    animation_slots?: AnimationSlot[];
}>;

type AnimationSlot = Readonly<{
    id: string;
    type: string;
    target?: string;
    normalized_rect?: [number, number, number, number];
    creepiness_level?: number;
}>;

type EditableLayerFile = Readonly<{
    layers: EditableLayer[];
}>;

type EditableLayer = Readonly<{
    asset_id: string;
    editable_text_regions?: EditableTextRegion[];
}>;

type EditableTextRegion = Readonly<{
    id: string;
    purpose?: string;
    normalized_rect?: [number, number, number, number];
}>;

type ModalState =
    | { type: 'inspect'; nodeId: string }
    | { type: 'journal' }
    | { type: 'external'; targetId: string; fromNodeId: string };

type ActiveTransition = Readonly<{
    action: string;
    transitionId: string;
    assetIds: string[];
}>;

const ROUTE_PACK_BASE = '/assets/urman_route_map/';
const ROUTE_GRAPH_URL = `${ROUTE_PACK_BASE}route_graph.json`;
const MANIFEST_URL = `${ROUTE_PACK_BASE}asset_manifest.json`;
const ANIMATION_URL = `${ROUTE_PACK_BASE}animation_layers.json`;
const EDITABLE_URL = `${ROUTE_PACK_BASE}editable_layers.json`;

const ACTION_ORDER = ['turnLeft', 'forward', 'turnRight', 'back', 'inspect', 'talk'] as const;

const ACTION_LABELS: Record<string, string> = {
    forward: 'Вперёд',
    back: 'Назад',
    turnLeft: 'Налево',
    turnRight: 'Направо',
    inspect: 'Осмотреть',
    talk: 'Поговорить',
};

const ACTION_MARKS: Record<string, string> = {
    forward: '↑',
    back: '↓',
    turnLeft: '←',
    turnRight: '→',
    inspect: '⌕',
    talk: '…',
};

const LOCATION_LABELS: Record<string, string> = {
    admin_archive_corner: 'Администрация',
    alsu_meeting_spot: 'Место Алсу',
    arrival_road: 'Дорога в Кырлай',
    bridge_river_turn: 'Поворот к реке',
    forest_approach: 'Подступ к лесу',
    kara_urman_edge: 'Кромка Кара-Урмана',
    main_street_entry: 'Главная улица',
    mansur_house_turn: 'Поворот к дому Мансура',
    mosque_sign: 'Указатель к мечети',
    selsmag_fap_lane: 'ФАП и сельмаг',
    unsafe_path: 'Закрытая тропа',
    village_crossroad: 'Перекрёсток',
    zirat_road: 'Дорога к зирату',
};

const TIME_LABELS: Record<string, string> = {
    day: 'день',
    dusk: 'сумерки',
    evening: 'вечер',
};

const STATE_TRANSITION_LABELS: Record<string, string> = {
    village_pressure_2: 'Оглянуться на окна',
    village_pressure_2_or_evening_return: 'Оглянуться ещё раз',
    medical_contradiction_key_used: 'Вспомнить медзапись',
    time_phase_evening: 'Дождаться вечера',
    forest_pressure_flag: 'Прислушаться к лесу',
    marat_voice_trigger: 'Замереть',
    rinat_interrupts_answer: 'Не отвечать',
    time_phase_evening_or_after_grave_clue: 'Идти дальше к вечеру',
    cemetery_contradiction_or_pressure_2: 'Сверить дату в голове',
};

const SIGN_LABEL_OVERRIDES: Record<string, string> = {
    blank_admin_notice: 'доска объявлений',
    blank_blocked_notice: 'закрытая тропа',
    blank_crossroad_marker: 'перекрёсток',
    blank_house_marker: 'дом Мансура',
    blank_main_street_marker: 'главная улица',
    blank_mansur_house_sign: 'дом Мансура',
    blank_notice_board: 'объявления',
    blank_roadside_sign: 'Кырлай',
    blank_street_sign: 'улица',
    blank_worse_signs: 'старые указатели',
    blocked_notice: 'закрыто',
    old_place_name: 'старое название',
    old_warning_or_zirat_back: 'назад к зирату',
    'ФАП / notice': 'ФАП',
    'су / bridge': 'су / мост',
};

const EXTERNAL_HANDOFFS: Record<string, { title: string; description: string; sceneId?: string; imagePath?: string; actionLabel?: string }> = {
    fap_pressure_document_desk: {
        title: 'ФАП',
        description: 'На столе слишком аккуратно лежат бумаги. Вход в отдельную сцену расследования пока оставлен как handoff.',
        imagePath: '/assets/urman_mvp_remaining/loc_fap_pressure_document_desk.png',
    },
    fap_waiting_room_day: {
        title: 'ФАП',
        description: 'Пахнет лекарствами, старой краской и пылью. За дверью кабинета слышно короткое движение.',
        imagePath: '/assets/urman_mvp_remaining/loc_fap_waiting_room_day.png',
    },
    mansur_house_exterior_day: {
        title: 'Дом Мансура',
        description: 'Двор выглядит обычным, почти слишком обычным: калитка, старая Нива, тёплое окно.',
        sceneId: 'house',
        imagePath: '/assets/urman_mvp_remaining/loc_mansur_house_exterior_day.png',
        actionLabel: 'Войти в дом',
    },
    mansur_house_exterior_evening: {
        title: 'Дом Мансура',
        description: 'Вечерний свет в окне держится дольше, чем должен. Внутри ждут бабай и әби.',
        sceneId: 'house',
        imagePath: '/assets/urman_mvp_remaining/loc_mansur_house_exterior_evening.png',
        actionLabel: 'Войти в дом',
    },
    mosque_exterior_day: {
        title: 'Мечеть',
        description: 'Здесь тише. Не пусто, а спокойно.',
        sceneId: 'mosque',
        imagePath: '/assets/urman_mvp_remaining/loc_mosque_exterior_day_safe_light.png',
        actionLabel: 'Войти',
    },
    mosque_exterior_evening_safe: {
        title: 'Мечеть',
        description: 'Свет у мечети не спорит с темнотой, а удерживает её на расстоянии.',
        sceneId: 'mosque',
        imagePath: '/assets/urman_mvp_remaining/loc_mosque_exterior_day_safe_light.png',
        actionLabel: 'Войти',
    },
    mvp_end_cliffhanger: {
        title: 'Кромка леса',
        description: 'Голос замирает на полуслове. Ринат стоит ближе, чем должен был успеть.',
        sceneId: 'forest',
        imagePath: '/assets/urman_route_map/route_kara_urman_edge_rinat_interruption.png',
        actionLabel: 'Остаться у леса',
    },
    river_bank_day: {
        title: 'Берег',
        description: 'Камыш почти не шевелится. Вода отражает небо чуть темнее, чем оно есть.',
        imagePath: '/assets/urman_mvp_remaining/loc_river_bank_day.png',
    },
};

export class RouteNavigationScene extends BaseScene {
    private graph: RouteGraph | null = null;
    private nodes = new Map<string, RouteNode>();
    private assets = new Map<string, ManifestAsset>();
    private animationSlots = new Map<string, AnimationSlot[]>();
    private editableLayers = new Map<string, EditableLayer>();
    private currentNodeId = 'arrival_vehicle_dusk';
    private isLoading = true;
    private errorMessage: string | null = null;
    private activeTransition: ActiveTransition | null = null;
    private modal: ModalState | null = null;
    private timeouts = new Set<number>();

    private readonly clickHandler = (event: MouseEvent) => this.handleClick(event);
    private readonly keydownHandler = (event: KeyboardEvent) => this.handleKeydown(event);

    init(container: HTMLElement): void {
        this.container = container;
        container.addEventListener('click', this.clickHandler);
        window.addEventListener('keydown', this.keydownHandler);
        this.render();
        void this.loadRouteData();
    }

    destroy(): void {
        if (this.container) {
            this.container.removeEventListener('click', this.clickHandler);
        }
        window.removeEventListener('keydown', this.keydownHandler);
        for (const timeout of this.timeouts) {
            window.clearTimeout(timeout);
        }
        this.timeouts.clear();
        super.destroy();
    }

    private async loadRouteData(): Promise<void> {
        try {
            const [graph, manifest, animationLayers, editableLayers] = await Promise.all([
                this.fetchJson<RouteGraph>(ROUTE_GRAPH_URL),
                this.fetchJson<AssetManifest>(MANIFEST_URL),
                this.fetchJson<AnimationLayerFile>(ANIMATION_URL),
                this.fetchJson<EditableLayerFile>(EDITABLE_URL),
            ]);

            this.graph = graph;
            this.nodes = new Map(graph.nodes.map((node) => [node.id, node]));
            this.assets = new Map(manifest.assets.map((asset) => [asset.asset_id, asset]));
            this.animationSlots = new Map(animationLayers.assets.map((asset) => [asset.asset_id, asset.animation_slots ?? []]));
            this.editableLayers = new Map(editableLayers.layers.map((layer) => [layer.asset_id, layer]));

            const savedNodeId = this.game.state.routeCurrentNodeId;
            const startNodeId = graph.startNodeId ?? 'arrival_vehicle_dusk';
            this.currentNodeId = this.nodes.has(savedNodeId) ? savedNodeId : startNodeId;
            this.visitNode(this.currentNodeId);
            this.isLoading = false;
            this.render();
        } catch (error) {
            this.isLoading = false;
            this.errorMessage = error instanceof Error ? error.message : 'Не удалось загрузить route graph.';
            this.render();
        }
    }

    private async fetchJson<T>(url: string): Promise<T> {
        const response = await fetch(url);
        if (!response.ok) {
            throw new Error(`Route data load failed: ${url} (${response.status})`);
        }
        return response.json() as Promise<T>;
    }

    private handleClick(event: MouseEvent): void {
        const rawTarget = event.target;
        if (!(rawTarget instanceof HTMLElement)) return;

        const routeAction = rawTarget.closest('[data-route-action]') as HTMLElement | null;
        if (routeAction) {
            this.executeRouteAction(routeAction.dataset.routeAction ?? '');
            return;
        }

        const targetNode = rawTarget.closest('[data-route-target]') as HTMLElement | null;
        if (targetNode) {
            this.goToTarget(targetNode.dataset.routeTarget ?? '', 'inspect');
            return;
        }

        const stateTransition = rawTarget.closest('[data-state-transition]') as HTMLElement | null;
        if (stateTransition) {
            this.applyStateTransition(stateTransition.dataset.stateTransition ?? '');
            return;
        }

        const modalAction = rawTarget.closest('[data-modal-action]') as HTMLElement | null;
        if (modalAction) {
            this.executeModalAction(modalAction.dataset.modalAction ?? '');
        }
    }

    private handleKeydown(event: KeyboardEvent): void {
        if (this.isLoading || this.errorMessage) return;
        if (event.key === 'Escape') {
            this.modal = null;
            this.render();
            return;
        }
        if (this.modal) return;

        const actionByKey: Record<string, string> = {
            ArrowUp: 'forward',
            KeyW: 'forward',
            ArrowDown: 'back',
            KeyS: 'back',
            ArrowLeft: 'turnLeft',
            KeyA: 'turnLeft',
            ArrowRight: 'turnRight',
            KeyD: 'turnRight',
            KeyI: 'inspect',
            KeyJ: 'journal',
        };
        const action = actionByKey[event.code];
        if (!action) return;
        event.preventDefault();
        if (action === 'journal') {
            this.modal = { type: 'journal' };
            this.render();
            return;
        }
        this.executeRouteAction(action);
    }

    private executeRouteAction(action: string): void {
        if (this.activeTransition) return;
        const node = this.getCurrentNode();
        if (!node) return;

        if (action === 'inspect') {
            this.modal = { type: 'inspect', nodeId: node.id };
            this.render();
            return;
        }

        if (action === 'talk') {
            this.modal = { type: 'inspect', nodeId: node.id };
            this.render();
            return;
        }

        const targetId = node.exits[action];
        if (!targetId) return;
        this.goToTarget(targetId, action);
    }

    private goToTarget(targetId: string, action: string): void {
        if (this.nodes.has(targetId)) {
            this.moveToNode(targetId, action);
            return;
        }
        this.modal = { type: 'external', targetId, fromNodeId: this.currentNodeId };
        this.render();
    }

    private moveToNode(targetId: string, action: string): void {
        const transitionId = this.pickTransitionId(action, targetId);
        const transitionConfig = this.graph?.transitions[transitionId];
        const assetIds = [transitionConfig?.assetId, transitionConfig?.overlayAssetId].filter((value): value is string => Boolean(value));
        const duration = this.pickTransitionDuration(transitionConfig);

        this.modal = null;
        this.activeTransition = { action, transitionId, assetIds };
        this.render();

        if (action === 'forward' || action === 'back') {
            this.game.audio?.play('footsteps', 0.18);
        }

        const timeout = window.setTimeout(() => {
            this.timeouts.delete(timeout);
            this.activeTransition = null;
            this.visitNode(targetId);
            this.render();
        }, duration);
        this.timeouts.add(timeout);
    }

    private pickTransitionId(action: string, targetId: string): string {
        const node = this.getCurrentNode();
        if (node?.id.includes('forest_approach') && targetId.includes('kara_urman')) {
            return 'forest_pressure_edge';
        }
        if (action === 'turnLeft' || action === 'turnRight') {
            return this.graph?.navigation_contract?.action_transition_defaults?.[action]?.transitionId ?? 'turn_90_default';
        }
        return this.graph?.navigation_contract?.action_transition_defaults?.forward?.transitionId ?? 'step_forward_default';
    }

    private pickTransitionDuration(config?: RouteTransitionConfig): number {
        const range = config?.durationMs;
        if (!range) return 420;
        return Math.round((range[0] + range[1]) / 2);
    }

    private applyStateTransition(indexValue: string): void {
        const index = Number(indexValue);
        const transition = this.getAvailableStateTransitions()[index];
        const targetId = transition ? transition.to ?? transition.toNodeId : undefined;
        if (!targetId) return;
        this.goToTarget(targetId, 'forward');
    }

    private executeModalAction(action: string): void {
        if (action === 'close') {
            this.modal = null;
            this.render();
            return;
        }
        if (action === 'journal') {
            this.modal = { type: 'journal' };
            this.render();
            return;
        }
        if (action === 'external-scene') {
            const targetId = this.modal?.type === 'external' ? this.modal.targetId : '';
            const sceneId = EXTERNAL_HANDOFFS[targetId]?.sceneId;
            if (sceneId) {
                this.game.scenes.switchScene(sceneId);
            }
        }
    }

    private visitNode(nodeId: string): void {
        const node = this.nodes.get(nodeId);
        if (!node) return;
        this.currentNodeId = node.id;
        this.game.state.rememberRouteVisit(node.id, node.journalSketchUpdate, node.pressureVariant);
        this.game.state.locationName = this.getLocationLabel(node);
        this.game.state.timeOfDay = TIME_LABELS[node.timePhase] ?? node.timePhase;
    }

    private getCurrentNode(): RouteNode | undefined {
        return this.nodes.get(this.currentNodeId);
    }

    private getAvailableStateTransitions(): RouteStateTransition[] {
        const transitions = this.graph?.stateTransitions ?? [];
        return transitions.filter((transition) => {
            const from = transition.from ?? transition.fromNodeId;
            return from === this.currentNodeId;
        });
    }

    private getLocationLabel(node: RouteNode): string {
        return LOCATION_LABELS[node.locationId] ?? node.locationId.replaceAll('_', ' ');
    }

    private assetUrl(assetId: string): string {
        const asset = this.assets.get(assetId);
        return asset ? `${ROUTE_PACK_BASE}${asset.file_name}` : `${ROUTE_PACK_BASE}${assetId}.png`;
    }

    private render(): void {
        if (!this.container) return;
        if (this.isLoading) {
            this.container.innerHTML = `${this.renderStyles()}<main class="route-scene route-loading"><div>Кырлай проявляется на бумаге…</div></main>`;
            return;
        }
        if (this.errorMessage) {
            this.container.innerHTML = `${this.renderStyles()}<main class="route-scene route-loading"><div>${this.escape(this.errorMessage)}</div></main>`;
            return;
        }

        const node = this.getCurrentNode();
        if (!node) {
            this.container.innerHTML = `${this.renderStyles()}<main class="route-scene route-loading"><div>Route node not found.</div></main>`;
            return;
        }

        const bgUrl = this.assetUrl(node.backgroundAssetId);
        const isLocked = Boolean(this.activeTransition);
        const rootClasses = ['route-scene', this.isPressureNode(node) ? 'pressure-node' : '', isLocked ? 'route-locked' : ''].join(' ');

        this.container.innerHTML = `
            ${this.renderStyles()}
            <main class="${rootClasses}">
                <section class="route-view" aria-label="${this.escape(this.getLocationLabel(node))}">
                    <img class="route-bg" src="${bgUrl}" alt="${this.escape(this.getLocationLabel(node))}">
                    ${this.renderAnimationSlots(node)}
                    ${this.renderEditableRegions(node)}
                    ${this.renderTransition()}
                </section>
                <section class="route-topbar">
                    <div>
                        <div class="route-place">${this.escape(this.getLocationLabel(node))}</div>
                        <div class="route-subline">${this.escape(TIME_LABELS[node.timePhase] ?? node.timePhase)} · ${this.escape(node.facing.replaceAll('_', ' '))}</div>
                    </div>
                    <button class="route-journal-btn" data-modal-action="journal">Журнал</button>
                </section>
                ${this.renderSigns(node)}
                ${this.renderControls(node)}
                ${this.renderFooter(node)}
                ${this.renderModal()}
            </main>
        `;
    }

    private renderAnimationSlots(node: RouteNode): string {
        const slots = this.animationSlots.get(node.backgroundAssetId) ?? [];
        return slots
            .filter((slot) => this.shouldRenderSlot(slot, node))
            .map((slot) => {
                const rect = slot.normalized_rect ?? [0, 0, 1, 1];
                const style = this.rectStyle(rect);
                const slotType = this.slotTypeClass(slot.type);
                return `<div class="route-slot ${slotType}" style="${style}" aria-hidden="true"></div>`;
            })
            .join('');
    }

    private shouldRenderSlot(slot: AnimationSlot, node: RouteNode): boolean {
        if (slot.type === 'pressure_overlay') return this.isPressureNode(node);
        if (slot.creepiness_level && slot.creepiness_level >= 2) return this.isPressureNode(node);
        return true;
    }

    private slotTypeClass(type: string): string {
        if (type.includes('light') || type.includes('soft')) return 'slot-light';
        if (type.includes('sky')) return 'slot-sky';
        if (type.includes('water')) return 'slot-water';
        if (type.includes('pressure')) return 'slot-pressure';
        if (type.includes('ui')) return 'slot-ui';
        return 'slot-drift';
    }

    private renderEditableRegions(node: RouteNode): string {
        const layer = this.editableLayers.get(node.backgroundAssetId);
        const regions = layer?.editable_text_regions ?? [];
        if (!regions.length) return '';
        return regions
            .map((region) => {
                const rect = region.normalized_rect ?? [0, 0, 1, 1];
                return `<div class="editable-region" style="${this.rectStyle(rect)}" title="${this.escape(region.id)}"></div>`;
            })
            .join('');
    }

    private renderTransition(): string {
        if (!this.activeTransition) return '';
        const transitionAssets = this.activeTransition.assetIds
            .map((assetId) => `<img class="transition-image" src="${this.assetUrl(assetId)}" alt="">`)
            .join('');
        const actionClass = this.activeTransition.action === 'turnLeft'
            ? 'turn-left'
            : this.activeTransition.action === 'turnRight'
                ? 'turn-right'
                : 'step-forward';
        return `
            <div class="transition-layer ${actionClass}">
                ${transitionAssets}
                <div class="transition-wash"></div>
            </div>
        `;
    }

    private renderSigns(node: RouteNode): string {
        const signs = node.diegeticSigns ?? [];
        if (!signs.length) return '';
        return `
            <section class="route-sign-stack" aria-label="Указатели">
                ${signs.map((sign) => `
                    <button class="route-sign" data-route-target="${this.escape(sign.target)}">
                        <span>${this.escape(this.cleanSignLabel(sign.label))}</span>
                    </button>
                `).join('')}
            </section>
        `;
    }

    private renderControls(node: RouteNode): string {
        const actions = ACTION_ORDER.filter((action) => {
            if (action === 'inspect' || action === 'talk') return node.availableActions.includes(action);
            return node.availableActions.includes(action) && Boolean(node.exits[action]);
        });
        return `
            <nav class="route-controls" aria-label="Навигация">
                ${actions.map((action) => `
                    <button class="route-control ${action}" data-route-action="${action}" ${this.activeTransition ? 'disabled' : ''}>
                        <span class="route-control-mark">${ACTION_MARKS[action]}</span>
                        <span>${ACTION_LABELS[action]}</span>
                    </button>
                `).join('')}
            </nav>
        `;
    }

    private renderFooter(node: RouteNode): string {
        const discovered = this.game.state.routeDiscoveredNodeIds.length;
        const total = this.nodes.size;
        const ambience = node.ambienceId ? ` · ${node.ambienceId.replaceAll('_', ' ')}` : '';
        return `
            <section class="route-footer">
                <span>${this.escape(node.id)}</span>
                <span>открыто ${discovered}/${total}${this.escape(ambience)}</span>
            </section>
        `;
    }

    private renderModal(): string {
        if (!this.modal) return '';
        if (this.modal.type === 'journal') return this.renderJournalModal();
        if (this.modal.type === 'external') return this.renderExternalModal(this.modal.targetId);
        return this.renderInspectModal(this.modal.nodeId);
    }

    private renderInspectModal(nodeId: string): string {
        const node = this.nodes.get(nodeId);
        if (!node) return '';
        const signs = node.diegeticSigns ?? [];
        const landmarks = node.landmarks ?? [];
        const transitions = this.getAvailableStateTransitions();
        return `
            <section class="route-modal-backdrop">
                <div class="route-modal route-modal-narrow">
                    <button class="route-modal-close" data-modal-action="close">×</button>
                    <h2>${this.escape(this.getLocationLabel(node))}</h2>
                    <p>${this.escape(this.describeNode(node))}</p>
                    ${landmarks.length ? `<div class="modal-list">${landmarks.map((item) => `<span>${this.escape(item)}</span>`).join('')}</div>` : ''}
                    ${signs.length ? `
                        <div class="modal-actions">
                            ${signs.map((sign) => `<button data-route-target="${this.escape(sign.target)}">${this.escape(this.cleanSignLabel(sign.label))}</button>`).join('')}
                        </div>
                    ` : ''}
                    ${transitions.length ? `
                        <div class="modal-actions muted">
                            ${transitions.map((transition, index) => `<button data-state-transition="${index}">${this.escape(this.stateTransitionLabel(transition))}</button>`).join('')}
                        </div>
                    ` : ''}
                </div>
            </section>
        `;
    }

    private renderJournalModal(): string {
        const pressureJournal = this.game.state.routeJournalPressure;
        const journalAssetId = pressureJournal ? 'ui_journal_route_sketch_pressure' : 'ui_journal_route_sketch_base';
        const updates = this.game.state.routeJournalUpdateIds;
        return `
            <section class="route-modal-backdrop">
                <div class="route-modal route-journal-modal">
                    <button class="route-modal-close" data-modal-action="close">×</button>
                    <h2>Журнал Айдара</h2>
                    <div class="journal-image-wrap">
                        <img src="${this.assetUrl(journalAssetId)}" alt="Неполная схема маршрутов Кырлая">
                    </div>
                    <div class="journal-note">
                        <span>Узлов открыто: ${this.game.state.routeDiscoveredNodeIds.length}/${this.nodes.size}</span>
                        <span>Обновлений схемы: ${updates.length}</span>
                    </div>
                </div>
            </section>
        `;
    }

    private renderExternalModal(targetId: string): string {
        const handoff = EXTERNAL_HANDOFFS[targetId] ?? {
            title: targetId.replaceAll('_', ' '),
            description: 'Эта точка ведёт в отдельную сцену.',
        };
        const image = handoff.imagePath ? `<img class="external-preview" src="${handoff.imagePath}" alt="${this.escape(handoff.title)}">` : '';
        const sceneButton = handoff.sceneId
            ? `<button data-modal-action="external-scene">${this.escape(handoff.actionLabel ?? 'Перейти')}</button>`
            : '';
        return `
            <section class="route-modal-backdrop">
                <div class="route-modal">
                    <button class="route-modal-close" data-modal-action="close">×</button>
                    <h2>${this.escape(handoff.title)}</h2>
                    ${image}
                    <p>${this.escape(handoff.description)}</p>
                    <div class="modal-actions">
                        ${sceneButton}
                        <button data-modal-action="close">Остаться на дороге</button>
                    </div>
                </div>
            </section>
        `;
    }

    private describeNode(node: RouteNode): string {
        if (node.pressureVariant === 'pressure') return 'Здесь ничего не произошло, но дорога выглядит так, будто ждала именно этого.';
        if (node.pressureVariant === 'restricted') return 'Путь не закрыт полностью. Он просто не хочет быть выбранным сейчас.';
        if (node.pressureVariant === 'cliffhanger' || node.pressureVariant === 'voice_lock') return 'Тишина перестаёт быть пустотой.';
        if (node.timePhase === 'evening') return 'Вечер сглаживает обычные детали, и от этого они становятся заметнее.';
        return 'Обычный деревенский участок: дорога, заборы, следы быта и несколько направлений дальше.';
    }

    private stateTransitionLabel(transition: RouteStateTransition): string {
        const trigger = transition.trigger ?? '';
        return STATE_TRANSITION_LABELS[trigger] ?? 'Проверить это место ещё раз';
    }

    private cleanSignLabel(label: string): string {
        const override = SIGN_LABEL_OVERRIDES[label];
        if (override) return override;
        return label
            .replaceAll('blank_', '')
            .replaceAll('_', ' ')
            .replaceAll('placeholder', '')
            .replaceAll('multiple', 'указатели')
            .trim() || 'указатель';
    }

    private isPressureNode(node: RouteNode): boolean {
        return !['normal', 'evening'].includes(node.pressureVariant);
    }

    private rectStyle(rect: readonly number[]): string {
        const [x, y, w, h] = rect;
        return `left:${x * 100}%;top:${y * 100}%;width:${w * 100}%;height:${h * 100}%;`;
    }

    private escape(value: string): string {
        return value
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;');
    }

    private renderStyles(): string {
        return `
            <style>
                @keyframes routeLightPulse {
                    0%, 100% { opacity: 0.12; transform: scale(1); }
                    50% { opacity: 0.26; transform: scale(1.018); }
                }

                @keyframes routeDrift {
                    0%, 100% { transform: translate3d(-0.15%, 0, 0); opacity: 0.11; }
                    50% { transform: translate3d(0.18%, -0.08%, 0); opacity: 0.18; }
                }

                @keyframes routeWater {
                    0%, 100% { transform: translateX(-0.25%); opacity: 0.10; }
                    50% { transform: translateX(0.25%); opacity: 0.18; }
                }

                @keyframes routeTransitionWash {
                    0% { opacity: 0; transform: scale(1); }
                    35% { opacity: 0.92; transform: scale(1.015); }
                    100% { opacity: 0; transform: scale(1.03); }
                }

                @keyframes routeTransitionSlideLeft {
                    0% { transform: translateX(0); opacity: 0; }
                    30% { opacity: 0.95; }
                    100% { transform: translateX(-4%); opacity: 0; }
                }

                @keyframes routeTransitionSlideRight {
                    0% { transform: translateX(0); opacity: 0; }
                    30% { opacity: 0.95; }
                    100% { transform: translateX(4%); opacity: 0; }
                }

                .route-scene,
                .route-scene * {
                    box-sizing: border-box;
                    image-rendering: auto;
                }

                .route-scene {
                    position: relative;
                    width: 100vw;
                    height: 100vh;
                    overflow: hidden;
                    background: #15130f;
                    color: #f1ead8;
                    font-family: "Philosopher", "Cormorant Garamond", serif;
                    user-select: none;
                }

                .route-loading {
                    display: grid;
                    place-items: center;
                    background:
                        radial-gradient(circle at 50% 45%, rgba(196, 177, 130, 0.13), transparent 36%),
                        #0c0b09;
                    letter-spacing: 0.03em;
                }

                .route-view {
                    position: absolute;
                    inset: 0;
                    overflow: hidden;
                    background: #0d0c09;
                }

                .route-bg {
                    position: absolute;
                    inset: 0;
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                    filter: saturate(0.9) contrast(1.02);
                }

                .route-view::after {
                    content: "";
                    position: absolute;
                    inset: 0;
                    pointer-events: none;
                    background:
                        radial-gradient(circle at 50% 48%, transparent 42%, rgba(14, 11, 7, 0.26) 100%),
                        linear-gradient(180deg, rgba(24, 21, 16, 0.20), transparent 24%, rgba(9, 8, 7, 0.28));
                    mix-blend-mode: multiply;
                }

                .pressure-node .route-view::after {
                    background:
                        radial-gradient(circle at 50% 48%, transparent 33%, rgba(7, 6, 5, 0.55) 100%),
                        linear-gradient(180deg, rgba(18, 14, 11, 0.32), transparent 18%, rgba(7, 6, 5, 0.45));
                }

                .route-slot,
                .editable-region {
                    position: absolute;
                    pointer-events: none;
                    border-radius: 2px;
                }

                .slot-light {
                    background: radial-gradient(circle, rgba(255, 219, 143, 0.55), transparent 64%);
                    mix-blend-mode: screen;
                    animation: routeLightPulse 7s ease-in-out infinite;
                }

                .slot-sky {
                    background:
                        radial-gradient(circle at 18% 42%, rgba(28, 25, 21, 0.38) 0 1px, transparent 2px),
                        radial-gradient(circle at 62% 28%, rgba(28, 25, 21, 0.28) 0 1px, transparent 2px);
                    animation: routeDrift 18s ease-in-out infinite;
                }

                .slot-water {
                    background: linear-gradient(92deg, transparent, rgba(231, 226, 203, 0.24), transparent);
                    mix-blend-mode: screen;
                    animation: routeWater 8s ease-in-out infinite;
                }

                .slot-drift {
                    background: linear-gradient(105deg, transparent, rgba(31, 29, 20, 0.24), transparent);
                    mix-blend-mode: multiply;
                    animation: routeDrift 12s ease-in-out infinite;
                }

                .slot-pressure {
                    background: radial-gradient(circle at 50% 50%, transparent 36%, rgba(0, 0, 0, 0.38));
                    mix-blend-mode: multiply;
                    opacity: 0.55;
                }

                .slot-ui {
                    background: rgba(245, 231, 190, 0.08);
                    outline: 1px solid rgba(245, 231, 190, 0.10);
                }

                .editable-region {
                    background: rgba(244, 221, 151, 0.04);
                    outline: 1px dashed rgba(244, 221, 151, 0.12);
                    opacity: 0;
                    transition: opacity 180ms ease;
                }

                .route-scene:has(.route-sign:hover) .editable-region,
                .route-scene:has(.route-journal-btn:hover) .editable-region {
                    opacity: 1;
                }

                .route-topbar {
                    position: absolute;
                    top: 18px;
                    left: 20px;
                    right: 20px;
                    z-index: 20;
                    display: flex;
                    justify-content: space-between;
                    align-items: flex-start;
                    pointer-events: none;
                }

                .route-place {
                    font-size: clamp(24px, 3vw, 44px);
                    line-height: 0.95;
                    text-shadow: 0 2px 18px rgba(0, 0, 0, 0.65);
                }

                .route-subline {
                    margin-top: 8px;
                    font-size: 13px;
                    letter-spacing: 0.08em;
                    text-transform: uppercase;
                    color: rgba(241, 234, 216, 0.68);
                    text-shadow: 0 2px 10px rgba(0, 0, 0, 0.7);
                }

                .route-journal-btn,
                .route-control,
                .route-sign,
                .route-modal button {
                    font: inherit;
                    color: #f2ead7;
                    background: rgba(19, 17, 13, 0.68);
                    border: 1px solid rgba(241, 234, 216, 0.26);
                    cursor: pointer;
                    backdrop-filter: blur(8px);
                    transition: transform 160ms ease, background 160ms ease, border-color 160ms ease;
                }

                .route-journal-btn {
                    pointer-events: auto;
                    min-height: 38px;
                    padding: 0 16px;
                    border-radius: 4px;
                }

                .route-journal-btn:hover,
                .route-control:hover,
                .route-sign:hover,
                .route-modal button:hover {
                    background: rgba(58, 48, 35, 0.78);
                    border-color: rgba(245, 225, 170, 0.55);
                    transform: translateY(-1px);
                }

                .route-sign-stack {
                    position: absolute;
                    left: 20px;
                    top: 118px;
                    z-index: 20;
                    display: flex;
                    flex-direction: column;
                    gap: 8px;
                    max-width: min(320px, calc(100vw - 40px));
                }

                .route-sign {
                    min-height: 32px;
                    padding: 7px 11px;
                    border-radius: 3px;
                    text-align: left;
                    color: rgba(244, 232, 202, 0.88);
                }

                .route-controls {
                    position: absolute;
                    left: 50%;
                    bottom: 28px;
                    z-index: 24;
                    transform: translateX(-50%);
                    display: grid;
                    grid-template-columns: repeat(3, minmax(92px, 1fr));
                    gap: 8px;
                    width: min(480px, calc(100vw - 40px));
                }

                .route-control {
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    gap: 8px;
                    min-height: 46px;
                    border-radius: 4px;
                    padding: 0 12px;
                }

                .route-control:disabled {
                    opacity: 0.45;
                    cursor: default;
                    transform: none;
                }

                .route-control-mark {
                    font-size: 18px;
                    line-height: 1;
                    color: #d9c38c;
                }

                .route-control.inspect,
                .route-control.talk {
                    grid-column: span 1;
                }

                .route-footer {
                    position: absolute;
                    left: 20px;
                    right: 20px;
                    bottom: 12px;
                    z-index: 18;
                    display: flex;
                    justify-content: space-between;
                    gap: 16px;
                    color: rgba(241, 234, 216, 0.42);
                    font-size: 11px;
                    pointer-events: none;
                    text-transform: lowercase;
                }

                .transition-layer {
                    position: absolute;
                    inset: 0;
                    z-index: 50;
                    pointer-events: none;
                    overflow: hidden;
                    background: rgba(11, 9, 7, 0.08);
                }

                .transition-image {
                    position: absolute;
                    inset: 0;
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                    opacity: 0.85;
                    mix-blend-mode: multiply;
                    animation: routeTransitionWash 620ms ease-in-out forwards;
                }

                .turn-left .transition-image {
                    animation-name: routeTransitionSlideLeft;
                }

                .turn-right .transition-image {
                    animation-name: routeTransitionSlideRight;
                }

                .transition-wash {
                    position: absolute;
                    inset: 0;
                    background:
                        radial-gradient(circle at 50% 50%, transparent 22%, rgba(9, 7, 5, 0.78)),
                        linear-gradient(90deg, rgba(0,0,0,0.5), transparent, rgba(0,0,0,0.5));
                    animation: routeTransitionWash 620ms ease-in-out forwards;
                }

                .route-modal-backdrop {
                    position: absolute;
                    inset: 0;
                    z-index: 80;
                    display: grid;
                    place-items: center;
                    padding: 24px;
                    background: rgba(8, 7, 5, 0.55);
                    backdrop-filter: blur(5px);
                }

                .route-modal {
                    position: relative;
                    width: min(760px, 100%);
                    max-height: min(82vh, 760px);
                    overflow: auto;
                    padding: 24px;
                    border-radius: 6px;
                    background:
                        linear-gradient(180deg, rgba(55, 48, 36, 0.94), rgba(22, 19, 15, 0.96)),
                        #1b1712;
                    border: 1px solid rgba(230, 205, 150, 0.24);
                    box-shadow: 0 28px 90px rgba(0, 0, 0, 0.62);
                }

                .route-modal-narrow {
                    width: min(560px, 100%);
                }

                .route-modal h2 {
                    margin: 0 38px 10px 0;
                    font-size: 30px;
                    font-weight: 600;
                }

                .route-modal p {
                    margin: 0 0 16px;
                    color: rgba(241, 234, 216, 0.78);
                    line-height: 1.5;
                }

                .route-modal-close {
                    position: absolute;
                    top: 14px;
                    right: 14px;
                    width: 32px;
                    height: 32px;
                    border-radius: 3px;
                    padding: 0;
                    font-size: 22px;
                    line-height: 1;
                }

                .modal-list {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 8px;
                    margin: 14px 0 18px;
                }

                .modal-list span {
                    padding: 6px 9px;
                    border-radius: 999px;
                    color: rgba(241, 234, 216, 0.72);
                    background: rgba(255,255,255,0.06);
                }

                .modal-actions {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 10px;
                    margin-top: 14px;
                }

                .modal-actions.muted {
                    padding-top: 12px;
                    border-top: 1px solid rgba(241, 234, 216, 0.12);
                }

                .modal-actions button {
                    min-height: 38px;
                    padding: 0 14px;
                    border-radius: 4px;
                }

                .external-preview {
                    display: block;
                    width: 100%;
                    aspect-ratio: 16 / 9;
                    object-fit: cover;
                    margin: 0 0 16px;
                    border-radius: 4px;
                    border: 1px solid rgba(241, 234, 216, 0.16);
                }

                .route-journal-modal {
                    width: min(920px, 100%);
                }

                .journal-image-wrap {
                    position: relative;
                    width: 100%;
                    aspect-ratio: 16 / 9;
                    overflow: hidden;
                    border-radius: 4px;
                    border: 1px solid rgba(241, 234, 216, 0.16);
                    background: #201b14;
                }

                .journal-image-wrap img {
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                    display: block;
                }

                .journal-note {
                    display: flex;
                    justify-content: space-between;
                    gap: 12px;
                    margin-top: 12px;
                    color: rgba(241, 234, 216, 0.68);
                    font-size: 13px;
                }

                @media (max-width: 720px) {
                    .route-topbar {
                        top: 12px;
                        left: 12px;
                        right: 12px;
                    }

                    .route-place {
                        font-size: 25px;
                    }

                    .route-sign-stack {
                        left: 12px;
                        top: 96px;
                    }

                    .route-controls {
                        grid-template-columns: repeat(2, minmax(116px, 1fr));
                        bottom: 30px;
                    }

                    .route-footer {
                        display: none;
                    }

                    .journal-note {
                        flex-direction: column;
                    }
                }
            </style>
        `;
    }
}
