import { Game } from '../game/Game';

export class NotebookUI {
    private game: Game;
    private element: HTMLElement;
    private isOpen = false;

    constructor(game: Game) {
        this.game = game;
        this.element = document.createElement('div');
        this.element.id = 'notebook-ui';
        this.element.style.cssText = `
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%) scale(0.8);
            width: 500px;
            height: 650px;
            background: #fdfdfd;
            box-shadow: 0 30px 60px rgba(0,0,0,0.5);
            border-radius: 8px;
            display: none;
            opacity: 0;
            transition: all 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
            z-index: 10005;
            padding: 40px;
            font-family: 'Kalam', cursive;
            background-image: 
                linear-gradient(rgba(100, 150, 255, 0.1) 1.5px, transparent 1.5px),
                linear-gradient(90deg, rgba(255, 100, 100, 0.4) 2px, transparent 2px);
            background-size: 100% 28px, 100% 100%;
            background-position: 0 35px, 60px 0;
            color: #1a1a4a;
            user-select: none;
            overflow-y: auto;
        `;
        
        // Add font
        if (!document.getElementById('notebook-font')) {
            const link = document.createElement('link');
            link.id = 'notebook-font';
            link.rel = 'stylesheet';
            link.href = 'https://fonts.googleapis.com/css2?family=Kalam:wght@300;400;700&display=swap';
            document.head.appendChild(link);
        }

        document.body.appendChild(this.element);
        
        this.initEvents();
    }

    private initEvents() {
        this.element.addEventListener('click', (e) => {
            if (e.target === this.element) return;
            // Clicking anywhere inside might eventually flip pages
        });

        // ESC to close
        window.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.isOpen) this.close();
        });
    }

    public open() {
        this.isOpen = true;
        this.element.style.display = 'block';
        this.render();
        setTimeout(() => {
            this.element.style.opacity = '1';
            this.element.style.transform = 'translate(-50%, -50%) scale(1)';
        }, 10);
    }

    public close() {
        this.isOpen = false;
        this.element.style.opacity = '0';
        this.element.style.transform = 'translate(-50%, -50%) scale(0.8)';
        setTimeout(() => {
            this.element.style.display = 'none';
        }, 300);
    }

    private render() {
        const words = this.game.state.vocabulary;
        const total = this.game.state.tatarKnowledge;

        let html = `
            <div style="position: absolute; top: -10px; right: 10px; font-size: 24px; cursor: pointer; color: #a00;" id="close-notebook">×</div>
            <h1 style="margin-top: 5px; font-size: 28px; border-bottom: 2px solid #555; display: inline-block;">Мой блокнот (Татарский)</h1>
            <div style="margin: 20px 0 10px 40px; font-size: 16px; opacity: 0.7;">
                Знание языка: ${total}%
            </div>
            <div id="notebook-words" style="margin-top: 35px; line-height: 28px; margin-left: 30px; font-size: 19px;">
        `;

        if (words.length === 0) {
            html += `<p style="opacity: 0.5;">Пока ничего не записал... Надо поспрашивать у бабая.</p>`;
        } else {
            words.forEach(w => {
                html += `
                    <div style="margin-bottom: 5px; animation: write 0.5s ease-out forwards;">
                        <span style="font-weight: 700;">${w.tatar}</span> 
                        <span style="opacity: 0.4; font-size: 14px;"> — </span>
                        <span>${w.learned ? w.russian : '?? (надо еще раз услышать)'}</span>
                        ${w.learned ? '<span style="color: #2a8a2a; margin-left: 10px;">✓</span>' : ''}
                    </div>
                `;
            });
        }

        html += `</div>`;
        
        // Add scribble/doodles
        html += `
            <div style="position: absolute; bottom: 20px; right: 20px; font-weight: 300; opacity: 0.4; transform: rotate(-5deg); font-size: 14px;">
                Айдар, ИТИС, 2026
            </div>
        `;

        this.element.innerHTML = html;
        this.element.querySelector('#close-notebook')?.addEventListener('click', () => this.close());
    }
}
