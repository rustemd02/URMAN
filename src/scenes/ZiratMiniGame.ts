import { BaseScene } from './BaseScene';
import { Game } from '../game/Game';

enum ZiratMode {
    LOCKED = 'locked',
    NEED_BRUSH = 'brush',
    TUTORIAL = 'tutorial',
    CLEANING = 'cleaning',
    DECODING = 'decoding',
    FINISHED = 'finished'
}

export class ZiratMiniGame extends BaseScene {
    private mode: ZiratMode = ZiratMode.CLEANING;
    private canvas: HTMLCanvasElement | null = null;
    private ctx: CanvasRenderingContext2D | null = null;
    private cleanPercent = 0;
    private errors = 0;
    private currentLetterIndex = 0;

    private targetText = "ӘЛСҮ БИРЕДӘ ЯТА 1926";
    private decodedText = "";

    private boundOnKeyDown: (e: KeyboardEvent) => void;
    private boundOnResize: () => void;

    constructor(game: Game) {
        super(game);

        const hasBrush = game.state.inventory.some(i => i.id === 'brush');
        const kledge = (game.state.tatarKnowledge || 0) >= 30;

        if (!hasBrush && !game.state.flags['debug_bypass']) {
            this.mode = ZiratMode.NEED_BRUSH;
        } else if (!kledge && !game.state.flags['debug_bypass']) {
            this.mode = ZiratMode.LOCKED;
        } else {
            this.mode = ZiratMode.TUTORIAL;
        }

        this.boundOnKeyDown = (e) => this.handleKeyDown(e);
        this.boundOnResize = () => this.handleResize();
    }

    public init(container: HTMLElement) {
        this.container = container;
        container.style.background = '#0a1008';
        this.canvas = document.createElement('canvas');
        this.ctx = this.canvas.getContext('2d');
        container.appendChild(this.canvas);

        this.handleResize();
        this.initInteraction();
        this.render();

        window.addEventListener('keydown', this.boundOnKeyDown);
        window.addEventListener('resize', this.boundOnResize);
    }

    private handleResize() {
        if (!this.canvas) return;
        this.canvas.width = window.innerWidth;
        this.canvas.height = window.innerHeight;
        this.render();
    }

    private initInteraction() {
        if (!this.canvas) return;

        this.canvas.addEventListener('mousedown', (e) => {
            const w = this.canvas!.width;
            if (e.clientX > w - 60 && e.clientY < 60) {
                this.game.scenes.switchScene('village');
                return;
            }

            if (this.mode === ZiratMode.NEED_BRUSH || this.mode === ZiratMode.LOCKED) {
                this.game.scenes.switchScene('village');
            } else if (this.mode === ZiratMode.TUTORIAL) {
                this.mode = ZiratMode.CLEANING;
                this.render();
            }
        });

        this.canvas.addEventListener('mousemove', (e) => {
            if (this.mode === ZiratMode.CLEANING && (e.buttons === 1)) {
                this.cleanPercent += 1.2; 
                if (this.cleanPercent >= 100) {
                    this.mode = ZiratMode.DECODING;
                }
                this.render();
            }
        });
    }

    private handleKeyDown(e: KeyboardEvent) {
        if (e.key === 'Escape') {
            this.game.scenes.switchScene('village');
            return;
        }

        if (this.mode === ZiratMode.DECODING) {
            const key = e.key.toUpperCase();
            const expected = this.targetText[this.currentLetterIndex];

            // Fuzzy matching for Tatar characters if user lacks layout
            const match = (k: string, target: string) => {
                if (k === target) return true;
                if (target === 'Ә' && (k === 'А' || k === 'A' || k === 'E')) return true;
                if (target === 'Ү' && (k === 'У' || k === 'U')) return true;
                if (target === 'Ө' && (k === 'О' || k === 'O')) return true;
                if (target === 'Җ' && (k === 'Ж' || k === 'J')) return true;
                if (target === 'Ң' && (k === 'Н' || k === 'N')) return true;
                if (target === 'Һ' && (k === 'Х' || k === 'H')) return true;
                if (target === ' ' && k === ' ') return true;
                if (target >= '0' && target <= '9' && k === target) return true;
                return false;
            };

            if (match(key, expected)) {
                this.decodedText += expected;
                this.currentLetterIndex++;
                if (this.currentLetterIndex >= this.targetText.length) {
                    this.mode = ZiratMode.FINISHED;
                    this.game.state.setFlag('alsu_secret_revealed', true);
                    this.game.state.modifyStat('tatarKnowledge', 15);
                }
            } else if (key.length === 1) {
                this.errors++;
                if (this.errors >= 8) {
                    this.mode = ZiratMode.LOCKED; // Soft reset
                    this.errors = 0;
                    this.decodedText = "";
                    this.currentLetterIndex = 0;
                }
            }
            this.render();
        }
    }

