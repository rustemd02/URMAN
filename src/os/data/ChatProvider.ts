import { CHAT_CONTACTS, CHAT_MESSAGES } from '../../data/chat_data';

export const getActiveChats = () => {
    return CHAT_CONTACTS;
};

export const getMessagesForChat = (chatId: string) => {
    const game = (window as any).URMAN;
    const state = game?.state;
    
    if (!state) return [];

    return CHAT_MESSAGES.filter((msg: any) => {
        if (msg.chatId !== chatId) return false;
        if (msg.unlocked) return true;
        
        // Проверяем флаг по ID сообщения
        return msg.id && state.flags && state.flags[msg.id];
    });
};