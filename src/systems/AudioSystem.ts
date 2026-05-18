import { Game } from '../game/Game';

type MvpAudioId =
    | 'home_ambience'
    | 'street_silence'
    | 'old_pc_hum'
    | 'mosque_calm'
    | 'zirat_wind'
    | 'forest_presence'
    | 'marat_voice'
    | 'rinat_ne_otvechai';

type GeneratedLayerKind = 'silence_air' | 'pc_hum' | 'mosque_calm' | 'forest_presence' | 'voice_marat' | 'voice_rinat';

type FileLayer = Readonly<{
    type: 'file';
    path: string;
    volume: number;
    loop?: boolean;
}>;

type GeneratedLayer = Readonly<{
    type: 'generated';
    kind: GeneratedLayerKind;
    volume: number;
    loop?: boolean;
}>;

type AudioLayer = FileLayer | GeneratedLayer;

type CueDefinition = Readonly<{
    id: MvpAudioId;
    mode: 'cue' | 'ambience';
    layers: AudioLayer[];
    notes: string;
}>;

type ActiveGeneratedLayer = Readonly<{
    stop: () => void;
}>;

const AUDIO_FILES = {
    doorCreak: '/assets/audio/door_creak.mp3',
    footsteps: '/assets/audio/footsteps.mp3',
    wind: '/assets/audio/wind.mp3',
};

const CUE_DEFINITIONS: Record<MvpAudioId, CueDefinition> = {
    home_ambience: {
        id: 'home_ambience',
        mode: 'ambience',
        layers: [
            { type: 'generated', kind: 'silence_air', volume: 0.035, loop: true },
        ],
        notes: 'Generated room air until an authored home ambience exists; no livestock bed in the MVP path.',
    },
    street_silence: {
        id: 'street_silence',
        mode: 'ambience',
        layers: [
            { type: 'generated', kind: 'silence_air', volume: 0.045, loop: true },
        ],
        notes: 'Intentionally sparse village air; no fake crowd or wildlife bed.',
    },
    old_pc_hum: {
        id: 'old_pc_hum',
        mode: 'ambience',
        layers: [
            { type: 'generated', kind: 'pc_hum', volume: 0.08, loop: true },
        ],
        notes: 'Generated CRT/electrical hum until an authored old PC loop exists.',
    },
    mosque_calm: {
        id: 'mosque_calm',
        mode: 'ambience',
        layers: [
            { type: 'generated', kind: 'mosque_calm', volume: 0.055, loop: true },
        ],
        notes: 'Quiet respectful calm; no caricatured chant or stock religious marker.',
    },
    zirat_wind: {
        id: 'zirat_wind',
        mode: 'ambience',
        layers: [
            { type: 'file', path: AUDIO_FILES.wind, volume: 0.22, loop: true },
            { type: 'generated', kind: 'silence_air', volume: 0.025, loop: true },
        ],
        notes: 'Uses existing wind honestly for cemetery wind.',
    },
    forest_presence: {
        id: 'forest_presence',
        mode: 'ambience',
        layers: [
            { type: 'file', path: AUDIO_FILES.wind, volume: 0.16, loop: true },
            { type: 'generated', kind: 'forest_presence', volume: 0.09, loop: true },
        ],
        notes: 'Kara-Urman presence without generic wolf howl identity.',
    },
    marat_voice: {
        id: 'marat_voice',
        mode: 'cue',
        layers: [
            { type: 'generated', kind: 'voice_marat', volume: 0.13 },
        ],
        notes: 'Placeholder distant human-like tone; not a voiced line.',
    },
    rinat_ne_otvechai: {
        id: 'rinat_ne_otvechai',
        mode: 'cue',
        layers: [
            { type: 'generated', kind: 'voice_rinat', volume: 0.16 },
        ],
        notes: 'Placeholder warning hit for "Не отвечай"; needs authored voice.',
    },
};

