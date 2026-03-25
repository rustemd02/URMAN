import { Game } from '../game/Game';
import { VocabularyItem } from '../game/GameState';

export class VocabularySystem {
    private game: Game;

    constructor(game: Game) {
        this.game = game;
    }

    /**
     * Добавить новое слово в словарь
     */
    public discoverWord(tatar: string, russian: string) {
        const id = tatar.toLowerCase();
        const existing = this.game.state.vocabulary.find(v => v.id === id);
        
        if (!existing) {
            const newItem: VocabularyItem = {
                id,
                tatar,
                russian,
                count: 0,
                learned: false
            };
            this.game.state.vocabulary.push(newItem);
            this.game.events.emit('word_discovered', newItem);
            
            // Если узнали новое слово, знание татарского растет
            this.updateTatarKnowledge();
            this.updateNotepadDictionary();
            
            // Показываем уведомление
            this.game.scenes.currentScene?.onOSMessage?.('Яңа сүз табылды!', `Сүз: ${tatar}\nТәрҗемә: ${russian}\n\nСүзлеккә өстәлде.`);
        }
    }

    /**
     * Попробовать "использовать" слово (посмотреть перевод)
     */
    public useWord(id: string) {
        const word = this.game.state.vocabulary.find(v => v.id === id);
        if (word && !word.learned) {
            word.count++;
            if (word.count >= 3) {
                word.learned = true;
                this.updateTatarKnowledge();
            }
            this.updateNotepadDictionary();
            return word;
        }
        return word;
    }

    private updateTatarKnowledge() {
        // Упрощенная формула: каждое новое слово дает 1%, выученное — еще 1%
        const totalWords = this.game.state.vocabulary.length;
        const learnedWords = this.game.state.vocabulary.filter(v => v.learned).length;
        
        const newKnowledge = Math.min(100, (totalWords * 0.5) + (learnedWords * 1.5));
        this.game.state.tatarKnowledge = Math.floor(newKnowledge);
    }

    private updateNotepadDictionary() {
        // Обновляем содержимое файла "Словарь.txt" в Notepad
        const dictionaryContent = this.game.state.vocabulary
            .map(v => `${v.tatar} — ${v.learned ? '✅ (Выучено)' : v.russian + ' (' + (3 - v.count) + ' подсказки осталось)'}`)
            .join('\n');
            
        // Это можно автоматизировать, если мы перехватываем Notepad state
        // Для простоты, мы будем "инжектить" это в localStorage
        try {
            const raw = localStorage.getItem('game.notepad.files.v1');
            if (raw) {
                const state = JSON.parse(raw);
                let dictFile = state.files.find((f: any) => f.name === 'СУЗЛЕК (Словарь).txt');
                if (!dictFile) {
                    dictFile = {
                        id: 'dictionary_file',
                        name: 'СУЗЛЕК (Словарь).txt',
                        content: '',
                        createdAt: Date.now(),
                        updatedAt: Date.now(),
                    };
                    state.files.unshift(dictFile);
                }
                dictFile.content = "ВАШ ПЕРСОНАЛЬНЫЙ СЛОВАРЬ (ТАТАРСКИЙ ЯЗЫК)\n" + "=".repeat(30) + "\n\n" + dictionaryContent;
                dictFile.updatedAt = Date.now();
                localStorage.setItem('game.notepad.files.v1', JSON.stringify(state));
            }
        } catch (e) {
            console.error("Failed to update notepad dictionary:", e);
        }
    }
}
