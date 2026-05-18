import { BaseScene } from './BaseScene';

export class HouseScene extends BaseScene {
    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
            <div style="
                min-height:100vh;
                box-sizing:border-box;
                padding:32px;
                background:#10100d;
                color:#f2ead8;
                font-family:Tahoma, sans-serif;
            ">
                <h1 style="margin:0 0 12px; font-size:28px;">Дом Мансура и Гөлсинә</h1>
                <p style="max-width:560px; line-height:1.55;">
                    На столе у стены гудит старый компьютер. Бабай сказал, что им можно пользоваться, если нужно найти старые бумаги.
                </p>
                <button id="open-pc-btn" style="
                    margin-top:18px;
                    padding:10px 16px;
                    border:1px solid #f2ead8;
                    background:#d4d0c8;
                    color:#111;
                    cursor:pointer;
                    font-size:14px;
                ">Сесть за компьютер</button>
            </div>
        `;

        container.querySelector('#open-pc-btn')?.addEventListener('click', () => {
            this.game.scenes.switchScene('computer');
        });
    }
}