const AMBIENCE_ALIASES: Record<string, MvpAudioId> = {
    admin_day: 'street_silence',
    admin_route_day: 'street_silence',
    alsu_spot_day: 'street_silence',
    arrival_vehicle_dusk: 'street_silence',
    crossroad_day: 'street_silence',
    crossroad_pressure: 'street_silence',
    forest_retreat_evening: 'forest_presence',
    forest_silence_entering: 'forest_presence',
    forest_silence_pressure: 'forest_presence',
    forest_presence_low: 'forest_presence',
    lane_day: 'street_silence',
    lane_day_back_view: 'street_silence',
    lane_pressure: 'street_silence',
    marat_voice_far: 'forest_presence',
    marat_voice_near: 'forest_presence',
    mosque_route_back_view: 'mosque_calm',
    mosque_route_day: 'mosque_calm',
    mosque_route_evening: 'mosque_calm',
    river_day: 'street_silence',
    river_route_day: 'street_silence',
    rinat_ne_otvechai: 'forest_presence',
    street_day: 'street_silence',
    street_evening: 'street_silence',
    street_pressure_silence: 'street_silence',
    unsafe_route_evening: 'forest_presence',
    yard_day: 'home_ambience',
    yard_day_back_view: 'home_ambience',
    yard_evening: 'home_ambience',
    zirat_day: 'zirat_wind',
    zirat_pressure: 'zirat_wind',
    zirat_wind_low: 'zirat_wind',
};

const ONE_SHOT_ALIASES: Record<string, MvpAudioId> = {
    marat_voice_far: 'marat_voice',
    marat_voice_near: 'marat_voice',
    rinat_ne_otvechai: 'rinat_ne_otvechai',
};

export class AudioSystem {
    private sounds: Map<string, HTMLAudioElement> = new Map();
    private activeFileLayers: HTMLAudioElement[] = [];
    private activeGeneratedLayers: ActiveGeneratedLayer[] = [];
    private playedOneShotAliases = new Set<string>();
    private audioContext: AudioContext | null = null;
    private masterGain: GainNode | null = null;
    private unlocked = false;
    private currentAmbienceId: MvpAudioId | null = null;
    private lastAmbienceRequest: { id: string; volume: number } | null = null;

    constructor(_game: Game) {
        this.init();
    }

    private init(): void {
        this.loadSound('footsteps', AUDIO_FILES.footsteps);
        this.loadSound('door_creak', AUDIO_FILES.doorCreak);
        this.loadSound('wind', AUDIO_FILES.wind);

        window.addEventListener('pointerdown', this.unlockAudio, { once: true, passive: true });
        window.addEventListener('keydown', this.unlockAudio, { once: true });
    }

    private readonly unlockAudio = (): void => {
        this.unlocked = true;
        void this.ensureAudioContext()?.resume()
            .then(() => {
                const request = this.lastAmbienceRequest;
                if (!request) return;
                this.stopAmbience();
                this.startAmbience(request.id, request.volume);
            })
            .catch((error) => {
                console.info('AudioContext resume failed:', error);
            });
    };

    private loadSound(name: string, path: string): void {
        const audio = new Audio(path);
        audio.preload = 'auto';
        this.sounds.set(name, audio);
    }

    public play(name: string, volume: number = 1.0): void {
        if (name === 'wolf_howl') {
            this.playCue('forest_presence', volume * 0.5);
            return;
        }
        if (name === 'cattle') {
            this.playCue('home_ambience', volume * 0.5);
            return;
        }

        const sound = this.sounds.get(name);
        if (!sound) {
            this.playCue(name, volume);
            return;
        }

        const clone = sound.cloneNode() as HTMLAudioElement;
        clone.volume = this.clampVolume(volume);
        void clone.play().catch((error) => {
            console.info(`Audio play failed for "${name}":`, error);
        });
    }

    public playCue(id: string, volume: number = 1.0): void {
        const resolvedId = this.resolveCueId(id);
        const definition = CUE_DEFINITIONS[resolvedId];
        if (definition.mode === 'ambience') {
            this.startAmbience(resolvedId, volume);
            return;
        }
        for (const layer of definition.layers) {
            if (layer.type === 'file') {
                this.playFileCue(layer, volume);
            } else {
                this.startGeneratedLayer(layer, volume);
            }
        }
    }

