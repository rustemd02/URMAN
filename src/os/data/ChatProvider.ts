import masterDb from './chats/master_db.json';
// Импортируй GameState правильно. Если это синглтон, оставь getInstance. 
// Если нет — напиши просто new GameState() или как ты его обычно вызываешь.
import { GameState } from '../../game/GameState';

export const getActiveChats = () => {
    // Используем каст к any, чтобы TS не ругался на структуру JSON
    return (masterDb as any).contacts;
};

export const getMessagesForChat = (chatId: string) => {
    // ВНИМАНИЕ: Если ошибка на getInstance останется, 
    // попробуй заменить на: const state = new GameState(); 
    // или посмотри как ты вызываешь его в других файлах.
    const state = (GameState as any).getInstance ? (GameState as any).getInstance() : new (GameState as any)();
    
    const allMessages = (masterDb as any).messages;
    
    return allMessages.filter((msg: any) => {
        if (msg.chatId !== chatId) return false;
        if (msg.unlocked) return true;
        
        // Проверяем флаг
        return msg.id && state.hasFlag && state.hasFlag(msg.id);
    });
};