    private render() {
        const ctx = this.ctx;
        if (!ctx) return;
        const w = this.canvas!.width;
        const h = this.canvas!.height;

        ctx.clearRect(0, 0, w, h);

        // Backdrop
        const grad = ctx.createRadialGradient(w/2, h/2, 50, w/2, h/2, w/0.8);
        grad.addColorStop(0, '#151a12');
        grad.addColorStop(1, '#050804');
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, w, h);

        // Tombstone
        const sW = 400, sH = 580;
        const sX = (w - sW) / 2, sY = (h - sH) / 2 + 20;

        // Shadow
        ctx.fillStyle = 'rgba(0,0,0,0.5)';
        ctx.beginPath();
        // @ts-ignore
        ctx.roundRect(sX + 10, sY + 10, sW, sH, [180, 180, 10, 10]);
        ctx.fill();

        // Stone texture
        const stoneGrad = ctx.createLinearGradient(sX, sY, sX + sW, sY + sH);
        stoneGrad.addColorStop(0, '#3a3a3a');
        stoneGrad.addColorStop(0.5, '#2c2c2c');
        stoneGrad.addColorStop(1, '#1a1a1a');
        ctx.fillStyle = stoneGrad;
        ctx.beginPath();
        // @ts-ignore
        ctx.roundRect(sX, sY, sW, sH, [180, 180, 10, 10]);
        ctx.fill();
        
        ctx.strokeStyle = '#111'; ctx.lineWidth = 4; ctx.stroke();

        this.drawExitButton(ctx, w);

        if (this.mode === ZiratMode.NEED_BRUSH) {
            this.drawOverlay("Кирәк щетка. (Нужна щетка).", "Камень слишком грязный. Поищи что-нибудь в деревне.");
            return;
        }

        if (this.mode === ZiratMode.LOCKED) {
            this.drawOverlay("Белем җитми. (Не хватает знаний).", "Буквы плывут перед глазами. Нужно лучше знать язык.");
            return;
        }

        if (this.mode === ZiratMode.TUTORIAL) {
            this.drawTutorial(ctx, w, h);
            return;
        }

        ctx.save();
        ctx.textAlign = 'center';

        if (this.mode === ZiratMode.CLEANING) {
            // Moss layer
            ctx.fillStyle = '#1e2616';
            ctx.beginPath();
            // @ts-ignore
            ctx.roundRect(sX, sY, sW, sH, [180, 180, 10, 10]);
            ctx.fill();

            // Erase moss
            ctx.globalCompositeOperation = 'destination-out';
            ctx.filter = 'blur(20px)';
            ctx.beginPath();
            ctx.arc(w / 2, h / 2, (this.cleanPercent / 100) * 600, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();

            ctx.fillStyle = '#8a9a7a'; ctx.font = 'bold 18px "Philosopher", sans-serif';
            ctx.fillText("ЗАТЕРТО: Очисти камень движениями мыши", w / 2, h - 50);
        } else {
            // Engraving
            ctx.fillStyle = 'rgba(0,0,0,0.85)';
            ctx.font = '56px "Times New Roman"';
            ctx.shadowColor = 'rgba(255,255,255,0.05)'; ctx.shadowBlur = 1;

            ctx.fillText("ألسو بیره ده یاتا", w / 2, h / 2 - 10);
            ctx.fillText("١٩٢٦", w / 2, h / 2 + 70);
            ctx.restore();

            if (this.mode === ZiratMode.DECODING) {
                ctx.fillStyle = '#d4af37';
                ctx.font = '22px "Philosopher", sans-serif';
                ctx.fillText(`РАСШИФРОВКА: ${this.decodedText}|`, w / 2, h - 100);
                
                if (this.errors > 0) {
                    ctx.fillStyle = '#ff4444';
                    ctx.font = '14px Tahoma';
                    ctx.fillText(`УСТАЛОСТЬ: ${this.errors}/8`, w / 2, h - 130);
                }

                this.drawHint(ctx, w, h);
            }
        }

        if (this.mode === ZiratMode.FINISHED) {
            this.drawFinalScreen(ctx, w, h);
        }
    }