    public startAmbience(id: string, volume: number = 1.0): void {
        this.lastAmbienceRequest = { id, volume };
        const ambienceId = this.resolveAmbienceId(id);
        if (this.currentAmbienceId === ambienceId) {
            this.playOneShotForAlias(id);
            return;
        }

        this.stopAmbience();
        this.currentAmbienceId = ambienceId;

        const definition = CUE_DEFINITIONS[ambienceId];
        for (const layer of definition.layers) {
            if (layer.type === 'file') {
                this.startFileAmbience(layer, volume);
            } else {
                const generatedLayer = this.startGeneratedLayer(layer, volume);
                if (generatedLayer) this.activeGeneratedLayers.push(generatedLayer);
            }
        }
        this.playOneShotForAlias(id);
    }

    public stopAmbience(id?: string): void {
        if (id && this.resolveAmbienceId(id) !== this.currentAmbienceId) return;
        const stoppedAmbienceId = this.currentAmbienceId;

        for (const audio of this.activeFileLayers) {
            audio.pause();
            audio.currentTime = 0;
        }
        for (const layer of this.activeGeneratedLayers) {
            layer.stop();
        }

        this.activeFileLayers = [];
        this.activeGeneratedLayers = [];
        this.currentAmbienceId = null;
        if (!id || (this.lastAmbienceRequest && this.resolveAmbienceId(this.lastAmbienceRequest.id) === stoppedAmbienceId)) {
            this.lastAmbienceRequest = null;
        }
    }

    public startWind(): void {
        this.startAmbience('zirat_wind');
    }

    public stopWind(): void {
        this.stopAmbience('zirat_wind');
    }

    public setWindVolume(v: number): void {
        for (const audio of this.activeFileLayers) {
            if (audio.src.includes('/assets/audio/wind.mp3')) {
                audio.volume = this.clampVolume(v);
            }
        }
    }

    public getCueFallbacks(): Record<MvpAudioId, string> {
        return Object.fromEntries(
            Object.entries(CUE_DEFINITIONS).map(([id, definition]) => [id, definition.notes]),
        ) as Record<MvpAudioId, string>;
    }

    private playFileCue(layer: FileLayer, volumeMultiplier: number): void {
        const audio = new Audio(layer.path);
        audio.volume = this.clampVolume(layer.volume * volumeMultiplier);
        audio.loop = false;
        void audio.play().catch((error) => {
            console.info(`Audio cue failed for "${layer.path}":`, error);
        });
    }

    private startFileAmbience(layer: FileLayer, volumeMultiplier: number): void {
        if (!this.unlocked) return;
        const audio = new Audio(layer.path);
        audio.loop = Boolean(layer.loop);
        audio.volume = this.clampVolume(layer.volume * volumeMultiplier);
        this.activeFileLayers.push(audio);
        void audio.play().catch((error) => {
            console.info(`Ambience failed for "${layer.path}":`, error);
        });
    }

    private startGeneratedLayer(layer: GeneratedLayer, volumeMultiplier: number): ActiveGeneratedLayer | null {
        const context = this.ensureAudioContext();
        if (!context || !this.masterGain) return null;
        if (!this.unlocked && context.state === 'suspended') return null;

        switch (layer.kind) {
            case 'silence_air':
                return this.createNoiseLayer(context, layer.volume * volumeMultiplier, 900);
            case 'pc_hum':
                return this.createHumLayer(context, layer.volume * volumeMultiplier, [50, 100, 157]);
            case 'mosque_calm':
                return this.createHumLayer(context, layer.volume * volumeMultiplier, [174, 261]);
            case 'forest_presence':
                return this.createForestPresenceLayer(context, layer.volume * volumeMultiplier);
            case 'voice_marat':
                return this.createVoiceCue(context, layer.volume * volumeMultiplier, 340, 1.8);
            case 'voice_rinat':
                return this.createVoiceCue(context, layer.volume * volumeMultiplier, 145, 0.85);
            default:
                return null;
        }
    }

    private createNoiseLayer(context: AudioContext, volume: number, filterFrequency: number, durationSeconds?: number): ActiveGeneratedLayer {
        const source = context.createBufferSource();
        const buffer = context.createBuffer(1, context.sampleRate * 2, context.sampleRate);
        const data = buffer.getChannelData(0);
        for (let i = 0; i < data.length; i += 1) {
            data[i] = (Math.random() * 2 - 1) * 0.12;
        }
        const filter = context.createBiquadFilter();
        filter.type = 'lowpass';
        filter.frequency.value = filterFrequency;
        const gain = context.createGain();
        gain.gain.value = this.clampVolume(volume);
        source.buffer = buffer;
        source.loop = !durationSeconds;
        source.connect(filter);
        filter.connect(gain);
        gain.connect(this.masterGain!);
        source.start();
        if (durationSeconds) source.stop(context.currentTime + durationSeconds);
        return { stop: () => source.stop() };
    }

