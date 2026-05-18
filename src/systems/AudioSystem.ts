import { Game } from '../game/Game';

export class AudioSystem {
    private sounds: Map<string, HTMLAudioElement> = new Map();
    private ambientWind: HTMLAudioElement | null = null;

    constructor(_game: Game) {
        this.init();
    }

    private init() {
        // Preload sounds
        this.loadSound('footsteps', '/assets/audio/footsteps.mp3');
        this.loadSound('wolf_howl', '/assets/audio/forest_howl.mp3');
        this.loadSound('door_creak', '/assets/audio/door_creak.mp3');
        this.loadSound('cattle', '/assets/audio/cattle.mp3');
        
        // Wind is special (ambient)
        this.ambientWind = new Audio('/assets/audio/wind.mp3');
        this.ambientWind.loop = true;
        this.ambientWind.volume = 0.3;
    }

    private loadSound(name: string, path: string) {
        const audio = new Audio(path);
        this.sounds.set(name, audio);
    }

    public play(name: string, volume: number = 1.0) {
        const sound = this.sounds.get(name);
        if (sound) {
            const clone = sound.cloneNode() as HTMLAudioElement;
            clone.volume = volume;
            clone.play().catch(e => console.log('Audio play failed:', e));
        }
    }

    public startWind() {
        if (this.ambientWind) {
            this.ambientWind.play().catch(e => console.log('Wind play failed:', e));
        }
    }

    public stopWind() {
        if (this.ambientWind) {
            this.ambientWind.pause();
        }
    }

    public setWindVolume(v: number) {
        if (this.ambientWind) {
            this.ambientWind.volume = Math.min(1.0, Math.max(0, v));
        }
    }
}