    private drawExitButton(ctx: CanvasRenderingContext2D, w: number) {
        ctx.fillStyle = 'rgba(255,0,0,0.1)';
        ctx.fillRect(w - 60, 10, 50, 50);
        ctx.strokeStyle = '#600';
        ctx.lineWidth = 1;
        ctx.strokeRect(w - 60, 10, 50, 50);
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 24px Arial';
        ctx.textAlign = 'center';
        ctx.fillText("×", w - 35, 42);
        ctx.font = '9px Arial';
        ctx.fillText("ESC", w - 35, 55);
    }

    private drawOverlay(title: string, sub: string) {
        const ctx = this.ctx!;
        ctx.fillStyle = 'rgba(0,0,0,0.88)';
        ctx.fillRect(0, 0, this.canvas!.width, this.canvas!.height);
        
        ctx.fillStyle = '#e6b34b';
        ctx.font = '24px "Philosopher", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(title, this.canvas!.width / 2, this.canvas!.height / 2 - 20);

        ctx.fillStyle = '#ccc';
        ctx.font = '16px Tahoma';
        ctx.fillText(sub, this.canvas!.width / 2, this.canvas!.height / 2 + 20);

        ctx.fillStyle = '#222';
        ctx.fillRect(this.canvas!.width / 2 - 100, this.canvas!.height / 2 + 80, 200, 45);
        ctx.strokeStyle = '#444'; ctx.strokeRect(this.canvas!.width / 2 - 100, this.canvas!.height / 2 + 80, 200, 45);
        ctx.fillStyle = '#fff';
        ctx.fillText("Уйти", this.canvas!.width / 2, this.canvas!.height / 2 + 108);
    }

    private drawTutorial(ctx: CanvasRenderingContext2D, w: number, h: number) {
        ctx.fillStyle = 'rgba(5,10,5,0.96)';
        ctx.fillRect(0, 0, w, h);
        ctx.fillStyle = '#e6b34b';
        ctx.font = '32px "Philosopher", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText("Иске Имлә — Шепот Камней", w / 2, h / 2 - 120);

        ctx.fillStyle = '#aaa';
        ctx.font = '18px serif';
        const rules = [
            "Камень зарос густым мхом.",
            "Очисть его, чтобы увидеть старое письмо.",
            "Затем впиши русскими буквами то, что прочитал.",
            "Древние тексты читаются справа налево.",
            "",
            "[ Жми в любое место ]"
        ];
        rules.forEach((r, i) => ctx.fillText(r, w / 2, h / 2 - 30 + i * 35));
    }

    private drawHint(ctx: CanvasRenderingContext2D, w: number, h: number) {
        ctx.fillStyle = 'rgba(255,255,255,0.03)';
        ctx.fillRect(20, h - 50, w - 40, 40);
        ctx.fillStyle = '#555';
        ctx.font = '13px "Philosopher", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText("أ=Ә | ل=Л | с=С | и=Ү | ب=Б | ی=И | р=Р | ه=Ә | д=Д | ی=Я | т=Т | а=А", w / 2, h - 25);
    }

    private drawFinalScreen(ctx: CanvasRenderingContext2D, w: number, h: number) {
        ctx.fillStyle = 'rgba(0,0,0,0.95)';
        ctx.fillRect(0, 0, w, h);
        
        ctx.fillStyle = '#fff';
        ctx.font = '24px "Philosopher", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText("Холод сковал твое сердце...", w / 2, h / 2 - 60);
        
        ctx.fillStyle = '#ff4444';
        ctx.font = '20px "Philosopher", sans-serif';
        ctx.fillText("Алсу Биредэ Ята. 1926.", w / 2, h / 2);
        
        ctx.fillStyle = '#888';
        ctx.font = '16px "Philosopher", sans-serif';
        ctx.fillText("Но кто же тогда присылает тебе сообщения сейчас?", w / 2, h / 2 + 50);
        
        ctx.fillStyle = '#e6b34b';
        ctx.fillText("[ ESC — БЕЖАТЬ В ДЕРЕВНЮ ]", w / 2, h / 2 + 120);
    }

    public destroy() {
        window.removeEventListener('keydown', this.boundOnKeyDown);
        window.removeEventListener('resize', this.boundOnResize);
        super.destroy();
    }
}