    private createHumLayer(context: AudioContext, volume: number, frequencies: number[]): ActiveGeneratedLayer {
        const gain = context.createGain();
        gain.gain.value = this.clampVolume(volume);
        gain.connect(this.masterGain!);
        const oscillators = frequencies.map((frequency, index) => {
            const oscillator = context.createOscillator();
            oscillator.type = index === 0 ? 'sine' : 'triangle';
            oscillator.frequency.value = frequency;
            const localGain = context.createGain();
            localGain.gain.value = 1 / (index + 1);
            oscillator.connect(localGain);
            localGain.connect(gain);
            oscillator.start();
            return oscillator;
        });
        return {
            stop: () => {
                for (const oscillator of oscillators) {
                    oscillator.stop();
                }
                gain.disconnect();
            },
        };
    }

    private createForestPresenceLayer(context: AudioContext, volume: number): ActiveGeneratedLayer {
        const noise = this.createNoiseLayer(context, volume * 0.55, 420);
        const hum = this.createHumLayer(context, volume * 0.45, [41, 82]);
        return {
            stop: () => {
                noise.stop();
                hum.stop();
            },
        };
    }

    private createVoiceCue(context: AudioContext, volume: number, baseFrequency: number, durationSeconds: number): ActiveGeneratedLayer {
        const gain = context.createGain();
        gain.gain.setValueAtTime(0.0001, context.currentTime);
        gain.gain.exponentialRampToValueAtTime(Math.max(0.0001, this.clampVolume(volume)), context.currentTime + 0.08);
        gain.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + durationSeconds);
        gain.connect(this.masterGain!);

        const oscillator = context.createOscillator();
        oscillator.type = 'sawtooth';
        oscillator.frequency.setValueAtTime(baseFrequency, context.currentTime);
        oscillator.frequency.exponentialRampToValueAtTime(baseFrequency * 0.74, context.currentTime + durationSeconds);

        const filter = context.createBiquadFilter();
        filter.type = 'bandpass';
        filter.frequency.value = baseFrequency * 2.1;
        filter.Q.value = 0.9;

        oscillator.connect(filter);
        filter.connect(gain);
        oscillator.start();
        oscillator.stop(context.currentTime + durationSeconds);

        return {
            stop: () => {
                oscillator.stop();
                gain.disconnect();
            },
        };
    }

    private ensureAudioContext(): AudioContext | null {
        if (this.audioContext) return this.audioContext;

        const AudioContextConstructor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
        if (!AudioContextConstructor) return null;

        this.audioContext = new AudioContextConstructor();
        this.masterGain = this.audioContext.createGain();
        this.masterGain.gain.value = 0.8;
        this.masterGain.connect(this.audioContext.destination);
        return this.audioContext;
    }

    private resolveCueId(id: string): MvpAudioId {
        if (id in CUE_DEFINITIONS) return id as MvpAudioId;
        return ONE_SHOT_ALIASES[id] ?? AMBIENCE_ALIASES[id] ?? 'street_silence';
    }

    private resolveAmbienceId(id: string): MvpAudioId {
        const resolved = AMBIENCE_ALIASES[id] ?? id;
        if (resolved in CUE_DEFINITIONS && CUE_DEFINITIONS[resolved as MvpAudioId].mode === 'ambience') {
            return resolved as MvpAudioId;
        }
        return 'street_silence';
    }

    private playOneShotForAlias(id: string): void {
        const cueId = ONE_SHOT_ALIASES[id];
        if (!cueId || this.playedOneShotAliases.has(id)) return;
        if (!this.unlocked) return;
        this.playedOneShotAliases.add(id);
        this.playCue(cueId);
    }

    private clampVolume(value: number): number {
        return Math.min(1, Math.max(0, value));
    }
}
