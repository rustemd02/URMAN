import './style.css';
import { DedOS } from './core/OS';

document.addEventListener('DOMContentLoaded', () => {
    console.log("URMAN: Инициализация системы Бабая...");
    new DedOS();
});
