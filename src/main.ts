import './os/os.css';
import { Game } from './game/Game';

document.addEventListener('DOMContentLoaded', () => {
    console.log("URMAN: Запуск игры...");
    const game = new Game();
    game.start();
});
